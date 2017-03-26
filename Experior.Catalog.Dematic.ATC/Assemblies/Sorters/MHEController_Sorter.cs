using Dematic.ATC;
using Experior.Catalog.Dematic.Case.Devices;
using Experior.Catalog.Dematic.Sorter.Assemblies;
using Experior.Catalog.Dematic.Sorter.Assemblies.Induction;
using Experior.Core.Assemblies;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace Experior.Catalog.Dematic.ATC.Assemblies.Sorters
{
    class MHEController_Sorter : BaseATCController, IController
    {
        MHEController_SorterATCInfo baseATCControllerInfo;
        SorterElement sorterElement;
        List<MHEControl> controls = new List<MHEControl>();
        public Experior.Core.Communication.TCPIP.Connection DespatchConnection;


        public MHEController_Sorter(MHEController_SorterATCInfo info) : base(info)
        {
            baseATCControllerInfo = info;

            if (info.connectionIDDespatch != 0)
            {
                ConnectionIDespatch = info.connectionIDDespatch;
            }
        }

        public MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info)
        {
            sorterElement = assem as SorterElement;

            MHEControl protocolConfig = null;  //generic plc config object

            if (assem is SorterElement)
            {
                protocolConfig = CreateMHEControlGeneric<SorterATCInfo, MHEControl_Sorter>(assem, info);
            }
            else if(assem is CommunicationPoint)
            {
                protocolConfig = CreateMHEControlGeneric<MHEControl_CommPointInfo, MHEControl_CommPoint>(assem, info);
            }
            else
            {
                Experior.Core.Environment.Log.Write("Can't create MHE Control, object is not defined in the 'CreateMHEControl' of the controller", Color.Red);
                return null;
            }
            //......other assemblies should be added here....do this with generics...correction better to do this with reflection...That is BaseController should use reflection
            //and not generics as we do not know the types at design time and it means that the above always has to be edited when adding a new MHE control object.
            protocolConfig.ParentAssembly = (Assembly)assem;
            controls.Add(protocolConfig);
            return protocolConfig as MHEControl;
        }

        public override void HandleTelegrams(string[] telegramFields, TelegramTypes type)
        {
            switch (type)
            {
                case TelegramTypes.StartTransportTelegram:
                    StartTransportTelegramReceived(telegramFields);
                    break;
                case TelegramTypes.SorterTransportMissionTelegram:
                    SorterTransportMissionTelegramReceived(telegramFields);
                    break;
                default:
                    break;
            }
        }

        private void StartTransportTelegramReceived(string[] telegramFields)
        {
            SorterInduction si = (SorterInduction)Assembly.Get(telegramFields.GetFieldValue(TelegramFields.source));
            ((ATCCaseLoad)si.CurrentLoad).Destination = telegramFields.GetFieldValue(TelegramFields.destination);
        }

        private void SorterTransportMissionTelegramReceived(string[] telegramFields)
        {
            string tuIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent);
            ATCCaseLoad caseLoad = ATCCaseLoad.GetCaseFromIdentification(tuIdent) as ATCCaseLoad;
            if (caseLoad != null)
            {
                //Add the sorter load data to the project fields
                AddorAppendField(caseLoad, TelegramFields.plcTrackingId.ToString(), telegramFields.GetFieldValue(TelegramFields.plcTrackingId));
                AddorAppendField(caseLoad, TelegramFields.carrierId.ToString(), telegramFields.GetFieldValue(TelegramFields.carrierId));
                AddorAppendField(caseLoad, TelegramFields.loadingType.ToString(), telegramFields.GetFieldValue(TelegramFields.loadingType));
                AddorAppendField(caseLoad, TelegramFields.dischargeCode.ToString(), telegramFields.GetFieldValue(TelegramFields.dischargeCode));
                AddorAppendField(caseLoad, TelegramFields.transportUnitDataStatus.ToString(), telegramFields.GetFieldValue(TelegramFields.transportUnitDataStatus));
                AddorAppendField(caseLoad, TelegramFields.recirculationReason.ToString(), telegramFields.GetFieldValue(TelegramFields.recirculationReason));
                AddorAppendField(caseLoad, TelegramFields.recircCounter.ToString(), telegramFields.GetFieldValue(TelegramFields.recircCounter));

                caseLoad.Destination = telegramFields.GetFieldValue(TelegramFields.destination);

                List<SorterElementFixPoint> destinations = sorterElement.Control.FixPointsWithChutePoint;
                SorterElementFixPoint destination = destinations.Find(x => x.Name == caseLoad.Destination);
                if (destination != null)
                {
                    sorterElement.Control.SetLoadDestination(caseLoad, destination);
                }
            }
            else
            {
                Log.Write(string.Format("{0} Error SorterTransportMissionTelegram; Cannot find Load from TUIdent {1}", Name, tuIdent), Color.Orange);
            }
        }

        private void AddorAppendField(ATCCaseLoad caseLoad, string field, string value)
        {
            if (caseLoad.ProjectFields.ContainsKey(field))
            {
                caseLoad.ProjectFields[field] = value;
            }
            else
            {
                caseLoad.ProjectFields.Add(field, value);
            }
        }


        protected override void Connection_OnDisconnected(Core.Communication.Connection connection)
        {
            Experior.Core.Communication.TCPIP.Connection thisConnection = connection as Experior.Core.Communication.TCPIP.Connection;
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection dropped for ID " + thisConnection.Id + " on IP " + thisConnection.Ip.ToString() + " and port " + thisConnection.Port.ToString(), Color.Red);

            if (DespatchConnection.State == Core.Communication.State.Disconnected && ControllerConnection.State == Core.Communication.State.Disconnected)
            {
                DisplayText.Color = Color.Red;  // PLC object text
            }
            else
            {
                DisplayText.Color = Color.Orange;  // PLC object text
            }

            plcConnected = false;
            PLC_State = ATCPLCStates.Disconnected;
        }

        protected override void Connection_OnConnected(Core.Communication.Connection connection)
        {
            Experior.Core.Communication.TCPIP.Connection thisConnection = connection as Experior.Core.Communication.TCPIP.Connection;
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection established for ID " + thisConnection.Id.ToString() + " on IP " + thisConnection.Ip.ToString() + " and port " + thisConnection.Port.ToString(), Color.DarkGreen);

            if (DespatchConnection.State == Core.Communication.State.Connected && ControllerConnection.State == Core.Communication.State.Connected)
            {
                DisplayText.Color = Color.LightGreen;  // PLC object text
                plcConnected = true;
                PLC_State = ATCPLCStates.Connected;
            }
            else
            {
                DisplayText.Color = Color.Orange;  // PLC object text
                plcConnected = false;
                PLC_State = ATCPLCStates.Disconnected;
            }
        }

        public void SendTelegram(string telegram, ConnectionChannel channel, bool logMessage)
        {
            string ChannelName = "";
            if (ControllerConnection != null && DespatchConnection != null && plcConnected)
            {
                if (channel == ConnectionChannel.Main)
                {
                    ChannelName = Name;
                    ControllerConnection.Send(telegram);
                }
                else if (channel == ConnectionChannel.Despatch)
                {
                    ChannelName = NameDespatch;
                    DespatchConnection.Send(telegram);
                }

                if (logMessage)
                {
                    LogTelegrams(string.Format("{0} ATC<{1}: {2}", DateTime.Now.ToString(), ChannelName, telegram), Color.Black);
                }
            }
            else
            {
                LogTelegrams(string.Format("Error: Cannot send message, controller {0} connection not configured; {1}", ChannelName, telegram), Color.Red);
            }
        }

        public void RemoveSSCCBarcode(string ULID)
        {
            throw new NotImplementedException();
        }

        #region User Interface
        [Category("Configuration")]
        [DisplayName("Connection ID (Main)")]
        [PropertyOrder(3)]
        [Description("Communicaion must be defined before this value can be set. Use ID from the communication list")]
        public override int ConnectionID
        {
            get
            {
                return base.ConnectionID;
            }

            set
            {
                base.ConnectionID = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Connection ID (Despatch)")]
        [Description("Communicaion must be defined before this value can be set. Use ID from the communication list")]
        [PropertyOrder(4)]
        public int ConnectionIDespatch
        {
            get { return baseATCControllerInfo.connectionIDDespatch; }
            set
            {
                Experior.Core.Communication.Connection connectionTemp = Experior.Core.Communication.Connection.Get(value);

                if (connectionTemp is Core.Communication.TCPIP.Connection && connectionTemp != null & value != 0)
                {
                    DespatchConnection = (Core.Communication.TCPIP.Connection)connectionTemp;
                    baseATCControllerInfo.connectionIDDespatch = value;
                    Core.Environment.Log.Write(DateTime.Now.ToString() + " " + NameDespatch + " is linked to communication ID " + value.ToString());

                    if (baseATCControllerInfo.connectionIDDespatch != 0)
                    {
                        DespatchConnection.OnTelegramReceived += Connection_OnTelegramReceived;
                        DespatchConnection.OnConnected += Connection_OnConnected;
                        DespatchConnection.OnDisconnected += Connection_OnDisconnected;
                    }
                }
                else
                {
                    Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + "Communication id must be equal to Receiver ID and not 0 (zero). Set communication id to a value first", Color.Red);
                }

            }
        }


        [Category("Configuration")]
        [DisplayName("Name (Main)")]
        [Description("Name of the MTS when sending from the Main sort controller")]
        [PropertyOrder(1)]
        public override string Name
        {
            get
            {
                return base.Name;
            }

            set
            {
                base.Name = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Name (Despatch)")]
        [Description("Name of the MTS when sending from the Despatch sort controller")]
        [PropertyOrder(2)]
        public string NameDespatch
        {
            get
            {
                return baseATCControllerInfo.NameDespatch;
            }

            set
            {
                baseATCControllerInfo.NameDespatch = value;
            }
        }


        #endregion

    }

    [Serializable]
    [TypeConverter(typeof(MHEController_SorterATCInfo))]
    public class MHEController_SorterATCInfo: BaseATCControllerInfo
    {
        public int connectionIDDespatch;
        public string NameDespatch;
    }

    enum ConnectionChannel
    {
        Main,
        Despatch
    }

}
