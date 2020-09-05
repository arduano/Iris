using MIDIModificationFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris.Audio
{
    interface IAudioPreview
    {
        void SetNoteSource(IEnumerable<MIDIEvent> notes);
        void StopSourcing();
    }
}
