using System;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class MilitaryTerminalAutoSpawnService
{
    private const string RuntimeSoftReloadSubsystemName = "military-terminal-auto-spawn";
    private const float RetryIntervalSeconds = 1f;
    private const float AuthoredItemGracePeriodSeconds = 12f;
    private const float SpawnDistanceMeters = 1.8f;
    private const float SpawnHeightOffsetMeters = 0.25f;
    private const float PlacementResolveStepMeters = 0.08f;
    private const int PlacementResolveMaxSteps = 10;
    private const string PreferredItemAssetName = "Item Havoc Military Terminal";
    private const string PreferredDisplayNameEnglish = "Havoc Military Terminal";
    private const string PreferredDisplayNameChinese = "\u519b\u7528\u4fe1\u606f\u7ec8\u7aef";
    private const string SurrogateItemAssetName = "Item Valuable Tracker";
    private static readonly string[] SurrogateComponentTypeNamesToDisable =
    {
        "ItemTracker",
        "MapCustom",
        "MapCustomEntity",
        "Map",
    };

    private static readonly string[] TerminalToolUiComponentTypeNamesToDisable =
    {
        "ItemBattery",
        "BatteryVisualLogic",
        "ItemToggle",
        "GraphicRaycaster",
    };

    private static readonly string[] TerminalToolUiObjectNamesToDisable =
    {
        "Canvas",
        "Display",
        "Item Battery",
        "Battery Visual",
        "Battery BG",
        "BatteryBarsContainer",
        "Battery Out Line",
        "Battery Out",
        "Battery Border",
        "Battery Border Shadow",
        "Battery Charge",
        "Battery Drain",
        "Charge Needed",
        "Upgrade",
        "Health Pack",
        "Large Plus",
        "Valuable Tracker Mesh",
    };

    private static int _activeSceneHandle = -1;
    private static string _activeSceneName = "<unknown>";
    private static float _nextAttemptAtTime;
    private static bool _sceneSpawnFinished;
    private static bool _missingItemLogged;
    private static bool _existingTerminalLogged;
    private static float _gameplayReadyAtTime = -1f;
    private static PendingInventoryEquip? _pendingInventoryEquip;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _activeSceneHandle = -1;
        _activeSceneName = "<unknown>";
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _sceneSpawnFinished = false;
        _missingItemLogged = false;
        _existingTerminalLogged = false;
        _gameplayReadyAtTime = -1f;
        _pendingInventoryEquip = null;

        RepoDeltaForceMod.Logger.LogInfo(
            $"military terminal auto-spawn state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        if (!ModFeatureSettings.AutomaticMilitaryTerminalSceneSpawnEnabled)
        {
            return;
        }

        if (!OpeningHavocEventService.IsMilitaryTerminalInsertionActive)
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

        TickPendingInventoryEquip();

        if (_sceneSpawnFinished || Time.unscaledTime < _nextAttemptAtTime)
        {
            return;
        }

        if (!IsGameplaySceneReady())
        {
            _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
            return;
        }

        if (_gameplayReadyAtTime < 0f)
        {
            _gameplayReadyAtTime = Time.unscaledTime;
        }

        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;

        if (HasOfficialTerminalPresent())
        {
            if (!_existingTerminalLogged)
            {
                RepoDeltaForceMod.Logger.LogInfo(
                    $"Military terminal auto-spawn skipped in scene '{_activeSceneName}': an official terminal is already present.");
                _existingTerminalLogged = true;
            }

            _sceneSpawnFinished = true;
            return;
        }

        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        if (!TryResolveTerminalSource(out var terminalSource, out var failureReason))
        {
            if (!_missingItemLogged)
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Military terminal auto-spawn could not find the authored item in scene '{_activeSceneName}'. {failureReason}");
                _missingItemLogged = true;
            }

            return;
        }

        if (!TryResolveSpawnPose(out var spawnPosition, out var spawnRotation))
        {
            return;
        }

        if (!TrySpawnTerminal(terminalSource, spawnPosition, spawnRotation, out var spawnedTerminal, out var spawnReason))
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"Military terminal auto-spawn failed in scene '{_activeSceneName}'. {spawnReason}");
            return;
        }

        _sceneSpawnFinished = true;
        RepoDeltaForceMod.Logger.LogInfo(
            $"Military terminal auto-spawned in scene '{_activeSceneName}': Source={terminalSource.SourceLabel} | ItemAsset={terminalSource.Item.name} | ItemName={terminalSource.Item.itemName} | Position={spawnPosition} | RuntimeName={spawnedTerminal.name}");
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"军用信息终端已在场景 {_activeSceneName} 自动补发");

        if (spawnedTerminal.TryGetComponent<ItemEquippable>(out var itemEquippable))
        {
            _pendingInventoryEquip = new PendingInventoryEquip(itemEquippable, terminalSource.SourceLabel);
        }
    }

    internal static bool TrySpawnIntoWorld(
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject spawnedTerminal,
        out string failureReason)
    {
        spawnedTerminal = null!;
        failureReason = string.Empty;

        if (!TryResolveTerminalSource(out var terminalSource, out failureReason))
        {
            return false;
        }

        return TrySpawnTerminal(terminalSource, spawnPosition, spawnRotation, out spawnedTerminal, out failureReason);
    }

    private static void ResetForScene(Scene scene)
    {
        _activeSceneHandle = scene.handle;
        _activeSceneName = string.IsNullOrWhiteSpace(scene.name) ? "<unnamed>" : scene.name;
        _nextAttemptAtTime = Time.unscaledTime + RetryIntervalSeconds;
        _sceneSpawnFinished = false;
        _missingItemLogged = false;
        _existingTerminalLogged = false;
        _gameplayReadyAtTime = -1f;
        _pendingInventoryEquip = null;

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

    private static bool HasOfficialTerminalPresent()
    {
        if (Inventory.instance is not null)
        {
            foreach (var spot in Inventory.instance.GetAllSpots())
            {
                if (spot?.CurrentItem is not null && MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(spot.CurrentItem))
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

            if (MilitaryTerminalIdentity.IsOfficialMilitaryTerminal(itemAttributes))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveTerminalSource(out TerminalSpawnSource terminalSource, out string failureReason)
    {
        terminalSource = null!;
        failureReason = string.Empty;

        if (StatsManager.instance is null)
        {
            failureReason = "StatsManager.instance is not available yet.";
            return false;
        }

        var authoredItem = StatsManager.instance.itemDictionary.Values.FirstOrDefault(MatchesTerminalItem);
        if (authoredItem is not null)
        {
            if (authoredItem.prefab is null || !authoredItem.prefab.IsValid())
            {
                failureReason =
                    $"Matched item '{authoredItem.name}', but its prefab reference is empty. Re-open the Item asset in Unity and confirm the prefab field points to '{MilitaryTerminalIdentity.PrefabRootName}'.";
                return false;
            }

            if (GameManager.instance.gameMode != 0 && string.IsNullOrWhiteSpace(authoredItem.prefab.ResourcePath))
            {
                failureReason =
                    $"Matched item '{authoredItem.name}', but its prefab ResourcePath is empty. Multiplayer room spawning cannot work until the prefab is inside a Resources folder.";
                return false;
            }

            terminalSource = TerminalSpawnSource.Authored(authoredItem);
            return true;
        }

        var surrogateItem = StatsManager.instance.itemDictionary.Values.FirstOrDefault(MatchesSurrogateItem);
        if (surrogateItem is not null)
        {
            var authoredWaitRemainingSeconds = GetAuthoredItemWaitRemainingSeconds();
            if (authoredWaitRemainingSeconds > 0f)
            {
                failureReason =
                    $"Authored item '{PreferredItemAssetName}' is still missing, but the fallback surrogate item is available. Waiting {authoredWaitRemainingSeconds:0.#} more seconds for REPOLib/authored content to finish registering before falling back to the runtime surrogate.";
                return false;
            }

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

            terminalSource = TerminalSpawnSource.Surrogate(surrogateItem);
            return true;
        }

        failureReason =
            $"Expected authored item '{PreferredItemAssetName}' but it is not present in StatsManager.itemDictionary. This means the Unity-authored asset has not entered the live game build yet. No fallback surrogate item named '{SurrogateItemAssetName}' was found either.";
        return false;
    }

    private static float GetAuthoredItemWaitRemainingSeconds()
    {
        if (_gameplayReadyAtTime < 0f)
        {
            return AuthoredItemGracePeriodSeconds;
        }

        var elapsed = Time.unscaledTime - _gameplayReadyAtTime;
        return Mathf.Max(0f, AuthoredItemGracePeriodSeconds - elapsed);
    }

    private static bool MatchesTerminalItem(Item item)
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
            || string.Equals(candidate, MilitaryTerminalIdentity.PrefabRootName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalized = candidate
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return normalized.IndexOf("havocmilitaryterminal", StringComparison.OrdinalIgnoreCase) >= 0
            || normalized.IndexOf("militaryterminal", StringComparison.OrdinalIgnoreCase) >= 0
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

    private static bool TrySpawnTerminal(
        TerminalSpawnSource terminalSource,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject spawnedTerminal,
        out string failureReason)
    {
        spawnedTerminal = null!;
        failureReason = string.Empty;

        try
        {
            GameObject? spawnedObject;
            if (GameManager.instance.gameMode == 0)
            {
                var prefab = terminalSource.Item.prefab.Prefab;
                if (prefab is null)
                {
                    failureReason =
                        $"PrefabRef could not load '{terminalSource.Item.prefab.ResourcePath}'. Confirm the source prefab lives under a Resources folder in the exported build.";
                    return false;
                }

                spawnedObject = UnityObject.Instantiate(prefab, spawnPosition, spawnRotation);
            }
            else
            {
                spawnedObject = PhotonNetwork.InstantiateRoomObject(
                    terminalSource.Item.prefab.ResourcePath,
                    spawnPosition,
                    spawnRotation,
                    0);
            }

            if (spawnedObject is null)
            {
                failureReason = $"Instantiate returned null for source item '{terminalSource.Item.name}'.";
                return false;
            }

            ApplyMilitaryTerminalRuntimeIdentity(spawnedObject);
            ResolveTerminalPlacement(spawnedObject);

            if (terminalSource.RequiresRuntimeSurrogateMutation)
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Authored military terminal item is not in the live runtime yet, so a temporary surrogate terminal was spawned from '{terminalSource.Item.name}' for gameplay testing.");
            }

            spawnedTerminal = spawnedObject;
            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Exception while spawning '{terminalSource.Item.name}': {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static void ApplyMilitaryTerminalRuntimeIdentity(GameObject spawnedObject)
    {
        spawnedObject.name = MilitaryTerminalIdentity.PrefabRootName;

        var identity = spawnedObject.GetComponent<HavocSupplyIdentity>() ?? spawnedObject.AddComponent<HavocSupplyIdentity>();
        SetMemberValue(identity, "stableId", MilitaryTerminalIdentity.StableId);
        SetMemberValue(identity, "displayName", PreferredDisplayNameEnglish);
        SetMemberValue(identity, "prefabRootName", MilitaryTerminalIdentity.PrefabRootName);

        if (spawnedObject.GetComponent<HavocMilitaryTerminalBehaviour>() is null)
        {
            spawnedObject.AddComponent<HavocMilitaryTerminalBehaviour>();
        }

        if (spawnedObject.TryGetComponent<ItemAttributes>(out var itemAttributes))
        {
            SetMemberValue(itemAttributes, "itemName", PreferredDisplayNameEnglish);
            SetMemberValue(itemAttributes, "instanceName", MilitaryTerminalIdentity.PrefabRootName);
        }

        EnforceToolBatteryPresentationDisabled(spawnedObject);

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

    private static void ResolveTerminalPlacement(GameObject spawnedObject)
    {
        if (spawnedObject == null)
        {
            return;
        }

        var colliders = spawnedObject
            .GetComponentsInChildren<Collider>(includeInactive: true)
            .Where(collider => collider != null && collider.enabled && !collider.isTrigger)
            .ToArray();
        if (colliders.Length == 0)
        {
            return;
        }

        var originalPosition = spawnedObject.transform.position;
        var moved = false;

        for (var step = 0; step < PlacementResolveMaxSteps; step++)
        {
            if (!HasBlockingOverlap(colliders))
            {
                break;
            }

            spawnedObject.transform.position += Vector3.up * PlacementResolveStepMeters;
            moved = true;
        }

        if (moved)
        {
            RepoDeltaForceMod.Logger.LogInfo(
                $"Military terminal placement resolved: RuntimeName={spawnedObject.name} | From={originalPosition} | To={spawnedObject.transform.position}");
        }
    }

    private static bool HasBlockingOverlap(Collider[] ownColliders)
    {
        foreach (var ownCollider in ownColliders)
        {
            if (ownCollider == null)
            {
                continue;
            }

            var overlaps = Physics.OverlapBox(
                ownCollider.bounds.center,
                ownCollider.bounds.extents * 0.95f,
                ownCollider.transform.rotation,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (var overlap in overlaps)
            {
                if (overlap == null || overlap.isTrigger || overlap.transform.IsChildOf(ownCollider.transform.root))
                {
                    continue;
                }

                if (overlap.GetComponentInParent<PhysGrabObject>() != null)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    internal static void EnforceToolBatteryPresentationDisabled(GameObject spawnedObject)
    {
        foreach (var component in spawnedObject.GetComponentsInChildren<Component>(true))
        {
            if (component is null)
            {
                continue;
            }

            if (TerminalToolUiComponentTypeNamesToDisable.Contains(component.GetType().Name, StringComparer.Ordinal))
            {
                UnityObject.Destroy(component);
            }
        }

        foreach (var transform in spawnedObject.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (transform is null || transform == spawnedObject.transform)
            {
                continue;
            }

            // The original tracker surrogate carries a world-space battery canvas.
            // Keep forcing every RectTransform-based subtree off so pickup/inventory
            // state changes cannot re-enable those old UI boxes.
            if (transform is RectTransform)
            {
                transform.gameObject.SetActive(false);
                UnityObject.Destroy(transform.gameObject);
                continue;
            }

            if (!TerminalToolUiObjectNamesToDisable.Contains(transform.name, StringComparer.Ordinal))
            {
                continue;
            }

            transform.gameObject.SetActive(false);
            UnityObject.Destroy(transform.gameObject);
        }
    }

    private static void TickPendingInventoryEquip()
    {
        if (_pendingInventoryEquip is null)
        {
            return;
        }

        var pending = _pendingInventoryEquip;
        if (pending.ItemEquippable is null)
        {
            _pendingInventoryEquip = null;
            return;
        }

        if (Time.unscaledTime < pending.NextAttemptAtTime)
        {
            return;
        }

        if (Inventory.instance is null || PhysGrabber.instance is null || !Inventory.instance.spotsFeched)
        {
            pending.ScheduleRetry(Time.unscaledTime + 0.5f);
            return;
        }

        if (Inventory.instance.IsItemEquipped(pending.ItemEquippable))
        {
            RepoDeltaForceMod.Logger.LogInfo(
                $"Military terminal auto-equip confirmed in scene '{_activeSceneName}': Source={pending.SourceLabel}");
            _pendingInventoryEquip = null;
            return;
        }

        var freeSpotIndex = Inventory.instance.GetFirstFreeInventorySpotIndex();
        if (freeSpotIndex < 0)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"Military terminal auto-equip could not use the inventory in scene '{_activeSceneName}' because all three slots are occupied.");
            _pendingInventoryEquip = null;
            return;
        }

        var requestingPlayerId = GameManager.instance.gameMode == 0
            ? -1
            : PhysGrabber.instance.photonView.ViewID;

        pending.ItemEquippable.RequestEquip(freeSpotIndex, requestingPlayerId);
        pending.ScheduleRetry(Time.unscaledTime + 0.5f);
        RepoDeltaForceMod.Logger.LogInfo(
            $"Military terminal auto-equip requested in scene '{_activeSceneName}': Slot={freeSpotIndex + 1} | Source={pending.SourceLabel}");
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

    private sealed class TerminalSpawnSource
    {
        private TerminalSpawnSource(Item item, bool requiresRuntimeSurrogateMutation, string sourceLabel)
        {
            Item = item;
            RequiresRuntimeSurrogateMutation = requiresRuntimeSurrogateMutation;
            SourceLabel = sourceLabel;
        }

        internal Item Item { get; }

        internal bool RequiresRuntimeSurrogateMutation { get; }

        internal string SourceLabel { get; }

        internal static TerminalSpawnSource Authored(Item item)
        {
            return new TerminalSpawnSource(item, requiresRuntimeSurrogateMutation: false, sourceLabel: "authored");
        }

        internal static TerminalSpawnSource Surrogate(Item item)
        {
            return new TerminalSpawnSource(item, requiresRuntimeSurrogateMutation: true, sourceLabel: "runtime-surrogate");
        }
    }

    private sealed class PendingInventoryEquip
    {
        internal PendingInventoryEquip(ItemEquippable itemEquippable, string sourceLabel)
        {
            ItemEquippable = itemEquippable;
            SourceLabel = sourceLabel;
            NextAttemptAtTime = Time.unscaledTime + 0.25f;
        }

        internal ItemEquippable ItemEquippable { get; }

        internal string SourceLabel { get; }

        internal float NextAttemptAtTime { get; private set; }

        internal void ScheduleRetry(float nextAttemptAtTime)
        {
            NextAttemptAtTime = nextAttemptAtTime;
        }
    }
}
