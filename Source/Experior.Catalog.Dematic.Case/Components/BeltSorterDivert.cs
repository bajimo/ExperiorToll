using Experior.Catalog.Logistic.Track;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Microsoft.DirectX;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class BeltSorterDivert : StraightBeltConveyor, IControllable
    {
        BeltSorterDivertInfo beltSorterDivertInfo;
        StraightBeltConveyor divertSection;
        ActionPoint apStraight = new ActionPoint();
        ActionPoint apDivert = new ActionPoint();
        float divertCentreOffset;
        RouteStatus NextDivertRouteStatus = null;

        public static event EventHandler<BeltSorterDivertArgs> OnDivertPointArrivedRoutingScript; //This is the event that can be subscribed to in the routing script        
        public static event EventHandler<BeltSorterDivertArgs> OnDivertPointDivertedRoutingScript;

        public event EventHandler<BeltSorterDivertArgs> OnDivertPointArrivedControl; //This is the event that can be subscribed to in a control object        
        public event EventHandler<BeltSorterDivertArgs> OnDivertPointDivertedControl;

        public BeltSorterDivert(BeltSorterDivertInfo info): base(info)
        {            
            beltSorterDivertInfo = info;

            if (info.type == DivertType.PopUp)
            {
                arrow.Dispose();
            }

            StraightBeltConveyorInfo divertSectionInfo = new StraightBeltConveyorInfo()
            {
                Length    = info.divertConveyorLength,
                thickness = info.thickness,
                Width     = info.width,
                Speed     = info.Speed,
                color     = info.color
            };

            divertSection                       = new StraightBeltConveyor(divertSectionInfo);
            divertSection.startLine.Visible     = false;
            divertSection.StartFixPoint.Visible = false;
            divertSection.StartFixPoint.Enabled = false;
          
            Add(divertSection);
           
            TransportSection.Route.InsertActionPoint(apStraight);
            divertSection.TransportSection.Route.InsertActionPoint(apDivert, 0);       
     
            apStraight.OnEnter += apStraight_OnEnter;
                     
            if (beltControl.LineReleasePhotocell != null)
            {
                beltControl.LineReleasePhotocell.Dispose();
            }

            if (divertSection.beltControl.LineReleasePhotocell != null)
            {
                divertSection.beltControl.LineReleasePhotocell.Dispose();
            }

            DivertAngle          = info.divertAngle;
            Length               = info.length;
            DivertConveyorOffset = info.divertConveyorOffset;

            UpdateConveyor();
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(beltSorterDivertInfo, this);
            }

            UpdateConveyor();

            SetNextDivertRouteStatus(NextBlocked);
        }

        private void SetNextDivertRouteStatus(bool value)
        {
            if (value)
            {
                IRouteStatus afterNextConv = divertSection.NextConveyor;

                if (afterNextConv is StraightConveyor)
                {
                    StraightConveyor straight = afterNextConv as StraightConveyor;
                    NextDivertRouteStatus = straight.NextConveyor.GetRouteStatus(divertSection.EndFixPoint.Attached);
                }
                else if (afterNextConv is CurveConveyor)
                {
                    CurveConveyor curve = afterNextConv as CurveConveyor;
                    NextDivertRouteStatus = curve.NextConveyor.GetRouteStatus(divertSection.EndFixPoint.Attached);
                }
            }
            else
            {
                NextDivertRouteStatus = divertSection.NextConveyor.GetRouteStatus(divertSection.EndFixPoint.Attached);
            }
        }

        void apStraight_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            if(OnDivertPointArrivedRoutingScript != null)
            {
                OnDivertPointArrivedRoutingScript(this,new BeltSorterDivertArgs(this,load, DivertRoute.None));
            }

            if (OnDivertPointArrivedControl != null)
            {
                OnDivertPointArrivedControl(this, new BeltSorterDivertArgs(this, load, DivertRoute.None));
            }

            if (Controller == null)
            {
                if (DefaultRoute == DivertRoute.Divert)
                {
                    bool keepOrienation = beltSorterDivertInfo.type == DivertType.Angled ? false : true;
                    load.Switch(apDivert, keepOrienation);
                }
            }
        }

        public void RouteLoad(DivertRoute direction, Load load)
        {
            if (direction == DivertRoute.Divert && 
                NextDivertRouteStatus.Available == RouteStatuses.Available && 
                divertSection.RouteAvailable == RouteStatuses.Available && 
                !apDivert.Active && divertSection.TransportSection.Route.Loads.Count <= 1)
            {
                RouteDivert(load);
            }
            else
            {
                RouteStraight(load);
            }
        }

        private void RouteDivert(Load load)
        {
            bool keepOrienation = beltSorterDivertInfo.type == DivertType.Angled ? false : true;
            load.Switch(apDivert, keepOrienation);

            if (OnDivertPointDivertedControl != null)
            {
                OnDivertPointDivertedControl(this, new BeltSorterDivertArgs(this, load, DivertRoute.Divert));
            }

            if (OnDivertPointDivertedRoutingScript != null)
            {
                OnDivertPointDivertedRoutingScript(this, new BeltSorterDivertArgs(this, load, DivertRoute.Divert));
            }
        }

        private void RouteStraight(Load load)
        {
            if (OnDivertPointDivertedControl != null)
            {
                OnDivertPointDivertedControl(this, new BeltSorterDivertArgs(this, load, DivertRoute.Straight));
            }

            if (OnDivertPointDivertedRoutingScript != null)
            {
                OnDivertPointDivertedRoutingScript(this, new BeltSorterDivertArgs(this, load, DivertRoute.Straight));
            }
        }

        public override void EndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            Speed = ((BaseTrack)stranger.Parent).Speed;
            StraightConveyor nextConveyor = stranger.Parent as StraightConveyor; //This is a bit dodgy how do we know the next conveyor is going to be straight (TODO)

            if (nextConveyor != null)
            {
                nextConveyor.OnSpeedUpdated += BeltSorterMerge_OnSpeedUpdated;
            }
            base.EndFixPoint_OnSnapped(stranger, e);
        }

        void BeltSorterMerge_OnSpeedUpdated(object sender, EventArgs e)
        {
            Speed = ((BaseTrack)sender).Speed;
        }

        public override void UpdateConveyor()
        {
            apStraight.Distance = DivertConveyorOffset;
            divertSection.Width = (float)beltSorterDivertInfo.divertWidth / 1000;

            float zOffset = (float)(Math.Sin(DivertAngle) * (divertSection.Length / 2));

            if (beltSorterDivertInfo.type == DivertType.PopUp)
            {
                divertSection.LocalPosition = new Vector3(Length / 2 - beltSorterDivertInfo.divertConveyorOffset, divertSection.LocalPosition.Y, (int)DivertSide * divertSection.Length / 2);
            }
            else if (beltSorterDivertInfo.type == DivertType.Angled)
            {
                divertSection.LocalPosition                = new Vector3(divertCentreOffset - beltSorterDivertInfo.divertConveyorOffset, divertSection.LocalPosition.Y, (int)DivertSide * zOffset );
                divertSection.arrow.LocalPosition          = new Vector3(-divertSection.Length / 2 + 0.2f, 0, 0);
                divertSection.SnapEndTransformation.Offset = new Vector3(0, 0, beltSorterDivertInfo.divertEndOffset);
            }

            divertSection.LocalYaw = (int)DivertSide * DivertAngle;

        }

        #region User Interface

        #region Size & Speed

        [Category("Size and Speed")]
        [DisplayName("Straight Length")]
        [Description("Length of the Straight conveyor (meter)")]
        [TypeConverter()]
        [PropertyOrder(2)]
        public override float Length
        {
            get { return base.Length; }
            set
            {
                if (value < DivertConveyorOffset && beltSorterDivertInfo.type == DivertType.Angled)
                {
                    Core.Environment.Log.Write(string.Format("Conveyor Length must not be less than 'Divert Conveyor Offset' ({0}).", DivertConveyorOffset), System.Drawing.Color.Red);
                }
                else
                {
                    base.Length = value;
                    divertCentreOffset = (float)((value / 2) - (Math.Cos(DivertAngle) * (DivertLength / 2)));
                    Core.Environment.Invoke(() => UpdateConveyor());

                    if (beltSorterDivertInfo.type == DivertType.PopUp)
                    {
                        divertSection.Width = value;
                        DivertConveyorOffset = value / 2;
                    }
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Length")]
        [Description("Length of the Divert conveyor (meter)")]
        [TypeConverter()]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [PropertyOrder(3)]
        public float DivertLength
        {
            get
            {
                return beltSorterDivertInfo.divertConveyorLength;
            }
            set
            {
                if (value > 0)
                {
                    divertCentreOffset = (float)((Length / 2) - (Math.Cos(DivertAngle) * (value / 2)));
                    beltSorterDivertInfo.divertConveyorLength = value;
                    divertSection.Length = value;
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Straight Width")]
        [Description("Width of the straight section conveyor based on standard Dematic case conveyor widths")]
        [PropertyOrder(4)]
        public override CaseConveyorWidth ConveyorWidth
        {
            get { return beltSorterDivertInfo.conveyorWidth; }
            set
            {
                Width = (float)value / 1000;
                beltSorterDivertInfo.conveyorWidth = value;

                if (beltSorterDivertInfo.type == DivertType.PopUp)
                {
                    DivertLength = Width / 2;
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Width")]
        [Description("Width of the divert section conveyor based on standard Dematic case conveyor widths")]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [PropertyOrder(5)]
        public CaseConveyorWidth DivertWidth
        {
            get { return beltSorterDivertInfo.divertWidth; }
            set
            {
                beltSorterDivertInfo.divertWidth = value;
                divertSection.Width = (float)value / 1000;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Speed")]
        [PropertyOrder(6)]
        [Description("Speed of the Divert section conveyor (Speed of straight section is taken from the next conveyor)")]
        [TypeConverter(typeof(SpeedConverter))]
        public float DivertSpeed
        {
            get { return beltSorterDivertInfo.divertSpeed; }
            set
            {
                beltSorterDivertInfo.divertSpeed = value;
                divertSection.Speed = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Angle")]
        [Description("The divert angle in degrees")]
        [PropertyOrder(7)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [TypeConverter(typeof(Rad2AngleConverter))]
        public float DivertAngle
        {
            get { return beltSorterDivertInfo.divertAngle; }
            set
            {
                {
                    beltSorterDivertInfo.divertAngle = value;
                    divertCentreOffset = (float)((Length / 2) - (Math.Cos(value) * (DivertLength / 2)));
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Offset")]
        [Description("Distance from start of conveyor until the divert conveyor (meter)")]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [TypeConverter()]
        [PropertyOrder(8)]
        public float DivertConveyorOffset
        {
            get { return beltSorterDivertInfo.divertConveyorOffset; }
            set
            {
                beltSorterDivertInfo.divertConveyorOffset = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Side")]
        [Description("Left or right divert")]
        [TypeConverter()]
        [PropertyOrder(9)]
        public Side DivertSide
        {
            get { return beltSorterDivertInfo.divertSide; }
            set
            {
                beltSorterDivertInfo.divertSide = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Fix Points")]
        [DisplayName("Divert End Offset")]
        [Description("Move the fix point position in the local Z axis (meter)")]
        [PropertyOrder(2)]
        public float DivertEndOffset
        {
            get { return beltSorterDivertInfo.divertEndOffset; }
            set
            {
                beltSorterDivertInfo.divertEndOffset = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        public override Color Color
        {
            get { return base.Color; }
            set
            {
                base.Color = value;
                divertSection.Color = value;
            }
        }
        #endregion

        #region Routing

        [Category("Routing")]
        [DisplayName("Default Route")]
        [Description("Set the default route, routing will be controlled by the controller")]
        [PropertyOrder(1)]
       // [PropertyAttributesProvider("DynamicPropertyLocalControl")]
        public DivertRoute DefaultRoute 
        {
            get { return beltSorterDivertInfo.defaultRoute; }
            set 
            {
                beltSorterDivertInfo.defaultRoute = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [PropertyOrder(2)]
        [TypeConverter(typeof(CaseControllerConverter))]
        public string ControllerName
        {
            get
            {
                return beltSorterDivertInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(beltSorterDivertInfo.ControllerName))
                {
                    ControllerProperties = null;
                    beltSorterDivertInfo.ProtocolInfo = null;
                    Controller = null;
                }

                beltSorterDivertInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(beltSorterDivertInfo, this);

                    if (ControllerProperties == null)
                    {
                        beltSorterDivertInfo.ControllerName = "No Controller";
                    }

                }
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(3)]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        public MHEControl ControllerProperties
        {
            get { return controllerProperties; }
            set 
            {
                controllerProperties = value;
                if (value == null)
                {
                    Controller = null;
                }
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Next Conveyor Blocked")]
        [Description("Use the conveyor after the next conveyor to determine if the route is blocked or not")]
        [PropertyOrder(4)]
        public bool NextBlocked
        {
            get { return beltSorterDivertInfo.NextBlocked; }
            set
            {
                beltSorterDivertInfo.NextBlocked = value;
                SetNextDivertRouteStatus(value);
            }
        }
        #endregion

        #endregion

        public void DynamicPropertyPopUporAngled(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = beltSorterDivertInfo.type == DivertType.Angled;
        }

        [Browsable(false)]
        public override string LineReleasePhotocellName
        {
            get{return base.LineReleasePhotocellName;}
            set{base.LineReleasePhotocellName = value;}
        }

        [Browsable(false)]
        public override float Speed
        {
            get { return base.Speed; }
            set{base.Speed = value;}
        }

        private MHEControl controllerProperties;
        private IController controller;

        [Browsable(false)]
        public IController Controller
        {
            get
            {
                return controller;
            }
            set
            {                
                if (value != null)
                {   //If the PLC is deleted then any conveyor referencing the PLC will need to remove references to the deleted PLC.           
                    value.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    value.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                else if (controller != null && value == null)
                {
                    controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent -= controller_OnControllerRenamedEvent;
                }

                controller = value;
                Core.Environment.Properties.Refresh();
            }
        }

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            beltSorterDivertInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            beltSorterDivertInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        public void DynamicPropertyLocalControl(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller == null;
        }

        public override string Category
        {
            get { return "Belt Sorter"; }
        }

        public override Image Image
        {
            get {
                if (beltSorterDivertInfo.type == DivertType.Angled)
                {
                    return Common.Icons.Get("BeltSorterDivert"); 
                }
                return Common.Icons.Get("BeltSorterDivertPopUp");                 
            }
        }
    }

    [Serializable]
    [XmlInclude(typeof(BeltSorterDivertInfo))]
    public class BeltSorterDivertInfo : StraightBeltConveyorInfo, IControllableInfo
    {
        public float divertConveyorOffset = 0;
        public float divertConveyorLength = 1; //From centre point of straight conveyor
        public Side divertSide = Side.Left;
        public DivertRoute defaultRoute = DivertRoute.Straight;
        public CaseConveyorWidth divertWidth = CaseConveyorWidth._500mm;

        public float divertSpeed = 0.7f;
        public bool RemoveFromRoutingTable;
        public bool DivertFullLane = true;
        public bool AlwaysUseDefaultDirection;
        public bool ControllerPoint;
        public float releaseDelay = 0;
        public float divertEndOffset;
        public float divertAngle = (float)Math.PI / 4;
        public bool NextBlocked = false;

        public DivertType type;

        public ControlTypes ControlType;
        #region IControllableInfo

        private string controllerName = "No Controller";
        public string ControllerName
        {
            get{return controllerName;}
            set{controllerName = value;}
        }

        private ProtocolInfo protocolInfo;
        public ProtocolInfo ProtocolInfo
        {
            get{return protocolInfo;}
            set{protocolInfo = value;}
        }

        #endregion

    }

    public class BeltSorterDivertArgs : EventArgs
    {
        public readonly BeltSorterDivert _sender;
        public readonly Load _load;
        public readonly DivertRoute _direction;

        public BeltSorterDivertArgs(BeltSorterDivert sender, Load load, DivertRoute direction)
        {
            _sender = sender;
            _load = load;
            _direction = direction;
        }
    }

}