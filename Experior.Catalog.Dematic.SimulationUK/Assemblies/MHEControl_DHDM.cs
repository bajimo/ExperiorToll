using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Experior.Dematic;
using Experior.Catalog.Dematic.Case.Components;


namespace Experior.Catalog.Dematic.SimulationUK.Assemblies
{
    class MHEControl_Transfer : MHEControl
    {
        private Transfer theTransfer;
        private TransferSimulationInfo dhdmInfo;

        public MHEControl_Transfer(TransferSimulationInfo info, Transfer transfer)
        {
            dhdmInfo = info;
            theTransfer = transfer;
            theTransfer.OnArrivedAtTransferController += transfer_OnArrivedAtTransferController; 
        }

        void transfer_OnArrivedAtTransferController(object sender, TransferArrivalArgs e)
        {
            theTransfer.RouteLoad(e._fromSide, Side.Left, e._load);
        }

        public int MyProperty { get; set; }

        public override void Dispose()
        {
            theTransfer.OnArrivedAtTransferController -= transfer_OnArrivedAtTransferController; 
            theTransfer = null;
            dhdmInfo = null;
        }        
    }

    [Serializable]
    [XmlInclude(typeof(TransferSimulationInfo))]
    class TransferSimulationInfo : ProtocolInfo
    {

    }
}
