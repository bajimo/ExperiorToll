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
        private PickPutStation theLift;
        private CasePLC_Datcom casePLC;

        public MHEControl_PickPutStation(PickPutStationDatcomInfo info, PickPutStation lift)
        {
            Info               = info;  // set this to save properties 
            transferDatcomInfo = info;
            theLift            = lift;
            casePLC            = lift.Controller as CasePLC_Datcom;

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
            casePLC.SendDivertConfirmation(PickPositionName, ((Case_Load)e.Load).SSCCBarcode);
        }

        private void TheLift_OnArrivedAtPutPosition(object sender, PickPutStationArrivalArgs e)
        {
            casePLC.SendDivertConfirmation(PutPositionName, ((Case_Load)e.Load).SSCCBarcode);
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
    [XmlInclude(typeof(PickPutStationDatcomInfo))]
    public class PickPutStationDatcomInfo : ProtocolInfo
    {
        public string PickPositionName;
        public string PutPositionName;
    }
}