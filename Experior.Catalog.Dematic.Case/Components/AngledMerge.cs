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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class AngledMerge : StraightConveyor
    {
        AngledMergeInfo angledMergeInfo;
        StraightConveyor mergeSection;
        ActionPoint apStraight = new ActionPoint();
        ActionPoint apMerge = new ActionPoint();
        ActionPoint apEnd = new ActionPoint(); //This is at the end of the conveyor to say that the transfer is now clear, instead of using timers
        LoadWaitingStatus PreviousLoadWaitingStraight;
        LoadWaitingStatus PreviousLoadWaitingMerge;
        bool loadActive = false; //This is to say that there is a load in the merge

        //Timer ReleaseTimer = new Timer(100);

        float mergeCentreOffset;

        List<Arrived> ArrivalOrder = new List<Arrived>();

        public AngledMerge(AngledMergeInfo info):base(info)
        {
            angledMergeInfo = info;

            StraightConveyorInfo divertSectionInfo = new StraightConveyorInfo()
            {
                Length    = info.mergeConveyorLength,
                thickness = info.thickness,
                Width     = info.width,
                Speed     = info.Speed,
                color     = info.color
            };

            mergeSection = new StraightConveyor(divertSectionInfo);
            mergeSection.endLine.Visible     = false;
            mergeSection.EndFixPoint.Enabled = false;
            mergeSection.EndFixPoint.Visible = false;

            Add(mergeSection);

            TransportSection.Route.InsertActionPoint(apStraight);
            TransportSection.Route.InsertActionPoint(apEnd, Length);
            apEnd.Edge = ActionPoint.Edges.Trailing;
            mergeSection.TransportSection.Route.InsertActionPoint(apMerge, info.mergeConveyorLength);

            apMerge.OnEnter += apMerge_OnEnter;
            apStraight.OnEnter += apStraight_OnEnter;
            apEnd.OnEnter += apEnd_OnEnter;

            mergeSection.StartFixPoint.OnSnapped += MergeSectionStartFixPoint_OnSnapped;
            mergeSection.StartFixPoint.OnUnSnapped += MergeSectionStartFixPoint_OnUnSnapped;

            MergeAngle          = info.mergeAngle;
            Length              = info.length;
            MergeConveyorOffset = info.mergeConveyorOffset;
            MergeWidth          = info.mergeWidth;
            //ReleaseTimer.OnElapsed += ReleaseTimer_OnElapsed;

            TransportSection.Route.OnLoadRemoved += Route_OnLoadRemoved;
            mergeSection.TransportSection.Route.OnLoadRemoved += Route_OnLoadRemoved;

            UpdateConveyor();
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            RouteAvailable = RouteStatuses.Request;
        }

        public override void Reset()
        {
            base.Reset();
            mergeSection.RouteAvailable = RouteStatuses.Request;
            RouteAvailable = RouteStatuses.Request;
            ArrivalOrder.Clear();
            //ReleaseTimer.Stop();
            //ReleaseTimer.Reset();
            loadActive = false;
        }

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

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Available)
            {
                ReleaseNextLoad();
            }

            if (e._available == RouteStatuses.Blocked)
            {
                if (TransportSection.Route.Motor != null)
                {
                    TransportSection.Route.Motor.Stop();
                }
                mergeSection.TransportSection.Route.Motor.Stop();
            }
            else
            {
                if (TransportSection.Route.Motor != null)
                {
                    TransportSection.Route.Motor.Start();
                }
                mergeSection.TransportSection.Route.Motor.Start();
            }
        }

        void Route_OnLoadRemoved(Route sender, Load load)
        {
            if (load.StartDisposing)
            {
                loadActive = false;
                ReleaseNextLoad();
            }
        }

        public void MergeSectionStartFixPoint_OnSnapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            IRouteStatus connectedConv = fixpoint.Parent as IRouteStatus;
            PreviousLoadWaitingMerge = connectedConv.GetLoadWaitingStatus(fixpoint);
            PreviousLoadWaitingMerge.OnLoadWaitingChanged += PreviousLoadWaitingMerge_OnLoadWaitingChanged;
        }

        public void MergeSectionStartFixPoint_OnUnSnapped(FixPoint fixpoint)
        {
            if (PreviousLoadWaiting != null)
            {
                PreviousLoadWaiting.OnLoadWaitingChanged -= PreviousLoadWaitingMerge_OnLoadWaitingChanged;
                PreviousLoadWaiting = null;
            }
        }

        public override void StartFixPoint_OnSnapped(FixPoint stranger, FixPoint.SnapEventArgs e)
        {
            IRouteStatus connectedConv = stranger.Parent as IRouteStatus;
            PreviousLoadWaitingStraight = connectedConv.GetLoadWaitingStatus(stranger);
            PreviousLoadWaitingStraight.OnLoadWaitingChanged += PreviousLoadWaitingStraight_OnLoadWaitingChanged;
        }

        public override void StartFixPoint_OnUnSnapped(FixPoint stranger)
        {
            PreviousLoadWaitingStraight.OnLoadWaitingChanged -= PreviousLoadWaitingStraight_OnLoadWaitingChanged;
            PreviousLoadWaitingStraight = null;
        }

        #region Routing Logic
        void PreviousLoadWaitingStraight_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            if (e._loadWaiting)
            {
                if (!ArrivalOrder.Contains(Arrived.Straight))
                {
                    ArrivalOrder.Add(Arrived.Straight);
                }
                ReleaseNextLoad();
            }
            else
            {
                RouteAvailable = RouteStatuses.Request;
            }
        }

        void PreviousLoadWaitingMerge_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            if (e._loadWaiting)
            {
                if (!ArrivalOrder.Contains(Arrived.Merge))
                {
                    ArrivalOrder.Add(Arrived.Merge);
                }
                ReleaseNextLoad();
            }
            else
            {
                mergeSection.RouteAvailable = RouteStatuses.Request;
            }
        }

        void ReleaseTimer_OnElapsed(Timer sender)
        {
            //ReleaseTimer.Reset();
            ReleaseNextLoad();
        }


        private void apEnd_OnEnter(ActionPoint sender, Load load)
        {
            loadActive = false;
            ReleaseNextLoad();
        }

        void ReleaseNextLoad()
        {
            //if (!ReleaseTimer.Running && ArrivalOrder.Count > 0 && NextRouteStatus.Available == RouteStatuses.Available)
            if (!loadActive && ArrivalOrder.Count > 0 && NextRouteStatus != null && NextRouteStatus.Available == RouteStatuses.Available)
                {
                    //The next load can be released
                if (ArrivalOrder[0] == Arrived.Straight)
                {
                    //ReleaseTimer.Timeout = StraightDelayTime;
                    RouteAvailable = RouteStatuses.Available;
                    loadActive = true;
                }
                else if (ArrivalOrder[0] == Arrived.Merge)
                {
                    //ReleaseTimer.Timeout = MergeDelayTime;
                    mergeSection.RouteAvailable = RouteStatuses.Available;
                    loadActive = true;
                }
                ArrivalOrder.RemoveAt(0);
                //ReleaseTimer.Start();
            }
        }
        #endregion

        void apMerge_OnEnter(ActionPoint sender, Load load)
        {
            load.Switch(apStraight, KeepOrientation);
        }

        void apStraight_OnEnter(ActionPoint sender, Load load)
        {

        }

        public override void UpdateConveyor()
        {
            apStraight.Distance = MergeConveyorOffset;
            apMerge.Distance = MergeLength;
        
            float zOffsetCentre = (float)(Math.Sin(MergeAngle) * (mergeSection.Length / 2));

            if (MergeSide == Side.Left)
            {
                mergeSection.LocalPosition = new Vector3(mergeCentreOffset - angledMergeInfo.mergeConveyorOffset, mergeSection.LocalPosition.Y, -zOffsetCentre);
                mergeSection.LocalYaw = MergeAngle;
            }
            else
            {
                mergeSection.LocalPosition = new Vector3(mergeCentreOffset - angledMergeInfo.mergeConveyorOffset, mergeSection.LocalPosition.Y, zOffsetCentre);
                mergeSection.LocalYaw = -MergeAngle;
            }

            mergeSection.arrow.LocalPosition = new Vector3(mergeSection.Length / 2 - 0.2f, 0, 0);
        }

        #region User Interface
        [Category("Orientation")]
        [DisplayName("Keep Orientation")]
        [Description("Keeps the orienation when transfering onto the straight section.")]
        public bool KeepOrientation
        {
            get { return angledMergeInfo.keepOrientation; }
            set { angledMergeInfo.keepOrientation = value; }
        }


        #region Merge Timers
        [Category("Timers")]
        [DisplayName("Straight Delay")]
        [Description("Once a load has released straight, what is the delay before releasing the next load")]
        [PropertyOrder(1)]
        public float StraightDelayTime
        {
            get { return angledMergeInfo.StraightDelayTime; }
            set
            {
                angledMergeInfo.StraightDelayTime = value;
            }
        }

        [Category("Timers")]
        [DisplayName("Merge Delay")]
        [Description("Once a load has released from the merge, what is the delay before releasing the next load")]
        [PropertyOrder(1)]
        public float MergeDelayTime
        {
            get { return angledMergeInfo.MergeDelayTime; }
            set
            {
                angledMergeInfo.MergeDelayTime = value;
            }
        }
        #endregion

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
                    float adjustedLength = (float)(mergeSection.Width / (Math.Sin(MergeAngle)));
                    float AdjustedOffset = (float)(((Width / 2) / Math.Tan(MergeAngle)) + adjustedLength / 2);
                    Log.Write("Adjusted Length to fit: " + adjustedLength);
                    Log.Write("Adjusted Offset to fit: " + AdjustedOffset);
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Straight Length")]
        [PropertyOrder(1)]
        [Description("Length of the straight section conveyor (meter)")]
        public override float Length
        {
            get { return base.Length; }
            set
            {
                if (value < MergeConveyorOffset)
                {
                    Core.Environment.Log.Write(string.Format("Conveyor Length must not be less than 'Divert Conveyor Offset' ({0}).", MergeConveyorOffset), System.Drawing.Color.Red);
                }
                else
                {
                    base.Length = value;
                    mergeCentreOffset = (float)(Length / 2) + (float)Math.Cos(MergeAngle) * (MergeLength / 2);
                    Core.Environment.Invoke(() => UpdateConveyor());
                    apEnd.Distance = value;
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Merge Length")]
        [PropertyOrder(2)]
        [Description("Length of the merge section conveyor (meter)")]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [TypeConverter()]
        public float MergeLength
        {
            get
            {
                return angledMergeInfo.mergeConveyorLength;
            }
            set
            {
                if (value > 0)
                {
                    mergeCentreOffset = (float)(Length / 2) + (float)Math.Cos(MergeAngle) * (value / 2);
                    angledMergeInfo.mergeConveyorLength = value;
                    mergeSection.Length = value;
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Straight Width")]
        [Description("Width of the straight section conveyor based on standard Dematic case conveyor widths")]
        [PropertyOrder(3)]
        public override CaseConveyorWidth ConveyorWidth
        {
            get { return angledMergeInfo.conveyorWidth; }
            set
            {
                Width = (float)value / 1000;
                angledMergeInfo.conveyorWidth = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Merge Width")]
        [PropertyOrder(4)]
        [Description("Width of the merge section conveyor based on standard Dematic case conveyor widths")]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        public CaseConveyorWidth MergeWidth
        {
            get { return angledMergeInfo.mergeWidth; }
            set
            {
                mergeSection.Width = (float)value / 1000;
                angledMergeInfo.mergeWidth = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Merge Speed")]
        [PropertyOrder(5)]
        [Description("Speed of the Merge section conveyor (Speed of straight section is taken from the next conveyor)")]
        [TypeConverter(typeof(SpeedConverter))]
        public float MergeSpeed
        {
            get { return angledMergeInfo.mergeSpeed; }
            set
            {
                angledMergeInfo.mergeSpeed = value;
                mergeSection.Speed = value;
            }
        }


        [Category("Size and Speed")]
        [DisplayName("Merge Angle")]
        [Description("The merge section angle in degrees")]
        [PropertyOrder(6)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [TypeConverter(typeof(Rad2AngleConverter))]
        public float MergeAngle
        {
            get { return angledMergeInfo.mergeAngle; }
            set
            {
                {
                    angledMergeInfo.mergeAngle = value;
                    mergeCentreOffset = (float)(Length / 2) + (float)Math.Cos(MergeAngle) * (MergeLength / 2);
                    Core.Environment.Invoke(() => UpdateConveyor());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Merge Offset")]
        [PropertyOrder(7)]
        [Description("Distance from start of the straight section conveyor until the merge section conveyor (meter)")]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [TypeConverter()]
        public float MergeConveyorOffset
        {
            get { return angledMergeInfo.mergeConveyorOffset; }
            set
            {
                angledMergeInfo.mergeConveyorOffset = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Merge Side")]
        [Description("Left or right merge")]
        [PropertyOrder(8)]
        [TypeConverter()]
        public Side MergeSide
        {
            get { return angledMergeInfo.mergeSide; }
            set
            {
                angledMergeInfo.mergeSide = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        public override Color Color
        {
            get { return base.Color; }
            set
            {
                base.Color = value;
                mergeSection.Color = value;
            }
        }
        #endregion

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
            get { return base.Speed; }
            set
            {
                base.Speed = value;
            }
        }

        #endregion

        
    }





    [Serializable]
    [XmlInclude(typeof(AngledMergeInfo))]
    public class AngledMergeInfo : StraightBeltConveyorInfo
    {
        public float mergeConveyorOffset = 0.5f;
        public float mergeConveyorLength = 0.7f;
        public Side mergeSide = Side.Left;
        public CaseConveyorWidth mergeWidth = CaseConveyorWidth._500mm;
        public float mergeAngle;
        public float mergeSpeed = 0.7f;
        public ControlTypesSubSet ControlType;
        public float StraightDelayTime;
        public float MergeDelayTime;
        public bool keepOrientation = false;

    }
}