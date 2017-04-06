using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    public class CaseData : BaseCaseData
    {
        public CaseData()
        {
            //Set default values;

            ProfileStatus = "@@@@";
            MissionStatus = "00";
            ULID = string.Empty;
            ULType = string.Empty;
            OriginalPosition = string.Empty;
            CurrentPosition = string.Empty;
            DestinationPosition = string.Empty;
            BarcodeScannedLocation = string.Empty;
            HeightType = string.Empty;
            TimeStamp = string.Empty;
            PLCName = string.Empty;
        }

        public string TimeStamp { get; set; }
        public string OriginalPosition { get; set; }
        public string CurrentPosition { get; set; }
        public string DestinationPosition { get; set; }
        public string MissionStatus { get; set; }
        public string TourId { get; set; }
        public string PickId { get; set; }
        public string DropId { get; set; }
        public string LHDno { get; set; }
        public string HeightType { get; set; }
        public string Spare { get; set; }
        public string OrgTelegram { get; set; }
        public string TelegramLastSent { get; set; }
        public bool ReadyForType20Delete { get; set; }
        public string ULType { get; set; }
        public string ULID { get; set; }
        public bool HandshakeSent { get; set; }
        public bool RoutingTableUpdateWait { get; set; }
        public bool CallforwardWait { get; set; }
        public string BarcodeScannedLocation { get; set; }
        public bool BarcodeFail { get; set; }
        //public float Weight { get; set; }
        public string ProfileStatus { get; set; }
        public string[] MissionTelegram { get; set; }
        public string PLCName { get; set; }
        public bool Empty { get; set; }
        public string UserData { get; set; }
        public int BinDepth { get; set; }
        public string HeightClass { get; set; }
    }
}