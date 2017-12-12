using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NextMidi.DataElement;
using NextMidi.Data.Domain;
using NextMidi.Data.Track;
using System.Text.RegularExpressions;

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

        /// <summary>
        /// 正規表現によるルート音(A,C#,Dbなど)のパターン
        /// </summary>
        private Regex RootNameP = new Regex("^[ABCDEFG]+[b#]*", RegexOptions.Compiled);
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="tick"></param>
        /// <param name="root"></param>
        /// <param name="mode"></param>
        public Chord(string chordName, int tickFromStart)
        {
            //コードの開始時間を設定
            TickFromStart = tickFromStart;

            //コードの根音の音高(root)と構成(structure)を取得
            GetStructure(chordName, out byte root, out string structure);

            //rootとstructureからコードの構成音の配列(Elements)を作成
            byte[] numbers = GetElementsConstitute(root, structure);
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

            //Base音の設定
            Base = new NoteEvent((byte)(root - 24), 80, 240 * 4)//音高，音量，長さ
            {
                Tick = tickFromStart//Base音の開始タイミングを指定
            };

            //Chordの長さはBase音の長さとする
            Gate = Base.Gate;

            //WholeToneモード用にNotesListを初期化
            SetWholeToneMode();

            //PivotとPivotRangeを求める
            SetPivot();
        }

        /// <summary>
        /// コードネームからNoteナンバー(byte型)と構造(string型)を決定
        /// </summary>
        /// <param name="chordName"></param>
        /// <returns></returns>
        private void GetStructure(String chordName, out byte root, out String structure)
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
            if (rootName == "C")
            {
                structure = chordName.Remove(0, 1);
                root = 60;
            }
            else if (rootName == "C#" || rootName == "Db")
            {
                structure = chordName.Remove(0, 2);
                root = 61;
            }
            else if (rootName == "D")
            {
                structure = chordName.Remove(0, 1);
                root = 62;
            }
            else if (rootName == "D#" || rootName == "Eb")
            {
                structure = chordName.Remove(0, 2);
                root = 63;
            }
            else if (rootName == "E")
            {
                structure = chordName.Remove(0, 1);
                root = 64;
            }
            else if (rootName == "F")
            {
                structure = chordName.Remove(0, 1);
                root = 65;
            }
            else if (rootName == "F#" || rootName == "Gb")
            {
                structure = chordName.Remove(0, 2);
                root = 66;
            }
            else if (rootName == "G")
            {
                structure = chordName.Remove(0, 1);
                root = 55;
            }
            else if (rootName == "G#" || rootName == "Ab")
            {
                structure = chordName.Remove(0, 2);
                root = 56;
            }
            else if (rootName == "A")
            {
                structure = chordName.Remove(0, 1);
                root = 57;
            }
            else if (rootName == "A#" || rootName == "Bb")
            {
                structure = chordName.Remove(0, 2);
                root = 58;
            }
            else if (rootName == "B")
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
        /// コードの構造から構成音の音高(Elements)を決定
        /// </summary>
        /// <param name="structure"></param>
        /// <param name="root"></param>
        /// <returns></returns>
        private byte[] GetElementsConstitute(byte root, string structure)
        {
            switch (structure)
            {
                case "":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 7) };
                case "6":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 9) };
                case "7":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 10) };
                case "M7":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 11) };
                case "m":
                    return new byte[] { root, (byte)(root + 3), (byte)(root + 7) };
                case "m6":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 9) };
                case "m7":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 7), (byte)(root + 10) };
                case "m7-5":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 6), (byte)(root + 10) };
                case "dim":
                    return new byte[] { root, (byte)(root + 3), (byte)(root + 6) };
                case "sus":
                    return new byte[] { root, (byte)(root + 5), (byte)(root + 7) };
                case "aug":
                    return new byte[] { root, (byte)(root + 4), (byte)(root + 8) };
                default:
                    Console.WriteLine("設定していないコードの構造:" + structure);
                    return null;
            }
        }
        
        /// <summary>
        /// PivotとPivotRangeを求める
        /// </summary>
        private void SetPivot()
        {
            //平均を計算
            foreach (var element in Elements) Pivot += element.Note;
            Pivot /= 3;

            //Noteナンバーの範囲[0-127]を4つずつ32領域に分割をする
            //ただし伴奏で使う範囲（一度に画面に表示される演奏領域）の数は5と想定

            //計算したPivotがどのPivotRangeに配属されるかを調べる
            for (int rangeIndex = 0; rangeIndex < 31; rangeIndex++)
            {
                if (rangeIndex * 4 <= Pivot && Pivot < (rangeIndex + 1) * 4)
                {
                    PivotRange = rangeIndex;
                    break;
                }
            }
        }

        /// <summary>
        /// Elements配列からWholeTone用のNotesListを作成する
        /// </summary>
        private void SetWholeToneMode()
        {
            //NoteListをすべて削除
            NotesList.Clear();
            //Elementsの中身はWholeToneのNotes配列なのでコピーをリストに追加
            NotesList.Add((NoteEvent[])Elements.Clone());
        }

        /// <summary>
        /// Elements配列からQuaterTone用のNotesListを作成する
        /// </summary>
        private void SetQuaterTone()
        {

        }

        /// <summary>
        /// Elements配列からArppegio用のNotesListを作成する
        /// </summary>
        private void SetArppegioMode()
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

        /// <summary>
        /// Elements配列からFree用のNotesListを作成する
        /// </summary>
        private void SetFreeMode()
        {

        }
    }
}
