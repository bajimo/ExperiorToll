using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Runtime.Serialization;
using System.Xml.Serialization;

using Experior.Core;
using Experior.Core.Assemblies;
using Experior.Core.Communication.PLC;
using Experior.Core.Loads;
using Experior.Core.Mathematics;
using Experior.Core.Motors;
using Experior.Core.Parts;
using Experior.Core.Properties;
using Experior.Core.Properties.TypeConverter;
using Experior.Core.Routes;

using Microsoft.DirectX;

namespace Experior.Catalog.Dematic.Case.Components
{
    public class LoadRotateConveyor : StraightBeltConveyor
    {
        #region Constructors
        LoadRotateConveyorInfo loadRotateConveyorInfo;
        ActionPoint apRotate;

        public LoadRotateConveyor(LoadRotateConveyorInfo info) : base(info)
        {
            loadRotateConveyorInfo = info;
            apRotate = TransportSection.Route.InsertActionPoint(info.rotateDistance);

            apRotate.OnEnter += ap_OnEnter;
        }

        void ap_OnEnter(ActionPoint sender, Load load)
        {
            load.Rotate(RotateTime, (float)Math.PI / 2, 0, 0);
        }

        #endregion

        #region Properties

        public override string Category
        {
            get { return "Rotation Conveyor"; }
        }

        [Category("Configuration")]
        [DisplayName("Rotate Time")]
        [Description("The time to rotate 90 degrees")]
        public float RotateTime 
        {
            get { return loadRotateConveyorInfo.rotateTime; }
            set
            { 
                loadRotateConveyorInfo.rotateTime = value;                
            } 
        }

        [Category("Configuration")]
        [DisplayName("Rotate Distance")]
        [Description("The point at which the load will start rotating")]
        public float RotateDistance
        {
            get { return loadRotateConveyorInfo.rotateDistance; }
            set
            {
                loadRotateConveyorInfo.rotateDistance = value;
                apRotate.Distance = value;
            }
        }

        #endregion
    }

    [Serializable]
    [XmlInclude(typeof(LoadRotateConveyorInfo))]
    public class LoadRotateConveyorInfo : StraightBeltConveyorInfo
    {
        public float rotateTime = 0.25f;
        public float rotateDistance = 1;
    }
}