using System;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class MilitaryTerminalHeldUiSuppressionService
{
    private static readonly HarmonyLib.AccessTools.FieldRef<InventorySpot, BatteryVisualLogic> InventorySpotBatteryVisualLogicRef =
        HarmonyLib.AccessTools.FieldRefAccess<InventorySpot, BatteryVisualLogic>("batteryVisualLogic");

    internal static void Tick()
    {
        if (!ShouldSuppressAnyMilitaryTerminalBatteryUi())
        {
            if (MilitaryTerminalGrabGridSuppressionHelpers.IsHoldingOfficialTerminal(PhysGrabber.instance))
            {
                MilitaryTerminalGrabGridSuppressionHelpers.ClearGridObjects(PhysGrabber.instance);
            }

            return;
        }

        if (MilitaryTerminalGrabGridSuppressionHelpers.IsHoldingOfficialTerminal(PhysGrabber.instance))
        {
            MilitaryTerminalGrabGridSuppressionHelpers.ClearGridObjects(PhysGrabber.instance);
        }

        foreach (var batteryVisualLogic in UnityObject.FindObjectsByType<BatteryVisualLogic>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (batteryVisualLogic is null)
            {
                continue;
            }

            if (!ShouldForceHide(batteryVisualLogic))
            {
                continue;
            }

            ForceHide(batteryVisualLogic);
        }

        foreach (var inventoryBattery in UnityObject.FindObjectsByType<InventoryBattery>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (inventoryBattery is null)
            {
                continue;
            }

            if (!MilitaryTerminalToolUiPatchHelpers.IsInventoryBatteryBoundToOfficialTerminal(inventoryBattery))
            {
                continue;
            }

            MilitaryTerminalToolUiPatchHelpers.HideInventoryBattery(inventoryBattery);
        }

        SuppressInventorySpotBatteryUi();
    }

    internal static bool IsHoldingOfficialMilitaryTerminal()
    {
        var physGrabber = PhysGrabber.instance;
        if (physGrabber is null || !physGrabber.grabbed)
        {
            return false;
        }

        var grabbedPhysGrabObject = physGrabber.grabbedPhysGrabObject;
        if (grabbedPhysGrabObject is null)
        {
            return false;
        }

        if (MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(grabbedPhysGrabObject))
        {
            return true;
        }

        return MatchesTerminalName(grabbedPhysGrabObject.name)
            || MatchesTerminalName(grabbedPhysGrabObject.transform.root.name)
            || MatchesTerminalName(grabbedPhysGrabObject.GetComponentInParent<ItemAttributes>()?.itemName)
            || MatchesTerminalName(grabbedPhysGrabObject.GetComponentInParent<ItemAttributes>()?.instanceName);
    }

    internal static bool IsOfficialMilitaryTerminalEquipped()
    {
        if (Inventory.instance is null)
        {
            return false;
        }

        foreach (var spot in Inventory.instance.GetAllSpots())
        {
            var currentItem = spot?.CurrentItem;
            if (currentItem is null)
            {
                continue;
            }

            if (!MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(currentItem))
            {
                continue;
            }

            if (currentItem.isEquipped)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ShouldSuppressAnyMilitaryTerminalBatteryUi()
    {
        return IsHoldingOfficialMilitaryTerminal() || IsOfficialMilitaryTerminalEquipped() || HasOfficialMilitaryTerminalInInventory();
    }

    private static bool ShouldForceHide(BatteryVisualLogic batteryVisualLogic)
    {
        if (BatteryUI.instance is not null && ReferenceEquals(batteryVisualLogic, BatteryUI.instance.batteryVisualLogic))
        {
            return false;
        }

        if (MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(batteryVisualLogic.itemBattery))
        {
            return true;
        }

        var inventorySpot = batteryVisualLogic.GetComponentInParent<InventorySpot>();
        if (inventorySpot?.CurrentItem is not null
            && MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(inventorySpot.CurrentItem))
        {
            return true;
        }

        var parentBatteryUi = batteryVisualLogic.GetComponentInParent<BatteryUI>();
        if (parentBatteryUi is not null)
        {
            return false;
        }

        return MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(batteryVisualLogic.gameObject)
            || MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(batteryVisualLogic.transform.root.gameObject)
            || MatchesTerminalName(batteryVisualLogic.name)
            || MatchesTerminalName(batteryVisualLogic.transform.root.name);
    }

    private static void ForceHide(BatteryVisualLogic batteryVisualLogic)
    {
        batteryVisualLogic.itemBattery = null;
        batteryVisualLogic.BatteryOutro();
        batteryVisualLogic.gameObject.SetActive(false);
    }

    private static bool HasOfficialMilitaryTerminalInInventory()
    {
        if (Inventory.instance is null)
        {
            return false;
        }

        foreach (var spot in Inventory.instance.GetAllSpots())
        {
            if (spot?.CurrentItem is not null && MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(spot.CurrentItem))
            {
                return true;
            }
        }

        return false;
    }

    private static void SuppressInventorySpotBatteryUi()
    {
        if (Inventory.instance is null)
        {
            return;
        }

        foreach (var spot in Inventory.instance.GetAllSpots())
        {
            if (spot is null)
            {
                continue;
            }

            var currentItem = spot.CurrentItem;
            if (currentItem is null || !MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(currentItem))
            {
                continue;
            }

            var batteryVisualLogic = InventorySpotBatteryVisualLogicRef(spot);
            if (batteryVisualLogic is null)
            {
                continue;
            }

            ForceHide(batteryVisualLogic);
        }
    }

    private static bool MatchesTerminalName(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.Equals(candidate, "Havoc_MilitaryTerminal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, "Military Terminal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, "军用信息终端", StringComparison.Ordinal))
        {
            return true;
        }

        var normalized = candidate
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return normalized.IndexOf("militaryterminal", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("havocmilitaryterminal", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("军用信息终端", StringComparison.Ordinal) >= 0;
    }
}
