using Experior.Dematic.Base;

namespace Experior.Catalog
{
    public interface IATCPalletLoadType : IATCLoadType
    {
        float PalletWeight { get; set; }
        void SetYaw(PalletConveyorType conveyorType);
        void AddLoad(float width, float height, float length);
    }
}
