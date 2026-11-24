using ABI_RC.Core;
using ABI_RC.Core.Base;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core.Networking.GameServer;
using ABI_RC.Core.Player;
using ABI_RC.Systems.ContentClones;
using ABI.CCK.Components;
using GameCommon.Packets.Garrison.GameServer;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Kafe.MiniMePlusPlus;

public class MiniMePlusPlus : MelonMod
{
    private static Vector3 _playSpaceLocalPos;
    private static Quaternion _playSpaceLocalRot = Quaternion.identity;
    private static bool _hasPlaySpaceOffset;

    public override void OnInitializeMelon()
    {
        ModConfig.InitializeMelonPrefs();

        Networking.Initialize();
        RemoteMiniMe.Initialize();

        Networking.Show += RemoteMiniMe.ShowFromNetwork;
        Networking.Hide += RemoteMiniMe.Hide;

        GSInfoHandler.OnGSInfoUpdate += OnGSInfoUpdate;
        CVRFrameDriver.OnPostLateUpdate += UpdatePlaySpaceFollow;
    }

    /// <summary>
    /// Make the MiniMe sync data updates respect the server tick rate updates
    /// </summary>
    private static void OnGSInfoUpdate(ServerInfoPacket update)
    {
        try
        {
            var newInvokeRate = 1f / update.TickRate;
            RemoteMiniMe.NetworkInterval = newInvokeRate;
            Networking.UpdateTickRate(newInvokeRate);
            MelonLogger.Msg($"Updated the MiniMe sending data rate to {newInvokeRate}Hz");
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Error during {nameof(OnGSInfoUpdate)}", e);
        }
    }

    private static float GetPlaySpaceScale()
    {
        return PlayerSetup.Instance != null
            ? Mathf.Max(PlayerSetup.Instance._avatarScaleRelation, 0.01f)
            : 1f;
    }

    private static void SavePlaySpaceOffset()
    {
        if (!MiniMe._cloneHolderObject || PlayerSetup.Instance == null)
            return;

        var transform = MiniMe._cloneHolderObject.transform;
        var playerPos = PlayerSetup.Instance.GetPlayerPosition();
        var playerRot = PlayerSetup.Instance.GetPlayerRotation();
        var scale = GetPlaySpaceScale();

        _playSpaceLocalPos = Quaternion.Inverse(playerRot) * (transform.position - playerPos) / scale;
        _playSpaceLocalRot = Quaternion.Inverse(playerRot) * transform.rotation;
        _hasPlaySpaceOffset = true;
    }

    private static void UpdatePlaySpaceFollow()
    {
        if (!MiniMe._cloneHolderObject || !MiniMe._cloneHolderObject.activeSelf
            || MiniMe._localPlayerClone == null || MiniMe._localPlayerClone.IsDestroyed
            || MiniMe._currentAnchorMode != MiniMe.AnchorMode.PlaySpacePickup
            || PlayerSetup.Instance == null)
            return;

        var transform = MiniMe._cloneHolderObject.transform;
        var scale = GetPlaySpaceScale();

        if (MiniMe._cloneHolderObject.TryGetComponent<CVRPickupObject>(out var pickup) && pickup.IsGrabbedByMe)
        {
            SavePlaySpaceOffset();
            transform.localScale = Vector3.one * (ModConfig.MeMiniMeScaleMultiplier.Value * scale);
            return;
        }

        if (!_hasPlaySpaceOffset)
            SavePlaySpaceOffset();

        var playerPos = PlayerSetup.Instance.GetPlayerPosition();
        var playerRot = PlayerSetup.Instance.GetPlayerRotation();

        transform.SetPositionAndRotation(
            playerPos + playerRot * (_playSpaceLocalPos * scale),
            playerRot * _playSpaceLocalRot);
        transform.localScale = Vector3.one * (ModConfig.MeMiniMeScaleMultiplier.Value * scale);
    }

    [HarmonyPatch]
    public class HarmonyPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MiniMe), nameof(MiniMe.Initialize))]
        private static void After_MiniMe_Initialize()
        {
            try
            {
                // ModConfig.InitializeUILibMenu(); Init after self portrait
                Networking.MiniMeInitialize();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_MiniMe_Initialize)} Patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CVR_MenuManager), nameof(CVR_MenuManager.UILibInit))]
        private static void After_CVR_MenuManager_UILibInit()
        {
            try
            {
                ModConfig.InitializeUILibMenu();
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_CVR_MenuManager_UILibInit)} Patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MiniMe), nameof(MiniMe.UpdateAnchorMode))]
        private static void After_MiniMe_UpdateAnchorMode()
        {
            try
            {
                if (!MiniMe._cloneHolderObject || MiniMe._localPlayerClone == null || MiniMe._localPlayerClone.IsDestroyed)
                    return;

                Transform transform = MiniMe._cloneHolderObject.transform;
                CapsuleCollider collider = MiniMe._cloneHolderObject.GetComponent<CapsuleCollider>();
                switch (MiniMe._currentAnchorMode)
                {
                    case MiniMe.AnchorMode.QuickMenu:
                        collider.enabled = false;
                        break;
                    case MiniMe.AnchorMode.PlaySpacePickup:
                        collider.enabled = true;
                        transform.SetParent(null, worldPositionStays: true);
                        SavePlaySpaceOffset();
                        var localScalePlaySpace = Vector3.one * (ModConfig.MeMiniMeScaleMultiplier.Value * GetPlaySpaceScale());
                        if (localScalePlaySpace != transform.localScale)
                            transform.localScale = localScalePlaySpace;
                        break;
                    case MiniMe.AnchorMode.WorldSpacePickup:
                        collider.enabled = true;
                        var localScaleWorldSpace = Vector3.one * ModConfig.MeMiniMeScaleMultiplier.Value;
                        if (localScaleWorldSpace != transform.localScale)
                        {
                            // MelonLogger.Msg($"Changed WorldSpace MiniMe local scale to: {localScaleWorldSpace:F2}");
                            transform.localScale = localScaleWorldSpace;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_MiniMe_UpdateAnchorMode)} Patch", e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MiniMe), nameof(MiniMe.TrySetupClone))]
        private static void After_MiniMe_TrySetupClone()
        {
            try
            {
                if (!MiniMe._cloneHolderObject || MiniMe._localPlayerClone == null || MiniMe._localPlayerClone.IsDestroyed)
                    return;

                if (!MiniMe._cloneHolderObject.TryGetComponent<CVRPickupObject>(out var cvrPickupObject))
                    return;

                // Add event handles for grabbing and dropping
                cvrPickupObject.onGrab.AddListener(_ => Networking.SendUpdate());
                cvrPickupObject.onDrop.AddListener(_ =>
                {
                    if (MiniMe._currentAnchorMode == MiniMe.AnchorMode.PlaySpacePickup)
                        SavePlaySpaceOffset();
                    Networking.SendUpdate();
                });
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error executing {nameof(After_MiniMe_TrySetupClone)} Patch", e);
            }
        }
    }
}
