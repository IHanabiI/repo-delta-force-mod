using UnityEngine;

namespace RepoDeltaForceMod;

[DisallowMultipleComponent]
public sealed class HavocFlightRecorderBehaviour : MonoBehaviour
{
    private const string DefaultDisplayName = "\u98de\u884c\u8bb0\u5f55\u4eea";

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
        if (!logAuthoringWarnings)
        {
            return;
        }

        var identity = GetComponent<HavocSupplyIdentity>();
        if (identity is null)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"HavocFlightRecorderBehaviour is missing HavocSupplyIdentity on '{name}'. Official flight recorder detection may fall back to name matching only.");
            return;
        }

        if (identity.StableId != FlightRecorderIdentity.StableId)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"HavocFlightRecorderBehaviour on '{name}' has unexpected StableId '{identity.StableId}'. Expected '{FlightRecorderIdentity.StableId}'.");
            return;
        }

        var validation = HavocSupplyContractValidator.ValidateFlightRecorder(this);
        if (!validation.IsReadyAsHighValueValuable)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"HavocFlightRecorderBehaviour on '{name}' is not wired as a full high-value valuable yet. Missing={string.Join(", ", validation.MissingParts)}");
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"HavocFlightRecorderBehaviour ready: Name={name} | DisplayName={GetReadableDisplayName()} | StableId={identity.StableId} | Contract={validation.ContractStatusLine} | Value={validation.ValueStatusLine}");
    }

    private void LateUpdate()
    {
        FlightRecorderAutoSpawnService.EnforceToolOnlyComponentsDisabled(gameObject);
    }

    private string GetReadableDisplayName()
    {
        return string.IsNullOrWhiteSpace(debugDisplayName)
            ? DefaultDisplayName
            : debugDisplayName;
    }
}
