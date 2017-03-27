using Experior.Catalog.Dematic.Case;
using Experior.Dematic.Base;
using System.Drawing;

namespace Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies
{
    public interface IPickDropConvInfo
    {
        float thickness { get; set; }
        float Width { get; set; }
        Color color { get; set; }
        MultiShuttle parentMultiShuttle { get; set; }
        Elevator elevator { get; set; }
        LevelID level { get; set; }
        string name { get; set; }
    }


    public interface IPickDropConv
    {
        void ConvLocationConfiguration(string level);
        RackSide Side { get; set; }
        int AisleNumber { get; set; }
        float Height { get; set; }
        int Positions { get; set; }
        float Width { get; set; }
        AccumulationPitch AccPitch { get; set; }
        OutfeedLength OutfeedSection { get; set; }
        float InfeedSection { get; set; }

    }
}
