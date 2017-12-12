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
    class MIDIManager
    {
        private MidiOutPort port;

        /// <summary>
        /// MIDIファイルのデータ境界を定める
        /// </summary>
        private MidiFileDomain domain;
        /// <summary>
        /// MIDIデータを管理する
        /// </summary>
        private MidiData midiData;
        
        /// <summary>
        /// MIDI再生用オブジェクト
        /// </summary>
        public MidiPlayer player;
        /// <summary>
        /// 現在の演奏地点を記録する
        /// </summary>
        private MusicTime currentTime;

        /// <summary>
        /// モードを入力するとトラックが出力される
        /// </summary>
        private Dictionary<string, MidiTrack> ModeToTrack = new Dictionary<string, MidiTrack>();
        
        /// <summary>
        /// コード進行の各コードのリスト
        /// </summary>
        List<Chord> chordList = new List<Chord>();

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

        /// <summary>
        /// MIDIの初期化
        /// </summary>
        /// <param name="portnum"></param>
        public MIDIManager(int portnum)
        {
            //モードとトラックを対応付ける
            foreach (var modeName in ModeList)
            {
                ModeToTrack[modeName] = new MidiTrack();
            }

            //String[] chordProgress = { "C", "Am", "F", "G", "Em", "F", "G", "C" }; //背景楽曲のコード進行配列
            String[] chordProgress = { "D", "A", "Bm", "F#m", "G", "D", "G", "A" }; //背景楽曲のコード進行配列 
            
            //コードの開始位置を0に初期化
            int ChordTickFromStart = 0;
            //コード進行配列(chordProgress)から根音(root)を決定する
            foreach (String chordName in chordProgress)
            {
                // newでインスタンスを作成し、変数chordに格納
                Chord chord = new Chord(chordName,ChordTickFromStart);

                ModeToTrack["WHOLETONE"].Insert(chord.Base);//ベース音の登録
                foreach (var notes in chord.NotesList)
                    foreach (var note in notes) ModeToTrack["WHOLETONE"].Insert(note);

                chordList.Add(chord); // chordをリストchordlistに追加
                ChordTickFromStart += chord.Gate; //次のchordの開始タイミングにする
            }

            midiData = new MidiData();
            midiData.Tracks.Add(ModeToTrack["WHOLETONE"]); //midiDataにtrackを対応付け

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
            //player.Stopped += Player_Stopped;
        }

        public void PlayMIDI()
        {
            player.Stop();
            currentTime = player.MusicTime;
            player.Play(domain);
        }

        public void StopMIDI()
        {
            player.Stop();
        }

        public bool IsPlaying()
        {
            return !(player == null || player.Playing);
        }

        /// <summary>
        /// 各chordlistの各構成音を表示
        /// </summary>
        public void ShowAllStructure()
        {
            /*
            for (int i = 0; i < chordlist.Count; i++)
            {
                Console.WriteLine("chordlist[" + i + "]");
                Console.WriteLine("Note[0]=" + chordlist[i].Notes[0].Note);
                Console.WriteLine("Note[1]=" + chordlist[i].Notes[1].Note);
                Console.WriteLine("Note[2]=" + chordlist[i].Notes[2].Note);
                Console.WriteLine("Pivot=" + chordlist[i].Pivot);
                Console.WriteLine("PivotRange=" + chordlist[i].PivotRange + "\n");
            }
            */
        }

        /// <summary>
        /// ユーザ指定したRangeにコードを転回してPivotRangeを移動する
        /// </summary>
        public void SetRange(int Range)
        {
            MusicTime current = player.MusicTime; // 現在の演奏カーソルを取得

            // 次の小節のコードのPivotRangeとユーザが指定したRangeとの差分だけ転回
            // 次の小節からそれ以降の小節まで
            for (int i = current.Measure; i < chordList.Count; i++)
            {
                //ユーザが指定したRangeとの差分だけ転回
                Turn(Range - chordList[i].PivotRange, i);
            }
        }

        /// <summary>
        /// 転回メソッド : i小節目のコードをk回だけ転回する
        /// </summary>
        /// <param name="k"></param>
        /// <param name="i"></param>
        private void Turn(int k, int i)
        {/*
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
            */
        }

        /// <summary>
        /// コード進行を全音表記に
        /// </summary>
        public void SetWholeTone()
        {
            foreach (var chord in chordList)
            {
                /*
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
                */
            }
        }

        /// <summary>
        /// 自由なタイミングに和音
        /// </summary>
        public void SetOnNote()
        {
            //タップタイミングの1ms後のタイミングに演奏音を書き換え
            MusicTime current = player.MusicTime; // 現在(Tap時)の演奏カーソルを取得

            foreach (var notes in chordList[current.Measure].NotesList)
            {
                foreach (var note in notes)
                {
                    //note.Tick = tickUnit * current.Measure + current.Tick + 1;
                    note.Velocity = 80;
                    note.Gate = 240;
                    note.Speed = 120;
                }
            }
        }

        /// <summary>
        /// 入力されたモードに対応するトラックにNoteEventを挿入する
        /// </summary>
        /// <param name="modeName"></param>
        /// <returns></returns>
        public void Insert(string modeName, NoteEvent note)
        {
            ModeToTrack[modeName].Insert(note);
        }

        /// <summary>
        /// 入力されたモードに対応するトラックを得る
        /// </summary>
        /// <param name="modeName"></param>
        /// <returns></returns>
        public MidiTrack GetTrack(string modeName)
        {
            return ModeToTrack[modeName];
        }

        /// <summary>
        /// 入力したモードのトラックをMuteにする
        /// </summary>
        /// <param name="modeName"></param>
        public void SetMute(string modeName)
        {
            //ModeToTrack[modeName];
        }

        /// <summary>
        /// 演奏中のトラックと入力したモードのトラックを入れ替える
        /// </summary>
        /// <param name="modeName"></param>
        public void ExchangeTrack(string modeName)
        {

        }
    }
}
