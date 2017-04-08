using Experior.Core.Assemblies;
using Experior.Core.Routes;
using System;
using System.Collections.Generic;


namespace Experior.Dematic.Base
{
    /// <summary>
    /// All controllers (PLCs etc) need to implement this interface
    /// </summary>
    public interface IController
    {
        string Name { get; set; }
        MHEControl CreateMHEControl(IControllable assem, ProtocolInfo info);
        event EventHandler OnControllerDeletedEvent;
        event EventHandler OnControllerRenamedEvent;
        void RemoveFromRoutingTable(string barcode);
        Case_Load CreateCaseLoad(BaseCaseData caseData);
        EuroPallet CreateEuroPallet(BasePalletData baseData);
    }

    public interface ICaseController
    {
        string Name { get; set; }
        BaseCaseData GetCaseData();
    }

    public interface IMinloadController
    {
        BaseCaseData GetCaseData();
    }

    public interface IMultiShuttleController
    {
        BaseCaseData GetCaseData();
    }

    public interface IPalletController
    {
        string Name { get; set; }
        BasePalletData GetPalletData();
    }

    public interface IEmulationController
    {
        Case_Load GetCaseLoad(Route route, float position);

        Tray GetTray(Route route, float position, TrayStatus status);

        EuroPallet GetEuroPallet(Route route, float position, PalletStatus status);
    }

     
}
