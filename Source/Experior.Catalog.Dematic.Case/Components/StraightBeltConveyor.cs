using Experior.Catalog.Dematic.Case.Devices;
using Experior.Core.Assemblies;
using Experior.Core.Loads;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using Experior.Dematic.Base.Devices;
using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class StraightBeltConveyor : StraightConveyor, IBeltControl
    {
        public StraightBeltConveyorInfo straightBeltInfo;
        public BeltControl beltControl;
        
        #region Constructors

        public StraightBeltConveyor(StraightBeltConveyorInfo info): base(info)
        {
            straightBeltInfo = info;
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
        public virtual string LineReleasePhotocellName
        {
            get { return straightBeltInfo.LineReleasePhotocellName; }
            set
            {
                straightBeltInfo.LineReleasePhotocellName = value;

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

        [Category("Belt Configuration")]
        [DisplayName("Release Delay")]
        [Description("Set a release time delay to control the flow of loads through the conveyor, delay in seconds, if set to '0' then there will be no delay")]
        [PropertyAttributesProvider("DynamicPropertyScriptRelease")]

        public float ReleaseDelay
        {
            get { return straightBeltInfo.ReleaseDelay; }
            set { straightBeltInfo.ReleaseDelay = value; }
        }

        [Category("Belt Configuration")]
        [DisplayName("Load Waiting Delay")]
        [Description("Set a load waiting time delay to control the flow of loads through the conveyor, delay in seconds, if set to '0' then there will be no delay")]
        [PropertyAttributesProvider("DynamicPropertyScriptRelease")]

        public float LoadWaitingDelay
        {
            get { return straightBeltInfo.LoadWaitingDelay; }
            set { straightBeltInfo.LoadWaitingDelay = value; }
        }


        [Category("Belt Configuration")]
        [DisplayName("Script Release")]
        [Description("if this is set true then the load will wait on the line release photocell and should be released by the routing script")]
        public bool ScriptRelease
        {
            get { return straightBeltInfo.ScriptRelease; }
            set { straightBeltInfo.ScriptRelease = value; }
        }

        public void DynamicPropertyScriptRelease(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = straightBeltInfo.ScriptRelease == false;
        }

        public void ReleaseWaitingLoad(Load load)
        {
            beltControl.EnableRelease(load);
        }
        #endregion

    }

    [Serializable]
    [XmlInclude(typeof(StraightBeltConveyorInfo))]
    public class StraightBeltConveyorInfo : StraightConveyorInfo
    {
        //Specific case straight belt conveyor info to be added here
        public string LineReleasePhotocellName = "LineRelease";
        public float ReleaseDelay = 0;
        public float LoadWaitingDelay = 0;
        public bool ScriptRelease = false;
    }
}
