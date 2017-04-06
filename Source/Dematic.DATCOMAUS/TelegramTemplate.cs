using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Dematic.DATCOMAUS
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
                FieldList = fieldList;
            }
        }

        public HashSet<Telegram> TelegramsData = new HashSet<Telegram>();
        public TelegramTemplate()
        {

            //Initialise the message formats
            List<TelegramFields> fieldList = new List<TelegramFields>();
            fieldList = new List<TelegramFields>
            {
                TelegramFields.Start,
                TelegramFields.CycleNumber,
                TelegramFields.Type,
                TelegramFields.SenderIdent,
                TelegramFields.ReceiverIdent,
                TelegramFields.ProgramIdent,
                TelegramFields.Current,
                TelegramFields.Destination,
                TelegramFields.ULStatus,
                TelegramFields.ULIdentification,
                TelegramFields.Barcode1,
                TelegramFields.Barcode2,
                TelegramFields.Profile,
                TelegramFields.CarrierSize,
                TelegramFields.SpecialData,
                TelegramFields.Weight,
                TelegramFields.Height,
                TelegramFields.Length,
                TelegramFields.Width,
                TelegramFields.Filler,
                TelegramFields.End1,
                TelegramFields.End2
            };

            TelegramsData.Add(new Telegram("01", TelegramTypes.TransportOrder, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("02", TelegramTypes.Arrival, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("03", TelegramTypes.Left, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("04", TelegramTypes.CancelMission, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("05", TelegramTypes.ModifyMission, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("06", TelegramTypes.Exception, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("20", TelegramTypes.AppAcknowledge, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("31", TelegramTypes.RemapULData, fieldList));

            fieldList = new List<TelegramFields>
            {
                TelegramFields.Start,
                TelegramFields.CycleNumber,
                TelegramFields.Type,
                TelegramFields.SenderIdent,
                TelegramFields.ReceiverIdent,
                TelegramFields.ProgramIdent,
                TelegramFields.SSNull1,
                TelegramFields.SystemStatus,
                TelegramFields.SSNull2,
                TelegramFields.Filler,
                TelegramFields.End1,
                TelegramFields.End2
            };

            TelegramsData.Add(new Telegram("12", TelegramTypes.SetSystemStatus, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("13", TelegramTypes.SystemStatusReport, fieldList));

            fieldList = new List<TelegramFields>
            {
                TelegramFields.Start,
                TelegramFields.CycleNumber,
                TelegramFields.Type,
                TelegramFields.SenderIdent,
                TelegramFields.ReceiverIdent,
                TelegramFields.ProgramIdent,
                TelegramFields.FunctionGroup,
                TelegramFields.GroupStatus,
                TelegramFields.ESNull1,
                TelegramFields.Filler,
                TelegramFields.End1,
                TelegramFields.End2
            };

            TelegramsData.Add(new Telegram("10", TelegramTypes.EquipmentStatus, fieldList));

            fieldList = new List<TelegramFields>
            {
                TelegramFields.Start,
                TelegramFields.CycleNumber,
                TelegramFields.Type,
                TelegramFields.SenderIdent,
                TelegramFields.ReceiverIdent,
                TelegramFields.ProgramIdent,
                TelegramFields.STNull1,
                TelegramFields.Filler,
                TelegramFields.End1,
                TelegramFields.End2
            };

            TelegramsData.Add(new Telegram("14", TelegramTypes.MaterialFlowStart, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("15", TelegramTypes.MaterialFlowStop, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("30", TelegramTypes.RequestAllData, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("32", TelegramTypes.EndRemap, fieldList));
            fieldList = fieldList.ConvertAll(x => x);
            TelegramsData.Add(new Telegram("99", TelegramTypes.HeartBeat, fieldList));
            //..Add other message types here
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
        
        /// <summary>
        /// Create the framework for a new DCI Telegram, fields should be populated once this framework is complete
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public string CreateTelegram(IDATCOMAUSTelegrams controller, TelegramTypes type)
        {
            //Build Telegram
            string header = string.Format("{0},,{1},{2},{3},{4},",
                "/", //Start - cycle number is just a space
                GetTelegramName(type),
                controller.VFCIdentifier, //Sender
                controller.PLCIdentifier, //Receiver
                "00"); //Program Ident

            string body = "".PadLeft(GetTelegramFieldCount(type) - 9, ','); //Only adds the place holders for the message fields
            string tail = ",<13>,<10>";

            return string.Format("{0}{1}{2}", header, body, tail);
        }

        /// <summary>
        /// Create a telegram from another telegram (This will only create a not grouped telegram, and TU types only)
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="type"></param>
        /// <param name="telegram">Telegram to create this telegram from</param>
        /// <returns></returns>
        public string CreateTelegram(IDATCOMAUSTelegrams controller, TelegramTypes type, string telegram)
        {
            string newTelegram = telegram;
            newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.Type, TelegramsData.First(x => x.Type == type).Name);
            newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.SenderIdent, controller.VFCIdentifier);
            newTelegram = newTelegram.SetFieldValue(controller, TelegramFields.ReceiverIdent, controller.PLCIdentifier);
            return newTelegram;
        }
    }

    public enum HeaderPos
    {
        Start = 0,
        CycleNumber = 1,
        Type = 2,
        SenderIdent = 3,
        ReceiverIdent = 4,
        ProgramIdent = 5,
    }

    public enum TelegramTypes
    {
        TransportOrder,     //01
        Arrival,            //02
        Left,               //03
        CancelMission,      //04
        ModifyMission,      //05
        Exception,          //06
        EquipmentStatus,    //10
        SetSystemStatus,    //12
        SystemStatusReport, //13
        MaterialFlowStart,  //14
        MaterialFlowStop,   //15
        AppAcknowledge,     //20
        RequestAllData,     //30
        RemapULData,        //31
        EndRemap,           //32
        HeartBeat,          //99

        Unknown             //Do not know what type the telegram is therefore cannot process
    }

    public enum TelegramFields
    {
        //Header Fields
        Start,
        CycleNumber,
        Type,
        SenderIdent,
        ReceiverIdent,
        ProgramIdent,

        //Tail
        Filler,
        End1,
        End2,

        //Material Flow Telegrams
        Current,
        Destination,
        ULStatus,
        ULIdentification, //Not used padded with spaces (8)
        Barcode1,
        Barcode2,
        Profile,
        CarrierSize,
        SpecialData,
        Weight,
        Height,
        Length,
        Width,

        //System Status
        SSNull1,
        SystemStatus,
        SSNull2,

        //Equipment Status
        FunctionGroup,
        GroupStatus,
        ESNull1,

        //Start Stop
        STNull1
    }
}
