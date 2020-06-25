using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MIDIModificationFramework;
using MIDIModificationFramework.MIDIEvents;

namespace Iris
{
    public static class BasicExtensions
    {
        public static IEnumerable<Note> SetChannel(this IEnumerable<Note> notes, int channel)
        {
            foreach(var n in notes)
            {
                var nc = n.Clone();
                nc.Channel = (byte)channel;
                yield return nc;
            }
        }
    }
}
