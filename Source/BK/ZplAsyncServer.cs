using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Experior.Plugin
{
    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Client  socket.  
        public Socket WorkSocket;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] Buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder Sb = new StringBuilder();
    }

    public class ZplAsyncServer
    {
        // Thread signal.  
        private readonly ManualResetEvent allDone = new ManualResetEvent(false);

        private Socket listener;
        private bool listening;

        public event EventHandler<string> TelegramReceived;

        public void StartListening(int port)
        {
            // Establish the local endpoint for the socket.  
            var localEndPoint = new IPEndPoint(IPAddress.Any, port);
            listening = true;
            
            // Create a TCP/IP socket.  
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (listening)
                {
                    // Set the event to nonsignaled state.  
                    allDone.Reset();
                    // Start an asynchronous socket to listen for connections.  
                    //Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(AcceptCallback, listener);
                    // Wait until a connection is made before continuing.  
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public void Dispose()
        {
            //listener.Disconnect(false);
            listener.Dispose();
            listening = false;
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                // Signal the main thread to continue.  
                allDone.Set();
                // Get the socket that handles the client request.  
                var listener = (Socket)ar.AsyncState;
                var handler = listener.EndAccept(ar);
                // Create the state object.  
                var state = new StateObject { WorkSocket = handler };
                handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            var state = (StateObject)ar.AsyncState;
            var handler = state.WorkSocket;
            // Read data from the client socket.   
            var bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far.  
                state.Sb.Append(Encoding.ASCII.GetString(state.Buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read   
                // more data.  
                var content = state.Sb.ToString();
                if (content.IndexOf("^XZ", StringComparison.Ordinal) > -1)
                {
                    // All the data has been read from the client   
                    OnTelegramReceived(content);
                }
                else
                {
                    // Not all data received. Get more.  
                    handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
                }
            }
        }

        protected virtual void OnTelegramReceived(string e)
        {
            TelegramReceived?.Invoke(this, e);
        }
    }
}