using System;
using Experior.Dematic.Storage.Base;
using Experior.Dematic.Base;
using Experior.Catalog.Assemblies;

namespace Experior.Catalog.Dematic.Storage.Assemblies
{
    /// <summary>
    /// Interface between multishuttle object and controller object
    /// </summary>
    public interface IMultiShuttleController
    {
        string Name { get; }
        void Build();
        void Clear();
        void Reset();
        void RemoveMultiShuttle(MultiShuttle multishuttle);  
        void AddMultiShuttle(MultiShuttle multishuttle);
        void ToteArrivedAtConvDropStation(DropStationConveyor dropStationPoint, Elevator elevator, Case_Load caseload, MultiShuttle multishuttle);
        void ToteArrivedAtConvElevatorLocation1(Experior.Core.Routes.ActionPoint sender, Experior.Core.Loads.Load load, Elevator elevator);
        void StartElevatorOutfeedAndSendArrival(Elevator elevator);
        void StartElevatorInfeedAndSendArrival(Elevator elevator);
        void StartSecondJob(Elevator elevator);
        void InfeedTimeOut(Case_Load caseload2, Case_Load caseload1);
        void ToteArrivedAtConvPickStationLocation2(Experior.Core.Routes.ActionPoint sender, Experior.Core.Loads.Load load, MultiShuttle multishuttle);
        void ToteArrivedAtRackConvLocation1(Experior.Core.Routes.ActionPoint sender, Experior.Core.Loads.Load load, MultiShuttle multishuttle);
        void ToteArrivedAtRackConvLocation2(Experior.Core.Routes.ActionPoint sender, Experior.Core.Loads.Load load, MultiShuttle multishuttle);
    }
}
