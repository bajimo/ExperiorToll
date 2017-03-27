using Experior.Core.Loads;
using Experior.Core.TransportSections;
using Microsoft.DirectX;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{

    public class TrackVehicle : Box
    {
        public TrackVehicle(LoadInfo info) : base(info) { }

        public enum ExceptionTypes
        {
            [Description("None")]
            None,
            [Description("Bin Store Full (status 04)")]
            BinStoreFull,
            [Description("Bin Store Blocked (status 12)")]
            BinStoreBlocked,
            [Description("Bin Retrieve Empty (status 05)")]
            BinRetrieveEmpty,
            [Description("Bin Retrieve Blocked (status 11)")]
            BinRetrieveBlocked
        }

        ExceptionTypes exceptionType = ExceptionTypes.None;

        [Category("Status")]
        [DisplayName("Exception")]
        [Description("Create this exception in next job. Note BinStoreBlocked and BinRetrieveBlocked is only sent if depth > 1.")]

        [ReadOnly(false)]
        [Core.Properties.AlwaysEditable]
        public ExceptionTypes ExceptionType
        {
            get { return exceptionType; }
            set { exceptionType = value; }
        }
        public bool InException = false;

        [Category("Status")]
        [DisplayName("Motor status")]
        [Description("Motor status - for debugging")]
        public string MotorStatus
        {
            get
            {
                if (Disposed)
                {
                    return "DISPOSED";
                }

                if (Route == null)
                {
                    return "NO MOTOR";
                }

                if (Stopped && !Route.Motor.Running)
                {
                    return "STOPPED";
                }

                if (!Stopped && Route.Motor.Running)
                {
                    return "RUNNING";
                }

                return "UNKNOWN";
            }
        }

        public string ShuttleTaskDisplay
        {
            get;
            set;
        }

        [Browsable(false)]
        public override float Angle
        {
            get
            {
                return base.Angle;
            }
            set
            {
                base.Angle = value;
            }
        }

    }
}
