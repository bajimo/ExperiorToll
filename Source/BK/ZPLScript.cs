using System;
using System.Linq;
using System.Text;
using Experior.Core.Communication.TCPIP;
using TCP = Xcelgo.Communication.TCP;

namespace Experior.Plugin
{
    public class ZplScript : TCP
    {
        private byte[] dataBuffer = new byte[16284];
        private int count;

        public ZplScript(int port)
            : base(port, false)
        {

        }

        public override void ConnectionEstablished()
        {
            EmptyQueue();
            Read(1);
        }

        protected override void Received(byte[] receivedData, int length)
        {
            try
            {
                dataBuffer[count++] = receivedData[0];
                var message = Encoding.ASCII.GetString(dataBuffer, 0, count);
                if (message.EndsWith("^XZ"))
                {
                    var data = dataBuffer.Take(count).ToArray();
                    NotifySocketDataRecived(data);
                    count = 0;
                }
                Read(1);
            }
            catch (Exception e)
            {
                Core.Environment.Log.Write(e, 26235262);
                Disconnect(true);
            }
        }
    }
}