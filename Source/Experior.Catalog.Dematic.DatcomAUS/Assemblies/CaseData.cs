using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    public class CaseData : BaseCaseData
    {
        public CaseData()
        {
            //Set default values;
            OriginalPosition = string.Empty;
            CurrentPosition = string.Empty;
            DestinationPosition = string.Empty;
            ULStatus = "00"; 
            Profile = "@@@@";
            PLCName = string.Empty;
        }
        public string OriginalPosition { get; set; }
        public string CurrentPosition { get; set; }
        public string DestinationPosition { get; set; }
        public string ULStatus { get; set; }
       // public string Barcode1 { get; set; }
        public string Barcode2 { get; set; }
        public string Profile { get; set; }
        public string CarrierSize { get; set; }
        public string SpecialData { get; set; }
        public string PLCName { get; set; }
        //public bool ActiveLocation { get; set; }
    }
}