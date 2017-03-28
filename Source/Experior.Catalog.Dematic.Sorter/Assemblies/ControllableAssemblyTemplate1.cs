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
    public class ControllableAssemblyTemplate1 : Assembly, IControllable
    {
        MyAssemblyInfo myAssemblyInfo;

        public ControllableAssemblyTemplate1(MyAssemblyInfo info)
            : base(info)
        {
            myAssemblyInfo = info;
            ControllerProperties = StandardCase.SetMHEControl(info, this);
        }

        //public override void Scene_OnLoaded()
        //{
        //    base.Scene_OnLoaded();
        //    if (ControllerProperties == null)
        //    {
        //        ControllerProperties = StandardCase.SetMHEControl(myAssemblyInfo, this);
        //    }
        //    UpdateConveyor();
        //}

        #region Properties

        public override string Category
        {
            get { return "ControllableAssemblyTemplate1"; }
        }

        public override Image Image
        {
            get { return Common.Icons.Get("ControllableAssemblyTemplate1"); }
        }

        #endregion

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
                controller = value;
                if (controller != null)
                {   //If the PLC is deleted then any conveyor referencing the PLC will need to remove references to the deleted PLC.
                    controller = value;
                    controller.OnControllerDeletedEvent += controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent += controller_OnControllerRenamedEvent;
                }
                else if (controller != null && value == null)
                {
                    controller.OnControllerDeletedEvent -= controller_OnControllerDeletedEvent;
                    controller.OnControllerRenamedEvent -= controller_OnControllerRenamedEvent;
                    controller = value;
                }
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
            ControllerName = ((Experior.Core.Assemblies.Assembly)sender).Name;
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