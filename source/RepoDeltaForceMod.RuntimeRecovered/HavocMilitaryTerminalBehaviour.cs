using UnityEngine;

namespace RepoDeltaForceMod;

[DisallowMultipleComponent]
public sealed class HavocMilitaryTerminalBehaviour : MonoBehaviour
{
    private const string DefaultDisplayName = "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef";
    private const string ItemLabelEnglish = "Havoc Military Terminal";

    [SerializeField]
    private string debugDisplayName = DefaultDisplayName;

    [SerializeField]
    private bool logAuthoringWarnings = true;

    private void Reset()
    {
        debugDisplayName = DefaultDisplayName;
    }

    private void Awake()
    {
        MilitaryTerminalAutoSpawnService.EnforceToolBatteryPresentationDisabled(gameObject);
        ApplyDisplayNameOverride();

        if (!logAuthoringWarnings)
        {
            return;
        }

        var identity = GetComponent<HavocSupplyIdentity>();
        if (identity is null)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"HavocMilitaryTerminalBehaviour is missing HavocSupplyIdentity on '{name}'. Official terminal detection may fall back to name matching only.");
            return;
        }

        if (identity.StableId != MilitaryTerminalIdentity.StableId)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"HavocMilitaryTerminalBehaviour on '{name}' has unexpected StableId '{identity.StableId}'. Expected '{MilitaryTerminalIdentity.StableId}'.");
            return;
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"HavocMilitaryTerminalBehaviour ready: Name={name} | DisplayName={GetReadableDisplayName()} | StableId={identity.StableId}");
    }

    private void LateUpdate()
    {
        MilitaryTerminalAutoSpawnService.EnforceToolBatteryPresentationDisabled(gameObject);
        ApplyDisplayNameOverride();
    }

    private void ApplyDisplayNameOverride()
    {
        if (!TryGetComponent<ItemAttributes>(out var itemAttributes))
        {
            return;
        }

        itemAttributes.itemName = ItemLabelEnglish;
        if (itemAttributes.item != null)
        {
            itemAttributes.item.itemName = ItemLabelEnglish;
        }
    }

    private string GetReadableDisplayName()
    {
        return string.IsNullOrWhiteSpace(debugDisplayName)
            ? DefaultDisplayName
            : debugDisplayName;
    }
}
