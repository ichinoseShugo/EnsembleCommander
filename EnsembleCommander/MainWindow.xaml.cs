using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.Generic;

using NextMidi.Data;
using NextMidi.Data.Domain;
using NextMidi.Data.Track;
using NextMidi.DataElement;
using NextMidi.Filing.Midi;
using NextMidi.MidiPort.Output;
using NextMidi.Time;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace EnsembleCommander
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Midiデータを扱うインスタンス
        /// </summary>
        MidiManager midiManager;

        int NowMode = 0;
        private const int MODE_WHOLE = 0;
        private const int MODE_QUARTER = 1;
        private const int MODE_ARPEGGIO = 2;
        private const int MODE_DELAY = 3;
        private const int MODE_FREE = 4;

        /// <summary>
        /// MIDI再生用オブジェクト
        /// </summary>
        public MidiPlayer player;
        /// <summary>
        /// 現在選択しているRange
        /// </summary>
        int NowRange = -1;

        /// <summary>
        /// 現在の調性(長調か短調か)
        /// </summary>
        public string NowTonality = "major";

        public bool IsConnectRealSense = false;

        PXCMSenseManager senseManager;
        /// <summary>
        /// 座標変換オブジェクト
        /// </summary>
        PXCMProjection projection;
        /// <summary>
        /// deviceのインタフェース
        /// </summary>
        PXCMCapture.Device device;

        PXCMHandModule handAnalyzer;
        PXCMHandData handData;

        const int COLOR_WIDTH = 960;
        const int COLOR_HEIGHT = 540;
        const int COLOR_FPS = 30;

        const int DEPTH_WIDTH = 640;
        const int DEPTH_HEIGHT = 480;
        const int DEPTH_FPS = 30;
        Brush[] brushes = new Brush[]{Brushes.Red,
                                    Brushes.OrangeRed,
                                    Brushes.Orange,
                                    Brushes.Yellow,
                                    Brushes.YellowGreen,
                                    Brushes.Green,
                                    Brushes.LightBlue,
                                    Brushes.Blue,
                                    Brushes.Navy,
                                    Brushes.Purple
        };
        Color[] colors = new Color[]{Colors.Red,
                                    Colors.OrangeRed,
                                    Colors.Orange,
                                    Colors.Yellow,
                                    Colors.YellowGreen,
                                    Colors.Green,
                                    Colors.LightBlue,
                                    Colors.Blue,
                                    Colors.Navy,
                                    Colors.Purple
        };

        //Mainイベント-------------------------------------------------------------------

        /// <summary>
        /// 一番最初に呼び出される部分
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Windowのロード時に初期化及び周期処理の登録を行う
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeMIDI();
            InitializeView();
            //RealSenseの初期化
            IsConnectRealSense = InitializeRealSense();
            ConnectCheck.Content = IsConnectRealSense;//バインド予定
            //WPFのオブジェクトがレンダリングされるタイミング(およそ1秒に50から60)に呼び出される
            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        /// <summary>
        /// Windowが終了するとき呼び出される
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            Uninitialize();
        }

        /// <summary>
        /// フレームごとの更新及び個別のデータ更新処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (IsConnectRealSense) UpdateRealSense();
            if (player.Playing) UpdateMIDI();
        }

        //RealSenseイベント-------------------------------------------------------------------

        /// <summary>
        /// ジェスチャーが呼び出された時のイベント
        /// </summary>
        /// <param name="data"></param>
        void OnFiredGesture(PXCMHandData.GestureData data)
        {
            if (data.name.CompareTo("thumb_up") == 0)
            {
                PlayMIDI();
            }
            if (data.name.CompareTo("fist") == 0)
            {
                StopMIDI();
            }
            if (data.name.CompareTo("tap") == 0)
            {
                midiManager.SetOnNote(player.MusicTime);
            }
        }

        //MIDIイベント-------------------------------------------------------------------

        /// <summary>
        /// 音源再生ボタンイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMidi_Click(object sender, RoutedEventArgs e)
        {
            PlayMIDI();
        }

        /// <summary>
        /// Midi停止時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Player_Stopped(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(
             new Action(() =>
             {
                 OffMidi.IsChecked = true;
             })
            );
            //転回したものを初期化
            foreach (var chord in midiManager.chordProgList[MODE_WHOLE]) chord.SetNotes(MODE_WHOLE);
            foreach (var chord in midiManager.chordProgList[MODE_QUARTER]) chord.SetNotes(MODE_QUARTER);
            foreach (var chord in midiManager.chordProgList[MODE_ARPEGGIO]) chord.SetNotes(MODE_ARPEGGIO);
            foreach (var chord in midiManager.chordProgList[MODE_DELAY]) chord.SetNotes(MODE_DELAY);
            foreach (var chord in midiManager.chordProgList[MODE_FREE]) chord.SetNotes(MODE_FREE);
        }

        /// <summary>
        /// 音源停止ボタンイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OffMidi_Click(object sender, RoutedEventArgs e)
        {
            StopMIDI();
        }

        /// <summary>
        /// WholeToneモード:全音符(初期設定と同じ)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWholeTone_Checked(object sender, RoutedEventArgs e)
        {
            if (midiManager != null)
            {
                midiManager.ExchangeTrack(MODE_WHOLE);
                NowMode = MODE_WHOLE;
            }
        }

        /// <summary>
        /// 四分音符モード
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnQuarterTone_Checked(object sender, RoutedEventArgs e)
        {
            midiManager.ExchangeTrack(MODE_QUARTER);
            NowMode = MODE_QUARTER;
        }

        /// <summary>
        /// Arpeggioモード:分散和音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnArpeggio_Checked(object sender, RoutedEventArgs e)
        {
            midiManager.ExchangeTrack(MODE_ARPEGGIO);
            NowMode = MODE_ARPEGGIO;
        }

        /// <summary>
        /// Delayモード:反響音を鳴らす
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnDelay_Checked(object sender, RoutedEventArgs e)
        {
            midiManager.ExchangeTrack(MODE_DELAY);
            NowMode = MODE_DELAY;
        }

        /// <summary>
        /// Freeモード:任意のタイミングで発音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFree_Checked(object sender, RoutedEventArgs e)
        {
            midiManager.ExchangeTrack(MODE_FREE);
            NowMode = MODE_FREE;
        }

        /// <summary>
        /// Freeモード時にクリックで発音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNote_Click(object sender, RoutedEventArgs e)
        {
            midiManager.SetOnNote(player.MusicTime);
        }

        /// <summary>
        /// ListBoxのitemが変わった時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PivotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //TODO: 今のモードを展開しているが、モードを変更したら転回が反映されてないのを何とかする
            midiManager.SetRange((int)PivotList.SelectedItem, player.MusicTime, NowMode);
            NowRange = (int)PivotList.SelectedItem;
        }

        /// <summary>
        /// Majorダイアトニックに書き換える
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Major_Click(object sender, RoutedEventArgs e)
        {
            if (NowTonality == "minor")
                midiManager.TurnMajor(player.MusicTime, NowMode);
            NowTonality = "major";
        }

        /// <summary>
        /// Minorダイアトニックに書き換える
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Minor_Click(object sender, RoutedEventArgs e)
        {
            if (NowTonality == "major")
                midiManager.TurnMinor(player.MusicTime, NowMode);
            NowTonality = "minor";
        }

        /// <summary>
        /// Elementsが変わっていないことを自由に確認したい
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DisplayElements_Click(object sender, RoutedEventArgs e)
        {


        }

        //RealSenseメソッド-------------------------------------------------------------------

        /// <summary>
        /// 機能の初期化
        /// </summary>
        private bool InitializeRealSense()
        {
            try
            {
                //SenseManagerを生成
                senseManager = PXCMSenseManager.CreateInstance();

                //カラーストリームの有効
                var sts = senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, COLOR_WIDTH, COLOR_HEIGHT, COLOR_FPS);
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    throw new Exception("Colorストリームの有効化に失敗しました");
                }

                // Depthストリームを有効にする
                sts = senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_DEPTH,
                    DEPTH_WIDTH, DEPTH_HEIGHT, DEPTH_FPS);
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    throw new Exception("Depthストリームの有効化に失敗しました");
                }

                // 手の検出を有効にする
                sts = senseManager.EnableHand();

                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    throw new Exception("手の検出の有効化に失敗しました");
                }

                //パイプラインを初期化する
                //(インスタンスはInit()が正常終了した後作成されるので，機能に対する各種設定はInit()呼び出し後となる)
                sts = senseManager.Init();
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR) throw new Exception("パイプラインの初期化に失敗しました");

                //ミラー表示にする
                senseManager.QueryCaptureManager().QueryDevice().SetMirrorMode(
                    PXCMCapture.Device.MirrorMode.MIRROR_MODE_HORIZONTAL);

                //デバイスを取得する
                device = senseManager.captureManager.device;

                //座標変換オブジェクトを作成
                projection = device.CreateProjection();

                // 手の検出の初期化
                InitializeHandTracking();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 手の検出の初期化
        /// </summary>
        private void InitializeHandTracking()
        {
            // 手の検出器を取得する
            handAnalyzer = senseManager.QueryHand();
            if (handAnalyzer == null)
            {
                throw new Exception("手の検出器の取得に失敗しました");
            }

            // 手のデータを作成する
            handData = handAnalyzer.CreateOutput();
            if (handData == null)
            {
                throw new Exception("手の検出器の作成に失敗しました");
            }

            // RealSense カメラであれば、プロパティを設定する
            var device = senseManager.QueryCaptureManager().QueryDevice();
            PXCMCapture.DeviceInfo dinfo;
            device.QueryDeviceInfo(out dinfo);
            if (dinfo.model == PXCMCapture.DeviceModel.DEVICE_MODEL_IVCAM)
            {
                device.SetDepthConfidenceThreshold(1);
                //device.SetMirrorMode( PXCMCapture.Device.MirrorMode.MIRROR_MODE_DISABLED );
                device.SetIVCAMFilterOption(6);
            }

            // 手の検出の設定
            var config = handAnalyzer.CreateActiveConfiguration();
            config.EnableSegmentationImage(true);
            config.EnableGesture("thumb_up");
            config.EnableGesture("tap");
            config.EnableGesture("fist");
            config.SubscribeGesture(OnFiredGesture);
            config.ApplyChanges();
            config.Update();
        }

        /// <summary>
        /// RealSesnseの更新
        /// </summary>
        private void UpdateRealSense()
        {
            //フレームを取得する
            //AcquireFrame()の引数はすべての機能の更新が終るまで待つかどうかを指定
            //ColorやDepthによって更新間隔が異なるので設定によって値を変更
            var ret = senseManager.AcquireFrame(true);
            if (ret < pxcmStatus.PXCM_STATUS_NO_ERROR) return;

            //フレームデータを取得する
            PXCMCapture.Sample sample = senseManager.QuerySample();
            if (sample != null)
            {
                //カラー画像の表示
                UpdateColorImage(sample.color);
            }
            //手のデータを更新
            UpdateHandFrame();
            //演奏領域の表示
            for (int k = 0; k < 5; k++)
            {
                SolidColorBrush myBrush = new SolidColorBrush(colors[k]);
                myBrush.Opacity = 0.25;
                AddRectangle(
                    imageColor.Height / 5 * k,
                    imageColor.Height / 5,
                    imageColor.Width,
                    Brushes.Black,
                    1.0d,
                    myBrush);
            }
           
            //フレームを解放する
            senseManager.ReleaseFrame();
        }

        /// <summary>
        /// カラーイメージが更新された時の処理
        /// </summary>
        /// <param name="color"></param>
        private void UpdateColorImage(PXCMImage colorFrame)
        {
            if (colorFrame == null) return;
            //データの取得
            PXCMImage.ImageData data;

            //アクセス権の取得
            pxcmStatus ret = colorFrame.AcquireAccess(
                PXCMImage.Access.ACCESS_READ, PXCMImage.PixelFormat.PIXEL_FORMAT_RGB32, out data);
            if (ret < pxcmStatus.PXCM_STATUS_NO_ERROR) throw new Exception("カラー画像の取得に失敗");

            //ビットマップに変換する
            //画像の幅と高さ，フォーマットを取得
            var info = colorFrame.QueryInfo();

            //1ライン当たりのバイト数を取得し(pitches[0]) 高さをかける　(1pxel 3byte)
            var length = data.pitches[0] * info.height;

            //画素の色データの取得
            //ToByteArrayでは色データのバイト列を取得する．
            var buffer = data.ToByteArray(0, length);
            //バイト列をビットマップに変換
            imageColor.Source = BitmapSource.Create(info.width, info.height, 96, 96, PixelFormats.Bgr32, null, buffer, data.pitches[0]);

            //データを解放する
            colorFrame.ReleaseAccess(data);
        }

        /// <summary>
        /// 手のデータを更新する
        /// </summary>
        private void UpdateHandFrame()
        {
            // 手のデータを更新する
            handData.Update();

            // データを初期化する
            CanvasFaceParts.Children.Clear();

            // 検出した手の数を取得する
            var numOfHands = handData.QueryNumberOfHands();
            for (int i = 0; i < numOfHands; i++)
            {
                // 手を取得する
                PXCMHandData.IHand hand;
                var sts = handData.QueryHandData(
                    PXCMHandData.AccessOrderType.ACCESS_ORDER_BY_ID, i, out hand);
                if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                {
                    continue;
                }

                GetFingerData(hand, PXCMHandData.JointType.JOINT_MIDDLE_TIP);
            }
        }

        /// <summary>
        /// 指のデータを取得する
        /// </summary>
        /// <param name="hand"></param>
        /// <param name="jointType"></param>
        /// <returns></returns>
        private bool GetFingerData(PXCMHandData.IHand hand, PXCMHandData.JointType jointType)
        {
            PXCMHandData.JointData jointData;
            var sts = hand.QueryTrackedJoint(jointType, out jointData);
            if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                return false;
            }

            // Depth座標系をカラー座標系に変換する
            var depthPoint = new PXCMPoint3DF32[1];
            var colorPoint = new PXCMPointF32[1];
            depthPoint[0].x = jointData.positionImage.x;
            depthPoint[0].y = jointData.positionImage.y;
            depthPoint[0].z = jointData.positionWorld.z * 1000;
            projection.MapDepthToColor(depthPoint, colorPoint);

            //演奏領域の当たり判定
            for (int i = 0; i < 5; i++)
            {
                if ((imageColor.Height / 5) * i <= colorPoint[0].y && colorPoint[0].y < (imageColor.Height / 5) * (i + 1))
                {
                    if (16 - i != NowRange)
                    {
                        NowRange = 16 - i;
                        PivotList.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                PivotList.SelectedItem = NowRange;
                            }
                            ));
                    }
                }
            }

            HitCheck(colorPoint[0]);

            AddEllipse(new Point(colorPoint[0].x, colorPoint[0].y), 5, Brushes.White, 1);

            return true;
        }

        /// <summary>
        /// 円を表示する
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="point"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="thickness"></param>
        private void AddEllipse(Point point, int radius, Brush color, int thickness)
        {
            var ellipse = new Ellipse()
            {
                Width = radius,
                Height = radius,
            };

            if (thickness <= 0)
            {
                ellipse.Fill = color;
            }
            else
            {
                ellipse.Stroke = Brushes.Black;
                ellipse.StrokeThickness = thickness;
                ellipse.Fill = color;
            }

            Canvas.SetLeft(ellipse, point.X);
            Canvas.SetTop(ellipse, point.Y);
            CanvasFaceParts.Children.Add(ellipse);
        }

        /// <summary>
        /// 四角生成オブジェクト
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="stroke"></param>
        /// <param name="thickness"></param>
        /// <param name="fill"></param>
        /// <returns></returns>
        private void AddRectangle(double y, double height, double width, Brush stroke, double thickness, Brush fill)
        {
            Rectangle rect = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = stroke,
                StrokeThickness = thickness,
                Fill = fill
            };
            Canvas.SetTop(rect, y);
            CanvasFaceParts.Children.Add(rect);
        }

        private void HitCheck(PXCMPointF32 p)
        {
            if (p.y < 10 || p.y > 60) return;
            for (int mode = 0; mode < 5; mode++)
            {
                if (10 + 100 * mode < p.x && p.x < 110 + 100 * mode)
                {
                    Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                switch (mode)
                                {
                                    case MODE_WHOLE:
                                        OnWholeTone.IsChecked = true;
                                        break;
                                    case MODE_QUARTER:
                                        OnQuarterTone.IsChecked = true;
                                        break;
                                    case MODE_ARPEGGIO:
                                        OnArpeggio.IsChecked = true;
                                        break;
                                    case MODE_DELAY:
                                        OnDelay.IsChecked = true;
                                        break;
                                    case MODE_FREE:
                                        OnFree.IsChecked = true;
                                        break;
                                    default:
                                        break;
                                }
                            }
                            ));
                    break;
                }
            }
        }

        /// <summary>
        /// 終了処理
        /// </summary>
        private void Uninitialize()
        {
            if (senseManager != null)
            {
                senseManager.Dispose();
                senseManager = null;
            }
            if (projection != null)
            {
                projection.Dispose();
                projection = null;
            }
            if (handData != null)
            {
                handData.Dispose();
                handData = null;
            }

            if (handAnalyzer != null)
            {
                handAnalyzer.Dispose();
                handAnalyzer = null;
            }
            //handConfig.UnsubscribeGesture(OnFiredGesture);
            //handConfig.Dispose();
        }

        //MIDIメソッド-----------------------------------------------------------

        /// <summary>
        /// MIDIの初期化
        /// </summary>
        /// <param name="portnum"></param>
        void InitializeMIDI()
        {
            midiManager = new MidiManager();
            player = new MidiPlayer(midiManager.port);
            player.Stopped += Player_Stopped;
        }

        /// <summary>
        /// 各フレームにおけるMIDIの処理
        /// </summary>
        private void UpdateMIDI()
        {
            double pos = (MidiManager.TICK_UNIT * player.MusicTime.Measure + player.MusicTime.Tick)
                / (double)(MidiManager.TICK_UNIT * midiManager.inputedChord.Length)
                * Score.Width;
            CurrentLine.X1 = pos;
            CurrentLine.X2 = pos;
        }

        public void PlayMIDI()
        {
            player.Play(midiManager.domain);
        }

        public void StopMIDI()
        {
            player.Stop();
        }

        /// <summary>
        /// ボタンなどの初期化
        /// </summary>
        void InitializeView()
        {
            int[] Ranges = new int[16];
            for (int i = 0; i < 16; i++) Ranges[i] = 31 - (i + 8);
            PivotList.ItemsSource = null;
            PivotList.ItemsSource = Ranges;
        }
    }
}