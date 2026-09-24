using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonGlobalState.States;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Common.CommonGlobalState
{
    public static class GameSettingsOverlayFormatter
    {
        public static string BuildFooter(GameSettingsState settings, bool pageNavigationSelected = false)
        {
            settings.Normalize();
            if (pageNavigationSelected)
            {
                return settings.EffectiveControlScheme == ControlInputMode.XboxController
                    ? "D-PAD LEFT/RIGHT CHANGE PAGE | DOWN EDIT SETTINGS | [B] BACK"
                    : "LEFT/RIGHT CHANGE PAGE | DOWN EDIT SETTINGS | ESC BACK";
            }

            return settings.EffectiveControlScheme == ControlInputMode.XboxController
                ? "D-PAD UP/DOWN SELECT | LEFT/RIGHT ADJUST | [LB]/[RB] CHANGE PAGE | [B] BACK"
                : "UP/DOWN SELECT | LEFT/RIGHT ADJUST | PAGE UP/DOWN CHANGE PAGE | ESC BACK";
        }

        public static string BuildAudioBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();

            var lines = new List<string>
            {
                "Adjust the shipboard audio mix. Changes are saved locally.",
                ""
            };

            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.MasterVolume, "MASTER", settings.MasterVolumePercent);
            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.MusicVolume, "MUSIC", settings.MusicVolumePercent);
            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.EffectsVolume, "EFFECTS", settings.EffectsVolumePercent);
            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.VoiceVolume, "HAL-E / VOICE", settings.VoiceVolumePercent);

            return string.Join("\n", lines);
        }

        public static string BuildGraphicsBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();

            var lines = new List<string>
            {
                "Tune visual detail for this machine. Changes are saved locally.",
                ""
            };

            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.QualityPreset, "QUALITY", settings.GraphicsQuality.ToString().ToUpperInvariant());
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.CameraAngle, "CAMERA ANGLE", settings.CameraAngle.ToString().ToUpperInvariant());
            AddPercentLine(lines, selectedIndex, (int)GraphicsSettingsField.ParticleDensity, "PARTICLES", settings.ParticleDensityPercent);
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.GlowEffects, "GLOW", OnOff(settings.GlowEffectsEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.EnhancedWeather, "WEATHER FX", OnOff(settings.EnhancedWeatherEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.EnhancedShadows, "SHADOWS", OnOff(settings.EnhancedShadowsEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.SceneRasterBackground, "SKY RASTER", OnOff(settings.SceneRasterBackgroundEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.Clouds, "CLOUDS", OnOff(settings.CloudsEnabled));

            return string.Join("\n", lines);
        }

        public static string BuildControlsBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();

            var lines = new List<string>
            {
                "Choose how to play, then which device bindings to configure.",
                "Keyboard weapon keys 1/2/3 stay live for quick weapon select.",
                ""
            };

            AddValueLine(lines, selectedIndex, 0, "PLAY USING", FormatControlMode(settings.ActiveControlScheme));
            AddValueLine(lines, selectedIndex, 1, "CONFIGURE", FormatControlMode(settings.ControlsEditorScheme));
            lines.Add("");
            lines.Add($"{FormatControlMode(settings.ControlsEditorScheme)} MAPPINGS");

            switch (settings.ControlsEditorScheme)
            {
                case ControlInputMode.Mouse:
                    AddValueLine(lines, selectedIndex, 2, "THRUST", FormatMouseButton(settings.MouseThrustButton));
                    AddValueLine(lines, selectedIndex, 3, "FIRE", FormatMouseButton(settings.MouseFireButton));
                    AddValueLine(lines, selectedIndex, -1, "STEER", "MOUSE MOVE");
                    break;
                case ControlInputMode.XboxController:
                    AddValueLine(lines, selectedIndex, 2, "THRUST", FormatXboxButton(settings.XboxThrustButton));
                    AddValueLine(lines, selectedIndex, 3, "FIRE", FormatXboxButton(settings.XboxFireButton));
                    AddValueLine(lines, selectedIndex, 4, "PITCH UP", FormatXboxButton(settings.XboxPitchUpButton));
                    AddValueLine(lines, selectedIndex, 5, "PITCH DOWN", FormatXboxButton(settings.XboxPitchDownButton));
                    AddValueLine(lines, selectedIndex, 6, "TURN LEFT", FormatXboxButton(settings.XboxTurnLeftButton));
                    AddValueLine(lines, selectedIndex, 7, "TURN RIGHT", FormatXboxButton(settings.XboxTurnRightButton));
                    AddValueLine(lines, selectedIndex, 8, "BULLET POWERUP", FormatXboxButton(settings.XboxBulletButton));
                    AddValueLine(lines, selectedIndex, 9, "DECOY", FormatXboxButton(settings.XboxDecoyButton));
                    AddValueLine(lines, selectedIndex, 10, "LASER POWERUP", FormatXboxButton(settings.XboxLazerButton));
                    AddValueLine(lines, selectedIndex, 11, "SPECIAL POWERUP", FormatXboxButton(settings.XboxPowerup4Button));
                    break;
                default:
                    AddValueLine(lines, selectedIndex, 2, "THRUST", FormatKeyboardKey(settings.KeyboardThrustKey));
                    AddValueLine(lines, selectedIndex, 3, "FIRE", FormatKeyboardKey(settings.KeyboardFireKey));
                    AddValueLine(lines, selectedIndex, 4, "PITCH UP", FormatKeyboardKey(settings.KeyboardPitchUpKey));
                    AddValueLine(lines, selectedIndex, 5, "PITCH DOWN", FormatKeyboardKey(settings.KeyboardPitchDownKey));
                    AddValueLine(lines, selectedIndex, 6, "TURN LEFT", FormatKeyboardKey(settings.KeyboardTurnLeftKey));
                    AddValueLine(lines, selectedIndex, 7, "TURN RIGHT", FormatKeyboardKey(settings.KeyboardTurnRightKey));
                    break;
            }

            return string.Join("\n", lines);
        }

        public static string BuildFlightBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();
            var lines = new List<string>
            {
                "Tune player ship handling. Enemies, weapons and biome physics remain unchanged.",
                ""
            };

            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.Preset, "FLIGHT FEEL", settings.FlightPreset.ToString().ToUpperInvariant());
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.FlightInertia, "FLIGHT INERTIA", FormatFlightInertia(settings.FlightCoastingSetting));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.RotationInertia, "ROTATION INERTIA", settings.FlightRotationInertiaSetting.ToString().ToUpperInvariant());
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.ThrustResponse, "THRUST ACCEL.", FormatThrustAcceleration(settings.FlightThrustResponseSetting));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.GravityResponse, "GRAVITY PULL", FormatGravityPull(settings.FlightGravityResponseSetting));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.ResetDefaults, "RESET", "BALANCED DEFAULTS");
            return string.Join("\n", lines);
        }

        private static void AddPercentLine(List<string> lines, int selectedIndex, int index, string label, int percent)
        {
            AddValueLine(lines, selectedIndex, index, label, $"{BuildBar(percent)} {percent,3}%");
        }

        private static void AddValueLine(List<string> lines, int selectedIndex, int index, string label, string value)
        {
            string marker = selectedIndex == index ? ">" : " ";
            lines.Add($"{marker} {label,-14} {value}");
        }

        private static string BuildBar(int percent)
        {
            int blocks = Math.Clamp((int)MathF.Round(percent / 10f), 0, 10);
            return "[" + new string('#', blocks) + new string('-', 10 - blocks) + "]";
        }

        private static string OnOff(bool value) => value ? "ON" : "OFF";

        private static string FormatThrustAcceleration(FlightThrustResponse value) => value switch
        {
            FlightThrustResponse.Soft => "GENTLE",
            FlightThrustResponse.Quick => "STRONG",
            _ => "NORMAL"
        };

        private static string FormatFlightInertia(FlightCoasting value) => value switch
        {
            FlightCoasting.Short => "LOW",
            FlightCoasting.Long => "HIGH",
            _ => "NORMAL"
        };

        private static string FormatGravityPull(FlightGravityResponse value) => value switch
        {
            FlightGravityResponse.Light => "LIGHT",
            FlightGravityResponse.Strong => "STRONG",
            _ => "NORMAL"
        };

        private static string FormatControlMode(ControlInputMode mode) =>
            mode switch
            {
                ControlInputMode.Mouse => "MOUSE",
                ControlInputMode.XboxController => "XBOX CONTROLLER",
                _ => "KEYBOARD"
            };

        private static string FormatKeyboardKey(string key) =>
            key switch
            {
                "Left" => "LEFT ARROW",
                "Right" => "RIGHT ARROW",
                "Up" => "UP ARROW",
                "Down" => "DOWN ARROW",
                "Space" => "SPACE",
                "RShiftKey" => "RIGHT SHIFT",
                "LShiftKey" => "LEFT SHIFT",
                "Enter" => "ENTER",
                _ => (key ?? "").ToUpperInvariant()
            };

        private static string FormatMouseButton(MouseControlButton button) =>
            button switch
            {
                MouseControlButton.Right => "RIGHT BUTTON",
                MouseControlButton.Middle => "MIDDLE BUTTON",
                _ => "LEFT BUTTON"
            };

        private static string FormatXboxButton(XboxControlButton button) =>
            button switch
            {
                XboxControlButton.LeftShoulder => "LEFT SHOULDER",
                XboxControlButton.RightShoulder => "RIGHT SHOULDER",
                XboxControlButton.LeftTrigger => "LEFT TRIGGER",
                XboxControlButton.RightTrigger => "RIGHT TRIGGER",
                XboxControlButton.DPadUp => "D-PAD UP",
                XboxControlButton.DPadDown => "D-PAD DOWN",
                XboxControlButton.DPadLeft => "D-PAD LEFT",
                XboxControlButton.DPadRight => "D-PAD RIGHT",
                XboxControlButton.LeftStick => "LEFT STICK",
                XboxControlButton.RightStick => "RIGHT STICK",
                XboxControlButton.LeftStickUp => "LEFT STICK UP",
                XboxControlButton.LeftStickDown => "LEFT STICK DOWN",
                XboxControlButton.LeftStickLeft => "LEFT STICK LEFT",
                XboxControlButton.LeftStickRight => "LEFT STICK RIGHT",
                XboxControlButton.RightStickUp => "RIGHT STICK UP",
                XboxControlButton.RightStickDown => "RIGHT STICK DOWN",
                XboxControlButton.RightStickLeft => "RIGHT STICK LEFT",
                XboxControlButton.RightStickRight => "RIGHT STICK RIGHT",
                _ => button.ToString().ToUpperInvariant()
            };
    }
}
