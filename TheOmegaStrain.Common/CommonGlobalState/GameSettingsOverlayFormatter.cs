using TheOmegaStrain.Common.CommonGlobalState.States;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Common.CommonGlobalState
{
    public static class GameSettingsOverlayFormatter
    {
        public const string Footer = "UP/DOWN SELECT  //  LEFT/RIGHT ADJUST  //  ENTER OR ESC TO CLOSE";

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
            AddPercentLine(lines, selectedIndex, (int)GraphicsSettingsField.ParticleDensity, "PARTICLES", settings.ParticleDensityPercent);
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.GlowEffects, "GLOW", OnOff(settings.GlowEffectsEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.EnhancedWeather, "WEATHER FX", OnOff(settings.EnhancedWeatherEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.EnhancedShadows, "SHADOWS", OnOff(settings.EnhancedShadowsEnabled));

            return string.Join("\n", lines);
        }

        public static string BuildControlsBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();

            var lines = new List<string>
            {
                "Choose one active control source. Keyboard weapon keys 1/2/3 always stay live.",
                ""
            };

            AddValueLine(lines, selectedIndex, 0, "CONTROL TYPE", FormatControlMode(settings.ActiveControlScheme));

            switch (settings.ActiveControlScheme)
            {
                case ControlInputMode.Mouse:
                    AddValueLine(lines, selectedIndex, 1, "THRUST", FormatMouseButton(settings.MouseThrustButton));
                    AddValueLine(lines, selectedIndex, 2, "FIRE", FormatMouseButton(settings.MouseFireButton));
                    AddValueLine(lines, selectedIndex, -1, "STEER", "MOUSE MOVE");
                    break;
                case ControlInputMode.XboxController:
                    AddValueLine(lines, selectedIndex, 1, "THRUST", FormatXboxButton(settings.XboxThrustButton));
                    AddValueLine(lines, selectedIndex, 2, "FIRE", FormatXboxButton(settings.XboxFireButton));
                    AddValueLine(lines, selectedIndex, 3, "PITCH UP", FormatXboxButton(settings.XboxPitchUpButton));
                    AddValueLine(lines, selectedIndex, 4, "PITCH DOWN", FormatXboxButton(settings.XboxPitchDownButton));
                    AddValueLine(lines, selectedIndex, 5, "TURN LEFT", FormatXboxButton(settings.XboxTurnLeftButton));
                    AddValueLine(lines, selectedIndex, 6, "TURN RIGHT", FormatXboxButton(settings.XboxTurnRightButton));
                    AddValueLine(lines, selectedIndex, 7, "POWERUP 1", FormatXboxButton(settings.XboxBulletButton));
                    AddValueLine(lines, selectedIndex, 8, "POWERUP 2", FormatXboxButton(settings.XboxDecoyButton));
                    AddValueLine(lines, selectedIndex, 9, "POWERUP 3", FormatXboxButton(settings.XboxLazerButton));
                    AddValueLine(lines, selectedIndex, 10, "POWERUP 4", FormatXboxButton(settings.XboxPowerup4Button));
                    break;
                default:
                    AddValueLine(lines, selectedIndex, 1, "THRUST", FormatKeyboardKey(settings.KeyboardThrustKey));
                    AddValueLine(lines, selectedIndex, 2, "FIRE", FormatKeyboardKey(settings.KeyboardFireKey));
                    AddValueLine(lines, selectedIndex, 3, "PITCH UP", FormatKeyboardKey(settings.KeyboardPitchUpKey));
                    AddValueLine(lines, selectedIndex, 4, "PITCH DOWN", FormatKeyboardKey(settings.KeyboardPitchDownKey));
                    AddValueLine(lines, selectedIndex, 5, "TURN LEFT", FormatKeyboardKey(settings.KeyboardTurnLeftKey));
                    AddValueLine(lines, selectedIndex, 6, "TURN RIGHT", FormatKeyboardKey(settings.KeyboardTurnRightKey));
                    break;
            }

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
