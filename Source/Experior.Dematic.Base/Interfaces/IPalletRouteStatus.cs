using Experior.Core.Assemblies;
using Experior.Core.Loads;
using System;

namespace Experior.Dematic.Base.Devices
{
    /// <summary>
    /// Implementation required if connecting fix points to other conveyors
    /// </summary>
    public interface IPalletRouteStatus : IRouteStatus
    {
        event EventHandler<LoadArrivedEventArgs> OnLoadArrived;
    }
}
