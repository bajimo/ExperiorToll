using Experior.Core.Routes;

namespace Experior.Dematic.Base.Devices
{
    public class DematicActionPoint : ActionPoint
    {
        public DematicActionPoint()
        {
            
        }
        public string LocName;

        public override string ToString()
        {
            return LocName;
        }
    }
}
