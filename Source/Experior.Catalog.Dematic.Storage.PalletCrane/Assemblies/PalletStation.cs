using System;
using System.Drawing;
using Experior.Catalog.Dematic.Pallet.Assemblies;
using Experior.Core.Routes;
using Experior.Dematic.Base;
using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Storage.PalletCrane.Assemblies
{
    public class PalletStation : PalletStraight
    {
        public StationConfiguration Configuration { get; }
        public ActionPoint PositionAp { get; }

        public PalletStation(StationConfiguration config) : base(config)
        {
            Configuration = config;
            Configuration.StationConfigurationChanged += StationConfigChanged;

            //Color = Color.Gray;
            TransportSection.Visible = false;
            TransportSection.Route.Motor.Visible = false;
            if (ConveyorType == PalletConveyorType.Chain)
            {
                ChainLeft.Visible = false;
                ChainRight.Visible = false;
                StartLine2.Visible = false;
                EndLine2.Visible = false;
            }
            StartLine1.Visible = false;
            EndLine1.Visible = false;
            Arrow.Visible = false;
            LineReleasePhotocell.Visible = false;

            PositionAp = TransportSection.Route.InsertActionPoint(Length / 2);

            UpdateType();
        }

        void StationConfigChanged(object sender, StationConfigurationChangedEventArgs e)
        {
            Update();
        }

        public void Update()
        {
            UpdatePosition();
            UpdateSide();
            UpdateType();
        }

        private void UpdatePosition()
        {
            LocalPosition = new Vector3(Configuration.DistanceX, Configuration.LevelHeight, LocalPosition.Z);
        }

        public override void Dispose()
        {
            Configuration.StationConfigurationChanged -= StationConfigChanged;
            base.Dispose();
        }

        private void UpdateType()
        {
            if (Configuration.StationType == PalletCraneStationTypes.DropStation)
            {
                StartFixPoint.Visible = false;
                StartFixPoint.Enabled = false;
                EndFixPoint.Visible = true;
                EndFixPoint.Enabled = true;
                RouteAvailable = RouteStatuses.Available;
                ThisRouteStatus.Available = RouteStatuses.Available;
            }
            else
            {
                StartFixPoint.Visible = true;
                StartFixPoint.Enabled = true;
                EndFixPoint.Visible = false;
                EndFixPoint.Enabled = false;
                RouteAvailable = RouteStatuses.Blocked;
                ThisRouteStatus.Available = RouteStatuses.Blocked;
            }
        }

        private void UpdateSide()
        {
            if (Configuration.StationType == PalletCraneStationTypes.PickStation)
            {
                if (Configuration.Side == PalletCraneStationSides.Left)
                {
                    LocalYaw = -(float)Math.PI / 2;
                }
                else
                {
                    LocalYaw = (float)Math.PI / 2;
                }
            }
            else
            {
                if (Configuration.Side == PalletCraneStationSides.Left)
                {
                    LocalYaw = (float)Math.PI / 2;
                }
                else
                {
                    LocalYaw = -(float)Math.PI / 2;
                }
            }       
        }

    }

    public enum PalletCraneStationTypes
    {
        PickStation,
        DropStation
    }

    public enum PalletCraneStationSides
    {
        Left,
        Right
    }
}