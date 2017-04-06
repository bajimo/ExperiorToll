using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.DatcomUK.Assemblies
{
    class MHEControl_PickPutStation : MHEControl
    {
        private PickPutStationDatcomInfo transferDatcomInfo;
        private PickPutStation rapidPickStation;
        private CasePLC_Datcom casePLC;

        public MHEControl_PickPutStation(PickPutStationDatcomInfo info, PickPutStation pickStation)
        {
            Info               = info;  // set this to save properties 
            transferDatcomInfo = info;
            rapidPickStation   = pickStation;
            casePLC            = pickStation.Controller as CasePLC_Datcom;

            //Add event subscriptions here
            rapidPickStation.OnArrivedAtRightPosition += RapidPickStationOnArrivedAtRightPosition;
            rapidPickStation.OnArrivedAtLeftPosition += RapidPickStationOnArrivedAtLeftPosition;
        }

        public override void Dispose()
        {
            //Add event un-subscriptions here
            rapidPickStation.OnArrivedAtRightPosition -= RapidPickStationOnArrivedAtRightPosition;
            rapidPickStation.OnArrivedAtLeftPosition -= RapidPickStationOnArrivedAtLeftPosition;

            rapidPickStation = null;
            transferDatcomInfo = null;
        }   

        private void RapidPickStationOnArrivedAtRightPosition(object sender, PickPutStationArrivalArgs e)
        {
            casePLC.SendDivertConfirmation(RightPositionName, ((Case_Load)e.Load).SSCCBarcode);
        }

        private void RapidPickStationOnArrivedAtLeftPosition(object sender, PickPutStationArrivalArgs e)
        {
            casePLC.SendDivertConfirmation(LeftPositionName, ((Case_Load)e.Load).SSCCBarcode);
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
    [XmlInclude(typeof(PickPutStationDatcomInfo))]
    public class PickPutStationDatcomInfo : ProtocolInfo
    {
        public string RightPositionName;
        public string LeftPositionName;
    }
}