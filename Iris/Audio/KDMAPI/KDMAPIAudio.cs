using MIDIModificationFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iris.Audio.KDMAPI
{
    class KDMAPIAudio : IAudioPreview
    {
        public event EventHandler SourcingFinished;
        public event EventHandler<ViewRenderedArgs> RenderedFrame;

        public void SetNoteSource(IEnumerable<MIDIEvent> notes)
        {
            throw new NotImplementedException();
        }

        public void StopSourcing()
        {
            throw new NotImplementedException();
        }
    }
}
