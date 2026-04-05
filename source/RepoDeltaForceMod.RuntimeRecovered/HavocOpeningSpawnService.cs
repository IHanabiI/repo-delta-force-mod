using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RepoDeltaForceMod;

internal static class HavocOpeningSpawnService
{
    private const string RuntimeSoftReloadSubsystemName = "havoc-opening-spawn";
    private const float SameSpotToleranceMeters = 0.05f;
    private const float MilitaryTerminalClearanceRadius = 0.35f;
    private const float FlightRecorderClearanceRadius = 0.45f;
    private const float AirDropCaseClearanceRadius = 0.85f;

    private static readonly Dictionary<OpeningHavocSupplyType, ValuableVolume.Type[]> AllowedVolumeTypes =
        new()
        {
            { OpeningHavocSupplyType.MilitaryTerminal, new[] { ValuableVolume.Type.Medium, ValuableVolume.Type.Small } },
            { OpeningHavocSupplyType.FlightRecorder, new[] { ValuableVolume.Type.Small, ValuableVolume.Type.Medium, ValuableVolume.Type.Big } },
            { OpeningHavocSupplyType.AirDropCase, new[] { ValuableVolume.Type.Wide, ValuableVolume.Type.Big, ValuableVolume.Type.Tall } },
        };

    private static readonly OpeningHavocSupplyType[] ReservationOrder =
    {
        OpeningHavocSupplyType.AirDropCase,
        OpeningHavocSupplyType.FlightRecorder,
        OpeningHavocSupplyType.MilitaryTerminal,
    };

    private static int _reservedSceneHandle = -1;
    private static int _spawnedSceneHandle = -1;
    private static bool _planBuiltForScene;
    private static readonly List<ReservedSupplySpawn> ReservedSpawns = new();
    private static readonly HashSet<int> ReservedVolumeIds = new();

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _reservedSceneHandle = -1;
        _spawnedSceneHandle = -1;
        _planBuiltForScene = false;
        ReservedSpawns.Clear();
        ReservedVolumeIds.Clear();

        RepoDeltaForceMod.Logger.LogInfo(
            $"havoc opening spawn state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static bool IsReservedVolume(ValuableVolume? volume)
    {
        return volume != null && ReservedVolumeIds.Contains(volume.GetInstanceID());
    }

    internal static void EnsureReservationPlanReady()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        if (!OpeningHavocEventService.TryGetSelectedSupplyTypes(out var selectedSupplyTypes) || selectedSupplyTypes.Count == 0)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        if (_planBuiltForScene && _reservedSceneHandle == scene.handle)
        {
            return;
        }

        _reservedSceneHandle = scene.handle;
        _planBuiltForScene = true;
        ReservedSpawns.Clear();
        ReservedVolumeIds.Clear();

        var allVolumes = UnityEngine.Object.FindObjectsByType<ValuableVolume>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None)
            .Where(IsVolumeUsable)
            .ToList();
        if (allVolumes.Count == 0)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"Havoc opening spawn could not find any ValuableVolume entries in scene '{scene.name}'.");
            return;
        }

        var playerPosition = ResolveReferencePosition();
        var reservedPositions = new List<Vector3>();
        var selectedSet = new HashSet<OpeningHavocSupplyType>(selectedSupplyTypes);

        foreach (var supplyType in ReservationOrder)
        {
            if (!selectedSet.Contains(supplyType))
            {
                continue;
            }

            if (!TryReserveNearestVolumeForSupply(supplyType, allVolumes, reservedPositions, playerPosition, out var reservedSpawn))
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Havoc opening spawn could not reserve a legal volume for {supplyType} in scene '{scene.name}'.");
                continue;
            }

            ReservedSpawns.Add(reservedSpawn);
            ReservedVolumeIds.Add(reservedSpawn.VolumeInstanceId);
            reservedPositions.Add(reservedSpawn.Position);

            RepoDeltaForceMod.Logger.LogInfo(
                $"Havoc opening spawn reserved volume: Supply={supplyType} | Scene={scene.name} | VolumeType={reservedSpawn.VolumeType} | Position={reservedSpawn.Position}");
        }

        if (ReservedSpawns.Count > 0)
        {
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                $"哈夫克物资已预占正式点位：{string.Join(", ", ReservedSpawns.Select(x => $"{x.SupplyType}@{x.VolumeType}"))}");
        }
    }

    internal static void TrySpawnReservedSupplies()
    {
        if (!SemiFunc.IsMasterClientOrSingleplayer())
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        if (_spawnedSceneHandle == scene.handle)
        {
            return;
        }

        if (ReservedSpawns.Count == 0)
        {
            _spawnedSceneHandle = scene.handle;
            return;
        }

        var spawnedSupplyLabels = new List<string>();
        var occupiedPositions = new List<Vector3>();
        foreach (var reservedSpawn in ReservedSpawns)
        {
            if (!IsSpawnPositionClear(reservedSpawn.Position, reservedSpawn.SupplyType, occupiedPositions))
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Havoc opening spawn skipped for {reservedSpawn.SupplyType} in scene '{scene.name}' because the reserved position is now occupied. Position={reservedSpawn.Position}");
                continue;
            }

            if (!TrySpawnReservedSupply(reservedSpawn, out var spawnedObject, out var failureReason))
            {
                RepoDeltaForceMod.Logger.LogWarning(
                    $"Havoc opening spawn failed for {reservedSpawn.SupplyType} in scene '{scene.name}'. {failureReason}");
                continue;
            }

            occupiedPositions.Add(reservedSpawn.Position);
            spawnedSupplyLabels.Add($"{reservedSpawn.SupplyType}@{reservedSpawn.VolumeType}");
            RepoDeltaForceMod.Logger.LogInfo(
                $"Havoc opening spawn succeeded: Supply={reservedSpawn.SupplyType} | Scene={scene.name} | VolumeType={reservedSpawn.VolumeType} | Position={reservedSpawn.Position} | RuntimeName={spawnedObject.name}");
        }

        _spawnedSceneHandle = scene.handle;

        if (spawnedSupplyLabels.Count > 0)
        {
            RuntimeSoftReloadManager.MarkSubsystemDirty(
                RuntimeSoftReloadSubsystemName,
                $"哈夫克物资已按正式点位池生成：{string.Join(", ", spawnedSupplyLabels)}");
        }
    }

    private static bool TryReserveNearestVolumeForSupply(
        OpeningHavocSupplyType supplyType,
        IReadOnlyList<ValuableVolume> allVolumes,
        IReadOnlyList<Vector3> reservedPositions,
        Vector3 playerPosition,
        out ReservedSupplySpawn reservedSpawn)
    {
        reservedSpawn = default;

        if (!AllowedVolumeTypes.TryGetValue(supplyType, out var allowedTypes))
        {
            return false;
        }

        foreach (var volumeType in allowedTypes)
        {
            var candidate = allVolumes
                .Where(volume => volume.VolumeType == volumeType)
                .OrderBy(volume => Vector3.Distance(playerPosition, volume.transform.position))
                .FirstOrDefault(volume => IsVolumeLegalForReservation(volume, supplyType, reservedPositions));

            if (candidate == null)
            {
                continue;
            }

            reservedSpawn = new ReservedSupplySpawn(
                supplyType,
                candidate.GetInstanceID(),
                candidate.VolumeType,
                candidate.transform.position,
                candidate.transform.rotation);
            return true;
        }

        return false;
    }

    private static bool TrySpawnReservedSupply(
        ReservedSupplySpawn reservedSpawn,
        out GameObject spawnedObject,
        out string failureReason)
    {
        spawnedObject = null!;
        failureReason = string.Empty;

        switch (reservedSpawn.SupplyType)
        {
            case OpeningHavocSupplyType.MilitaryTerminal:
                return MilitaryTerminalAutoSpawnService.TrySpawnIntoWorld(
                    reservedSpawn.Position,
                    reservedSpawn.Rotation,
                    out spawnedObject,
                    out failureReason);
            case OpeningHavocSupplyType.FlightRecorder:
                return FlightRecorderAutoSpawnService.TrySpawnIntoWorld(
                    reservedSpawn.Position,
                    reservedSpawn.Rotation,
                    out spawnedObject,
                    out failureReason);
            case OpeningHavocSupplyType.AirDropCase:
                return AirDropCaseAutoSpawnService.TrySpawnIntoWorld(
                    reservedSpawn.Position,
                    reservedSpawn.Rotation,
                    out spawnedObject,
                    out failureReason);
            default:
                failureReason = $"Unsupported supply type '{reservedSpawn.SupplyType}'.";
                return false;
        }
    }

    private static bool IsVolumeUsable(ValuableVolume volume)
    {
        return volume != null
            && volume.isActiveAndEnabled
            && volume.gameObject.activeInHierarchy;
    }

    private static bool IsVolumeLegalForReservation(
        ValuableVolume volume,
        OpeningHavocSupplyType supplyType,
        IReadOnlyList<Vector3> reservedPositions)
    {
        var position = volume.transform.position;

        if (supplyType == OpeningHavocSupplyType.MilitaryTerminal)
        {
            foreach (var reservedPosition in reservedPositions)
            {
                if (Vector3.Distance(position, reservedPosition) < SameSpotToleranceMeters)
                {
                    return false;
                }
            }

            return true;
        }

        foreach (var reservedPosition in reservedPositions)
        {
            if (Vector3.Distance(position, reservedPosition) < GetClearanceRadius(supplyType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSpawnPositionClear(
        Vector3 position,
        OpeningHavocSupplyType supplyType,
        IReadOnlyList<Vector3> occupiedPositions)
    {
        foreach (var occupiedPosition in occupiedPositions)
        {
            if (Vector3.Distance(position, occupiedPosition) < SameSpotToleranceMeters)
            {
                return false;
            }
        }

        var clearance = GetClearanceRadius(supplyType);
        var overlaps = Physics.OverlapSphere(position, clearance, ~0, QueryTriggerInteraction.Ignore);
        foreach (var overlap in overlaps)
        {
            if (overlap == null || overlap.isTrigger)
            {
                continue;
            }

            if (overlap.GetComponentInParent<ValuableObject>() != null)
            {
                return false;
            }
        }

        return true;
    }

    private static Vector3 ResolveReferencePosition()
    {
        if (PlayerController.instance != null)
        {
            return PlayerController.instance.transform.position;
        }

        if (Camera.main != null)
        {
            return Camera.main.transform.position;
        }

        if (PhysGrabber.instance != null)
        {
            return PhysGrabber.instance.transform.position;
        }

        return Vector3.zero;
    }

    private static float GetClearanceRadius(OpeningHavocSupplyType supplyType)
    {
        return supplyType switch
        {
            OpeningHavocSupplyType.MilitaryTerminal => MilitaryTerminalClearanceRadius,
            OpeningHavocSupplyType.FlightRecorder => FlightRecorderClearanceRadius,
            OpeningHavocSupplyType.AirDropCase => AirDropCaseClearanceRadius,
            _ => 0.5f,
        };
    }

    private readonly struct ReservedSupplySpawn
    {
        internal ReservedSupplySpawn(
            OpeningHavocSupplyType supplyType,
            int volumeInstanceId,
            ValuableVolume.Type volumeType,
            Vector3 position,
            Quaternion rotation)
        {
            SupplyType = supplyType;
            VolumeInstanceId = volumeInstanceId;
            VolumeType = volumeType;
            Position = position;
            Rotation = rotation;
        }

        internal OpeningHavocSupplyType SupplyType { get; }
        internal int VolumeInstanceId { get; }
        internal ValuableVolume.Type VolumeType { get; }
        internal Vector3 Position { get; }
        internal Quaternion Rotation { get; }
    }
}

[HarmonyPatch(typeof(ValuableDirector), "Spawn")]
internal static class HavocOpeningSpawnSkipReservedVolumePatch
{
    private static bool Prefix(ValuableVolume _volume)
    {
        OpeningHavocEventService.EnsureSelectionPlanReady();
        HavocOpeningSpawnService.EnsureReservationPlanReady();

        if (!HavocOpeningSpawnService.IsReservedVolume(_volume))
        {
            return true;
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"Havoc opening spawn reserved volume skipped by vanilla valuable spawn: VolumeType={_volume.VolumeType} | Position={_volume.transform.position}");
        return false;
    }
}

[HarmonyPatch(typeof(ValuableDirector), "VolumesAndSwitchSetupRPC")]
internal static class HavocOpeningSpawnPatch
{
    private static void Postfix()
    {
        HavocOpeningSpawnService.TrySpawnReservedSupplies();
    }
}
