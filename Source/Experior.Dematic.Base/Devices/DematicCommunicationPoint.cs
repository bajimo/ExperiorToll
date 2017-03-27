using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Routes;

using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Dematic.Base.Devices
{
    /// <summary>
    /// This is a generic type of communication point. When this is added to a conveyor
    /// and the PLC is set then the correct properties will be avaiable
    /// </summary>
    public class DematicCommunicationPoint : Device, IControllable
    {
        private DematicCommunicationPointInfo commPointInfo;

        public Cylinder commCylinder;
        public ActionPoint apCommPoint = new ActionPoint();
        protected Text3D nameLabel;

        public delegate void CommPointArrival(DematicCommunicationPoint commPoint, Load load);
        /// <summary>
        /// The MHE control object will assign an "ap_enter" method in its contructor
        /// </summary>
        public CommPointArrival commPointArrival;

        static int nameIndex = 0;

        private MHEControl controllerProperties;
        private IController controller;

        #region Constructors

        public DematicCommunicationPoint(DematicCommunicationPointInfo info, BaseTrack conv) : base(info, conv)
        {
            commPointInfo = info as DematicCommunicationPointInfo;

            commCylinder = new Cylinder(Color.DarkOrange, 0.15f, 0.08f, 20);
            commCylinder.Pitch = (float)Math.PI / 2;
            commCylinder.Selectable = true;
            commCylinder.Color = LoadColour;
            commCylinder.OnSelected += commPoint_OnSelected;
            Add((RigidPart)commCylinder);

            Font font = new Font("Helvetica", 1f, FontStyle.Bold, GraphicsUnit.Pixel);
            nameLabel = new Text3D(Color.FromArgb(60, 60, 60), 0.2f, 0.05f, font);
            nameLabel.Text = "  " + Name;
            nameLabel.Pitch = (float)Math.PI / 2;
            nameLabel.Roll = Trigonometry.Angle2Rad(90);
            Add((RigidPart)nameLabel);
            nameLabel.LocalPosition = commCylinder.LocalPosition + new Vector3(0.065f, 0.05f, 0);
            nameLabel.OnSelected += commPoint_OnSelected;

            apCommPoint.Distance = info.distance;
            apCommPoint.Edge = ActionPoint.Edges.Leading;
            apCommPoint.OnEnter += new ActionPoint.EnterEvent(apCommPoint_OnEnter);
            apCommPoint.Visible = false;
            apCommPoint.Name = commPointInfo.name;

            conv.TransportSection.Route.InsertActionPoint(apCommPoint);
            ShowLabel = info.showLabel;
            LabelAngle = info.labelAngle;

            ControllerProperties = StandardCase.SetMHEControl(commPointInfo, this);
            if (Controller == null)
            {
                ControllerName = "No Controller"; //This will set the default value of controller dropdown box
            }
        }

        public override void Device_OnSizeUpdated(object sender, SizeUpdateEventArgs e)
        {
            if (e._radius != null)
            {
                DeviceDistance = commPointInfo.distance;
            }
        }

        #endregion

        #region Administration methods

        public static string GetValidCommPointNameName(string prefix)
        {
            return prefix + nameIndex++.ToString();
        }

        /// <summary>
        /// Make the CommuncationPoint show properties when selected
        /// </summary>
        /// <param name="sender"></param>
        void commPoint_OnSelected(RigidPart sender)
        {
            Core.Environment.Properties.Set(this);
        }

        #endregion

        void apCommPoint_OnEnter(ActionPoint sender, Load load)
        {
            Experior.Core.Controller.Arrived((INode)apCommPoint, load);

            if (commPointArrival != null)
            {
                commPointArrival(this, load);
            }
        }

        #region Properties

        #region User Interface

        #region IControllable Implementation

        /// <summary>
        /// Generic property for a PLC of any type, DatCom, DCI etc it is set when the ControllerName is set
        /// </summary>
        [Category("Routing")]
        [DisplayName("Controller Setup")]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        public MHEControl ControllerProperties
        {
            set { controllerProperties = value; }
            get { return controllerProperties; }
        }

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        //[PropertyAttributesProvider("DynamicPropertyControllers")]
        [TypeConverter(typeof(CaseControllerConverter))]
        public string ControllerName
        {
            get { return commPointInfo.ControllerName; }
            set
            {
                if (!value.Equals(commPointInfo.ControllerName))
                {
                    ControllerProperties = null;
                    commPointInfo.ProtocolInfo = null;
                }

                commPointInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(commPointInfo, this);
                }
            }
        }

        //[Category("Routing")]
        //[DisplayName("Control Type")]
        //[Description("Defines if the control is handled by a controller, by a routing script or uses only local control.")]
        //[PropertyOrder(1)]
        //public CommControlTypes ControlType
        //{
        //    get
        //    {
        //        return commPointInfo.ControlType;
        //    }
        //    set
        //    {
        //        commPointInfo.ControlType = value;
        //        Core.Environment.Properties.Refresh();
        //    }
        //}


        /// <summary>
        /// This will be set by setting the ControllerName in method StandardCase.SetMHEControl(commPointInfo, this) !!
        /// </summary>
        [Browsable(false)]
        public IController Controller
        {
            get { return controller; }
            set
            {
                controller = value;
                if (controller != null)
                {   //If the PLC is deleted then any conveyor referencing the PLC will need to remove references to the deleted PLC.
                    controller.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                Core.Environment.Properties.Refresh();
            }
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            ControllerName = ((Experior.Core.Assemblies.Assembly)sender).Name;
        }

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            commPointInfo.ProtocolInfo = null;
            ControllerProperties = null;
        }

        //public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        //{
        //    attributes.IsBrowsable = commPointInfo.ControlType == CommControlTypes.Controller;
        //}

        #endregion

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        [Category("Configuration")]
        [DisplayName("Distance (m.)")]
        [Description("The distance from the start of the conveyor")]
        [TypeConverter()]
        public override float DeviceDistance
        {
            get
            {
                return commPointInfo.distance;
            }
            set
            {
                apCommPoint.Distance = value;
                commPointInfo.distance = value;
                // ((Experior.Core.Assemblies.Assembly)(this.parent))
                Experior.Catalog.Logistic.Track.Curve assem = parent as Experior.Catalog.Logistic.Track.Curve;

                if (assem != null)
                {
                    double theta = value / assem.Radius;
                    double x, z;
                    if (assem.Revolution == Core.Environment.Revolution.Counterclockwise)
                    {
                        z = assem.Radius * (Math.Sin(theta));
                        x = assem.Radius * (Math.Cos(theta));
                        this.LocalPosition = new Vector3(-(float)x, 0.05f, (float)z);
                        this.LocalYaw = -(float)theta;
                    }
                    else
                    {
                        z = assem.Radius * (Math.Cos(theta));
                        x = assem.Radius * (Math.Sin(theta));

                        this.LocalPosition = new Vector3((float)x, 0.05f, (float)z);
                        this.LocalYaw = (float)theta;
                    }
                }
                else  //It's a straight conveyor
                {
                    Straight assem2 = Parent as Straight;
                    this.LocalPosition = new Vector3(assem2.Length / 2 - value, 0.05f, 0);
                }

                nameLabel.LocalPosition = commCylinder.LocalPosition + new Vector3(0.065f, 0.05f, 0);
            }
        }

        [Category("Configuration")]
        [PropertyOrder(1)]
        public override string Name
        {
            set
            {
                base.Name = value;
                if (commPointInfo.name == value)
                {
                    nameLabel.Text = "  " + value;
                    apCommPoint.Name = value;
                }
                Core.Environment.SolutionExplorer.Update(this);
            }
        }

        [Category("Configuration")]
        [DisplayName("Colour")]
        [PropertyOrder(2)]
        public Color LoadColour
        {
            get
            {
                return Color.FromName(commPointInfo.cylinderColour);
            }
            set
            {
                commPointInfo.cylinderColour = value.Name;
                commCylinder.Color = Color.FromName(commPointInfo.cylinderColour);
            }
        }


        [Category("Configuration")]
        [DisplayName("Label Angle")]
        [PropertyOrder(3)]
        public float LabelAngle
        {
            get { return commPointInfo.labelAngle; }
            set
            {
                commPointInfo.labelAngle = value;
                nameLabel.Yaw = (float)(Math.PI / 180) * value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Show Label")]
        [PropertyOrder(4)]
        public virtual bool ShowLabel
        {
            get { return commPointInfo.showLabel; }
            set
            {
                commPointInfo.showLabel = value;
                nameLabel.Visible = value;
            }
        }

        #endregion

        #region Catalogue Properties

        public override string Category
        {
            get { return "Communication Point"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("commpoint"); }
        }

        #endregion

        #endregion

        //Would like the user to be able to change the color of the cyclinder but cannot seem to get the color to 
        //save to the info (BG)
        //[Category("Configuration")]
        //[DisplayName("Comm Point Colour")]
        //public Color CommPointColor
        //{
        //    get { return commPointInfo.commPointColour; }
        //    set
        //    {
        //        commPointInfo.commPointColour = value;
        //        commCylinder.Color = value;
        //    }
        //}

    }

    [Serializable]
    [XmlInclude(typeof(DematicCommunicationPointInfo))]
    public class DematicCommunicationPointInfo : DeviceInfo, IControllableInfo
    {
        public float labelRotate = 45;
        public bool showLabel = true;
        //public Color commPointColour { get; set; }
        public CommControlTypes ControlType;
        public string cylinderColour = System.Drawing.Color.Orange.Name;
        public bool callForwardArrivalMessage;
        public float labelAngle = 0;
        private string controllerName = string.Empty;
        public string ControllerName
        {
            get { return controllerName; }
            set { controllerName = value; }
        }

        private ProtocolInfo protocolInfo;
        public ProtocolInfo ProtocolInfo
        {
            get { return protocolInfo; }
            set { protocolInfo = value; }
        }

        public override void SetCustomInfoFields(Assembly assem, object obj, ref DeviceInfo info)
        {
            // throw new NotImplementedException();
        }
    }

}