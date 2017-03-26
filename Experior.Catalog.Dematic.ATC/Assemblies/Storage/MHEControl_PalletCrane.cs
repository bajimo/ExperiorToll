using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Dematic.ATC;
using Experior.Catalog.Dematic.ATC;
using Experior.Catalog.Dematic.ATC.Assemblies;
using Experior.Catalog.Dematic.ATC.Assemblies.Storage;
using Experior.Catalog.Dematic.Storage.PalletCrane.Assemblies;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using Experior.Catalog.Dematic.Pallet.Assemblies;

namespace Experior.Catalog.Assemblies.Storage
{
    class MHEControl_PalletCrane : MHEControl
    {
        private readonly PalletCrane thePalletCrane;
        private readonly MHEController_PalletCrane controller;
        private readonly PalletCraneATCInfo palletCraneAtcInfo;
        private readonly List<PalletCraneTask> taskList = new List<PalletCraneTask>();

        public MHEControl_PalletCrane(PalletCraneATCInfo info, PalletCrane palletCrane)
        {
            Info = info;  // set this to save properties 
            palletCraneAtcInfo = info;
            thePalletCrane = palletCrane;
            controller = ((MHEController_PalletCrane)thePalletCrane.Controller);

            //Subscribe to all of the palletCrane events here 
            thePalletCrane.OnPalletCraneTaskComplete += PalletCraneTaskComplete;
            thePalletCrane.OnPalletCraneDropStationAvailableChanged += DropStationAvailableChanged;
            thePalletCrane.OnPalletCraneReset += PalletCraneReset;
            thePalletCrane.OnPalletArrivedAtPickStation += PalletArrivedAtPickStation;
        }

        private void PalletArrivedAtPickStation(object sender, PalletStationEventArgs e)
        {        
            if (SendPSRequest)
            {
                //send TransportRequestTelegram
                var pallet = e.Load as IATCLoadType;        
                if (pallet != null)
                {
                    //Update location
                    pallet.Location = e.PickStationName;
                    //Send Transport request
                    controller.SendTransportRequestTelegram(pallet);
                }
                else
                {
                    Log.Write("Cannot send TransportRequestTelegram: Load arriving at Pick station is not a IATCLoadType");
                }       
            }
        }

        public void StartTransportTelegramReceived(string[] telegramFields)
        {
            //Create palletCrane tasklist from message
            PalletCraneTask newTask = new PalletCraneTask(new List<object> { telegramFields }); //Convert telegram to TelegramData

            //is it a storage, retreival, relocation or reject task type
            string source = telegramFields.GetFieldValue(TelegramFields.source);
            string destination = telegramFields.GetFieldValue(TelegramFields.destination);

            newTask.TaskType = GetTaskType(source, destination);

            if (newTask.TaskType == PalletCraneTaskType.Storage || newTask.TaskType == PalletCraneTaskType.Reject) //Store 1 in the racking from pick station
            {
                var pallet = thePalletCrane.PickStationPallet(source) as ATCEuroPallet;

                //This will only work if the pick station is on the RHS of the PalletCrane, Pickstation MergeSide set as left
                if (pallet == null || pallet.TUIdent != telegramFields.GetFieldValue(TelegramFields.tuIdent))
                {
                    Log.Write(string.Format("PalletCrane {0}: Loads at pick station and 'StartTransportTelegram' from ATC do not match, telegram ignored", thePalletCrane.Name));
                    return;
                }

                controller.UpDateLoadParameters(telegramFields, pallet);

                //Create the palletCrane half cycles
                float x, y;
                thePalletCrane.GetPickStationLocation(source, out x, out y);
                var pickFromPs = new PalletCraneHalfCycle //Pick from pick station
                {
                    Cycle = PalletCraneCycle.PickPS,
                    TuIdent = pallet.TUIdent,
                    Length = x - thePalletCrane.LHDWidth / 2,
                    Height = y,
                    StationName = source
                };
                newTask.HalfCycles.Add(pickFromPs);

                if (newTask.TaskType == PalletCraneTaskType.Storage)
                {
                    var palletDrop = new PalletCraneHalfCycle //Drop the load in the racking
                    {
                        Cycle = PalletCraneCycle.DropRack,
                        Lhd = 1,
                        Length = GetXLoc(telegramFields, pallet),
                        Height = GetYLoc(telegramFields, pallet),
                        Depth = GetDepth(telegramFields, pallet),
                        RackSide = GetSide(telegramFields, pallet),
                    };
                    newTask.HalfCycles.Add(palletDrop);
                }

                else if (newTask.TaskType == PalletCraneTaskType.Reject)
                {
                    thePalletCrane.GetDropStationLocation(destination, out x, out y);
                    var dropToDs = new PalletCraneHalfCycle //Drop to the drop Station
                    {
                        Cycle = PalletCraneCycle.DropDS,
                        Length = x - thePalletCrane.LHDWidth / 2,
                        Height = y,
                        StationName = destination
                    };
                    newTask.HalfCycles.Add(dropToDs);
                }
            }
            else if (newTask.TaskType == PalletCraneTaskType.Retrieval || newTask.TaskType == PalletCraneTaskType.Relocation)
            {
                //Create the palletCrane half cycles
                var palletData = controller.CreateATCPalletData(telegramFields);
                string loadSource = telegramFields.GetFieldValue(TelegramFields.source);
                string loadTuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
                string loadDestination = telegramFields.GetFieldValue(TelegramFields.destination);
                int lhd = 1;

                PalletCraneHalfCycle loadPick = new PalletCraneHalfCycle
                {
                    Cycle = PalletCraneCycle.PickRack,
                    Lhd = lhd,
                    Length = GetXLoc(loadSource),
                    Height = GetYLoc(loadSource),
                    Depth = GetDepth(loadSource),
                    RackSide = GetSide(loadSource),
                    TuIdent = loadTuIdent,
                    PalletData = palletData
                };
                newTask.HalfCycles.Add(loadPick);

                if (newTask.TaskType == PalletCraneTaskType.Retrieval)
                {
                    float x, y;
                    thePalletCrane.GetDropStationLocation(destination, out x, out y);
                    var dropToDs = new PalletCraneHalfCycle //Drop to the drop Station
                    {
                        Cycle = PalletCraneCycle.DropDS,
                        TuIdent = loadTuIdent,
                        Length = x - thePalletCrane.LHDWidth / 2,
                        Height = y,
                        StationName = destination
                    };
                    newTask.HalfCycles.Add(dropToDs);
                }
                else if (newTask.TaskType == PalletCraneTaskType.Relocation)
                {
                    var loadDrop = new PalletCraneHalfCycle
                    {
                        Cycle = PalletCraneCycle.DropRack,
                        Lhd = lhd,
                        Length = GetXLoc(loadDestination),
                        Height = GetYLoc(loadDestination),
                        Depth = GetDepth(loadDestination),
                        RackSide = GetSide(loadDestination),
                        TuIdent = loadTuIdent,
                        PalletData = palletData
                    };
                    newTask.HalfCycles.Add(loadDrop);
                }
            }

            AddNewTasks(newTask);
        }

        //public void StartMultipleTransportTelegramReceived(string[] telegramFields)
        //{
        //    //Create palletCrane tasklist from message
        //    List<string> indexMatches = new List<string>();
        //    List<string> telegrams = Telegrams.DeMultaplise(telegramFields, TelegramTypes.StartMultipleTransportTelegram, out indexMatches);
        //    for (int i = 0; i < telegrams.Count; i++)
        //    {
        //        telegrams[i] = telegrams[i].Replace(string.Format("s{0}", indexMatches[i]), "");
        //    }

        //    PalletCraneTask newTasks = new PalletCraneTask(new List<object> { telegrams[0].Split(','), telegrams[1].Split(',') }); //Convert telegram to TelegramData
        //    //PalletCraneTask newTasks = new PalletCraneTask(telegramFields);

        //    int telegramCount = telegramFields.ArrayCount();
        //    if (telegramCount == 2)
        //    {
        //        //is it a storage, retreival, relocation or reject task type
        //        string source = telegramFields.GetFieldValue(TelegramFields.sources, "[0]");
        //        string destination = telegramFields.GetFieldValue(TelegramFields.destinations, "[0]");

        //        newTasks.TaskType = GetTaskType(source, destination);

        //        if (newTasks.TaskType == PalletCraneTaskType.Storage || newTasks.TaskType == PalletCraneTaskType.Reject) //Store 2 in the racking from pick station
        //        {
        //            ATCCaseLoad pallet = thePalletCrane.PickStationPallet() as ATCCaseLoad;

        //            if (pallet != null && pallet.TUIdent == telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]"))
        //            {
        //                //Now we know what data to put onto what load
        //                controller.UpDateLoadParameters(telegramFields, pallet, "[0]");
        //            }
        //            else
        //            {
        //                //There is a problem
        //                Log.Write(string.Format("PalletCrane {0}: Loads at pick station and 'MultipleStartTransportTelegram' from ATC do not match, telegram ignored", thePalletCrane.Name));
        //                return;
        //            }

        //            var pickFromPs = new PalletCraneHalfCycle() //Pick from pick station
        //            {
        //                Cycle = PalletCraneCycle.PickPS,
        //                TuIdent = pallet.TUIdent,
        //            };
        //            newTasks.HalfCycles.Add(pickFromPs);

        //            if (newTasks.TaskType == PalletCraneTaskType.Storage)
        //            {
        //                var drop = new PalletCraneHalfCycle() //Drop the load
        //                {
        //                    Cycle = PalletCraneCycle.DropRack,
        //                    Lhd = 1,
        //                    Length = GetXLoc(telegramFields, pallet),
        //                    Height = GetYLoc(telegramFields, pallet),
        //                    Depth = GetDepth(telegramFields, pallet),
        //                    RackSide = GetSide(telegramFields, pallet),
        //                    TuIdent = pallet.TUIdent
        //                };

        //                if (drop.RackSide == null)
        //                {
        //                    Log.Write(string.Format("PalletCrane {0}: 'MultipleStartTransportTelegram' cannot resolve drop side, telegram ignored", thePalletCrane.Name));
        //                    return;
        //                }
        //                //We now know which loads are being sent to which side so that we can schedule the palletCrane accordingly

        //                newTasks.HalfCycles.Add(drop);
        //            }
        //            else if (newTasks.TaskType == PalletCraneTaskType.Reject)
        //            {
        //                var dropToDS = new PalletCraneHalfCycle() //Drop to the drop Station
        //                {
        //                    Cycle = PalletCraneCycle.DropDS,
        //                    TuIdent = pallet.TUIdent,
        //                };
        //                newTasks.HalfCycles.Add(dropToDS);
        //            }
        //        }
        //        else if (newTasks.TaskType == PalletCraneTaskType.Retrieval)
        //        {
        //            //Initially i need to know which load i am going to pick first
        //            string posASource = telegramFields.GetFieldValue(TelegramFields.sources, "[0]");

        //            string posATuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdents, "[0]");

        //            Side? posASide = GetSide(posASource);
        //            float posAXLoc = GetXLoc(posASource);

        //            if (posASide == null || posATuIdent == null)
        //            {
        //                //There is a problem
        //                Log.Write(string.Format("PalletCrane {0}: Error processing drop missions from 'MultipleStartTransportTelegram', telegram ignored", thePalletCrane.Name));
        //                return;
        //            }

        //            //Create the case data, this will be used by the palletCrane to create the load (from the controller)
        //            ATCCaseData pos1CaseData = controller.CreateATCCaseData(telegramFields, "[0]");

        //            var palletPick = new PalletCraneHalfCycle()
        //            {
        //                Cycle = PalletCraneCycle.PickRack,
        //                Lhd = 1,
        //                Length = GetXLoc(posASource),
        //                Height = GetYLoc(posASource),
        //                Depth = GetDepth(posASource),
        //                RackSide = posASide,
        //                TuIdent = posATuIdent,
        //                PalletData = pos1CaseData
        //            };

        //            newTasks.HalfCycles.Add(palletPick);

        //            var dropToDs = new PalletCraneHalfCycle() //Drop to the drop Station
        //            {
        //                Cycle = PalletCraneCycle.DropDS,
        //                TuIdent = posATuIdent,

        //            };

        //            newTasks.HalfCycles.Add(dropToDs);
        //        }
        //        else if (newTasks.TaskType == PalletCraneTaskType.Relocation)
        //        {
        //            Log.Write(string.Format("PalletCrane {0}: cannot generate double relocation missions from 'MultipleStartTransportTelegram', telegram ignored", thePalletCrane.Name), Color.Red);
        //            return;
        //        }
        //    }
        //    else if (telegramCount == 1)
        //    {
        //        //Not sure if this is needed or not, why send a multiple message with only 1 load in it?
        //    }

        //    AddNewTasks(newTasks);
        //}

        public void RequestStateTelegramReceived(string[] telegramFields)
        {
            string telegram = telegramFields.CreateTelegramFromTelegram(TelegramTypes.StateChangedTelegram);
            telegram = telegram.SetFieldValue(TelegramFields.newState, "AU");
            controller.SendTelegram(telegram, true);
        }

        void PalletCraneTaskComplete(object sender, PalletCraneTaskCompleteEventArgs e)
        {
            if (taskList[0].HalfCycles[0] == e.PalletCraneTask)
            {
                //Need to send a message to the WMS at this point depending on the cycle
                if (e.PalletCraneTask.Cycle == PalletCraneCycle.DropRack || e.PalletCraneTask.Cycle == PalletCraneCycle.DropDS)
                {
                    string telegram = ((string[])taskList[0].MissionData[0]).CreateTelegramFromTelegram(TelegramTypes.TransportFinishedTelegram, true);
                    telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                    telegram = telegram.SetFieldValue(TelegramFields.source, ((string[])taskList[0].MissionData[0]).GetFieldValue(TelegramFields.source));
                    telegram = telegram.SetFieldValue(TelegramFields.location, ((string[])taskList[0].MissionData[0]).GetFieldValue(TelegramFields.destination));
                    telegram = telegram.SetFieldValue(TelegramFields.stateCode, ((string[])taskList[0].MissionData[0]).GetFieldValue(TelegramFields.presetStateCode)); //May need to be changed when dealing with exception
                    controller.SendTelegram(telegram, true);

                }
                else if (e.PalletCraneTask.Cycle == PalletCraneCycle.PickPS)
                {
                    if (SendPSArrival)
                    //Log.Write(string.Format("A load or loads have been picked from the Pick Station"));
                    {
                        if (!string.IsNullOrEmpty(e.PalletCraneTask.TuIdent))
                        {
                            string telegram = ((string[])taskList[0].MissionData[0]).CreateTelegramFromTelegram(TelegramTypes.LocationArrivedTelegram, true);
                            telegram = telegram.SetFieldValue(TelegramFields.location, LHDName);
                            telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                            controller.SendTelegram(telegram, true);
                        }
                    }
                    if (SendPSLeft)
                    {
                        if (!string.IsNullOrEmpty(e.PalletCraneTask.TuIdent))
                        {
                            string telegram = ((string[])taskList[0].MissionData[0]).CreateTelegramFromTelegram(TelegramTypes.LocationLeftTelegram, true);
                            telegram = telegram.SetFieldValue(TelegramFields.location, taskList[0].HalfCycles[0].StationName);
                            telegram = telegram.SetFieldValue(TelegramFields.mts, controller.Name);
                            controller.SendTelegram(telegram, true);
                        }
                    }
                }

                taskList[0].HalfCycles.RemoveAt(0);
                if (taskList[0].HalfCycles.Count == 0) //All half cycles for this task are complete
                {
                    taskList.RemoveAt(0);
                    StartNewTask();
                }
                else //Or just send the next half cycle to the palletCrane
                {
                    thePalletCrane.StartPalletCraneHalfCycle(taskList[0].HalfCycles[0]);
                }

                StartNewTask();
            }
            else
            {
                Log.Write(string.Format("PalletCrane {0}: Cycle error controller and PalletCrane tasks are not aligned", thePalletCrane.Name));
            }
        }

        void DropStationAvailableChanged(object sender, PalletCraneDropStationAvailableChangedEventArgs e)
        {
            if (e.Available)
            {
                StartNewTask();
            }
        }

        public void StartNewTask()
        {
            //Check if a new task can be started
            if (thePalletCrane.CurrentHalfCycle == null && taskList.Count > 0)
            {
                PalletCraneTask selectedTask = null;
                foreach (var task in taskList)
                {
                    if (thePalletCrane.DropStationAvailable || task.TaskType != PalletCraneTaskType.Retrieval)
                    {
                        selectedTask = task;
                        break;
                    }
                }

                if (selectedTask != null)
                {
                    if (selectedTask != taskList[0]) //Move the selected task to the top of the list, then this becomes the current task
                    {
                        taskList.Remove(selectedTask);
                        taskList.Insert(0, selectedTask);
                    }
                    thePalletCrane.StartPalletCraneHalfCycle(taskList[0].HalfCycles[0]);
                }
            }
        }

        void AddNewTasks(PalletCraneTask newTask)
        {
            //If the crane isn't doing anything then start the new task
            taskList.Add(newTask);
            StartNewTask();
        }

        public override void Dispose()
        {
            taskList.Clear();
            if (thePalletCrane != null)
            {
                thePalletCrane.OnPalletCraneTaskComplete -= PalletCraneTaskComplete;
                thePalletCrane.OnPalletCraneDropStationAvailableChanged -= DropStationAvailableChanged;
                thePalletCrane.OnPalletCraneReset -= PalletCraneReset;
                thePalletCrane.OnPalletArrivedAtPickStation -= PalletArrivedAtPickStation;
            }
        }

        void PalletCraneReset(object sender, EventArgs e)
        {
            Reset();
        }

        public void Reset()
        {
            taskList.Clear();
        }


        #region Helper Methods
        /// <summary>
        /// Returns the task type based on the source and destination of the load
        /// </summary>
        private PalletCraneTaskType GetTaskType(string source, string destination)
        {
            if (thePalletCrane.PickStations.Any(p => p.Name == source) && thePalletCrane.DropStations.All(d => d.Name != destination))
            {
                return PalletCraneTaskType.Storage;
            }
            if (thePalletCrane.PickStations.All(p => p.Name != source) && thePalletCrane.DropStations.Any(d => d.Name == destination))
            {
                return PalletCraneTaskType.Retrieval;
            }
            if (thePalletCrane.PickStations.Any(p => p.Name == source) && thePalletCrane.DropStations.Any(d => d.Name == destination))
            {
                return PalletCraneTaskType.Reject;
            }
            return PalletCraneTaskType.Relocation;
        }

        /// <summary>
        /// Returns the X distance that a load is to be dropped off at, can only be used with a multiple message as it is looking into the destination arrays
        /// </summary>
        /// <param name="telegramFields">MultipleStartTransportTelegram</param>
        /// <param name="load">The load to be checked</param>
        /// <returns>X distance that the load is to be dropped off at</returns>
        private float GetXLoc(string[] telegramFields, IATCLoadType load)
        {
            string missionTuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);

            if (missionTuIdent == load.TUIdent)
            {
                return GetXLoc(telegramFields.GetFieldValue(TelegramFields.destination));
            }

            return 0;
        }

        /// <summary>
        /// Get the load X Location position from either the source or destination field (must be a valid bin location)
        /// </summary>
        /// <param name="field">Either source or destination field of a telegram</param>
        /// <returns>float position that the palletCrane should travel to</returns>
        private float GetXLoc(string field)
        {
            string xLoc = BaseATCController.GetBinLocField(field, BinLocFields.XLoc);
            string rast = BaseATCController.GetBinLocField(field, BinLocFields.RasterType);
            string posi = BaseATCController.GetBinLocField(field, BinLocFields.RasterPos);
            return thePalletCrane.CalculateLengthFromXLoc(xLoc, rast, posi);
        }

        /// <summary>
        /// Returns the Y distance that a load is to be dropped off at, can only be used with a multiple message as it is looking into the destination arrays
        /// </summary>
        /// <param name="telegramFields">MultipleStartTransportTelegram</param>
        /// <param name="load">The load to be checked</param>
        /// <returns>Y distance that the load is to be dropped off at</returns>
        private float GetYLoc(string[] telegramFields, IATCLoadType load)
        {
            string missionTuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);

            if (missionTuIdent == load.TUIdent)
            {
                return GetYLoc(telegramFields.GetFieldValue(TelegramFields.destination));
            }

            return 0;
        }

        /// <summary>
        /// Get the load Y Location position from either the source or destination field (must be a valid bin location)
        /// </summary>
        /// <param name="field">Either source or destination field of a telegram</param>
        /// <returns>float position that the palletCrane should travel to</returns>
        private float GetYLoc(string field)
        {
            string yLoc = BaseATCController.GetBinLocField(field, BinLocFields.YLoc);
            return thePalletCrane.CalculateHeightFromYLoc(yLoc);
        }

        private Side? GetSide(string field)
        {
            string side = BaseATCController.GetBinLocField(field, BinLocFields.Side);

            if (side.ToLower() == "l" || side == "1")
            {
                return Side.Left;
            }
            if (side.ToLower() == "r" || side == "2")
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
        private Side? GetSide(string[] telegramFields, IATCLoadType load)
        {
            string missionTuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);

            if (missionTuIdent == load.TUIdent)
            {
                return GetSide(telegramFields.GetFieldValue(TelegramFields.destination));
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

        private int GetDepth(string[] telegramFields, IATCLoadType load)
        {
            string missionTuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);

            if (missionTuIdent == load.TUIdent)
            {
                return GetDepth(telegramFields.GetFieldValue(TelegramFields.destination));
            }

            return 0;
        }

        #endregion

        #region UserInterface

        [DisplayName(@"LHD Name")]
        [Description("Name of the LHD")]
        public string LHDName
        {
            get { return palletCraneAtcInfo.LHdName; }
            set { palletCraneAtcInfo.LHdName = value; }
        }

        [DisplayName(@"Pick Station TUNO")]
        [Description("Send TUNO (LocationArrivedTelegram) on LHD when picking up from the pick station")]
        public bool SendPSArrival
        {
            get { return palletCraneAtcInfo.SendPsArrival; }
            set { palletCraneAtcInfo.SendPsArrival = value; }
        }

        [DisplayName(@"Pick Station TULL")]
        [Description("Send TULL (LocationLeftTelegram) on Pick Station when picking up from the pick station")]
        public bool SendPSLeft
        {
            get { return palletCraneAtcInfo.SendPSLeft; }
            set { palletCraneAtcInfo.SendPSLeft = value; }
        }

        [DisplayName(@"Pick Station TUDR")]
        [Description("Send TUDR (TransportRequestTelegram) on Pick Station when load arrives")]
        public bool SendPSRequest
        {
            get { return palletCraneAtcInfo.SendPSRequest; }
            set { palletCraneAtcInfo.SendPSRequest = value; }
        }

        [DisplayName(@"Drop Station load wait")]
        [Description("Set load LoadWaitingForWCS to true when arriving at DS.")]
        public bool DSPalletWait
        {
            get { return palletCraneAtcInfo.DSPalletWait; }
            set { palletCraneAtcInfo.DSPalletWait = value; }
        }
        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(PalletCraneATCInfo))]
    public class PalletCraneATCInfo : ProtocolInfo
    {
        public string LHdName;
        public bool SendPsArrival;
        public bool SendPSLeft;
        public bool SendPSRequest;
        public bool DSPalletWait;
    }
}