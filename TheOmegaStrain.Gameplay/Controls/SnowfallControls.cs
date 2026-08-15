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
    public sealed class SnowfallControls : IObjectMovement
    {
        public const int VisibleFlakeTarget = 150;
        private const int OffscreenFlakeReserve = 250;
        public const int TargetFlakeCount = VisibleFlakeTarget + OffscreenFlakeReserve;
        public const float StartGuideYOffset = -1000f;
        public const float DepthSpread = 1000f;

        private const string SnowColor = "ffffff";
        private const float DepthStartZ = 750f;
        private const float DepthBehindSpread = 1800f;
        private const float DepthAheadSpread = 3400f;
        private const float MinSize = 1.5f;
        private const float MaxSize = 3.3f;
        private const float MinFallSpeed = 1.2f;
        private const float MaxFallSpeed = 3.0f;
        private const float HorizontalDrift = 0.28f;
        private const float DepthDrift = 0.18f;
        private const float TopRespawnJitter = 180f;
        private static readonly WeatherFieldSettings FieldSettings = new(
            DepthStartZ: DepthStartZ,
            VisibleDepthSpread: DepthSpread,
            BehindSpread: DepthBehindSpread,
            AheadSpread: DepthAheadSpread,
            OffscreenMargin: 240f,
            DirectionalSpawnAheadMin: 900f,
            DirectionalSpawnAheadMax: 3000f,
            DirectionalLateralSpreadFactor: 0.88f,
            TravelBehindRecycleDistance: 2200f,
            TravelAheadRecycleDistance: 3800f,
            DirectionalSpawnModulo: 5,
            VisibleSpreadScreenMultiplier: 0.58f,
            WorldSpreadScreenMultiplier: 2.4f);

        /// <summary>
        /// Controls snow visibility (1 = fully visible, 0 = fully hidden).
        /// Set each frame from outside based on star field opacity.
        /// </summary>
        public static float GlobalSnowOpacity { get; set; } = 1f;
        public static int CurrentVisibleFlakeTarget => GameState.SettingsState.ScaleParticleCount(VisibleFlakeTarget);
        public static int CurrentTargetFlakeCount => GameState.SettingsState.ScaleParticleCount(TargetFlakeCount);

        private readonly Random _random = new();
        private readonly WorldWeatherField _weatherField;
        private readonly List<Snowflake> _flakes = new(TargetFlakeCount);

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject? ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public SnowfallControls()
        {
            _weatherField = new WorldWeatherField(_random, FieldSettings);
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            WorldWeatherField.SyncEmitterToGround(theObject, mapPosition);
            _weatherField.UpdateTravelDirection(mapPosition);
            float endOffsetY = GetEndGuideYOffset();
            EnsureFlakes(mapPosition, endOffsetY);
            EnsureTriangleBuffer(theObject);

            var triangles = GetSnowflakePart(theObject).Triangles;
            float objectZ = theObject.ObjectOffsets?.z ?? 0f;

            for (int i = 0; i < _flakes.Count; i++)
            {
                var flake = _flakes[i];
                MoveFlake(flake);

                if (ShouldRecycle(flake, mapPosition, endOffsetY))
                    ResetFlakeAtTop(flake, mapPosition, _weatherField.ShouldSpawnAhead());

                WriteTriangle(triangles[i], flake, mapPosition, objectZ);
            }

            return theObject;
        }

        private void EnsureFlakes(IVector3 mapPosition, float endOffsetY)
        {
            int visibleTarget = CurrentVisibleFlakeTarget;
            int targetCount = CurrentTargetFlakeCount;

            if (_flakes.Count == targetCount)
                return;

            _flakes.Clear();
            if (targetCount <= 0)
                return;

            float halfVisibleSpread = _weatherField.HalfVisibleSpread;
            for (int i = 0; i < visibleTarget; i++)
            {
                _flakes.Add(CreateVisibleFlake(
                    mapPosition,
                    RandomRange(-halfVisibleSpread, halfVisibleSpread),
                    RandomRange(StartGuideYOffset, endOffsetY)));
            }

            for (int i = visibleTarget; i < targetCount; i++)
                _flakes.Add(CreateReserveFlake(mapPosition));
        }

        private void EnsureTriangleBuffer(I3dObject theObject)
        {
            var snowflakePart = GetSnowflakePart(theObject);
            int targetCount = _flakes.Count;
            if (snowflakePart.Triangles.Count == targetCount)
                return;

            snowflakePart.Triangles.Clear();
            for (int i = 0; i < targetCount; i++)
                snowflakePart.Triangles.Add(CreateSnowTriangle());
        }

        private static I3dObjectPart GetSnowflakePart(I3dObject theObject)
        {
            var part = theObject.ObjectParts.FirstOrDefault(p => p.PartName == "Snowflakes");
            if (part != null)
                return part;

            part = new _3dObjectPart
            {
                PartName = "Snowflakes",
                IsVisible = true
            };
            theObject.ObjectParts.Add(part);
            return part;
        }

        private static void MoveFlake(Snowflake flake)
        {
            float frameScale = GameState.FrameScale90;
            flake.Phase += flake.SwaySpeed * frameScale;
            flake.WorldX += (flake.DriftX + MathF.Sin(flake.Phase) * flake.SwayAmount) * frameScale;
            flake.OffsetY += flake.FallSpeed * frameScale;
            flake.WorldZ += flake.DriftZ * frameScale;
        }

        private bool ShouldRecycle(Snowflake flake, IVector3 mapPosition, float endOffsetY)
        {
            if (flake.OffsetY > endOffsetY)
                return true;

            return _weatherField.ShouldRecycle(flake.WorldX, flake.WorldZ, mapPosition);
        }

        private void ResetFlakeAtTop(Snowflake flake, IVector3 mapPosition, bool preferAhead)
        {
            var spawn = preferAhead && _weatherField.HasTravelDirection
                ? _weatherField.GetDirectionalSpawnOffset()
                : _weatherField.GetAmbientSpawnOffset(preferOutside: true);

            flake.WorldX = mapPosition.x + spawn.x;
            flake.OffsetY = StartGuideYOffset - RandomRange(0f, TopRespawnJitter);
            flake.WorldZ = mapPosition.z + spawn.z;
            flake.Size = RandomRange(MinSize, MaxSize);
            flake.FallSpeed = RandomRange(MinFallSpeed, MaxFallSpeed);
            flake.DriftX = RandomRange(-HorizontalDrift, HorizontalDrift);
            flake.DriftZ = RandomRange(-DepthDrift, DepthDrift);
            flake.SwayAmount = RandomRange(0.05f, 0.22f);
            flake.SwaySpeed = RandomRange(0.012f, 0.035f);
            flake.Phase = RandomRange(0f, MathF.PI * 2f);
        }

        private Snowflake CreateVisibleFlake(IVector3 mapPosition, float offsetX, float offsetY)
        {
            var flake = new Snowflake();
            ResetFlakeAtTop(flake, mapPosition, preferAhead: false);
            flake.WorldX = mapPosition.x + offsetX;
            flake.OffsetY = offsetY;
            flake.WorldZ = mapPosition.z + RandomRange(DepthStartZ, DepthStartZ + DepthSpread);
            return flake;
        }

        private Snowflake CreateReserveFlake(IVector3 mapPosition)
        {
            var flake = new Snowflake();
            ResetFlakeAtTop(flake, mapPosition, preferAhead: false);
            flake.OffsetY = RandomRange(StartGuideYOffset, GetEndGuideYOffset());
            return flake;
        }

        private static void WriteTriangle(ITriangleMeshWithColor triangle, Snowflake flake, IVector3 mapPosition, float objectZ)
        {
            float opacity = GlobalSnowOpacity;
            if (opacity <= 0.01f)
            {
                // Collapse the triangle to a single point so it is invisible
                triangle.vert1.x = triangle.vert2.x = triangle.vert3.x = 0f;
                triangle.vert1.y = triangle.vert2.y = triangle.vert3.y = 0f;
                triangle.vert1.z = triangle.vert2.z = triangle.vert3.z = 0f;
                triangle.angle = 0f;
                return;
            }

            float relativeX = flake.WorldX - mapPosition.x;
            float relativeZ = flake.WorldZ - mapPosition.z;

            float scale = WorldWeatherField.GetProjectionScale(relativeZ, objectZ);
            float centerX = relativeX / scale;
            float centerY = flake.OffsetY / scale;
            float halfSize = flake.Size * opacity;

            triangle.Color = SnowColor;
            triangle.noHidden = true;
            triangle.angle = 1f;

            triangle.vert1.x = centerX - halfSize;
            triangle.vert1.y = centerY - halfSize;
            triangle.vert1.z = relativeZ;

            triangle.vert2.x = centerX + halfSize;
            triangle.vert2.y = centerY;
            triangle.vert2.z = relativeZ;

            triangle.vert3.x = centerX - halfSize * 0.2f;
            triangle.vert3.y = centerY + halfSize;
            triangle.vert3.z = relativeZ;
        }

        private static ITriangleMeshWithColor CreateSnowTriangle()
        {
            return new TriangleMeshWithColor
            {
                Color = SnowColor,
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
                return surfaceY.Value - 30f;

            return ScreenSetup.screenSizeY * 0.46f;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void Dispose()
        {
            _flakes.Clear();
            _weatherField.Reset();
        }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        private sealed class Snowflake
        {
            public float WorldX;
            public float OffsetY;
            public float WorldZ;
            public float Size;
            public float FallSpeed;
            public float DriftX;
            public float DriftZ;
            public float Phase;
            public float SwayAmount;
            public float SwaySpeed;
        }
    }
}
