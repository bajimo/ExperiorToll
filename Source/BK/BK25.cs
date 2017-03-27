using System;
using System.Collections.Generic;
using System.Text;
using Xcelgo.Communication;
using System.Drawing;
using System.ComponentModel;
using Experior.Core.Properties;
using Experior.Core.Communication;

namespace Experior.Plugin
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [TypeConverter(typeof(Experior.Core.Properties.ObjectConverter))]
    public class BK25 : Experior.Core.Communication.TCPIP.RCF1006
    {
        public BK25(BKInfo info):base(info)
        {
        }

        protected override void Create()
        {
            try
            {

                if (Server) //Server
                    Socket = new Xcelgo.Communication.Protocol.RFC1006BK25(Port, false, ((Core.Communication.TCPIP.RFC1006CommunicationInfo)info).rfc1006);
                else
                    Socket = new Xcelgo.Communication.Protocol.RFC1006BK25(Port, Ip, false, ((Core.Communication.TCPIP.RFC1006CommunicationInfo)info).rfc1006);
            }
            catch (Exception se)
            {
                Log.Write(se, 130065);
            }
        }
        //Overrides
        public override Image Image
        {
            get
            {
                return Experior.Plugin.Properties.Resources.BK25;
            }
        }


        public override string Protocol
        {
            get { return "BK25"; }
        }
    }
}
