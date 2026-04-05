using System;
using System.Collections.Generic;
using UnityEngine;

namespace RepoDeltaForceMod;

internal static class HavocSupplyContractValidator
{
    internal static HavocSupplyContractValidationResult ValidateFlightRecorder(object? value)
    {
        return Validate(
            value,
            expectedStableId: FlightRecorderIdentity.StableId,
            expectedBehaviourTypeName: nameof(HavocFlightRecorderBehaviour));
    }

    private static HavocSupplyContractValidationResult Validate(
        object? value,
        string expectedStableId,
        string expectedBehaviourTypeName)
    {
        if (value is null)
        {
            return HavocSupplyContractValidationResult.MissingTarget();
        }

        var transform = TryGetTransform(value);
        var sceneInfo = ObservedSceneObjectInfo.From(value);
        var hasIdentity = false;
        var hasExpectedStableId = false;
        var hasRuntimeBehaviour = false;
        var hasItemAttributes = false;
        var hasValuableObject = false;
        var hasRigidbody = false;
        var hasCollider = false;

        if (transform is not null)
        {
            for (Transform? current = transform; current is not null; current = current.parent)
            {
                foreach (var component in current.GetComponents<Component>())
                {
                    if (component is null)
                    {
                        continue;
                    }

                    var typeName = component.GetType().Name;
                    if (string.Equals(typeName, nameof(HavocSupplyIdentity), StringComparison.Ordinal))
                    {
                        hasIdentity = true;
                        var stableId = ObservationReflection.TryGetKnownValue(component, "StableId")?.ToString()
                            ?? ObservationReflection.TryGetKnownValue(component, "stableId")?.ToString();
                        if (string.Equals(stableId, expectedStableId, StringComparison.Ordinal))
                        {
                            hasExpectedStableId = true;
                        }
                    }
                    else if (string.Equals(typeName, expectedBehaviourTypeName, StringComparison.Ordinal))
                    {
                        hasRuntimeBehaviour = true;
                    }
                    else if (string.Equals(typeName, "ItemAttributes", StringComparison.Ordinal))
                    {
                        hasItemAttributes = true;
                    }
                    else if (string.Equals(typeName, "ValuableObject", StringComparison.Ordinal))
                    {
                        hasValuableObject = true;
                    }
                }
            }

            hasRigidbody = transform.GetComponentInParent<Rigidbody>() is not null
                || transform.GetComponentInChildren<Rigidbody>(true) is not null;
            hasCollider = transform.GetComponentInParent<Collider>() is not null
                || transform.GetComponentsInChildren<Collider>(true).Length > 0;
        }

        var missingParts = new List<string>();
        if (!hasIdentity)
        {
            missingParts.Add("HavocSupplyIdentity");
        }
        else if (!hasExpectedStableId)
        {
            missingParts.Add("StableId");
        }

        if (!hasRuntimeBehaviour)
        {
            missingParts.Add(expectedBehaviourTypeName);
        }

        if (!hasItemAttributes)
        {
            missingParts.Add("ItemAttributes");
        }

        if (!hasValuableObject)
        {
            missingParts.Add("ValuableObject");
        }

        if (!hasRigidbody)
        {
            missingParts.Add("Rigidbody");
        }

        if (!hasCollider)
        {
            missingParts.Add("Collider");
        }

        var hasValueReadings = sceneInfo.DollarValueCurrent.HasValue || sceneInfo.DollarValueOriginal.HasValue;
        var dollarValueInitialized = sceneInfo.DollarValueSet != false;
        if (!hasValueReadings && dollarValueInitialized)
        {
            missingParts.Add("价值读数");
        }

        return new HavocSupplyContractValidationResult(
            hasTarget: true,
            hasIdentity: hasIdentity,
            hasExpectedStableId: hasExpectedStableId,
            hasRuntimeBehaviour: hasRuntimeBehaviour,
            hasItemAttributes: hasItemAttributes,
            hasValuableObject: hasValuableObject,
            hasRigidbody: hasRigidbody,
            hasCollider: hasCollider,
            dollarValueSet: sceneInfo.DollarValueSet,
            dollarValueCurrent: sceneInfo.DollarValueCurrent,
            dollarValueOriginal: sceneInfo.DollarValueOriginal,
            missingParts: missingParts.ToArray());
    }

    private static Transform? TryGetTransform(object? value)
    {
        return value switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null,
        };
    }
}

internal readonly struct HavocSupplyContractValidationResult
{
    internal HavocSupplyContractValidationResult(
        bool hasTarget,
        bool hasIdentity,
        bool hasExpectedStableId,
        bool hasRuntimeBehaviour,
        bool hasItemAttributes,
        bool hasValuableObject,
        bool hasRigidbody,
        bool hasCollider,
        bool? dollarValueSet,
        float? dollarValueCurrent,
        float? dollarValueOriginal,
        IReadOnlyList<string> missingParts)
    {
        HasTarget = hasTarget;
        HasIdentity = hasIdentity;
        HasExpectedStableId = hasExpectedStableId;
        HasRuntimeBehaviour = hasRuntimeBehaviour;
        HasItemAttributes = hasItemAttributes;
        HasValuableObject = hasValuableObject;
        HasRigidbody = hasRigidbody;
        HasCollider = hasCollider;
        DollarValueSet = dollarValueSet;
        DollarValueCurrent = dollarValueCurrent;
        DollarValueOriginal = dollarValueOriginal;
        MissingParts = missingParts;
    }

    internal bool HasTarget { get; }
    internal bool HasIdentity { get; }
    internal bool HasExpectedStableId { get; }
    internal bool HasRuntimeBehaviour { get; }
    internal bool HasItemAttributes { get; }
    internal bool HasValuableObject { get; }
    internal bool HasRigidbody { get; }
    internal bool HasCollider { get; }
    internal bool? DollarValueSet { get; }
    internal float? DollarValueCurrent { get; }
    internal float? DollarValueOriginal { get; }
    internal IReadOnlyList<string> MissingParts { get; }

    internal bool IsReadyAsHighValueValuable => HasTarget && MissingParts.Count == 0;

    internal string ContractStatusLine =>
        !HasTarget
            ? "valuable链路：未找到目标"
            : IsReadyAsHighValueValuable
                ? "valuable链路：已接入"
                : $"valuable链路：待补全（{string.Join("、", MissingParts)}）";

    internal string ValueStatusLine
    {
        get
        {
            if (DollarValueSet == false)
            {
                return "Value reading: awaiting random price initialization";
            }

            if (DollarValueCurrent.HasValue || DollarValueOriginal.HasValue)
            {
                var current = DollarValueCurrent.HasValue ? $"${DollarValueCurrent.Value:0.##}" : "?";
                var original = DollarValueOriginal.HasValue ? $"${DollarValueOriginal.Value:0.##}" : "?";
                return $"价值读数：当前 {current} / 原始 {original}";
            }

            return "价值读数：尚未接入";
        }
    }

    internal static HavocSupplyContractValidationResult MissingTarget()
    {
        return new HavocSupplyContractValidationResult(
            hasTarget: false,
            hasIdentity: false,
            hasExpectedStableId: false,
            hasRuntimeBehaviour: false,
            hasItemAttributes: false,
            hasValuableObject: false,
            hasRigidbody: false,
            hasCollider: false,
            dollarValueSet: null,
            dollarValueCurrent: null,
            dollarValueOriginal: null,
            missingParts: Array.Empty<string>());
    }
}
