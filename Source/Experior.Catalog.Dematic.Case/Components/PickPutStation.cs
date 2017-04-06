using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Experior.Core.Assemblies;
using Experior.Core.Parts;
using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class PickPutStation : Assembly, IControllable
    {
        private readonly PickPutStationInfo pickPutStationInfo;
        private readonly StraightConveyor rightConveyorIn;
        private readonly StraightConveyor leftConveyorIn;
        private readonly StraightConveyor rightConveyorOut;
        private readonly StraightConveyor leftConveyorOut;
        private readonly ActionPoint rightIn, rightStation, rightClear, leftIn, leftStation, leftClear;
        private readonly ActionPoint leavingRight, leavingLeft, enteringRight, enteringLeft;
        private readonly Cube leftSide, rightSide, front, bottom;

        //logic
        private readonly RapidLiftControl rightLiftControl, leftLiftControl;

        //IControllable
        private MHEControl controllerProperties;
        private IController controller;

        //Events
        public event EventHandler<PickPutStationArrivalArgs> OnArrivedAtRightPosition;
        protected virtual void ArrivedAtRightPosition(PickPutStationArrivalArgs e)
        {
            OnArrivedAtRightPosition?.Invoke(this, e);
        }

        public event EventHandler<PickPutStationArrivalArgs> OnArrivedAtLeftPosition;
        protected virtual void ArrivedAtLeftPosition(PickPutStationArrivalArgs e)
        {
            OnArrivedAtLeftPosition?.Invoke(this, e);
        }

        public PickPutStation(PickPutStationInfo info) : base(info)
        {
            pickPutStationInfo = info;

            rightConveyorIn = new StraightConveyor(NewStraightInfo());
            Add(rightConveyorIn);
            rightConveyorIn.LocalPosition = new Vector3(0, 0, -0.4f);
            leftConveyorIn = new StraightConveyor(NewStraightInfo());
            Add(leftConveyorIn);
            leftConveyorIn.LocalPosition = new Vector3(0, 0, 0.4f);

            rightConveyorIn.EndFixPoint.Dispose();
            leftConveyorIn.EndFixPoint.Dispose();

            rightConveyorOut = new StraightConveyor(NewStraightInfo());
            Add(rightConveyorOut);
            rightConveyorOut.LocalPosition = new Vector3(0, 0.85f, -0.4f);
            rightConveyorOut.LocalYaw = (float)Math.PI;
            leftConveyorOut = new StraightConveyor(NewStraightInfo());
            Add(leftConveyorOut);
            leftConveyorOut.LocalPosition = new Vector3(0, 0.85f, 0.4f);
            leftConveyorOut.LocalYaw = (float)Math.PI;

            rightConveyorOut.StartFixPoint.Dispose();
            leftConveyorOut.StartFixPoint.Dispose();

            rightIn = rightConveyorIn.TransportSection.Route.InsertActionPoint(1.2f);
            rightStation = rightConveyorOut.TransportSection.Route.InsertActionPoint(0.35f);
            rightClear = rightConveyorOut.TransportSection.Route.InsertActionPoint(0.7f);
            rightClear.Edge = ActionPoint.Edges.Trailing;

            leftIn = leftConveyorIn.TransportSection.Route.InsertActionPoint(1.2f);
            leftStation = leftConveyorOut.TransportSection.Route.InsertActionPoint(0.35f);
            leftClear = leftConveyorOut.TransportSection.Route.InsertActionPoint(0.7f);
            leftClear.Edge = ActionPoint.Edges.Trailing;

            rightLiftControl = new RapidLiftControl(rightIn, rightStation, rightClear, ArrivedAtRightPosition, UpdateRightConveyorStatus);
            leftLiftControl = new RapidLiftControl(leftIn, leftStation, leftClear, ArrivedAtLeftPosition, UpdateLeftConveyorStatus);

            enteringRight = rightConveyorIn.TransportSection.Route.InsertActionPoint(0);
            enteringRight.Edge = ActionPoint.Edges.Trailing;
            enteringRight.OnEnter += EnteringRightOnEnter;

            leavingRight = rightConveyorOut.TransportSection.Route.InsertActionPoint(rightConveyorOut.Length - 0.05f);
            leavingRight.Edge = ActionPoint.Edges.Leading;
            leavingRight.OnEnter += LeavingRightOnEnter;

            enteringLeft = leftConveyorIn.TransportSection.Route.InsertActionPoint(0);
            enteringLeft.Edge = ActionPoint.Edges.Trailing;
            enteringLeft.OnEnter += EnteringLeftOnEnter;

            leavingLeft = leftConveyorOut.TransportSection.Route.InsertActionPoint(leftConveyorOut.Length - 0.05f);
            leavingLeft.Edge = ActionPoint.Edges.Leading;
            leavingLeft.OnEnter += LeavingLeftOnEnter;

            //Graphics
            leftSide = new Cube(Color.Gray, 1, 1.2f, 0.05f);
            Add(leftSide);
            leftSide.LocalPosition = new Vector3(-leftConveyorIn.Length / 2 + leftSide.Length / 2, leftSide.Height / 2, leftConveyorIn.LocalPosition.Z + leftConveyorIn.Width / 2 + leftSide.Width / 2);

            rightSide = new Cube(Color.Gray, 1, 1.2f, 0.05f);
            Add(rightSide);
            rightSide.LocalPosition = new Vector3(-leftConveyorIn.Length / 2 + leftSide.Length / 2, leftSide.Height / 2, rightConveyorIn.LocalPosition.Z - rightConveyorIn.Width / 2 - leftSide.Width / 2);

            front = new Cube(Color.Gray, 0.05f, 1.0f, leftSide.LocalPosition.Z - rightSide.LocalPosition.Z + leftSide.Width);
            Add(front);
            front.LocalPosition = new Vector3(-leftConveyorIn.Length / 2 - front.Length / 2, front.Height / 2, 0);

            bottom = new Cube(Color.Gray, rightConveyorIn.Length + 1, 0.05f, front.Width);
            Add(bottom);
            bottom.LocalPosition = new Vector3(-0.5f, -0.025f, 0);

            rightConveyorOut.OnNextRouteStatusAvailableChanged += RightConveyorOutOnNextRouteStatusAvailableChanged;
            leftConveyorOut.OnNextRouteStatusAvailableChanged += LeftConveyorOutOnNextRouteStatusAvailableChanged;
            Core.Environment.Scene.OnLoaded += Scene_OnLoaded;
        }

        private void EnteringLeftOnEnter(ActionPoint sender, Load load)
        {
            UpdateLeftConveyorStatus();
        }

        private void EnteringRightOnEnter(ActionPoint sender, Load load)
        {
            UpdateRightConveyorStatus();
        }

        private void UpdateRightConveyorStatus()
        {
            if (rightConveyorIn.TransportSection.Route.Loads.Count >= 2)
            {
                rightConveyorIn.RouteAvailable = RouteStatuses.Blocked;
            }
            else
            {
                rightConveyorIn.RouteAvailable = RouteStatuses.Available;
            }
        }

        private void UpdateLeftConveyorStatus()
        {
            if (leftConveyorIn.TransportSection.Route.Loads.Count >= 2)
            {
                leftConveyorIn.RouteAvailable = RouteStatuses.Blocked;
            }
            else
            {
                leftConveyorIn.RouteAvailable = RouteStatuses.Available;
            }
        }

        private void LeavingLeftOnEnter(ActionPoint sender, Load load)
        {
            if (leftConveyorOut.NextRouteStatus.Available != RouteStatuses.Available)
                load.Stop();
        }

        private void LeftConveyorOutOnNextRouteStatusAvailableChanged(object sender, Experior.Dematic.Base.Devices.RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available && leavingLeft.Active)
            {
                leavingLeft.Release();
            }
        }

        private void LeavingRightOnEnter(ActionPoint sender, Load load)
        {
            if (rightConveyorOut.NextRouteStatus.Available != RouteStatuses.Available)
                load.Stop();
        }

        private void RightConveyorOutOnNextRouteStatusAvailableChanged(object sender, Experior.Dematic.Base.Devices.RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available && leavingRight.Active)
            {
                leavingRight.Release();
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
            enteringRight.OnEnter -= EnteringRightOnEnter;
            enteringLeft.OnEnter -= EnteringLeftOnEnter;
            rightConveyorOut.OnNextRouteStatusAvailableChanged -= RightConveyorOutOnNextRouteStatusAvailableChanged;
            leftConveyorOut.OnNextRouteStatusAvailableChanged -= LeftConveyorOutOnNextRouteStatusAvailableChanged;
            Core.Environment.Scene.OnLoaded -= Scene_OnLoaded;
            leavingRight.OnEnter -= LeavingRightOnEnter;
            leavingLeft.OnEnter -= LeavingLeftOnEnter;
            rightLiftControl.Dispose();
            leftLiftControl.Dispose();
            base.Dispose();
        }

        public override void Reset()
        {
            base.Reset();
            rightLiftControl.Reset();
            leftLiftControl.Reset();
            rightConveyorIn.RouteAvailable = RouteStatuses.Available;
            leftConveyorIn.RouteAvailable = RouteStatuses.Available;
        }

        [Browsable(false)]
        public string RightBarcode
        {
            get { return rightStation.ActiveLoad is Case_Load ? ((Case_Load)rightStation.ActiveLoad).SSCCBarcode : null; }
        }

        [Browsable(false)]
        public string LeftBarcode
        {
            get { return leftStation.ActiveLoad is Case_Load ? ((Case_Load)leftStation.ActiveLoad).SSCCBarcode : null; }
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
                else if (controller != null)
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
                Core.Environment.Properties.Refresh();
            }
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
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
