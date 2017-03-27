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
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class BeltSorterMerge : StraightBeltConveyor
    {
        BeltSorterMergeInfo beltSorterMergeInfo;
        StraightConveyor mergeSection;
        ActionPoint apStraight = new ActionPoint();
        ActionPoint apMerge = new ActionPoint();
        BeltSorterInduct MainInduct;
        RouteStatuses mergeRouteStatus = RouteStatuses.Request;

        Timer TeachTimer = new Timer(100);
        Timer OffSetTimer = new Timer(1);

        CasePhotocell MergePhotocell;

        public static event EventHandler<BeltSorterMergeArgs> OnMergeLoadArrived; //This is the event that can be subscribed to in the routing script        

        float mergeCentreOffset;

        public BeltSorterMerge(BeltSorterMergeInfo info):base(info)
        {

            beltSorterMergeInfo = info;

            if (info.type == MergeType.PopUp)
            {
                arrow.Dispose();
            }

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
            mergeSection.TransportSection.Route.InsertActionPoint(apMerge, info.mergeConveyorLength);

            apMerge.OnEnter += apMerge_OnEnter;
            apStraight.OnEnter += apStraight_OnEnter;

            mergeSection.StartFixPoint.OnSnapped += MergeSectionStartFixPoint_OnSnapped;
            mergeSection.StartFixPoint.OnUnSnapped += MergeSectionStartFixPoint_OnUnSnapped;

            ControlType = info.ControlType;

            if (beltControl.LineReleasePhotocell != null)
            {
                beltControl.LineReleasePhotocell.Dispose();
            }

            //if (mergeSection.beltControl.LineReleasePhotocell != null)
            //{
            //    mergeSection.beltControl.LineReleasePhotocell.Dispose();
            //}

            MergeAngle          = info.mergeAngle;
            Length              = info.length;
            MergeConveyorOffset = info.mergeConveyorOffset;

            OffSetTimer.OnElapsed += OffSetTimer_OnElapsed;
            OffSetTimer.Timeout = OffSetInductTime;

            UpdateConveyor();
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();

            if (MainInductName != null && Core.Assemblies.Assembly.Items.ContainsKey(MainInductName))
            {
                MainInduct = Core.Assemblies.Assembly.Items[MainInductName] as BeltSorterInduct;
                MainInduct.WindowTimeout.OnElapsed += WindowTimeout_OnElapsed;
            }

            MergePhotocell = GetMergePhotocell(beltSorterMergeInfo.MergePhotocellName);
        }

        public override void Reset()
        {
            base.Reset();
            if (ControlType == ControlTypesSubSet.Test)
            {
                SetMergeSectionRouteStatus(RouteStatuses.Request);
            }
        }

        private void SetMergeSectionRouteStatus(RouteStatuses routeStatus)
        {
            //if the conveyor becomes blocked, and the offset timer is running, then stop it and restart when the conveyor clears
            if (routeStatus == RouteStatuses.Blocked && OffSetTimer.Running)
            {
                OffSetTimer.Stop();
                OffSetTimer.UserData = true; //User data used to define if offset timer was running when the conveyor becomes blocked
            }
            else
            {
                if (OffSetTimer.UserData is bool && (bool)OffSetTimer.UserData)
                {
                    OffSetTimer.UserData = false;
                    OffSetTimer.Start();
                }
            }
            //if the sorter becomes blocked and a load is diverting on to it, then stop it, but remember it was running when the sorter starts again
            mergeSection.RouteAvailable = routeStatus;
            if (routeStatus != RouteStatuses.Blocked)
            {
                mergeRouteStatus = routeStatus;
            }
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
            base.NextRouteStatus_OnAvailableChanged(sender, e); //Handles the normal belt control

            if (ControlType == ControlTypesSubSet.Test)
            {
                SetMergeSectionRouteStatus(e._available);
            }
            else
            {
                if (mergeSection.RouteAvailable == RouteStatuses.Blocked)
                {
                    SetMergeSectionRouteStatus(mergeRouteStatus);
                }
                else
                {
                    SetMergeSectionRouteStatus(e._available);
                }
            }
        }

        void Route_OnLoadRemoved(Route sender, Load load)
        {
            SetMergeSectionRouteStatus(RouteStatuses.Available);
        }

        void Route_OnArrived(Load load)
        {
            SetMergeSectionRouteStatus(RouteStatuses.Request);
        }

        public void MergeSectionStartFixPoint_OnSnapped(FixPoint fixpoint, FixPoint.SnapEventArgs e)
        {
            PreviousConveyor = fixpoint.Parent as IRouteStatus;
            PreviousLoadWaiting = PreviousConveyor.GetLoadWaitingStatus(fixpoint);
            PreviousLoadWaiting.OnLoadWaitingChanged += PreviousLoadWaiting_OnLoadWaitingChanged;
        }

        public void MergeSectionStartFixPoint_OnUnSnapped(FixPoint fixpoint)
        {
            PreviousLoadWaiting.OnLoadWaitingChanged -= PreviousLoadWaiting_OnLoadWaitingChanged;
            PreviousLoadWaiting = null;
            PreviousConveyor = null;
        }

        void PreviousLoadWaiting_OnLoadWaitingChanged(object sender, LoadWaitingChangedEventArgs e)
        {
            if (OnMergeLoadArrived != null)
            {
                OnMergeLoadArrived(this, new BeltSorterMergeArgs(this, e._waitingLoad));
            }

            if (!e._loadWaiting)
            {
                SetMergeSectionRouteStatus(RouteStatuses.Request);
            }

            //********** TEACHING **********
            if (e._loadWaiting == true)
            {
                if (e._waitingLoad.Identification.ToUpper() == "TEACH2") //This Timer position is being taught
                {
                    TeachTimer.Reset();
                    TeachTimer.Start();

                    SetMergeSectionRouteStatus(RouteStatuses.Request);
                }
            }
            //********** TEACHING **********
        }

        void apMerge_OnEnter(ActionPoint sender, Load load)
        {
            //********** TEACHING **********

            if (load.Identification.ToUpper() == "TEACH2" && TeachTimer.Running)
            {
                TeachTimer.Stop();
                ThisInductTime = 100 - (float)TeachTimer.TimeRemaining;

                Log.Write(string.Format("Belt Sorter Induct {0}, This Induct Timer = {1}", Name, ThisInductTime));

                CalculateTeachOffset();

            }
            //********** TEACHING **********


            bool keepOrientation = false;
            if (beltSorterMergeInfo.type == MergeType.PopUp)
            {
                keepOrientation = true;
            }
            load.Switch(apStraight, keepOrientation);
        }

        void apStraight_OnEnter(ActionPoint sender, Load load)
        {
            if (load.Identification.ToUpper() == "TEACH1" && MainInduct != null)
            {
                //Add values into the array of the induct
                
                MainInductTime = MainInduct.TimeFromInductTime - (float)MainInduct.TimeFromInduct.TimeRemaining;
                Log.Write(string.Format("Belt Sorter Induct {0}, Main Induct Timer = {1}", Name, MainInductTime));

                CalculateTeachOffset();
            }
        }

        private void CalculateTeachOffset()
        {
            if (MainInductTime != 0 && ThisInductTime != 0 && MainInduct != null)
            {
                OffSetInductTime = ((MainInductTime - ThisInductTime) % MainInduct.WindowTimeoutTime);
                OffSetTimer.Stop(); //This will get restarted when the induct timer event triggers again
                OffSetTimer.Timeout = OffSetInductTime;

                //Also add relevant values to the main induct conveyor
                if (MainInduct.TeachTimes.ContainsKey(Name))
                {
                    MainInduct.TeachTimes.Remove(Name);
                }
                MainInduct.TeachTimes.Add(Name, MainInductTime - ThisInductTime);
            }
        }

        void WindowTimeout_OnElapsed(Timer sender)
        {
            if (!OffSetTimer.Running)
            {
                OffSetTimer.Start();
            }
            else
            {
                Log.Write(string.Format("Error on Sundec sorter at induct {0}, window timeout occured before offset timeout complete, please perform teach operation to resolve", Name));
            }
        }

        void OffSetTimer_OnElapsed(Timer sender)
        {
            OffSetTimer.Reset();

            //When this timer has elapsed then the load can be released if there is no other load in the window
            if (MergePhotocell != null)
            {
                if (PreviousLoadWaiting.LoadWaiting && MergePhotocell.PhotocellStatus == PhotocellState.Clear)
                {
                    SetMergeSectionRouteStatus(RouteStatuses.Available);
                }
            }
            else
            {
                if (PreviousLoadWaiting.LoadWaiting)
                {
                    SetMergeSectionRouteStatus(RouteStatuses.Available);
                }
            }
        }

        public override void UpdateConveyor()
        {
            apStraight.Distance = MergeConveyorOffset;
            apMerge.Distance = MergeLength;
        
            float zOffsetCentre = (float)(Math.Sin(MergeAngle) * (mergeSection.Length / 2));

            if (MergeSide == Side.Left)
            {
                mergeSection.LocalPosition = new Vector3(mergeCentreOffset - beltSorterMergeInfo.mergeConveyorOffset, mergeSection.LocalPosition.Y, -zOffsetCentre);
                mergeSection.LocalYaw = MergeAngle;
            }
            else
            {
                mergeSection.LocalPosition = new Vector3(mergeCentreOffset - beltSorterMergeInfo.mergeConveyorOffset, mergeSection.LocalPosition.Y, zOffsetCentre);
                mergeSection.LocalYaw = -MergeAngle;
            }

            if (beltSorterMergeInfo.type == MergeType.Angled)
            {
                mergeSection.arrow.LocalPosition = new Vector3(mergeSection.Length / 2 - 0.2f, 0, 0);
            }
        }

        public CasePhotocell GetMergePhotocell(string PhotocellName)
        {
            if (!string.IsNullOrEmpty(PhotocellName))
            {
                string[] assemDev = PhotocellName.Split(',');
                StraightBeltConveyor conv = Core.Assemblies.Assembly.Items[assemDev[0]] as StraightBeltConveyor;
                if (conv != null)
                {
                    if (conv.Assemblies != null)
                    {
                        foreach (Assembly device in conv.Assemblies)
                        {
                            if (device.Name == assemDev[1])
                            {
                                // MergePhotocell = device as Photocell;
                                return device as CasePhotocell;
                                //break;
                            }
                        }
                        //Did not find the device as it has not been created on the conveyor yet wait until they are 
                    }
                    else
                    {
                        conv.OnDevicesCreated += conv_OnDevicesCreated;
                    }
                }
            }
            return null;
        }

        //If the devices on the found conveyor are created after this one then subscribe to the devices created 
        //event and try and find it then
        void conv_OnDevicesCreated(object sender, EventArgs e)
        {
            StraightBeltConveyor conv = sender as StraightBeltConveyor;
            conv.OnDevicesCreated -= conv_OnDevicesCreated;

            string[] assemDev = beltSorterMergeInfo.MergePhotocellName.Split(',');
            
            foreach (Assembly device in ((StraightConveyor)sender).Assemblies)
            {
                if (device.Name == assemDev[1])
                {
                     MergePhotocell = device as CasePhotocell;
                    break;
                }
            }
        }

        public override string Category
        {
            get { return "Belt Sorter"; }
        }

        public override Image Image
        {
            get 
            {
                if (beltSorterMergeInfo.type == MergeType.Angled)
                {
                    return Common.Icons.Get("BeltSorterMerge");
                }
                return Common.Icons.Get("BeltSorterMergePopUp");   
            }
        }

        #region User Interface

        #region Belt Sorter
        [Category("Belt Sorter")]
        [DisplayName("Main Induct Point")]
        [Description("Conveyor where the loads enter the Belt Sorter")]
        [PropertyOrder(1)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [TypeConverter(typeof(InductConveyorConverter))]
        public string MainInductName
        {
            get { return beltSorterMergeInfo.MainInductName; }
            set
            {
                if (beltControl.LineReleasePhotocell != null && Core.Assemblies.Assembly.Items.ContainsKey(beltSorterMergeInfo.MainInductName))
                {
                    MainInduct.WindowTimeout.OnElapsed -= WindowTimeout_OnElapsed;
                }

                if (Core.Assemblies.Assembly.Items.ContainsKey(value))
                {

                    MainInduct = Core.Assemblies.Assembly.Items[value] as BeltSorterInduct;
                    MainInduct.WindowTimeout.OnElapsed += WindowTimeout_OnElapsed;
                }
                beltSorterMergeInfo.MainInductName = value;
            }
        }

        [Category("Belt Sorter")]
        [DisplayName("Main Induct Time")]
        [Description("Time it takes to travel from main induct point to transfer induct point (See Belt Sorter teach instructions)")]
        [PropertyOrder(2)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [ReadOnly(true)]
        public float MainInductTime
        {
            get { return beltSorterMergeInfo.MainInductTime; }
            set
            {
                beltSorterMergeInfo.MainInductTime = value;
            }
        }

        [Category("Belt Sorter")]
        [DisplayName("This Induct Time")]
        [Description("Time it takes to travel from this induct point to transfer induct point (See Belt Sorter teach instructions)")]
        [PropertyOrder(3)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [ReadOnly(true)]
        public float ThisInductTime
        {
            get { return beltSorterMergeInfo.ThisInductTime; }
            set
            {
                beltSorterMergeInfo.ThisInductTime = value;
            }
        }

        [Category("Belt Sorter")]
        [DisplayName("Offset Timer")]
        [Description("Time off set from this induct to the main induct point, based on the TEACH1 and TEACH2 timings (See Belt Sorter teach instructions")]
        [PropertyOrder(4)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        [ReadOnly(true)]
        public float OffSetInductTime
        {
            get { return beltSorterMergeInfo.OffsetInductTime; }
            set
            {
                beltSorterMergeInfo.OffsetInductTime = value;
            }
        }

        [Category("Belt Sorter")]
        [DisplayName("Merge Photocell")]
        [TypeConverter(typeof(AllPhotocellConverter))]
        [PropertyOrder(1)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        public string MergePhotocellName
        {
            get { return beltSorterMergeInfo.MergePhotocellName; }
            set
            {
                beltSorterMergeInfo.MergePhotocellName = value;
                MergePhotocell = GetMergePhotocell(value);
                Core.Environment.Properties.Refresh();
            }
        }


        #endregion

        #region Routing

        [Category("Routing")]
        [DisplayName("Control Type")]
        [Description("Set to Test when checking that the load is released correctly onto the route, Once the Induct has been taught then set to Local and the loads will be released into the windows")]
        [PropertyOrder(20)]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        public ControlTypesSubSet ControlType
        {
            get
            {
                return beltSorterMergeInfo.ControlType;
            }
            set
            {
                beltSorterMergeInfo.ControlType = value;
                if (value == ControlTypesSubSet.Test)
                {
                    TransportSection.Route.OnArrived += Route_OnArrived;
                    TransportSection.Route.OnLoadRemoved += Route_OnLoadRemoved;
                    SetMergeSectionRouteStatus(RouteStatuses.Available);
                }
                else
                {
                    TransportSection.Route.OnArrived -= Route_OnArrived;
                    TransportSection.Route.OnLoadRemoved -= Route_OnLoadRemoved;
                    SetMergeSectionRouteStatus(RouteStatuses.Request);

                }
                Core.Environment.Properties.Refresh();
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
                if (value < MergeConveyorOffset && beltSorterMergeInfo.type == MergeType.Angled)
                {
                    Core.Environment.Log.Write(string.Format("Conveyor Length must not be less than 'Divert Conveyor Offset' ({0}).", MergeConveyorOffset), System.Drawing.Color.Red);
                }
                else
                {
                    base.Length = value;
                    mergeCentreOffset = (float)(Length / 2) + (float)Math.Cos(MergeAngle) * (MergeLength / 2);
                    Core.Environment.Invoke(() => UpdateConveyor());

                    if (beltSorterMergeInfo.type == MergeType.PopUp)
                    {
                        mergeSection.Width = value;
                        MergeConveyorOffset = value / 2;
                    }

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
                return beltSorterMergeInfo.mergeConveyorLength;
            }
            set
            {
                if (value > 0)
                {
                    mergeCentreOffset = (float)(Length / 2) + (float)Math.Cos(MergeAngle) * (value / 2);
                    beltSorterMergeInfo.mergeConveyorLength = value;
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
            get { return beltSorterMergeInfo.conveyorWidth; }
            set
            {
                Width = (float)value / 1000;
                beltSorterMergeInfo.conveyorWidth = value;

                if(beltSorterMergeInfo.type == MergeType.PopUp)
                {
                    MergeLength = Width / 2;
                }
            }
        }



        [Category("Size and Speed")]
        [DisplayName("Merge Width")]
        [PropertyOrder(4)]
        [Description("Width of the merge section conveyor based on standard Dematic case conveyor widths")]
        [PropertyAttributesProvider("DynamicPropertyPopUporAngled")]
        public CaseConveyorWidth MergeWidth
        {
            get { return beltSorterMergeInfo.mergeWidth; }
            set
            {
                mergeSection.Width = (float)value / 1000;
                beltSorterMergeInfo.mergeWidth = value;
            }
        }

        [Category("Size and Speed")]
        [DisplayName("Merge Speed")]
        [PropertyOrder(5)]
        [Description("Speed of the Merge section conveyor (Speed of straight section is taken from the next conveyor)")]
        [TypeConverter(typeof(SpeedConverter))]
        public float MergeSpeed
        {
            get { return beltSorterMergeInfo.mergeSpeed; }
            set
            {
                beltSorterMergeInfo.mergeSpeed = value;
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
            get { return beltSorterMergeInfo.mergeAngle; }
            set
            {
                {
                    beltSorterMergeInfo.mergeAngle = value;
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
            get { return beltSorterMergeInfo.mergeConveyorOffset; }
            set
            {
                beltSorterMergeInfo.mergeConveyorOffset = value;
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
            get { return beltSorterMergeInfo.mergeSide; }
            set
            {
                beltSorterMergeInfo.mergeSide = value;
                Core.Environment.Invoke(() => UpdateConveyor());
            }
        }

        public void DynamicPropertyPopUporAngled(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = true;
            //attributes.IsBrowsable = beltSorterMergeInfo.type  == MergeType.Angled;
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
        public override string LineReleasePhotocellName
        {
            get { return base.LineReleasePhotocellName; }
            set { base.LineReleasePhotocellName = value; }
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

        //public override System.Drawing.Color Color
        //{
        //    get
        //    {
        //        return base.Color;
        //    }
        //    set
        //    {
        //        base.Color = value;
        //        mergeSection.Color = value;
        //    }
        //}

        #endregion

    }
    
    [Serializable]
    [XmlInclude(typeof(BeltSorterMergeInfo))]
    public class BeltSorterMergeInfo : StraightBeltConveyorInfo
    {
        public float mergeConveyorOffset = 0.5f;
        public float mergeConveyorLength = 0.7f;
        public Side mergeSide = Side.Left;
        public CaseConveyorWidth mergeWidth = CaseConveyorWidth._500mm;
        public float mergeAngle;
        public float mergeSpeed = 0.7f;
        public ControlTypesSubSet ControlType;
        public string MainInductName;
        public float MainInductTime;
        public float ThisInductTime;
        public float OffsetInductTime;
        public string MergePhotocellName;
        public MergeType type;
    }


    
    public class BeltSorterMergeArgs : EventArgs
    {
        public readonly BeltSorterMerge _beltSorterMerge;
        public readonly Load _load;

        public BeltSorterMergeArgs(BeltSorterMerge beltSorterMerge, Load load)
        {
            _beltSorterMerge = beltSorterMerge;
            _load = load;
        }
    }

}