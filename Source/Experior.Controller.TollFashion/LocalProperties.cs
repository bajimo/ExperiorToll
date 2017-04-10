using System.ComponentModel;

namespace Experior.Controller.TollFashion
{
    enum IdentificationMode
    {
        Normal,
        Manual,
        Enhanced
    }

    enum ReplenStationMode
    {
        Replenishment,
        Reverse
    }

    class LocalProperties
    {
        private IdentificationMode _iDMode = IdentificationMode.Normal;
        [DisplayName("ID Station Mode")]
        [Description("Decide what mode the Identification Stations at CCRE01NP01 should be working in")]
        public IdentificationMode iDMode
        {
            get { return _iDMode; }
            set { _iDMode = value; }
        }
    }
}
