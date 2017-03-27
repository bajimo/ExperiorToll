using Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Experior.Dematic;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.DatcomUK.Assemblies
{
    /// <summary>
    /// This is a PLC that handels Datcom messages
    /// </summary>
    public class MHEController_Multishuttle : BaseController, IController, IMultiShuttleController
    {
        public static List<string> datcomVersion = new List<string>() { "3.0", "3.7" }; //Add new (or old) versions here and set the apprepriate action in the property DatcomVersion.
        public static int datcomVersionIndexOffset;

        MHEController_MultishuttleInfo mHEController_MultishuttleInfo;
        List<MultiShuttle> multishuttles = new List<MultiShuttle>();
        List<MHEControl> controls = new List<MHEControl>();

        public MHEController_Multishuttle(MHEController_MultishuttleInfo info) : base(info)
        {
            mHEController_MultishuttleInfo = info;
            DatcomVersion = info.datcomVersion;
            OnPLCStateChange += MHEController_Multishuttle_OnPLCStateChange;
        }

        void MHEController_Multishuttle_OnPLCStateChange(object sender, PLCStateChangeEventArgs e)
        {
            MSPLC_State = e._MSState;
        }

        public static event EventHandler<MSPLCStatusChangeEventArgs> OnMSPLCStatusChanged;

        private MultiShuttlePLC_State? _MSPLC_State = MultiShuttlePLC_State.Unknown_00;
        [Browsable(false)]
        public MultiShuttlePLC_State? MSPLC_State
        {
            get { return _MSPLC_State; }
            set
            {
                _MSPLC_State = value;
                if (OnMSPLCStatusChanged != null)
                {
                    OnMSPLCStatusChanged(this, new MSPLCStatusChangeEventArgs(value));
                }
            }
        }

        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            multishuttles.Add(assem as MultiShuttle);

            MHEControl protocolConfig = null;  //generic plc config object

            if (assem is MultiShuttle)
            {
                protocolConfig = CreateMHEControlGeneric<MultiShuttleDatcomInfo, MHEControl_MultiShuttle>(assem, info);
            }
            else
            {
                Experior.Core.Environment.Log.Write("Can't create MHE Control, object is not defined in the 'CreateMHEControl' of the controller");
                return null;
            }
            //......other assemblies should be added here....do this with generics...correction better to do this with reflection...That is BaseController should use reflection
            //and not generics as we do not know the types at design time and it means that the above always has to be edited when adding a new MHE control object.
            protocolConfig.ParentAssembly = (Assembly)assem;
            controls.Add(protocolConfig);
            return protocolConfig as MHEControl;
        }

        public void RemoveSSCCBarcode(string ULID)            //IController
        {
            throw new NotImplementedException();
        }

        #region ICaseController
        public BaseCaseData GetCaseData()
        {
            return new CaseData();
        }
        #endregion


        public override void HandleTelegrams(string[] telegramFields, string type, ushort number_of_blocks)
        {
            switch (type)
            {
                case "01":
                    Telegram01Recieved(telegramFields, number_of_blocks);
                    break;
                case "13":
                    TelegramSystemStatusRecieved(telegramFields, number_of_blocks);
                    break;
                case "12":
                    TelegramSystemStatusRecieved(telegramFields, number_of_blocks);
                    break;
                case "30":
                    TelegramRemapRecieved(telegramFields, number_of_blocks);
                    break;
                case "14":
                    TelegramRemapRecieved(telegramFields, number_of_blocks);
                    break;
                case "20":
                    Telegram20Received(telegramFields, number_of_blocks);
                    break;
                default:
                    break;
            }
        }

        private void Telegram20Received(string[] fields, ushort blocks)
        {
            int aisleNum;
            int.TryParse(fields[7].Datcom_Aisle(), out aisleNum);
            var ms = multishuttles.Find(x => x.AisleNumber == aisleNum);

            if (ms != null)
            {
                ((MHEControl_MultiShuttle)ms.ControllerProperties).Telegram20Received(fields, blocks);
            }
        }


        //[Category("Configuration")]
        //[DisplayName("Infeed Group Name")]
        //[Description("Single character to identify the group.")]
        //public String InfeedGroupName 
        //{ 
        //    get{return mHEController_MultishuttleInfo.infeedGroupName;}
        //    set
        //    {
        //        if(value.Length == 1)
        //        {
        //            mHEController_MultishuttleInfo.infeedGroupName = value;
        //        }
        //    }
        //}

        //[Category("Configuration")]
        //[DisplayName("Outfeed Group Name")]
        //[Description("Single character to identify the group.")]
        //public String OutfeedGroupName 
        //{ 
        //    get{return mHEController_MultishuttleInfo.outfeedGroupName;}
        //    set
        //    {
        //        if(value.Length == 1)
        //        {
        //            mHEController_MultishuttleInfo.outfeedGroupName = value;
        //        }
        //    }
        //}

        private void TelegramSystemStatusRecieved(string[] splittelegram, int datasets)
        {
            // lastSentTelegram = "";
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
                    if (MSPLC_State != MultiShuttlePLC_State.Unknown_00)
                    {
                        Log.Write("Warning: Plc state not Unknown00 when recieving telegram type 13, status 02!");
                    }
                    MSPLC_State = MultiShuttlePLC_State.Ready_02;
                    string body = ",02,";  //Status 02
                    SendTelegram("13", body, 1);
                }
                else if (status == "07")
                {
                    //Heartbeat message
                    string body = ",07,";  //Status 07
                    SendTelegram("13", body, 1);
                }
            }
            else if (telegramType == "12")
            {
                if (status == "03")
                {
                    if (MSPLC_State != MultiShuttlePLC_State.Ready_02)
                    {
                        Log.Write("Warning: Plc state not Ready_02 when recieving telegram type 12, status 03!");
                    }
                    //The message sequence is as follows:
                    //MFH sends type 12 status 03 - set system status to automatic no move and request system status.
                    MSPLC_State = MultiShuttlePLC_State.Auto_No_Move_03;
                    //MSC sends equipment status message (type 10) for each piece of equipment
                    SendEquipmentStatus("00");
                    //MSC sends elevator operational mode status (type 82) for each elevator (Project dependant)
                    // SendElevatorOperationalModeStatus("00");
                    //MSC sends type 13 status 03
                    string body = ",03,";  //Status 03
                    SendTelegram("13", body, 1);
                }
            }
        }

        private void SendEquipmentStatus(string status)
        {
            // foreach (DematicMultiShuttle multishuttle in multishuttles)
            //{
            //MSC sends equipment status message (type 10) for each piece of equipment

            foreach (MultiShuttle ms in multishuttles)
            {
                string aisleNum = ms.AisleNumber.ToString().PadLeft(2, '0');
                //string groupName, type;

                foreach (RackConveyor r in ms.RackConveyors)
                {
                    //if (r.RackConveyorType == MultiShuttleDirections.Infeed)
                    //{
                    //    groupName = InfeedGroupName;
                    //}
                    //else
                    //{
                    //    groupName = OutfeedGroupName;
                    //}

                    string name = (char)r.RackConveyorType + aisleNum + r.Elevator.GroupName + "   " + r.Name.Substring(3, 2);
                    string body = name + "," + status + ",";
                    SendTelegram("10", body, 1);
                }

                foreach (Elevator e in ms.elevators)
                {
                    string name = "E" + aisleNum + e.GroupName + (char)e.Side + "     ";
                    string body = name + "," + status + ",";
                    SendTelegram("10", body, 1);
                }

                foreach (PickStationConveyor c in ms.PickStationConveyors)
                {
                    //string name = "P" + multishuttle.AisleNo + c.Elevator.ElevatorName + "   " + c.Level; BG Changed
                    string name = "P" + aisleNum + c.Elevator.GroupName + (char)c.Side + "   " + c.Name.Substring(4, 2);
                    string body = name + "," + status + ",";
                    SendTelegram("10", body, 1);
                }

                foreach (DropStationConveyor d in ms.DropStationConveyors)
                {
                    string name = "D" + aisleNum + d.Elevator.GroupName + (char)d.Side + "   " + d.Name.Substring(4, 2);
                    string body = name + "," + status + ",";
                    SendTelegram("10", body, 1);
                }

                foreach (MSlevel s in ms.shuttlecars.Values)
                {
                    string name = "S" + aisleNum + "     " + s.Track.Level.ToString().PadLeft(2, '0');// s.Level;
                    string body = name + "," + status + ",";
                    SendTelegram("10", body, 1);
                }

                foreach (MSlevel s in ms.shuttlecars.Values)
                {
                    //Level group!
                    string name = "L" + aisleNum + "     " + s.Track.Level.ToString().PadLeft(2, '0');// s.Level;
                    string body = name + "," + status + ",";
                    SendTelegram("10", body, 1);
                }

                //Aisle group
                string AisleName = "A" + aisleNum + "       ";
                string AisleBody = AisleName + "," + status + ",";
                SendTelegram("10", AisleBody, 1);

            }
        }

        private void TelegramRemapRecieved(string[] splittelegram, int datasets)
        {
            //lastSentTelegram = "";
            string startSignal = splittelegram[0];
            string seperator = splittelegram[1];
            string telegramType = splittelegram[2];
            string telegramsender = splittelegram[3];
            string receiver = splittelegram[4];
            string status = splittelegram[7];

            if (telegramType == "30")
            {
                if (MSPLC_State != MultiShuttlePLC_State.Auto_No_Move_03)
                {
                    Log.Write("Warning: Plc state not Auto_No_Move_03 when recieving telegram type 30!");
                }

                SendULData();

                //MSC sends type 32 on completion of mapping data
                string body = "";
                SendTelegram("32", body, 1);
            }
            else if (telegramType == "14")
            {
                if (status == "04")
                {
                    //Go to Auto_04
                    if (MSPLC_State != MultiShuttlePLC_State.Auto_No_Move_03)
                    {
                        Log.Write("Warning: Plc state not Auto_No_Move_03 when recieving telegram type 30!");
                    }
                    MSPLC_State = MultiShuttlePLC_State.Auto_04;

                    string body = ",04,";  //Status 04
                    SendTelegram("13", body, 1);
                    //Remap finished!
                }
            }
        }

        private void SendULData()
        {
            foreach (MultiShuttle ms in multishuttles)
            {
                var dematicActionPoints = ms.ConveyorLocations.FindAll(x => x.Active);
                var caseLoads = dematicActionPoints.Select(x => x.ActiveLoad);

                foreach (Case_Load caseload in caseLoads)
                {
                    CaseData cData = (CaseData)caseload.Case_Data;

                    if (cData.CurrentPosition.Length == 0)
                    {
                        continue;
                    }

                    //MSC sends type 31 for every place (other than the PS) which has unit load data 
                    if (cData.CurrentPosition.Datcom_Location() == "P")
                    {
                        continue;
                    }

                    string body = CreateMissionDataSetBody(cData);

                    SendTelegram("31", body, 1);

                    if (cData.CurrentPosition.Length >= 10 && cData.CurrentPosition.Datcom_Location() == "I" && cData.CurrentPosition.Datcom_X_horizontal() == "002" && cData.DestinationPosition.Datcom_Location() != "I")
                    {
                        //Caseload is on infeed rack conveyor position 002. Destination is not Infeed so shuttle car has got a mission. Send 31 with current position = shuttle car and status 02 pending.

                        //Remap will be sent from two locations:
                        //1. From Rack Conveyor, present location is rack conveyor (pos2), status ‘00’
                        //2. From shuttle car, present location is shuttle car with mission status ‘02’ (Pending) 
                        //If mission is not also remapped from shuttle, MFH should re-send mission.
                        //If mission reported from shuttle but status is not ’02’, remap should be failed.
                        //If mission is remapped from shuttle only with status ‘02’. This should also cause remap failure.

                        //Create telegram body with mission status 02 pending and current is shuttlecar
                        string current = cData.CurrentPosition;
                        string level = current.Substring(8, 2);
                        cData.CurrentPosition = "S" + ms.AisleNumber + "     " + level;
                        cData.MissionStatus = "02";
                        body = CreateMissionDataSetBody(cData);

                        //Restore current and mission status
                        cData.CurrentPosition = current;
                        cData.MissionStatus = "00";

                        SendTelegram("31", body, 1);
                    }
                }

                foreach (PickStationConveyor conv in ms.PickStationConveyors)
                {
                    //MSC sends type 35 for Pick Station unit loads

                    DematicActionPoint ap1 = conv.LocationA;
                    DematicActionPoint ap2 = conv.LocationB;

                    if (ap2.Active)
                    {
                        Case_Load caseload2 = ap2.ActiveLoad as Case_Load;

                        conv.psTimeoutTimer.Stop();
                        caseload2.Stop();

                        //update current position
                        ((CaseData)caseload2.Case_Data).CurrentPosition = string.Format("P{0}{1}{2}001{3}",
                                                                          ap2.LocName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                          ms.ElevatorGroup(ap2.LocName),
                                                                          (char)(ap2.LocName.Side()),
                                                                          //ap2.LocName.ConvPosition(),
                                                                          ap2.LocName.Level()); //TODO elevator group

                        Case_Load caseload1 = ap1.ActiveLoad as Case_Load;

                        if (caseload1 != null)
                        {
                            //update current position
                            //update current position
                            ((CaseData)caseload1.Case_Data).CurrentPosition = string.Format("P{0}{1}{2}002{3}",
                                                                              ap1.LocName.AisleNumber().ToString().PadLeft(2, '0'),
                                                                              ms.ElevatorGroup(ap1.LocName),
                                                                              (char)(ap1.LocName.Side()),
                                                                              //ap1.LocName.Datcom_X_horizontal(),
                                                                              ap1.LocName.Level()); //TODO elevator group

                        }

                        string body = CreatePickStationDataSetBody(caseload2, caseload1);
                        SendTelegram("35", body, 1);
                    }
                }
            }
        }

        /// <summary>
        /// Sets up the casedata for a Mission Data Set - see Dematic Multi-Shuttle Control principles 3.7
        /// this is valid for message types 01,02,04,05,06,20,21,22,31.
        /// </summary>
        /// <param name="splitTelegram"></param>
        /// <param name="blockNumber">The number of data sets in the telegram</param>
        /// <returns> CaseData : Case_Load has a property of type casedata to hold specific information mainly regarding telegrams</returns>
        private CaseData SetUpCaseMissionDataSet(string[] splitTelegram, int blockNumber)
        {
            int dataSetOffset = 7 + datcomVersionIndexOffset; // If 2 data sets then splitTelegram are seperated by an index depending on the veraion of datcom with regard to splitTelegram array
            CaseData caseData = null;

            if (blockNumber == 1)
            {
                dataSetOffset = 0;
            }

            try
            {
                caseData = new CaseData()
                {
                    OriginalPosition = splitTelegram[6 + dataSetOffset],
                    CurrentPosition = splitTelegram[7 + dataSetOffset],
                    DestinationPosition = splitTelegram[8 + dataSetOffset],
                    MissionStatus = splitTelegram[9 + dataSetOffset],
                    ULID = splitTelegram[10 + dataSetOffset],
                    ULType = splitTelegram[11 + dataSetOffset],

                    Length = float.Parse(splitTelegram[12 + dataSetOffset]) / 1000f,
                    Width = float.Parse(splitTelegram[13 + dataSetOffset]) / 1000f,
                    Height = float.Parse(splitTelegram[14 + dataSetOffset]) / 1000f,
                    Weight = float.Parse(splitTelegram[15 + dataSetOffset]) / 1000f,

                    TimeStamp = splitTelegram[16 + dataSetOffset],
                    MissionTelegram = splitTelegram
                };

                if (DatcomVersion == "3.0")
                {
                    caseData.Length = 0.6f;
                    caseData.Width = 0.4f;
                    caseData.Height = 0.25f;
                    caseData.Weight = 1;
                    caseData.TimeStamp = "0000000000";
                }
            }
            catch (Exception ex)
            {
                Log.Write("Error in Mission Data Set, maybe length, width, height or weight are not valid to be parsed to a float!", Color.Red);
                Log.Write(ex.Message, Color.Red);
            }

            return caseData;
        }

        public string CreatePickStationDataSetBody(Case_Load caseload2, Case_Load caseload1)
        {
            string body = "";
            PickStationConveyor pSC = ((PickStationConveyor)caseload2.CurrentActionPoint.Parent.Parent.Parent);
            string PickStation = string.Format("P{0}{1}{2}002{3}",
                                 pSC.LocationA.LocName.AisleNumber().ToString().PadLeft(2, '0'),
                                 pSC.Elevator.GroupName,
                                 (char)pSC.LocationA.LocName.Side(),
                                 pSC.Name.Substring(4, 2));

            if (caseload1 == null)
            {
                body = string.Format("{0},,{1},{2},,,",
                    PickStation,
                    caseload2.SSCCBarcode,
                    "00");
                //(caseload2.Height * 1000).ToString());
            }
            else
            {
                body = string.Format("{0},,{1},{2},,{3},{4}",
                    PickStation,
                    caseload2.SSCCBarcode,
                    //(caseload2.Height * 1000).ToString(),
                    "00",
                    caseload1.SSCCBarcode,
                    // (caseload1.Height * 1000).ToString());
                    "00");
            }

            return body;
        }

        public string CreateMissionDataSetBody(CaseData cData)
        {
            string body = "";

            string length, width, height, weight;
            GetLengthWidthHeightWeight(cData, out length, out width, out height, out weight);

            if (DatcomVersion == "3.0")
            {
                body = string.Format("{0},{1},{2},{3},{4},{5},0000000000",
                                                                    cData.OriginalPosition,
                                                                    cData.CurrentPosition,
                                                                    cData.DestinationPosition,
                                                                    cData.MissionStatus,
                                                                    cData.ULID,
                                                                    cData.ULType);
            }
            else if (DatcomVersion == "3.7")
            {
                body = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                                                                                    cData.OriginalPosition,
                                                                                    cData.CurrentPosition,
                                                                                    cData.DestinationPosition,
                                                                                    cData.MissionStatus,
                                                                                    cData.ULID,
                                                                                    cData.ULType,
                                                                                    length,
                                                                                    width,
                                                                                    height,
                                                                                    weight,
                                                                                    cData.TimeStamp);
            }

            return body;
        }

        void GetLengthWidthHeightWeight(CaseData caseload, out string length, out string width, out string height, out string weight)
        {
            length = (caseload.Width * 1000).ToString("0000"); //Unit Load Length (x-axis)
            width = (caseload.Length * 1000).ToString("0000"); //Unit Load Width (z-axis)
            height = (caseload.Height * 1000).ToString("0000");
            weight = (caseload.Weight * 1000).ToString("000000");

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

        /// <summary>
        /// Uses a Mission Data Set to identify the correct aisle (multishuttle
        /// </summary>
        /// <param name="telegram">Telegram in the form of the Mission Data Set</param>
        /// <returns>A multishuttle that is controlled by this controller</returns>
        private MultiShuttle GetMultishuttleFromAisleNum(string[] telegram)
        {
            int aNum;
            int.TryParse(telegram[7].Datcom_Aisle(), out aNum);
            if (aNum == 0) return null;

            foreach (MultiShuttle ms in multishuttles)
            {
                if (ms.AisleNumber == aNum)
                {
                    return ms;
                }
            }

            return null;
        }

        private void Telegram01Recieved(string[] splittelegram, int datasets)
        {

            MultiShuttle ms = GetMultishuttleFromAisleNum(splittelegram);

            #region Case on Pos2 is the first in dataset, Case on Pos1 is the second in dataset

            CaseData cData = new CaseData();
            cData.UserData = string.Empty;

            if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "R") && (splittelegram[8].Length > 0 && splittelegram[8].Substring(0, 1) == "O"))
            {
                //From rack to outfeed rack conveyor
                cData = SetUpCaseMissionDataSet(splittelegram, 1);
                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateShuttleTask(splittelegram[7], splittelegram[8], cData, ShuttleTaskTypes.RackToConv);

                return;
            }
            else if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "R") && (splittelegram[8].Length > 0 && splittelegram[8].Substring(0, 1) == "R"))
            {
                //Shuffle move from rack loc to rack loc
                cData = SetUpCaseMissionDataSet(splittelegram, 1);
                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateShuttleTask(splittelegram[7], splittelegram[8], cData, ShuttleTaskTypes.Shuffle);
                return;
            }
            else if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "O") && (splittelegram[8].Length > 0 && splittelegram[8].Substring(0, 1) == "D") && datasets == 1)
            {
                //Outfeed rack to Drop Station single load
                cData = SetUpCaseMissionDataSet(splittelegram, 1);
                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateElevatorTask(null, null, null, splittelegram[7], splittelegram[8], cData, Cycle.Single, Cycle.Single, TaskType.Outfeed);
                return;
            }
            else if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "O") && (splittelegram[8].Length > 0 && splittelegram[8].Substring(0, 1) == "D") && datasets == 2)
            {
                //Outfeed rack to Drop Station double load
                CaseData cDataA = SetUpCaseMissionDataSet(splittelegram, 2);
                CaseData cDataB = SetUpCaseMissionDataSet(splittelegram, 1);

                cDataA.UserData = cDataA.ULID + "," + cDataB.ULID; //Tag double loads for the correct 02 at the dropstation
                cDataB.UserData = cDataA.UserData;

                Cycle unloadcycle = Cycle.Double;
                if (splittelegram[8].Datcom_Y_Vertical() != splittelegram[15 + datcomVersionIndexOffset].Datcom_Y_Vertical())
                {
                    unloadcycle = Cycle.Single;
                }

                Cycle loadcycle = Cycle.Double;
                if (splittelegram[6].Datcom_Y_Vertical() != splittelegram[13 + datcomVersionIndexOffset].Datcom_Y_Vertical())
                {
                    loadcycle = Cycle.Single;
                }

                if (unloadcycle == Cycle.Single && loadcycle == Cycle.Single)
                {

                }

                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateElevatorTask(splittelegram[14 + datcomVersionIndexOffset], splittelegram[15 + datcomVersionIndexOffset], cDataA, splittelegram[7], splittelegram[8], cDataB, loadcycle, unloadcycle, TaskType.Outfeed);
                return;
            }
            else if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "P") && ((splittelegram[7].Length > 0 && splittelegram[8].Substring(0, 1) == "D")))
            {
                //Pick Station to drop station
                if (datasets == 1)
                {
                    cData = SetUpCaseMissionDataSet(splittelegram, 1);
                    ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateElevatorTask(null, null, null, splittelegram[7], splittelegram[8], cData, Cycle.Single, Cycle.Single);
                }
                else if (datasets == 2)
                {

                    cData = SetUpCaseMissionDataSet(splittelegram, 1);
                    CaseData cDataA = SetUpCaseMissionDataSet(splittelegram, 2);

                    cDataA.UserData = cDataA.ULID + "," + cData.ULID; //Tag double loads for the correct 02 at the dropstation
                    cData.UserData = cDataA.UserData;

                    ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateElevatorTask(splittelegram[18], splittelegram[19], cDataA, splittelegram[7], splittelegram[8], cData, Cycle.Double, Cycle.Double);
                }

            }
            else if (splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "P" && datasets == 1)
            {
                cData = SetUpCaseMissionDataSet(splittelegram, 1);
                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateElevatorTask(null, null, null, splittelegram[7], splittelegram[8], cData, Cycle.Single, Cycle.Single, TaskType.Infeed);

            }
            else if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "I") && (splittelegram[8].Length > 0 && splittelegram[8].Substring(0, 1) == "R"))
            {
                //From infeed conveyor to rack location 
                cData = SetUpCaseMissionDataSet(splittelegram, 1);
                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateShuttleTask(splittelegram[7], splittelegram[8], cData, ShuttleTaskTypes.ConvToRack);
                return;
            }
            else if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "P") && (splittelegram[8].Length > 0 && splittelegram[8].Substring(0, 1) == "I") && datasets == 2 &&
                    splittelegram[8].Length > 0 && splittelegram[8].Datcom_Y_Vertical() == splittelegram[15 + datcomVersionIndexOffset].Datcom_Y_Vertical())
            {

                CaseData cDataA;
                CaseData cDataB;
                SetCaseData(splittelegram, out cDataA, out cDataB);

                // CaseData cDataA = SetUpCaseMissionDataSet(splittelegram, 2);
                // CaseData cDataB = SetUpCaseMissionDataSet(splittelegram, 1);

                // cDataA.UserData = cDataA.ULID + "," + cDataB.ULID; //Tag double loads for the correct 02 at the dropstation
                // cDataB.UserData = cDataA.UserData;

                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateElevatorTask(splittelegram[14 + datcomVersionIndexOffset], splittelegram[15 + datcomVersionIndexOffset], cDataA, splittelegram[7], splittelegram[8], cDataB, Cycle.Double, Cycle.Double, TaskType.Infeed);
                return;
            }
            else if ((splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) == "P") && (splittelegram[8].Length > 0 && splittelegram[8].Substring(0, 1) == "I") && datasets == 2 &&
                splittelegram[8].Length > 0 && splittelegram[8].Datcom_Y_Vertical() != splittelegram[15 + datcomVersionIndexOffset].Datcom_Y_Vertical())
            {
                CaseData cDataA;
                CaseData cDataB;
                SetCaseData(splittelegram, out cDataA, out cDataB);

                ((MHEControl_MultiShuttle)ms.ControllerProperties).CreateElevatorTask(splittelegram[14 + datcomVersionIndexOffset], splittelegram[15 + datcomVersionIndexOffset], cDataA, splittelegram[7], splittelegram[8], cDataB, Cycle.Double, Cycle.Single, TaskType.Infeed);
                return;
            }
            else
            {
                Log.Write(string.Join(",", splittelegram));
                Log.Write("ERROR: 01 not handled by Experior.Catalog.Dematic.DatcomUK.Assemblies.MHEController_Multishuttle.Telegram01Recieved", Color.Red);
            }

            //Doesn't seem to do anything
            //if (datasets == 2 && splittelegram[7].Length > 0 && splittelegram[7].Substring(0, 1) != "R" && splittelegram[14 + datcomVersionIndexOffset].Length > 0 && splittelegram[14 + datcomVersionIndexOffset].Substring(0, 1) != "R")
            //{
            //    //switch D, E, I, O pos2 & pos1
            //    if (splittelegram[7].Contains("001") && splittelegram[14 + datcomVersionIndexOffset].Contains("002"))
            //    {
            //        //switch around!
            //        string temp17 = splittelegram[13 + datcomVersionIndexOffset];
            //        string temp18 = splittelegram[14 + datcomVersionIndexOffset];
            //        string temp19 = splittelegram[15 + datcomVersionIndexOffset];
            //        string temp20 = splittelegram[26 + datcomVersionIndexOffset];
            //        string temp21 = splittelegram[27 + datcomVersionIndexOffset];

            //        splittelegram[13 + datcomVersionIndexOffset] = splittelegram[6];
            //        splittelegram[14 + datcomVersionIndexOffset] = splittelegram[7];
            //        splittelegram[15 + datcomVersionIndexOffset] = splittelegram[8];
            //        splittelegram[16 + datcomVersionIndexOffset] = splittelegram[9];
            //        splittelegram[17 + datcomVersionIndexOffset] = splittelegram[10];

            //        splittelegram[6] = temp17;
            //        splittelegram[7] = temp18;
            //        splittelegram[8] = temp19;
            //        splittelegram[9] = temp20;
            //        splittelegram[10] = temp21;
            //    }
            //}
            #endregion


            //string originalPosition2;
            //string currentPosition2;
            //string destinationPosition2;
            //string missionStatus2;
            //string ULID2;
            //string originalPosition1;
            //string currentPosition1;
            //string destinationPosition1;
            //string missionStatus1;
            //string ULID1;
            //MultiShuttle multishuttle;
            //Case_Load caseload1, caseload2;

            //if (!VerifyReceivedMission(splittelegram, datasets, out originalPosition2, out currentPosition2, out destinationPosition2, out missionStatus2, out ULID2,
            //    out originalPosition1, out currentPosition1, out destinationPosition1, out missionStatus1, out ULID1, out multishuttle, out caseload1, out caseload2))
            //{
            //    return; //Reason is written to log in VerifyReceivedMission method
            //}

        }

        private void SetCaseData(string[] splittelegram, out CaseData cDataA, out CaseData cDataB)
        {

            if (splittelegram[7].Datcom_X_horizontal() == "001")
            {
                //first dataset is position 1
                cDataA = SetUpCaseMissionDataSet(splittelegram, 1);
                cDataB = SetUpCaseMissionDataSet(splittelegram, 2);
            }
            else
            {
                //first dataset is position 2
                cDataA = SetUpCaseMissionDataSet(splittelegram, 2);
                cDataB = SetUpCaseMissionDataSet(splittelegram, 1);
            }
        }


        private float GetXDistance(int xCoord, MultiShuttle multishuttle)
        {
            if (xCoord < 1)
            {
                Log.Write("Error: X coordinate less then 1: " + xCoord, Color.Red);
                Core.Environment.Scene.Pause();
                xCoord = 1;
            }

            //if (multishuttle.MultiShuttleDriveThrough)
            //{
            //    //assuming equal number on both sides!
            //    int midpoint = multishuttle.RackBays / 2;
            //    if (xCoord <= midpoint)
            //        return (xCoord - 0.5f) * multishuttle.BayLength;

            //    return (xCoord - 0.5f) * multishuttle.BayLength + multishuttle.RackConveyorLength * 2 + multishuttle.ElevatorConveyorLength;
            //}

            if (multishuttle.FrontLeftElevator || multishuttle.FrontRightElevator)
                return (xCoord - 0.5f) * multishuttle.LocationLength + multishuttle.RackConveyorLength;

            return (xCoord - 0.5f) * multishuttle.LocationLength;
        }

        [Category("DATCOM")]
        [DisplayName("DatCom Version")]
        [DescriptionAttribute("The version of DatCom that you are using.")]
        [TypeConverter(typeof(DatComConverter))]
        public string DatcomVersion
        {
            get { return mHEController_MultishuttleInfo.datcomVersion; }
            set
            {
                mHEController_MultishuttleInfo.datcomVersion = value;
                if (value == "3.7")
                {
                    datcomVersionIndexOffset = 4;
                }
                else if (value == "3.0")
                {
                    datcomVersionIndexOffset = 0;
                }
            }
        }

        public Case_Load CreateCaseLoad(BaseCaseData caseData)
        {
            MeshInfo boxInfo = new MeshInfo()
            {
                color = caseData.colour,
                filename = Case_Load.GraphicsMesh,
                length = caseData.Length,
                width = caseData.Width,
                height = caseData.Height
            };

            Case_Load load = new Case_Load(boxInfo);
            CaseData DatComData = caseData as CaseData;

            if (DatComData == null)
            {
                Log.Write("ERROR: Bad cast to ATCCaseData in CreateCaseLoad", Color.Red);
                return null;
            }

            load.Weight = caseData.Weight;
            load.Identification = DatComData.ULID;
            load.Case_Data = DatComData;
            return load;
        }

        public Experior.Dematic.Base.EuroPallet CreateEuroPallet(BasePalletData baseData)
        {
            throw new NotImplementedException();
        }
    }

    public class MSPLCStatusChangeEventArgs : EventArgs
    {
        public readonly MultiShuttlePLC_State? _state;

        public MSPLCStatusChangeEventArgs(MultiShuttlePLC_State? state)
        {
            _state = state;
        }
    }

    [Serializable]
    [TypeConverter(typeof(MHEController_MultishuttleInfo))]
    public class MHEController_MultishuttleInfo : BaseControllerInfo
    {
        public string infeedGroupName;
        public string outfeedGroupName;
        public string datcomVersion = "3.7";
    }

    public class DatComConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true; //true means show a combobox
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;//true will limit to list. false will show the list, but allow free-form entry
        }

        public override System.ComponentModel.TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(MHEController_Multishuttle.datcomVersion);
        }
    }

}