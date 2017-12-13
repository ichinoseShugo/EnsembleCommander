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
        string[] ModeList = new string[]
        {
            "WHOLETONE",
            "QUARTERTONE",
            "ARPEGGIO",
            "FREE"
        };
        public string[] inputedChord = { "C", "Am", "F", "G", "Em", "F", "G", "C" }; //背景楽曲のコード進行配列
        //public string[] inputedChord = { "D", "A", "Bm", "F#m", "G", "D", "G", "A" }; //背景楽曲のコード進行配列 
        //public string[] inputedChord = { "F", "Fm", "Em", "A7" }; //白松研においでよ
        //MODE用定数
        private const int MODE_WHOLE = 0;
        private const int MODE_QUARTER = 1;
        private const int MODE_ARPEGGIO = 2;
        private const int MODE_FREE = 3;

        public const int TICK_UNIT = 240*4;

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
            for (int i=0; i<NumOfMode; i++)
            {
                tracks[i] = new MidiTrack();
                chordProgList[i] = new List<Chord>();
            }

            //Modeの数だけコード進行配列の初期化
            for (int mode=0; mode<NumOfMode; mode++) {
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
        public void SetRange(int Range, MusicTime time ,int mode)
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
                    for(int j=0; j < chord.NoteList.Count; j++)
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
            if(time.Measure<inputedChord.Length)
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

    }
}
