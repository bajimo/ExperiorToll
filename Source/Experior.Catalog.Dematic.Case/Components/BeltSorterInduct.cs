using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using System.Linq;
using Experior.Core.Loads;
using Experior.Core.Assemblies;
using Experior.Dematic.Base.Devices;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class BeltSorterInduct : StraightBeltConveyor
    {
        private BeltSorterInductInfo beltSorterInductInfo;
        private float windowTimeoutTime;
        public Timer WindowTimeout;

        public Timer TimeFromInduct;
        public float TimeFromInductTime = 9999; // set to some large time

        Timer photoCellPlacment;

        //public Dictionary<string, Tuple<float, float>> TeachTimes = new Dictionary<string, Tuple<float, float>>();
        public Dictionary<string, float> TeachTimes = new Dictionary<string, float>(); //key is the induct name
        //TODO if the induct name is changed after the creation of photocells then the photocell deleted will not work
        IEnumerator eSortedTeachTimes;
        Load photoCellCreator;
        float previousTime;

        public BeltSorterInduct(BeltSorterInductInfo info): base(info)
        {
            TimeFromInduct = new Timer(TimeFromInductTime);
            beltSorterInductInfo = info;

            WindowTimeout = new Timer(info.windowSize);
            WindowTimeout.OnElapsed += WindowTimeout_OnElapsed;            
            WindowTimeout.AutoReset = true;

            WindowSize = info.windowSize; //This also will start WindowTimeout

            if (!Core.Environment.Scene.Loading)
            {
                beltControl.LineReleasePhotocell.OnPhotocellStatusChanged +=LineReleasePhotocell_OnPhotocellStatusChanged;
            }
        }

        Dictionary<BeltSorterMerge, CasePhotocell> AllInductPhotoCells = new Dictionary<BeltSorterMerge, CasePhotocell>();
        List<BeltSorterMerge> lbsm = new List<BeltSorterMerge>();

        public override void Scene_OnLoaded()
        {
            if (!WindowTimeout.Running)
            {
                WindowTimeout.Start();
            }
            base.Scene_OnLoaded();
            beltControl.LineReleasePhotocell.OnPhotocellStatusChanged += LineReleasePhotocell_OnPhotocellStatusChanged;
        }

        public override void EndFixPoint_OnSnapped(Core.Parts.FixPoint stranger, Core.Parts.FixPoint.SnapEventArgs e)
        {
            RouteStatus rs = ((IRouteStatus)stranger.Parent).GetRouteStatus(stranger);
            rs.OnRouteStatusChanged += rs_OnRouteStatusChanged;
            base.EndFixPoint_OnSnapped(stranger, e);
        }

        public override void EndFixPoint_OnUnSnapped(Core.Parts.FixPoint stranger)
        {
            RouteStatus rs = ((IRouteStatus)stranger.Parent).GetRouteStatus(stranger);
            rs.OnRouteStatusChanged -= rs_OnRouteStatusChanged;
            base.EndFixPoint_OnUnSnapped(stranger);
        }

        /// <summary>
        /// make sure that the WindowTimeout is only running if the belts in front are runing
        /// </summary>     
        void rs_OnRouteStatusChanged(object sender, RouteStatusChangedEventArgs e)
        {
            if (e._available != RouteStatuses.Available && WindowTimeout.Running)
            {
                WindowTimeout.Stop();
            }
            else if (e._available == RouteStatuses.Available && !WindowTimeout.Running)
            {
                WindowTimeout.Start();
            }
        }

        private void AssignMergePhotoCellDeleted()
        {
            using (IEnumerator<Assembly> eAssem = Experior.Core.Assemblies.Assembly.Items.Values.GetEnumerator())
            {
                while (eAssem.MoveNext())
                {
                    if (eAssem.Current is BeltSorterMerge)
                    {
                        BeltSorterMerge current = eAssem.Current as BeltSorterMerge;
                        lbsm.Add(current);
                    }
                }
            }

            foreach (BeltSorterMerge bsm in lbsm)
            {
                if (!string.IsNullOrEmpty(bsm.MergePhotocellName))
                {
                    CasePhotocell mergePhotcell = bsm.GetMergePhotocell(bsm.MergePhotocellName);
                    AllInductPhotoCells.Add(bsm, mergePhotcell);
                    mergePhotcell.OnDeviceDeleted += mergePhotcell_OnDeviceDeleted;
                }
            }
        }

        public override void Reset()
        {
            WindowTimeout.Stop();
            photoCellCreator = null;
            TeachTimes.Clear();
            base.Reset();
            WindowTimeout.Reset();
            WindowTimeout.Start();
        }

        void WindowTimeout_OnElapsed(Timer sender)
        {
            if (windowedLoad != null)
            {
                //windowedLoad.Release();
                RouteAvailable = RouteStatuses.Available;
                windowedLoad = null;
            }

        }

        Load windowedLoad;
        void LineReleasePhotocell_OnPhotocellStatusChanged(object sender, PhotocellStatusChangedEventArgs e)
        {
            if (e._Load != null) //a reset will cause the load to be null and the status to change
            {
                if (e._PhotocellStatus == PhotocellState.Blocked && e._Load.Identification.ToUpper() == "TEACH1")
                {
                    //TimeFromInduct.AutoReset = true;
                    TimeFromInduct.Reset();
                    TimeFromInduct.Start();
                }
                else if (e._Load.Identification.ToUpper() == "TEACH3" && photoCellCreator == null)
                {
                    if (TeachTimes.Any())
                    {
                        //Sort the dictionary as we have no control in the order that thet were created this is used in the creation of photocells
                        var sortedTeachTimes = from entry in TeachTimes orderby entry.Value ascending select entry;
                        eSortedTeachTimes = sortedTeachTimes.GetEnumerator();
                    }

                    if (eSortedTeachTimes != null && eSortedTeachTimes.MoveNext())
                    {
                        photoCellCreator = e._Load;
                        KeyValuePair<string, float> photoCellInfo = (KeyValuePair<string, float>)eSortedTeachTimes.Current;
                        photoCellPlacment = new Timer(photoCellInfo.Value);
                        previousTime = photoCellInfo.Value;
                        photoCellPlacment.Start();
                        photoCellPlacment.OnElapsed += photoCellPlacment_OnElapsed;
                    }
                }
                else
                {
                    if (e._PhotocellStatus == PhotocellState.Blocked)
                    {
                        RouteAvailable = RouteStatuses.Blocked;
                        //e._Load.Stop();
                        windowedLoad = e._Load;
                    }
                }
            }
        }

        void photoCellPlacment_OnElapsed(Timer sender)
        {
            try
            {
                StraightBeltConveyor photocellBelt = photoCellCreator.Route.Parent.Parent as StraightBeltConveyor; //TODO what about other types --- really need a Dematic base conveyor            
                CasePhotocellInfo pcinfo = new CasePhotocellInfo()
                {
                    name = "Induct_" + ((KeyValuePair<string, float>)eSortedTeachTimes.Current).Key,
                    distance = photoCellCreator.Distance,
                    distanceFrom = PositionPoint.Start,
                    type = photocellBelt.beltControl.constructDevice.DeviceTypes["Add Photocell"].Item1
                };

                CasePhotocell pc = beltControl.constructDevice.InsertDevice(pcinfo, photocellBelt as IConstructDevice) as CasePhotocell;
                photocellBelt.DeviceInfos.Add(pcinfo); //Add the device info to the assembly that it was created on so that it will be saved when the user saves the model                
                BeltSorterMerge bsm = Experior.Core.Assemblies.Assembly.Items[((KeyValuePair<string, float>)eSortedTeachTimes.Current).Key] as BeltSorterMerge;

                bsm.MergePhotocellName = string.Format("{0},{1}", photocellBelt.Name, pc.Name);

                if (eSortedTeachTimes.MoveNext()) //reset the timer ready for the next photocell
                {
                    KeyValuePair<string, float> photoCellInfo = (KeyValuePair<string, float>)eSortedTeachTimes.Current;
                    photoCellPlacment.Timeout = photoCellInfo.Value - previousTime;
                    previousTime += photoCellInfo.Value - previousTime;
                    photoCellPlacment.Reset();
                    photoCellPlacment.Start();
                }
                else //After all photocells have been created subcribe to there deleted event
                {
                    AssignMergePhotoCellDeleted();
                }
            }
            catch (Exception ex)
            {
                Log.Write("Error in photoCellPlacment_OnElapsed: " + ex.Message);
            }
        }

        void mergePhotcell_OnDeviceDeleted(object sender, EventArgs e)
        {
            CasePhotocell pC = sender as CasePhotocell;
            BeltSorterMerge bsm = AllInductPhotoCells.FirstOrDefault(x => x.Value == pC).Key;
            bsm.MergePhotocellName = null;
        }

        #region Properties

        public override string Category
        {
            get { return "Belt Sorter"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("BeltSorterInduct"); }
        }

        #endregion

        [Category("Belt Sorter")]
        [DisplayName("Window Size")]
        [Description("Size of window required in meters")]
        public float WindowSize
        {
            get {return beltSorterInductInfo.windowSize; }
            set
            {
                WindowTimeout.Stop();
                beltSorterInductInfo.windowSize = value;
                WindowTimeoutTime = value / Speed;
                WindowTimeout.Timeout = WindowTimeoutTime;
                WindowTimeout.Reset();
                WindowTimeout.Start();
            }
        }

        [Category("Belt Sorter")]
        [DisplayName("Window Time")]
        [Description("Time between each load release from induct")]
        [ReadOnly(true)]
        public float WindowTimeoutTime
        {
            get { return windowTimeoutTime;}
            set
            {
                windowTimeoutTime = value;
            }
        }

        public override float Speed
        {
            get{return base.Speed;}
            set
            {
                base.Speed = value;
            }
        }
    }

    //public struct InductPos
    //{
    //    string s;
    //    int i;
    //}

    [Serializable]
    [XmlInclude(typeof(BeltSorterInductInfo))]
    public class BeltSorterInductInfo : StraightBeltConveyorInfo
    {
        public float windowSize = 1;
   
    }
}