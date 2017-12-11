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
        MidiOutPort port;
        MidiPlayer player;
        MidiFileDomain domain;
        MusicTime currentTime;
        MidiTrack track;
        /// <summary>
        /// 一小節の時間
        /// </summary>
        public int tickUnit = 240 * 4;
        /// <summary>
        /// コード進行の各コードのリスト
        /// </summary>
        List<Chord> chordlist = new List<Chord>();
        /// <summary>
        /// 現在選択しているRange
        /// </summary>
        int NowRange = -1;

        /// <summary>
        /// 正規表現によるルート音(A,C#,Dbなど)のパターン
        /// </summary>
        Regex RootNameP = new Regex("^[ABCDEFG]+[b#]*", RegexOptions.Compiled);

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

        const int COLOR_WIDTH = 640;
        const int COLOR_HEIGHT = 480;
        const int COLOR_FPS = 30;

        const int DEPTH_WIDTH = 640;
        const int DEPTH_HEIGHT = 480;
        const int DEPTH_FPS = 30;

        Color[] color = new Color[]{Colors.Red,
                                    Colors.OrangeRed,
                                    Colors.Orange,
                                    Colors.Yellow,
                                    Colors.YellowGreen,
                                    Colors.Green,
                                    Colors.LightBlue,
                                    Colors.Blue,
                                    Colors.Navy,
                                    Colors.Purple };

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
            InitializeMIDI(0);
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
                Console.WriteLine("thumb_up");
                if (!player.Playing)
                {
                    currentTime = player.MusicTime;
                    player.Play(domain);
                }
            }
            if (data.name.CompareTo("fist") == 0)
            {
                Console.WriteLine("fist");
                if (player != null || !player.Playing) player.Stop();
            }
            if (data.name.CompareTo("tap") == 0)
            {
                Console.WriteLine("tap");
                SetOnNote();
            }
        }

        //MIDIイベント-------------------------------------------------------------------

        /// <summary>
        /// 各フレームにおけるMIDIの処理
        /// </summary>
        private void UpdateMIDI()
        {
            Measure.Content = player.MusicTime.Measure;
            Tick.Content = player.MusicTime.Tick;
            double pos = ((tickUnit * player.MusicTime.Measure + player.MusicTime.Tick) / ((double)track.TickLength + 1000)) * Score.Width;

            CurrentLine.X1 = pos;
            CurrentLine.X2 = pos;

            WholeTime.Content = track.TickLength;
            PlayTime.Content = tickUnit * player.MusicTime.Measure + player.MusicTime.Tick;
            Position.Content = pos;
        }

        /// <summary>
        /// 音源再生ボタンイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMidi_Click(object sender, RoutedEventArgs e)
        {
            player.Stop();
            currentTime = player.MusicTime;
            player.Play(domain);
        }

        /// <summary>
        /// Midi停止時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Player_Stopped(object sender, EventArgs e)
        {
            player.Stop();
            port.Close();
            InitializeMIDI(0);
            OnWholeTone.Dispatcher.BeginInvoke(
             new Action(() =>
             {
                 OnWholeTone.IsChecked = true;
             })
            );
            player.Play(domain);
        }

        /// <summary>
        /// 音源停止ボタンイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OffMidi_Click(object sender, RoutedEventArgs e)
        {
            if (player != null || !player.Playing) player.Stop();
        }

        /// <summary>
        /// WholeToneモード:全音符(初期設定と同じ)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWholeTone_Click(object sender, RoutedEventArgs e)
        {
            SetWholeTone();
        }

        /// <summary>
        /// Arpeggioモード:分散和音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnArpeggio_Click(object sender, RoutedEventArgs e)
        {
            SetArpeggio();
        }

        /// <summary>
        /// Freeモード:任意のタイミングで発音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFree_Click(object sender, RoutedEventArgs e)
        {
            // Alpggioの場合一度WholeNoteに戻す
            SetWholeTone();
            foreach (var chord in chordlist)
            {
                foreach (var note in chord.Notes)
                {
                    note.Velocity = 0;
                }
            }
        }

        /// <summary>
        /// Freeモード時にクリックで発音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNote_Click(object sender, RoutedEventArgs e)
        {
            SetOnNote();
        }

        /// <summary>
        /// ListBoxのitemが変わった時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PivotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetRange((int)PivotList.SelectedItem);
            NowRange = (int)PivotList.SelectedItem;
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
                var sts = senseManager.EnableStream(PXCMCapture.StreamType.STREAM_TYPE_COLOR, 640, 480, 30);
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
                SolidColorBrush myBrush = new SolidColorBrush(color[k]);
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

                /*
                // 指の関節を列挙する
                for (int j = 0; j < PXCMHandData.NUMBER_OF_JOINTS; j++)
                {
                    if (!ShowFingerPosition(hand, (PXCMHandData.JointType)i)) continue;
                }
                */
            }
        }

        private bool GetFingerData(PXCMHandData.IHand hand, PXCMHandData.JointType jointType)
        {
            // 指のデータを取得する
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

            AddEllipse(
                new Point(colorPoint[0].x, colorPoint[0].y),
                5,
                Brushes.White,
                1);

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
        void InitializeMIDI(int portnum)
        {
            //コードリストの初期化
            chordlist.Clear();

            // Midiデータの作成
            //String[] chordProgress = { "C", "Am", "F", "G", "Em", "F", "G", "C" }; //背景楽曲のコード進行配列
            String[] chordProgress = { "D", "A", "Bm", "F#m", "G", "D", "G", "A" }; //背景楽曲のコード進行配列 
            MidiData midiData = new MidiData();
            track = new MidiTrack(); //各楽器が見る楽譜
            midiData.Tracks.Add(track); //midiDataにtrackを対応付け

            int tick = 0;
            //コード進行配列(chordProgress)から根音(root)を決定する
            foreach (String chordName in chordProgress)
            {
                //コードの根音を取得
                String structure = "";
                byte root = 60;

                GetStructure(chordName, out root, out structure);

                // newでインスタンスを作成し、変数chordに格納
                Chord chord = new Chord(tick, root, structure);

                track.Insert(chord.Base);//低い根音
                foreach (var note in chord.Notes) track.Insert(note);

                chordlist.Add(chord); // chordをリストchordlistに追加
                tick += tickUnit; //一小節進む
            }
            //showAllStructure();

            port = new MidiOutPort(portnum);
            try
            {
                port.Open();
            }
            catch
            {
                Console.WriteLine("no such port exists");
                return;
            }

            // テンポマップを作成
            domain = new MidiFileDomain(midiData);
            player = new MidiPlayer(port);
            player.Stopped += Player_Stopped;
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

        /// <summary>
        /// コードネームからNoteナンバー(byte型)と構造(string型)を決定
        /// </summary>
        /// <param name="chordName"></param>
        /// <returns></returns>
        public void GetStructure(String chordName, out byte root, out String structure)
        {
            //ルート音の初期化
            string rootName = "none";

            //chordNameで正規表現と一致する対象を1つ検索
            Match m = RootNameP.Match(chordName);
            //パターンがマッチする限り繰り返す
            while (m.Success)
            {
                //一致した対象が見つかったときキャプチャした部分文字列を表示
                rootName = m.Value;
                //次に一致する対象を検索
                m = m.NextMatch();
            }
            
            //chordNameをmidiナンバーに変換し，rootに格納
            if (rootName=="C")
            {
                structure = chordName.Remove(0, 1);
                root = 60;
            }
            else if (rootName=="C#" || rootName=="Db")
            {
                structure = chordName.Remove(0, 2);
                root = 61;
            }
            else if (rootName=="D")
            {
                structure = chordName.Remove(0, 1);
                root = 62;
            }
            else if (rootName=="D#" || rootName=="Eb")
            {
                structure = chordName.Remove(0, 2);
                root = 63;
            }
            else if (rootName=="E")
            {
                structure = chordName.Remove(0, 1);
                root = 64;
            }
            else if (rootName=="F")
            {
                structure = chordName.Remove(0, 1);
                root = 65;
            }
            else if (rootName=="F#" || rootName=="Gb")
            {
                structure = chordName.Remove(0, 2);
                root = 66;
            }
            else if (rootName=="G")
            {
                structure = chordName.Remove(0, 1);
                root = 55;
            }
            else if (rootName=="G#" || rootName=="Ab")
            {
                structure = chordName.Remove(0, 2);
                root = 56;
            }
            else if (rootName=="A")
            {
                structure = chordName.Remove(0, 1);
                root = 57;
            }
            else if (rootName=="A#" || rootName=="Bb")
            {
                structure = chordName.Remove(0, 2);
                root = 58;
            }
            else if (rootName=="B")
            {
                structure = chordName.Remove(0, 1);
                root = 59;
            }
            else
            {
                structure = "none";
                root = 128;
            }
        }

        /// <summary>
        /// 各chordlistの各構成音を表示
        /// </summary>
        public void ShowAllStructure()
        {
            for (int i = 0; i < chordlist.Count; i++)
            {
                Console.WriteLine("chordlist[" + i + "]");
                Console.WriteLine("Note[0]=" + chordlist[i].Notes[0].Note);
                Console.WriteLine("Note[1]=" + chordlist[i].Notes[1].Note);
                Console.WriteLine("Note[2]=" + chordlist[i].Notes[2].Note);
                Console.WriteLine("Pivot=" + chordlist[i].Pivot);
                Console.WriteLine("PivotRange=" + chordlist[i].PivotRange + "\n");
            }
        }

        /// <summary>
        /// ユーザ指定したRangeにコードを転回してPivotRangeを移動する
        /// </summary>
        public void SetRange(int Range)
        {
            MusicTime current = player.MusicTime; // 現在の演奏カーソルを取得

            // 次の小節のコードのPivotRangeとユーザが指定したRangeとの差分だけ転回
            // 次の小節からそれ以降の小節まで
            for (int i = current.Measure; i < chordlist.Count; i++)
            {
                //ユーザが指定したRangeとの差分だけ転回
                Turn(Range - chordlist[i].PivotRange, i);
            }
        }

        /// <summary>
        /// 転回メソッド : i小節目のコードをk回だけ転回する
        /// </summary>
        /// <param name="k"></param>
        /// <param name="i"></param>
        private void Turn(int k, int i)
        {
            if (k > 0) // +k転回
            {
                for (int j = 0; j < k; j++)
                {
                    // Notes[0]からNotes[3]のNoteうち、要素のnoteナンバーが一番小さいNotes[].Noteを一オクターブ上げる
                    int min = chordlist[i].Notes[0].Note;
                    int minIndex = 0;
                    if (min > chordlist[i].Notes[1].Note)
                    {
                        min = chordlist[i].Notes[1].Note;
                        minIndex = 1;
                    }
                    if (min > chordlist[i].Notes[2].Note)
                    {
                        min = chordlist[i].Notes[2].Note;
                        minIndex = 2;
                    }
                    // int MinIndex = Array.IndexOf(chordlist[i].Notes, max);
                    chordlist[i].Notes[minIndex].Note += 12;
                }

                // Pivotの更新
                chordlist[i].Pivot = (chordlist[i].Notes[0].Note + chordlist[i].Notes[1].Note + chordlist[i].Notes[2].Note) / 3;

                //PivotRangeの更新
                for (int rangeIndex = 0; rangeIndex < 31; rangeIndex++)
                {
                    if (rangeIndex * 4 <= chordlist[i].Pivot && chordlist[i].Pivot < (rangeIndex + 1) * 4)
                    {
                        chordlist[i].PivotRange = rangeIndex;
                        break;
                    }
                }

            }

            if (k < 0) // -k転回
            {
                k = k * (-1);
                for (int j = 0; j < k; j++)
                {
                    // Notes[0]からNotes[3]のNoteうち、要素の値が一番小さいNotes[].Noteを一オクターブ上げる
                    int max = chordlist[i].Notes[0].Note;
                    int maxIndex = 0;
                    if (max < chordlist[i].Notes[1].Note)
                    {
                        max = chordlist[i].Notes[1].Note;
                        maxIndex = 1;
                    }
                    if (max < chordlist[i].Notes[2].Note)
                    {
                        max = chordlist[i].Notes[2].Note;
                        maxIndex = 2;
                    }
                    // int MinIndex = Array.IndexOf(chordlist[i].Notes, max);
                    chordlist[i].Notes[maxIndex].Note -= 12;
                }

                // Pivot, PivotRangeの更新
                chordlist[i].Pivot = (chordlist[i].Notes[0].Note + chordlist[i].Notes[1].Note + chordlist[i].Notes[2].Note) / 3;

                //PivotRangeの更新
                for (int rangeIndex = 0; rangeIndex < 31; rangeIndex++)
                {
                    if (rangeIndex * 4 <= chordlist[i].Pivot && chordlist[i].Pivot < (rangeIndex + 1) * 4)
                    {
                        chordlist[i].PivotRange = rangeIndex;
                        break;
                    }
                }
            }

        }

        /// <summary>
        /// コード進行を全音表記に
        /// </summary>
        public void SetWholeTone()
        {
            foreach (var chord in chordlist)
            {
                //コードの和音の数とノートリストの数が合わなければ
                //(現時点ではアルペジオによって三和音でも4つのノートが鳴る場合)
                if (chord.NoteCount < chord.Notes.Count)
                {
                    //トラックから音を削除
                    domain.MidiData.Tracks[0].Remove(chord.Notes[chord.Notes.Count - 1]);
                    //リストから音を削除
                    chord.Notes.Remove(chord.Notes[chord.Notes.Count - 1]);
                }
                foreach (var note in chord.Notes)
                {
                    note.Gate = tickUnit; //音の長さを一小節の時間に
                    note.Tick = chord.TickFromStart; //TickのタイミングをchordのTickと合わせる
                }
            }
        }

        /// <summary>
        /// コード進行をアルペジオに
        /// </summary>
        public void SetArpeggio()
        {
            foreach (var chord in chordlist)
            {
                //三和音なら4拍目に最初の音を追加する
                if (chord.Notes.Count != 4)
                {
                    //リストに最初の音を追加
                    chord.Notes.Add((NoteEvent)chord.Notes[0].Clone());
                    //トラックに追加の音を挿入
                    domain.MidiData.Tracks[0].Insert(chord.Notes[chord.Notes.Count - 1]);
                }
                //ノートをアルペジオに
                for (int i = 0; i < chord.Notes.Count; i++)
                {
                    int gate = chord.Notes[i].Gate / 4;
                    chord.Notes[i].Gate = gate;
                    chord.Notes[i].Tick += gate * i;
                }
            }
        }

        /// <summary>
        /// 自由なタイミングに和音
        /// </summary>
        public void SetOnNote()
        {
            //Freeモードでないなら終了
            /*
            Dispatcher.Invoke(new Action(() =>
            {
                // ここで UI を操作する。
                if (OnFree.IsChecked == false) return;
            }));
            */

            //タップタイミングの1ms後のタイミングに演奏音を書き換え
            MusicTime current = player.MusicTime; // 現在(Tap時)の演奏カーソルを取得
            foreach (var note in chordlist[current.Measure].Notes)
            {
                note.Tick = tickUnit * current.Measure + current.Tick + 1;
                note.Velocity = 80;
                note.Gate = 240;
                note.Speed = 120;
            }
        }
    }
}