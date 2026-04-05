using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class FlightRecorderHighlightService
{
    private const string RuntimeSoftReloadSubsystemName = "flight-recorder-visuals";
    private static readonly Color BoostedRedColor = new(1f, 0.16f, 0.12f, 1f);
    private static readonly Color EmissionRedColor = new(1f, 0.12f, 0.08f, 1f);

    private const float RedIntensityMultiplier = 1.35f;
    private const float RedRangeMultiplier = 1.12f;
    private const float RedColorBlend = 0.48f;
    private const float RedEmissionBrightnessMultiplier = 2.25f;
    private const string RuntimeRedLightObjectName = "Codex Flight Recorder Red Light";
    private const float RuntimeRedLightIntensity = 3.4f;
    private const float RuntimeRedLightRange = 4.5f;
    private const float RescanIntervalSeconds = 1f;

    private static readonly Dictionary<int, RecorderVisualSession> ActiveSessions = new();
    private static float _nextRescanTime;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        foreach (var session in ActiveSessions.Values)
        {
            session.Dispose();
        }

        ActiveSessions.Clear();
        _nextRescanTime = 0f;

        RepoDeltaForceMod.Logger.LogInfo(
            $"flight recorder visuals state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        CleanupDeadSessions();

        foreach (var session in ActiveSessions.Values)
        {
            session.Apply();
        }

        if (Time.unscaledTime < _nextRescanTime)
        {
            return;
        }

        _nextRescanTime = Time.unscaledTime + RescanIntervalSeconds;
        Rescan();
    }

    private static void Rescan()
    {
        var seenIds = new HashSet<int>();

        foreach (var root in EnumerateRecorderRoots())
        {
            if (root is null || !root)
            {
                continue;
            }

            var instanceId = root.GetInstanceID();
            seenIds.Add(instanceId);

            if (ActiveSessions.ContainsKey(instanceId))
            {
                continue;
            }

            if (!RecorderVisualSession.TryCreate(root, out var session))
            {
                continue;
            }

            ActiveSessions[instanceId] = session;
            RepoDeltaForceMod.Logger.LogInfo(
                $"flight recorder visuals attached: Root={root.name} | LightCount={session.LightCount} | EmissiveMaterialCount={session.MaterialCount} | RuntimeLight={(session.HasRuntimeLight ? "yes" : "no")}");
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                $"flight recorder visuals attached to '{root.name}'");
        }

        foreach (var staleId in ActiveSessions.Keys.Where(id => !seenIds.Contains(id)).ToArray())
        {
            ActiveSessions[staleId].Dispose();
            ActiveSessions.Remove(staleId);
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                $"flight recorder visuals detached from instance {staleId}");
        }
    }

    private static IEnumerable<GameObject> EnumerateRecorderRoots()
    {
        var emittedIds = new HashSet<int>();

        foreach (var behaviour in UnityObject.FindObjectsOfType<HavocFlightRecorderBehaviour>(true))
        {
            if (behaviour is null || !behaviour)
            {
                continue;
            }

            var root = behaviour.gameObject;
            if (root is null || !root)
            {
                continue;
            }

            if (emittedIds.Add(root.GetInstanceID()))
            {
                yield return root;
            }
        }

        foreach (var itemAttributes in UnityObject.FindObjectsByType<ItemAttributes>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (itemAttributes is null || !itemAttributes)
            {
                continue;
            }

            if (!FlightRecorderIdentity.IsOfficialFlightRecorder(itemAttributes))
            {
                continue;
            }

            var root = itemAttributes.gameObject;
            if (root is null || !root)
            {
                continue;
            }

            if (emittedIds.Add(root.GetInstanceID()))
            {
                yield return root;
            }
        }
    }

    private static void CleanupDeadSessions()
    {
        foreach (var pair in ActiveSessions.Where(pair => !pair.Value.IsAlive).ToArray())
        {
            pair.Value.Dispose();
            ActiveSessions.Remove(pair.Key);
        }
    }

    private sealed class RecorderVisualSession : IDisposable
    {
        private readonly GameObject _root;
        private readonly LightSession[] _lights;
        private readonly MaterialSession[] _materials;
        private readonly RuntimeLightSession? _runtimeLight;
        private bool _disposed;

        private RecorderVisualSession(
            GameObject root,
            LightSession[] lights,
            MaterialSession[] materials,
            RuntimeLightSession? runtimeLight)
        {
            _root = root;
            _lights = lights;
            _materials = materials;
            _runtimeLight = runtimeLight;
        }

        internal int LightCount => _lights.Length;
        internal int MaterialCount => _materials.Length;
        internal bool HasRuntimeLight => _runtimeLight is not null;
        internal bool IsAlive => !_disposed && _root;

        internal void Apply()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var light in _lights)
            {
                light.Apply();
            }

            foreach (var material in _materials)
            {
                material.Apply();
            }

            _runtimeLight?.Apply();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var light in _lights)
            {
                light.Dispose();
            }

            foreach (var material in _materials)
            {
                material.Dispose();
            }

            _runtimeLight?.Dispose();
        }

        internal static bool TryCreate(GameObject root, out RecorderVisualSession session)
        {
            session = null!;

            var lights = root.GetComponentsInChildren<Light>(true)
                .Where(light => light is not null && light)
                .Select(light => new LightSession(light))
                .ToArray();

            var materials = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is not null && renderer)
                .SelectMany(MaterialSession.CreateForRenderer)
                .ToArray();

            var runtimeLight = RuntimeLightSession.TryCreate(root);

            if (lights.Length == 0 && materials.Length == 0 && runtimeLight is null)
            {
                return false;
            }

            session = new RecorderVisualSession(root, lights, materials, runtimeLight);
            return true;
        }
    }

    private sealed class LightSession : IDisposable
    {
        private readonly Light _light;
        private readonly bool _originalEnabled;
        private readonly float _originalIntensity;
        private readonly float _originalRange;
        private readonly Color _originalColor;
        private bool _disposed;

        internal LightSession(Light light)
        {
            _light = light;
            _originalEnabled = light.enabled;
            _originalIntensity = light.intensity;
            _originalRange = light.range;
            _originalColor = light.color;
        }

        internal void Apply()
        {
            if (_disposed || !_light)
            {
                return;
            }

            _light.enabled = true;
            _light.intensity = _originalIntensity * RedIntensityMultiplier;
            _light.range = _originalRange * RedRangeMultiplier;
            _light.color = Color.Lerp(_originalColor, BoostedRedColor, RedColorBlend);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_light)
            {
                return;
            }

            _light.enabled = _originalEnabled;
            _light.intensity = _originalIntensity;
            _light.range = _originalRange;
            _light.color = _originalColor;
        }
    }

    private sealed class MaterialSession : IDisposable
    {
        private readonly Material _material;
        private readonly bool _emissionKeywordEnabled;
        private readonly Color _originalEmissionColor;
        private bool _disposed;

        private MaterialSession(Material material, bool emissionKeywordEnabled, Color originalEmissionColor)
        {
            _material = material;
            _emissionKeywordEnabled = emissionKeywordEnabled;
            _originalEmissionColor = originalEmissionColor;
        }

        internal void Apply()
        {
            if (_disposed || _material is null)
            {
                return;
            }

            _material.EnableKeyword("_EMISSION");
            _material.SetColor("_EmissionColor", EmissionRedColor * RedEmissionBrightnessMultiplier);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_material is null)
            {
                return;
            }

            _material.SetColor("_EmissionColor", _originalEmissionColor);
            if (_emissionKeywordEnabled)
            {
                _material.EnableKeyword("_EMISSION");
            }
            else
            {
                _material.DisableKeyword("_EMISSION");
            }
        }

        internal static IEnumerable<MaterialSession> CreateForRenderer(Renderer renderer)
        {
            Material[] materials;

            try
            {
                materials = renderer.materials;
            }
            catch
            {
                yield break;
            }

            foreach (var material in materials)
            {
                if (material is null || !material.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                yield return new MaterialSession(
                    material,
                    material.IsKeywordEnabled("_EMISSION"),
                    material.GetColor("_EmissionColor"));
            }
        }
    }

    private sealed class RuntimeLightSession : IDisposable
    {
        private readonly GameObject _lightObject;
        private readonly Light _light;
        private readonly bool _createdAtRuntime;
        private readonly bool _originalEnabled;
        private readonly float _originalIntensity;
        private readonly float _originalRange;
        private readonly Color _originalColor;
        private bool _disposed;

        private RuntimeLightSession(
            GameObject lightObject,
            Light light,
            bool createdAtRuntime,
            bool originalEnabled,
            float originalIntensity,
            float originalRange,
            Color originalColor)
        {
            _lightObject = lightObject;
            _light = light;
            _createdAtRuntime = createdAtRuntime;
            _originalEnabled = originalEnabled;
            _originalIntensity = originalIntensity;
            _originalRange = originalRange;
            _originalColor = originalColor;
        }

        internal void Apply()
        {
            if (_disposed || !_light)
            {
                return;
            }

            _light.enabled = true;
            _light.type = LightType.Point;
            _light.color = BoostedRedColor;
            _light.intensity = RuntimeRedLightIntensity;
            _light.range = RuntimeRedLightRange;
            _light.shadows = LightShadows.None;

            if (_lightObject)
            {
                _lightObject.transform.localPosition = Vector3.zero;
                _lightObject.transform.localRotation = Quaternion.identity;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (!_lightObject)
            {
                return;
            }

            if (_createdAtRuntime)
            {
                UnityObject.Destroy(_lightObject);
                return;
            }

            if (!_light)
            {
                return;
            }

            _light.enabled = _originalEnabled;
            _light.intensity = _originalIntensity;
            _light.range = _originalRange;
            _light.color = _originalColor;
        }

        internal static RuntimeLightSession? TryCreate(GameObject root)
        {
            if (root is null || !root)
            {
                return null;
            }

            var existingTransform = root.transform.Find(RuntimeRedLightObjectName);
            var createdAtRuntime = existingTransform is null;
            var lightObject = existingTransform is not null
                ? existingTransform.gameObject
                : new GameObject(RuntimeRedLightObjectName);

            if (createdAtRuntime)
            {
                lightObject.transform.SetParent(root.transform, false);
            }

            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;

            var light = lightObject.GetComponent<Light>() ?? lightObject.AddComponent<Light>();
            return new RuntimeLightSession(
                lightObject,
                light,
                createdAtRuntime,
                light.enabled,
                light.intensity,
                light.range,
                light.color);
        }
    }
}
