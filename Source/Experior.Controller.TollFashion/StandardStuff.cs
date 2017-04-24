using System;
using System.Drawing;

//Put Standard stuuf here to get it out of the way

namespace Experior.Controller.TollFashion
{
    public partial class TollFashionRouting
    {
        private Core.Environment.UI.Toolbar.Button Speed1, Speed2, Speed5, Speed10, Speed20, HideShow, Reset, fps1, localProp, connectButt, disconnectButt;
        private Core.Environment.UI.Toolbar.Button btnWMSt;
        private ModelTools toolsForm;

        private void StandardConstructor()
        {
            //Add some speed buttons
            Speed1 = new Core.Environment.UI.Toolbar.Button("  1  ", Speed1_Click);
            Speed5 = new Core.Environment.UI.Toolbar.Button("  5  ", Speed5_Click);
            Speed2 = new Core.Environment.UI.Toolbar.Button("  2  ", Speed2_Click);
            Speed10 = new Core.Environment.UI.Toolbar.Button("  10  ", Speed10_Click);
            Speed20 = new Core.Environment.UI.Toolbar.Button("  20  ", Speed20_Click);
            Reset = new Core.Environment.UI.Toolbar.Button("Reset", Reset_Click);
            fps1 = new Core.Environment.UI.Toolbar.Button("1 FPS", FPS1_Click);
            localProp = new Core.Environment.UI.Toolbar.Button("Local", localProp_Click);
            connectButt = new Core.Environment.UI.Toolbar.Button("Connect", connectButt_Click);
            disconnectButt = new Core.Environment.UI.Toolbar.Button("Disconnect", disconnectButt_Click);

            Core.Environment.UI.Toolbar.Add(Speed1, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed2, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed5, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed10, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed20, "Speed");
            Core.Environment.UI.Toolbar.Add(Reset, "Scene");
            Core.Environment.UI.Toolbar.Add(fps1, "Scene");
            Core.Environment.UI.Toolbar.Add(localProp, "Tools");
            Core.Environment.UI.Toolbar.Add(connectButt, "Communication");
            Core.Environment.UI.Toolbar.Add(disconnectButt, "Communication");

            //Add a button to load the form
            btnWMSt = new Core.Environment.UI.Toolbar.Button("Tools", btnWMSt_Click);
            Core.Environment.UI.Toolbar.Add(btnWMSt, "Tools");

            toolsForm = new ModelTools(this);
        }

        private enum MessageSeverity
        {
            Critical,           //RED
            Warning,            //ORANGE
            Information,        //BLACK
            Test                //BLUE
        }

        private enum LoadType
        {
            Tote,
            Carton
        }

        private void ExperiorOutputMessage(string message, MessageSeverity messageSeverity)
        {
            Color colour = new Color();

            switch (messageSeverity)
            {
                case MessageSeverity.Critical: colour = Color.Red; break;
                case MessageSeverity.Warning: colour = Color.Orange; break;
                case MessageSeverity.Information: colour = Color.Black; break;
                case MessageSeverity.Test: colour = Color.Blue; break;
                default: colour = Color.Black; break;
            }
            Core.Environment.Log.Write(DateTime.Now + " Routing: " + message, colour);
        }

        void btnWMSt_Click(object sender, EventArgs e)
        {
            toolsForm.Show();
            //Experior.Core.Environment.Scene.Reset();
        }

        void Speed20_Click(object sender, EventArgs e)
        {
            Core.Environment.Time.Scale = 20;
        }

        void Speed10_Click(object sender, EventArgs e)
        {
            Core.Environment.Time.Scale = 10;
        }

        void Speed5_Click(object sender, EventArgs e)
        {
            Core.Environment.Time.Scale = 5;
        }

        void Speed2_Click(object sender, EventArgs e)
        {
            Core.Environment.Time.Scale = 2;
        }

        void Speed1_Click(object sender, EventArgs e)
        {
            Core.Environment.Time.Scale = 1;
        }

        void Reset_Click(object sender, EventArgs e)
        {
            ResetStandard();
        }

        void FPS1_Click(object sender, EventArgs e)
        {
            Core.Environment.Scene.FPS = 1;
        }

        void localProp_Click(object sender, EventArgs e)
        {
            //Core.Environment.Properties.Set(localProperties);
        }

        void connectButt_Click(object sender, EventArgs e)
        {
            foreach (var connection in Core.Communication.Connection.Items.Values)
            {
                if (connection is Core.Communication.TCPIP.TCP)
                {
                    Core.Communication.TCPIP.TCP ffsConn = connection as Core.Communication.TCPIP.TCP;
                    ffsConn.Connect();
                    ffsConn.AutoConnect = false;
                }
            }
        }

        void disconnectButt_Click(object sender, EventArgs e)
        {
            foreach (var connection in Core.Communication.Connection.Items.Values)
            {
                connection.Disconnect();
                connection.AutoConnect = false;
                connection.Reset();
            }
        }
    }
}