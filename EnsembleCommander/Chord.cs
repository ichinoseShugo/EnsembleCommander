using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NextMidi.DataElement;

namespace EnsembleCommander
{ 
    class Chord
    {
        /// <summary>
        /// このコードで演奏されるノート
        /// </summary>
        public List<NoteEvent> Notes = new List<NoteEvent>();
        /// <summary>
        /// 根音から2オクターブ下がった音
        /// </summary>
        public NoteEvent Base;
        /// <summary>
        /// コード全体の演奏開始時間（初期化時点では小節の頭）
        /// </summary>
        public int TickFromStart;
        /// <summary>
        /// ベース音以外のコードの和音数
        /// </summary>
        public int NoteCount;
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

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="root"></param>
        /// <param name="mode"></param>
        public Chord(int tick, byte root, String structure)
        {
            //コードの演奏開始時刻を設定
            TickFromStart = tick;

            //Base音の設定
            Base = new NoteEvent((byte)(root - 24), 80, 240 * 4);//音高，音量，長さ
            Base.Tick = tick;//Base音の開始タイミングを指定

            byte[] numbers=null;

            //コードの構造から各和音の音高を決定
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
                //コードの和音数を記録
                NoteCount = numbers.Length;
                for (int i = 0; i < numbers.Length; i++)
                {
                    Notes.Add(new NoteEvent(numbers[i], 80, 240 * 4)
                    {
                        Tick = tick
                    });
                }
            }

            //平均を計算
            foreach (var note in Notes) Pivot += note.Note;
            Pivot /= Notes.Count;

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
    }
}
