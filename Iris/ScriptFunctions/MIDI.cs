using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MIDIModificationFramework;

namespace Iris
{
    public class MIDI
    {
        internal static Dictionary<string, MidiFile> LoadedFiles { get; } = new Dictionary<string, MidiFile>();

        internal static void DisposeAll()
        {
            lock (LoadedFiles)
            {
                foreach (var midi in LoadedFiles.Values)
                {
                    midi.Dispose();
                }
                LoadedFiles.Clear();
            }
        }

        public static MIDI Load(string path)
        {
            lock (LoadedFiles)
            {
                if (!LoadedFiles.ContainsKey(path))
                {
                    var file = new MidiFile(path);
                    LoadedFiles.Add(path, file);
                }
                return new MIDI(LoadedFiles[path]);
            }
        }

        MidiFile midi;

        public MIDI(MidiFile midi)
        {
            this.midi = midi;
        }

        public IEnumerable<IEnumerable<MIDIEvent>> Events()
        {
            return midi.IterateTracks().Select(t => t.ChangePPQ(midi.PPQ, 1));
        }

        public IEnumerable<IEnumerable<Note>> Notes()
        {
            return Events().Select(t => t.ExtractNotes());
        }

        public IEnumerable<MIDIEvent> EventsMerged()
        {
            return Events().MergeAllTracks();
        }

        public IEnumerable<Note> NotesMerged()
        {
            return Events().Select(t => t.ExtractNotes()).MergeAll();
        }
    }
}
