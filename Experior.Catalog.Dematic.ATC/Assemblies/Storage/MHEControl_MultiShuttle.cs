using Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies;
using Experior.Dematic.Base;
using Experior.Dematic;
using System;
using System.Drawing;
using System.Xml.Serialization;
using System.Collections.Generic;
using Dematic.ATC;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;


namespace Experior.Catalog.Dematic.ATC.Assemblies.Storage
{
    /// <summary>
    /// This class is not strictly needed as only one controller will control a single aisle so everything here could go into the controller
    /// however following the normal pattern of an assembly that is controllable we have this object. So this class is used to interface to the assembly. 
    /// i.e. responding to events and creating shuttle and elevator tasks are all placed here.
    /// </summary>
    class MHEControl_MultiShuttle : MHEControl
    {
        MultiShuttle theMultishuttle;
        MHEController_Multishuttle mheController_Multishuttle;
        Dictionary<ElevatorTask, List<Case_Load>> currentTasks = new Dictionary<ElevatorTask, List<Case_Load>>();
        MultiShuttleATCInfo multiShuttleATCInfo;
        //private Func<ObservableCollection<ElevatorTask>, ElevatorTask, ElevatorTask> elevatorPriority;
        
        public MHEControl_MultiShuttle(MultiShuttleATCInfo info, MultiShuttle multishuttle)
        {
            Info = info;  // set this to save properties 
            multiShuttleATCInfo = info;
            theMultishuttle = multishuttle;
            mheController_Multishuttle = ((MHEController_Multishuttle)theMultishuttle.Controller);

            multishuttle.AutoNewElevatorTask = false;  // Let this controller decide when a new task is created call Elevator.GetNewElevatorTask to assign the next task if there is one

            //Shuttle
            theMultishuttle.OnArrivedAtShuttle             += theMultishuttle_OnArrivedAtShuttle;
                                                          
            //Bin Location                                
            theMultishuttle.OnArrivedAtRackLocation        += theMultishuttle_OnArrivedAtRackLocation;
                                                          
            //InfeedRack                                  
            theMultishuttle.OnArrivedAtInfeedRackConvPosA  += theMultishuttle_OnArrivedAtInfeedRackConvPosA;
            theMultishuttle.OnArrivedAtInfeedRackConvPosB  += theMultishuttle_OnArrivedAtInfeedRackConvPosB;

            //OutfeedRack
            theMultishuttle.OnArrivedAtOutfeedRackConvPosA += theMultishuttle_OnArrivedAtOutfeedRackConvPosA;
            theMultishuttle.OnArrivedAtOutfeedRackConvPosB += theMultishuttle_OnArrivedAtOutfeedRackConvPosB;

            //Elevator Conv
            theMultishuttle.OnArrivedAtElevatorConvPosA    += theMultishuttle_OnArrivedAtElevatorConvPosA;
            theMultishuttle.OnArrivedAtElevatorConvPosB    += theMultishuttle_OnArrivedAtElevatorConvPosB;
            theMultishuttle.OnElevatorTasksStatusChanged += TheMultishuttle_OnElevatorTasksStatusChanged;

            //DropStation
            theMultishuttle.OnArrivedAtDropStationConvPosA += theMultishuttle_OnArrivedAtDropStationConvPosA;
            theMultishuttle.OnArrivedAtDropStationConvPosB += theMultishuttle_OnArrivedAtDropStationConvPosB;
            theMultishuttle.OnDropStationConvClear += TheMultishuttle_OnDropStationConvClear;

            //PickStation
            theMultishuttle.OnArrivedAtPickStationConvPosA += theMultishuttle_OnArrivedAtPickStationConvPosA;
            theMultishuttle.OnArrivedAtPickStationConvPosB += theMultishuttle_OnArrivedAtPickStationConvPosB;
        }

        private void TheMultishuttle_OnElevatorTasksStatusChanged(object sender, ElevatorTasksStatusChangedEventArgs e)
        {
        }

        private void TheMultishuttle_OnDropStationConvClear(object sender, DropStationConvClearEventArgs e)
        {
        }

        void theMultishuttle_OnArrivedAtPickStationConvPosA(object sender, PickDropStationArrivalEventArgs e)
        {

            IATCCaseLoadType caseload = (IATCCaseLoadType)(e._caseLoad);
            caseload.Location = FormatPickDropLocation(e._locationName, ConveyorTypes.Pick); //Update the location
            caseload.Destination = caseload.Location;

            string tlg = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.TransportRequestTelegram, caseload);

            string[] tlgSplit = tlg.Split(',');
            tlgSplit.SetFieldValue(TelegramFields.stateCode, "OK");
            caseload.UserData = string.Join(",", tlgSplit); //putting it in user data alows easer message creation for the ATC multipal messages , the load reference is held on the conveyor see below

            var loc = theMultishuttle.ConveyorLocations.Find(x => x.LocName == e._locationName);
            var conv = loc.Parent.Parent.Parent as PickStationConveyor;

            if (e._numberOfLoads == 2)
            {
                string body1 = (string)((IATCCaseLoadType)conv.UserData).UserData; //Grab the already created message from the load using the conveyor load reference created below
                string body2 = (string)(caseload.UserData);
                string sendTelegram = Telegrams.CreateMultipalMessage(new List<string>() { body1, body2 }, TelegramTypes.MultipleTransportRequestTelegram, mheController_Multishuttle.Name);
                mheController_Multishuttle.SendTelegram(sendTelegram, true);
            }
            else //save the load reference so that if a second load arrives multipal telegram construction is easier
            {
                conv.UserData = caseload; //save case load to userdata for easier multipal message creation i.e. when e._numberOfLoads == 2
            }
        }

        /// <summary>
        /// if there are 2 loads on the pick station then this would be in theMultishuttle_OnArrivedAtPickStationConvPosA event
        /// </summary>
        void theMultishuttle_OnArrivedAtPickStationConvPosB(object sender, PickDropStationArrivalEventArgs e)
        {
            mheController_Multishuttle.SendTelegram((string)e._caseLoad.UserData, true); //telegram already created when at location A 
        }

        /// <summary>
        /// Creates a PS or a DS locaion name from an ATC location
        /// </summary>
        /// <param name="locationName"></param>
        /// <param name="PorD"></param>
        /// <returns></returns>
        private string FormatPickDropLocation(string locationName, ConveyorTypes PorD)
        {
            return string.Format("MSAI{0}C{1}{2}{3}S10", locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                         (char)locationName.Side(),
                                                         locationName.Level(),
                                                         (char)PorD);
        }

        private string FormatRackConvLocation(string locationName, ConveyorTypes IorO)
        {
            return string.Format("MSAI{0}L{1}{2}R{3}10", locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                         (char)locationName.Side(),
                                                         locationName.Level(),
                                                         (char)IorO);
        }

        void theMultishuttle_OnArrivedAtInfeedRackConvPosB(object sender, RackConveyorArrivalEventArgs e)
        {
            ShuttleTask st = new ShuttleTask();
            string destination = ((IATCCaseLoadType)e._caseLoad).Destination;

            if (destination.LocationType() == LocationTypes.RackConv)
            {
                IATCCaseLoadType atcLoad = e._caseLoad as IATCCaseLoadType;
                atcLoad.Location = atcLoad.Destination;
                string sendTelegram = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.MultishuttleTransportFinishedTelegram, atcLoad);
                mheController_Multishuttle.SendTelegram(sendTelegram, true);

                return;
            }

            if (destination.LocationType() == LocationTypes.DS)
            {
                //Need to create a RI to RO task to the shuttle
                int level;
                if (!int.TryParse(e._locationName.Substring(3,2), out level))
                {
                    Log.Write(string.Format("Multishuttle Control {0} Error: Error arriving at infeed raCK conveyor when trying to find level", ParentAssembly.Name));
                    return; //Error!
                }

                st.Destination = MHEController_Multishuttle.DCILocToMultiShuttleConvLoc(level, destination);
                st.Level = level;
                st.LoadID = e._caseLoad.Identification;
                st.Source = e._locationName;

                theMultishuttle.shuttlecars[level].ShuttleTasks.Add(st);

            }
            else if (destination.LocationType() != LocationTypes.BinLocation)
            {
                Log.Write("WARNING: Arrived at infeed rack and destination is NOT a binlocation.", Color.Red);
                return;
            }
            else
            {
                int level;
                st.Destination = mheController_Multishuttle.DCIbinLocToMultiShuttleLoc(destination, out level, theMultishuttle);
                st.Level = level;
                st.LoadID = e._caseLoad.Identification;
                st.Source = e._locationName;

                theMultishuttle.shuttlecars[level].ShuttleTasks.Add(st);
            }
        }

        void theMultishuttle_OnArrivedAtInfeedRackConvPosA(object sender, RackConveyorArrivalEventArgs e)
        {
            if (e._elevator.ElevatorConveyor.Route.Loads.Count == 0)
            {
                e._elevator.SetNewElevatorTask();
            }
        }

        void theMultishuttle_OnArrivedAtElevatorConvPosA(object sender, ArrivedOnElevatorEventArgs e)
        {
            if (e._loadB == null) //Only send the arrival for the load once when it arrves at position A - Do not send again if a double pick is detected (LoadB is populated)
            {
                IATCCaseLoadType load = (IATCCaseLoadType)e._loadA;
                load.Location = string.Format("MSAI{0}E{1}01LO00", e._elevator.AisleNumber.ToString().PadLeft(2, '0'), (char)e._elevator.Side);
                string sendMessage = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.LocationArrivedTelegram, load);
                mheController_Multishuttle.SendTelegram(sendMessage, true);

                if (((ElevatorTask)e._task).NumberOfLoadsInTask == 1 && ((ElevatorTask)e._task).Flow == TaskType.Outfeed)
                {
                    ((ElevatorTask)e._task).OptimiseTask(e._elevator);
                }
            }
        }

        void theMultishuttle_OnArrivedAtElevatorConvPosB(object sender, ArrivedOnElevatorEventArgs e)
        {
            if (e._elevator.CurrentTask.SourceLoadB.ConvType() == ConveyorTypes.Pick && e._elevator.CurrentTask.DestinationLoadB.ConvType() == ConveyorTypes.Drop)
            {
                //Deals with a sinlge or double load from PS to DS
                e._elevator.ElevatorConveyor.Route.Motor.Backward();
                e._elevator.CurrentTask.Flow = TaskType.Outfeed;
                e._elevator.ElevatorConveyor.UnLoading = true;
            }
        }

        void theMultishuttle_OnArrivedAtDropStationConvPosA(object sender, PickDropStationArrivalEventArgs e)
        {
            IATCCaseLoadType load = (IATCCaseLoadType)e._caseLoad;

            load.Location = load.Destination;
            load.UserData = null;

            string sendMessage;
            sendMessage = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.MultishuttleTransportFinishedTelegram, load);
            mheController_Multishuttle.SendTelegram(sendMessage, true);

            if (e._elevator.CurrentTask != null && e._elevator.CurrentTask.RelevantElevatorTask(e._caseLoad))
            {
                e._elevator.SetNewElevatorTask();
            }
        }

        void theMultishuttle_OnArrivedAtDropStationConvPosB(object sender, PickDropStationArrivalEventArgs e)
        {
            IATCCaseLoadType load = (IATCCaseLoadType)e._caseLoad;

            if (e._numberOfLoads == 2 && e._elevator.ElevatorConveyor.Route.Loads.Count == 0) // Double dropoff received
            {
                e._elevator.SetNewElevatorTask();
            }
        }

        void theMultishuttle_OnArrivedAtOutfeedRackConvPosA(object sender, RackConveyorArrivalEventArgs e)
        { 
            IATCCaseLoadType caseload = (IATCCaseLoadType)(e._caseLoad);
            caseload.Location    = FormatRackConvLocation(e._locationName, ConveyorTypes.OutfeedRack); //Update the location

            if (BaseATCController.GetLocFields(caseload.Destination, PSDSRackLocFields.ConvType) == "D")
            {
                string sendMessage = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.LocationArrivedTelegram, caseload);
                mheController_Multishuttle.SendTelegram(sendMessage, true); //Always Send a notification at location A

                //Combine a task with an existing task but only if the final destination is a drop station because if destination is the rack conveyor then we need to wait for another message from the WMS
                if (e._elevator.CurrentTask != null && e._rackConveyor.LocationB.Active && !e._elevator.CurrentTask.RelevantElevatorTask(e._rackConveyor.LocationB.ActiveLoad))
                {
                    string aisle = e._elevator.AisleNumber.ToString().PadLeft(2, '0');
                    string level = BaseATCController.GetLocFields(((IATCCaseLoadType)e._caseLoad).Destination, PSDSRackLocFields.Level);
                    string destLoadA = string.Format("{0}{1}{2}{3}A", aisle, (char)e._locationName.Side(), level, (char)ConveyorTypes.Drop);

                    //Create the task but do not add it to elevator tasks list
                    ElevatorTask et = new ElevatorTask(e._caseLoad.Identification, null)
                    {
                        SourceLoadA = e._locationName,
                        DestinationLoadA = destLoadA,
                        SourceLoadAConv = theMultishuttle.GetConveyorFromLocName(e._locationName),
                        DestinationLoadAConv = theMultishuttle.GetConveyorFromLocName(destLoadA),
                        Elevator = e._elevator
                    };

                    //This task should just be added and not combined as the combining is now done later
                    //TODO: Make it so that direct outfeed loads still work
                    et.CreateNewDoubleLoadCycleTask(et, e._rackConveyor.LocationB.ActiveLoad.Identification);
                }
            }
            else
            {
                string sendMessage = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.MultishuttleTransportFinishedTelegram, caseload);
                mheController_Multishuttle.SendTelegram(sendMessage, true); //Always Send a notification at location A
            }
        }

        void theMultishuttle_OnArrivedAtOutfeedRackConvPosB(object sender, RackConveyorArrivalEventArgs e)
        {
            //e._caseLoad.Stop();
            IATCCaseLoadType atcLoad = e._caseLoad as IATCCaseLoadType;

            //If destination is a drop station ("D") there will be no new StartTransportTelegram so continue and create a elevator task
            if (BaseATCController.GetLocFields(atcLoad.Destination, PSDSRackLocFields.ConvType) == "D" ) 
            {
                if (e._elevator.CurrentTask == null || (e._elevator.CurrentTask != null && !e._elevator.CurrentTask.RelevantElevatorTask(e._caseLoad)))
                {
                    string level = BaseATCController.GetPSDSLocFields(((IATCCaseLoadType)e._caseLoad).Destination, PSDSRackLocFields.Level);

                    // create an elevator task            
                    string aisle = e._locationName.AisleNumber().ToString().PadLeft(2, '0');
                    char side = (char)e._locationName.Side();

                    ElevatorTask et = new ElevatorTask(null, e._caseLoad.Identification)
                    {
                        SourceLoadB = e._locationName,
                        DestinationLoadB = string.Format("{0}{1}{2}{3}A", aisle, side, level, (char)ConveyorTypes.Drop), // aasyyxz: a=aisle, s = side, yy = level, x = input or output, Z = loc A or B e.g. 01R05OA 
                        DropIndexLoadB = atcLoad.DropIndex,
                        LoadCycle = Cycle.Single,
                        UnloadCycle = Cycle.Single,
                        Flow = TaskType.Outfeed
                    };

                    string elevatorName = string.Format("{0}{1}", side, aisle);
                    theMultishuttle.elevators.First(x => x.ElevatorName == elevatorName).ElevatorTasks.Add(et);
                }
            }
        }

        void theMultishuttle_OnArrivedAtRackLocation(object sender, TaskEventArgs e)
        {
            IATCCaseLoadType ATCload = (IATCCaseLoadType)e._load;
            string sendTelegram = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.MultishuttleTransportFinishedTelegram, ATCload);
            string[] sendTelegramSplit = sendTelegram.Split(',');
            sendTelegramSplit.SetFieldValue(TelegramFields.location, ATCload.Destination);
            sendTelegramSplit.SetFieldValue(TelegramFields.stateCode, ATCload.PresetStateCode);
            mheController_Multishuttle.SendTelegram(string.Join(",", sendTelegramSplit), true);
        }

        void theMultishuttle_OnArrivedAtShuttle(object sender, TaskEventArgs e)
        {

            IATCCaseLoadType load = (IATCCaseLoadType)e._load;
            //load.Location = string.Format("MSAI{0}LV{1}SH01", BaseATCController.GetRackLocFields(load.Destination, PSDSRackLocFields.Aisle), BaseATCController.GetRackLocFields(load.Destination, PSDSRackLocFields.Level));
            string aisle = BaseATCController.GetBinLocField(load.Source, BinLocFields.Aisle);
            if (aisle == "" && ((ShuttleTask)e._task).Source.Length > 2) //If you cannot get the aisle from the bin location, then get from the task instead
            {
                string checkAisle = ((ShuttleTask)e._task).Source.Substring(0, 2);
                int result;
                if (int.TryParse(checkAisle, out result))
                {
                    aisle = checkAisle;
                }
            }
            //load.Location = string.Format("MSAI{0}LV{1}SH01", BaseATCController.GetBinLocField(load.Source, BinLocFields.Aisle), ((ShuttleTask)e._task).Level.ToString().PadLeft(2, '0'));

            load.Location = string.Format("MSAI{0}LV{1}SH01", aisle, ((ShuttleTask)e._task).Level.ToString().PadLeft(2, '0'));

            string sendMessage = mheController_Multishuttle.CreateTelegramFromLoad(TelegramTypes.LocationArrivedTelegram, load);
            mheController_Multishuttle.SendTelegram(sendMessage, true);
        }


        private string GetLoadIDFromCaseData(ATCCaseData cData)
        {
            if (cData != null)
            {
                return cData.TUIdent;
            }
            return string.Empty;
        }

        /// <summary>
        /// Takes the x location from a location and creates the absolute x location that the multishuttle assembly needs.
        /// returns it as a string
        /// </summary>
        /// <param name="xBayLoc"></param>
        /// <param name="bayPosition"></param>
        /// <returns> x location formatted to 3 characters </returns>
        public string XLocConverter(string xBayLoc, string bayPosition)
        {
            int ixxx, iBayPos;
            int.TryParse(xBayLoc, out ixxx);
            int.TryParse(bayPosition, out iBayPos);

            int xLoc = ((ixxx-1)* LocationsPerBay) + iBayPos;

            if (xLoc <= theMultishuttle.RackLocations + theMultishuttle.PSDSlocations)
            {
                return xLoc.ToString().PadLeft(3,'0');
            }
            else
            {
                Log.Write("ERRROR: X location " + xLoc + " is outseide bounds of the multishuttle.", Color.Red);
            }
            return "000";
        }

        [Category("Rack Config")]
        [DisplayName("Number Of Bays")]
        [DescriptionAttribute("The number of bays in the rack.")]
        public int NumOfBays
        {
            get { return multiShuttleATCInfo.numOfBays;}
            set
            {
                multiShuttleATCInfo.numOfBays = value;
            }
        }

        [Category("Rack Config")]
        [DisplayName("Locations Per Bay")]
        [DescriptionAttribute("The number of locations per bay, assumes that this is an equal number")]
        public int LocationsPerBay
        {
            get { return multiShuttleATCInfo.locationsPerBay; }
            set
            {
                multiShuttleATCInfo.locationsPerBay = value;
            }
        }



        public override void Dispose()
        {
            theMultishuttle.OnArrivedAtShuttle -= theMultishuttle_OnArrivedAtShuttle;

            //Bin Location                                
            theMultishuttle.OnArrivedAtRackLocation -= theMultishuttle_OnArrivedAtRackLocation;

            //InfeedRack                                  
            theMultishuttle.OnArrivedAtInfeedRackConvPosA -= theMultishuttle_OnArrivedAtInfeedRackConvPosA;
            theMultishuttle.OnArrivedAtInfeedRackConvPosB -= theMultishuttle_OnArrivedAtInfeedRackConvPosB;

            //OutfeedRack
            theMultishuttle.OnArrivedAtOutfeedRackConvPosA -= theMultishuttle_OnArrivedAtOutfeedRackConvPosA;
            theMultishuttle.OnArrivedAtOutfeedRackConvPosB -= theMultishuttle_OnArrivedAtOutfeedRackConvPosB;

            //Elevator Conv
            theMultishuttle.OnArrivedAtElevatorConvPosA -= theMultishuttle_OnArrivedAtElevatorConvPosA;
            theMultishuttle.OnArrivedAtElevatorConvPosB -= theMultishuttle_OnArrivedAtElevatorConvPosB;

            //DropStation
            theMultishuttle.OnArrivedAtDropStationConvPosA -= theMultishuttle_OnArrivedAtDropStationConvPosA;
            theMultishuttle.OnArrivedAtDropStationConvPosB -= theMultishuttle_OnArrivedAtDropStationConvPosB;

            //PickStation
            theMultishuttle.OnArrivedAtPickStationConvPosA -= theMultishuttle_OnArrivedAtPickStationConvPosA;
            theMultishuttle.OnArrivedAtPickStationConvPosB -= theMultishuttle_OnArrivedAtPickStationConvPosB;
        }
    }

    [Serializable]
    [XmlInclude(typeof(MultiShuttleATCInfo))]
    public class MultiShuttleATCInfo : ProtocolInfo
    {
        public int numOfBays;
        public int locationsPerBay;
    }
}
