using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using Experior.Core;
using Experior.Core.Assemblies;

namespace Experior.Catalog.Dematic.Storage
{
    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class MultiShuttles : Experior.Core.Catalog
    {
        public MultiShuttles() : base("MultiShuttles")
        {

            Simulation = Experior.Core.Environment.Simulation.Events | Experior.Core.Environment.Simulation.Physics;

            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("MultiShuttle"), "MultiShuttle", "Captive", Core.Environment.Simulation.Events);
            Add(Common.Icons.Get("MSmiddleElevator"), "MultiShuttle", "Drive Through", Core.Environment.Simulation.Events);
            Add(Common.Icons.Get("ParentMultiShuttle"), "MultiShuttle", "Mixed", Core.Environment.Simulation.Events);

        }
        public override Image Logo
        {
            get
            {
                return Common.Icons.Get("dematic");
            }
        }

        public override Experior.Core.Assemblies.Assembly Construct(string title, string subtitle, object properties)
        {

            if (title == "MultiShuttle" && subtitle == "Captive")
                return Create.CreateDematicMultiShuttle(false, subtitle);
            else if (title == "MultiShuttle" && subtitle == "Mixed")
                return Create.CreateDematicMultiShuttle(false, subtitle);
            else if (title == "MultiShuttle")
                return Create.CreateDematicMultiShuttle(true, subtitle);
            return null;
        }

    }
}
