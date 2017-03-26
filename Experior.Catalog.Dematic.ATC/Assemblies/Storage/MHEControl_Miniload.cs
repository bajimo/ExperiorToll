using Experior.Catalog.Dematic.Storage.Miniload.Assemblies;
using Experior.Dematic.Base;
using System;
using System.Drawing;
using System.Xml.Serialization;
using System.Collections.Generic;
using Dematic.ATC;
using System.ComponentModel;

namespace Experior.Catalog.Dematic.ATC.Assemblies.Storage
{
    class MHEControl_Miniload : MHEControl
    {
        Miniload theMiniload;
        MHEController_Miniload controller;
        MiniloadATCInfo miniloadATCInfo;
        List<MiniloadTask> TaskList = new List<MiniloadTask>();

        public MHEControl_Miniload(MiniloadATCInfo info, Miniload miniload)
        {
            Info = info;  // set this to save properties 
            miniloadATCInfo = info;
            theMiniload = miniload;
            controller = ((MHEController_Miniload)theMiniload.Controller);

            //Subscribe to all of the miniload events here 
            theMiniload.OnMiniloadTaskComplete += theMiniload_OnMiniloadTaskComplete;
            theMiniload.OnMiniloadDropStationAvailableChanged += theMiniload_OnMiniloadDropStationAvailableChanged;
            theMiniload.OnMiniloadReset += theMiniload_OnMiniloadReset;
        }

        public void StartTransportTelegramReceived(string[] telegramFields)
        {
            //Create miniload tasklist from message
            MiniloadTask newTasks = new MiniloadTask(new List<object> { telegramFields }); //Convert telegram to TelegramData

            //is it a storage, retreival, relocation or reject task type
            string source = telegramFields.GetFieldValue(TelegramFields.source);
            string destination = telegramFields.GetFieldValue(TelegramFields.destination);

            newTasks.TaskType = GetTaskType(source, destination);

            if (newTasks.TaskType == MiniloadTaskType.Storage || newTasks.TaskType == MiniloadTaskType.Reject) //Store 1 in the racking from pick station
            {
                ATCCaseLoad LoadPos2 = theMiniload.Position2Load() as ATCCaseLoad;
                ATCCaseLoad LoadPos1 = theMiniload.Position1Load() as ATCCaseLoad;

                //This will only work if the pick station is on the RHS of the Miniload, Pickstation MergeSide set as left
                if (LoadPos1 == null || LoadPos1.TUIdent != telegramFields.GetFieldValue(TelegramFields.tuIdent) || LoadPos2 != null)
                {
                    Log.Write(string.Format("Miniload {0}: Loads at pick station and 'StartTransportTelegram' from ATC do not match, telegram ignored", theMiniload.Name));
                    return;
                }
                
                theMiniload.LockPickStation(); //Don't allow any more loads into the pick station

                //Set the Load in position 1 data correctly
                controller.UpDateLoadParameters(telegramFields, LoadPos1);

                //Create the miniload half cycles
                MiniloadHalfCycle pickFromPS = new MiniloadHalfCycle() //Pick from pick station
                {
                    Cycle = MiniloadCycle.PickPS,
                    TuIdentPos1 = LoadPos1.TUIdent
                };
                newTasks.HalfCycles.Add(pickFromPS);

                if (newTasks.TaskType == MiniloadTaskType.Storage)
                {

                    MiniloadHalfCycle pos2Drop = new MiniloadHalfCycle() //Drop the load in the racking
                    {
                        Cycle = MiniloadCycle.DropRack,
                        LHD = 2,
                        Length = GetXLoc(telegramFields, LoadPos1),
                        Height = GetYLoc(telegramFields, LoadPos1),
                        Depth = GetDepth(telegramFields, LoadPos1),
                        RackSide = GetSide(telegramFields, LoadPos1),
                        TuIdentPos2 = LoadPos1.TUIdent
                    };
                    newTasks.HalfCycles.Add(pos2Drop);
                }

                else if (newTasks.TaskType == MiniloadTaskType.Reject)
                {
                    MiniloadHalfCycle dropToDS = new MiniloadHalfCycle() //Drop to the drop Station
                    {
                        Cycle = MiniloadCycle.DropDS,
                        TuIdentPos2 = LoadPos1.TUIdent,
                    };
                    newTasks.HalfCycles.Add(dropToDS);
                }
            }
            else if (newTasks.TaskType == MiniloadTaskType.Retreival || newTasks.TaskType == MiniloadTaskType.Relocation)
            {
                //Create the miniload half cycles
                ATCCaseData caseData = controller.CreateATCCaseData(telegramFields);
                string loadSource = telegramFields.GetFieldValue(TelegramFields.source);
                string loadTUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
                string loadDestination = telegramFields.GetFieldValue(TelegramFields.destination);
                int lhd = 1;

                //Decide on what LHD the load is picked to
                if (newTasks.TaskType == MiniloadTaskType.Relocation && GetSide(loadDestination) == Side.Right)
                {
                    lhd = 2;
                }

                MiniloadHalfCycle loadPick = new MiniloadHalfCycle()
                {
                    Cycle = MiniloadCycle.PickRack,
                    LHD = lhd,
                    Length = GetXLoc(loadSource),
                    Height = GetYLoc(loadSource),
                    Depth = GetDepth(loadSource),
                    RackSide = GetSide(loadSource),
                    TuIdentPos1 = lhd == 1 ? loadTUIdent : null,
                    TuIdentPos2 = lhd == 2 ? loadTUIdent : null,
                    CaseData = caseData
                };
                newTasks.HalfCycles.Add(loadPick);

                if (newTasks.TaskType == MiniloadTaskType.Retreival)
                {
                    MiniloadHalfCycle dropToDS = new MiniloadHalfCycle() //Drop to the drop Station
                    {
                        Cycle = MiniloadCycle.DropDS,
                        TuIdentPos1 = loadTUIdent,
                    };
                    newTasks.HalfCycles.Add(dropToDS);
                }
                else if (newTasks.TaskType == MiniloadTaskType.Relocation)
                {
                    MiniloadHalfCycle loadDrop = new MiniloadHalfCycle()
                    {
                        Cycle = MiniloadCycle.DropRack,
                        LHD = lhd,
                        Length = GetXLoc(loadDestination),
                        Height = GetYLoc(loadDestination),
                        Depth = GetDepth(loadDestination),
                        RackSide = GetSide(loadDestination),
                        TuIdentPos1 = lhd == 1 ? loadTUIdent : null,
                        TuIdentPos2 = lhd == 2 ? loadTUIdent : null,
                        CaseData = caseData
                    };
                    newTasks.HalfCycles.Add(loadDrop);
                }
            }

            AddNewTasks(newTasks);
        }

        public void StartMultipleTransportTelegramReceived(string[] telegramFields)
        {
            //Create miniload tasklist from message
            List<string> indexMatches = new List<string>();
            List<string> telegrams = Telegrams.DeMultaplise(telegramFields, TelegramTypes.StartMultipleTransportTelegram, out indexMatches);
            for (int i = 0; i < telegrams.Count; i++)
            {
                telegrams[i] = telegrams[i].Replace(string.Format("s{0}", indexMatches[i]), "");
            }

            MiniloadTask newTasks = new MiniloadTask(new List<object> { telegrams[0].Split(','), telegrams[1].Split(',') }); //Convert telegram to TelegramData
            //MiniloadTask newTasks = new MiniloadTask(telegramFields);

            int telegramCount = telegramFields.ArrayCount();
            if (telegramCount == 2)
            {
                //is it a storage, retreival, relocation or reject task type
                string source = telegramFields.GetFieldValue(TelegramFields.sources, "[0]");
                string destination = telegramFields.GetFieldValue(TelegramFields.destinations, "[0]");

                newTasks.TaskType = GetTaskType(source, destination);
                
                if (newTasks.TaskType == MiniloadTaskType.Storage || newTasks.TaskType == MiniloadTaskType.Reject) //Store 2 in the racking from pick station
                {
                    ATCCaseLoad LoadPos1 = theMiniload.Position1Load() as ATCCaseLoad;
                    ATCCaseLoad LoadPos2 = theMiniload.Position2Load() as ATCCaseLoad;

                    //Check that the load in Pos 1 matches one of the messages, what is the index??
                    if ((LoadPos1 != null && LoadPos2 != null) && LoadPos1.TUIdent == telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]") && LoadPos2.TUIdent == telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]"))
                    {
                        //Now we know what data to put onto what load
                        controller.UpDateLoadParameters(telegramFields, LoadPos1, "[0]");
                        controller.UpDateLoadParameters(telegramFields, LoadPos2, "[1]");
                    }
                    else if ((LoadPos1 != null && LoadPos2 != null) && LoadPos1.TUIdent == telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]") && LoadPos2.TUIdent == telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]"))
                    {
                        //Also here we know what data to put on what load
                        controller.UpDateLoadParameters(telegramFields, LoadPos1, "[1]");
                        controller.UpDateLoadParameters(telegramFields, LoadPos2, "[0]");
                    }
                    else
                    {
                        //There is a problem
                        Log.Write(string.Format("Miniload {0}: Loads at pick station and 'MultipleStartTransportTelegram' from ATC do not match, telegram ignored", theMiniload.Name));
                        return;
                    }

                    MiniloadHalfCycle pickFromPS = new MiniloadHalfCycle() //Pick from pick station
                    {
                        Cycle = MiniloadCycle.PickPS,
                        TuIdentPos1 = LoadPos1.TUIdent,
                        TuIdentPos2 = LoadPos2.TUIdent
                    };
                    newTasks.HalfCycles.Add(pickFromPS);

                    if (newTasks.TaskType == MiniloadTaskType.Storage)
                    {
                        //Create the half cycles first, then decide which one the miniload performs fisrt
                        MiniloadHalfCycle pos1Drop = new MiniloadHalfCycle() //Drop the first load
                        {
                            Cycle = MiniloadCycle.DropRack,
                            LHD = 1,
                            Length = GetXLoc(telegramFields, LoadPos1),
                            Height = GetYLoc(telegramFields, LoadPos1),
                            Depth = GetDepth(telegramFields, LoadPos1),
                            RackSide = GetSide(telegramFields, LoadPos1),
                            TuIdentPos1 = LoadPos1.TUIdent
                        };

                        MiniloadHalfCycle pos2Drop = new MiniloadHalfCycle() //Drop the second load
                        {
                            Cycle = MiniloadCycle.DropRack,
                            LHD = 2,
                            Length = GetXLoc(telegramFields, LoadPos2),
                            Height = GetYLoc(telegramFields, LoadPos2),
                            Depth = GetDepth(telegramFields, LoadPos2),
                            RackSide = GetSide(telegramFields, LoadPos2),
                            TuIdentPos2 = LoadPos2.TUIdent
                        };

                        if (pos1Drop.RackSide == null || pos2Drop.RackSide == null)
                        {
                            Log.Write(string.Format("Miniload {0}: 'MultipleStartTransportTelegram' cannot resolve drop side, telegram ignored", theMiniload.Name));
                            return;
                        }
                        else if (pos1Drop.RackSide == Side.Right && pos2Drop.RackSide == Side.Left)
                        {
                            Log.Write(string.Format("Miniload {0}: 'MulipleStartTransportTelegram' cannot perform drop, left load drop right and right load drop left not possible", theMiniload.Name));
                        }
                        else //We now know which loads are being sent to which side so that we can schedule the miniload accordingly
                        {
                            if ((pos1Drop.RackSide == Side.Left && pos2Drop.RackSide == Side.Left) || (pos1Drop.RackSide == Side.Left && (pos1Drop.Length < pos2Drop.Length))) //Both are going left or pos1 is going left, pos2 is going right and pos1 is the closest to the p/d
                            {
                                newTasks.HalfCycles.Add(pos1Drop);
                                newTasks.HalfCycles.Add(pos2Drop);
                            }
                            else
                            {
                                newTasks.HalfCycles.Add(pos2Drop);
                                newTasks.HalfCycles.Add(pos1Drop);
                            }
                        }
                    }
                    else if (newTasks.TaskType == MiniloadTaskType.Reject)
                    {
                        MiniloadHalfCycle dropToDS = new MiniloadHalfCycle() //Drop to the drop Station
                        {
                            Cycle = MiniloadCycle.DropDS,
                            TuIdentPos1 = LoadPos1.TUIdent,
                            TuIdentPos2 = LoadPos2.TUIdent
                        };
                        newTasks.HalfCycles.Add(dropToDS);
                    }
                }
                else if (newTasks.TaskType == MiniloadTaskType.Retreival)
                {
                    //Initially i need to know which load i am going to pick first
                    string posASource = telegramFields.GetFieldValue(TelegramFields.sources, "[0]");
                    string posBSource = telegramFields.GetFieldValue(TelegramFields.sources, "[1]");

                    string posATuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]");
                    string posBTuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]");

                    Side? posASide = GetSide(posASource);
                    Side? posBSide = GetSide(posBSource);
                    float posAXLoc = GetXLoc(posASource);
                    float posBXLoc = GetXLoc(posBSource);
                    
                    if (posASide == null || posBSide == null || posATuIdent == null || posBTuIdent == null)
                    {
                        //There is a problem
                        Log.Write(string.Format("Miniload {0}: Error processing drop missions from 'MultipleStartTransportTelegram', telegram ignored", theMiniload.Name));
                        return;
                    }

                    //Create the case data, this will be used by the miniload to create the load (from the controller)
                    ATCCaseData pos1CaseData = controller.CreateATCCaseData(telegramFields, "[0]");
                    ATCCaseData pos2CaseData = controller.CreateATCCaseData(telegramFields, "[1]");

                    int LHDposA = 0, LHDposB = 0;
                    bool? AthenB = null;
                    if (posASide == Side.Left && posBSide == Side.Left)
                    {
                        if (posAXLoc > posBXLoc){ LHDposA = 2; LHDposB = 1; AthenB = true;}
                        else { LHDposA = 1; LHDposB = 2; AthenB = false; }
                    }
                    else if (posASide == Side.Right && posBSide == Side.Right)
                    {
                        if (posAXLoc > posBXLoc) { LHDposA = 1; LHDposB = 2; AthenB = true; }
                        else { LHDposA = 2; LHDposB = 1; AthenB = false; }
                    }
                    else if (posASide == Side.Left && posBSide == Side.Right)
                    {
                        LHDposA = 1; LHDposB = 2;
                        AthenB = posAXLoc > posBXLoc ? true : false;
                    }
                    else if (posASide == Side.Right && posBSide == Side.Left)
                    {
                        LHDposA = 2; LHDposB = 1;
                        AthenB = posAXLoc > posBXLoc ? true : false;
                    }

                    if (LHDposA == 0 || LHDposB == 0 || AthenB == null)
                    {
                        Log.Write(string.Format("Miniload {0}: Error calculating pickup order or LHD assignment", theMiniload.Name), Color.Red);
                        return;
                    }

                    MiniloadHalfCycle posAPick = new MiniloadHalfCycle()
                    {
                        Cycle = MiniloadCycle.PickRack,
                        LHD = LHDposA,
                        Length = GetXLoc(posASource),
                        Height = GetYLoc(posASource),
                        Depth = GetDepth(posASource),
                        RackSide = posASide,
                        TuIdentPos1 = posATuIdent, 
                        CaseData = pos1CaseData
                    };

                    MiniloadHalfCycle posBPick = new MiniloadHalfCycle()
                    {
                        Cycle = MiniloadCycle.PickRack,
                        LHD = LHDposB,
                        Length = GetXLoc(posBSource),
                        Height = GetYLoc(posBSource),
                        Depth = GetDepth(posBSource),
                        RackSide = posBSide,
                        TuIdentPos2 = posBTuIdent,
                        CaseData = pos2CaseData
                    };

                    if (AthenB == true)
                    {
                        newTasks.HalfCycles.Add(posAPick);
                        newTasks.HalfCycles.Add(posBPick);
                    }
                    else
                    {
                        newTasks.HalfCycles.Add(posBPick);
                        newTasks.HalfCycles.Add(posAPick);
                    }

                    MiniloadHalfCycle dropToDS = new MiniloadHalfCycle() //Drop to the drop Station
                    {
                        Cycle = MiniloadCycle.DropDS,
                        TuIdentPos1 = posATuIdent,
                        TuIdentPos2 = posBTuIdent
                    };
                    newTasks.HalfCycles.Add(dropToDS);
                }
                else if (newTasks.TaskType == MiniloadTaskType.Relocation)
                {
                    Log.Write(string.Format("Miniload {0}: cannot generate double relocation missions from 'MultipleStartTransportTelegram', telegram ignored", theMiniload.Name), Color.Red);
                    return;
                }
            }
            else if (telegramCount == 1)
            {
                //Not sure if this is needed or not, why send a multiple message with only 1 load in it?
            }

            AddNewTasks(newTasks);
        }

        public void RequestStateTelegramReceived(string[] telegramFields)
        {
            string telegram = telegramFields.CreateTelegramFromTelegram(TelegramTypes.StateChangedTelegram);
            telegram = telegram.SetFieldValue(TelegramFields.newState, "AU");
            controller.SendTelegram(telegram, true);
        }

        void theMiniload_OnMiniloadTaskComplete(object sender, MiniloadTaskCompleteEventArgs e)
        {
            if (TaskList[0].HalfCycles[0] == e._miniloadTask) //TaskLis[0].HalfCycle[0] Should be the current task 
            {
                //Need to send a message to the WMS at this point depending on the cycle
                if (e._miniloadTask.Cycle == MiniloadCycle.DropRack || e._miniloadTask.Cycle == MiniloadCycle.DropDS)
                {
                    //Log.Write(string.Format("A load has been dropped in the rack"));
                    //string[] index = new string[] {null, null};
                    int?[] index = new int?[] { null, null };
                    if (!string.IsNullOrEmpty(e._miniloadTask.TuIdentPos1))
                    {
                        index[0] = GetIndex(TaskList[0].MissionData, e._miniloadTask.TuIdentPos1);
                    }
                    if (!string.IsNullOrEmpty(e._miniloadTask.TuIdentPos2))
                    {
                        index[1] = GetIndex(TaskList[0].MissionData, e._miniloadTask.TuIdentPos2);
                    }

                    if (index[0] != null && index[1] != null) //Multiple drop to drop station
                    {
                        string blah = ((string[])TaskList[0].MissionData[(int)index[0]]).CreateTelegramFromTelegram(TelegramTypes.TransportFinishedTelegram, true);


                        List<string> telegrams = new List<string> {
                            ((string[])TaskList[0].MissionData[(int)index[0]]).CreateTelegramFromTelegram(TelegramTypes.TransportFinishedTelegram, true),
                            ((string[])TaskList[0].MissionData[(int)index[1]]).CreateTelegramFromTelegram(TelegramTypes.TransportFinishedTelegram, true) };

                        for (int i = 0; i < 2; i++)
                        {
                            telegrams[i] = telegrams[i].SetFieldValue(TelegramFields.source, ((string[])TaskList[0].MissionData[(int)index[i]]).GetFieldValue(TelegramFields.source));
                            telegrams[i] = telegrams[i].SetFieldValue(TelegramFields.location, ((string[])TaskList[0].MissionData[(int)index[i]]).GetFieldValue(TelegramFields.destination));
                            telegrams[i] = telegrams[i].SetFieldValue(TelegramFields.stateCode, ((string[])TaskList[0].MissionData[(int)index[i]]).GetFieldValue(TelegramFields.presetStateCode));
                        }

                        string telegram = Telegrams.CreateMultipalMessage(telegrams, TelegramTypes.MultipleTransportFinishedTelegram, controller.Name);
                        //telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                        controller.SendTelegram(telegram, true);
                    }
                    else if (index[0] != null || index[1] != null) //Single drop from multiple pick
                    {
                        foreach (int? dex in index) //Double loads being dropped
                        {
                            if (dex != null)
                            {
                                string telegram = ((string[])TaskList[0].MissionData[(int)dex]).CreateTelegramFromTelegram(TelegramTypes.TransportFinishedTelegram, true);
                                telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                                telegram = telegram.SetFieldValue(TelegramFields.source, ((string[])TaskList[0].MissionData[(int)dex]).GetFieldValue(TelegramFields.source));
                                telegram = telegram.SetFieldValue(TelegramFields.location, ((string[])TaskList[0].MissionData[(int)dex]).GetFieldValue(TelegramFields.destination));
                                telegram = telegram.SetFieldValue(TelegramFields.stateCode, ((string[])TaskList[0].MissionData[(int)dex]).GetFieldValue(TelegramFields.presetStateCode)); //May need to be changed when dealing with exceptions
                                controller.SendTelegram(telegram, true);
                            }
                        }
                    }
                    else //Single load being droppped from single pick
                    {
                        string telegram = ((string[])TaskList[0].MissionData[0]).CreateTelegramFromTelegram(TelegramTypes.TransportFinishedTelegram, true);
                        telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                        telegram = telegram.SetFieldValue(TelegramFields.source, ((string[])TaskList[0].MissionData[0]).GetFieldValue(TelegramFields.source));
                        telegram = telegram.SetFieldValue(TelegramFields.location, ((string[])TaskList[0].MissionData[0]).GetFieldValue(TelegramFields.destination));
                        telegram = telegram.SetFieldValue(TelegramFields.stateCode, ((string[])TaskList[0].MissionData[0]).GetFieldValue(TelegramFields.presetStateCode)); //May need to be changed when dealing with exception
                        controller.SendTelegram(telegram, true);
                    }
                }
                else if ((e._miniloadTask.Cycle == MiniloadCycle.PickPS && SendPSArrival) || e._miniloadTask.Cycle == MiniloadCycle.PickRack)
                {
                    //Log.Write(string.Format("A load or loads have been picked from the Pick Station"));
                    if (!string.IsNullOrEmpty(e._miniloadTask.TuIdentPos1))
                    {
                        string telegram = ((string[])TaskList[0].MissionData[(int)GetIndex(TaskList[0].MissionData, e._miniloadTask.TuIdentPos1)]).CreateTelegramFromTelegram(TelegramTypes.LocationArrivedTelegram, true);
                        telegram = telegram.SetFieldValue(TelegramFields.location, string.Format("{0}11", LHDName));
                        telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                        controller.SendTelegram(telegram, true);
                    }
                    if (!string.IsNullOrEmpty(e._miniloadTask.TuIdentPos2))
                    {
                        string telegram = ((string[])TaskList[0].MissionData[(int)GetIndex(TaskList[0].MissionData, e._miniloadTask.TuIdentPos2)]).CreateTelegramFromTelegram(TelegramTypes.LocationArrivedTelegram, true);
                        telegram = telegram.SetFieldValue(TelegramFields.location, string.Format("{0}12", LHDName));
                        telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                        controller.SendTelegram(telegram, true);
                    }
                }

                TaskList[0].HalfCycles.RemoveAt(0);
                if (TaskList[0].HalfCycles.Count == 0) //All half cycles for this task are complete
                {
                    TaskList.RemoveAt(0);
                    StartNewTask();
                }
                else //Or just send the next half cycle to the miniload
                {
                    theMiniload.StartMiniloadHalfCycle(TaskList[0].HalfCycles[0]);
                }

                StartNewTask();
            }
            else
            {
                Log.Write(string.Format("Miniload {0}: Cycle error controller and Miniload tasks are not aligned", theMiniload.Name));
            }
        }

        void theMiniload_OnMiniloadDropStationAvailableChanged(object sender, MiniloadDropStationAvailableChangedEventArgs e)
        {
            if (e._available)
            {
                StartNewTask();
            }
        }

        public void StartNewTask()
        {
            //Check if a new task can be started
            if (theMiniload.CurrentHalfCycle == null && TaskList.Count > 0)
            {
                MiniloadTask selectedTask = null;
                foreach (MiniloadTask task in TaskList)
                {
                    if (theMiniload.DropStationAvailable || task.TaskType != MiniloadTaskType.Retreival)
                    {
                        selectedTask = task;
                        break;
                    }
                }

                //Have found the task i'm going to do next, if it's a retreival task then find another retreival and combine the two
                MiniloadTask combinedTask = null;
                if (selectedTask != null && selectedTask.TaskType == MiniloadTaskType.Retreival && selectedTask.MissionData.Count == 1)
                {
                    foreach (MiniloadTask task in TaskList)
                    {
                        if (task != selectedTask && task.TaskType == MiniloadTaskType.Retreival && task.MissionData.Count == 1) //Have founnd two tasks, however it might not be the closest to the current task
                        {
                            combinedTask = task;
                            break;
                        }
                    }
                }

                if (selectedTask != null && combinedTask != null)
                {
                    //telegram message to be removed... as it should not be needed
                    MiniloadTask newTask = new MiniloadTask(new List<object>() { selectedTask.MissionData[0], combinedTask.MissionData[0] });

                    string posATuIdent = null; string posBTuIdent = null;
                    if (combinedTask.HalfCycles[0].RackSide == Side.Right) //selected LHD1, combined LHD2
                    {
                        posATuIdent = selectedTask.HalfCycles[0].TuIdentPos1 != null ? selectedTask.HalfCycles[0].TuIdentPos1 : selectedTask.HalfCycles[0].TuIdentPos2;
                        posBTuIdent = combinedTask.HalfCycles[0].TuIdentPos1 != null ? combinedTask.HalfCycles[0].TuIdentPos1 : combinedTask.HalfCycles[0].TuIdentPos2;
                        
                        selectedTask.HalfCycles[0].LHD = 1;
                        combinedTask.HalfCycles[0].LHD = 2;

                        selectedTask.HalfCycles[0].TuIdentPos1 = posATuIdent;
                        selectedTask.HalfCycles[0].TuIdentPos2 = null;
                        combinedTask.HalfCycles[0].TuIdentPos2 = posBTuIdent;
                        combinedTask.HalfCycles[0].TuIdentPos1 = null;

                    }
                    else //selected LHD2, combined LHD1
                    {
                        //deep copy?????
                        posBTuIdent = selectedTask.HalfCycles[0].TuIdentPos1 != null ? selectedTask.HalfCycles[0].TuIdentPos1 : selectedTask.HalfCycles[0].TuIdentPos2;
                        posATuIdent = combinedTask.HalfCycles[0].TuIdentPos1 != null ? combinedTask.HalfCycles[0].TuIdentPos1 : combinedTask.HalfCycles[0].TuIdentPos2;

                        selectedTask.HalfCycles[0].LHD = 2;
                        combinedTask.HalfCycles[0].LHD = 1;

                        selectedTask.HalfCycles[0].TuIdentPos2 = posBTuIdent;
                        selectedTask.HalfCycles[0].TuIdentPos1 = null;
                        combinedTask.HalfCycles[0].TuIdentPos1 = posATuIdent;
                        combinedTask.HalfCycles[0].TuIdentPos2 = null;
                    }

                    MiniloadHalfCycle dropToDS = new MiniloadHalfCycle() //Drop to the drop Station
                    {
                        Cycle = MiniloadCycle.DropDS,
                        TuIdentPos1 = posATuIdent,
                        TuIdentPos2 = posBTuIdent
                    };

                    newTask.HalfCycles.Add(selectedTask.HalfCycles[0]);
                    newTask.HalfCycles.Add(combinedTask.HalfCycles[0]);
                    newTask.HalfCycles.Add(dropToDS);

                    TaskList.Remove(selectedTask);
                    TaskList.Remove(combinedTask);
                    TaskList.Insert(0, newTask);
                    theMiniload.StartMiniloadHalfCycle(TaskList[0].HalfCycles[0]);
                    return;
                }

                if (selectedTask != null)
                {
                    if (selectedTask != TaskList[0]) //Move the selected task to the top of the list, then this becomes the current task
                    {
                        TaskList.Remove(selectedTask);
                        TaskList.Insert(0, selectedTask);
                    }
                    theMiniload.StartMiniloadHalfCycle(TaskList[0].HalfCycles[0]);
                }
            }
        }

        public void StartNewTask_OLD()
        {
            //Check if a new task can be started
            if (theMiniload.CurrentHalfCycle == null && TaskList.Count > 0)
            {
                MiniloadTask selectedTask = null;
                foreach (MiniloadTask task in TaskList)
                {
                    if (theMiniload.DropStationAvailable || task.TaskType != MiniloadTaskType.Retreival)
                    {
                        selectedTask = task;
                        break;
                    }
                }

                if (selectedTask != null)
                {
                    if (selectedTask != TaskList[0]) //Move the selected task to the top of the list, then this becomes the current task
                    {
                        TaskList.Remove(selectedTask);
                        TaskList.Insert(0, selectedTask);
                    }
                    theMiniload.StartMiniloadHalfCycle(TaskList[0].HalfCycles[0]);
                }
            }
        }

        void AddNewTasks(MiniloadTask newTask)
        {
            //If the crane isn't doing anything then start the new task
            TaskList.Add(newTask);
            StartNewTask();
        }

        public override void Dispose()
        {

        }

        void theMiniload_OnMiniloadReset(object sender, EventArgs e)
        {
            Reset();
        }

        public void Reset()
        {
            TaskList.Clear();
        }


        #region Helper Methods
        /// <summary>
        /// Returns the task type based on the source and destination of the load
        /// </summary>
        private MiniloadTaskType GetTaskType(string source, string destination)
        {
            if (source.Substring(0,13) == PickStationName.Substring(0,13) && destination != DropStationName)
            {
                return MiniloadTaskType.Storage;
            }
            else if (destination == DropStationName && source.Substring(0,13) != PickStationName.Substring(0,13))
            {
                return MiniloadTaskType.Retreival;
            }
            else if (source.Substring(0,13) == PickStationName.Substring(0,13) && destination == DropStationName)
            {
                return MiniloadTaskType.Reject;
            }
            return MiniloadTaskType.Relocation;
        }

        /// <summary>
        /// Returns the X distance that a load is to be dropped off at, can only be used with a multiple message as it is looking into the destination arrays
        /// </summary>
        /// <param name="telegramFields">MultipleStartTransportTelegram</param>
        /// <param name="load">The load to be checked</param>
        /// <returns>X distance that the load is to be dropped off at</returns>
        private float GetXLoc(string[] telegramFields, ATCCaseLoad load)
        {
            string MissionTUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
            string Mission0TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]");
            string Mission1TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]");

            if (MissionTUIdent == load.TUIdent)
            {
                return GetXLoc(telegramFields.GetFieldValue(TelegramFields.destination));
            }
            else if (Mission0TUIdent == load.TUIdent)
            {
                return GetXLoc(telegramFields.GetFieldValue(TelegramFields.destinations, "[0]"));
            }
            else if (Mission1TUIdent == load.TUIdent)
            {
                return GetXLoc(telegramFields.GetFieldValue(TelegramFields.destinations, "[1]"));
            }
            return 0;
        }

        /// <summary>
        /// Get the load X Location position from either the source or destination field (must be a valid bin location)
        /// </summary>
        /// <param name="field">Either source or destination field of a telegram</param>
        /// <returns>float position that the miniload should travel to</returns>
        private float GetXLoc(string field)
        {
            string xLoc = BaseATCController.GetBinLocField(field, BinLocFields.XLoc);
            string rast = BaseATCController.GetBinLocField(field, BinLocFields.RasterType);
            string posi = BaseATCController.GetBinLocField(field, BinLocFields.RasterPos);
            return theMiniload.CalculateLengthFromXLoc(xLoc, rast, posi);
        }

        /// <summary>
        /// Returns the Y distance that a load is to be dropped off at, can only be used with a multiple message as it is looking into the destination arrays
        /// </summary>
        /// <param name="telegramFields">MultipleStartTransportTelegram</param>
        /// <param name="load">The load to be checked</param>
        /// <returns>Y distance that the load is to be dropped off at</returns>
        private float GetYLoc(string[] telegramFields, ATCCaseLoad load)
        {
            string MissionTUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
            string Mission0TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]");
            string Mission1TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]");

            if (MissionTUIdent == load.TUIdent)
            {
                return GetYLoc(telegramFields.GetFieldValue(TelegramFields.destination));
            }
            else if (Mission0TUIdent == load.TUIdent)
            {
                return GetYLoc(telegramFields.GetFieldValue(TelegramFields.destinations, "[0]"));
            }
            else if (Mission1TUIdent == load.TUIdent)
            {
                return GetYLoc(telegramFields.GetFieldValue(TelegramFields.destinations, "[1]"));
            }
            return 0;
        }

        /// <summary>
        /// Get the load Y Location position from either the source or destination field (must be a valid bin location)
        /// </summary>
        /// <param name="field">Either source or destination field of a telegram</param>
        /// <returns>float position that the miniload should travel to</returns>
        private float GetYLoc(string field)
        {
            string yLoc = BaseATCController.GetBinLocField(field, BinLocFields.YLoc);
            return theMiniload.CalculateHeightFromYLoc(yLoc);
        }

        private Side? GetSide(string field)
        {
            string side = BaseATCController.GetBinLocField(field, BinLocFields.Side);

            if (side.ToLower() == "l" || side == "1")
            {
                return Side.Left;
            }
            else if (side.ToLower() == "r" || side == "2")
            {
                return Side.Right;
            }

            return null;
        }

        /// <summary>
        /// Returns the side that a load is to be dropped off at, can only be used with a multiple message as it is looking into the destination arrays
        /// </summary>
        /// <param name="telegramFields">MultipleStartTransportTelegram</param>
        /// <param name="load">The load to be checked</param>
        /// <returns>Side that the load is to be dropped off at</returns>
        private Side? GetSide(string[] telegramFields, ATCCaseLoad load)
        {
            string MissionTUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
            string Mission0TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]");
            string Mission1TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]");

            if (MissionTUIdent == load.TUIdent)
            {
                return GetSide(telegramFields.GetFieldValue(TelegramFields.destination));
            }
            else if (Mission0TUIdent == load.TUIdent)
            {
                return GetSide(telegramFields.GetFieldValue(TelegramFields.destinations, "[0]"));
            }
            else if (Mission1TUIdent == load.TUIdent)
            {
                return GetSide(telegramFields.GetFieldValue(TelegramFields.destinations, "[1]"));
            }
            return null;
        }

        private int GetDepth(string field)
        {
            string yLoc = BaseATCController.GetBinLocField(field, BinLocFields.Depth);
            int result;
            if (int.TryParse(yLoc, out result))
            {
                return result;
            }
            return 0;
        }

        private int GetDepth(string[] telegramFields, ATCCaseLoad load)
        {
            string MissionTUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
            string Mission0TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]");
            string Mission1TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]");

            if (MissionTUIdent == load.TUIdent)
            {
                return GetDepth(telegramFields.GetFieldValue(TelegramFields.destination));
            }
            else if (Mission0TUIdent == load.TUIdent)
            {
                return GetDepth(telegramFields.GetFieldValue(TelegramFields.destinations, "[0]"));
            }
            else if (Mission1TUIdent == load.TUIdent)
            {
                return GetDepth(telegramFields.GetFieldValue(TelegramFields.destinations, "[1]"));
            }
            return 0;
        }

        /// <summary>
        /// Finds the index of a load within a message from the tuIdent
        /// </summary>
        /// <param name="telegramFields">MultipleStartTransprtTelegram</param>
        /// <param name="tuIdent"></param>
        /// <returns>Index as a string e.g '[0]'</returns>
        private string GetIndex(string[] telegramFields, string tuIdent)
        {
            string Mission0TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]");
            string Mission1TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[1]");

            if (Mission0TUIdent == tuIdent)
            {
                return "[0]";
            }
            else if (Mission1TUIdent == tuIdent)
            {
                return "[1]";
            }
            return null;
        }

        private int? GetIndex(List<object> MissionData, string tuIdent)
        {
            string Mission0TUIdent = ((string[])MissionData[0]).GetFieldValue(TelegramFields.tuIdent);
            string Mission1TUIdent = MissionData.Count == 2 ? ((string[])MissionData[1]).GetFieldValue(TelegramFields.tuIdent) : null;

            if (tuIdent == Mission0TUIdent)
            {
                return 0;
            }
            else if (tuIdent == Mission1TUIdent)
            {
                return 1;
            }
            return null;
        }
        #endregion

        #region UserInterface
        [DisplayName("Pick Station Name")]
        [Description("Name of the pick station as received in the messages from ATC")]
        public string PickStationName
        {
            get { return miniloadATCInfo.pickStationName; }
            set { miniloadATCInfo.pickStationName = value; }
        }

        [DisplayName("Drop Station Name")]
        [Description("Name of the drop station as received in the messages from ATC")]
        public string DropStationName
        {
            get { return miniloadATCInfo.dropStationName; }
            set { miniloadATCInfo.dropStationName = value; }
        }

        [DisplayName("LHD Name")]
        [Description("Name of the LHD with the last 2 characters omitted, used to define which LHD the load has been picked to")]
        public string LHDName
        {
            get { return miniloadATCInfo.lHDName; }
            set { miniloadATCInfo.lHDName = value; }
        }

        [DisplayName("Pick Station TUNO")]
        [Description("Send TUNO (LocationArrivedTelegram) on LHD when picking up from the pick station")]
        public bool SendPSArrival
        {
            get { return miniloadATCInfo.sendPSArrival; }
            set { miniloadATCInfo.sendPSArrival = value; }
        }
        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(MiniloadATCInfo))]
    public class MiniloadATCInfo : ProtocolInfo
    {
        public string pickStationName;
        public string dropStationName;
        public string lHDName;
        public bool sendPSArrival;
    }
}
