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

namespace EnsembleCommander
{
    class BluetoothManager
    {
        public int deviceId = 0;
        private StreamSocket socket;
        private RfcommServiceProvider rfcommProvider;
        private StreamSocketListener socketListener;
        public List<long> delayTimeList = new List<long>();
        System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();

        BluetoothWindow bWindow;

        public BluetoothManager(BluetoothWindow window)
        {
            bWindow = window;
        }

        //イベントハンドラ-----------------------------------------------------------------------------------------------------

        //Clientとの接続が確立した際に呼び出されるイベントハンドラ
        public void OnConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            //接続が確立した後はソケットリスナーは必要ないため閉じる
            socketListener.Dispose();
            socketListener = null;
            try
            {
                socket = args.Socket;
                //接続が確立したことをMainプログラムに通知
                bWindow.Player_Connect(deviceId);
            }
            catch
            {
                Disconnect();
                Console.WriteLine("【connect】Player" + (deviceId + 1) + "との通信が切断されました");
                return;
            }
        }

        //メソッド-----------------------------------------------------------------------------------------------------

        //BluetoothClientからの接続を受け付けるソケットを生成する命令
        public async void Listen(int deviceorder)
        {
            try
            {
                //サービスUUIDを生成 (ClientとUUIDが一致していればOK)
                Guid RfcommChatServiceUuid = Guid.Parse("17fcf242-f86d-4e35-805e-546ee3040b84");
                rfcommProvider = await RfcommServiceProvider.CreateAsync(RfcommServiceId.FromUuid(RfcommChatServiceUuid));
            }
            // Catch exception HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE).
            catch (Exception ex) when ((uint)ex.HResult == 0x800710DF)
            {
                // The Bluetooth radio may be off.
                return;
            }
            //Windows.Devices.Bluetooth.BluetoothDevice
            Console.WriteLine(deviceorder);
            Console.WriteLine();
            //Console.WriteLine("接続待機中");
            //接続されるデバイスの順番を表すID
            deviceId = deviceorder;

            //OnConnectionReceivedをClientからの接続が確立したイベントとして登録
            socketListener = new StreamSocketListener();
            socketListener.ConnectionReceived += OnConnectionReceived;

            var rfcomm = rfcommProvider.ServiceId.AsString();
            await socketListener.BindServiceNameAsync(rfcommProvider.ServiceId.AsString(),
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);

            //Set the SDP attributes and start Bluetooth advertising
            //advertising: 自分はこんなサービスを提供していますよ」と言う情報を周囲に発信します
            //この情報を載せたパケットをアドバタイジングパケットと言います。サービスの識別にはUUIDを利用します。
            InitializeServiceSdpAttributes(rfcommProvider);

            try
            {
                rfcommProvider.StartAdvertising(socketListener, true);
            }
            catch
            {
                // RfcommServiceProviderへの参照を取得できない場合は、その理由をユーザーに伝えます。
                //通常、ユーザが自分のプライバシー設定を変更してデバイスとの同期を防止すると例外がスローされます。
                return;
            }
            return;
        }

        //周囲にサービスの存在を告知する命令 (コメントアウトしたら接続できなくなったんで一応必要な模様)
        public void InitializeServiceSdpAttributes(RfcommServiceProvider rfcommProvider)
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

        //接続したClientとの送受信遅延時間を計測する命令
        public async void Ping(int times)
        {
            try
            {
                if (socket != null)
                {
                    //System.Diagnostics.Stopwatch StopWatch2 = new System.Diagnostics.Stopwatch();
                    //7文字(7バイト)のデータ
                    string data = "ABCDEFG";
                    //バイトデータの文字コードを変更(androidを想定してUTF8に変更しているが変更の必要があるかどうかは未実験、必要ないかも)
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(data);
                    //StopWatch2.Reset();
                    //StopWatch2.Start();
                    //OutputStreamに文字列を送信
                    await socket.OutputStream.WriteAsync(bytes.AsBuffer());
                    var ns = socket.InputStream;
                    byte[] buffer = new byte[120];
                    //InputStreamのデータを変数bufferに格納
                    await socket.InputStream.ReadAsync(buffer.AsBuffer(), 120, InputStreamOptions.Partial);
                    //StopWatch2.Stop();
                    //delayTimeList.Add(StopWatch2.ElapsedMilliseconds);
                    //受信したbyteデータを文字列に変換
                    string str = Encoding.GetEncoding("ASCII").GetString(buffer);
                    times++;
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
            if (times < 50)
            {
                try
                {
                    //System.Threading.Thread.Sleep();
                    Ping(times);
                }
                catch
                {
                }
            }
            else
            {
                times = 0;
                Console.WriteLine("ping実験終了");
            }
        }

        string now;
        public async void SyncStart()
        {
            try
            {
                if (socket != null)
                {
                    //7文字(7バイト)のデータ
                    string data = "ABCDEFG";
                    //バイトデータの文字コードを変更(androidを想定してUTF8に変更しているが変更の必要があるかどうかは未実験、必要ないかも)
                    byte[] bytes = Encoding.UTF8.GetBytes(data);
                    stopWatch.Reset();
                    stopWatch.Start();
                    //OutputStreamに文字列を送信
                    await socket.OutputStream.WriteAsync(bytes.AsBuffer());
                    var ns = socket.InputStream;
                    byte[] buffer = new byte[120];
                    //InputStreamのデータを変数bufferに格納
                    await socket.InputStream.ReadAsync(buffer.AsBuffer(), 120, InputStreamOptions.Partial);
                    stopWatch.Stop();
                    //受信したbyteデータを文字列に変換
                    string str = Encoding.GetEncoding("ASCII").GetString(buffer);
                    int delay = int.Parse(stopWatch.ElapsedMilliseconds.ToString());
                    //Console.WriteLine(stopWatch.ElapsedMilliseconds);
                    Console.WriteLine(5);
                    await socket.OutputStream.WriteAsync(bytes.AsBuffer());
                    await Task.Delay(delay);
                    now = DateTime.Now.ToString();
                    Console.WriteLine(now);
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

        //InputStreamの格納されている受信メッセージを受け取る命令
        public async void Receive()
        {
            try
            {
                if (socket != null)
                {
                    //whileで回す
                    byte[] buffer = new byte[120];
                    //InputStreamのデータを変数bufferに格納
                    await socket.InputStream.ReadAsync(buffer.AsBuffer(), 120, InputStreamOptions.Partial);
                    //受信したbyteデータを文字列に変換
                    string str = Encoding.GetEncoding("ASCII").GetString(buffer);
                    MessageBox.Show("" + str);
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
                        MessageBox.Show("Player" + (deviceId + 1) + "との通信が切断されました");
                    }
                }
            }
        }

        //送信メッセージをOutputStreamに格納する命令
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
                //stopWatch.Reset();
                //stopWatch.Start();
                //OutputStreamに文字列を送信
                await socket.OutputStream.WriteAsync(bytes.AsBuffer());
            }
        }

        //接続切断命令
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

            if (socket.InputStream != null)
            {
                socket.InputStream.Dispose();
            }

            if (socket.OutputStream != null)
            {
                socket.OutputStream.Dispose();
            }

            if (socket != null)
            {
                socket.Dispose();
                socket = null;
            }

        }
    }
}
