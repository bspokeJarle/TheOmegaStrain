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
    public sealed class SandDriftControls : IObjectMovement
    {
        public const int VisibleDustTarget = 95;
        private const int OffscreenDustReserve = 155;
        public const int TargetDustCount = VisibleDustTarget + OffscreenDustReserve;
        public const float StartGuideYOffset = -820f;
        public const float DepthSpread = 1300f;

        private const string SandColor = "D8B66A";
        private const float DepthStartZ = 650f;
        private const float DepthBehindSpread = 1800f;
        private const float DepthAheadSpread = 3600f;
        private const float MinSize = 0.75f;
        private const float MaxSize = 2.15f;
        private const float MinVerticalDrift = -0.08f;
        private const float MaxVerticalDrift = 0.42f;
        private const float BaseWindX = 1.15f;
        private const float WindPulseX = 1.45f;
        private const float BaseWindZ = 0.16f;
        private const float WindPulseZ = 0.24f;
        private const float HorizontalDrift = 0.55f;
        private const float DepthDrift = 0.22f;
        private const float TopRespawnJitter = 220f;

        private static readonly WeatherFieldSettings FieldSettings = new(
            DepthStartZ: DepthStartZ,
            VisibleDepthSpread: DepthSpread,
            BehindSpread: DepthBehindSpread,
            AheadSpread: DepthAheadSpread,
            OffscreenMargin: 280f,
            DirectionalSpawnAheadMin: 1000f,
            DirectionalSpawnAheadMax: 3300f,
            DirectionalLateralSpreadFactor: 0.92f,
            TravelBehindRecycleDistance: 2300f,
            TravelAheadRecycleDistance: 4000f,
            DirectionalSpawnModulo: 4,
            VisibleSpreadScreenMultiplier: 0.64f,
            WorldSpreadScreenMultiplier: 2.5f,
            OutsideSpawnChance: 0.75d);

        public static float GlobalSandOpacity { get; set; } = 1f;
        public static int CurrentVisibleDustTarget => GameState.SettingsState.ScaleParticleCount(VisibleDustTarget);
        public static int CurrentTargetDustCount => GameState.SettingsState.ScaleParticleCount(TargetDustCount);

        private readonly Random _random = new();
        private readonly WorldWeatherField _weatherField;
        private readonly List<DustMote> _motes = new(TargetDustCount);
        private float _windPhase;
        private float _windX = BaseWindX;
        private float _windZ = BaseWindZ;

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject? ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public SandDriftControls()
        {
            _weatherField = new WorldWeatherField(_random, FieldSettings);
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            WorldWeatherField.SyncEmitterToGround(theObject, mapPosition);
            _weatherField.UpdateTravelDirection(mapPosition);
            UpdateWind();

            float endOffsetY = GetEndGuideYOffset();
            EnsureMotes(mapPosition, endOffsetY);
            EnsureTriangleBuffer(theObject);

            var triangles = GetDustPart(theObject).Triangles;
            float objectZ = theObject.ObjectOffsets?.z ?? 0f;

            for (int i = 0; i < _motes.Count; i++)
            {
                var mote = _motes[i];
                MoveMote(mote);

                if (ShouldRecycle(mote, mapPosition, endOffsetY))
                    ResetMote(mote, mapPosition, _weatherField.ShouldSpawnAhead());

                WriteTriangle(triangles[i], mote, mapPosition, objectZ);
            }

            return theObject;
        }

        private void EnsureMotes(IVector3 mapPosition, float endOffsetY)
        {
            int visibleTarget = CurrentVisibleDustTarget;
            int targetCount = CurrentTargetDustCount;

            if (_motes.Count == targetCount)
                return;

            _motes.Clear();
            if (targetCount <= 0)
                return;

            float halfVisibleSpread = _weatherField.HalfVisibleSpread;
            for (int i = 0; i < visibleTarget; i++)
            {
                _motes.Add(CreateVisibleMote(
                    mapPosition,
                    RandomRange(-halfVisibleSpread, halfVisibleSpread),
                    RandomRange(StartGuideYOffset, endOffsetY)));
            }

            for (int i = visibleTarget; i < targetCount; i++)
                _motes.Add(CreateReserveMote(mapPosition));
        }

        private void EnsureTriangleBuffer(I3dObject theObject)
        {
            var part = GetDustPart(theObject);
            int targetCount = _motes.Count;
            if (part.Triangles.Count == targetCount)
                return;

            part.Triangles.Clear();
            for (int i = 0; i < targetCount; i++)
                part.Triangles.Add(CreateDustTriangle());
        }

        private static I3dObjectPart GetDustPart(I3dObject theObject)
        {
            var part = theObject.ObjectParts.FirstOrDefault(p => p.PartName == "SandDust");
            if (part != null)
                return part;

            part = new _3dObjectPart
            {
                PartName = "SandDust",
                IsVisible = true
            };
            theObject.ObjectParts.Add(part);
            return part;
        }

        private void UpdateWind()
        {
            _windPhase += 0.018f * GameState.FrameScale90;
            float gust = MathF.Max(0f, MathF.Sin(_windPhase * 0.31f));
            _windX = BaseWindX + MathF.Sin(_windPhase) * WindPulseX + gust * 1.2f;
            _windZ = BaseWindZ + MathF.Cos(_windPhase * 0.66f) * WindPulseZ;
        }

        private void MoveMote(DustMote mote)
        {
            float frameScale = GameState.FrameScale90;
            mote.Phase += mote.SwaySpeed * frameScale;
            mote.WorldX += ((_windX * mote.WindWeight) + mote.DriftX + MathF.Sin(mote.Phase) * mote.SwayAmount) * frameScale;
            mote.WorldZ += ((_windZ * mote.WindWeight) + mote.DriftZ) * frameScale;
            mote.OffsetY += (mote.VerticalDrift + MathF.Sin(mote.Phase * 0.7f) * mote.VerticalSway) * frameScale;
            mote.Opacity = Math.Min(1f, mote.Opacity + 0.035f * frameScale);
        }

        private bool ShouldRecycle(DustMote mote, IVector3 mapPosition, float endOffsetY)
        {
            if (mote.OffsetY > endOffsetY + 80f)
                return true;
            if (mote.OffsetY < StartGuideYOffset - TopRespawnJitter - 120f)
                return true;

            return _weatherField.ShouldRecycle(mote.WorldX, mote.WorldZ, mapPosition);
        }

        private void ResetMote(DustMote mote, IVector3 mapPosition, bool preferAhead)
        {
            var spawn = preferAhead && _weatherField.HasTravelDirection
                ? _weatherField.GetDirectionalSpawnOffset()
                : _weatherField.GetAmbientSpawnOffset(preferOutside: true);

            mote.WorldX = mapPosition.x + spawn.x;
            mote.OffsetY = RandomRange(StartGuideYOffset, GetEndGuideYOffset());
            mote.WorldZ = mapPosition.z + spawn.z;
            mote.Size = RandomRange(MinSize, MaxSize);
            mote.VerticalDrift = RandomRange(MinVerticalDrift, MaxVerticalDrift);
            mote.DriftX = RandomRange(-HorizontalDrift, HorizontalDrift);
            mote.DriftZ = RandomRange(-DepthDrift, DepthDrift);
            mote.WindWeight = RandomRange(0.65f, 1.45f);
            mote.SwayAmount = RandomRange(0.08f, 0.42f);
            mote.SwaySpeed = RandomRange(0.010f, 0.030f);
            mote.VerticalSway = RandomRange(0.02f, 0.12f);
            mote.Phase = RandomRange(0f, MathF.PI * 2f);
            mote.Opacity = RandomRange(0.12f, 0.70f);
        }

        private DustMote CreateVisibleMote(IVector3 mapPosition, float offsetX, float offsetY)
        {
            var mote = new DustMote();
            ResetMote(mote, mapPosition, preferAhead: false);
            mote.WorldX = mapPosition.x + offsetX;
            mote.OffsetY = offsetY;
            mote.WorldZ = mapPosition.z + RandomRange(DepthStartZ, DepthStartZ + DepthSpread);
            return mote;
        }

        private DustMote CreateReserveMote(IVector3 mapPosition)
        {
            var mote = new DustMote();
            ResetMote(mote, mapPosition, preferAhead: false);
            return mote;
        }

        private static void WriteTriangle(ITriangleMeshWithColor triangle, DustMote mote, IVector3 mapPosition, float objectZ)
        {
            float opacity = mote.Opacity * GlobalSandOpacity;
            if (opacity <= 0.015f)
            {
                CollapseTriangle(triangle);
                return;
            }

            float relativeX = mote.WorldX - mapPosition.x;
            float relativeZ = mote.WorldZ - mapPosition.z;
            float scale = WorldWeatherField.GetProjectionScale(relativeZ, objectZ);
            float centerX = relativeX / scale;
            float centerY = mote.OffsetY / scale;
            float size = mote.Size * (0.55f + opacity * 0.35f);
            float smear = size * (0.75f + mote.WindWeight * 0.25f);

            triangle.Color = ScaleHexColor(SandColor, 0.22f + QuantizeOpacity(opacity) * 0.42f);
            triangle.noHidden = true;
            triangle.angle = 1f;

            triangle.vert1.x = centerX - smear;
            triangle.vert1.y = centerY - size * 0.25f;
            triangle.vert1.z = relativeZ;

            triangle.vert2.x = centerX + smear;
            triangle.vert2.y = centerY + size * 0.05f;
            triangle.vert2.z = relativeZ;

            triangle.vert3.x = centerX - size * 0.15f;
            triangle.vert3.y = centerY + size * 0.55f;
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

        private static ITriangleMeshWithColor CreateDustTriangle()
        {
            return new TriangleMeshWithColor
            {
                Color = SandColor,
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
                return surfaceY.Value - 20f;

            return ScreenSetup.screenSizeY * 0.46f;
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }

        public void Dispose()
        {
            _motes.Clear();
            _weatherField.Reset();
            _windPhase = 0f;
            _windX = BaseWindX;
            _windZ = BaseWindZ;
        }

        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        private sealed class DustMote
        {
            public float WorldX;
            public float OffsetY;
            public float WorldZ;
            public float Size;
            public float VerticalDrift;
            public float DriftX;
            public float DriftZ;
            public float WindWeight;
            public float Phase;
            public float SwayAmount;
            public float SwaySpeed;
            public float VerticalSway;
            public float Opacity;
        }
    }
}
