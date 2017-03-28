using System;
using System.Collections.Generic;
using System.ComponentModel;
using Xcelgo.Communication;
using Xcelgo.Utils;

namespace Experior.Plugin
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ATCConnection : TCP
    {

        //private byte[] data = new byte[1024];
        private List<byte> buffer = new List<byte>();
        private string Name;
        private int count = 0;

        public ATCConnection(int port, bool bufferEnabled, string name) : base(port, bufferEnabled)
        {
            Name = name;
        }

        public ATCConnection(int port, string ip, bool bufferEnabled, string name) : base(port, ip, bufferEnabled)
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
                //this.data[count++] = data[0];
                count++;
                buffer.Add(data[0]);
                if ((int)data[0] == (byte)35 && buffer.Count > 2) //If the byte value == 35 (ASCII #) then it is the end of the message, unless it is the first bytes which is the length of the message
                {
                    //this.count = 0;

                    //Check the message is correct by checking the length is correct

                    byte[] header = new byte[2] { buffer[1], buffer[0] }; //bytes are swapped
                    UInt16 bodyLength = BitConverter.ToUInt16(header, 0); //the header is the length of the body of the message (which does not contain the header)

                    //Take the header off then convert to byte array
                    byte[] body = new byte[buffer.Count - 2];
                    buffer.CopyTo(2, body, 0, buffer.Count - 2); 

                    if (bodyLength == buffer.Count - 2)
                    {
                        this.NotifySocketDataRecived(Converting.CopyBytes(0, body, body.Length));
                    }
                    else
                    {
                        string errorTelegram = string.Format("<{0}>", bodyLength.ToString());
                        errorTelegram += System.Text.Encoding.ASCII.GetString(body);
                        Core.Environment.Log.Write(string.Format("ATC Socket {0} Incorrect message length, message ignored: {1}", Name, errorTelegram));
                    }

                    buffer.Clear();
                }
                this.Read(1);
            }
            catch 
            {
                this.Disconnect(true);
            }
        }

        public override bool Send(byte[] byData)
        {
            List<byte> body = new List<byte>();
            foreach (byte b in byData)
            {
                body.Add(b);
            }

            //Add the # if required
            if (byData[byData.Length -1] != (byte)35)
            {
                body.Add((byte)35);
            }

            //Add the length
            UInt16 bodyLength = (UInt16)body.Count;
            byte[] length = BitConverter.GetBytes(bodyLength);
            body.Insert(0, length[1]);
            body.Insert(1, length[0]);

            byte[] telegram = new byte[body.Count];
            body.CopyTo(0, telegram, 0, telegram.Length);

            return base.Send(telegram);
        }
    }
}
