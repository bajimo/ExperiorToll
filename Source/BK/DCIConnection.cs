using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xcelgo.Communication;
using Xcelgo.Utils;

namespace Experior.Plugin
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class DCIConnection : TCP
    {

        //private byte[] data = new byte[1024];
        private List<byte> buffer = new List<byte>();
        private string Name;

        public DCIConnection(int port, bool bufferEnabled, string name) : base(port, bufferEnabled)
        {
            Name = name;
        }

        public DCIConnection(int port, string ip, bool bufferEnabled, string name) : base(port, ip, bufferEnabled)
        {
            Name = name;
        }

        public override void ConnectionEstablished()
        {
            this.EmptyQueue();
            this.Read(1);
        }

        protected override void Received(byte[] data, int length)
        {
            try
            {
                buffer.Add(data[0]);
                if (data[0] == 35 && buffer[buffer.Count - 2] == 35) //The last 2 bytes are '##'
                {
                    //Take the header off then convert to byte array
                    byte[] body = new byte[buffer.Count];
                    buffer.CopyTo(0, body, 0, buffer.Count); 

                    this.NotifySocketDataRecived(Converting.CopyBytes(0, body, body.Length));

                    //{
                    //    string errorTelegram = string.Format("<{0}>", bodyLength.ToString());
                    //    errorTelegram += System.Text.Encoding.ASCII.GetString(body);
                    //    Core.Environment.Log.Write(string.Format("ATC Socket {0} Incorrect message length, message ignored: {1}", Name, errorTelegram));
                    //}

                    buffer.Clear();
                }
                this.Read(1);
            }
            catch 
            {
                this.Disconnect(true);
            }
        }

        //public override bool Send(byte[] byData)
        //{
        //    List<byte> body = new List<byte>();
        //    foreach (byte b in byData)
        //    {
        //        body.Add(b);
        //    }

        //    //Add the length
        //    byte[] telegram = new byte[body.Count];
        //    body.CopyTo(0, telegram, 0, telegram.Length);

        //    return base.Send(telegram);
        //}
    }
}
