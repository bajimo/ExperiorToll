using System;

namespace Experior.Dematic.Base.Devices
{
    public class SizeUpdateEventArgs : EventArgs
    {
        public readonly float? _width, _length, _radius;

        public SizeUpdateEventArgs(float? length, float? width, float? radius)
        {
            _width = width;
            _length = length;
            _radius = radius;
        }
    }
}
