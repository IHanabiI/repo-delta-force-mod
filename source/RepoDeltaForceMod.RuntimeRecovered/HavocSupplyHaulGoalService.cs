using HarmonyLib;
using UnityEngine;

namespace RepoDeltaForceMod;

internal static class HavocSupplyHaulGoalService
{
    private const int SpecialValuableHaulGoalContribution = 2000;

    internal static int ResolveContribution(GameObject gameObject, ValuableObject valuableObject)
    {
        if (gameObject == null || valuableObject == null)
        {
            return 0;
        }

        if (AirDropCaseIdentity.IsOfficialAirDropCase(gameObject))
        {
            return SpecialValuableHaulGoalContribution;
        }

        if (FlightRecorderIdentity.IsOfficialFlightRecorder(gameObject))
        {
            return SpecialValuableHaulGoalContribution;
        }

        if (MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(gameObject))
        {
            return 0;
        }

        return Mathf.RoundToInt(valuableObject.dollarValueCurrent);
    }

    internal static bool IsOfficialHavocSupply(GameObject gameObject)
    {
        return gameObject != null
            && (AirDropCaseIdentity.IsOfficialAirDropCase(gameObject)
                || FlightRecorderIdentity.IsOfficialFlightRecorder(gameObject)
                || MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(gameObject));
    }
}

[HarmonyPatch(typeof(ValuableObject), "DollarValueSet")]
internal static class HavocSupplyHaulGoalPatch
{
    private static void Prefix(ValuableObject __instance, ref int __state)
    {
        __state = RoundDirector.instance != null
            ? RoundDirector.instance.haulGoalMax
            : 0;
    }

    private static void Postfix(ValuableObject __instance, int __state)
    {
        if (__instance == null || RoundDirector.instance == null)
        {
            return;
        }

        var contribution = HavocSupplyHaulGoalService.ResolveContribution(__instance.gameObject, __instance);
        RoundDirector.instance.haulGoalMax = __state + contribution;

        if (HavocSupplyHaulGoalService.IsOfficialHavocSupply(__instance.gameObject))
        {
            RepoDeltaForceMod.Logger.LogInfo(
                $"Havoc haul-goal contribution applied: Object={__instance.gameObject.name} | ValueCurrent={(int)__instance.dollarValueCurrent} | Contribution={contribution} | HaulGoalMax={RoundDirector.instance.haulGoalMax}");
        }
    }
}
