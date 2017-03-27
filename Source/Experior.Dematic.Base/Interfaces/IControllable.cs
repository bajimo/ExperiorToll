using Experior.Core.Properties;

namespace Experior.Dematic.Base
{
    public interface IControllable
    {
        IController Controller { get; set; } //A handle on the controller e.g. a PLC
        MHEControl ControllerProperties { get; set; } //
        string ControllerName { get; set; } //This is the name of the controller e.g. PLC
        void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes); //This should be implemented to remove the controller properties if the controller is not set
        string Name { get; set; } //This is the name the MHEComponent e.g. commpoint or mergedivert conveyor
    }

    /// <summary>
    /// Implementation required when defining info for embedded MHE_Control objects
    /// </summary>
    public interface IControllableInfo
    {
        string ControllerName { get; set; }
        ProtocolInfo ProtocolInfo { get; set; } //The specific type of communication point that is associated to this comm point. 
    }
}