using Dematic.ATC;
using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.CaseConveyor
{
    public class MHEControl_MiniloadPickStation : MHEControl
    {
        private MiniloadPickStationATCInfo miniloadPickStationInfo;
        private MiniloadPickStation miniloadPickStation;
        private MHEController_Case casePLC;

        public MHEControl_MiniloadPickStation(MiniloadPickStationATCInfo info, MiniloadPickStation pickStation)
        {
            miniloadPickStation = pickStation;
            miniloadPickStationInfo = info;
            Info = info;  // set this to save properties 
            
            casePLC = miniloadPickStation.Controller as MHEController_Case;

            miniloadPickStation.OnPickStationStatusUpdate += miniloadPickStation_OnPickStationStatusUpdate;
        }

        void miniloadPickStation_OnPickStationStatusUpdate(object sender, PickStationStatusUpdateEventArgs e)
        {
            string load1 = e._LoadPosOutside == null ? "Empty" : e._LoadPosOutside.Identification;
            string load2 = e._LoadPosInside == null ? "Empty" : e._LoadPosInside.Identification;

            if (e._LoadPosOutside != null && e._LoadPosInside != null)
            {
                string telegram1 = casePLC.CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, (ATCCaseLoad)e._LoadPosOutside);
                string telegram2 = casePLC.CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, (ATCCaseLoad)e._LoadPosInside);
                telegram1 = telegram1.SetFieldValue(TelegramFields.location, OutsidePSName);
                telegram2 = telegram2.SetFieldValue(TelegramFields.location, InsidePSName);
                telegram1 = telegram1.SetFieldValue(TelegramFields.stateCode, ((ATCCaseLoad)e._LoadPosOutside).PresetStateCode);
                telegram2 = telegram2.SetFieldValue(TelegramFields.stateCode, ((ATCCaseLoad)e._LoadPosInside).PresetStateCode);

                string telegram = Telegrams.CreateMultipalMessage(new List<string>() { telegram1, telegram2 }, TelegramTypes.MultipleTransportFinishedTelegram, casePLC.Name);
                casePLC.SendTelegram(telegram, true);
            }
            else
            {
                //string telegram = casePLC.CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, (ATCCaseLoad)e._LoadPosOutside);
                //telegram = telegram.SetFieldValue(TelegramFields.location, OutsidePSName);
                //telegram = telegram.SetFieldValue(TelegramFields.stateCode, ((ATCCaseLoad)e._LoadPosOutside).PresetStateCode);
                //casePLC.SendTelegram(telegram, true);

                string telegram = casePLC.CreateTelegramFromLoad(TelegramTypes.TransportFinishedTelegram, (ATCCaseLoad)e._LoadPosInside);
                telegram = telegram.SetFieldValue(TelegramFields.location, InsidePSName);
                telegram = telegram.SetFieldValue(TelegramFields.stateCode, ((ATCCaseLoad)e._LoadPosInside).PresetStateCode);
                casePLC.SendTelegram(telegram, true);
            }
        }

        public override void Dispose()
        {

        }

        [DisplayName("Outside PS Name")]
        [Description("Name of the outside pick station (furthest from the SRM) to be included in the location message to the ATC")]
        public string OutsidePSName
        {
            get { return miniloadPickStationInfo.OutsidePSName; }
            set { miniloadPickStationInfo.OutsidePSName = value; }
        }

        [DisplayName("Inside PS Name")]
        [Description("Name of the inside pick station (closest to the SRM) to be included in the location message to the ATC")]
        public string InsidePSName
        {
            get { return miniloadPickStationInfo.InsidePSName; }
            set { miniloadPickStationInfo.InsidePSName = value; }
        }

    }

    [Serializable]
    [XmlInclude(typeof(MiniloadPickStationATCInfo))]
    public class MiniloadPickStationATCInfo : ProtocolInfo
    {
        public string OutsidePSName;
        public string InsidePSName;

    }
}