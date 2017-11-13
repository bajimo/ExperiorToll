using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    class MHEControl_PickPutStation : MHEControl
    {
        private PickPutStationDatcomAusInfo transferDatcomInfo;
        private PickPutStation theLift;
        private MHEControllerAUS_Case casePLC;

        public MHEControl_PickPutStation(PickPutStationDatcomAusInfo info, PickPutStation lift)
        {
            Info               = info;  // set this to save properties 
            transferDatcomInfo = info;
            theLift            = lift;
            casePLC            = lift.Controller as MHEControllerAUS_Case;

            //Add event subscriptions here
            theLift.OnArrivedAtRightPosition += TheLiftOnArrivedAtRightPosition;
            theLift.OnArrivedAtLeftPosition += TheLiftOnArrivedAtLeftPosition;
            theLift.RightStationName = RightPositionName;
            theLift.LeftStationName = LeftPositionName;
            casePLC.OnTransportOrderTelegramReceived += CasePLC_OnTransportOrderTelegramReceived;  
        }

        private void CasePLC_OnTransportOrderTelegramReceived(object sender, MessageEventArgs e)
        {
            if (e.Load == null)
                return;

            if (e.Location == RightPositionName && e.Load == theLift.RightLoad)
            {
                e.Load.Release();
            }
            else if (e.Location == LeftPositionName && e.Load == theLift.LeftLoad)
            {
                e.Load.Release();
            }
        }

        public override void Dispose()
        {
            //Add event un-subscriptions here
            theLift.OnArrivedAtRightPosition -= TheLiftOnArrivedAtRightPosition;
            theLift.OnArrivedAtLeftPosition -= TheLiftOnArrivedAtLeftPosition;
            casePLC.OnTransportOrderTelegramReceived -= CasePLC_OnTransportOrderTelegramReceived;
            theLift = null;
            transferDatcomInfo = null;
        }

        private void Load_OnDisposed(Core.Loads.Load load)
        {
            load.OnDisposing -= Load_OnDisposed;
            load.OnReleased -= Load_OnReleased;
            if (load == theLift.LeftLoad)
            {
                casePLC.SendExceptionMessage(LeftPositionName, ((Case_Load)load), "MD");
                return;
            }
            if (load == theLift.RightLoad)
            {
                casePLC.SendExceptionMessage(RightPositionName, ((Case_Load)load), "MD");
            }

        }

        private void Load_OnReleased(Core.Loads.Load load)
        {
            load.OnDisposing -= Load_OnDisposed;
            load.OnReleased -= Load_OnReleased;
        }

        private void TheLiftOnArrivedAtRightPosition(object sender, PickPutStationArrivalArgs e)
        {
            e.Load.OnDisposing += Load_OnDisposed;
            e.Load.OnReleased += Load_OnReleased;
            casePLC.SendArrivalMessage(RightPositionName, ((Case_Load)e.Load));
        }

        private void TheLiftOnArrivedAtLeftPosition(object sender, PickPutStationArrivalArgs e)
        {
            e.Load.OnDisposing += Load_OnDisposed;
            e.Load.OnReleased += Load_OnReleased;
            casePLC.SendArrivalMessage(LeftPositionName, ((Case_Load)e.Load));
        }

        [DisplayName("Right Position Name")]
        [Description("Name of the Right Hand Side Conveyor - from picker point of view")]
        [PropertyOrder(1)]
        public string RightPositionName
        {
            get { return transferDatcomInfo.RightPositionName; }
            set { transferDatcomInfo.RightPositionName = value; }
        }

        [DisplayName("Left Position Name")]
        [Description("Name of the Left Hand Side Conveyor - from picker point of view")]
        [PropertyOrder(2)]
        public string LeftPositionName
        {
            get { return transferDatcomInfo.LeftPositionName; }
            set { transferDatcomInfo.LeftPositionName = value; }
        }
    }

    [Serializable]
    [XmlInclude(typeof(PickPutStationDatcomAusInfo))]
    public class PickPutStationDatcomAusInfo : ProtocolInfo
    {
        public string RightPositionName;
        public string LeftPositionName;
    }
}