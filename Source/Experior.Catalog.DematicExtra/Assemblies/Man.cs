using System;
using System.Xml.Serialization;
using Experior.Core.Parts;
using Experior.Catalog.Extras;

namespace Experior.Catalog.Assemblies.Extra
{
    public class Man : Core.Assemblies.Assembly
    {
        Core.Parts.Mesh man;

        public Man(ManInfo info)
            : base(info)
        {
            man = new Mesh(Common.Meshes.Get("Man1.x"));
            Add(man);
            man.LocalPosition = new Microsoft.DirectX.Vector3(0, man.Height / 2, 0);
        }

        public override string Category
        {
            get { return "Man"; }
        }

        public override System.Drawing.Image Image
        {
            get
            {
                return Common.Icons.Get("Man1");
            }
        }
   
    }

    [Serializable]
    [XmlInclude(typeof(ManInfo))]
    public class ManInfo : Core.Assemblies.AssemblyInfo
    {     

    }
}