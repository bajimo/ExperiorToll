using Experior.Catalog.Dematic.Case;

namespace Experior.Catalog.Dematic.ATC
{
    public interface IATCCaseLoadType : IATCLoadType
    {
        float CaseWeight { get; set; }
        int SortID { get; set; }
        int SortSequence { get; set; }
        string SortInfo { get; set; }
        int DropIndex { get; set; }
        string ExceptionHeight { get; set; }
        string ExceptionWeight { get; set; }
        void SetYaw(float convWidth, CaseOrientation caseOrientation);
    }
}
