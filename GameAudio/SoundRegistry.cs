using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GameAudioInstances
{
    /// <summary>
    /// Simple ISoundRegistry implementation that loads all sound definitions
    /// from a sounds.json file once and keeps them in memory.
    /// </summary>
    public sealed class JsonSoundRegistry : ISoundRegistry
    {
        private readonly Dictionary<string, SoundDefinition> _byId;

        public JsonSoundRegistry(string jsonPath)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"sounds.json not found at: {jsonPath}");

            var json = File.ReadAllText(jsonPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var root = JsonSerializer.Deserialize<SoundConfigRoot>(json, options)
                       ?? throw new InvalidOperationException("Failed to deserialize sounds.json");

            _byId = new Dictionary<string, SoundDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in root.Sounds)
            {
                if (string.IsNullOrWhiteSpace(s.Id))
                    continue;

                _byId[s.Id] = s;
            }
        }

        public SoundDefinition Get(string id)
        {
            if (_byId.TryGetValue(id, out var def))
                return def;

            throw new KeyNotFoundException($"No sound with id '{id}' in JsonSoundRegistry.");
        }

        public bool TryGet(string id, out SoundDefinition definition)
        {
            return _byId.TryGetValue(id, out definition!);
        }
    }

    /// <summary>
    /// JSON root type that matches:
    /// { "sounds": [ { ...SoundDefinition... }, ... ] }
    /// </summary>
    internal sealed class SoundConfigRoot
    {
        public List<SoundDefinition> Sounds { get; set; } = new();
    }
}
