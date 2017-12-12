using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NextMidi.DataElement;
using NextMidi.Data.Domain;
using NextMidi.Data.Track;

namespace EnsembleCommander
{ 
    class Chord
    {
        /// <summary>
        /// このコード内で演奏される伴奏音(和音やアルペジオを表す配列)が時系列順に入ったリスト
        /// </summary>
        public List<NoteEvent[]> NotesList = new List<NoteEvent[]>();
        /// <summary>
        /// 根音から2オクターブ下がった音
        /// </summary>
        public NoteEvent Base;
        /// <summary>
        /// コードの構成音
        /// </summary>
        public NoteEvent[] Elements;
        /// <summary>
        /// コード全体の演奏開始地点から数えた演奏開始時間
        /// </summary>
        public int TickFromStart;
        /// <summary>
        /// コード全体の小節の頭から数えた演奏開始時間
        /// </summary>
        public int TickFromMeasure;
        /// <summary>
        /// コード全体の長さ(最初の伴奏音のオンセットから最後の伴奏音のオフセットまでの長さ)
        /// </summary>
        public int Gate;
        /// <summary>
        /// コードの構成音のMidiナンバーの平均値.コードの高さ(三和音の平均値)を「コードの軸」と呼ぶことにする
        /// </summary>
        public float Pivot;
        /// <summary>
        /// コードの軸のある演奏領域(Range1～Range5).
        /// 動きの少ないコードの転回形の決定のため,1オクターブを3分割して領域を分ける.
        /// するとどのコードにおいても各領域につき一意に転回形が当てはまる.
        /// </summary>
        public int PivotRange;

        MidiTrack Track;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="root"></param>
        /// <param name="mode"></param>
        public Chord(int tickFromStart, byte root, String structure, MidiTrack track)
        {
            Track = track;
            //コードの演奏開始時刻を設定
            TickFromStart = tickFromStart;

            //Base音の設定
            Base = new NoteEvent((byte)(root - 24), 80, 240 * 4);//音高，音量，長さ
            Base.Tick = tickFromStart;//Base音の開始タイミングを指定

            //コードの構成音の音高をNoteナンバーで表した配列
            byte[] numbers=null;

            //コードの構造から構成音の音高(Elements)を決定
            switch (structure)
            {
                case "":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 7) };
                    break;
                case "6":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 9) };
                    break;
                case "7":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 10)};
                    break;
                case "M7":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 11) };
                    break;
                case "m":
                    numbers = new byte[] { root, (byte)(root + 3), (byte)(root + 7) };
                    break;
                case "m6":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 9) };
                    break;
                case "m7":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 10) };
                    break;
                case "m7-5":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 6), (byte)(root + 10) };
                    break;
                case "dim":
                    numbers = new byte[] { root, (byte)(root + 3), (byte)(root + 6) };
                    break;
                case "sus":
                    numbers = new byte[] { root, (byte)(root + 5), (byte)(root + 7) };
                    break;
                case "aug":
                    numbers = new byte[] { root, (byte)(root + 4), (byte)(root + 8) };
                    break;
                default:
                    Console.WriteLine("設定していないコードの構造:"+structure);
                    break;
            }
            if (numbers != null)
            {
                Elements = new NoteEvent[numbers.Length];
                for (int i = 0; i < numbers.Length; i++)
                {
                    Elements[i] = new NoteEvent(numbers[i], 80, 240 * 4)
                    {
                        Tick = tickFromStart
                    };
                }
            }

            //WholeToneモード用にNotesListを初期化
            SetWholeToneMode();

            //WholeToneで初期化していることを前提としたGateの求め方
            var lastnote = NotesList[0][Elements.Length - 1];
            Gate = lastnote.Tick + lastnote.Gate - tickFromStart;

            //平均を計算
            foreach (var element in Elements) Pivot += element.Note;
            Pivot /= 3;

            //Noteナンバーの範囲[0-127]を4つずつ32領域に分割をする
            //ただし伴奏で使う範囲（一度に画面に表示される演奏領域）の数は5と想定

            //計算したPivotがどのPivotRangeに配属されるかを調べる
            for (int rangeIndex=0; rangeIndex <31; rangeIndex++)
            {
                if (rangeIndex*4 <= Pivot && Pivot < (rangeIndex + 1) * 4)
                {
                    PivotRange = rangeIndex;
                    break;
                }
            }
        }


        /// <summary>
        /// Elements配列からWholeTone用のNotesListを作成する
        /// </summary>
        public void SetWholeToneMode()
        {
            //NoteListをすべて削除
            NotesList.Clear();
            //Elementsの中身はWholeToneのNotes配列なのでコピーをリストに追加
            NotesList.Add((NoteEvent[])Elements.Clone());
        }

        /// <summary>
        /// Elements配列からArppegio用のNotesListを作成する
        /// </summary>
        public void SetArppegioMode()
        {
            ClearTrack();
            //アルペジオ用のNotes配列を準備
            NoteEvent[] ArpeggioNotes = new NoteEvent[]
            {
                new NoteEvent(Elements[0].Note, 80, 240),//音高，音量，長さ
                new NoteEvent(Elements[1].Note, 80, 240),
                new NoteEvent(Elements[2].Note, 80, 240),
                new NoteEvent(Elements[0].Note, 80, 240)
            };
            for (int i = 0; i < ArpeggioNotes.Length; i++)
            {
                ArpeggioNotes[i].Tick = TickFromStart + i * 240;
            }

            foreach (var note in ArpeggioNotes)
                Track.Insert(note);

            //NoteListをすべて削除したあとNotes配列を追加
            NotesList.Clear();
            NotesList.Add(ArpeggioNotes);

        }

        private void ClearTrack()
        {
            foreach(var notes in NotesList)
                foreach (var note in notes)
                    Track.Remove(note);
        }
    }
}
