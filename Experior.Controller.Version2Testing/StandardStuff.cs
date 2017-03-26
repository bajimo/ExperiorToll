using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Experior.Catalog.Assemblies;
using Experior.Catalog.MainBK25;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Experior.Core.Communication;
using Experior.Catalog.Assemblies.BK10;

//Put Standard stuuf here to get it out of the way

namespace Experior.Controller
{
    public partial class Version2Testing : Experior.Catalog.ControllerExtended
    {

        Experior.Core.Environment.UI.Toolbar.Button Speed1, Speed2, Speed5, Speed10, Speed20, HideShow, Reset, fps1;
        Core.Environment.UI.Toolbar.Button btnWMSt;
        ModelTools toolsForm;

        private void StandardConstructor()
        {
            //Add some speed buttons
            Speed1 = new Core.Environment.UI.Toolbar.Button("  1  ", Speed1_Click);
            Speed5 = new Core.Environment.UI.Toolbar.Button("  5  ", Speed5_Click);
            Speed2 = new Core.Environment.UI.Toolbar.Button("  2  ", Speed2_Click);
            Speed10 = new Core.Environment.UI.Toolbar.Button("  10  ", Speed10_Click);
            Speed20 = new Core.Environment.UI.Toolbar.Button("  20  ", Speed20_Click);
            HideShow = new Core.Environment.UI.Toolbar.Button("Hide Show MS", HideShow_Click);
            Reset = new Core.Environment.UI.Toolbar.Button("Reset", Reset_Click);
            fps1 = new Core.Environment.UI.Toolbar.Button("1 FPS", FPS1_Click);

            Core.Environment.UI.Toolbar.Add(Speed1, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed2, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed5, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed10, "Speed");
            Core.Environment.UI.Toolbar.Add(Speed20, "Speed");
            Core.Environment.UI.Toolbar.Add(HideShow, "HideShow");
            Core.Environment.UI.Toolbar.Add(Reset, "Olefs Buttons");
            Core.Environment.UI.Toolbar.Add(fps1, "Olefs Buttons");

            toolsForm = new ModelTools();
            toolsForm.Show();
            toolsForm.Hide();

            //Add a button to load the form
            btnWMSt = new Core.Environment.UI.Toolbar.Button("Tools", btnWMSt_Click);
            Core.Environment.UI.Toolbar.Add(btnWMSt, "Tools");
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

        public enum ClalitLoadType
        {
            StockToteWithWindow,
            StockToteWithoutWindow,
            PickTote,
            ChilledTote,
            ToxicTote,
            Carton
        }

        private void ExperiorOutputMessage(string message, MessageSeverity messageSeverity)
        {
            Color colour = new Color();

            switch (messageSeverity)
            {
                case MessageSeverity.Critical: colour = System.Drawing.Color.Red; break;
                case MessageSeverity.Warning: colour = System.Drawing.Color.Orange; break;
                case MessageSeverity.Information: colour = System.Drawing.Color.Black; break;
                case MessageSeverity.Test: colour = System.Drawing.Color.Blue; break;
                default: colour = System.Drawing.Color.Black; break;
            }
            Experior.Core.Environment.Log.Write(DateTime.Now + " Routing: " + message, colour);
        }

        void btnWMSt_Click(object sender, EventArgs e)
        {
            toolsForm.Show();
            //Experior.Core.Environment.Scene.Reset();
        }

        void Speed20_Click(object sender, EventArgs e)
        {
            Experior.Core.Environment.Time.Scale = 20;
        }

        void Speed10_Click(object sender, EventArgs e)
        {
            Experior.Core.Environment.Time.Scale = 10;
        }

        void Speed5_Click(object sender, EventArgs e)
        {
            Experior.Core.Environment.Time.Scale = 5;
        }

        void Speed2_Click(object sender, EventArgs e)
        {
            Experior.Core.Environment.Time.Scale = 2;
        }

        void Speed1_Click(object sender, EventArgs e)
        {
            Experior.Core.Environment.Time.Scale = 1;
        }

        void HideShow_Click(object sender, EventArgs e)
        {
            try
            {
                Experior.Core.Section section = Experior.Core.Section.Get("MultiShuttle");
                bool visible = true;
                if (section.Assemblies[0].Visible == true)
                    visible = false;

                foreach (Experior.Core.Assemblies.Assembly assembly in section.Assemblies)
                {
                    assembly.Visible = visible;
                }
            }
            catch { }
        }

        void Reset_Click(object sender, EventArgs e)
        {
            ResetStandard();
        }

        void FPS1_Click(object sender, EventArgs e)
        {
            Experior.Core.Environment.Scene.FPS = 1;
        }

        private ushort[] getDest(int word, int bit)
        {
            ushort[] result = new ushort[4];
            BitArray bitarray = new BitArray(16);
            bitarray[bit - 1] = true;
            byte[] b = new byte[2];
            bitarray.CopyTo(b, 0);

            ushort w = BitConverter.ToUInt16(b, 0);
            result[word - 1] = w;
            return result;
        }

        private ushort[] joinDest(ushort[] dest1, ushort[] dest2)
        {
            ushort[] result = new ushort[4];

            result[0] = (ushort)(dest1[0] | dest2[0]);
            result[1] = (ushort)(dest1[1] | dest2[1]);
            result[2] = (ushort)(dest1[2] | dest2[2]);
            result[3] = (ushort)(dest1[3] | dest2[3]);

            return result;
        }

    }

}
