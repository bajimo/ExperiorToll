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
    class ATCSocket : SocketConnection
    {

        public ATCSocket(ConnectionInfo info) : base(info)
        {
            this.Create();
        }

        protected override void Create()
        {
            try
            {
                if (Server) //Server
                    this.Socket = (Xcelgo.Communication.TCP)new ATCConnection(Port, false, Name);
                else
                    this.Socket = (Xcelgo.Communication.TCP)new ATCConnection(Port, Ip, false, Name);

            }
            catch (Exception se)
            {
                Log.Write(se, 130075);
            }
        }

        [Browsable(false)]
        public override string MVT
        {
            get
            {
                return base.MVT;
            }
            set
            {
                base.MVT = value;
            }
        }

        public override string Protocol 
        {
            get { return "ATC Socket"; }
        }
    }
}
