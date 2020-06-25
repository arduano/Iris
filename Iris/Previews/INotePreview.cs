using MIDIModificationFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris.Previews
{
    interface INotePreview
    {
        void SetNoteSource(IEnumerable<TrackNote> notes, int trackCount);
        void StopSourcing();

        double ViewTop { get; set; }
        double ViewBottom { get; set; }
        double ViewLeft { get; set; }
        double ViewRight { get; set; }
    }
}
