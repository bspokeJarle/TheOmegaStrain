using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class OutroFireworksControls : IObjectMovement
    {
        public const string FireworkSoundId = "fireworks";
        // Per-user direction: use only the "fireworks" entry (start=0, end=2.0s).
        // The fireworks_burst_1..4 variants seek into the middle of a ~5MB WAV,
        // which is the ONLY one-shot in the game that does that — and is what
        // was crashing playback after a few bangs. Every other working one-shot
        // in sounds.json (ship_thud, water_splash, mothership_lazer, etc.) has
        // start: 0.0, so we match that shape here.
        public static readonly string[] FireworkSoundIds =
        {
            "fireworks"
        };

        private const float CycleSeconds = 5.2f;
        private const float RiseSeconds = 0.95f;
        private const float ExplosionSeconds = 1.45f;
        private const int ExplosionParticleCount = 28;
        private const float ExplosionPower = 1.38f;

        private static readonly string[] ExplosionColors =
        {
            "FF2D55", "FFD60A", "00E5FF", "7CFF4F", "FF7AF5", "FFFFFF", "FF8C00", "5E5CFF"
        };

        private readonly FireworkBurst[] _bursts =
        {
            new(0f, -520f, 125f, -465f, -355f, 0f, 22f, "FFF4A3", 0),
            new(0.45f, 390f, 132f, 305f, -420f, 16f, 36f, "A8F7FF", 2),
            new(0.95f, -180f, 118f, -240f, -500f, 8f, 52f, "FF9FE7", 4),
            new(1.45f, 555f, 138f, 470f, -320f, -4f, 24f, "D0FF72", 1),
            new(2.05f, 35f, 124f, 80f, -405f, 24f, 44f, "FFFFFF", 5),
            new(2.7f, -390f, 136f, -310f, -285f, -10f, 30f, "FFC56E", 3)
        };

        private IAudioPlayer? _audio;
        private readonly List<SoundDefinition> _fireworkSounds = new();
        private SoundDefinition? _fallbackFireworksSound;
        private float _elapsedSeconds;
        private int _nextSoundIndex;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;

        public OutroFireworksControls()
        {
            for (int i = 0; i < _bursts.Length; i++)
                _bursts[i].Particles = CreateParticleSeeds(i + 1);
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ConfigureAudio(audioPlayer, soundRegistry);

            var part = theObject.ObjectParts.Count > 0 ? theObject.ObjectParts[0] : null;
            if (part == null)
                return theObject;

            _elapsedSeconds += GetDeltaSeconds();
            part.Triangles = CreateFrameTriangles();
            return theObject;
        }

        private List<ITriangleMeshWithColor> CreateFrameTriangles()
        {
            var triangles = new List<ITriangleMeshWithColor>(ExplosionParticleCount * _bursts.Length * 4);
            int cycleIndex = (int)(_elapsedSeconds / CycleSeconds);
            float cycleTime = _elapsedSeconds - (cycleIndex * CycleSeconds);

            for (int i = 0; i < _bursts.Length; i++)
                AddBurstFrame(triangles, _bursts[i], cycleTime - _bursts[i].LaunchDelay, cycleIndex);

            return triangles;
        }

        private void AddBurstFrame(List<ITriangleMeshWithColor> triangles, FireworkBurst burst, float burstTime, int cycleIndex)
        {
            if (burstTime < 0f || burstTime > RiseSeconds + ExplosionSeconds)
                return;

            if (burstTime < RiseSeconds)
            {
                AddLaunchTrail(triangles, burst, burstTime / RiseSeconds);
                return;
            }

            if (burst.LastSoundCycle != cycleIndex)
            {
                PlayFireworkSound();
                burst.LastSoundCycle = cycleIndex;
            }

            AddExplosion(triangles, burst, (burstTime - RiseSeconds) / ExplosionSeconds);
        }

        private static void AddLaunchTrail(List<ITriangleMeshWithColor> triangles, FireworkBurst burst, float progress)
        {
            float eased = SmoothStep(progress);
            float x = Lerp(burst.StartX, burst.PeakX, eased);
            float y = Lerp(burst.StartY, burst.PeakY, eased);
            float z = Lerp(burst.StartZ, burst.PeakZ, eased);

            AddDiamond(triangles, x, y, z, 5.5f, burst.TrailColor);
            for (int i = 1; i <= 5; i++)
            {
                float tail = i / 5f;
                float tailX = Lerp(x, burst.StartX, tail * 0.18f);
                float tailY = y + (tail * 55f);
                float tailZ = Lerp(z, burst.StartZ, tail * 0.35f);
                float size = 4.5f - (tail * 2.7f);
                string color = i % 2 == 0 ? "FF8C3A" : "FFE46A";
                AddDiamond(triangles, tailX, tailY, tailZ, size, color);
            }
        }

        private static void AddExplosion(List<ITriangleMeshWithColor> triangles, FireworkBurst burst, float progress)
        {
            progress = Math.Clamp(progress, 0f, 1f);
            float expansion = SmoothStep(progress);
            float fade = 1f - progress;
            float gravity = progress * progress * 58f;

            for (int i = 0; i < burst.Particles.Length; i++)
            {
                var particle = burst.Particles[i];
                float radius = particle.Speed * ExplosionPower * expansion;
                float wobble = MathF.Sin((progress * 7.5f) + particle.Phase) * 4.5f * fade;
                float x = burst.PeakX + (particle.DirX * radius) + wobble;
                float y = burst.PeakY + (particle.DirY * radius) + gravity;
                float z = burst.PeakZ + (particle.DirZ * radius * 0.55f);
                float size = Math.Max(1.2f, particle.Size * fade);
                string color = ExplosionColors[(i + burst.ColorOffset) % ExplosionColors.Length];
                AddDiamond(triangles, x, y, z, size, color);
            }
        }

        private void PlayFireworkSound()
        {
            if (_audio == null)
                return;

            var sound = GetNextFireworkSound();
            if (sound == null)
                return;

            _audio.PlayOneShot(
                sound,
                new AudioPlayOptions { VolumeOverride = sound.Settings.Volume });
        }

        private SoundDefinition? GetNextFireworkSound()
        {
            if (_fireworkSounds.Count == 0)
                return _fallbackFireworksSound;

            var sound = _fireworkSounds[_nextSoundIndex % _fireworkSounds.Count];
            _nextSoundIndex++;
            return sound;
        }

        private static FireworkParticle[] CreateParticleSeeds(int seed)
        {
            var particles = new FireworkParticle[ExplosionParticleCount];
            for (int i = 0; i < particles.Length; i++)
            {
                float angle = ((i * 137.5f) + (seed * 31f)) * MathF.PI / 180f;
                float lift = MathF.Sin(((i * 53f) + (seed * 19f)) * MathF.PI / 180f) * 0.24f;
                float dirX = MathF.Cos(angle);
                float dirY = (MathF.Sin(angle) * 0.82f) + lift;
                float dirZ = MathF.Sin((angle * 1.7f) + seed) * 0.7f;
                float speed = 96f + (((i * 17f) + (seed * 11f)) % 68f);
                float size = 5.8f + (((i * 7f) + seed) % 5f);
                particles[i] = new FireworkParticle(dirX, dirY, dirZ, speed, size, i * 0.41f + seed);
            }

            return particles;
        }

        private static void AddDiamond(List<ITriangleMeshWithColor> tris, float x, float y, float z, float size, string color)
        {
            var top = new Vector3(x, y - size, z);
            var right = new Vector3(x + size, y, z);
            var bottom = new Vector3(x, y + size, z);
            var left = new Vector3(x - size, y, z);
            var center = new Vector3(x, y, z);

            tris.Add(CreateTri(top, right, center, color));
            tris.Add(CreateTri(right, bottom, center, color));
            tris.Add(CreateTri(bottom, left, center, color));
            tris.Add(CreateTri(left, top, center, color));
        }

        private static TriangleMeshWithColor CreateTri(Vector3 a, Vector3 b, Vector3 c, string color)
        {
            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = a,
                vert2 = b,
                vert3 = c,
                normal1 = new Vector3 { z = 1 },
                normal2 = new Vector3 { z = 1 },
                normal3 = new Vector3 { z = 1 },
                noHidden = true
            };
        }

        private static float GetDeltaSeconds()
        {
            if (GameState.DeltaTime > 0f)
                return Math.Min(GameState.DeltaTime, 0.1f);

            return GameState.GameplayBaselineDeltaTime;
        }

        private static float SmoothStep(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            return value * value * (3f - (2f * value));
        }

        private static float Lerp(float from, float to, float amount)
        {
            return from + ((to - from) * amount);
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            if (_fireworkSounds.Count == 0)
            {
                for (int i = 0; i < FireworkSoundIds.Length; i++)
                {
                    if (soundRegistry.TryGet(FireworkSoundIds[i], out var fireworkSound))
                        _fireworkSounds.Add(fireworkSound);
                }
            }

            if (_fallbackFireworksSound == null && soundRegistry.TryGet(FireworkSoundId, out var fallbackSound))
                _fallbackFireworksSound = fallbackSound;
        }

        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void Dispose() { }

        private sealed class FireworkBurst
        {
            public FireworkBurst(float launchDelay, float startX, float startY, float peakX, float peakY, float startZ, float peakZ, string trailColor, int colorOffset)
            {
                LaunchDelay = launchDelay;
                StartX = startX;
                StartY = startY;
                PeakX = peakX;
                PeakY = peakY;
                StartZ = startZ;
                PeakZ = peakZ;
                TrailColor = trailColor;
                ColorOffset = colorOffset;
            }

            public float LaunchDelay { get; }
            public float StartX { get; }
            public float StartY { get; }
            public float PeakX { get; }
            public float PeakY { get; }
            public float StartZ { get; }
            public float PeakZ { get; }
            public string TrailColor { get; }
            public int ColorOffset { get; }
            public int LastSoundCycle { get; set; } = -1;
            public FireworkParticle[] Particles { get; set; } = Array.Empty<FireworkParticle>();
        }

        private readonly struct FireworkParticle
        {
            public FireworkParticle(float dirX, float dirY, float dirZ, float speed, float size, float phase)
            {
                DirX = dirX;
                DirY = dirY;
                DirZ = dirZ;
                Speed = speed;
                Size = size;
                Phase = phase;
            }

            public float DirX { get; }
            public float DirY { get; }
            public float DirZ { get; }
            public float Speed { get; }
            public float Size { get; }
            public float Phase { get; }
        }
    }
}
