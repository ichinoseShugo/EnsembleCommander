using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using EnsembleCommander.Bluetooth;

namespace EnsembleCommander
{
    public class BluetoothServer
    {
        /// <summary> 接続された順番 </summary>
        public int deviceOrder;
        /// <summary> BluetoothDeviceID (12桁の英数字) </summary>
        public RfcommDeviceDisplay deviceInfo;
        public string deviceName = "";
        public string deviceId = "";
        private BluetoothWindow mBWindow;

        private StreamSocket socket;
        private RfcommServiceProvider rfcommProvider;
        private StreamSocketListener socketListener;

        private MainWindow main;

        public BluetoothServer(int dOrder, RfcommDeviceDisplay dinfo, BluetoothWindow b, MainWindow m)
        {
            deviceOrder = dOrder;
            deviceInfo = dinfo;
            deviceName = dinfo.Name;
            deviceId = dinfo.Id.Split('-')[1].Replace(":", "");
            mBWindow = b;
            main = m;
        }

        #region イベントハンドラ
        /// <summary> Clientとの接続が確立したとき呼び出されるイベント </summary>
        public void OnConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            //接続が確立した後はソケットリスナーは必要ないため閉じる
            socketListener.Dispose();
            socketListener = null;

            try
            {
                socket = args.Socket;
                //接続が確立したことをMainプログラムに通知
                mBWindow.Player_Connect(deviceInfo);
            }
            catch
            {
                Disconnect();
                Console.WriteLine("【connect】Player" + (deviceId + 1) + "との通信が切断されました");
                return;
            }
        }
        #endregion

        #region メソッド
        /// <summary> Clientとの接続のためにソケットを生成し接続待機状態へ移行する </summary>
        public async void Listen()
        {
            try
            {
                //uuidの下12桁はClient側のbluetooth idとする
                string uuid = "17fcf242-f86d-4e35-805e-" + deviceId;
                //サービスUUIDを生成 (ClientとUUIDが一致していればOK)
                Guid RfcommChatServiceUuid = Guid.Parse(uuid);
                rfcommProvider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.FromUuid(RfcommChatServiceUuid));
            }
            // Catch exception HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE).
            catch (Exception ex) when ((uint)ex.HResult == 0x800710DF)
            {
                // The Bluetooth radio may be off.
                return;
            }

            //OnConnectionReceivedをClientからの接続が確立したイベントとして登録
            socketListener = new StreamSocketListener();
            socketListener.ConnectionReceived += OnConnectionReceived;

            //SDP属性の付与とBluetooth advertisingの開始
            //advertising: 「自分はこんなサービスを提供していますよ」と言う情報を周囲に発信します。
            //この情報を載せたパケットをアドバタイジングパケットと言います。サービスの識別にはUUIDを利用します。
            string rfcomm = rfcommProvider.ServiceId.AsString();
            await socketListener.BindServiceNameAsync(rfcommProvider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);
            InitializeServiceSdpAttributes(rfcommProvider);

            try
            {
                //接続待機
                rfcommProvider.StartAdvertising(socketListener, true);
                Console.WriteLine("接続待機中");
            }
            catch
            {
                // RfcommServiceProviderへの参照を取得できない場合は、その理由をユーザーに伝えます。
                //通常、ユーザが自分のプライバシー設定を変更してデバイスとの同期を防止すると例外がスローされます。
                return;
            }
        }

        /// <summary> 周囲に告知するサービスの内容を設定 </summary>
        private void InitializeServiceSdpAttributes(RfcommServiceProvider rfcommProvider)
        {
            var sdpWriter = new DataWriter();
            const UInt16 SdpServiceNameAttributeId = 0x100;
            const byte SdpServiceNameAttributeType = (4 << 3) | 5;
            const string SdpServiceName = "BluetoothMusicConnect";
            // Write the Service Name Attribute.
            sdpWriter.WriteByte(SdpServiceNameAttributeType);
            // The length of the UTF-8 encoded Service Name SDP Attribute.
            sdpWriter.WriteByte((byte)SdpServiceName.Length);
            // The UTF-8 encoded Service Name value.
            sdpWriter.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            sdpWriter.WriteString(SdpServiceName);
            // Set the SDP Attribute on the RFCOMM Service Provider.
            rfcommProvider.SdpRawAttributes.Add(SdpServiceNameAttributeId, sdpWriter.DetachBuffer());
        }

        /// <summary> 送信メッセージをOutputStreamに格納する </summary>
        public async void Send()
        {
            // There's no need to send a zero length message
            // Make sure that the connection is still up and there is a message to send
            if (socket != null)
            {
                //7文字(7バイト)のデータ
                string data = "ABCDEFG";
                //バイトデータの文字コードを変更(androidを想定してUTF8に変更しているが変更の必要があるかどうかは未実験、必要ないかも)
                byte[] bytes = Encoding.UTF8.GetBytes(data);

                //OutputStreamに文字列を送信
                await socket.OutputStream.WriteAsync(bytes.AsBuffer());
            }
        }

        /// <summary> InputStreamに格納されている受信メッセージを受け取る </summary>
        public async void Receive()
        {
            try
            {
                if (socket != null)
                {
                    byte[] buffer = new byte[120];
                    //InputStreamのデータを変数bufferに格納
                    await socket.InputStream.ReadAsync(buffer.AsBuffer(), 120, InputStreamOptions.Partial);
                    //受信したbyteデータを文字列に変換
                    string message = Encoding.GetEncoding("ASCII").GetString(buffer);
                    Console.WriteLine("receive message:" + message);
                }
            }
            catch
            {
                lock (this)
                {
                    if (socket == null)
                    {
                        // Do not print anything here -  the user closed the sock
                    }
                    else
                    {
                        Disconnect();
                        Console.WriteLine("Player" + (deviceId + 1) + "との通信が切断されました");
                    }
                }
            }
        }

        /// <summary> Midiの開始 </summary>
        public async void StartMidi(string target)
        {
            try
            {
                if (socket != null)
                {
                    //文字のデータ
                    byte[] bytes = Encoding.ASCII.GetBytes(target + ":");
                    //OutputStreamに文字列を送信
                    await socket.OutputStream.WriteAsync(bytes.AsBuffer());
                }
            }
            catch
            {
                lock (this)
                {
                    if (socket == null)
                    {
                        // Do not print anything here -  the user closed the sock
                    }
                    else
                    {
                        Disconnect();
                        Console.WriteLine("【ping】Player" + (deviceId + 1) + "との通信が切断されました");
                    }
                }
            }
        }

        /// <summary> Clientとの接続を切断 </summary>
        public void Disconnect()
        {
            if (rfcommProvider != null)
            {
                rfcommProvider.StopAdvertising();
                rfcommProvider = null;
            }

            if (socketListener != null)
            {
                socketListener.Dispose();
                socketListener = null;
            }

            if (socket != null)
            {
                if (socket.InputStream != null)
                {
                    socket.InputStream.Dispose();
                }

                if (socket.OutputStream != null)
                {
                    socket.OutputStream.Dispose();
                }
                socket.Dispose();
                socket = null;
            }
        }
        #endregion
    }
}
