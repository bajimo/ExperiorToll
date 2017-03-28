using System;
using System.ComponentModel;
using Xcelgo.Communication;
using Xcelgo.Utils;

namespace Experior.Plugin
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class TCPDematic : TCP
    {
        public TCPDematic(int port, bool bufferEnabled)
            : base(port, bufferEnabled)
        {
        }

        public TCPDematic(int port, string ip, bool bufferEnabled)
            : base(port, ip, bufferEnabled)
        {
        }

        public override void ConnectionEstablished()
        {
            this.EmptyQueue();
            this.Read(1);
        }
    }
}
