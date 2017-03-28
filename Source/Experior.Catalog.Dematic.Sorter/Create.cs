using Experior.Catalog.Dematic.Sorter.Assemblies;
using Experior.Catalog.Dematic.Sorter.Assemblies.Chute;
using Experior.Catalog.Dematic.Sorter.Assemblies.Induction;
using Experior.Catalog.Dematic.Sorter.Assemblies.SorterController;
using Experior.Core;
using Experior.Core.Assemblies;

namespace Experior.Catalog.Dematic.Sorter
{
    public static class Create
    {
        public static Assembly SorterStraight(string title, string subtitle, object properties)
        {
            var info = new SorterElementInfo { name = Assembly.GetValidName("Sorter Straight ") };
            return new SorterElementStraight(info);
        }

        public static Assembly SorterCurve(string title, string subtitle, object properties)
        {
            if (subtitle == "Clockwise")
            {
                var info = new SorterElementInfo
                {
                    Revolution = Environment.Revolution.Clockwise,
                    name = Assembly.GetValidName("Sorter Curve ")
                };
                return new SorterElementCurve(info);
            }
            else
            {
                var info = new SorterElementInfo
                {
                    Revolution = Environment.Revolution.Counterclockwise,
                    name = Assembly.GetValidName("Sorter Curve ")
                };
                return new SorterElementCurve(info);
            }
        }

        public static Assembly SorterInduction(string title, string subtitle, object properties)
        {
            var info = new SorterInductionInfo { name = Assembly.GetValidName("Induction "), InductionDisctance = 1 };
            return new SorterInduction(info);
        }

        public static Assembly SorterChute(string title, string subtitle, object properties)
        {
            var info = new SorterChuteInfo { name = Assembly.GetValidName("Chute ") };
            return new SorterChute(info);
        }

        public static Assembly SorterController(string title, string subtitle, object properties)
        {
            var info = new SorterControllerExampleInfo { name = Assembly.GetValidName("Sorter controller ") };
            return new SorterControllerExample(info);
        }
    }
}