using Experior.Core.Loads;
using System;

namespace Experior.Dematic.Devices
{
    public class ConveyorExitLoadEventArgs : EventArgs
    {
        public readonly Load Load;
        public ConveyorExitLoadEventArgs(Load load)
        {
            Load = load;
        }
    }
}
