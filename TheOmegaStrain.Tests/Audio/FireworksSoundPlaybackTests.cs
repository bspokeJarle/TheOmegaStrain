using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Domain;
using GameAiAndControls.Controls;

namespace _3DSpesificsUnitTests.Audio
{
    /// <summary>
    /// Smoke tests for the fireworks sound IDs used by OutroFireworksControls.
    /// Verifies that
    ///   1. every firework id declared in code is present in sounds.json
    ///   2. each one can actually be loaded and started through the live
    ///      NAudio playback pipeline without throwing.
    /// </summary>
    [TestClass]
    public class FireworksSoundPlaybackTests
    {
        // The firework ids the controller actually plays. We deliberately do
        // NOT test the fireworks_burst_1..4 variants here — those seek into
        // the middle of a 5MB WAV (the only one-shots in the game that do),
        // which is what was crashing live playback after the 3rd bang.
        private static readonly string[] AllFireworkIds =
            OutroFireworksControls.FireworkSoundIds;

        private static string? FindSoundsJson()
        {
            // Walk up from the test bin dir until we find TheOmegaStrain.Wpf\Soundeffects\sounds.json.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "TheOmegaStrain.Wpf", "Soundeffects", "sounds.json");
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        [TestMethod]
        public void AllFireworkIds_AreResolvedByRegistry()
        {
            var jsonPath = FindSoundsJson();
            Assert.IsNotNull(jsonPath, "Could not locate sounds.json from test working dir.");

            var registry = new JsonSoundRegistry(jsonPath!);

            var missing = new List<string>();
            foreach (var id in AllFireworkIds)
            {
                if (!registry.TryGet(id, out _))
                    missing.Add(id);
            }

            Assert.AreEqual(0, missing.Count, "Missing firework ids in registry: " + string.Join(", ", missing));
        }

        [TestMethod]
        public void AllFireworkSounds_CanBePlayedOnceWithoutThrowing()
        {
            var jsonPath = FindSoundsJson();
            Assert.IsNotNull(jsonPath, "Could not locate sounds.json from test working dir.");

            var audioBasePath = Path.GetDirectoryName(jsonPath!)!;
            var registry = new JsonSoundRegistry(jsonPath!);

            using var player = new NAudioAudioPlayer(audioBasePath);

            var failures = new List<string>();
            foreach (var id in AllFireworkIds)
            {
                if (!registry.TryGet(id, out var def))
                {
                    failures.Add($"{id}: not in registry");
                    continue;
                }

                try
                {
                    player.PlayOneShot(def, new AudioPlayOptions { VolumeOverride = 0f });
                }
                catch (Exception ex)
                {
                    failures.Add($"{id}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Give the mixer thread a moment to actually run each one-shot
            // through the pipeline so any decoder/segment errors surface.
            Thread.Sleep(500);

            Assert.AreEqual(0, failures.Count,
                "Firework sound playback failures:\n" + string.Join("\n", failures));
        }

        [TestMethod]
        public void RepeatedFireworksBurst_DoesNotThrow()
        {
            // Reproduces the "crashes after a few bangs" scenario: play the
            // same one-shot repeatedly through the live NAudio pipeline.
            var jsonPath = FindSoundsJson();
            Assert.IsNotNull(jsonPath, "Could not locate sounds.json from test working dir.");

            var audioBasePath = Path.GetDirectoryName(jsonPath!)!;
            var registry = new JsonSoundRegistry(jsonPath!);
            Assert.IsTrue(registry.TryGet("fireworks", out var def));

            using var player = new NAudioAudioPlayer(audioBasePath);

            for (int i = 0; i < 8; i++)
            {
                player.PlayOneShot(def, new AudioPlayOptions { VolumeOverride = 0f });
                Thread.Sleep(120);
            }
        }
    }
}
