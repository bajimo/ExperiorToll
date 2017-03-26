using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dematic.ATC;
using Experior.Dematic.Base;
using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Loads;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.ATC.Assemblies.Sorters
{
    class MHEControl_CommPoint : MHEControl
    {
        CommunicationPoint commPoint;

        public MHEControl_CommPoint(MHEControl_CommPointInfo info, CommunicationPoint commPoint)
        {
            Info = info;  // set this to save properties 
            this.commPoint = commPoint;
            commPoint.commPointArrival = ap_Enter; //CommunicationPoint will use this delegate for ap_enter.
        }

        void ap_Enter(DematicCommunicationPoint commPoint, Load load)
        {

        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class MHEControl_CommPointInfo:ProtocolInfo
    {
    }
}
