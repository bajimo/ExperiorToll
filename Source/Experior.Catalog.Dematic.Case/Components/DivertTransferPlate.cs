using Experior.Core.Parts;
using System;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Drawing;
using Experior.Dematic.Base;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;
using Microsoft.DirectX;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class DivertTransferPlate : StraightConveyor
    {
        public DivertTransferPlateInfo divertTransferPlateInfo;
        StraightConveyor divertSection;
        ActionPoint apStraight = new ActionPoint();
        ActionPoint apDivert = new ActionPoint();
        FixPoint DivertEndFixPoint;


        
        public DivertTransferPlate(DivertTransferPlateInfo info) : base(info)
        {
            divertTransferPlateInfo = info;
            arrow.Visible = false; //Don't show the straight arrow
            EndFixPoint.Visible = false; //Loads can only transfer, they cannot go straight
            endLine.Visible = false;

            StraightConveyorInfo divertSectionInfo = new StraightConveyorInfo()
            {
                Length = Width / 2,
                thickness = info.thickness,
                Width = info.width,
                Speed = info.Speed
            };

            divertSection = new StraightConveyor(divertSectionInfo);
            divertSection.startLine.Visible = false;
            divertSection.StartFixPoint.Visible = false;
            divertSection.StartFixPoint.Enabled = false;
            divertSection.Color = Core.Environment.Scene.DefaultColor;
            divertSection.EndFixPoint.Visible = false;
            divertSection.EndFixPoint.Enabled = false;

            DivertEndFixPoint = new FixPoint(FixPoint.Types.End, this);
            Add(DivertEndFixPoint);
            DivertEndFixPoint.Route = divertSection.TransportSection.Route;
            DivertEndFixPoint.OnSnapped += DivertSection_EndFixPoint_OnSnapped;
            DivertEndFixPoint.OnUnSnapped += DivertSection_EndFixPoint_OnUnSnapped;

            Add(divertSection);

            TransportSection.Route.InsertActionPoint(apStraight);
            divertSection.TransportSection.Route.InsertActionPoint(apDivert, 0);

            apStraight.OnEnter += apStraight_OnEnter;

            UpdateConveyor();
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            if (NextRouteStatus != null)
            {
                RouteAvailable = NextRouteStatus.Available;
                NextRouteStatus_OnAvailableChanged(this, new RouteStatusChangedEventArgs(RouteAvailable));
            }
        }

        void apStraight_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            //Switch to divert point
            load.Switch(apDivert, true);
        }

        public override void UpdateConveyor()
        {
            apStraight.Distance = Length / 2;
            divertSection.Width = Length - Math.Abs(DivertConveyorOffset);
            divertSection.Length = DivertConveyorLength; //Width / 2;
            divertSection.arrow.LocalPosition = new Vector3(divertSection.Length / 2, 0, 0);

            divertSection.LocalPosition = new Vector3(DivertConveyorOffset / 2, divertSection.LocalPosition.Y, (int)DivertSide * divertSection.Length / 2);
            divertSection.LocalYaw = (int)DivertSide * (float)(Math.PI / 2);

            DivertEndFixPoint.LocalPosition = new Vector3(DivertConveyorOffset / 2, 0, -(int)DivertSide * -divertSection.Length);
            DivertEndFixPoint.LocalYaw = -(int)DivertSide * -(float)Math.PI / 2;

        }

        public override void StartFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e) 
        {
            //Get the load waiting status of the previous conveyor and set as this conveyors load waiting status
            //This load waiting will mirror the load waiting status of the previous conveyor (merge/Divert)
            PreviousConveyor = stranger.Parent as IRouteStatus;
            PreviousLoadWaiting = PreviousConveyor.GetLoadWaitingStatus(stranger);
            SetLoadWaiting(PreviousLoadWaiting.LoadWaiting, false, null);
            PreviousLoadWaiting.OnLoadWaitingChanged += PreviousLoadWaiting_OnLoadWaitingChanged;

            Reset();
        }

        void PreviousLoadWaiting_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            SetLoadWaiting(e._loadWaiting, e._loadDeleted, e._waitingLoad);
        }

        public override void StartFixPoint_OnUnSnapped(FixPoint stranger) 
        {
            PreviousLoadWaiting.OnLoadWaitingChanged -= PreviousLoadWaiting_OnLoadWaitingChanged;
            PreviousLoadWaiting = null;
            Reset();
        }

        public void DivertSection_EndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            if (stranger.Type != FixPoint.Types.End)
            {
                //Get the available status of the next conveyor and set as this conveyors available status
                //This route available status will mirror the route available status of the next conveyor (merge/divert)

                divertSection.TransportSection.Route.NextRoute = stranger.Route;

                NextConveyor = stranger.Parent as IRouteStatus;
                NextRouteStatus = NextConveyor.GetRouteStatus(stranger);// .GetAvailableStatus(stranger);
                RouteAvailable = NextRouteStatus.Available;
                NextRouteStatus.OnRouteStatusChanged += NextRouteStatus_OnAvailableChanged;
            }
            else
            {
                Core.Environment.Log.Write("WARNING can't snap an end to and end; turn your conveyor arround", Color.Orange);
            }

            Reset();
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e) 
        {
            RouteAvailable = e._available;

            if (e._available == RouteStatuses.Available)
                divertSection.arrow.Color = Color.Green;
            else if (e._available == RouteStatuses.Request)
                divertSection.arrow.Color = Color.Yellow;
            else
                divertSection.arrow.Color = Color.Red;
        }

        public void DivertSection_EndFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            divertSection.TransportSection.Route.NextRoute = null;
            ThisRouteStatus = new RouteStatus();
            NextRouteStatus.OnRouteStatusChanged -= NextRouteStatus_OnAvailableChanged;
            NextConveyor = null;
            NextRouteStatus = null;

            Reset();
        }

        public override void Reset()
        {
            if (NextRouteStatus != null)
            {
                RouteAvailable = NextRouteStatus.Available;
                NextRouteStatus_OnAvailableChanged(this, new RouteStatusChangedEventArgs(RouteAvailable));
            }

            base.Reset();
        }

        #region User Interface
        [Category("Size and Speed")]
        [DisplayName("Width")]
        [Browsable(true)]
        public override float Width
        {
            get
            {
                return base.Width;
            }
            set
            {
                base.Width = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Length")]
        [Description("Length of the Divert conveyor (meter)")]
        [TypeConverter()]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [PropertyOrder(3)]
        public float DivertConveyorLength
        {
            get
            {
                return divertTransferPlateInfo.divertConveyorLength;
            }
            set
            {
                if (value > 0)
                {
                    divertTransferPlateInfo.divertConveyorLength = value;
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Divert Speed")]
        [PropertyOrder(6)]
        [Description("Speed of the Divert section conveyor (Speed of straight section is taken from the next conveyor)")]
        [TypeConverter(typeof(SpeedConverter))]
        public float DivertSpeed
        {
            get { return divertTransferPlateInfo.divertSpeed; }
            set
            {
                divertTransferPlateInfo.divertSpeed = value;
                divertSection.Speed = value;
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
            get { return divertTransferPlateInfo.divertConveyorOffset; }
            set
            {
                divertTransferPlateInfo.divertConveyorOffset = value;
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
            get { return divertTransferPlateInfo.divertSide; }
            set
            {
                divertTransferPlateInfo.divertSide = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        #endregion


        [Browsable(false)]
        public override CaseConveyorWidth ConveyorWidth
        {
            get { return base.ConveyorWidth; }
            set { base.ConveyorWidth = value; }
        }
    }

    [Serializable]
    [XmlInclude(typeof(DivertTransferPlateInfo))]
    public class DivertTransferPlateInfo : StraightConveyorInfo
    {
        public Side divertSide = Side.Left;
        public float divertConveyorOffset;
        public float divertConveyorLength;

        public float divertSpeed = 0.7f;
    }
}
