using System;
using System.Xml.Serialization;
using Experior.Core.Parts;
using Experior.Catalog.Extras;

namespace Experior.Catalog.Assemblies.Extra
{
    public class KUKAKR180 : Core.Assemblies.Assembly
    {
        Core.Parts.Model robot;

        public KUKAKR180(KUKAKR180Info info)
            : base(info)
        {
            robot = new Model(Common.Meshes.Get("KUKAKR180.dae"));
            robot.Height *= 0.001f;
            robot.Length *= 0.001f;
            robot.Width *= 0.001f;
            Add(robot);
        }

        public override string Category
        {
            get { return "Extra"; }
        }


        public override System.Drawing.Image Image
        {
            get
            {
                return Common.Icons.Get("KUKAKR180");
            }
        }
   
    }

    [Serializable]
    [XmlInclude(typeof(KUKAKR180Info))]
    public class KUKAKR180Info : Core.Assemblies.AssemblyInfo
    {     

    }
}