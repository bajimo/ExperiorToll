using Experior.Core.Assemblies;
using Experior.Core.Properties;
using Experior.Core.Routes;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Experior.Catalog.Logistic.Basic;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class TwoToOneMerge : Assembly
    {
        private StraightConveyor  lhsConveyor, rhsConveyor ;
        private TwoToOneMergeInfo twoToOneInfo;
        private ActionPoint rhsEnd, lhsEnd;
        private LoadWaitingStatus lhsPreviousLoadWaitingStatus, rhsPreviousLoadWaitingStatus; //Reference to the waiting status of the previous conveyor
        private Core.Timer ReleaseDelayTimer;
        private List<StraightConveyor> convsWithALoadToRelease = new List<StraightConveyor>();
        private Cube box;

        public TwoToOneMerge(TwoToOneMergeInfo info): base(info)
        {
            info.height = info.height * 2; // assembley is placed at height/2 so that it comes out at height ?!!??
            twoToOneInfo  = info;
            rhsConveyor = new StraightConveyor(NewStraightInfo());
            Add(rhsConveyor);
            rhsEnd = rhsConveyor.TransportSection.Route.InsertActionPoint(3);

            lhsConveyor = new StraightConveyor(NewStraightInfo());
            Add(lhsConveyor);
            lhsEnd = lhsConveyor.TransportSection.Route.InsertActionPoint(3);
            
            lhsConveyor.EndFixPoint.Enabled = false; //Disable one of the fixpoints so that there is only one that can be fixed to
            lhsConveyor.EndFixPoint.Visible = false;

            #region Make it look nice

            BasicInfo boxInfo = new BasicInfo
            {
                length = info.length,
                width  = info.width,
                height = 0.05f,
                color  = Core.Environment.Scene.DefaultColor
            };

            box = new Cube(boxInfo);
            Add(box);
        
            #endregion

            lhsEnd.OnEnter   += lhsEnd_OnEnter;
            TwoToOneWidth     = info.width;
            TwoToOneLength    = info.length;
            InternalConvWidth = info.internalConvWidth;
            UpdateMergeAngles();

            lhsConveyor.RouteAvailable = RouteStatuses.Request; //We are setting this to request because the DHDM controls the release of loads from the previous conveyor onto it.
            rhsConveyor.RouteAvailable = RouteStatuses.Request; //same here

            ReleaseDelayTimer = new Core.Timer(ReleaseDelayTime);  //loads will be only be released when the timer elapses the timer will start when a load transfers 
            ReleaseDelayTimer.OnElapsed += ReleaseDelayTimer_OnElapsed;

            lhsConveyor.StartFixPoint.OnSnapped   += LHSStartFixPoint_OnSnapped;
            lhsConveyor.StartFixPoint.OnUnSnapped += LHSStartFixPoint_OnUnSnapped;

            rhsConveyor.StartFixPoint.OnSnapped   += RHSStartFixPoint_OnSnapped;
            rhsConveyor.StartFixPoint.OnUnSnapped += RHSStartFixPoint_OnUnSnapped;

            rhsConveyor.OnNextRouteStatusAvailableChanged += rhsConveyor_OnNextRouteStatusAvailableChanged;
        }

        private StraightConveyorInfo NewStraightInfo()
        {
            StraightConveyorInfo straightInfo = new StraightConveyorInfo();
            straightInfo.Length = 2;
            straightInfo.Speed = 0.7f;
            straightInfo.thickness = 0.05f;
            straightInfo.color = Core.Environment.Scene.DefaultColor;
            straightInfo.conveyorWidth = CaseConveyorWidth._500mm;
            return straightInfo;
        }

        public override void Reset()
        {
            convsWithALoadToRelease.Clear();
            base.Reset();
        }

        #region Routing Configuration

        void LHSStartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            lhsConveyor.PreviousConveyor = stranger.Parent as IRouteStatus;
            lhsPreviousLoadWaitingStatus = lhsConveyor.PreviousConveyor.GetLoadWaitingStatus(stranger);
            lhsPreviousLoadWaitingStatus.OnLoadWaitingChanged += lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
        }

        void RHSStartFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            rhsConveyor.PreviousConveyor = stranger.Parent as IRouteStatus;
            rhsPreviousLoadWaitingStatus = rhsConveyor.PreviousConveyor.GetLoadWaitingStatus(stranger);
            rhsPreviousLoadWaitingStatus.OnLoadWaitingChanged += rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
        }

        void LHSStartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            lhsConveyor.PreviousConveyor = null;
            lhsPreviousLoadWaitingStatus.OnLoadWaitingChanged -= lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            lhsPreviousLoadWaitingStatus = null;
            Reset();
        }

        void RHSStartFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            rhsConveyor.PreviousConveyor = null;
            rhsPreviousLoadWaitingStatus.OnLoadWaitingChanged -= rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged;
            rhsPreviousLoadWaitingStatus = null;
            Reset();
        }

        void rhsPreviousLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            if (e._loadWaiting)
            {
                convsWithALoadToRelease.Add(rhsConveyor.PreviousConveyor as StraightConveyor);
            }
            if (e._loadWaiting && !ReleaseDelayTimer.Running)
            {
                Release();
            }
            else if (!e._loadWaiting)
            {
                ((StraightConveyor)(rhsConveyor)).RouteAvailable = RouteStatuses.Request;
                rhsTransfering = false;
            }         
        }

        void lhsPreviousLoadWaitingStatus_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            if (e._loadWaiting)
            {
                convsWithALoadToRelease.Add(lhsConveyor.PreviousConveyor as StraightConveyor);
            }
            if (e._loadWaiting && !ReleaseDelayTimer.Running)
            {
                Release();
            }
            else if (!e._loadWaiting)
            {
                ((StraightConveyor)(lhsConveyor)).RouteAvailable = RouteStatuses.Request;
                lhsTransfering = false;
            }
        }

        private bool rhsTransfering, lhsTransfering;

        //Only the RHS has the attached conveyor
        void rhsConveyor_OnNextRouteStatusAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available == RouteStatuses.Blocked)
            {                
                ReleaseDelayTimer.Stop();

                if (lhsConveyor.RouteAvailable == RouteStatuses.Available)
                {
                    lhsTransfering = true;
                }

                if (rhsConveyor.RouteAvailable == RouteStatuses.Available)
                {
                    rhsTransfering = true;
                }

                lhsConveyor.RouteAvailable = RouteStatuses.Blocked;
                rhsConveyor.RouteAvailable = RouteStatuses.Blocked;
                lhsConveyor.TransportSection.Route.Motor.Stop();
                rhsConveyor.TransportSection.Route.Motor.Stop();
            }
            else if (e._available == RouteStatuses.Available)
            {
                ReleaseDelayTimer.Start();
                if (rhsTransfering)
                {
                    rhsConveyor.RouteAvailable = RouteStatuses.Available;
                }
                else
                {
                    rhsConveyor.RouteAvailable = RouteStatuses.Request;
                }

                if (lhsTransfering)
                {
                    lhsConveyor.RouteAvailable = RouteStatuses.Available;
                }
                else
                {
                    lhsConveyor.RouteAvailable = RouteStatuses.Request;
                }

                lhsConveyor.TransportSection.Route.Motor.Start();
                rhsConveyor.TransportSection.Route.Motor.Start();
            }

            if (!ReleaseDelayTimer.Running)
            {
                Release();
            }  
        }

        
       
        #endregion

        #region Routing Logic

        void ReleaseDelayTimer_OnElapsed(Core.Timer sender)
        {
            Release();
        }

        bool Release()
        {               
            if (convsWithALoadToRelease.Count > 0 && rhsConveyor.NextRouteStatus.Available == RouteStatuses.Available)
            { 
                StraightConveyor conv = convsWithALoadToRelease[0];
                convsWithALoadToRelease.Remove(conv);

                if (rhsConveyor.NextRouteStatus.Available == RouteStatuses.Available)
                {
                    ((StraightConveyor)conv.NextConveyor).RouteAvailable = RouteStatuses.Available;
                    ReleaseDelayTimer.Start();
                    return true;
                }
            }
            return false;
        }

        #endregion

        void lhsEnd_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            load.Switch(rhsEnd);
        }

        private void UpdateMergeAngles() 
        {
            float offset = (twoToOneInfo.width / 2) - (rhsConveyor.Width / 2);
            rhsConveyor.SnapEndTransformation.Offset = new Vector3(0, 0, -offset - EndOffSet);
            lhsConveyor.SnapEndTransformation.Offset = new Vector3(0, 0, offset - EndOffSet);

            double c = Math.Sqrt(Math.Pow((double)lhsConveyor.Length, 2) + Math.Pow((double)(offset - EndOffSet), 2));
            lhsEnd.Distance = (float)c; // could just set this to a big value and let it default to the end!
            rhsEnd.Distance = (float)c; // could just set this to a big value and let it default to the end!

            rhsConveyor.LocalPosition = new Vector3(rhsConveyor.LocalPosition.X, rhsConveyor.LocalPosition.Y, rhsConveyor.LocalPosition.Z);
            lhsConveyor.LocalPosition = new Vector3(lhsConveyor.LocalPosition.X, lhsConveyor.LocalPosition.Y, lhsConveyor.LocalPosition.Z);

            box.Length = rhsConveyor.Length;
            box.Width  = TwoToOneWidth;

            lhsConveyor.arrow.Yaw = (float)Math.Asin((double)(offset - EndOffSet) / c);
            rhsConveyor.arrow.Yaw = -(float)Math.Asin((double)(offset + EndOffSet) / c);

        }

        #region User Interface

        #region Standard Properties

        public override string Category
        {
            get { return "Assembly"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("TwoToOneMerge"); }
        }

        #endregion

        #region User Interface Size and Speed

        [Category("Size and Speed")]
        [DisplayName("Length")]
        [Description("Length of the merge conveyor (meter)")]
        [TypeConverter()]
        [PropertyOrder(1)]
        public float TwoToOneLength
        {
            get { return twoToOneInfo.length; }
            set
            {
                if (value > 0)
                {
                    twoToOneInfo.length = value;
                    rhsConveyor.Length = value;
                    lhsConveyor.Length = value;
                    Core.Environment.Invoke(() => UpdateMergeAngles());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Width")]
        [Description("Width of the Transfer (meter)")]
        [TypeConverter()]
        [PropertyOrder(2)]        
        public float TwoToOneWidth
        {
            get { return twoToOneInfo.width; }
            set
            {
                if (value > 0)
                {
                    twoToOneInfo.width  = value;
                    rhsConveyor.LocalPosition = new Vector3(rhsConveyor.LocalPosition.X, rhsConveyor.LocalPosition.Y, (value / 2) - (rhsConveyor.Width / 2));
                    lhsConveyor.LocalPosition = new Vector3(lhsConveyor.LocalPosition.X, lhsConveyor.LocalPosition.Y, -((value / 2) - (lhsConveyor.Width / 2)));
                    Core.Environment.Invoke(() => UpdateMergeAngles());
                }
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Internal Conv Width")]
        [Description("Width of the internal assembly conveyor based on standard Dematic case conveyor widths")]
        [TypeConverter()]
        [PropertyOrder(3)]
        public CaseConveyorWidth InternalConvWidth
        {
            get { return twoToOneInfo.internalConvWidth; }
            set
            {
                if (value > 0)
                {
                    twoToOneInfo.internalConvWidth = value;
                    lhsConveyor.ConveyorWidth = value;
                    rhsConveyor.ConveyorWidth = value;

                    TwoToOneWidth = TwoToOneWidth;//toggling the width for some reason .. seems to work!!
                    Core.Environment.Invoke(() => UpdateMergeAngles());
                }
            }
        } 

        [Category("Size and Speed")]
        [DisplayName("End Offset")]
        [Description("Off-set of the merge point (meter)")]
        [TypeConverter()]
        [PropertyOrder(4)]
        public float EndOffSet
        {
            get { return twoToOneInfo.endOffSet; }
            set
            {
                twoToOneInfo.endOffSet = value;
                Core.Environment.Invoke(() => UpdateMergeAngles());
            }
        }


        [Category("Size and Speed")]
        [Description("Speed of transfer")]
        [TypeConverter()]
        [PropertyOrder(5)]
        public float Speed
        {
            get { return twoToOneInfo.speed; }
            set
            {
                lhsConveyor.Speed = value;
                rhsConveyor.Speed = value;

                twoToOneInfo.speed = value;
                Core.Environment.Properties.Refresh();
            }
        }

        #endregion

        #region User Interface Routing

        [Category("Routing")]
        [DisplayName("Release Time (s)")]
        [Description("Only alows a load to enter the transfer at timed intervals")]
        [PropertyOrder(1)]
        public float ReleaseDelayTime
        {
            get { return twoToOneInfo.releaseDelayTime; }
            set
            {
                if (ReleaseDelayTimer.Running)
                {
                    ReleaseDelayTimer.Stop();
                    ReleaseDelayTimer.Timeout = value;
                    ReleaseDelayTimer.Start();
                }
                else
                {
                    ReleaseDelayTimer.Timeout = value;
                }
                twoToOneInfo.releaseDelayTime = value;
            }
        }

        #endregion

        #region User Interface Position

        [Category("Position")]
        [DisplayName("Height")]
        [Description("Height of the transfer (meter)")]
        [TypeConverter()]
        public float TwoToOneHeight
        {
            get { return Position.Y; }
            set
            {
                Position = new Vector3(Position.X, value, Position.Z);
                Core.Environment.Properties.Refresh();
            }
        }

        #endregion

        [Browsable(false)]
        public override Core.Assemblies.EventCollection Events
        {
            get
            {
                return base.Events;
            }
        }

        [Browsable(false)]
        public override bool Enabled
        {
            get
            {
                return base.Enabled;
            }
            set
            {
                base.Enabled = value;
            }
        }

        [Browsable(false)]
        public override float Yaw
        {
            get
            {
                return base.Yaw;
            }
            set
            {
                base.Yaw = value;
            }
        }

        [Browsable(false)]
        public override float Roll
        {
            get
            {
                return base.Roll;
            }
            set
            {
                base.Roll = value;
            }
        }

        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(TwoToOneMergeInfo))]
    public class TwoToOneMergeInfo : AssemblyInfo
    {
        public float endOffSet = 0;
        public CaseConveyorWidth internalConvWidth = CaseConveyorWidth._500mm;
        public float releaseDelayTime = 1;
        public float speed = 0.7f;
        #region Fields

        private static TwoToOneMergeInfo properties = new TwoToOneMergeInfo();

        #endregion
        #region Properties

        public static object Properties
        {
            get
            {
                properties.color = Experior.Core.Environment.Scene.DefaultColor;
                return properties;
            }
        }

        #endregion
    }
}