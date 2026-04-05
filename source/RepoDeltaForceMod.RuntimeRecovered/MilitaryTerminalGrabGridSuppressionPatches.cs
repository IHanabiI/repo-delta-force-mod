using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

[HarmonyPatch(typeof(PhysGrabber), "GridObjectsInstantiate")]
internal static class MilitaryTerminalGrabGridInstantiatePatch
{
    private static bool Prefix(PhysGrabber __instance)
    {
        if (__instance is null || !MilitaryTerminalGrabGridSuppressionHelpers.IsHoldingOfficialTerminal(__instance))
        {
            return true;
        }

        MilitaryTerminalGrabGridSuppressionHelpers.ClearGridObjects(__instance);
        return false;
    }
}

internal static class MilitaryTerminalGrabGridSuppressionHelpers
{
    private static readonly AccessTools.FieldRef<PhysGrabber, List<GameObject>> PhysGrabPointVisualGridObjectsRef =
        AccessTools.FieldRefAccess<PhysGrabber, List<GameObject>>("physGrabPointVisualGridObjects");

    internal static bool IsHoldingOfficialTerminal(PhysGrabber physGrabber)
    {
        if (physGrabber is null || !physGrabber.grabbed)
        {
            return false;
        }

        return MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(physGrabber.grabbedPhysGrabObject)
            || MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(physGrabber.grabbedObjectTransform?.gameObject);
    }

    internal static void ClearGridObjects(PhysGrabber physGrabber)
    {
        if (physGrabber is null)
        {
            return;
        }

        var gridObjects = PhysGrabPointVisualGridObjectsRef(physGrabber);
        if (gridObjects is not null)
        {
            foreach (var gridObject in gridObjects)
            {
                if (gridObject is not null)
                {
                    UnityObject.Destroy(gridObject);
                }
            }

            gridObjects.Clear();
        }

        if (physGrabber.physGrabPointVisualGrid is not null)
        {
            foreach (Transform child in physGrabber.physGrabPointVisualGrid)
            {
                if (child is not null)
                {
                    child.gameObject.SetActive(false);
                }
            }

            physGrabber.physGrabPointVisualGrid.gameObject.SetActive(false);
        }
    }
}
