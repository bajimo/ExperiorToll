using System;
using System.ComponentModel;
using Experior.Core.Communication.TCPIP;

namespace Experior.Plugin
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [TypeConverter(typeof(Experior.Core.Properties.ObjectConverter))]
    class DCISocket : SocketConnection
    {

        public DCISocket(ConnectionInfo info) : base(info)
        {
            Create();
        }

        protected override void Create()
        {
            try
            {
                if (Server) //Server
                    Socket = (Xcelgo.Communication.TCP)new DCIConnection(Port, false, Name);
                else
                    Socket = (Xcelgo.Communication.TCP)new DCIConnection(Port, Ip, false, Name);

            }
            catch (Exception se)
            {
                Log.Write(se, 130075);
            }
        }

        public override string Protocol 
        {
            get { return "DCI Socket"; }
        }
    }
}
