using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MIDIModificationFramework;
using MIDIModificationFramework.MIDIEvents;

namespace Iris
{
    public static class Lines
    {
        public static IEnumerable<Note> BasicLine(int startKey, int count, double offset, double length, int offsetPerKey)
        {
            for(int i = 0; i < count; i++)
            {
                yield return new Note(0, (byte)(startKey + offsetPerKey * i), 127, offset * i, offset * i + length);
            }
        }
    }
}
