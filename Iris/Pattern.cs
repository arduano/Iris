using MIDIModificationFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris
{
    public class Pattern : IPattern
    {
        public IEnumerable<Note>[] Notes { get; }

        public Pattern(IEnumerable<Note>[] notes)
        {
            Notes = notes;
        }
    }
}
