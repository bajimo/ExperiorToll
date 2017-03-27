using System.Collections.Generic;
using System.Drawing;
using Experior.Catalog.Dematic.Sorter.Assemblies;
using Experior.Core.Routes;
using Environment = Experior.Core.Environment;
using Experior.Catalog.Dematic.Sorter.Properties;

namespace Experior.Catalog.Dematic.Sorter
{
    public class Sorter : Core.Catalog
    {
        public Sorter()
            : base("Dematic Sorter")
        {
            Simulation = Environment.Simulation.Events;

            Common.Meshes = new Core.Resources.Meshes(System.Reflection.Assembly.GetExecutingAssembly());
            Common.Icons = new Core.Resources.Icons(System.Reflection.Assembly.GetExecutingAssembly());

            //Add dependencies for chute and induction conveyors 
            Core.Dependencies.Add("Experior.Catalog.Dematic.Case.Conveyors.dll");
            Core.Dependencies.Add("Experior.Catalog.Logistic.Track.dll");

            Add(Common.Icons.Get("SorterStraight"), "Straight", Environment.Simulation.Events, Create.SorterStraight);
            Add(Common.Icons.Get("SorterCurveClockwise"), "Curve", "Clockwise", Environment.Simulation.Events, Create.SorterCurve);
            Add(Common.Icons.Get("SorterCurveCounterClockwise"), "Curve", "Counterclockwise", Environment.Simulation.Events, Create.SorterCurve);
            Add(Common.Icons.Get("Straight"), "Induction", Environment.Simulation.Events, Create.SorterInduction);
            Add(Common.Icons.Get("Straight"), "Chute", Environment.Simulation.Events, Create.SorterChute);
            Add(Common.Icons.Get("SorterController"), "Sorter Controller", "Example", Environment.Simulation.Events, Create.SorterController);

            Environment.Scene.OnLocked += SceneLocking;
            Environment.Nodes.OnListNodes += Nodes_ListNodes;
        }

        private void SceneLocking()
        {
            //Initialize all sorters
            Environment.InvokeIfRequired(SorterElement.InitializeMasterSorters);
        }

        private void Nodes_ListNodes()
        {
            //Add all sorter fix points to the Experior node list
            var allnodes = new List<INode>();

            foreach (SorterElement element in SorterElement.SorterElements)
            {
                allnodes.AddRange(element.SorterFixPointList);
            }

            Environment.Nodes.List(allnodes);
        }

        public override Image Logo
        {
            get
            {
                return Resources.dematic;
            }
        }

    }
}
