using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Xml.Serialization;
using Experior.Core.Loads;
using Experior.Core.Routes;
using Experior.Dematic.Base;

namespace Experior.Catalog.Dematic.Case.Components
{
    public sealed class GravityConveyor : StraightConveyor
    {
        private ActionPoint entry, exit;
        private GravityConveyorInfo info;

        public GravityConveyor(GravityConveyorInfo info) : base(info)
        {
            this.info = info;
            entry = TransportSection.Route.InsertActionPoint(0);
            exit = TransportSection.Route.InsertActionPoint(Length);
            entry.OnEnter += Entry_OnEnter;
            exit.OnEnter += Exit_OnEnter;
        }

        public override void UpdateLength(float length)
        {
            base.UpdateLength(length);
            exit.Distance = Length;
        }

        private void Exit_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            load.OnDisposed -= Load_OnDisposed;
            Core.Environment.Invoke(SetAvailable);
        }

        private void Entry_OnEnter(ActionPoint sender, Core.Loads.Load load)
        {
            load.OnDisposed += Load_OnDisposed;
            SetAvailable();
        }

        private void Load_OnDisposed(Load load)
        {
            load.OnDisposed -= Load_OnDisposed;
            SetAvailable();
        }

        private void SetAvailable()
        {
            HashSet<Load> loads = new HashSet<Load>(TransportSection.Route.Loads);
            if (exit.Active)
                loads.Remove(exit.ActiveLoad);
            if (entry.Active)
                loads.Add(entry.ActiveLoad);

            var fillLength = loads.Sum(l => l.OccupyingDistanceOnRoute * 2);
            var fillPercent = fillLength / Length * 100f;
            if (fillPercent >= FillPercent)
            {
                RouteAvailable = RouteStatuses.Blocked;
            }
            else
            {
                RouteAvailable = RouteStatuses.Available;
            }
        }

        public override void Reset()
        {
            Core.Environment.Invoke(SetAvailable);
            base.Reset();
        }

        [DisplayName("Fill Percent")]
        [Category("Configuration")]
        public float FillPercent
        {
            get { return info.FillPercent; }
            set
            {
                if (value < 0)
                    return;
                if (value > 100)
                    return;
                info.FillPercent = value;
            }
        }

        public override Image Image => Common.Icons.Get("RaRoller");
        public override string Category => "Gravity conveyor";
    }

    [Serializable]
    [XmlInclude(typeof(GravityConveyorInfo))]
    public class GravityConveyorInfo : StraightConveyorInfo
    {
        public float FillPercent { get; set; } = 85;
    }
}