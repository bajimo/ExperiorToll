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
        private readonly PickPutStationInfo pickPutStationInfo;
        private readonly StraightConveyor pickConveyorIn;
        private readonly StraightConveyor putConveyorIn;
        private readonly StraightConveyor pickConveyorOut;
        private readonly StraightConveyor putConveyorOut;
        private readonly ActionPoint pickIn, pickStation, pickClear, putIn, putStation, putClear;
        private readonly ActionPoint leavingPick, leavingPut, enteringPick, enteringPut;
        private readonly Cube leftSide, rightSide, front, bottom;

        //logic
        private readonly RapidLiftControl pickLiftControl, putLiftControl;

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
            pickStation = pickConveyorOut.TransportSection.Route.InsertActionPoint(0.35f);
            pickClear = pickConveyorOut.TransportSection.Route.InsertActionPoint(0.7f);
            pickClear.Edge = ActionPoint.Edges.Trailing;

            putIn = putConveyorIn.TransportSection.Route.InsertActionPoint(1.2f);
            putStation = putConveyorOut.TransportSection.Route.InsertActionPoint(0.35f);
            putClear = putConveyorOut.TransportSection.Route.InsertActionPoint(0.7f);
            putClear.Edge = ActionPoint.Edges.Trailing;

            pickLiftControl = new RapidLiftControl(pickIn, pickStation, pickClear, ArrivedAtPickPosition, UpdatePickConveyorStatus);
            putLiftControl = new RapidLiftControl(putIn, putStation, putClear, ArrivedAtPutPosition, UpdatePutConveyorStatus);

            enteringPick = pickConveyorIn.TransportSection.Route.InsertActionPoint(0);
            enteringPick.Edge = ActionPoint.Edges.Trailing;
            enteringPick.OnEnter += EnteringPick_OnEnter;

            leavingPick = pickConveyorOut.TransportSection.Route.InsertActionPoint(pickConveyorOut.Length - 0.05f);
            leavingPick.Edge = ActionPoint.Edges.Leading;
            leavingPick.OnEnter += LeavingPick_OnEnter;

            enteringPut = putConveyorIn.TransportSection.Route.InsertActionPoint(0);
            enteringPut.Edge = ActionPoint.Edges.Trailing;
            enteringPut.OnEnter += EnteringPut_OnEnter;

            leavingPut = putConveyorOut.TransportSection.Route.InsertActionPoint(putConveyorOut.Length - 0.05f);
            leavingPut.Edge = ActionPoint.Edges.Leading;
            leavingPut.OnEnter += LeavingPut_OnEnter;

            //Graphics
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

            pickConveyorOut.OnNextRouteStatusAvailableChanged += PickConveyorOut_OnNextRouteStatusAvailableChanged;
            putConveyorOut.OnNextRouteStatusAvailableChanged += PutConveyorOut_OnNextRouteStatusAvailableChanged;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
        }

        private void EnteringPut_OnEnter(ActionPoint sender, Load load)
        {
            UpdatePutConveyorStatus();
        }

        private void EnteringPick_OnEnter(ActionPoint sender, Load load)
        {
            UpdatePickConveyorStatus();
        }

        private void UpdatePickConveyorStatus()
        {
            if (pickConveyorIn.TransportSection.Route.Loads.Count >= 2)
            {
                pickConveyorIn.RouteAvailable = RouteStatuses.Blocked;
            }
            else
            {
                pickConveyorIn.RouteAvailable = RouteStatuses.Available;
            }
        }

        private void UpdatePutConveyorStatus()
        {
            if (putConveyorIn.TransportSection.Route.Loads.Count >= 2)
            {
                putConveyorIn.RouteAvailable = RouteStatuses.Blocked;
            }
            else
            {
                putConveyorIn.RouteAvailable = RouteStatuses.Available;
            }
        }

        private void LeavingPut_OnEnter(ActionPoint sender, Load load)
        {
            if (putConveyorOut.NextRouteStatus.Available != RouteStatuses.Available)
                load.Stop();
        }

        private void PutConveyorOut_OnNextRouteStatusAvailableChanged(object sender, Experior.Dematic.Base.Devices.RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available && leavingPut.Active)
            {
                leavingPut.Release();
            }
        }

        private void LeavingPick_OnEnter(ActionPoint sender, Load load)
        {
            if (pickConveyorOut.NextRouteStatus.Available != RouteStatuses.Available)
                load.Stop();
        }

        private void PickConveyorOut_OnNextRouteStatusAvailableChanged(object sender, Experior.Dematic.Base.Devices.RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available && leavingPick.Active)
            {
                leavingPick.Release();
            }
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
            enteringPick.OnEnter -= EnteringPick_OnEnter;
            enteringPut.OnEnter -= EnteringPut_OnEnter;
            pickConveyorOut.OnNextRouteStatusAvailableChanged -= PickConveyorOut_OnNextRouteStatusAvailableChanged;
            putConveyorOut.OnNextRouteStatusAvailableChanged -= PutConveyorOut_OnNextRouteStatusAvailableChanged;
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            leavingPick.OnEnter -= LeavingPick_OnEnter;
            leavingPut.OnEnter -= LeavingPut_OnEnter;
            pickLiftControl.Dispose();
            putLiftControl.Dispose();
            base.Dispose();
        }

        public override void Reset()
        {
            base.Reset();
            pickLiftControl.Reset();
            putLiftControl.Reset();
            pickConveyorIn.RouteAvailable = RouteStatuses.Available;
            putConveyorIn.RouteAvailable = RouteStatuses.Available;
        }

        [Browsable(false)]
        public string PickBarcode
        {
            get { return pickStation.ActiveLoad is Case_Load ? ((Case_Load)pickStation.ActiveLoad).SSCCBarcode : null; }
        }

        [Browsable(false)]
        public string PutBarcode
        {
            get { return putStation.ActiveLoad is Case_Load ? ((Case_Load)putStation.ActiveLoad).SSCCBarcode : null; }
        }

        private StraightConveyorInfo NewStraightInfo()
        {
            StraightConveyorInfo straightInfo = new StraightConveyorInfo();
            straightInfo.Length = 1.6f;
            straightInfo.Speed = 1;
            straightInfo.spacing = 0.1f;
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

        public override string Category { get; } = "GTP";
        public override Image Image { get; }

        private class RapidLiftControl
        {
            private readonly ActionPoint bottom, top, clear;
            private bool lifting;
            private readonly Action<PickPutStationArrivalArgs> arrival;
            private readonly Action liftingFinished;

            public RapidLiftControl(ActionPoint bottom, ActionPoint top, ActionPoint clear, Action<PickPutStationArrivalArgs> arrival, Action liftingFinished)
            {
                this.arrival = arrival;
                this.liftingFinished = liftingFinished;
                this.bottom = bottom;
                this.top = top;
                this.clear = clear;

                bottom.OnEnter += Bottom_OnEnter;
                top.OnEnter += Top_OnEnter;
                clear.OnEnter += Clear_OnEnter;
            }

            private void Clear_OnEnter(ActionPoint sender, Load load)
            {
                if (lifting)
                    return;
                if (top.Active)
                    return;

                if (bottom.Active)
                {
                    Bottom_OnEnter(bottom, bottom.ActiveLoad);
                }
            }

            private void Top_OnEnter(ActionPoint sender, Load load)
            {
                lifting = false;
                liftingFinished();
                arrival(new PickPutStationArrivalArgs(load));
            }

            private void Bottom_OnEnter(ActionPoint sender, Load load)
            {
                load.Stop();

                if (lifting)
                    return;

                if (top.Active)
                    return;

                lifting = true;

                var distance = top.Position - bottom.Position;
                load.Translate(() => load.Switch(top, true), distance, 1.5f);
            }

            public void Reset()
            {
                lifting = false;
            }

            public void Dispose()
            {
                bottom.OnEnter -= Bottom_OnEnter;
                top.OnEnter -= Top_OnEnter;
                clear.OnEnter -= Clear_OnEnter;
            }
        }



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
