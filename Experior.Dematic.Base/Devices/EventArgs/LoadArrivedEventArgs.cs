using Experior.Core.Loads;
using System;

namespace Experior.Dematic.Base
{
    public class LoadArrivedEventArgs : EventArgs
    {
        public readonly Load _Load;
        public LoadArrivedEventArgs(Load load)
        {
            _Load = load;
        }
    }
}
