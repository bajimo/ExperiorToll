using System;
using System.Collections.Generic;
using System.Text;
using Experior.Core;
using Microsoft.DirectX;
using System.Drawing;
using Experior.Core.Assemblies;
using Experior.Catalog.Storage.SRM;
using Experior.Catalog.Dematic.Storage.Miniload.Assemblies;


namespace Experior.Catalog
{
    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class Create
    {
        internal static Assembly DematicMiniLoad1x2()
        {
            MiniloadInfo info = new MiniloadInfo();
            info.name = Experior.Core.Assemblies.Assembly.GetValidName("MiniLoad ");
            info.RailLength = 20;
            info.Craneheight = 10;
            info.LHDLength = 0.78f;
            info.LHDNumber = 2;
            info.LHDWidth = 0.5f;
            info.PdOffsetHeight = 5.55f;
            info.AisleWidth = 1.61f;
            info.PickAndDropSide = Miniload.PickDropSide.DropLeft_PickRight;

            return new Miniload(info);
        }
    }
}
