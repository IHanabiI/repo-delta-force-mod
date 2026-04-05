using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class FlightRecorderEnvironmentalInterferenceService
{
    private const string RuntimeSoftReloadSubsystemName = "flight-recorder-environment";
    private const float ScanRadiusMeters = 22f;
    private const float RescanIntervalSeconds = 0.45f;
    private const float RigidbodyShakeRadiusMeters = 13f;
    private const float ScreenInterferenceRadiusMeters = 20f;
    private static readonly Color SignalTintColor = new(0.66f, 1f, 0.9f, 1f);
    private static readonly Color ScreenTintColor = new(0.74f, 1f, 0.9f, 1f);

    private static readonly Dictionary<int, LightInterferenceSession> ActiveLightSessions = new();
    private static readonly Dictionary<int, RigidbodyInterferenceSession> ActiveRigidbodySessions = new();
    private static readonly Dictionary<int, RendererInterferenceSession> ActiveRendererSessions = new();
    private static float _nextRescanAtTime;
    private static bool _wasActiveLastTick;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        foreach (var session in ActiveLightSessions.Values)
        {
            session.Dispose();
        }

        foreach (var session in ActiveRendererSessions.Values)
        {
            session.Dispose();
        }

        ActiveRendererSessions.Clear();
        ActiveRigidbodySessions.Clear();
        ActiveLightSessions.Clear();
        _nextRescanAtTime = 0f;
        _wasActiveLastTick = false;

        RepoDeltaForceMod.Logger.LogInfo(
            $"flight recorder environmental interference reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        var effectActive = FlightRecorderStatusHudService.CurrentHudState?.EffectTriggered == true;
        if (!effectActive)
        {
            if (_wasActiveLastTick)
            {
                ClearSessions();
            }

            _wasActiveLastTick = false;
            return;
        }

        _wasActiveLastTick = true;
        CleanupDeadSessions();

        foreach (var session in ActiveLightSessions.Values)
        {
            session.Apply();
        }

        foreach (var session in ActiveRigidbodySessions.Values)
        {
            session.Apply();
        }

        foreach (var session in ActiveRendererSessions.Values)
        {
            session.Apply();
        }

        if (Time.unscaledTime < _nextRescanAtTime)
        {
            return;
        }

        _nextRescanAtTime = Time.unscaledTime + RescanIntervalSeconds;
        RescanNearbyLights();
    }

    private static void RescanNearbyLights()
    {
        var origin = TryGetOrigin();
        if (origin is null)
        {
            return;
        }

        RescanNearbyLights(origin);
        RescanNearbyRigidbodies(origin);
        RescanNearbyScreenRenderers(origin);

        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"飞行记录仪正在干扰 {ActiveLightSessions.Count} 个环境光源，{ActiveRigidbodySessions.Count} 个可抓取物体，{ActiveRendererSessions.Count} 个屏幕/发光面板");
    }

    private static void RescanNearbyLights(Transform origin)
    {
        var seenIds = new HashSet<int>();
        foreach (var light in UnityObject.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (light is null || !light)
            {
                continue;
            }

            var distance = Vector3.Distance(origin.position, light.transform.position);
            if (distance > ScanRadiusMeters)
            {
                continue;
            }

            var instanceId = light.GetInstanceID();
            seenIds.Add(instanceId);

            if (ActiveLightSessions.TryGetValue(instanceId, out var existingSession))
            {
                existingSession.UpdateDistance(distance);
                continue;
            }

            if (!LightInterferenceSession.TryCreate(light, distance, out var session))
            {
                continue;
            }

            ActiveLightSessions[instanceId] = session;
        }

        foreach (var staleId in ActiveLightSessions.Keys.Where(id => !seenIds.Contains(id)).ToArray())
        {
            ActiveLightSessions[staleId].Dispose();
            ActiveLightSessions.Remove(staleId);
        }
    }

    private static void RescanNearbyRigidbodies(Transform origin)
    {
        var seenIds = new HashSet<int>();
        var heldObject = TryGetHeldObject();

        foreach (var rigidbody in UnityObject.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (rigidbody is null
                || !rigidbody
                || rigidbody.isKinematic
                || rigidbody.transform is null)
            {
                continue;
            }

            if (IsHeldObjectRigidbody(rigidbody, heldObject))
            {
                continue;
            }

            var distance = Vector3.Distance(origin.position, rigidbody.worldCenterOfMass);
            if (distance > RigidbodyShakeRadiusMeters)
            {
                continue;
            }

            if (!LooksLikeGrabbableCandidate(rigidbody))
            {
                continue;
            }

            var instanceId = rigidbody.GetInstanceID();
            seenIds.Add(instanceId);

            if (ActiveRigidbodySessions.TryGetValue(instanceId, out var existingSession))
            {
                existingSession.UpdateDistance(distance);
                continue;
            }

            ActiveRigidbodySessions[instanceId] = new RigidbodyInterferenceSession(
                rigidbody,
                distance,
                Mathf.Abs(rigidbody.GetInstanceID() % 997) * 0.019f);
        }

        foreach (var staleId in ActiveRigidbodySessions.Keys.Where(id => !seenIds.Contains(id)).ToArray())
        {
            ActiveRigidbodySessions.Remove(staleId);
        }
    }

    private static void RescanNearbyScreenRenderers(Transform origin)
    {
        var seenIds = new HashSet<int>();
        foreach (var renderer in UnityObject.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (renderer is null
                || !renderer
                || !LooksLikeScreenCandidate(renderer))
            {
                continue;
            }

            var boundsCenter = renderer.bounds.center;
            var distance = Vector3.Distance(origin.position, boundsCenter);
            if (distance > ScreenInterferenceRadiusMeters)
            {
                continue;
            }

            var instanceId = renderer.GetInstanceID();
            seenIds.Add(instanceId);

            if (ActiveRendererSessions.TryGetValue(instanceId, out var existingSession))
            {
                existingSession.UpdateDistance(distance);
                continue;
            }

            if (!RendererInterferenceSession.TryCreate(renderer, distance, out var session))
            {
                continue;
            }

            ActiveRendererSessions[instanceId] = session;
        }

        foreach (var staleId in ActiveRendererSessions.Keys.Where(id => !seenIds.Contains(id)).ToArray())
        {
            ActiveRendererSessions[staleId].Dispose();
            ActiveRendererSessions.Remove(staleId);
        }
    }

    private static void CleanupDeadSessions()
    {
        foreach (var pair in ActiveLightSessions.Where(pair => !pair.Value.IsAlive).ToArray())
        {
            pair.Value.Dispose();
            ActiveLightSessions.Remove(pair.Key);
        }

        foreach (var pair in ActiveRigidbodySessions.Where(pair => !pair.Value.IsAlive).ToArray())
        {
            ActiveRigidbodySessions.Remove(pair.Key);
        }

        foreach (var pair in ActiveRendererSessions.Where(pair => !pair.Value.IsAlive).ToArray())
        {
            pair.Value.Dispose();
            ActiveRendererSessions.Remove(pair.Key);
        }
    }

    private static void ClearSessions()
    {
        foreach (var session in ActiveLightSessions.Values)
        {
            session.Dispose();
        }

        foreach (var session in ActiveRendererSessions.Values)
        {
            session.Dispose();
        }

        ActiveRendererSessions.Clear();
        ActiveRigidbodySessions.Clear();
        ActiveLightSessions.Clear();
        _nextRescanAtTime = 0f;
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            "飞行记录仪环境干扰已解除");
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

    private static bool IsHeldObjectRigidbody(Rigidbody rigidbody, object? heldObject)
    {
        if (heldObject is null)
        {
            return false;
        }

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

        for (Transform? current = rigidbody.transform; current is not null; current = current.parent)
        {
            if (current == heldTransform)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeGrabbableCandidate(Rigidbody rigidbody)
    {
        for (Transform? current = rigidbody.transform; current is not null; current = current.parent)
        {
            foreach (var component in current.GetComponents<Component>())
            {
                if (component is null)
                {
                    continue;
                }

                var typeName = component.GetType().Name;
                if (string.Equals(typeName, "PhysGrabObject", StringComparison.Ordinal)
                    || string.Equals(typeName, "ValuableObject", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool LooksLikeScreenCandidate(Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer)
        {
            return false;
        }

        var candidates = new List<string?>
        {
            renderer.name,
            renderer.gameObject.name,
        };

        foreach (var material in renderer.sharedMaterials)
        {
            if (material is null)
            {
                continue;
            }

            candidates.Add(material.name);
            if (MaterialLooksEmissive(material))
            {
                return true;
            }
        }

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = candidate.Replace(" ", string.Empty, StringComparison.Ordinal);
            if (normalized.IndexOf("screen", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("display", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("monitor", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("tv", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("lcd", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("led", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("video", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MaterialLooksEmissive(Material material)
    {
        return material.HasProperty("_EmissionColor")
            || material.IsKeywordEnabled("_EMISSION");
    }

    private static float EvaluateSignalField(Vector3 worldPosition, float time, float phaseOffset)
    {
        var sampleX = worldPosition.x * 0.16f + time * 0.55f + phaseOffset;
        var sampleY = worldPosition.z * 0.16f - time * 0.38f + phaseOffset * 0.6f;
        var sampleZ = worldPosition.y * 0.11f + time * 0.27f + phaseOffset * 0.35f;

        var noiseA = Mathf.PerlinNoise(sampleX, sampleY);
        var noiseB = Mathf.PerlinNoise(sampleY + 9.7f, sampleZ + 4.1f);
        var noiseC = Mathf.PerlinNoise(sampleZ + 13.4f, sampleX + 2.8f);
        return Mathf.Clamp01(noiseA * 0.5f + noiseB * 0.3f + noiseC * 0.2f);
    }

    private sealed class LightInterferenceSession : IDisposable
    {
        private readonly Light _light;
        private readonly bool _originalEnabled;
        private readonly float _originalIntensity;
        private readonly float _originalRange;
        private readonly Color _originalColor;
        private readonly float _phaseOffset;
        private bool _disposed;
        private float _distanceMeters;

        private LightInterferenceSession(
            Light light,
            float distanceMeters,
            bool originalEnabled,
            float originalIntensity,
            float originalRange,
            Color originalColor,
            float phaseOffset)
        {
            _light = light;
            _distanceMeters = distanceMeters;
            _originalEnabled = originalEnabled;
            _originalIntensity = originalIntensity;
            _originalRange = originalRange;
            _originalColor = originalColor;
            _phaseOffset = phaseOffset;
        }

        internal bool IsAlive => !_disposed && _light;

        internal void UpdateDistance(float distanceMeters)
        {
            _distanceMeters = distanceMeters;
        }

        internal void Apply()
        {
            if (_disposed || !_light)
            {
                return;
            }

            var normalizedDistance = Mathf.Clamp01(_distanceMeters / ScanRadiusMeters);
            var strength = 1f - normalizedDistance;
            var time = Time.unscaledTime + _phaseOffset;
            var fieldWave = EvaluateSignalField(_light.transform.position, time, _phaseOffset);
            var shimmer = 0.78f + 0.22f * (0.5f + 0.5f * Mathf.Sin(time * 10.6f));
            var pulse = 0.84f + 0.16f * (0.5f + 0.5f * Mathf.Sin(time * 3.2f + fieldWave * 2.4f));
            var surge = 0.8f + 0.2f * (0.5f + 0.5f * Mathf.Sin(time * 1.9f + _phaseOffset * 0.8f));
            var flicker = 0.54f + 0.46f * (0.5f + 0.5f * Mathf.Sin(time * 18.2f + fieldWave * 5.8f));
            var microDrop = Mathf.PerlinNoise(
                    _light.transform.position.x * 0.42f + time * 2.2f + _phaseOffset,
                    _light.transform.position.z * 0.42f + time * 1.8f + 7.1f)
                > 0.8f
                    ? 0.46f
                    : 1f;
            var sag = Mathf.Lerp(0.38f, 1.2f, fieldWave);
            var intensityCore = shimmer * pulse * surge * flicker * microDrop * sag;
            var intensityMultiplier = Mathf.Lerp(1f, intensityCore, 0.98f * strength);
            var rangeMultiplier = Mathf.Lerp(1f, 0.82f + fieldWave * 0.22f, 0.72f * strength);
            var tintBlend = 0.2f * strength + (1f - fieldWave) * 0.16f * strength;

            _light.enabled = _originalEnabled;
            _light.intensity = Mathf.Max(0.01f, _originalIntensity * intensityMultiplier);
            _light.range = Mathf.Max(0.1f, _originalRange * rangeMultiplier);
            _light.color = Color.Lerp(_originalColor, SignalTintColor, tintBlend);
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

        internal static bool TryCreate(Light light, float distanceMeters, out LightInterferenceSession session)
        {
            session = null!;

            if (light is null || !light || light.type == LightType.Disc)
            {
                return false;
            }

            session = new LightInterferenceSession(
                light,
                distanceMeters,
                light.enabled,
                light.intensity,
                light.range,
                light.color,
                Mathf.Abs(light.GetInstanceID() % 997) * 0.013f);
            return true;
        }
    }

    private sealed class RigidbodyInterferenceSession
    {
        private readonly Rigidbody _rigidbody;
        private readonly float _phaseOffset;
        private float _distanceMeters;
        private float _nextPulseAtTime;

        internal RigidbodyInterferenceSession(
            Rigidbody rigidbody,
            float distanceMeters,
            float phaseOffset)
        {
            _rigidbody = rigidbody;
            _distanceMeters = distanceMeters;
            _phaseOffset = phaseOffset;
            _nextPulseAtTime = Time.unscaledTime + 0.05f + phaseOffset;
        }

        internal bool IsAlive => _rigidbody && !_rigidbody.isKinematic;

        internal void UpdateDistance(float distanceMeters)
        {
            _distanceMeters = distanceMeters;
        }

        internal void Apply()
        {
            if (!IsAlive || Time.unscaledTime < _nextPulseAtTime)
            {
                return;
            }

            var normalizedDistance = Mathf.Clamp01(_distanceMeters / RigidbodyShakeRadiusMeters);
            var strength = 1f - normalizedDistance;
            var time = Time.unscaledTime + _phaseOffset;

            var horizontal = new Vector3(
                Mathf.Sin(time * 7.1f),
                Mathf.Sin(time * 8.8f + _phaseOffset) * 0.12f,
                Mathf.Cos(time * 6.3f + _phaseOffset)).normalized;
            var vertical = Vector3.up * (0.22f + 0.48f * (0.5f + 0.5f * Mathf.Sin(time * 10.6f)));
            var force = (horizontal * 0.48f + vertical * 0.12f) * (0.8f * strength);
            var torque = new Vector3(
                Mathf.Cos(time * 5.2f),
                Mathf.Sin(time * 4.7f + _phaseOffset),
                Mathf.Sin(time * 6.1f)) * (0.14f * strength);

            _rigidbody.WakeUp();
            _rigidbody.AddForce(force, ForceMode.VelocityChange);
            _rigidbody.AddTorque(torque, ForceMode.VelocityChange);

            _nextPulseAtTime = Time.unscaledTime + Mathf.Lerp(0.1f, 0.22f, 1f - strength);
        }
    }

    private sealed class RendererInterferenceSession : IDisposable
    {
        private readonly Renderer _renderer;
        private readonly MaterialChannel[] _channels;
        private readonly float _phaseOffset;
        private bool _disposed;
        private float _distanceMeters;

        private RendererInterferenceSession(
            Renderer renderer,
            MaterialChannel[] channels,
            float distanceMeters,
            float phaseOffset)
        {
            _renderer = renderer;
            _channels = channels;
            _distanceMeters = distanceMeters;
            _phaseOffset = phaseOffset;
        }

        internal bool IsAlive => !_disposed && _renderer;

        internal void UpdateDistance(float distanceMeters)
        {
            _distanceMeters = distanceMeters;
        }

        internal void Apply()
        {
            if (_disposed || !_renderer)
            {
                return;
            }

            var normalizedDistance = Mathf.Clamp01(_distanceMeters / ScreenInterferenceRadiusMeters);
            var strength = 1f - normalizedDistance;
            var time = Time.unscaledTime + _phaseOffset;
            var fieldWave = EvaluateSignalField(_renderer.bounds.center, time, _phaseOffset);
            var shimmer = 0.8f + 0.2f * (0.5f + 0.5f * Mathf.Sin(time * 9.1f));
            var pulse = 0.86f + 0.14f * (0.5f + 0.5f * Mathf.Sin(time * 3.4f + fieldWave * 2.1f));
            var surge = 0.82f + 0.18f * (0.5f + 0.5f * Mathf.Sin(time * 1.8f + _phaseOffset));
            var flicker = 0.58f + 0.42f * (0.5f + 0.5f * Mathf.Sin(time * 15.6f + fieldWave * 4.9f));
            var scanlineNoise = Mathf.PerlinNoise(
                _renderer.bounds.center.x * 0.36f + time * 1.9f + _phaseOffset,
                _renderer.bounds.center.y * 0.52f + time * 2.5f + 3.8f);
            var microDrop = scanlineNoise > 0.82f ? 0.4f : 1f;
            var surfaceWave = Mathf.Lerp(0.7f, 1.16f, fieldWave)
                * (0.92f + 0.08f * Mathf.Sin(time * 4.9f))
                * (0.95f + 0.05f * flicker);
            var emissionWave = shimmer * pulse * surge * flicker * microDrop * Mathf.Lerp(0.46f, 1.46f, fieldWave);
            var surfaceMultiplier = Mathf.Lerp(1f, surfaceWave, 0.56f * strength);
            var emissionMultiplier = Mathf.Lerp(1f, emissionWave, 1.02f * strength);
            var tintBlend = 0.18f * strength + (1f - fieldWave) * 0.14f * strength + (microDrop < 1f ? 0.06f * strength : 0f);

            foreach (var channel in _channels)
            {
                channel.Apply(surfaceMultiplier, emissionMultiplier, tintBlend);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var channel in _channels)
            {
                channel.Dispose();
            }
        }

        internal static bool TryCreate(Renderer renderer, float distanceMeters, out RendererInterferenceSession session)
        {
            session = null!;

            Material[] materials;
            try
            {
                materials = renderer.materials;
            }
            catch
            {
                return false;
            }

            var channels = new List<MaterialChannel>();
            for (var i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material is null)
                {
                    continue;
                }

                if (MaterialChannel.TryCreate(material, out var channel))
                {
                    channels.Add(channel);
                }
            }

            if (channels.Count == 0)
            {
                return false;
            }

            session = new RendererInterferenceSession(
                renderer,
                channels.ToArray(),
                distanceMeters,
                Mathf.Abs(renderer.GetInstanceID() % 991) * 0.017f);
            return true;
        }
    }

    private sealed class MaterialChannel : IDisposable
    {
        private readonly Material _material;
        private readonly bool _hasColor;
        private readonly bool _hasBaseColor;
        private readonly bool _hasEmissionColor;
        private readonly Color _originalColor;
        private readonly Color _originalBaseColor;
        private readonly Color _originalEmissionColor;
        private bool _disposed;

        private MaterialChannel(
            Material material,
            bool hasColor,
            bool hasBaseColor,
            bool hasEmissionColor,
            Color originalColor,
            Color originalBaseColor,
            Color originalEmissionColor)
        {
            _material = material;
            _hasColor = hasColor;
            _hasBaseColor = hasBaseColor;
            _hasEmissionColor = hasEmissionColor;
            _originalColor = originalColor;
            _originalBaseColor = originalBaseColor;
            _originalEmissionColor = originalEmissionColor;
        }

        internal void Apply(float surfaceBrightnessMultiplier, float emissionBrightnessMultiplier, float tintBlend)
        {
            if (_disposed || _material is null)
            {
                return;
            }

            if (_hasColor)
            {
                var tinted = Color.Lerp(_originalColor, ScreenTintColor, tintBlend);
                _material.SetColor("_Color", tinted * surfaceBrightnessMultiplier);
            }

            if (_hasBaseColor)
            {
                var tinted = Color.Lerp(_originalBaseColor, ScreenTintColor, tintBlend);
                _material.SetColor("_BaseColor", tinted * surfaceBrightnessMultiplier);
            }

            if (_hasEmissionColor)
            {
                var tinted = Color.Lerp(_originalEmissionColor, ScreenTintColor * 1.18f, tintBlend + 0.04f);
                _material.SetColor("_EmissionColor", tinted * emissionBrightnessMultiplier);
            }
        }

        public void Dispose()
        {
            if (_disposed || _material is null)
            {
                return;
            }

            _disposed = true;

            if (_hasColor)
            {
                _material.SetColor("_Color", _originalColor);
            }

            if (_hasBaseColor)
            {
                _material.SetColor("_BaseColor", _originalBaseColor);
            }

            if (_hasEmissionColor)
            {
                _material.SetColor("_EmissionColor", _originalEmissionColor);
            }
        }

        internal static bool TryCreate(Material material, out MaterialChannel channel)
        {
            var hasColor = material.HasProperty("_Color");
            var hasBaseColor = material.HasProperty("_BaseColor");
            var hasEmissionColor = material.HasProperty("_EmissionColor");

            channel = null!;
            if (!hasColor && !hasBaseColor && !hasEmissionColor)
            {
                return false;
            }

            channel = new MaterialChannel(
                material,
                hasColor,
                hasBaseColor,
                hasEmissionColor,
                hasColor ? material.GetColor("_Color") : default,
                hasBaseColor ? material.GetColor("_BaseColor") : default,
                hasEmissionColor ? material.GetColor("_EmissionColor") : default);
            return true;
        }
    }
}
