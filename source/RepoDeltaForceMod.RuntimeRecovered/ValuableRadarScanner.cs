using System;
using System.Collections.Generic;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class ValuableRadarScanner
{
    internal static ValuableRadarScanResult? ScanFromOrigin(
        Transform originTransform,
        string originObjectName,
        float? originValue,
        string? excludedHostGameObjectPath,
        string? lockedTargetPath = null)
    {
        var candidates = FindLeads(originTransform, excludedHostGameObjectPath);
        if (candidates.Count == 0)
        {
            return null;
        }

        var selectedLead = SelectLead(candidates, lockedTargetPath);
        return new ValuableRadarScanResult(
            originObjectName: originObjectName,
            originValue: originValue,
            totalLeadCount: candidates.Count,
            leads: new[] { selectedLead });
    }

    private static List<ValuableRadarLead> FindLeads(Transform originTransform, string? excludedHostGameObjectPath)
    {
        var candidates = new List<ValuableRadarLead>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        var rigidbodies = UnityObject.FindObjectsByType<Rigidbody>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var rigidbody in rigidbodies)
        {
            if (rigidbody is null)
            {
                continue;
            }

            var valuableHost = FindAncestorWithComponentName(rigidbody.transform, "ValuableObject");
            if (valuableHost is null)
            {
                continue;
            }

            var sceneInfo = ObservedSceneObjectInfo.From(valuableHost);
            if (!sceneInfo.HasValuableComponent)
            {
                continue;
            }

            var hostPath = sceneInfo.HostGameObjectPath;
            if (string.IsNullOrWhiteSpace(hostPath))
            {
                continue;
            }

            if (!seenPaths.Add(hostPath))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(excludedHostGameObjectPath)
                && string.Equals(hostPath, excludedHostGameObjectPath, StringComparison.Ordinal))
            {
                continue;
            }

            var targetPosition = valuableHost.transform.position;
            var distance = Vector3.Distance(originTransform.position, targetPosition);
            candidates.Add(new ValuableRadarLead(
                name: sceneInfo.HostGameObjectName ?? valuableHost.name,
                valuableKind: sceneInfo.ValuableKind,
                distanceMeters: distance,
                directionHint: DescribeDirection(originTransform, targetPosition),
                dollarValueCurrent: sceneInfo.DollarValueCurrent,
                path: hostPath));
        }

        candidates.Sort(static (left, right) =>
        {
            var leftValue = left.DollarValueCurrent ?? float.MinValue;
            var rightValue = right.DollarValueCurrent ?? float.MinValue;
            var byValue = rightValue.CompareTo(leftValue);
            if (byValue != 0)
            {
                return byValue;
            }

            return left.DistanceMeters.CompareTo(right.DistanceMeters);
        });

        return candidates;
    }

    private static ValuableRadarLead SelectLead(
        IReadOnlyList<ValuableRadarLead> candidates,
        string? lockedTargetPath)
    {
        if (!string.IsNullOrWhiteSpace(lockedTargetPath))
        {
            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate.Path, lockedTargetPath, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }
        }

        return candidates[0];
    }

    private static GameObject? FindAncestorWithComponentName(Transform transform, string componentTypeName)
    {
        for (Transform? current = transform; current is not null; current = current.parent)
        {
            foreach (var component in current.GetComponents<Component>())
            {
                if (component is null)
                {
                    continue;
                }

                if (string.Equals(component.GetType().Name, componentTypeName, StringComparison.Ordinal))
                {
                    return current.gameObject;
                }
            }
        }

        return null;
    }

    private static string DescribeDirection(Transform originTransform, Vector3 targetPosition)
    {
        var delta = targetPosition - originTransform.position;
        var planar = Vector3.ProjectOnPlane(delta, Vector3.up);
        if (planar.sqrMagnitude < 0.25f)
        {
            return "very-close";
        }

        var direction = planar.normalized;
        var forwardDot = Vector3.Dot(originTransform.forward, direction);
        var rightDot = Vector3.Dot(originTransform.right, direction);

        var parts = new List<string>();
        if (forwardDot > 0.55f)
        {
            parts.Add("ahead");
        }
        else if (forwardDot < -0.55f)
        {
            parts.Add("behind");
        }

        if (rightDot > 0.35f)
        {
            parts.Add("right");
        }
        else if (rightDot < -0.35f)
        {
            parts.Add("left");
        }

        if (parts.Count == 0)
        {
            return "nearby";
        }

        return string.Join("-", parts);
    }
}

internal sealed class ValuableRadarScanResult
{
    internal ValuableRadarScanResult(
        string originObjectName,
        float? originValue,
        int totalLeadCount,
        IReadOnlyList<ValuableRadarLead> leads)
    {
        OriginObjectName = originObjectName;
        OriginValue = originValue;
        TotalLeadCount = totalLeadCount;
        Leads = leads;
    }

    internal string OriginObjectName { get; }
    internal float? OriginValue { get; }
    internal int TotalLeadCount { get; }
    internal IReadOnlyList<ValuableRadarLead> Leads { get; }

    internal string ToLogSummary()
    {
        var parts = new List<string>
        {
            $"OriginObject={OriginObjectName}",
            $"CandidateCount={TotalLeadCount}",
        };

        if (OriginValue.HasValue)
        {
            parts.Add($"OriginValue={OriginValue.Value:0.##}");
        }

        for (var i = 0; i < Leads.Count; i++)
        {
            parts.Add($"Lead{i + 1}={Leads[i].ToLogSummary()}");
        }

        return string.Join(" | ", parts);
    }
}

internal sealed class ValuableRadarLead
{
    internal ValuableRadarLead(
        string name,
        string? valuableKind,
        float distanceMeters,
        string directionHint,
        float? dollarValueCurrent,
        string path)
    {
        Name = name;
        ValuableKind = valuableKind;
        DistanceMeters = distanceMeters;
        DirectionHint = directionHint;
        DollarValueCurrent = dollarValueCurrent;
        Path = path;
    }

    internal string Name { get; }
    internal string? ValuableKind { get; }
    internal float DistanceMeters { get; }
    internal string DirectionHint { get; }
    internal float? DollarValueCurrent { get; }
    internal string Path { get; }

    internal string ToLogSummary()
    {
        var parts = new List<string>
        {
            Name,
            $"{DistanceMeters:0.0}m",
            DirectionHint,
        };

        if (!string.IsNullOrWhiteSpace(ValuableKind))
        {
            parts.Add(ValuableKind!);
        }

        if (DollarValueCurrent.HasValue)
        {
            parts.Add($"${DollarValueCurrent.Value:0.##}");
        }

        return string.Join(",", parts);
    }

    internal string ToHudValueText()
    {
        return DollarValueCurrent.HasValue
            ? $"${DollarValueCurrent.Value:0.##}"
            : "$?";
    }

    internal string ToHudDirectionText()
    {
        return DirectionHint switch
        {
            "ahead" => "\u524d\u65b9",
            "behind" => "\u540e\u65b9",
            "left" => "\u5de6\u4fa7",
            "right" => "\u53f3\u4fa7",
            "ahead-left" => "\u5de6\u524d\u65b9",
            "ahead-right" => "\u53f3\u524d\u65b9",
            "behind-left" => "\u5de6\u540e\u65b9",
            "behind-right" => "\u53f3\u540e\u65b9",
            "nearby" => "\u9644\u8fd1",
            "very-close" => "\u8d34\u8fd1",
            _ => DirectionHint,
        };
    }

    internal string ToHudDistanceText()
    {
        return DistanceMeters switch
        {
            < 3f => "\u6781\u8fd1",
            < 8f => "\u5f88\u8fd1",
            < 16f => "\u8f83\u8fd1",
            < 30f => "\u4e2d\u7b49",
            < 45f => "\u8f83\u8fdc",
            _ => "\u5f88\u8fdc",
        };
    }
}
