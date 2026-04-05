using HarmonyLib;

namespace RepoDeltaForceMod;

[HarmonyPatch(typeof(ItemEquippable))]
internal static class FlightRecorderInventoryPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ItemEquippable.RequestEquip))]
    private static bool PrefixRequestEquip(ItemEquippable __instance)
    {
        return ShouldAllowEquip(__instance, "RequestEquip");
    }

    [HarmonyPrefix]
    [HarmonyPatch("RPC_RequestEquip")]
    private static bool PrefixRpcRequestEquip(ItemEquippable __instance)
    {
        return ShouldAllowEquip(__instance, "RPC_RequestEquip");
    }

    [HarmonyPrefix]
    [HarmonyPatch("RPC_UpdateItemState")]
    private static bool PrefixRpcUpdateItemState(ItemEquippable __instance, int state)
    {
        if (state != (int)ItemEquippable.ItemState.Equipped)
        {
            return true;
        }

        return ShouldAllowEquip(__instance, "RPC_UpdateItemState");
    }

    private static bool ShouldAllowEquip(ItemEquippable itemEquippable, string sourceMethod)
    {
        if (!FlightRecorderIdentity.IsOfficialFlightRecorder(itemEquippable))
        {
            return true;
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"Flight recorder inventory equip blocked via {sourceMethod}: official flight recorder should remain a carried valuable, not a storable tool.");
        return false;
    }
}
