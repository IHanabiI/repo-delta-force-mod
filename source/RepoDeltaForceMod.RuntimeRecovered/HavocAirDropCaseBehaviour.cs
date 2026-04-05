using UnityEngine;
using Photon.Pun;
using System;

namespace RepoDeltaForceMod;

[DisallowMultipleComponent]
public sealed class HavocAirDropCaseBehaviour : MonoBehaviour
{
    private const string DefaultDisplayName = "航空箱";

    [SerializeField]
    private string debugDisplayName = DefaultDisplayName;

    [SerializeField]
    private bool logAuthoringWarnings = true;

    [SerializeField]
    private bool opened;

    [NonSerialized]
    private bool closedValueInitialized;

    private void Reset()
    {
        debugDisplayName = DefaultDisplayName;
    }

    internal bool IsOpened => opened;

    internal bool ClosedValueInitialized
    {
        get => closedValueInitialized;
        set => closedValueInitialized = value;
    }

    private void Awake()
    {
        if (!logAuthoringWarnings)
        {
            return;
        }

        var identity = GetComponent<HavocSupplyIdentity>();
        if (identity is null)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"HavocAirDropCaseBehaviour is missing HavocSupplyIdentity on '{name}'. Official air drop case detection may fall back to name matching only.");
            return;
        }

        if (identity.StableId != AirDropCaseIdentity.StableId)
        {
            RepoDeltaForceMod.Logger.LogWarning(
                $"HavocAirDropCaseBehaviour on '{name}' has unexpected StableId '{identity.StableId}'. Expected '{AirDropCaseIdentity.StableId}'.");
            return;
        }

        RepoDeltaForceMod.Logger.LogInfo(
            $"HavocAirDropCaseBehaviour ready: Name={name} | DisplayName={GetReadableDisplayName()} | StableId={identity.StableId}");
    }

    private void Update()
    {
        if (opened)
        {
            return;
        }

        if (!IsHeldByLocalPlayer())
        {
            return;
        }

        if (PlayerController.instance != null && PlayerController.instance.InputDisableTimer > 0f)
        {
            return;
        }

        if (!SemiFunc.InputDown(InputKey.Interact))
        {
            return;
        }

        AirDropCaseOpenService.RequestOpen(this);
    }

    [PunRPC]
    public void RequestOpenOnMasterRpc(int immediateReward, int openedCaseValue, PhotonMessageInfo info = default)
    {
        if (!SemiFunc.MasterOnlyRPC(info) || !PhotonNetwork.IsMasterClient || opened || !AirDropCaseOpenService.CanOpenCase(gameObject))
        {
            return;
        }

        var photonView = GetComponent<PhotonView>();
        if (photonView == null)
        {
            OpenCaseLocally(immediateReward, openedCaseValue);
            return;
        }

        photonView.RPC(nameof(OpenCaseRpc), RpcTarget.All, immediateReward, openedCaseValue);
    }

    [PunRPC]
    public void OpenCaseRpc(int immediateReward, int openedCaseValue)
    {
        OpenCaseLocally(immediateReward, openedCaseValue);
    }

    internal void OpenCaseLocally(int immediateReward, int openedCaseValue)
    {
        if (opened || !AirDropCaseOpenService.CanOpenCase(gameObject))
        {
            return;
        }

        opened = true;
        AirDropCaseOpenService.ApplyOpenedState(gameObject, immediateReward, openedCaseValue);
    }

    private bool IsHeldByLocalPlayer()
    {
        var physGrabObject = GetComponent<PhysGrabObject>();
        if (physGrabObject == null)
        {
            return false;
        }

        return physGrabObject.heldByLocalPlayer
            || (PhysGrabber.instance != null && PhysGrabber.instance.grabbedPhysGrabObject == physGrabObject);
    }

    private string GetReadableDisplayName()
    {
        return string.IsNullOrWhiteSpace(debugDisplayName)
            ? DefaultDisplayName
            : debugDisplayName;
    }
}
