using System;
using System.Collections.Generic;
using System.Text;
using Xcelgo.Communication;
using System.Drawing;
using System.ComponentModel;
using Experior.Core.Properties;
using Experior.Core.Communication;
using System.Xml.Serialization;

namespace Experior.Plugin
{
    [XmlInclude(typeof(BKInfo))]
    [Serializable]
    public class BKInfo : Experior.Core.Communication.TCPIP.RFC1006CommunicationInfo
    {
        public BKInfo()
            : base()
        {

        }
    }
}