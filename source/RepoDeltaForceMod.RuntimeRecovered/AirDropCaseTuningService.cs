using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace RepoDeltaForceMod;

internal static class AirDropCaseTuningService
{
    private const string ClosedDisplayName = "Havoc AirDrop Case";
    private const string OpenedDisplayName = "Havoc AirDrop Case Opened";
    private const string ClosedVisualName = "Mesh";
    private const string OpenVisualName = "MeshOpen";

    private const float ClosedCaseMass = 4f;
    private const float ClosedCaseAngularDrag = 1.4f;
    private const float FragilityValue = 65f;
    private const float DurabilityValue = 90f;
    private const float FragilityMultiplier = 1.25f;
    private const float ImpactFragilityMultiplier = 1.2f;
    private const float ImpactThresholdLight = 220f;
    private const float ImpactThresholdMedium = 320f;
    private const float ImpactThresholdHeavy = 430f;

    private static readonly Dictionary<int, ClosedGeometrySnapshot> ClosedGeometrySnapshots = new();

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        ClosedGeometrySnapshots.Clear();

        RepoDeltaForceMod.Logger.LogInfo(
            $"air drop case tuning state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Apply(GameObject rootObject, bool opened)
    {
        if (rootObject == null)
        {
            return;
        }

        ApplyDisplayNames(rootObject, opened);
        ApplyPhysics(rootObject);
        ApplyFragility(rootObject);

        if (TryGetOrCaptureClosedGeometry(rootObject, out var snapshot))
        {
            ApplyClosedGeometryBaseline(rootObject, snapshot, opened);
        }
    }

    private static void ApplyDisplayNames(GameObject rootObject, bool opened)
    {
        var displayName = opened ? OpenedDisplayName : ClosedDisplayName;
        var runtimeName = opened ? AirDropCaseIdentity.OpenPrefabRootName : AirDropCaseIdentity.PrefabRootName;

        rootObject.name = runtimeName;

        var identity = rootObject.GetComponent<HavocSupplyIdentity>();
        if (identity != null)
        {
            SetStringMember(identity, "stableId", AirDropCaseIdentity.StableId);
            SetStringMember(identity, "displayName", displayName);
            SetStringMember(identity, "prefabRootName", runtimeName);
        }

        if (rootObject.TryGetComponent<ItemAttributes>(out var itemAttributes))
        {
            SetStringMember(itemAttributes, "itemName", displayName);
            SetStringMember(itemAttributes, "instanceName", runtimeName);
        }
    }

    private static void ApplyPhysics(GameObject rootObject)
    {
        if (!rootObject.TryGetComponent<Rigidbody>(out var rigidbody))
        {
            return;
        }

        rigidbody.mass = ClosedCaseMass;
        rigidbody.angularDrag = ClosedCaseAngularDrag;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private static void ApplyFragility(GameObject rootObject)
    {
        foreach (var component in rootObject.GetComponents<Component>())
        {
            if (component == null)
            {
                continue;
            }

            SetFloatMember(component, "fragility", FragilityValue);
            SetFloatMember(component, "durability", DurabilityValue);
            SetFloatMember(component, "fragilityMultiplier", FragilityMultiplier);
            SetFloatMember(component, "impactFragilityMultiplier", ImpactFragilityMultiplier);
            SetFloatMember(component, "impactLevel1", ImpactThresholdLight);
            SetFloatMember(component, "impactLevel2", ImpactThresholdMedium);
            SetFloatMember(component, "impactLevel3", ImpactThresholdHeavy);
        }
    }

    private static bool TryGetOrCaptureClosedGeometry(GameObject rootObject, out ClosedGeometrySnapshot snapshot)
    {
        var instanceId = rootObject.GetInstanceID();
        if (ClosedGeometrySnapshots.TryGetValue(instanceId, out snapshot))
        {
            return true;
        }

        if (TryCaptureClosedGeometry(rootObject, out snapshot))
        {
            ClosedGeometrySnapshots[instanceId] = snapshot;
            return true;
        }

        return false;
    }

    private static bool TryCaptureClosedGeometry(GameObject rootObject, out ClosedGeometrySnapshot snapshot)
    {
        snapshot = default;

        var objectRoot = rootObject.transform.Find("Object");
        if (objectRoot == null)
        {
            return false;
        }

        var closedVisual = FindDirectChild(objectRoot, ClosedVisualName)
            ?? FindDirectChild(objectRoot, OpenVisualName);
        if (closedVisual == null)
        {
            return false;
        }

        var colliderTransform = FindDirectChild(objectRoot, "Valuable Collider")
            ?? FindNamedTransform(objectRoot, "Valuable Collider");
        var boxCollider = colliderTransform != null
            ? colliderTransform.GetComponent<BoxCollider>()
            : null;
        var roomVolumeCheck = rootObject.GetComponent("RoomVolumeCheck");

        snapshot = new ClosedGeometrySnapshot(
            TransformSnapshot.From(closedVisual),
            TransformSnapshot.TryFrom(FindDirectChild(objectRoot, OpenVisualName), out var openedVisualSnapshot)
                ? openedVisualSnapshot
                : null,
            ColliderSnapshot.TryFrom(colliderTransform, boxCollider, out var colliderSnapshot)
                ? colliderSnapshot
                : null,
            RoomVolumeSnapshot.TryFrom(roomVolumeCheck, out var roomVolumeSnapshot)
                ? roomVolumeSnapshot
                : null);
        return true;
    }

    private static void ApplyClosedGeometryBaseline(GameObject rootObject, ClosedGeometrySnapshot snapshot, bool opened)
    {
        var objectRoot = rootObject.transform.Find("Object");
        if (objectRoot == null)
        {
            return;
        }

        var closedVisual = FindDirectChild(objectRoot, ClosedVisualName);
        if (closedVisual != null)
        {
            snapshot.ClosedVisual.ApplyTo(closedVisual);
        }

        if (opened)
        {
            var openedVisual = FindDirectChild(objectRoot, OpenVisualName);
            if (openedVisual != null)
            {
                (snapshot.OpenedVisual ?? snapshot.ClosedVisual).ApplyTo(openedVisual);
            }
        }

        if (snapshot.Collider is { } colliderSnapshot)
        {
            var colliderTransform = FindDirectChild(objectRoot, "Valuable Collider")
                ?? FindNamedTransform(objectRoot, "Valuable Collider");
            if (colliderTransform == null)
            {
                colliderTransform = new GameObject("Valuable Collider").transform;
                colliderTransform.SetParent(objectRoot, worldPositionStays: false);
            }

            var boxCollider = colliderTransform.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = colliderTransform.gameObject.AddComponent<BoxCollider>();
            }

            colliderSnapshot.ApplyTo(colliderTransform, boxCollider);
        }

        if (snapshot.RoomVolume is { } roomVolumeSnapshot)
        {
            var roomVolumeCheck = rootObject.GetComponent("RoomVolumeCheck");
            roomVolumeSnapshot.ApplyTo(roomVolumeCheck);
        }
    }

    private static Transform? FindDirectChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    private static Transform? FindNamedTransform(Transform root, string targetName)
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

    private static void SetStringMember(object instance, string memberName, string value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field != null && field.FieldType == typeof(string))
        {
            field.SetValue(instance, value);
            return;
        }

        var property = type.GetProperty(memberName, Flags);
        if (property != null && property.PropertyType == typeof(string) && property.CanWrite)
        {
            property.SetValue(instance, value);
        }
    }

    private static void SetFloatMember(object instance, string memberName, float value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field != null)
        {
            if (field.FieldType == typeof(float))
            {
                field.SetValue(instance, value);
                return;
            }

            if (field.FieldType == typeof(int))
            {
                field.SetValue(instance, Mathf.RoundToInt(value));
                return;
            }
        }

        var property = type.GetProperty(memberName, Flags);
        if (property == null || !property.CanWrite)
        {
            return;
        }

        if (property.PropertyType == typeof(float))
        {
            property.SetValue(instance, value);
        }
        else if (property.PropertyType == typeof(int))
        {
            property.SetValue(instance, Mathf.RoundToInt(value));
        }
    }

    private static void SetVector3Member(object instance, string memberName, Vector3 value)
    {
        var type = instance.GetType();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var field = type.GetField(memberName, Flags);
        if (field != null && field.FieldType == typeof(Vector3))
        {
            field.SetValue(instance, value);
            return;
        }

        var property = type.GetProperty(memberName, Flags);
        if (property != null && property.PropertyType == typeof(Vector3) && property.CanWrite)
        {
            property.SetValue(instance, value);
        }
    }

    private readonly struct ClosedGeometrySnapshot
    {
        internal ClosedGeometrySnapshot(
            TransformSnapshot closedVisual,
            TransformSnapshot? openedVisual,
            ColliderSnapshot? collider,
            RoomVolumeSnapshot? roomVolume)
        {
            ClosedVisual = closedVisual;
            OpenedVisual = openedVisual;
            Collider = collider;
            RoomVolume = roomVolume;
        }

        internal TransformSnapshot ClosedVisual { get; }

        internal TransformSnapshot? OpenedVisual { get; }

        internal ColliderSnapshot? Collider { get; }

        internal RoomVolumeSnapshot? RoomVolume { get; }
    }

    private readonly struct TransformSnapshot
    {
        private TransformSnapshot(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        internal Vector3 LocalPosition { get; }

        internal Quaternion LocalRotation { get; }

        internal Vector3 LocalScale { get; }

        internal static TransformSnapshot From(Transform transform)
        {
            return new TransformSnapshot(
                transform.localPosition,
                transform.localRotation,
                transform.localScale);
        }

        internal static bool TryFrom(Transform? transform, out TransformSnapshot snapshot)
        {
            snapshot = default;
            if (transform == null)
            {
                return false;
            }

            snapshot = From(transform);
            return true;
        }

        internal void ApplyTo(Transform transform)
        {
            transform.localPosition = LocalPosition;
            transform.localRotation = LocalRotation;
            transform.localScale = LocalScale;
        }
    }

    private readonly struct ColliderSnapshot
    {
        private ColliderSnapshot(
            TransformSnapshot transform,
            Vector3 center,
            Vector3 size)
        {
            Transform = transform;
            Center = center;
            Size = size;
        }

        internal TransformSnapshot Transform { get; }

        internal Vector3 Center { get; }

        internal Vector3 Size { get; }

        internal static bool TryFrom(Transform? transform, BoxCollider? collider, out ColliderSnapshot snapshot)
        {
            snapshot = default;
            if (transform == null || collider == null)
            {
                return false;
            }

            snapshot = new ColliderSnapshot(
                TransformSnapshot.From(transform),
                collider.center,
                collider.size);
            return true;
        }

        internal void ApplyTo(Transform transform, BoxCollider collider)
        {
            Transform.ApplyTo(transform);
            collider.center = Center;
            collider.size = Size;
        }
    }

    private readonly struct RoomVolumeSnapshot
    {
        private RoomVolumeSnapshot(Vector3 checkPosition, Vector3 currentSize)
        {
            CheckPosition = checkPosition;
            CurrentSize = currentSize;
        }

        internal Vector3 CheckPosition { get; }

        internal Vector3 CurrentSize { get; }

        internal static bool TryFrom(Component? roomVolumeCheck, out RoomVolumeSnapshot snapshot)
        {
            snapshot = default;
            if (roomVolumeCheck == null)
            {
                return false;
            }

            var checkPositionValue = ObservationReflection.TryGetKnownValue(roomVolumeCheck, "CheckPosition");
            var currentSizeValue = ObservationReflection.TryGetKnownValue(roomVolumeCheck, "currentSize");
            if (checkPositionValue is not Vector3 checkPosition || currentSizeValue is not Vector3 currentSize)
            {
                return false;
            }

            snapshot = new RoomVolumeSnapshot(checkPosition, currentSize);
            return true;
        }

        internal void ApplyTo(Component? roomVolumeCheck)
        {
            if (roomVolumeCheck == null)
            {
                return;
            }

            SetVector3Member(roomVolumeCheck, "CheckPosition", CheckPosition);
            SetVector3Member(roomVolumeCheck, "currentSize", CurrentSize);
        }
    }
}
