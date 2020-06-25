using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MIDIModificationFramework;

namespace Iris
{
    interface IPattern
    {
        IEnumerable<Note>[] Notes { get; }
    }

    public static class BasicPatternExtensions
    {
        public static ArrayPattern ToArrayPatten(this Pattern pattern)
        {
            return new ArrayPattern(pattern.Notes.Select(n => n.ToArray()).ToArray());
        }
    }
}
