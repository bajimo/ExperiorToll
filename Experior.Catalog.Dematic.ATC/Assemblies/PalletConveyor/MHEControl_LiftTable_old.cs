//using Experior.Catalog.Dematic.Pallet.Assemblies;
//using Experior.Core.Assemblies;
//using Experior.Core.Loads;
//using Experior.Core.Properties;
//using Experior.Dematic.Base;
//using System;
//using System.ComponentModel;
//using System.Xml.Serialization;

//namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
//{
//    public class MHEControl_LiftTable_old : MHEControl
//    {
//        private LiftTableATCInfo liftTableAssemblyATCInfo;
//        private MHEController_Pallet palletPLC;
//        private LiftTable liftTable;
//        private static Experior.Core.Timer delayTimer = new Experior.Core.Timer(2);

//        #region Constructors

//        public MHEControl_LiftTable_old(LiftTableATCInfo info, LiftTable liftTableAssembly)
//        {
//            liftTableAssemblyATCInfo = info;
//            Info = info;  // set this to save properties 
//            liftTable = liftTableAssembly;
//            liftTable.OnLoadArrived += LiftTable_OnLoadArrived;
//            palletPLC = liftTableAssembly.Controller as MHEController_Pallet;
//        }

//        private void LiftTable_OnLoadArrived(object sender, LoadArrivedEventArgs eventArgs)
//        {
//            IATCLoadType palletLoad = eventArgs._Load as IATCLoadType;
//            if (palletLoad == null)
//            {
//                return;
//            }

//            palletLoad.Location = liftTable.Name; //Update the palletLoad location

//            switch (LiftTableType)
//            {
//                case LiftTableATCInfo.LiftTableATCTypes.None: break;
//                case LiftTableATCInfo.LiftTableATCTypes.TransportRequest: TransportRequest(liftTable, palletLoad); break;
//                default: break;
//            }
//        }

//        #endregion

//        #region Private Methods


//        //TODO: For the loads that are stopping on the case conveyor at these communication points it
//        //has to be placed in the correct position otherwise its can be an issue (I have found that placing 
//        //them in exactly the correct place compaired to a accumulation sensor is a good position as all atction point are triggered
//        private void TransportRequest(LiftTable liftTableAssembly, IATCLoadType palletLoad)
//        {
//            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination || string.IsNullOrEmpty(palletLoad.Destination))
//            {
//                palletLoad.Location = liftTableAssembly.Name;
//                //palletLoad.MTS = liftTableAssembly.ControllerName;
//                palletPLC.SendTransportRequestTelegram(palletLoad);

//                if (LoadWait)
//                {
//                    palletLoad.LoadWaitingForWCS = true;
//                    palletLoad.Stop();

//                }
//            }
//        }

//        #endregion

//        #region Public Overrides

//        public override void Dispose()
//        {
            
//        }

//        #endregion

//        #region Public Properties

//        public void DynamicPropertyAlwaysTelegram(PropertyAttributes attributes)
//        {
//            attributes.IsBrowsable = (LiftTableType == LiftTableATCInfo.LiftTableATCTypes.TransportRequest);
//        }

//        public void DynamicPropertyLoadWait(PropertyAttributes attributes)
//        {
//            attributes.IsBrowsable = (LiftTableType == LiftTableATCInfo.LiftTableATCTypes.TransportRequest);
//        }

//        [Category("Configuration")]
//        [DisplayName("Always Send Arrival")]
//        [Description("If false the telegram will only be sent if the Location equals the Destination")]
//        [PropertyAttributesProvider("DynamicPropertyAlwaysTelegram")]
//        public bool AlwaysArrival
//        {
//            get { return liftTableAssemblyATCInfo.alwaysArrival; }
//            set { liftTableAssemblyATCInfo.alwaysArrival = value; }
//        }

//        [Category("Configuration")]
//        [DisplayName("Load Wait")]
//        [Description("Should the load wait for a StartTransportTelegram before releasing from action point")]
//        [PropertyAttributesProvider("DynamicPropertyLoadWait")]
//        [Experior.Core.Properties.AlwaysEditable]
//        public bool LoadWait
//        {
//            get { return liftTableAssemblyATCInfo.loadWait; }
//            set { liftTableAssemblyATCInfo.loadWait = value; }
//        }

//        [DisplayName("Type")]
//        [DescriptionAttribute("None - No messaging.\n" +
//          "TransportRequest - Send TransportRequestTelegram when load arrives at the action point\n")]
//        public LiftTableATCInfo.LiftTableATCTypes LiftTableType
//        {
//            get { return liftTableAssemblyATCInfo.liftTableAssemblyType; }
//            set
//            {
//                liftTableAssemblyATCInfo.liftTableAssemblyType = value;
//                Core.Environment.Properties.Refresh();
//            }
//        }

//        #endregion

//    }

//    [Serializable]
//    [XmlInclude(typeof(LiftTable_oldATCInfo))]
//    public class LiftTable_oldATCInfo : ProtocolInfo
//    {
//        public enum LiftTableATCTypes
//        {
//            None,
//            TransportRequest,
//        }

//        public LiftTableATCTypes liftTableAssemblyType = LiftTableATCTypes.None;
//        public bool alwaysArrival = true;
//        public bool loadWait = true;
//    }

//}
