using Experior.Catalog.Logistic.Track;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
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
    public class AngledDivert : StraightBeltConveyor, IControllable
    {
        AngledDivertInfo angledDivertInfo;
        StraightConveyor divertSection;
        ActionPoint apStraight = new ActionPoint();
        ActionPoint apDivert = new ActionPoint();

        Timer ReleaseDelayTimer = new Timer(0);
               
        LoadWaitingStatus PreviousLoadWaitingStatus;

        //private Load WaitingLoad = null; //Handle on the load that is waiting on the previous conveyor to be released in

        //The events below should call RouteLoad
        public event EventHandler<AngleDivertArgs> OnDivertPointArrivedControl;
        public event EventHandler<AngleDivertArgs> OnDivertPointDivertedControl;

        public static event EventHandler<AngleDivertArgs> OnDivertPointArrivedRoutingScript;
        public static event EventHandler<AngleDivertArgs> OnDivertPointDivertedRoutingScript;

        float divertCentreOffset;
        DivertRoute SelectedRoute; //used to rember the last route that a load was sent to when using local control        

        public AngledDivert(AngledDivertInfo info) : base(info)
        {
            angledDivertInfo = info;
            StraightConveyorInfo divertSectionInfo = new StraightConveyorInfo() 
            { 
                Length    = info.DivertConveyorLength, 
                thickness = info.thickness, 
                Width     = info.width, 
                Speed     = info.divertSpeed,
                color     = info.color
            };

            divertSection                          = new StraightConveyor(divertSectionInfo) ;
            divertSection.startLine.Visible        = false;
            divertSection.StartFixPoint.Visible    = false;
            divertSection.StartFixPoint.Enabled    = false;
            divertSection.EndFixPoint.OnSnapped   += DivertSectionEndFixPoint_OnSnapped;
            divertSection.EndFixPoint.OnUnSnapped += DivertSectionEndFixPoint_OnUnSnapped;

            Add(divertSection);
            DivertAngle = info.divertAngle;

            TransportSection.Route.InsertActionPoint(apStraight);
            divertSection.TransportSection.Route.InsertActionPoint(apDivert, 0);

            apStraight.OnEnter += apStraight_OnEnter;
            ReleaseDelayTimer.OnElapsed += ReleaseDelayTimer_OnElapsed;
              
            RouteAvailable = RouteStatuses.Request;

            DivertConveyorOffset = info.DivertConveyorOffset;

            if (beltControl.LineReleasePhotocell != null)
            {
                beltControl.LineReleasePhotocell.Dispose();
            }

            UpdateConveyor();
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(angledDivertInfo, this);
            }
            UpdateConveyor();
        }

        public override void Reset()
        {
            loadInDivert = false;
            loadisWaiting = false;
            base.Reset();
            ThisRouteStatus.Available = RouteStatuses.Request;
        }

        #region Configuration

        void BeltSorterMerge_OnSpeedUpdated(object sender, EventArgs e)
        {
            Speed = ((BaseTrack)sender).Speed;
        }

        public override void EndFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            Speed = ((BaseTrack)stranger.Parent).Speed;
            StraightConveyor nextConveyor = stranger.Parent as StraightConveyor;          

            if (nextConveyor != null)
            {
                nextConveyor.OnSpeedUpdated += BeltSorterMerge_OnSpeedUpdated;
            }

            base.EndFixPoint_OnSnapped(stranger, e);
        }

        public override void StartFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {         
            PreviousConveyor = stranger.Parent as IRouteStatus;
            PreviousLoadWaitingStatus = PreviousConveyor.GetLoadWaitingStatus(stranger);
            PreviousLoadWaitingStatus.OnLoadWaitingChanged += PreviousLoadWaitingStatus_OnLoadWaitingChanged;
        }

        public override void StartFixPoint_OnUnSnapped(FixPoint stranger)
        {
            PreviousConveyor = null;
            PreviousLoadWaitingStatus.OnLoadWaitingChanged -= PreviousLoadWaitingStatus_OnLoadWaitingChanged;
            PreviousLoadWaitingStatus = null;

            Reset();
        }

        public void DivertSectionEndFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            divertSection.NextRouteStatus.OnRouteStatusChanged += DivertRouteStatus_OnAvailableChanged;
        }

        public void DivertSectionEndFixPoint_OnUnSnapped(FixPoint stranger)
        {
            if (divertSection.NextRouteStatus != null)
            {
                divertSection.NextRouteStatus.OnRouteStatusChanged -= DivertRouteStatus_OnAvailableChanged;
            }
        }
        #endregion

        #region Routing Logic

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available && LoadisWaiting)
            {
                RouteLoad(DivertRoute.Straight);
                return;
            }

            if (e._available == RouteStatuses.Available)
            {
                RouteWaitingLoad();
            }
        }

        public void DivertRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if(e._available == RouteStatuses.Available && LoadisWaiting)
            {
                RouteLoad(DivertRoute.Divert);
                return;
            }
            
            if (e._available == RouteStatuses.Available)
            {
                RouteWaitingLoad();
            }
        }

        void PreviousLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {     
            if (e._loadWaiting)
            {
                RouteWaitingLoad();
            }
            else
            {
                if (ReleaseDelayTimer.Running)
                {
                    ThisRouteStatus.Available = RouteStatuses.Blocked;
                }
                else
                {
                    ThisRouteStatus.Available = RouteStatuses.Request;
                }
            }
            
            if (e._loadDeleted)
            {
                ThisRouteStatus.Available = RouteStatuses.Request;
                LoadInDivert = false;
            }
        }

        private void RouteWaitingLoad()
        {
            if (PreviousLoadWaitingStatus != null && PreviousLoadWaitingStatus.LoadWaiting && !LoadInDivert)
            {
                if (ControlType == ControlTypes.Local)
                {
                    if (LocalControlStratergy != AngledDivertLocalControl.None)
                    {
                        SelectedRoute = SelectRoute();                       
                    }
                    else
                    {
                        SelectedRoute = DefaultRoute;
                    }

                    if (SelectedRoute != DivertRoute.None)
                    {
                        RouteLoad(SelectedRoute);
                    }
                }
                else if (ControlType == ControlTypes.Controller)
                {
                    if (OnDivertPointArrivedControl != null) //Controller handles load arrival at entry point the controller will call RouteLoad
                    {
                        OnDivertPointArrivedControl(this, new AngleDivertArgs(this, PreviousLoadWaitingStatus.WaitingLoad, DivertRoute.None));                           
                    }
                }
                else if (ControlType == ControlTypes.Project_Script)
                {
                    if (OnDivertPointArrivedRoutingScript != null) //Routing script will call RouteLoad
                    {
                        OnDivertPointArrivedRoutingScript(this, new AngleDivertArgs(this, PreviousLoadWaitingStatus.WaitingLoad, DivertRoute.None));
                    }
                }
            }
        }

        /// <summary>
        /// Can be called from a controller of project script
        /// </summary>
        /// <param name="route"></param>
        public void RouteLoad(DivertRoute route)
        {
            SelectedRoute = route;

            //Check that the route is available before releasing the load, can switch route if default route is opposite and is available
            if (RouteBlockedBehaviour == RouteBlocked.Route_To_Default && !LoadisWaiting)
            {
                if (route == DivertRoute.Divert && divertSection.NextRouteStatus.Available != RouteStatuses.Available && DefaultRoute == DivertRoute.Straight && NextRouteStatus.Available == RouteStatuses.Available)
                {
                    SelectedRoute = DivertRoute.Straight;
                }
                else if (route == DivertRoute.Straight && NextRouteStatus.Available != RouteStatuses.Available && DefaultRoute == DivertRoute.Divert && divertSection.NextRouteStatus.Available == RouteStatuses.Available)
                {
                    SelectedRoute = DivertRoute.Divert;
                }
                else if (NextRouteStatus.Available != RouteStatuses.Available && divertSection.NextRouteStatus.Available != RouteStatuses.Available) //Neither route is available
                {
                    LoadisWaiting = true; //The load cannot be routed so need to wait for route available changes to take effect
                    return;
                }
            }
            else if (RouteBlockedBehaviour == RouteBlocked.Wait_Until_Route_Available)
            {
                SelectedRoute = route;
                if ((route == DivertRoute.Divert && divertSection.NextRouteStatus.Available != RouteStatuses.Available) || (route == DivertRoute.Straight && NextRouteStatus.Available != RouteStatuses.Available))
                {
                    LoadisWaiting = true;
                    return;
                }
            }

            LoadisWaiting = false;
            ThisRouteStatus.Available = RouteStatuses.Available; //This will release the load
            LoadInDivert = true;

            if (NextRouteStatus != null && route == DivertRoute.Straight && NextRouteStatus.Available == RouteStatuses.Available)
            {
                if (SelectedRoute == DivertRoute.Straight && StraigthReleaseDelay > 0)
                {
                    ReleaseDelayTimer.Timeout = StraigthReleaseDelay;
                    ReleaseDelayTimer.Reset();
                    ReleaseDelayTimer.Start();
                }             
            }
            else if (divertSection.NextRouteStatus != null && route == DivertRoute.Divert && divertSection.NextRouteStatus.Available == RouteStatuses.Available)
            {
                if (SelectedRoute == DivertRoute.Divert && DivertReleaseDelay > 0)
                {
                    ReleaseDelayTimer.Timeout = DivertReleaseDelay;
                    ReleaseDelayTimer.Reset();
                    ReleaseDelayTimer.Start();
                }
            }
        }

        /// <summary>
        /// Chooses the next avaiable route according to LocalControlStratergy used
        /// </summary>
        /// <returns>DivertRoute</returns>
        private DivertRoute SelectRoute()
        {
            //simple case of 1 or zero sections attached to the divert
            if (NextRouteStatus == null && divertSection.NextRouteStatus != null)
            {
                return DivertRoute.Divert;
            }
            else if (NextRouteStatus != null && divertSection.NextRouteStatus == null)
            {
                return DivertRoute.Straight;
            }
            else if (NextRouteStatus == null && divertSection.NextRouteStatus == null)
            {
                return DivertRoute.None;
            }

            if (LocalControlStratergy == AngledDivertLocalControl.Round_Robin)
            {
                if (SelectedRoute == DivertRoute.Divert && NextRouteStatus.Available == RouteStatuses.Available) //Last route was divert check straight for avaiability
                {
                    return DivertRoute.Straight;
                }
                else if (SelectedRoute == DivertRoute.Straight && divertSection.NextRouteStatus.Available == RouteStatuses.Available) // Last route was straight check divert for aviability
                {
                    return DivertRoute.Divert;
                }
            }
            else if (LocalControlStratergy == AngledDivertLocalControl.Route_To_Default)
            {
                if (DefaultRoute == DivertRoute.Straight && NextRouteStatus.Available == RouteStatuses.Available)
                {
                    return DivertRoute.Straight;
                }
                else if (DefaultRoute == DivertRoute.Divert && divertSection.NextRouteStatus.Available == RouteStatuses.Available)
                {
                    return DivertRoute.Divert;
                }
            }

            if (NextRouteStatus.Available == RouteStatuses.Available) //At this point just choose the one that is avaiable; same for both statergies.
            {
                return DivertRoute.Straight;
            }
            else if (divertSection.NextRouteStatus.Available == RouteStatuses.Available)
            {
                return DivertRoute.Divert;
            }

            return DivertRoute.None;
        }

        void ReleaseDelayTimer_OnElapsed(Timer sender)
        {
            LoadInDivert = false;
            ThisRouteStatus.Available = RouteStatuses.Request;
            RouteWaitingLoad();
        }

        void apStraight_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            if (SelectedRoute == DivertRoute.Straight)
            {
                RouteLoadStraight(load);
            }
            else if (SelectedRoute == DivertRoute.Divert)
            {
                RouteLoadDivert(load);
            }
        }

        private void RouteToDefault(Load load)
        {
            if (DefaultRoute == DivertRoute.Divert)
            {
                load.Switch(apDivert);
            }
            else
            {
                load.Switch(apStraight);
            }

            if (!ReleaseDelayTimer.Running)
            {
                LoadInDivert = false;
                RouteWaitingLoad();
            }
        }

        private void RouteLoadStraight(Load load)
        {
            if (!ReleaseDelayTimer.Running)
            {
                LoadInDivert = false;
                RouteWaitingLoad();
            }

            if (OnDivertPointDivertedControl != null)
            {
                OnDivertPointDivertedControl(this, new AngleDivertArgs(this, load, DivertRoute.Straight));
            }
            if (ControlType == ControlTypes.Project_Script && OnDivertPointDivertedRoutingScript != null)
            {
                OnDivertPointDivertedRoutingScript(this, new AngleDivertArgs(this, load, DivertRoute.Straight));
            }
        }            

        private void RouteLoadDivert(Load load)
        {
            ClearPreviousPhotocells(load);
            load.Switch(apDivert);

            if (!ReleaseDelayTimer.Running)
            {
                LoadInDivert = false;
                RouteWaitingLoad();
            }

            if (OnDivertPointDivertedControl != null)
            {
                OnDivertPointDivertedControl(this, new AngleDivertArgs(this, load, DivertRoute.Divert));
            }
            if (ControlType == ControlTypes.Project_Script && OnDivertPointDivertedRoutingScript != null)
            {
                OnDivertPointDivertedRoutingScript(this, new AngleDivertArgs(this, load, DivertRoute.Divert));
            }
        }

        private void ClearPreviousPhotocells(Load load)
        {
            //Look back at the previous conveyor and clear the load from any photocells that it may still be covering
            //This is to stop the load from locking up the divert when the divert point is closer than half the load
            //length away from the feeding conveyor photocell.

            for (int i = load.Route.LastRoute.ActionPoints.Count - 1; i >= 0; i--)
            {
                ActionPoint actionPoint = load.Route.LastRoute.ActionPoints[i];
                if (actionPoint is DematicSensor && actionPoint.ActiveLoad == load)
                {
                    ((DematicSensor)actionPoint).ForceLoadClear(load);
                    return;
                }
            }
        }

        #endregion

        #region Helper Methods


        public override void UpdateConveyor()
        {
            apStraight.Distance  = DivertConveyorOffset;
            divertSection.Width = (float)angledDivertInfo.divertWidth / 1000;
            divertSection.Length = DivertLength;

            float zOffset = (float)(Math.Sin(DivertAngle) * (divertSection.Length / 2));         

            if (DivertSide == Side.Left)
            {
                divertSection.LocalPosition = new Vector3(divertCentreOffset - angledDivertInfo.DivertConveyorOffset, divertSection.LocalPosition.Y, -zOffset);
                divertSection.LocalYaw = -DivertAngle;
            }
            else
            {
                divertSection.LocalPosition = new Vector3(divertCentreOffset - angledDivertInfo.DivertConveyorOffset, divertSection.LocalPosition.Y, zOffset);
                divertSection.LocalYaw = DivertAngle;
            }
            
            divertSection.arrow.LocalPosition = new Vector3(-divertSection.Length / 2 + 0.2f, 0, 0);

            //divertSection.EndFixPoint.LocalPosition = new Vector3(-divertSection.Length / 2, 0, angledDivertInfo.divertEndOffset);
            divertSection.SnapEndTransformation.Offset = new Vector3(0, 0, angledDivertInfo.divertEndOffset);
        }
        #endregion

        #region User Interface

        #region Size and Speed

        bool autoAdjust;

        [Category("Size and Speed")]
        [DisplayName("AutoAlign")]
        [PropertyOrder(1)]
        [Description("Prints the settings that to adjust length and offset to align the conveyors.\nToggle back to true for printout.")]
        public bool PrintAutoAdjust
        {
            get { return autoAdjust; }
            set
            {
                autoAdjust = value;

                if (value)
                {
                    float adjustedLength = (float)(divertSection.Width / (Math.Sin(DivertAngle)));
                    float AdjustedOffset = (float)(adjustedLength / 2 - ((Width / 2) / Math.Tan(DivertAngle)));
                    Log.Write("Adjusted Length to fit: " + adjustedLength);
                    Log.Write("Adjusted Offset to fit: " + AdjustedOffset);
                }
            }
        }

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
                if (value > DivertConveyorOffset)
                {
                    base.Length = value;
                    divertCentreOffset = (float)((value / 2) - (Math.Cos(DivertAngle) * (DivertLength / 2)));
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
                else
                {
                    Core.Environment.Log.Write(string.Format("Conveyor Length must not be less than 'Divert Conveyor Offset' ({0}).", DivertConveyorOffset), System.Drawing.Color.Red);
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Length")]        
        [Description("Length of the Divert conveyor (meter)")]
        [TypeConverter()]
        [PropertyOrder(3)]
        public float DivertLength
        {
            get
            {
                return angledDivertInfo.DivertConveyorLength;
            }
            set
            {
                if (value > 0)
                {
                    divertCentreOffset = (float)((Length / 2) - (Math.Cos(DivertAngle) * (value / 2)));
                    angledDivertInfo.DivertConveyorLength = value;
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
            get { return angledDivertInfo.conveyorWidth; }
            set
            {
                angledDivertInfo.conveyorWidth = value;
                Width = (float)value / 1000;
                //Core.Environment.InvokeEvent(new Action(UpdateConveyor));
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Width")]
        [Description("Width of the divert section conveyor based on standard Dematic case conveyor widths")]
        [PropertyOrder(5)]
        public CaseConveyorWidth DivertWidth
        {
            get { return angledDivertInfo.divertWidth; }
            set
            {
                angledDivertInfo.divertWidth = value;
                divertSection.Width = (float)value / 1000;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Speed")]
        [PropertyOrder(6)]
        [Description("Speed of the Divert section conveyor (Speed of straight section is taken from the next conveyor)")]
        [TypeConverter(typeof(SpeedConverter))]
        public float DivertSpeed
        {
            get { return angledDivertInfo.divertSpeed; }
            set
            {
                angledDivertInfo.divertSpeed = value;
                divertSection.Speed = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Angle")]
        [Description("The divert angle in degrees")]
        [PropertyOrder(7)]
        [TypeConverter(typeof(Rad2AngleConverter))]
        public float DivertAngle
        {
            get { return angledDivertInfo.divertAngle; }
            set
            {
                {
                    angledDivertInfo.divertAngle = value;
                    divertCentreOffset = (float)((Length / 2) - (Math.Cos(value) * (DivertLength / 2)));
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Offset")]
        [Description("Distance from start of conveyor until the divert conveyor (meter)")]
        [TypeConverter()]
        [PropertyOrder(8)]
        public float DivertConveyorOffset
        {
            get { return angledDivertInfo.DivertConveyorOffset; }
            set
            {
                angledDivertInfo.DivertConveyorOffset = value;
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
            get { return angledDivertInfo.divertSide; }
            set
            {
                angledDivertInfo.divertSide = value;
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

        private bool loadInDivert;
        private bool LoadInDivert
        {
            get { return loadInDivert; }
            set { loadInDivert = value; }
        }

        private bool loadisWaiting;
        private bool LoadisWaiting
        {
            get { return loadisWaiting; }
            set { loadisWaiting = value; }
        }

        [Browsable(false)]
        public override float Speed
        {
            get { return base.Speed; }
            set
            {
                base.Speed = value;
            }
        }

        [Browsable(false)]
        public override float EndOffset
        {
            get{return base.EndOffset;}
            set{base.EndOffset = value;}
        }

        [Category("Fix Points")]
        [DisplayName("Divert End Offset")]
        [Description("Move the fix point position in the local Z axis (meter)")]
        [PropertyOrder(2)]
        public float DivertEndOffset
        {
            get { return angledDivertInfo.divertEndOffset; }
            set
            {
                angledDivertInfo.divertEndOffset = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        #endregion

        [Category("Routing")]
        [DisplayName("Control Type")]
        [Description("Defines if the control is handled by a controller, by a routing script or uses only local control. ")]       
        [PropertyOrder(19)]
        public ControlTypes ControlType
        {
            get{return angledDivertInfo.ControlType;}
            set
            {
                angledDivertInfo.ControlType = value;
                if (ControllerProperties != null && value != ControlTypes.Controller)
                {
                    ControllerName = "No Controller";
                }
                Core.Environment.Properties.Refresh();
            }
        }

        #region IControllable Implementation

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [PropertyAttributesProvider("DynamicPropertyControllers")]
        [PropertyOrder(20)]
        [TypeConverter(typeof(CaseControllerConverter))]
        public string ControllerName
        {
            get{ return angledDivertInfo.ControllerName;}
            set
            {
                if (!value.Equals(angledDivertInfo.ControllerName))
                {
                    ControllerProperties = null;
                    angledDivertInfo.ProtocolInfo = null;
                    Controller = null;
                }

                angledDivertInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(angledDivertInfo, this);
                    if (ControllerProperties == null)
                    {
                        angledDivertInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        public void DynamicPropertyControllers(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = angledDivertInfo.ControlType == ControlTypes.Controller;
        }

        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(21)]
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
        [DisplayName("Default Route")]
        [Description("Set the default route, routing will be controlled by the controller")]
        [PropertyOrder(1)]
        public DivertRoute DefaultRoute
        {
            get{ return angledDivertInfo.DefaultRoute;}
            set
            {
                angledDivertInfo.DefaultRoute = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Straight Release Delay")]
        [PropertyOrder(2)]
        [Description("Delay time to allow load into merge/divert after the previous load has cleared the conveyor when going in the straight direction (Not diverting)")]
        public float StraigthReleaseDelay
        {
            get { return angledDivertInfo.straightReleaseDelay; }
            set
            {
                angledDivertInfo.straightReleaseDelay = value;
            }
        }

        [Category("Routing")]
        [DisplayName("Divert Release Delay")]
        [PropertyOrder(3)]
        [Description("Delay time to allow load into merge/divert after the previous load has cleared the conveyor when diverting and not going straight")]
        public float DivertReleaseDelay
        {
            get { return angledDivertInfo.divertRreleaseDelay; }
            set
            {
                angledDivertInfo.divertRreleaseDelay = value;
            }
        }


        [Category("Routing")]
        [DisplayName("Local Control Stratergy")]
        [Description("Defines what should happen under local control.\n\nRound_Robin will route loads alternatitly to divert and straight until one route becomes blocked then will route to the avaiable route but will always try to round robin.\n\nRoute_To_Default will route all loads to the default route until it is blocked.When default is blocked will route to avaiable route but will always try to route to the Default.\n\nNone doesn't do anything.")]
        [PropertyAttributesProvider("DynamicPropertyLocalControlStratergy")]
        [PropertyOrder(4)]
        public AngledDivertLocalControl LocalControlStratergy
        {
            get { return angledDivertInfo.localControlStratergy; }
            set 
            {
                angledDivertInfo.localControlStratergy = value;
            }
        }

        #region Move to a MHE control object as it has no function in the assembly

        Timer RouteBlockedTimer = new Timer(10);//Used only for a controller ???

        //Used only for a controller ???
        [Category("Routing")]
        [DisplayName("Route Blocked Behaviour")]
        [Description("Define what the happens to the load when the routed destination is blocked.")]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        [PropertyOrder(22)]
        public RouteBlocked RouteBlockedBehaviour
        {
            get { return angledDivertInfo.routeBlockedBehaviour; }
            set
            {
                angledDivertInfo.routeBlockedBehaviour = value;
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        //Used only for a controller ???
        [Category("Routing")]
        [DisplayName("Route Blocked TimeOut")]
        [Description("Define how long a load should wait if the route is blocked")]
        [PropertyAttributesProvider("DynamicPropertyRouteBlockedTimeout")]
        [PropertyOrder(23)]
        public float RouteBlockedTimeout
        {
            get { return angledDivertInfo.routeBlockedTimeout; }
            set
            {
                if (RouteBlockedTimer.Running)
                {
                    RouteBlockedTimer.Stop();
                    RouteBlockedTimer.Timeout = value;
                    RouteBlockedTimer.Start();
                }
                else
                {
                    RouteBlockedTimer.Timeout = value;
                }
                angledDivertInfo.routeBlockedTimeout = value;
            }
        }

        //Used only for a controller ???
        public void DynamicPropertyRouteBlockedTimeout(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = RouteBlockedBehaviour == RouteBlocked.Wait_Timeout && Controller != null;
        }

        #endregion

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        public void DynamicPropertyLocalControlStratergy(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = ControlType == ControlTypes.Local;
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

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            angledDivertInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            angledDivertInfo.ProtocolInfo = null;
        }

        #endregion
    }

    

    [Serializable]
    [XmlInclude(typeof(AngledDivertInfo))]
    public class AngledDivertInfo : StraightBeltConveyorInfo, IControllableInfo
    {
        //Divert Conveyor Section
        public float DivertConveyorOffset = 0;
        public float DivertConveyorLength = 1; //From centre point of straight conveyor
        public Side divertSide = Side.Left;
        public DivertRoute DefaultRoute   = DivertRoute.Straight;
        public CaseConveyorWidth divertWidth = CaseConveyorWidth._500mm;

        public float divertAngle;
        public float divertSpeed = 0.7f;
        public bool RemoveFromRoutingTable;
        public bool DivertFullLane = true;
        public bool AlwaysUseDefaultDirection;
        public bool ControllerPoint;
        public float straightReleaseDelay = 0;
        public float divertRreleaseDelay = 0;
        public string type;
        public float divertEndOffset;
        public AngledDivertLocalControl localControlStratergy = AngledDivertLocalControl.None;

        public ControlTypes ControlType;
        //Routing Info
        public RouteBlocked routeBlockedBehaviour = RouteBlocked.Wait_Until_Route_Available;
        public float routeBlockedTimeout = 10;

        private string controllerName = "No Controller";
        private ProtocolInfo protocolInfo;

        public string ControllerName
        {
            get{return controllerName;}
            set{controllerName = value;}
        }

        public ProtocolInfo ProtocolInfo
        {
            get{return protocolInfo;}
            set{protocolInfo = value;}
        }
    }

    public class AngleDivertArgs : EventArgs
    {
        public readonly AngledDivert _sender;
        public readonly Load _load;
        public readonly DivertRoute _direction;

        public AngleDivertArgs(AngledDivert sender, Load load, DivertRoute direction)
        {
            _sender = sender;
            _load = load;
            _direction = direction;
        }
    }

}
