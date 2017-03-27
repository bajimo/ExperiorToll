using Experior.Core.Assemblies;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;

namespace Experior.Dematic.Base.Devices
{
    public interface IConstructDevice
    {
        List<DeviceInfo> DeviceInfos { get; }
        // List<CommunicationPointInfo> CommPoints { get; }          //Use these properties to wrap an xxinfo class list of the same type. This enables the devices to be constructed from the saved parameters.
        // List<PhotocellInfo> PhotocellInfos { get; }                 //Use these properties to wrap an xxinfo class list of the same type. This enables the devices to be constructed from the saved parameters.
        void addAssembly(Assembly assembly, Vector3 localPosition); //wrapper for Experior.Core.Assemblies.Assembly.AddAssembly
        void removeAssembly(Assembly assembly);                     //wrapper for Experior.Core.Assemblies.Assembly.RemoveAssembly
        bool containsAssembly(string assemblyName);

        event EventHandler<SizeUpdateEventArgs> OnSizeUpdated;

        float Width { get; }
        float Length { get; }
    }
}
