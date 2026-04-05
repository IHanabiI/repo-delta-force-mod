using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class FlightRecorderResidualReplayService
{
    private const string RuntimeSoftReloadSubsystemName = "flight-recorder-residual-replay";
    private const float ReplayRadiusMeters = 16f;
    private const float GhostLifetimeSeconds = 0.95f;

    private static readonly List<GhostInstance> ActiveGhosts = new();
    private static readonly System.Random Random = new();

    private static float _nextReplayAtTime;
    private static bool _wasActiveLastTick;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        ClearGhosts();
        _nextReplayAtTime = 0f;
        _wasActiveLastTick = false;

        RepoDeltaForceMod.Logger.LogInfo(
            $"flight recorder residual replay reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        var effectActive = FlightRecorderStatusHudService.CurrentHudState?.EffectTriggered == true;
        if (!effectActive)
        {
            if (_wasActiveLastTick)
            {
                ClearGhosts();
            }

            _wasActiveLastTick = false;
            return;
        }

        _wasActiveLastTick = true;
        UpdateGhosts();

        if (Time.unscaledTime < _nextReplayAtTime)
        {
            return;
        }

        EmitResidualReplay();
    }

    private static void EmitResidualReplay()
    {
        var origin = TryGetOrigin();
        if (origin is null)
        {
            _nextReplayAtTime = Time.unscaledTime + 1f;
            return;
        }

        var candidates = FindReplayCandidates(origin);
        if (candidates.Count == 0)
        {
            _nextReplayAtTime = Time.unscaledTime + 1.2f;
            return;
        }

        var structuralCandidates = candidates
            .Where(candidate => candidate.Kind == ReplayTargetKind.Structural)
            .ToList();
        var spawnCount = Mathf.Min(candidates.Count, structuralCandidates.Count > 0 ? Random.Next(2, 5) : Random.Next(1, 4));
        for (var i = 0; i < spawnCount; i++)
        {
            ReplayCandidate candidate;
            if (i == 0 && structuralCandidates.Count > 0)
            {
                var structuralIndex = Random.Next(structuralCandidates.Count);
                candidate = structuralCandidates[structuralIndex];
                structuralCandidates.RemoveAt(structuralIndex);
                candidates.Remove(candidate);
            }
            else
            {
                var selectionPool = candidates.Take(Mathf.Min(candidates.Count, 5)).ToList();
                var candidateIndex = Random.Next(selectionPool.Count);
                candidate = selectionPool[candidateIndex];
                candidates.Remove(candidate);
                structuralCandidates.Remove(candidate);
            }

            if (GhostInstance.TryCreate(candidate, out var ghost))
            {
                ActiveGhosts.Add(ghost);
            }
        }

        _nextReplayAtTime = Time.unscaledTime + Mathf.Lerp(0.8f, 1.7f, (float)Random.NextDouble());
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"飞行记录仪残影回放已触发：{ActiveGhosts.Count} 个残影");
    }

    private static List<ReplayCandidate> FindReplayCandidates(Transform origin)
    {
        var heldObject = TryGetHeldObject();
        var candidates = new List<ReplayCandidate>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var renderer in UnityObject.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer is null || !renderer || !renderer.enabled)
            {
                continue;
            }

            if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly)
            {
                continue;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter is null || meshFilter.sharedMesh is null)
            {
                continue;
            }

            if (IsHeldObjectTransform(renderer.transform, heldObject))
            {
                continue;
            }

            var distance = Vector3.Distance(origin.position, renderer.bounds.center);
            if (distance > ReplayRadiusMeters)
            {
                continue;
            }

            var classification = ClassifyReplayTarget(renderer);
            if (classification.Priority <= 0)
            {
                continue;
            }

            var path = ObservedSceneObjectInfo.From(renderer).HostGameObjectPath
                ?? renderer.transform.GetInstanceID().ToString();
            if (!seenKeys.Add(path))
            {
                continue;
            }

            candidates.Add(new ReplayCandidate(
                renderer,
                meshFilter,
                distance,
                classification.Priority,
                classification.Kind));
        }

        candidates.Sort(static (left, right) =>
        {
            var byPriority = right.Priority.CompareTo(left.Priority);
            if (byPriority != 0)
            {
                return byPriority;
            }

            return left.DistanceMeters.CompareTo(right.DistanceMeters);
        });
        return candidates.Take(14).ToList();
    }

    private static ReplayTargetClassification ClassifyReplayTarget(MeshRenderer renderer)
    {
        var sceneInfo = ObservedSceneObjectInfo.From(renderer);
        if (sceneInfo.HasValuableComponent || sceneInfo.HasPhysGrabObjectComponent)
        {
            return new ReplayTargetClassification(priority: 2, kind: ReplayTargetKind.Grabbable);
        }

        foreach (var candidate in new[]
        {
            renderer.name,
            renderer.gameObject.name,
            sceneInfo.HostGameObjectName,
            sceneInfo.HostGameObjectPath,
        })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = candidate.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (normalized.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("lid", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("safe", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("toilet", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("drawer", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("hatch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ReplayTargetClassification(priority: 4, kind: ReplayTargetKind.Structural);
            }

            if (normalized.IndexOf("valuable", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new ReplayTargetClassification(priority: 2, kind: ReplayTargetKind.Grabbable);
            }
        }

        return new ReplayTargetClassification(priority: 0, kind: ReplayTargetKind.None);
    }

    private static void UpdateGhosts()
    {
        for (var i = ActiveGhosts.Count - 1; i >= 0; i--)
        {
            var ghost = ActiveGhosts[i];
            if (!ghost.Apply())
            {
                ghost.Dispose();
                ActiveGhosts.RemoveAt(i);
            }
        }
    }

    private static void ClearGhosts()
    {
        foreach (var ghost in ActiveGhosts)
        {
            ghost.Dispose();
        }

        ActiveGhosts.Clear();
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            "飞行记录仪残影回放已清除");
    }

    private static Transform? TryGetOrigin()
    {
        if (Camera.main is not null)
        {
            return Camera.main.transform;
        }

        if (PhysGrabber.instance is not null)
        {
            return PhysGrabber.instance.transform;
        }

        return null;
    }

    private static object? TryGetHeldObject()
    {
        if (PhysGrabber.instance is null || !PhysGrabber.instance.grabbed)
        {
            return null;
        }

        return PhysGrabber.instance.grabbedPhysGrabObject
            ?? ObservationReflection.TryGetKnownValue(PhysGrabber.instance, "grabbedObject");
    }

    private static bool IsHeldObjectTransform(Transform transform, object? heldObject)
    {
        var heldTransform = heldObject switch
        {
            GameObject gameObject => gameObject.transform,
            Component component => component.transform,
            _ => null,
        };
        if (heldTransform is null)
        {
            return false;
        }

        for (Transform? current = transform; current is not null; current = current.parent)
        {
            if (current == heldTransform)
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct ReplayCandidate
    {
        internal ReplayCandidate(
            MeshRenderer renderer,
            MeshFilter meshFilter,
            float distanceMeters,
            int priority,
            ReplayTargetKind kind)
        {
            Renderer = renderer;
            MeshFilter = meshFilter;
            DistanceMeters = distanceMeters;
            Priority = priority;
            Kind = kind;
        }

        internal MeshRenderer Renderer { get; }
        internal MeshFilter MeshFilter { get; }
        internal float DistanceMeters { get; }
        internal int Priority { get; }
        internal ReplayTargetKind Kind { get; }
    }

    private readonly struct ReplayTargetClassification
    {
        internal ReplayTargetClassification(int priority, ReplayTargetKind kind)
        {
            Priority = priority;
            Kind = kind;
        }

        internal int Priority { get; }
        internal ReplayTargetKind Kind { get; }
    }

    private sealed class GhostInstance : IDisposable
    {
        private readonly GameObject _root;
        private readonly MeshRenderer _renderer;
        private readonly Material[] _materials;
        private readonly float _spawnedAtTime;
        private readonly float _lifetimeSeconds;
        private bool _disposed;

        private GhostInstance(
            GameObject root,
            MeshRenderer renderer,
            Material[] materials,
            float spawnedAtTime,
            float lifetimeSeconds)
        {
            _root = root;
            _renderer = renderer;
            _materials = materials;
            _spawnedAtTime = spawnedAtTime;
            _lifetimeSeconds = lifetimeSeconds;
        }

        internal bool Apply()
        {
            if (_disposed || !_root || !_renderer)
            {
                return false;
            }

            var age = Time.unscaledTime - _spawnedAtTime;
            if (age >= _lifetimeSeconds)
            {
                return false;
            }

            var normalized = 1f - age / _lifetimeSeconds;
            var alpha = Mathf.SmoothStep(0f, 0.34f, normalized) * normalized;
            foreach (var material in _materials)
            {
                ApplyGhostMaterial(material, alpha);
            }

            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var material in _materials)
            {
                if (material is not null)
                {
                    UnityObject.Destroy(material);
                }
            }

            if (_root)
            {
                UnityObject.Destroy(_root);
            }
        }

        internal static bool TryCreate(ReplayCandidate candidate, out GhostInstance ghost)
        {
            ghost = null!;

            if (candidate.Renderer is null
                || !candidate.Renderer
                || candidate.MeshFilter is null
                || candidate.MeshFilter.sharedMesh is null)
            {
                return false;
            }

            var ghostRoot = new GameObject($"ResidualReplay_{candidate.Renderer.gameObject.name}");
            ghostRoot.hideFlags = HideFlags.HideAndDontSave;
            ghostRoot.transform.SetPositionAndRotation(
                candidate.MeshFilter.transform.position,
                candidate.MeshFilter.transform.rotation);
            ghostRoot.transform.localScale = candidate.MeshFilter.transform.lossyScale;

            if (candidate.Kind == ReplayTargetKind.Structural)
            {
                ghostRoot.transform.position += candidate.MeshFilter.transform.forward * -0.04f;
            }
            else
            {
                ghostRoot.transform.position += new Vector3(
                    Mathf.Lerp(-0.03f, 0.03f, (float)Random.NextDouble()),
                    Mathf.Lerp(-0.01f, 0.02f, (float)Random.NextDouble()),
                    Mathf.Lerp(-0.03f, 0.03f, (float)Random.NextDouble()));
            }

            var ghostFilter = ghostRoot.AddComponent<MeshFilter>();
            ghostFilter.sharedMesh = candidate.MeshFilter.sharedMesh;

            var ghostRenderer = ghostRoot.AddComponent<MeshRenderer>();
            ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ghostRenderer.receiveShadows = false;
            ghostRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            ghostRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            ghostRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            var sourceMaterials = candidate.Renderer.sharedMaterials;
            var ghostMaterials = new Material[sourceMaterials.Length];
            for (var i = 0; i < sourceMaterials.Length; i++)
            {
                var sourceMaterial = sourceMaterials[i];
                if (sourceMaterial is null)
                {
                    ghostMaterials[i] = new Material(Shader.Find("Standard"));
                }
                else
                {
                    ghostMaterials[i] = new Material(sourceMaterial);
                }

                ApplyGhostMaterial(ghostMaterials[i], 0.3f);
            }

            ghostRenderer.materials = ghostMaterials;
            ghost = new GhostInstance(
                ghostRoot,
                ghostRenderer,
                ghostMaterials,
                Time.unscaledTime,
                candidate.Kind == ReplayTargetKind.Structural
                    ? GhostLifetimeSeconds + Mathf.Lerp(0.28f, 0.56f, (float)Random.NextDouble())
                    : GhostLifetimeSeconds + Mathf.Lerp(0.08f, 0.28f, (float)Random.NextDouble()));
            return true;
        }

        private static void ApplyGhostMaterial(Material material, float alpha)
        {
            ConfigureTransparentMaterial(material);

            var tint = new Color(0.7f, 1f, 0.88f, alpha);
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", new Color(0.26f, 0.78f, 0.64f, 1f) * (0.7f + alpha * 1.1f));
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 3f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }
    }

    private enum ReplayTargetKind
    {
        None,
        Grabbable,
        Structural,
    }
}
