using System;
using System.Collections.Generic;
using Experior.Dematic.Base;
namespace Experior.Dematic.Storage.Base
{
    /// <summary>
    /// Interface to BK10 plc´s for objects in other catalogs (Storage, BK25, etc)
    /// </summary>
    public interface IBK10PLCCommon
    {
        string Name { get; set; }
        //bool Divert(string ULID, int bit, int word);
        int ReceiverID { get; set; }
        int SenderID { get; set; }
        void RemoveSSCCBarcode(string ULID);
        //global::System.Collections.Generic.Dictionary<string, ushort[]> RoutingTable { get; }
        void SendCallForwardException(string location, ushort Outstanding_quantity_of_ULs_not_released);
        void SendDivertConfirmation(string location, string SSCCBarcode);
        void SendLeftMessage(string location, string SSCCBarcode);    
        void SendLaneOccupancyMessage(string location, string status);
        void SendRoutingException(string SSCCBarcode, string location, string ReasonCode);
        void SendCraneInputStationArrival(string craneNumber, List<Case_Load> EPCases, string status = "");
        void SendCraneInputStationArrival(string craneNumber, List<string> CaseBarcodes, string status = "");
        void SendTelegram(string tlgType, string body, ushort blocks);
        event Experior.Dematic.Storage.Base.Common.Nofication SendAllLaneOccupancyEvent;
        event Experior.Dematic.Storage.Base.Common.PickStationStatus MiniloadPickStationStatusEvent;
    }
}
