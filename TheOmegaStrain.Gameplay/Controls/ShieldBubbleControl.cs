using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls
{
    /// <summary>
    /// Drives the ship's shield bubble: mirrors the ship's on-screen position each
    /// frame, rebuilds the rippling panel mesh, and shows/hides the whole object
    /// based on GamePlayState.ShieldPoints. Builds geometry itself (rather than the
    /// Game-project mesh helpers) because this project only depends on Domain/RetroMesh.Engine.
    /// </summary>
    public class ShieldBubbleControl : IObjectMovement
    {
        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // Ellipsoid hugging the ship: widest across X, shorter on the vertical Z axis.
        private const float RadiusX = 112f;
        private const float RadiusY = 98f;
        private const float RadiusZ = 76f;
        private const float CenterZOffset = 8f;

        // Hex panels are laid out on latitude bands; segment count follows cos(lat)
        // so panels stay roughly equal-sized instead of pinching at the poles.
        private static readonly float[] BandLatitudesDeg = { -70f, -42f, -14f, 14f, 42f, 70f };
        private const int EquatorSegments = 16;
        private const float HexScaleMin = 0.52f;
        private const float HexScaleMax = 0.86f;

        private const float WaveAmplitude = 7f;
        private const float PrimaryWaveSpeed = 2.1f;
        private const float SecondaryWaveSpeed = -1.35f;
        private const float PrimaryWaveLongitudeFreq = 2.0f;
        private const float PrimaryWaveLatitudeFreq = 1.4f;
        private const float SecondaryWaveLongitudeFreq = -1.1f;
        private const float SecondaryWaveLatitudeFreq = 2.6f;

        private const string DeepColor = "10386E";
        private const string BaseColor = "2E8BE6";
        private const string CrestColor = "D8F4FF";

        private const float ImpactFlashSeconds = 0.45f;

        private static readonly Vector3 BubbleCenter = new Vector3 { x = 0, y = 0, z = 0 };

        private float _timeSeconds;
        private float _flashSecondsLeft;
        private float _lastShieldPoints = -1f;
        private DateTime _lastUpdate = DateTime.MinValue;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            float shieldPoints = GameState.GamePlayState.ShieldPoints;
            bool hasShield = shieldPoints > 0f;
            SetObjectVisibility(theObject, hasShield);

            // Track drops even while hidden so the flash does not fire on respawn.
            if (_lastShieldPoints >= 0f && shieldPoints < _lastShieldPoints)
                _flashSecondsLeft = ImpactFlashSeconds;
            _lastShieldPoints = shieldPoints;

            if (!hasShield)
                return theObject;

            var now = DateTime.Now;
            if (_lastUpdate == DateTime.MinValue)
                _lastUpdate = now;

            float deltaSeconds = (float)(now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;
            _timeSeconds += deltaSeconds;
            _flashSecondsLeft = MathF.Max(0f, _flashSecondsLeft - deltaSeconds);

            // Bubble shares the ship's local pivot; it just needs the ship's current screen offset.
            theObject.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            var shipOffsets = GameState.ShipState?.ShipObjectOffsets;
            theObject.ObjectOffsets = shipOffsets != null
                ? new Vector3 { x = shipOffsets.x, y = shipOffsets.y, z = shipOffsets.z }
                : new Vector3 { x = 0, y = 0, z = 0 };

            float strength = GamePlayState.MaxShieldPoints > 0f
                ? Math.Clamp(shieldPoints / GamePlayState.MaxShieldPoints, 0f, 1f)
                : 0f;
            float flash = _flashSecondsLeft / ImpactFlashSeconds;

            if (theObject.ObjectParts.Count > 0)
                theObject.ObjectParts[0].Triangles = BuildPanels(_timeSeconds, strength, flash);

            return theObject;
        }

        /// <summary>
        /// Rebuilds the panel shell. Each panel is a flat hexagonal tile sitting in the
        /// local tangent plane of an ellipsoid, displaced along its normal by two
        /// counter-rotating waves. A stronger shield yields larger, brighter tiles;
        /// <paramref name="flash01"/> briefly flares the whole shell after a hit.
        /// </summary>
        public static List<ITriangleMeshWithColorAndTexture> BuildPanels(
            float timeSeconds,
            float strength01 = 1f,
            float flash01 = 0f)
        {
            strength01 = Math.Clamp(strength01, 0f, 1f);
            flash01 = Math.Clamp(flash01, 0f, 1f);

            var tris = new List<ITriangleMeshWithColorAndTexture>();
            float hexScale = HexScaleMin + (HexScaleMax - HexScaleMin) * strength01;

            for (int band = 0; band < BandLatitudesDeg.Length; band++)
            {
                float lat = BandLatitudesDeg[band];
                float cosLat = MathF.Cos(lat * (MathF.PI / 180f));
                int segments = Math.Max(5, (int)MathF.Round(EquatorSegments * cosLat));
                float lonStep = 360f / segments;
                // Stagger alternate bands so the tiles interlock like a honeycomb.
                float lonOffset = (band % 2 == 0) ? 0f : lonStep * 0.5f;

                float latSpanDeg = band + 1 < BandLatitudesDeg.Length
                    ? BandLatitudesDeg[band + 1] - lat
                    : lat - BandLatitudesDeg[band - 1];

                for (int seg = 0; seg < segments; seg++)
                {
                    float lon = seg * lonStep + lonOffset;
                    AddHexPanel(tris, lat, lon, lonStep, latSpanDeg, hexScale, timeSeconds, strength01, flash01);
                }
            }

            // Pole caps keep the shell from looking open at the top and bottom.
            float poleLatSpan = 90f - BandLatitudesDeg[^1];
            AddHexPanel(tris, 90f, 0f, 360f / 6f, poleLatSpan, hexScale, timeSeconds, strength01, flash01);
            AddHexPanel(tris, -90f, 0f, 360f / 6f, poleLatSpan, hexScale, timeSeconds, strength01, flash01);

            return tris;
        }

        private static void AddHexPanel(
            List<ITriangleMeshWithColorAndTexture> tris,
            float latDeg,
            float lonDeg,
            float lonSpanDeg,
            float latSpanDeg,
            float hexScale,
            float timeSeconds,
            float strength01,
            float flash01)
        {
            float lat = latDeg * (MathF.PI / 180f);
            float lon = lonDeg * (MathF.PI / 180f);

            float wave = MathF.Sin(timeSeconds * PrimaryWaveSpeed
                        - (lonDeg * PrimaryWaveLongitudeFreq + latDeg * PrimaryWaveLatitudeFreq) * (MathF.PI / 180f))
                       + 0.55f * MathF.Sin(timeSeconds * SecondaryWaveSpeed
                        - (lonDeg * SecondaryWaveLongitudeFreq + latDeg * SecondaryWaveLatitudeFreq) * (MathF.PI / 180f));
            wave *= 0.645f; // renormalize the summed waves back to roughly -1..1

            // Unit direction and the tangent frame around it.
            var dir = UnitDirection(lat, lon);
            var east = Normalize(new Vector3 { x = MathF.Cos(lon), y = -MathF.Sin(lon), z = 0f });
            var north = Normalize(Cross(dir, east));

            float bulge = 1f + (WaveAmplitude * wave) / RadiusY;
            var center = new Vector3
            {
                x = dir.x * RadiusX * bulge,
                y = dir.y * RadiusY * bulge,
                z = dir.z * RadiusZ * bulge + CenterZOffset
            };

            // Tangent-plane extents in world units, derived from the cell's angular size.
            float cosLat = MathF.Max(0.25f, MathF.Cos(lat));
            float uRadius = RadiusX * (lonSpanDeg * (MathF.PI / 180f)) * cosLat * 0.5f * hexScale;
            float vRadius = RadiusZ * (MathF.Abs(latSpanDeg) * (MathF.PI / 180f)) * 0.5f * hexScale;

            float brightness = (wave + 1f) * 0.5f;
            string panelColor = PanelColor(brightness, strength01, flash01);

            var hex = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * (MathF.PI / 3f);
                float cu = MathF.Cos(a) * uRadius;
                float cv = MathF.Sin(a) * vRadius;
                hex[i] = new Vector3
                {
                    x = center.x + east.x * cu + north.x * cv,
                    y = center.y + east.y * cu + north.y * cv,
                    z = center.z + east.z * cu + north.z * cv
                };
            }

            // Fan the hexagon from its first vertex: 4 triangles, no center vertex needed.
            for (int i = 1; i < 5; i++)
            {
                tris.Add(MeshGeometryOperations.CreateTriangleOutward(
                    hex[0], hex[i], hex[i + 1], BubbleCenter, panelColor,
                    static () => new TriangleMeshWithColor()));
            }
        }

        private static string PanelColor(float brightness01, float strength01, float flash01)
        {
            // Dim toward the deep tone as the shield drains, then flare on impact.
            string cool = LerpHexColor(DeepColor, BaseColor, 0.35f + 0.65f * strength01);
            string lit = LerpHexColor(cool, CrestColor, brightness01 * (0.45f + 0.55f * strength01));
            return flash01 > 0f ? LerpHexColor(lit, CrestColor, flash01 * 0.85f) : lit;
        }

        private static Vector3 UnitDirection(float lat, float lon)
        {
            float cosLat = MathF.Cos(lat);
            return new Vector3
            {
                x = cosLat * MathF.Sin(lon),
                y = cosLat * MathF.Cos(lon),
                z = MathF.Sin(lat)
            };
        }

        private static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3
        {
            x = a.y * b.z - a.z * b.y,
            y = a.z * b.x - a.x * b.z,
            z = a.x * b.y - a.y * b.x
        };

        private static Vector3 Normalize(Vector3 v)
        {
            float length = MathF.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            if (length <= 0.0001f) return new Vector3 { x = 1f, y = 0f, z = 0f };
            return new Vector3 { x = v.x / length, y = v.y / length, z = v.z / length };
        }

        private static string LerpHexColor(string from, string to, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            (int fr, int fg, int fb) = ParseHex(from);
            (int tr, int tg, int tb) = ParseHex(to);

            int r = fr + (int)((tr - fr) * t);
            int g = fg + (int)((tg - fg) * t);
            int b = fb + (int)((tb - fb) * t);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        private static (int r, int g, int b) ParseHex(string color)
        {
            return (
                Convert.ToInt32(color.Substring(0, 2), 16),
                Convert.ToInt32(color.Substring(2, 2), 16),
                Convert.ToInt32(color.Substring(4, 2), 16));
        }

        private static void SetObjectVisibility(I3dObject theObject, bool visible)
        {
            for (int i = 0; i < theObject.ObjectParts.Count; i++)
            {
                theObject.ObjectParts[i].IsVisible = visible;
            }
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void Dispose() { }
    }
}
