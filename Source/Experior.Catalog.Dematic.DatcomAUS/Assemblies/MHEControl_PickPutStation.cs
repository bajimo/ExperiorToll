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
            theLift.OnArrivedAtPickPosition += TheLift_OnArrivedAtPickPosition;
            theLift.OnArrivedAtPutPosition += TheLift_OnArrivedAtPutPosition;
        }

        public MHEControl_PickPutStation()
        {

        }

        public override void Dispose()
        {
            //Add event un-subscriptions here
            theLift.OnArrivedAtPickPosition -= TheLift_OnArrivedAtPickPosition;
            theLift.OnArrivedAtPutPosition -= TheLift_OnArrivedAtPutPosition;

            theLift = null;
            transferDatcomInfo = null;
        }   

        private void TheLift_OnArrivedAtPickPosition(object sender, PickPutStationArrivalArgs e)
        {
            casePLC.SendArrivalMessage(PickPositionName, ((Case_Load)e.Load));
        }

        private void TheLift_OnArrivedAtPutPosition(object sender, PickPutStationArrivalArgs e)
        {
            casePLC.SendArrivalMessage(PutPositionName, ((Case_Load)e.Load));
        }

        [DisplayName("Pick Position Name")]
        [Description("Name of the Right Hand Side Conveyor - from picker poin of view")]
        [PropertyOrder(1)]
        public string PickPositionName
        {
            get { return transferDatcomInfo.PickPositionName; }
            set { transferDatcomInfo.PickPositionName = value; }
        }

        [DisplayName("Put Position Name")]
        [Description("Name of the Left Hand Side Conveyor - from picker point of view")]
        [PropertyOrder(2)]
        public string PutPositionName
        {
            get { return transferDatcomInfo.PutPositionName; }
            set { transferDatcomInfo.PutPositionName = value; }
        }
    }

    [Serializable]
    [XmlInclude(typeof(PickPutStationDatcomAusInfo))]
    public class PickPutStationDatcomAusInfo : ProtocolInfo
    {
        public string PickPositionName;
        public string PutPositionName;
    }
}