using Dematic.DCI;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Experior.Core.Communication;
using Environment = Experior.Core.Environment;

namespace Experior.Catalog.Dematic.DCI.Assemblies
{
    public abstract class BaseDCIController : Assembly, IDCITelegrams
    {
        public event EventHandler OnControllerDeletedEvent;
        public event EventHandler OnControllerRenamedEvent;

        private BaseDCIControllerInfo baseControllerInfo;
        public Text3D DisplayText;
        private Cube plcCube, door1, door2, plinth;
        public bool plcConnected;
    
        public List<string> ProjectFields = new List<string>();

        //DCI Telegram ack handling 
        bool waitingForAck;
        string waitingAckTelegram;
        System.Timers.Timer ackTimer = new System.Timers.Timer();
        int ackTimerInterval = 1000;
        private List<string> sendList = new List<string>();
        int ackResendCount = 0;
        int lastAckCycle = 0;

        //public static event EventHandler<PLCStatusChangeEventArgs> OnPLCStatusChanged;//Static Routing Script Events

        public delegate bool TelegramRecievedHandle(BaseController sender, string type, string[] telegramFields, ushort number_of_blocks);
        /// <summary>
        /// Handle project specific telegrams. If false is returned then the plc will handle it. If true is returned the plc expects the user to handle the telegram.
        /// </summary>
        public TelegramRecievedHandle HandleTelegram;

        public Experior.Core.Communication.TCPIP.Connection ControllerConnection;

        public static Regex binLocRegEx = new Regex(@"[aA-zZ]{2}(?<Aisle>\d{2}){1}?(?<Side>\d{1}){1}?(?<XLoc>\d{3}){1}?(?<YLoc>\d{2}){1}?(?<Depth>[0-9iIaA]{2}){1}?(?<RasterType>\d{1}){1}?(?<RasterPos>\d{1}){1}?", RegexOptions.Compiled);
        //public static Regex binLocRegEx = new Regex(@"(?<Aisle>\d{2}){1}?(?<Side>\d{1}){1}?(?<XLoc>\d{3}){1}?(?<YLoc>\d{2}){1}?(?<Depth>\d{2}){1}?(?<RasterType>\d{1}){1}?(?<RasterPos>\d{1}){1}?", RegexOptions.Compiled);
        public static Regex racklocRegEx = new Regex(@"MSAI(?<Aisle>\d{2}){1}?L(?<Side>[LlRr]){1}?(?<Level>\d{2}){1}?R(?<ConvType>[IiOo]){1}?(?<LiftNum>\d{1}){1}?(?<ConvPos>\d{1}){1}?", RegexOptions.Compiled);
        public static Regex psDSlocRegEx = new Regex(@"MSAI(?<Aisle>\d{2}){1}?C(?<Side>[LlRr]){1}?(?<Level>\d{2}){1}?(?<ConvType>[PpDd]){1}?S(?<LiftNum>\d{1}){1}?(?<ConvPos>\d{1}){1}?", RegexOptions.Compiled);

        protected BaseDCIController(BaseDCIControllerInfo info) : base(info)
        {
            baseControllerInfo = info;

            plcCube = new Cube(Color.Wheat, 1.3f, 1.8f, 0.4f);
            door1 = new Cube(Color.Wheat, 0.62f, 1.585f, 0.25f);
            door2 = new Cube(Color.Wheat, 0.62f, 1.585f, 0.25f);
            plinth = new Cube(Color.DimGray, 1.32f, 0.1985f, 0.42f);
            Font f = new Font("Helvetica", 0.4f, FontStyle.Bold, GraphicsUnit.Pixel);
            DisplayText = new Text3D(Color.Red, 0.4f, 0.3f, f);
            DisplayText.Pitch = (float)Math.PI / 2;
            Add((RigidPart)DisplayText, new Vector3(-0.62f, 1.25f, -0.125f));

            Add((RigidPart)plcCube, new Vector3(0, 0.45f, 0));
            Add((RigidPart)door1, new Vector3(-0.325f, 0.5425f, -0.1f));
            Add((RigidPart)door2, new Vector3(0.325f, 0.5425f, -0.1f));
            Add((RigidPart)plinth, new Vector3(0, -0.355f, 0));

            DisplayText.Text = info.name;

            OnNameChanged += CaseDatcom_OnNameChanged;
            ConfigureTelegramTemplate();

            if (info.connectionID != 0)
            {
                ConnectionID = info.connectionID;
            }

            ackTimer.Elapsed += AckTimer_Elapsed;
        }

        public override void Reset()
        {
            base.Reset();
            sendList.Clear();
        }

        public override void Dispose()
        {
            if (OnControllerDeletedEvent != null)
            {
                OnControllerDeletedEvent(this, new EventArgs());
            }
            base.Dispose();
        }

        void CaseDatcom_OnNameChanged(Assembly sender, string current, string old)
        {
            if (OnControllerRenamedEvent != null)
            {
                OnControllerRenamedEvent(this, new EventArgs());
            }
            DisplayText.Text = Name;
        }

        //BaseController should use reflection
        //and not generics as we do not know the types at design time
        public static MHEControl CreateMHEControlGeneric<T, U>(IControllable assem, ProtocolInfo info)
            where T : ProtocolInfo
            where U : MHEControl
        {
            MHEControl protocolConfig = null; //generic plc config object
            try
            {
                if (info == null)
                {
                    var i = (T)Activator.CreateInstance(typeof(T), null);
                    ProtocolInfo protocolInfo = i; //generic plc config object constructor argument type
                    protocolInfo.assem = assem.Name;
                    object[] args = { protocolInfo, assem };
                    var ctr = (U)Activator.CreateInstance(typeof(U), args);
                    protocolConfig = ctr;
                }
                else
                {
                    object[] args = { info, assem };
                    var ctr = (U)Activator.CreateInstance(typeof(U), args);
                    info.assem = assem.Name;
                    protocolConfig = ctr;
                }
            }
            catch (Exception e)
            {
                Experior.Core.Environment.Log.Write("Can't create control!", Color.Red);
                Environment.Log.Write(e.Message, Color.Red);
            }
            return protocolConfig;
        }

        public override string Category
        {
            get { return "Controller"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("PLC"); }
        }

        private void ConfigureTelegramTemplate()
        {
            _Template = new TelegramTemplate();
            foreach (TelegramTemplate.Telegram telegram in Template.TelegramsData)
            {
                if (telegram.Type == TelegramTypes.TUMission || telegram.Type == TelegramTypes.TUReport || telegram.Type == TelegramTypes.TUNotification || telegram.Type == TelegramTypes.TUException ||
                    telegram.Type == TelegramTypes.TUMissionCancel || telegram.Type == TelegramTypes.TUCancel || telegram.Type == TelegramTypes.TUDataRequest || telegram.Type == TelegramTypes.TULocationLeft)
                {
                    telegram.FieldList.Add(TelegramFields.DropIndex);
                    telegram.FieldList.Add(TelegramFields.ShuttleDynamics);
                    telegram.FieldList.Add(TelegramFields.LiftDynamics);
                    telegram.FieldList.Add(TelegramFields.SourceShuttleExtension);
                    telegram.FieldList.Add(TelegramFields.DestinationShuttleExtension);
                    if (DCIVersion == DCIVersions._1_60)
                    {
                        telegram.FieldList.Add(TelegramFields.CaseConveyorDynamics);
                    }
                }
            }
        }

        public void SendTelegram(string telegram, bool noAckResend = false)
        {
            if (ControllerConnection != null)
            {
                if (plcConnected && (!waitingForAck || noAckResend))
                {
                    Send(telegram);
                    if (telegram.GetFieldValue(this, TelegramFields.Flow) == "R")
                    {
                        //Remember the last cycle number that was sent so that the ack can be checked against it
                        int.TryParse(telegram.GetFieldValue(this, TelegramFields.CycleNo), out lastAckCycle);
                        waitingForAck = true;
                        //When message sent start ack timer
                        waitingAckTelegram = telegram;
                        ackTimer.Interval = ackTimerInterval;
                        ackTimer.Start();
                    }
                }
                else
                {
                    sendList.Add(telegram);
                }
            }
            else
            {
                LogTelegrams(string.Format("Error {0}: Sending message - {1}", Name, telegram), Color.Red);
            }
        }

        private void SendBuffer()
        {
            try
            {
                if (sendList.Count != 0)
                {
                    if (ControllerConnection != null && plcConnected)
                    {
                        string telegram = string.Copy(sendList[0]);
                        sendList.Remove(sendList[0]);
                        SendTelegram(telegram);
                    }
                }
            }
            catch { }
        }

        private void AckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (ackResendCount > 2)
            {    
                if (ControllerConnection.State == State.Connected)
                    ControllerConnection.Disconnect();
            }
            else
            {
                SendTelegram(waitingAckTelegram, true);
                ackResendCount++;
            }
        }

        private void Send(string telegram)
        {
            if (ControllerConnection != null && plcConnected)
            {
                ControllerConnection.Send(telegram);

                if (!telegram.Contains(",LIVE,") || LogAll)
                {
                    LogTelegrams(string.Format("{0} {1}>{2}: {3}", DateTime.Now.ToString(), SenderID, ReceiverID, telegram), Color.Black);
                }
            }
            else
            {
                LogTelegrams(string.Format("Error: Cannot send message, controller {0} connection not configured; {1}", Name, telegram), Color.Red);
            }
        }

        private void SendAckOnly(string telegram)
        {
            if (ControllerConnection != null)
            {
                if (plcConnected)
                {
                    ControllerConnection.Send(telegram);
                    if (LogAll)
                    {
                        LogTelegrams(string.Format("{0} {1}>{2}: {3}", DateTime.Now.ToString(), SenderID, ReceiverID, telegram), Color.Black);
                    }
                }
            }
            else
            {
                LogTelegrams(string.Format("Error: Cannot send message, controller {0} connection not configured; {1}", Name, telegram), Color.Red);
            }
        }



        public void LogTelegrams(string message, Color colour)
        {
            if (!message.Contains(",LIVE,") || LogAll)
                Environment.Log.Write(message, colour);
        }

        [Category("Project")]
        [DisplayName("Load Type")]
        [Description("Load Type to create when receiving a retreival telegram from the racking")]
        public virtual LoadTypes LoadType
        {
            get { return baseControllerInfo.loadType; }
            set { baseControllerInfo.loadType = value; }
        }

        [Category("Configuration")]
        [DisplayName("Connection ID")]
        [PropertyOrder(4)]
        [Description("Communicaion must be defined before this value can be set. Use ID from the communication list")]
        public virtual int ConnectionID
        {
            get
            {
                return baseControllerInfo.connectionID;
            }
            set
            {
                Experior.Core.Communication.Connection connectionTemp = Experior.Core.Communication.Connection.Get(value);

                if (connectionTemp is Core.Communication.TCPIP.Connection && connectionTemp != null & value != 0)
                {
                    ControllerConnection = (Core.Communication.TCPIP.Connection)connectionTemp;
                    baseControllerInfo.connectionID = value;
                    Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " is linked to communication ID " + value.ToString());

                    if (baseControllerInfo.connectionID != 0)
                    {
                        ControllerConnection.OnTelegramReceived += Connection_OnTelegramReceived;
                        ControllerConnection.OnConnected += Connection_OnConnected;
                        ControllerConnection.OnDisconnected += Connection_OnDisconnected;
                    }
                }
                else
                {
                    Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + "Communication id must be equal to Receiver ID and not 0 (zero). Set communication id to a value first", Color.Red);
                }
            }
        }

        public event EventHandler<DCIPLCStateChangeEventArgs> OnPLCStateChange;

        protected virtual void Connection_OnDisconnected(Core.Communication.Connection connection)
        {
            DisplayText.Color = Color.Red;  // PLC object text
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString(CultureInfo.InvariantCulture) + " " + this.Name + " connection dropped for ID " + ControllerConnection.Id + " on IP " + ControllerConnection.Ip.ToString() + " and port " + ControllerConnection.Port.ToString(), Color.Red);
            // ControllerConnection.AutoConnect = true;
            plcConnected = false;
            ackTimer.Stop();
            waitingForAck = false;
            ackResendCount = 0;
            waitingAckTelegram = "";
            PLC_State = DCIPLCStates.Disconnected;
        }

        protected virtual void Connection_OnConnected(Core.Communication.Connection connection)
        {
            DisplayText.Color = Color.LightGreen;  // PLC object text
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection established for ID " + ControllerConnection.Id.ToString() + " on IP " + ControllerConnection.Ip.ToString() + " and port " + ControllerConnection.Port.ToString(), Color.DarkGreen);
            plcConnected = true;
            PLC_State = DCIPLCStates.Disconnected;
        }

        public delegate void connection_OnTelegramReceivedEvent(Experior.Core.Communication.TCPIP.Connection sender, string telegram);
        public delegate bool OverrideTelegramReceived(BaseDCIController controller, string telegram);
        public static event OverrideTelegramReceived OnOverrideTelegramReceived; //Return true if the message was handled elsewhere (Routing Script)

        public virtual void Connection_OnTelegramReceived(Core.Communication.TCPIP.Connection sender, string telegram)
        {
            if (InvokeRequired)
            {
                Core.Environment.Invoke(() => Connection_OnTelegramReceived(sender, telegram));
                return;
            }
            try
            {
                if (telegram.GetFieldValue(this, TelegramFields.Flow) == "A")
                {
                    int result = 0;
                    if (int.TryParse(telegram.GetFieldValue(this, TelegramFields.CycleNo), out result) && result == lastAckCycle) //This will ignore ack if its in the wrong sequence
                    {
                        if (LogAll)
                        {
                            LogTelegrams(string.Format("{0} {1}>{2}: {3}", DateTime.Now.ToString(), ReceiverID, SenderID, telegram), Color.Black);
                        }

                        waitingForAck = false;
                        ackResendCount = 0;
                        ackTimer.Stop();
                        SendBuffer();
                    }
                    return;
                }

                LogTelegrams(string.Format("{0} {1}>{2}: {3}", DateTime.Now.ToString(), ReceiverID, SenderID, telegram), Color.Black);

                //Check if this is an ack telegram
                TelegramTypes type = telegram.GetTelegramType(this);

                if (telegram.GetFieldValue(this, TelegramFields.Flow) == "R")
                {
                    string ackTelegram = string.Format("/,A,{0},{1},{2},{3},{4},{5},{6},0030,##",
                        telegram.GetFieldValue(this, TelegramFields.Type),
                        VFCIdentifier,
                        PLCIdentifier,
                        telegram.GetFieldValue(this, TelegramFields.CycleNo),
                        telegram.GetFieldValue(this, TelegramFields.Code),
                        telegram.GetFieldValue(this, TelegramFields.BlocksCount),
                        telegram.GetFieldValue(this, TelegramFields.BlocksType));

                    SendAckOnly(ackTelegram);
                }

                if (OnOverrideTelegramReceived == null || !OnOverrideTelegramReceived(this, telegram))
                {
                    HandleTelegrams(telegram, telegram.GetTelegramType(this));
                }
            }
            catch (Exception se)
            {
                Experior.Core.Environment.Log.Write("Exception recieving telegram: ");
                Experior.Core.Environment.Log.Write(se);
            }
        }

        public void UpDateLoadParameters(string telegram, Case_Load load, int blockPosition = 0)
        {
            int dropIndex = 0;
            int.TryParse(telegram.GetFieldValue(this, TelegramFields.DropIndex, blockPosition), out dropIndex);
            DCICaseData caseData = null;

            //Check that the load has the correct case data type
            if (!(load.Case_Data is DCICaseData))
            {
                load.Case_Data = new DCICaseData();
            }
            
            caseData = load.Case_Data as DCICaseData;
            load.Identification = telegram.GetFieldValue(this, TelegramFields.TUIdent, blockPosition);
            caseData.TUIdent = telegram.GetFieldValue(this, TelegramFields.TUIdent, blockPosition);
            caseData.TUType = telegram.GetFieldValue(this, TelegramFields.TUType, blockPosition);
            caseData.Source = telegram.GetFieldValue(this, TelegramFields.Source, blockPosition);
            caseData.Current = telegram.GetFieldValue(this, TelegramFields.Current, blockPosition);
            caseData.Destination = telegram.GetFieldValue(this, TelegramFields.Destination, blockPosition);
            caseData.EventCode = telegram.GetFieldValue(this, TelegramFields.EventCode, blockPosition);

            caseData.DropIndex = dropIndex;
            caseData.ShuttleDynamics = telegram.GetFieldValue(this, TelegramFields.ShuttleDynamics, blockPosition);
            caseData.LiftDynamics = telegram.GetFieldValue(this, TelegramFields.LiftDynamics, blockPosition);
            caseData.SourceShuttleExtension = telegram.GetFieldValue(this, TelegramFields.SourceShuttleExtension, blockPosition);
            caseData.DestinationShuttleExtension = telegram.GetFieldValue(this, TelegramFields.DestinationShuttleExtension, blockPosition);
            caseData.CaseConveyorDynamics = telegram.GetFieldValue(this, TelegramFields.CaseConveyorDynamics, blockPosition);

            float weight;
            float.TryParse(telegram.GetFieldValue(this, TelegramFields.TUWeight), out weight);
            load.Weight = weight;
        }

        /// <summary>
        /// Create transport telegrams only from load, do not use for any other type of telegram 
        /// </summary>
        /// <param name="telegramType"></param>
        /// <param name="load"></param>
        /// <returns></returns>
        public string CreateTelegramFromLoad(TelegramTypes telegramType, Case_Load load)
        {
            string telegram = Template.CreateTelegram(this, telegramType);
            DCICaseData caseData = load.Case_Data as DCICaseData;

            //Populate the correct field values
            telegram = telegram.SetFieldValue(this, TelegramFields.Source, caseData.Source);
            telegram = telegram.SetFieldValue(this, TelegramFields.Current, caseData.Current);
            telegram = telegram.SetFieldValue(this, TelegramFields.Destination, caseData.Destination);
            telegram = telegram.SetFieldValue(this, TelegramFields.TUIdent, load.Identification);
            telegram = telegram.SetFieldValue(this, TelegramFields.TUType, caseData.TUType);
            telegram = telegram.SetFieldValue(this, TelegramFields.TULength, (caseData.Length * 1000).ToString("0000"));
            telegram = telegram.SetFieldValue(this, TelegramFields.TUWidth, (caseData.Width * 1000).ToString("0000"));
            telegram = telegram.SetFieldValue(this, TelegramFields.TUHeight, (caseData.Height * 1000).ToString("0000"));
            telegram = telegram.SetFieldValue(this, TelegramFields.TUWeight, (caseData.Weight * 1000).ToString("000000"));
            telegram = telegram.SetFieldValue(this, TelegramFields.EventCode, caseData.EventCode);
            telegram = telegram.SetFieldValue(this, TelegramFields.DropIndex, caseData.DropIndex.ToString());
            telegram = telegram.SetFieldValue(this, TelegramFields.ShuttleDynamics, caseData.ShuttleDynamics);
            telegram = telegram.SetFieldValue(this, TelegramFields.LiftDynamics, caseData.LiftDynamics);
            telegram = telegram.SetFieldValue(this, TelegramFields.SourceShuttleExtension, caseData.SourceShuttleExtension);
            telegram = telegram.SetFieldValue(this, TelegramFields.DestinationShuttleExtension, caseData.DestinationShuttleExtension);

            if (DCIVersion == DCIVersions._1_60)
            {
                telegram = telegram.SetFieldValue(this, TelegramFields.CaseConveyorDynamics, caseData.CaseConveyorDynamics);
            }

            return telegram;
        }

        public static string GetBinLocField(string binLoc, BinLocFields field)
        {
            if (binLoc.Length == 14 || binLoc.Substring(0, 2) == "MS")
            {
                MatchCollection matchCollectiontest = binLocRegEx.Matches(binLoc);
                if (matchCollectiontest.Count > 0)
                {
                    return matchCollectiontest[0].Groups[field.ToString()].Value;
                }
            }
            return "";
        }

        public static string GetRackLocFields(string rackLoc, PSDSRackLocFields field)
        {
            if (rackLoc.Length == 14 && rackLoc.Substring(6, 1) == "L")
            {
                MatchCollection mc = racklocRegEx.Matches(rackLoc);
                if (mc.Count > 0)
                {
                    return mc[0].Groups[field.ToString()].Value;
                }
            }
            return "";
        }

        public static string GetPSDSLocFields(string rackLoc, PSDSRackLocFields field)
        {
            if (rackLoc.Length == 14 && rackLoc.Substring(6, 1) == "C")
            {
                MatchCollection mc = psDSlocRegEx.Matches(rackLoc);
                if (mc.Count > 0)
                {
                    return mc[0].Groups[field.ToString()].Value;
                }
            }
            return "";
        }

        public static string GetLocFields(string loc, PSDSRackLocFields field)
        {
            MatchCollection mc = psDSlocRegEx.Matches(loc);
            if (mc.Count > 0 && mc[0].Success)
            {
                if (mc.Count > 0)
                {
                    return mc[0].Groups[field.ToString()].Value;
                }
            }
            else
            {
                mc = racklocRegEx.Matches(loc);
                if (mc.Count > 0 && mc[0].Success)
                {
                    if (mc.Count > 0)
                    {
                        return mc[0].Groups[field.ToString()].Value;
                    }
                }
            }
            return "";
        }

        /// <summary>
        /// The actual controller will handle the telegrams, this method is called when a telegram has been received
        /// </summary>
        public abstract void HandleTelegrams(string telegram, TelegramTypes type);

        private DCIPLCStates _PLC_State = DCIPLCStates.Disconnected;
        [Browsable(false)]
        public DCIPLCStates PLC_State
        {
            get { return _PLC_State; }
            set
            {
                _PLC_State = value;
                if (OnPLCStateChange != null)
                {
                    OnPLCStateChange(this, new DCIPLCStateChangeEventArgs(value));
                }
            }
        }

        #region IDCITelegrams
        private TelegramTemplate _Template = new TelegramTemplate();
        [Browsable(false)]
        public TelegramTemplate Template
        {
            get { return _Template; }
            set { _Template = value; }
        }

        [Browsable(false)]
        public string PLCIdentifier
        {
            get { return baseControllerInfo.ReceiverID; }
        }

        [Browsable(false)]
        public string VFCIdentifier
        {
            get { return baseControllerInfo.SenderID; }
        }

        [Category("DCI")]
        [DisplayName("Sender ID")]
        [Description("Sender Identification used in the DCI Telegrams")]
        [PropertyOrder(1)]
        public string SenderID
        {
            get { return baseControllerInfo.SenderID; }
            set
            {
                if (value.Length == 4)
                {
                    baseControllerInfo.SenderID = value;
                }
            }
        }

        [Category("DCI")]
        [DisplayName("Receiver ID")]
        [Description("Receiver Identification used in the DCI Telegrams")]
        [PropertyOrder(1)]
        public string ReceiverID
        {
            get { return baseControllerInfo.ReceiverID; }
            set
            {
                if (value.Length == 4)
                {
                    baseControllerInfo.ReceiverID = value;
                }
            }
        }

        [Category("DCI")]
        [DisplayName("Log All Telegrams")]
        [Description("Log all telegrams including LIVE and ACK's")]
        [PropertyOrder(3)]
        [Experior.Core.Properties.AlwaysEditable]
        public bool LogAll
        {
            get { return baseControllerInfo.LogAll; }
            set { baseControllerInfo.LogAll = value; }
        }

        [Category("DCI")]
        [DisplayName("DCI Version")]
        [PropertyOrder(4)]
        [Description("Which version of DCI is being used 1.54 or 1.6 - This changes the number of fields in the material flow telegrams")]
        public DCIVersions DCIVersion
        {
            get
            {
                return baseControllerInfo.DCIVersion;
            }
            set
            {
                baseControllerInfo.DCIVersion = value;
            }
        }

        [Category("DCI")]
        [DisplayName("Specific Locations")]
        [Description("Should the DMS report specific location names instead of logical names e.g. PS11 and PS12 instead of PS10")]
        public bool SpecificNames
        {
            get
            {
                return baseControllerInfo.SpecificLocations;
            }
            set
            {
                baseControllerInfo.SpecificLocations = value;
            }
        }

        [Category("Project")]
        [DisplayName("Default Load Color")]
        [Description("Loads in rack are created with this color")]
        [PropertyOrder(3)]
        public Color DefaultLoadColor
        {
            get { return Color.FromArgb(baseControllerInfo.DefaultLoadColor); }
            set
            {
                
                baseControllerInfo.DefaultLoadColor = value.ToArgb();
            }
        }

        private readonly Dictionary<TelegramTypes, int> telegramLengthCache = new Dictionary<TelegramTypes, int>();

        public int GetTelegramLength(TelegramTypes telegramType) 
        {
            if (telegramLengthCache.ContainsKey(telegramType))
                return telegramLengthCache[telegramType];

            var type = Template.GetTelegramName(telegramType);

            var experiorMvt = ControllerConnection.Templates.FirstOrDefault(t => t.Exists(f => f.Identifier && f.Identification == type));

            if (experiorMvt == null)
                return 0;

            var telegramLength = experiorMvt.Sum(field => field.Length);

            telegramLengthCache[telegramType] = telegramLength;

            return telegramLength;
        }
        #endregion

        #region Create Loads and Data

        /// <summary>
        /// Creates the ATC Case Data that the load will hold from the telegram
        /// </summary>
        public DCICaseData CreateDCICaseData(string telegram, int blockPosition = 0)
        {
            DCICaseData caseData = new DCICaseData();
            float length, width, height, weight;
            int dropIndex = 0;

            float.TryParse(telegram.GetFieldValue(this, TelegramFields.TULength, blockPosition), out length);
            float.TryParse(telegram.GetFieldValue(this, TelegramFields.TUWidth, blockPosition), out width);
            float.TryParse(telegram.GetFieldValue(this, TelegramFields.TUHeight, blockPosition), out height);
            float.TryParse(telegram.GetFieldValue(this, TelegramFields.TUWeight, blockPosition), out weight);
            int.TryParse(telegram.GetFieldValue(this, TelegramFields.DropIndex, blockPosition), out dropIndex);

            caseData.Length = length / 1000;
            caseData.Width = width / 1000;
            caseData.Height = height / 1000;
            caseData.Weight = weight;
            caseData.colour = DefaultLoadColor;
            caseData.DropIndex = dropIndex;

            caseData.TUIdent = telegram.GetFieldValue(this, TelegramFields.TUIdent, blockPosition);
            caseData.TUType = telegram.GetFieldValue(this, TelegramFields.TUType, blockPosition);
            caseData.Source = telegram.GetFieldValue(this, TelegramFields.Source, blockPosition);
            caseData.Current = telegram.GetFieldValue(this, TelegramFields.Current, blockPosition);
            caseData.Destination = telegram.GetFieldValue(this, TelegramFields.Destination, blockPosition);
            caseData.EventCode = telegram.GetFieldValue(this, TelegramFields.EventCode, blockPosition);
            caseData.ShuttleDynamics = telegram.GetFieldValue(this, TelegramFields.ShuttleDynamics, blockPosition);
            caseData.LiftDynamics = telegram.GetFieldValue(this, TelegramFields.LiftDynamics, blockPosition);
            caseData.SourceShuttleExtension = telegram.GetFieldValue(this, TelegramFields.SourceShuttleExtension, blockPosition);
            caseData.DestinationShuttleExtension = telegram.GetFieldValue(this, TelegramFields.DestinationShuttleExtension, blockPosition);
            caseData.CaseConveyorDynamics = telegram.GetFieldValue(this, TelegramFields.CaseConveyorDynamics, blockPosition);

            return caseData;
        }

        public virtual Case_Load CreateCaseLoad(TelegramTypes Type, string Telegram, int blockPosition = 0)
        {
            Case_Load newLoad = null;

            string length = Telegram.GetFieldValue(this, TelegramFields.TULength, blockPosition);
            string width = Telegram.GetFieldValue(this, TelegramFields.TUWidth);
            string height = Telegram.GetFieldValue(this, TelegramFields.TUHeight);
            string weight = Telegram.GetFieldValue(this, TelegramFields.TUWeight);

            //IEmulationController emulation = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IEmulationController) as IEmulationController;
            newLoad.Case_Data = CreateDCICaseData(Telegram, blockPosition);
            return newLoad;
        }

        public virtual Case_Load CreateCaseLoad(BaseCaseData caseData)
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
            DCICaseData caseDataDCi = caseData as DCICaseData;

            if (caseDataDCi == null)
            {
                Log.Write("ERROR: Bad cast to DCICaseData in CreateCaseLoad", Color.Red);
                return null;
            }

            load.Weight = caseData.Weight;
            load.Identification = caseDataDCi.TUIdent;
            load.SSCCBarcode = caseDataDCi.TUIdent;
            load.Case_Data = caseData;
            return load;
        }

        public Experior.Dematic.Base.EuroPallet CreateEuroPallet(BasePalletData palletLoad)
        {
            throw new NotImplementedException();
        }

        #endregion

    }

    [Serializable]
    [TypeConverter(typeof(BaseDCIControllerInfo))]
    public class BaseDCIControllerInfo : BaseControllerInfo
    {
        public int connectionID;
        public string AdditionalFields;
        public LoadTypes loadType = LoadTypes.Case;
        //public bool LogHeartBeat;
        //public string meansOfTransport;
        public string SenderID;
        public string ReceiverID;
        public bool LogAll;
        public DCIVersions DCIVersion = DCIVersions._1_60;
        public bool SpecificLocations = false;
        public int DefaultLoadColor = System.Drawing.Color.Blue.ToArgb();
    }

    public class DCIPLCStateChangeEventArgs : EventArgs
    {
        public readonly DCIPLCStates _ATCPLCState;
        public DCIPLCStateChangeEventArgs(DCIPLCStates ATCPLCState)
        {
            _ATCPLCState = ATCPLCState;
        }
    }

    public enum DCIPLCStates
    {
        Disconnected,
        Connected,
        Manual,
    }

    public enum PSDSRackLocFields
    {
        Aisle,
        Side,
        Level,
        ConvType,
        LiftNum,
        ConvPos
    }

    public enum BinLocFields
    {
        Aisle,
        Side,
        XLoc,
        YLoc,
        Depth,
        RasterType,
        RasterPos
    }

    public enum DCIVersions
    {
        _1_54,
        _1_60
    }

    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;

        static Common()
        {
            Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());
        }
    }
}

