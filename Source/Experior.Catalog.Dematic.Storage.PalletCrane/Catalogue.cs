using System.Drawing;

namespace Experior.Catalog.Dematic.Storage.PalletCrane
{
    public sealed class PalletCraneCatalog : Core.Catalog
    { 
        public PalletCraneCatalog()
            : base("Pallet Crane")
        {
            Simulation = Core.Environment.Simulation.Events;
          
            Core.Dependencies.Add("Experior.Catalog.Logistic.Storage.dll");
            Core.Dependencies.Add("Experior.Catalog.Dematic.Pallet.Conveyors.dll");

            InitCatalog();

            Common.Meshes = new Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("DematicHBW"), "Pallet Crane", "1x1", Core.Environment.Simulation.Events);
        }

        public static void InitCatalog()
        {
            Common.Meshes = new Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());
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
            if (title == "Pallet Crane" && subtitle == "1x1")
                return Create.DematicPalletCrane1X1();

            return null;
        }
    }
}
