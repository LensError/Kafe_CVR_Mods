using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.ContentClones;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using Kafe.MiniMePlusPlus.Properties;
using MelonLoader;
using UnityEngine;

namespace Kafe.MiniMePlusPlus;

public static class ModConfig
{
    private static MelonPreferences_Category _melonCategory;

    public static MelonPreferences_Entry<float> MeMiniMeScaleMultiplier;
    public static MelonPreferences_Entry<bool> MeSyncLocalMiniMe;
    public static MelonPreferences_Entry<bool> MeSyncRemoteMiniMe;

    public static ToggleButton SyncLocalToggle;
    public static ToggleButton SyncRemoteToggle;
    public static SliderFloat ScaledMultiplier;

    public static void InitializeMelonPrefs()
    {
        _melonCategory = MelonPreferences.CreateCategory(AssemblyInfoParams.Name);

        MeMiniMeScaleMultiplier = _melonCategory.CreateEntry("MiniMeScaleMultiplier", 1.0f,
            description: "The scale multiplier to apply to the MiniMe when not on the menu.");
        MeMiniMeScaleMultiplier.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (Mathf.Approximately(oldValue, newValue)) return;
            if (ScaledMultiplier != null)
                ScaledMultiplier.SliderValue = newValue;
            MiniMe.UpdateAnchorMode();
            Networking.SendUpdate();
        });

        MeSyncLocalMiniMe = _melonCategory.CreateEntry("SyncLocalMiniMe", true,
            description: "Whether to sync your own MiniMe to other players.");
        MeSyncLocalMiniMe.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (oldValue == newValue) return;
            if (SyncLocalToggle != null)
                SyncLocalToggle.ToggleValue = newValue;
            if (newValue)
            {
                Networking.UpdateMsgSubscriptions();
                Networking.SendUpdate();
            }
            else
            {
                Networking.SendUpdate();
                Networking.UpdateMsgSubscriptions();
            }
        });

        MeSyncRemoteMiniMe = _melonCategory.CreateEntry("SyncRemoteMiniMe", true,
            description: "Whether to receive and show other players' MiniMes.");
        MeSyncRemoteMiniMe.OnEntryValueChanged.Subscribe((oldValue, newValue) =>
        {
            if (oldValue == newValue) return;
            if (SyncRemoteToggle != null)
                SyncRemoteToggle.ToggleValue = newValue;
            if (!newValue)
                RemoteMiniMe.NukeAll();
            Networking.UpdateMsgSubscriptions();
        });
    }

    public static void InitializeUILibMenu()
    {
        ScaledMultiplier = CVR_MenuManager.CVRUtilsCategory.AddSlider(
            "MiniMe Scale",
            "",
            MeMiniMeScaleMultiplier.Value,
            0.1f,
            5f,
            2,
            1f,
            true);
        // ScaledMultiplier.ColumnCount = 2;
        ScaledMultiplier.SliderValue = MeMiniMeScaleMultiplier.Value;
        ScaledMultiplier.OnValueUpdated += newValue =>
        {
            MeMiniMeScaleMultiplier.Value = newValue;
        };

        SyncLocalToggle = CVR_MenuManager.CVRUtilsCategory.AddToggle(
            "Sync Own MiniMe",
            "Whether to sync your own MiniMe to other players",
            MeSyncLocalMiniMe.Value);
        SyncLocalToggle.OnValueUpdated += newValue =>
        {
            MeSyncLocalMiniMe.Value = newValue;
        };

        SyncRemoteToggle = CVR_MenuManager.CVRUtilsCategory.AddToggle(
            "Sync Remote MiniMe",
            "Whether to receive and show other players' MiniMes",
            MeSyncRemoteMiniMe.Value);
        SyncRemoteToggle.OnValueUpdated += newValue =>
        {
            MeSyncRemoteMiniMe.Value = newValue;
        };
    }
}
