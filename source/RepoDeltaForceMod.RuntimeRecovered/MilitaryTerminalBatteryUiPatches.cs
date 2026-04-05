using HarmonyLib;
using UnityEngine;

namespace RepoDeltaForceMod;

[HarmonyPatch(typeof(BatteryUI), nameof(BatteryUI.BatteryFetch))]
internal static class MilitaryTerminalHeldBatteryUiPatch
{
    private static bool Prefix(BatteryUI __instance)
    {
        if (!MilitaryTerminalToolUiPatchHelpers.TryDriveHeldBatteryUi(__instance))
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(BatteryUI), "Update")]
internal static class MilitaryTerminalHeldBatteryUiUpdatePatch
{
    private static bool Prefix(BatteryUI __instance)
    {
        MilitaryTerminalToolUiPatchHelpers.TryDriveHeldBatteryUi(__instance);
        return true;
    }
}

[HarmonyPatch(typeof(InventorySpot), "StateOccupied")]
internal static class MilitaryTerminalInventoryBatteryUiPatch
{
    private static readonly AccessTools.FieldRef<InventorySpot, BatteryVisualLogic> BatteryVisualLogicRef =
        AccessTools.FieldRefAccess<InventorySpot, BatteryVisualLogic>("batteryVisualLogic");

    private static readonly AccessTools.FieldRef<InventorySpot, bool> StateStartRef =
        AccessTools.FieldRefAccess<InventorySpot, bool>("stateStart");

    private static bool Prefix(InventorySpot __instance)
    {
        if (!MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(__instance.CurrentItem))
        {
            return true;
        }

        var batteryVisualLogic = BatteryVisualLogicRef(__instance);
        if (batteryVisualLogic is not null)
        {
            MilitaryTerminalToolUiPatchHelpers.HideBatteryVisualLogic(batteryVisualLogic);
        }

        StateStartRef(__instance) = false;
        return false;
    }
}

[HarmonyPatch(typeof(ItemBattery), "Start")]
internal static class MilitaryTerminalItemBatteryStartPatch
{
    private static bool Prefix(ItemBattery __instance)
    {
        if (!MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(__instance))
        {
            return true;
        }

        MilitaryTerminalToolUiPatchHelpers.SuppressTerminalBatteryPresentation(__instance);
        __instance.enabled = false;
        return false;
    }
}

[HarmonyPatch(typeof(ItemBattery), "Update")]
internal static class MilitaryTerminalItemBatteryUpdatePatch
{
    private static bool Prefix(ItemBattery __instance)
    {
        if (!MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(__instance))
        {
            return true;
        }

        MilitaryTerminalToolUiPatchHelpers.SuppressTerminalBatteryPresentation(__instance);
        __instance.enabled = false;
        return false;
    }
}

[HarmonyPatch(typeof(ItemToggle), "Update")]
internal static class MilitaryTerminalItemToggleUpdatePatch
{
    private static readonly AccessTools.FieldRef<ItemToggle, bool> ToggleDisabledRef =
        AccessTools.FieldRefAccess<ItemToggle, bool>("disabled");

    private static bool Prefix(ItemToggle __instance)
    {
        if (!MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(__instance))
        {
            return true;
        }

        __instance.toggleState = false;
        __instance.toggleStatePrevious = false;
        ToggleDisabledRef(__instance) = true;
        __instance.enabled = false;
        return false;
    }
}

[HarmonyPatch(typeof(Inventory), nameof(Inventory.GetBatteryStateFromInventorySpot))]
internal static class MilitaryTerminalInventoryBatteryStatePatch
{
    private static bool Prefix(Inventory __instance, int index, ref int __result)
    {
        if (__instance is null)
        {
            return true;
        }

        var spot = __instance.GetSpotByIndex(index);
        if (spot?.CurrentItem is null || !MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(spot.CurrentItem))
        {
            return true;
        }

        __result = -1;
        return false;
    }
}

[HarmonyPatch(typeof(InventoryBattery), nameof(InventoryBattery.BatteryFetch))]
internal static class MilitaryTerminalInventoryBatteryFetchPatch
{
    private static bool Prefix(InventoryBattery __instance)
    {
        if (!MilitaryTerminalHeldUiSuppressionService.ShouldSuppressAnyMilitaryTerminalBatteryUi()
            && !MilitaryTerminalToolUiPatchHelpers.IsInventoryBatteryBoundToOfficialTerminal(__instance))
        {
            return true;
        }

        MilitaryTerminalToolUiPatchHelpers.HideInventoryBattery(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(InventoryBattery), nameof(InventoryBattery.BatteryShow))]
internal static class MilitaryTerminalInventoryBatteryShowPatch
{
    private static bool Prefix(InventoryBattery __instance)
    {
        if (!MilitaryTerminalHeldUiSuppressionService.ShouldSuppressAnyMilitaryTerminalBatteryUi()
            && !MilitaryTerminalToolUiPatchHelpers.IsInventoryBatteryBoundToOfficialTerminal(__instance))
        {
            return true;
        }

        MilitaryTerminalToolUiPatchHelpers.HideInventoryBattery(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(InventoryBattery), "Update")]
internal static class MilitaryTerminalInventoryBatteryUpdatePatch
{
    private static bool Prefix(InventoryBattery __instance)
    {
        if (!MilitaryTerminalHeldUiSuppressionService.ShouldSuppressAnyMilitaryTerminalBatteryUi()
            && !MilitaryTerminalToolUiPatchHelpers.IsInventoryBatteryBoundToOfficialTerminal(__instance))
        {
            return true;
        }

        MilitaryTerminalToolUiPatchHelpers.HideInventoryBattery(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(BatteryVisualLogic), nameof(BatteryVisualLogic.BatteryBarsSet))]
internal static class MilitaryTerminalBatteryVisualBarsSetPatch
{
    private static bool Prefix(BatteryVisualLogic __instance)
    {
        return MilitaryTerminalToolUiPatchHelpers.AllowBatteryVisualLogicExecution(__instance);
    }
}

[HarmonyPatch(typeof(BatteryVisualLogic), nameof(BatteryVisualLogic.BatteryBarsUpdate))]
internal static class MilitaryTerminalBatteryVisualBarsUpdatePatch
{
    private static bool Prefix(BatteryVisualLogic __instance)
    {
        return MilitaryTerminalToolUiPatchHelpers.AllowBatteryVisualLogicExecution(__instance);
    }
}

[HarmonyPatch(typeof(BatteryVisualLogic), "Update")]
internal static class MilitaryTerminalBatteryVisualUpdatePatch
{
    private static bool Prefix(BatteryVisualLogic __instance)
    {
        return MilitaryTerminalToolUiPatchHelpers.AllowBatteryVisualLogicExecution(__instance);
    }
}

internal static class MilitaryTerminalToolUiPatchHelpers
{
    private static readonly AccessTools.FieldRef<ItemBattery, BatteryVisualLogic> BatteryVisualLogicRef =
        AccessTools.FieldRefAccess<ItemBattery, BatteryVisualLogic>("batteryVisualLogic");

    private static readonly AccessTools.FieldRef<BatteryUI, int> BatteryCurrentBarsRef =
        AccessTools.FieldRefAccess<BatteryUI, int>("batteryCurrentBars");

    private static readonly AccessTools.FieldRef<BatteryUI, int> BatteryCurrentBarsMaxRef =
        AccessTools.FieldRefAccess<BatteryUI, int>("batteryCurrenyBarsMax");

    private static readonly AccessTools.FieldRef<BatteryUI, int> BatteryCurrentBarsPrevRef =
        AccessTools.FieldRefAccess<BatteryUI, int>("batteryCurrentBarsPrev");

    private static readonly AccessTools.FieldRef<BatteryUI, float> BatteryShowTimerRef =
        AccessTools.FieldRefAccess<BatteryUI, float>("batteryShowTimer");

    private static readonly AccessTools.FieldRef<SemiUI, float> SemiUiShowTimerRef =
        AccessTools.FieldRefAccess<SemiUI, float>("showTimer");

    private static readonly AccessTools.FieldRef<InventoryBattery, float> InventoryBatteryShowTimerRef =
        AccessTools.FieldRefAccess<InventoryBattery, float>("batteryShowTimer");

    internal static bool ShouldSuppressHeldBatteryUi()
    {
        return MilitaryTerminalHeldUiSuppressionService.IsHoldingOfficialMilitaryTerminal();
    }

    internal static bool TryDriveHeldBatteryUi(BatteryUI batteryUi)
    {
        if (batteryUi is null)
        {
            return false;
        }

        if (!ValuableHoldRadarService.TryGetHeldBatteryUiState(out var batteryUiState))
        {
            return false;
        }

        BatteryCurrentBarsRef(batteryUi) = batteryUiState.CurrentBars;
        BatteryCurrentBarsMaxRef(batteryUi) = batteryUiState.MaxBars;
        BatteryCurrentBarsPrevRef(batteryUi) = batteryUiState.CurrentBars;
        BatteryShowTimerRef(batteryUi) = 0.25f;
        SemiUiShowTimerRef(batteryUi) = 0.25f;

        if (batteryUi.batteryVisualLogic is not null)
        {
            var needsVisualReset = batteryUi.batteryVisualLogic.itemBattery is not null
                || batteryUi.batteryVisualLogic.batteryBars != batteryUiState.MaxBars;

            batteryUi.batteryVisualLogic.itemBattery = null;
            if (!batteryUi.batteryVisualLogic.gameObject.activeSelf)
            {
                batteryUi.batteryVisualLogic.gameObject.SetActive(true);
            }

            if (needsVisualReset)
            {
                batteryUi.batteryVisualLogic.BatteryBarsSet();
            }

            batteryUi.batteryVisualLogic.BatteryBarsUpdate(batteryUiState.CurrentBars, _forceUpdate: true);

            if (batteryUiState.TargetSwitchBlockedByBattery || batteryUiState.CurrentBars <= batteryUiState.MaxBars / 2)
            {
                batteryUi.batteryVisualLogic.OverrideChargeNeeded(0.2f);
            }
        }

        batteryUi.Show();
        return true;
    }

    internal static bool AllowBatteryVisualLogicExecution(BatteryVisualLogic batteryVisualLogic)
    {
        if (batteryVisualLogic is null)
        {
            return true;
        }

        if (!ShouldSuppressBatteryVisualLogic(batteryVisualLogic))
        {
            return true;
        }

        HideBatteryVisualLogic(batteryVisualLogic);
        return false;
    }

    internal static void SuppressTerminalBatteryPresentation(ItemBattery itemBattery)
    {
        if (itemBattery is null)
        {
            return;
        }

        itemBattery.batteryActive = false;
        itemBattery.onlyShowWhenItemToggleIsOn = false;

        var batteryVisualLogic = BatteryVisualLogicRef(itemBattery) ?? itemBattery.GetComponentInChildren<BatteryVisualLogic>(true);
        if (batteryVisualLogic is not null)
        {
            HideBatteryVisualLogic(batteryVisualLogic);
        }

        if (BatteryUI.instance is not null)
        {
            SuppressGlobalBatteryUi(BatteryUI.instance);
        }
    }

    internal static void SuppressGlobalBatteryUi(BatteryUI batteryUi)
    {
        if (batteryUi is null)
        {
            return;
        }

        BatteryShowTimerRef(batteryUi) = 0f;
        SemiUiShowTimerRef(batteryUi) = 0f;

        if (batteryUi.batteryVisualLogic is not null)
        {
            HideBatteryVisualLogic(batteryUi.batteryVisualLogic);
        }

        batteryUi.Hide();
    }

    internal static void HideBatteryVisualLogic(BatteryVisualLogic batteryVisualLogic)
    {
        if (batteryVisualLogic is null)
        {
            return;
        }

        batteryVisualLogic.itemBattery = null;
        batteryVisualLogic.BatteryOutro();
        batteryVisualLogic.gameObject.SetActive(false);
    }

    internal static bool IsInventoryBatteryBoundToOfficialTerminal(InventoryBattery inventoryBattery)
    {
        if (inventoryBattery is null || Inventory.instance is null)
        {
            return false;
        }

        var spotIndex = inventoryBattery.inventorySpot;
        if (spotIndex < 0)
        {
            return false;
        }

        var spots = Inventory.instance.GetAllSpots();
        if (spotIndex >= spots.Count)
        {
            return false;
        }

        var spot = spots[spotIndex];
        return spot?.CurrentItem is not null && MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(spot.CurrentItem);
    }

    internal static void HideInventoryBattery(InventoryBattery inventoryBattery)
    {
        if (inventoryBattery is null)
        {
            return;
        }

        InventoryBatteryShowTimerRef(inventoryBattery) = 0f;
        inventoryBattery.transform.localScale = Vector3.zero;

        if (inventoryBattery.batteryImage is null)
        {
            inventoryBattery.batteryImage = inventoryBattery.GetComponent<UnityEngine.UI.RawImage>();
        }

        if (inventoryBattery.batteryImage is not null)
        {
            inventoryBattery.batteryImage.enabled = false;
        }
    }

    private static bool ShouldSuppressBatteryVisualLogic(BatteryVisualLogic batteryVisualLogic)
    {
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

        var batteryUi = batteryVisualLogic.GetComponentInParent<BatteryUI>();
        return MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(batteryVisualLogic.gameObject)
            || MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(batteryVisualLogic.transform.root.gameObject);
    }
}
