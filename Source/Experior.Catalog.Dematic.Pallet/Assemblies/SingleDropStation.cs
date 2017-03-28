using Experior.Catalog.Logistic.Track;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Pallet.Assemblies
{
    public class SingleDropStation : LiftTable, IPalletRouteStatus, IControllable
    {
        SingleDropStationInfo singleDropStationInfo;

        public SingleDropStation(SingleDropStationInfo info) : base(info)
        {
            singleDropStationInfo = info;

        }

        protected override void ControlDivertPoint(Load load)
        {
            if (!LoadHandledByController || load.Stopped) //Never call the controller code more than once
            {
                LoadHandledByController = true;
                if (divertArrival == null || divertArrival(load))
                {
                    LoadOnDivertPoint(this, load); //Event can be subscribed to in the routing script if not handled by the controller
                }
            }
        }
    }

    [Serializable]
    [XmlInclude(typeof(LiftTableInfo))]
    public class SingleDropStationInfo : LiftTableInfo, IControllableInfo
    {
    }
}
