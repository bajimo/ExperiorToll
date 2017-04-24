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
using Environment = Experior.Core.Environment;
using EuroPallet = Experior.Dematic.Base.EuroPallet;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    /// <summary>
    /// This is a PLC that handels Datcom AUS messages
    /// </summary>
    public class MHEControllerAUS_Case : BaseDatcomAusController, IController, ICaseController
    {
        public enum FailedDatcomAusMessageType { _02, _06 }

        CaseDatcomAusInfo caseDatcomInfo;
        public int MaxRoutingTableEntries = Int32.MaxValue;
        public Dictionary<string, string> RoutingTable = new Dictionary<string, string>(); //barcode1, Destination
        public event EventHandler<MessageEventArgs> OnTransportOrderTelegramReceived;
        protected virtual void TransportOrderTelegramReceived(MessageEventArgs e)
        {
            OnTransportOrderTelegramReceived?.Invoke(this, e);
        }

        public event EventHandler<MessageEventArgs> OnRequestAllDataTelegramReceived;
        protected virtual void RequestAllDataTelegramReceived(MessageEventArgs e)
        {
            OnRequestAllDataTelegramReceived?.Invoke(this, e);
        }

        public event EventHandler<MessageEventArgs> OnSetSystemStatusTelegramReceived;
        protected virtual void SetSystemStatusTelegramReceived(MessageEventArgs e)
        {
            OnSetSystemStatusTelegramReceived?.Invoke(this, e);
        }

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

        public void RemoveFromRoutingTable(string barcode)
        {
            RoutingTable.Remove(barcode);
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
                Environment.Log.Write("Can't create MHE Control, object is not defined in the 'CreateMHEControl' of the controller");
                return null;
            }
            //......other assemblies should be added here....do this with generics...correction better to do this with reflection...That is BaseDatcomAusController should use reflection
            //and not generics as we do not know the types at design time and it means that the above always has to be edited when adding a new MHE control object.
            protocolConfig.ParentAssembly = (Assembly)assem;
            return protocolConfig;
        }

        /// <summary>
        /// Send 02 arrival message
        /// </summary>
        /// <param name="location"></param>
        /// <param name="load"></param>
        /// <param name="status">‘00’ Normal , ‘08’ Blocked , ‘09’ Waiting for Acknowledgement Blocked, ‘MD’ Manually Deleted, ‘DC’ Delete Confirmed, ‘DF’ Delete Fail</param>
        public void SendArrivalMessage(string location, Case_Load load, string status = "00")
        {
            if (String.IsNullOrWhiteSpace(location))
                return;

            if (load == null)
                return;

            if (PLC_State == CasePLC_State.Auto)
            {
                var caseData = load.Case_Data as CaseData;
                if (caseData == null)
                {
                    Log.Write($"{Name} failed to send arrival message: CaseData is null!");
                    return;
                }
                caseData.CurrentPosition = location;
                caseData.ULStatus = status;
                var telegram = CreateTelegramFromLoad(TelegramTypes.Arrival, load);
                SendTelegram(telegram);
            }
        }

        /// <summary>
        /// Send 06 exception message
        /// </summary>
        /// <param name="location"></param>
        /// <param name="load"></param>
        /// <param name="status">‘00’ Normal , ‘08’ Blocked , ‘09’ Waiting for Acknowledgement Blocked, ‘MD’ Manually Deleted, ‘DC’ Delete Confirmed, ‘DF’ Delete Fail</param>
        public void SendExceptionMessage(string location, Case_Load load, string status = "00")
        {
            if (String.IsNullOrWhiteSpace(location))
                return;

            if (load == null)
                return;

            if (PLC_State == CasePLC_State.Auto)
            {
                var caseData = load.Case_Data as CaseData;
                if (caseData == null)
                {
                    Log.Write($"{Name} failed to send arrival message: CaseData is null!");
                    return;
                }
                caseData.CurrentPosition = location;
                caseData.ULStatus = status;
                var telegram = CreateTelegramFromLoad(TelegramTypes.Exception, load);
                SendTelegram(telegram);
            }
        }

        public void SendRemapUlData(Case_Load load)
        {
            var type31 = CreateTelegramFromLoad(TelegramTypes.RemapULData, load);
            SendTelegram(type31);
        }

        public void SendEquipmentStatus(string functionGroup, string groupStatus)
        {
            var type10 = Template.CreateTelegram(this, TelegramTypes.EquipmentStatus);
            type10 = type10.SetFieldValue(this, TelegramFields.FunctionGroup, functionGroup);
            type10 = type10.SetFieldValue(this, TelegramFields.GroupStatus, groupStatus);
            SendTelegram(type10);
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
                case TelegramTypes.SetSystemStatus:
                    SetSystemStatusRecieved(telegram);
                    break;
                case TelegramTypes.SystemStatusReport:
                    SystemStatusReportRecieved(telegram);
                    break;
                case TelegramTypes.RequestAllData:
                    RequestAllDataRecieved(telegram);
                    break;
                case TelegramTypes.MaterialFlowStart:
                    MaterialFlowStartRecieved(telegram);
                    break;
                case TelegramTypes.MaterialFlowStop:
                    MaterialFlowStopRecieved(telegram);
                    break;
            }
        }

        void CasePLC_Datcom_OnPLCStateChange(object sender, PLCStateChangeEventArgs e)
        {
            PLC_State = e.CaseState;
        }

        public static event EventHandler<PlcStatusChangeEventArgs> OnCasePLCStatusChanged;

        private CasePLC_State? plcState = CasePLC_State.Ready;
        [Browsable(false)]
        public CasePLC_State? PLC_State
        {
            get { return plcState; }
            set
            {
                plcState = value;
                OnCasePLCStatusChanged?.Invoke(this, new PlcStatusChangeEventArgs(value));
            }
        }

        private void MaterialFlowStopRecieved(string telegram)
        {
            PLC_State = CasePLC_State.AutoNoMovement;
            string reply = Template.CreateTelegram(this, TelegramTypes.SystemStatusReport);
            reply = reply.SetFieldValue(this, TelegramFields.SystemStatus, "03");
            SendTelegram(reply);
        }

        private void MaterialFlowStartRecieved(string telegram)
        {
            PLC_State = CasePLC_State.Auto;
            string reply = Template.CreateTelegram(this, TelegramTypes.SystemStatusReport);
            reply = reply.SetFieldValue(this, TelegramFields.SystemStatus, "04");
            SendTelegram(reply);
        }

        private void RequestAllDataRecieved(string telegram)
        {
            //A “Request All Data” telegram(Type 30) is sent by the WCS.For each of the conveyor locations listed above, 
            //the PLC checks to see if there is a carrier present. For each carrier place for which data is present, 
            //a “One Location Data Record” telegram(Type 31) is sent.The telegram contains all the details about the carrier.

            //Controller must send type 31 messages "Re-map Unit Load Data"
            //Notify subscribers
            RequestAllDataTelegramReceived(new MessageEventArgs("", "", telegram, null, TelegramTypes.RequestAllData));

            //After the last “One Location Data Record” telegram has been sent by the PLC, an ‘End of Re - map’ telegram(Type 32) is sent to indicate an end of the remap.
            string reply = Template.CreateTelegram(this, TelegramTypes.EndRemap);
            SendTelegram(reply);
        }

        private void SystemStatusReportRecieved(string telegram)
        {
            string telegramStatus = telegram.GetFieldValue(this, TelegramFields.SystemStatus);
            if (telegramStatus == "02")
            {
                PLC_State = CasePLC_State.Ready;
            }

            string reply = Template.CreateTelegram(this, TelegramTypes.SystemStatusReport);
            var status = "00";
            if (PLC_State == CasePLC_State.Ready)
                status = "02";
            else if (PLC_State == CasePLC_State.AutoNoMovement)
                status = "03";
            else if (PLC_State == CasePLC_State.Auto)
                status = "04";

            reply = reply.SetFieldValue(this, TelegramFields.SystemStatus, status);
            SendTelegram(reply);
        }

        private void SetSystemStatusRecieved(string telegram)
        {
            string status = telegram.GetFieldValue(this, TelegramFields.SystemStatus);

            if (status == "02")
            {
                PLC_State = CasePLC_State.Ready;
            }
            if (status == "03")
            {
                PLC_State = CasePLC_State.AutoNoMovement;
            }
            if (status == "00")
            {
                PLC_State = CasePLC_State.Unknown;
            }

            //Notify subscribers
            SetSystemStatusTelegramReceived(new MessageEventArgs("", "", telegram, null, TelegramTypes.SetSystemStatus));

            string reply = Template.CreateTelegram(this, TelegramTypes.SystemStatusReport);
            reply = reply.SetFieldValue(this, TelegramFields.SystemStatus, status);
            SendTelegram(reply);
        }

        public void CancelMissionRecieved(string telegram)
        {
            //Just remove from routing table?
            var barcode1 = telegram.GetFieldValue(this, TelegramFields.Barcode1);
            RoutingTable.Remove(barcode1);
        }

        public void TransportOrderRecieved(string telegram)
        {
            //This message creates new entries in the Routing Table for specific ULs or updates existing records in the Routing Table for specific ULs.
            int count = RoutingTable.Count;

            var barcode1 = telegram.GetFieldValue(this, TelegramFields.Barcode1);
            var current = telegram.GetFieldValue(this, TelegramFields.Current);

            Case_Load caseload = null;
            if (string.IsNullOrWhiteSpace(barcode1))
            {
                //Search by current location
                caseload = Case_Load.AllCases.FirstOrDefault(c => c.CurrentActionPoint != null && c.CurrentActionPoint.Name == current);
            }
            else
            {
                //Search by barcode
                caseload = Case_Load.GetCaseFromIdentification(barcode1);
            }

            var destination = telegram.GetFieldValue(this, TelegramFields.Destination).Trim();

            //Check if the load has a datcom case data, if not create it as it may have come from a different system that has different case data e.g. DCI multishuttle
            if (caseload != null && caseload.Case_Data.GetType() != typeof(CaseData))
            {
                CaseData caseData = new CaseData();
                caseData.Length = caseload.Case_Data.Length;
                caseData.Width = caseload.Case_Data.Width;
                caseData.Height = caseload.Case_Data.Height;
                caseData.Weight = caseload.Case_Data.Weight;
                caseData.colour = caseload.Case_Data.colour;
                caseload.Case_Data = caseData;
            }

            var cData = caseload?.Case_Data as CaseData;
            if (cData != null)
            {
                //Update destination
                cData.DestinationPosition = destination;
            }
       
            RoutingTable[barcode1] = destination;

            if (caseload != null && caseload.LoadWaitingForWCS)
                caseload.ReleaseLoad_WCSControl();

            int newcount = RoutingTable.Count;

            if (newcount > MaxRoutingTableEntries && newcount > count)
            {
                //TODO do AUS datcom do this?
                //New entry added to the routing table and routing table exceeds limit.
            }

            //Notify subscribers
            TransportOrderTelegramReceived(new MessageEventArgs(current, barcode1, telegram, caseload, TelegramTypes.TransportOrder));
        }

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

        /// <summary>
        /// Takes the string input from the assembly that is entered by the user
        /// and if valid then converts it into a List of string
        /// </summary>
        /// <param name="code">Routing code for routing: format destination1,destination2,...,destination n</param>
        /// <returns>List of integer array</returns>
        public List<string> ValidateRoutingCode(string code)
        {
            return code.Split(',').ToList();
        }

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
            // load.Identification = datComData.Barcode1;
            load.Case_Data = datComData;
            return load;
        }

        public EuroPallet CreateEuroPallet(BasePalletData baseData)
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

    [Serializable]
    [TypeConverter(typeof(CaseDatcomAusInfo))]
    public class CaseDatcomAusInfo : BaseDatcomAusControllerInfo { }

    public class MessageEventArgs : EventArgs
    {
        public readonly TelegramTypes Type;
        public readonly string Location;
        public readonly string Barcode;
        public readonly string Telegram;
        public readonly Load Load;
        public MessageEventArgs(string location, string barcode, string telegram, Load load, TelegramTypes type)
        {
            Type = type;
            Location = location;
            Barcode = barcode;
            Telegram = telegram;
            Load = load;
        }
    }
}