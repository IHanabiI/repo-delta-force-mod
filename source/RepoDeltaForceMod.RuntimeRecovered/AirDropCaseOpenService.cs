using System;
using System.Collections.Generic;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace RepoDeltaForceMod;

internal static class AirDropCaseOpenService
{
    private const string RuntimeSoftReloadSubsystemName = "air-drop-case-open";
    private const string OpenedVisualResourcePath = "Items/Havoc_AirDropCase_Open";
    private const float RescanIntervalSeconds = 1f;
    private const int ClosedCaseValue = 8000;
    private const int OpenImmediateReward = 2500;
    private const int OpenedCaseValue = 1500;
    private const float ClosedCaseMass = 4f;
    private const string ClosedVisualName = "Mesh";
    private const string OpenVisualName = "MeshOpen";

    private static float _nextRescanAtTime;
    private static readonly HashSet<int> KnownRoots = new();

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        KnownRoots.Clear();
        _nextRescanAtTime = 0f;
        AirDropCaseTuningService.ResetRuntimeState(context);

        RepoDeltaForceMod.Logger.LogInfo(
            $"air drop case open state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        if (Time.unscaledTime < _nextRescanAtTime)
        {
            return;
        }

        _nextRescanAtTime = Time.unscaledTime + RescanIntervalSeconds;
        Rescan();
    }

    internal static void RequestOpen(HavocAirDropCaseBehaviour behaviour)
    {
        if (behaviour == null || behaviour.IsOpened)
        {
            return;
        }

        var rootObject = behaviour.gameObject;
        if (rootObject == null)
        {
            return;
        }

        if (!CanOpenCase(rootObject))
        {
            return;
        }

        var photonView = rootObject.GetComponent<PhotonView>();
        if (GameManager.instance.gameMode == 0 || photonView == null)
        {
            behaviour.OpenCaseLocally(OpenImmediateReward, OpenedCaseValue);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(nameof(HavocAirDropCaseBehaviour.OpenCaseRpc), RpcTarget.All, OpenImmediateReward, OpenedCaseValue);
        }
        else
        {
            photonView.RPC(nameof(HavocAirDropCaseBehaviour.RequestOpenOnMasterRpc), RpcTarget.MasterClient, OpenImmediateReward, OpenedCaseValue);
        }
    }

    internal static void ApplyOpenedState(GameObject rootObject, int immediateReward, int openedCaseValue)
    {
        if (rootObject == null)
        {
            return;
        }

        // Capture the authored closed-state spatial baseline before the open visual is shown.
        AirDropCaseTuningService.Apply(rootObject, opened: false);
        ApplyOpenedValue(rootObject, openedCaseValue);
        ApplyOpenedIdentity(rootObject);
        EnsureOpenedVisualPresent(rootObject);
        ApplyOpenedVisual(rootObject);
        AirDropCaseTuningService.Apply(rootObject, opened: true);
        ApplyImmediateReward(rootObject, immediateReward);
    }

    private static void ApplyOpenedVisual(GameObject rootObject)
    {
        var closedVisual = FindVisualTransform(rootObject.transform, ClosedVisualName);
        var openedVisual = FindVisualTransform(rootObject.transform, OpenVisualName);

        if (closedVisual != null)
        {
            closedVisual.gameObject.SetActive(openedVisual == null);
        }

        if (openedVisual != null)
        {
            openedVisual.gameObject.SetActive(true);
        }
    }

    private static void EnsureOpenedVisualPresent(GameObject rootObject)
    {
        if (FindVisualTransform(rootObject.transform, OpenVisualName) != null)
        {
            return;
        }

        var objectRoot = rootObject.transform.Find("Object");
        if (objectRoot == null)
        {
            return;
        }

        var templatePrefab = Resources.Load<GameObject>(OpenedVisualResourcePath);
        if (templatePrefab == null)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"Air drop case open visual prefab could not be loaded from Resources path '{OpenedVisualResourcePath}'.");
            return;
        }

        var templateVisual = FindOpenedVisualTemplate(templatePrefab.transform);
        if (templateVisual == null)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"Air drop case open visual prefab '{templatePrefab.name}' is missing both '{ClosedVisualName}' and '{OpenVisualName}' under Object.");
            return;
        }

        var instantiatedVisual = UnityObject.Instantiate(templateVisual.gameObject, objectRoot).transform;
        instantiatedVisual.name = OpenVisualName;
        instantiatedVisual.localPosition = templateVisual.localPosition;
        instantiatedVisual.localRotation = templateVisual.localRotation;
        instantiatedVisual.localScale = templateVisual.localScale;
        instantiatedVisual.gameObject.SetActive(false);
    }

    private static void Rescan()
    {
        foreach (var behaviour in UnityObject.FindObjectsOfType<HavocAirDropCaseBehaviour>(true))
        {
            if (behaviour == null)
            {
                continue;
            }

            KnownRoots.Add(behaviour.gameObject.GetInstanceID());
        }

        foreach (var itemAttributes in UnityObject.FindObjectsByType<ItemAttributes>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (itemAttributes == null || !AirDropCaseIdentity.IsOfficialAirDropCase(itemAttributes))
            {
                continue;
            }

            var root = itemAttributes.gameObject;
            if (root == null)
            {
                continue;
            }

            if (root.GetComponent<HavocAirDropCaseBehaviour>() == null)
            {
                root.AddComponent<HavocAirDropCaseBehaviour>();
                RuntimeSoftReloadManager.MarkSubsystemDirty(
                    RuntimeSoftReloadSubsystemName,
                    $"航空箱开启逻辑已附着到对象：{root.name}");
            }

            var behaviour = root.GetComponent<HavocAirDropCaseBehaviour>();
            if (behaviour != null && !behaviour.IsOpened)
            {
                ApplyClosedState(root, behaviour);
            }

            AirDropCaseTuningService.Apply(root, behaviour?.IsOpened == true);

            KnownRoots.Add(root.GetInstanceID());
        }
    }

    internal static void ApplyClosedState(GameObject rootObject, HavocAirDropCaseBehaviour? behaviour = null)
    {
        if (rootObject == null)
        {
            return;
        }

        var closedVisual = FindVisualTransform(rootObject.transform, ClosedVisualName);
        var openedVisual = FindVisualTransform(rootObject.transform, OpenVisualName);
        if (closedVisual != null)
        {
            closedVisual.gameObject.SetActive(true);
        }

        if (openedVisual != null)
        {
            openedVisual.gameObject.SetActive(false);
        }

        if (rootObject.TryGetComponent<Rigidbody>(out var rigidbody))
        {
            rigidbody.mass = ClosedCaseMass;
        }

        if (behaviour == null)
        {
            behaviour = rootObject.GetComponent<HavocAirDropCaseBehaviour>();
        }

        var shouldInitializeClosedValue = behaviour == null || !behaviour.ClosedValueInitialized;
        if (rootObject.TryGetComponent<ValuableObject>(out var valuableObject) && shouldInitializeClosedValue)
        {
            valuableObject.dollarValueCurrent = ClosedCaseValue;
            valuableObject.dollarValueOriginal = ClosedCaseValue;
            valuableObject.dollarValueOverride = ClosedCaseValue;
            valuableObject.dollarValueSet = true;
        }

        if (rootObject.TryGetComponent<ItemAttributes>(out var itemAttributes) && shouldInitializeClosedValue)
        {
            SetMemberValue(itemAttributes, "value", ClosedCaseValue);
        }

        if (shouldInitializeClosedValue && behaviour != null)
        {
            behaviour.ClosedValueInitialized = true;
        }
    }

    private static void ApplyOpenedIdentity(GameObject rootObject)
    {
        rootObject.name = AirDropCaseIdentity.OpenPrefabRootName;

        var identity = rootObject.GetComponent<HavocSupplyIdentity>();
        if (identity != null)
        {
            SetMemberValue(identity, "stableId", AirDropCaseIdentity.StableId);
            SetMemberValue(identity, "displayName", "航空箱（已开启）");
            SetMemberValue(identity, "prefabRootName", AirDropCaseIdentity.OpenPrefabRootName);
        }

        if (rootObject.TryGetComponent<ItemAttributes>(out var itemAttributes))
        {
            SetMemberValue(itemAttributes, "itemName", "Havoc AirDrop Case Opened");
            SetMemberValue(itemAttributes, "instanceName", AirDropCaseIdentity.OpenPrefabRootName);
            SetMemberValue(itemAttributes, "value", OpenedCaseValueFallback(openedCaseValue: 0));
        }
    }

    private static int OpenedCaseValueFallback(int openedCaseValue)
    {
        return openedCaseValue <= 0 ? OpenedCaseValue : openedCaseValue;
    }

    private static void ApplyOpenedValue(GameObject rootObject, int openedCaseValue)
    {
        var targetValue = OpenedCaseValueFallback(openedCaseValue);

        if (rootObject.TryGetComponent<ValuableObject>(out var valuableObject))
        {
            valuableObject.dollarValueCurrent = targetValue;
            valuableObject.dollarValueOriginal = targetValue;
            valuableObject.dollarValueOverride = targetValue;
            valuableObject.dollarValueSet = true;
        }

        if (rootObject.TryGetComponent<ItemAttributes>(out var itemAttributes))
        {
            SetMemberValue(itemAttributes, "value", targetValue);
        }
    }

    private static void ApplyImmediateReward(GameObject rootObject, int immediateReward)
    {
        if (immediateReward <= 0)
        {
            return;
        }

        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        AirDropCaseHaulRewardService.AddPendingReward(immediateReward);

        var physGrabObject = rootObject.GetComponent<PhysGrabObject>();
        if (physGrabObject != null && WorldSpaceUIValue.instance != null)
        {
            WorldSpaceUIValue.instance.Show(physGrabObject, immediateReward, _cost: false, Vector3.up * 0.12f);
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"Air drop case opened: RuntimeName={rootObject.name} | ImmediateReward={immediateReward} | RemainingValue={OpenedCaseValue} | OriginalValue={ClosedCaseValue} | PendingHaulReward={AirDropCaseHaulRewardService.PendingHaulReward}");

        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"航空箱已开启：完整价值 {ClosedCaseValue}，即时收益 {immediateReward}，剩余价值 {OpenedCaseValue}");
    }

    internal static bool CanOpenCase(GameObject rootObject)
    {
        if (!rootObject.TryGetComponent<ValuableObject>(out var valuableObject))
        {
            return true;
        }

        return valuableObject.dollarValueCurrent >= ClosedCaseValue;
    }

    private static Transform? FindVisualTransform(Transform root, string targetName)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (transform != null && string.Equals(transform.name, targetName, StringComparison.Ordinal))
            {
                return transform;
            }
        }

        return null;
    }

    private static Transform? FindOpenedVisualTemplate(Transform prefabRoot)
    {
        var objectRoot = prefabRoot.Find("Object");
        if (objectRoot == null)
        {
            return null;
        }

        return FindVisualTransform(objectRoot, OpenVisualName)
            ?? FindVisualTransform(objectRoot, ClosedVisualName);
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

    private static void SetMemberValue(object instance, string memberName, int value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field is not null && field.FieldType == typeof(int))
        {
            field.SetValue(instance, value);
            return;
        }

        var property = type.GetProperty(memberName, Flags);
        if (property is not null && property.PropertyType == typeof(int) && property.CanWrite)
        {
            property.SetValue(instance, value);
        }
    }
}
