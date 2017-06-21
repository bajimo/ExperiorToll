using System;
using System.Collections.Generic;
using System.Linq;
using Experior.Dematic.Base;
using System.ComponentModel;
using Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies;
using Experior.Core.Assemblies;
using System.Drawing;
using Dematic.DCI;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.DCI.Assemblies.Storage
{
    public class MHEController_Multishuttle : BaseDCIController, IController, IMultiShuttleController
    {
        MHEController_MultishuttleDCIInfo mHEController_MultishuttleInfo;
        List<MultiShuttle> multishuttles = new List<MultiShuttle>();
        List<MHEControl> controls = new List<MHEControl>();
        MHEControl_MultiShuttle control;

        public MHEController_Multishuttle(MHEController_MultishuttleDCIInfo info) : base(info)
        {
            mHEController_MultishuttleInfo = info;
        }

        public override void HandleTelegrams(string telegram, TelegramTypes type)
        {
            if (type == TelegramTypes.StatusRequest)
            {
                StatusRequest(telegram);
            }
            else if (type == TelegramTypes.TUMission)
            {
                if (telegram.GetNumberOfBlocks(this) > 1)
                {
                    TUMissionMultiBlock(telegram);
                }
                else
                {
                    TUMissionSingleBlock(telegram);
                }
            }
            else if (type == TelegramTypes.TUMissionCancel)
            {
                HandleMissionCancel(telegram);
            }
        }

        private void HandleMissionCancel(string telegram)
        {
            //Just reply with a TUCA to acknowledge mission cancel.
            //TODO check we have a mission to cancel and then cancel it. 
            //Currently MS will clear all task before sending bin empty, bin full etc.
            string sendTelegram = Template.CreateTelegram(this, TelegramTypes.TUCancel);
            sendTelegram = sendTelegram.SetFieldValue(this, TelegramFields.EventCode, "OK");
            //Populate the field values from TUMC
            sendTelegram = sendTelegram.SetFieldValue(this, TelegramFields.Source, telegram.GetFieldValue(this, TelegramFields.Source));
            sendTelegram = sendTelegram.SetFieldValue(this, TelegramFields.Current, telegram.GetFieldValue(this, TelegramFields.Current));
            sendTelegram = sendTelegram.SetFieldValue(this, TelegramFields.Destination, telegram.GetFieldValue(this, TelegramFields.Destination));
            sendTelegram = sendTelegram.SetFieldValue(this, TelegramFields.TUIdent, telegram.GetFieldValue(this, TelegramFields.TUIdent));
            sendTelegram = sendTelegram.SetFieldValue(this, TelegramFields.TUType, telegram.GetFieldValue(this, TelegramFields.TUType));
            SendTelegram(sendTelegram);
        }

        private void StatusRequest(string telegram)
        {
            //Send the multishuttle status
            string sendTelegarm = Template.CreateTelegram(this, TelegramTypes.Status);
            sendTelegarm = sendTelegarm.SetFieldValue(this, TelegramFields.DeviceIdent, "ALL");
            sendTelegarm = sendTelegarm.SetFieldValue(this, TelegramFields.AvailabilityStatus, "AU");
            SendTelegram(sendTelegarm);

            //Send the status end - this will complete the connection
            sendTelegarm = Template.CreateTelegram(this, TelegramTypes.StatusEnd);
            SendTelegram(sendTelegarm);
        }

        private void TUMissionMultiBlock(string telegram)
        {
            string currentLoc = string.Empty;
            DematicActionPoint locA = null, locB = null;
            MultiShuttle ms = null;

            TelegramTypes type = telegram.GetTelegramType(this);

            string current0 = telegram.GetFieldValue(this, TelegramFields.Current, 0);
            string current1 = telegram.GetFieldValue(this, TelegramFields.Current, 1);
            string destLoc0 = telegram.GetFieldValue(this, TelegramFields.Destination, 0);
            string destLoc1 = telegram.GetFieldValue(this, TelegramFields.Destination, 1);
            string tuIdent0 = telegram.GetFieldValue(this, TelegramFields.TUIdent, 0);
            string tuIdent1 = telegram.GetFieldValue(this, TelegramFields.TUIdent, 1);

            string destA = null, destB = null;

            if (current0.LocationType() == LocationTypes.PickStation)
            {
                string aisle = GetPSDSLocFields(current0, PSDSRackLocFields.Aisle);
                string side = GetPSDSLocFields(current0, PSDSRackLocFields.Side);
                // takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05PA
                string loc = string.Format("{0}{1}{2}P", aisle, side, GetPSDSLocFields(current0, PSDSRackLocFields.Level));

                ms = GetMultishuttleFromAisleNum(loc + "A");
                locA = ms.ConveyorLocations.Find(x => x.LocName == loc + "A");
                locB = ms.ConveyorLocations.Find(x => x.LocName == loc + "B");

                if ((locA != null && locA.Active) && (locB != null && locB.Active))
                {
                    string messageA = telegram.Split(',').ToList().Find(x => x.Contains(locA.ActiveLoad.Identification));
                    string messageB = telegram.Split(',').ToList().Find(x => x.Contains(locB.ActiveLoad.Identification));

                    if (destLoc0 == null || destLoc1 == null)
                    {
                        Log.Write(string.Format("{0}: Invalid destinations sent in TUMission - Logically Grouped", Name), Color.Red);
                        return;
                    }

                    if (telegram.GetFieldValue(this, TelegramFields.Destination, 0).LocationType() == LocationTypes.DropStation)  //Assume that both going to DS
                    {
                        destA = string.Format("{0}{1}{2}{3}A", aisle,
                                                              side,
                                                              GetPSDSLocFields(destLoc0, PSDSRackLocFields.Level),
                                                              GetPSDSLocFields(destLoc0, PSDSRackLocFields.ConvType));
                        destB = destA;
                    }
                    else if (telegram.GetFieldValue(this, TelegramFields.Destination).LocationType() == LocationTypes.RackConvIn)
                    {
                        //need to calculate which way around the loads are on the PS and ensure that the elevator task is correct
                        if (locA.ActiveLoad.Identification == tuIdent0)
                        {
                            destA = string.Format("{0}{1}{2}{3}B", aisle, side, GetLocFields(destLoc0, PSDSRackLocFields.Level), GetLocFields(destLoc0, PSDSRackLocFields.ConvType));

                        }
                        else if (locA.ActiveLoad.Identification == tuIdent1)
                        {
                            destA = string.Format("{0}{1}{2}{3}B", aisle, side, GetLocFields(destLoc1, PSDSRackLocFields.Level), GetLocFields(destLoc1, PSDSRackLocFields.ConvType));
                        }
                        else
                        {
                            Log.Write(string.Format("Error {0}: The tuIdent at the PS does not match the tuIdent in the TU Mission - Logically Grouped (Position A"), Color.Orange);
                            return;
                        }

                        if (locB.ActiveLoad.Identification == tuIdent0)
                        {
                            destB = string.Format("{0}{1}{2}{3}B", aisle, side, GetLocFields(destLoc0, PSDSRackLocFields.Level), GetLocFields(destLoc0, PSDSRackLocFields.ConvType));

                        }
                        else if (locB.ActiveLoad.Identification == tuIdent1)
                        {
                            destB = string.Format("{0}{1}{2}{3}B", aisle, side, GetLocFields(destLoc1, PSDSRackLocFields.Level), GetLocFields(destLoc1, PSDSRackLocFields.ConvType));
                        }
                        else
                        {
                            Log.Write(string.Format("Error {0}: The tuIdent at the PS does not match the tuIdent in the TU Mission - Logically Grouped (Position A"), Color.Orange);
                            return;
                        }
                    }
                    else
                    {
                        destA = GetRackDestinationFromDCIBinLocation(telegram, 0);
                        destB = GetRackDestinationFromDCIBinLocation(telegram, 1);
                    }

                    if (messageA != null && messageB != null)
                    {
                        ElevatorTask et = new ElevatorTask(locA.ActiveLoad.Identification, locB.ActiveLoad.Identification)
                        {
                            LoadCycle = Cycle.Double,
                            Flow = TaskType.Infeed,
                            SourceLoadA = locA.LocName,
                            SourceLoadB = locB.LocName,
                            DestinationLoadA = destA,
                            DestinationLoadB = destB,
                            UnloadCycle = Cycle.Single
                        };
                        if (et.DestinationLoadA == et.DestinationLoadB)
                        {
                            et.UnloadCycle = Cycle.Double;
                        }

                        UpDateLoadParameters(telegram, (Case_Load)locA.ActiveLoad, 1);
                        UpDateLoadParameters(telegram, (Case_Load)locB.ActiveLoad, 0);

                        ms.elevators.First(x => x.ElevatorName == side + aisle).ElevatorTasks.Add(et);
                    }
                    else
                    {
                        Log.Write("ERROR: Load ids from telegram do not match active load ids in TU Mission - Logically Grouped", Color.Red);
                        return;
                    }
                }
                else
                {
                    Log.Write("ERROR: Can't find locations or can't find loads on the locations named in TU Mission - Logically Grouped", Color.Red);
                }
            }
            else if (current0.LocationType() == LocationTypes.RackConvOut)
            {
                string aisle = GetRackLocFields(current0, PSDSRackLocFields.Aisle);
                string side = GetRackLocFields(current0, PSDSRackLocFields.Side);

                // takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05PA
                string locNameA = string.Format("{0}{1}{2}O", aisle, side, GetRackLocFields(current0, PSDSRackLocFields.Level));
                string locNameB = string.Format("{0}{1}{2}O", aisle, side, GetRackLocFields(current1, PSDSRackLocFields.Level));
                Cycle loadCycle = Cycle.Double;

                if (current0.RackLevel() == current1.RackLevel())
                {
                    //assume current0 is in front of current1
                    locNameA += "B";
                    locNameB += "A";
                }
                else
                {
                    //if both loads are on different levels then they must both be at the front position and the elevator need to do a sigle load cycle
                    locNameA += "B";
                    locNameB += "B";
                    loadCycle = Cycle.Single;
                }

                ms = GetMultishuttleFromAisleNum(locNameA);
                locA = ms.ConveyorLocations.Find(x => x.LocName == locNameA);
                locB = ms.ConveyorLocations.Find(x => x.LocName == locNameB);

                if ((locA != null && locA.Active) && (locB != null && locB.Active))
                {
                    string messageA = telegram.Split(',').ToList().Find(x => x.Contains(locA.ActiveLoad.Identification));
                    string messageB = telegram.Split(',').ToList().Find(x => x.Contains(locB.ActiveLoad.Identification));

                    if (destLoc0 == null || destLoc1 == null)
                    {
                        Log.Write(string.Format("{0}: Invalid destinations sent in TUMission - Logically Grouped", Name), Color.Red);
                        return;
                    }

                    if (telegram.GetFieldValue(this, TelegramFields.Destination, 0).LocationType() == LocationTypes.DropStation)  //Assume that both going to DS
                    {
                        destA = string.Format("{0}{1}{2}{3}A", aisle,
                                                              side,
                                                              GetPSDSLocFields(destLoc0, PSDSRackLocFields.Level),
                                                              GetPSDSLocFields(destLoc0, PSDSRackLocFields.ConvType));
                        destB = destA;
                    }

                    if (messageA != null && messageB != null)
                    {
                        ElevatorTask et = new ElevatorTask(locB.ActiveLoad.Identification, locA.ActiveLoad.Identification)
                        {
                            LoadCycle = loadCycle,
                            Flow = TaskType.Outfeed,
                            SourceLoadA = locB.LocName,
                            SourceLoadB = locA.LocName,
                            DestinationLoadA = destB,
                            DestinationLoadB = destA,
                            UnloadCycle = Cycle.Double
                        };

                        UpDateLoadParameters(telegram, (Case_Load)locB.ActiveLoad, 1);
                        UpDateLoadParameters(telegram, (Case_Load)locA.ActiveLoad, 0);

                        ms.elevators.First(x => x.ElevatorName == side + aisle).ElevatorTasks.Add(et);
                    }
                    else
                    {
                        Log.Write("ERROR: Load ids from telegram do not match active load ids in TU Mission - Logically Grouped", Color.Red);
                        return;
                    }
                }
            }
        }

        private void TUMissionSingleBlock(string telegram)
        {
            try
            {
                if (Core.Environment.InvokeRequired)
                {
                    Core.Environment.Invoke(() => TUMissionSingleBlock(telegram));
                    return;
                }

                //Look for the load somewhere in the model, if the load is found then change it's status, if not create it at the current location
                Case_Load caseLoad = Case_Load.GetCaseFromIdentification(telegram.GetFieldValue(this, TelegramFields.TUIdent));

                if (caseLoad != null)
                {
                    DCICaseData caseData = caseLoad.Case_Data as DCICaseData;
                    UpDateLoadParameters(telegram, caseLoad);

                    if (telegram.GetFieldValue(this, TelegramFields.Current).LocationType() == LocationTypes.PickStation)
                    {
                        //Check how many loads are at the pickstation, first i need to find the pick station

                        string currentLoc = telegram.GetFieldValue(this, TelegramFields.Current);
                        string destLoc = telegram.GetFieldValue(this, TelegramFields.Destination);
                        string aisle = GetPSDSLocFields(currentLoc, PSDSRackLocFields.Aisle);
                        string side = GetPSDSLocFields(currentLoc, PSDSRackLocFields.Side);
                        string psLevel = GetPSDSLocFields(currentLoc, PSDSRackLocFields.Level);

                        string psA = string.Format("{0}{1}{2}{3}A", aisle, side, psLevel, GetPSDSLocFields(currentLoc, PSDSRackLocFields.ConvType));
                        string psB = string.Format("{0}{1}{2}{3}B", aisle, side, psLevel, GetPSDSLocFields(currentLoc, PSDSRackLocFields.ConvType));

                        MultiShuttle ms = GetMultishuttleFromAisleNum(psA);
                        PickStationConveyor psConv = ms.PickStationConveyors.Find(x => x.Name == string.Format("{0}{1}PS{2}", aisle, side, psLevel));

                        //Check how many loads are on the pickstation if there is only 1 then send a single mission
                        if (psConv.LocationA.Active && psConv.LocationB.Active)
                        {
                            Case_Load caseA = psConv.LocationA.ActiveLoad as Case_Load;
                            Case_Load caseB = psConv.LocationB.ActiveLoad as Case_Load;

                            DCICaseData caseDataA = caseA.Case_Data as DCICaseData;
                            DCICaseData caseDataB = caseB.Case_Data as DCICaseData;

                            //This is a double move to the elevator so don't send a single
                            if (caseB.Identification == telegram.GetFieldValue(this, TelegramFields.TUIdent)) //LocationB just set the destination
                            {
                                UpDateLoadParameters(telegram, caseB);
                                return;
                            }
                            else if (caseA.Identification == telegram.GetFieldValue(this, TelegramFields.TUIdent)) //LocationA Should be the second message so create the elevator task
                            {
                                UpDateLoadParameters(telegram, caseA);

                                string DestLoadA, DestLoadB;

                                if (caseDataA.Destination.LocationType() == LocationTypes.DropStation && GetPSDSLocFields(caseDataA.Destination, PSDSRackLocFields.Side) != side)
                                {
                                    //First check if the destination is to a drop station... check if the drop station is on this elevator
                                    //If not choose a destination to level 1 and remember the load route 
                                    DestLoadA = string.Format("{0}{1}{2}{3}B",
                                                        GetPSDSLocFields(caseDataA.Current, PSDSRackLocFields.Aisle),
                                                        GetPSDSLocFields(caseDataA.Current, PSDSRackLocFields.Side),
                                                        FindLevelForReject(ms),
                                                        "I");
                                }
                                else
                                {

                                    DestLoadA = string.Format("{0}{1}{2}{3}B",
                                                            aisle,
                                                            side,
                                                            //GetLocFields(caseDataA.Destination, PSDSRackLocFields.Level),
                                                            //GetLocFields(caseDataA.Destination, PSDSRackLocFields.ConvType));
                                                            GetLocFields(caseDataA.Destination, PSDSRackLocFields.Level) != "" ? GetLocFields(caseDataA.Destination, PSDSRackLocFields.Level) : GetBinLocField(caseDataA.Destination, BinLocFields.YLoc),
                                                            GetLocFields(caseDataA.Destination, PSDSRackLocFields.ConvType) != "" ? GetLocFields(caseDataA.Destination, PSDSRackLocFields.ConvType) : "I");

                                }

                                if (caseDataB.Destination.LocationType() == LocationTypes.DropStation && GetPSDSLocFields(caseDataB.Destination, PSDSRackLocFields.Side) != side)
                                {
                                    //First check if the destination is to a drop station... check if the drop station is on this elevator
                                    //If not choose a destination to level 1 and remember the load route 
                                    DestLoadB = string.Format("{0}{1}{2}{3}B",
                                                        GetPSDSLocFields(caseDataB.Current, PSDSRackLocFields.Aisle),
                                                        GetPSDSLocFields(caseDataB.Current, PSDSRackLocFields.Side),
                                                        FindLevelForReject(ms),
                                                        "I");
                                }
                                else
                                {
                                    DestLoadB = string.Format("{0}{1}{2}{3}B",
                                                            aisle,
                                                            side,
                                                            GetLocFields(caseDataB.Destination, PSDSRackLocFields.Level) != "" ? GetLocFields(caseDataB.Destination, PSDSRackLocFields.Level) : GetBinLocField(caseDataB.Destination, BinLocFields.YLoc),
                                                            GetLocFields(caseDataB.Destination, PSDSRackLocFields.ConvType) != "" ? GetLocFields(caseDataB.Destination, PSDSRackLocFields.ConvType) : "I");
                                }

                                ElevatorTask et = new ElevatorTask(caseA.Identification, caseB.Identification)
                                {
                                    LoadCycle = Cycle.Double,
                                    Flow = TaskType.Infeed,
                                    SourceLoadA = psA,
                                    SourceLoadB = psB,
                                    DestinationLoadA = DestLoadA,
                                    DestinationLoadB = DestLoadB,
                                    UnloadCycle = Cycle.Single
                                };
                                if (et.DestinationLoadA == et.DestinationLoadB)
                                {
                                    et.UnloadCycle = Cycle.Double;
                                }

                                ms.elevators.First(x => x.ElevatorName == side + aisle).ElevatorTasks.Add(et);
                            }
                            else
                            {
                                Log.Write(string.Format("Error {0}: None of the tuIdents match any of the loads at the PS on StartTransportTelegram"), Color.Orange);
                            }
                        }
                        else
                        {
                            SingleLoadAtPS(telegram, caseLoad);
                        }
                    }
                    else if (telegram.GetFieldValue(this, TelegramFields.Current).LocationType() == LocationTypes.RackConvOut)
                    {
                        LoadAtRackConv(telegram, caseLoad);
                    }
                    else if (telegram.GetFieldValue(this, TelegramFields.Current).LocationType() == LocationTypes.RackConvIn &&
                            caseData.Current == telegram.GetFieldValue(this, TelegramFields.Current)) //Mission for load at Infeed Rack conveyor
                    {

                        ShuttleTask st = new ShuttleTask();
                        string destination = caseData.Destination;

                        if (destination.LocationType() != LocationTypes.BinLocation)
                        {
                            Log.Write("WARNING: Arrived at infeed rack and destination is NOT a binlocation.", Color.Red);
                            return;
                        }

                        string current = telegram.GetFieldValue(this, TelegramFields.Current);
                        string aisle = GetRackLocFields(current, PSDSRackLocFields.Aisle);
                        string side = GetRackLocFields(current, PSDSRackLocFields.Side);
                        string location = string.Format("{0}{1}{2}IB", aisle, side, GetRackLocFields(current, PSDSRackLocFields.Level));

                        MultiShuttle ms = GetMultishuttleFromAisleNum(location);

                        int level;
                        st.Destination = DCIbinLocToMultiShuttleLoc(destination, out level, ms);
                        st.Level = level;
                        st.LoadID = caseLoad.Identification;
                        st.Source = location;

                        ms.shuttlecars[level].ShuttleTasks.Add(st);
                    }
                    else if (telegram.GetFieldValue(this, TelegramFields.Current).LocationType() ==
                             LocationTypes.Shuttle &&
                             caseData.Current == telegram.GetFieldValue(this, TelegramFields.Current)
                    ) //Mission for load on the shuttle
                    {
                        //MRP 16.06.2017 dont think this is the correct way but seems to work for "modify mission after bin full"
                        ShuttleTask st = new ShuttleTask();
                        string destination = caseData.Destination;

                        MultiShuttle ms = multishuttles.First();
                        var levelstring = caseData.Current.Substring(8, 2);
                        int level = int.Parse(levelstring);

                        if (destination.LocationType() == LocationTypes.RackConvOut)//A move to a drop station
                        {
                            // takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05OA
                            st.Destination = DCIrackLocToMultiShuttleRackLoc(level, destination);
                        }
                        else
                        {
                            st.Destination = DCIbinLocToMultiShuttleLoc(destination, out level, ms);
                        }

                        st.Level = level;
                        st.LoadID = caseLoad.Identification;
                        st.Source = st.Destination; //Shuttle will start moving to source, but when done it will try drop because we have a load on board...

                        ms.shuttlecars[level].ShuttleTasks.Add(st);
                    }
                    else
                    {
                        Log.Write(
                            string.Format(
                                "{0}: MultishuttleStartTransportTelegram received but the load was found elsewhere in the model ({1}) message ignored",
                                Name, caseData.Current), Color.Red);
                    }
                }
                //Anything FROM a binloc will not have a load until the shuttle gets to the bin location at this point it will be created.
                else if (telegram.GetFieldValue(this, TelegramFields.Current).LocationType() == LocationTypes.BinLocation)
                {
                    CreateShuttleTask(telegram);
                }
                else if (telegram.GetFieldValue(this, TelegramFields.Current).LocationType() == LocationTypes.BinLocation &&
                    telegram.GetFieldValue(this, TelegramFields.Destination).LocationType() == LocationTypes.DropStation) //It's a shuffle move
                {
                    SingleLoadOut(telegram);
                }
                else
                {
                    Log.Write("Error: Load not found in StartTransportTelegramReceived", Color.Red);
                }
            }
            catch (Exception ex)
            {
                Log.Write(ex.ToString());
                if (ex.InnerException != null)
                {
                    Log.Write(ex.InnerException.ToString());
                }
            }
        }

        private string FindLevelForReject(MultiShuttle ms)
        {
            string destLevel = "01";
            foreach (RackConveyor rackConveyor in ms.RackConveyors.Reverse<RackConveyor>())
            {
                if (rackConveyor.RackConveyorType == MultiShuttleDirections.Infeed)
                {
                    if (rackConveyor.TransportSection.Route.Loads.Count < 2)
                    {
                        destLevel = rackConveyor.Level.ToString("00");
                    }
                }
            }
            return destLevel;
        }

        private void LoadAtRackConv(string telegram, Case_Load caseLoad)
        {
            if (telegram.GetFieldValue(this, TelegramFields.Destination).LocationType() == LocationTypes.DropStation)
            {
                DCICaseData caseData = caseLoad.Case_Data as DCICaseData;

                string currentLevel = GetRackLocFields(caseData.Current, PSDSRackLocFields.Level);
                string destLevel = GetPSDSLocFields(caseData.Destination, PSDSRackLocFields.Level);
                string aisle = GetRackLocFields(caseData.Current, PSDSRackLocFields.Aisle);
                string side = GetRackLocFields(caseData.Current, PSDSRackLocFields.Side);

                //Set somedata on the load
                caseData.Current = telegram.GetFieldValue(this, TelegramFields.Current);
                caseData.Destination = telegram.GetFieldValue(this, TelegramFields.Destination);

                int dropIndex = 0;
                if (int.TryParse(telegram.GetFieldValue(this, TelegramFields.DropIndex), out dropIndex))
                {
                    caseData.DropIndex = dropIndex;
                }

                ElevatorTask et = new ElevatorTask(null, caseLoad.Identification)
                {
                    SourceLoadB = string.Format("{0}{1}{2}{3}B", aisle, side, currentLevel, (char)ConveyorTypes.OutfeedRack),
                    DestinationLoadB = string.Format("{0}{1}{2}{3}A", aisle, side, destLevel, (char)ConveyorTypes.Drop), // aasyyxz: a=aisle, s = side, yy = level, x = input or output, Z = loc A or B e.g. 01R05OA 
                    DropIndexLoadB = dropIndex,
                    LoadCycle = Cycle.Single,
                    UnloadCycle = Cycle.Single,
                    Flow = TaskType.Outfeed
                };

                string elevatorName = string.Format("{0}{1}", side, aisle);
                MultiShuttle ms = GetMultishuttleFromAisleNum(aisle);
                Elevator elevator = ms.elevators.First(x => x.ElevatorName == elevatorName);
                elevator.ElevatorTasks.Add(et);
            }
            else if (telegram.GetFieldValue(this, TelegramFields.Destination).LocationType() == LocationTypes.RackConvIn) //This will only work for drive through!
            {
                DCICaseData caseData = caseLoad.Case_Data as DCICaseData;

                string currentLevel = GetRackLocFields(caseData.Current, PSDSRackLocFields.Level);
                string destLevel = GetRackLocFields(caseData.Destination, PSDSRackLocFields.Level);
                string aisle = GetRackLocFields(caseData.Current, PSDSRackLocFields.Aisle);
                string side = GetRackLocFields(caseData.Current, PSDSRackLocFields.Side);

                //Set somedata on the load
                caseData.Current = telegram.GetFieldValue(this, TelegramFields.Current);
                caseData.Destination = telegram.GetFieldValue(this, TelegramFields.Destination);

                int dropIndex = 0;
                if (int.TryParse(telegram.GetFieldValue(this, TelegramFields.DropIndex), out dropIndex))
                {
                    caseData.DropIndex = dropIndex;
                }

                ElevatorTask et = new ElevatorTask(null, caseLoad.Identification)
                {
                    SourceLoadB = string.Format("{0}{1}{2}{3}B", aisle, side, currentLevel, (char)ConveyorTypes.OutfeedRack),
                    DestinationLoadB = string.Format("{0}{1}{2}{3}A", aisle, side, destLevel, (char)ConveyorTypes.InfeedRack), // aasyyxz: a=aisle, s = side, yy = level, x = input or output, Z = loc A or B e.g. 01R05OA 
                    DropIndexLoadB = dropIndex,
                    LoadCycle = Cycle.Single,
                    UnloadCycle = Cycle.Single,
                    Flow = TaskType.HouseKeep
                };

                string elevatorName = string.Format("{0}{1}", side, aisle);
                MultiShuttle ms = GetMultishuttleFromAisleNum(aisle);
                Elevator elevator = ms.elevators.First(x => x.ElevatorName == elevatorName);
                elevator.ElevatorTasks.Add(et);
            }
        }

        /// <summary>
        /// A single load out of a bin location to a drop station
        /// </summary>
        private void SingleLoadOut(string telegram)
        {
            CreateShuttleTask(telegram);
        }

        /// <summary>
        /// Shuffle Move
        /// </summary>
        /// <param name="telegram"></param>
        private void CreateShuttleTask(string telegram) //TODO should this go into the control object?
        {
            int level;
            string destination = string.Empty;
            string current = telegram.GetFieldValue(this, TelegramFields.Current); //GetFieldValue by string not enum (faster)

            MultiShuttle ms = GetMultishuttleFromAisleNum(GetBinLocField(current, BinLocFields.Aisle));
            ShuttleTask st = new ShuttleTask();

            st.Source = DCIbinLocToMultiShuttleLoc(current, out level, ms);
            string tlgDest = telegram.GetFieldValue(this, TelegramFields.Destination);

            if (tlgDest.LocationType() == LocationTypes.BinLocation)//A shuffle move
            {
                destination = DCIbinLocToMultiShuttleLoc(tlgDest, out level, ms);
            }
            else if (tlgDest.LocationType() == LocationTypes.DropStation)//A move to a drop station
            {
                // takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05OA
                destination = string.Format("{0}{1}{2}OA", GetPSDSLocFields(tlgDest, PSDSRackLocFields.Aisle),
                                                           GetPSDSLocFields(tlgDest, PSDSRackLocFields.Side),
                                                           level.ToString().PadLeft(2, '0'));
            }
            else if (tlgDest.LocationType() == LocationTypes.RackConvOut)
            {
                destination = DCIrackLocToMultiShuttleRackLoc(level, tlgDest);//Create the rack location destination for the shuttle task
            }

            st.Destination = destination;
            st.Level = level;
            st.LoadID = telegram.GetFieldValue(this, TelegramFields.TUIdent);
            st.caseData = CreateDCICaseData(telegram);
            ms.shuttlecars[level].ShuttleTasks.Add(st);
        }

        public static string DCIrackLocToMultiShuttleRackLoc(int level, string dest, string pos = "A")
        {
            // takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05OA
            return string.Format("{0}{1}{2}O{3}", GetRackLocFields(dest, PSDSRackLocFields.Aisle),
                                                   GetRackLocFields(dest, PSDSRackLocFields.Side),
                                                   level.ToString().PadLeft(2, '0'),
                                                   pos);
        }

        public static string DCILocToMultiShuttleConvLoc(int level, string dest, string pos = "A")
        {
            // takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05OA
            return string.Format("{0}{1}{2}O{3}", GetLocFields(dest, PSDSRackLocFields.Aisle),
                                                  GetLocFields(dest, PSDSRackLocFields.Side),
                                                  level.ToString().PadLeft(2, '0'),
                                                  pos);
        }

        /// <summary>
        /// Tasks are split into elevator task and shuttle task this method gives you the corrent rack location (i.e. the destination for an infeed elevator task)
        /// </summary>
        /// <param name="messageA">Can be either a full message or just the destination</param>
        /// <param name="multipal">We need to know if it is multipal as we need to know if we need to extract destinations or destination</param>
        /// <returns>A rack destination that the assembly will understand</returns>
        private string GetRackDestinationFromDCIBinLocation(string telegram, int blockPosition)
        {
            string dest = null, current = null;

            current = telegram.GetFieldValue(this, TelegramFields.Current, blockPosition);
            dest = telegram.GetFieldValue(this, TelegramFields.Destination, blockPosition);

            //Need to make the intermediate position to the rack conveyor
            //takes the form aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05OA
            //The actual final destination from the message is stored on a load parameter and this will be used when creating the shuttle task

            MultiShuttle ms = GetMultishuttleFromAisleNum(GetPSDSLocFields(current, PSDSRackLocFields.Aisle));
            string destinationLoc = string.Format("{0}{1}{2}IA", GetPSDSLocFields(current, PSDSRackLocFields.Aisle), GetPSDSLocFields(current, PSDSRackLocFields.Side), GetBinLocField(dest, BinLocFields.YLoc));

            DematicActionPoint ap = ms.ConveyorLocations.Find(x => x.LocName == destinationLoc);

            if (ap == null)
            {
                Log.Write("Can't find location in MHEController_Multishuttle.GetRackDestinationFromDCIBinLocation.", Color.Red);
                return null;
            }

            return destinationLoc;
        }

        /// <summary>
        /// Takes a dci bin location in the form MS011005080152 and converts it to a shuttletask location (sxxxyydd: Side, xxx location, yy = level, dd = depth)
        /// 
        /// </summary>
        public string DCIbinLocToMultiShuttleLoc(string dciLoc, out int _level, MultiShuttle ms)
        {
            string side = GetBinLocField(dciLoc, BinLocFields.Side);

            if (side == "1") { side = "L"; }
            else if (side == "2") { side = "R"; }

            string yLoc = GetBinLocField(dciLoc, BinLocFields.YLoc);
            int.TryParse(yLoc, out _level); //assign out parameter

            string xLoc = ((MHEControl_MultiShuttle)ms.ControllerProperties).XLocConverter(GetBinLocField(dciLoc, BinLocFields.XLoc), GetBinLocField(dciLoc, BinLocFields.RasterPos));

            // takes the form  sxxxyydd: Side, xxx location, yy = level, dd = depth
            return string.Format("{0}{1}{2}{3}", side,
                                                 xLoc,  //Need control object here to get properties of raster position and type 
                                                 yLoc,
                                                 GetBinLocField(dciLoc, BinLocFields.Depth));
        }

        private void SingleLoadAtPS(string telegramFields, Case_Load caseLoad)
        {
            // Rack Location for an ElevatorTask takes the form:  aasyyxz: aa=aisle, s = side, yy = level, x = conv type see enum ConveyorTypes , Z = loc A or B e.g. 01R05OA e.g. 01R05OA
            // Source location for a shuttleTask takes the form: sxxxyydd: Side, xxx location, yy = level, dd = depth
            // Elevator Format saa s = Side ( L or R), aa = aisle

            string currentLoc = telegramFields.GetFieldValue(this, TelegramFields.Current);
            string destLoc = telegramFields.GetFieldValue(this, TelegramFields.Destination);
            string aisle = GetPSDSLocFields(currentLoc, PSDSRackLocFields.Aisle);
            string side = GetPSDSLocFields(currentLoc, PSDSRackLocFields.Side);
            string dest = null;
            string sourceLoadB = string.Format("{0}{1}{2}{3}B", aisle,    //Single load will always be at B
                                                                side,
                                                                GetPSDSLocFields(currentLoc, PSDSRackLocFields.Level),
                                                                GetPSDSLocFields(currentLoc, PSDSRackLocFields.ConvType));

            MultiShuttle ms = GetMultishuttleFromAisleNum(sourceLoadB);

            if (telegramFields.GetFieldValue(this, TelegramFields.Destination).LocationType() == LocationTypes.DropStation)
            {
                //First check if the destination is to a drop station... check if the drop station is on this elevator
                if (GetPSDSLocFields(destLoc, PSDSRackLocFields.Side) != side)
                {
                    //If not choose a destination to level 1 and remember the load route 
                    dest = string.Format("{0}{1}{2}{3}A", aisle, side, FindLevelForReject(ms), "I");
                }
                else
                {
                    dest = string.Format("{0}{1}{2}{3}A",
                                        aisle,
                                        side,
                                        GetPSDSLocFields(destLoc, PSDSRackLocFields.Level),
                                        GetPSDSLocFields(destLoc, PSDSRackLocFields.ConvType));
                }


            }
            else if (telegramFields.GetFieldValue(this, TelegramFields.Destination).LocationType() == LocationTypes.RackConvIn)
            {
                dest = string.Format("{0}{1}{2}{3}B", aisle,
                                                      side,
                                                      GetLocFields(destLoc, PSDSRackLocFields.Level),
                                                      GetLocFields(destLoc, PSDSRackLocFields.ConvType));
            }
            else
            {
                dest = GetRackDestinationFromDCIBinLocation(string.Join(",", telegramFields), 0);
            }


            ElevatorTask et = new ElevatorTask(null, caseLoad.Identification)
            {
                SourceLoadB = sourceLoadB,
                DestinationLoadB = dest,
                LoadCycle = Cycle.Single,
                UnloadCycle = Cycle.Single,
                Flow = TaskType.Infeed
            };

            ms.elevators.First(x => x.ElevatorName == side + aisle).ElevatorTasks.Add(et);
            //ms.elevators[side+aisle].ElevatorTasks.Add(et);
        }

        /// <summary>
        /// Uses a Mission Data Set to identify the correct aisle (multishuttle
        /// </summary>
        /// <param name="location">Telegram in the form of the Mission Data Set</param>
        /// <returns>A multishuttle that is controlled by this controller</returns>
        private MultiShuttle GetMultishuttleFromAisleNum(string location)
        {
            int aNum;
            int.TryParse(location.Substring(0, 2), out aNum);
            if (aNum == 0) return null;

            var aisleNum = multishuttles.Where(x => x.AisleNumber == aNum);
            if (aisleNum.Count() > 1)
            {
                Log.Write("ERROR: List of multishuttles [Experior.Catalog.Dematic.ATC.Assemblies.Storage.MHEController_Multishuttle.multishuttles] do not have unique aisle numbers.", Color.Red);
                Log.Write("List of multishuttles accessed in Experior.Catalog.Dematic.ATC.Assemblies.Storage.MHEController_Multishuttle.GetMultishuttleFromAisleNum().", Color.Red);
                return null;
            }
            else if (aisleNum.Any())
            {
                return aisleNum.First();
            }
            else
            {
                Log.Write("ERROR: Multishuttle not found in list of multishuttles [Experior.Catalog.Dematic.ATC.Assemblies.Storage.MHEController_Multishuttle.multishuttles].", Color.Red);
                Log.Write("Maybe aisle number from message does not match avaiable aisle numbers in avaiable multishuttles.", Color.Red);
            }

            return null;
        }

        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            multishuttles.Add(assem as MultiShuttle);

            MHEControl protocolConfig = null;  //generic plc config object

            if (assem is MultiShuttle)
            {
                protocolConfig = CreateMHEControlGeneric<MultiShuttleDCIInfo, MHEControl_MultiShuttle>(assem, info);
            }
            else
            {
                Experior.Core.Environment.Log.Write("Can't create MHE Control, object is not defined in the 'CreateMHEControl' of the controller", Color.Red);
                return null;
            }
            //......other assemblies should be added here....do this with generics...correction better to do this with reflection...That is BaseController should use reflection
            //and not generics as we do not know the types at design time and it means that the above always has to be edited when adding a new MHE control object.
            protocolConfig.ParentAssembly = (Assembly)assem;
            controls.Add(protocolConfig);
            control = protocolConfig as MHEControl_MultiShuttle;
            return protocolConfig as MHEControl;
        }

        public void RemoveSSCCBarcode(string ULID)
        {
            throw new NotImplementedException();
        }

        public void RemoveFromRoutingTable(string barcode)
        {
            throw new NotImplementedException();
        }

        public BaseCaseData GetCaseData()
        {
            return new DCICaseData();
        }
    }

    [Serializable]
    [TypeConverter(typeof(MHEController_MultishuttleDCIInfo))]
    public class MHEController_MultishuttleDCIInfo : BaseDCIControllerInfo
    {
    }
}
