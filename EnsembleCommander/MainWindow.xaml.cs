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
        bool Free = false;

        /// <summary>
        /// 一小節の時間
        /// </summary>
        public int tickUnit = 240 * 4;

        /// <summary>
        /// コード進行の各コードのリスト
        /// </summary>
        List<Chord> chordlist = new List<Chord>();

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

        //イベントハンドラ-------------------------------------------------------------------

        /// <summary>
        /// Windowのロード時に初期化及び周期処理の登録を行う
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //InitializeRealSense();
            InitializeMIDI(0);
            InitializeView();
            //WPFのオブジェクトがレンダリングされるタイミング(およそ1秒に50から60)に呼び出される
            //CompositionTarget.Rendering += CompositionTarget_Rendering;
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
            try
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

                UpdateHandFrame();

                //フレームを解放する
                senseManager.ReleaseFrame();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
            }
        }

        /// <summary>
        /// ジェスチャーが呼び出された時のイベント
        /// </summary>
        /// <param name="data"></param>
        void OnFiredGesture(PXCMHandData.GestureData data)
        {
            if (data.name.CompareTo("tap") == 0)
            {
            }
        }

        /// <summary>
        /// 音源再生ボタンイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnMidi_Click(object sender, RoutedEventArgs e)
        {
            player.Stop();
            player.Play(domain);
        }

        /// <summary>
        /// Midi停止時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Player_Stopped(object sender, EventArgs e)
        {
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
            player.Stop();
        }

        /// <summary>
        /// 任意タイミングでの発音(Freeボタン)イベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rbtn3_Checked(object sender, RoutedEventArgs e)
        {
            Free = true;
            for (int i = 0; i < chordlist.Count; i++)
            {
                //Rbtn1_Checked(sender, e); // Alpggioの場合一度WholeNoteに戻す

                chordlist[i].Notes[0].Velocity = 0;
                chordlist[i].Notes[1].Velocity = 0;
                chordlist[i].Notes[2].Velocity = 0;
                // chordlist[i].Notes[3].Velocity = 0;
            }
        }

        /// <summary>
        /// ボタン3イベント：Freeモード時、ボタンを押したタイミングで発音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn3_Click(object sender, RoutedEventArgs e)
        {
            // タップで発音
            if (Free == true)
            {
                //この時の和音だけを鳴らす。
                MusicTime current = player.MusicTime; // 現在(Tap時)の演奏カーソルを取得
                System.Console.WriteLine("curent 小節: " + current.Measure + ", Tick: " + current.Tick);
                System.Console.WriteLine("Notes.Tick:" + chordlist[current.Measure].Notes[0].Tick);

                chordlist[current.Measure].Notes[0].Tick = tickUnit * current.Measure + current.Tick + 1;
                chordlist[current.Measure].Notes[1].Tick = tickUnit * current.Measure + current.Tick + 1;
                chordlist[current.Measure].Notes[2].Tick = tickUnit * current.Measure + current.Tick + 1;
                // chordlist[current.Measure].Notes[3].Tick = tickUnit * current.Measure + current.Tick + 1;
                chordlist[current.Measure].Notes[0].Velocity = 80;
                chordlist[current.Measure].Notes[1].Velocity = 80;
                chordlist[current.Measure].Notes[2].Velocity = 80;
                // chordlist[current.Measure].Notes[3].Velocity = 80;

                chordlist[current.Measure].Notes[0].Gate = 240;
                chordlist[current.Measure].Notes[1].Gate = 240;
                chordlist[current.Measure].Notes[2].Gate = 240;
                // chordlist[current.Measure].Notes[3].Gate = 240;

                chordlist[current.Measure].Notes[0].Speed = 120;
                chordlist[current.Measure].Notes[1].Speed = 120;
                chordlist[current.Measure].Notes[2].Speed = 120;
                // chordlist[current.Measure].Notes[3].Speed = 120;

            }
        }

        /// <summary>
        /// WholeToneモード:全音符(初期設定と同じ)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWholeTone_Click(object sender, RoutedEventArgs e)
        {
            foreach(var chord in chordlist)
            {
                //コードの和音の数とノートリストの数が合わなければ
                //(現時点ではアルペジオによって三和音でも4つのノートが鳴る場合)
                if (chord.NoteCount < chord.Notes.Count)
                {
                    //トラックから音を削除
                    domain.MidiData.Tracks[0].Remove(chord.Notes[chord.Notes.Count - 1]);
                    //リストから音を削除
                    chord.Notes.Remove(chord.Notes[chord.Notes.Count-1]);
                }
                foreach (var note in chord.Notes)
                {
                    note.Gate = tickUnit; //音の長さを一小節の時間に
                    note.Tick = chord.TickFromStart; //TickのタイミングをchordのTickと合わせる
                }
            }
        }

        /// <summary>
        /// Arpeggioモード:分散和音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnArpeggio_Click(object sender, RoutedEventArgs e)
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

                for (int i = 0; i < chord.Notes.Count; i++)
                {
                    int gate = chord.Notes[i].Gate / 4;
                    chord.Notes[i].Gate = gate;
                    chord.Notes[i].Tick += gate * i;
                }
            }
        }

        /// <summary>
        /// Freeモード:任意のタイミングで発音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnFree_Click(object sender, RoutedEventArgs e)
        {
            Free = true;
            for (int i = 0; i < chordlist.Count; i++)
            {
                //Rbtn1_Checked(sender, e); // Alpggioの場合一度WholeNoteに戻す

                chordlist[i].Notes[0].Velocity = 0;
                chordlist[i].Notes[1].Velocity = 0;
                chordlist[i].Notes[2].Velocity = 0;
                chordlist[i].Notes[3].Velocity = 0;
            }
        }
        
        /// <summary>
        /// Freeモード時にクリックで発音
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNote_Click(object sender, RoutedEventArgs e)
        {
            // タップで発音
            if (Free == true)
            {
                //この時の和音だけを鳴らす。
                MusicTime current = player.MusicTime; // 現在(Tap時)の演奏カーソルを取得
                System.Console.WriteLine("curent 小節: " + current.Measure + ", Tick: " + current.Tick);
                System.Console.WriteLine("Notes.Tick:" + chordlist[current.Measure].Notes[0].Tick);

                chordlist[current.Measure].Notes[0].Tick = tickUnit * current.Measure + current.Tick + 1;
                chordlist[current.Measure].Notes[1].Tick = tickUnit * current.Measure + current.Tick + 1;
                chordlist[current.Measure].Notes[2].Tick = tickUnit * current.Measure + current.Tick + 1;
                chordlist[current.Measure].Notes[3].Tick = tickUnit * current.Measure + current.Tick + 1;
                chordlist[current.Measure].Notes[0].Velocity = 80;
                chordlist[current.Measure].Notes[1].Velocity = 80;
                chordlist[current.Measure].Notes[2].Velocity = 80;
                chordlist[current.Measure].Notes[3].Velocity = 80;

                chordlist[current.Measure].Notes[0].Gate = 240;
                chordlist[current.Measure].Notes[1].Gate = 240;
                chordlist[current.Measure].Notes[2].Gate = 240;
                chordlist[current.Measure].Notes[3].Gate = 240;

                chordlist[current.Measure].Notes[0].Speed = 120;
                chordlist[current.Measure].Notes[1].Speed = 120;
                chordlist[current.Measure].Notes[2].Speed = 120;
                chordlist[current.Measure].Notes[3].Speed = 120;

            }
        }

        /// <summary>
        /// ListBoxのitemが変わった時のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PivotList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            setRange((int)PivotList.SelectedItem);
        }

        /// <summary>
        /// 一番最初に呼び出される部分
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        //RealSenseメソッド-------------------------------------------------------------------

        /// <summary>
        /// 機能の初期化
        /// </summary>
        private void InitializeRealSense()
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
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
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
            config.EnableGesture("tap");
            config.SubscribeGesture(OnFiredGesture);
            config.ApplyChanges();
            config.Update();
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

                // 指の関節を列挙する
                for (int j = 0; j < PXCMHandData.NUMBER_OF_JOINTS; j++)
                {
                    // 指のデータを取得する
                    PXCMHandData.JointData jointData;
                    sts = hand.QueryTrackedJoint((PXCMHandData.JointType)j, out jointData);
                    if (sts < pxcmStatus.PXCM_STATUS_NO_ERROR)
                    {
                        continue;
                    }

                    // Depth座標系をカラー座標系に変換する
                    var depthPoint = new PXCMPoint3DF32[1];
                    var colorPoint = new PXCMPointF32[1];
                    depthPoint[0].x = jointData.positionImage.x;
                    depthPoint[0].y = jointData.positionImage.y;
                    depthPoint[0].z = jointData.positionWorld.z * 1000;
                    projection.MapDepthToColor(depthPoint, colorPoint);

                    AddEllipse(CanvasFaceParts,
                        new Point(colorPoint[0].x, colorPoint[0].y),
                        5, Brushes.Green);
                }


            }
        }

        /// <summary>
        /// 円を表示する
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="point"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="thickness"></param>
        void AddEllipse(Canvas canvas, Point point, int radius, Brush color, int thickness = 1)
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
                ellipse.Stroke = color;
                ellipse.StrokeThickness = thickness;
            }

            Canvas.SetLeft(ellipse, point.X);
            Canvas.SetTop(ellipse, point.Y);
            canvas.Children.Add(ellipse);
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
            String[] chordProgress = { "C", "Am", "F", "G", "Em", "F", "G", "C" }; //背景楽曲のコード進行配列
            MidiData midiData = new MidiData();
            MidiTrack track = new MidiTrack(); //各楽器が見る楽譜
            midiData.Tracks.Add(track); //midiDataにtrackを対応付け

            int tick = 0;
            //コード進行配列(chordProgress)から根音(root)を決定する
            foreach (String chordName in chordProgress)
            {
                //コードの根音を取得
                String structure = "";
                byte root = 60;

                getStructure(chordName, out root, out structure);

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
        public void getStructure(String chordName, out byte root, out String structure)
        {
            //chordNameをmidiナンバーに変換し，rootに格納
            if (chordName.StartsWith("C"))
            {
                structure = chordName.Remove(0, 1);
                root = 60;
            }
            else if (chordName.StartsWith("C#") || chordName.StartsWith("Db"))
            {
                structure = chordName.Remove(0, 2);
                root = 61;
            }
            else if (chordName.StartsWith("D"))
            {
                structure = chordName.Remove(0, 1);
                root = 62;
            }
            else if (chordName.StartsWith("D#") || chordName.StartsWith("Eb"))
            {
                structure = chordName.Remove(0, 2);
                root = 63;
            }
            else if (chordName.StartsWith("E"))
            {
                structure = chordName.Remove(0, 1);
                root = 64;
            }
            else if (chordName.StartsWith("F"))
            {
                structure = chordName.Remove(0, 1);
                root = 65;
            }
            else if (chordName.StartsWith("F#") || chordName.StartsWith("Gb"))
            {
                structure = chordName.Remove(0, 2);
                root = 66;
            }
            else if (chordName.StartsWith("G"))
            {
                structure = chordName.Remove(0, 1);
                root = 55;
            }
            else if (chordName.StartsWith("G#") || chordName.StartsWith("Ab"))
            {
                structure = chordName.Remove(0, 2);
                root = 56;
            }
            else if (chordName.StartsWith("A"))
            {
                structure = chordName.Remove(0, 1);
                root = 57;
            }
            else if (chordName.StartsWith("A#") || chordName.StartsWith("Bb"))
            {
                structure = chordName.Remove(0, 2);
                root = 58;
            }
            else if (chordName.StartsWith("B"))
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
        public void showAllStructure()
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
        public void setRange(int Range)
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
    }
}