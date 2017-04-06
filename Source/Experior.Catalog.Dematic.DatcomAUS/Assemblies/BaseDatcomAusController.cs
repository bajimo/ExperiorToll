using System;
using System.ComponentModel;
using System.Drawing;
using Dematic.DATCOMAUS;
using Experior.Core.Assemblies;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Microsoft.DirectX;
using Environment = Experior.Core.Environment;

namespace Experior.Catalog.Dematic.DatcomAUS.Assemblies
{
    public class BaseDatcomAusController : Assembly //, IDATCOMAUSTelegrams
    {
        public event EventHandler OnControllerDeletedEvent;
        public event EventHandler OnControllerRenamedEvent;

        private BaseDatcomAusControllerInfo baseControllerInfo;
        public Text3D DisplayText;
        private Cube plcCube, door1, door2, plinth;
        public bool plcConnected;
        private static string telegramTail = ",,13,10";
        //public static event EventHandler<PLCStatusChangeEventArgs> OnPLCStatusChanged;//Static Routing Script Events

        public delegate bool TelegramRecievedHandle(BaseDatcomAusController sender, string type, string[] telegramFields);
        /// <summary>
        /// Handle project specific telegrams. If false is returned then the plc will handle it. If true is returned the plc expects the user to handle the telegram.
        /// </summary>
        public TelegramRecievedHandle HandleTelegram;

        public Experior.Core.Communication.TCPIP.Connection RecieveConnection, SendConnection;

        protected BaseDatcomAusController(BaseDatcomAusControllerInfo info)
            : base(info)
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


            if (info.receiverID != 0)
            {
                ReceiverID = info.receiverID;
            }

            if (info.senderID != 0)
            {
                SenderID = info.senderID;
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

        //BaseDatcomAusController should use reflection
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
            get { return Common.Icons.Get("Controller2"); }
        }

        public void SendTelegram(string tlgType, string body)
        {
            SendTelegram(tlgType, body, true);
        }


        public void SendTelegram(string tlgType, string body, bool LogMessage)
        {
            if (SendConnection != null && plcConnected)
            {
                string telegramHeader = "/,," + tlgType + "," + SystemIdentifier + ",01,,";
                string telegram = telegramHeader + body + telegramTail;
                this.SendConnection.Send(telegram);
                if (LogMessage)
                    this.LogTelegrams(DateTime.Now.ToString() + " MFH<PLC: " + SenderID.ToString() + " " + telegram, Color.Black);
            }
        }

        public void LogTelegrams(string message, Color colour)
        {
            //if (TlgSuppression == SupressBK10Telegrams.Never)
            //{
            Experior.Core.Environment.Log.Write(message, colour);
            //}
        }

        [Category("Configuration")]
        [DisplayName("Receiver ID")]
        [PropertyOrder(4)]
        [Description("Communicaion must be defined before this value can be set. Receiver ID must be the same as Communication id")]
        public int ReceiverID
        {
            get
            {
                return baseControllerInfo.receiverID;
            }
            set
            {
                Experior.Core.Communication.Connection connectionTemp = Experior.Core.Communication.Connection.Get(value);

                if (connectionTemp is Core.Communication.TCPIP.Connection && connectionTemp != null & value != 0)
                {
                    RecieveConnection = (Core.Communication.TCPIP.Connection)connectionTemp;
                    baseControllerInfo.receiverID = value;
                    Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " is linked to communication ID " + value.ToString());

                    if (baseControllerInfo.receiverID != 0)
                    {
                        RecieveConnection.OnTelegramReceived += Connection_OnTelegramReceived;
                        RecieveConnection.OnConnected += Connection_OnConnected;
                        RecieveConnection.OnDisconnected += Connection_OnDisconnected;
                    }
                }
                else
                {
                    Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + "Communication id must be equal to Receiver ID and not 0 (zero). Set communication id to a value first", Color.Red);
                }
            }
        }

        [Category("Configuration")]
        [DisplayName("Sender ID")]
        [PropertyOrder(5)]
        [Description("Communicaion must be defined before this value can be set. Sender ID must be the same as Communication id")]
        public int SenderID
        {
            get
            {
                return baseControllerInfo.senderID;
            }
            set
            {
                Experior.Core.Communication.Connection connectionTemp = Experior.Core.Communication.Connection.Get(value);

                if (connectionTemp is Core.Communication.TCPIP.Connection && connectionTemp != null & value != 0)
                {
                    SendConnection = (Core.Communication.TCPIP.Connection)connectionTemp;
                    baseControllerInfo.senderID = value;
                    Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " is linked to communication ID " + value.ToString());

                    if (baseControllerInfo.senderID != 0)
                    {
                        SendConnection.OnTelegramReceived += Connection_OnTelegramReceived;
                        SendConnection.OnConnected += Connection_OnConnected;
                        SendConnection.OnDisconnected += SendConnection_OnDisconnected;
                    }
                }
                else
                {
                    Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + "Communication id must be equal to Sender ID and not 0 (zero). Set communication id to a value first", Color.Red);
                }
            }
        }

        public event EventHandler<PLCStateChangeEventArgs> OnPLCStateChange;

        void SendConnection_OnDisconnected(Core.Communication.Connection connection)
        {
            DisplayText.Color = Color.Red;  // PLC object text
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection dropped for ID " + SendConnection.Id + " on IP " + SendConnection.Ip.ToString() + " and port " + SendConnection.Port.ToString(), Color.Red);
            SendConnection.AutoConnect = true;
            plcConnected = false;

            string obj = this.GetType().Name;

            if (this.GetType().Name == "MHEController_Multishuttle")
            {
                if (OnPLCStateChange != null)
                {
                    OnPLCStateChange(this, new PLCStateChangeEventArgs(null, MultiShuttlePLC_State.Unknown_00));
                }
            }
            else if (this.GetType().Name == "CasePLC_Datcom") //TODO change the name of this assembly
            {
                if (OnPLCStateChange != null)
                {
                    OnPLCStateChange(this, new PLCStateChangeEventArgs(CasePLC_State.NotReady, null));
                }
                //PLC_State = CasePLC_State.NotReady;
            }
            else
            {
                Log.Write("ERROR in SendConnection_OnDisconnected, cant find controller type", Color.Red);
            }

        }


        void Connection_OnDisconnected(Core.Communication.Connection connection)
        {
            DisplayText.Color = Color.Red;  // PLC object text
            Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection dropped for ID " + RecieveConnection.Id + " on IP " + RecieveConnection.Ip.ToString() + " and port " + RecieveConnection.Port.ToString(), Color.Red);
            RecieveConnection.AutoConnect = true;
            plcConnected = false;
            object obj = this.GetType();

            // PLC_State = CasePLC_State.NotReady;
        }

        void Connection_OnConnected(Core.Communication.Connection connection)
        {
            if (RecieveConnection.State == Experior.Core.Communication.State.Connected && SendConnection.State == Core.Communication.State.Connected)
            {
                DisplayText.Color = Color.LightGreen;  // PLC object text
                Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " " + this.Name + " connection established for ID " + RecieveConnection.Id.ToString() + " on IP " + RecieveConnection.Ip.ToString() + " and port " + RecieveConnection.Port.ToString(), Color.DarkGreen);
                plcConnected = true;
            }
            else
            {
                plcConnected = false;
            }
        }

        public void Connection_OnTelegramReceived(Core.Communication.TCPIP.Connection sender, string telegram)
        {
            if (sender == SendConnection && (SendConnection != RecieveConnection))
            {
                return; //Only ack telegrams should be received on this connection. For now just ignore...
            }

            if (InvokeRequired)
            {
                Core.Environment.Invoke(() => Connection_OnTelegramReceived(sender, telegram));
                return;
            }
            try
            {
                string[] telegramFields = telegram.Split(sender.MVTSeparator.ToCharArray());
                string type = telegramFields[2];
                string telegramsender = telegramFields[3];
                string telegramreciever = telegramFields[4];
                ushort number_of_blocks = ushort.Parse(telegramFields[5]);
                bool heartbeat = false;

                if (type == "13")
                {
                    if (telegramFields[6] == "07")
                    {
                        // type 13 Heartbeat status 07
                        heartbeat = true;
                    }
                }

                if (!heartbeat)
                    Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " MFH>PLC: " + sender.Id.ToString() + " " + telegram);

                if (LogHeartBeat && heartbeat)
                    Experior.Core.Environment.Log.Write(DateTime.Now.ToString() + " MFH>PLC: " + sender.Id.ToString() + " " + telegram);

                if (HandleTelegram != null)
                {
                    //If user method returns true then it is handled by the user.
                    if (HandleTelegram(this, type, telegramFields))
                        return;
                }

                HandleTelegrams(telegramFields, type);

            }
            catch (Exception se)
            {
                Experior.Core.Environment.Log.Write("Exception recieving telegram: ");
                Experior.Core.Environment.Log.Write(se);
            }
        }

        /// <summary>
        /// The actual controller will handle the telegrams, this method is called when a telegram has been received
        /// </summary>
        /// <param name="telegramFields"></param>
        /// <param name="type"></param>
        /// <param name="number_of_blocks"></param>
        public virtual void HandleTelegrams(string[] telegramFields, string type) { }

        //private CasePLC_State _PLC_State = CasePLC_State.Unknown;
        //[Browsable(false)]
        //public CasePLC_State PLC_State
        //{
        //    get { return _PLC_State; }
        //    set
        //    {
        //        _PLC_State = value;
        //        if (OnPLCStatusChanged != null)
        //            OnPLCStatusChanged(this, new PLCStatusChangeEventArgs(value));
        //    }
        //}

        [Category("Configuration")]
        [DisplayName("System Identifier")]
        [PropertyOrder(6)]
        [Description("The system identifier is part of the header, and indicate where the message originated and is destined.")]
        public string SystemIdentifier
        {
            get { return baseControllerInfo.SystemIdentifier; }
            set { baseControllerInfo.SystemIdentifier = value; }
        }

        [Category("Configuration")]
        [DisplayName("Log Heartbeats")]
        [Description("Log received and sent heartbeat messages")]
        [PropertyOrder(11)]
        public bool LogHeartBeat
        {
            get { return baseControllerInfo.LogHeartBeat; }
            set { baseControllerInfo.LogHeartBeat = value; }
        }
    }

    [Serializable]
    [TypeConverter(typeof(BaseDatcomAusControllerInfo))]
    public class BaseDatcomAusControllerInfo : AssemblyInfo
    {
        public int receiverID;
        public bool LogHeartBeat;
        public string SystemIdentifier;
        public int senderID;
    }

    public class PLCStateChangeEventArgs : EventArgs
    {
        public readonly CasePLC_State? _CaseState;
        public readonly MultiShuttlePLC_State? _MSState;

        public PLCStateChangeEventArgs(CasePLC_State? CaseState, MultiShuttlePLC_State? MSState)
        {
            _CaseState = CaseState;
            _MSState = MSState;
        }
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