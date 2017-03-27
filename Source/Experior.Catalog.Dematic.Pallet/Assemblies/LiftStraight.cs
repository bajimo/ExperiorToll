using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class LiftStraight : PalletStraight
    {
        public LiftStraight(LiftStraightInfo info) : base(info)
        {
        }

        public override void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._PhotocellStatus == PhotocellState.Blocked)
            {
                if (ControlType == ControlTypes.Controller && Controller != null)
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                }
                else
                {
                    //SetLoadWaiting(true, false, e._Load);

                    //Always set this conveyor blocked whenever the load arrives at the photocell, this tells the 
                    //previous conveyor that it has arrived and therefore the next load can be released into it
                    if (NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available && ThisRouteStatus.Available != RouteStatuses.Request)
                    {
                        ThisRouteStatus.Available = RouteStatuses.Blocked;
                        ThisRouteStatus.Available = RouteStatuses.Request; //This means the load can just travel into the next location
                    }
                    else
                    {
                        ThisRouteStatus.Available = RouteStatuses.Blocked;
                    }
                }

                // Always fire the event - it will not do anything however unless it has been subscribed too
                LoadArrived(new LoadArrivedEventArgs(e._Load));
            }
            else if (e._PhotocellStatus == PhotocellState.Clear)
            {
                //SetLoadWaiting(false, false, e._Load);
                ThisRouteStatus.Available = RouteStatuses.Request;

                //If the load was deleted then, it does not need to wait to transfer to the next conveyor
                if (e._LoadDeleted)
                {
                    ThisRouteStatus.Available = RouteStatuses.Available;
                }
                if (e._Load != null)
                {
                    LoadLeft(new LoadArrivedEventArgs(e._Load));
                }
            }
        }

    }

    [Serializable]
    [XmlInclude(typeof(LiftStraightInfo))]
    public class LiftStraightInfo : PalletStraightInfo
    {

    }
}
