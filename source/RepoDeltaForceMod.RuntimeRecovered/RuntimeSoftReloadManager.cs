using System;
using BepInEx.Configuration;

namespace RepoDeltaForceMod;

internal static class RuntimeSoftReloadManager
{
    internal static void Initialize(ConfigFile config)
    {
    }

    internal static void Tick()
    {
    }

    internal static void Shutdown()
    {
    }

    internal static void MarkSubsystemDirty(string subsystemName, string reason)
    {
    }
}

internal readonly struct RuntimeSoftReloadContext
{
    internal RuntimeSoftReloadContext(int generation, string reason, DateTimeOffset triggeredAtUtc)
    {
        Generation = generation;
        Reason = reason;
        TriggeredAtUtc = triggeredAtUtc;
    }

    internal int Generation { get; }

    internal string Reason { get; }

    internal DateTimeOffset TriggeredAtUtc { get; }
}
