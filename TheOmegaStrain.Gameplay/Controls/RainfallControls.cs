using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls.Weather;
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public sealed class RainfallControls : IObjectMovement
    {
        public const int VisibleDropTarget = 180;
        private const int OffscreenDropReserve = 260;
        public const int TargetDropCount = VisibleDropTarget + OffscreenDropReserve;
        public const float StartGuideYOffset = -1050f;
        public const float DepthSpread = 1200f;

        private const string RainColor = "BDEAFF";
        private const float DepthStartZ = 650f;
        private const float DepthBehindSpread = 2000f;
        private const float DepthAheadSpread = 3800f;
        private const float MinFallSpeed = 14f;
        private const float MaxFallSpeed = 25f;
        private const float MinLength = 24f;
        private const float MaxLength = 58f;
        private const float MinWidth = 0.75f;
        private const float MaxWidth = 1.7f;
        private const float BaseWindX = -1.7f;
        private const float BaseWindZ = 0.08f;
        private const float WindPulseX = 0.65f;
        private const float WindPulseZ = 0.08f;
        private const float DriftX = 0.18f;
        private const float DriftZ = 0.08f;
        private const float TopRespawnJitter = 140f;
        private const float FadeInStep = 0.18f;
        private const float GroundFadeDistance = 210f;

        private static readonly WeatherFieldSettings FieldSettings = new(
            DepthStartZ: DepthStartZ,
            VisibleDepthSpread: DepthSpread,
            BehindSpread: DepthBehindSpread,
            AheadSpread: DepthAheadSpread,
            OffscreenMargin: 270f,
            DirectionalSpawnAheadMin: 1000f,
            DirectionalSpawnAheadMax: 3400f,
            DirectionalLateralSpreadFactor: 0.9f,
            TravelBehindRecycleDistance: 2200f,
            TravelAheadRecycleDistance: 4100f,
            DirectionalSpawnModulo: 5,
            VisibleSpreadScreenMultiplier: 0.66f,
            WorldSpreadScreenMultiplier: 2.55f,
            OutsideSpawnChance: 0.72d);

        public static float GlobalRainOpacity { get; set; } = 1f;
        public static int CurrentVisibleDropTarget => GameState.SettingsState.ScaleParticleCount(VisibleDropTarget);
        public static int CurrentTargetDropCount => GameState.SettingsState.ScaleParticleCount(TargetDropCount);

        private readonly Random _random = new();
        private readonly WorldWeatherField _weatherField;
        private readonly List<Raindrop> _drops = new(TargetDropCount);
        private float _windPhase;
        private float _windX = BaseWindX;
        private float _windZ = BaseWindZ;
        private bool _audioConfigured;
        private IAudioPlayer? _audio;
        private SoundDefinition? _rainLoopSound;
        private IAudioInstance? _rainLoopInstance;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject? ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public RainfallControls()
        {
            _weatherField = new WorldWeatherField(_random, FieldSettings);
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;
            ConfigureAudio(audioPlayer, soundRegistry);
            EnsureRainLoopPlaying();

            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            WorldWeatherField.SyncEmitterToGround(theObject, mapPosition);
            _weatherField.UpdateTravelDirection(mapPosition);
            UpdateWind();

            float endOffsetY = GetEndGuideYOffset();
            EnsureDrops(mapPosition, endOffsetY);
            EnsureTriangleBuffer(theObject);

            var triangles = GetRaindropPart(theObject).Triangles;
            float objectZ = theObject.ObjectOffsets?.z ?? 0f;

            for (int i = 0; i < _drops.Count; i++)
            {
                var drop = _drops[i];
                MoveDrop(drop);

                if (ShouldRecycle(drop, mapPosition, endOffsetY))
                    ResetDropAtTop(drop, mapPosition, _weatherField.ShouldSpawnAhead());

                WriteTriangle(triangles[i], drop, mapPosition, objectZ, endOffsetY);
            }

            return theObject;
        }

        private void EnsureDrops(IVector3 mapPosition, float endOffsetY)
        {
            int visibleTarget = CurrentVisibleDropTarget;
            int targetCount = CurrentTargetDropCount;

            if (_drops.Count == targetCount)
                return;

            _drops.Clear();
            if (targetCount <= 0)
                return;

            float halfVisibleSpread = _weatherField.HalfVisibleSpread;
            for (int i = 0; i < visibleTarget; i++)
            {
                _drops.Add(CreateVisibleDrop(
                    mapPosition,
                    RandomRange(-halfVisibleSpread, halfVisibleSpread),
                    RandomRange(StartGuideYOffset, endOffsetY)));
            }

            for (int i = visibleTarget; i < targetCount; i++)
                _drops.Add(CreateReserveDrop(mapPosition));
        }

        private void EnsureTriangleBuffer(I3dObject theObject)
        {
            var rainPart = GetRaindropPart(theObject);
            int targetCount = _drops.Count;
            if (rainPart.Triangles.Count == targetCount)
                return;

            rainPart.Triangles.Clear();
            for (int i = 0; i < targetCount; i++)
                rainPart.Triangles.Add(CreateRainTriangle());
        }

        private static I3dObjectPart GetRaindropPart(I3dObject theObject)
        {
            var part = theObject.ObjectParts.FirstOrDefault(p => p.PartName == "Raindrops");
            if (part != null)
                return part;

            part = new _3dObjectPart
            {
                PartName = "Raindrops",
                IsVisible = true
            };
            theObject.ObjectParts.Add(part);
            return part;
        }

        private void UpdateWind()
        {
            _windPhase += 0.012f * GameState.FrameScale90;
            _windX = BaseWindX + MathF.Sin(_windPhase) * WindPulseX;
            _windZ = BaseWindZ + MathF.Cos(_windPhase * 0.7f) * WindPulseZ;
        }

        private void MoveDrop(Raindrop drop)
        {
            float frameScale = GameState.FrameScale90;
            drop.OffsetY += drop.FallSpeed * frameScale;
            drop.WorldX += ((_windX * drop.WindWeight) + drop.DriftX) * frameScale;
            drop.WorldZ += ((_windZ * drop.WindWeight) + drop.DriftZ) * frameScale;
            drop.Opacity = Math.Min(1f, drop.Opacity + FadeInStep * frameScale);
            drop.VisualWindX = _windX * drop.WindWeight;
        }

        private bool ShouldRecycle(Raindrop drop, IVector3 mapPosition, float endOffsetY)
        {
            if (drop.OffsetY > endOffsetY)
                return true;

            return _weatherField.ShouldRecycle(drop.WorldX, drop.WorldZ, mapPosition);
        }

        private void ResetDropAtTop(Raindrop drop, IVector3 mapPosition, bool preferAhead)
        {
            var spawn = preferAhead && _weatherField.HasTravelDirection
                ? _weatherField.GetDirectionalSpawnOffset()
                : _weatherField.GetAmbientSpawnOffset(preferOutside: true);

            drop.WorldX = mapPosition.x + spawn.x;
            drop.OffsetY = StartGuideYOffset - RandomRange(0f, TopRespawnJitter);
            drop.WorldZ = mapPosition.z + spawn.z;
            drop.FallSpeed = RandomRange(MinFallSpeed, MaxFallSpeed);
            drop.Length = RandomRange(MinLength, MaxLength);
            drop.Width = RandomRange(MinWidth, MaxWidth);
            drop.WindWeight = RandomRange(0.75f, 1.25f);
            drop.DriftX = RandomRange(-DriftX, DriftX);
            drop.DriftZ = RandomRange(-DriftZ, DriftZ);
            drop.SlantJitter = RandomRange(-2.5f, 2.5f);
            drop.VisualWindX = _windX * drop.WindWeight;
            drop.Opacity = 0f;
        }

        private Raindrop CreateVisibleDrop(IVector3 mapPosition, float offsetX, float offsetY)
        {
            var drop = new Raindrop();
            ResetDropAtTop(drop, mapPosition, preferAhead: false);
            drop.WorldX = mapPosition.x + offsetX;
            drop.OffsetY = offsetY;
            drop.WorldZ = mapPosition.z + RandomRange(DepthStartZ, DepthStartZ + DepthSpread);
            drop.Opacity = RandomRange(0.35f, 1f);
            return drop;
        }

        private Raindrop CreateReserveDrop(IVector3 mapPosition)
        {
            var drop = new Raindrop();
            ResetDropAtTop(drop, mapPosition, preferAhead: false);
            drop.OffsetY = RandomRange(StartGuideYOffset, GetEndGuideYOffset());
            drop.Opacity = RandomRange(0.15f, 0.75f);
            return drop;
        }

        private static void WriteTriangle(
            ITriangleMeshWithColor triangle,
            Raindrop drop,
            IVector3 mapPosition,
            float objectZ,
            float endOffsetY)
        {
            float groundFade = Math.Clamp((endOffsetY - drop.OffsetY) / GroundFadeDistance, 0f, 1f);
            float opacity = drop.Opacity * groundFade * GlobalRainOpacity;
            if (opacity <= 0.025f)
            {
                CollapseTriangle(triangle);
                return;
            }

            float relativeX = drop.WorldX - mapPosition.x;
            float relativeZ = drop.WorldZ - mapPosition.z;
            float scale = WorldWeatherField.GetProjectionScale(relativeZ, objectZ);
            float centerX = relativeX / scale;
            float centerY = drop.OffsetY / scale;

            float length = drop.Length * (0.72f + opacity * 0.28f);
            float halfWidth = drop.Width * (0.75f + opacity * 0.25f);
            float slant = (drop.VisualWindX * 4.5f) + drop.SlantJitter;
            float tailX = centerX - slant;
            float tailY = centerY - length;

            triangle.Color = ScaleHexColor(RainColor, 0.3f + QuantizeOpacity(opacity) * 0.7f);
            triangle.noHidden = true;
            triangle.angle = 1f;

            triangle.vert1.x = tailX - halfWidth;
            triangle.vert1.y = tailY;
            triangle.vert1.z = relativeZ;

            triangle.vert2.x = centerX + halfWidth;
            triangle.vert2.y = centerY;
            triangle.vert2.z = relativeZ;

            triangle.vert3.x = centerX - halfWidth;
            triangle.vert3.y = centerY;
            triangle.vert3.z = relativeZ;
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

        private static ITriangleMeshWithColor CreateRainTriangle()
        {
            return new TriangleMeshWithColor
            {
                Color = RainColor,
                noHidden = true,
                angle = 1f,
                vert1 = new Vector3(),
                vert2 = new Vector3(),
                vert3 = new Vector3()
            };
        }

        private static float QuantizeOpacity(float opacity)
        {
            return MathF.Round(Math.Clamp(opacity, 0f, 1f) * 8f) / 8f;
        }

        private static string ScaleHexColor(string color, float factor)
        {
            factor = Math.Clamp(factor, 0f, 1f);
            string hex = color.Trim().TrimStart('#');
            int red = Convert.ToInt32(hex.Substring(0, 2), 16);
            int green = Convert.ToInt32(hex.Substring(2, 2), 16);
            int blue = Convert.ToInt32(hex.Substring(4, 2), 16);

            return $"{(int)(red * factor):X2}{(int)(green * factor):X2}{(int)(blue * factor):X2}";
        }

        private static float GetEndGuideYOffset()
        {
            var surfaceY = GameState.SurfaceState.SurfaceViewportObject?.ObjectOffsets?.y;
            if (surfaceY.HasValue)
                return surfaceY.Value - 12f;

            return ScreenSetup.screenSizeY * 0.46f;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private void EnsureRainLoopPlaying()
        {
            if (_audio == null || _rainLoopSound == null)
                return;

            float volume = _rainLoopSound.Settings.Volume * Math.Clamp(GlobalRainOpacity, 0f, 1f);
            if (_rainLoopInstance == null || !_rainLoopInstance.IsPlaying)
            {
                _rainLoopInstance = _audio.Play(
                    _rainLoopSound,
                    AudioPlayMode.SegmentedLoop,
                    new AudioPlayOptions { VolumeOverride = volume });
                return;
            }

            _rainLoopInstance.SetVolume(volume);
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (_audioConfigured)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            if (soundRegistry.TryGet("rain_loop", out var rainLoopSound))
                _rainLoopSound = rainLoopSound;

            _audioConfigured = true;
        }

        public void Dispose()
        {
            _rainLoopInstance?.Stop(playEndSegment: false);
            _rainLoopInstance = null;
            _drops.Clear();
            _weatherField.Reset();
            _windPhase = 0f;
            _windX = BaseWindX;
            _windZ = BaseWindZ;
            _audioConfigured = false;
            _audio = null;
            _rainLoopSound = null;
        }

        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        private sealed class Raindrop
        {
            public float WorldX;
            public float OffsetY;
            public float WorldZ;
            public float FallSpeed;
            public float Length;
            public float Width;
            public float WindWeight;
            public float DriftX;
            public float DriftZ;
            public float VisualWindX;
            public float SlantJitter;
            public float Opacity;
        }
    }
}
