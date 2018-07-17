using NextMidi.Data;
using NextMidi.Data.Domain;
using NextMidi.Data.Track;
using NextMidi.DataElement;
using NextMidi.MidiPort.Output;
using NextMidi.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace EnsembleCommander
{
    class MidiManager
    {
        public MidiOutPort port;

        /// <summary>
        /// MIDIファイルのデータ境界を定める
        /// </summary>
        public MidiFileDomain domain;
        /// <summary>
        /// MIDIデータを管理する
        /// </summary>
        private MidiData midiData;

        /// <summary>
        /// MidiTrackの配列（モードの数だけ用意）
        /// </summary>
        public MidiTrack[] tracks;
        /// <summary>
        /// 「コード進行を表わすリスト」の配列（モードの数だけ用意）
        /// </summary>
        public List<Chord>[] chordProgList;
        /// <summary>
        /// モードのリスト
        /// </summary>
        public string[] ModeList = new string[]
        {
            "WHOLETONE",
            "QUARTERTONE",
            "ARPEGGIO",
            "DELAY",
            "FREE"
        };

        //public string[] inputedChord = { "C", "Am", "F", "G", "Em", "F", "G", "C" }; //背景楽曲のコード進行配列
        //public string[] inputedChord = { "C", "Am", "F", "G", "Em", "F", "G", "C", "C", "Am", "F", "G", "Em", "F", "G", "C" }; //背景楽曲のコード進行配列
        //public string[] inputedChord = { "C", "Em", "F", "G", "Em", "Am", "Dm", "G", "Am", "Em", "F", "Em", "Dm", "C", "G", "C" }; //背景楽曲のコード進行配列 
        //public string[] inputedChord = { "C", "G", "Am", "Em", "F", "C", "F", "G" }; //背景楽曲のコード進行配列 
        public string[] inputedChord = { "D", "A", "Bm", "F#m", "G", "D", "G", "A" }; //背景楽曲のコード進行配列 
        //public string[] inputedChord = { "F", "Fm", "Em", "A7" }; //白松研においでよ
        public byte KeyNoteNumber = 60;
        //public string KeyNote = "C";//将来的に使うかもしれない

        /// <summary>
        /// KeyNoteのNoteNumberを取得
        /// </summary>
        /// <param name="KeyNote"></param>
        /// <returns></returns>
        public byte StringToKeyNum(string KeyNote)
        {
            byte KeyNoteNumber = 60;
            switch (KeyNote)
            {
                case "C":
                    KeyNoteNumber = 60;
                    break;
                case "C#":
                    KeyNoteNumber = 61;
                    break;
                case "Db":
                    KeyNoteNumber = 61;
                    break;
                case "D":
                    KeyNoteNumber = 62;
                    break;
                case "D#":
                    KeyNoteNumber = 63;
                    break;
                case "Eb":
                    KeyNoteNumber = 63;
                    break;
                case "E":
                    KeyNoteNumber = 64;
                    break;
                case "F":
                    KeyNoteNumber = 65;
                    break;
                case "F#":
                    KeyNoteNumber = 66;
                    break;
                case "Gb":
                    KeyNoteNumber = 66;
                    break;
                case "G":
                    KeyNoteNumber = 67;
                    break;
                case "G#":
                    KeyNoteNumber = 68;
                    break;
                case "Ab":
                    KeyNoteNumber = 68;
                    break;
                case "A":
                    KeyNoteNumber = 69;
                    break;
                case "A#":
                    KeyNoteNumber = 70;
                    break;
                case "Bb":
                    KeyNoteNumber = 70;
                    break;
                case "B":
                    KeyNoteNumber = 71;
                    break;
            }
            return KeyNoteNumber;
        }

        //MODE用定数
        private const int MODE_WHOLE = 0;
        private const int MODE_QUARTER = 1;
        private const int MODE_ARPEGGIO = 2;
        private const int MODE_DELAY = 3;
        private const int MODE_FREE = 4;

        public const int TICK_UNIT = 240 * 4;

        /// <summary>
        /// MIDIの初期化
        /// </summary>
        /// <param name="portnum"></param>
        public MidiManager()
        {
            int NumOfMode = ModeList.Length;
            //モードの数だけトラック配列の要素数を用意する
            tracks = new MidiTrack[NumOfMode];
            //モードの数だけコード進行配列の要素数を用意する
            chordProgList = new List<Chord>[NumOfMode];
            //コード進行配列の初期化
            for (int i = 0; i < NumOfMode; i++)
            {
                tracks[i] = new MidiTrack();
                chordProgList[i] = new List<Chord>();
            }

            //Modeの数だけコード進行配列の初期化
            for (int mode = 0; mode < NumOfMode; mode++)
            {
                //コードの開始位置を0に初期化
                int ChordTickFromStart = 0;
                //入力コード進行(chordProgress)からコード進行リストを初期化する
                foreach (String chordName in inputedChord)
                {
                    //Chordの初期化
                    Chord chord = new Chord(chordName, ChordTickFromStart, mode); //Chordをインスタンス化
                    chordProgList[mode].Add(chord); //Modeに対応するインデックスのコード進行配列chordを追加
                    ChordTickFromStart += chord.Gate; //次のchordの開始タイミングにする

                    //Trackの初期化
                    tracks[mode].Insert(chord.Base); //ベース音の挿入
                    foreach (var note in chord.NoteList) tracks[mode].Insert(note); //伴奏音の挿入
                }
            }
            port = new MidiOutPort(0);
            try
            {
                port.Open();
            }
            catch
            {
                Console.WriteLine("no such port exists");
                return;
            }

            //midiDataにtrackを対応付け
            midiData = new MidiData();
            midiData.Tracks.Add(tracks[MODE_WHOLE]);
            // テンポマップを作成
            domain = new MidiFileDomain(midiData);
        }

        /// <summary>
        /// ユーザ指定したRangeにコードを転回してPivotRangeを移動する
        /// </summary>
        public void SetRange(int Range, MusicTime time, int mode)
        {
            // 小節のコードのPivotRangeとユーザが指定したRangeとの差分だけ転回
            for (int measure = time.Measure; measure < chordProgList[mode].Count; measure++)
            {
                //ユーザが指定したRangeとの差分だけ転回
                Turn(Range - chordProgList[mode][measure].PivotRange, measure, chordProgList[mode][measure]);
            }
        }

        /// <summary>
        /// 転回メソッド : i小節目のコードをk回だけ転回する
        /// </summary>
        /// <param name="times"></param>
        /// <param name="measure"></param>
        private void Turn(int times, int measure, Chord chord)
        {
            //times回展開する
            if (times > 0)
            {
                for (int i = 0; i < times; i++)
                {
                    // NoteListの中でnoteナンバーが一番小さいものを一オクターブ上げる
                    byte min = byte.MaxValue;
                    for (int j = 0; j < chord.NoteList.Count; j++)
                    {
                        if (chord.NoteList[j].Note < min)
                        {
                            min = chord.NoteList[j].Note;
                        }
                    }
                    foreach (var note in chord.NoteList) if (note.Note == min) note.Note += 12;
                }
            }
            else
            {
                for (int i = 0; i < -times; i++)
                {
                    // NoteListの中でnoteナンバーが一番大きいを一オクターブ下げる
                    byte max = byte.MinValue;
                    for (int j = 0; j < chord.NoteList.Count; j++)
                    {
                        if (chord.NoteList[j].Note > max)
                        {
                            max = chord.NoteList[j].Note;
                        }
                    }
                    foreach (var note in chord.NoteList) if (note.Note == max) note.Note -= 12;
                }
            }

            // Pivotの更新
            chord.Pivot += times * 4;
            chord.SetRange();
            //転回数の更新
            chord.NumOfTurn += times;
        }

        /// <summary>
        /// 自由なタイミングに和音
        /// </summary>
        public void SetOnNote(MusicTime time)
        {
            //タップタイミングの1ms後のタイミングに演奏音を書き換え
            if (time.Measure < inputedChord.Length)
                foreach (var note in chordProgList[MODE_FREE][time.Measure].NoteList)
                {
                    note.Tick = TICK_UNIT * time.Measure + time.Tick + 1;
                    note.Velocity = 80;
                    note.Gate = 240;
                    note.Speed = 120;
                }
        }

        /// <summary>
        /// 演奏中のトラックと入力したモードのトラックを入れ替える
        /// </summary>
        /// <param name="modeName"></param>
        public void ExchangeTrack(int mode)
        {
            midiData.Tracks.Add(tracks[mode]);
            midiData.Tracks.RemoveAt(0);
        }

        /* MEMO
         * 
         * 将来的にユーザがコード進行を作っていく場合に、
         * 曲内で使われるコードはダイアトニックコード内から選択されることを想定している。
         * その場合、システムがダイアトニックコードを決定するためにユーザは最初に曲のKey(調)を指定することになる。
         * その入力をKeyNoteとする。
         * (参考)ダイアトニックコードについて：https://www.studiorag.com/blog/fushimiten/diatonic-chord?disp=more
         * 
         * ただしメジャーダイアトニックコード⇔マイナーダイアトニックコードの変換だと不自然に聞こえたため、今回は以下の変換を行った。
         * Ⅰ, Ⅱm, Ⅲm, Ⅳ, Ⅴ, Ⅵm, Ⅶm-5
         * ↓
         * Ⅰm, Ⅱm-5, Ⅲm, Ⅳm, Ⅴ, Ⅵm-5, Ⅶm-5
         * 
         */
        /// <summary>
        /// コードを長調(明るい)に書き換える
        /// </summary>
        public void TurnMajor(MusicTime time, int mode)
        {

            for (int i = 0; i < 5; i++)
            {
                // 以降のコードをメジャーに書き換える
                for (int measure = time.Measure; measure < chordProgList[mode].Count; measure++)
                {
                    if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12) //1
                    {
                        chordProgList[mode][measure].NoteList[1 + (3 * i)].Note += 1;

                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 2) //2
                    {
                        chordProgList[mode][measure].NoteList[2 + (3 * i)].Note += 1;
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 4) //3
                    {
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 5) //4
                    {
                        chordProgList[mode][measure].NoteList[1 + (3 * i)].Note += 1;
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 7) //5
                    {
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 9) //6
                    {
                        chordProgList[mode][measure].NoteList[2 + (3 * i)].Note += 1;
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 11) //7
                    {
                    }
                }
                if (mode == 0 || mode == 2 || mode == 4) break; //Whole, Arpeggio, Free なら1ループで抜け出す．
                if (mode == 1 && i == 3) break; //Quarterは4回音が鳴るので，4ループで抜け出す．
            }
        }

        /// <summary>
        /// コードを短調(暗い)に書き換える
        /// </summary>
        public void TurnMinor(MusicTime time, int mode)
        {

            for (int i = 0; i < 5; i++) // 1小節内で複数回の音を鳴らす場合があるためループ(最大がDelayモードの5回)
            {
                // 以降のコードをマイナーに書き換える
                for (int measure = time.Measure; measure < chordProgList[mode].Count; measure++)
                {
                    if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12) //1→1m
                    {
                        chordProgList[mode][measure].NoteList[1 + (3 * i)].Note -= 1;

                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 2) //2m→2m(-5)
                    {
                        chordProgList[mode][measure].NoteList[2 + (3 * i)].Note -= 1;
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 4) //3m→3m
                    {
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 5) //4→4m
                    {
                        chordProgList[mode][measure].NoteList[1 + (3 * i)].Note -= 1;
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 7) //5→5
                    {
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 9) //6m→6m(-5)
                    {
                        chordProgList[mode][measure].NoteList[2 + (3 * i)].Note -= 1;
                    }
                    else if (chordProgList[mode][measure].NoteList[0].Note % 12 == KeyNoteNumber % 12 + 11) //7m(-5)→7m(-5)
                    {
                    }
                }
                if (mode == 0 || mode == 2 || mode == 4) break; //Whole, Arpeggio, Free なら1ループで抜け出す．
                if (mode == 1 && i == 3) break; //Quarterは4回音が鳴るので，4ループで抜け出す．
            }

        }

    }
}
