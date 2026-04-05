using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal sealed class GrabObservationSnapshot
{
    internal GrabObservationSnapshot(
        int observationId,
        string sourceMethod,
        DateTimeOffset observedAtUtc,
        int frameCount,
        string grabberType,
        string? grabberName,
        string? grabbedObjectType,
        string? grabbedObjectName,
        int? grabbedObjectInstanceId,
        string? hostGameObjectName,
        string? hostGameObjectPath,
        IReadOnlyList<string> interestingComponentTypes,
        string? valuableKind,
        float? dollarValueCurrent,
        float? dollarValueOriginal,
        int? dollarValueOverride,
        bool? valuableDiscovered,
        bool? valuableDiscoveredReminder,
        bool? isValuable,
        bool? isNonValuable,
        bool hasValuableComponent,
        bool hasNotValuableComponent,
        bool hasPhysGrabObjectComponent,
        bool looksValuableByName)
    {
        ObservationId = observationId;
        SourceMethod = sourceMethod;
        ObservedAtUtc = observedAtUtc;
        FrameCount = frameCount;
        GrabberType = grabberType;
        GrabberName = grabberName;
        GrabbedObjectType = grabbedObjectType;
        GrabbedObjectName = grabbedObjectName;
        GrabbedObjectInstanceId = grabbedObjectInstanceId;
        HostGameObjectName = hostGameObjectName;
        HostGameObjectPath = hostGameObjectPath;
        InterestingComponentTypes = interestingComponentTypes;
        ValuableKind = valuableKind;
        DollarValueCurrent = dollarValueCurrent;
        DollarValueOriginal = dollarValueOriginal;
        DollarValueOverride = dollarValueOverride;
        ValuableDiscovered = valuableDiscovered;
        ValuableDiscoveredReminder = valuableDiscoveredReminder;
        IsValuable = isValuable;
        IsNonValuable = isNonValuable;
        HasValuableComponent = hasValuableComponent;
        HasNotValuableComponent = hasNotValuableComponent;
        HasPhysGrabObjectComponent = hasPhysGrabObjectComponent;
        LooksValuableByName = looksValuableByName;
    }

    internal int ObservationId { get; }
    internal string SourceMethod { get; }
    internal DateTimeOffset ObservedAtUtc { get; }
    internal int FrameCount { get; }
    internal string GrabberType { get; }
    internal string? GrabberName { get; }
    internal string? GrabbedObjectType { get; }
    internal string? GrabbedObjectName { get; }
    internal int? GrabbedObjectInstanceId { get; }
    internal string? HostGameObjectName { get; }
    internal string? HostGameObjectPath { get; }
    internal IReadOnlyList<string> InterestingComponentTypes { get; }
    internal string? ValuableKind { get; }
    internal float? DollarValueCurrent { get; }
    internal float? DollarValueOriginal { get; }
    internal int? DollarValueOverride { get; }
    internal bool? ValuableDiscovered { get; }
    internal bool? ValuableDiscoveredReminder { get; }
    internal bool? IsValuable { get; }
    internal bool? IsNonValuable { get; }
    internal bool HasValuableComponent { get; }
    internal bool HasNotValuableComponent { get; }
    internal bool HasPhysGrabObjectComponent { get; }
    internal bool LooksValuableByName { get; }

    internal bool IsValuableLike => ValueClassification is "valuable" or "valuable-component" or "valuable-like-name";

    internal string ValueClassification
    {
        get
        {
            if (IsValuable == true)
            {
                return "valuable";
            }

            if (HasValuableComponent)
            {
                return "valuable-component";
            }

            if (IsNonValuable == true)
            {
                return "non-valuable";
            }

            if (HasNotValuableComponent)
            {
                return "non-valuable-component";
            }

            if (LooksValuableByName)
            {
                return "valuable-like-name";
            }

            return "unknown";
        }
    }
}

internal sealed class ObservedSceneObjectInfo
{
    private static readonly string[] DetectionKeywords =
    [
        "valuable",
        "grab",
        "item",
        "loot",
        "scrap",
        "phys",
    ];

    private ObservedSceneObjectInfo(
        string? hostGameObjectName,
        string? hostGameObjectPath,
        IReadOnlyList<string> interestingComponentTypes,
        string? valuableKind,
        bool? dollarValueSet,
        float? dollarValueCurrent,
        float? dollarValueOriginal,
        int? dollarValueOverride,
        bool? valuableDiscovered,
        bool? valuableDiscoveredReminder,
        bool hasValuableComponent,
        bool hasNotValuableComponent,
        bool hasPhysGrabObjectComponent)
    {
        HostGameObjectName = hostGameObjectName;
        HostGameObjectPath = hostGameObjectPath;
        InterestingComponentTypes = interestingComponentTypes;
        ValuableKind = valuableKind;
        DollarValueSet = dollarValueSet;
        DollarValueCurrent = dollarValueCurrent;
        DollarValueOriginal = dollarValueOriginal;
        DollarValueOverride = dollarValueOverride;
        ValuableDiscovered = valuableDiscovered;
        ValuableDiscoveredReminder = valuableDiscoveredReminder;
        HasValuableComponent = hasValuableComponent;
        HasNotValuableComponent = hasNotValuableComponent;
        HasPhysGrabObjectComponent = hasPhysGrabObjectComponent;
    }

    internal string? HostGameObjectName { get; }
    internal string? HostGameObjectPath { get; }
    internal IReadOnlyList<string> InterestingComponentTypes { get; }
    internal string? ValuableKind { get; }
    internal bool? DollarValueSet { get; }
    internal float? DollarValueCurrent { get; }
    internal float? DollarValueOriginal { get; }
    internal int? DollarValueOverride { get; }
    internal bool? ValuableDiscovered { get; }
    internal bool? ValuableDiscoveredReminder { get; }
    internal bool HasValuableComponent { get; }
    internal bool HasNotValuableComponent { get; }
    internal bool HasPhysGrabObjectComponent { get; }

    internal static ObservedSceneObjectInfo From(object? value)
    {
        var gameObject = TryGetGameObject(value);
        if (gameObject is null)
        {
            return new ObservedSceneObjectInfo(
                hostGameObjectName: null,
                hostGameObjectPath: null,
                interestingComponentTypes: Array.Empty<string>(),
                valuableKind: null,
                dollarValueSet: null,
                dollarValueCurrent: null,
                dollarValueOriginal: null,
                dollarValueOverride: null,
                valuableDiscovered: null,
                valuableDiscoveredReminder: null,
                hasValuableComponent: false,
                hasNotValuableComponent: false,
                hasPhysGrabObjectComponent: false);
        }

        var interestingComponents = CollectInterestingComponentInstances(gameObject.transform);
        var interestingComponentTypes = interestingComponents
            .Select(component => component.GetType().Name)
            .ToArray();
        var valuableMetadata = ValuableMetadata.From(interestingComponents);
        return new ObservedSceneObjectInfo(
            hostGameObjectName: gameObject.name,
            hostGameObjectPath: BuildTransformPath(gameObject.transform),
            interestingComponentTypes: interestingComponentTypes,
            valuableKind: valuableMetadata.ValuableKind,
            dollarValueSet: valuableMetadata.DollarValueSet,
            dollarValueCurrent: valuableMetadata.DollarValueCurrent,
            dollarValueOriginal: valuableMetadata.DollarValueOriginal,
            dollarValueOverride: valuableMetadata.DollarValueOverride,
            valuableDiscovered: valuableMetadata.ValuableDiscovered,
            valuableDiscoveredReminder: valuableMetadata.ValuableDiscoveredReminder,
            hasValuableComponent: HasExactComponentType(interestingComponentTypes, "ValuableObject"),
            hasNotValuableComponent: HasExactComponentType(interestingComponentTypes, "NotValuableObject"),
            hasPhysGrabObjectComponent: ContainsKeyword(interestingComponentTypes, "physgrab"));
    }

    internal static bool ShouldIncludeComponentType(string componentTypeName)
    {
        if (componentTypeName.StartsWith("Unity", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var keyword in DetectionKeywords)
        {
            if (componentTypeName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject? TryGetGameObject(object? value)
    {
        return value switch
        {
            GameObject gameObject => gameObject,
            Component component => component.gameObject,
            _ => null,
        };
    }

    private static IReadOnlyList<Component> CollectInterestingComponentInstances(Transform transform)
    {
        var result = new List<Component>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (Transform? current = transform; current is not null; current = current.parent)
        {
            foreach (var component in current.GetComponents<Component>())
            {
                if (component is null)
                {
                    continue;
                }

                var componentTypeName = component.GetType().Name;
                if (!ShouldIncludeComponentType(componentTypeName))
                {
                    continue;
                }

                if (seen.Add(componentTypeName))
                {
                    result.Add(component);
                }
            }
        }

        return result;
    }

    private static bool ContainsKeyword(IReadOnlyList<string> componentTypes, string keyword)
    {
        foreach (var componentType in componentTypes)
        {
            if (componentType.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExactComponentType(IReadOnlyList<string> componentTypes, string typeName)
    {
        foreach (var componentType in componentTypes)
        {
            if (string.Equals(componentType, typeName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildTransformPath(Transform transform)
    {
        var parts = new List<string>();

        for (Transform? current = transform; current is not null; current = current.parent)
        {
            parts.Insert(0, current.name);
        }

        return string.Join("/", parts);
    }

    private sealed class ValuableMetadata
    {
        private ValuableMetadata(
            string? valuableKind,
            bool? dollarValueSet,
            float? dollarValueCurrent,
            float? dollarValueOriginal,
            int? dollarValueOverride,
            bool? valuableDiscovered,
            bool? valuableDiscoveredReminder)
        {
            ValuableKind = valuableKind;
            DollarValueSet = dollarValueSet;
            DollarValueCurrent = dollarValueCurrent;
            DollarValueOriginal = dollarValueOriginal;
            DollarValueOverride = dollarValueOverride;
            ValuableDiscovered = valuableDiscovered;
            ValuableDiscoveredReminder = valuableDiscoveredReminder;
        }

        internal string? ValuableKind { get; }
        internal bool? DollarValueSet { get; }
        internal float? DollarValueCurrent { get; }
        internal float? DollarValueOriginal { get; }
        internal int? DollarValueOverride { get; }
        internal bool? ValuableDiscovered { get; }
        internal bool? ValuableDiscoveredReminder { get; }

        internal static ValuableMetadata From(IReadOnlyList<Component> interestingComponents)
        {
            object? valuableObjectComponent = null;
            string? valuableKind = null;

            foreach (var component in interestingComponents)
            {
                var componentTypeName = component.GetType().Name;

                if (componentTypeName == "ValuableObject")
                {
                    valuableObjectComponent = component;
                    continue;
                }

                if (valuableKind is null
                    && componentTypeName.StartsWith("Valuable", StringComparison.Ordinal)
                    && componentTypeName != "ValuablePropSwitch")
                {
                    valuableKind = componentTypeName;
                }
            }

            return new ValuableMetadata(
                valuableKind: valuableKind,
                dollarValueSet: ObservationReflection.TryGetBool(valuableObjectComponent, "dollarValueSet"),
                dollarValueCurrent: ObservationReflection.TryGetSingle(valuableObjectComponent, "dollarValueCurrent"),
                dollarValueOriginal: ObservationReflection.TryGetSingle(valuableObjectComponent, "dollarValueOriginal"),
                dollarValueOverride: ObservationReflection.TryGetInt32(valuableObjectComponent, "dollarValueOverride"),
                valuableDiscovered: ObservationReflection.TryGetBool(valuableObjectComponent, "discovered"),
                valuableDiscoveredReminder: ObservationReflection.TryGetBool(valuableObjectComponent, "discoveredReminder"));
        }
    }
}

internal static class ObservationReflection
{
    internal static object? TryGetKnownValue(object? instance, string memberName)
    {
        if (instance is null)
        {
            return null;
        }

        var type = instance.GetType();

        try
        {
            var property = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(instance);
            }

            var field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null)
            {
                return field.GetValue(instance);
            }
        }
        catch
        {
        }

        return null;
    }

    internal static bool? TryGetBool(object? instance, string memberName)
    {
        var value = TryGetKnownValue(instance, memberName);
        return value switch
        {
            bool boolValue => boolValue,
            null => null,
            _ when bool.TryParse(value.ToString(), out var parsedBool) => parsedBool,
            _ => null,
        };
    }

    internal static float? TryGetSingle(object? instance, string memberName)
    {
        var value = TryGetKnownValue(instance, memberName);
        return value switch
        {
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            int intValue => intValue,
            long longValue => longValue,
            null => null,
            _ when float.TryParse(value.ToString(), out var parsedFloat) => parsedFloat,
            _ => null,
        };
    }

    internal static int? TryGetInt32(object? instance, string memberName)
    {
        var value = TryGetKnownValue(instance, memberName);
        return value switch
        {
            int intValue => intValue,
            short shortValue => shortValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => (int)longValue,
            null => null,
            _ when int.TryParse(value.ToString(), out var parsedInt) => parsedInt,
            _ => null,
        };
    }

    internal static string? TryGetDisplayName(object? instance)
    {
        if (instance is null)
        {
            return null;
        }

        if (instance is UnityObject unityObject && !string.IsNullOrWhiteSpace(unityObject.name))
        {
            return unityObject.name;
        }

        return TryGetKnownValue(instance, "name")?.ToString();
    }
}
