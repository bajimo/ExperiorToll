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
    class MHEControl_AngledDivert : MHEControl
    {
        private AngledDivertSimulationInfo angledDivertSimulaionInfo;
        private AngledDivert angledDivertConveyor;

        private Random random = new Random();
        private DivertRoute lastRouting = DivertRoute.None;

        #region Constructors
        public MHEControl_AngledDivert(AngledDivertSimulationInfo info, AngledDivert angledDivert)
        {
            angledDivertConveyor      = angledDivert;
            angledDivertSimulaionInfo = info;
            Info                      = info;

           // angledDivertConveyor.divertEntryArrival = divertEntryArrival;
           // angledDivertConveyor.divertArrival      = divertArrival;

            angledDivertConveyor.OnDivertPointArrivedControl += angledDivertConveyor_OnDivertArrivalController;
        }

        void angledDivertConveyor_OnDivertArrivalController(object sender, AngleDivertArgs e)
        {
            
            List<DivertRoute> validRoutes = new List<DivertRoute>();
            List<DivertRoute> directions = new List<DivertRoute>();
            directions.Add(DivertRoute.Straight);
            directions.Add(DivertRoute.Divert);

            DivertRoute divertDirection = DivertRoute.None;

            if (Distribution == RouteDistribution.Random)
            {
                divertDirection = directions[random.Next(0, 2)];
            }
            else if (Distribution == RouteDistribution.RoundRobin)
            {
                if (lastRouting != DivertRoute.Straight)
                {
                    divertDirection = DivertRoute.Straight;
                }
                else
                {
                    divertDirection = DivertRoute.Divert;
                }
            }
            lastRouting = divertDirection;

            if (divertDirection != DivertRoute.None)
            {
                validRoutes.Add(divertDirection); //A list of loads, sometimes either route will be valid and the divert 
               
                //can decide which is the best.
                //angledDivertConveyor.RouteLoad(e._load, validRoutes);
                angledDivertConveyor.RouteLoad(divertDirection);

            }
        }


        #endregion
   
       #region User Interface

        [Category("Routing")]
        [DisplayName("Distribution")]
        [Description("How to distribute the loads at the divert point")]
        [PropertyOrder(11)]
        public RouteDistribution Distribution
        {
            get { return angledDivertSimulaionInfo.distribution; }
            set { angledDivertSimulaionInfo.distribution = value; }
        }
        #endregion



        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    [Serializable]
    [XmlInclude(typeof(AngledDivertSimulationInfo))]
    public class AngledDivertSimulationInfo : ProtocolInfo
    {
        public bool controllerPoint = false;
        public RouteDistribution distribution = RouteDistribution.Random;
    }
}

