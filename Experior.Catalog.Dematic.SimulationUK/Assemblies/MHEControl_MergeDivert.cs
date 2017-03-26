using Experior.Catalog.Dematic.Case.Components;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.SimulationUK.Assemblies
{
    public class MHEControl_MergeDivert : MHEControl
    {
        private MergeDivertSimulationInfo mergeDivertSimulationInfo;
        private MergeDivertConveyor mergeDivertConveyor;

        private Random random = new Random();
        private Direction lastRouting = Direction.None;

        #region Constructors
        public MHEControl_MergeDivert(MergeDivertSimulationInfo info, MergeDivertConveyor mergeDivert)
        {
            mergeDivertConveyor = mergeDivert;
            mergeDivertSimulationInfo = info;
            Info = info;  // set this to save properties 
            
            mergeDivertConveyor.divertArrival = divertArrival;
            mergeDivertConveyor.loadDeleted = loadDeleted;
            mergeDivertConveyor.releasedStraight = releasedStraight;
            mergeDivertConveyor.releasedLeft = releasedLeft;
            mergeDivertConveyor.releasedRight = releasedRight;
        }
        #endregion

        bool divertArrival(Load load)
        {
            if (ControllerPoint)
                return false; //Returns false if it should be handled by the routing script need to add this to the main component

            //int validRouteCount = 0;
            bool DivertLeft = false, DivertRight = false, DivertStraight = false;
            List<Direction> validRoutes = new List<Direction>();

            List<Direction> directions = new List<Direction>();
            if (mergeDivertConveyor.LeftMode == MergeDivertConveyor.Modes.Divert) directions.Add(Direction.Left);
            if (mergeDivertConveyor.StraightMode == MergeDivertConveyor.Modes.Divert) directions.Add(Direction.Straight);
            if (mergeDivertConveyor.RightMode == MergeDivertConveyor.Modes.Divert) directions.Add(Direction.Right);

            if (directions.Count == 0)
                return true; //Returns true as if you return false the routing script event will be called

            Direction divertDirection = Direction.None;
            if (Distribution == RouteDistribution.Random)
            {
                divertDirection = directions[random.Next(0, directions.Count)];
            }
            else if (Distribution == RouteDistribution.RoundRobin)
            {
                for (int i = 0; i < directions.Count; i++)
                {
                    if (directions[i] == lastRouting)
                    {
                        if (i + 1 == directions.Count)
                            divertDirection = directions[0];
                        else
                            divertDirection = directions[i + 1];

                    }
                }
                if (divertDirection == Direction.None)
                    divertDirection = directions[0];
            }
            lastRouting = divertDirection;

            if (divertDirection == Direction.None)
                return true; //Failed to find a valid routing, just jumping out
            else
            {
                switch (divertDirection)
                {
                    case Direction.Left: DivertLeft = true; break;
                    case Direction.Right: DivertRight = true; break;
                    case Direction.Straight: DivertStraight = true; break;
                }
            }

            validRoutes.Add(divertDirection);

            mergeDivertConveyor.RouteLoad(load, validRoutes, false);
            return true; //returns true if handled by this controller
        }

        bool loadDeleted(Load load)
        {
            return true;
        }

        bool releasedStraight(Load load)
        {
            return true;
        }

        bool releasedLeft(Load load)
        {
            return true;
        }

        bool releasedRight(Load load)
        {
            return true;
        }

        #region User Interface

        [Category("Configuration")]
        [DisplayName("Controller Point")]
        [Description("Set true if the controller script should handle the routing. The routing will not be handleb by the selected PLC, however the configuration can still be used for routing within the controller script")]
        [PropertyOrder(1)]
        public bool ControllerPoint
        {
            get
            {
                return mergeDivertSimulationInfo.controllerPoint;
            }
            set
            {
                mergeDivertSimulationInfo.controllerPoint = value;
            }
        }


        [Category("Routing")]
        [DisplayName("Distribution")]
        [Description("How to distribute the loads at the divert point")]
        [PropertyOrder(11)]
        public RouteDistribution Distribution
        {
            get { return mergeDivertSimulationInfo.distribution; }
            set { mergeDivertSimulationInfo.distribution = value; }
        }
        #endregion

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    [XmlInclude(typeof(MergeDivertSimulationInfo))]
    public class MergeDivertSimulationInfo : ProtocolInfo
    {
        public bool controllerPoint = false;
        public RouteDistribution distribution = RouteDistribution.Random;
    }

    public enum RouteDistribution
    {
        RoundRobin,
        Random
    }
}