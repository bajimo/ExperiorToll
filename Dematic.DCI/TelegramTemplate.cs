using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Dematic.DCI
{
    public class TelegramTemplate
    {
        public class Telegram
        {
            public string Name;
            public TelegramTypes Type;
            //public int Length;
            public List<TelegramFields> FieldList;

            public Telegram(string name, TelegramTypes type, List<TelegramFields> fieldList)
            {
                Name = name;
                Type = type;
                //Length = length;
                FieldList = fieldList;
            }
        }

        //public Dictionary<TelegramTypes, List<TelegramFields>> TelegramSignatures = new Dictionary<TelegramTypes, List<TelegramFields>>();
        public HashSet<Telegram> TelegramsData = new HashSet<Telegram>();
        public TelegramTemplate()
        {
            //Q: Should this be xml...


            //Initialise the message formats
            List<TelegramFields> fieldList = new List<TelegramFields>();

            //Setup the HashSet so that all the telegram type names can be converted (must be in the order that the fields are within the message)
            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.Source,
                TelegramFields.Current, TelegramFields.Destination, TelegramFields.TUIdent, TelegramFields.TUType, TelegramFields.TULength, TelegramFields.TUWidth,
                TelegramFields.TUHeight, TelegramFields.TUWeight, TelegramFields.EventCode };
            TelegramsData.Add(new Telegram("TUMI", TelegramTypes.TUMission, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("TURP", TelegramTypes.TUReport, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("TUNO", TelegramTypes.TUNotification, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("TUEX", TelegramTypes.TUException, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("TUMC", TelegramTypes.TUMissionCancel, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("TUCA", TelegramTypes.TUCancel, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("TUDR", TelegramTypes.TUDataRequest, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("TULL", TelegramTypes.TULocationLeft, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length };
            TelegramsData.Add(new Telegram("LIVE", TelegramTypes.Live, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent };
            TelegramsData.Add(new Telegram("STRQ", TelegramTypes.StatusRequest, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent,
                TelegramFields.AvailabilityStatus };
            TelegramsData.Add(new Telegram("STAT", TelegramTypes.Status, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length };
            TelegramsData.Add(new Telegram("STEN", TelegramTypes.StatusEnd, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent,
                TelegramFields.CurrentFillingLevel, TelegramFields.MaximumFillingLevel };
            TelegramsData.Add(new Telegram("STFI", TelegramTypes.StatusFill, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent,
                TelegramFields.Status };
            TelegramsData.Add(new Telegram("STMF", TelegramTypes.StatusMaterialFlow, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length,TelegramFields.DeviceIdent,
                TelegramFields.FaultCode, TelegramFields.Classification, TelegramFields.TextVersionCounter, TelegramFields.TUIdent, TelegramFields.ABSXPos,
                TelegramFields.ABSYPos, TelegramFields.ABSZPos };
            TelegramsData.Add(new Telegram("STAX", TelegramTypes.ExStatus, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.FaultCode,
                TelegramFields.Language };
            TelegramsData.Add(new Telegram("FTRQ", TelegramTypes.FaultTextReq, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.FaultCode,
                TelegramFields.Language, TelegramFields.EventCode, TelegramFields.CharacterSet, TelegramFields.Text, TelegramFields.TextVersionCounter };
            TelegramsData.Add(new Telegram("FTDF", TelegramTypes.FaultTextDef, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent,
            TelegramFields.Destination, TelegramFields.EventCode};
            TelegramsData.Add(new Telegram("MOMI", TelegramTypes.MoveMission, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.Location};
            TelegramsData.Add(new Telegram("LORQ", TelegramTypes.LocationRequest, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent,
            TelegramFields.ForcedStatus};
            TelegramsData.Add(new Telegram("SETD", TelegramTypes.SetDevice, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.Date,
            TelegramFields.Time};
            TelegramsData.Add(new Telegram("SETT", TelegramTypes.SetDateTime, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent};
            TelegramsData.Add(new Telegram("MFST", TelegramTypes.StartMaterialFlow, fieldList));

            fieldList = new List<TelegramFields> { TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type, TelegramFields.Sender, TelegramFields.Receiver,
                TelegramFields.CycleNo, TelegramFields.Code, TelegramFields.BlocksCount, TelegramFields.BlocksType, TelegramFields.Length, TelegramFields.DeviceIdent};
            TelegramsData.Add(new Telegram("MFSP", TelegramTypes.StopMaterialFlow, fieldList));

            //DEMI
            fieldList = new List<TelegramFields>() {TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type,TelegramFields.Sender,TelegramFields.Receiver,TelegramFields.CycleNo,
                TelegramFields.Code, TelegramFields.BlocksCount,TelegramFields.BlocksType,TelegramFields.Length,TelegramFields.Source,TelegramFields.SourceTUIdent,
                TelegramFields.Destination, TelegramFields.DestinationTUIdent, TelegramFields.TestPick1Layer, TelegramFields.PickPrg1Layer, TelegramFields.TestPick2Layer,
                TelegramFields.PickPrg2Layer, TelegramFields.TestPick3Layer, TelegramFields.PickPrg3Layer, TelegramFields.TestPick4Layer, TelegramFields.PickPrg4Layer,
                TelegramFields.LPFSpeed, TelegramFields.LPF_Acc, TelegramFields.LPF_Dec, TelegramFields.LayersToPick, TelegramFields.LayersOnPallet, TelegramFields.PickPalletHeight,
                TelegramFields.BasePalletHeight, TelegramFields.ItemsPerLayer, TelegramFields.ItemsToPick, TelegramFields.ItemXDim, TelegramFields.ItemYDim, TelegramFields.ItemZDim,
                TelegramFields.ItemWeight, TelegramFields.NestingHeight, TelegramFields.LugHeight, TelegramFields.SlipSheetInfo, TelegramFields.LayersGlued, TelegramFields.SpecHandling,
                TelegramFields.Extra, TelegramFields.TUType, TelegramFields.AF_Speed, TelegramFields.AF_AccDec, TelegramFields.DepalMode, TelegramFields.LastPick, TelegramFields.SSCCBarCode };
            TelegramsData.Add(new Telegram("DEMI", TelegramTypes.DEPALMission, fieldList));

            //DERP
            fieldList = new List<TelegramFields>() {TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type,TelegramFields.Sender,TelegramFields.Receiver,TelegramFields.CycleNo,
                TelegramFields.Code, TelegramFields.BlocksCount,TelegramFields.BlocksType,TelegramFields.Length,TelegramFields.Source,TelegramFields.SourceTUIdent,
                TelegramFields.Destination, TelegramFields.DestinationTUIdent, TelegramFields.LayersPicked, TelegramFields.PickPalletHeight, TelegramFields.EventCode,
                TelegramFields.ManualOperation, TelegramFields.LayersOnFloor, TelegramFields.TestPick1Layer,TelegramFields.PickPrg1Layer,TelegramFields.TestPick2Layer,
                TelegramFields.PickPrg2Layer,TelegramFields.TestPick3Layer,TelegramFields.PickPrg3Layer,TelegramFields.TestPick4Layer, TelegramFields.PickPrg4Layer,
                TelegramFields.SpecHandling, TelegramFields.SSCCBarCode};
            TelegramsData.Add(new Telegram("DERP", TelegramTypes.DEPALMissionReport, fieldList));

            //DENO
            fieldList = new List<TelegramFields>() {TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type,TelegramFields.Sender,TelegramFields.Receiver,TelegramFields.CycleNo,
                TelegramFields.Code, TelegramFields.BlocksCount,TelegramFields.BlocksType,TelegramFields.Length,TelegramFields.Source,TelegramFields.SourceTUIdent, TelegramFields.LayerCount,
                TelegramFields.LayersInGripper, TelegramFields.LayersToPick, TelegramFields.ItemsDepositioned, TelegramFields.ManualOperation, TelegramFields.DepositPalletHeight, TelegramFields.Destination,
                TelegramFields.DestinationTUIdent, TelegramFields.SSCCBarCode};
            TelegramsData.Add(new Telegram("DENO", TelegramTypes.DEPALNotification, fieldList));

            //DELL
            fieldList = new List<TelegramFields>() {TelegramFields.Start, TelegramFields.Flow, TelegramFields.Type,TelegramFields.Sender,TelegramFields.Receiver,TelegramFields.CycleNo,
                TelegramFields.Code, TelegramFields.BlocksCount,TelegramFields.BlocksType,TelegramFields.Length,TelegramFields.Source,TelegramFields.SourceTUIdent, TelegramFields.LayerCount, TelegramFields.LayersToPick,
                TelegramFields.ItemsPicked, TelegramFields.LastLayer, TelegramFields.PickPalletHeight, TelegramFields.Destination, TelegramFields.DestinationTUIdent,TelegramFields.SSCCBarCode};
            TelegramsData.Add(new Telegram("DELL", TelegramTypes.DEPALLeftSource, fieldList));

            //..Add other message types here
        }

        private int cycleNumber = 0;

        public string CycleNumber()
        {
            string newCycleNumber = "";

            cycleNumber++;
            if (cycleNumber > 9999)
                cycleNumber = 1;
            newCycleNumber = cycleNumber.ToString("0000");
            return newCycleNumber;
        }

        public string GetTelegramName(TelegramTypes type)
        {
            Telegram telegram = TelegramsData.FirstOrDefault(x => x.Type == type);
            if (telegram != null)
            {
                return TelegramsData.FirstOrDefault(x => x.Type == type).Name;
            }
            else
            {
                return null;
            }
        }

        private TelegramTypes GetTelegramType(string telegram)
        {
            var telegramType = TelegramsData.FirstOrDefault(x => x.Name == telegram);
            if (telegramType != null)
            {
                return telegramType.Type;
            }
            else
            {
                return TelegramTypes.Unknown;
            }
        }

        //private int GetTelegramLength(TelegramTypes type)
        //{
        //    return TelegramsData.FirstOrDefault(x => x.Type == type).Length;
        //}

        private int GetTelegramFieldCount(TelegramTypes type)
        {
            return TelegramsData.FirstOrDefault(x => x.Type == type).FieldList.Count;
        }


        public TelegramTypes TelegramType(string telegram)
        {
            return TelegramType(telegram.Split(','));
        }

        private TelegramTypes TelegramType(string[] telegram)
        {
            return GetTelegramType(telegram[(int)HeaderPos.Type]);
        }

        public Telegram GetTelegramData(TelegramTypes type)
        {
            return TelegramsData.FirstOrDefault(x => x.Type == type);
        }

        public string NumberOfBlocks(string telegram)
        {
            return telegram.Split(',')[(int)HeaderPos.BlocksCount];
        }

        public string CreateTelegram(IDCITelegrams controller, TelegramTypes type)
        {
            return CreateTelegram(controller, type, false, 1);
        }

        public string CreateTelegram(IDCITelegrams controller, TelegramTypes type, bool grouped)
        {
            return CreateTelegram(controller, type, grouped, 1);
        }

        /// <summary>
        /// Create the framework for a new DCI Telegram, fields should be populated once this framework is complete
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="type"></param>
        /// <param name="grouped"></param>
        /// <param name="bodyCount"></param>
        /// <param name="telegramLength">Length of the telegram from telegram validation</param>
        /// <returns></returns>
        public string CreateTelegram(IDCITelegrams controller, TelegramTypes type, bool grouped, int bodyCount)
        {
            //Which messages require Ack?
            string flow = "R";

            //Is the message Grouped
            string group = "NG";
            if (grouped) { group = "LG"; }

            //What is the length of the telegram
            string length = (((controller.GetTelegramLength(type) - 30) * bodyCount) + 30).ToString("0000"); //30 is the header and tail length

            //Build Telegram
            string header = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                "/",
                flow,
                GetTelegramName(type),
                controller.VFCIdentifier,
                controller.PLCIdentifier,
                CycleNumber(),
                "OK", //Return Code
                bodyCount.ToString("00"),
                group,
                length);

            string body = "".PadLeft(GetTelegramFieldCount(type) - 9, ','); //Only adds the place holders for the message fields
            string tail = "##";

            return string.Format("{0}{1}{2}", header, body, tail);
        }

        /// <summary>
        /// Create a telegram from another telegram (This will only create a not grouped telegram, and TU types only)
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="type"></param>
        /// <param name="telegram">Telegram to create this telegram from</param>
        /// <returns></returns>
        public string CreateTelegram(IDCITelegrams controller, TelegramTypes type, string telegram)
        {
            string newTelegram = telegram;
            if (type == TelegramTypes.TUMissionCancel || type == TelegramTypes.TUDataRequest)
            {
                newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.Flow, "");
            }
            else
            {
                newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.Flow, "R");
            }
            newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.Type, TelegramsData.First(x => x.Type == type).Name);
            newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.Sender, controller.VFCIdentifier);
            newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.Receiver, controller.PLCIdentifier);
            newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.CycleNo, controller.Template.CycleNumber());
            return newTelegram;
        }

        /// <summary>
        /// Group any number of DCI telegrams into 1 Logically grouped telegrams and return a single telegram
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="type"></param>
        /// <param name="telegrams"></param>
        /// <param name="logicallyGrouped">Is the telegram logically grouped</param>
        /// <returns></returns>
        public string GroupTelegrams(IDCITelegrams controller, TelegramTypes type, string[] telegrams, bool logicallyGrouped)
        {
            //Fisrt check that all of the telegrams are of the same type
            foreach (string telegram in telegrams)
            {
                if (telegram.GetTelegramType(controller) != type)
                {
                    return null;
                }
            }

            List<string> fields = new List<string>();
            string[] fieldsHeader = telegrams[0].Split(',');
            for (int i = 0; i < 10; i++)
            {
                if (i == 7) //Number of Blocks
                {
                    fieldsHeader[i] = telegrams.Length.ToString("00");
                }
                else if (i == 8) //Type of Blocks
                {
                    fieldsHeader[i] = logicallyGrouped ? "LG" : "NG";
                }
                else if (i == 9) // Length of Message
                {
                    fieldsHeader[i] = (30 + ((controller.GetTelegramLength(type) - 30) * telegrams.Length)).ToString("0000"); //30 is the header and tail length
                }

                fields.Add(fieldsHeader[i]);
            }

            foreach (string telegram in telegrams)
            {
                string[] fieldsBody = telegram.Split(',');

                for (int i = 0; i < fieldsBody.Length - 1; i++)
                {
                    if (i > 9)
                    {
                        fields.Add(fieldsBody[i]);
                    }
                }
            }

            fields.Add("##");

            return string.Join(",", fields.Select(x => x).ToArray());
        }

        public string[] SplitTelegrams(IDCITelegrams controller, string telegram)
        {
            int numberOfBlocks;
            if (int.TryParse(telegram.GetFieldValue(controller, TelegramFields.BlocksCount), out numberOfBlocks))
            {
                string[] telegramFields = telegram.Split(',');
                string[] telegrams = new string[numberOfBlocks];

                int fieldCount = GetTelegramFieldCount(telegram.GetTelegramType(controller)) - 10;

                for (int i = 0; i < numberOfBlocks; i++)
                {
                    List<string> fields = new List<string>();
                    for (int h = 0; h < 10; h++) //Add the header
                    {
                        fields.Add(telegramFields[h]);
                    }

                    int startposition = (fieldCount * i) + 10;

                    for (int f = startposition; f < (startposition + fieldCount); f++)
                    {
                        fields.Add(telegramFields[f]);
                    }
                    fields.Add("##");
                    telegrams[i] = string.Join(",", fields.Select(x => x).ToArray());
                }
                return telegrams;
            }
            return null;
        }
    }

    public enum HeaderPos
    {
        Start = 0,
        Flow = 1,
        Type = 2,
        Sender = 3,
        Receiver = 4,
        CycleNo = 5,
        Code = 6,
        BlocksCount = 7,
        BlocksType = 8,
        Length = 9
    }

    public enum TelegramTypes
    {
        TUMission,                  //TUMI
        TUReport,                   //TURP
        TUNotification,             //TUNO
        TUException,                //TUEX
        TUMissionCancel,            //TUMC
        TUCancel,                   //TUCA
        TUDataRequest,              //TUDR
        TULocationLeft,             //TULL
        Live,                       //LIVE
        StatusRequest,              //STRQ
        Status,                     //STAT
        StatusEnd,                  //STEN
        ExStatus,                   //STAX
        FaultTextReq,               //FTRQ
        FaultTextDef,               //FTDF
        StatusFill,                 //STFI
        MoveMission,                //MOMI
        LocationRequest,            //LORQ
        SetDevice,                  //SETD
        SetDateTime,                //SETT
        StartMaterialFlow,          //MFST
        StopMaterialFlow,           //MFSP
        StatusMaterialFlow,         //STMF
        DEPALMission,               //DEMI
        DEPALNotification,          //DENO
        DEPALLeftSource,            //DELL
        DEPALMissionReport,         //DERP
        //...etc.

        Unknown             //Do not know what type the telegram is therefore cannot process
    }

    public enum TelegramFields
    {
        //header fields
        Start,
        Flow,
        Type,
        Sender,
        Receiver,
        CycleNo,
        Code,
        BlocksCount,
        BlocksType,
        Length,

        //Transport Messages (TUMI, TURP, TUNO, TUEX, TUMC, TUCA, TUDR, TULL)
        Source, //14
        Current, //14
        Destination, //14
        TUIdent, //20
        TUType, //4
        TULength, //4
        TUWidth, //4
        TUHeight, //4
        TUWeight, //8
        EventCode, //2

        //ML Extra TU fields
        Dynamics, //4
        Location, //14
        ForcedStatus, //2
        Date,  //8
        Time,  //6

        //Status Message (STAT)
        DeviceIdent, //14
        AvailabilityStatus, //2

        //Status Material Flow (STMF)
        Status, //2

        //Status Filling (STFI)
        CurrentFillingLevel,
        MaximumFillingLevel,

        //Extended Status Message (STAX)
        //DeviceIdent //14
        FaultCode, //6
        Classification, //2 - need further enum
        TextVersionCounter, //2
                            //TUIdent //22
        ABSXPos, //6
        ABSYPos, //6
        ABSZPos, //6

        //Fault Text Request (FTRQ)
        //FaultCode //6
        Language, //8

        //Fault Text Definition (FTDF)
        //FaultCode //6
        //Language //8
        //EventCode //2
        CharacterSet, //10
        Text,  //180
               //TextVersionCounter //2

        //Extra shuttle message types for Transport Unit messages
        DropIndex, //4
        ShuttleDynamics, //4
        LiftDynamics, //4
        SourceShuttleExtension, //4
        DestinationShuttleExtension, //4
        CaseConveyorDynamics,//4
                             //Case conveyor extra TU fields
        Contour, //14
        HeightClass, //2
        SortID, //6
        SortSeq, //6
        SortInfo, //2

        //Project Fields (Used by any controller to add projects specific fields to telegrams)
        ProjectField1,
        ProjectField2,
        ProjectField3,
        ProjectField4,
        ProjectField5,

        //Depalletiser DEMI
        SourceTUIdent, //24
        DestinationTUIdent, // 24
        TestPick1Layer, //2
        PickPrg1Layer, //2
        TestPick2Layer, //2
        PickPrg2Layer, //2
        TestPick3Layer, //2
        PickPrg3Layer, //2
        TestPick4Layer, //2
        PickPrg4Layer, //2
        LPFSpeed, //4
        LPF_Acc, //4
        LPF_Dec, //4
        LayersToPick, //2
        LayersOnPallet, //2
        PickPalletHeight, //4
        BasePalletHeight, //4
        ItemsPerLayer, //2
        ItemsToPick, //2
        ItemXDim, //4
        ItemYDim, //4
        ItemZDim, //4
        ItemWeight, //8
        NestingHeight, //2
        LugHeight, //2
        SlipSheetInfo, //2
        LayersGlued, //2
        SpecHandling, //6
        Extra, //2
               //TUType, //4
        AF_Speed,  // 4
        AF_AccDec, //4
        DepalMode, //2
        LastPick, //2
        SSCCBarCode, //22  

        //De-Palletizing Layer Left Source Load Unit <DELL>
        LayerCount, //2
        ItemsPicked, //2
        LastLayer,  //2

        //De-Palletizing Notification <DENO>
        LayersInGripper,    //2
        ItemsDepositioned,  //2
        DepositPalletHeight, // 4

        //De-palletizing Mission Report <DERP>
        LayersPicked, //2
        ManualOperation, //2
        LayersOnFloor, //2

        //TUMI AMCAP extra fields
        SKUID,
        SKUDifferetiiator,
        ScanNo,
        QuantityOfCases,
        CurrentSeqNo,
        LastSeqNo,
        CaseOrientation,
        LengthMin,
        LengthMax,
        WidthMin,
        WidthMax,
        HeightMin,
        HeightMax,
        WeightMin,
        WeightMax,
        PinExtension,
        LoadUnitId,
        LoadUnitType,
        CasePusherDynamicsLong,
        CaseConveyorDynamicsLong,
        StackNo,
        CurrentStackSeqNo,
        LastStackSeqNo,
        CasePusherDynamicsShort,
        CaseConveyorDynamicsShort
    }

    public enum FlowControl
    {
        NoAck,    //. - No Acknowledge required
        AckReq,   //R - Acknowledgement required
        AckFlag,  //A - Acknowledgement flag
    }

    public enum AvailabilityStatus
    {
        Automatic,      //AU - Automatic mode without any fault. Device is available
        Manual,         //MA - Manual mode without any fault
        Fault,          //FL - Short term fault
        Off,            //OF - Long term not available (off or long term fault)
        NoOperation     //.. - No operation mode
    }

    public enum EventCodes
    {
        Ok,                 //OK - Everything is fine
        BinOccupied,        //BO - Bin Occupied
        BinEmpty,           //BE - Bin Empty
        DestNotReachable,   //DN - Destination not reachable
        SourceNotReachable, //SN - Source not reachable
        TooHigh,            //TH - Too high
        CurrentNoExist,     //CE - Current location does not exist
        DestNoExist,        //DE - Destination does not exist
        DestUnreachable,    //DU - Destination is unreachable
        TUUnknown           //TU - TU Unknown
    }

    public enum LocationTypes
    {
        RackConvIn,
        RackConvOut,
        Lift,
        PickStation,
        DropStation,
        Shuttle,
        BinLocation,
        LHD,
        None
    }


    public enum RackSide
    {
        Left = 1,
        Right = 2
    }
}
