using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class HavocSupplyHighlightService
{
    private const string RuntimeSoftReloadSubsystemName = "military-terminal-visuals";
    private static readonly Color BoostedGreenColor = new(0.12f, 1f, 0.2f, 1f);

    private const float GreenIntensityMultiplier = 1.35f;
    private const float GreenRangeMultiplier = 1.12f;
    private const float GreenColorBlend = 0.4f;
    private const float RescanIntervalSeconds = 1f;

    private static readonly Dictionary<int, TerminalLightSession> ActiveSessions = new();

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
            $"military terminal visuals state reset for soft reload #{context.Generation}: Reason={context.Reason}");
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

        foreach (var root in EnumerateTerminalRoots())
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

            if (!TerminalLightSession.TryCreate(root, out var session))
            {
                continue;
            }

            ActiveSessions[instanceId] = session;
            RepoDeltaForceMod.Logger.LogInfo(
                $"havoc terminal visuals attached: Root={root.name} | LightCount={session.LightCount}");
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                $"终端视觉已附着到对象：{root.name}");
        }

        foreach (var staleId in ActiveSessions.Keys.Where(id => !seenIds.Contains(id)).ToArray())
        {
            ActiveSessions[staleId].Dispose();
            ActiveSessions.Remove(staleId);
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                $"终端视觉已从实例 {staleId} 脱离");
        }
    }

    private static IEnumerable<GameObject> EnumerateTerminalRoots()
    {
        var emittedIds = new HashSet<int>();

        foreach (var behaviour in UnityObject.FindObjectsOfType<HavocMilitaryTerminalBehaviour>(true))
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

            if (!MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(itemAttributes))
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

    private sealed class TerminalLightSession : IDisposable
    {
        private readonly GameObject _root;
        private readonly LightSession[] _lights;
        private bool _disposed;

        private TerminalLightSession(GameObject root, LightSession[] lights)
        {
            _root = root;
            _lights = lights;
        }

        internal int LightCount => _lights.Length;

        internal bool IsAlive => !_disposed && _root;

        internal void Apply()
        {
            if (_disposed)
            {
                return;
            }

            MilitaryTerminalAutoSpawnService.EnforceToolBatteryPresentationDisabled(_root);

            foreach (var light in _lights)
            {
                light.Apply();
            }
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
        }

        internal static bool TryCreate(GameObject root, out TerminalLightSession session)
        {
            session = null!;

            var lights = root.GetComponentsInChildren<Light>(true)
                .Select(LightSession.TryCreate)
                .Where(light => light is not null)
                .Cast<LightSession>()
                .ToArray();

            if (lights.Length == 0)
            {
                return false;
            }

            session = new TerminalLightSession(root, lights);
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
        private readonly LightHandling _handling;
        private bool _disposed;

        private LightSession(
            Light light,
            bool originalEnabled,
            float originalIntensity,
            float originalRange,
            Color originalColor,
            LightHandling handling)
        {
            _light = light;
            _originalEnabled = originalEnabled;
            _originalIntensity = originalIntensity;
            _originalRange = originalRange;
            _originalColor = originalColor;
            _handling = handling;
        }

        internal void Apply()
        {
            if (_disposed || !_light)
            {
                return;
            }

            switch (_handling)
            {
                case LightHandling.BoostGreen:
                    _light.enabled = true;
                    _light.intensity = _originalIntensity * GreenIntensityMultiplier;
                    _light.range = _originalRange * GreenRangeMultiplier;
                    _light.color = Color.Lerp(_originalColor, BoostedGreenColor, GreenColorBlend);
                    break;
                case LightHandling.Suppress:
                    _light.enabled = false;
                    break;
            }
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

        internal static LightSession? TryCreate(Light light)
        {
            if (light is null || !light)
            {
                return null;
            }

            return new LightSession(
                light,
                light.enabled,
                light.intensity,
                light.range,
                light.color,
                Classify(light));
        }

        private static LightHandling Classify(Light light)
        {
            if (IsGreenish(light.color))
            {
                return LightHandling.BoostGreen;
            }

            return LightHandling.Suppress;
        }

        private static bool IsGreenish(Color color)
        {
            return color.g > 0.45f
                && color.g >= color.r * 1.15f
                && color.g >= color.b * 1.3f;
        }
    }

    private enum LightHandling
    {
        Suppress,
        BoostGreen,
    }
}
