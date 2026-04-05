using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class FlightRecorderStatusHudService
{
    private const string RuntimeSoftReloadSubsystemName = "flight-recorder-status-hud";

    private static FlightRecorderStatusHudState? _currentHudState;
    private static string? _lastSignature;

    internal static FlightRecorderStatusHudState? CurrentHudState => _currentHudState;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _currentHudState = null;
        _lastSignature = null;

        RepoDeltaForceMod.Logger.LogInfo(
            $"flight recorder status HUD reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        var openingEventActive = OpeningHavocEventService.IsFlightRecorderInsertionActive;
        var heldObject = TryGetHeldObject();
        var heldOfficial = FlightRecorderIdentity.IsOfficialFlightRecorder(heldObject);
        var inventoryObject = TryGetInventoryFlightRecorder(excluding: heldObject);
        var worldObject = TryGetSceneFlightRecorder(excludingHeld: heldObject, excludingInventory: inventoryObject);

        if (!openingEventActive && heldObject is null && inventoryObject is null && worldObject is null)
        {
            UpdateState(null);
            return;
        }

        var trackedObject = heldOfficial
            ? heldObject
            : inventoryObject ?? worldObject;
        var trackedName = ResolveRecorderName(trackedObject);
        var contractValidation = HavocSupplyContractValidator.ValidateFlightRecorder(trackedObject);

        var itemState = heldOfficial
            ? "手持中"
            : inventoryObject is not null
                ? "背包中"
                : worldObject is not null
                    ? "场景中"
                    : "未发现";
        var effectTriggered = heldOfficial;
        var effectStatus = effectTriggered ? "已触发" : "未触发";

        var detailLines = new List<string>
        {
            $"事件状态：{(openingEventActive ? "飞行记录仪介入中" : "未介入")}",
            $"物品状态：{itemState}",
            $"判定依据：{(effectTriggered ? "当前玩家正在手持官方飞行记录仪" : "当前玩家未手持官方飞行记录仪")}",
        };

        if (!string.IsNullOrWhiteSpace(trackedName))
        {
            detailLines.Add($"识别目标：{trackedName}");
        }

        detailLines.Add(contractValidation.ContractStatusLine);
        detailLines.Add(contractValidation.ValueStatusLine);

        _currentHudState = new FlightRecorderStatusHudState(
            "飞行记录仪",
            $"效果状态：{effectStatus}",
            effectTriggered,
            detailLines);
        UpdateState(_currentHudState);
    }

    private static void UpdateState(FlightRecorderStatusHudState? state)
    {
        _currentHudState = state;

        var signature = state?.GetDebugSignature();
        if (string.Equals(_lastSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _lastSignature = signature;
        if (state is null)
        {
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                "飞行记录仪状态 HUD 已隐藏");
            return;
        }

        RepoDeltaForceMod.Logger.LogInfo($"Flight recorder HUD state changed: {state.GetDebugSignature()}");
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            state.StatusLine);
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

    private static object? TryGetInventoryFlightRecorder(object? excluding)
    {
        if (Inventory.instance is null)
        {
            return null;
        }

        foreach (var spot in Inventory.instance.GetAllSpots())
        {
            var currentItem = spot?.CurrentItem;
            if (currentItem is null || ReferenceEquals(currentItem, excluding))
            {
                continue;
            }

            if (FlightRecorderIdentity.IsOfficialFlightRecorder(currentItem))
            {
                return currentItem;
            }
        }

        return null;
    }

    private static object? TryGetSceneFlightRecorder(object? excludingHeld, object? excludingInventory)
    {
        foreach (var itemAttributes in UnityObject.FindObjectsByType<ItemAttributes>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (itemAttributes is null
                || ReferenceEquals(itemAttributes, excludingHeld)
                || ReferenceEquals(itemAttributes, excludingInventory))
            {
                continue;
            }

            if (FlightRecorderIdentity.IsOfficialFlightRecorder(itemAttributes))
            {
                return itemAttributes;
            }
        }

        return null;
    }

    private static string ResolveRecorderName(object? value)
    {
        if (value is null)
        {
            return "未识别到飞行记录仪";
        }

        return FlightRecorderIdentity.TryGetItemName(value)
            ?? FlightRecorderIdentity.TryGetDisplayName(value)
            ?? ObservedSceneObjectInfo.From(value).HostGameObjectName
            ?? "飞行记录仪";
    }
}

internal sealed class FlightRecorderStatusHudState
{
    internal FlightRecorderStatusHudState(
        string title,
        string statusLine,
        bool effectTriggered,
        IReadOnlyList<string> detailLines)
    {
        Title = title;
        StatusLine = statusLine;
        EffectTriggered = effectTriggered;
        DetailLines = detailLines;
    }

    internal string Title { get; }
    internal string StatusLine { get; }
    internal bool EffectTriggered { get; }
    internal IReadOnlyList<string> DetailLines { get; }

    internal string GetDebugSignature()
    {
        return $"{Title} | {StatusLine} | {string.Join(" | ", DetailLines)}";
    }
}
