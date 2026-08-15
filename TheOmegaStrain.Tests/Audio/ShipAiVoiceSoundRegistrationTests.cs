using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TheOmegaStrain.Tests.Audio;

[TestClass]
public class ShipAiVoiceSoundRegistrationTests
{
    private static readonly (string Id, string File, string Usage)[] ShipAiVoiceSounds =
    [
        ("ship_ai_clean_loop", "OmegaStrain_clean_loop.mp3", "ShipAiVoiceCleanLoop"),
        ("ship_ai_great_flying", "OmegaStrain_great_flying.mp3", "ShipAiVoiceGreatFlying"),
        ("ship_ai_low_altitude_bonus", "OmegaStrain_low_altitude_bonus.mp3", "ShipAiVoiceLowAltitudeBonus"),
        ("ship_ai_planet_bonus_complete", "OmegaStrain_planet_bonus_reached.mp3", "ShipAiVoicePlanetBonusComplete"),
        ("ship_ai_tutorial_intro", "OmegaStrain_tutorial_intro.mp3", "ShipAiVoiceTutorialIntro"),
        ("ship_ai_tutorial_thrust", "OmegaStrain_tutorial_thrust.mp3", "ShipAiVoiceTutorialThrust"),
        ("ship_ai_tutorial_weapons", "OmegaStrain_tutorial_weapons.mp3", "ShipAiVoiceTutorialWeapons"),
        ("ship_ai_tutorial_seeder_one_down", "OmegaStrain_tutorial_seeder_one_down.mp3", "ShipAiVoiceTutorialSeederOneDown"),
        ("ship_ai_tutorial_powerup", "OmegaStrain_tutorial_powerup1.mp3", "ShipAiVoiceTutorialPowerup"),
        ("ship_ai_tutorial_decoy_select", "OmegaStrain_tutorial_decoy_select.mp3", "ShipAiVoiceTutorialDecoySelect"),
        ("ship_ai_tutorial_drone_inbound", "OmegaStrain_tutorial_drone_inbound.mp3", "ShipAiVoiceTutorialDroneInbound"),
        ("ship_ai_tutorial_drone_destroyed", "OmegaStrain_tutorial_drone_destroyed.mp3", "ShipAiVoiceTutorialDroneDestroyed"),
        ("ship_ai_tutorial_complete", "OmegaStrain_tutorial_complete.mp3", "ShipAiVoiceTutorialComplete"),
        ("ship_ai_tutorial_skip", "OmegaStrain_tutorial_skip.mp3", "ShipAiVoiceTutorialSkip"),
        ("ship_ai_tutorial_warning_low_altitude", "OmegaStrain_tutorial_warning_low_altitude.mp3", "ShipAiVoiceTutorialWarningLowAltitude"),
        ("ship_ai_tutorial_checkpoint", "OmegaStrain_tutorial_checkpoint.mp3", "ShipAiVoiceTutorialCheckpoint"),
        ("ship_ai_tutorial_laser_hint", "OmegaStrain_tutorial_laser_hint.mp3", "ShipAiVoiceTutorialLaserHint"),
        ("ship_ai_tutorial_decoy_hint", "OmegaStrain_tutorial_decoy_hint.mp3", "ShipAiVoiceTutorialDecoyHint")
    ];

    [TestMethod]
    public void ShipAiVoiceSounds_AreRegisteredAndCopiedToOutput()
    {
        var repoRoot = FindRepoRoot();
        Assert.IsNotNull(repoRoot, "Could not locate repository root from test working dir.");

        var soundsJson = Path.Combine(repoRoot!, "TheOmegaStrain.Wpf", "Soundeffects", "sounds.json");
        var audioBasePath = Path.GetDirectoryName(soundsJson)!;
        var registry = new JsonSoundRegistry(soundsJson);
        var frontendProject = XDocument.Load(Path.Combine(repoRoot!, "TheOmegaStrain.Wpf", "TheOmegaStrain.Wpf.csproj"));

        foreach (var voiceSound in ShipAiVoiceSounds)
        {
            Assert.IsTrue(
                registry.TryGet(voiceSound.Id, out var definition),
                $"{voiceSound.Id} is missing from sounds.json.");

            Assert.AreEqual(voiceSound.File, definition.File);
            Assert.AreEqual(voiceSound.Usage, definition.Usage);
            Assert.IsFalse(definition.Settings.Is3D, $"{voiceSound.Id} should play as cockpit AI voice, not as world audio.");
            Assert.IsTrue(
                File.Exists(Path.Combine(audioBasePath, voiceSound.File)),
                $"{voiceSound.File} is missing.");

            var copied = frontendProject
                .Descendants("None")
                .Any(e => string.Equals((string?)e.Attribute("Update"), $@"Soundeffects\{voiceSound.File}", StringComparison.OrdinalIgnoreCase) &&
                          e.Elements("CopyToOutputDirectory").Any(c => string.Equals(c.Value, "Always", StringComparison.OrdinalIgnoreCase)));

            Assert.IsTrue(copied, $"{voiceSound.File} must be copied to output.");
        }
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TheOmegaStrain.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }
}
