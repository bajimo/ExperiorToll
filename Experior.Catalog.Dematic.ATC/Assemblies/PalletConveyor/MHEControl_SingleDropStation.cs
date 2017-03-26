using Experior.Catalog.Dematic.Pallet;
using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
{
    public class MHEControl_SingleDropStation : MHEControl
    {
        private SingleDropStationATCInfo SingleDropStationATCInfo;
        private SingleDropStation SingleDropStationConveyor;
        private MHEController_Pallet palletPLC;
        //private IATCPalletLoadType activeLoad = null;
        
        public MHEControl_SingleDropStation(SingleDropStationATCInfo info, SingleDropStation dropStation)
        {
            SingleDropStationConveyor = dropStation;
            SingleDropStationATCInfo = info;
            Info = info;  // set this to save properties 

            SingleDropStationConveyor.divertArrival = divertArrival;
            SingleDropStationConveyor.loadDeleted = loadDeleted;
            
            palletPLC = SingleDropStationConveyor.Controller as MHEController_Pallet;
        }

        bool divertArrival(Load load)
        {
            IATCPalletLoadType atcLoad = load as IATCPalletLoadType;
            atcLoad.StopLoad_WCSControl();
            return true; //returns true if handled by this controller
        }

        bool loadDeleted(Load load)
        {
            //activeLoad = null;
            return true;
        }

        public override void Dispose()
        {
            //throw new NotImplementedException();
        }
    }

    [Serializable]
    [XmlInclude(typeof(SingleDropStationATCInfo))]
    public class SingleDropStationATCInfo : ProtocolInfo
    {
    }
}