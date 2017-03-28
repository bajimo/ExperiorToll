using Experior.Core.Assemblies;
using Experior.Catalog.Assemblies.Extra;


namespace Experior.Catalog.Extras
{

    internal class Common
    {
        public static Experior.Core.Resources.Meshes Meshes;
        public static Experior.Core.Resources.Icons Icons;
    }

    public class ConstructAssembly
    {

        public static Assembly CreateAssembly(string type, string subtitle)
        {

            if (type == "KUKA KR180")
            {
                KUKAKR180Info info = new KUKAKR180Info();

                info.name = Experior.Core.Assemblies.Assembly.GetValidName("KUKA KR180 ");

                return new KUKAKR180(info);
            }
            else if (type == "Man1" || type == "Man2")
            {
                GraphicsInfo info = new GraphicsInfo();
                info.name = Experior.Core.Assemblies.Assembly.GetValidName(type + " ");
                info.GraphicsName = type;
                info.Category = "Man";
                info.height = 0;

                return new Graphics(info);
            }
            else if (type == "Text")
            {
                Catalog.Logistic.Basic.TextBitmapInfo info = new Logistic.Basic.TextBitmapInfo();
                info.name = Experior.Core.Assemblies.Assembly.GetValidName(type + " ");
                return new Catalog.Logistic.Basic.TextBitmap(info);
            }
            else if (type == "3D Text")
            {
                TextLabel3DInfo tInfo = new TextLabel3DInfo();
                tInfo.name = Experior.Core.Assemblies.Assembly.GetValidName(type + " ");
                return new TextLabel3D(tInfo);
            }
            else if (type == "Logo")
            {
                DematicLogoInfo logoinfo = new DematicLogoInfo();
                logoinfo.name = Experior.Core.Assemblies.Assembly.GetValidName("DematicLogo ");
                return new Dematic_Logo(logoinfo);
            }
            else if (type == "Box")
            {
                DematicBoxInfo boxInfo = new DematicBoxInfo();
                boxInfo.name = Experior.Core.Assemblies.Assembly.GetValidName("Box ");
                return new DematicBox(boxInfo);
            }
            else if (type == "Image")
            {
                GraphicsInfo ImageInfo = new GraphicsInfo();
                ImageInfo.GraphicsName = @"C:\tesco logo.png";
                return new Graphics(ImageInfo);
            }

            return null;
        }
    }
}

