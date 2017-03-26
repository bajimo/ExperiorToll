using Experior.Core.Parts;
using Experior.Core.TransportSections;

namespace Experior.Dematic.Base.Devices
{
    /// <summary>
    /// Implementation required if connecting fix points to other conveyors
    /// </summary>
    public interface IRouteStatus
    {
        RouteStatus GetRouteStatus(FixPoint startFixPoint);
        LoadWaitingStatus GetLoadWaitingStatus(FixPoint endFixPoint);
        ITransportSection TransportSection { get; set; }
        float Speed { get; set; }
        int LoadCount { get; }
        FixPoint EndFixPoint { get; }
    }
}
