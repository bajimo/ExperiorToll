using Experior.Core.Loads;
using System;

namespace Experior.Dematic.Base.Devices
{
    public class LoadWaitingChangedEventArgs : EventArgs
    {
        public readonly bool _loadWaiting;
        public readonly bool _loadDeleted;
        public readonly Load _waitingLoad;
        public LoadWaitingChangedEventArgs(bool loadWaiting, bool loadDeleted, Load waitingLoad)
        {
            _loadWaiting = loadWaiting; _loadDeleted = loadDeleted; _waitingLoad = waitingLoad;
        }
    }
}
