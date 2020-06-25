using MIDIModificationFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris
{
    public class ArrayPattern : IPattern
    {
        public IEnumerable<Note>[] Notes => NoteArrays;
        public Note[][] NoteArrays { get; }

        public ArrayPattern(Note[][] arrays)
        {
            NoteArrays = arrays;
        }
    }
}
