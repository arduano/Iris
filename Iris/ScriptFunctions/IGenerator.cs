using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MIDIModificationFramework;

namespace Iris
{
    public interface IGenerator
    {
        IEnumerable<IEnumerable<MIDIEvent>> Generate();
    }
}
