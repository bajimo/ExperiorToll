using Experior.Catalog.Dematic.Case.Components;
using Experior.Catalog.Dematic.Case.Devices;
using Experior.Catalog.Dematic.Custom.Components;
using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using Dematic.DATCOMAUS;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    /// <summary>
    /// This is a PLC that handels Datcom AUS messages
    /// </summary>
    public class MHEControllerAUS_Case : BaseDatcomAusController, IController, ICaseController
    {
        CaseDatcomAusInfo caseDatcomInfo;
        //public Dictionary<string, CallForwardLocation> callForwardTable = new Dictionary<string, CallForwardLocation>();
        public int MaxRoutingTableEntries = int.MaxValue;
        public Dictionary<string, string> RoutingTable = new Dictionary<string, string>(); //SSCCBarcode, Destination

        //public event EventHandler<CallForwardEventArgs> OnCallForwardTelegramReceived;
        //protected virtual void CallForwardTelegramReceived(CallForwardEventArgs e)
        //{
        //    OnCallForwardTelegramReceived?.Invoke(this, e);
        //}

        public MHEControllerAUS_Case(CaseDatcomAusInfo info) : base(info)
        {
            caseDatcomInfo = info;
            OnPLCStateChange += CasePLC_Datcom_OnPLCStateChange;
        }

        public override void Dispose()
        {
            //callForwardTable.Clear();
            RoutingTable.Clear();

            base.Dispose();
        }

        public override Image Image
        {
            get { return Common.Icons.Get("PLC"); }
        }

        public void RemoveSSCCBarcode(string ULID)
        {
            if (RoutingTable.ContainsKey(ULID))
            {
                RoutingTable.Remove(ULID);
            }
        }

        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            MHEControl protocolConfig = null;  //generic plc config object
            //ProtocolInfo protocolInfo = null;  //generic plc config object constructor argument type
            Dictionary<string, Type> dt = new Dictionary<string, Type>();

            if (assem is CommunicationPoint)
            {
                protocolConfig = CreateMHEControlGeneric<CommPointDatcomAusInfo, MHEControl_CommPoint>(assem, info);
            }
            else if (assem is MergeDivertConveyor)
            {
                protocolConfig = CreateMHEControlGeneric<MergeDivertDatcomAusInfo, MHEControl_MergeDivert>(assem, info);
            }
            else if (assem is Transfer)
            {
                protocolConfig = CreateMHEControlGeneric<TransferDatcomAusInfo, MHEControl_Transfer>(assem, info);
            }
            else if (assem is StraightAccumulationConveyor)
            {
                protocolConfig = CreateMHEControlGeneric<ManualPickingDatcomAusInfo, MHEControl_ManualPicking>(assem, info);
            }
            else if (assem is BeltSorterDivert)
            {
                protocolConfig = CreateMHEControlGeneric<BeltSorterDivertDatcomAusInfo, MHEControl_BeltSorterDivert>(assem, info);
            }
            else if (assem is AngledDivert)
            {
                protocolConfig = CreateMHEControlGeneric<AngledDivertDatcomAusInfo, MHEControl_AngledDivert>(assem, info);
            }
            else if (assem is ThreeWaySwitch)
            {
                protocolConfig = CreateMHEControlGeneric<ThreeWaySwitchDatcomAusInfo, MHEControl_ThreeWaySwitch>(assem, info);
            }
            else if (assem is PickPutStation)
            {
                protocolConfig = CreateMHEControlGeneric<PickPutStationDatcomAusInfo, MHEControl_PickPutStation>(assem, info);
            }
            else
            {
                Core.Environment.Log.Write("Can't create MHE Control, object is not defined in the 'CreateMHEControl' of the controller");
                return null;
            }
            //......other assemblies should be added here....do this with generics...correction better to do this with reflection...That is BaseDatcomAusController should use reflection
            //and not generics as we do not know the types at design time and it means that the above always has to be edited when adding a new MHE control object.
            protocolConfig.ParentAssembly = (Assembly)assem;
            return protocolConfig;
        }

        public void SendArrivalMessage(string location, Case_Load load)
        {
            if (string.IsNullOrWhiteSpace(location))
                return;  

            if (load == null)
                return;

            if (PLC_State == CasePLC_State.Ready)
            {
                CaseData caseData = load.Case_Data as CaseData;
                caseData.CurrentPosition = location;
                var telegram = CreateTelegramFromLoad(TelegramTypes.Arrival, load);
                //telegram = telegram.SetFieldValue(this, TelegramFields.Current, location);
                SendTelegram(telegram);
            }
        }

        public override void HandleTelegrams(TelegramTypes type, string telegram)
        {
            switch (type)
            {
                case TelegramTypes.TransportOrder: //01
                    TransportOrderRecieved(telegram);
                    break;
                case TelegramTypes.ModifyMission: //05
                    TransportOrderRecieved(telegram); //Just update routing destination?
                    break;
                case TelegramTypes.CancelMission: //04
                    CancelMissionRecieved(telegram);
                    break;
                //case "04":
                //    PurgeRoutingTableRecieved(telegram);
                //    break;
                //case "09":
                //    CraneForkStatusRecieved(telegram);
                //    break;
                case TelegramTypes.SetSystemStatus:
                    SetSystemStatusRecieved(telegram);
                    break;
                //case "26":
                //    CallForwardRecieved(telegram);
                //    break;
                //case "86":
                //    CallForwardWithBarcode(telegram);
                //    break;
                default:
                    break;
            }
        }

        void CasePLC_Datcom_OnPLCStateChange(object sender, PLCStateChangeEventArgs e)
        {
            PLC_State = e.CaseState;
        }

        public static event EventHandler<PlcStatusChangeEventArgs> OnCasePLCStatusChanged;

        private CasePLC_State? _PLC_State = CasePLC_State.Unknown;
        [Browsable(false)]
        public CasePLC_State? PLC_State
        {
            get { return _PLC_State; }
            set
            {
                _PLC_State = value;
                if (OnCasePLCStatusChanged != null)
                {
                    OnCasePLCStatusChanged(this, new PlcStatusChangeEventArgs(value));
                }
            }
        }

        //public void CallForwardWithBarcode(string[] telegramFields)
        //{
        //    string location = telegramFields[6];
        //    ushort quantity = ushort.Parse(telegramFields[7]);
        //    string barcode = telegramFields[8];

        //    //Call forward at manual picking points on Accumulation conveyor
        //    if (Core.Assemblies.Assembly.Items.ContainsKey(location) && Core.Assemblies.Assembly.Items[location] is StraightAccumulationConveyor)
        //    {
        //        StraightAccumulationConveyor conv = Core.Assemblies.Assembly.Items[location] as StraightAccumulationConveyor;
        //        if (conv.ControllerProperties != null)
        //        {
        //            MHEControl_ManualPicking control = conv.ControllerProperties as MHEControl_ManualPicking;
        //            control.CallForwardReceived(barcode);
        //        }
        //    }
        //    CallForwardTelegramReceived(new CallForwardEventArgs(location, barcode));
        //}

        //public void CallForwardRecieved(string[] telegramFields)
        //{
        //    //This message is used by MFH to instruct CCC to release a number of ULs from a location.
        //    string location = telegramFields[6];
        //    ushort quantity = ushort.Parse(telegramFields[7]);

        //    //Call forward at manual picking points on Accumulation conveyor
        //    if (Core.Assemblies.Assembly.Items.ContainsKey(location) && Core.Assemblies.Assembly.Items[location] is StraightAccumulationConveyor)
        //    {
        //        StraightAccumulationConveyor conv = Core.Assemblies.Assembly.Items[location] as StraightAccumulationConveyor;
        //        if (conv.ControllerProperties != null)
        //        {
        //            MHEControl_ManualPicking control = conv.ControllerProperties as MHEControl_ManualPicking;
        //            control.CallForwardReceived(null);
        //        }
        //    }
        //}

        //public void RemoveCallForwardLocation(string location)
        //{
        //    callForwardTable[location].Timer.Stop();
        //    callForwardTable[location].Timer.OnElapsed -= new Timer.ElapsedEvent(CallForwardElapsed);
        //    callForwardTable[location].Timer.Dispose();
        //    callForwardTable.Remove(location);
        //}

        //public void CallForwardElapsed(Timer sender)
        //{
        //    string location = sender.UserData as string;

        //    if (callForwardTable.ContainsKey(location))
        //    {
        //        if (callForwardTable[location].Quantity > 0)
        //        {
        //            //If the CCC is unable to supply the required quantity of totes, it sends a Call Forward Exception
        //            //Message (Type 27) to MFH. The quantity field sent in this message is the outstanding number of
        //            //totes.
        //            SendCallForwardException(location, callForwardTable[location].Quantity);
        //        }

        //        RemoveCallForwardLocation(location);
        //    }
        //}

        //public void SendCallForwardException(string location, ushort Outstanding_quantity_of_ULs_not_released)
        //{
        //    if (string.IsNullOrWhiteSpace(location))
        //        return;

        //    if (PLC_State == CasePLC_State.Ready)
        //        SendTelegram("27", location + "," + Outstanding_quantity_of_ULs_not_released);
        //}

        //public void SendLaneOccupancyMessage(string location, string status)
        //{
        //    if (string.IsNullOrWhiteSpace(location))
        //        return;

        //    SendTelegram("28", location + "," + status);
        //}

        public void SetSystemStatusRecieved(string telegram)
        {
            string status = telegram.GetFieldValue(this, TelegramFields.SystemStatus);

            if (status == "02")
            {
                PLC_State = CasePLC_State.Ready;
            }
            if (status == "00")
            {
                PLC_State = CasePLC_State.Unknown;
            }

            string reply = Template.CreateTelegram(this, TelegramTypes.SystemStatusReport);
            reply.SetFieldValue(this, TelegramFields.SystemStatus, status);
            SendTelegram(reply);
        }

        //public void CraneForkStatusRecieved(string[] telegramFields)
        //{

        //    bool[] pickStationUsed = new bool[4] { true, true, true, true };

        //    for (int i = 7; i < telegramFields.Length - 2; i++)
        //    {
        //        if (telegramFields[i] == "01")
        //            pickStationUsed[i - 7] = false;
        //    }

        //    MiniloadPickStationStatus(this, telegramFields[6], pickStationUsed);

        //    //TODO
        //    //SendCraneInputStationArrival(string craneNumber, List<Case_Load> EPCases)();
        //    //string status = "00";
        //    //SendAPLevelStatus(status);
        //}

        //public event PickStationStatus MiniloadPickStationStatusEvent;

        //public void MiniloadPickStationStatus(IController sender, string crane, bool[] pickStationStatus)
        //{
        //    //MiniloadPickStationStatusEvent(sender, crane, pickStationStatus);
        //}

        //public void PurgeRoutingTableRecieved(string[] telegramFields)
        //{
        //    //This message deletes the entire Routing Table in a CCC and will be typically used as part of some sort of housekeeping functionality.
        //    RoutingTable.Clear();
        //    //Purging the Routing Table by CCC (as instructed by MFH) will trigger another Routing Table Status
        //    //message indicating that the table is 'no longer critically full'.
        //    string status = "00"; //Routing table no longer critically full.  Status 01 means routing Table critically full.
        //    SendTelegram("10", status);
        //}

        public void CancelMissionRecieved(string telegram)
        {
            //Just remove from routing table?
            var SSCCBarcode = telegram.GetFieldValue(this, TelegramFields.ULIdentification);
            RoutingTable.Remove(SSCCBarcode);
        }

        public void TransportOrderRecieved(string telegram)
        {
            //This message creates new entries in the Routing Table for specific ULs or updates existing records in the Routing Table for specific ULs.
            int count = RoutingTable.Count;

            var SSCCBarcode = telegram.GetFieldValue(this, TelegramFields.ULIdentification);
       
            var caseload = Case_Load.GetCaseFromIdentification(SSCCBarcode);
            //Check if the load has a datcom case data, if not create it as it may have come from a different system that has different case data e.g. DCI multishuttle

            if (caseload != null && caseload.Case_Data.GetType() != typeof(CaseData))
            {
                CaseData caseData = new CaseData();
                caseData.Length = caseload.Case_Data.Length;
                caseData.Width = caseload.Case_Data.Width;
                caseData.Height = caseload.Case_Data.Height;
                caseData.Weight = caseload.Case_Data.Weight;
                caseData.colour = caseload.Case_Data.colour;
                caseload.SSCCBarcode = caseload.Identification;
                caseload.Case_Data = caseData;
            }

            RoutingTable[SSCCBarcode] = telegram.GetFieldValue(this, TelegramFields.Destination).Trim();

            //Remove if destinations is empty? (or does not exist?)
            
            //Update caseload if it exists
            if (caseload != null)
            {
                if (((CaseData)caseload.Case_Data).CallforwardWait)
                {
                    return; //Tote is waiting to be called forward. Do not release
                }

                ((CaseData)caseload.Case_Data).RoutingTableUpdateWait = false;
            }

            int newcount = RoutingTable.Count;

            if (newcount > MaxRoutingTableEntries && newcount > count)
            {
                //TODO do AUS datcom do this?
                //New entry added to the routing table and routing table exceeds limit.
                //string status = "01"; //Status 01 means routing Table critically full.
                //SendTelegram("10", status);
            }
        }

        //*****************************************************************
        #region Route Checking

        public bool DivertSet(string barcode, List<string> validRoutes)
        {
            if (!RoutingTable.ContainsKey(barcode) || validRoutes == null || validRoutes.Count == 0)
                return false;

            var destination = RoutingTable[barcode];

            return validRoutes.Any(divert => destination == divert);
        }

        public bool IsDivertSet(string barcode, List<string> validRoutes)
        {
            return DivertSet(barcode, validRoutes);
        }

        #endregion

        /// <summary>
        /// Takes the string input from the assembly that is entered by the user
        /// and if valid then converts it into a List of string
        /// </summary>
        /// <param name="code">Routing code for routing: format destination1,destination2,...,destination n</param>
        /// <returns>List of integer array</returns>
        public List<string> ValidateRoutingCode(string code) 
        {
            return code.Split(';').ToList();
        }

        //*******************************************************************

        #region ICaseController
        public BaseCaseData GetCaseData()
        {
            return new CaseData();
        }

        public Case_Load GetCaseLoad(Route route, float position)
        {
            return null;
        }
        #endregion

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
            CaseData datComData = caseData as CaseData;

            if (datComData == null)
            {
                Log.Write("ERROR: Bad cast to CaseData in CreateCaseLoad", Color.Red);
                return null;
            }

            load.Weight = caseData.Weight;
            load.Identification = datComData.ULID;
            load.Case_Data = datComData;
            return load;
        }

        public Experior.Dematic.Base.EuroPallet CreateEuroPallet(BasePalletData baseData)
        {
            return null;
        }
    }

    public class PlcStatusChangeEventArgs : EventArgs
    {
        public readonly CasePLC_State? State;

        public PlcStatusChangeEventArgs(CasePLC_State? state)
        {
            State = state;
        }
    }

    #region Helper classes

    public class CallForwardLocation
    {
        public ushort Quantity;
        public string Location;
        public Timer Timer;
    }

    #endregion

    [Serializable]
    [TypeConverter(typeof(CaseDatcomAusInfo))]
    public class CaseDatcomAusInfo : BaseDatcomAusControllerInfo { }

    public class CallForwardEventArgs : EventArgs
    {
        public readonly string Location;
        public readonly string Barcode;
        public CallForwardEventArgs(string location, string barcode)
        {
            Location = location;
            Barcode = barcode;
        }
    }
}