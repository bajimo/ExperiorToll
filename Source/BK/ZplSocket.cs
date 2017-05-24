using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Experior.Core.Communication;
using Experior.Core.Communication.TCPIP;
using Xcelgo.Communication;
using ConnectionInfo = Experior.Core.Communication.TCPIP.ConnectionInfo;

namespace Experior.Plugin
{
    public sealed class ZplSocket : SocketConnection
    {
        //private ZplAsyncServer listener;
        //private State state = State.Idle;
        //private readonly ConnectionInfo settings;
        //public event EventHandler<string> ZplScriptReceived;

        public ZplSocket(ConnectionInfo info)
            : base(info)
        {
            //settings = info;
            Create();
        }

        //public override void EstablishConnection()
        //{
        //    if (state == State.Listening)
        //        return;

        //    state = State.Listening;

        //    StateChanged(States.Listening);

        //    var serverThread = new System.Threading.Thread(() => listener.StartListening(settings.port));
        //    serverThread.IsBackground = true;
        //    serverThread.Start();
        //}

        public override string Protocol
        {
            get { return "ZPL Script"; }
        }

        protected override void Create()
        {
            try
            {
                //if (listener != null)
                //    return;

                Socket = new ZplScript(((ConnectionInfo)info).port);
                //listener = new ZplAsyncServer();
                //listener.TelegramReceived += Listener_TelegramReceived;
            }
            catch (Exception se)
            {
                Log.Write(se, 135768);
            }
        }

        //public override void Disconnect()
        //{
        //    try
        //    {
        //        listener.Dispose();
        //    }
        //    catch (Exception e)
        //    {
        //        Log.Write(e, 3464363);
        //    }

        //    state = State.Idle;
        //    StateChanged(States.Idle);
        //}

        //private void Listener_TelegramReceived(object sender, string message)
        //{
        //    OnZplScriptReceived(message);
        //}

        //public override State State
        //{
        //    get
        //    {
        //        return state;
        //    }
        //}

        [Browsable(false)]
        public override string Mode { get; set; }

        [Browsable(false)]
        public override string Ip { get; set; }

        //public override int Port
        //{
        //    get
        //    {
        //        return settings.port;
        //    }
        //    set
        //    {
        //        if (state == State.Listening)
        //            Log.Write("Note: Server needs to be restarted before port number is updated!");
        //        settings.port = value;
        //    }
        //}

        [Browsable(false)]
        public override string MVT { get; set; }

        [Browsable(false)]
        public override uint AutoConnectDelay { get; set; }

        //private void OnZplScriptReceived(string e)
        //{
        //    ZplScriptReceived?.Invoke(this, e);
        //}
    }
}