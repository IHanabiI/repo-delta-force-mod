using System;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class AirDropCaseAutoSpawnService
{
    private const string RuntimeSoftReloadSubsystemName = "air-drop-case-auto-spawn";
    private const float RetryIntervalSeconds = 1f;
    private const float SpawnDistanceMeters = 2.2f;
    private const float SpawnHeightOffsetMeters = 0.25f;
    private const string PreferredItemAssetName = "Item Havoc AirDrop Case";
    private const string PreferredDisplayNameEnglish = "Havoc AirDrop Case";
    private const string PreferredDisplayNameChinese = "航空箱";

    private static int _activeSceneHandle = -1;
    private static string _activeSceneName = "<unknown>";
    private static float _nextAttemptAtTime;
    private static bool _sceneSpawnFinished;
    private static bool _missingItemLogged;
    private static bool _existingCaseLogged;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _activeSceneHandle = -1;
        _activeSceneName = "<unknown>";
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _sceneSpawnFinished = false;
        _missingItemLogged = false;
        _existingCaseLogged = false;

        RepoDeltaForceMod.Logger.LogInfo(
            $"air drop case auto-spawn state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        if (!OpeningHavocEventService.IsAirDropCaseInsertionActive)
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

        if (HasOfficialAirDropCasePresent())
        {
            if (!_existingCaseLogged)
            {
                RepoDeltaForceMod.Logger.LogInfo(
                    $"Air drop case auto-spawn skipped in scene '{_activeSceneName}': an official air drop case is already present.");
                _existingCaseLogged = true;
            }

            _sceneSpawnFinished = true;
            return;
        }

        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        if (!TryResolveAirDropCaseItem(out var item, out var failureReason))
        {
            if (!_missingItemLogged)
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Air drop case auto-spawn could not find the authored item in scene '{_activeSceneName}'. {failureReason}");
                _missingItemLogged = true;
            }

            return;
        }

        if (!TryResolveSpawnPose(out var spawnPosition, out var spawnRotation))
        {
            return;
        }

        if (!TrySpawnAirDropCase(item, spawnPosition, spawnRotation, out var spawnedCase, out var spawnReason))
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"Air drop case auto-spawn failed in scene '{_activeSceneName}'. {spawnReason}");
            return;
        }

        _sceneSpawnFinished = true;
        RepoDeltaForceMod.Logger.LogInfo(
            $"Air drop case auto-spawned in scene '{_activeSceneName}': ItemAsset={item.name} | ItemName={item.itemName} | Position={spawnPosition} | RuntimeName={spawnedCase.name}");

        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"航空箱已在场景 {_activeSceneName} 自动投放");
    }

    internal static bool TrySpawnIntoWorld(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject spawnedCase,
        out string failureReason)
    {
        spawnedCase = null!;
        failureReason = string.Empty;

        if (!TryResolveAirDropCaseItem(out var item, out failureReason))
        {
            return false;
        }

        return TrySpawnAirDropCase(item, spawnPosition, spawnRotation, out spawnedCase, out failureReason);
    }

    private static void ResetForScene(Scene scene)
    {
        _activeSceneHandle = scene.handle;
        _activeSceneName = string.IsNullOrWhiteSpace(scene.name) ? "<unnamed>" : scene.name;
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _sceneSpawnFinished = false;
        _missingItemLogged = false;
        _existingCaseLogged = false;

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

    private static bool HasOfficialAirDropCasePresent()
    {
        if (Inventory.instance is not null)
        {
            foreach (var spot in Inventory.instance.GetAllSpots())
            {
                if (spot?.CurrentItem is not null && AirDropCaseIdentity.IsOfficialAirDropCase(spot.CurrentItem))
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

            if (AirDropCaseIdentity.IsOfficialAirDropCase(itemAttributes))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveAirDropCaseItem(out Item item, out string failureReason)
    {
        item = null!;
        failureReason = string.Empty;

        if (StatsManager.instance is null)
        {
            failureReason = "StatsManager.instance is not available yet.";
            return false;
        }

        item = StatsManager.instance.itemDictionary.Values.FirstOrDefault(MatchesAirDropCaseItem)!;
        if (item is null)
        {
            failureReason =
                $"Expected authored item '{PreferredItemAssetName}' but it is not present in StatsManager.itemDictionary.";
            return false;
        }

        if (item.prefab is null || !item.prefab.IsValid())
        {
            failureReason =
                $"Matched item '{item.name}', but its prefab reference is empty. Re-open the Item asset in Unity and confirm the prefab field points to '{AirDropCaseIdentity.PrefabRootName}'.";
            return false;
        }

        if (GameManager.instance.gameMode != 0 && string.IsNullOrWhiteSpace(item.prefab.ResourcePath))
        {
            failureReason =
                $"Matched item '{item.name}', but its prefab ResourcePath is empty. Multiplayer room spawning cannot work until the prefab is inside a Resources folder.";
            return false;
        }

        return true;
    }

    private static bool MatchesAirDropCaseItem(Item item)
    {
        if (!item || item.disabled)
        {
            return false;
        }

        return MatchesName(item.name)
            || MatchesName(item.itemName)
            || MatchesName(item.prefab?.PrefabName)
            || MatchesName(item.prefab?.ResourcePath);
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
            || string.Equals(candidate, AirDropCaseIdentity.PrefabRootName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = candidate
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return normalized.IndexOf("havocairdropcase", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("airdropcase", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("airdrop", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf(PreferredDisplayNameChinese, StringComparison.Ordinal) >= 0;
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

    private static bool TrySpawnAirDropCase(
        Item item,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject spawnedCase,
        out string failureReason)
    {
        spawnedCase = null!;
        failureReason = string.Empty;

        try
        {
            GameObject? spawnedObject;
            if (GameManager.instance.gameMode == 0)
            {
                var prefab = item.prefab.Prefab;
                if (prefab is null)
                {
                    failureReason =
                        $"PrefabRef could not load '{item.prefab.ResourcePath}'. Confirm the source prefab lives under a Resources folder in the exported build.";
                    return false;
                }

                spawnedObject = UnityObject.Instantiate(prefab, spawnPosition, spawnRotation);
            }
            else
            {
                spawnedObject = PhotonNetwork.InstantiateRoomObject(
                    item.prefab.ResourcePath,
                    spawnPosition,
                    spawnRotation,
                    0);
            }

            if (spawnedObject is null)
            {
                failureReason = $"Instantiate returned null for source item '{item.name}'.";
                return false;
            }

            ApplyAirDropCaseRuntimeIdentity(spawnedObject);
            spawnedCase = spawnedObject;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Exception while spawning '{item.name}': {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static void ApplyAirDropCaseRuntimeIdentity(GameObject spawnedObject)
    {
        spawnedObject.name = AirDropCaseIdentity.PrefabRootName;

        var identity = spawnedObject.GetComponent<HavocSupplyIdentity>() ?? spawnedObject.AddComponent<HavocSupplyIdentity>();
        SetMemberValue(identity, "stableId", AirDropCaseIdentity.StableId);
        SetMemberValue(identity, "displayName", PreferredDisplayNameChinese);
        SetMemberValue(identity, "prefabRootName", AirDropCaseIdentity.PrefabRootName);

        if (spawnedObject.GetComponent<HavocAirDropCaseBehaviour>() is null)
        {
            spawnedObject.AddComponent<HavocAirDropCaseBehaviour>();
        }

        if (spawnedObject.TryGetComponent<ItemAttributes>(out var itemAttributes))
        {
            SetMemberValue(itemAttributes, "itemName", PreferredDisplayNameEnglish);
            SetMemberValue(itemAttributes, "instanceName", AirDropCaseIdentity.PrefabRootName);
        }

        AirDropCaseTuningService.Apply(spawnedObject, opened: false);
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
}
