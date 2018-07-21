using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using EnsembleCommander.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;

namespace EnsembleCommander
{
    /// <summary>
    /// BluetoothWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class BluetoothWindow : Window
    {
        private MainWindow main;
        
        /// <summary> 複数のClientに対して用意するServer変数の格納リスト </summary>
        private List<BluetoothServer> bServerList = new List<BluetoothServer>();
        /// <summary> 選択されているデバイスの順番 </summary>
        private int selectedIndex = 0;
        /// <summary> 接続されているデバイスの数 </summary>
        private int deviceCount = 0;

        /// <summary> 通信可能なデバイスの検索と更新のための変数 </summary>
        private DeviceWatcher deviceWatcher;
        /// <summary> 通信可能なデバイスを表示するためのリスト </summary>
        public ObservableCollection<RfcommDeviceDisplay> ResultCollection { get; private set; }

        /// <summary> 接続済デバイスを表示するためのリスト </summary>
        public ObservableCollection<RfcommDeviceDisplay> PairingCollection { get; private set; }

        //はじめに呼び出される
        public BluetoothWindow(MainWindow m)
        {
            InitializeComponent();

            main = m;

            Left = m.ActualWidth;
            Top = 90;

            ResultCollection = new ObservableCollection<RfcommDeviceDisplay>();
            PairingCollection = new ObservableCollection<RfcommDeviceDisplay>();
        }

        //Windowがロードされた時に呼び出される
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ResultsListView.DataContext = ResultCollection;
            PairingList.DataContext = PairingCollection;
        }

        private void ListenButton_Click(object sender, RoutedEventArgs e)
        {
            //デバイス一覧からデバイスが選択されているか確認
            if (ResultsListView.SelectedItem == null)
            {
                StatusMessage.Text = "接続先デバイスが選択されてないよ";
                return;
            }

            //DeviceWatcherの終了
            StopWatcher();

            //Serverの準備と接続待機状態への移行
            var selectedDevice = ResultsListView.SelectedItem as RfcommDeviceDisplay;
            if (PairingCollection.Contains(selectedDevice))
            {
                Console.WriteLine("もうある");
                return;
            }
            BluetoothServer bluetoothServer = new BluetoothServer(deviceCount++, selectedDevice, this, main);
            bServerList.Add(bluetoothServer);
            bluetoothServer.Listen();

            //各種ボタンを使用可能に
            ListenButton.IsEnabled = false;
            DisconnectButton.IsEnabled = true;
            ReadButton.IsEnabled = true;
            SendButton.IsEnabled = true;
            //StartMidiButton.IsEnabled = true;
        }
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (PairingList.SelectedValue == null) return;
            bServerList[selectedIndex].Send();
        }
        private void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (PairingList.SelectedValue == null) return;
            bServerList[selectedIndex].Receive();
        }
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (PairingList.SelectedValue == null) return;
            bServerList[selectedIndex].Disconnect();
            bServerList.RemoveAt(selectedIndex);
            PairingCollection.RemoveAt(selectedIndex);
            for (int i = 0; i < bServerList.Count; i++)
            {
                bServerList[i].deviceOrder = i;
            }
            deviceCount--;
        }
        private void StartMidiButton_Click(object sender, RoutedEventArgs e)
        {
            if (PairingList.SelectedValue == null) return;

            main.UpdateNTPTime();
            bServerList[selectedIndex].StartMidi();
        }

        #region 接続可能なデバイス一覧の取得と表示
        //接続候補のリストのアイテム選択時に発生するイベントハンドラ
        private void ResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePairingButtons();
        }

        /// <summary> ListenButtonの使用可不可の更新 </summary>
        private void UpdatePairingButtons()
        {
            RfcommDeviceDisplay deviceDisp = (RfcommDeviceDisplay)ResultsListView.SelectedItem;

            if (deviceDisp != null)
            {
                ListenButton.IsEnabled = true;
            }
            else
            {
                ListenButton.IsEnabled = false;
            }
        }

        private void EnumerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (deviceWatcher == null)
            {
                SetDeviceWatcherUI();
                StartUnpairedDeviceWatcher();
            }
            else
            {
                ResetMainUI();
            }
        }

        /// <summary> リスト表示のUIの初期化処理 </summary>
        private void SetDeviceWatcherUI()
        {
            // Disable the button while we do async operations so the user can't Run twice.
            EnumerateButton.Content = "Stop";
            ResultsListView.Visibility = Visibility.Visible;
            ResultsListView.IsEnabled = true;
        }

        /// <summary> デバイス一覧を取得してリストに表示する </summary>
        private void StartUnpairedDeviceWatcher()
        {
            // Request additional properties
            string[] requestedProperties = new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
            deviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")",
                                                            requestedProperties,
                                                            DeviceInformationKind.AssociationEndpoint);
            //接続可能なデバイス候補が出現した際に呼び出されるイベントハンドラ
            deviceWatcher.Added += new TypedEventHandler<DeviceWatcher, DeviceInformation>(async (watcher, deviceInfo) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                await Dispatcher.BeginInvoke(
                 new Action(() =>
                 {
                     // Make sure device name isn't blank
                     if (deviceInfo.Name != "")
                     {
                         Console.WriteLine(deviceInfo.Name);
                         ResultCollection.Add(new RfcommDeviceDisplay(deviceInfo));
                     }
                 }
                ));
            });

            //デバイス候補が更新されるたびに呼び出されるイベントハンドラ
            deviceWatcher.Updated += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(async (watcher, deviceInfoUpdate) =>
            {
                await Dispatcher.BeginInvoke(
                new Action(() => {
                    foreach (RfcommDeviceDisplay rfcommInfoDisp in ResultCollection)
                    {
                        if (rfcommInfoDisp.Id == deviceInfoUpdate.Id)
                        {
                            rfcommInfoDisp.Update(deviceInfoUpdate);
                            break;
                        }
                    }
                }
                ));
            });

            //デバイス一覧の表示が終了した際に呼び出されるイベントハンドラ（現段階では使用していない）
            deviceWatcher.EnumerationCompleted += new TypedEventHandler<DeviceWatcher, Object>(async (watcher, obj) =>
            {
                await Dispatcher.BeginInvoke(
                new Action(() => {
                }
                ));
            });

            //一覧から削除された際に呼び出されるイベントハンドラ
            deviceWatcher.Removed += new TypedEventHandler<DeviceWatcher, DeviceInformationUpdate>(async (watcher, deviceInfoUpdate) =>
            {
                // Since we have the collection databound to a UI element, we need to update the collection on the UI thread.
                await Dispatcher.BeginInvoke(
                new Action(() => {
                    // Find the corresponding DeviceInformation in the collection and remove it
                    foreach (RfcommDeviceDisplay rfcommInfoDisp in ResultCollection)
                    {
                        if (rfcommInfoDisp.Id == deviceInfoUpdate.Id)
                        {
                            ResultCollection.Remove(rfcommInfoDisp);
                            break;
                        }
                    }
                }));
            });

            //デバイス一覧の列挙が停止した際に呼び出されるイベントハンドラ
            deviceWatcher.Stopped += new TypedEventHandler<DeviceWatcher, Object>(async (watcher, obj) =>
            {
                await Dispatcher.BeginInvoke(
                new Action(() => {
                    //ResultCollection.Clear();
                }));
            });

            deviceWatcher.Start();
        }

        /// <summary> リストの一覧表示をリセットする </summary>
        private void ResetMainUI()
        {
            EnumerateButton.Content = "Start";
            ListenButton.Visibility = Visibility.Visible;
            ResultsListView.Visibility = Visibility.Visible;
            ResultsListView.IsEnabled = true;

            // Re-set device specific UX
            //RequestAccessButton.Visibility = Visibility.Collapsed;
            StopWatcher();
        }

        /// <summary> DeviceWatcherの終了 </summary>
        private void StopWatcher()
        {
            if (deviceWatcher != null)
            {
                if ((DeviceWatcherStatus.Started == deviceWatcher.Status ||
                     DeviceWatcherStatus.EnumerationCompleted == deviceWatcher.Status))
                {
                    deviceWatcher.Stop();
                }
                deviceWatcher = null;
            }
        }
        #endregion

        #region 接続済みデバイス一覧の取得と表示
        public async void Player_Connect(RfcommDeviceDisplay device)
        {
            await Dispatcher.BeginInvoke(
                 new Action(() =>
                 {
                     PairingCollection.Add(device);
                 }
                ));
        }

        private void PairingList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedIndex = PairingList.SelectedIndex;
        }
        #endregion
    }
}