using System;
using System.Xml.Serialization;
using Experior.Core.Parts;
using Experior.Catalog.Extras;

namespace Experior.Catalog.Assemblies.Extra
{
    public class Graphics : Core.Assemblies.Assembly
    {
        Core.Parts.Mesh graphics;

        public Graphics(GraphicsInfo info)
            : base(info)
        {
            graphics = new Core.Parts.Mesh(Common.Meshes.Get(info.GraphicsName));

            Add(graphics);
            graphics.LocalPosition = new Microsoft.DirectX.Vector3(0, graphics.Height / 2, 0);
        }



        public override System.Drawing.Image Image
        {
            get
            {
                return Common.Icons.Get(((GraphicsInfo)Info).GraphicsName);
            }
        }
        //public override string Category
        //{
        //    get { return ((GraphicsInfo)Info).Category; }
        //}
        public override string Category
        {
            get { return "Extra"; }
        }

    }

    [Serializable]
    [XmlInclude(typeof(GraphicsInfo))]
    public class GraphicsInfo : Core.Assemblies.AssemblyInfo
    {
        public string GraphicsName;
        public string Category;
    }
}