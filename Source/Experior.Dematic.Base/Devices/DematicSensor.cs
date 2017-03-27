using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Core.Routes;

namespace Experior.Dematic.Base.Devices
{
    [Serializable]
    [XmlInclude(typeof(DematicSensor))]
    [TypeConverter(typeof(ObjectConverter))]
    public class DematicSensor : DematicActionPoint
    {
        private Load currentload;
        private Load previousload;
        public ActionPoint leaving;

        public DematicSensor()
        {
            base.Visible = false;
            base.Color = Color.Red;
            base.Edge = Edges.Leading;
            base.OnEnter += Entering;
            OnRemoved += Sensor_Removed;
            leaving = new ActionPoint { Edge = Edges.Trailing };
            leaving.Visible = false;
            leaving.Color = Color.Blue;
            leaving.OnEnter += Leaving;
            OnEnabledChanged += Sensor_OnEnabledChanged;
        }

        public delegate void SensorEvent(DematicSensor sender, Load load);

        [field: NonSerialized]
        new public event SensorEvent OnEnter;

        [field: NonSerialized]
        public event SensorEvent OnLeave;

        public override bool Active
        {
            get
            {
                return currentload != null;
            }
        }

        public override Load ActiveLoad
        {
            get
            {
                return currentload;
            }
        }

        /// <summary>
        /// Is the load waiting on the entry action point i.e. waiting to be released
        /// </summary>
        public bool LoadWaiting
        {
            get
            {
                return base.Active;
            }
        }


        /// <summary>
        /// Only true if the previous load has not left the sensor before the next one arrives
        /// Possible if two loads are touching and the enter and leaving events are in the wrong
        /// order
        /// </summary>
        public bool PreviousActive
        {
            get
            {
                return previousload != null;
            }
        }

        public Load PreviousActiveLoad
        {
            get
            {
                return previousload;
            }
        }

        [XmlIgnore]
        [Browsable(false)]
        public override Edges Collision
        {
            get
            {
                return base.Collision;
            }
            set
            {
            }
        }

        [Browsable(false)]
        public override Color Color
        {
            get
            {
                return base.Color;
            }
            set
            {

            }
        }

        public override float Distance
        {
            get
            {
                return base.Distance;
            }
            set
            {
                base.Distance = value;
                leaving.Distance = value;
            }
        }

        /// <summary>
        /// Gets or sets the edge.
        /// </summary>
        /// <value>The edge.</value>
        [Browsable(false)]
        public override Edges Edge
        {
            get
            {
                return base.Edge;
            }
            set
            {

            }
        }

        [XmlIgnore]
        [Browsable(false)]
        public override Image Image
        {
            get
            {
                return Common.Icons.Get("photoeye");

            }
        }

        [XmlIgnore]
        public override Route Parent
        {
            get
            {
                return base.Parent;
            }
            protected set
            {
                base.Parent = value;

                if (leaving.Parent != null)
                {
                    leaving.Parent.RemoveActionPoint(leaving);
                }

                if (value != null)
                {
                    value.InsertActionPoint(leaving, Distance - 0.01f);
                }
            }
        }

        //[XmlIgnore]
        //[Browsable(false)]
        //public override StoppingMode StopLoadModeEvent
        //{
        //    get
        //    {
        //        return base.StopLoadModeEvent;
        //    }
        //    set
        //    {

        //    }
        //}

        void Sensor_OnEnabledChanged(ActionPoint sender)
        {
            leaving.Enabled = Enabled;
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public override void Dispose()
        {
            base.OnEnter -= Entering;
            base.Dispose();
            leaving.OnEnter -= Leaving;

            if (currentload != null)
            {
                currentload.OnDisposed -= CurrentLoadDisposed;
                ClearCurrentLoadRemovedEvent(currentload.Route);
            }

            currentload = null;

            leaving.Dispose();

            OnRemoved -= Sensor_Removed;

            OnEnabledChanged -= Sensor_OnEnabledChanged;
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public override void Reset()
        {
            if (currentload != null)
            {
                currentload.OnDisposed -= CurrentLoadDisposed;
                ClearCurrentLoadRemovedEvent(currentload.Route);
            }

            currentload = null;
            previousload = null;
            base.Reset();
        }

        /// <summary>
        /// If switching a load but it still covers this photocell, then force the load to clear this photocell first
        /// </summary>
        /// <param name="load">The load being switched</param>
        public void ForceLoadClear(Load load)
        {
            if (load == currentload)
            {
                Leaving(leaving, load);
            }
        }

        public override string ShowHelp()
        {
            return string.Empty;
        }

        private void CurrentLoadDisposed(Load load)
        {
            Leaving(leaving, load);
        }

        void CurrentLoadRemoved(Route sender, Load load)
        {
            //Only loads that are stopped on the sensor and are swicthed will be set as leaving
            if (currentload == load && (load.Stopped || !sender.Motor.Running))
            {
                Leaving(leaving, load);
            }
        }

        private void ClearCurrentLoadRemovedEvent(Route route)
        {
            if (route != null)
            {
                route.OnLoadRemoved -= CurrentLoadRemoved;
            }
        }

        private void Entering(ActionPoint sender, Load load)
        {
            if (currentload != null)
            {
                previousload = currentload;
            }

            if (currentload != load)
            {
                currentload = load;

                load.OnDisposed += CurrentLoadDisposed;
                if (load.Route != null)
                    load.Route.OnLoadRemoved += CurrentLoadRemoved; //This will occur when the load is switched to another AP or Route

                OnEnter?.Invoke(this, load);
            }
        }

       
        private void Leaving(ActionPoint sender, Load load)
        {
            load.OnDisposed -= CurrentLoadDisposed;
            ClearCurrentLoadRemovedEvent(load.Route);

            OnLeave?.Invoke(this, load);

            if (previousload != null && previousload.Equals(load))
                previousload = null;

            if (currentload != null && currentload.Equals(load))
                currentload = null;
        }

        public void StopActiveLoads()
        {
            if (ActiveLoad != null)
                ActiveLoad.Stop();

            if (PreviousActiveLoad != null)
                PreviousActiveLoad.Stop();
        }

        public void ReleaseActiveCaseLoads()
        {
            if (ActiveLoad != null)
                ((Case_Load)ActiveLoad).ReleaseLoad();

            if (PreviousActiveLoad != null)
                ((Case_Load)PreviousActiveLoad).Release();
        }

        public void ReleaseActivePalletLoads()
        {
            if (ActiveLoad != null)
                ((EuroPallet)ActiveLoad).ReleaseLoad();

            if (PreviousActiveLoad != null)
                ((EuroPallet)PreviousActiveLoad).Release();
        }

        private void Sensor_Removed(ActionPoint sender, Route route)
        {
            route.RemoveActionPoint(leaving);
        }
        
    }
}