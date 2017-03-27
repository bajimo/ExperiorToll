// Guids.cs
// MUST match guids.h
using System;

namespace Dematic.ControllableAssemTemplateInstall
{
    static class GuidList
    {
        public const string guidControllableAssemTemplateInstallPkgString = "62a77528-57b6-4430-8150-ef271a09c1d9";
        public const string guidControllableAssemTemplateInstallCmdSetString = "bde9c6ab-943b-44aa-bcae-d5d6a3d5343d";

        public static readonly Guid guidControllableAssemTemplateInstallCmdSet = new Guid(guidControllableAssemTemplateInstallCmdSetString);
    };
}