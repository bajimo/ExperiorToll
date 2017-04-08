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

//using System.Windows.Forms;

namespace Experior.Catalog.Dematic.DatcomUK.Assemblies
{
    /// <summary>
    /// This is a PLC that handels Datcom messages
    /// </summary>
    public class CasePLC_Datcom : BaseController, IController, ICaseController //TODO Change the name to MHEController_Case
    {
        CaseDatcomInfo caseDatcomInfo;
        public Dictionary<string, CallForwardLocation> callForwardTable = new Dictionary<string, CallForwardLocation>();
        public int MaxRoutingTableEntries = int.MaxValue;
        public int NumberOfDestWords = 4;
        public Dictionary<string, UInt16[]> RoutingTable = new Dictionary<string, UInt16[]>(); //SSCCBarcode, Destinations[]

        public event EventHandler<CallForwardEventArgs> OnCallForwardTelegramReceived;
        protected virtual void CallForwardTelegramReceived(CallForwardEventArgs e)
        {
            OnCallForwardTelegramReceived?.Invoke(this, e);
        }

        public CasePLC_Datcom(CaseDatcomInfo info): base(info)
        {
            caseDatcomInfo = info;
            OnPLCStateChange += CasePLC_Datcom_OnPLCStateChange;
        }

        public override void Dispose()
        {
            callForwardTable.Clear();
            RoutingTable.Clear();

            base.Dispose();
        }

        public override Image Image
        {
            get { return Common.Icons.Get("PLC"); }
        }

        public void RemoveFromRoutingTable(string barcode)
        {
            if (RoutingTable.ContainsKey(barcode))
            {
                RoutingTable.Remove(barcode);
            }
        }

        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            MHEControl protocolConfig = null;  //generic plc config object
            //ProtocolInfo protocolInfo = null;  //generic plc config object constructor argument type
            Dictionary<string, Type> dt = new Dictionary<string, Type>();

            if (assem is CommunicationPoint)
            {
                protocolConfig = CreateMHEControlGeneric<CommPointDatcomInfo, MHEControl_CommPoint>(assem, info);
            }
            else if (assem is MergeDivertConveyor)
            {
                protocolConfig = CreateMHEControlGeneric<MergeDivertDatcomInfo, MHEControl_MergeDivert>(assem, info);
            }
            else if (assem is Transfer)
            {
                protocolConfig = CreateMHEControlGeneric<TransferDatcomInfo, MHEControl_Transfer>(assem, info);
            }
            else if (assem is StraightAccumulationConveyor)
            {
                protocolConfig = CreateMHEControlGeneric<ManualPickingDatcomInfo, MHEControl_ManualPicking>(assem, info);
            }
            else if (assem is BeltSorterDivert)
            {
                protocolConfig = CreateMHEControlGeneric<BeltSorterDivertDatcomInfo, MHEControl_BeltSorterDivert>(assem, info);
            }
            else if (assem is AngledDivert)
            {
                protocolConfig = CreateMHEControlGeneric<AngledDivertDatcomInfo, MHEControl_AngledDivert>(assem, info);
            }
            else if (assem is ThreeWaySwitch)
            {
                protocolConfig = CreateMHEControlGeneric<ThreeWaySwitchDatcomInfo, MHEControl_ThreeWaySwitch>(assem, info);
            }
            else if (assem is PickDoubleLift)
            {
                protocolConfig = CreateMHEControlGeneric<PickDoubleLiftDatcomInfo, MHEControl_PickDoubleLift>(assem, info);
            }
            else if (assem is PickPutStation)
            {
                protocolConfig = CreateMHEControlGeneric<PickPutStationDatcomInfo, MHEControl_PickPutStation>(assem, info);
            }
            else
            {
                Experior.Core.Environment.Log.Write("Can't create MHE Control, object is not defined in the 'CreateMHEControl' of the controller");
                return null;
            }
            //......other assemblies should be added here....do this with generics...correction better to do this with reflection...That is BaseController should use reflection
            //and not generics as we do not know the types at design time and it means that the above always has to be edited when adding a new MHE control object.
            protocolConfig.ParentAssembly = (Assembly)assem;
            return protocolConfig as MHEControl;
        }

        public void SendDivertConfirmation(string location, string SSCCBarcode)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return;
            }

            if (PLC_State == CasePLC_State.Ready)
            {
                SendTelegram("02", SSCCBarcode + "," + location, 1);
            }
        }

        public override void HandleTelegrams(string[] telegramFields, string type, ushort number_of_blocks)
        {
            switch (type)
            {
                case "01":
                    RoutingTableUpdateRecieved(telegramFields, number_of_blocks);
                    break;
                case "04":
                    PurgeRoutingTableRecieved(telegramFields, number_of_blocks);
                    break;
                case "09":
                    CraneForkStatusRecieved(telegramFields, number_of_blocks);
                    break;
                case "13":
                    NodeStatusReportRecieved(telegramFields, number_of_blocks);
                    break;
                case "26":
                    CallForwardRecieved(telegramFields, number_of_blocks);
                    break;
                case "86":
                    CallForwardWithBarcode(telegramFields, number_of_blocks);
                    break;
                default:
                    break;
            }
        }

        void CasePLC_Datcom_OnPLCStateChange(object sender, PLCStateChangeEventArgs e)
        {
            PLC_State = e._CaseState;
        }

        public static event EventHandler<PLCStatusChangeEventArgs> OnCasePLCStatusChanged;

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
                    OnCasePLCStatusChanged(this, new PLCStatusChangeEventArgs(value));
                }
            }
        }

        public void CallForwardWithBarcode(string[] telegramFields, ushort number_of_blocks)
        {
            for (ushort block = 0; block < number_of_blocks; block++)
            {
                string location = telegramFields[6 + block * 2];
                ushort quantity = ushort.Parse(telegramFields[7 + block * 2]);
                string barcode = telegramFields[8 + block * 2];

                //Call forward at manual picking points on Accumulation conveyor
                if (Core.Assemblies.Assembly.Items.ContainsKey(location) && Core.Assemblies.Assembly.Items[location] is StraightAccumulationConveyor)
                {
                    StraightAccumulationConveyor conv = Core.Assemblies.Assembly.Items[location] as StraightAccumulationConveyor;
                    if (conv.ControllerProperties != null)
                    {
                        MHEControl_ManualPicking control = conv.ControllerProperties as MHEControl_ManualPicking;
                        control.CallForwardReceived(barcode);
                    }
                }
                CallForwardTelegramReceived(new CallForwardEventArgs(location, barcode));
            }
        }

        public void CallForwardRecieved(string[] telegramFields, ushort number_of_blocks)
        {
            //This message is used by MFH to instruct CCC to release a number of ULs from a location.

            for (ushort block = 0; block < number_of_blocks; block++)
            {
                string location = telegramFields[6 + block * 2];
                ushort quantity = ushort.Parse(telegramFields[7 + block * 2]);

                //Call forward at manual picking points on Accumulation conveyor
                if (Core.Assemblies.Assembly.Items.ContainsKey(location) && Core.Assemblies.Assembly.Items[location] is StraightAccumulationConveyor)
                {
                    StraightAccumulationConveyor conv = Core.Assemblies.Assembly.Items[location] as StraightAccumulationConveyor;
                    if (conv.ControllerProperties != null)
                    {
                        MHEControl_ManualPicking control = conv.ControllerProperties as MHEControl_ManualPicking;
                        control.CallForwardReceived(null);
                    }
                }
            }
        }

        public void RemoveCallForwardLocation(string location)
        {
            callForwardTable[location].Timer.Stop();
            callForwardTable[location].Timer.OnElapsed -= new Timer.ElapsedEvent(CallForwardElapsed);
            callForwardTable[location].Timer.Dispose();
            callForwardTable.Remove(location);
        }

        public void CallForwardElapsed(Timer sender)
        {
            string location = sender.UserData as string;

            if (callForwardTable.ContainsKey(location))
            {
                if (callForwardTable[location].Quantity > 0)
                {
                    //If the CCC is unable to supply the required quantity of totes, it sends a Call Forward Exception
                    //Message (Type 27) to MFH. The quantity field sent in this message is the outstanding number of
                    //totes.
                    SendCallForwardException(location, callForwardTable[location].Quantity);
                }

                RemoveCallForwardLocation(location);
            }
        }

        public void SendCallForwardException(string location, ushort Outstanding_quantity_of_ULs_not_released)
        {
            if (string.IsNullOrWhiteSpace(location))
                return;

            if (PLC_State == CasePLC_State.Ready)
                SendTelegram("27", location + "," + Outstanding_quantity_of_ULs_not_released, 1);
        }

        public void SendLaneOccupancyMessage(string location, string status)
        {
            if (string.IsNullOrWhiteSpace(location))
                return;

            SendTelegram("28", location + "," + status, 1);
        }

        public void NodeStatusReportRecieved(string[] telegramFields, ushort number_of_blocks)
        {
            string status = telegramFields[6];

            //Status 02 ready during remap. For Heartbeat status 07 always just reply with the same status:
            bool heartbeat = false;
            if (status == "07")
                heartbeat = true;

            bool log = true;

            if (!LogHeartBeat && heartbeat)
                log = false;

            SendTelegram("13", status, 1, log);

            if (status == "02")
            {
                int count = RoutingTable.Count;
                string routingTablestatus = "00";
                if (count > MaxRoutingTableEntries)
                {
                    routingTablestatus = "01"; //Status 01 means routing Table critically full.
                }

                SendTelegram("10", routingTablestatus, 1);

                PLC_State = CasePLC_State.Ready;
            }
        }

        public void CraneForkStatusRecieved(string[] telegramFields, ushort number_of_blocks)
        {

            bool[] pickStationUsed = new bool[4] { true, true, true, true };

            for (int i = 7; i < telegramFields.Length - 2; i++)
            {
                if (telegramFields[i] == "01")
                    pickStationUsed[i - 7] = false;
            }

            MiniloadPickStationStatus(this, telegramFields[6], pickStationUsed);

            //TODO
            //SendCraneInputStationArrival(string craneNumber, List<Case_Load> EPCases)();
            //string status = "00";
            //SendAPLevelStatus(status);
        }

        //public event PickStationStatus MiniloadPickStationStatusEvent;

        public void MiniloadPickStationStatus(IController sender, string crane, bool[] pickStationStatus)
        {
            //MiniloadPickStationStatusEvent(sender, crane, pickStationStatus);
        }

        public void PurgeRoutingTableRecieved(string[] telegramFields, ushort number_of_blocks)
        {
            //This message deletes the entire Routing Table in a CCC and will be typically used as part of some sort of housekeeping functionality.
            RoutingTable.Clear();
            //Purging the Routing Table by CCC (as instructed by MFH) will trigger another Routing Table Status
            //message indicating that the table is 'no longer critically full'.
            string status = "00"; //Routing table no longer critically full.  Status 01 means routing Table critically full.
            SendTelegram("10", status, 1);
        }

        public void RoutingTableUpdateRecieved(string[] telegramFields, ushort number_of_blocks)
        {
            //This message creates new entries in the Routing Table for specific ULs or updates existing records in the Routing Table for specific ULs.
            int count = RoutingTable.Count;

            int offset = NumberOfDestWords + 1;

            for (ushort block = 0; block < number_of_blocks; block++)
            {
                string SSCCBarcode = telegramFields[6 + block * offset];
                //Case_Load caseload = Case_Load.GetCaseFromSSCCBarcode(SSCCBarcode);
                Case_Load caseload = Case_Load.GetCaseFromIdentification(SSCCBarcode);
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

                RoutingTable[SSCCBarcode] = new UInt16[NumberOfDestWords];

                for (int k = 7; k < 7 + NumberOfDestWords; k++)
                {
                    string word = telegramFields[k + block * offset];
                    RoutingTable[SSCCBarcode][k - 7] = UInt16.Parse(word);
                }

                //Remove if destinations is 0
                //To explicitly delete all entries for a specific UL from the Routing Table the destination fields for the UL will be set to all zero ('0').
                ushort[] dest = RoutingTable[SSCCBarcode];
                bool delete = true;
                for (int i = 0; i < dest.Length; i++)
                {
                    if (dest[i] != 0)
                        delete = false;
                }
                if (delete)
                    RoutingTable.Remove(SSCCBarcode);

                //Update caseload if it exists
                if (caseload != null)
                {
                    if (caseload.CurrentActionPoint != null)
                    {
                        //if (caseload.CurrentActionPoint.UserData is RapidPick5400StationA6)
                        //{
                        //    //tote is on inpoint to rapid pick. Let rapid pick decide if the tote can be released.
                        //    RapidPick5400StationA6 rapid = caseload.CurrentActionPoint.UserData as RapidPick5400StationA6;
                        //    rapid.ReleaseInPoint();
                        //    return;
                        //}
                    }

                    if (((CaseData)caseload.Case_Data).CallforwardWait)
                    {
                        return; //Tote is waiting to be called forward. Do not release
                    }

                    ((CaseData)caseload.Case_Data).RoutingTableUpdateWait = false;

                }
            }

            int newcount = RoutingTable.Count;

            if (newcount > MaxRoutingTableEntries && newcount > count)
            {
                //New entry added to the routing table and routing table exceeds limit.
                string status = "01"; //Status 01 means routing Table critically full.
                SendTelegram("10", status, 1);
            }
        }

        //*****************************************************************
        #region Route Checking

        public bool DivertSet(string barcode, List<int[]> validRoutes)
        {
            if (!RoutingTable.ContainsKey(barcode) || validRoutes == null || validRoutes.Count == 0)
                return false;

            foreach (int[] wordBit in validRoutes)
            {
                if (GetBit(RoutingTable[barcode], wordBit[1], wordBit[0]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// A better name for DivertSet
        /// </summary>

        public bool IsDivertSet(string barcode, List<int[]> validRoutes)
        {
            return DivertSet(barcode, validRoutes);
        }

        private bool GetBit(UInt16[] routing, int bit, int word)
        {
            return (routing[word - 1] & (1 << (bit - 1))) != 0;
        }

        #endregion

        /// <summary>
        /// Takes the string input from the assembly that is entered by the user
        /// and if valid then converts it into a List of integer array
        /// </summary>
        /// <param name="code">Routing code for routing: format w,b;w,b... where w = word and b = bit e.g. 1,1;2,1 - route to lhs if word 1 bit 1 or word 2 bit 1 is set in the PLC routing table</param>
        /// <returns>List of integer array</returns>
        public List<int[]> ValidateRoutingCode(string code) //TODO would be nice to have this as a static method for use in a routing script when not using a controller
        {
            try
            {
                List<int[]> routes = new List<int[]>();
                if (!string.IsNullOrEmpty(code))
                {
                    string[] splitRoutes = code.Split(';');
                    foreach (string route in splitRoutes)
                    {
                        string[] splitRoute = route.Split(',');
                        if (splitRoute.Length != 2)
                            throw new Exception();
                        int word, bit;
                        if (int.TryParse(splitRoute[0], out word) && (int.TryParse(splitRoute[1], out bit)))
                        {
                            if (word < 5 && word > 0 && bit < 17 && bit > 0)
                            {
                                int[] wordBit = { word, bit };
                                routes.Add(wordBit);
                            }
                            else
                                throw new Exception();
                        }
                        else
                            throw new Exception();
                    }
                }
                return routes;
            }
            catch //Could not convert to valid routing
            {
                return null;
            }
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
            CaseData DatComData = caseData as CaseData;

            if (DatComData == null)
            {
                Log.Write("ERROR: Bad cast to CaseData in CreateCaseLoad", Color.Red);
                return null;
            }

            load.Weight = caseData.Weight;
            load.Identification = DatComData.ULID;
            load.Case_Data = DatComData;
            return load;
        }

        public Experior.Dematic.Base.EuroPallet CreateEuroPallet(BasePalletData baseData)
        {
            return null;
        }
    }




    public class PLCStatusChangeEventArgs : EventArgs
    {
        public readonly CasePLC_State? _state;

        public PLCStatusChangeEventArgs(CasePLC_State? state)
        {
            _state = state;
        }
    }

    #region Helper classes

    public class WordBit
    {
        public int Word;
        public int Bit;
    }

    public class CallForwardLocation
    {
        public ushort Quantity;
        public string Location;
        public Timer Timer;
    }

    #endregion

    [Serializable]
    [TypeConverter(typeof(CaseDatcomInfo))]
    public class CaseDatcomInfo : BaseControllerInfo{}

    public class CallForwardEventArgs : EventArgs
    {
        public readonly string _location;
        public readonly string _barcode;
        public CallForwardEventArgs(string location, string barcode)
        {
            _location = location;
            _barcode = barcode;
        }
    }
}
