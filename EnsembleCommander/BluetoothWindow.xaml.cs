using System;
using System.Collections.Generic;
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

namespace EnsembleCommander
{
    /// <summary>
    /// BluetoothWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class BluetoothWindow : Window
    {
        List<BluetoothManager> bServerList = new List<BluetoothManager>();
        /// <summary>
        /// 接続されるデバイスの順番を表すID
        /// </summary>
        int deviceOerder = 0;
        System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        public int s = 0;

        MainWindow main;

        public BluetoothWindow(MainWindow m, double mainWidth)
        {
            InitializeComponent();
            Top = 90;
            Left = mainWidth;

            main = m;
        }

        private void ListenButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (deviceOerder != 0)
            {
                bServerList[0].Listen(deviceOerder++);
                return;
            }
            BluetoothManager bluetoothServer = new BluetoothManager(main, this);
            bluetoothServer.Listen(deviceOerder);
            bServerList.Add(bluetoothServer);
            deviceOerder++;
            //各種ボタンを使用可能に
            ListenButton.IsEnabled = true;
            DisconnectButton.IsEnabled = true;
            ReadButton.IsEnabled = true;
            SendButton.IsEnabled = true;
            PingButton.IsEnabled = true;
        }
        private void SendButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bServerList[0].Send();
        }
        private void ReadButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bServerList[0].Receive();
        }
        private void PingButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //bServerList[0].Ping(0);
            bServerList[0].SyncStart();
        }
        private void DisconnectButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //bluetoothServer.Disconnect();
            for (int i = 0; i < 50; i++)
                MessageBox.Show("" + bServerList[0].delayTimeList[i]);
            bServerList[0].delayTimeList.Clear();
        }

        private void Player1_Checked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("プレイヤー1接続");
        }
        private void Player2_Checked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("プレイヤー2接続");
        }
        private void Player3_Checked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("プレイヤー3接続");
        }

        public async void Player_Connect(int PlayerId)
        {
            await Dispatcher.BeginInvoke(
                                new Action(() =>
                                {
                                    if (PlayerId == 0)
                                    {
                                        Player1.IsChecked = true;
                                    }
                                    else if (PlayerId == 1)
                                        Player2.IsChecked = true;
                                    else if (PlayerId == 2)
                                        Player3.IsChecked = true;
                                })
                        );
        }
    }
}