using System;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class FlightRecorderAutoSpawnService
{
    private const string RuntimeSoftReloadSubsystemName = "flight-recorder-auto-spawn";
    private const float RetryIntervalSeconds = 1f;
    private const float SpawnDistanceMeters = 1.8f;
    private const float SpawnHeightOffsetMeters = 0.25f;
    private const float FlightRecorderDollarValue = 9998f;
    private const int FlightRecorderDollarValueOverride = 9998;
    private const string PreferredItemAssetName = "Item Havoc Flight Recorder";
    private const string PreferredDisplayNameEnglish = "Havoc Flight Recorder";
    private const string PreferredDisplayNameChinese = "\u98de\u884c\u8bb0\u5f55\u4eea";
    private const string SurrogateItemAssetName = "Item Valuable Tracker";
    private static readonly string[] SurrogateComponentTypeNamesToDisable =
    {
        "ItemTracker",
        "ItemBattery",
        "MapCustom",
        "MapCustomEntity",
        "Map",
    };

    private static readonly string[] FlightRecorderToolComponentTypeNamesToDisable =
    {
        "ItemEquippable",
        "ItemBattery",
        "ItemToggle",
        "BatteryVisualLogic",
    };

    private static readonly string[] FlightRecorderToolObjectNamesToDisable =
    {
        "Canvas",
        "Item Battery",
        "Battery Visual",
        "Battery Out Line",
        "Battery Border",
        "Battery Charge",
        "Charge Needed",
        "Large Plus",
        "Force Grab Point",
    };

    // The tracker surrogate is only a temporary gameplay carrier for testing.
    // The intended product definition remains a high-value Havoc valuable that can
    // lose value, break, and emit environmental interference.

    private static int _activeSceneHandle = -1;
    private static string _activeSceneName = "<unknown>";
    private static float _nextAttemptAtTime;
    private static bool _sceneSpawnFinished;
    private static bool _missingItemLogged;
    private static bool _existingRecorderLogged;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _activeSceneHandle = -1;
        _activeSceneName = "<unknown>";
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _sceneSpawnFinished = false;
        _missingItemLogged = false;
        _existingRecorderLogged = false;

        RepoDeltaForceMod.Logger.LogInfo(
            $"flight recorder auto-spawn state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        if (!OpeningHavocEventService.IsFlightRecorderInsertionActive)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        if (_activeSceneHandle != scene.handle)
        {
            ResetForScene(scene);
        }

        if (_sceneSpawnFinished || Time.unscaledTime < _nextAttemptAtTime)
        {
            return;
        }

        if (!IsGameplaySceneReady())
        {
            _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
            return;
        }

        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;

        if (HasOfficialFlightRecorderPresent())
        {
            if (!_existingRecorderLogged)
            {
                RepoDeltaForceMod.Logger.LogInfo(
                    $"Flight recorder auto-spawn skipped in scene '{_activeSceneName}': an official flight recorder is already present.");
                _existingRecorderLogged = true;
            }

            _sceneSpawnFinished = true;
            return;
        }

        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        if (!TryResolveFlightRecorderSource(out var recorderSource, out var failureReason))
        {
            if (!_missingItemLogged)
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Flight recorder auto-spawn could not find a usable source item in scene '{_activeSceneName}'. {failureReason}");
                _missingItemLogged = true;
            }

            return;
        }

        if (!TryResolveSpawnPose(out var spawnPosition, out var spawnRotation))
        {
            return;
        }

        if (!TrySpawnFlightRecorder(recorderSource, spawnPosition, spawnRotation, out var spawnedRecorder, out var spawnReason))
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"Flight recorder auto-spawn failed in scene '{_activeSceneName}'. {spawnReason}");
            return;
        }

        _sceneSpawnFinished = true;
        RepoDeltaForceMod.Logger.LogInfo(
            $"Flight recorder auto-spawned in scene '{_activeSceneName}': Source={recorderSource.SourceLabel} | ItemAsset={recorderSource.Item.name} | ItemName={recorderSource.Item.itemName} | Position={spawnPosition} | RuntimeName={spawnedRecorder.name}");

        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"飞行记录仪已在场景 {_activeSceneName} 自动投放");
    }

    internal static bool TrySpawnIntoWorld(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject spawnedRecorder,
        out string failureReason)
    {
        spawnedRecorder = null!;
        failureReason = string.Empty;

        if (!TryResolveFlightRecorderSource(out var recorderSource, out failureReason))
        {
            return false;
        }

        return TrySpawnFlightRecorder(recorderSource, spawnPosition, spawnRotation, out spawnedRecorder, out failureReason);
    }

    private static void ResetForScene(Scene scene)
    {
        _activeSceneHandle = scene.handle;
        _activeSceneName = string.IsNullOrWhiteSpace(scene.name) ? "<unnamed>" : scene.name;
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _sceneSpawnFinished = false;
        _missingItemLogged = false;
        _existingRecorderLogged = false;

        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"场景已切换：{_activeSceneName}");
    }

    private static bool IsGameplaySceneReady()
    {
        if (LevelGenerator.Instance is null || !LevelGenerator.Instance.Generated)
        {
            return false;
        }

        if (StatsManager.instance is null || GameManager.instance is null)
        {
            return false;
        }

        if (Camera.main is null && PhysGrabber.instance is null)
        {
            return false;
        }

        return true;
    }

    private static bool HasOfficialFlightRecorderPresent()
    {
        if (Inventory.instance is not null)
        {
            foreach (var spot in Inventory.instance.GetAllSpots())
            {
                if (spot?.CurrentItem is not null && FlightRecorderIdentity.IsOfficialFlightRecorder(spot.CurrentItem))
                {
                    return true;
                }
            }
        }

        foreach (var itemAttributes in UnityObject.FindObjectsByType<ItemAttributes>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            if (itemAttributes is null)
            {
                continue;
            }

            if (FlightRecorderIdentity.IsOfficialFlightRecorder(itemAttributes))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveFlightRecorderSource(out FlightRecorderSpawnSource recorderSource, out string failureReason)
    {
        recorderSource = null!;
        failureReason = string.Empty;

        if (StatsManager.instance is null)
        {
            failureReason = "StatsManager.instance is not available yet.";
            return false;
        }

        var authoredItem = StatsManager.instance.itemDictionary.Values.FirstOrDefault(MatchesFlightRecorderItem);
        if (authoredItem is not null)
        {
            if (authoredItem.prefab is null || !authoredItem.prefab.IsValid())
            {
                failureReason =
                    $"Matched item '{authoredItem.name}', but its prefab reference is empty. Re-open the Item asset in Unity and confirm the prefab field points to '{FlightRecorderIdentity.PrefabRootName}'.";
                return false;
            }

            if (GameManager.instance.gameMode != 0 && string.IsNullOrWhiteSpace(authoredItem.prefab.ResourcePath))
            {
                failureReason =
                    $"Matched item '{authoredItem.name}', but its prefab ResourcePath is empty. Multiplayer room spawning cannot work until the prefab is inside a Resources folder.";
                return false;
            }

            recorderSource = FlightRecorderSpawnSource.Authored(authoredItem);
            return true;
        }

        var surrogateItem = StatsManager.instance.itemDictionary.Values.FirstOrDefault(MatchesSurrogateItem);
        if (surrogateItem is not null)
        {
            if (surrogateItem.prefab is null || !surrogateItem.prefab.IsValid())
            {
                failureReason =
                    $"The fallback surrogate item '{surrogateItem.name}' exists, but its prefab reference is empty.";
                return false;
            }

            if (GameManager.instance.gameMode != 0 && string.IsNullOrWhiteSpace(surrogateItem.prefab.ResourcePath))
            {
                failureReason =
                    $"The fallback surrogate item '{surrogateItem.name}' exists, but its prefab ResourcePath is empty.";
                return false;
            }

            recorderSource = FlightRecorderSpawnSource.Surrogate(surrogateItem);
            return true;
        }

        failureReason =
            $"Expected authored item '{PreferredItemAssetName}' but it is not present in StatsManager.itemDictionary. No fallback surrogate item named '{SurrogateItemAssetName}' was found either.";
        return false;
    }

    private static bool MatchesFlightRecorderItem(Item item)
    {
        if (!item || item.disabled)
        {
            return false;
        }

        if (MatchesName(item.name)
            || MatchesName(item.itemName)
            || MatchesName(item.prefab?.PrefabName)
            || MatchesName(item.prefab?.ResourcePath))
        {
            return true;
        }

        return false;
    }

    private static bool MatchesName(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (string.Equals(candidate, PreferredItemAssetName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, PreferredDisplayNameEnglish, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate, PreferredDisplayNameChinese, StringComparison.Ordinal)
            || string.Equals(candidate, FlightRecorderIdentity.PrefabRootName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = candidate
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return normalized.IndexOf("havocflightrecorder", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("flightrecorder", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf(PreferredDisplayNameChinese, StringComparison.Ordinal) >= 0;
    }

    private static bool MatchesSurrogateItem(Item item)
    {
        if (!item || item.disabled)
        {
            return false;
        }

        return string.Equals(item.name, SurrogateItemAssetName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.itemName, "Valuable Tracker", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveSpawnPose(out Vector3 spawnPosition, out Quaternion spawnRotation)
    {
        spawnPosition = default;
        spawnRotation = Quaternion.identity;

        var origin = Camera.main is not null
            ? Camera.main.transform
            : PhysGrabber.instance is not null
                ? PhysGrabber.instance.transform
                : null;
        if (origin is null)
        {
            return false;
        }

        var forward = origin.forward;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        var horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (horizontalForward.sqrMagnitude < 0.0001f)
        {
            horizontalForward = forward.normalized;
        }

        var rawSpawnPosition = origin.position + horizontalForward * SpawnDistanceMeters + Vector3.up * 0.5f;
        if (Physics.Raycast(rawSpawnPosition, Vector3.down, out var hitInfo, 3f))
        {
            spawnPosition = hitInfo.point + Vector3.up * SpawnHeightOffsetMeters;
        }
        else
        {
            spawnPosition = rawSpawnPosition;
        }

        spawnRotation = Quaternion.LookRotation(horizontalForward, Vector3.up);
        return true;
    }

    private static bool TrySpawnFlightRecorder(
        FlightRecorderSpawnSource recorderSource,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject spawnedRecorder,
        out string failureReason)
    {
        spawnedRecorder = null!;
        failureReason = string.Empty;

        try
        {
            GameObject? spawnedObject;
            if (GameManager.instance.gameMode == 0)
            {
                var prefab = recorderSource.Item.prefab.Prefab;
                if (prefab is null)
                {
                    failureReason =
                        $"PrefabRef could not load '{recorderSource.Item.prefab.ResourcePath}'. Confirm the source prefab lives under a Resources folder in the exported build.";
                    return false;
                }

                spawnedObject = UnityObject.Instantiate(prefab, spawnPosition, spawnRotation);
            }
            else
            {
                spawnedObject = PhotonNetwork.InstantiateRoomObject(
                    recorderSource.Item.prefab.ResourcePath,
                    spawnPosition,
                    spawnRotation,
                    0);
            }

            if (spawnedObject is null)
            {
                failureReason = $"Instantiate returned null for source item '{recorderSource.Item.name}'.";
                return false;
            }

            ApplyFlightRecorderRuntimeIdentity(spawnedObject);

            EnsureFlightRecorderValueContract(spawnedObject);

            if (recorderSource.RequiresRuntimeSurrogateMutation)
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Authored flight recorder item is not in the live runtime yet, so a temporary surrogate recorder was spawned from '{recorderSource.Item.name}' for gameplay testing.");
            }

            var validation = HavocSupplyContractValidator.ValidateFlightRecorder(spawnedObject);
            if (!validation.IsReadyAsHighValueValuable)
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Flight recorder spawn contract is still incomplete on '{spawnedObject.name}'. Missing={string.Join(", ", validation.MissingParts)}");
            }

            spawnedRecorder = spawnedObject;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Exception while spawning '{recorderSource.Item.name}': {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static void ApplyFlightRecorderRuntimeIdentity(GameObject spawnedObject)
    {
        spawnedObject.name = FlightRecorderIdentity.PrefabRootName;

        var identity = spawnedObject.GetComponent<HavocSupplyIdentity>() ?? spawnedObject.AddComponent<HavocSupplyIdentity>();
        SetMemberValue(identity, "stableId", FlightRecorderIdentity.StableId);
        SetMemberValue(identity, "displayName", PreferredDisplayNameChinese);
        SetMemberValue(identity, "prefabRootName", FlightRecorderIdentity.PrefabRootName);

        if (spawnedObject.GetComponent<HavocFlightRecorderBehaviour>() is null)
        {
            spawnedObject.AddComponent<HavocFlightRecorderBehaviour>();
        }

        if (spawnedObject.TryGetComponent<ItemAttributes>(out var itemAttributes))
        {
            SetMemberValue(itemAttributes, "itemName", PreferredDisplayNameChinese);
            SetMemberValue(itemAttributes, "instanceName", FlightRecorderIdentity.PrefabRootName);
        }

        ApplyPreferredDisplayMetadata(spawnedObject);
        EnforceToolOnlyComponentsDisabled(spawnedObject);

        // When we fall back to the tracker prefab, strip tracker-only systems so the
        // runtime object behaves like the flight recorder identity instead of a map tool.
        foreach (var component in spawnedObject.GetComponentsInChildren<Component>(true))
        {
            if (component is null)
            {
                continue;
            }

            if (!SurrogateComponentTypeNamesToDisable.Contains(component.GetType().Name, StringComparer.Ordinal))
            {
                continue;
            }

            UnityObject.Destroy(component);
        }
    }

    internal static void EnforceToolOnlyComponentsDisabled(GameObject spawnedObject)
    {
        foreach (var component in spawnedObject.GetComponentsInChildren<Component>(true))
        {
            if (component is null)
            {
                continue;
            }

            if (FlightRecorderToolComponentTypeNamesToDisable.Contains(component.GetType().Name, StringComparer.Ordinal))
            {
                UnityObject.Destroy(component);
            }
        }

        foreach (var transform in spawnedObject.GetComponentsInChildren<Transform>(true))
        {
            if (transform is null || transform == spawnedObject.transform)
            {
                continue;
            }

            if (!FlightRecorderToolObjectNamesToDisable.Contains(transform.name, StringComparer.Ordinal))
            {
                continue;
            }

            transform.gameObject.SetActive(false);
        }
    }

    private static void SetMemberValue(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field is not null && field.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
            return;
        }

        var property = type.GetProperty(memberName, Flags);
        if (property is not null && property.PropertyType == typeof(string) && property.CanWrite)
        {
            property.SetValue(instance, value);
        }
    }

    private static void EnsureFlightRecorderValueContract(GameObject spawnedObject)
    {
        var valuableObject = spawnedObject.GetComponent<ValuableObject>();
        if (valuableObject is null)
        {
            valuableObject = spawnedObject.AddComponent<ValuableObject>();
            RepoDeltaForceMod.Logger.LogInfo(
                $"Flight recorder '{spawnedObject.name}' was missing ValuableObject. Added a runtime ValuableObject component for high-value valuable testing.");
        }

        SetMemberValue(valuableObject, "dollarValueOriginal", FlightRecorderDollarValue);
        SetMemberValue(valuableObject, "dollarValueCurrent", FlightRecorderDollarValue);
        SetMemberValue(valuableObject, "dollarValueOverride", FlightRecorderDollarValueOverride);
        SetMemberValue(valuableObject, "dollarValueSet", true);
        SetMemberValue(valuableObject, "discovered", false);
        SetMemberValue(valuableObject, "discoveredReminder", false);
    }

    private static void ApplyPreferredDisplayMetadata(GameObject spawnedObject)
    {
        foreach (var component in spawnedObject.GetComponentsInChildren<Component>(true))
        {
            if (component is null)
            {
                continue;
            }

            SetMemberValue(component, "itemName", PreferredDisplayNameChinese);
            SetMemberValue(component, "displayName", PreferredDisplayNameChinese);
            SetMemberValue(component, "debugDisplayName", PreferredDisplayNameChinese);
            SetMemberValue(component, "instanceName", FlightRecorderIdentity.PrefabRootName);
        }
    }

    private static void SetMemberValue(object instance, string memberName, float value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field is not null)
        {
            if (field.FieldType == typeof(float))
            {
                field.SetValue(instance, value);
                return;
            }

            if (field.FieldType == typeof(int))
            {
                field.SetValue(instance, (int)value);
                return;
            }
        }

        var property = type.GetProperty(memberName, Flags);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType == typeof(float))
        {
            property.SetValue(instance, value);
        }
        else if (property.PropertyType == typeof(int))
        {
            property.SetValue(instance, (int)value);
        }
    }

    private static void SetMemberValue(object instance, string memberName, int value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field is not null)
        {
            if (field.FieldType == typeof(int))
            {
                field.SetValue(instance, value);
                return;
            }

            if (field.FieldType == typeof(float))
            {
                field.SetValue(instance, (float)value);
                return;
            }
        }

        var property = type.GetProperty(memberName, Flags);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType == typeof(int))
        {
            property.SetValue(instance, value);
        }
        else if (property.PropertyType == typeof(float))
        {
            property.SetValue(instance, (float)value);
        }
    }

    private static void SetMemberValue(object instance, string memberName, bool value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field is not null && field.FieldType == typeof(bool))
        {
            field.SetValue(instance, value);
            return;
        }

        var property = type.GetProperty(memberName, Flags);
        if (property is not null && property.PropertyType == typeof(bool) && property.CanWrite)
        {
            property.SetValue(instance, value);
        }
    }

    private sealed class FlightRecorderSpawnSource
    {
        private FlightRecorderSpawnSource(Item item, string sourceLabel, bool requiresRuntimeSurrogateMutation)
        {
            Item = item;
            SourceLabel = sourceLabel;
            RequiresRuntimeSurrogateMutation = requiresRuntimeSurrogateMutation;
        }

        internal Item Item { get; }

        internal string SourceLabel { get; }

        internal bool RequiresRuntimeSurrogateMutation { get; }

        internal static FlightRecorderSpawnSource Authored(Item item)
        {
            return new FlightRecorderSpawnSource(item, "authored", false);
        }

        internal static FlightRecorderSpawnSource Surrogate(Item item)
        {
            return new FlightRecorderSpawnSource(item, "surrogate", true);
        }
    }
}
