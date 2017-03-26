//using Experior.Catalog.Dematic.Case;
using Experior.Core.Assemblies;
using Experior.Core.Properties;
using Experior.Dematic.Base;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Serialization;

namespace Experior.Catalog.Assemblies
{
    public class Controllable_Assembly_Template : Assembly, IControllable
    {
        MyAssemblyInfo myAssemblyInfo;

        public Controllable_Assembly_Template(MyAssemblyInfo info) : base(info)
        {
            myAssemblyInfo = info;
        }

        public override void Scene_OnLoaded()
        {
            if (ControllerProperties == null)
            {
                ControllerProperties = StandardCase.SetMHEControl(myAssemblyInfo, this);
            }
            //base.Scene_OnLoaded();
            //UpdateConveyor();
        }

        #region Properties

        public override string Category
        {
            get { return "Controllable_Assembly_Template"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("Controllable_Assembly_Template"); }
        }

        #endregion


        //If a assembly can be set to local control then use this property to set how the component is controlled.
        //[Category("Routing")]
        //[DisplayName("Control Type")]
        //[Description("Defines if the control is handled by a controller, by a routing script or uses only local control. ")]
        //[PropertyOrder(1)]
        //public ControlTypes ControlType
        //{
        //    get
        //    {
        //        return myAssemblyInfo.ControlType;
        //    }
        //    set
        //    {
        //        myAssemblyInfo.ControlType = value;
        //        if (ControllerProperties != null && value != ControlTypes.Controller)
        //        {
        //            ControllerName = "No Controller";
        //        }
        //        Core.Environment.Properties.Refresh();
        //    }
        //}

        #region IControllable
        private MHEControl controllerProperties;
        private IController controller;

        [Browsable(false)]
        public IController Controller
        {
            get
            {
                return controller;
            }
            set
            {
                if (value != null)
                {   //If the PLC is deleted then any conveyor referencing the PLC will need to remove references to the deleted PLC.
                    value.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    value.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                else if (controller != null && value == null)
                {
                    controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent -= controller_OnControllerRenamedEvent;
                }
                controller = value;
                Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Control")]
        [Description("Embedded routing control with protocol and routing specific configuration")]
        [PropertyOrder(21)]
        [PropertyAttributesProvider("DynamicPropertyAssemblyPLCconfig")]
        public MHEControl ControllerProperties
        {
            get { return controllerProperties; }
            set
            {
                controllerProperties = value;
                if (value == null)
                {
                    Controller = null;
                }
                Experior.Core.Environment.Properties.Refresh();
            }
        }

        [Category("Routing")]
        [DisplayName("Controller")]
        [Description("Controller name that handles this conveyor")]
        [TypeConverter(typeof(CaseControllerConverter))]
        public string ControllerName
        {
            get
            {
                return myAssemblyInfo.ControllerName;
            }
            set
            {
                if (!value.Equals(myAssemblyInfo.ControllerName))
                {
                    ControllerProperties = null;
                    myAssemblyInfo.ProtocolInfo = null;
                    Controller = null;
                }

                myAssemblyInfo.ControllerName = value;
                if (value != null)
                {
                    //Note StandardCase.SetMHEControl will set the ControllerProperties and Controller properties!
                    ControllerProperties = StandardCase.SetMHEControl(myAssemblyInfo, this);
                    if (ControllerProperties == null)
                    {
                        myAssemblyInfo.ControllerName = "No Controller";
                    }
                }
            }
        }

        #endregion

        public void controller_OnControllerDeletedEvent(object sender, EventArgs e)
        {
            controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
            ControllerName = "No Controller";
            Controller = null;
            myAssemblyInfo.ProtocolInfo = null;
        }

        void controller_OnControllerRenamedEvent(object sender, EventArgs e)
        {            
            myAssemblyInfo.ControllerName = ((Assembly)sender).Name;
            Core.Environment.Properties.Refresh();
        }

        public void DynamicPropertyAssemblyPLCconfig(PropertyAttributes attributes)
        {
            attributes.IsBrowsable = Controller != null;
        }
    }

    [Serializable]
    [XmlInclude(typeof(MyAssemblyInfo))]
    public class MyAssemblyInfo : Experior.Core.Assemblies.AssemblyInfo, IControllableInfo
    {
        public ControlTypes ControlType;

        #region Fields

        private static MyAssemblyInfo properties = new MyAssemblyInfo();

        #endregion

        #region Properties

        public static object Properties
        {
            get
            {
                properties.color = Experior.Core.Environment.Scene.DefaultColor;
                return properties;
            }
        }

        #endregion

        #region IControllableInfo

        private string controllerName = "No Controller";
        public string ControllerName
        {
            get { return controllerName; }
            set { controllerName = value; }
        }

        private ProtocolInfo protocolInfo;
        public ProtocolInfo ProtocolInfo
        {
            get { return protocolInfo; }
            set { protocolInfo = value; }
        }

        #endregion

    }
}