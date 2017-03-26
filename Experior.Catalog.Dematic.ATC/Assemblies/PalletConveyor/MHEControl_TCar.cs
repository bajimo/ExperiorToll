using Experior.Catalog.Dematic.Pallet;
using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Loads;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.ATC.Assemblies.PalletConveyor
{
    public class MHEControl_TCar : MHEControl
    {
        private TCarATCInfo tCarATCInfo;
        private MHEController_Pallet palletPLC;
        private TCar tCar;
        
        #region Constructors

        public MHEControl_TCar(TCarATCInfo info, TCar tCarAssembly)
        {
            tCarATCInfo = info;
            Info = info;  // set this to save properties 
            tCar = tCarAssembly;
            palletPLC = tCar.Controller as MHEController_Pallet;
            tCar.sourceArrival = SourceArrival;
        }

        public void SourceArrival(Load load, DematicFixPoint sourceFixPoint)
        {
            // Get the correct FixPoint for this destination
            List<FixPoint> fixpointDestinations = GetFixPointDestinations();
            string destinationName = GetRandomDestination(fixpointDestinations); // Temporary : Get random one for now
            var fpName = fixpointDestinations.Find(x => x.ContainsDestination(destinationName) == true).Name;
            DematicFixPoint fp = (DematicFixPoint)tCar.FixPoints.Find(x => x.Name == fpName);





            TCarTask task = new TCarTask
            {
                Source = sourceFixPoint,
                Destination = fp,
            };
            tCar.Tasks.Add(task);
        }

        private string GetRandomDestination(List<FixPoint> fixpointDestinations)
        {
            var random = new Random();
            FixPoint fp = fixpointDestinations[random.Next(fixpointDestinations.Count)];
            string destination = fp.Destinations[random.Next(fp.Destinations.Count)];
            return destination;
        }

        private List<FixPoint> GetFixPointDestinations()
        {
            List<FixPoint> list = new List<FixPoint>();

            // Example Input = D1=RO 10;D2=RO 12;D3=RO 14;D4=RO 16;
            var configArray = DestinationConfig.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in configArray)
            {
                var fixPointArray = item.Split('=');
                if (fixPointArray.Length == 2)
                {
                    var fixPointName = fixPointArray[0];
                    var destinationArray = fixPointArray[1].Split(',');
                    List<string> destinationList = new List<string>(destinationArray);
                    list.Add(new FixPoint() { Name = fixPointName, Destinations = destinationList });
                }
            }

            return list;
        }
                
        public override void Dispose()
        {
           
        }

        #endregion


        [DisplayName("Destination Config")]
        [Description("Destination Config")]
        public string DestinationConfig
        {
            get { return tCarATCInfo.destinationConfig; }
            set { tCarATCInfo.destinationConfig = value; }
        }

    }

    [Serializable]
    [XmlInclude(typeof(TCarATCInfo))]
    public class TCarATCInfo : ProtocolInfo
    {
        public bool alwaysArrival = true;
        public bool loadWait = true;
        public string destinationConfig;
    }

    public class FixPoint
    {
        public FixPoint()
        {
        }

        public bool ContainsDestination(string destination)
        {
            return Destinations.Any(x => x == destination);
        }

        public string Name { get; set; }
        public List<string> Destinations { get; set; }
    }
}
