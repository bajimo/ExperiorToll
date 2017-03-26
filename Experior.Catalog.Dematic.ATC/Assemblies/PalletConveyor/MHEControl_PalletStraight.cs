using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
{
    public class MHEControl_PalletStraight : MHEControl
    {
        private PalletStraightATCInfo palletStraightATCInfo;
        private MHEController_Pallet palletPLC;
        private PalletStraight palletStraight;
        private static Experior.Core.Timer delayTimer = new Experior.Core.Timer(2);

        #region Constructors

        public MHEControl_PalletStraight(PalletStraightATCInfo info, PalletStraight palletStraightAssembly)
        {
            palletStraightATCInfo = info;
            Info = info;  // set this to save properties 
            palletStraight = palletStraightAssembly;
            palletStraight.OnLoadArrived += PalletStraight_OnLoadArrived;
            palletStraight.OnLoadLeft += PalletStraight_OnLoadLeft;


            palletPLC = palletStraight.Controller as MHEController_Pallet;
        }



        private void PalletStraight_OnLoadArrived(object sender, LoadArrivedEventArgs e)
        {
            IATCLoadType palletLoad = e._Load as IATCLoadType;
            if (palletLoad == null)
            {
                return;
            }

            palletLoad.Location = palletStraight.Name; //Update the palletLoad location

            switch (PalletStraightType)
            {
                case PalletStraightATCInfo.PalletStraightATCTypes.None: break;
                case PalletStraightATCInfo.PalletStraightATCTypes.LocationArrived: LocationArrived(palletStraight, palletLoad); break;
                case PalletStraightATCInfo.PalletStraightATCTypes.LocationLeft: LocationLeft(palletStraight, palletLoad); break;
                case PalletStraightATCInfo.PalletStraightATCTypes.TransportRequest: TransportRequest(palletStraight, palletLoad); break;
                case PalletStraightATCInfo.PalletStraightATCTypes.TransportFinished: TransportFinished(palletStraight, palletLoad); break;
                case PalletStraightATCInfo.PalletStraightATCTypes.TransportRequestOrFinished: TransportRequestOrFinished(palletStraight, palletLoad); break;
                default: break;
            }

            palletStraight.ReleaseLoad(e._Load);
        }

        private void PalletStraight_OnLoadLeft(object sender, LoadArrivedEventArgs e)
        {
            if (LeftTelegram)
            {
                //send a left telegram
                palletPLC.SendLocationLeftTelegram((IATCLoadType)e._Load);
            }
        }


        #endregion

        #region Private Methods

        private void LocationArrived(PalletStraight palletStraight, IATCLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination)
            {
                palletLoad.Location = palletStraight.Name;
                palletPLC.SendLocationArrivedTelegram(palletLoad);
                if (LoadWait)
                {
                    palletLoad.LoadWaitingForWCS = true;
                }
            }
        }

        private void LocationLeft(PalletStraight palletStraight, IATCLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination)
            {
                palletLoad.Location = palletStraight.Name;
                palletPLC.SendLocationLeftTelegram(palletLoad); // Should we not use the base IATCLoadType for this?
                
            }
        }

        //TODO: For the loads that are stopping on the case conveyor at these communication points it
        //has to be placed in the correct position otherwise its can be an issue (I have found that placing 
        //them in exactly the correct place compaired to a accumulation sensor is a good position as all atction point are triggered
        private void TransportRequest(PalletStraight palletStraight, IATCLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination || string.IsNullOrEmpty(palletLoad.Destination))
            {
                palletLoad.Location = palletStraight.Name;
                palletPLC.SendTransportRequestTelegram(palletLoad);

                if (LoadWait)
                {
                    palletLoad.LoadWaitingForWCS = true;
                }
            }
        }

        private void TransportFinished(PalletStraight palletStraight, IATCLoadType palletLoad)
        {
            if (AlwaysArrival || palletLoad.Location == palletLoad.Destination)
            {
                palletLoad.Location = palletStraight.Name;
                palletPLC.SendTransportFinishedTelegram(palletLoad);

                if (LoadWait)
                {
                    palletLoad.LoadWaitingForWCS = true;
                }
            }
        }

        private void TransportRequestOrFinished(PalletStraight palletStraight, IATCLoadType palletLoad)
        {
            if (palletLoad.Location == palletLoad.Destination)
            {
                //Send TransportFinishedTelegram
                palletLoad.Location = palletStraight.Name;
                palletPLC.SendTransportFinishedTelegram(palletLoad);
            }
            else
            {
                //Send TransportRequestTelegram
                palletLoad.Location = palletStraight.Name;
                palletPLC.SendTransportRequestTelegram(palletLoad);
            }

            if (LoadWait)
            {
                palletLoad.LoadWaitingForWCS = true;
            }
        }


        #endregion

        #region Public Overrides

        public override void Dispose()
        {
            
        }

        #endregion

        #region Public Properties

        public void DynamicPropertyAlwaysTelegram(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.LocationArrived ||
                                      PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.LocationLeft ||
                                      PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.TransportRequest ||
                                      PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.TransportFinished);
        }

        public void DynamicPropertyLoadWait(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = (PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.TransportRequest ||
                                      PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.TransportFinished ||
                                      PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.TransportRequestOrFinished ||
                                      PalletStraightType == PalletStraightATCInfo.PalletStraightATCTypes.LocationArrived);
        }

        [Category("Configuration")]
        [DisplayName("Always Send Arrival")]
        [Description("If false the telegram will only be sent if the Location equals the Destination")]
        [PropertyAttributesProvider("DynamicPropertyAlwaysTelegram")]
        public bool AlwaysArrival
        {
            get { return palletStraightATCInfo.alwaysArrival; }
            set { palletStraightATCInfo.alwaysArrival = value; }
        }

        [Category("Configuration")]
        [DisplayName("Load Wait")]
        [Description("Should the load wait for a StartTransportTelegram before releasing from photocell")]
        [PropertyAttributesProvider("DynamicPropertyLoadWait")]
        [Experior.Core.Properties.AlwaysEditable]
        public bool LoadWait
        {
            get { return palletStraightATCInfo.loadWait; }
            set { palletStraightATCInfo.loadWait = value; }
        }

        [Category("Configuration")]
        [DisplayName("Send Left")]
        [Description("If true a LocationLeft Telegram will be sent when the load is released from the conveyor position")]
        [PropertyAttributesProvider("DynamicPropertyAlwaysTelegram")]
        public bool LeftTelegram
        {
            get { return palletStraightATCInfo.leftTelegram; }
            set { palletStraightATCInfo.leftTelegram = value; }
        }

        [DisplayName("Type")]
        [DescriptionAttribute("None - No messaging.\n" +
          "LocationArrived - Send LocationArrivedTelegram when load arrives at the photocell\n" +
          "TransportRequest - Send TransportRequestTelegram when load arrives at the photocell\n" +
          "TransportFinished - Send TransportFinishedTelegram when load arrives at the photocell\n" +
          "TransportRequestOrFinished - Send TransportRequest When load does not have photocell as destination, or finished if it does" +
          "ControllerPoint - This gives an Arrival notification in the controller.\n")]
        public PalletStraightATCInfo.PalletStraightATCTypes PalletStraightType
        {
            get { return palletStraightATCInfo.palletStraightType; }
            set
            {
                palletStraightATCInfo.palletStraightType = value;
                Core.Environment.Properties.Refresh();
            }
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(PalletStraightATCInfo))]
    public class PalletStraightATCInfo : ProtocolInfo
    {
        public enum PalletStraightATCTypes
        {
            None,
            LocationArrived,
            LocationLeft,
            TransportRequest,
            TransportFinished,
            TransportRequestOrFinished,
            ControllerPoint
        }

        public PalletStraightATCTypes palletStraightType = PalletStraightATCTypes.None;
        public bool alwaysArrival = true;
        public bool loadWait = true;
        public bool leftTelegram = false;
    }

}
