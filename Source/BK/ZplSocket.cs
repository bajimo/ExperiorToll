using System;
using System.ComponentModel;
using Experior.Core.Communication.TCPIP;
using ConnectionInfo = Experior.Core.Communication.TCPIP.ConnectionInfo;

namespace Experior.Plugin
{
    public sealed class ZplSocket : SocketConnection
    {
        //private ZplAsyncServer listener;

        public ZplSocket(ConnectionInfo info)
            : base(info)
        {
            Create();
        }

        public override string Protocol
        {
            get { return "ZPL Script"; }
        }

        protected override void Create()
        {
            try
            {
                Socket = new ZplScript(((ConnectionInfo)info).port);
                //listener = new ZplAsyncServer();
            }
            catch (Exception se)
            {
                Log.Write(se, 135768);
            }
        }

        [Browsable(false)]
        public override string Mode { get; set; }
    }
}