using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class MilitaryTerminalIdentity
{
    internal const string StableId = "havoc.military_terminal";
    internal const string PrefabRootName = "Havoc_MilitaryTerminal";

    private const string RuntimeBehaviourTypeName = "HavocMilitaryTerminalBehaviour";
    private const string IdentityComponentTypeName = "HavocSupplyIdentity";
    private const string ItemAttributesComponentTypeName = "ItemAttributes";
    private const string ChineseDisplayName = "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef";

    internal static bool IsOfficialMilitaryTerminal(object? value, GrabObservationSnapshot? snapshot = null)
    {
        if (value is null)
        {
            return false;
        }

        if (HasHierarchyComponent(value, RuntimeBehaviourTypeName))
        {
            return true;
        }

        if (TryReadIdentityStableId(value, out var stableId)
            && string.Equals(stableId, StableId, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var candidate in EnumerateCandidateNames(value, snapshot))
        {
            if (MatchesOfficialTerminalName(candidate))
            {
                return true;
            }
        }

        return false;
    }

    internal static string? TryGetDisplayName(object? value)
    {
        return ObservationReflection.TryGetDisplayName(value);
    }

    internal static string? TryGetItemName(object? value)
    {
        return TryReadItemAttributesText(value, "itemName");
    }

    internal static string? TryGetInstanceName(object? value)
    {
        return TryReadItemAttributesText(value, "instanceName");
    }

    internal static string? TryGetHostPath(object? value)
    {
        return ObservedSceneObjectInfo.From(value).HostGameObjectPath;
    }

    internal static string CreateSessionKey(string prefix, object? value)
    {
        var hostInstanceId = TryGetHostInstanceId(value);
        if (hostInstanceId.HasValue)
        {
            return $"{prefix}:host:{hostInstanceId.Value}";
        }

        var hostPath = TryGetHostPath(value);
        if (!string.IsNullOrWhiteSpace(hostPath))
        {
            return $"{prefix}:path:{hostPath}";
        }

        if (value is UnityObject unityObject)
        {
            return $"{prefix}:object:{unityObject.GetInstanceID()}";
        }

        var itemName = TryGetItemName(value);
        if (!string.IsNullOrWhiteSpace(itemName))
        {
            return $"{prefix}:item:{itemName}";
        }

        var displayName = TryGetDisplayName(value);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return $"{prefix}:display:{displayName}";
        }

        return prefix;
    }

    private static IEnumerable<string?> EnumerateCandidateNames(object? value, GrabObservationSnapshot? snapshot)
    {
        yield return TryGetDisplayName(value);
        yield return TryGetItemName(value);
        yield return TryGetInstanceName(value);

        var sceneInfo = ObservedSceneObjectInfo.From(value);
        yield return sceneInfo.HostGameObjectName;
        yield return sceneInfo.HostGameObjectPath;

        if (snapshot is not null)
        {
            yield return snapshot.GrabbedObjectName;
            yield return snapshot.HostGameObjectName;
            yield return snapshot.HostGameObjectPath;
        }
    }

    private static bool MatchesOfficialTerminalName(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.Equals(candidate, StableId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(candidate, PrefabRootName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(candidate, "Military Terminal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(candidate, ChineseDisplayName, StringComparison.Ordinal))
        {
            return true;
        }

        var normalized = candidate
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return normalized.IndexOf("militaryterminal", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf(ChineseDisplayName, StringComparison.Ordinal) >= 0;
    }

    private static bool HasHierarchyComponent(object? value, string componentTypeName)
    {
        for (Transform? current = TryGetTransform(value); current is not null; current = current.parent)
        {
            foreach (var component in current.GetComponents<Component>())
            {
                if (component is null)
                {
                    continue;
                }

                if (string.Equals(component.GetType().Name, componentTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadIdentityStableId(object? value, out string? stableId)
    {
        stableId = null;

        for (Transform? current = TryGetTransform(value); current is not null; current = current.parent)
        {
            foreach (var component in current.GetComponents<Component>())
            {
                if (component is null)
                {
                    continue;
                }

                if (!string.Equals(component.GetType().Name, IdentityComponentTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                stableId = ObservationReflection.TryGetKnownValue(component, "StableId")?.ToString()
                    ?? ObservationReflection.TryGetKnownValue(component, "stableId")?.ToString();
                return !string.IsNullOrWhiteSpace(stableId);
            }
        }

        return false;
    }

    private static string? TryReadItemAttributesText(object? value, string memberName)
    {
        for (Transform? current = TryGetTransform(value); current is not null; current = current.parent)
        {
            foreach (var component in current.GetComponents<Component>())
            {
                if (component is null)
                {
                    continue;
                }

                if (!string.Equals(component.GetType().Name, ItemAttributesComponentTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                var text = ObservationReflection.TryGetKnownValue(component, memberName)?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
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

    private static int? TryGetHostInstanceId(object? value)
    {
        var transform = TryGetTransform(value);
        if (transform is null)
        {
            return null;
        }

        return transform.gameObject.GetInstanceID();
    }
}
