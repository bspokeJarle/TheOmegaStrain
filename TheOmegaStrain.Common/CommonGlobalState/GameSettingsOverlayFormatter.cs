using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonGlobalState.States;
using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.Localization;

namespace TheOmegaStrain.Common.CommonGlobalState
{
    public static class GameSettingsOverlayFormatter
    {
        private static string T(GameSettingsState settings, string key) =>
            GameText.Get("settings." + key, settings.LanguageCode);

        public static string BuildFooter(GameSettingsState settings, bool pageNavigationSelected = false)
        {
            settings.Normalize();
            if (pageNavigationSelected)
            {
                return settings.EffectiveControlScheme == ControlInputMode.XboxController
                    ? T(settings, "footerPageController")
                    : T(settings, "footerPageKeyboard");
            }

            return settings.EffectiveControlScheme == ControlInputMode.XboxController
                ? T(settings, "footerEditController")
                : T(settings, "footerEditKeyboard");
        }

        public static string BuildAudioBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();

            var lines = new List<string>
            {
                T(settings, "audioDescription"),
                ""
            };

            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.MasterVolume, T(settings, "master"), settings.MasterVolumePercent);
            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.MusicVolume, T(settings, "music"), settings.MusicVolumePercent);
            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.EffectsVolume, T(settings, "effects"), settings.EffectsVolumePercent);
            AddPercentLine(lines, selectedIndex, (int)AudioSettingsField.VoiceVolume, T(settings, "voice"), settings.VoiceVolumePercent);

            return string.Join("\n", lines);
        }

        public static string BuildGraphicsBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();

            var lines = new List<string>
            {
                T(settings, "graphicsDescription"),
                ""
            };

            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.QualityPreset, T(settings, "quality"), T(settings, settings.GraphicsQuality.ToString().ToLowerInvariant()));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.CameraAngle, T(settings, "cameraAngle"), T(settings, settings.CameraAngle.ToString().ToLowerInvariant()));
            AddPercentLine(lines, selectedIndex, (int)GraphicsSettingsField.ParticleDensity, T(settings, "particles"), settings.ParticleDensityPercent);
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.GlowEffects, T(settings, "glow"), OnOff(settings, settings.GlowEffectsEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.EnhancedWeather, T(settings, "weather"), OnOff(settings, settings.EnhancedWeatherEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.EnhancedShadows, T(settings, "shadows"), OnOff(settings, settings.EnhancedShadowsEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.SceneRasterBackground, T(settings, "skyRaster"), OnOff(settings, settings.SceneRasterBackgroundEnabled));
            AddValueLine(lines, selectedIndex, (int)GraphicsSettingsField.Clouds, T(settings, "clouds"), OnOff(settings, settings.CloudsEnabled));

            return string.Join("\n", lines);
        }

        public static string BuildControlsBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();

            var lines = new List<string>
            {
                T(settings, "controlsDescription"),
                T(settings, "weaponKeysHint"),
                ""
            };

            AddValueLine(lines, selectedIndex, 0, T(settings, "playUsing"), FormatControlMode(settings, settings.ActiveControlScheme));
            AddValueLine(lines, selectedIndex, 1, T(settings, "configure"), FormatControlMode(settings, settings.ControlsEditorScheme));
            lines.Add("");
            lines.Add(GameText.Format("settings.mappings", settings.LanguageCode, ("device", FormatControlMode(settings, settings.ControlsEditorScheme))));

            switch (settings.ControlsEditorScheme)
            {
                case ControlInputMode.Mouse:
                    AddValueLine(lines, selectedIndex, 2, T(settings, "thrust"), FormatMouseButton(settings.MouseThrustButton));
                    AddValueLine(lines, selectedIndex, 3, T(settings, "fire"), FormatMouseButton(settings.MouseFireButton));
                    AddValueLine(lines, selectedIndex, -1, T(settings, "steer"), T(settings, "mouseMove"));
                    break;
                case ControlInputMode.XboxController:
                    AddValueLine(lines, selectedIndex, 2, T(settings, "thrust"), FormatXboxButton(settings.XboxThrustButton));
                    AddValueLine(lines, selectedIndex, 3, T(settings, "fire"), FormatXboxButton(settings.XboxFireButton));
                    AddValueLine(lines, selectedIndex, 4, T(settings, "pitchUp"), FormatXboxButton(settings.XboxPitchUpButton));
                    AddValueLine(lines, selectedIndex, 5, T(settings, "pitchDown"), FormatXboxButton(settings.XboxPitchDownButton));
                    AddValueLine(lines, selectedIndex, 6, T(settings, "turnLeft"), FormatXboxButton(settings.XboxTurnLeftButton));
                    AddValueLine(lines, selectedIndex, 7, T(settings, "turnRight"), FormatXboxButton(settings.XboxTurnRightButton));
                    AddValueLine(lines, selectedIndex, 8, T(settings, "bulletPowerup"), FormatXboxButton(settings.XboxBulletButton));
                    AddValueLine(lines, selectedIndex, 9, T(settings, "decoy"), FormatXboxButton(settings.XboxDecoyButton));
                    AddValueLine(lines, selectedIndex, 10, T(settings, "laserPowerup"), FormatXboxButton(settings.XboxLazerButton));
                    AddValueLine(lines, selectedIndex, 11, T(settings, "specialPowerup"), FormatXboxButton(settings.XboxPowerup4Button));
                    break;
                default:
                    AddValueLine(lines, selectedIndex, 2, T(settings, "thrust"), FormatKeyboardKey(settings.KeyboardThrustKey));
                    AddValueLine(lines, selectedIndex, 3, T(settings, "fire"), FormatKeyboardKey(settings.KeyboardFireKey));
                    AddValueLine(lines, selectedIndex, 4, T(settings, "pitchUp"), FormatKeyboardKey(settings.KeyboardPitchUpKey));
                    AddValueLine(lines, selectedIndex, 5, T(settings, "pitchDown"), FormatKeyboardKey(settings.KeyboardPitchDownKey));
                    AddValueLine(lines, selectedIndex, 6, T(settings, "turnLeft"), FormatKeyboardKey(settings.KeyboardTurnLeftKey));
                    AddValueLine(lines, selectedIndex, 7, T(settings, "turnRight"), FormatKeyboardKey(settings.KeyboardTurnRightKey));
                    break;
            }

            return string.Join("\n", lines);
        }

        public static string BuildFlightBody(GameSettingsState settings, int selectedIndex)
        {
            settings.Normalize();
            var lines = new List<string>
            {
                T(settings, "flightDescription"),
                ""
            };

            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.Preset, T(settings, "flightFeel"), T(settings, settings.FlightPreset.ToString().ToLowerInvariant()));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.FlightInertia, T(settings, "flightInertia"), T(settings, FormatFlightInertia(settings.FlightCoastingSetting).ToLowerInvariant()));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.RotationInertia, T(settings, "rotationInertia"), T(settings, settings.FlightRotationInertiaSetting.ToString().ToLowerInvariant()));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.ThrustResponse, T(settings, "thrustAccel"), T(settings, FormatThrustAcceleration(settings.FlightThrustResponseSetting).ToLowerInvariant()));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.GravityResponse, T(settings, "gravityPull"), T(settings, FormatGravityPull(settings.FlightGravityResponseSetting).ToLowerInvariant()));
            AddValueLine(lines, selectedIndex, (int)FlightSettingsField.ResetDefaults, T(settings, "reset"), T(settings, "balancedDefaults"));
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

        private static string OnOff(GameSettingsState settings, bool value) => T(settings, value ? "on" : "off");

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

        private static string FormatControlMode(GameSettingsState settings, ControlInputMode mode) =>
            mode switch
            {
                ControlInputMode.Mouse => T(settings, "mouse"),
                ControlInputMode.XboxController => T(settings, "xboxController"),
                _ => T(settings, "keyboard")
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
