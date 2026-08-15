using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls.Weather;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public sealed class LightningControls : IObjectMovement
    {
        public const int MaxActiveBolts = 3;
        public const float MinDepthZ = 650f;
        public const float MaxDepthZ = 1850f;
        public const int MaxMainSegmentsPerBolt = 26;
        private const int MaxBranchSegmentsPerBolt = 10;
        private const int TrianglesPerSegment = 2;
        public const int TrianglesPerBolt = (MaxMainSegmentsPerBolt + MaxBranchSegmentsPerBolt) * TrianglesPerSegment;
        public const int TargetTriangleCount = MaxActiveBolts * TrianglesPerBolt;

        private const string LightningColor = "E8FBFF";
        private const float StrikeGrowthSegmentsPerSecond = 58f;
        private const float HoldAfterScreenCoverageSeconds = 0.09f;
        private const float FadeOutPerSecond = 5.8f;
        private const float FlashDecayPerSecond = 1.85f;
        private const float TravelBehindRecycleDistance = 1500f;
        private const float TravelAheadRecycleDistance = 2600f;

        private static readonly string[] MainColorRamp = BuildColorRamp(0.64f, 1f);
        private static readonly string[] BranchColorRamp = BuildColorRamp(0.42f, 0.82f);

        private static readonly WeatherFieldSettings FieldSettings = new(
            DepthStartZ: MinDepthZ,
            VisibleDepthSpread: MaxDepthZ - MinDepthZ,
            BehindSpread: 1400f,
            AheadSpread: 2200f,
            OffscreenMargin: 460f,
            DirectionalSpawnAheadMin: 950f,
            DirectionalSpawnAheadMax: 2300f,
            DirectionalLateralSpreadFactor: 0.8f,
            TravelBehindRecycleDistance: TravelBehindRecycleDistance,
            TravelAheadRecycleDistance: TravelAheadRecycleDistance,
            DirectionalSpawnModulo: 4,
            VisibleSpreadScreenMultiplier: 0.85f,
            WorldSpreadScreenMultiplier: 2.4f,
            OutsideSpawnChance: 0.45d);

        private readonly Random _random = new();
        private readonly WorldWeatherField _weatherField;
        private readonly List<LightningBolt> _bolts = new(MaxActiveBolts);
        private float _secondsUntilNextStrike;
        private bool _audioConfigured;
        private IAudioPlayer? _audio;
        private SoundDefinition? _thunderOneSound;
        private SoundDefinition? _thunderTwoSound;
        private int _nextThunderIndex;

        public LightningControls()
        {
            _weatherField = new WorldWeatherField(_random, FieldSettings);
        }

        public int ActiveBoltCount => _bolts.Count;
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject? ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            ConfigureAudio(audioPlayer, soundRegistry);

            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            WorldWeatherField.SyncEmitterToGround(theObject, mapPosition);
            _weatherField.UpdateTravelDirection(mapPosition);

            float deltaSeconds = GetDeltaSeconds();
            GameState.WeatherVisualState.DecayLightningFlash(FlashDecayPerSecond * deltaSeconds);

            EnsureTriangleBuffer(theObject);
            UpdateBolts(deltaSeconds, mapPosition);
            TrySpawnBolts(deltaSeconds, mapPosition);
            WriteBolts(theObject, mapPosition);
            RaiseAmbientFlashForVisibleBolts();

            return theObject;
        }

        private void UpdateBolts(float deltaSeconds, IVector3 mapPosition)
        {
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var bolt = _bolts[i];

                if (ShouldRecycleBolt(bolt, mapPosition))
                {
                    _bolts.RemoveAt(i);
                    continue;
                }

                if (!bolt.HasCoveredScreen)
                {
                    bolt.VisibleMainSegments += StrikeGrowthSegmentsPerSecond * deltaSeconds;
                    if (bolt.VisibleMainSegments >= bolt.MainSegmentCount)
                    {
                        bolt.VisibleMainSegments = bolt.MainSegmentCount;
                        bolt.HasCoveredScreen = true;
                    }
                }
                else if (bolt.HoldSeconds > 0f)
                {
                    bolt.HoldSeconds -= deltaSeconds;
                }
                else
                {
                    bolt.Opacity -= FadeOutPerSecond * deltaSeconds;
                }

                if (bolt.Opacity <= 0.02f)
                    _bolts.RemoveAt(i);
            }
        }

        private void TrySpawnBolts(float deltaSeconds, IVector3 mapPosition)
        {
            _secondsUntilNextStrike -= deltaSeconds;
            if (_secondsUntilNextStrike > 0f || _bolts.Count >= MaxActiveBolts)
                return;

            int availableSlots = MaxActiveBolts - _bolts.Count;
            int clusterCount = _random.NextDouble() < 0.22d
                ? Math.Min(availableSlots, _random.Next(2, 4))
                : 1;

            for (int i = 0; i < clusterCount; i++)
            {
                var bolt = CreateBolt(mapPosition);
                _bolts.Add(bolt);
                GameState.WeatherVisualState.RaiseLightningFlash(bolt.FlashIntensity);
            }

            PlayNextThunder();
            _secondsUntilNextStrike = RandomRange(1.35f, 4.4f);
        }

        private LightningBolt CreateBolt(IVector3 mapPosition)
        {
            var spawnOffset = GetSpawnOffset();
            float endY = GetEndGuideYOffset();
            float startY = -ScreenSetup.screenSizeY * RandomRange(0.68f, 0.86f);
            float targetY = endY + RandomRange(-35f, 80f);
            int jagCount = _random.Next(11, MaxMainSegmentsPerBolt);

            var points = new List<LightningPoint>(MaxMainSegmentsPerBolt + 1);
            float currentX = RandomRange(-ScreenSetup.screenSizeX * 0.42f, ScreenSetup.screenSizeX * 0.42f);
            float currentY = startY - RandomRange(0f, 90f);
            points.Add(new LightningPoint(currentX, currentY));

            for (int i = 0; i < jagCount && points.Count <= MaxMainSegmentsPerBolt; i++)
            {
                float remainingY = targetY - currentY;
                if (remainingY <= 0f)
                    break;

                float baseStep = Math.Max(44f, remainingY / Math.Max(1, jagCount - i));
                bool upwardJag = i > 1 && remainingY > baseStep * 2.2f && _random.NextDouble() < 0.2d;
                float dy = upwardJag
                    ? -RandomRange(20f, 64f)
                    : RandomRange(baseStep * 0.76f, baseStep * 1.42f);
                float angleDegrees = RandomRange(-54f, 54f);
                float dx = MathF.Tan(ToRadians(angleDegrees)) * Math.Abs(dy) * 0.42f + RandomRange(-38f, 38f);

                if (upwardJag)
                    dx *= 0.75f;

                currentX += dx;
                currentY += dy;
                points.Add(new LightningPoint(currentX, currentY));
            }

            if (points.Count <= MaxMainSegmentsPerBolt && points[^1].Y < targetY)
            {
                points.Add(new LightningPoint(points[^1].X + RandomRange(-80f, 80f), targetY));
            }
            else if (points[^1].Y < targetY)
            {
                points[^1] = new LightningPoint(points[^1].X + RandomRange(-60f, 60f), targetY);
            }

            var bolt = new LightningBolt
            {
                WorldX = mapPosition.x + spawnOffset.x,
                WorldZ = mapPosition.z + spawnOffset.z,
                MainSegmentCount = points.Count - 1,
                HoldSeconds = HoldAfterScreenCoverageSeconds,
                Opacity = 1f,
                FlashIntensity = RandomRange(0.48f, 0.95f)
            };

            for (int i = 0; i < points.Count - 1; i++)
            {
                bolt.Segments.Add(new LightningSegment(
                    points[i],
                    points[i + 1],
                    RandomRange(3.8f, 8.5f),
                    activationMainSegment: i + 1,
                    isBranch: false));
            }

            AddBranches(bolt, points);
            return bolt;
        }

        private (float x, float z) GetSpawnOffset()
        {
            if (_weatherField.ShouldSpawnAhead())
            {
                var ahead = _weatherField.GetDirectionalSpawnOffset();
                return (
                    ahead.x + RandomRange(-ScreenSetup.screenSizeX * 0.45f, ScreenSetup.screenSizeX * 0.45f),
                    Math.Clamp(ahead.z, MinDepthZ, MaxDepthZ));
            }

            var ambient = _weatherField.GetAmbientSpawnOffset(preferOutside: false);
            return (
                ambient.x,
                Math.Clamp(ambient.z, MinDepthZ, MaxDepthZ));
        }

        private void AddBranches(LightningBolt bolt, List<LightningPoint> mainPoints)
        {
            if (mainPoints.Count < 6)
                return;

            int branchCount = _random.Next(0, 3);
            int branchSegmentsLeft = MaxBranchSegmentsPerBolt;

            for (int i = 0; i < branchCount && branchSegmentsLeft > 0; i++)
            {
                int anchorIndex = _random.Next(2, mainPoints.Count - 2);
                var anchor = mainPoints[anchorIndex];
                int segments = Math.Min(branchSegmentsLeft, _random.Next(2, 5));
                float direction = _random.NextDouble() < 0.5d ? -1f : 1f;
                var previous = anchor;

                for (int s = 0; s < segments; s++)
                {
                    var next = new LightningPoint(
                        previous.X + direction * RandomRange(55f, 150f) + RandomRange(-25f, 25f),
                        previous.Y + RandomRange(35f, 115f));

                    bolt.Segments.Add(new LightningSegment(
                        previous,
                        next,
                        RandomRange(1.8f, 4.4f),
                        activationMainSegment: anchorIndex + s,
                        isBranch: true));

                    previous = next;
                    branchSegmentsLeft--;
                    if (branchSegmentsLeft <= 0)
                        break;
                }
            }
        }

        private void EnsureTriangleBuffer(I3dObject theObject)
        {
            var part = GetLightningPart(theObject);
            if (part.Triangles.Count == TargetTriangleCount)
                return;

            part.Triangles.Clear();
            for (int i = 0; i < TargetTriangleCount; i++)
                part.Triangles.Add(CreateLightningTriangle());
        }

        private static I3dObjectPart GetLightningPart(I3dObject theObject)
        {
            var part = theObject.ObjectParts.FirstOrDefault(p => p.PartName == "LightningBolts");
            if (part != null)
                return part;

            part = new _3dObjectPart
            {
                PartName = "LightningBolts",
                IsVisible = true
            };
            theObject.ObjectParts.Add(part);
            return part;
        }

        private void WriteBolts(I3dObject theObject, IVector3 mapPosition)
        {
            var triangles = GetLightningPart(theObject).Triangles;
            int triangleIndex = 0;
            float objectZ = theObject.ObjectOffsets?.z ?? 0f;

            for (int boltIndex = 0; boltIndex < _bolts.Count && triangleIndex < triangles.Count - 1; boltIndex++)
            {
                var bolt = _bolts[boltIndex];
                float relativeX = bolt.WorldX - mapPosition.x;
                float relativeZ = bolt.WorldZ - mapPosition.z;
                float scale = WorldWeatherField.GetProjectionScale(relativeZ, objectZ);
                int visibleMainSegments = Math.Min(bolt.MainSegmentCount, (int)MathF.Floor(bolt.VisibleMainSegments));

                for (int segmentIndex = 0; segmentIndex < bolt.Segments.Count && triangleIndex < triangles.Count - 1; segmentIndex++)
                {
                    var segment = bolt.Segments[segmentIndex];
                    if (segment.ActivationMainSegment > visibleMainSegments)
                        continue;

                    WriteSegment(
                        triangles[triangleIndex],
                        triangles[triangleIndex + 1],
                        segment,
                        relativeX,
                        relativeZ,
                        scale,
                        bolt.Opacity);

                    triangleIndex += TrianglesPerSegment;
                }
            }

            for (int i = triangleIndex; i < triangles.Count; i++)
                CollapseTriangle(triangles[i]);
        }

        private static void WriteSegment(
            ITriangleMeshWithColor first,
            ITriangleMeshWithColor second,
            LightningSegment segment,
            float relativeX,
            float relativeZ,
            float scale,
            float opacity)
        {
            if (opacity <= 0.02f)
            {
                CollapseTriangle(first);
                CollapseTriangle(second);
                return;
            }

            float startX = (relativeX + segment.Start.X) / scale;
            float startY = segment.Start.Y / scale;
            float endX = (relativeX + segment.End.X) / scale;
            float endY = segment.End.Y / scale;
            float dx = endX - startX;
            float dy = endY - startY;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            if (length <= 0.001f)
            {
                CollapseTriangle(first);
                CollapseTriangle(second);
                return;
            }

            float halfWidth = (segment.Width * (0.7f + opacity * 0.3f)) / scale;
            float normalX = -dy / length * halfWidth;
            float normalY = dx / length * halfWidth;
            string color = GetLightningColor(opacity, segment.IsBranch);

            WriteTriangle(first, startX - normalX, startY - normalY, startX + normalX, startY + normalY, endX + normalX, endY + normalY, relativeZ, color);
            WriteTriangle(second, startX - normalX, startY - normalY, endX + normalX, endY + normalY, endX - normalX, endY - normalY, relativeZ, color);
        }

        private static void WriteTriangle(
            ITriangleMeshWithColor triangle,
            float x1,
            float y1,
            float x2,
            float y2,
            float x3,
            float y3,
            float z,
            string color)
        {
            triangle.Color = color;
            triangle.noHidden = true;
            triangle.angle = 1f;

            triangle.vert1.x = x1;
            triangle.vert1.y = y1;
            triangle.vert1.z = z;

            triangle.vert2.x = x2;
            triangle.vert2.y = y2;
            triangle.vert2.z = z;

            triangle.vert3.x = x3;
            triangle.vert3.y = y3;
            triangle.vert3.z = z;
        }

        private static void CollapseTriangle(ITriangleMeshWithColor triangle)
        {
            triangle.Color = "000000";
            triangle.angle = 0f;
            triangle.noHidden = true;
            triangle.vert1.x = triangle.vert2.x = triangle.vert3.x = 0f;
            triangle.vert1.y = triangle.vert2.y = triangle.vert3.y = 0f;
            triangle.vert1.z = triangle.vert2.z = triangle.vert3.z = 0f;
        }

        private bool ShouldRecycleBolt(LightningBolt bolt, IVector3 mapPosition)
        {
            float relativeX = bolt.WorldX - mapPosition.x;
            float relativeZ = bolt.WorldZ - mapPosition.z;

            return MathF.Abs(relativeX) > _weatherField.HalfWorldSpread + FieldSettings.OffscreenMargin
                || relativeZ < -TravelBehindRecycleDistance
                || relativeZ > ScreenSetup.RenderFarZ + TravelAheadRecycleDistance;
        }

        private void RaiseAmbientFlashForVisibleBolts()
        {
            if (_bolts.Count == 0)
                return;

            float activeOpacity = 0f;
            for (int i = 0; i < _bolts.Count; i++)
                activeOpacity = Math.Max(activeOpacity, _bolts[i].Opacity);

            GameState.WeatherVisualState.RaiseLightningFlash(activeOpacity * 0.18f);
        }

        private static ITriangleMeshWithColor CreateLightningTriangle()
        {
            return new TriangleMeshWithColor
            {
                Color = "000000",
                noHidden = true,
                angle = 1f,
                vert1 = new Vector3(),
                vert2 = new Vector3(),
                vert3 = new Vector3()
            };
        }

        private static float GetEndGuideYOffset()
        {
            var surfaceY = GameState.SurfaceState.SurfaceViewportObject?.ObjectOffsets?.y;
            if (surfaceY.HasValue)
                return surfaceY.Value - 18f;

            return ScreenSetup.screenSizeY * 0.46f;
        }

        private static float GetDeltaSeconds()
        {
            float deltaSeconds = GameState.DeltaTime;
            if (deltaSeconds <= 0f || deltaSeconds > 0.25f)
                return GameState.GameplayBaselineDeltaTime;

            return deltaSeconds;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private static float ToRadians(float degrees)
        {
            return degrees * MathF.PI / 180f;
        }

        private static string GetLightningColor(float opacity, bool isBranch)
        {
            var ramp = isBranch ? BranchColorRamp : MainColorRamp;
            int index = (int)MathF.Round(Math.Clamp(opacity, 0f, 1f) * (ramp.Length - 1));
            return ramp[index];
        }

        private static string[] BuildColorRamp(float minFactor, float maxFactor)
        {
            var colors = new string[9];
            for (int i = 0; i < colors.Length; i++)
            {
                float t = i / (float)(colors.Length - 1);
                colors[i] = ScaleHexColor(LightningColor, minFactor + (maxFactor - minFactor) * t);
            }

            return colors;
        }

        private static string ScaleHexColor(string color, float factor)
        {
            factor = Math.Clamp(factor, 0f, 1f);

            ReadOnlySpan<char> span = string.IsNullOrEmpty(color)
                ? "000000".AsSpan()
                : color.AsSpan().TrimStart('#');

            if (span.Length < 6)
                return "000000";

            int r = Math.Clamp((int)(ParseHexByte(span[0], span[1]) * factor), 0, 255);
            int g = Math.Clamp((int)(ParseHexByte(span[2], span[3]) * factor), 0, 255);
            int b = Math.Clamp((int)(ParseHexByte(span[4], span[5]) * factor), 0, 255);

            return string.Create(6, (r, g, b), static (dst, rgb) =>
            {
                const string hex = "0123456789ABCDEF";
                dst[0] = hex[rgb.r >> 4];
                dst[1] = hex[rgb.r & 0xF];
                dst[2] = hex[rgb.g >> 4];
                dst[3] = hex[rgb.g & 0xF];
                dst[4] = hex[rgb.b >> 4];
                dst[5] = hex[rgb.b & 0xF];
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHexByte(char hi, char lo)
            => (HexDigit(hi) << 4) | HexDigit(lo);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }

        private void PlayNextThunder()
        {
            if (_audio == null)
                return;

            var thunderSound = GetNextThunderSound();
            if (thunderSound == null)
                return;

            _audio.PlayOneShot(
                thunderSound,
                new AudioPlayOptions { VolumeOverride = thunderSound.Settings.Volume });
        }

        private SoundDefinition? GetNextThunderSound()
        {
            if (_thunderOneSound == null)
                return _thunderTwoSound;
            if (_thunderTwoSound == null)
                return _thunderOneSound;

            var sound = _nextThunderIndex % 2 == 0 ? _thunderOneSound : _thunderTwoSound;
            _nextThunderIndex++;
            return sound;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            if (soundRegistry.TryGet("rainforest_thunder_1", out var thunderOneSound))
                _thunderOneSound = thunderOneSound;
            if (soundRegistry.TryGet("rainforest_thunder_2", out var thunderTwoSound))
                _thunderTwoSound = thunderTwoSound;

            _audioConfigured = true;
        }

        public void Dispose()
        {
            _bolts.Clear();
            _weatherField.Reset();
            _secondsUntilNextStrike = 0f;
            _audioConfigured = false;
            _audio = null;
            _thunderOneSound = null;
            _thunderTwoSound = null;
            _nextThunderIndex = 0;
            GameState.WeatherVisualState.ClearLightningFlash();
        }

        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        private readonly record struct LightningPoint(float X, float Y);

        private sealed class LightningSegment
        {
            public LightningSegment(
                LightningPoint start,
                LightningPoint end,
                float width,
                int activationMainSegment,
                bool isBranch)
            {
                Start = start;
                End = end;
                Width = width;
                ActivationMainSegment = activationMainSegment;
                IsBranch = isBranch;
            }

            public LightningPoint Start { get; }
            public LightningPoint End { get; }
            public float Width { get; }
            public int ActivationMainSegment { get; }
            public bool IsBranch { get; }
        }

        private sealed class LightningBolt
        {
            public readonly List<LightningSegment> Segments = new(MaxMainSegmentsPerBolt + MaxBranchSegmentsPerBolt);
            public float WorldX;
            public float WorldZ;
            public float VisibleMainSegments = 1f;
            public int MainSegmentCount;
            public float HoldSeconds;
            public float Opacity;
            public float FlashIntensity;
            public bool HasCoveredScreen;
        }
    }
}
