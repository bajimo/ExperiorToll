using System;
using System.ComponentModel;
using Experior.Core.Assemblies;
using Experior.Core.Properties;

namespace Experior.Dematic.Base
{
    /// <summary>
    /// Generic class for all types of plant and all types of PLC protocol.
    /// All MHE control objects should inherit this class.
    /// </summary>
    [Serializable]
    [TypeConverter(typeof(ObjectConverter))]
    public abstract class MHEControl 
    {
        private ProtocolInfo info;
        private Assembly parentAssembly;

        /// <summary>
        /// unsubscribe from all subscribed to events here. If this is not done then setting an assembly controller to "no controller" 
        /// will have no effect and the events will still be received be the controller object
        /// </summary>
        public abstract void Dispose();

        [Browsable(false)]
        public Assembly ParentAssembly
        {
            get { return parentAssembly; }
            set { parentAssembly = value; }
        }

        /// <summary>
        /// Set this property in inherting class constructor
        /// This will alow a communication point to have information on the type of communication point that it is 
        /// </summary>
        [Browsable(false)]
        public ProtocolInfo Info
        {
            get { return info; }
            set { info = value; }
        }

    }

    /// <summary>
    /// A base class for different types of PLC protocols. 
    /// Inherit this class for a new info class to give the CommunicationPoint a generic type for PLC config.
    /// </summary>
    public class ProtocolInfo 
    {
        public string assem;
    }
}