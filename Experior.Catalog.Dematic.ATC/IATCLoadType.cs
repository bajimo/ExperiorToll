using Dematic.ATC;
using Experior.Core.Routes;
using System.Collections.Generic;
using System.Drawing;

namespace Experior.Catalog
{
    public interface IATCLoadType
    {
        Dictionary<string, string> ProjectFields { get; set; }
        string TUIdent { get; set; }
        string TUType { get; set; }
        string Source { get; set; }
        string Destination { get; set; }
        string PresetStateCode { get; set; }
        string Location { get; set; }
        Color Color { get; set; }
        float Length { get; }
        float Width { get; }
        float Height { get; }
        float Weight { get; set; }
        float Yaw { get; set; }
        bool Stopped { get; }
        Route Route { get; }
        string GetPropertyValueFromEnum(TelegramFields field);
        void ReleaseLoad();
        object UserData { get; set; }
        string Identification { get; set; }
        void Stop();
        void StopLoad_WCSControl();
        void ReleaseLoad_WCSControl();
        void StopLoad_PLCControl();
        void ReleaseLoad_PLCControl();
        bool LoadWaitingForWCS { get; set; }
        bool LoadWaitingForPLC { get; set; }
        void Dispose();
    }
}
