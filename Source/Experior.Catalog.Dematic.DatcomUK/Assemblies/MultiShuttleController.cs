using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Experior.Catalog.Assemblies;
using Experior.Catalog.Dematic.Case;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Routes;
using System.Drawing;
using Experior.Dematic.Storage.Base;
using Experior.Core.Parts;
using System.ComponentModel;
using Experior.Core.Properties;
using Microsoft.DirectX;
using Experior.Core.Communication;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Storage.Assemblies
{    
        /// <summary>
        /// See 41995553 Dematic Multi-Shuttle Control Principles v3.3.doc
        /// The controller will control shuttles, elevators and pick stations.
        /// The controller handles communication with the Material Flow Host (MFH).
        /// All Dematic telegram handling is located here.
        /// </summary>
        public class MultiShuttleController : Assembly, IMultiShuttleController, IController
        {
            public delegate string GetSingleDataSet(MultiShuttleController controller, Case_Load caseload);
            public delegate string GetDoubleDataSet(MultiShuttleController controller, Case_Load caseload2, Case_Load caseload1);
            public delegate string GetPickstationDataSet(MultiShuttleController controller, Case_Load caseload2, Case_Load caseload1, string pickStation, string position2Ulid, string position2UlBarcode, string position2UlHeight, string position1Ulid, string position1UlBarcode, string position1UlHeight);
            public GetSingleDataSet GetSingleMissionDataSetBody;
            public GetDoubleDataSet GetDoubleMissionDataSetBody;
            public GetPickstationDataSet GetPickStationDataSetBody;
            public enum MSCSuppressStates { Never, Always}; 

            private readonly List<DematicMultiShuttle> multishuttles = new List<DematicMultiShuttle>();
            private readonly MultiShuttleControllerInfo controllerinfo;
            private readonly Cube plcCube;
            private PLCStates plcState;
            private readonly Text3D plcNum;
            private Core.Communication.TCPIP.Connection connectionRecieving;
            private Core.Communication.TCPIP.Connection connectionSending;
            private char separationCharacter = ',';
            private string lastSentTelegram = string.Empty;

            public MultiShuttleController(MultiShuttleControllerInfo info) : base(info)
            {
                controllerinfo = info;

                plcCube = new Cube(Color.Black, 1.0f, 1.0f, 1.0f);
                AddPart(plcCube);
                //Setting to 'dummy' state first and then to unknown  in order to get the correct color
                PLCState = PLCStates.Auto_No_Move_03;
                PLCState = PLCStates.Unknown_00;

                Font font = new Font("Times New Roman", 0.8f, FontStyle.Regular, GraphicsUnit.Pixel);
                plcNum = new Text3D(Color.Red, 0.8f, 0.2f, font);
                plcNum.Text = PLCID;
                AddPart(plcNum, plcCube.LocalPosition + new Vector3(+0.3f, plcCube.Height / 2, -0.3f));
                plcNum.LocalPitch = (float)Math.PI / 2;
                plcNum.LocalRoll = -(float)Math.PI / 2;
                plcNum.LocalYaw = (float)Math.PI;

                CreateConnections();

                DematicMultiShuttle.AllControllers.Add(this);
            }

            #region Communication

            private void CreateConnections()
            {
                //Unsubscribe from old connection
                if (connectionRecieving != null)
                {
                    connectionRecieving.OnTelegramReceived -= Connection_TelegramReceived;
                    connectionRecieving.OnConnected -= connection_Connected;
                    connectionRecieving.OnDisconnected -= connection_DisConnected;
                }
                if (connectionSending != null)
                {
                    connectionSending.OnTelegramReceived -= Connection_TelegramReceived;
                    connectionSending.OnConnected -= connection_Connected;
                    connectionSending.OnDisconnected -= connection_DisConnected;
                }

                //Find connection
                connectionRecieving = (Core.Communication.TCPIP.Connection)Connection.Items.Values.FirstOrDefault(c => c.Name == controllerinfo.ConnectionNameRecieving);
                connectionSending = (Core.Communication.TCPIP.Connection)Connection.Items.Values.FirstOrDefault(c => c.Name == controllerinfo.ConnectionNameSending);

                //Subscribe to new connection
                if (connectionRecieving != null)
                {
                    connectionRecieving.OnTelegramReceived += Connection_TelegramReceived;
                    connectionRecieving.OnConnected += connection_Connected;
                    connectionRecieving.OnDisconnected += connection_DisConnected;
                    if (connectionRecieving.Server)
                        connectionRecieving.AutoConnect = true;
                    if (!string.IsNullOrWhiteSpace(connectionRecieving.MVTSeparator) && connectionRecieving.MVTSeparator.Length == 1)
                        separationCharacter = connectionRecieving.MVTSeparator.ToCharArray()[0];
                }
                if (connectionSending != null)
                {
                    connectionSending.OnTelegramReceived += Connection_TelegramReceived;
                    connectionSending.OnConnected += connection_Connected;
                    connectionSending.OnDisconnected += connection_DisConnected;
                    if (connectionSending.Server)
                        connectionSending.AutoConnect = true;
                }
            }

            private delegate void connection_TelegramReceivedEvent(Connection sender, string telegram);

            void connection_DisConnected(Connection sender)
            {
                plcNum.Color = Color.Red;  // PLC object text
                Core.Environment.Log.Write(DateTime.Now + " " + Name + " connection dropped", Color.Red);
                PLCState = PLCStates.Unknown_00;
                lastSentTelegram = "";
            }

            void connection_Connected(Connection sender)
            {
                lastSentTelegram = "";
                if (connectionRecieving != null && connectionRecieving.State != State.Connected && !connectionRecieving.Server)
                {
                    connectionRecieving.EstablishConnection();
                    return;
                }

                if (connectionSending != null && connectionSending.State != State.Connected && !connectionSending.Server)
                {
                    connectionSending.EstablishConnection();
                    return;
                }

                if (connectionSending != null && connectionSending.State == State.Connected && connectionRecieving != null && connectionRecieving.State == State.Connected)
                {
                    plcNum.Color = Color.LightGreen;  // PLC object text
                    Core.Environment.Log.Write(DateTime.Now + " " + Name + " connection established", Color.DarkGreen);
                }
            }

            private void Connection_TelegramReceived(Connection sender, string telegram)
            {
                lastSentTelegram = "";
                if (InvokeRequired)
                {
                    Core.Environment.InvokeEvent(new connection_TelegramReceivedEvent(Connection_TelegramReceived), sender, telegram);
                    return;
                }

                if (sender == connectionRecieving)
                    TelegramRecieved(sender, telegram);
                else
                    AckTelegramRecieved(sender, telegram);

            }

            #endregion

            #region Telegram handling

            private void TelegramRecieved(Connection sender, string telegram)
            {
                try
                {
                    LogMessage(DateTime.Now + " MSC<MFH: " + PLCID + " " + telegram, false);

                    string[] splittelegram = telegram.Split(separationCharacter);
                    string startSignal = splittelegram[0];
                    string seperator = splittelegram[1];
                    string telegramType = splittelegram[2];
                    string telegramsender = splittelegram[3];
                    string receiver = splittelegram[4];
                    string datasetcount = splittelegram[5];
                    int datasets = int.Parse(datasetcount);

                    if (telegramsender != WMSID)
                    {
                        LogMessage("Received telegram with sender ID " + sender + ". Expected " + WMSID + ". Telegram is ignored!", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (receiver != PLCID)
                    {
                        LogMessage("Received telegram with reciever ID " + receiver + ". Expected " + PLCID + ". Telegram is ignored!", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (telegramType == "01")
                    {
                        Telegram01Recieved(splittelegram, datasets);
                        return;
                    }

                    if (telegramType == "04")
                    {
                        Telegram04Recieved(splittelegram, datasets);
                        return;
                    }

                    if (telegramType == "05")
                    {
                        Telegram05Recieved(splittelegram, datasets);
                        return;
                    }

                    if (telegramType == "20")
                    {
                        Telegram20Recieved(splittelegram, datasets);
                        return;
                    }

                    if (telegramType == "12" || telegramType == "13")
                    {
                        TelegramSystemStatusRecieved(splittelegram, datasets);
                        return;
                    }
                    if (telegramType == "30" || telegramType == "14")
                    {
                        TelegramRemapRecieved(splittelegram, datasets);
                        return;
                    }
                }
                catch (Exception se)
                {
                    LogMessage("Error in message: " + telegram + " Exception: " + se, Color.Red);
                    Core.Environment.Scene.Pause();
                }
            }

            private void Telegram04Recieved(string[] splittelegram, int datasets)
            {
                //Mission cancel, sent by the MFH to cancel mission (usually only after bin empty exception)

                #region Description from 41995553 Dematic Multi-Shuttle Control Principles v3.0
                //41995553 Dematic Multi-Shuttle Control Principles v3.0
                //8.2.1 Exceptions at Retrieval from Racking Location
                //If the shuttle car cannot retrieve the unit load from the racking location (because the location is empty
                //(status 05), or because there is a unit load in a front location (status 11)), the MSC reports an
                //exception (type 06) to the MFH, and waits for a response.
                //For location empty conditions, there is an MFH parameter to determine whether it is allowed to accept
                //the exception automatically. This is a project-specific decision. The reason for not always allowing this
                //is that the automatic handling (defined on a project-specific basis) may be to record the unit load as
                //missing, which may have implications on stock reporting to the host system.
                //If the location is reported as empty, and if automatic handling is not allowed, then the MFH allows an
                //operator to decide whether the retrieval to be re-tried, or the location empty condition is to be
                //accepted. If the retrieval is to be re-tried, the MFH sends a mission modify (type 05) with the original
                //mission details to the MSC. If the location empty condition is accepted, the MFH records this (typically
                //by updating the location as empty and the unit load as missing, but this is defined on a project-specific
                //basis), and cancels the mission to the MSC (type 04); the MFH is responsible for selecting an
                //alternate unit load, if appropriate.
                //If the location is reported as empty, and if automatic handling is allowed, then the MFH handles this
                //as documented above (for when the location empty condition is manually accepted).
                //If the location is reported as blocked, the MFH records the presence of the unknown unit load in the
                //location, and cancels the current retrieval mission to the MSC (type 04). The MFH then generates a   
                //mission to move the blocking unit load to a different racking location, and sends this to the MSC. This
                //mission is handled according to the rules for intra-level shuffle missions (see later). If there is no
                //available racking location, then the MFH directs the MSC to deliver the unknown unit load to the
                //outfeed rack conveyor. If the outfeed rack conveyor is currently occupied or in fault, then the MFH
                //continues to perform infeeds only (if there are any), until the outfeed rack conveyor becomes available
                //again, and then the blocking unit load can be delivered to the outfeed rack conveyor. When the
                //blocking unit load has successfully been moved out of the way, the MFH re-sends the original
                //retrieval mission to the MSC.
                #endregion

                #region Verify telegram

                if (datasets != 1)
                {
                    Core.Environment.Log.Write(Name + " can only handle 1 dataset in 04 telegrams. Telegram ignored");
                    return;
                }

                string originalPosition2;
                string currentPosition2;
                string destinationPosition2;
                string missionStatus2;
                string ULID2;
                string originalPosition1;
                string currentPosition1;
                string destinationPosition1;
                string missionStatus1;
                string ULID1;
                DematicMultiShuttle multishuttle;
                Case_Load caseload1, caseload2;

                if(!VerifyReceivedMission(splittelegram, datasets, out originalPosition2, out currentPosition2, out destinationPosition2, out missionStatus2, out ULID2,
                    out originalPosition1, out currentPosition1, out destinationPosition1, out missionStatus1, out ULID1, out multishuttle, out caseload1, out caseload2))
                {
                    //Reason is written to log in VerifyReceivedMission method
                    return;
                }
                #endregion

                #region Modify shuttle job
                if (currentPosition2.Substring(0, 1) != "R")
                {
                    LogMessage("Multishuttle cancek missions are only possible for totes in rack! (Current position should start with R)", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                string level = currentPosition2.Substring(8, 2);
                Shuttle shuttle = GetShuttle(level, multishuttle);
                if (shuttle == null)
                {
                    LogMessage("Error. Could not find shuttle on level " + level, Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                if (!(shuttle.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinRetrieveEmpty || shuttle.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinRetrieveBlocked))
                {
                    LogMessage("Multishuttle cancel mission is not expected. Shuttle on level " + level + " is not in bin empty exception.", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                //Reset shuttle exception
                shuttle.ShuttleCar.ExceptionType = ShuttleCar.ExceptionTypes.None;
                shuttle.ShuttleCar.InException = false;

                caseload2.Case_Data.MissionStatus = "00";
                string body = MissionDataSet(caseload2);

                caseload2.Case_Data.MissionTelegram = null;
                multishuttle.caseloads.Remove(caseload2);
                caseload2.Dispose();

                if (shuttle.Control.JobQueue.Count < 3)
                {
                    LogMessage("Error. No jobs in shuttle job queue after cancel mission!", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                if (shuttle.Control.JobQueue[0].JobType != ShuttleJob.JobTypes.Pick)
                {
                    LogMessage("Error. Cancel mission should be after bin empty! (Next shuttle job is not a drop job)", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                //Remove old pick
                shuttle.Control.JobQueue.RemoveAt(0);
                //Remove old goto drop pos
                shuttle.Control.JobQueue.RemoveAt(0);
                //Remove old drop
                shuttle.Control.JobQueue.RemoveAt(0);

                //Start queued jobs
                shuttle.Control.Start();

                //MSC reports unit load data deletion (message type 06 status 00)
                SendTelegram("06", body, 1);


                #endregion
            }

            private void Telegram05Recieved(string[] splittelegram, int datasets)
            {
                //Mission modify, sent by the MFH to modify destination after exception
                #region Description from 41995553 Dematic Multi-Shuttle Control Principles v3.0
                //41995553 Dematic Multi-Shuttle Control Principles v3.0

                //7.7.1 Exceptions at Storage to Racking Location
                //If the shuttle car cannot deliver the unit load to the racking location (because the location is occupied
                //(status 04), or because there is a unit load in a front location (status 12)), the MSC reports an
                //exception (type 06) to the MFH, and waits for a response. The MFH records the presence of the
                //unknown unit load in the location, then selects an alternate destination racking location for the unit
                //load on board the shuttle car, and sends a mission modify (type 05) to the MSC. If there is no
                //available racking location, then the MFH instead sets the destination to be the outfeed rack conveyor,
                //and sends a mission modify (type 05) to the MSC. If the outfeed rack conveyor is currently fully
                //occupied or in fault, then this shuttle car is effectively out-of-action until it becomes available (the MFH
                //does not send the re-direction mission until it believes that the outfeed rack conveyor now has space
                //available for the unit load and is not in fault).
                #endregion

                #region Verify telegram

                if (datasets != 1)
                {
                    Core.Environment.Log.Write(Name + " can only handle 1 dataset in 05 telegrams. Telegram ignored");
                    return;
                }

                string originalPosition2;
                string currentPosition2;
                string destinationPosition2;
                string missionStatus2;
                string ULID2;
                string originalPosition1;
                string currentPosition1;
                string destinationPosition1;
                string missionStatus1;
                string ULID1;
                DematicMultiShuttle multishuttle;
                Case_Load caseload1, caseload2;

                if (!VerifyReceivedMission(splittelegram, datasets, out originalPosition2, out currentPosition2, out destinationPosition2, out missionStatus2, out ULID2,
                    out originalPosition1, out currentPosition1, out destinationPosition1, out missionStatus1, out ULID1, out multishuttle, out caseload1, out caseload2))
                {
                    //Reason is written to log in VerifyReceivedMission method
                    return;
                }
                #endregion

                #region Modify shuttle job
                if (currentPosition2.Substring(0, 1) == "S")
                {
                    //Tote is on shuttle. Modify drop job - MS expects new Rack location or Outfeed rack location
                    string level = currentPosition2.Substring(8, 2);
                    Shuttle shuttle = GetShuttle(level, multishuttle);
                    if (shuttle == null)
                    {
                        LogMessage("Error. Could not find shuttle on level " + level, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (shuttle.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.None || shuttle.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinRetrieveEmpty)
                    {
                        LogMessage("Multishuttle modify mission is not expected. Shuttle on level " + level + " is not in bin full or bin blocked Exception.", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    //Reset shuttle exception
                    shuttle.ShuttleCar.ExceptionType = ShuttleCar.ExceptionTypes.None;
                    shuttle.ShuttleCar.InException = false;

                    caseload2.Case_Data.MissionTelegram = splittelegram;

                    float destinationdistance = -1;
                    bool dropinrack = false;
                    RackConveyor rackconv = null;

                    if (destinationPosition2.Substring(0, 1) == "R")
                    {
                        //destination is rack location
                        string x = destinationPosition2.Substring(5, 3);
                        int xCoord = int.Parse(x);

                        if (xCoord > multishuttle.RackBays)
                        {
                            LogMessage("Error. Recieved x coordinate " + xCoord + " but multishuttle has " + multishuttle.RackBays + " bays", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        destinationdistance = GetXDistance(xCoord, multishuttle); 
                        if (destinationdistance > multishuttle.Raillength)
                            destinationdistance = multishuttle.Raillength;

                        dropinrack = true;
                    }
                    if (destinationPosition2.Substring(0, 1) == "O")
                    {
                        //Destination is out feed rack
                        dropinrack = false;

                        string destinationlevel = destinationPosition2.Substring(8, 2);
                        if (level != destinationlevel)
                        {
                            LogMessage("Error. Current level does not matcht destination level!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        string elevatorname = destinationPosition2.Substring(3, 2);
                        string key = elevatorname + level;

                        if (!multishuttle.RackConveyors.ContainsKey(key))
                        {
                            LogMessage("Error. Could not find rack conveyor " + elevatorname + " at level " + destinationlevel, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        string pos = destinationPosition2.Substring(5, 3);
                        if (pos != multishuttle.POS2OUTFEED)
                        {
                            LogMessage("Error. Outfeedjob position is not " + multishuttle.POS2OUTFEED + "! Position: " + pos, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        rackconv = multishuttle.RackConveyors[key];
                        if(rackconv.RackConveyorType == MultiShuttleDirections.Infeed)
                        {
                            LogMessage("Error. Destination rack is not outfeed rack!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        float positionoffset = multishuttle.RackConveyorLength / 4;
                        if (rackconv.LocalYaw == (float)Math.PI)
                            positionoffset = -multishuttle.RackConveyorLength / 4;

                        destinationdistance = multishuttle.Raillength / 2 - rackconv.LocalPosition.X + positionoffset;// rackconv.Length - rackconv.Length / 4;

                     
                    }

                    if (shuttle.Control.JobQueue.Count == 0)
                    {
                        LogMessage("Error. No jobs in shuttle job queue after modify mission!", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (shuttle.Control.JobQueue[0].JobType != ShuttleJob.JobTypes.Drop)
                    {
                        LogMessage("Error. Modify mission should be after bin full or bin blocked! (Next shuttle job is not a drop job)", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    //Remove old drop
                    shuttle.Control.JobQueue.RemoveAt(0);

                    //Get a copy of all jobs.
                    List<ShuttleJob> currentqueue = new List<ShuttleJob>(shuttle.Control.JobQueue);
                    shuttle.Control.JobQueue.Clear();


                    shuttle.Control.Goto(destinationdistance, "");
                    shuttle.Control.DropLoad(multishuttle.DepthDist, dropinrack, "", rackconv);
                    //Add old jobs to end of job ques
                    shuttle.Control.JobQueue.AddRange(currentqueue);
                    //Start shuttle. 
                    shuttle.Control.Start();
                }

                else if (currentPosition2.Substring(0, 1) == "R")
                {
                    //Tote is in rack. Rack location should be the same for a retry. Else a cancel job is expected.
                    string level = currentPosition2.Substring(8, 2);
                    Shuttle shuttle = GetShuttle(level, multishuttle);
                    if (shuttle == null)
                    {
                        LogMessage("Error. Could not find shuttle on level " + level, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (!(shuttle.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinRetrieveEmpty || shuttle.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinRetrieveBlocked) )
                    {
                        LogMessage("Multishuttle modify mission is not expected. Shuttle on level " + level + " is not in bin empty.", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    //Reset shuttle exception
                    shuttle.ShuttleCar.ExceptionType = ShuttleCar.ExceptionTypes.None;
                    shuttle.ShuttleCar.InException = false;

                    //remove the caseload from the MS list
                    multishuttle.caseloads.Remove(caseload2);
                    //Delete the tote
                    caseload2.Dispose();

                    //Remove old jobs
                    shuttle.Control.JobQueue.RemoveAt(0);
                    shuttle.Control.JobQueue.RemoveAt(0);
                    shuttle.Control.JobQueue.RemoveAt(0);

                    //Get a copy of all jobs.
                    List<ShuttleJob> currentqueue = new List<ShuttleJob>(shuttle.Control.JobQueue);
                    shuttle.Control.JobQueue.Clear();

                    //Handle as a new 01
                    splittelegram[2] = "01";
                    Telegram01Recieved(splittelegram, datasets);

                    //Add old jobs to end of job ques
                    shuttle.Control.JobQueue.AddRange(currentqueue);
                    //Start shuttle. 
                    shuttle.Control.Start();
       
                }

                #endregion

            }

            private void TelegramRemapRecieved(string[] splittelegram, int datasets)
            {
                lastSentTelegram = "";
                string startSignal = splittelegram[0];
                string seperator = splittelegram[1];
                string telegramType = splittelegram[2];
                string telegramsender = splittelegram[3];
                string receiver = splittelegram[4];
                string status = splittelegram[7];

                if (telegramType == "30")
                {
                    if (PLCState != PLCStates.Auto_No_Move_03)
                    {
                        LogMessage("Warning: Plc state not Auto_No_Move_03 when recieving telegram type 30!");
                    }

                    SendULData();

                    //MSC sends type 32 on completion of mapping data
                    string body = ",";
                    SendTelegram("32", body, 1);
                }
                else if (telegramType == "14")
                {
                    if (status == "04")
                    {
                        //Go to Auto_04
                        if (PLCState != PLCStates.Auto_No_Move_03)
                        {
                            LogMessage("Warning: Plc state not Auto_No_Move_03 when recieving telegram type 30!");
                        }
                        PLCState = PLCStates.Auto_04;

                        string body = ",04,,";  //Status 04
                        SendTelegram("13", body, 1);
                        //Remap finished!
                    }
                }
            }

            private void SendULData()
            {
                foreach (DematicMultiShuttle multishuttle in multishuttles)
                {
                    foreach (Case_Load caseload in multishuttle.caseloads)
                    {
                        if (caseload.Case_Data.CurrentPosition.Length == 0)
                            continue;
                        //MSC sends type 31 for every place (other than the PS) which has unit load data 
                        if (caseload.Case_Data.CurrentPosition.Substring(0, 1) == "P")
                            continue;
                        string body = MissionDataSet(caseload);

                        SendTelegram("31", body, 1);

                        if (caseload.Case_Data.CurrentPosition.Length >= 10 && caseload.Case_Data.CurrentPosition.Substring(0, 1) == "I" && caseload.Case_Data.CurrentPosition.Substring(5, 3) == "002" && caseload.Case_Data.DestinationPosition.Substring(0, 1) != "I")
                        {
                            //Caseload is on infeed rack conveyor position 002. Destination is not Infeed so shuttle car has got a mission. Send 31 with current position = shuttle car and status 02 pending.
                            
                            //Remap will be sent from two locations:
                            //1. From Rack Conveyor, present location is rack conveyor (pos2), status ‘00’
                            //2. From shuttle car, present location is shuttle car with mission status ‘02’ (Pending) 
                            //If mission is not also remapped from shuttle, MFH should re-send mission.
                            //If mission reported from shuttle but status is not ’02’, remap should be failed.
                            //If mission is remapped from shuttle only with status ‘02’. This should also cause remap failure.
                            
                            //Create telegram body with mission status 02 pending and current is shuttlecar
                            string current = caseload.Case_Data.CurrentPosition;
                            string level = current.Substring(8, 2);
                            caseload.Case_Data.CurrentPosition = "S" + multishuttle.AisleNo + "     " + level;
                            caseload.Case_Data.MissionStatus = "02";
                            body = MissionDataSet(caseload);
                          
                            //Restore current and mission status
                            caseload.Case_Data.CurrentPosition = current;
                            caseload.Case_Data.MissionStatus = "00";

                            SendTelegram("31", body, 1);
                        }
                    }

                    foreach (InfeedPickStationConveyor conv in multishuttle.PickStationConveyors)
                    {
                        //MSC sends type 35 for Pick Station unit loads

                        ActionPoint ap1 = conv.MultiShuttle.PickStationNameToActionPoint[conv.infeedInfo.location1Name];
                        ActionPoint ap2 = conv.MultiShuttle.PickStationNameToActionPoint[conv.infeedInfo.location2Name];

                        if (ap2.Active)
                        {
                            Case_Load caseload2 = ap2.ActiveLoad as Case_Load;

                            if (caseload2 == null)
                            {
                                Core.Environment.Log.Write("Error. Load on MS pick station location 2 is not a case load!");
                                Core.Environment.Scene.Pause();
                                return;
                            }

                            //Remove time out from first case load. Arrival is sent with 35 message now in remap.
                            caseload2.OnFinishedWaitingEvent -= Tote_PickStation2TimeOut;
                            caseload2.WaitingTime = 0;
                            caseload2.Stop();

                            //update current position
                            string location2 = "P" + conv.Elevator.ParentMultiShuttle.AisleNo + ap2.Name.Substring(3, 2) + conv.Elevator.ParentMultiShuttle.POS2 + conv.Level;
                            caseload2.CurrentPosition = location2;
                            caseload2.Case_Data.RoutingTableUpdateWait = true;

                            string Position2ULID = caseload2.Case_Data.ULID;
                            string Position2ULBarcode = SSCCBarcodePrefix + caseload2.Case_Data.SSCCBarcode;
                            string Position2ULHeight = (caseload2.Height * 1000).ToString("0000"); 

                            if (!string.IsNullOrWhiteSpace(Position2ULID))
                                Position2ULBarcode = "";  //Mission sent. Remap message contains ULID. Barcode field is blank (spaces)

                            string Position1ULID = "";
                            string Position1ULBarcode = "";
                            string Position1ULHeight = "";

                            Case_Load caseload1 = ap1.ActiveLoad as Case_Load;

                            if (caseload1 != null)
                            {
                                //update current position
                                string location1 = "P" + conv.Elevator.ParentMultiShuttle.AisleNo + ap1.Name.Substring(3, 2) + conv.Elevator.ParentMultiShuttle.POS1 + conv.Level;
                                caseload1.CurrentPosition = location1;
                                caseload1.Case_Data.RoutingTableUpdateWait = true;
                                Position1ULID = caseload1.Case_Data.ULID;
                                Position1ULBarcode = SSCCBarcodePrefix + caseload1.Case_Data.SSCCBarcode;
                                Position1ULHeight = (caseload1.Height * 1000).ToString("0000"); 


                                if (!string.IsNullOrWhiteSpace(Position1ULID))
                                    Position1ULBarcode = "";  //Mission sent. Remap message contains ULID. Barcode field is blank (spaces)
                            }

                            string body = PickStationDataSet(caseload2, caseload1, caseload2.Case_Data.CurrentPosition,
                                              Position2ULID,
                                              Position2ULBarcode,
                                              Position2ULHeight,
                                              Position1ULID,
                                              Position1ULBarcode,
                                              Position1ULHeight);

                            SendTelegram("35", body, 1);
                        }
                    }
                }
            }

            private void TelegramSystemStatusRecieved(string[] splittelegram, int datasets)
            {
                lastSentTelegram = "";
                string startSignal = splittelegram[0];
                string seperator = splittelegram[1];
                string telegramType = splittelegram[2];
                string telegramsender = splittelegram[3];
                string receiver = splittelegram[4];
                string status = splittelegram[7];

                if (telegramType == "13")
                {
                    if (status == "02")
                    {
                        if (PLCState != PLCStates.Unknown_00)
                        {
                            LogMessage("Warning: Plc state not Unknown00 when recieving telegram type 13, status 02!");
                        }
                        PLCState = PLCStates.Ready_02;
                        string body = ",02,,";  //Status 02
                        SendTelegram("13", body, 1);
                    }
                    else if (status == "07")
                    {
                        //Heartbeat message
                        string body = ",07,,";  //Status 07
                        SendTelegram("13", body, 1);
                    }
                }
                else if (telegramType == "12")
                {
                    if (status == "03")
                    {
                        if (PLCState != PLCStates.Ready_02)
                        {
                            LogMessage("Warning: Plc state not Ready_02 when recieving telegram type 12, status 03!");
                        }
                        //The message sequence is as follows:
                        //MFH sends type 12 status 03 - set system status to automatic no move and request system status.
                        PLCState = PLCStates.Auto_No_Move_03;
                        //MSC sends equipment status message (type 10) for each piece of equipment
                        SendEquipmentStatus("00");
                        //MSC sends elevator operational mode status (type 82) for each elevator (Project dependant)
                        SendElevatorOperationalModeStatus("00");
                        //MSC sends type 13 status 03
                        string body = ",03,,";  //Status 03
                        SendTelegram("13", body, 1);          
                    }
                }
            }

            private void SendElevatorOperationalModeStatus(string status)
            {
                //MSC sends elevator operational mode status (type 82) for each elevator (Project dependant)        
                //TODO ?
                //string body = ",,,"; 
                //SendTelegram("82", body, 1);         
            }

            private void SendEquipmentStatus(string status)
            {
                foreach (DematicMultiShuttle multishuttle in multishuttles)
                {
                    //MSC sends equipment status message (type 10) for each piece of equipment

                    foreach (RackConveyor r in multishuttle.RackConveyors.Values)
                    {
                        string type = "O";
                        if (r.RackConveyorType == MultiShuttleDirections.Infeed)
                            type = "I";

                        string name = type + multishuttle.AisleNo + r.RackName.Substring(0,2) + "   " + r.Level;
                        string body = name + "," + status + ",,";
                        SendTelegram("10", body, 1);
                    }

                    foreach (MultiShuttleElevator e in multishuttle.elevators.Values)
                    {
                        string name = "E" + multishuttle.AisleNo + e.ElevatorName + "     ";
                        string body = name + "," + status + ",,";
                        SendTelegram("10", body, 1);
                    }

                    foreach (InfeedPickStationConveyor c in multishuttle.PickStationConveyors)
                    {
                        // c.infeedInfo.location1Name
                        //string name = "P" + multishuttle.AisleNo + c.Elevator.ElevatorName + "   " + c.Level; BG Changed
                        string name = c.infeedInfo.location1Name.Substring(0, 5) + "   " + c.Level;
                        //string name = "P" + multishuttle.AisleNo + c.Name.Substring(c.Name.Length - 4, 2) + "   " + c.Level; This should work but doesn't when adding extra elevator as in Clalit Aisle 5
                        string body = name + "," + status + ",,";
                        SendTelegram("10", body, 1);
                    }

                    foreach (OutfeedDropStationConveyor d in multishuttle.DropStationPoints.Values)
                    {
                        //string name = "D" + multishuttle.AisleNo + d.Elevator.ElevatorName + "   " + d.Level; BG Changed
                        string name = "D" + multishuttle.AisleNo + d.Name.Substring(d.Name.Length - 4, 2) + "   " + d.Level;
                        string body = name + "," + status + ",,";
                        SendTelegram("10", body, 1);
                    }

                    foreach (Shuttle s in multishuttle.shuttlecars.Values)
                    {
                        string name = "S" + multishuttle.AisleNo + "     " + s.Level;
                        string body = name + "," + status + ",,";
                        SendTelegram("10", body, 1);
                    }

                    foreach (Shuttle s in multishuttle.shuttlecars.Values)
                    {
                        //Level group!
                        string name = "L" + multishuttle.AisleNo + "     " + s.Level;
                        string body = name + "," + status + ",,";
                        SendTelegram("10", body, 1);
                    }

                    //Aisle group
                    string AisleName = "A" + multishuttle.AisleNo + "       ";
                    string AisleBody = AisleName + "," + status + ",,";
                    SendTelegram("10", AisleBody, 1);
                }
            }

            private void Telegram20Recieved(string[] splittelegram, int datasets)
            {
                string datasetcount = splittelegram[5];
                int dataset = int.Parse(datasetcount);
                string originalPosition = splittelegram[6];
                string currentPosition = splittelegram[7];
                string destinationPosition = splittelegram[8];
                string missionStatus = splittelegram[9];
                string ULID = splittelegram[10];

                
                if (currentPosition.Length < 3)
                {
                    LogMessage("Error in current position " + currentPosition, Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                string aisleno = currentPosition.Substring(1, 2);
                DematicMultiShuttle multishuttle = GetMultiShuttle(aisleno);

                if (multishuttle == null)
                {
                    LogMessage("Error. Aisle not found " + aisleno, Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                Case_Load caseload = GetCaseLoadInMS(ULID, multishuttle);

                if (caseload == null)
                {
                    LogMessage("Error. No caseload in MS with ULID " + ULID, Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                if (caseload.Case_Data.CurrentPosition != currentPosition)
                {
                    LogMessage("Error. Caseload in MS with ULID " + ULID + "does not match recieved current position " + currentPosition + "(" + caseload.Case_Data.CurrentPosition + ")", Color.Red);
                    Core.Environment.Scene.Pause();
                }

                multishuttle.caseloads.Remove(caseload);
                caseload.ULID = "";

                if (dataset == 2)
                {
                    originalPosition = splittelegram[17];
                    currentPosition = splittelegram[18];
                    destinationPosition = splittelegram[19];
                    missionStatus = splittelegram[20];
                    ULID = splittelegram[21];

                    if (currentPosition.Length < 3)
                    {
                        LogMessage("Error in current position " + currentPosition, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    aisleno = currentPosition.Substring(1, 2);
                    multishuttle = GetMultiShuttle(aisleno);

                    if (multishuttle == null)
                    {
                        LogMessage("Error. Aisle not found " + aisleno, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    caseload = GetCaseLoadInMS(ULID, multishuttle);

                    if (caseload == null)
                    {
                        LogMessage("Error. No caseload in MS with ULID " + ULID, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (caseload.Case_Data.CurrentPosition != currentPosition)
                    {
                        LogMessage("Error. Caseload in MS with ULID " + ULID + "does not match recieved current position " + currentPosition + "(" + caseload.Case_Data.CurrentPosition + ")", Color.Red);
                        Core.Environment.Scene.Pause();
                    }

                    multishuttle.caseloads.Remove(caseload);
                    caseload.ULID = "";
                }
            }

            private Case_Load CreateCaseInRack(string currentpos, string[] splittelegram, string ulid, DematicMultiShuttle multishuttle)
            {
                MeshInfo boxInfo = new MeshInfo();
                boxInfo.color = multishuttle.ToteColor;

                //l and w is swapped in Experior (different coordinate system)
                float l = multishuttle.ToteLength; //Unit Load Width (z-axis)
                if (!string.IsNullOrWhiteSpace(splittelegram[12]))
                     l = float.Parse(splittelegram[12]) / 1000f;

                float w = multishuttle.ToteWidth; //Unit Load Length (x-axis) 
                if (!string.IsNullOrWhiteSpace(splittelegram[13]))
                    w = float.Parse(splittelegram[13]) / 1000f;

                float h = multishuttle.ToteHeight;
                if (!string.IsNullOrWhiteSpace(splittelegram[14]))
                    h = float.Parse(splittelegram[14]) / 1000f;

                boxInfo.length = l;
                boxInfo.height = h;
                boxInfo.width = w;
                boxInfo.filename = Case_Load.GraphicsMesh;

                Case_Load load = new Case_Load(boxInfo);

                load.UserDeletable = false;
                load.Movable = false;
                load.MissionTelegram = splittelegram;

                load.Case_Data.CurrentPosition = currentpos;
                load.Case_Data.OriginalPosition = currentpos;
                load.Case_Data.Weight = multishuttle.ToteWeight;
                if (!string.IsNullOrWhiteSpace(splittelegram[15]))
                    load.Case_Data.Weight = float.Parse(splittelegram[15]) / 1000f;

                load.ULID = ulid;
                load.Orientation = multishuttle.Orientation * Matrix.RotationY((float)Math.PI / 2);

                multishuttle.caseloads.Add(load);

                return load;
            }

            private bool VerifyReceivedMission(string[] splittelegram, int datasets, out string originalPosition2, out string currentPosition2, out string destinationPosition2, out string missionStatus2, out string ULID2, out string originalPosition1, out string currentPosition1, out string destinationPosition1, out string missionStatus1, out string ulid1, out DematicMultiShuttle multishuttle, out Case_Load caseload1, out Case_Load caseload2)
            { 
                originalPosition2 = splittelegram[6];
                currentPosition2 = splittelegram[7];
                destinationPosition2 = splittelegram[8];
                missionStatus2 = splittelegram[9];
                ULID2 = splittelegram[10];
                originalPosition1 = string.Empty;
                currentPosition1 = string.Empty;
                destinationPosition1 = string.Empty;
                missionStatus1 = string.Empty;
                ulid1 = string.Empty;
                caseload1 = null;
                caseload2 = null;
                multishuttle = null;

                if (currentPosition2.Length < 3)
                {
                    LogMessage("Error in current position: " + currentPosition2, Color.Red);
                    Core.Environment.Scene.Pause();
                    return false;
                }

                string aisleno = currentPosition2.Substring(1, 2);
                multishuttle = GetMultiShuttle(aisleno: aisleno);

                if (multishuttle == null)
                {
                    LogMessage("Aisle no not found: " + aisleno, Color.Red);
                    Core.Environment.Scene.Pause();
                    return false;
                }

                //If only one dataset caseload2 is used!!! 
                caseload2 = Case_Load.GetCaseFromULID(ULID2);
                caseload1 = null;

                if (caseload2 == null)
                {
                    if (originalPosition2.Length > 1 && originalPosition2.Substring(0, 1) == "R" && currentPosition2.Length > 1 && currentPosition2.Substring(0, 1) == "R")
                    {
                        //Create tote in rack
                        caseload2 = CreateCaseInRack(currentPosition2, splittelegram, ULID2, multishuttle);
                    }
                    else if (multishuttle.PickStationNameToActionPoint.ContainsKey(currentPosition2))
                    {
                        //Tote located at pick station. Find tote on pick station and update the missionID.
                        caseload2 = multishuttle.PickStationNameToActionPoint[currentPosition2].ActiveLoad as Case_Load;

                        if (caseload2 == null)
                        {
                            LogMessage("Error. No case found on pick station: " + currentPosition2, Color.Red);
                            return false;
                        }

                        caseload2.ULID = ULID2;
                    }
                    else
                    {
                        //caseload2 not found! Should always be found.
                        LogMessage("Error. No case found with ULID " + ULID2, Color.Red);
                        return false;
                    }
                }

                if (caseload2.Case_Data.CurrentPosition != currentPosition2)
                {
                    LogMessage("Error. Case with ULID " + ULID2 + " is currently at " + caseload2.Case_Data.CurrentPosition, Color.Red);
                    Core.Environment.Scene.Pause();
                    return false;
                }

                caseload2.Destination = destinationPosition2;
                caseload2.Case_Data.RoutingTableUpdateWait = false;
                caseload2.Case_Data.MissionTelegram = splittelegram;
                caseload2.Case_Data.OriginalPosition = originalPosition2;
                caseload2.Case_Data.TimeStamp = splittelegram[16];

                if (string.IsNullOrWhiteSpace(currentPosition2))
                {
                    LogMessage("Error. Current position is empty.", Color.Red);
                    return false;
                }
                if (currentPosition2.Length < 10)
                {
                    LogMessage("Error. Current position is too short: " + currentPosition2, Color.Red);
                    return false;
                }
                if (datasets == 2)
                {
                    //telegram contains information for two totes.
                    originalPosition1 = splittelegram[17];
                    currentPosition1 = splittelegram[18];
                    destinationPosition1 = splittelegram[19];
                    missionStatus1 = splittelegram[20];
                    ulid1 = splittelegram[21];

                    caseload1 = Case_Load.GetCaseFromULID(ulid1);

                    if (multishuttle.PickStationNameToActionPoint.ContainsKey(currentPosition1))
                    {
                        //Tote located at pick station. Find tote on pick station and update the missionID.
                        caseload1 = multishuttle.PickStationNameToActionPoint[currentPosition1].ActiveLoad as Case_Load;

                        if (caseload1 == null)
                        {
                            LogMessage("Error. No case found on pick station: " + currentPosition1, Color.Red);
                            return false;
                        }

                        caseload1.ULID = ulid1;
                    }

                    if (caseload1 == null)
                    {
                        LogMessage("Error. No case found with ULID " + ulid1, Color.Red);
                        Core.Environment.Scene.Pause();
                        return false;
                    }

                    if (caseload1.Case_Data.CurrentPosition != currentPosition1)
                    {
                        LogMessage("Error. Case with ULID " + ulid1 + " is currently at " + caseload1.Case_Data.CurrentPosition, Color.Red);
                        Core.Environment.Scene.Pause();
                        return false;
                    }

                    caseload1.Destination = destinationPosition1;
                    caseload1.Case_Data.RoutingTableUpdateWait = false;
                    caseload1.Case_Data.MissionTelegram = splittelegram;
                    caseload1.Case_Data.OriginalPosition = originalPosition1;
                    caseload1.Case_Data.TimeStamp = splittelegram[27];
                }

                return true;
            }

            private void Telegram01Recieved(string[] splittelegram, int datasets)
            {
                #region Case on Pos2 is the first in dataset, Case on Pos1 is the second in dataset

                if (datasets == 2 && splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) != "R" && splittelegram[18].Length > 0 && splittelegram[18].Substring(0, 1) != "R")
                {
                    //switch D, E, I, O pos2 & pos1
                    if (splittelegram[7].Contains("001") && splittelegram[18].Contains("002"))
                    {
                        //switch around!
                        string temp17 = splittelegram[17];
                        string temp18 = splittelegram[18];
                        string temp19 = splittelegram[19];
                        string temp20 = splittelegram[20];
                        string temp21 = splittelegram[21];

                        splittelegram[17] = splittelegram[6];
                        splittelegram[18] = splittelegram[7];
                        splittelegram[19] = splittelegram[8];
                        splittelegram[20] = splittelegram[9];
                        splittelegram[21] = splittelegram[10];

                        splittelegram[6] = temp17;
                        splittelegram[7] = temp18;
                        splittelegram[8] = temp19;
                        splittelegram[9] = temp20;
                        splittelegram[10] = temp21;
                    }
                }
                #endregion

                #region Verify telegram
                string originalPosition2;
                string currentPosition2;
                string destinationPosition2;
                string missionStatus2;
                string ULID2;
                string originalPosition1;
                string currentPosition1;
                string destinationPosition1;
                string missionStatus1;
                string ULID1;
                DematicMultiShuttle multishuttle;
                Case_Load caseload1, caseload2;

                if(!VerifyReceivedMission(splittelegram, datasets, out originalPosition2, out currentPosition2, out destinationPosition2, out missionStatus2, out ULID2,
                    out originalPosition1, out currentPosition1, out destinationPosition1, out missionStatus1, out ULID1, out multishuttle, out caseload1, out caseload2))
                {                   
                    return; //Reason is written to log in VerifyReceivedMission method
                }
                #endregion

                #region current rack
                if (currentPosition2.Substring(0, 1) == "R")
                {
                    //current rack

                    string level = currentPosition2.Substring(8, 2);
                    Shuttle shuttle = GetShuttle(level, multishuttle);
                    if (shuttle == null)
                    {
                        LogMessage("Error. Could not find shuttle on level " + level, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    string x = currentPosition2.Substring(5, 3);
                    int xCoord = int.Parse(x);

                    if (xCoord > multishuttle.RackBays)
                    {
                        LogMessage("Error. Recieved x coordinate " + xCoord + " but multishuttle has " + multishuttle.RackBays + " bays", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    float currentdistance = GetXDistance(xCoord, multishuttle); 
                    if (currentdistance > multishuttle.Raillength)
                        currentdistance = multishuttle.Raillength;

                    #region Destination Outfeed rack
                    if (destinationPosition2.Substring(0, 1) == "O")
                    {
                        //Destination outfeed rack

                        string destinationlevel = destinationPosition2.Substring(8, 2);
                        if (level != destinationlevel)
                        {
                            LogMessage("Error. Current level does not matcht destination level!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        string elevatorname = destinationPosition2.Substring(3, 2);
                        string key = elevatorname + level;

                        if (!multishuttle.RackConveyors.ContainsKey(key))
                        {
                            LogMessage("Error. Could not find rack conveyor " + elevatorname + " at level " + destinationlevel, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        string pos = destinationPosition2.Substring(5, 3);
                        if (pos != multishuttle.POS2OUTFEED)
                        {
                            LogMessage("Error. Outfeedjob position is not " + multishuttle.POS2OUTFEED + "! Position: " + pos, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        RackConveyor rackconv = multishuttle.RackConveyors[key];
                        if (rackconv.RackConveyorType == MultiShuttleDirections.Infeed)
                        {
                            LogMessage("Error. Destination rack is not outfeed rack!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        float positionoffset = multishuttle.RackConveyorLength / 4;

                        if (rackconv.LocalYaw == (float)Math.PI) {positionoffset = -multishuttle.RackConveyorLength / 4;}

                        float dropdistance = multishuttle.Raillength / 2 - rackconv.LocalPosition.X + positionoffset;// multishuttle.RackConveyorLength - multishuttle.RackConveyorLength / 4;
        
                        shuttle.Control.Goto(currentdistance, "");
                        shuttle.Control.PickLoad(multishuttle.DepthDist, caseload2, true, "", null);
                        shuttle.Control.Goto(dropdistance, "");
                        shuttle.Control.DropLoad(multishuttle.DepthDist, false, "", rackconv);
                        shuttle.Control.Start();
                    }
                    #endregion

                    #region Destination Rack (shuffle move)
                    else if (destinationPosition2.Substring(0, 1) == "R")
                    {
                        //Destination rack (shuffle move)
                        //check current and destination same level!
                        string currentlevel = currentPosition2.Substring(8, 2);
                        string destlevel = destinationPosition2.Substring(8, 2);

                        if (currentlevel != destlevel)
                        {
                            LogMessage("Error. Current level does not matcht destination level! (Shuffle move)", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        string xDest = destinationPosition2.Substring(5, 3);
                        int xCoordDest = int.Parse(xDest);

                        if (xCoordDest > multishuttle.RackBays)
                        {
                            LogMessage("Error. Recieved x coordinate " + xCoordDest + " but multishuttle has " + multishuttle.RackBays + " bays", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        float destdistance = GetXDistance(xCoordDest, multishuttle); 

                        if (destdistance > multishuttle.Raillength) {destdistance = multishuttle.Raillength;}

                        shuttle.Control.Goto(currentdistance, "");
                        shuttle.Control.PickLoad(multishuttle.DepthDist, caseload2, true, "", null);
                        shuttle.Control.Goto(destdistance, "");
                        shuttle.Control.DropLoad(multishuttle.DepthDist, true, "", null);
                        shuttle.Control.Start();

                    #endregion

                    }
                }
                #endregion

                #region current pick station
                else if (currentPosition2.Substring(0, 1) == "P")
                {
                    //current pick station
            
                    string elevatorname = currentPosition2.Substring(3, 2);
                    string destinationlevel = destinationPosition2.Substring(8, 2);
                  
                    if (caseload2.CurrentActionPoint == null)
                    {
                        LogMessage("Error. No Current action point for current position: " + currentPosition2, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    InfeedPickStationConveyor conv = caseload2.CurrentActionPoint.Parent.Parent.Parent as InfeedPickStationConveyor;

                    if (conv == null)
                    {
                        LogMessage("Error. No infeed pick station conveyor found for current position: " + currentPosition2, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    
                    MultiShuttleElevator elevator = conv.Elevator;

                    if (elevator == null)
                    {
                        LogMessage("Error. No elevator found for current position: " + currentPosition2, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    

                    string destrackname = destinationPosition2.Substring(3, 2);
                    string key = destrackname + destinationlevel;

                    if (destinationPosition2.Substring(0,1) == "I" && !multishuttle.RackConveyors.ContainsKey(key))
                    {
                        LogMessage("Error. Destination not a rack conveyor: " + destinationPosition2, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (elevator.ParentMultiShuttle.MixedInfeedOutfeed && destinationPosition2.Substring(0, 1) == "I")
                    {
                        RackConveyor dest = multishuttle.RackConveyors[key];
                        if (dest.RackConveyorType == MultiShuttleDirections.Outfeed)
                        {
                            LogMessage("Error. Destination is outfeed rack conveyor: " + destinationPosition2, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }
                    }
                   
                    MultishuttleElevatorJobData elevatordata = new MultishuttleElevatorJobData()
                    {
                        JobType = MultiShuttleDirections.Infeed,
                        JobMode              = MultishuttleElevatorJobData.JobModes.Load1,
                        CaseLoadPosition2    = caseload2,
                        MissionTelegram      = splittelegram,
                        DestinationsLevel    = destinationlevel,
                        DestinationGroupSide = destrackname,
                        Parent               = elevator
                    };

                    if (destinationPosition2.Substring(0, 1) == "D")//Pick station to drop station move
                    {
                        elevatordata.JobType = MultiShuttleDirections.Outfeed;
                    }

                    if (datasets == 2)
                    {
                        elevatordata.JobMode = MultishuttleElevatorJobData.JobModes.Load2;
                        elevatordata.CaseLoadPosition1 = caseload1;

                        if (caseload1.UserData is MultishuttleElevatorJobData)
                        {
                            LogMessage("Error. caseload on position " + caseload1.Case_Data.CurrentPosition + " ULID " + caseload1.ULID + " already has elevator job!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        caseload1.UserData = elevatordata;
                    }

                    if (caseload2.UserData is MultishuttleElevatorJobData)
                    {
                        LogMessage("Error. caseload on position " + caseload2.Case_Data.CurrentPosition + " ULID " + caseload2.ULID + " already has elevator job!", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    caseload2.UserData = elevatordata;

                    elevator.Control.Goto(conv.LocalPosition.Y, "", elevatordata);
                    if (elevator.CurrentJobData == null)
                    {
                        elevator.Control.Start();                            
                    }

                }
                #endregion

                #region current drop station
                else if (currentPosition2.Substring(0, 1) == "D")
                {
                    //current drop station


                }
                #endregion

                #region current elevator
                else if (currentPosition2.Substring(0, 1) == "E")
                {
                    //current elevator

                }
                #endregion

                #region current infeed rack conv
                else if (currentPosition2.Substring(0, 1) == "I")
                {
                    //current infeed rack conv
                    if (destinationPosition2.Substring(0, 1) == "R")
                    {
                        //Destination Rack
                        string level = currentPosition2.Substring(8, 2);
                        Shuttle shuttle = GetShuttle(level, multishuttle);
                        if (shuttle == null)
                        {
                            LogMessage("Error. Could not find shuttle on level " + level, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }
                        string x = destinationPosition2.Substring(5, 3);
                        int xCoord = int.Parse(x);

                        if (xCoord > multishuttle.RackBays)
                        {
                            LogMessage("Error. Recieved x coordinate " + xCoord + " but multishuttle has " + multishuttle.RackBays + " bays", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        float destionationdistance = GetXDistance(xCoord, multishuttle); 

                        if (destionationdistance > multishuttle.Raillength)  {destionationdistance = multishuttle.Raillength;}

                        RackConveyor rackconv = caseload2.CurrentActionPoint.Parent.Parent.Parent as RackConveyor;
  
                        float positionoffset = multishuttle.RackConveyorLength / 4;
                        if (rackconv.LocalYaw == (float)Math.PI) {positionoffset = -multishuttle.RackConveyorLength / 4;}

                        float pickdistance = multishuttle.Raillength / 2 - rackconv.LocalPosition.X + positionoffset;// rackconv.Length - rackconv.Length / 4;
        
                        shuttle.Control.Goto(pickdistance, "");
                        shuttle.Control.PickLoad(multishuttle.DepthDist, caseload2, false, "", rackconv);
                        shuttle.Control.Goto(destionationdistance, "");
                        shuttle.Control.DropLoad(multishuttle.DepthDist, true, "", null);
                        shuttle.Control.Start();
                    }
                    else if (destinationPosition2.Substring(0, 1) == "O")
                    {
                        //Destination outfeed rack                                               
                        string level = currentPosition2.Substring(8, 2);
                        Shuttle shuttle = GetShuttle(level, multishuttle);
                        if (shuttle == null)
                        {
                            LogMessage("Error. Could not find shuttle on level " + level, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        //Check level
                        string destinationlevel = destinationPosition2.Substring(8, 2);
                        if (level != destinationlevel)
                        {
                            LogMessage("Error. Current level does not matcht destination level!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        //Check position (should be POS2)
                        string pos = destinationPosition2.Substring(5, 3);
                        if (pos != multishuttle.POS2OUTFEED)
                        {
                            LogMessage("Error. Outfeedjob position is not " + multishuttle.POS2OUTFEED + " ! Position: " + pos, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        string elevatorname = destinationPosition2.Substring(3, 2);
                        string key = elevatorname + level;

                        if (!multishuttle.RackConveyors.ContainsKey(key))
                        {
                            LogMessage("Error. Could not find rack conveyor " + elevatorname + " at level " + destinationlevel, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }

                        RackConveyor destrackconv = multishuttle.RackConveyors[key];
                        if (destrackconv.RackConveyorType == MultiShuttleDirections.Infeed)
                        {
                            LogMessage("Error. Destination rack is not outfeed rack!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }
                                                         
                        caseload2.UserData = null;//Delete elevator job from userdata.
                        RackConveyor infeedrackconv = caseload2.CurrentActionPoint.Parent.Parent as RackConveyor;

                        float positionoffset = multishuttle.RackConveyorLength / 4;
                        if (infeedrackconv.LocalYaw == (float)Math.PI) {positionoffset = -multishuttle.RackConveyorLength / 4;}

                        float pickdistance = multishuttle.Raillength / 2 - infeedrackconv.LocalPosition.X + positionoffset;// rackconv.Length - rackconv.Length / 4;

                        float positionoffsetdrop = multishuttle.RackConveyorLength / 4;

                        if (destrackconv.LocalYaw == (float)Math.PI) { positionoffsetdrop = -multishuttle.RackConveyorLength / 4;}

                        float dropdistance = multishuttle.Raillength / 2 - destrackconv.LocalPosition.X + positionoffsetdrop;// rackconv.Length - rackconv.Length / 4;                  

                        shuttle.Control.Goto(pickdistance, "");
                        shuttle.Control.PickLoad(multishuttle.DepthDist, caseload2, false, "", infeedrackconv);
                        shuttle.Control.Goto(dropdistance, "");
                        shuttle.Control.DropLoad(multishuttle.DepthDist, false, "", destrackconv);
                        shuttle.Control.Start();

                        //Future update if infeed and outfeed racks are mixed on same side:
                        //Check if outfeed rack is connected to this elevator! Else totes has to go to infeed rack, to shuttle, to outfeed rack

                    }

                }
                #endregion

                #region current outfeed rack conv
                else if (currentPosition2.Substring(0, 1) == "O")
                {
                    //current outfeed rack conv
                    string picklevel = currentPosition2.Substring(8, 2);

                    string[] secondsplittelegram = null;

                    if (currentPosition1.Length >= 10 && datasets == 2)
                    {
                        string secondpicklevel = currentPosition1.Substring(8, 2);
                        if (secondpicklevel != picklevel)
                        {
                            //double outfeed job with two different outfeed rack levels.

                            //Handle as two 01 telegrams with datasets == 1
                            datasets = 1;

                            secondsplittelegram = new string[20];
                            for (int k = 0; k < 20; k++)
                            {
                                if (k < 5)       {secondsplittelegram[k] = splittelegram[k];}
                                else if (k == 5) {secondsplittelegram[k] = "1";}
                                else             {secondsplittelegram[k] = splittelegram[k + 11];}
                            }
                        }
                    }
                    string rackname = currentPosition2.Substring(3, 2);
                    string destGroupSide = destinationPosition2.Substring(3, 2);
                    string destinationlevel = destinationPosition2.Substring(8, 2);

                    string key = rackname + picklevel;

                    if (!multishuttle.RackConveyors.ContainsKey(key))
                    {
                        Core.Environment.Log.Write(Name + " error: Rack location not found " + currentPosition2);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    RackConveyor rackconv = multishuttle.RackConveyors[key];

                    MultiShuttleElevator elevator = rackconv.Elevator;

                    if (datasets == 2 && caseload1.CurrentActionPoint == null)
                    {
                        LogMessage("Error. No Current action point for current position: " + currentPosition2, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    if (datasets == 1 && caseload2.CurrentActionPoint == null)
                    {
                        LogMessage("Error. No Current action point for current position: " + currentPosition2, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (elevator == null)
                    {
                        LogMessage("Error. No elevator found for current position: " + currentPosition2, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    MultishuttleElevatorJobData elevatordata = new MultishuttleElevatorJobData()
                    {
                        JobType = MultiShuttleDirections.Outfeed,
                        JobMode = MultishuttleElevatorJobData.JobModes.Load1,
                        CaseLoadPosition1 = caseload2,
                        MissionTelegram = splittelegram,
                        DestinationsLevel = destinationlevel,
                        DestinationGroupSide = destGroupSide,
                        Parent = elevator
                    };
                    if (datasets == 2)
                    {
                        elevatordata.JobMode = MultishuttleElevatorJobData.JobModes.Load2;
                        elevatordata.CaseLoadPosition1 = caseload1;
                        elevatordata.CaseLoadPosition2 = caseload2;

                        if (caseload2.UserData is MultishuttleElevatorJobData)
                        {
                            LogMessage("Error. caseload on position " + caseload2.Case_Data.CurrentPosition + " ULID " + caseload2.ULID + " already has elevator job!", Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }
                    }

                    if (destinationPosition2.Substring(0, 1) == "I")
                        elevatordata.JobType = MultiShuttleDirections.Infeed;

                    if (datasets == 2 && caseload1.UserData is MultishuttleElevatorJobData)
                    {
                        LogMessage("Error. caseload on position " + caseload1.Case_Data.CurrentPosition + " ULID " + caseload1.ULID + " already has elevator job!", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    if (datasets == 1 && caseload2.UserData is MultishuttleElevatorJobData)
                    {
                        LogMessage("Error. caseload on position " + caseload2.Case_Data.CurrentPosition + " ULID " + caseload2.ULID + " already has elevator job!", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    if (caseload1 != null && datasets == 2)
                        caseload1.UserData = elevatordata;

                    if (caseload2 != null)
                        caseload2.UserData = elevatordata;

                    elevator.Control.Goto(rackconv.LocalPosition.Y, "", elevatordata);
                    if (elevator.CurrentJobData == null)
                    {
                        OutfeedDropStationConveyor outConv = elevator.ParentMultiShuttle.DropStationPoints[destGroupSide + destinationlevel];

                        if (!outConv.Location1.Active && !outConv.Location2.Active && outConv.NextRouteStatus.Available == AvailableStatus.Available)
                        {
                            elevator.Control.Start();
                        }
                    }

                    if (secondsplittelegram != null)
                        Telegram01Recieved(secondsplittelegram, 1);

                }
                #endregion

                #region current shuttle car
                else if (currentPosition2.Substring(0, 1) == "S")
                {
                    //current shuttle car
                }
                #endregion

            }

            private float GetXDistance(int xCoord, DematicMultiShuttle multishuttle)
            {
                if (xCoord < 1)
                {
                    LogMessage("Error: X coordinate less then 1: " + xCoord, Color.Red);
                    Core.Environment.Scene.Pause();
                    xCoord = 1;
                }

                if (multishuttle.MultiShuttleDriveThrough)
                {
                    //assuming equal number on both sides!
                    int midpoint = multishuttle.RackBays / 2;
                    if (xCoord <= midpoint)
                        return (xCoord - 0.5f) * multishuttle.BayLength;
                    
                    return (xCoord - 0.5f) * multishuttle.BayLength + multishuttle.RackConveyorLength * 2 + multishuttle.ElevatorConveyorLength;
                }

                if (multishuttle.FrontLeftElevator || multishuttle.FrontRightElevator)
                    return (xCoord - 0.5f) * multishuttle.BayLength + multishuttle.RackConveyorLength;
                
                return (xCoord - 0.5f) * multishuttle.BayLength;
            }

            private void AckTelegramRecieved(Core.Communication.Connection sender, string telegram)
            {    
                //Ack telegram recieved
                //Handled by BK10 protocol...
            }

            void GetLengthWidthHeightWeight(Case_Load caseload, out string length, out string width, out string height, out string weight)
            {
                length = (caseload.Width * 1000).ToString("0000"); //Unit Load Length (x-axis)
                width = (caseload.Length * 1000).ToString("0000"); //Unit Load Width (z-axis)
                height = (caseload.Height * 1000).ToString("0000");
                weight = (caseload.CaseWeight * 1000).ToString("000000");

                if (caseload.MissionTelegram != null && caseload.MissionTelegram.Length > 16)
                {
                    //just return what was received in mission telegram?
                    if (caseload.MissionTelegram[10] == caseload.ULID)
                    {
                        length = caseload.MissionTelegram[12];
                        width = caseload.MissionTelegram[13];
                        height = caseload.MissionTelegram[14];
                        weight = caseload.MissionTelegram[15];
                    }
                    else if (caseload.MissionTelegram.Length > 26 && caseload.MissionTelegram[21] == caseload.ULID)
                    {
                        length = caseload.MissionTelegram[23];
                        width = caseload.MissionTelegram[24];
                        height = caseload.MissionTelegram[25];
                        weight = caseload.MissionTelegram[26];
                    }
                }

            }

            private string MissionDataSet(Case_Load caseload)
            {
                string body = "";

                if (GetSingleMissionDataSetBody != null)
                    body = GetSingleMissionDataSetBody(this, caseload); //User can make the telegram body
                else
                {
                    string length, width, height, weight;
                    GetLengthWidthHeightWeight(caseload, out length, out width, out height, out weight);

                    body = caseload.Case_Data.OriginalPosition + separationCharacter +
                                  caseload.Case_Data.CurrentPosition + separationCharacter +
                                  caseload.Case_Data.DestinationPosition + separationCharacter +
                                  caseload.Case_Data.MissionStatus + separationCharacter +
                                  caseload.Case_Data.ULID + separationCharacter +
                                  caseload.Case_Data.ULType + separationCharacter +
                                  length + separationCharacter +
                                  width + separationCharacter +
                                  height + separationCharacter +
                                  weight + separationCharacter +
                                  caseload.Case_Data.TimeStamp + separationCharacter;
                }

                return body;
            }

            private string MissionDataSet2(Case_Load caseload2, Case_Load caseload1)
            {
                string body = "";
                if (GetDoubleMissionDataSetBody != null)
                    body = GetDoubleMissionDataSetBody(this, caseload2, caseload1);  //User can make the telegram body
                else
                {

                    string length2, width2, height2, weight2;
                    GetLengthWidthHeightWeight(caseload2, out length2, out width2, out height2, out weight2);

                    string length1, width1, height1, weight1;
                    GetLengthWidthHeightWeight(caseload1, out length1, out width1, out height1, out weight1);

                    body = caseload2.Case_Data.OriginalPosition + separationCharacter +
                                  caseload2.Case_Data.CurrentPosition + separationCharacter +
                                  caseload2.Case_Data.DestinationPosition + separationCharacter +
                                  caseload2.Case_Data.MissionStatus + separationCharacter +
                                  caseload2.Case_Data.ULID + separationCharacter +
                                  caseload2.Case_Data.ULType + separationCharacter +
                                  length2 + separationCharacter +
                                  width2 + separationCharacter +
                                  height2 + separationCharacter +
                                  weight2 + separationCharacter +
                                  caseload2.Case_Data.TimeStamp + separationCharacter +
                                  caseload1.Case_Data.OriginalPosition + separationCharacter +
                                  caseload1.Case_Data.CurrentPosition + separationCharacter +
                                  caseload1.Case_Data.DestinationPosition + separationCharacter +
                                  caseload1.Case_Data.MissionStatus + separationCharacter +
                                  caseload1.Case_Data.ULID + separationCharacter +
                                  caseload1.Case_Data.ULType + separationCharacter +
                                  length1 + separationCharacter +
                                  width1 + separationCharacter +
                                  height1 + separationCharacter +
                                  weight1 + separationCharacter +
                                  caseload1.Case_Data.TimeStamp + separationCharacter;
                }

                return body;
            }

            public void InfeedTimeOut(Case_Load caseload2, Case_Load caseload1)
            {
                string Position1ULID = "";
                string Position1ULBarcode = "";
                string Position1ULHeight = "";
                int casedatacount = 1;              

                if (caseload1 != null)
                {
                    //caseload1.Case_Data.CurrentPosition = infeedInfo.location1Name;
                    caseload1.Case_Data.RoutingTableUpdateWait = true;

                    Position1ULID = caseload1.Case_Data.ULID;
                    Position1ULBarcode = SSCCBarcodePrefix + caseload1.Case_Data.SSCCBarcode;
                    Position1ULHeight = (caseload1.Height * 1000).ToString("0000");
                }

                // caseload2.Case_Data.CurrentPosition = infeedInfo.location2Name;
                caseload2.Case_Data.RoutingTableUpdateWait = true;

                string Position2ULHeight = (caseload2.Height * 1000).ToString("0000");

                string body = PickStationDataSet(caseload2, caseload1, caseload2.Case_Data.CurrentPosition,
                                          caseload2.Case_Data.ULID,
                                          SSCCBarcodePrefix + caseload2.Case_Data.SSCCBarcode,
                                          Position2ULHeight,
                                          Position1ULID,
                                          Position1ULBarcode,
                                          Position1ULHeight);



                SendTelegram("25", body, casedatacount);
            }

            private string PickStationDataSet(Case_Load caseload2, Case_Load caseload1, string PickStation, string Position2ULID, string Position2ULBarcode, string Position2ULHeight, string Position1ULID, string Position1ULBarcode, string Position1ULHeight)
            {
                string body = "";

                if (GetPickStationDataSetBody != null)
                    body = GetPickStationDataSetBody(this, caseload2, caseload1, PickStation, Position2ULID, Position2ULBarcode, Position2ULHeight, Position1ULID, Position1ULBarcode, Position1ULHeight); //User can make the telegram body
                else
                    body = PickStation + separationCharacter +
                              Position2ULID + separationCharacter +
                              Position2ULBarcode + separationCharacter +
                              Position2ULHeight + separationCharacter +
                              Position1ULID + separationCharacter +
                              Position1ULBarcode + separationCharacter +
                              Position1ULHeight + separationCharacter;

                return body;
            }

            private void SendTelegram(string type, string body, int casedatacount)
            {
                if (connectionSending == null)
                {
                    LogMessage("Error: Sending connection is null!", Color.Red);
                    return;
                }
                if (connectionSending.State != Core.Communication.State.Connected)
                {
                    LogMessage("Error: Sending connection is not connected!", Color.Red);
                    return;
                }

                string casedata = casedatacount.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');

                string header = "/" + separationCharacter + separationCharacter + type + separationCharacter + PLCID + separationCharacter + WMSID + separationCharacter + casedata + separationCharacter;      //Header
                string tail = separationCharacter + "13" + separationCharacter + "10";

                string telegram = header + body + tail;

                if (telegram == lastSentTelegram)
                {
                    Core.Environment.Log.Write("Note: " + this.Name + " tried sending same telegram twice.",  Core.Environment.Log.Filter.Debug);
                    return; //Hack-  dont send the same twice...
                }

                LogMessage(DateTime.Now + " MSC>MFH: " + PLCID + " " + telegram, false);
                connectionSending.Send(telegram);

                lastSentTelegram = telegram;
            }

            #endregion

            #region Control logic

            void Shuttle_FinishedJob(Shuttle shuttlecar, ShuttleJob job)
            {
                #region Check if shuttle should send exception
                if (job.JobType == ShuttleJob.JobTypes.Goto && shuttlecar.ShuttleCar.ExceptionType != ShuttleCar.ExceptionTypes.None)
                {
                    //Check if next job is drop or pick in rack. If yes then stop shuttle and send exception. Wait for modify mission or cancel mission for bin empty.

                    if (shuttlecar.Control.JobQueue.Count > 0 && shuttlecar.Control.JobQueue[0].JobType == ShuttleJob.JobTypes.Drop && shuttlecar.Control.JobQueue[0].Rack)
                    {
                        Case_Load caseload = shuttlecar.CurrentLoad as Case_Load;
                        if (caseload != null)
                        {
                            if (shuttlecar.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinStoreBlocked)
                            {
                                //Only send blocked exception if depth > 1
                                if (caseload.Case_Data.MissionTelegram != null && caseload.Case_Data.MissionTelegram[8] != null && caseload.Case_Data.MissionTelegram[8].Length > 5)
                                {
                                    string depthstring = caseload.Case_Data.MissionTelegram[8].Substring(3, 1);
                                    int depth = int.Parse(depthstring);
                                    if (depth > 1)
                                    {
                                        caseload.Case_Data.MissionStatus = "12";
                                        string body = MissionDataSet(caseload);
                                        SendTelegram("06", body, 1);
                                        shuttlecar.Control.Stop();
                                        shuttlecar.ShuttleCar.InException = true;
                                        return;
                                    }
                                }
                            }
                            else if (shuttlecar.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinStoreFull)
                            {
                                caseload.Case_Data.MissionStatus = "04";
                                string body = MissionDataSet(caseload);
                                SendTelegram("06", body, 1);
                                shuttlecar.Control.Stop();
                                shuttlecar.ShuttleCar.InException = true;
                                return;
                            }
                        }
                    }
                    if (shuttlecar.Control.JobQueue.Count > 0 && shuttlecar.Control.JobQueue[0].JobType == ShuttleJob.JobTypes.Pick && shuttlecar.Control.JobQueue[0].Rack)
                    {
                        Case_Load caseload = shuttlecar.Control.JobQueue[0].Load as Case_Load;
                        if (caseload != null)
                        {
                            if (shuttlecar.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinRetrieveEmpty)
                            {
                                caseload.Case_Data.MissionStatus = "05";
                                string body = MissionDataSet(caseload);
                                SendTelegram("06", body, 1);
                                shuttlecar.Control.Stop();
                                shuttlecar.ShuttleCar.InException = true;
                                return;
                            }
                            
                            if (shuttlecar.ShuttleCar.ExceptionType == ShuttleCar.ExceptionTypes.BinRetrieveBlocked)
                            {
                                if (caseload.Case_Data.MissionTelegram != null && caseload.Case_Data.MissionTelegram[7] != null && caseload.Case_Data.MissionTelegram[7].Length > 5)
                                {
                                    //Only send blocked exception if depth > 1
                                    string depthstring = caseload.Case_Data.MissionTelegram[7].Substring(3, 1);
                                    int depth = int.Parse(depthstring);
                                    if (depth > 1)
                                    {
                                        caseload.Case_Data.MissionStatus = "11";
                                        string body = MissionDataSet(caseload);
                                        SendTelegram("06", body, 1);
                                        shuttlecar.Control.Stop();
                                        shuttlecar.ShuttleCar.InException = true;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion

                if (job.JobType == ShuttleJob.JobTypes.Drop && job.Rack)
                {
                    Case_Load caseload = job.Load as Case_Load;

                    if (caseload == null)
                    {
                        LogMessage("Error. Drop load is null! Level " + shuttlecar.Level, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    caseload.Case_Data.CurrentPosition = caseload.Case_Data.DestinationPosition;

                    string body = MissionDataSet(caseload);
                    SendTelegram("02", body, 1);
                }           
                else if (job.JobType == ShuttleJob.JobTypes.Pick)
                {
                    if (job.Rack) { Load.Items.Add(job.Load); }

                    Case_Load caseload = job.Load as Case_Load;
                    caseload.Case_Data.CurrentPosition = "S" + shuttlecar.Multishuttle.AisleNo + "     " + shuttlecar.Level;

                    string body = MissionDataSet(caseload);
                    SendTelegram("02", body, 1);
                }

                if (job.JobType == ShuttleJob.JobTypes.Goto)
                {
                    //Check next job
                    if (shuttlecar.Control.JobQueue.Count > 0)
                    {
                        ShuttleJob nextjob = shuttlecar.Control.JobQueue[0];
                        if (nextjob.JobType == ShuttleJob.JobTypes.Drop && !nextjob.Rack)
                        {
                            //Check if drop conv is occupied.
                            //if true then stop shuttle
                            RackConveyor conv = nextjob.UserData as RackConveyor;
                            if (conv.Location2.Active) { shuttlecar.Control.Stop(lockshuttle: true);}
                        }
                    }
                }
            }

            void Shuttle_LoadPicked(Shuttle shuttlecar, Load load, bool rack, string id, ShuttleJob job)
            {
                Case_Load caseload = load as Case_Load;
                if (caseload != null)
                {                    
                    caseload.Case_Data.CurrentPosition = "S" + shuttlecar.Multishuttle.AisleNo + "     " + shuttlecar.Level;//Update current position to be on the shuttle
                    //Remove elevator job
                    caseload.UserData = null;
                }
                if (job.JobType == ShuttleJob.JobTypes.Pick && !job.Rack)//Shuttle picks a tote from rack conv. Release position 1. 
                {                    
                    RackConveyor rackconv = job.UserData as RackConveyor;
                    rackconv.Location1.Release();
                    CheckElevators();
                }
            }

            void Shuttle_LoadDropped(Shuttle shuttlecar, Load load, bool rack, string id, ShuttleJob job)
            {             
                if (!rack)
                {
                    RackConveyor conv = job.UserData as RackConveyor;
                    load.Switch(conv.Location2, true);
                }

            }

            private void CheckElevators()
            {
                foreach (var elevator in multishuttles.SelectMany(multishuttle => multishuttle.elevators.Values))
                {
                    CheckElevator(elevator);
                }
            }

            private void CheckElevator(MultiShuttleElevator elevator)
            {
                if (elevator == null) { return;}
                if (elevator.CurrentJobData == null) {return;}
                if (!(elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.WaitingUnload1 || elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.WaitingUnload2))
                {
                    return;
                }

                if (elevator.ElevatorConveyor.Location1.Active || elevator.ElevatorConveyor.Location2.Active)
                {
                    if (elevator.CurrentJobData.UnloadConveyor.ConvRoute.Loads.Count < 2)
                    {
                        elevator.ElevatorConveyor.Location2.Release();
                        elevator.ElevatorConveyor.Location1.Release();

                        switch (elevator.CurrentJobData.JobMode)
                        {
                            case MultishuttleElevatorJobData.JobModes.WaitingUnload1: 
                                elevator.CurrentJobData.JobMode = MultishuttleElevatorJobData.JobModes.Unload1;
                                break;
                            case MultishuttleElevatorJobData.JobModes.WaitingUnload2:
                                elevator.CurrentJobData.JobMode = MultishuttleElevatorJobData.JobModes.Unload2;
                                break;
                        }    
                    }
                }

            }

            /// <summary>
            /// All elevator jobs are goto jobs. Loading and Unloading is handled by this controller!
            /// </summary>
            /// <param name="shuttlecar"></param>
            /// <param name="job"></param>
            void Elevator_FinishedGotoJob(Shuttle shuttlecar, ShuttleJob job)
            {
                try
                {
                    if (job.JobType != ShuttleJob.JobTypes.Goto)
                    {
                        return;
                    }

                    MultiShuttleElevator elevator = shuttlecar.UserData as MultiShuttleElevator;
                    MultishuttleElevatorJobData jobdata = job.UserData as MultishuttleElevatorJobData;
                    elevator.CurrentJobData = jobdata;

                    if (elevator.CurrentJobData == null)
                    {
                        Core.Environment.Scene.Pause();
                        LogMessage("Error. Elevator finished goto error.", Color.Red);
                    }

                    elevator.Control.Stop();

                    Case_Load caseLoadPos = (jobdata.CaseLoadPosition2 != null) ? jobdata.CaseLoadPosition2 : jobdata.CaseLoadPosition1;   //can both ever be null?....do I need to check??                  
                    IConvToElevator conv;
                    if (caseLoadPos.CurrentActionPoint != null)
                    {
                        conv = caseLoadPos.CurrentActionPoint.Parent.Parent.Parent as IConvToElevator;

                        if (jobdata.JobMode == MultishuttleElevatorJobData.JobModes.Load2 || jobdata.JobMode == MultishuttleElevatorJobData.JobModes.Load1)
                        {
                            conv.Location1.Release();
                            conv.Location2.Release();
                            return;
                        }
                    }

                    switch (jobdata.JobMode)
                    {

                        case MultishuttleElevatorJobData.JobModes.Unload1:

                            if (jobdata.JobType == MultiShuttleDirections.Infeed)
                            {
                                if (elevator.CurrentJobData.UnloadConveyor.ConvRoute.Loads.Count < 2)                               
                                {
                                    if (jobdata.CaseLoadPosition1 != null){ jobdata.CaseLoadPosition1.Release();}
                                    if (jobdata.CaseLoadPosition2 != null){ jobdata.CaseLoadPosition2.Release();}
                                }
                                else{ jobdata.JobMode = MultishuttleElevatorJobData.JobModes.WaitingUnload1;}
                            }
                            else if (jobdata.JobType == MultiShuttleDirections.Outfeed)
                            {
                                string key = jobdata.DestinationGroupSide + jobdata.DestinationsLevel;
                                if (!shuttlecar.Multishuttle.DropStationPoints.ContainsKey(key))
                                {
                                    LogMessage("Error. No outfeed found at: " + jobdata.DestinationGroupSide + " level: " + jobdata.DestinationsLevel, Color.Red);
                                    Core.Environment.Scene.Pause();
                                    return;
                                }

                                OutfeedDropStationConveyor outpoint = shuttlecar.Multishuttle.DropStationPoints[key];

                                SetSSCCBarcode(jobdata);   

                                //Update last location for totes
                                if (jobdata.CaseLoadPosition1 != null)
                                {                          
                                    jobdata.CaseLoadPosition1.LastLocation = null;

                                    if (jobdata.CaseLoadPosition1.Destination.Substring(0, 1) == "D") { jobdata.CaseLoadPosition1.UserData = outpoint;}//Going to drop station
                                    {
                                        jobdata.CaseLoadPosition1.Release();
                                    }

                                }

                                if (jobdata.CaseLoadPosition2 != null)
                                {
                                    jobdata.CaseLoadPosition2.LastLocation = null;

                                    if (jobdata.CaseLoadPosition2.Destination.Substring(0, 1) == "D")  { jobdata.CaseLoadPosition2.UserData = outpoint;}//Going to drop station
                                    {
                                        jobdata.CaseLoadPosition2.Release();
                                    }
                                }
                            }
                            break;

                        case MultishuttleElevatorJobData.JobModes.Unload2:
                         
                            if (jobdata.JobType == MultiShuttleDirections.Infeed)
                            {
                                if (elevator.CurrentJobData.UnloadConveyor.ConvRoute.Loads.Count < 2)
                                {
                                    jobdata.CaseLoadPosition1.Release();
                                    jobdata.CaseLoadPosition2.Release();
                                }
                                else {jobdata.JobMode = MultishuttleElevatorJobData.JobModes.WaitingUnload2;}
                            }
                            else if (jobdata.JobType == MultiShuttleDirections.Outfeed)
                            {
                                //Outfeed job
                                string key = jobdata.DestinationGroupSide + jobdata.DestinationsLevel;
                                if (!shuttlecar.Multishuttle.DropStationPoints.ContainsKey(key))
                                {
                                    LogMessage("Error. No outfeed found at: " + jobdata.DestinationGroupSide + " level: " + jobdata.DestinationsLevel, Color.Red);
                                    Core.Environment.Scene.Pause();
                                    return;
                                }
                                OutfeedDropStationConveyor outpoint = shuttlecar.Multishuttle.DropStationPoints[key];
                                SetSSCCBarcode(jobdata);   
  
                                //Update last location for totes
                                jobdata.CaseLoadPosition1.LastLocation = null;
                                jobdata.CaseLoadPosition2.LastLocation = null;
                                jobdata.CaseLoadPosition1.UserData = outpoint;
                                jobdata.CaseLoadPosition2.UserData = outpoint;

                                jobdata.CaseLoadPosition1.Release();
                                jobdata.CaseLoadPosition2.Release();

                            }
                            break;
                    }
                }
                catch (Exception se)
                {
                    LogMessage("Exception. Elevator finished goto job!", Color.Red);
                    Core.Environment.Log.Write(se);
                    Core.Environment.Scene.Pause();
                }
   
            }

            /// <summary>
            /// bit bodgy...only needed when connected to VFC.
            /// </summary>
            private void SetSSCCBarcode(MultishuttleElevatorJobData jobdata)
            {
                if (jobdata.CaseLoadPosition1 != null && jobdata.CaseLoadPosition1.Case_Data.SSCCBarcode == string.Empty) 
                {
                    jobdata.CaseLoadPosition1.Case_Data.SSCCBarcode = jobdata.CaseLoadPosition1.Case_Data.ULID;
                }
                if (jobdata.CaseLoadPosition2 != null && jobdata.CaseLoadPosition2.Case_Data.SSCCBarcode == string.Empty)
                {
                    jobdata.CaseLoadPosition2.Case_Data.SSCCBarcode = jobdata.CaseLoadPosition2.Case_Data.ULID;
                }
            }

            private void ToteArrivedAtConvElevatorSecondPosition(ActionPoint sender, Load load, MultiShuttleElevator elevator)
            {
                if (elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Load1 || elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Load2)
                {
                    load.Stop();
                    if (elevator.CurrentJobData.JobType == MultiShuttleDirections.Infeed)
                    {
                        StartElevatorInfeedAndSendArrival(elevator);
                    }
                    else
                    {
                        StartElevatorOutfeedAndSendArrival(elevator);
                    }
                }
                else if (elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload2)
                {
                    if (elevator.CurrentJobData.DropStationConv != null)
                    {
                        //Tote going to drop station. 
                        load.Stop();
                    }
                    else if (elevator.CurrentJobData.UnloadConveyor.ConvRoute.Loads.Count > 1)
                    {
                        elevator.CurrentJobData.JobMode = MultishuttleElevatorJobData.JobModes.WaitingUnload2;
                        load.Stop();
                    }
                }
                else if (elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload1)
                {
                    if (elevator.CurrentJobData.CaseLoadPosition1 != null)
                    {
                        //Second job is always CaseLoadPosition1
                        load.Stop();
                        StartSecondJob(elevator);
                    }
                }
                else
                {
                    Core.Environment.Scene.Pause();
                    LogMessage("Error. Elevatorlocation2", Color.Red);
                }
            }

            public void ToteArrivedAtConvElevatorLocation1(ActionPoint sender, Load load, MultiShuttleElevator elevator)
            {
                ToteArrivedAtConvElevatorSecondPosition(sender, load, elevator);
            }

            public void ToteArrivedAtConvElevatorLocation2(ActionPoint sender, Load load, MultiShuttleElevator elevator)
            {
                ToteArrivedAtConvElevatorSecondPosition(sender, load, elevator);
            }

            public void StartSecondJob(MultiShuttleElevator elevator)
            {
                if (elevator.CurrentJobData.CaseLoadPosition1 == null)
                    elevator.CurrentJobData = null;
                else
                {
                    string destinationlevel                  = elevator.CurrentJobData.CaseLoadPosition1.Destination.Substring(8, 2);
                    string destgroupside                     = elevator.CurrentJobData.CaseLoadPosition1.Destination.Substring(3, 2);
                    string convType                          = elevator.CurrentJobData.CaseLoadPosition1.Destination.Substring(0, 1);
                    MultishuttleElevatorJobData elevatordata = new MultishuttleElevatorJobData
                    {
                        JobType              = MultiShuttleDirections.Infeed,
                        JobMode              = MultishuttleElevatorJobData.JobModes.Unload1,
                        CaseLoadPosition2    = elevator.CurrentJobData.CaseLoadPosition1,
                        MissionTelegram      = elevator.CurrentJobData.MissionTelegram,
                        DestinationsLevel    = destinationlevel,
                        DestinationGroupSide = destgroupside,
                        Parent               = elevator
                    };

                    elevator.CurrentJobData = elevatordata;
                    string key = destgroupside + elevator.CurrentJobData.DestinationsLevel;

                    OutfeedDropStationConveyor outpoint = null;
                    if (convType == "D")
                    {
                        //Pick station to drop station move
                        elevatordata.JobType = MultiShuttleDirections.Outfeed;
                        if (!elevator.ParentMultiShuttle.DropStationPoints.ContainsKey(key))
                        {
                            LogMessage("Error. No outfeed found at: " + destgroupside + " level: " + elevator.CurrentJobData.DestinationsLevel, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }
                        outpoint = elevator.ParentMultiShuttle.DropStationPoints[key];
                        elevatordata.CaseLoadPosition2.UserData = outpoint;
                    }


                    RackConveyor rackconv = null;
                    if (!elevator.ParentMultiShuttle.RackConveyors.ContainsKey(key) && elevatordata.JobType == MultiShuttleDirections.Infeed)
                    {
                        LogMessage("Error. Could not find rack conveyor " + elevator.ElevatorName + " at level " + elevator.CurrentJobData.DestinationsLevel, Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }
                    if (elevatordata.JobType == MultiShuttleDirections.Infeed)
                    {
                        rackconv = elevator.ParentMultiShuttle.RackConveyors[key];

                        if (rackconv.RackConveyorType == MultiShuttleDirections.Outfeed)
                        {
                            string dest = "";
                            if (elevator.CurrentJobData.MissionTelegram != null && elevator.CurrentJobData.MissionTelegram.Length > 8)
                                dest = elevator.CurrentJobData.MissionTelegram[8];
                            LogMessage("Error. Destination is not an infeed rack conveyor " + dest, Color.Red);
                            Core.Environment.Scene.Pause();
                            return;
                        }
                    }

                    elevator.CurrentJobData.UnloadConveyor = rackconv;
                    elevator.CurrentJobData.DropStationConv = outpoint as OutfeedDropStationConveyor;

                    float destheight = 0;
                    if (rackconv != null)
                    {
                        destheight = rackconv.LocalPosition.Y;
                    }
                    else if (outpoint != null)
                    {
                        destheight = outpoint.LocalPosition.Y;
                    }
                    else
                    {
                        LogMessage("Error. No drop station or outfeed rack height found!", Color.Red);
                    }

                    List<ShuttleJob> jobqueue = new List<ShuttleJob>(elevator.Control.JobQueue);
                    elevator.Control.JobQueue.Clear();
                    elevator.Control.Goto(destheight, "", elevator.CurrentJobData);
                    elevator.Control.JobQueue.AddRange(jobqueue);

                }

                elevator.Control.Start();  //in case elevator has more jobs.
            }

            public void StartElevatorOutfeedAndSendArrival(MultiShuttleElevator elevator)
            {
                int casedatacount = 1;
                if (elevator.CurrentJobData.CaseLoadPosition2 != null && elevator.CurrentJobData.CaseLoadPosition1 != null)
                    casedatacount = 2;

                string key = elevator.CurrentJobData.DestinationGroupSide + elevator.CurrentJobData.DestinationsLevel;

                if (!elevator.ParentMultiShuttle.DropStationPoints.ContainsKey(key))
                {
                    LogMessage("Error. Could not find outfeed point " + elevator.ElevatorName + " at level " + elevator.CurrentJobData.DestinationsLevel, Color.Red);
                    return;
                }

                OutfeedDropStationConveyor outfeedpoint = elevator.ParentMultiShuttle.DropStationPoints[key];

                if (elevator != outfeedpoint.Elevator)
                {
                    LogMessage("Error. Drop position is not on this elevator!", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                Send02Elevator(elevator, casedatacount, elevator.CurrentJobData.CaseLoadPosition1, elevator.CurrentJobData.CaseLoadPosition2);

                //Remove any jobs to make sure this is the first job
                List<ShuttleJob> jobqueue = new List<ShuttleJob>(elevator.Control.JobQueue);
                elevator.Control.JobQueue.Clear();

                //start elevator
                elevator.Control.Goto(outfeedpoint.LocalPosition.Y, "", elevator.CurrentJobData);
                elevator.Control.Start();

                //Add any removed jobs
                elevator.Control.JobQueue.AddRange(jobqueue);
            }

            public void StartElevatorInfeedAndSendArrival(MultiShuttleElevator elevator)
            {
                int casedatacount = 1;
                if (elevator.CurrentJobData.CaseLoadPosition2 != null && elevator.CurrentJobData.CaseLoadPosition1 != null){casedatacount = 2;}
                
                string key = elevator.CurrentJobData.DestinationGroupSide + elevator.CurrentJobData.DestinationsLevel;

                if (!elevator.ParentMultiShuttle.RackConveyors.ContainsKey(key))
                {
                    LogMessage("Error. Could not find rack conveyor " + elevator.ElevatorName + " at level " + elevator.CurrentJobData.DestinationsLevel, Color.Red);
                    return;
                }

                RackConveyor rackconv = elevator.ParentMultiShuttle.RackConveyors[key];
                Send02Elevator(elevator, casedatacount, elevator.CurrentJobData.CaseLoadPosition2, elevator.CurrentJobData.CaseLoadPosition1);

                //Remove any jobs to make sure this is the first job
                List<ShuttleJob> jobqueue = new List<ShuttleJob>(elevator.Control.JobQueue);
                elevator.Control.JobQueue.Clear();

                //start elevator
                elevator.Control.Goto(rackconv.LocalPosition.Y, "", elevator.CurrentJobData);
                elevator.Control.Start();

                //Add any removed jobs
                elevator.Control.JobQueue.AddRange(jobqueue);
            }

            private void Send02Elevator(MultiShuttleElevator elevator, int casedatacount, Case_Load caseLoadPosA, Case_Load caseLoadPosB)
            {
                string body = "";

                string pos1 = elevator.ParentMultiShuttle.POS1;
                string pos2 = elevator.ParentMultiShuttle.POS2;


                //Create telegram body
                if (casedatacount == 1 && caseLoadPosA != null)
                {
                    //Update current position (one tote always on pos 2)
                    caseLoadPosA.Case_Data.CurrentPosition = "E" + elevator.ParentMultiShuttle.AisleNo + elevator.ElevatorName + pos2 + "  ";
                    body = MissionDataSet(caseLoadPosA);
                }
                else if (casedatacount == 1 && caseLoadPosB != null)
                {
                    //Update current position (one tote always on pos 2)
                    caseLoadPosB.Case_Data.CurrentPosition = "E" + elevator.ParentMultiShuttle.AisleNo + elevator.ElevatorName + pos2 + "  ";
                    body = MissionDataSet(caseLoadPosB);
                }
                else if (casedatacount == 2)
                {
                    //Update current position
                    caseLoadPosA.Case_Data.CurrentPosition = "E" + elevator.ParentMultiShuttle.AisleNo + elevator.ElevatorName + pos1 + "  ";
                    caseLoadPosB.Case_Data.CurrentPosition = "E" + elevator.ParentMultiShuttle.AisleNo + elevator.ElevatorName + pos2 + "  ";
                    body = MissionDataSet2(caseLoadPosA, caseLoadPosB);
                }

                SendTelegram("02", body, casedatacount);
            }

            /// <summary>
            /// Handels the first tote to arrive at elevator pickup. Will set up a timer and wait for a timeout or a second tote
            /// </summary>
            /// <param name="sender"></param>
            /// <param name="load"></param>
            /// <param name="multishuttle"></param>
            public void ToteArrivedAtConvPickStationLocation2(ActionPoint sender, Load load, DematicMultiShuttle multishuttle)
            {
                if (load.UserData is MultishuttleElevatorJobData) { return;} //caseload is moving on to elevator. Ignore.

                load.Stop();

                InfeedPickStationConveyor conv = sender.Parent.Parent.Parent as InfeedPickStationConveyor;

                if (!multishuttle.PickStationNameToActionPoint[conv.infeedInfo.location1Name].Active) //No caseload on location 1. Wait...
                {
                    load.WaitingTime = multishuttle.PickStation2Timeout; //Wait for tote number 2
                    load.OnFinishedWaitingEvent += Tote_PickStation2TimeOut;
                }
                else {Tote_PickStation2TimeOut(load);} //A caseload waits on location 1}
            }

            /// <summary>
            /// The first tote to arrive and wait for an elevator sets a timer this timer has either expired or a second tote has arrived
            /// and stopped the timer triggering this method to be called via the OnFinishedWaitingEvent.
            /// </summary>
            /// <param name="load"></param>
            void Tote_PickStation2TimeOut(Load load)
            {
                load.OnFinishedWaitingEvent -= Tote_PickStation2TimeOut;
                load.Stop();

                InfeedPickStationConveyor conv = load.CurrentActionPoint.Parent.Parent.Parent as InfeedPickStationConveyor;
                Case_Load caseload2            = conv.infeedInfo.multiShuttle.PickStationNameToActionPoint[conv.infeedInfo.location2Name].ActiveLoad as Case_Load;
                Case_Load caseload1            = conv.infeedInfo.multiShuttle.PickStationNameToActionPoint[conv.infeedInfo.location1Name].ActiveLoad as Case_Load;
  
                if (caseload2.UserData is MultishuttleElevatorJobData)
                {
                    LogMessage("Error. caseload2 on pick station already has elevatorjob!", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                if (caseload1 != null && caseload1.UserData is MultishuttleElevatorJobData)
                {
                    LogMessage("Error. caseload1 on pick station already has elevatorjob!", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                if (caseload2.Case_Data.RoutingTableUpdateWait)
                {
                    LogMessage("Error. caseload2 on pick station already has WMSWait (25 arrival already sent)! Barcode: " + caseload2.SSCCBarcode, Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                if (caseload1 != null && caseload1.Case_Data.RoutingTableUpdateWait)
                {
                    LogMessage("Error. caseload1 on pick station already has WMSWait (25 arrival already sent)! Barcode: " + caseload1.SSCCBarcode, Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }
          
                string Position1ULID      = "";
                string Position1ULBarcode = "";
                string Position1ULHeight  = "";
                int casedatacount         = 1;

                if (caseload1 != null)
                {
                    caseload1.Case_Data.CurrentPosition        = conv.infeedInfo.location1Name;
                    caseload1.Case_Data.RoutingTableUpdateWait = true;

                    Position1ULID      = caseload1.Case_Data.ULID;
                    Position1ULBarcode = SSCCBarcodePrefix + caseload1.Case_Data.SSCCBarcode;
                    Position1ULHeight  = (caseload1.Height * 1000).ToString("0000"); 
                }

                caseload2.Case_Data.CurrentPosition        = conv.infeedInfo.location2Name;
                caseload2.Case_Data.RoutingTableUpdateWait = true;             

                string Position2ULHeight = (caseload2.Height * 1000).ToString("0000");

                string body = PickStationDataSet(caseload2, caseload1, conv.infeedInfo.location2Name,
                                          caseload2.Case_Data.ULID,
                                          SSCCBarcodePrefix + caseload2.Case_Data.SSCCBarcode,
                                          Position2ULHeight,
                                          Position1ULID,
                                          Position1ULBarcode,
                                          Position1ULHeight);

                

                SendTelegram("25", body, casedatacount);  
            }

            public void ToteArrivedAtRackConvLocation1(ActionPoint sender, Load load, DematicMultiShuttle multishuttle)
            {
                RackConveyor conv = sender.Parent.Parent.Parent as RackConveyor;

                if (conv.RackConveyorType == MultiShuttleDirections.Infeed)
                {
                    MultiShuttleElevator elevator = sender.UserData as MultiShuttleElevator;
                    Case_Load caseload = load as Case_Load;
                    caseload.Case_Data.RoutingTableUpdateWait = false;
                    
                    caseload.Case_Data.CurrentPosition = "I" + multishuttle.AisleNo + sender.Name;//Update current postion

                    string currentlevel = caseload.Case_Data.CurrentPosition.Substring(8, 2);
                    string destinationlevel = caseload.Case_Data.DestinationPosition.Substring(8, 2);
                    if (currentlevel != destinationlevel)
                    {
                        LogMessage("Error. Current rack level is not destination level!!", Color.Red);
                        Core.Environment.Scene.Pause();
                        return;
                    }

                    if (elevator.NumberOfTotes == 0)
                    {
                        elevator.CurrentJobData = null;
                        elevator.Control.Start();  //in case elevator has more jobs.
                    }
                    
                    MultishuttleElevatorJobData elevatorjob = load.UserData as MultishuttleElevatorJobData;

                    if (conv.ConvRoute.Loads.Count > 1)
                    {
                        load.Stop();
                    }

                    if (elevatorjob.JobMode == MultishuttleElevatorJobData.JobModes.Unload1)
                    {
                        string body = MissionDataSet(caseload);
                        SendTelegram("02", body, 1);
                    }
                    else if (elevatorjob.JobMode == MultishuttleElevatorJobData.JobModes.Unload2)
                    {
                        if (CheckDoubleArrival(elevatorjob, conv.Location1, conv.Location2, multishuttle))
                        {
                            elevatorjob.CaseLoadPosition2.Case_Data.RoutingTableUpdateWait = true;
                            string body = MissionDataSet2(elevatorjob.CaseLoadPosition2, elevatorjob.CaseLoadPosition1);
                            SendTelegram("02", body, 2);
                        }
                    }
                }
                else if (conv.RackConveyorType == MultiShuttleDirections.Outfeed)
                {
                    if (!conv.Location2.Active)
                    {
                        //Location 2 is free, shuttle is not running, next job is drop, not in rack ( == outfeed rack conv).
                        Shuttle shuttle = GetShuttle(conv.Level, multishuttle);
                        if (shuttle.Control.CurrentJob == null && !shuttle.Control.Running && shuttle.Control.JobQueue.Count > 0 && shuttle.Control.ShuttleLocked)
                        {
                            ShuttleJob nextjob = shuttle.Control.JobQueue[0];
                            if (nextjob.JobType == ShuttleJob.JobTypes.Drop && !nextjob.Rack)
                            {
                                //Check if drop conv is occupied. There might be more outfeed conveyors!
                                RackConveyor dropconv = nextjob.UserData as RackConveyor;
                                if (!dropconv.Location2.Active) {shuttle.Control.Start(unLockshuttle: true);}
                            }
                        }

                    }

                    if (load.UserData is MultishuttleElevatorJobData) { return;} //Tote is moving on to elevator. ignore

                    load.Stop();
                    Case_Load caseload = load as Case_Load;
                    caseload.Case_Data.CurrentPosition = "O" + multishuttle.AisleNo + conv.RackName.Substring(0, 2) + multishuttle.POS1OUTFEED + conv.Level;//Update current position                     
                    string body = MissionDataSet(caseload);
                    SendTelegram("02", body, 1);     
                }

            }

            private bool CheckDoubleArrival(MultishuttleElevatorJobData elevatorjob, ActionPoint loc1, ActionPoint loc2, DematicMultiShuttle multishuttle)
            {
                bool result = false;
                //Unload 2. Front case arrives. Send double arrival message.
                if (loc1.Active && loc2.Active && elevatorjob.CaseLoadPosition2 == loc2.ActiveLoad)
                {
                    //Both cases arrived
                    if (!elevatorjob.CaseLoadPosition2.Case_Data.RoutingTableUpdateWait)
                    {
                        //Update current postion
                        elevatorjob.CaseLoadPosition1.Case_Data.CurrentPosition = "I" + multishuttle.AisleNo + loc1.Name;
                        elevatorjob.CaseLoadPosition2.Case_Data.CurrentPosition = "I" + multishuttle.AisleNo + loc2.Name;
                        result = true;
                    }
                }
                return result;
            }

            public void ToteArrivedAtRackConvLocation2(ActionPoint sender, Load load, DematicMultiShuttle multishuttle)
            {
               
                RackConveyor conv = sender.Parent.Parent.Parent as RackConveyor;
                
                if (conv.RackConveyorType == MultiShuttleDirections.Infeed)
                {
                    MultishuttleElevatorJobData elevatorjob = load.UserData as MultishuttleElevatorJobData;
                    load.Stop();
                    Case_Load caseload = load as Case_Load;
                    caseload.Case_Data.RoutingTableUpdateWait = false;

                    //Update current postion
                    caseload.Case_Data.CurrentPosition = "I" + multishuttle.AisleNo + sender.Name;

                    if (elevatorjob.JobMode == MultishuttleElevatorJobData.JobModes.Unload2)
                    {
                        //Unload 2. Front case arrives. Send double arrival message.
                        if (CheckDoubleArrival(elevatorjob, conv.Location1, conv.Location2, multishuttle))
                        {
                            elevatorjob.CaseLoadPosition2.Case_Data.RoutingTableUpdateWait = true;
                            string body = MissionDataSet2(elevatorjob.CaseLoadPosition2, elevatorjob.CaseLoadPosition1);
                            SendTelegram("02", body, 2);
                        }
                        else if (conv.Location2.ActiveLoad == elevatorjob.CaseLoadPosition1)
                        {
                            //Last case arrives at pos2 in double unload.
                            //Send single arrival
                            caseload.Case_Data.RoutingTableUpdateWait = true;
                            //Send single 02 arrival                  
                            string body = MissionDataSet(caseload);
                            SendTelegram("02", body, 1);
                        }
                    }
                    else if (elevatorjob.JobMode == MultishuttleElevatorJobData.JobModes.Unload1)
                    {
                        caseload.Case_Data.RoutingTableUpdateWait = true;
                        //Send single 02 arrival                  
                        string body = MissionDataSet(caseload);
                        SendTelegram("02", body, 1);
                    }
                }
                else
                {
                    //conv.RackConveyorType
                    //Outfeed conv
                    load.Stop();

                    Case_Load caseload = load as Case_Load;
                
                    //Update current position 
                    caseload.Case_Data.CurrentPosition = "O" + multishuttle.AisleNo + conv.RackName.Substring(0, 2) + multishuttle.POS2OUTFEED + conv.Level;

                    //Send 02 arrival
                    string body = MissionDataSet(caseload);

                    SendTelegram("02", body, 1);

                    if (conv.ConvRoute.Loads.Count == 1)
                    {
                        load.Release();
                    }
                }
            }

            public void ToteArrivedAtConvDropStation(OutfeedDropStationConveyor dropStationPoint, MultiShuttleElevator elevator, Case_Load caseload, DematicMultiShuttle multishuttle)
            {
                if (elevator.CurrentJobData == null)
                {
                    LogMessage("Error. Tote arrived at drop station location 2 but elevator has no CurrentJobData!", Color.Red);
                    Core.Environment.Scene.Pause();
                    return;
                }

                if (elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload1)
                {
                    //caseload.Case_Data.CurrentPosition = "D" + multishuttle.AisleNo + dropStationPoint.DropPositionGroupSide + multishuttle.POS2OUTFEED + dropStationPoint.Level;
                    string body = MissionDataSet(caseload);
                    SendTelegram("02", body, 1);

                    if (elevator.CurrentJobData.JobType != MultiShuttleDirections.Outfeed) { return;}//Elevator started another job
  
                    //Elevator finished drop job (to drop station)
                    elevator.CurrentJobData = null;
                    elevator.Control.Start();
                    return;
                }
                
                if (elevator.CurrentJobData.JobMode == MultishuttleElevatorJobData.JobModes.Unload2)
                {              
                    string body = MissionDataSet2(elevator.CurrentJobData.CaseLoadPosition2, elevator.CurrentJobData.CaseLoadPosition1);
                    SendTelegram("02", body, 2);
                    elevator.CurrentJobData = null;
                    elevator.Control.Start();       
                }
            }

            #endregion

            #region Clear, Reset and Build control

            public override void Reset()
            {
                lastSentTelegram = string.Empty;
                base.Reset();
            }

            public void Build()
            {
                Clear();
                foreach (DematicMultiShuttle multishuttle in multishuttles)
                {
                    foreach (Shuttle shuttle in multishuttle.shuttlecars.Values)
                    {
                        shuttle.Control.FinishedJob += Shuttle_FinishedJob;
                        shuttle.Control.LoadDropped += Shuttle_LoadDropped;
                        shuttle.Control.LoadPicked += Shuttle_LoadPicked;
                    }

                    foreach (MultiShuttleElevator elevator in multishuttle.elevators.Values)
                        elevator.Control.FinishedJob += Elevator_FinishedGotoJob;
                }
            }

            public void Clear()
            {
                foreach (DematicMultiShuttle multishuttle in multishuttles)
                {
                    foreach (Shuttle shuttle in multishuttle.shuttlecars.Values)
                    {
                        shuttle.Control.FinishedJob -= Shuttle_FinishedJob;
                        shuttle.Control.LoadDropped -= Shuttle_LoadDropped;
                        shuttle.Control.LoadPicked -= Shuttle_LoadPicked;
                    }

                    foreach (MultiShuttleElevator elevator in multishuttle.elevators.Values)
                        elevator.Control.FinishedJob -= Elevator_FinishedGotoJob;
                }
            }

            public override void Dispose()
            {
                DematicMultiShuttle.AllControllers.Remove(this);

                if (connectionRecieving != null)
                    connectionRecieving.OnTelegramReceived -= Connection_TelegramReceived;

                if (connectionSending != null)
                    connectionSending.OnTelegramReceived -= Connection_TelegramReceived;
            }

            #endregion

            #region Assemmbly methods

            public override string Category
            {
                get { return "MultiShuttle Controller"; }
            }

            public override Image Image
            {
                get
                {

                    return Experior.Catalog.Dematic.DatcomUK.Common.Icons.Get("MSC");
                }
            }
            #endregion

            #region Helper methods

            private DematicMultiShuttle GetMultiShuttle(string aisleno)
            {
                return multishuttles.Find(m => m.AisleNo == aisleno);
            }

            public void AddMultiShuttle(DematicMultiShuttle multishuttle)
            {
                if (!multishuttles.Contains(multishuttle))
                {
                    multishuttles.Add(multishuttle);
                    Build(); // Rebuild tables and logic
                }
            }

            public void RemoveMultiShuttle(DematicMultiShuttle multishuttle)
            {
                if (multishuttles.Contains(multishuttle))
                {
                    multishuttles.Remove(multishuttle);
                    Build(); // Rebuild tables and logic
                }
            }

            private Shuttle GetShuttle(string level, DematicMultiShuttle multishuttle)
            {
                return multishuttle.shuttlecars.Values.FirstOrDefault(s => s.Level == level);
            }

            private Case_Load GetCaseLoadInMS(string ULID, DematicMultiShuttle multishuttle)
            {
                return multishuttle.caseloads.FirstOrDefault(c => c.ULID == ULID);
            }

            private void LogMessage(string message)
            {
                LogMessage(message, true);
            }

            private void LogMessage(string message, bool showname)
            {
                if (SuppressState == MSCSuppressStates.Never)
                {
                    if (showname)
                        Core.Environment.Log.Write(Name + ": " + message);
                    else
                        Core.Environment.Log.Write(message);
                }
            }

            private void LogMessage(string message, Color color)
            {
                if (SuppressState == MSCSuppressStates.Never)
                    Core.Environment.Log.Write(Name + ": " + message, color);
            }

            #endregion

            #region User interface
            [CategoryAttribute("PLC configuration")]
            [PropertyOrder(1)]
            [DescriptionAttribute("The internal connection no. that this assembly/PLC uses for communication")]
            [DisplayName("Connection recieving")]
            [TypeConverter(typeof(Connection.NameConverter))]
            public string ConnectionNameRecieving
            {
                get { return controllerinfo.ConnectionNameRecieving; }
                set
                {
                    controllerinfo.ConnectionNameRecieving = value;
                    CreateConnections();
                }
            }

            [CategoryAttribute("PLC configuration")]
            [PropertyOrder(1)]
            [DescriptionAttribute("The internal connection no. that this assembly/PLC uses for communication")]
            [DisplayName("Connection sending")]
            [TypeConverter(typeof(Connection.NameConverter))]
            public string ConnectionNameSending
            {
                get { return controllerinfo.ConnectionNameSending; }
                set
                {
                    controllerinfo.ConnectionNameSending = value;
                    CreateConnections();
                }
            }

            [CategoryAttribute("PLC configuration")]
            [PropertyOrder(2)]
            [DescriptionAttribute("The current status of the crane PLC")]
            [DisplayName("Status")]
            public PLCStates PLCState
            {
                get { return plcState; }
                set
                {
                    if (plcState != value)
                    {
                        plcState = value;
                        plcCube.Color = BaseConv.StatusColour[plcState];
                    }
                }
            }

            [CategoryAttribute("PLC configuration")]
            [PropertyOrder(3)]
            [DescriptionAttribute("The PLC ID sent/received in telegrams (not necessarily the same as the aisle no.). The number will be shown next to the physical PLC object. Please make sure that this is the exact same characters that are used in the received telegrams and that the number of characters doesn't exceed the length of the this field in the MVT.")]
            [DisplayName("PLC ID")]
            public string PLCID
            {
                get { return controllerinfo.PLCID; }
                set
                {
                    if (controllerinfo.PLCID != value)
                    {
                        controllerinfo.PLCID = value;
                        plcNum.Text = value;
                    }
                }
            }

            [CategoryAttribute("PLC configuration")]
            [PropertyOrder(4)]
            [DescriptionAttribute("The WMS ID sent/received in telegrams. Please make sure that this is the exact same characters that are used in the received telegrams and that the number of characters doesn't exceed the length of the this field in the MVT.")]
            [DisplayName("WMS ID")]
            public string WMSID
            {
                get { return controllerinfo.WMSID; }
                set { controllerinfo.WMSID = value; }
            }

            [CategoryAttribute("PLC configuration")]
            [PropertyOrder(5)]
            [DescriptionAttribute("Under which conditions should messages/telegrams sent to and from this object be printed in the log?")]
            [DisplayName("Suppress messages")]
            public MSCSuppressStates SuppressState
            {
                get { return controllerinfo.SuppressState; }
                set { controllerinfo.SuppressState = value; }
            }

            [CategoryAttribute("PLC configuration")]
            [PropertyOrder(20)]
            [DescriptionAttribute("Put this in front of barcodes on totes arriving at the pick station. Ex. BK10 uses 10 char barcode - Add 8 char prefix to get the 18 char SSCC barcode")]
            [DisplayName("SSCCBarcodePrefix")]
            public string SSCCBarcodePrefix
            {
                get { return controllerinfo.SSCCBarcodePrefix; }
                set { controllerinfo.SSCCBarcodePrefix = value; }
            }

            #endregion

            #region IController

            public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
            {
                throw new NotImplementedException();
            }

            public event EventHandler OnControllerDeletedEvent;

            public event EventHandler OnControllerRenamedEvent;

            public void SendCraneInputStationArrival(string craneNumber, List<Case_Load> EPCases, string status = "")
            {
                throw new NotImplementedException();
            }

            public void SendCraneInputStationArrival(string craneNumber, List<string> CaseBarcodes, string status = "")
            {
                throw new NotImplementedException();
            }

            public event PickStationStatus MiniloadPickStationStatusEvent;

            public void RemoveSSCCBarcode(string ULID)
            {
                throw new NotImplementedException();
            }

            #endregion

        }

        [Serializable]
        public class MultiShuttleControllerInfo : AssemblyInfo
        {
            public string ConnectionNameRecieving = "";
            public string ConnectionNameSending = "";
            public string WMSID = "01";
            public string PLCID = "";
            public Experior.Catalog.Dematic.Storage.Assemblies.MultiShuttleController.MSCSuppressStates SuppressState = MultiShuttleController.MSCSuppressStates.Never;
            public string SSCCBarcodePrefix = "";
        }   
}
