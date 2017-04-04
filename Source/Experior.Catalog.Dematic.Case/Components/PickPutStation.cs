using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Experior.Core.Assemblies;
using Experior.Core.Motors;
using Experior.Core.Parts;
using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class PickPutStation : Assembly, IControllable
    {
        PickPutStationInfo pickPutStationInfo;
        //StraightConveyor liftConveyor;
        StraightConveyor pickConveyorIn;
        StraightConveyor putConveyorIn;
        StraightConveyor pickConveyorOut;
        StraightConveyor putConveyorOut;
        ActionPoint pickIn, pickStation, putIn, putStation;

        Cube leftSide, rightSide, front, bottom;
        //ActionPoint feed, lower1, lower2, upper1, upper2, end;
        //int loadCount = 0;
        //Load feedToLower1 = null;
        //Load lower1ToLower2 = null;
        //Load lower2ToEnd = null;
        //Load waitingToLower1 = null;
        //Load waitingToLower2 = null;
        //Load lowering1 = null;
        //Load lowering2 = null;
        //Load lastUpper2 = null;

        //IControllable
        private MHEControl controllerProperties;
        private IController controller;

        //Events
        public event EventHandler<PickPutStationArrivalArgs> OnArrivedAtPickPosition;
        protected virtual void ArrivedAtPickPosition(PickPutStationArrivalArgs e)
        {
            OnArrivedAtPickPosition?.Invoke(this, e);
        }

        public event EventHandler<PickPutStationArrivalArgs> OnArrivedAtPutPosition;
        protected virtual void ArrivedAtPutPosition(PickPutStationArrivalArgs e)
        {
            OnArrivedAtPutPosition?.Invoke(this, e);
        }

        public PickPutStation(PickPutStationInfo info) : base(info)
        {
            pickPutStationInfo = info;

            pickConveyorIn = new StraightConveyor(NewStraightInfo());
            Add(pickConveyorIn);
            pickConveyorIn.LocalPosition = new Vector3(0, 0, -0.4f);
            putConveyorIn = new StraightConveyor(NewStraightInfo());
            Add(putConveyorIn);
            putConveyorIn.LocalPosition = new Vector3(0, 0, 0.4f);

            pickConveyorIn.EndFixPoint.Dispose();
            putConveyorIn.EndFixPoint.Dispose();

            pickConveyorOut = new StraightConveyor(NewStraightInfo());
            Add(pickConveyorOut);
            pickConveyorOut.LocalPosition = new Vector3(0, 0.85f, -0.4f);
            pickConveyorOut.LocalYaw = (float)Math.PI;
            putConveyorOut = new StraightConveyor(NewStraightInfo());
            Add(putConveyorOut);
            putConveyorOut.LocalPosition = new Vector3(0, 0.85f, 0.4f);
            putConveyorOut.LocalYaw = (float)Math.PI;

            pickConveyorOut.StartFixPoint.Dispose();
            putConveyorOut.StartFixPoint.Dispose();

            pickIn = pickConveyorIn.TransportSection.Route.InsertActionPoint(1.2f);
            pickStation = pickConveyorOut.TransportSection.Route.InsertActionPoint(0.2f);

            leftSide = new Cube(Color.Gray, 1, 1.2f, 0.05f);
            Add(leftSide);
            leftSide.LocalPosition = new Vector3(-putConveyorIn.Length / 2 + leftSide.Length / 2, leftSide.Height / 2, putConveyorIn.LocalPosition.Z + putConveyorIn.Width / 2 + leftSide.Width / 2);

            rightSide = new Cube(Color.Gray, 1, 1.2f, 0.05f);
            Add(rightSide);
            rightSide.LocalPosition = new Vector3(-putConveyorIn.Length / 2 + leftSide.Length / 2, leftSide.Height / 2, pickConveyorIn.LocalPosition.Z - pickConveyorIn.Width / 2 - leftSide.Width / 2);

            front = new Cube(Color.Gray, 0.05f, 1.0f, leftSide.LocalPosition.Z - rightSide.LocalPosition.Z + leftSide.Width);
            Add(front);
            front.LocalPosition = new Vector3(-putConveyorIn.Length / 2 - front.Length / 2, front.Height / 2, 0);

            bottom = new Cube(Color.Gray, pickConveyorIn.Length + 1, 0.05f, front.Width);
            Add(bottom);
            bottom.LocalPosition = new Vector3(-0.5f, -0.025f, 0);

            //liftConveyor = new StraightConveyor(NewStraightInfo(info));
            //Add(liftConveyor);

            //liftConveyor.ConveyorWidth = CaseConveyorWidth._500mm;
            //liftConveyor.StartFixPoint.Visible = false;
            //liftConveyor.EndFixPoint.Visible = false;
            //liftConveyor.arrow.Visible = false;
            //liftConveyor.LocalPosition = new Microsoft.DirectX.Vector3(liftConveyor.LocalPosition.X - 0.375f, liftConveyor.LocalPosition.Y + 0.5f, liftConveyor.LocalPosition.Z);
            //liftConveyor.TransportSection.Route.Motor.Stop();

            //feed = TransportSection.Route.InsertActionPoint(0.375f);
            //lower1 = TransportSection.Route.InsertActionPoint(1.125f);
            //lower2 = TransportSection.Route.InsertActionPoint(1.875f);
            //end = TransportSection.Route.InsertActionPoint(Length);

            //upper1 = liftConveyor.TransportSection.Route.InsertActionPoint(0.375f);
            //upper2 = liftConveyor.TransportSection.Route.InsertActionPoint(1.125f);

            //feed.OnEnter += Feed_OnEnter;
            //lower1.OnEnter += Lower1_OnEnter;
            //lower2.OnEnter += Lower2_OnEnter;
            //upper1.OnEnter += Upper1_OnEnter;
            //upper2.OnEnter += Upper2_OnEnter;
            //end.OnEnter += End_OnEnter;

            //feed.Visible = true;
            //lower1.Visible = true;
            //lower2.Visible = true;
            //upper1.Visible = true;
            //upper2.Visible = true;

            //OnNextRouteStatusAvailableChanged += PickDoubleLift_OnNextRouteStatusAvailableChanged;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
        }

        public void Scene_OnLoaded()
        {
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(pickPutStationInfo, this);
            }
        }

        public override void Dispose()
        {
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            base.Dispose();
        }

        //private void Feed_OnEnter(ActionPoint sender, Load load)
        //{
        //    load.Stop();
        //    load.UserDeletable = false;
        //   // ThisRouteStatus.Available = RouteStatuses.Blocked;
        //    InfeedCheck();

        //}
        //private void InfeedCheck()
        //{
        //    if (feed.Active && lowering1 == null && (loadCount < 2 || (lower2.Active && (string)lower2.ActiveLoad.UserData == "PICKED" && upper2.Active && loadCount < 3)))
        //    {
        //        releaseFeed();
        //    }
        //}

        //private void Lower1_OnEnter(ActionPoint sender, Load load)
        //{
        //    load.Stop();
        //    if ((string)load.UserData != "PICKED")
        //    {
        //        feedToLower1 = null;
        //        loadCount++;
        //        //ThisRouteStatus.Available = RouteStatuses.Available;
        //        if (lower2.Active || upper2.Active)
        //        {
        //            load.Translate(() => load.Switch(upper1, true), new Microsoft.DirectX.Vector3(0,0.5f,0), 1.5f);
        //        }
        //        else
        //        {
        //            releaseLower1();
        //        }
        //    }
        //    else //load has been picked an lowered
        //    {
        //        lowering1 = null;
        //        if (!lower2.Active && lowering2 == null)
        //        {
        //            releaseLower1();
        //        }
        //    }
        //}

        //private void Lower2_OnEnter(ActionPoint sender, Core.Loads.Load load)
        //{
        //    if ((string)load.UserData != "PICKED")
        //    {
        //        if (!upper2.Active)
        //        {
        //            load.Stop();
        //            load.Translate(() => load.Switch(upper2, true), new Microsoft.DirectX.Vector3(0, 0.5f, 0), 1.5f);
        //        }
        //        else
        //        {
        //            Log.Write("There is a problem in the picking station, tell Barry he would love to fix it");
        //            Pause();
        //        }
        //    }
        //    else //load has been picked and either lowered or picked at the first station
        //    {
        //        if (lastUpper2 == load)
        //        {
        //            lowering2 = null;
        //        }

        //        //if (NextRouteStatus.Available != Experior.Dematic.Base.RouteStatuses.Available)
        //        //{
        //        //    load.Stop();
        //        //}
        //        //else
        //        //{
        //        //    releaseLower2();
        //        //}
        //    }

        //    //if the load has arrived from the 
        //    if (lower1ToLower2 == load)
        //    {
        //        lower1ToLower2 = null;
        //        if (waitingToLower1 != null)
        //        {
        //            StartLower1();
        //        }
        //        else
        //        {
        //            InfeedCheck();
        //        }
        //    }
        //}

        //private void Upper1_OnEnter(ActionPoint sender, Core.Loads.Load load)
        //{
        //    load.UserData = "PICKED";
        //    ArrivedAtPickPosition(new PickPutStationArrivalArgs(load));
        //}

        //public void SendAwayPosition1()
        //{
        //    if (feedToLower1 == null && lower1ToLower2 == null && !lower1.Active)
        //    {
        //        StartLower1();
        //    }
        //    else
        //    {
        //        waitingToLower1 = upper1.ActiveLoad;
        //    }
        //}

        //private void StartLower1()
        //{
        //    waitingToLower1 = null;
        //    upper1.ActiveLoad.Translate(() => upper1.ActiveLoad.Switch(lower1, true), new Microsoft.DirectX.Vector3(0, -0.5f, 0), 1.5f);
        //    lowering1 = upper1.ActiveLoad;
        //}

        //private void Upper2_OnEnter(ActionPoint sender, Core.Loads.Load load)
        //{
        //    lastUpper2 = load;
        //    load.UserData = "PICKED";

        //    //Is there something at lower1 that could be released?
        //    if (lower1.Active && (string)lower1.ActiveLoad.UserData == "PICKED")
        //    {
        //        releaseLower1();
        //    }
        //    ArrivedAtPutPosition(new PickPutStationArrivalArgs(load));
        //}

        //public void SendAwayPosition2()
        //{
        //    if (lower1ToLower2 == null && lower2ToEnd == null && !lower2.Active)
        //    {
        //        StartLower2();
        //    }
        //    else
        //    {
        //        waitingToLower2 = upper2.ActiveLoad;
        //    }
        //}

        //private void StartLower2()
        //{
        //    waitingToLower2 = null;
        //    upper2.ActiveLoad.Translate(() => upper2.ActiveLoad.Switch(lower2, true), new Microsoft.DirectX.Vector3(0, -0.5f, 0), 1.5f);
        //    lowering2 = upper2.ActiveLoad;
        //}

        //private void End_OnEnter(ActionPoint sender, Core.Loads.Load load)
        //{
        //    lower2ToEnd = null;
        //    load.UserDeletable = true;
        //    load.UserData = null;
        //    loadCount--;

        //    if (waitingToLower2 != null)
        //    {
        //        StartLower2();
        //    }
        //    else
        //    {
        //        if (lower1.Active && (string)lower1.ActiveLoad.UserData == "PICKED")
        //        {
        //            releaseLower1();
        //        }
        //    }

        //    InfeedCheck();
        //}

        //private void PickDoubleLift_OnNextRouteStatusAvailableChanged(object sender, Experior.Dematic.Base.Devices.RouteStatusChangedEventArgs e)
        //{
        //    if (e._available == Experior.Dematic.Base.RouteStatuses.Available && lower2.Active && (string)lower2.ActiveLoad.UserData == "PICKED")
        //    {
        //        releaseLower2();
        //    }
        //}

        //private void releaseFeed()
        //{
        //    if (feed.Active)
        //    {
        //        feedToLower1 = feed.ActiveLoad;
        //        feed.ActiveLoad.Release();
        //    }
        //}

        //private void releaseLower1()
        //{
        //    if (lower1.Active)
        //    {
        //        lower1ToLower2 = lower1.ActiveLoad;
        //        lower1.ActiveLoad.Release();
        //    }
        //}

        //private void releaseLower2()
        //{
        //    if (lower2.Active)
        //    {
        //        lower2ToEnd = lower2.ActiveLoad;
        //        lower2.ActiveLoad.Release();
        //    }
        //}

        public override void Reset()
        {
            base.Reset();
            //loadCount = 0;
            //ThisRouteStatus.Available = Experior.Dematic.Base.RouteStatuses.Available;
            //feedToLower1 = null;
            //lower1ToLower2 = null;
            //lower2ToEnd = null;
            //waitingToLower1 = null;
            //waitingToLower2 = null;
            //lowering1 = null;
            //lowering2 = null;
            //lastUpper2 = null;
        }

        //[Browsable(false)]
        //public string Upper1Barcode
        //{
        //    get { return upper1.Active ? ((Case_Load)upper1.ActiveLoad).SSCCBarcode : null; }
        //}

        //[Browsable(false)]
        //public string Upper2Barcode
        //{
        //    get { return upper2.Active ? ((Case_Load)upper2.ActiveLoad).SSCCBarcode : null; }
        //}

        private StraightConveyorInfo NewStraightInfo()
        {
            StraightConveyorInfo straightInfo = new StraightConveyorInfo();
            straightInfo.Length = 1.6f;
            straightInfo.Speed = 1;
            straightInfo.thickness = 0.05f;
            straightInfo.color = Color.DarkGray;
            straightInfo.conveyorWidth = CaseConveyorWidth._600mm;
            return straightInfo;
        }

        #region IControllable
        /// <summary>
        /// This will be set by setting the ControllerName in method StandardCase.SetMHEControl(commPointInfo, this) !!
        /// </summary>
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

        private void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            if (controller != null)
            {
                controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            }
            ControllerName = "No Controller";
            Controller = null;
            ControllerProperties = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {
            pickPutStationInfo.ControllerName = ((StraightConveyor)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [TypeConverter(typeof(CaseControllerConverter))]
        [PropertyOrder(2)]
        public string ControllerName
        {
            get
            {
                return pickPutStationInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(pickPutStationInfo.ControllerName))
                {
                    ControllerProperties = null;
                    pickPutStationInfo.ProtocolInfo = null;
                    Controller = null;
                }

                pickPutStationInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(pickPutStationInfo, this);
                    if (ControllerProperties == null)
                    {
                        pickPutStationInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        /// <summary>
        /// Generic property for a PLC of any type, DatCom, DCI etc it is set when the ControllerName is set
        /// </summary>
        [Category("Routing")]
        [DisplayName("Controller Setup")]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        [PropertyOrder(3)]
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

        public void DynamicPropertyAssemblyPLCconfig(Core.Properties.PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }

        public override string Category { get; } = "Rapid pick station";
        public override Image Image { get; }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(PickPutStationInfo))]
    public class PickPutStationInfo : AssemblyInfo, IControllableInfo
    {
        private string controllerName = "No Controller";
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
    }

    public class PickPutStationArrivalArgs : EventArgs
    {
        public readonly Load Load;
        public PickPutStationArrivalArgs(Load load)
        {
            Load = load;
        }
    }
}
