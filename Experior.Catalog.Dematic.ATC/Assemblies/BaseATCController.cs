using Dematic.ATC;
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
using System.Linq;
using System.Text.RegularExpressions;
using Environment = Experior.Core.Environment;

namespace Experior.Catalog.Dematic.ATC.Assemblies
{
    public abstract class BaseATCController : Assembly
    {
        public event EventHandler OnControllerDeletedEvent;
        public event EventHandler OnControllerRenamedEvent;

        private BaseATCControllerInfo baseControllerInfo;
        public Text3D DisplayText;
        private Cube plcCube, door1, door2, plinth;
        public bool plcConnected;
        public Color DefaultLoadColour = Color.LightPink;
        public List<string> ProjectFields = new List<string>();
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

        protected BaseATCController(BaseATCControllerInfo info) : base(info)
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
            AdditionalFields = info.AdditionalFields;

            if (info.connectionID != 0)
            {
                ConnectionID = info.connectionID;
            }
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

        public virtual void SendTelegram(string telegram, bool logMessage)
        {
            if (ControllerConnection != null)
            {
                if (plcConnected)
                {
                    this.ControllerConnection.Send(telegram);
                    if (logMessage)
                    {
                        LogTelegrams(string.Format("{0} ATC<{1}: {2}", DateTime.Now.ToString(), Name, telegram), Color.Black);
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
            Experior.Core.Environment.Log.Write(message, colour);
        }

        [Category("Project")]
        [DisplayName("Additional Fields")]
        [Description("Name of any additional project specific fields for the ATC messages, seperated by a ',' (comma)")]
        public string AdditionalFields
        {
            get { return baseControllerInfo.AdditionalFields; }
            set
            {
                if (value != null)
                {
                    baseControllerInfo.AdditionalFields = value;
                    ProjectFields.Clear();
                    foreach (string field in value.Split(','))
                    {
                        ProjectFields.Add(field);
                    }
                }
            }
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

        public event EventHandler<ATCPLCStateChangeEventArgs> OnPLCStateChange;

        protected virtual void Connection_OnDisconnected(Core.Communication.Connection connection)
        {
            DisplayText.Color = Color.Red;  // PLC object text
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection dropped for ID " + ControllerConnection.Id + " on IP " + ControllerConnection.Ip.ToString() + " and port " + ControllerConnection.Port.ToString(), Color.Red);
           // ControllerConnection.AutoConnect = true;
            plcConnected = false;
            PLC_State = ATCPLCStates.Disconnected;
        }

        protected virtual void Connection_OnConnected(Core.Communication.Connection connection)
        {
            DisplayText.Color = Color.LightGreen;  // PLC object text
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection established for ID " + ControllerConnection.Id.ToString() + " on IP " + ControllerConnection.Ip.ToString() + " and port " + ControllerConnection.Port.ToString(), Color.DarkGreen);
            plcConnected = true;
            PLC_State = ATCPLCStates.Disconnected;
        }

        public delegate void connection_OnTelegramReceivedEvent(Experior.Core.Communication.TCPIP.Connection sender, string telegram);
        public delegate bool OverrideTelegramReceived(BaseATCController controller, string[] telegramFields);
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
                this.LogTelegrams(string.Format("{0} ATC>{1}: {2}", DateTime.Now.ToString(), Name, telegram), Color.Black);
                string[] telegramFields = telegram.Split(',');

                if (OnOverrideTelegramReceived == null || !OnOverrideTelegramReceived(this, telegramFields))
                {
                    HandleTelegrams(telegramFields, telegramFields.GetTelegramType());
                }
            }
            catch (Exception se)
            {
                Experior.Core.Environment.Log.Write("Exception recieving telegram: ");
                Experior.Core.Environment.Log.Write(se);
            }
        }

        public void UpDateLoadParameters(string[] telegramFields, IATCLoadType load, string index = "")
        {
            load.TUType = telegramFields.GetFieldValue(TelegramFields.tuType, index);
            load.Source = telegramFields.GetFieldValue(TelegramFields.source, index);
            load.Destination = telegramFields.GetFieldValue(TelegramFields.destination, index);
            load.PresetStateCode = telegramFields.GetFieldValue(TelegramFields.presetStateCode, index);

            Color c = LoadColor(telegramFields.GetFieldValue(TelegramFields.color, index));
            if (c != DefaultLoadColour)
            {
                if (load is IATCCaseLoadType)
                {
                    load.Color = c;
                }
                else if (load is IATCPalletLoadType)
                {
                    ATCEuroPallet palletLoad = load as ATCEuroPallet;
                    palletLoad.LoadColor = c;
                }
            }

            float weight;
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.weight), out weight);
            load.Weight = weight;
        }

        public string CreateTelegramFromLoad(TelegramTypes telegramType, IATCLoadType load)
        {
            List<TelegramFields> fieldList = Telegrams.TelegramSignatures[telegramType];
            string telegram = string.Empty.InsertType(telegramType);

            foreach (TelegramFields tf in fieldList)
            {
                telegram = telegram.AppendField(tf, load.GetPropertyValueFromEnum(tf));
            }

            if (fieldList.Contains(TelegramFields.mts))
            {
                telegram = telegram.SetFieldValue(TelegramFields.mts, this.Name);
            }

            if (fieldList.Contains(TelegramFields.stateCode))
            {
                telegram = telegram.SetFieldValue(TelegramFields.stateCode, load.PresetStateCode);
            }

            //If the PLC has further additional telegram fields but they are not on the load yet they need to be added
            if (ProjectFields.Count > 0)
            {
                Dictionary<string, string> loadProjectFields = new Dictionary<string, string>();
                foreach (string field in ProjectFields)
                {
                    if (load.ProjectFields.Keys.Contains(field))
                    {
                        //Field already exists copy into new version
                        loadProjectFields.Add(field, load.ProjectFields[field]);
                    }
                    else
                    {
                        loadProjectFields.Add(field, "");
                    }
                }
                if (loadProjectFields.Count > 0)
                {
                    load.ProjectFields = loadProjectFields;
                }
            }

            foreach (string field in load.ProjectFields.Keys)
            {
                if (telegram.Contains(string.Format(",{0}='", field)))
                {
                    telegram = telegram.SetFieldValue(field, load.ProjectFields[field]);
                }
                else
                {
                    telegram = telegram.AppendField(field, load.ProjectFields[field]);
                }
            }
            return telegram + "#";
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
        public abstract void HandleTelegrams(string[] telegramFields, TelegramTypes type);

        private ATCPLCStates _PLC_State = ATCPLCStates.Disconnected;
        [Browsable(false)]
        public ATCPLCStates PLC_State
        {
            get { return _PLC_State; }
            set
            {
                _PLC_State = value;
                if (OnPLCStateChange != null)
                {
                    OnPLCStateChange(this, new ATCPLCStateChangeEventArgs(value));
                }
            }
        }

        //[Category("Configuration")]
        //[DisplayName("Means Of Transport")]
        //[PropertyOrder(6)]
        //[Description("The name of the PLC to identify the correct controller has received the message")]
        //public string MeansOfTransport
        //{
        //    get { return baseControllerInfo.meansOfTransport; }
        //    set { baseControllerInfo.meansOfTransport = value; }
        //}


        [Category("Configuration")]
        [DisplayName("Log Heartbeats")]
        [Description("Log received and sent heartbeat messages")]
        [PropertyOrder(11)]
        public bool LogHeartBeat
        {
            get { return baseControllerInfo.LogHeartBeat; }
            set { baseControllerInfo.LogHeartBeat = value; }
        }

        #region Create Loads and Data

        /// <summary>
        /// Creates the ATC Case Data that the load will hold from the telegram
        /// </summary>
        public ATCCaseData CreateATCCaseData(string[] telegramFields, string index = "")
        {
            ATCCaseData caseData = new ATCCaseData();
            float length, width, height, weight;

            float.TryParse(telegramFields.GetFieldValue(TelegramFields.length, index), out length);
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.width, index), out width);
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.height, index), out height);
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.weight, index), out weight);

            caseData.Length = length / 1000;
            caseData.Width = width / 1000;
            caseData.Height = height / 1000;
            caseData.Weight = weight;
            caseData.colour = LoadColor(telegramFields.GetFieldValue(TelegramFields.color, index));

            caseData.TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent, index);
            caseData.TUType = telegramFields.GetFieldValue(TelegramFields.tuType, index);
            caseData.mts = telegramFields.GetFieldValue(TelegramFields.mts);
            caseData.presetStateCode = telegramFields.GetFieldValue(TelegramFields.presetStateCode, index);
            caseData.source = telegramFields.GetFieldValue(TelegramFields.source, index);
            caseData.destination = telegramFields.GetFieldValue(TelegramFields.destination, index);

            return caseData;
        }

        /// <summary>
        /// Creates the ATC Pallet Data that the load will hold from the telegram
        /// </summary>
        public ATCPalletData CreateATCPalletData(string[] telegramFields, string index = "")
        {
            ATCPalletData palletData = new ATCPalletData();

            float length, width, height, weight;
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.length, index), out length);
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.width, index), out width);
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.height, index), out height);
            float.TryParse(telegramFields.GetFieldValue(TelegramFields.weight, index), out weight);

            palletData.Length = length / 1000;
            palletData.Width = width / 1000;
            palletData.Height = height / 1000;
            palletData.Weight = weight;
            palletData.colour = LoadColor(telegramFields.GetFieldValue(TelegramFields.color, index));

            palletData.TUIdent = telegramFields.GetFieldValue(TelegramFields.tuIdent, index);
            palletData.TUType = telegramFields.GetFieldValue(TelegramFields.tuType, index);
            palletData.mts = telegramFields.GetFieldValue(TelegramFields.mts);
            palletData.presetStateCode = telegramFields.GetFieldValue(TelegramFields.presetStateCode, index);
            palletData.source = telegramFields.GetFieldValue(TelegramFields.source, index);
            palletData.destination = telegramFields.GetFieldValue(TelegramFields.destination, index);

            return palletData;
        }

        public Case_Load CreateCaseLoad(BaseCaseData caseData)
        {
            //Need to either create a case or a tray here
            if (LoadType == LoadTypes.Case)
            {
                MeshInfo boxInfo = new MeshInfo()
                {
                    color = caseData.colour,
                    length = caseData.Length,
                    width = caseData.Width,
                    height = caseData.Height,
                    filename = Case_Load.GraphicsMesh
                };

                ATCCaseLoad load = new ATCCaseLoad(boxInfo);
                ATCCaseData ATCcData = caseData as ATCCaseData;

                if (ATCcData == null)
                {
                    Log.Write("ERROR: Bad cast to ATCCaseData in CreateCaseLoad", Color.Red);
                    return null;
                }

                load.Weight = caseData.Weight / 1000f;
                load.TUIdent = ATCcData.TUIdent;
                load.TUType = ATCcData.TUType;
                load.Source = ATCcData.source;
                load.Destination = ATCcData.destination;
                load.PresetStateCode = ATCcData.presetStateCode;
                //load.MTS = ATCcData.mts;

                return load;
            }
            else if (LoadType == LoadTypes.Tray)
            {
                return CreateTray(caseData);
            }
            else
            {
                return null;
            }
        }

        public ATCCaseLoad CreateCaseLoad(string mts, string tuIdent, string tuType, string source, string destination, string presetStateCode, string height, string width, string length, string weight, string color)
        {
            MeshInfo boxInfo = new MeshInfo();
            boxInfo.color = LoadColor(color);
            boxInfo.filename = Case_Load.GraphicsMesh;

            float Length, Width, Height, Weight;
            float.TryParse(length, out Length); Length = Length / 1000;
            float.TryParse(width, out Width); Width = Width / 1000;
            float.TryParse(height, out Height); Height = Height / 1000;
            float.TryParse(weight, out Weight); Weight = Weight / 1000;

            boxInfo.length = Length;
            boxInfo.width = Width;
            boxInfo.height = Height;

            ATCCaseLoad boxLoad = new ATCCaseLoad(boxInfo);
            //boxLoad.MTS = mts;
            boxLoad.TUIdent = tuIdent;
            boxLoad.TUType = tuType;
            boxLoad.Source = source;
            boxLoad.Destination = destination;
            boxLoad.PresetStateCode = presetStateCode;
            boxLoad.CaseWeight = Weight;

            //Add project fields to load
            Experior.Core.Loads.Load.Items.Add(boxLoad);

            if (ProjectFields.Count > 0 )
            {
                foreach (string field in ProjectFields)
                {
                    boxLoad.ProjectFields.Add(field, "");
                }
            }

            return boxLoad;
        }

        protected ATCCaseLoad CreateCaseLoad(string height, string width, string length, string weight, string color)
        {
            return CreateCaseLoad("", FeedLoad.GetSSCCBarcode(), "", "", "", "", height, width, length, weight, color);
        }

        /// <summary>
        /// Create the case load from a message from ATC
        /// </summary>
        public virtual ATCCaseLoad CreateCaseLoad(TelegramTypes Type, string[] Telegram)
        {
            ATCCaseLoad newLoad = null;

            string length = Telegram.GetFieldValue(TelegramFields.length);
            string width = Telegram.GetFieldValue(TelegramFields.width);
            string height = Telegram.GetFieldValue(TelegramFields.height);
            string weight = Telegram.GetFieldValue(TelegramFields.weight);
            string color = Telegram.GetFieldValue(TelegramFields.color);

            EmulationATC emulation = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is EmulationATC) as EmulationATC;

            if (emulation != null)
            {
                length = (length == null || length == "0") ? emulation.CaseLoadLength : length;
                width = (width == null || width == "0") ? emulation.CaseLoadWidth : width;
                height = (height == null || height == "0") ? emulation.CaseLoadHeight : height;
                weight = (weight == null || weight == "0") ? emulation.CaseLoadWeight : weight;
            }

            if (Type == TelegramTypes.StartTransportTelegram)
            {
                newLoad =  CreateCaseLoad(
                    Telegram.GetFieldValue(TelegramFields.mts),
                    Telegram.GetFieldValue(TelegramFields.tuIdent),
                    Telegram.GetFieldValue(TelegramFields.tuType),
                    Telegram.GetFieldValue(TelegramFields.source), //Source
                    Telegram.GetFieldValue(TelegramFields.destination),
                    Telegram.GetFieldValue(TelegramFields.presetStateCode),
                    height,
                    width,
                    length,
                    weight,
                    Telegram.GetFieldValue(TelegramFields.color));
            }
            else if (Type == TelegramTypes.CreateTuTelegram)
            {
                newLoad = CreateCaseLoad(
                    Telegram.GetFieldValue(TelegramFields.mts),
                    Telegram.GetFieldValue(TelegramFields.tuIdent),
                    Telegram.GetFieldValue(TelegramFields.tuType),
                    Telegram.GetFieldValue(TelegramFields.location), //Location
                    Telegram.GetFieldValue(TelegramFields.destination),
                    Telegram.GetFieldValue(TelegramFields.presetStateCode),
                    height,
                    width,
                    length,
                    weight,
                    Telegram.GetFieldValue(TelegramFields.color));
            }

            if (newLoad != null)
            {
                //Add project fields to load
                foreach (string field in ProjectFields)
                {
                    string fieldValue = Telegram.GetFieldValue(field);
                    if (fieldValue != null)
                    {
                        // Update if field already exists | Insert New
                        if (newLoad.ProjectFields.ContainsKey(field))
                        {
                            newLoad.ProjectFields[field] = fieldValue;
                        }
                        else
                        {
                            newLoad.ProjectFields.Add(field, fieldValue);
                        }
                    }
                }
                return newLoad;
            }
            return null;
        }

        public Case_Load CreateTray(BaseCaseData caseData)
        {
            ATCCaseData ATCcData = caseData as ATCCaseData;
            if (ATCcData == null)
            {
                Log.Write("ERROR: Bad cast to ATCCaseData in CreateCaseLoad", Color.Red);
                return null;
            }

            return CreateTray(
                ATCcData.mts,
                ATCcData.TUIdent,
                ATCcData.TUType,
                ATCcData.source,
                ATCcData.destination,
                ATCcData.presetStateCode,
                ATCcData.Height,
                ATCcData.Width,
                ATCcData.Length,
                ATCcData.Weight,
                ATCcData.colour.ToString(),
                TrayStatus.Loaded,
                ATCcData.TrayStacks
                );
        }
        
        protected ATCTray CreateTray(string mts, string tuIdent, string tuType, string source, string destination, string presetStateCode, float height, float width, float length, float weight, string color, TrayStatus status, uint trayStacks)
        {
            TrayInfo trayInfo = new TrayInfo();
            trayInfo.LoadColor = LoadColor(color);
            trayInfo.filename = Tray.Mesh;
            trayInfo.Status = status;
            trayInfo.TrayStacks = trayStacks;

            //LoadHeight includes the height of the tray (50mm)
            trayInfo.LoadHeight = height;
            trayInfo.LoadWidth = width;
            trayInfo.LoadLength = length;
            //TODO: Weight

            //Set the dimensions of a tray
            trayInfo.length = 0.65f;
            trayInfo.width = 0.45f;
            trayInfo.height = 0.058f; // Actual size is 0.063f but reduced so visible space can be added in stack (0.005f space)

            ATCTray trayLoad = new ATCTray(trayInfo);

            trayLoad.TUIdent = tuIdent;
            trayLoad.TUType = tuType;
            trayLoad.Source = source;
            trayLoad.Destination = destination;
            trayLoad.PresetStateCode = presetStateCode;
            trayLoad.Weight = weight;

            //Add project fields to load
            Load.Items.Add(trayLoad);

            if (ProjectFields.Count > 0)
            {
                foreach (string field in ProjectFields)
                {
                    trayLoad.ProjectFields.Add(field, "");
                }
            }

            return trayLoad;
        }

        protected ATCTray CreateTray(float height, float width, float length, float weight, string color, TrayStatus status, uint trayStacks)
        {
            return CreateTray("", FeedLoad.GetSSCCBarcode(), "", "", "", "", height, width, length, weight, color, status, trayStacks);
        }

        protected ATCEuroPallet CreateEuroPallet(string mts, string tuIdent, string tuType, string source, string destination, string presetStateCode, float height, float width, float length, float weight, Color color, PalletStatus status)
        {
            EuroPalletInfo palletInfo = new EuroPalletInfo();
            palletInfo.LoadColor = color;//LoadColor(color);
            palletInfo.Status = status;
            palletInfo.filename = Experior.Dematic.Base.EuroPallet.Mesh;
            palletInfo.color = Color.Peru;

            //LoadHeight includes the height of the pallet (145mm)
            palletInfo.LoadHeight = height;
            palletInfo.LoadWidth = width;
            palletInfo.LoadLength = length;
            //TODO: Weight

            //Set the dimensions of a EuroPallet (This is the standard size)
            palletInfo.length = 1.2f;
            palletInfo.width = 0.8f;
            palletInfo.height = 0.145f;

            ATCEuroPallet palletLoad = new ATCEuroPallet(palletInfo);

            palletLoad.TUIdent = tuIdent;
            palletLoad.TUType = tuType;
            palletLoad.Source = source;
            palletLoad.Destination = destination;
            palletLoad.PresetStateCode = presetStateCode;
            palletLoad.PalletWeight = weight;

            //Add project fields to load
            Load.Items.Add(palletLoad);

            if (ProjectFields.Count > 0)
            {
                foreach (string field in ProjectFields)
                {
                    palletLoad.ProjectFields.Add(field, "");
                }
            }

            return palletLoad;
        }

        protected ATCEuroPallet CreateEuroPallet(float height, float width, float length, float weight, string color, PalletStatus status)
        {
            return CreateEuroPallet("", FeedLoad.GetSSCCBarcode(), "", "", "", "OK", height, width, length, weight, LoadColor(color), status);
        }

        /// <summary>
        /// Create the EuroPallet load from a message from ATC
        /// </summary>
        public virtual ATCEuroPallet CreateEuroPallet(TelegramTypes Type, string[] Telegram)
        {
            ATCEuroPallet newLoad = null;

            float length;
            bool lengthIsFloat = float.TryParse(Telegram.GetFieldValue(TelegramFields.length), out length);
            float width;
            bool widthIsFloat = float.TryParse(Telegram.GetFieldValue(TelegramFields.width), out width);
            float height;
            bool heightIsFloat = float.TryParse(Telegram.GetFieldValue(TelegramFields.height), out height);
            float weight;
            bool weightIsFloat = float.TryParse(Telegram.GetFieldValue(TelegramFields.weight), out weight);
            string color = Telegram.GetFieldValue(TelegramFields.color);
            PalletStatus palletStatus = PalletStatus.Loaded;

            EmulationATC emulation = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is EmulationATC) as EmulationATC;

            if (emulation != null)
            {
                length = (!lengthIsFloat || length == 0f) ? emulation.PalletLoadLength : length /1000;
                width = (!widthIsFloat || width == 0f) ? emulation.PalletLoadWidth : width / 1000;
                height = (!heightIsFloat || height == 0f) ? emulation.PalletLoadHeight : height / 1000;
                weight = (!weightIsFloat || weight == 0f) ? emulation.PalletLoadWeight : weight;
            }

            if (Type == TelegramTypes.StartTransportTelegram)
            {
                newLoad = CreateEuroPallet(
                    Telegram.GetFieldValue(TelegramFields.mts),
                    Telegram.GetFieldValue(TelegramFields.tuIdent),
                    Telegram.GetFieldValue(TelegramFields.tuType),
                    Telegram.GetFieldValue(TelegramFields.source), //Source
                    Telegram.GetFieldValue(TelegramFields.destination),
                    Telegram.GetFieldValue(TelegramFields.presetStateCode),
                    height,
                    width,
                    length,
                    weight,
                    LoadColor(Telegram.GetFieldValue(TelegramFields.color)),
                    palletStatus);
            }
            else if (Type == TelegramTypes.CreateTuTelegram)
            {
                newLoad = CreateEuroPallet(
                    Telegram.GetFieldValue(TelegramFields.mts),
                    Telegram.GetFieldValue(TelegramFields.tuIdent),
                    Telegram.GetFieldValue(TelegramFields.tuType),
                    Telegram.GetFieldValue(TelegramFields.location), //Location
                    Telegram.GetFieldValue(TelegramFields.destination),
                    Telegram.GetFieldValue(TelegramFields.presetStateCode),
                    height,
                    width,
                    length,
                    weight,
                    LoadColor(Telegram.GetFieldValue(TelegramFields.color)),
                    palletStatus);
            }

            if (newLoad != null)
            {
                //Add project fields to load
                foreach (string field in ProjectFields)
                {
                    string fieldValue = Telegram.GetFieldValue(field);
                    if (fieldValue != null)
                    {
                        // Update if field already exists | Insert New
                        if (newLoad.ProjectFields.ContainsKey(field))
                        {
                            newLoad.ProjectFields[field] = fieldValue;
                        }
                        else
                        {
                            newLoad.ProjectFields.Add(field, fieldValue);
                        }
                    }
                }
                return newLoad;
            }
            return null;
        }

        public Experior.Dematic.Base.EuroPallet CreateEuroPallet(BasePalletData baseData)
        {
            ATCPalletData palletData = baseData as ATCPalletData;
            //return CreateEuroPallet(baseData.Length, baseData.Width, baseData.Length, baseData.Weight, baseData.colour.ToString(), baseData.Height > 0.145f ? PalletStatus.Loaded : PalletStatus.Empty);
            return CreateEuroPallet(this.Name, palletData.TUIdent, palletData.TUType, palletData.source, palletData.destination, "OK", palletData.Height, palletData.Width, palletData.Length, palletData.Weight, palletData.colour, palletData.Height > 0.145f ? PalletStatus.Loaded : PalletStatus.Empty);
        }

        #endregion

        #region Helper methods


        protected Color LoadColor(string color)
        {
            switch (color)
            {
                case "white": return Color.White;
                case "black": return Color.Black;
                case "green": return Color.Green;
                case "blue": return Color.Blue;
                case "red": return Color.Red;
                case "yellow": return Color.Yellow;
                case "brown": return Color.Brown;
                case "cyan": return Color.Cyan;
                case "orange": return Color.Orange;
                case "purple": return Color.Purple;
                case "dkgray": return Color.DarkGray;
                case "ltyellow": return Color.LightYellow;
                case "ltblue": return Color.LightBlue;
                case "ltgreen": return Color.LightGreen;
                case "ltgray": return Color.LightGray;
                case "magenta": return Color.Magenta;
            }
            return DefaultLoadColour;
        }
        #endregion
    }

    [Serializable]
    [TypeConverter(typeof(BaseATCControllerInfo))]
    public class BaseATCControllerInfo : BaseControllerInfo
    {
        public int connectionID;
        public string AdditionalFields;
        public LoadTypes loadType = LoadTypes.Case;
        //public bool LogHeartBeat;
        //public string meansOfTransport;
    }

    public class ATCPLCStateChangeEventArgs : EventArgs
    {
        public readonly ATCPLCStates _ATCPLCState;
        public ATCPLCStateChangeEventArgs(ATCPLCStates ATCPLCState)
        {
            _ATCPLCState = ATCPLCState;
        }
    }

    public enum ATCPLCStates
    {
        Disconnected,
        Connected,
        Manual,
    };

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

