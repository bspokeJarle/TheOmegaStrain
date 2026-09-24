using RetroMesh.Engine;
using System;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Common.CommonGlobalState.States
{
    public enum GraphicsQualityPreset
    {
        Low = 0,
        Balanced = 1,
        High = 2
    }

    public enum CameraAnglePreset { Low = 0, Normal = 1, High = 2 }

    public enum AudioSettingsField
    {
        MasterVolume = 0,
        MusicVolume = 1,
        EffectsVolume = 2,
        VoiceVolume = 3
    }

    public enum GraphicsSettingsField
    {
        QualityPreset = 0,
        CameraAngle = 1,
        ParticleDensity = 2,
        GlowEffects = 3,
        EnhancedWeather = 4,
        EnhancedShadows = 5,
        SceneRasterBackground = 6,
        Clouds = 7
    }

    public enum FlightSettingsField
    {
        Preset = 0,
        FlightInertia = 1,
        RotationInertia = 2,
        ThrustResponse = 3,
        GravityResponse = 4,
        ResetDefaults = 5
    }

    public enum FlightHandlingPreset { Stable = 0, Balanced = 1, Inertial = 2, Custom = 3 }
    public enum FlightCoasting { Short = 0, Normal = 1, Long = 2 }
    public enum FlightRotationInertia { Low = 0, Normal = 1, High = 2 }
    public enum FlightThrustResponse { Soft = 0, Normal = 1, Quick = 2 }
    public enum FlightGravityResponse { Light = 0, Normal = 1, Strong = 2 }

    public sealed class GameSettingsState : IAudioVolumeProfile
    {
        public const int CurrentSettingsSchemaVersion = 8;
        public const int VolumeStepPercent = 5;
        public const int ParticleDensityStepPercent = 10;
        private const int ControlFlowOptionCount = 2;
        private const int KeyboardMappingOptionCount = 6;
        private const int MouseMappingOptionCount = 2;
        private const int XboxMappingOptionCount = 10;
        public const int KeyboardControlsOptionCount = ControlFlowOptionCount + KeyboardMappingOptionCount;
        public const int MouseControlsOptionCount = ControlFlowOptionCount + MouseMappingOptionCount;
        public const int XboxControlsOptionCount = ControlFlowOptionCount + XboxMappingOptionCount;

        private static readonly string[] KeyboardKeyOptions =
        {
            "Left",
            "Right",
            "Up",
            "Down",
            "Space",
            "RShiftKey",
            "LShiftKey",
            "W",
            "A",
            "S",
            "D",
            "Q",
            "E",
            "Z",
            "X",
            "C",
            "V",
            "F",
            "R",
            "Tab",
            "Enter"
        };

        public int SettingsSchemaVersion { get; set; } = CurrentSettingsSchemaVersion;
        public int MasterVolumePercent { get; set; } = 100;
        public int MusicVolumePercent { get; set; } = 100;
        public int EffectsVolumePercent { get; set; } = 100;
        public int VoiceVolumePercent { get; set; } = 100;

        public GraphicsQualityPreset GraphicsQuality { get; set; } = GraphicsQualityPreset.Balanced;
        public CameraAnglePreset CameraAngle { get; set; } = CameraAnglePreset.Normal;
        public int ParticleDensityPercent { get; set; } = 100;
        public bool GlowEffectsEnabled { get; set; } = false;
        public bool EnhancedWeatherEnabled { get; set; } = true;
        public bool EnhancedShadowsEnabled { get; set; } = true;
        public bool SceneRasterBackgroundEnabled { get; set; } = true;
        public bool CloudsEnabled { get; set; } = false;

        public FlightHandlingPreset FlightPreset { get; set; } = FlightHandlingPreset.Balanced;
        public FlightCoasting FlightCoastingSetting { get; set; } = FlightCoasting.Normal;
        public FlightRotationInertia FlightRotationInertiaSetting { get; set; } = FlightRotationInertia.Normal;
        public FlightThrustResponse FlightThrustResponseSetting { get; set; } = FlightThrustResponse.Normal;
        public FlightGravityResponse FlightGravityResponseSetting { get; set; } = FlightGravityResponse.Normal;

        public ControlInputMode ActiveControlScheme { get; set; } = ControlInputMode.Keyboard;
        public ControlInputMode ControlsEditorScheme { get; set; } = ControlInputMode.Keyboard;

        /// <summary>
        /// The scheme gameplay should actually use. Xbox only stays active while a
        /// controller is detected, otherwise the game falls back to the keyboard so
        /// the ship never becomes uncontrollable (important for cabinet setups).
        /// </summary>
        public ControlInputMode EffectiveControlScheme =>
            ActiveControlScheme == ControlInputMode.XboxController &&
            !GameState.InputDeviceState.AnyControllerConnected
                ? ControlInputMode.Keyboard
                : ActiveControlScheme;
        public string KeyboardThrustKey { get; set; } = "Space";
        public string KeyboardFireKey { get; set; } = "RShiftKey";
        public string KeyboardPitchUpKey { get; set; } = "Up";
        public string KeyboardPitchDownKey { get; set; } = "Down";
        public string KeyboardTurnLeftKey { get; set; } = "Left";
        public string KeyboardTurnRightKey { get; set; } = "Right";
        public MouseControlButton MouseThrustButton { get; set; } = MouseControlButton.Right;
        public MouseControlButton MouseFireButton { get; set; } = MouseControlButton.Left;
        public XboxControlButton XboxThrustButton { get; set; } = XboxControlButton.RightTrigger;
        public XboxControlButton XboxFireButton { get; set; } = XboxControlButton.LeftTrigger;
        public XboxControlButton XboxPitchUpButton { get; set; } = XboxControlButton.LeftStickUp;
        public XboxControlButton XboxPitchDownButton { get; set; } = XboxControlButton.LeftStickDown;
        public XboxControlButton XboxTurnLeftButton { get; set; } = XboxControlButton.LeftStickLeft;
        public XboxControlButton XboxTurnRightButton { get; set; } = XboxControlButton.LeftStickRight;
        public XboxControlButton XboxBulletButton { get; set; } = XboxControlButton.X;
        public XboxControlButton XboxDecoyButton { get; set; } = XboxControlButton.Y;
        public XboxControlButton XboxLazerButton { get; set; } = XboxControlButton.B;
        public XboxControlButton XboxPowerup4Button { get; set; } = XboxControlButton.A;

        public long Version { get; private set; } = 0;

        public float MasterVolumeMultiplier => PercentToMultiplier(MasterVolumePercent);
        public float MusicVolumeMultiplier => MasterVolumeMultiplier * PercentToMultiplier(MusicVolumePercent);
        public float EffectsVolumeMultiplier => MasterVolumeMultiplier * PercentToMultiplier(EffectsVolumePercent);
        public float VoiceVolumeMultiplier => MasterVolumeMultiplier * PercentToMultiplier(VoiceVolumePercent);
        public float ParticleDensityMultiplier => Math.Clamp(ParticleDensityPercent, 50, 200) / 100f;
        public float CameraPitchDegrees => CameraAngle switch
        {
            CameraAnglePreset.High => 70f,
            _ => 63f
        };
        public float ShipCoastingRetention => FlightCoastingSetting switch
        {
            FlightCoasting.Short => 0.9955f,
            FlightCoasting.Long => 0.9985f,
            _ => 0.9975f
        };
        public float ShipThrustRampRate => FlightThrustResponseSetting switch
        {
            FlightThrustResponse.Soft => 10f,
            FlightThrustResponse.Quick => 60f,
            _ => 30f
        };
        public float ShipThrustSpeedMultiplier => FlightThrustResponseSetting switch
        {
            FlightThrustResponse.Soft => 8.4f,
            FlightThrustResponse.Quick => 10.8f,
            _ => 9.6f
        };
        public float ShipGravityPullMultiplier => FlightGravityResponseSetting switch
        {
            FlightGravityResponse.Light => 7.6f,
            FlightGravityResponse.Strong => 10.4f,
            _ => 9f
        };
        public float ShipRotationRetention => FlightRotationInertiaSetting switch
        {
            FlightRotationInertia.Low => 0.82f,
            FlightRotationInertia.High => 0.96f,
            _ => 0.90f
        };
        public float ShipMouseRotationFollow => FlightRotationInertiaSetting switch
        {
            FlightRotationInertia.Low => 0.60f,
            FlightRotationInertia.High => 0.25f,
            _ => 0.42f
        };
        public float ShipHoverFloatDuration => FlightCoastingSetting switch
        {
            FlightCoasting.Short => 2f,
            FlightCoasting.Long => 1f,
            _ => 1.5f
        };

        public void Normalize()
        {
            MasterVolumePercent = ClampPercent(MasterVolumePercent);
            MusicVolumePercent = ClampPercent(MusicVolumePercent);
            EffectsVolumePercent = ClampPercent(EffectsVolumePercent);
            VoiceVolumePercent = ClampPercent(VoiceVolumePercent);
            ParticleDensityPercent = Math.Clamp(ParticleDensityPercent, 50, 200);

            if (!Enum.IsDefined(typeof(GraphicsQualityPreset), GraphicsQuality))
                GraphicsQuality = GraphicsQualityPreset.Balanced;
            // Low remains in the persisted enum for backwards compatibility,
            // but is no longer a shipping option until it has broader gameplay QA.
            if (!Enum.IsDefined(typeof(CameraAnglePreset), CameraAngle) ||
                CameraAngle == CameraAnglePreset.Low)
                CameraAngle = CameraAnglePreset.Normal;

            if (!Enum.IsDefined(typeof(FlightHandlingPreset), FlightPreset))
                FlightPreset = FlightHandlingPreset.Balanced;
            if (!Enum.IsDefined(typeof(FlightCoasting), FlightCoastingSetting))
                FlightCoastingSetting = FlightCoasting.Normal;
            if (!Enum.IsDefined(typeof(FlightRotationInertia), FlightRotationInertiaSetting))
                FlightRotationInertiaSetting = FlightRotationInertia.Normal;
            if (!Enum.IsDefined(typeof(FlightThrustResponse), FlightThrustResponseSetting))
                FlightThrustResponseSetting = FlightThrustResponse.Normal;
            if (!Enum.IsDefined(typeof(FlightGravityResponse), FlightGravityResponseSetting))
                FlightGravityResponseSetting = FlightGravityResponse.Normal;

            if (!Enum.IsDefined(typeof(ControlInputMode), ActiveControlScheme))
                ActiveControlScheme = ControlInputMode.Keyboard;
            if (!Enum.IsDefined(typeof(ControlInputMode), ControlsEditorScheme))
                ControlsEditorScheme = ActiveControlScheme;

            KeyboardThrustKey = NormalizeKeyboardKey(KeyboardThrustKey, "Space");
            KeyboardFireKey = NormalizeKeyboardKey(KeyboardFireKey, "RShiftKey");
            KeyboardPitchUpKey = NormalizeKeyboardKey(KeyboardPitchUpKey, "Up");
            KeyboardPitchDownKey = NormalizeKeyboardKey(KeyboardPitchDownKey, "Down");
            KeyboardTurnLeftKey = NormalizeKeyboardKey(KeyboardTurnLeftKey, "Left");
            KeyboardTurnRightKey = NormalizeKeyboardKey(KeyboardTurnRightKey, "Right");

            if (!Enum.IsDefined(typeof(MouseControlButton), MouseThrustButton))
                MouseThrustButton = MouseControlButton.Right;
            if (!Enum.IsDefined(typeof(MouseControlButton), MouseFireButton))
                MouseFireButton = MouseControlButton.Left;

            if (!Enum.IsDefined(typeof(XboxControlButton), XboxThrustButton))
                XboxThrustButton = XboxControlButton.RightTrigger;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxFireButton))
                XboxFireButton = XboxControlButton.LeftTrigger;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxPitchUpButton))
                XboxPitchUpButton = XboxControlButton.LeftStickUp;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxPitchDownButton))
                XboxPitchDownButton = XboxControlButton.LeftStickDown;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxTurnLeftButton))
                XboxTurnLeftButton = XboxControlButton.LeftStickLeft;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxTurnRightButton))
                XboxTurnRightButton = XboxControlButton.LeftStickRight;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxBulletButton))
                XboxBulletButton = XboxControlButton.X;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxDecoyButton))
                XboxDecoyButton = XboxControlButton.Y;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxLazerButton))
                XboxLazerButton = XboxControlButton.B;
            if (!Enum.IsDefined(typeof(XboxControlButton), XboxPowerup4Button))
                XboxPowerup4Button = XboxControlButton.A;

            SettingsSchemaVersion = CurrentSettingsSchemaVersion;
        }

        public void AdjustAudio(AudioSettingsField field, int direction)
        {
            if (direction == 0)
                return;

            int delta = direction > 0 ? VolumeStepPercent : -VolumeStepPercent;

            switch (field)
            {
                case AudioSettingsField.MasterVolume:
                    MasterVolumePercent = ClampPercent(MasterVolumePercent + delta);
                    break;
                case AudioSettingsField.MusicVolume:
                    MusicVolumePercent = ClampPercent(MusicVolumePercent + delta);
                    break;
                case AudioSettingsField.EffectsVolume:
                    EffectsVolumePercent = ClampPercent(EffectsVolumePercent + delta);
                    break;
                case AudioSettingsField.VoiceVolume:
                    VoiceVolumePercent = ClampPercent(VoiceVolumePercent + delta);
                    break;
            }

            Version++;
        }

        public void AdjustGraphics(GraphicsSettingsField field, int direction)
        {
            if (direction == 0)
                return;

            switch (field)
            {
                case GraphicsSettingsField.QualityPreset:
                    GraphicsQuality = AdjustQuality(GraphicsQuality, direction);
                    ApplyPresetDefaults(GraphicsQuality);
                    break;
                case GraphicsSettingsField.CameraAngle:
                    CameraAngle = CameraAngle == CameraAnglePreset.High
                        ? CameraAnglePreset.Normal
                        : CameraAnglePreset.High;
                    break;
                case GraphicsSettingsField.ParticleDensity:
                    int delta = direction > 0 ? ParticleDensityStepPercent : -ParticleDensityStepPercent;
                    ParticleDensityPercent = Math.Clamp(ParticleDensityPercent + delta, 50, 200);
                    break;
                case GraphicsSettingsField.GlowEffects:
                    GlowEffectsEnabled = !GlowEffectsEnabled;
                    break;
                case GraphicsSettingsField.EnhancedWeather:
                    EnhancedWeatherEnabled = !EnhancedWeatherEnabled;
                    break;
                case GraphicsSettingsField.EnhancedShadows:
                    EnhancedShadowsEnabled = !EnhancedShadowsEnabled;
                    break;
                case GraphicsSettingsField.SceneRasterBackground:
                    SceneRasterBackgroundEnabled = !SceneRasterBackgroundEnabled;
                    break;
                case GraphicsSettingsField.Clouds:
                    CloudsEnabled = !CloudsEnabled;
                    break;
            }

            Version++;
        }

        public void AdjustFlight(FlightSettingsField field, int direction)
        {
            if (direction == 0)
                return;

            Normalize();
            switch (field)
            {
                case FlightSettingsField.Preset:
                    int next = ((int)FlightPreset + (direction > 0 ? 1 : -1) + 3) % 3;
                    ApplyFlightPreset((FlightHandlingPreset)next);
                    break;
                case FlightSettingsField.FlightInertia:
                    FlightCoastingSetting = CycleEnum(FlightCoastingSetting, direction);
                    FlightPreset = FlightHandlingPreset.Custom;
                    break;
                case FlightSettingsField.RotationInertia:
                    FlightRotationInertiaSetting = CycleEnum(FlightRotationInertiaSetting, direction);
                    FlightPreset = FlightHandlingPreset.Custom;
                    break;
                case FlightSettingsField.ThrustResponse:
                    FlightThrustResponseSetting = CycleEnum(FlightThrustResponseSetting, direction);
                    FlightPreset = FlightHandlingPreset.Custom;
                    break;
                case FlightSettingsField.GravityResponse:
                    FlightGravityResponseSetting = CycleEnum(FlightGravityResponseSetting, direction);
                    FlightPreset = FlightHandlingPreset.Custom;
                    break;
                case FlightSettingsField.ResetDefaults:
                    ApplyFlightPreset(FlightHandlingPreset.Balanced);
                    break;
            }

            Version++;
        }

        private void ApplyFlightPreset(FlightHandlingPreset preset)
        {
            FlightPreset = preset;
            switch (preset)
            {
                case FlightHandlingPreset.Stable:
                    FlightCoastingSetting = FlightCoasting.Short;
                    FlightRotationInertiaSetting = FlightRotationInertia.Low;
                    FlightThrustResponseSetting = FlightThrustResponse.Quick;
                    FlightGravityResponseSetting = FlightGravityResponse.Strong;
                    break;
                case FlightHandlingPreset.Inertial:
                    FlightCoastingSetting = FlightCoasting.Long;
                    FlightRotationInertiaSetting = FlightRotationInertia.High;
                    FlightThrustResponseSetting = FlightThrustResponse.Soft;
                    FlightGravityResponseSetting = FlightGravityResponse.Light;
                    break;
                default:
                    FlightPreset = FlightHandlingPreset.Balanced;
                    FlightCoastingSetting = FlightCoasting.Normal;
                    FlightRotationInertiaSetting = FlightRotationInertia.Normal;
                    FlightThrustResponseSetting = FlightThrustResponse.Normal;
                    FlightGravityResponseSetting = FlightGravityResponse.Normal;
                    break;
            }
        }

        public int GetControlsOptionCount()
        {
            Normalize();

            return ControlsEditorScheme switch
            {
                ControlInputMode.Mouse => MouseControlsOptionCount,
                ControlInputMode.XboxController => XboxControlsOptionCount,
                _ => KeyboardControlsOptionCount
            };
        }

        public void AdjustControls(int selectedIndex, int direction)
        {
            if (direction == 0)
                return;

            Normalize();

            bool changed;
            if (selectedIndex == 0)
            {
                changed = AdjustActiveControlScheme(direction);
                if (changed)
                    ControlsEditorScheme = ActiveControlScheme;
            }
            else if (selectedIndex == 1)
            {
                changed = AdjustControlsEditorScheme(direction);
            }
            else
            {
                int mappingIndex = selectedIndex - 1;
                changed = ControlsEditorScheme switch
                {
                    ControlInputMode.Mouse => AdjustMouseControl(mappingIndex, direction),
                    ControlInputMode.XboxController => AdjustXboxControl(mappingIndex, direction),
                    _ => AdjustKeyboardControl(mappingIndex, direction)
                };
            }

            if (changed)
                Version++;
        }

        public float ApplyMusicVolume(float baseVolume) => Clamp01(baseVolume * MusicVolumeMultiplier);
        public float ApplyEffectsVolume(float baseVolume) => SanitizeVolume(baseVolume * EffectsVolumeMultiplier);
        public float ApplyVoiceVolume(float baseVolume) => SanitizeVolume(baseVolume * VoiceVolumeMultiplier);

        public int ScaleParticleCount(int baseCount)
        {
            if (!EnhancedWeatherEnabled)
                return 0;

            return Math.Max(0, (int)MathF.Round(baseCount * ParticleDensityMultiplier));
        }

        public bool IsVoiceSound(string? soundId, string? usage)
        {
            return StartsWithIgnoreCase(soundId, "ship_ai_") ||
                   StartsWithIgnoreCase(soundId, "ship_collision_warning") ||
                   ContainsIgnoreCase(usage, "ShipAiVoice") ||
                   ContainsIgnoreCase(usage, "Voice") ||
                   ContainsIgnoreCase(usage, "Warning");
        }

        private static GraphicsQualityPreset AdjustQuality(GraphicsQualityPreset current, int direction)
        {
            int next = (int)current + (direction > 0 ? 1 : -1);
            next = Math.Clamp(next, (int)GraphicsQualityPreset.Low, (int)GraphicsQualityPreset.High);
            return (GraphicsQualityPreset)next;
        }

        private void ApplyPresetDefaults(GraphicsQualityPreset preset)
        {
            switch (preset)
            {
                case GraphicsQualityPreset.Low:
                    ParticleDensityPercent = 70;
                    GlowEffectsEnabled = false;
                    EnhancedWeatherEnabled = false;
                    EnhancedShadowsEnabled = false;
                    CloudsEnabled = false;
                    break;
                case GraphicsQualityPreset.High:
                    ParticleDensityPercent = 180;
                    GlowEffectsEnabled = true;
                    EnhancedWeatherEnabled = true;
                    EnhancedShadowsEnabled = true;
                    CloudsEnabled = true;
                    break;
                default:
                    ParticleDensityPercent = 100;
                    GlowEffectsEnabled = false;
                    EnhancedWeatherEnabled = true;
                    EnhancedShadowsEnabled = true;
                    CloudsEnabled = false;
                    break;
            }
        }

        private bool AdjustActiveControlScheme(int direction)
        {
            ActiveControlScheme = CycleEnum(ActiveControlScheme, direction);
            return true;
        }

        private bool AdjustControlsEditorScheme(int direction)
        {
            ControlsEditorScheme = CycleEnum(ControlsEditorScheme, direction);
            return true;
        }

        private bool AdjustKeyboardControl(int selectedIndex, int direction)
        {
            switch (selectedIndex)
            {
                case 1:
                    KeyboardThrustKey = CycleKeyboardKey(KeyboardThrustKey, direction);
                    return true;
                case 2:
                    KeyboardFireKey = CycleKeyboardKey(KeyboardFireKey, direction);
                    return true;
                case 3:
                    KeyboardPitchUpKey = CycleKeyboardKey(KeyboardPitchUpKey, direction);
                    return true;
                case 4:
                    KeyboardPitchDownKey = CycleKeyboardKey(KeyboardPitchDownKey, direction);
                    return true;
                case 5:
                    KeyboardTurnLeftKey = CycleKeyboardKey(KeyboardTurnLeftKey, direction);
                    return true;
                case 6:
                    KeyboardTurnRightKey = CycleKeyboardKey(KeyboardTurnRightKey, direction);
                    return true;
                default:
                    return false;
            }
        }

        private bool AdjustMouseControl(int selectedIndex, int direction)
        {
            switch (selectedIndex)
            {
                case 1:
                    MouseThrustButton = CycleEnum(MouseThrustButton, direction);
                    return true;
                case 2:
                    MouseFireButton = CycleEnum(MouseFireButton, direction);
                    return true;
                default:
                    return false;
            }
        }

        private bool AdjustXboxControl(int selectedIndex, int direction)
        {
            switch (selectedIndex)
            {
                case 1:
                    XboxThrustButton = CycleEnum(XboxThrustButton, direction);
                    return true;
                case 2:
                    XboxFireButton = CycleEnum(XboxFireButton, direction);
                    return true;
                case 3:
                    XboxPitchUpButton = CycleEnum(XboxPitchUpButton, direction);
                    return true;
                case 4:
                    XboxPitchDownButton = CycleEnum(XboxPitchDownButton, direction);
                    return true;
                case 5:
                    XboxTurnLeftButton = CycleEnum(XboxTurnLeftButton, direction);
                    return true;
                case 6:
                    XboxTurnRightButton = CycleEnum(XboxTurnRightButton, direction);
                    return true;
                case 7:
                    XboxBulletButton = CycleEnum(XboxBulletButton, direction);
                    return true;
                case 8:
                    XboxDecoyButton = CycleEnum(XboxDecoyButton, direction);
                    return true;
                case 9:
                    XboxLazerButton = CycleEnum(XboxLazerButton, direction);
                    return true;
                case 10:
                    XboxPowerup4Button = CycleEnum(XboxPowerup4Button, direction);
                    return true;
                default:
                    return false;
            }
        }

        private static T CycleEnum<T>(T current, int direction) where T : struct, Enum
        {
            var values = Enum.GetValues<T>();
            int currentIndex = Array.IndexOf(values, current);
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = currentIndex + (direction > 0 ? 1 : -1);
            if (nextIndex < 0)
                nextIndex = values.Length - 1;
            else if (nextIndex >= values.Length)
                nextIndex = 0;

            return values[nextIndex];
        }

        private static string CycleKeyboardKey(string current, int direction)
        {
            int currentIndex = FindKeyboardKeyIndex(current);
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = currentIndex + (direction > 0 ? 1 : -1);
            if (nextIndex < 0)
                nextIndex = KeyboardKeyOptions.Length - 1;
            else if (nextIndex >= KeyboardKeyOptions.Length)
                nextIndex = 0;

            return KeyboardKeyOptions[nextIndex];
        }

        private static string NormalizeKeyboardKey(string? key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            int index = FindKeyboardKeyIndex(key.Trim());
            return index >= 0 ? KeyboardKeyOptions[index] : fallback;
        }

        private static int FindKeyboardKeyIndex(string key)
        {
            for (int i = 0; i < KeyboardKeyOptions.Length; i++)
            {
                if (string.Equals(KeyboardKeyOptions[i], key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static int ClampPercent(int value) => Math.Clamp(value, 0, 100);

        private static float PercentToMultiplier(int value) => ClampPercent(value) / 100f;

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

        private static float SanitizeVolume(float value) =>
            float.IsFinite(value) ? Math.Max(0f, value) : 0f;

        private static bool StartsWithIgnoreCase(string? value, string prefix) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        private static bool ContainsIgnoreCase(string? value, string needle) =>
            !string.IsNullOrWhiteSpace(value) &&
            value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
