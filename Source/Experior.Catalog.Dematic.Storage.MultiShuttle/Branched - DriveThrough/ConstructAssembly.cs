using System;
using System.Collections.Generic;
using System.Text;
using Experior.Core;
using System.Drawing;
using Experior.Core.Assemblies;
using System.Linq;
using Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies;

namespace Experior.Catalog.Dematic.Storage
{

    public class Create
    {
        internal static Assembly CreateDematicMultiShuttle(bool driveThrough, string subtitle)
        {
            if (!driveThrough)
            {
                MultiShuttleInfo info = new MultiShuttleInfo();
                info.name = Experior.Core.Assemblies.Assembly.GetValidName("Multi-Shuttle ");
                info.AisleNo = Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies.MultiShuttle.Aisles.Count + 1; 

                //if (subtitle == "Mixed")
                //{
                //    info.ElevatorFR = true;
                //    info.ElevatorFL = true;
                //    info.ElevatorFLtype = Experior.Catalog.Dematic.Storage.Assemblies.MultiShuttleDirections.Infeed;
                //    info.ElevatorFRtype = Experior.Catalog.Dematic.Storage.Assemblies.MultiShuttleDirections.Infeed;
                //    info.ElevatorBR = false;
                //    info.ElevatorBL = false;
                //    info.MixedInfeedOutfeed = true;

                //    info.LevelHeightDropstations.Add(new LevelHeight() { Level = "01", Height = 1.0f });
                //    info.LevelHeightPickstations.Add(new LevelHeight() { Level = "01", Height = 0.3f });
                //}
                //else
                {
                    info.dropStationConfig = "1:01;2:02";
                    info.pickStationConfig = "3:01;4:02";


                }

                return new Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies.MultiShuttle(info);
            }
            else
            {
                MultiShuttleInfo info = new MultiShuttleInfo();
                info.name = Experior.Core.Assemblies.Assembly.GetValidName("Multi-Shuttle ");
                info.AisleNo = Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies.MultiShuttle.Aisles.Count + 1; //TODO not sure how this works with drive through
                info.DriveThrough = true;
                //MS Drive Through uses the two "Front" elevators. Back elevators are removed.
                info.ElevatorFR = true;
                info.ElevatorFL = true;
                info.ElevatorBR = false;
                info.ElevatorBL = false;

                info.dropStationConfig = "0:01;1:02";  
                info.pickStationConfig = "2:01;3:02";
                info.rackHeight = 4;

              //  info.OutfeedNamingConvention = Experior.Catalog.Dematic.Storage.Assemblies.CreateDematicMultiShuttle.OutFeedNamingConventions.NEW_POS1_POS2_001_002;
                //info.FrontLeftElevatorGroupName = "A";
                //info.FrontRightElevatorGroupName = "A";

                //info.FrontLeftInfeedRackGroupName = "B";
                //info.FrontRightInfeedRackGroupName = "A";

                //info.FrontLeftOutfeedRackGroupName = "A";
                //info.FrontRightOutfeedRackGroupName = "B";


                return new Experior.Catalog.Dematic.Storage.MultiShuttle.Assemblies.MultiShuttle(info);
            }
        }

    }
}