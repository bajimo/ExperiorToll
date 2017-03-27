using System.ComponentModel;
using System.Xml.Serialization;
using Experior.Core;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Sorter.Assemblies
{
    [TypeConverter(typeof(ObjectConverter))]
    public class SorterElementFixPoint : INode
    {
        public delegate void ArrivedEvent(SorterCarrier carrier, Load load);
        public event ArrivedEvent OnCarrierArrived;
 
        private bool restarttimer;

        internal bool RestartTimer
        {
            get { return restarttimer; }
            set { restarttimer = value; }
        }

        internal void Arriving(SorterCarrier carrier, Load load)
        {
            if (OnCarrierArrived != null)
            {
                OnCarrierArrived(carrier, load);
            }
        }

        public enum SorterFixPointTypes
        {
            Induction,
            Chute
        }

        internal void Reset()
        {
            restarttimer = false;
            Timer.Stop();
            Timer.Reset();
        }

        internal void Stop()
        {
            if (Timer.Running)
            {
                restarttimer = true;
                Timer.Stop();
            }
        }

        internal void Start()
        {
            if (restarttimer)
            {
                Timer.Stop();
                Timer.Start();
            }

            restarttimer = false;
        }

        public enum DischargeSides
        {
            Left,
            Right
        }

        [Category("Configuration")]
        [Description("Discharge side of carriers when discharging at this fixpoint.")]
        [DisplayName(@"Discharge side")]
        public DischargeSides DischargeSide { get; set; }

        private SorterFixPointTypes type = SorterFixPointTypes.Induction;

        [Category("Configuration")]
        [Description("Type of this fixpoint.")]
        [DisplayName(@"Type")]
        public SorterFixPointTypes Type
        {
            get { return type; }
            set { type = value; }
        }

        [Category("Configuration")]
        [Description("Mark this as dump chute.")]
        [DisplayName(@"Dump chute")]
        public bool DumpChute { get; set; }

        public override string ToString()
        {
            return Name;
        }

        private bool disposed;
        internal static int GlobalCount;

        private string name;

        [PropertyOrder(0)]
        [Category("Configuration")]
        [Description("Name of this fixpoint. Note this must be a unique name.")]
        [DisplayName(@"Name")]
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                if (NameChanged != null)
                    NameChanged(this);
            }
        }
        [Browsable(false)]
        public bool Routing
        {
            get
            {
                return true;
            }
            set
            {
            }
        }
        private float distance;

        public delegate void NameChangedEvent(SorterElementFixPoint f);
        public event NameChangedEvent NameChanged;

        [PropertyOrder(1)]
        [Category("Configuration")]
        [Description("Distance in meters on this element. (Local distance)")]
        [DisplayName(@"Local Distance")]
        [TypeConverter(typeof(FloatConverter))]
        public float Distance
        {
            get { return distance; }
            set
            {
                if (parent == null)
                {
                    distance = value;
                    return;
                }

                float oldvalue = distance;
                distance = value;
                if (!Parent.UpdateSorterFixPointDistance(this, value))
                    distance = oldvalue;
            }
        }

        private float localYaw;

        [PropertyOrder(3)]
        [Category("Configuration")]
        [Description("Local yaw angle of the fix point")]
        [DisplayName(@"Angle")]
        [TypeConverter(typeof(AngleConverter))]
        public float LocalYaw
        {
            get { return localYaw; }
            set
            {
                if (value > 180 || value < -180)
                    return;

                if (parent == null)
                {
                    localYaw = value;
                    return;
                }

                localYaw = value;
                Parent.UpdateSorterFixPointDistance(this, distance);
            }
        }

        private Vector3 localOffset;

        [PropertyOrder(3)]
        [Category("Configuration")]
        [Description("Local offset of the fix point")]
        [TypeConverter(typeof(Vector3Converter))]
        [DisplayName(@"Local offset")]
        public Vector3 LocalOffset
        {
            get { return localOffset; }
            set
            {
                if (parent == null)
                {
                    localOffset = value;
                    return;
                }

                localOffset = value;
                Parent.UpdateSorterFixPointDistance(this, distance);
            }
        }

        [PropertyOrder(5)]
        [Category("Configuration")]
        [Description("Distance in meters on the sorter. The distance is measured from the beginning of the master element. (Global distance)")]
        [DisplayName(@"Global Distance")]
        [TypeConverter(typeof(FloatConverter))]
        [XmlIgnore]
        public float GlobalDistance { get; internal set; }
        [XmlIgnore]
        internal Timer Timer { get; set; }
        [XmlIgnore]
        internal SorterCarrier CarrierArriving { get; set; }
        [XmlIgnore]
        private SorterElement parent;
        [Browsable(false)]
        [XmlIgnore]
        public SorterElement Parent
        {
            get { return parent; }
            internal set
            {
                parent = value;
                Fixpoint = new FixPoint(FixPoint.Types.Anonymous, parent);
                Fixpoint.UserData = this;
            }
        }

        [XmlIgnore]
        [Browsable(false)]
        public FixPoint Fixpoint { get; internal set; }

        [PropertyOrder(4)]
        [Category("Configuration")]
        [Description("Global position of the fix point")]
        [DisplayName(@"Position")]
        [XmlIgnore]
        [TypeConverter(typeof(Vector3Converter))]
        public Vector3 Position
        {
            get
            {
                if (Fixpoint == null)
                    return Vector3.Empty;
                return Fixpoint.Position;
            }
            set { }
        }
        [Browsable(false)]
        public bool User { get; set; }
        [XmlIgnore]
        [Browsable(false)]
        public object UserData { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        [DefaultValue(true)]
        public bool Enabled { get; set; }

        private ActionPoint chutePoint;

        [XmlIgnore]
        [Browsable(false)]
        public ActionPoint ChutePoint
        {
            get { return chutePoint; }
            set
            {
                chutePoint = value;
                if (chutePoint != null)
                {
                    InductionPoint = null;
                    Type = SorterFixPointTypes.Chute;
                }
            }
        }

        private ActionPoint inductionPoint;

        [XmlIgnore]
        [Browsable(false)]
        public ActionPoint InductionPoint
        {
            get { return inductionPoint; }
            set
            {
                //Unsubscribe from old
                if (inductionPoint != null)
                {
                    inductionPoint.OnEnter -= inductionPoint_Enter;
                }

                inductionPoint = value;

                //Subscribe to new
                if (inductionPoint != null)
                {
                    inductionPoint.OnEnter += inductionPoint_Enter;
                    ChutePoint = null;
                    Type = SorterFixPointTypes.Induction;
                }
            }
        }

        private void inductionPoint_Enter(ActionPoint ap, Load load)
        {
            if (parent != null && parent.MasterElement != null)
            {
                parent.MasterElement.Control.OnLoadInductionArrivedEvent(this, load);
            }
        }


        [Browsable(false)]
        public bool Visible { get; set; }

        public SorterElementFixPoint()
        {
            Enabled = true;
            Timer = new Timer(60) {UserData = this};
            GlobalCount++;
            Name = GlobalCount.ToString();
            Visible = true;
        }

        public void Select()
        {
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Timer.Dispose();
            Parent.RemoveFixPoint(Fixpoint);
            Fixpoint.Dispose();
        }

    }
}