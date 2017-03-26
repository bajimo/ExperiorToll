using System.Drawing;
using Experior.Core;
using Experior.Core.Resources;

namespace Experior.Catalog.Dematic.Pallet
{
    internal class Common
    {
        public static Meshes Meshes;
        public static Icons Icons;
    }

    public class Catalogue : Core.Catalog
    {
        public Catalogue() : base("Pallet Conveyors")
        {
            Simulation = Environment.Simulation.Events;

            Common.Meshes = new Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Add(Common.Icons.Get("RollerStraight"), "Roller Straight", "", Environment.Simulation.Events, Create.RollerStraight);
            Add(Common.Icons.Get("RollerStraight"), "Chain Straight", "", Environment.Simulation.Events, Create.ChainStraight);
            Add(Common.Icons.Get("RollerStraight"), "Lift", "", Environment.Simulation.Events, Create.Lift);
            Add(Common.Icons.Get("MergeDivertConveyor"), "Lift Table", "", Environment.Simulation.Events, Create.LiftTable);
            Add(Common.Icons.Get("RollerStraight"), "TCar", "", Environment.Simulation.Events, Create.TCar);
            Add(Common.Icons.Get("RollerStraight"), "Stacker", "", Environment.Simulation.Events, Create.Stacker);
            Add(Common.Icons.Get("RollerStraight"), "Destacker", "", Environment.Simulation.Events, Create.Destacker);
            Add(Common.Icons.Get("Transfer"), "Transfer", "", Environment.Simulation.Events, Create.PalletTransfer);
            Add(Common.Icons.Get("MergeDivertConveyor"), "Single Drop Station", "", Environment.Simulation.Events, Create.SingleDropStation);
        }

        public override Image Logo
        {
            get { return Common.Icons.Get("dematic"); }
        }
    }
}
