using System.Reflection;
using ABI_RC.Core.InteractionSystem;
using ABI_RC.Systems.UI.UILib;
using ABI_RC.Systems.UI.UILib.UIObjects;
using ABI_RC.Systems.UI.UILib.UIObjects.Components;
using MelonLoader;
using UnityEngine.Rendering.PostProcessing;

namespace Kafe.PostProcessingOverrides;

public static class ModConfig
{
    private const int Multiplier = 10;

    // Melon Prefs
    private static MelonPreferences_Category _melonCategory;
    internal static MelonPreferences_Entry<OverrideType> MeDefaultWorldConfig;

    // BTKUI Stuff
    private static Page _page;
    private static Button _overrideButton;
    private static Category _currentOverrideCategory;
    private static Button _colorGradingModeButton;
    private static readonly List<(Button Button, Func<PostProcessingOverrides.JsonConfigPPSettings, PostProcessingOverrides.JsonConfigPPSetting> Getter)> EffectButtons = new();
    private static readonly List<Action<PostProcessingOverrides.JsonConfigPPSettings>> SliderRefreshes = new();

    // Internal
    private static readonly int TypeCount = Enum.GetValues(typeof(OverrideType)).Length;
    private static readonly int SettingCount = Enum.GetValues(typeof(OverrideSetting)).Length;

    public static void InitializeMelonPrefs()
    {
        // Melon Config
        _melonCategory = MelonPreferences.CreateCategory(nameof(PostProcessingOverrides));

        MeDefaultWorldConfig = _melonCategory.CreateEntry("DefaultWorldConfig", OverrideType.Original,
            description:
            "When joining a world for the first time which configuration should be used? Original = It won't override the world PP, " +
            "Default options will override with the respective configurations, Custom will override with a custom config with the defaults " +
            "matching the device you're using. Off is disabled PP.");
    }

    public static void InitializeBTKUI()
    {
        QuickMenuAPI.OnMenuRegenerate += SetupBTKUI;
    }

    private static void SetupBTKUI(CVR_MenuManager manager)
    {
        QuickMenuAPI.OnMenuRegenerate -= SetupBTKUI;

        // Load icons
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "Icon",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.BTKUIIcon.png"));
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Off",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Off.png"));
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Original",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Original.png"));
        QuickMenuAPI.PrepareIcon(nameof(PostProcessingOverrides), "TT_Override",
            Assembly.GetExecutingAssembly().GetManifestResourceStream("resources.TT_Override.png"));

        _page = new Page(nameof(PostProcessingOverrides), nameof(PostProcessingOverrides), true, "Icon")
        {
            MenuTitle = nameof(PostProcessingOverrides),
            MenuSubtitle = "Configure Post Processing Overrides per world",
        };

        if (PostProcessingOverrides.IsWorldLoaded())
            SetupOverrideButtons();

        PostProcessingOverrides.ConfigChanged += SetupOverrideButtons;
    }

    private static string GetTripleToggleIcon(OverrideSetting setting)
    {
        return setting switch
        {
            OverrideSetting.Off => "TT_Off",
            OverrideSetting.Original => "TT_Original",
            OverrideSetting.Override => "TT_Override",
            _ => "",
        };
    }

    private static PostProcessingOverrides.JsonConfigPPSettings GetActiveSettings(PostProcessingOverrides.JsonConfigWorldPP config)
    {
        return config.ConfigType switch
        {
            OverrideType.Global => PostProcessingOverrides.Config.Global,
            OverrideType.Custom => config.CustomConfig,
            _ => null,
        };
    }

    private static void CreateEffectButton(
        Category currentOverride,
        string settingName,
        Func<PostProcessingOverrides.JsonConfigPPSettings, PostProcessingOverrides.JsonConfigPPSetting> getter)
    {
        var button = currentOverride.AddButton(settingName, "", $"{settingName} Control");
        button.OnPress += () =>
        {
            if (!PostProcessingOverrides.IsWorldLoaded())
                return;

            var active = GetActiveSettings(PostProcessingOverrides.GetCurrentConfigSettings());
            if (active == null)
                return;

            var setting = getter(active);
            setting.Active = (OverrideSetting)(((int)setting.Active + 1) % SettingCount);
            button.ButtonIcon = GetTripleToggleIcon(setting.Active);
            PostProcessingOverrides.SaveConfigAndApplyChanges(true, notifyConfigChanged: false);
        };
        EffectButtons.Add((button, getter));
    }

    private static void CreateSlider(
        Category category,
        string name,
        string tooltip,
        float defaultValue,
        float minValue,
        float maxValue,
        int decimalPlaces,
        bool allowReset,
        Func<PostProcessingOverrides.JsonConfigPPSettings, float> getter,
        Action<PostProcessingOverrides.JsonConfigPPSettings, float> setter)
    {
        var slider = category.AddSlider(name, tooltip, defaultValue, minValue, maxValue, decimalPlaces, defaultValue, allowReset);
        slider.OnValueUpdated += newValue =>
        {
            if (float.IsNaN(newValue) || float.IsInfinity(newValue) || !PostProcessingOverrides.IsWorldLoaded())
                return;

            var active = GetActiveSettings(PostProcessingOverrides.GetCurrentConfigSettings());
            if (active == null)
                return;

            setter(active, newValue);
            PostProcessingOverrides.SaveConfigAndApplyChanges(true, notifyConfigChanged: false);
        };
        SliderRefreshes.Add(active => slider.SetSliderValue(getter(active)));
    }

    private static void BuildOverrideMenu()
    {
        _page.ClearChildren();
        EffectButtons.Clear();
        SliderRefreshes.Clear();

        var currentWorld = _page.AddCategory("Current World");
        _overrideButton = currentWorld.AddButton("PostProcessing: Original", "",
            "Which override type should we use? Original: Original Post processing of the world. " +
            "Global: Uses your Global override. Custom: Override specific for this world. Off: Disables PP.");

        _overrideButton.OnPress += () =>
        {
            if (!PostProcessingOverrides.IsWorldLoaded())
                return;

            var config = PostProcessingOverrides.GetCurrentConfigSettings();
            config.ConfigType = (OverrideType)(((int)config.ConfigType + 1) % TypeCount);
            RefreshOverrideUI();
            PostProcessingOverrides.SaveConfigAndApplyChanges(true, notifyConfigChanged: false);
        };

        _currentOverrideCategory = _page.AddCategory("Global Override");

        // Toggles for the overrides
        CreateEffectButton(_currentOverrideCategory, "Bloom", s => s.Bloom);
        CreateEffectButton(_currentOverrideCategory, "Ambient Occlusion", s => s.AO);
        CreateEffectButton(_currentOverrideCategory, "Color Grading", s => s.ColorGrading);
        CreateEffectButton(_currentOverrideCategory, "Auto Exposure", s => s.AutoExposure);
        CreateEffectButton(_currentOverrideCategory, "Chromatic Aberration", s => s.ChromaticAberration);
        CreateEffectButton(_currentOverrideCategory, "Depth of Field", s => s.DepthOfField);
        CreateEffectButton(_currentOverrideCategory, "Grain", s => s.Grain);
        CreateEffectButton(_currentOverrideCategory, "Lens Distortion", s => s.LensDistortion);
        CreateEffectButton(_currentOverrideCategory, "Motion Blur", s => s.MotionBlur);
        CreateEffectButton(_currentOverrideCategory, "Vignette", s => s.Vignette);

        // Sliders
        CreateSlider(_currentOverrideCategory, "Bloom Intensity", "Set the Bloom Intensity", 0.02f, 0f, 5f, 2, true,
            s => s.Bloom.Intensity / Multiplier, (s, v) => s.Bloom.Intensity = v * Multiplier);
        CreateSlider(_currentOverrideCategory, "Bloom Threshold", "Set the Bloom Threshold", 1.0f, 0f, 5f, 2, true,
            s => s.Bloom.Threshold, (s, v) => s.Bloom.Threshold = v);
        CreateSlider(_currentOverrideCategory, "AO Intensity", "Set the Ambient Occlusion Intensity", 0f, 0f, 4f, 2, true,
            s => s.AO.Intensity, (s, v) => s.AO.Intensity = v);
        CreateSlider(_currentOverrideCategory, "ColorGrading Post Exposure", "Set the ColorGrading Post Exposure (HDR mode, EV)", 0f, -5f, 5f, 2, true,
            s => s.ColorGrading.PostExposure, (s, v) => s.ColorGrading.PostExposure = v);
        CreateSlider(_currentOverrideCategory, "ColorGrading Brightness", "Set the ColorGrading Brightness (LDR mode only)", 0f, -100f, 100f, 1, true,
            s => s.ColorGrading.Brightness, (s, v) => s.ColorGrading.Brightness = v);
        CreateSlider(_currentOverrideCategory, "ColorGrading Hue Shift", "Set the ColorGrading Hue Shift", 0f, -180f, 180f, 1, true,
            s => s.ColorGrading.HueShift, (s, v) => s.ColorGrading.HueShift = v);

        _colorGradingModeButton = _currentOverrideCategory.AddButton("ColorGrading Mode: HighDefinitionRange", "", "ColorGrading Grading Mode", ButtonStyle.TextOnly);
        _colorGradingModeButton.OnPress += () =>
        {
            if (!PostProcessingOverrides.IsWorldLoaded())
                return;

            var active = GetActiveSettings(PostProcessingOverrides.GetCurrentConfigSettings());
            if (active == null)
                return;

            active.ColorGrading.GradingMode = active.ColorGrading.GradingMode == GradingMode.HighDefinitionRange
                ? GradingMode.LowDefinitionRange
                : GradingMode.HighDefinitionRange;
            _colorGradingModeButton.ButtonText = $"ColorGrading Mode: {active.ColorGrading.GradingMode}";
            PostProcessingOverrides.SaveConfigAndApplyChanges(true, notifyConfigChanged: false);
        };

        CreateSlider(_currentOverrideCategory, "AutoExposure Key Value", "Set the Auto Exposure Key Value", 1.0f, 0f, 4f, 2, true,
            s => s.AutoExposure.KeyValue, (s, v) => s.AutoExposure.KeyValue = v);
        CreateSlider(_currentOverrideCategory, "Chromatic Aberration", "Set the Chromatic Aberration Intensity", 0f, 0f, 1f, 2, true,
            s => s.ChromaticAberration.Intensity, (s, v) => s.ChromaticAberration.Intensity = v);
        CreateSlider(_currentOverrideCategory, "DoF Focus Distance", "Set the Depth of Field Focus Distance", 10f, 0.1f, 100f, 1, true,
            s => s.DepthOfField.FocusDistance, (s, v) => s.DepthOfField.FocusDistance = v);
        CreateSlider(_currentOverrideCategory, "DoF Aperture", "Set the Depth of Field Aperture", 5.6f, 0.1f, 32f, 1, true,
            s => s.DepthOfField.Aperture, (s, v) => s.DepthOfField.Aperture = v);
        CreateSlider(_currentOverrideCategory, "Grain Intensity", "Set the Grain Intensity", 0f, 0f, 1f, 2, true,
            s => s.Grain.Intensity, (s, v) => s.Grain.Intensity = v);
        CreateSlider(_currentOverrideCategory, "Lens Distortion", "Set the Lens Distortion Intensity", 0f, -100f, 100f, 1, true,
            s => s.LensDistortion.Intensity, (s, v) => s.LensDistortion.Intensity = v);
        CreateSlider(_currentOverrideCategory, "Motion Blur Angle", "Set the Motion Blur Shutter Angle", 270f, 0f, 360f, 0, true,
            s => s.MotionBlur.ShutterAngle, (s, v) => s.MotionBlur.ShutterAngle = v);
        CreateSlider(_currentOverrideCategory, "Vignette Intensity", "Set the Vignette Intensity", 0f, 0f, 1f, 2, true,
            s => s.Vignette.Intensity, (s, v) => s.Vignette.Intensity = v);
    }

    private static void RefreshOverrideUI()
    {
        if (!PostProcessingOverrides.IsWorldLoaded() || _overrideButton == null)
            return;

        var config = PostProcessingOverrides.GetCurrentConfigSettings();
        _overrideButton.ButtonText = $"PostProcessing: {config.ConfigType}";

        var active = GetActiveSettings(config);
        if (active == null)
        {
            _currentOverrideCategory.Hidden = true;
            return;
        }

        _currentOverrideCategory.CategoryName = config.ConfigType == OverrideType.Global
            ? "Global Override"
            : "Custom Override";
        _currentOverrideCategory.Hidden = false;

        foreach (var (button, getter) in EffectButtons)
            button.ButtonIcon = GetTripleToggleIcon(getter(active).Active);

        foreach (var refreshSlider in SliderRefreshes)
            refreshSlider(active);

        _colorGradingModeButton.ButtonText = $"ColorGrading Mode: {active.ColorGrading.GradingMode}";
    }

    private static void SetupOverrideButtons()
    {
        if (_page == null || !PostProcessingOverrides.IsWorldLoaded())
            return;

        if (_overrideButton == null)
            BuildOverrideMenu();

        RefreshOverrideUI();
    }
}
