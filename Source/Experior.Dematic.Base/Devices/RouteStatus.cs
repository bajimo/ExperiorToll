using System;

namespace Experior.Dematic.Base.Devices
{
    public class RouteStatus
    {
        public event EventHandler<RouteStatusChangedEventArgs> OnRouteStatusChanged;

        private RouteStatuses _Available = RouteStatuses.Available;
        public RouteStatuses Available
        {
            get { return _Available; }
            set
            {
                _Available = value;

                if (OnRouteStatusChanged != null)
                    OnRouteStatusChanged(this, new RouteStatusChangedEventArgs(value));
            }
        }
    }

}
