using Experior.Catalog.Dematic.Storage.PalletCrane.Assemblies;
using Experior.Core.Assemblies;
using Experior.Core.Properties.Collections;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Storage.PalletCrane
{
    internal class Common
    {
        public static Core.Resources.Meshes Meshes;
        public static Core.Resources.Icons Icons;
    }

    public class Create
    {
        internal static Assembly DematicPalletCrane1X1()
        {
            PalletCraneInfo info = new PalletCraneInfo();
            info.name = Assembly.GetValidName("Pallet Crane ");
            info.RailLength = 95;
            info.Craneheight = 20;
            info.LHDLength = 1.1f;
            info.LHDNumber = 1;
            info.LHDWidth = 0.978f;
            info.LHDSpacing = 0;
            info.AisleWidth = 1.61f;
            info.DropStations = new ExpandablePropertyList<StationConfiguration>();
            info.DropStations.Add(new StationConfiguration { LevelHeight = 1, DistanceX = 0.5f, StationType = PalletCraneStationTypes.DropStation, Side = PalletCraneStationSides.Left, Length = 1.61f, thickness = 0.05f, Width = 0.978f, Speed = 0.7f, ConveyorType = PalletConveyorType.Roller });
            info.DropStations.Add(new StationConfiguration { LevelHeight = 2, DistanceX = 0.5f, StationType = PalletCraneStationTypes.DropStation, Side = PalletCraneStationSides.Left, Length = 1.61f, thickness = 0.05f, Width = 0.978f, Speed = 0.7f, ConveyorType = PalletConveyorType.Roller });
            info.PickStations = new ExpandablePropertyList<StationConfiguration>();
            info.PickStations.Add(new StationConfiguration { LevelHeight = 1, DistanceX = 0.5f, StationType = PalletCraneStationTypes.PickStation, Side = PalletCraneStationSides.Right, Length = 1.61f, thickness = 0.05f, Width = 0.978f, Speed = 0.7f, ConveyorType = PalletConveyorType.Roller });
            info.PickStations.Add(new StationConfiguration { LevelHeight = 2, DistanceX = 0.5f, StationType = PalletCraneStationTypes.PickStation, Side = PalletCraneStationSides.Right, Length = 1.61f, thickness = 0.05f, Width = 0.978f, Speed = 0.7f, ConveyorType = PalletConveyorType.Roller });

            return new Assemblies.PalletCrane(info);
        }
    }
}
