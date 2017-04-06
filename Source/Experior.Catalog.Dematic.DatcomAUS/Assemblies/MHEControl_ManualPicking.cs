using Experior.Catalog.Dematic.Case.Components;
using Experior.Dematic.Base;
using System;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    class MHEControl_ManualPicking : MHEControl
    {
        private ManualPickingDatcomAusInfo manualPickDatcomInfo;
        private StraightAccumulationConveyor theConveyor;
        private MHEControllerAUS_Case casePLC;
        Case_Load caseLoad;

        public MHEControl_ManualPicking(ManualPickingDatcomAusInfo info, StraightAccumulationConveyor conveyor)
        {
            Info = info;  // set this to save properties 
            manualPickDatcomInfo = info;
            theConveyor = conveyor;
            casePLC = conveyor.Controller as MHEControllerAUS_Case;
            theConveyor.OnArrivedAtPickingPosition += theConveyor_OnArrivedAtPickingPosition;
        }

        void theConveyor_OnArrivedAtPickingPosition(object sender, ManualPickArrivalArgs e)
        {
            //Send Arrival message at the conveyor location 
            caseLoad = e._load as Case_Load;
            casePLC.SendArrivalMessage(theConveyor.Name, caseLoad);
        }

        public void CallForwardReceived(string barcode)
        {
            if (caseLoad == null)
            {
                Log.Write(string.Format("Call forward received: Cannot release load as there is no load in the picking station {0}", theConveyor.Name));
                return;
            }
            if (barcode != null && caseLoad.SSCCBarcode != barcode)
            {
                Log.Write(string.Format("Call forward received: Barcode in CF (type 86) message [{0}] does not match barcode of load in pick station {1} [{2}], load released anyway", barcode, theConveyor.Name, caseLoad.SSCCBarcode));
            }

            //This releases the load when the route is clear (do not set release on the load as this will releease the load regardless and cause issues)
            theConveyor.ReleasePickLoad = true;

            caseLoad = null;
        }

        public override void Dispose()
        {
            theConveyor.OnArrivedAtPickingPosition -= theConveyor_OnArrivedAtPickingPosition;
            theConveyor = null;
            manualPickDatcomInfo = null;
        }        
    }

    [Serializable]
    [XmlInclude(typeof(ManualPickingDatcomAusInfo))]
    public class ManualPickingDatcomAusInfo : ProtocolInfo
    {
        public string Location;
    }
}
