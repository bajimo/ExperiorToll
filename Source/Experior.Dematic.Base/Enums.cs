using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Experior.Dematic.Base
{
    //Put all Standard Enums here!


    public enum ShuttleTaskTypes 
    {
        Shuffle,
        RackToConv,
        ConvToRack
    }

    public enum ElevatorTaskTypes
    {
        OutfeedToDS,
        

    }
    public enum CasePLC_State 
    { 
        Unknown, 
        Ready, 
        AutoNoMovement, 
        Auto, 
        NotReady 
    };

    public enum MultiShuttlePLC_State
    {
        Unknown_00,/*Program_Started_01,*/
        Ready_02,
        Auto_No_Move_03,
        Auto_04
    };

    public enum DivertRoute
    {
        None,
        Straight,
        Divert
    }

    public enum RouteBlocked
    {
        Route_To_Default, //Route the load to the default route. What happens when the default route is also blocked??
        Wait_Timeout,
        Wait_Until_Route_Available
    }

    public enum Direction
    {
        Straight,
        Left,
        Right,
        None,
    }

    public enum Side
    {
        Left = -1,
        Right = 1       
    }

    public enum ControlTypes
    {
        Local,
        Project_Script,        
        Controller
    }

    public enum CommControlTypes //Control types used for controller point (no Local)
    {
        Project_Script,
        Controller
    }


    public enum RackSide 
    { 
        Left = 'L',
        Right = 'R',
        NA = 'x'
    }

    public enum ConveyorTypes //Pick,Drop,Infeed Rack,Outfeed Rack, Elevator
    {
        Pick = 'P',
        Drop = 'D',
        InfeedRack = 'I',
        OutfeedRack = 'O',
        Elevator = 'E',
        BinLoc = 'R',
        NA = 'X'
    }

    public enum Modes
    {
        Divert,
        Merge,
        None
    }

    public enum ReleasePriority
    {
        Left,
        Right,
        FIFO
    }
    
    public enum TaskType
    {
        Infeed,
        Outfeed
    }

    public enum MultiShuttleDirections
    {
        Infeed = 'I',
        Outfeed = 'O'
    }

    public enum Cycle
    {
        Single, //Am i loading a single load, am i unloading as singles
        Double  //Am i loading two loads, am i offloading a pair
    }

    public enum ShuttleConvDirRef  //If loading from the RHS conv needs to travel from right to left, if Unloading to RHS con needs to travel from left to right.
    {
        Loading, 
        Unloading
    }

    public enum RouteStatuses
    {
        Blocked, Request, Available
    }

    public enum PalletConveyorType
    {
        Roller,
        Chain,
    }

    public enum PositionPoint
    {
        Start,
        End
    }

    public enum PhotocellState
    {
        LoadBlocked,//The photocell beam has been broken
        LoadClear,  //The photocell beam has been cleared
        Clear,      //The photocell beam is clear (after clear timeout)
        Blocked     //The photocell is blocked (after blocked timeout)
    }
    //The sequence is that the photocell will be covered as soon as the load enters the sensor, this will trigger the covered event.
    //After the blocked timeout has elapsed the photocell will become blocked, then once the load has moved out of the sensor after 
    //the clear timeout the photocell will become clear again. Note: That if the timeouts are set to 0 the photocell will only be in
    //the state blocked and clear, also the times will be null.

    public enum LoadDirection
    {
        Source,
        Destination
    }

    public enum MoveDirection
    {
        Forward,
        Backward
    }

    public enum TCycle
    {
        Load,
        Unload,
    }

    public enum PalletStatus
    {
        Empty,
        Loaded,
        Stacked
    }

    public enum TrayStatus
    {
        Empty,
        Loaded,
        Stacked
    }

    public enum CaseLoadType
    {
        Tray,
        TrayLoaded,
        TrayStack,
        Case,
        Auto
    }

    public enum LoadTypes
    {
        Tray,
        Case
    }
}
