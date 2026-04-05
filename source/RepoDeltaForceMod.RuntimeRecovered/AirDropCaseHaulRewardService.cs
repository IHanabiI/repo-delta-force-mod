using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RepoDeltaForceMod;

internal static class AirDropCaseHaulRewardService
{
    private const string RuntimeSoftReloadSubsystemName = "air-drop-case-haul-reward";

    private static int _pendingHaulReward;
    private static int _activeSceneHandle = -1;

    internal static int PendingHaulReward => _pendingHaulReward;

    internal static void ResetRuntimeState(RuntimeSoftReloadContext context)
    {
        _pendingHaulReward = 0;
        _activeSceneHandle = -1;

        RepoDeltaForceMod.Logger.LogInfo(
            $"air drop case haul reward state reset for soft reload #{context.Generation}: Reason={context.Reason}");
    }

    internal static void Tick()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        if (_activeSceneHandle != scene.handle)
        {
            _activeSceneHandle = scene.handle;
            if (_pendingHaulReward != 0)
            {
                _pendingHaulReward = 0;
                RuntimeSoftReloadManager.MarkSubsystemDirty(
                    RuntimeSoftReloadSubsystemName,
                    $"场景切换后已清空航空箱待计入 haul 奖励");
            }
        }
    }

    internal static void AddPendingReward(int value)
    {
        if (value <= 0)
        {
            return;
        }

        _pendingHaulReward += value;
        RuntimeSoftReloadManager.MarkSubsystemDirty(
            RuntimeSoftReloadSubsystemName,
            $"航空箱开箱奖励已加入待计入 haul：+{value}，当前累计 {_pendingHaulReward}");
    }

    internal static void ApplyToRoundDirector(RoundDirector roundDirector)
    {
        if (roundDirector == null || _pendingHaulReward <= 0)
        {
            return;
        }

        roundDirector.currentHaul += _pendingHaulReward;
    }
}

[HarmonyPatch(typeof(RoundDirector), "Update")]
internal static class AirDropCaseRoundDirectorUpdatePatch
{
    private static void Postfix(RoundDirector __instance)
    {
        AirDropCaseHaulRewardService.ApplyToRoundDirector(__instance);
    }
}

[HarmonyPatch(typeof(RoundDirector), nameof(RoundDirector.HaulCheck))]
internal static class AirDropCaseRoundDirectorHaulCheckPatch
{
    private static void Postfix(RoundDirector __instance)
    {
        AirDropCaseHaulRewardService.ApplyToRoundDirector(__instance);
    }
}
