using Experior.Core.Assemblies;
using Experior.Core.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Experior.Dematic.Base
{

    public static class StandardCase
    {

        public static IProjectData DefaultSpecs;
        private static string projectDataKey = "pData";

        static StandardCase()
        {
            if (Assembly.Items.ContainsKey(projectDataKey))
            {
                Assembly pData = Experior.Core.Assemblies.Assembly.Items[projectDataKey] as Assembly;
                StandardCase.DefaultSpecs = pData.Info as IProjectData;
            }
        }

        /// <summary>
        /// Set up the PLC then set up the protocol that the PLC uses.
        /// Returns the controller and the controllerProperties (out MHEControl object) for the controller type and assembly
        /// The Info of the MHE Component must implement the IControllableInfo interface (a comment by BG)
        /// </summary>
        /// <param name="assemblyInfo"></param>
        /// <param name="assembly"></param>
        /// <returns>An MHE control object, if the controller doesnot exist (e.g. the string "No Controller") then null is returned</returns>
        public static MHEControl SetMHEControl(IControllableInfo assemblyInfo, IControllable assembly)
        {
            //BaseController controller = null;
            IController controller = null;
            MHEControl controllerProperties = null;

            if (!Assembly.Items.ContainsKey(assemblyInfo.ControllerName))
            {
                return null;
            }

            //controller = Assembly.Items[assemblyInfo.ControllerName] as BaseController;
            controller = Assembly.Items[assemblyInfo.ControllerName] as IController;

            if (controller != null && controllerProperties == null)
            {
                assemblyInfo.ControllerName = controller.Name;
                //We need to set the controller here so that it is set when the MHEControl is created otherwise it is null
                assembly.Controller = controller;

               // controllerProperties = BaseController.CreateMHEControlGeneric<>(assembly, assemblyInfo.ProtocolInfo);
                controllerProperties = controller.CreateMHEControl(assembly, assemblyInfo.ProtocolInfo) as MHEControl; //Create the correct type 
                if (controllerProperties != null)
                {
                    assemblyInfo.ProtocolInfo = controllerProperties.Info; //Save the protocol info so that it can be recreated             
                }
            }
            return controllerProperties;
        }

    }
}
