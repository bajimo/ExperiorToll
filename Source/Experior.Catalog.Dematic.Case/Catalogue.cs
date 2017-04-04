using System.Drawing;
using Experior.Core;
using Experior.Core.Resources;

namespace Experior.Catalog.Dematic.Case
{
    internal class Common
    {
        public static Meshes Meshes;
        public static Icons Icons;
    }

    public class Catalogue : Core.Catalog {

        public Catalogue() : base("Case Conveyors")
        {            
            Environment.Engine.RoutingGraph = false;

            Simulation = Environment.Simulation.Events;

            Common.Meshes = new Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Icons(System.Reflection.Assembly.GetExecutingAssembly());

            Dependencies.Add("Experior.Catalog.Logistic.Basic.dll");
            Dependencies.Add("Experior.Catalog.Logistic.Track.dll");

            Add(Common.Icons.Get("Straight"), "StraightBelt", Environment.Simulation.Events, Create.StraightBelt);
            Add(Common.Icons.Get("accumulation"), "StraightAccumulation", Environment.Simulation.Events, Create.StraightAccumulation);
            Add(Common.Icons.Get("Straight"), "Rotate Load", Environment.Simulation.Events, Create.RotateLoad);
            Add(Common.Icons.Get("RaRoller"),"Roller", Environment.Simulation.Events, Create.Roller);
            Add(Common.Icons.Get("CurveClockwise"), "Belt Curve", "(Clockwise)", Environment.Simulation.Events, Create.BeltCurve);
            Add(Common.Icons.Get("CurveCounterClockwise"), "Belt Curve", "(Counter Clockwise)", Environment.Simulation.Events, Create.BeltCurve);
            Add(Common.Icons.Get("AngledDivert"),"AngledDivert","", Environment.Simulation.Events, Create.AngledDivert);
            Add(Common.Icons.Get("AngledMerge"), "AngledMerge", "", Environment.Simulation.Events, Create.AngledMerge);
            Add(Common.Icons.Get("BeltSorterInduct"), "Belt Sorter", "Induct", Environment.Simulation.Events, Create.BeltSorter);
            Add(Common.Icons.Get("BeltSorterMerge"), "Belt Sorter", "Angled Merge", Environment.Simulation.Events, Create.BeltSorter);
            Add(Common.Icons.Get("BeltSorterDivert"), "Belt Sorter", "Angled Divert", Environment.Simulation.Events, Create.BeltSorter);
            Add(Common.Icons.Get("BeltSorterMergePopUp"), "Belt Sorter", "Pop Up Merge", Environment.Simulation.Events, Create.BeltSorter);
            Add(Common.Icons.Get("BeltSorterDivertPopUp"), "Belt Sorter", "Pop Up Divert", Environment.Simulation.Events, Create.BeltSorter);
            Add(Common.Icons.Get("DHDM"), "Transfer", "DHDM", Environment.Simulation.Events, Create.Transfer);
            Add(Common.Icons.Get("2Way"), "Transfer", "2 Way", Environment.Simulation.Events, Create.Transfer);
            Add(Common.Icons.Get("MergeDivertConveyor"), "Transfer", "Diverter/Merger", Environment.Simulation.Events, Create.Transfer);
            Add(Common.Icons.Get("TransferPlate"), "Transfer", "Transfer Plate", Environment.Simulation.Events, Create.Transfer);
            Add(Common.Icons.Get("MergeDivertConveyor"), "Transfer", "Divert Transfer Plate", Environment.Simulation.Events, Create.Transfer);
            Add(Common.Icons.Get("TwoToOneMerge"), "Transfer", "Two-To-One Merge", Environment.Simulation.Events, Create.Transfer);
            Add(Common.Icons.Get("BeltSorterMergePopUp"), "Miniload", "Pick Station", Environment.Simulation.Events, Create.Miniload);
            Add(Common.Icons.Get("BeltSorterMergePopUp"), "Miniload", "Drop Station", Environment.Simulation.Events, Create.Miniload);
            Add(Common.Icons.Get("InProgess"), "Tray Stacker", "", Environment.Simulation.Events, Create.TrayStacker);
            Add(Common.Icons.Get("InProgess"), "Tray Destacker", "", Environment.Simulation.Events, Create.TrayDestacker);
            Add(Common.Icons.Get("Straight"), "Pick Stn Double", "", Environment.Simulation.Events, Create.PickDoubleLift);
            Add(Common.Icons.Get("Straight"), "Pick Put Stn", "", Environment.Simulation.Events, Create.PickPutStation);
            //Add(Common.Icons.Get("3Way"), "Transfer", "3 Way", Environment.Simulation.Events);
            //Add(Common.Icons.Get("Straight"),"Conveyor Units", Environment.Simulation.Events);
        }

        public override Image Logo {
            get {return Common.Icons.Get("dematic");}                            
        }

    }
}
