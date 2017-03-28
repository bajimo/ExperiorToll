using Experior.Catalog.Dematic.Case.Devices;
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
using System.Diagnostics;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class MiniloadDropStation : StraightConveyor
    {
        MiniloadDropStationInfo miniloadDropStationInfo;
        StraightConveyor releaseSection;
        StraightConveyor reverseSection;
        ActionPoint apEnter = new ActionPoint();
        ActionPoint apRelStart = new ActionPoint();
        ActionPoint apRelEnd = new ActionPoint();
        ActionPoint apHold = new ActionPoint();
        ActionPoint apRevStart = new ActionPoint();
        ActionPoint apRevEnd = new ActionPoint();

        public MiniloadDropStation(MiniloadDropStationInfo info):base(info)
        {

            miniloadDropStationInfo = info;

            StraightConveyorInfo receiveSectionInfo = new StraightConveyorInfo()
            {
                Length    = Width / 2,
                thickness = info.thickness,
                Width     = info.width,
                Speed     = info.Speed
            };

            releaseSection = new StraightConveyor(receiveSectionInfo);
            releaseSection.startLine.Visible     = false;
            releaseSection.StartFixPoint.Enabled = false;
            releaseSection.StartFixPoint.Visible = false;
            releaseSection.Color = this.Color;
            releaseSection.TransportSection.Visible = false;

            StraightConveyorInfo reverseSectionInfo = new StraightConveyorInfo()
            {
                Length = Width / 2,
                thickness = info.thickness,
                Width = info.width,
                Speed = info.Speed
            };

            reverseSection = new StraightConveyor(reverseSectionInfo);
            reverseSection.endLine.Visible = false;
            reverseSection.startLine.Visible = false;
            reverseSection.EndFixPoint.Enabled = false;
            reverseSection.EndFixPoint.Visible = false;
            reverseSection.StartFixPoint.Enabled = false;
            reverseSection.StartFixPoint.Visible = false;
            reverseSection.Color = this.Color;
            reverseSection.TransportSection.Visible = false;
            reverseSection.arrow.Visible = false;

            Add(releaseSection);
            Add(reverseSection);

            TransportSection.Route.InsertActionPoint(apEnter);
            TransportSection.Route.InsertActionPoint(apHold, 0);

            releaseSection.TransportSection.Route.InsertActionPoint(apRelStart, 0);
            releaseSection.TransportSection.Route.InsertActionPoint(apRelEnd, releaseSection.Length);
            reverseSection.TransportSection.Route.InsertActionPoint(apRevStart, 0);
            reverseSection.TransportSection.Route.InsertActionPoint(apRevEnd, reverseSection.Length);

            apRelEnd.Edge = ActionPoint.Edges.Trailing;

            apRelStart.OnEnter += apRelStart_OnEnter;
            apRelEnd.OnEnter += apRelEnd_OnEnter;
            apEnter.OnEnter += apEnter_OnEnter;
            apHold.OnEnter += apHold_OnEnter;
            apRevEnd.OnEnter += apRevEnd_OnEnter;

            releaseSection.EndFixPoint.OnSnapped += ReleaseSectionStartFixPoint_OnSnapped;
            releaseSection.EndFixPoint.OnUnSnapped += ReleaseSectionStartFixPoint_OnUnSnapped;

            Length = info.length;
            releaseSection.Width = (float)info.receiveWidth / 1000;
            Speed = info.conveyorSpeed;
            reverseSection.Speed = info.conveyorSpeed;

            UpdateConveyor();
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            releaseSection.RouteAvailable = RouteStatuses.Request;
            RouteAvailable = RouteStatuses.Available;
        }

        public override void Reset()
        {
            base.Reset();
            releaseSection.RouteAvailable = RouteStatuses.Request;
            RouteAvailable = RouteStatuses.Available;
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {

        }

        public void ReleaseSectionStartFixPoint_OnSnapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            NextConveyor = fixpoint.Parent as IRouteStatus;
            NextRouteStatus = NextConveyor.GetRouteStatus(fixpoint);
            NextRouteStatus.OnRouteStatusChanged += NextRouteStatus_OnRouteStatusChanged;
            releaseSection.Speed = NextConveyor.Speed;
        }

        public void ReleaseSectionStartFixPoint_OnUnSnapped(FixPoint fixpoint)
        {
            NextRouteStatus.OnRouteStatusChanged -= NextRouteStatus_OnRouteStatusChanged;
            NextRouteStatus = null;
            NextConveyor = null;
        }

        void NextRouteStatus_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            //TODO Release loads when the status of the release conveyor becomes available
            if (e._available == RouteStatuses.Available && apRelStart.Active)
            {
                apRelStart.ActiveLoad.Release();
            }
        }

        void apRelStart_OnEnter(ActionPoint sender, Load load)
        {
            if (NextRouteStatus.Available != RouteStatuses.Available)
            {
                load.Stop();
            }
        }

        void apRelEnd_OnEnter(ActionPoint sender, Load load) //Trailing edge
        {
            if (apRevStart.Active)
            {
                apRevStart.ActiveLoad.Release();
            }
            else if (TransportSection.Route.Loads.Count == 0 && reverseSection.TransportSection.Route.Loads.Count == 0)
            {
                RouteAvailable = RouteStatuses.Available;
            }
        }

        //On main conveyor
        void apEnter_OnEnter(ActionPoint sender, Load load)
        {
            TransferComplete();
        }

        void apHold_OnEnter(ActionPoint sender, Load load)
        {
            load.Stop();
            TransferComplete();
        }

        private bool TransferComplete()
        {
            int LoadsOnDropStation = TransportSection.Route.Loads.Count;
            int LoadsOnMiniload = PreviousConveyor.TransportSection.Route.Loads.Count;

            if ((LoadsOnDropStation + LoadsOnMiniload) == 2)
            {
                if (apEnter.Active && apHold.Active)
                {
                    RouteAvailable = RouteStatuses.Blocked;
                    Load load2 = apEnter.ActiveLoad;
                    Load load1 = apHold.ActiveLoad;
                    load2.Switch(apRelStart, true);
                    //load2.Release();
                    load1.Switch(apRevStart, true);
                    return true;
                }
            }
            else if ((LoadsOnDropStation + LoadsOnMiniload) == 1)
            {
                if (apEnter.Active)
                {
                    RouteAvailable = RouteStatuses.Blocked;
                    Load load2 = apEnter.ActiveLoad;
                    load2.Switch(apRelStart, true);
                    //load2.Release();
                    return true;
                }
            }
            return false;
        }

        void apRevEnd_OnEnter(ActionPoint sender, Load load)
        {
            load.Switch(apRelStart, true);
        }

        public override void UpdateConveyor()
        {
            float hold = (Length - releaseSection.Width) / 2;
            float enter = Length - releaseSection.Width / 2;
            
            releaseSection.Length = Width / 2;
            reverseSection.Length = enter - hold;

            float xOffset = Length / 2 - releaseSection.Width / 2;

            reverseSection.LocalPosition = new Vector3((hold + (reverseSection.Length / 2) - Length /2), reverseSection.LocalPosition.Y, 0);
            reverseSection.LocalYaw = (float)Math.PI;

            if (ReleaseSide == Side.Left)
            {
                releaseSection.LocalPosition = new Vector3(xOffset, releaseSection.LocalPosition.Y, -releaseSection.Length / 2);
                releaseSection.LocalYaw = -(float)Math.PI / 2;
            }
            else
            {
                releaseSection.LocalPosition = new Vector3(xOffset, releaseSection.LocalPosition.Y, releaseSection.Length / 2);
                releaseSection.LocalYaw = (float)Math.PI / 2;
            }

            apEnter.Color = Color.Red;
            apRelStart.Color = Color.Red;
            apHold.Color = Color.Blue;
            apRevStart.Color = Color.DarkRed;
            apRevEnd.Color = Color.DarkBlue;

            apEnter.Visible = false;
            apRelStart.Visible = false;
            apHold.Visible = false;
            apRevStart.Visible = false;
            apRevEnd.Visible = false;

            apEnter.Distance = releaseSection.Width / 2;
            apHold.Distance =  Length - hold;
            apRelStart.Distance = -(releaseSection.Length / 2);
            apRelEnd.Distance = releaseSection.Length;
            apRevEnd.Distance = reverseSection.Length;
        }
        
        public override string Category
        {
            get { return "Miniload"; }
        }

        public override Image Image
        {
            get 
            {
                return Common.Icons.Get("BeltSorterMergePopUp");   
            }
        }

        #region Action Points

        [Browsable(false)]
        public Load Position1Load
        {
            get 
            {
                if (apEnter.Active)
                {
                    return apEnter.ActiveLoad;
                }
                else
                {
                    return null;
                }
            }
        }

        [Browsable(false)]
        public Load Position2Load
        {
            get
            {
                if (apHold.Active)
                {
                    return apHold.ActiveLoad;
                }
                else
                {
                    return null;
                }
            }
        }

        #endregion

        #region Size and Speed

        [Category("Size and Speed")]
        [DisplayName("Straight Length")]
        [PropertyOrder(1)]
        [Description("Length of the straight section conveyor (meter)")]
        public override float Length
        {
            get { return base.Length; }
            set
            {
                if (value < releaseSection.Width)
                {
                    Core.Environment.Log.Write(string.Format("Conveyor Length must not be less than the receive section conveyor width ({0}).", releaseSection.Width.ToString()), System.Drawing.Color.Red);

                }
                else
                {
                    base.Length = value;
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Receive Width")]
        [PropertyOrder(4)]
        [Description("Width of the receive section conveyor based on standard Dematic case conveyor widths")]
        public CaseConveyorWidth FeedWidth
        {
            get { return miniloadDropStationInfo.receiveWidth; }
            set
            {
                releaseSection.Width = (float)value / 1000;
                miniloadDropStationInfo.receiveWidth = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Release Side")]
        [Description("Left or right release")]
        [PropertyOrder(8)]
        [TypeConverter()]
        public Side ReleaseSide
        {
            get { return miniloadDropStationInfo.receiveSide; }
            set
            {
                miniloadDropStationInfo.receiveSide = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Speed")]
        [Description("Speed of loading conveyor")]
        [PropertyOrder(9)]
        [TypeConverter(typeof(SpeedConverter))]
        public float ConveyorSpeed
        {
            get { return miniloadDropStationInfo.conveyorSpeed; }
            set
            {
                miniloadDropStationInfo.conveyorSpeed = value;
                Speed = value;
                reverseSection.Speed = value;
            }
        }

        public void DynamicPropertyPopUporAngled(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = true;
            //attributes.IsBrowsable = beltSorterMergeInfo.type  == MergeType.Angled;
        }

        [Browsable(false)]
        public override float EndOffset
        {
            get { return base.EndOffset; }
            set { base.EndOffset = value; }
        }

        [Browsable(false)]
        public override float StartOffset
        {
            get { return base.StartOffset; }
            set { base.StartOffset = value; }
        }

        [Browsable(false)]
        public override float Speed
        {
            get
            {
                return base.Speed;
            }
            set
            {
                base.Speed = value;
            }
        }
        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(MiniloadDropStationInfo))]
    public class MiniloadDropStationInfo : StraightBeltConveyorInfo
    {
        public Side receiveSide = Side.Left;
        public CaseConveyorWidth receiveWidth = CaseConveyorWidth._500mm;
        public float conveyorSpeed = 0.7f;
        public ControlTypesSubSet ControlType;
    }
}