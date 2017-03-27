using Experior.Core.Loads;
using System;

namespace Experior.Dematic.Base.Devices
{
    public class PhotocellStatusChangedEventArgs : EventArgs
    {
        public readonly PhotocellState _PhotocellStatus;
        public readonly bool _LoadDeleted;
        public readonly Load _Load;
        public PhotocellStatusChangedEventArgs(PhotocellState photocellStatus, bool loadDeleted, Load load)
        {
            _PhotocellStatus = photocellStatus;
            _LoadDeleted = loadDeleted;
            _Load = load;
        }
    }

    public class PhotocellRenamedEventArgs : EventArgs
    {
        public readonly string _OldName, _NewName;
        public readonly PhotocellInfo _PhotocellInfo;

        public PhotocellRenamedEventArgs(PhotocellInfo PhotocellInfo, string oldName, string newName)
        {
            _OldName = oldName;
            _NewName = newName;
            _PhotocellInfo = PhotocellInfo;
        }
    }
}
