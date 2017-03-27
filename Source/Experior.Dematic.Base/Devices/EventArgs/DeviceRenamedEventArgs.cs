using System;

namespace Experior.Dematic.Base.Devices
{
    public class DeviceRenamedEventArgs : EventArgs
    {
        public readonly string OldName, NewName;
        public readonly DeviceInfo DevInfo;

        public DeviceRenamedEventArgs(DeviceInfo deviceInfo, string oldName, string newName)
        {
            OldName = oldName;
            NewName = newName;
            DevInfo = deviceInfo;
        }
    }
}
