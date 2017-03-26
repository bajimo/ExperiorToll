using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Assemblies;
using Experior.Dematic.Base;
using System.Collections.Generic;

namespace Experior.Catalog.Dematic.Pallet
{
    public class Create
    {
        public static Assembly RollerStraight(string title, string subtitle, object properties)
        {
            PalletStraightInfo info = new PalletStraightInfo
            {
                ConveyorType = PalletConveyorType.Roller,
                thickness = 0.05f,
                spacing = 0.1f,
                width = 0.972f,
                length = 1.4f,
                height = 0.7f,
                speed = 0.3f,
                name = Assembly.GetValidName("RO "),
            };
            return new PalletStraight(info);
        }

        public static Assembly ChainStraight(string title, string subtitle, object properties)
        {
            PalletStraightInfo info = new PalletStraightInfo
            {
                ConveyorType = PalletConveyorType.Chain,
                thickness = 0.05f,
                spacing = 0.1f,
                width = 1.25f,
                length = 1.0f,
                height = 0.7f,
                speed = 0.3f,
                name = Assembly.GetValidName("CH "),
            };
            return new PalletStraight(info);
        }

        public static Assembly Lift(string title, string subtitle, object properties)
        {
            var info = new LiftInfo();
            info.name = Assembly.GetValidName("Lift");
            info.LiftHeight = 1.5f;
            info.height = 0.7f;
            var assembly = new Lift(info);
            return assembly;
        }

        public static Assembly LiftTable(string title, string subtitle, object properties)
        {
            LiftTableInfo info = new LiftTableInfo();
            info.thickness = 0.05f;
            info.spacing = 0.1f;
            info.width = 0.972f;
            info.length = 1.4f;
            info.height = 0.7f;
            info.speed = 0.3f;
            info.name = Assembly.GetValidName("LT ");
            return new LiftTable(info);
        }

        public static Assembly SingleDropStation(string title, string subtitle, object properties)
        {
            SingleDropStationInfo info = new SingleDropStationInfo();
            info.thickness = 0.05f;
            info.spacing = 0.1f;
            info.width = 0.972f;
            info.length = 1.4f;
            info.height = 0.7f;
            info.speed = 0.3f;
            info.name = Assembly.GetValidName("DS ");
            return new SingleDropStation(info);
        }


        public static Assembly TCar(string title, string subtitle, object properties)
        {
            var source = "R:2.0:S1;R:3.5:S2;R:5.0:S3;R:6.5:S4;";
            var destination = "L:2.0:D1;L:3.5:D2;L:5.0:D3;L:6.5:D4;";

            TCarInfo info = new TCarInfo
            {
                TCarLength = 5.0f,
                TCarWidth = 1.25f,
                ConveyorWidth = 0.972f,
                height = 0.7f,
                Source = source,
                Destination = destination,
            };
            return new TCar(info);
        }

        public static Assembly Stacker(string title, string subtitle, object properties)
        {
            StackerInfo info = new StackerInfo
            {
                StackLimit = 10,
                PalletHeight = 0.145f,
                PalletLength = 1.2f,
                ConveyorType = PalletConveyorType.Roller,
                thickness = 0.05f,
                spacing = 0.1f,
                width = 0.972f,
                length = 1.4f,
                height = 0.7f,
                speed = 0.3f
            };
            return new Stacker(info);
        }

        public static Assembly Destacker(string title, string subtitle, object properties)
        {
            DestackerInfo info = new DestackerInfo
            {
                StackLimit = 10,
                PalletHeight = 0.145f,
                PalletLength = 1.2f,
                ConveyorType = PalletConveyorType.Roller,
                thickness = 0.05f,
                spacing = 0.1f,
                width = 0.972f,
                length = 1.4f,
                height = 0.7f,
                speed = 0.3f
            };
            return new Destacker(info);
        }

        public static Assembly PalletTransfer(string title, string subtitle, object properties)
        {
            PalletTransferInfo info = new PalletTransferInfo
            {
                ConveyorType = PalletConveyorType.Roller,
                thickness = 0.05f,
                spacing = 0.1f,
                width = 0.972f,
                length = 0.11f,
                height = 0.7f,
                speed = 0.3f
            };
            return new PalletTransfer(info);
        }
    }
}