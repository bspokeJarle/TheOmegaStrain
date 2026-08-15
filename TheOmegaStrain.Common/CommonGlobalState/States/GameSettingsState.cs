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
        ParticleDensity = 1,
        GlowEffects = 2,
        EnhancedWeather = 3,
        EnhancedShadows = 4
    }

    public sealed class GameSettingsState : IAudioVolumeProfile
    {
        public const int CurrentSettingsSchemaVersion = 3;
        public const int VolumeStepPercent = 5;
        public const int ParticleDensityStepPercent = 10;
        public const int KeyboardControlsOptionCount = 7;
        public const int MouseControlsOptionCount = 3;
        public const int XboxControlsOptionCount = 11;

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
        public int ParticleDensityPercent { get; set; } = 100;
        public bool GlowEffectsEnabled { get; set; } = false;
        public bool EnhancedWeatherEnabled { get; set; } = true;
        public bool EnhancedShadowsEnabled { get; set; } = true;

        public ControlInputMode ActiveControlScheme { get; set; } = ControlInputMode.Keyboard;
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

        public void Normalize()
        {
            MasterVolumePercent = ClampPercent(MasterVolumePercent);
            MusicVolumePercent = ClampPercent(MusicVolumePercent);
            EffectsVolumePercent = ClampPercent(EffectsVolumePercent);
            VoiceVolumePercent = ClampPercent(VoiceVolumePercent);
            ParticleDensityPercent = Math.Clamp(ParticleDensityPercent, 50, 200);

            if (!Enum.IsDefined(typeof(GraphicsQualityPreset), GraphicsQuality))
                GraphicsQuality = GraphicsQualityPreset.Balanced;

            if (!Enum.IsDefined(typeof(ControlInputMode), ActiveControlScheme))
                ActiveControlScheme = ControlInputMode.Keyboard;

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
            }

            Version++;
        }

        public int GetControlsOptionCount()
        {
            Normalize();

            return ActiveControlScheme switch
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

            bool changed = selectedIndex == 0
                ? AdjustActiveControlScheme(direction)
                : ActiveControlScheme switch
                {
                    ControlInputMode.Mouse => AdjustMouseControl(selectedIndex, direction),
                    ControlInputMode.XboxController => AdjustXboxControl(selectedIndex, direction),
                    _ => AdjustKeyboardControl(selectedIndex, direction)
                };

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
                    break;
                case GraphicsQualityPreset.High:
                    ParticleDensityPercent = 180;
                    GlowEffectsEnabled = true;
                    EnhancedWeatherEnabled = true;
                    EnhancedShadowsEnabled = true;
                    break;
                default:
                    ParticleDensityPercent = 100;
                    GlowEffectsEnabled = false;
                    EnhancedWeatherEnabled = true;
                    EnhancedShadowsEnabled = true;
                    break;
            }
        }

        private bool AdjustActiveControlScheme(int direction)
        {
            ActiveControlScheme = CycleEnum(ActiveControlScheme, direction);
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
