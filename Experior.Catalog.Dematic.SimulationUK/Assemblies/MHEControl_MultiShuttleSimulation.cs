using Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies;
using Experior.Dematic;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;

namespace Experior.Catalog.Assemblies
{
    class MHEControl_MultiShuttleSimulation : MHEControl
    {
        private MultiShuttleSimInfo multiShuttleSimInfo;
        MultiShuttle theMultishuttle;
        private static Random rand = new Random();

        /// <summary>
        /// Rack Location for an ElevatorTask takes the form: aasyyxz: a=aisle, s = side, y = level, x = input or output, Z = loc A or B e.g. 01R05OA
        /// Elevator Source location for a shuttleTask takes the form: sxxxyydd: Side, xxx location, yy = level, dd = depth
        /// </summary>
        /// <param name="info"></param>
        /// <param name="cPoint"></param>
        public MHEControl_MultiShuttleSimulation(MultiShuttleSimInfo info, MultiShuttle cPoint)
        {
            theMultishuttle = cPoint;
            multiShuttleSimInfo = info;  // set this to save properties 

            theMultishuttle.OnArrivedAtPickStationConvPosB += multiShuttle_OnArrivedAtPickStationConvPosB;
            theMultishuttle.OnArrivedAtPickStationConvPosA += multiShuttle_OnArrivedAtPickStationConvPosA;
            theMultishuttle.OnArrivedAtInfeedRackConvPosB  += theMultishuttle_OnArrivedAtInfeedRackConvPosB;
            theMultishuttle.OnArrivedAtOutfeedRackConvPosA += theMultishuttle_OnArrivedAtOutfeedRackConvPosA;
            theMultishuttle.OnArrivedAtOutfeedRackConvPosB += theMultishuttle_OnArrivedAtOutfeedRackConvPosB;
            theMultishuttle.OnLoadTransferingToPickStation += theMultishuttle_OnLoadTransferingToPickStation;
            theMultishuttle.OnArrivedAtRackLocation        += theMultishuttle_OnArrivedAtRackLocation;

            //List<string> setShuttleTasks = new List<string>() { "L0270902", "R0150202", "L0340102", "R0280302", "L0090701", "R0400802", "L0370802", "R0140102", "L0390402", "R0180602" };

            //foreach (string item in setShuttleTasks)
            //{
            //    ShuttleTask sT = new ShuttleTask();
            //    sT.LoadColor = Color.Peru;
            //    sT.LoadHeight = 0.32f;
            //    sT.LoadLength = 0.65f;
            //    sT.LoadWidth = 0.32f;
            //    sT.LoadWeight = 2.3f;
            //    sT.Source = item;
            //    sT.Level = sT.Source.Level();
            //    sT.Destination = string.Format("01R{0}OB", sT.Level.ToString().PadLeft(2, '0'));
            //    sT.Barcode = FeedCase.GetSSCCBarcode();
            //    theMultishuttle.shuttlecars[sT.Level].ShuttleTasks.Add(sT);

            //}

            ShuttleTask sT = new ShuttleTask();

            try
            {
                //sT.LoadColor = Color.Peru;
                //sT.LoadHeight = 0.32f;
                //sT.LoadLength = 0.65f;
                //sT.LoadWidth = 0.32f;
                //sT.LoadWeight = 2.3f;
                sT.Source = "R0011001";//"L0011001"
                sT.Level = sT.Source.Level();
                sT.Destination = string.Format("01{0}{1}OA", (char)(sT.Source.Side()), sT.Level.ToString().PadLeft(2, '0'));
                sT.Barcode = FeedCase.GetSSCCBarcode();
                theMultishuttle.shuttlecars[sT.Level].ShuttleTasks.Add(sT);

                sT = new ShuttleTask();
                //sT.LoadColor = Color.Peru;
                //sT.LoadHeight = 0.32f;
                //sT.LoadLength = 0.65f;
                //sT.LoadWidth = 0.32f;
                //sT.LoadWeight = 2.3f;
                sT.Source = "R0110801";
                sT.Level = sT.Source.Level();
                sT.Destination = string.Format("01{0}{1}OA", (char)sT.Source.Side(), sT.Level.ToString().PadLeft(2, '0'));
                sT.Barcode = FeedCase.GetSSCCBarcode();
                theMultishuttle.shuttlecars[sT.Level].ShuttleTasks.Add(sT);
            }
            catch (Exception ex)
            {
                Log.Write("Error in simulation: " + ex.Message, Color.Red);
            }

            //for (int i = 0; i < 30; i++)
            //{
            //    ShuttleTask task = GenerateBinLocRetrival(1);
            //    if (task != null)
            //    {
            //        theMultishuttle.shuttlecars[task.Source.Level()].ShuttleTasks.Add(task);
            //        task = null;
            //    }
            //}


            //List<RackConveyor> infeedRackConvTemp = new List<RackConveyor>();

            //foreach (DematicActionPoint dAP in theMultishuttle.ConveyorLocations)
            //{
            //    if (dAP.LocName.ConvType() == ConveyorTypes.InfeedRack)
            //    {
            //        infeedRackConvTemp.Add((dAP.Parent.Parent.Parent) as RackConveyor);
            //    }
            //}

            //var hash = new HashSet<RackConveyor>(infeedRackConvTemp);
            //List<RackConveyor> infeedRackConv = hash.ToList();
            

        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }

        private RackSide rSide = RackSide.Right;

        /// <summary>
        /// Alternates between left and right sides
        /// </summary>
        /// <returns></returns>
        private RackSide GetRackSide()
        {
            if (rSide == RackSide.Left)
            {
                rSide = RackSide.Right;
                return RackSide.Right;
            }

            rSide = RackSide.Left;
            return RackSide.Left;
        }

        /// <summary>
        /// Generate a random level
        /// </summary>
        /// <returns> String padded left with 0 if needed for a string length of 2</returns>
        private string GetRandomLevel()
        {
            return rand.Next(1, theMultishuttle.Levels + 1).ToString().PadLeft(2, '0');            
        }

        private string GetRandomFreeLevel()
        {
            string level = string.Empty;
            var sl           = theMultishuttle.shuttlecars.Values.ToList();
            var freeShuttles = sl.FindAll(x => x.CurrentTask == null); // x.ShuttleTasks.Count < 2);

            if (freeShuttles.Count == 0)
            {
                return level;
            }

            var shuttle = freeShuttles[rand.Next(freeShuttles.Count)];
            return shuttle.trackRail.Level.ToString().PadLeft(2, '0');           
        }

        private string GenerateBinLocation(string level)
        {
            return string.Format("{0}{1}{2}{3}", (char)GetRackSide()                                                  //s   - side
                                        , rand.Next(1, (int)theMultishuttle.RackBays + 1).ToString().PadLeft(3, '0')  //xxx - Bin loc
                                        , level.PadLeft(2,'0')                                                        //yy  - level
                                        , rand.Next(1, 3).ToString().PadLeft(2, '0'));                                //dd  - depth
        }

        private ShuttleTask GenerateBinLocRetrival(int aisle, int level = 0)
        {
            ShuttleTask sT     = new ShuttleTask();
            string freeLevel   = string.Empty;
            string binLocation = string.Empty;

            if (level == 0) // no level has been imputted
            {
                freeLevel = GetRandomFreeLevel();
            }
            else if (level > 0)
            {
                freeLevel = level.ToString().PadLeft(2, '0');
            }

            if (freeLevel != string.Empty) // if nothing was returned from GetRandomFreeLevel then string will be empty
            {
                //sT.LoadColor  = System.Drawing.Color.Peru;
                //sT.LoadHeight = 0.32f;
                //sT.LoadLength = 0.6f;
                //sT.LoadWidth  = 0.4f;
                //sT.LoadWeight = 2.3f;
                sT.Source = GenerateBinLocation(freeLevel);
                //sT.Source = string.Format("{0}{1}{2}{3}", (char)GetRackSide()                                                         //s   - side
                //                                        , rand.Next(1, (int)theMultishuttle.RackBays + 1).ToString().PadLeft(3, '0')  //xxx - Bin loc
                //                                        , freeLevel                                                                   //yy  - level
                //                                        , rand.Next(1, 3).ToString().PadLeft(2, '0'));                                //dd  - depth

                //Core.Environment.Log.Write(sT.Source);
                string side = "R";
                int intFreeLevel;
                int.TryParse(freeLevel, out intFreeLevel);
                if (theMultishuttle.LeftEvenLevelInfeed) //infeeds and outfeeds are mixed
                {
                    if (theMultishuttle.LeftEvenLevelInfeed && intFreeLevel % 2 == 0) 
                    {
                        side = "R";
                    }
                    else if (theMultishuttle.LeftEvenLevelInfeed && intFreeLevel % 2 > 0)
                    {
                        side = "L";
                    }
                }
                sT.Destination = string.Format("{0}{1}{2}OB", aisle.ToString().PadLeft(2, '0'),side, freeLevel);
                sT.Barcode     = FeedCase.GetSSCCBarcode();
                return sT;
            }

            return null;
        }


        void theMultishuttle_OnArrivedAtRackLocation(object sender, RackConveyorArrivalEventArgs e)
        {
         //   Timer timer = new Timer(obj => { DelayedShuttleTask(e); }, null, 10000, Timeout.Infinite);
        }

        private void DelayedShuttleTask(RackConveyorArrivalEventArgs e)
        {
            Core.Environment.Log.Write("task");

            ShuttleTask sT = new ShuttleTask();
            sT.Level = e._locationName.Level();
            //sT.LoadColor = Color.Peru;
            //sT.LoadHeight = 0.32f;
            //sT.LoadLength = 0.65f;
            //sT.LoadWidth = 0.32f;
            //sT.LoadWeight = 2.3f;
            sT.Destination = "01R06OB";
            sT.Source = e._locationName;
            sT.Barcode = FeedCase.GetSSCCBarcode();
            theMultishuttle.shuttlecars[sT.Level].ShuttleTasks.Add(sT);
        }

        #region outfeed

        bool LoadTransfering = false;
        void theMultishuttle_OnLoadTransferingToPickStation(object sender, PickDropStationArrivalEventArgs e)
        {
            LoadTransfering = true;
        }

        RackConveyorOutfeedArrivalEventArgs load1Args, load2Args;
        private bool loading = true;

        void theMultishuttle_OnArrivedAtOutfeedRackConvPosB(object sender, RackConveyorOutfeedArrivalEventArgs e)
        {
            //aasyyxz: a=aisle, s = side, y = level, x = input or output, Z = loc A or B e.g. 01R05OA
            //if (loading)//2 loads out from different levels
            //{
            //    if (load1Args == null)
            //    {
            //        load1Args = e;
            //    }
            //    else
            //    {
            //        loading = false;
            //        load2Args = e;

            //        ElevatorTask elevatorTask = new ElevatorTask();
            //        elevatorTask.BarcodeLoadA = load1Args._caseLoad.SSCCBarcode;
            //        elevatorTask.SourceLoadA = string.Format("{0}{1}{2}{3}B",
            //                                                      load1Args._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                                      (char)load1Args._locationName.Side(),
            //                                                      load1Args._locationName.Level().ToString().PadLeft(2, '0'),
            //                                                      (char)load1Args._locationName.ConvType());

            //        elevatorTask.DestinationLoadA = string.Format("{0}{1}02DB",
            //                                                      load1Args._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                                      (char)load1Args._locationName.Side());

            //        elevatorTask.BarcodeLoadB = load2Args._caseLoad.SSCCBarcode;
            //        elevatorTask.SourceLoadB = string.Format("{0}{1}{2}{3}B",
            //                                                      load2Args._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                                      (char)load2Args._locationName.Side(),
            //                                                      load2Args._locationName.Level().ToString().PadLeft(2, '0'),
            //                                                      (char)load2Args._locationName.ConvType());

            //        elevatorTask.DestinationLoadB = string.Format("{0}{1}01DB",
            //                                                      load2Args._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                                      (char)load2Args._locationName.Side());

            //        elevatorTask.LoadCycle = Cycle.Single;
            //        elevatorTask.UnloadCycle = Cycle.Single;
            //        elevatorTask.Flow = TaskType.Outfeed;
            //        load2Args._elevator.ElevatorTasks.Add(elevatorTask);
            //        load1Args = null;
            //        load2Args = null;
            //    }
            //}

            //Single load out
            ElevatorTask elevatorTask = new ElevatorTask();
            elevatorTask.BarcodeLoadB = e._caseLoad.SSCCBarcode;
            elevatorTask.SourceLoadB = string.Format("{0}{1}{2}{3}B",
                                                          e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                          (char)e._locationName.Side(),
                                                          e._locationName.Level().ToString().PadLeft(2, '0'),
                                                          (char)e._locationName.ConvType());

            elevatorTask.DestinationLoadB = string.Format("{0}{1}02DB",
                                                          e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                          (char)e._locationName.Side());
            elevatorTask.LoadCycle = Cycle.Single;
            elevatorTask.UnloadCycle = Cycle.Single;
            elevatorTask.Flow = TaskType.Outfeed;
            e._elevator.ElevatorTasks.Add(elevatorTask);       
        }


        void theMultishuttle_OnArrivedAtOutfeedRackConvPosA(object sender, RackConveyorOutfeedArrivalEventArgs e)
        {

            //Case_Load loadB = e._elevator.ParentMultiShuttle.ConveyorLocations.Find(x => x.LocName == e._locationName.Substring(0, e._locationName.Length - 1) + "B").ActiveLoad as Case_Load;

            //if (loadB != null) // 2 loads at a single rack conveyor create double job
            //{
            //    ElevatorTask elevatorTask = new ElevatorTask();

            //    elevatorTask.BarcodeLoadA = e._caseLoad.SSCCBarcode;
            //    elevatorTask.BarcodeLoadB = loadB.SSCCBarcode;

            //    elevatorTask.SourceLoadA = string.Format("{0}{1}{2}{3}A",
            //                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                    (char)e._locationName.Side(),
            //                                    e._locationName.Level().ToString().PadLeft(2, '0'),
            //                                    (char)e._locationName.ConvType());

            //    elevatorTask.SourceLoadB = string.Format("{0}{1}{2}{3}B",
            //                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                    (char)e._locationName.Side(),
            //                                    e._locationName.Level().ToString().PadLeft(2, '0'),
            //                                    (char)e._locationName.ConvType());



            //    elevatorTask.DestinationLoadA = string.Format("{0}{1}02DA",
            //                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                    (char)e._locationName.Side());

            //    elevatorTask.DestinationLoadB = string.Format("{0}{1}01DB",
            //                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
            //                                    (char)e._locationName.Side());

            //    //elevatorTask.LoadCycle = Cycle.Single;
            //    elevatorTask.LoadCycle = Cycle.Single;

            //    elevatorTask.UnloadCycle = Cycle.Single;
            //    elevatorTask.Flow = TaskType.Outfeed;

            //    e._elevator.ElevatorTasks.Add(elevatorTask);
            //}            
        }

        void theMultishuttle_OnArrivedAtInfeedRackConvPosB(object sender, RackConveyorArrivalEventArgs e)
        {
            ShuttleTask sT = new ShuttleTask();
            sT.Barcode     = e._caseLoad.SSCCBarcode;
            sT.Source      = e._locationName;
            sT.Destination = GenerateBinLocation(e._locationName.Level().ToString());
            theMultishuttle.shuttlecars[sT.Source.Level()].ShuttleTasks.Add(sT);            
        }

        #endregion

        void multiShuttle_OnArrivedAtPickStationConvPosA(object sender, PickDropStationArrivalEventArgs e)
        {
            string str = string.Format("{0}B", e._locationName.Substring(0, e._locationName.Length - 1));
            var locB = theMultishuttle.ConveyorLocations.Find(x => x.LocName == str);

            if (locB.Active)
            {
                ElevatorTask elevatorTask = new ElevatorTask();
                elevatorTask.BarcodeLoadB = ((Case_Load)locB.ActiveLoad).SSCCBarcode;
                elevatorTask.BarcodeLoadA = e._caseLoad.SSCCBarcode;

                elevatorTask.DestinationLoadA = "01L06IB";
                elevatorTask.DestinationLoadB = "01L08IB";


                // Randomly choose between doing a single cycle off load and a double offload
                // This assumes that that there will be space for a double somewhere but woulf bbe easy to check for this condition
             //   if (rand.Next(0, 2) == 0)
                //{
                //    ////UnLoad cycle single
                //    elevatorTask.UnloadCycle = Cycle.Single;
                //    //var infeedsWithSpace = theMultishuttle.RackConveyors.FindAll(x => (x.RackConveyorType == MultiShuttleDirections.Infeed && x.TransportSection.Route.Loads.Count < 2));
                //    //var infeedConv = infeedsWithSpace[rand.Next(infeedsWithSpace.Count)];
                //    //infeedsWithSpace.Remove(infeedConv);         //remove so that it is not choosen again for this task

                //    elevatorTask.DestinationLoadA = string.Format("{0}L02IB", e._elevator.AisleNumber.ToString().PadLeft(2, '0'));

                //    //infeedConv = infeedsWithSpace[rand.Next(infeedsWithSpace.Count)]; //rechoose infeedConv 

                //    elevatorTask.DestinationLoadB = string.Format("{0}L04IB", e._elevator.AisleNumber.ToString().PadLeft(2, '0'));
                //}
              //  else
                {
                    //Unload cycle Double

                    //var infeedsWithSpace = theMultishuttle.RackConveyors.FindAll(x => (x.RackConveyorType == MultiShuttleDirections.Infeed && x.TransportSection.Route.Loads.Count == 0));
                    //var infeedConv = infeedsWithSpace[rand.Next(infeedsWithSpace.Count)];

                    elevatorTask.DestinationLoadA = string.Format("{0}L20IB", e._elevator.AisleNumber.ToString().PadLeft(2, '0'));
                    elevatorTask.DestinationLoadB = elevatorTask.DestinationLoadA;
                    elevatorTask.UnloadCycle = Cycle.Double;
                }

                elevatorTask.SourceLoadA = e._locationName;
                elevatorTask.SourceLoadB = locB.LocName;
                elevatorTask.LoadCycle = Cycle.Double;
                
                elevatorTask.Flow = TaskType.Infeed;
                e._elevator.ElevatorTasks.Add(elevatorTask);
            }

            LoadTransfering = false;
        }

        private void multiShuttle_OnArrivedAtPickStationConvPosB(object sender, PickDropStationArrivalEventArgs e)
        {

            //if (!LoadTransfering)  // A load is(not) currently transferring but is not yet at the pickstation
            //{
            //    var locB = theMultishuttle.ConveyorLocations.Find(x => x.LocName == e._locationName);
            //    PickStationConveyor psConv = locB.Parent.Parent.Parent as PickStationConveyor;

            //    if (psConv.TransportSection.Route.Loads.Count == 1)
            //    {
            //        ElevatorTask elevatorTask = new ElevatorTask()
            //        {
            //            BarcodeLoadB = e._caseLoad.SSCCBarcode,
            //            //DestinationLoadB = string.Format("01L{0}IB", GetRandomLevel()),
            //            DestinationLoadB = "01L12IB",
            //            SourceLoadB = e._locationName,
            //            LoadCycle = Cycle.Single,
            //            UnloadCycle = Cycle.Single,
            //            Flow = TaskType.Infeed
            //        };
            //        e._elevator.ElevatorTasks.Add(elevatorTask);
            //    }
            //}
        }        

        [Category("Controls")]
        [DescriptionAttribute("Time in seconds load waits for a second load before being picked up as a single")]
        [DisplayName("Pick Station Timeout")]
        public float PickStationTimeout
        {
            get { return multiShuttleSimInfo.pickStationTimeout; }
            set
            {
                if (value > 0 && multiShuttleSimInfo.pickStationTimeout != value)
                {
                    multiShuttleSimInfo.pickStationTimeout = value;
                }
            }
        }

    }

    [Serializable]
    [XmlInclude(typeof(MultiShuttleSimInfo))]
    public class MultiShuttleSimInfo : ProtocolInfo
    {
        public float pickStationTimeout = 30;
    }

}
