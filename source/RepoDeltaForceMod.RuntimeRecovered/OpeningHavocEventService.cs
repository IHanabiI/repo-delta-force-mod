using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RepoDeltaForceMod;

internal static class OpeningHavocEventService
{
    private const string RuntimeSoftReloadSubsystemName = "havoc-opening-event";
    private const double EventActivationChance = 1d;
    private const float RetryIntervalSeconds = 1f;
    private const float TriggerDelayAfterGameplayReadySeconds = 9f;
    private const float OverlayMinimumDurationSeconds = 4f;
    private const float OverlayDismissMoveDistanceMeters = 0.2f;
    private static readonly string[] OverlayDetailLines =
    {
        "哈夫克公司投放了特殊物资",
        "收集它们以获得额外收益",
    };

    private static readonly IReadOnlyList<OpeningHavocEventDefinition> EventPool =
        new[]
        {
            new OpeningHavocEventDefinition(
                eventId: "havoc.opening.air_drop_case_insertion",
                displayName: "航空箱介入",
                logDescription: "opening event pool selected the air drop case insertion scenario",
                applyAction: _ => { }),
        };

    private static int _activeSceneHandle = -1;
    private static string _activeSceneName = "<unknown>";
    private static float _nextAttemptAtTime;
    private static bool _openingEventTriggered;
    private static bool _selectionPlanResolved;
    private static OpeningHavocEventNotice? _currentNotice;
    private static OpeningHavocEventState? _lastTriggeredEvent;
    private static OpeningHavocEventRuntimeState _runtimeState = new();
    private static float _gameplayReadyAtTime = -1f;

    internal static bool IsMilitaryTerminalInsertionActive => _runtimeState.MilitaryTerminalInsertionActive;
    internal static bool IsFlightRecorderInsertionActive => _runtimeState.FlightRecorderInsertionActive;
    internal static bool IsAirDropCaseInsertionActive => _runtimeState.AirDropCaseInsertionActive;

    internal static void EnsureSelectionPlanReady()
    {
        if (_selectionPlanResolved || !CanResolveSelectionPlan())
        {
            return;
        }

        ResolveSelectionPlan();
    }

    internal static bool TryGetSelectedSupplyTypes(out IReadOnlyList<OpeningHavocSupplyType> supplyTypes)
    {
        EnsureSelectionPlanReady();
        supplyTypes = _runtimeState.GetSelectedSupplyTypes();
        return supplyTypes.Count > 0;
    }

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _activeSceneHandle = -1;
        _activeSceneName = "<unknown>";
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _openingEventTriggered = false;
        _selectionPlanResolved = false;
        _currentNotice = null;
        _lastTriggeredEvent = null;
        _runtimeState = new OpeningHavocEventRuntimeState();
        _gameplayReadyAtTime = -1f;

        RepoDeltaForceMod.Logger.LogInfo(
            $"opening Havoc event state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        if (!ModFeatureSettings.OpeningHavocEventEnabled)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        if (_activeSceneHandle != scene.handle)
        {
            ResetForScene(scene);
        }

        if (_openingEventTriggered || Time.unscaledTime < _nextAttemptAtTime)
        {
            return;
        }

        if (!IsGameplaySceneReady())
        {
            _gameplayReadyAtTime = -1f;
            _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
            return;
        }

        if (_gameplayReadyAtTime < 0f)
        {
            _gameplayReadyAtTime = Time.unscaledTime;
            _nextAttemptAtTime = _gameplayReadyAtTime + TriggerDelayAfterGameplayReadySeconds;
            return;
        }

        TriggerOpeningEvent();
    }

    internal static bool TryGetOverlayState(out OpeningHavocEventOverlayState overlayState)
    {
        overlayState = default;

        if (_currentNotice is null)
        {
            return false;
        }

        if (_currentNotice.CanDismiss(Time.unscaledTime) && HasPlayerActedSinceNoticeStarted(_currentNotice))
        {
            _currentNotice = null;
            return false;
        }

        var notice = _currentNotice;
        var duration = Mathf.Max(0.01f, notice.MinimumVisibleUntilTime - notice.StartedAtTime);
        var elapsed = Mathf.Clamp(Time.unscaledTime - notice.StartedAtTime, 0f, duration);
        var normalized = Mathf.Clamp01(elapsed / duration);

        var revealLineCount = 0;
        if (elapsed >= 0.9f)
        {
            revealLineCount = 1;
        }

        if (elapsed >= 1.55f)
        {
            revealLineCount = 2;
        }

        overlayState = new OpeningHavocEventOverlayState(
            title: notice.Title,
            detailLines: notice.DetailLines,
            elapsedTime: elapsed,
            duration: duration,
            normalizedTime: normalized,
            revealLineCount: revealLineCount);
        return true;
    }

    private static void ResetForScene(Scene scene)
    {
        _activeSceneHandle = scene.handle;
        _activeSceneName = string.IsNullOrWhiteSpace(scene.name) ? "<unnamed>" : scene.name;
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _openingEventTriggered = false;
        _selectionPlanResolved = false;
        _currentNotice = null;
        _lastTriggeredEvent = null;
        _runtimeState = new OpeningHavocEventRuntimeState();
        _gameplayReadyAtTime = -1f;

        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"开局哈夫克事件等待在场景 {_activeSceneName} 触发");
    }

    private static bool IsGameplaySceneReady()
    {
        if (!SemiFunc.RunIsLevel())
        {
            return false;
        }

        if (LevelGenerator.Instance is null || !LevelGenerator.Instance.Generated)
        {
            return false;
        }

        if (StatsManager.instance is null || GameManager.instance is null)
        {
            return false;
        }

        if (Camera.main is null && PhysGrabber.instance is null)
        {
            return false;
        }

        return true;
    }

    private static bool CanResolveSelectionPlan()
    {
        if (!SemiFunc.RunIsLevel())
        {
            return false;
        }

        if (GameDirector.instance == null)
        {
            return false;
        }

        return true;
    }

    private static void ResolveSelectionPlan()
    {
        _selectionPlanResolved = true;

        var seed = GameDirector.instance != null
            ? GameDirector.instance.Seed ^ _activeSceneHandle ^ 0x5A17A1D
            : _activeSceneHandle ^ 0x5A17A1D;
        var random = new System.Random(seed);

        if (random.NextDouble() > EventActivationChance)
        {
            RepoDeltaForceMod.Logger.LogInfo(
                $"opening Havoc event selection resolved: Scene={_activeSceneName} | Active=false | Seed={seed}");
            return;
        }

        var candidates = new List<OpeningHavocSupplyType>
        {
            OpeningHavocSupplyType.MilitaryTerminal,
            OpeningHavocSupplyType.FlightRecorder,
            OpeningHavocSupplyType.AirDropCase,
        };

        var selectedSupplies = candidates
            .OrderBy(_ => random.Next())
            .Take(2)
            .ToArray();

        foreach (var selectedSupply in selectedSupplies)
        {
            _runtimeState.ActivateInsertion(selectedSupply);
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"opening Havoc event selection resolved: Scene={_activeSceneName} | Active=true | Seed={seed} | Supplies={string.Join(", ", selectedSupplies)}");
    }

    private static void TriggerOpeningEvent()
    {
        EnsureSelectionPlanReady();
        _openingEventTriggered = true;

        if (!_runtimeState.HasAnyInsertionActive)
        {
            RepoDeltaForceMod.Logger.LogInfo(
                $"opening Havoc event skipped in scene '{_activeSceneName}': no Havoc event selected for this run.");
            return;
        }

        var selectedDefinition = SelectOpeningEventDefinition();
        selectedDefinition.Apply(_runtimeState);

        _lastTriggeredEvent = new OpeningHavocEventState(
            selectedDefinition.EventId,
            selectedDefinition.DisplayName,
            _activeSceneName,
            DateTimeOffset.UtcNow);
        var triggeredEvent = _lastTriggeredEvent.Value;

        var noticeStartCameraPosition = Camera.main is not null ? Camera.main.transform.position : Vector3.zero;
        var noticeStartPlayerPosition = PlayerController.instance is not null ? PlayerController.instance.transform.position : noticeStartCameraPosition;

        _currentNotice = new OpeningHavocEventNotice(
            title: "哈夫克事件",
            detailLines: OverlayDetailLines,
            startedAtTime: Time.unscaledTime,
            minimumVisibleUntilTime: Time.unscaledTime + OverlayMinimumDurationSeconds,
            startPlayerPosition: noticeStartPlayerPosition,
            startCameraPosition: noticeStartCameraPosition);

        RepoDeltaForceMod.Logger.LogInfo(
            $"opening Havoc event triggered: EventId={triggeredEvent.EventId} | Scene={_activeSceneName} | DisplayName={triggeredEvent.DisplayName} | Description={selectedDefinition.LogDescription}");

        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"已触发开局事件：{triggeredEvent.DisplayName}");
    }

    private static OpeningHavocEventDefinition SelectOpeningEventDefinition()
    {
        // Current first pass: the pool structure exists, but selection stays deterministic
        // until more opening events are implemented and weighted.
        return EventPool[0];
    }

    private static bool HasPlayerActedSinceNoticeStarted(OpeningHavocEventNotice notice)
    {
        var playerTransform = PlayerController.instance is not null
            ? PlayerController.instance.transform
            : null;
        if (playerTransform is not null
            && Vector3.Distance(playerTransform.position, notice.StartPlayerPosition) >= OverlayDismissMoveDistanceMeters)
        {
            return true;
        }

        return false;
    }
}

internal sealed class OpeningHavocEventDefinition
{
    private readonly Action<OpeningHavocEventRuntimeState> _applyAction;

    internal OpeningHavocEventDefinition(
        string eventId,
        string displayName,
        string logDescription,
        Action<OpeningHavocEventRuntimeState> applyAction)
    {
        EventId = eventId;
        DisplayName = displayName;
        LogDescription = logDescription;
        _applyAction = applyAction;
    }

    internal string EventId { get; }

    internal string DisplayName { get; }

    internal string LogDescription { get; }

    internal void Apply(OpeningHavocEventRuntimeState state)
    {
        _applyAction(state);
    }
}

internal sealed class OpeningHavocEventRuntimeState
{
    internal bool MilitaryTerminalInsertionActive { get; private set; }
    internal bool FlightRecorderInsertionActive { get; private set; }
    internal bool AirDropCaseInsertionActive { get; private set; }

    internal bool HasAnyInsertionActive =>
        MilitaryTerminalInsertionActive
        || FlightRecorderInsertionActive
        || AirDropCaseInsertionActive;

    internal void ActivateInsertion(OpeningHavocSupplyType supplyType)
    {
        switch (supplyType)
        {
            case OpeningHavocSupplyType.MilitaryTerminal:
                ActivateMilitaryTerminalInsertion();
                break;
            case OpeningHavocSupplyType.FlightRecorder:
                ActivateFlightRecorderInsertion();
                break;
            case OpeningHavocSupplyType.AirDropCase:
                ActivateAirDropCaseInsertion();
                break;
        }
    }

    internal IReadOnlyList<OpeningHavocSupplyType> GetSelectedSupplyTypes()
    {
        var result = new List<OpeningHavocSupplyType>(3);
        if (MilitaryTerminalInsertionActive)
        {
            result.Add(OpeningHavocSupplyType.MilitaryTerminal);
        }

        if (FlightRecorderInsertionActive)
        {
            result.Add(OpeningHavocSupplyType.FlightRecorder);
        }

        if (AirDropCaseInsertionActive)
        {
            result.Add(OpeningHavocSupplyType.AirDropCase);
        }

        return result;
    }

    internal void ActivateMilitaryTerminalInsertion()
    {
        MilitaryTerminalInsertionActive = true;
    }

    internal void ActivateFlightRecorderInsertion()
    {
        FlightRecorderInsertionActive = true;
    }

    internal void ActivateAirDropCaseInsertion()
    {
        AirDropCaseInsertionActive = true;
    }
}

internal enum OpeningHavocSupplyType
{
    MilitaryTerminal,
    FlightRecorder,
    AirDropCase,
}

internal sealed class OpeningHavocEventNotice
{
    internal OpeningHavocEventNotice(
        string title,
        IReadOnlyList<string> detailLines,
        float startedAtTime,
        float minimumVisibleUntilTime,
        Vector3 startPlayerPosition,
        Vector3 startCameraPosition)
    {
        Title = title;
        DetailLines = detailLines;
        StartedAtTime = startedAtTime;
        MinimumVisibleUntilTime = minimumVisibleUntilTime;
        StartPlayerPosition = startPlayerPosition;
        StartCameraPosition = startCameraPosition;
    }

    internal string Title { get; }
    internal IReadOnlyList<string> DetailLines { get; }
    internal float StartedAtTime { get; }
    internal float MinimumVisibleUntilTime { get; }
    internal Vector3 StartPlayerPosition { get; }
    internal Vector3 StartCameraPosition { get; }

    internal bool CanDismiss(float now)
    {
        return now >= MinimumVisibleUntilTime;
    }
}

internal readonly struct OpeningHavocEventOverlayState
{
    internal OpeningHavocEventOverlayState(
        string title,
        IReadOnlyList<string> detailLines,
        float elapsedTime,
        float duration,
        float normalizedTime,
        int revealLineCount)
    {
        Title = title;
        DetailLines = detailLines;
        ElapsedTime = elapsedTime;
        Duration = duration;
        NormalizedTime = normalizedTime;
        RevealLineCount = revealLineCount;
    }

    internal string Title { get; }
    internal IReadOnlyList<string> DetailLines { get; }
    internal float ElapsedTime { get; }
    internal float Duration { get; }
    internal float NormalizedTime { get; }
    internal int RevealLineCount { get; }
}

internal readonly struct OpeningHavocEventState
{
    internal OpeningHavocEventState(
        string eventId,
        string displayName,
        string sceneName,
        DateTimeOffset triggeredAtUtc)
    {
        EventId = eventId;
        DisplayName = displayName;
        SceneName = sceneName;
        TriggeredAtUtc = triggeredAtUtc;
    }

    internal string EventId { get; }

    internal string DisplayName { get; }

    internal string SceneName { get; }

    internal DateTimeOffset TriggeredAtUtc { get; }
}
