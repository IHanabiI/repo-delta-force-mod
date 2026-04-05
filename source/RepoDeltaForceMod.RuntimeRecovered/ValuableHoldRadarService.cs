using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RepoDeltaForceMod;

internal static class ValuableHoldRadarService
{
    private const string RuntimeSoftReloadSubsystemName = "military-terminal-radar";
    private const float ScanIntervalSeconds = 7f;
    private const string OfficialTerminalContinuousSessionKey = "official-terminal-continuous";
    private const float OfficialTerminalPersistenceGraceSeconds = 1.5f;
    private const float OfficialTerminalTargetLossGraceSeconds = ScanIntervalSeconds + 0.5f;
    private const int OfficialTerminalBatteryMaxBars = 6;
    private const int OfficialTerminalTargetSwitchCostBars = 3;

    private static ValuableEffectProfile? _activeEffectProfile;
    private static ValuableRadarScanResult? _lastRadarScan;
    private static float _nextScanAtTime;
    private static string? _lockedTargetPath;
    private static string? _activeSessionKey;
    private static string? _activeModeLabel;
    private static string? _activeCarrierName;
    private static bool _hasCompletedScan;
    private static bool _hudVisible;
    private static ValuableRadarHudState? _hudState;
    private static float _lastOfficialSeenAtTime = float.NegativeInfinity;
    private static string? _lastOfficialCarrierName;
    private static float? _lastOfficialOriginValue;
    private static string? _lastOfficialExcludedHostGameObjectPath;
    private static int _activeSceneHandle = -1;
    private static int _officialBatteryBarsRemaining = OfficialTerminalBatteryMaxBars;
    private static bool _officialInitialLockGranted;
    private static bool _officialTargetSwitchBlockedByBattery;
    private static bool _lastOfficialLockUsedFreeTransition;
    private static int _lastOfficialLockCostBars;
    private static float _lockedTargetLastConfirmedAtTime = float.NegativeInfinity;

    internal static ValuableRadarHudState? CurrentHudState => _hudVisible ? _hudState : null;

    internal static bool TryGetHeldBatteryUiState(out MilitaryTerminalBatteryUiState batteryUiState)
    {
        batteryUiState = default;

        if (!MilitaryTerminalHeldUiSuppressionService.IsHoldingOfficialMilitaryTerminal())
        {
            return false;
        }

        batteryUiState = new MilitaryTerminalBatteryUiState(
            currentBars: _officialBatteryBarsRemaining,
            maxBars: OfficialTerminalBatteryMaxBars,
            targetSwitchBlockedByBattery: _officialTargetSwitchBlockedByBattery);
        return true;
    }

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _activeSceneHandle = -1;
        Clear(logSessionEnded: false, resetOfficialBatteryState: true);
        RepoDeltaForceMod.Logger.LogInfo(
            $"military terminal radar state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        EnsureSceneState();

        if (!TryResolveRuntimeContext(out var context))
        {
            Clear();
            return;
        }

        var now = Time.unscaledTime;
        if (!string.Equals(_activeSessionKey, context.SessionKey, StringComparison.Ordinal))
        {
            StartNewSession(context, now);
        }
        else
        {
            RefreshContextState(context);
        }

        _activeEffectProfile = context.EffectProfile;
        _hudVisible = context.HudVisible;

        if (now >= _nextScanAtTime)
        {
            PerformScan(now, context);
        }

        RefreshHudState(now);
    }

    private static void StartNewSession(RadarRuntimeContext context, float now)
    {
        _activeSessionKey = context.SessionKey;
        _activeEffectProfile = context.EffectProfile;
        _lastRadarScan = null;
        _nextScanAtTime = now + ScanIntervalSeconds;
        _lockedTargetPath = null;
        _lockedTargetLastConfirmedAtTime = float.NegativeInfinity;
        _hasCompletedScan = false;
        _activeModeLabel = context.ModeLabel;
        _activeCarrierName = context.CarrierName;
        _hudVisible = context.HudVisible;

        RepoDeltaForceMod.Logger.LogInfo(
            $"{context.EffectProfile.LogLabel} radar session started: Mode={context.ModeLabel} | Carrier={context.CarrierName} | HudVisible={context.HudVisible}{DescribeOfficialBatteryForLog(context.EffectProfile)}");
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"雷达会话已开始：{context.ModeLabel}");
    }

    private static void RefreshContextState(RadarRuntimeContext context)
    {
        var modeChanged = !string.Equals(_activeModeLabel, context.ModeLabel, StringComparison.Ordinal);
        var carrierChanged = !string.Equals(_activeCarrierName, context.CarrierName, StringComparison.Ordinal);
        var hudVisibilityChanged = _hudVisible != context.HudVisible;

        if (!modeChanged && !carrierChanged && !hudVisibilityChanged)
        {
            return;
        }

        _activeModeLabel = context.ModeLabel;
        _activeCarrierName = context.CarrierName;

        RepoDeltaForceMod.Logger.LogInfo(
            $"{context.EffectProfile.LogLabel} runtime state changed: Mode={context.ModeLabel} | Carrier={context.CarrierName} | HudVisible={context.HudVisible} | Summary={DescribeMode(context.ModeLabel, context.HudVisible)}");
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"雷达状态已变化：{context.ModeLabel} | HUD={context.HudVisible}");
    }

    private static string DescribeMode(string modeLabel, bool hudVisible)
    {
        return modeLabel switch
        {
            "official-held" => "official terminal is currently in hand, HUD is visible",
            "official-stowed" => "official terminal remains in inventory, radar keeps scanning in background",
            "official-transition" => "official terminal is moving between hand and inventory, radar stays active and HUD stays hidden",
            "proxy-held" => "proxy testing mode is active through a held valuable",
            _ when hudVisible => "active item is driving the visible radar HUD",
            _ => "radar session is active without HUD output",
        };
    }

    private static bool TryResolveRuntimeContext(out RadarRuntimeContext context)
    {
        context = default;

        var scanOrigin = TryResolveScanOrigin();
        if (scanOrigin is null)
        {
            return false;
        }

        var heldObject = TryGetHeldObject();
        var heldIsOfficial = MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(heldObject);

        if (TryResolveOwnedOfficialTerminal(heldObject, heldIsOfficial, out var officialTerminal))
        {
            var officialSceneInfo = ObservedSceneObjectInfo.From(officialTerminal);
            var officialName = MilitaryTerminalIdentity.TryGetItemName(officialTerminal)
                ?? MilitaryTerminalIdentity.TryGetDisplayName(officialTerminal)
                ?? officialSceneInfo.HostGameObjectName
                ?? "Havoc_MilitaryTerminal";

            _lastOfficialSeenAtTime = Time.unscaledTime;
            _lastOfficialCarrierName = officialName;
            _lastOfficialOriginValue = officialSceneInfo.DollarValueCurrent;
            _lastOfficialExcludedHostGameObjectPath = officialSceneInfo.HostGameObjectPath;

            context = new RadarRuntimeContext(
                sessionKey: OfficialTerminalContinuousSessionKey,
                modeLabel: heldIsOfficial ? "official-held" : "official-stowed",
                effectProfile: ValuableEffectProfile.MilitaryTerminalOfficial(
                    heldIsOfficial
                        ? "official terminal held in hand"
                        : "official terminal present in inventory"),
                scanOriginTransform: scanOrigin,
                hudVisible: heldIsOfficial,
                carrierName: officialName,
                originObjectName: officialName,
                originValue: officialSceneInfo.DollarValueCurrent,
                excludedHostGameObjectPath: officialSceneInfo.HostGameObjectPath);
            return true;
        }

        if (TryResolvePersistedOfficialTerminalContext(scanOrigin, out context))
        {
            return true;
        }

        return false;
    }

    private static bool TryResolveOwnedOfficialTerminal(
        object? heldObject,
        bool heldIsOfficial,
        out object? officialTerminal)
    {
        if (heldIsOfficial && heldObject is not null)
        {
            officialTerminal = heldObject;
            return true;
        }

        officialTerminal = null;
        if (Inventory.instance is null)
        {
            return false;
        }

        foreach (var spot in Inventory.instance.GetAllSpots())
        {
            if (spot?.CurrentItem is null)
            {
                continue;
            }

            if (!MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(spot.CurrentItem))
            {
                continue;
            }

            officialTerminal = spot.CurrentItem;
            return true;
        }

        return false;
    }

    private static bool TryResolvePersistedOfficialTerminalContext(
        Transform scanOrigin,
        out RadarRuntimeContext context)
    {
        context = default;

        if (!string.Equals(_activeSessionKey, OfficialTerminalContinuousSessionKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (_activeEffectProfile is null || !_activeEffectProfile.IsOfficialMilitaryTerminal)
        {
            return false;
        }

        if (Time.unscaledTime - _lastOfficialSeenAtTime > OfficialTerminalPersistenceGraceSeconds)
        {
            return false;
        }

        var carrierName = _lastOfficialCarrierName ?? "Havoc_MilitaryTerminal";
        context = new RadarRuntimeContext(
            sessionKey: OfficialTerminalContinuousSessionKey,
            modeLabel: "official-transition",
            effectProfile: ValuableEffectProfile.MilitaryTerminalOfficial(
                "official terminal temporarily unresolved during a hand or inventory transition"),
            scanOriginTransform: scanOrigin,
            hudVisible: false,
            carrierName: carrierName,
            originObjectName: carrierName,
            originValue: _lastOfficialOriginValue,
            excludedHostGameObjectPath: _lastOfficialExcludedHostGameObjectPath);
        return true;
    }

    private static object? TryGetHeldObject()
    {
        if (PhysGrabber.instance is null || !PhysGrabber.instance.grabbed)
        {
            return null;
        }

        return PhysGrabber.instance.grabbedPhysGrabObject
            ?? ObservationReflection.TryGetKnownValue(PhysGrabber.instance, "grabbedObject");
    }

    private static Transform? TryResolveScanOrigin()
    {
        if (Camera.main is not null)
        {
            return Camera.main.transform;
        }

        if (PhysGrabber.instance is not null)
        {
            return PhysGrabber.instance.transform;
        }

        return null;
    }

    private static void PerformScan(float now, RadarRuntimeContext context)
    {
        var previousLockedTargetPath = _lockedTargetPath;
        var previousRadarScan = _lastRadarScan;
        var radarScan = ValuableRadarScanner.ScanFromOrigin(
            context.ScanOriginTransform,
            context.OriginObjectName,
            context.OriginValue,
            context.ExcludedHostGameObjectPath,
            _lockedTargetPath);

        _hasCompletedScan = true;

        if (radarScan is null)
        {
            if (context.EffectProfile.IsOfficialMilitaryTerminal
                && !string.IsNullOrWhiteSpace(previousLockedTargetPath)
                && now - _lockedTargetLastConfirmedAtTime <= OfficialTerminalTargetLossGraceSeconds)
            {
                _lastRadarScan = previousRadarScan;
                RepoDeltaForceMod.Logger.LogInfo(
                    $"{context.EffectProfile.LogLabel} target link preserved across transient loss: Mode={context.ModeLabel} | Carrier={context.CarrierName} | TargetPath={previousLockedTargetPath} | GraceRemaining={Mathf.Max(0f, OfficialTerminalTargetLossGraceSeconds - (now - _lockedTargetLastConfirmedAtTime)):0.0}s");
                _nextScanAtTime = now + ScanIntervalSeconds;
                return;
            }

            _lastRadarScan = null;
            if (!string.IsNullOrWhiteSpace(previousLockedTargetPath))
            {
                RepoDeltaForceMod.Logger.LogInfo(
                    $"{context.EffectProfile.LogLabel} target lost: Mode={context.ModeLabel} | Carrier={context.CarrierName} | TargetPath={previousLockedTargetPath} | Reason=no valid valuable candidates remain");
                RuntimeSoftReloadManager.MarkSubsystemDirty(
                    RuntimeSoftReloadSubsystemName,
                    "雷达已丢失当前锁定目标");
            }

            _lockedTargetPath = null;
            _nextScanAtTime = now + ScanIntervalSeconds;
            return;
        }

        var selectedLead = radarScan.Leads[0];
        _lastRadarScan = radarScan;
        _lockedTargetLastConfirmedAtTime = now;
        var targetChanged = !string.Equals(previousLockedTargetPath, selectedLead.Path, StringComparison.Ordinal);
        if (context.EffectProfile.IsOfficialMilitaryTerminal
            && targetChanged
            && !TryAcceptOfficialTargetLockTransition(context, selectedLead, previousLockedTargetPath, out var blockedReason))
        {
            _lastRadarScan = null;
            _lockedTargetPath = null;
            RepoDeltaForceMod.Logger.LogInfo(
                $"{context.EffectProfile.LogLabel} target switch blocked: Mode={context.ModeLabel} | Carrier={context.CarrierName} | RequestedTarget={selectedLead.Name} | RemainingBattery={_officialBatteryBarsRemaining}/{OfficialTerminalBatteryMaxBars} | Reason={blockedReason}");
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                "军用信息终端电量不足，无法锁定下一个目标");
            _nextScanAtTime = now + ScanIntervalSeconds;
            return;
        }

        _lockedTargetPath = selectedLead.Path;

        if (!string.Equals(previousLockedTargetPath, _lockedTargetPath, StringComparison.Ordinal))
        {
            var lockReason = string.IsNullOrWhiteSpace(previousLockedTargetPath)
                ? "initial lock"
                : "previous target lost or invalid, relocked";
            if (context.EffectProfile.IsOfficialMilitaryTerminal)
            {
                lockReason = BuildOfficialTargetLockReason(previousLockedTargetPath);
            }

            RepoDeltaForceMod.Logger.LogInfo(
                $"{context.EffectProfile.LogLabel} target locked: Mode={context.ModeLabel} | Carrier={context.CarrierName} | Target={selectedLead.Name} | Value={selectedLead.ToHudValueText()} | Distance={selectedLead.DistanceMeters:0.0}m | Direction={selectedLead.DirectionHint} | CandidateCount={radarScan.TotalLeadCount} | Reason={lockReason}{DescribeOfficialBatteryForLog(context.EffectProfile)}");
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                $"雷达已锁定新目标：{selectedLead.Name}");
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"{context.EffectProfile.LogLabel} radar pulse: Mode={context.ModeLabel} | Carrier={context.CarrierName} | {radarScan.ToLogSummary()}{DescribeOfficialBatteryForLog(context.EffectProfile)}");

        _nextScanAtTime = now + ScanIntervalSeconds;
    }

    private static void RefreshHudState(float now)
    {
        var secondsUntilNextScan = Mathf.Max(0f, _nextScanAtTime - now);
        _hudState = ValuableRadarHudState.Build(
            _activeEffectProfile,
            _lastRadarScan,
            secondsUntilNextScan,
            _hasCompletedScan,
            TryBuildOfficialBatteryHudState(_activeEffectProfile));
    }

    private static void Clear()
    {
        Clear(logSessionEnded: true, resetOfficialBatteryState: false);
    }

    private static void Clear(bool logSessionEnded, bool resetOfficialBatteryState)
    {
        if (logSessionEnded && !string.IsNullOrWhiteSpace(_activeSessionKey))
        {
            RepoDeltaForceMod.Logger.LogInfo("military terminal radar session ended: no valid carrier is currently active");
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                "雷达会话已结束");
        }

        _activeEffectProfile = null;
        _lastRadarScan = null;
        _nextScanAtTime = 0f;
        _lockedTargetPath = null;
        _activeSessionKey = null;
        _activeModeLabel = null;
        _activeCarrierName = null;
        _hasCompletedScan = false;
        _hudVisible = false;
        _hudState = null;
        _lastOfficialSeenAtTime = float.NegativeInfinity;
        _lastOfficialCarrierName = null;
        _lastOfficialOriginValue = null;
        _lastOfficialExcludedHostGameObjectPath = null;

        if (resetOfficialBatteryState)
        {
            ResetOfficialBatteryState();
        }
    }

    private static void EnsureSceneState()
    {
        var scene = SceneManager.GetActiveScene();
        var currentSceneHandle = scene.IsValid() ? scene.handle : -1;
        if (currentSceneHandle == _activeSceneHandle)
        {
            return;
        }

        _activeSceneHandle = currentSceneHandle;
        Clear(logSessionEnded: false, resetOfficialBatteryState: true);
    }

    private static void ResetOfficialBatteryState()
    {
        _officialBatteryBarsRemaining = OfficialTerminalBatteryMaxBars;
        _officialInitialLockGranted = false;
        _officialTargetSwitchBlockedByBattery = false;
        _lastOfficialLockUsedFreeTransition = false;
        _lastOfficialLockCostBars = 0;
        _lockedTargetLastConfirmedAtTime = float.NegativeInfinity;
    }

    private static bool TryAcceptOfficialTargetLockTransition(
        RadarRuntimeContext context,
        ValuableRadarLead selectedLead,
        string? previousLockedTargetPath,
        out string blockedReason)
    {
        blockedReason = string.Empty;

        if (!_officialInitialLockGranted)
        {
            _officialInitialLockGranted = true;
            _officialTargetSwitchBlockedByBattery = false;
            _lastOfficialLockUsedFreeTransition = true;
            _lastOfficialLockCostBars = 0;
            return true;
        }

        if (_officialBatteryBarsRemaining >= OfficialTerminalTargetSwitchCostBars)
        {
            _officialBatteryBarsRemaining -= OfficialTerminalTargetSwitchCostBars;
            _officialTargetSwitchBlockedByBattery = false;
            _lastOfficialLockUsedFreeTransition = false;
            _lastOfficialLockCostBars = OfficialTerminalTargetSwitchCostBars;

            if (_officialBatteryBarsRemaining == 0)
            {
                RepoDeltaForceMod.Logger.LogInfo(
                    $"{context.EffectProfile.LogLabel} battery reserve exhausted after locking '{selectedLead.Name}': Mode={context.ModeLabel} | Carrier={context.CarrierName} | RemainingBattery=0/{OfficialTerminalBatteryMaxBars}");
                RuntimeSoftReloadManager.MarkSubsystemDirty(
                    RuntimeSoftReloadSubsystemName,
                    "军用信息终端电量已耗尽");
            }

            return true;
        }

        _officialTargetSwitchBlockedByBattery = true;
        _lastOfficialLockUsedFreeTransition = false;
        _lastOfficialLockCostBars = 0;
        blockedReason = string.IsNullOrWhiteSpace(previousLockedTargetPath)
            ? "battery depleted before reacquiring the next target"
            : "battery depleted before switching to the next target";
        return false;
    }

    private static string BuildOfficialTargetLockReason(string? previousLockedTargetPath)
    {
        if (_lastOfficialLockUsedFreeTransition)
        {
            return "initial free lock";
        }

        if (string.IsNullOrWhiteSpace(previousLockedTargetPath))
        {
            return $"reacquired next target (-{_lastOfficialLockCostBars}, remaining {_officialBatteryBarsRemaining}/{OfficialTerminalBatteryMaxBars})";
        }

        return $"switched target (-{_lastOfficialLockCostBars}, remaining {_officialBatteryBarsRemaining}/{OfficialTerminalBatteryMaxBars})";
    }

    private static string DescribeOfficialBatteryForLog(ValuableEffectProfile effectProfile)
    {
        return effectProfile.IsOfficialMilitaryTerminal
            ? $" | Battery={_officialBatteryBarsRemaining}/{OfficialTerminalBatteryMaxBars} | InitialLockGranted={_officialInitialLockGranted} | SwitchBlocked={_officialTargetSwitchBlockedByBattery}"
            : string.Empty;
    }

    private static OfficialMilitaryTerminalBatteryHudState? TryBuildOfficialBatteryHudState(ValuableEffectProfile? effectProfile)
    {
        if (effectProfile is null || !effectProfile.IsOfficialMilitaryTerminal)
        {
            return null;
        }

        return new OfficialMilitaryTerminalBatteryHudState(
            currentBars: _officialBatteryBarsRemaining,
            maxBars: OfficialTerminalBatteryMaxBars,
            targetSwitchBlockedByBattery: _officialTargetSwitchBlockedByBattery);
    }

    private readonly struct RadarRuntimeContext
    {
        internal RadarRuntimeContext(
            string sessionKey,
            string modeLabel,
            ValuableEffectProfile effectProfile,
            Transform scanOriginTransform,
            bool hudVisible,
            string carrierName,
            string originObjectName,
            float? originValue,
            string? excludedHostGameObjectPath)
        {
            SessionKey = sessionKey;
            ModeLabel = modeLabel;
            EffectProfile = effectProfile;
            ScanOriginTransform = scanOriginTransform;
            HudVisible = hudVisible;
            CarrierName = carrierName;
            OriginObjectName = originObjectName;
            OriginValue = originValue;
            ExcludedHostGameObjectPath = excludedHostGameObjectPath;
        }

        internal string SessionKey { get; }
        internal string ModeLabel { get; }
        internal ValuableEffectProfile EffectProfile { get; }
        internal Transform ScanOriginTransform { get; }
        internal bool HudVisible { get; }
        internal string CarrierName { get; }
        internal string OriginObjectName { get; }
        internal float? OriginValue { get; }
        internal string? ExcludedHostGameObjectPath { get; }
    }
}

internal sealed class ValuableRadarHudState
{
    private ValuableRadarHudState(
        string title,
        string statusLine,
        IReadOnlyList<string> leadLines)
    {
        Title = title;
        StatusLine = statusLine;
        LeadLines = leadLines;
    }

    internal string Title { get; }
    internal string StatusLine { get; }
    internal IReadOnlyList<string> LeadLines { get; }

    internal static ValuableRadarHudState Build(
        ValuableEffectProfile? effectProfile,
        ValuableRadarScanResult? radarScan,
        float secondsUntilNextScan,
        bool hasCompletedScan,
        OfficialMilitaryTerminalBatteryHudState? officialBatteryHudState)
    {
        var leadLines = new List<string>();
        string statusLine;
        var batteryHudState = officialBatteryHudState.GetValueOrDefault();

        if (officialBatteryHudState is not null
            && batteryHudState.TargetSwitchBlockedByBattery
            && (radarScan is null || radarScan.Leads.Count == 0))
        {
            statusLine = "\u7535\u91cf\u4e0d\u8db3";
            leadLines.Add("\u65e0\u6cd5\u9501\u5b9a\u65b0\u76ee\u6807");
        }
        else if (radarScan is not null && radarScan.Leads.Count > 0)
        {
            var topLead = radarScan.Leads[0];
            statusLine = "\u5df2\u9501\u5b9a\u76ee\u6807";
            leadLines.Add($"\u65b9\u5411\uff1a{topLead.ToHudDirectionText()}");
            leadLines.Add($"\u8ddd\u79bb\uff1a{topLead.ToHudDistanceText()}");
        }
        else
        {
            statusLine = "\u6b63\u5728\u641c\u7d22";
            leadLines.Add(hasCompletedScan
                ? "\u6682\u672a\u53d1\u73b0\u6709\u6548\u76ee\u6807"
                : "\u7b49\u5f85\u9996\u6b21\u626b\u63cf");
        }

        if (!(officialBatteryHudState is not null && batteryHudState.TargetSwitchBlockedByBattery))
        {
            leadLines.Add(BuildCountdownText(secondsUntilNextScan));
        }

        return new ValuableRadarHudState(
            title: effectProfile?.GetHudTitle() ?? "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef\uff08\u6d4b\u8bd5\uff09",
            statusLine: statusLine,
            leadLines: leadLines);
    }

    private static string BuildCountdownText(float secondsUntilNextScan)
    {
        var seconds = Mathf.Clamp(Mathf.CeilToInt(secondsUntilNextScan), 0, 999);
        return $"\u4e0b\u6b21\u626b\u63cf\uff1a{seconds} \u79d2";
    }
}

internal readonly struct OfficialMilitaryTerminalBatteryHudState
{
    internal OfficialMilitaryTerminalBatteryHudState(int currentBars, int maxBars, bool targetSwitchBlockedByBattery)
    {
        CurrentBars = currentBars;
        MaxBars = maxBars;
        TargetSwitchBlockedByBattery = targetSwitchBlockedByBattery;
    }

    internal int CurrentBars { get; }
    internal int MaxBars { get; }
    internal bool TargetSwitchBlockedByBattery { get; }
}

internal readonly struct MilitaryTerminalBatteryUiState
{
    internal MilitaryTerminalBatteryUiState(int currentBars, int maxBars, bool targetSwitchBlockedByBattery)
    {
        CurrentBars = currentBars;
        MaxBars = maxBars;
        TargetSwitchBlockedByBattery = targetSwitchBlockedByBattery;
    }

    internal int CurrentBars { get; }
    internal int MaxBars { get; }
    internal bool TargetSwitchBlockedByBattery { get; }
}
