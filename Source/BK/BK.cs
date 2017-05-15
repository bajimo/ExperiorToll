using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Core;
using Experior.Core.Communication;

namespace Experior.Plugin
{
    public class BK : Experior.Core.Plugin
    {
        public BK()
            : base("BK")
        {
            Experior.Core.Environment.Communication.Protocol.Add("BK10", Experior.Plugin.Properties.Resources.BK10, new EventHandler(CreateBK10), typeof(BK10));
            Experior.Core.Environment.Communication.Protocol.Add("BK25", Experior.Plugin.Properties.Resources.BK25, new EventHandler(CreateBK25), typeof(BK25));
            Experior.Core.Environment.Communication.Protocol.Add("ATC", Experior.Plugin.Properties.Resources.TCP, new EventHandler(CreateATC), typeof(ATCSocket));
            Experior.Core.Environment.Communication.Protocol.Add("DCI", Experior.Plugin.Properties.Resources.TCP, new EventHandler(CreateDCI), typeof(DCISocket));
            Experior.Core.Environment.Communication.Protocol.Add("ZPL Script", Experior.Plugin.Properties.Resources.TCP, new EventHandler(CreateZPL), typeof(ZplSocket));
        }

        private void CreateBK25(object sender, EventArgs e)
        {
            try
            {
                BK25 com = new BK25(new BKInfo());
                Experior.Core.Communication.Connection.Items.Add(com);
            }
            catch (Exception se)
            {
                Log.Write(se, 120009);
            }
        }
        private void CreateBK10(object sender, EventArgs e)
        {
            try
            {
                BK10 com = new BK10(new BKInfo());
                Experior.Core.Communication.Connection.Items.Add(com);
            }
            catch (Exception se)
            {
                Log.Write(se, 120009);
            }
        }
        private void CreateATC(object sender, EventArgs e)
        {
            try
            {
                Experior.Core.Communication.TCPIP.ConnectionInfo info = new Core.Communication.TCPIP.ConnectionInfo();
                info.autoconnect = true;
                info.server = true;

                ATCSocket com = new ATCSocket(info);
                Experior.Core.Communication.Connection.Items.Add(com);
            }
            catch (Exception se)
            {
                Log.Write(se, 120009);
            }
        }
        private void CreateDCI(object sender, EventArgs e)
        {
            try
            {
                Experior.Core.Communication.TCPIP.ConnectionInfo info = new Core.Communication.TCPIP.ConnectionInfo();
                info.autoconnect = true;
                info.server = true;

                DCISocket com = new DCISocket(info);
                Experior.Core.Communication.Connection.Items.Add(com);
            }
            catch (Exception se)
            {
                Log.Write(se, 120009);
            }
        }

        private void CreateZPL(object sender, EventArgs e)
        {
            try
            {
                Experior.Core.Communication.TCPIP.ConnectionInfo info = new Core.Communication.TCPIP.ConnectionInfo();
                info.autoconnect = true;
                info.server = true;

                ZplSocket com = new ZplSocket(info);
                Experior.Core.Communication.Connection.Items.Add(com);
            }
            catch (Exception se)
            {
                Log.Write(se, 120209);
            }
        }

        public override System.Drawing.Image Logo
        {
            get
            {
                return Experior.Plugin.Properties.Resources.Icon;
            }
        }
    }
}