using System;
using System.Numerics;

namespace RetroMesh.Engine
{
    /// <summary>
    /// Abstraction for looking up sound definitions by id.
    /// Typically backed by a JSON file or some in-memory table.
    /// </summary>
    public interface ISoundRegistry
    {
        /// <summary>
        /// Get a sound definition by its id (e.g. "rocket_main").
        /// Throws if not found.
        /// </summary>
        SoundDefinition Get(string id);

        /// <summary>
        /// Try to get a sound definition by its id.
        /// Returns true if found.
        /// </summary>
        bool TryGet(string id, out SoundDefinition definition);
    }

    /// <summary>
    /// How the sound should be interpreted relative to its segments.
    /// </summary>
    public enum AudioPlayMode
    {
        /// <summary>
        /// Play the whole file from start to end once. Ignores loop segment.
        /// </summary>
        OneShot,

        /// <summary>
        /// Play start segment once, loop between LoopStart/LoopEnd,
        /// and optionally play end segment when stopped.
        /// </summary>
        SegmentedLoop
    }

    /// <summary>
    /// Optional parameters used when a sound instance is created.
    /// </summary>
    public sealed class AudioPlayOptions
    {
        /// <summary>
        /// Overrides SoundSettings.Volume if specified (0..1).
        /// </summary>
        public float? VolumeOverride { get; set; }

        /// <summary>
        /// Overrides SoundSpeed.Base/random if specified.
        /// </summary>
        public float? SpeedOverride { get; set; }

        /// <summary>
        /// Explicit stereo pan override in the range [-1, 1].
        /// When omitted, the implementation can derive pan from spatial position instead.
        /// </summary>
        public float? Pan { get; set; }

        /// <summary>
        /// Initial spatial position for the emitter.
        /// </summary>
        public Vector3? WorldPosition { get; set; }

        /// <summary>
        /// Arbitrary tag attached to the sound instance.
        /// Typical usage includes ship ids, enemy ids, or gameplay ownership metadata.
        /// </summary>
        public object? Tag { get; set; }
    }

    /// <summary>
    /// Handle returned when a sound is playing.
    /// Store this on your Ship/Rocket/etc so you can stop or modify it later.
    /// </summary>
    public interface IAudioInstance
    {
        Guid Id { get; }

        /// <summary>
        /// Logical id of the sound, e.g. "rocket_main", "laser_main".
        /// </summary>
        string SoundId { get; }

        bool IsPlaying { get; }
        bool IsLooping { get; }

        void SetVolume(float volume);
        void SetSpeed(float speed);
        void SetWorldPosition(Vector3 position);
        void Stop(bool playEndSegment);
    }

    /// <summary>
    /// High-level audio API used by the game.
    /// Implementation wraps NAudio (or something else) under the hood.
    /// </summary>
    public interface IAudioPlayer
    {
        /// <summary>
        /// Play a sound using the given definition and play mode.
        /// Returns a handle that can be stored and controlled later.
        /// For explosions/laser, use OneShot.
        /// For rocket/thrusters, use SegmentedLoop.
        /// </summary>
        IAudioInstance Play(
            SoundDefinition definition,
            AudioPlayMode mode,
            AudioPlayOptions? options = null);

        /// <summary>
        /// Convenience: play a one-shot (full file) and forget the handle.
        /// Good for explosions, UI clicks, etc.
        /// </summary>
        void PlayOneShot(
            SoundDefinition definition,
            AudioPlayOptions? options = null);

        /// <summary>
        /// Stop a specific instance.
        /// If playEndSegment is true and AudioPlayMode.SegmentedLoop was used,
        /// implementation should jump to the end segment before finishing.
        /// </summary>
        void Stop(IAudioInstance instance, bool playEndSegment);

        /// <summary>
        /// Stop all currently playing sounds.
        /// </summary>
        void StopAll();

        /// <summary>
        /// Stop all currently playing sounds except background music.
        /// </summary>
        void StopNonMusic();

        /// <summary>
        /// Start playing music
        /// </summary>
        void PlayMusic(SoundDefinition definition, float? volumeOverride = null);

        /// <summary>
        /// Current background music volume. Used by voice ducking to restore
        /// the exact volume that was active before speech started.
        /// </summary>
        float MusicVolume { get; }

        /// <summary>
        /// Adjust currently playing background music volume.
        /// </summary>
        void SetMusicVolume(float volume);

        /// <summary>
        /// Stop playing music
        /// </summary>
        void StopMusic();

        /// <summary>
        /// Optionally called every frame if the implementation needs
        /// to advance segment state or other audio state.
        /// </summary>
        void Update(double deltaTimeSeconds);
    }

    // ---------- Audio models used by both game and implementation ----------

    public sealed class SoundDefinition
    {
        /// <summary>
        /// Logical id, e.g. "rocket_main".
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Usage hint, e.g. "Rocket", "Lazer", "ShipExplosion".
        /// </summary>
        public string Usage { get; set; } = string.Empty;

        /// <summary>
        /// File name of the WAV, relative to your audio root path.
        /// </summary>
        public string File { get; set; } = string.Empty;

        public SoundSegments Segments { get; set; } = new();
        public SoundSettings Settings { get; set; } = new();
        public SoundSpeed Speed { get; set; } = new();
    }

    public sealed class SoundSegments
    {
        /// <summary>Seconds from start of file.</summary>
        public double Start { get; set; }

        /// <summary>Seconds where looping can begin.</summary>
        public double LoopStart { get; set; }

        /// <summary>Seconds where looping should end.</summary>
        public double LoopEnd { get; set; }

        /// <summary>Seconds where the sound is effectively done (end-of-tail).</summary>
        public double End { get; set; }
    }

    public sealed class SoundSettings
    {
        public float Volume { get; set; } = 1.0f;
        public int MaxVoices { get; set; } = 4;
        public bool Is3D { get; set; } = true;

        /// <summary>
        /// Higher = more important when voice limits are hit.
        /// </summary>
        public int Priority { get; set; } = 100;
    }

    public sealed class SoundSpeed
    {
        /// <summary>Base playback speed (1.0 = original).</summary>
        public float Base { get; set; } = 1.0f;

        /// <summary>Random variation added to Base. Example: [-0.05, 0.05].</summary>
        public float RandomMin { get; set; } = 0f;
        public float RandomMax { get; set; } = 0f;

        /// <summary>Minimum effective speed when modified at runtime.</summary>
        public float Min { get; set; } = 0.8f;

        /// <summary>Maximum effective speed when modified at runtime.</summary>
        public float Max { get; set; } = 1.4f;

        /// <summary>
        /// Returns a speed value with random variation applied and clamped to [Min, Max].
        /// You can call this each time you play a sound.
        /// </summary>
        public float GetRandomizedSpeed(Random rng)
        {
            var r = (float)(rng.NextDouble() * (RandomMax - RandomMin) + RandomMin);
            var result = Base + r;
            if (result < Min) result = Min;
            if (result > Max) result = Max;
            return result;
        }
    }
}
