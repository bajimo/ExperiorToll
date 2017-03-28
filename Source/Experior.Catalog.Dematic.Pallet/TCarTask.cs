using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;

namespace Experior.Catalog.Dematic.Pallet
{
    public class TCarTask
    {
        public DematicFixPoint Source { get; set; }
        public DematicFixPoint Destination { get; set; }
        public TCycle TCarCycle { get; set; }
    }
}
