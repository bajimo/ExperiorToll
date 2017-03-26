using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Experior.Dematic.Base.Devices
{
    public class RouteStatusChangedEventArgs : EventArgs
    {
        public readonly RouteStatuses _available;
        public RouteStatusChangedEventArgs(RouteStatuses available)
        {
            _available = available;
        }
    }
}
