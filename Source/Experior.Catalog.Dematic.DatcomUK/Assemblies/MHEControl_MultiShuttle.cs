using Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies;
using Experior.Dematic.Base;
using Experior.Dematic;
using System;
using System.Drawing;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using Experior.Core.Loads;

namespace Experior.Catalog.Dematic.DatcomUK.Assemblies
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
        string elevatorGroup = "F";

        public MHEControl_MultiShuttle(MultiShuttleDatcomInfo info, MultiShuttle multishuttle)
        {
            Info = info;  // set this to save properties 
            theMultishuttle = multishuttle;
            mheController_Multishuttle = ((MHEController_Multishuttle)theMultishuttle.Controller);
            multishuttle.AutoNewElevatorTask = false;  // Let this controller decide when a new task is created call Elevator.GetNewElevatorTask to assign the next task if there is one

            

            //theMultishuttle.OnLoadTransferingToPickStation += theMultishuttle_OnLoadTransferingToPickStation;

            //Shuttle
            theMultishuttle.OnArrivedAtShuttle             += theMultishuttle_OnArrivedAtShuttle;

            //Bin Location 
            theMultishuttle.OnArrivedAtRackLocation        += theMultishuttle_OnArrivedAtRackLocation;

            //OutfeedRack
            theMultishuttle.OnArrivedAtOutfeedRackConvPosA += theMultishuttle_OnArrivedAtOutfeedRackConvPosA;
            theMultishuttle.OnArrivedAtOutfeedRackConvPosB += theMultishuttle_OnArrivedAtOutfeedRackConvPosB;

            //Elevator Conv
            theMultishuttle.OnArrivedAtElevatorConvPosA    += theMultishuttle_OnArrivedAtElevatorConvPosA;
            theMultishuttle.OnArrivedAtElevatorConvPosB    += theMultishuttle_OnArrivedAtElevatorConvPosB;

            //DropStation
            theMultishuttle.OnArrivedAtDropStationConvPosA += theMultishuttle_OnArrivedAtDropStationConvPosA;
            theMultishuttle.OnArrivedAtDropStationConvPosB += theMultishuttle_OnArrivedAtDropStationConvPosB;

            //PickStation
            theMultishuttle.OnArrivedAtPickStationConvPosA += theMultishuttle_OnArrivedAtPickStationConvPosA;
            theMultishuttle.OnArrivedAtPickStationConvPosB += theMultishuttle_OnArrivedAtPickStationConvPosB;

            //InfeedRack
            theMultishuttle.OnArrivedAtInfeedRackConvPosA  += theMultishuttle_OnArrivedAtInfeedRackConvPosA;
            theMultishuttle.OnArrivedAtInfeedRackConvPosB  += theMultishuttle_OnArrivedAtInfeedRackConvPosB;          
        }
                
        public void Telegram20Received(string[] fields, ushort blocks)
        {
            var elev = theMultishuttle.elevators.First(x => (x.ElevatorName.Substring(0, 1) == fields[7].Datcom_Side() && x.ElevatorName.Substring(1, 2) == fields[7].Datcom_Aisle()));
           // var elev = theMultishuttle.elevators.First(x => (x.Key.Substring(0, 1) == fields[7].Datcom_Side() && x.Key.Substring(1, 2) == fields[7].Datcom_Aisle()));
           // elev.Value.GetNewElevatorTask();

            //    if(fields[10] == CurrentLeftElevatorTask.LoadA_ID )
            //    {  
            //        CurrentLeftElevatorTask.LoadA_ID = string.Empty;
            //    }

            //    if(fields[10] == CurrentLeftElevatorTask.LoadB_ID)
            //    {
            //        CurrentLeftElevatorTask.LoadB_ID = string.Empty;
            //    }

            //    if(CurrentLeftElevatorTask.LoadA_ID == string.Empty && CurrentLeftElevatorTask.LoadB_ID == string.Empty)
            //    {
            //        CurrentLeftElevatorTask = null;
            //      //  CurrentLeftElevatorTask.Elevator.GetNextElevatorTask();
            //    }
            //}
        }

        void theMultishuttle_OnArrivedAtPickStationConvPosA(object sender, PickDropStationArrivalEventArgs e)
        {
            if (e._numberOfLoads == 2)
            {
                var loc = theMultishuttle.ConveyorLocations.Find(x => x.LocName == e._locationName);
                var conv = loc.Parent.Parent.Parent as PickStationConveyor;
                var loadB = conv.TransportSection.Route.Loads.ToList().Find(x => ((Case_Load)x).SSCCBarcode != e._caseLoad.SSCCBarcode);
                string body = mheController_Multishuttle.CreatePickStationDataSetBody((Case_Load)loadB, e._caseLoad);
                mheController_Multishuttle.SendTelegram("25", body, 1);
            }
        }

        void theMultishuttle_OnArrivedAtInfeedRackConvPosB(object sender, RackConveyorArrivalEventArgs e)
        {

            if(e._rackConveyor.TransportSection.Route.Loads.Count == 1)
            {
                ((CaseData)((Case_Load)e._caseLoad).Case_Data).CurrentPosition = string.Format("I{0}{1}{2}002{3}",
                                                                                 e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                                 theMultishuttle.ElevatorGroup(e._locationName),
                                                                                 (char)e._locationName.Side(),
                                                                                 e._locationName.Level());

                string body = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)(((Case_Load)e._caseLoad).Case_Data));
                mheController_Multishuttle.SendTelegram("02", body, 1);
            }
            string bodyB = string.Empty;
            
            if (e._rackConveyor.TransportSection.Route.Loads.Count == 2)
            {
                ((CaseData)((Case_Load)e._caseLoad).Case_Data).CurrentPosition = string.Format("I{0}{1}{2}002{3}",
                                                                                 e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                                 theMultishuttle.ElevatorGroup(e._locationName),
                                                                                 (char)e._locationName.Side(),
                                                                                 e._locationName.Level());

                bodyB = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)e._caseLoad.Case_Data);



                var loadA = e._rackConveyor.TransportSection.Route.Loads.ToList().Find(x => ((Case_Load)x).SSCCBarcode != e._caseLoad.SSCCBarcode);

                ((CaseData)((Case_Load)loadA).Case_Data).CurrentPosition = string.Format("I{0}{1}{2}001{3}",
                                                                           e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                           elevatorGroup,
                                                                           (char)e._locationName.Side(),
                                                                           e._locationName.Level());

                string bodyA = mheController_Multishuttle.CreateMissionDataSetBody(((CaseData)((Case_Load)loadA).Case_Data));
                mheController_Multishuttle.SendTelegram("02", bodyA + "," + bodyB, 2);
                TryGetNewElevatorTask(e._elevator.CurrentTask, e._caseLoad, (Case_Load)loadA);
            }
        }

        void theMultishuttle_OnArrivedAtInfeedRackConvPosA(object sender, RackConveyorArrivalEventArgs e)
        {
            ((CaseData)((Case_Load)e._caseLoad).Case_Data).CurrentPosition = string.Format("I{0}{1}{2}001{3}",
                                                                             e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                             theMultishuttle.ElevatorGroup(e._locationName),
                                                                             (char)e._locationName.Side(),
                                                                             e._locationName.Level());
            string bodyB = string.Empty;
            bodyB = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)e._caseLoad.Case_Data);

            //if(e._UnloadCycle != null && e._UnloadCycle == Cycle.Single)
            if(e._elevator.CurrentTask.UnloadCycle == Cycle.Single)
            {                
                mheController_Multishuttle.SendTelegram("02", bodyB, 1);
                TryGetNewElevatorTask(e._elevator.CurrentTask, e._caseLoad);
            }

        }

        /// <summary>
        /// After sending an 02 arrival then try to get a new elevator task but only if the loads that the 02 was sent is relevant to the current task
        /// </summary>
        private void TryGetNewElevatorTask(ElevatorTask task, Case_Load load1, Case_Load load2 = null)
        {
            if (task.Elevator.ElevatorConveyor.Route.Loads.Count == 0)  //Only get a new task if the evlevator is empty
            {
                if (task.Elevator.ElevatorConveyor.Route.Loads.Count == 0 && ((load1 != null && task.RelevantElevatorTask(load1)) || (load2 != null && task.RelevantElevatorTask(load2))))
                {
                    Func<ObservableCollection<ElevatorTask>, ElevatorTask, ElevatorTask> elevatorPriority = (tasks, lastTask) =>
                    {
                        if (tasks.Any())
                        {
                            return tasks.First();
                        }
                        else
                        {
                            return null;
                        }
                    };

                    //task.Elevator.GetNewElevatorTask((tasks) => tasks.Any() ? tasks.First() : null));
                    // TODO [CN] This method was removed in a previous release, however as Multishuttle is not needed for final project commenting this out to fix build issues
                    //task.Elevator.GetNewElevatorTask(elevatorPriority);
                }
            }
        }

        /// <summary>
        /// if there are 2 loads on the pick station then this would be in theMultishuttle_OnArrivedAtPickStationConvPosA event
        /// </summary>
        void theMultishuttle_OnArrivedAtPickStationConvPosB(object sender, PickDropStationArrivalEventArgs e)
        {
            string body = mheController_Multishuttle.CreatePickStationDataSetBody(e._caseLoad, null);
            mheController_Multishuttle.SendTelegram("25", body, 1);
        }

        void theMultishuttle_OnArrivedAtElevatorConvPosA(object sender, ArrivedOnElevatorEventArgs e)
        {
            if (e._elevator.CurrentTask.LoadCycle == Cycle.Single && e._elevator.ElevatorConveyor.Route.Loads.Count == 2 && e._elevator.CurrentTask.Flow == TaskType.Outfeed)
            {
                ((CaseData)e._loadA.Case_Data).CurrentPosition = string.Format("E{0}{1}{2}001  ",
                                                                 e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                 theMultishuttle.ElevatorGroup(e._locationName),
                                                                 (char)e._locationName.Side());

                string bodyA = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)e._loadA.Case_Data);
                mheController_Multishuttle.SendTelegram("02", bodyA, 1);   
            }
            //else if (e._loadA != null && e._loadB != null)
            //else if (e._elevator.CurrentTask.LoadCycle == Cycle.Double && e._elevator.ElevatorConveyor.Route.Loads.Count == 2)
            else if (e._elevator.ElevatorConveyor.LocationA.Active && e._elevator.ElevatorConveyor.LocationB.Active)
            {
                Case_Load loadA,loadB;
                loadA = e._loadA;
                loadB = Case_Load.GetCaseFromIdentification(e._elevator.CurrentTask.LoadB_ID);

                string posA = "001", posB = "002";
                if (e._elevator.CurrentTask.Flow == TaskType.Outfeed)
                {
                    posA = "002";
                    posB = "001";
                }
                ((CaseData)loadA.Case_Data).CurrentPosition = string.Format("E{0}{1}{2}{3}  ",
                                                                 e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                 theMultishuttle.ElevatorGroup(e._locationName),
                                                                 (char)e._locationName.Side(),
                                                                 posA);

                ((CaseData)loadB.Case_Data).CurrentPosition = string.Format("E{0}{1}{2}{3}  ",
                                                                 e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                 theMultishuttle.ElevatorGroup(e._locationName),
                                                                 (char)e._locationName.Side(),
                                                                 posB);

                string bodyA = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)loadA.Case_Data);
                string bodyB = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)loadB.Case_Data);
                mheController_Multishuttle.SendTelegram("02", bodyA +","+ bodyB, 2);   
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

                if (((ElevatorTask)e._task).NumberOfLoadsInTask == 1)
                {
                    SendElevatorArrivalMessage(e);
                }
            }
            else  if(e._elevator.CurrentTask.LoadCycle == Cycle.Single && e._elevator.CurrentTask.NumberOfLoadsInTask == 1)
            {
                SendElevatorArrivalMessage(e);
            }
        }


        /// <summary>
        /// Used in theMultishuttle_OnArrivedAtElevatorConvPosB to create the arrival on the elevator message 
        /// </summary>
        /// <param name="e"> The ArrivedOnElevatorEventArgs from the theMultishuttle_OnArrivedAtElevatorConvPosB event</param>
        private void SendElevatorArrivalMessage(ArrivedOnElevatorEventArgs e)
        {
            ((CaseData)((Case_Load)e._loadB).Case_Data).CurrentPosition = string.Format("E{0}{1}{2}001  ",
                                                                                        ((ElevatorTask)e._task).SourceLoadB.AisleNumber().ToString().PadLeft(2, '0'),
                                                                                        theMultishuttle.ElevatorGroup(e._locationName),
                                                                                        ((char)((ElevatorTask)e._task).SourceLoadB.Side()));

            string body = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)(((Case_Load)e._loadB).Case_Data));
            mheController_Multishuttle.SendTelegram("02", body, 1);
        }


        void theMultishuttle_OnArrivedAtDropStationConvPosA(object sender, PickDropStationArrivalEventArgs e)
        {
            if (e._elevator.CurrentTask != null && e._elevator.CurrentTask.RelevantElevatorTask(e._caseLoad) && e._elevator.CurrentTask.UnloadCycle == Cycle.Single) 
            { 
                ((CaseData)e._caseLoad.Case_Data).CurrentPosition = string.Format("D{0}{1}{2}002{3}",
                                                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                    theMultishuttle.ElevatorGroup(e._locationName),
                                                                    (char)e._locationName.Side(),
                                                                    e._locationName.Level());

                string body = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)e._caseLoad.Case_Data);
                mheController_Multishuttle.SendTelegram("02", body, 1);
                TryGetNewElevatorTask(e._elevator.CurrentTask, e._caseLoad);
            }
        }

        void theMultishuttle_OnArrivedAtDropStationConvPosB(object sender, PickDropStationArrivalEventArgs e)
        {
            if (e._numberOfLoads == 2 && e._elevator.ElevatorConveyor.Route.Loads.Count == 0)
            {
                string[] ulIDs = ((CaseData)e._caseLoad.Case_Data).UserData.Split(',' );
                Case_Load loadA = Case_Load.GetCaseFromIdentification(ulIDs[0]);
                Case_Load loadB = Case_Load.GetCaseFromIdentification(ulIDs[1]);

                ((CaseData)loadA.Case_Data).CurrentPosition = string.Format("D{0}{1}{2}002{3}",
                                                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                    theMultishuttle.ElevatorGroup(e._locationName),
                                                                    (char)e._locationName.Side(),
                                                                    e._locationName.Level());

                ((CaseData)loadB.Case_Data).CurrentPosition = string.Format("D{0}{1}{2}001{3}",
                                                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                    theMultishuttle.ElevatorGroup(e._locationName),
                                                                    (char)e._locationName.Side(),
                                                                    e._locationName.Level());

                string bodyA = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)loadA.Case_Data);
                string bodyB = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)loadB.Case_Data);
                mheController_Multishuttle.SendTelegram("02", bodyB + "," + bodyA, 2);
                TryGetNewElevatorTask(e._elevator.CurrentTask, loadA, loadB);
            }
        }

        void theMultishuttle_OnArrivedAtOutfeedRackConvPosA(object sender, RackConveyorArrivalEventArgs e)
        {            
            ((CaseData)e._caseLoad.Case_Data).CurrentPosition = string.Format("O{0}{1}{2}002{3}", 
                                                                e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                theMultishuttle.ElevatorGroup(e._locationName),
                                                                (char)e._locationName.Side(), 
                                                                e._locationName.Level());

            string body = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)e._caseLoad.Case_Data);
            mheController_Multishuttle.SendTelegram("02", body, 1);   
        }

        void theMultishuttle_OnArrivedAtOutfeedRackConvPosB(object sender, RackConveyorArrivalEventArgs e)
        {
            if (e._elevator.CurrentTask != null && e._elevator.CurrentTask.RelevantElevatorTask(e._caseLoad) && e._elevator.CurrentTask.LoadCycle == Cycle.Double)
            {
                //do nothing
            }
            else
            {
                ((CaseData)e._caseLoad.Case_Data).CurrentPosition = string.Format("O{0}{1}{2}001{3}",
                                                                    e._locationName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                    theMultishuttle.ElevatorGroup(e._locationName),
                                                                    (char)e._locationName.Side(), 
                                                                    e._locationName.Level());

                string body = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)e._caseLoad.Case_Data);
                mheController_Multishuttle.SendTelegram("02", body, 1);  
            }
        }

        void theMultishuttle_OnArrivedAtRackLocation(object sender, TaskEventArgs e)
        {
            ((CaseData)((Case_Load)e._load).Case_Data).CurrentPosition = ((CaseData)((Case_Load)e._load).Case_Data).DestinationPosition;
            string body = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)(((Case_Load)e._load).Case_Data));
            mheController_Multishuttle.SendTelegram("02", body, 1);   
        }

        void theMultishuttle_OnArrivedAtShuttle(object sender, TaskEventArgs e)
        {
            ((CaseData)((Case_Load)e._load).Case_Data).CurrentPosition = "S" + ((MultiShuttle)sender).AisleNumber.ToString().PadLeft(2, '0') + "     " + ((ShuttleTask)e._task).Destination.Level();
            string body = mheController_Multishuttle.CreateMissionDataSetBody((CaseData)(((Case_Load)e._load).Case_Data));
            mheController_Multishuttle.SendTelegram("02", body, 1);   
        }

        /// Rack Location for an ElevatorTask takes the form: aasyyxz: a=aisle, s = side, y = level, x = input or output, Z = loc A or B e.g. 01R05OA
        /// Source location for a shuttleTask takes the form: sxxxyydd: Side, xxx location, yy = level, dd = depth
        public bool CreateShuttleTask(string origin, string dest, CaseData cData, ShuttleTaskTypes taskType)
        {
            ShuttleTask sT = new ShuttleTask();
            
            if (taskType == ShuttleTaskTypes.RackToConv)
            {
                int l = 0;
                int.TryParse(origin.Datcom_Y_Vertical(), out l);
                sT.Level = l;

                sT.Source = string.Format("{0}{1}{2}{3}", 
                            origin.Datcom_Side(),
                            origin.Datcom_X_horizontal(),
                            origin.Datcom_Y_Vertical(), 
                            origin.Datcom_GroupOrDepth().PadLeft(2, '0'));

                sT.Destination = string.Format("{0}{1}{2}OA", 
                                 dest.Datcom_Aisle(), 
                                 dest.Datcom_Side(), 
                                 dest.Datcom_Y_Vertical());
            }
            else if (taskType == ShuttleTaskTypes.ConvToRack)
            {

                int l = 0;
                int.TryParse(origin.Datcom_Y_Vertical(), out l);
                sT.Level = l;

                sT.Source = string.Format("{0}{1}{2}IB",
                 origin.Datcom_Aisle(),
                 origin.Datcom_Side(),
                 origin.Datcom_Y_Vertical());

                sT.Destination = string.Format("{0}{1}{2}{3}",
                            dest.Datcom_Side(),
                            dest.Datcom_X_horizontal(),
                            dest.Datcom_Y_Vertical(),
                            dest.Datcom_GroupOrDepth().PadLeft(2, '0'));

                var loc = theMultishuttle.ConveyorLocations.Find(x => x.LocName == sT.Source);

                if (loc != null && loc.LocName.ConvType() == ConveyorTypes.InfeedRack && loc.Active && cData.ULID == loc.ActiveLoad.Identification)
                {
                    ((Case_Load)loc.ActiveLoad).Case_Data = cData;
                }
                else
                {
                    Log.Write(loc + " does not have an active load or the load is incorrect, ignoring message",Color.Red);  //possibly an issue with VFC!
                    return false;
                }
            }
            else if (taskType == ShuttleTaskTypes.Shuffle)
            {
                int l = 0;
                int.TryParse(origin.Datcom_Y_Vertical(), out l);
                sT.Level = l;

                sT.Source = string.Format("{0}{1}{2}{3}",
                            origin.Datcom_Side(), 
                            origin.Datcom_X_horizontal(), 
                            origin.Datcom_Y_Vertical(), 
                            origin.Datcom_GroupOrDepth().PadLeft(2, '0'));

                sT.Destination = string.Format("{0}{1}{2}{3}",
                                 dest.Datcom_Side(), 
                                 dest.Datcom_X_horizontal(), 
                                 dest.Datcom_Y_Vertical(), 
                                 dest.Datcom_GroupOrDepth().PadLeft(2, '0'));
            }

            sT.LoadID = cData.ULID;
            sT.caseData = cData;
            sT.caseData.colour = Color.Blue;

            theMultishuttle.shuttlecars[sT.Source.LevelasInt()].ShuttleTasks.Add(sT);
            
            return true;
        }

        private string GetLoadIDFromCaseData(CaseData cData)
        {
            if (cData != null)
            {
                return cData.ULID;
            }
            return string.Empty;
        }

        /// <summary>
        /// Creates a PS to DS task for the elevator
        /// Loads always travel from A to B
        /// </summary>
        /// <param name="originA">Load A origin</param>
        /// <param name="destA">Load A destination</param>
        /// <param name="cDataA">case data for load A</param>
        /// <param name="originB">Load B origin</param>
        /// <param name="destB">Load B destination</param>
        /// <param name="cDataB"> case data for load B</param>
        /// <param name="loadCycle">The load cycle type</param>
        /// <param name="unloadCycle">The unload cycle type</param>
        public void CreateElevatorTask(string originA, string destA, CaseData cDataA, string originB, string destB, CaseData cDataB, Cycle loadCycle, Cycle unloadCycle)
        {
            ElevatorTask eT = new ElevatorTask(GetLoadIDFromCaseData(cDataA), GetLoadIDFromCaseData(cDataB));

            eT.SourceLoadB = string.Format("{0}{1}{2}{3}B",
                             originB.Datcom_Aisle(),
                             originB.Datcom_Side(),
                             originB.Datcom_Y_Vertical(),
                             (char)ConveyorTypes.Pick);

            eT.DestinationLoadB = string.Format("{0}{1}{2}{3}A",
                                  destB.Datcom_Aisle(),
                                  destB.Datcom_Side(),
                                  destB.Datcom_Y_Vertical(),
                                  (char)ConveyorTypes.Drop);

            eT.caseDataB = cDataB;

            ((Case_Load)Load.Get(cDataB.ULID)).Case_Data = cDataB;

            //Should always have a set of B data but no always a set of A data.
            if (cDataA != null)
            {
                eT.SourceLoadA = string.Format("{0}{1}{2}{3}A",
                                 originA.Datcom_Aisle(),
                                 originA.Datcom_Side(),
                                 originA.Datcom_Y_Vertical(),
                                 (char)ConveyorTypes.Pick);

                eT.DestinationLoadA = string.Format("{0}{1}{2}{3}A",
                                      destA.Datcom_Aisle(),
                                      destA.Datcom_Side(),
                                      destA.Datcom_Y_Vertical(),
                                      (char)ConveyorTypes.Drop);

                eT.caseDataA = cDataA;

                ((Case_Load)Load.Get(cDataA.ULID)).Case_Data = cDataA;

            }

            eT.LoadCycle = loadCycle;
            eT.UnloadCycle = unloadCycle;
            eT.Flow = TaskType.Infeed;

            string elevatorName = string.Format("{0}{1}", destB.Datcom_Side(), destB.Datcom_Aisle());
            theMultishuttle.elevators.First(x => x.ElevatorName == elevatorName).ElevatorTasks.Add(eT);
        }

        //aasyyxz: a=aisle, s = side, y = level, x = conv type see enum ConveyorTypes, Z = loc A or B e.g. 01R05OA
        //TODO refactor this nastyness
        public void CreateElevatorTask(string originA, string destA, CaseData cDataA, string originB, string destB, CaseData cDataB, Cycle loadCycle, Cycle unloadCycle, TaskType taskType)
        {
            ElevatorTask eT = new ElevatorTask(GetLoadIDFromCaseData(cDataA), GetLoadIDFromCaseData(cDataB));
            char sourceConvType, destConvType;

            if (taskType == TaskType.Outfeed)
            {
                sourceConvType = (char)ConveyorTypes.OutfeedRack;
                destConvType = (char)ConveyorTypes.Drop;
            }
            else
            {
                sourceConvType = (char)ConveyorTypes.Pick;
                destConvType = (char)ConveyorTypes.InfeedRack;
            }

            eT.SourceLoadB = string.Format("{0}{1}{2}{3}B",
                             originB.Datcom_Aisle(),
                             originB.Datcom_Side(),
                             originB.Datcom_Y_Vertical(),
                             sourceConvType);

            eT.DestinationLoadB = string.Format("{0}{1}{2}{3}A",
                                  destB.Datcom_Aisle(),
                                  destB.Datcom_Side(),
                                  destB.Datcom_Y_Vertical(),
                                  destConvType);

           // eT.LoadB_ID = cDataB.ULID;
            eT.caseDataB = cDataB;

            var locB = theMultishuttle.ConveyorLocations.Find(x => x.LocName == eT.SourceLoadB);
            if (locB != null && locB.Active)
            {
                ((Case_Load)locB.ActiveLoad).Case_Data = cDataB; //
                locB.ActiveLoad.Identification = cDataB.ULID;
            }
            else
            {
                Log.Write(string.Format("Multishuttle {0}: Cannot create elevator task as source location does not have load", originB.Datcom_Aisle()));
            }

            if (loadCycle == Cycle.Double) //just assuming that if you have a double load cycle then you have all the rest of the informatiom to go with it
            {
                eT.SourceLoadA = string.Format("{0}{1}{2}{3}A",
                                    originA.Datcom_Aisle(),
                                    originA.Datcom_Side(),
                                    originA.Datcom_Y_Vertical(),
                                    sourceConvType);

                eT.DestinationLoadA = string.Format("{0}{1}{2}{3}B",
                                        destA.Datcom_Aisle(),
                                        destA.Datcom_Side(),
                                        destA.Datcom_Y_Vertical(),
                                        destConvType);

                //eT.LoadA_ID = cDataA.ULID;
                eT.caseDataA = cDataA;
               // Case_Load.GetCaseFromULID(cDataA.ULID).Case_Data = cDataA;

                var locA = theMultishuttle.ConveyorLocations.Find(x => x.LocName == eT.SourceLoadA);
                if (locA != null && locA.Active)
                {
                    ((Case_Load)locA.ActiveLoad).Case_Data = cDataA;
                    locA.ActiveLoad.Identification = cDataA.ULID;
                }

            }
            else if (loadCycle == Cycle.Single && unloadCycle == Cycle.Double)
            {
                eT.SourceLoadA = string.Format("{0}{1}{2}{3}B",
                                    originA.Datcom_Aisle(),
                                    originA.Datcom_Side(),
                                    originA.Datcom_Y_Vertical(),
                                    sourceConvType);

                eT.DestinationLoadA = string.Format("{0}{1}{2}{3}B",
                                        destA.Datcom_Aisle(),
                                        destA.Datcom_Side(),
                                        destA.Datcom_Y_Vertical(),
                                        destConvType);

               // eT.LoadA_ID = cDataA.ULID;
                eT.caseDataA = cDataA;
                // Case_Load.GetCaseFromULID(cDataA.ULID).Case_Data = cDataA;

                var locA = theMultishuttle.ConveyorLocations.Find(x => x.LocName == eT.SourceLoadA);
                if (locA != null && locA.Active)
                {
                    ((Case_Load)locA.ActiveLoad).Case_Data = cDataA;
                    locA.ActiveLoad.Identification = cDataA.ULID;
                }
            }
            else if (loadCycle == Cycle.Single && unloadCycle == Cycle.Single && cDataA != null && cDataB != null)
            {
                eT.SourceLoadA = string.Format("{0}{1}{2}{3}B",
                                    originA.Datcom_Aisle(),
                                    originA.Datcom_Side(),
                                    originA.Datcom_Y_Vertical(),
                                    sourceConvType);

                eT.DestinationLoadA = string.Format("{0}{1}{2}{3}B",
                                        destA.Datcom_Aisle(),
                                        destA.Datcom_Side(),
                                        destA.Datcom_Y_Vertical(),
                                        destConvType);

               // eT.LoadA_ID = cDataA.ULID;
                eT.caseDataA = cDataA;
                // Case_Load.GetCaseFromULID(cDataA.ULID).Case_Data = cDataA;

                var locA = theMultishuttle.ConveyorLocations.Find(x => x.LocName == eT.SourceLoadA);
                if (locA != null && locA.Active)
                {
                    ((Case_Load)locA.ActiveLoad).Case_Data = cDataA;
                    locA.ActiveLoad.Identification = cDataA.ULID;
                }
            }

            
            eT.LoadCycle   = loadCycle;
            eT.UnloadCycle = unloadCycle;
            eT.Flow        = taskType;

            string elevatorName = string.Format("{0}{1}", destB.Datcom_Side(), destB.Datcom_Aisle());
            theMultishuttle.elevators.First(x => x.ElevatorName == elevatorName).ElevatorTasks.Add(eT);
            //theMultishuttle.elevators[elevatorName].ElevatorTasks.Add(eT);
        }

        public override void Dispose()
        {
            theMultishuttle.OnArrivedAtShuttle -= theMultishuttle_OnArrivedAtShuttle;

            //Bin Location 
            theMultishuttle.OnArrivedAtRackLocation -= theMultishuttle_OnArrivedAtRackLocation;

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

            //InfeedRack
            theMultishuttle.OnArrivedAtInfeedRackConvPosA -= theMultishuttle_OnArrivedAtInfeedRackConvPosA;
            theMultishuttle.OnArrivedAtInfeedRackConvPosB -= theMultishuttle_OnArrivedAtInfeedRackConvPosB;
        }
    }

    [Serializable]
    [XmlInclude(typeof(MultiShuttleDatcomInfo))]
    public class MultiShuttleDatcomInfo : ProtocolInfo
    {

    }
}
