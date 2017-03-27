using Experior.Core.Loads;
using System;

namespace Experior.Dematic.Devices
{
    public class ConveyorEnterLoadEventArgs : EventArgs
    {
        public readonly Load Load;
        public ConveyorEnterLoadEventArgs(Load load)
        {
            Load = load;
        }
    }
}
