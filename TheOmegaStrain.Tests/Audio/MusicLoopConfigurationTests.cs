using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TheOmegaStrain.Tests.Audio
{
    [TestClass]
    public class MusicLoopConfigurationTests
    {
        private const double SegmentToleranceSeconds = 0.01;

        // Do not loop music at exact EOF. Some decoders/mixers can report end-of-stream
        // before the loop seek gets a clean chance, which makes the song stop instead of loop.
        private const double MusicLoopSafetyMarginSeconds = 2.0;

        [TestMethod]
        public void ConnectedMusicDefinitions_LoopSafelyBeforeActualWavEnd()
        {
            var jsonPath = FindSoundsJson();
            Assert.IsNotNull(jsonPath, "Could not locate sounds.json from test working dir.");

            var audioBasePath = Path.GetDirectoryName(jsonPath!)!;
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath!));

            var failures = new List<string>();
            var musicCount = 0;

            foreach (var sound in document.RootElement.GetProperty("sounds").EnumerateArray())
            {
                var id = sound.GetProperty("id").GetString() ?? string.Empty;
                if (!id.StartsWith("music_", StringComparison.OrdinalIgnoreCase))
                    continue;

                musicCount++;

                var file = sound.GetProperty("file").GetString() ?? string.Empty;
                if (!file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{id}: connected music should stay on WAV, got '{file}'.");
                    continue;
                }

                var fullPath = Path.Combine(audioBasePath, file);
                if (!File.Exists(fullPath))
                {
                    failures.Add($"{id}: missing audio file '{file}'.");
                    continue;
                }

                var duration = ReadWavDurationSeconds(fullPath);
                var segments = sound.GetProperty("segments");

                var start = segments.GetProperty("start").GetDouble();
                var loopStart = segments.GetProperty("loopStart").GetDouble();
                var loopEnd = segments.GetProperty("loopEnd").GetDouble();
                var end = segments.GetProperty("end").GetDouble();

                ExpectClose(failures, id, "start", 0.0, start);
                ExpectClose(failures, id, "loopStart", 0.0, loopStart);
                var safeLoopEnd = Math.Max(0.0, duration - MusicLoopSafetyMarginSeconds);

                ExpectClose(failures, id, "loopEnd", safeLoopEnd, loopEnd);
                ExpectClose(failures, id, "end", safeLoopEnd, end);
            }

            Assert.IsTrue(musicCount > 0, "No music definitions found in sounds.json.");
            Assert.AreEqual(0, failures.Count, "Music loop configuration mismatches:\n" + string.Join("\n", failures));
        }

        private static string? FindSoundsJson()
        {
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

        private static void ExpectClose(List<string> failures, string id, string field, double expected, double actual)
        {
            if (Math.Abs(expected - actual) <= SegmentToleranceSeconds)
                return;

            failures.Add($"{id}: {field}={actual:F3}, expected {expected:F3}.");
        }

        private static double ReadWavDurationSeconds(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

            var riff = ReadChunkId(reader);
            if (!string.Equals(riff, "RIFF", StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported WAV header in {path}.");

            _ = reader.ReadUInt32();

            var wave = ReadChunkId(reader);
            if (!string.Equals(wave, "WAVE", StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported WAV format in {path}.");

            uint? byteRate = null;
            uint? dataSize = null;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = ReadChunkId(reader);
                var chunkSize = reader.ReadUInt32();
                var chunkStart = stream.Position;

                if (string.Equals(chunkId, "fmt ", StringComparison.Ordinal))
                {
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt16();
                    _ = reader.ReadUInt32();
                    byteRate = reader.ReadUInt32();
                }
                else if (string.Equals(chunkId, "data", StringComparison.Ordinal))
                {
                    dataSize = chunkSize;
                }

                var nextChunk = chunkStart + chunkSize + (chunkSize % 2);
                stream.Position = Math.Min(nextChunk, stream.Length);

                if (byteRate.HasValue && dataSize.HasValue)
                    return dataSize.Value / (double)byteRate.Value;
            }

            throw new InvalidDataException($"Could not read WAV duration for {path}.");
        }

        private static string ReadChunkId(BinaryReader reader)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(4));
        }
    }
}
