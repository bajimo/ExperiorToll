using Experior.Core.Assemblies;
using Experior.Core.Parts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Experior.Dematic.Base.Devices
{
    public class DematicFixPoint : FixPoint
    {
        public DematicFixPoint(Color color, Types type, Assembly parent) : base(color, type, parent)
        {
            FixPointRouteStatus.Available = RouteStatuses.Request;
        }

        public RouteStatus FixPointRouteStatus = new RouteStatus();

        public LoadWaitingStatus FixPointLoadWaitingStatus = new LoadWaitingStatus();
    }
}
