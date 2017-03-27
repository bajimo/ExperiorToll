using System;
using System.Collections.Generic;
using System.Text;
using Xcelgo.Communication;
using System.Drawing;
using System.ComponentModel;
using Experior.Core.Properties;
using Experior.Core.Communication.TCPIP;

namespace Experior.Plugin
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [TypeConverter(typeof(Experior.Core.Properties.ObjectConverter))]
    class TCPIPSocket : SocketConnection
    {

        public TCPIPSocket(ConnectionInfo info) : base(info)
        {
            this.Create();
        }

        protected override void Create()
        {
            try
            {
                if (Server) //Server
                    this.Socket = (Xcelgo.Communication.TCP)new TCPDematic(Port, false);
                else
                    this.Socket = (Xcelgo.Communication.TCP)new TCPDematic(Port, Ip, false);

            }
            catch (Exception se)
            {
                Log.Write(se, 130075);
            }
        }

        public override string Protocol 
        {
            get { return "TCPIP Socket"; }
        }
    }
}
