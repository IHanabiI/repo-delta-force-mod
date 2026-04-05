using System;

namespace RepoDeltaForceMod;

internal static class ValuableEffectProfileRouter
{
    internal static ValuableEffectProfile ResolveForGrab(object grabber, GrabObservationSnapshot snapshot)
    {
        var grabbedObject = ObservationReflection.TryGetKnownValue(grabber, "grabbedObject");
        if (FlightRecorderIdentity.IsOfficialFlightRecorder(grabbedObject, snapshot))
        {
            return ValuableEffectProfile.FlightRecorderOfficial("matched authored Havoc flight recorder identity");
        }

        if (MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(grabbedObject, snapshot))
        {
            return ValuableEffectProfile.MilitaryTerminalOfficial("matched authored Havoc military terminal identity");
        }

        return ValuableEffectProfile.Disabled(
            snapshot.IsValuableLike
                ? "ordinary valuable proxy testing retired"
                : "not a valuable candidate");
    }

    internal static ValuableEffectProfile ResolveForObject(object? value)
    {
        if (FlightRecorderIdentity.IsOfficialFlightRecorder(value))
        {
            return ValuableEffectProfile.FlightRecorderOfficial("matched authored Havoc flight recorder identity");
        }

        if (MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(value))
        {
            return ValuableEffectProfile.MilitaryTerminalOfficial("matched authored Havoc military terminal identity");
        }

        return ValuableEffectProfile.Disabled("ordinary valuable proxy testing retired");
    }
}

internal sealed class ValuableEffectProfile
{
    private ValuableEffectProfile(
        string effectId,
        string hudTitle,
        string logLabel,
        bool enablesRadarPrototype,
        bool isOrdinaryValuableFallback,
        string decisionReason)
    {
        EffectId = effectId;
        HudTitle = hudTitle;
        LogLabel = logLabel;
        EnablesRadarPrototype = enablesRadarPrototype;
        IsOrdinaryValuableFallback = isOrdinaryValuableFallback;
        DecisionReason = decisionReason;
    }

    internal string EffectId { get; }
    internal string HudTitle { get; }
    internal string LogLabel { get; }
    internal bool EnablesRadarPrototype { get; }
    internal bool IsOrdinaryValuableFallback { get; }
    internal string DecisionReason { get; }
    internal bool IsOfficialMilitaryTerminal => string.Equals(EffectId, MilitaryTerminalIdentity.StableId, StringComparison.Ordinal);
    internal bool IsOfficialFlightRecorder => string.Equals(EffectId, FlightRecorderIdentity.StableId, StringComparison.Ordinal);
    internal bool IsOfficialHavocSupply => IsOfficialMilitaryTerminal || IsOfficialFlightRecorder;

    internal string GetHudTitle()
    {
        return EffectId switch
        {
            var effectId when string.Equals(effectId, FlightRecorderIdentity.StableId, StringComparison.Ordinal)
                => "飞行记录仪",
            var effectId when string.Equals(effectId, MilitaryTerminalIdentity.StableId, StringComparison.Ordinal)
                => "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef",
            "military-terminal-proxy" => "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef\uff08\u6d4b\u8bd5\uff09",
            _ => HudTitle,
        };
    }

    internal static ValuableEffectProfile Disabled(string reason)
    {
        return new ValuableEffectProfile(
            effectId: "disabled",
            hudTitle: string.Empty,
            logLabel: "disabled",
            enablesRadarPrototype: false,
            isOrdinaryValuableFallback: false,
            decisionReason: reason);
    }

    internal static ValuableEffectProfile SpecialValuable(string reason)
    {
        return new ValuableEffectProfile(
            effectId: "special-valuable-reserved",
            hudTitle: string.Empty,
            logLabel: "special-valuable-reserved",
            enablesRadarPrototype: false,
            isOrdinaryValuableFallback: false,
            decisionReason: reason);
    }

    internal static ValuableEffectProfile MilitaryTerminalOfficial(string reason)
    {
        return new ValuableEffectProfile(
            effectId: MilitaryTerminalIdentity.StableId,
            hudTitle: "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef",
            logLabel: "military terminal",
            enablesRadarPrototype: true,
            isOrdinaryValuableFallback: false,
            decisionReason: reason);
    }

    internal static ValuableEffectProfile MilitaryTerminalProxy(string reason)
    {
        return new ValuableEffectProfile(
            effectId: "military-terminal-proxy",
            hudTitle: "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef\uff08\u6d4b\u8bd5\uff09",
            logLabel: "military terminal proxy",
            enablesRadarPrototype: true,
            isOrdinaryValuableFallback: true,
            decisionReason: reason);
    }

    internal static ValuableEffectProfile FlightRecorderOfficial(string reason)
    {
        return new ValuableEffectProfile(
            effectId: FlightRecorderIdentity.StableId,
            hudTitle: "飞行记录仪",
            logLabel: "flight recorder",
            enablesRadarPrototype: false,
            isOrdinaryValuableFallback: false,
            decisionReason: reason);
    }
}
