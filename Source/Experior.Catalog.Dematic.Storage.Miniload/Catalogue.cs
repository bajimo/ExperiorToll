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
using Microsoft.DirectX;

namespace Experior.Catalog
{
    public class Miniloads : Experior.Core.Catalog
    { 
        public Miniloads()
            : base("Miniloads")
        {
            Simulation = Core.Environment.Simulation.Events;
          
            Core.Dependencies.Add("Experior.Catalog.Logistic.Storage.dll");

            InitCatalog();

            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("DematicHBW"), "Miniload", "1x2", Core.Environment.Simulation.Events);
        }

        public static void InitCatalog()
        {
            Common.Meshes = new Experior.Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Experior.Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());
        }

        public override Image Logo
        {
            get
            {
                return Common.Icons.Get("dematic");
            }
        }

        public override Core.Assemblies.Assembly Construct(string title, string subtitle, object properties)
        {
            if (title == "Miniload" && subtitle == "1x2")
                return Create.DematicMiniLoad1x2();

            return null;
        }

    }
}
