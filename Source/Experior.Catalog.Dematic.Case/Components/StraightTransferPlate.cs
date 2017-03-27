using Experior.Core.Parts;
using System;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Drawing;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class StraightTransferPlate : StraightConveyor
    {
        public StraightTransferPlateInfo straightTransferPlateInfo;
        
        public StraightTransferPlate(StraightTransferPlateInfo info) : base(info)
        {
            straightTransferPlateInfo = info;
        }

        public override void StartFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e) 
        {
            //Get the load waiting status of the previous conveyor and set as this conveyors load waiting status
            //This load waiting will mirror the load waiting status of the previous conveyor (merge/Divert)
            PreviousConveyor = stranger.Parent as IRouteStatus;
            PreviousLoadWaiting = PreviousConveyor.GetLoadWaitingStatus(stranger);
            SetLoadWaiting(PreviousLoadWaiting.LoadWaiting, false, null);
            PreviousLoadWaiting.OnLoadWaitingChanged += PreviousLoadWaiting_OnLoadWaitingChanged;
        }

        void PreviousLoadWaiting_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            SetLoadWaiting(e._loadWaiting, e._loadDeleted, e._waitingLoad);
        }

        public override void StartFixPoint_OnUnSnapped(FixPoint stranger) 
        {
         //   PreviousConveyor = null;
            PreviousLoadWaiting.OnLoadWaitingChanged -= PreviousLoadWaiting_OnLoadWaitingChanged;
            PreviousLoadWaiting = null;
        }

        public override void EndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            if (stranger.Type != FixPoint.Types.End)
            {
                //Get the available status of the next conveyor and set as this conveyors available status
                //This route available status will mirror the route available status of the next conveyor (merge/divert)
                NextConveyor = stranger.Parent as IRouteStatus;
                NextRouteStatus = NextConveyor.GetRouteStatus(stranger);// .GetAvailableStatus(stranger);
                RouteAvailable = NextRouteStatus.Available;
                //ThisRouteStatus.Available = NextRouteStatus.Available;
                NextRouteStatus.OnRouteStatusChanged += NextRouteStatus_OnAvailableChanged;
            }
            else
            {
                Core.Environment.Log.Write("WARNING can't snap an end to and end; turn your conveyor arround", Color.Orange);
            }
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e) 
        {
            RouteAvailable = e._available;

            if (e._available == RouteStatuses.Available)
                arrow.Color = Color.Green;
            else if (e._available == RouteStatuses.Request)
                arrow.Color = Color.Yellow;
            else
                arrow.Color = Color.Red;

            //ThisRouteStatus.Available = e._available;
        }

        public override void EndFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            ThisRouteStatus = new RouteStatus();
            NextRouteStatus.OnRouteStatusChanged -= NextRouteStatus_OnAvailableChanged;
            NextConveyor = null;
            NextRouteStatus = null;
        }

        [Category("Size and Speed")]
        [DisplayName("Width")]
        [Browsable(true)]
        public override float Width
        {
            get
            {
                return base.Width;
            }
            set
            {
                base.Width = value;
            }
        }

        [Browsable(false)]
        public override CaseConveyorWidth ConveyorWidth
        {
            get { return base.ConveyorWidth; }
            set { base.ConveyorWidth = value; }
        }

    }

    [Serializable]
    [XmlInclude(typeof(StraightTransferPlateInfo))]
    public class StraightTransferPlateInfo : StraightConveyorInfo
    {
        //Specific case straight belt conveyor info to be added here, none yet!
    }
}
