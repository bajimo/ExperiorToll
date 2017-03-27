using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Assemblies;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class CurveBeltConveyor : CurveConveyor, IBeltControl
    {
        public CurveBeltConveyorInfo curveBeltInfo;
        private BeltControl beltControl;

        #region Constructors

        public CurveBeltConveyor(CurveBeltConveyorInfo info): base(info)
        {
            curveBeltInfo = info;
            beltControl = new BeltControl(this);
        }

        public override void Scene_OnLoaded()
        {
            base.Scene_OnLoaded();
            beltControl.Scene_OnLoaded();
            Reset();
        }

        public override void Dispose()
        {
            beltControl.Dispose();
            base.Dispose();
        }

        public override void Reset()
        {
            base.Reset();
            beltControl.Reset();
        }

        public override void NextRouteStatus_OnAvailableChanged(object sender, RouteStatusChangedEventArgs e)
        {
            beltControl.NextRouteStatus_OnAvailableChanged(sender, e);
        }

        #endregion

        #region User Interface
        [Category("Status")]
        [DisplayName("Available")]
        [Description("Is this conveyor route available to be released into")]
        [ReadOnly(true)]
        public override RouteStatuses RouteAvailable
        {
            get { return _RouteAvailable; }
            set
            {
                if (value != _RouteAvailable)
                {
                    _RouteAvailable = value;
                    beltControl.SetRouteAvailable(value);
                }
            }
        }

        [Category("Belt Configuration")]
        [DisplayName("Line Release Photocell")]
        [TypeConverter(typeof(PhotocellConverter))]
        public string LineReleasePhotocellName
        {
            get { return curveBeltInfo.LineReleasePhotocellName; }
            set
            {
                curveBeltInfo.LineReleasePhotocellName = value;

                foreach (Assembly assembly in Assemblies)
                {
                    if (assembly.Name == value)
                    {
                        beltControl.LineReleaseEvents(false);
                        beltControl.LineReleasePhotocell = assembly as CasePhotocell;
                    }
                }

                Core.Environment.Properties.Refresh();
            }
        }

        [Browsable(false)]
        public float ReleaseDelay
        {
            get; set;
        }

        [Browsable(false)]
        public float LoadWaitingDelay
        {
            get; set;
        }

        [Browsable(false)]
        public bool ScriptRelease
        {
            get; set;
        }


        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(CurveBeltConveyorInfo))]
    public class CurveBeltConveyorInfo : CurveConveyorInfo
    {
        //Specific case Curve belt conveyor info to be added here
        public string LineReleasePhotocellName = "LineRelease";
    }
}
