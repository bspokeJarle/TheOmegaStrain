using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using RetroMesh.Engine;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Runtime.Rendering
{
    using NumericsVector3 = System.Numerics.Vector3;
    using EngineVector3 = Vector3;

    public class StarFieldHandler
    {
        public const int VisibleStarTarget = 260;
        private const int OffscreenStarReserve = 430;
        public const int TargetStarCount = VisibleStarTarget + OffscreenStarReserve;

        private const float FadeInStep = 0.035f;
        private const float FadeOutStep = 0.06f;
        private const float MinRenderOpacity = 0.02f;
        private const float BaseOffscreenMargin = 260f;
        private const float BaseVisibleDepthMin = -700f;
        private const float BaseVisibleDepthMax = 500f;
        private const float BaseDepthBehindSpread = 1800f;
        private const float BaseDepthAheadSpread = 3600f;
        private const float BaseDirectionalSpawnAheadMin = 900f;
        private const float BaseDirectionalSpawnAheadMax = 3300f;
        private const float DirectionalLateralSpreadFactor = 0.88f;
        private const float BaseTravelBehindRecycleDistance = 2200f;
        private const float BaseTravelAheadRecycleDistance = 3900f;
        private const int DirectionalSpawnModulo = 5;

        // Star geometry is written pre-projection, so the renderer magnifies it by the projection
        // scale. Cap that natural perspective growth before a close star becomes overwhelming.
        private const float MaxApparentSize = 18f;

        // Keep the sky clear until the Surface has moved farther out of view. Scaling from the
        // design height preserves the same transition point at other window resolutions.
        public static float StarFadeInAltitude => 400f * ScreenSetup.ScreenScaleY;
        private static float OffscreenMargin => BaseOffscreenMargin * ScreenSetup.ScreenScaleX;
        private static float VisibleDepthMin => BaseVisibleDepthMin * ScreenSetup.ScreenScaleX;
        private static float VisibleDepthMax => BaseVisibleDepthMax * ScreenSetup.ScreenScaleX;
        private static float DepthBehindSpread => BaseDepthBehindSpread * ScreenSetup.ScreenScaleX;
        private static float DepthAheadSpread => BaseDepthAheadSpread * ScreenSetup.ScreenScaleX;
        private static float DirectionalSpawnAheadMin => BaseDirectionalSpawnAheadMin * ScreenSetup.ScreenScaleX;
        private static float DirectionalSpawnAheadMax => BaseDirectionalSpawnAheadMax * ScreenSetup.ScreenScaleX;
        private static float TravelBehindRecycleDistance => BaseTravelBehindRecycleDistance * ScreenSetup.ScreenScaleX;
        private static float TravelAheadRecycleDistance => BaseTravelAheadRecycleDistance * ScreenSetup.ScreenScaleX;

        private readonly Random random = new();
        private readonly List<StarState> stars = new(TargetStarCount);
        private readonly List<OmegaObject3D> renderableStars = new(TargetStarCount);
        private bool enableLogging = false;
        private bool hasLastWorldPosition;
        private bool isMoving;
        private bool hasTravelDirection;
        private float lastWorldX;
        private float lastWorldZ;
        private float travelX;
        private float travelZ;
        private int spawnSequence;

        public ISurface ParentSurface { get; set; }
        public int PooledStarCount => stars.Count;
        public int RenderableStarCount => renderableStars.Count;

        public StarFieldHandler(ISurface surface)
        {
            ParentSurface = surface;
        }

        public void SetLogging(bool enabled)
        {
            enableLogging = enabled;
        }

        public bool HasStars()
        {
            return renderableStars.Count > 0;
        }

        /// <summary>
        /// Average opacity across all pooled stars (0 = all faded out, 1 = all fully visible).
        /// Used to drive snow fade: snow opacity = 1 - PoolOpacity.
        /// </summary>
        public float PoolOpacity
        {
            get
            {
                if (stars.Count == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < stars.Count; i++)
                    sum += stars[i].Opacity;
                return Math.Clamp(sum / stars.Count, 0f, 1f);
            }
        }

        /// <summary>
        /// Clears all stars and resets state. Scene reset/transition should still use this.
        /// Altitude gating fades the pool out instead of clearing it.
        /// </summary>
        public void ClearStars()
        {
            stars.Clear();
            renderableStars.Clear();
            hasLastWorldPosition = false;
            isMoving = false;
            hasTravelDirection = false;
            spawnSequence = 0;

            if (Logger.ShouldLog(enableLogging))
                Logger.Log("[StarField] ClearStars() called. Pool and render list cleared.");
        }

        public void GenerateStarfield()
        {
            if (ParentSurface == null)
                return;

            var currentWorldPos = GameState.SurfaceState.GlobalMapPosition;
            UpdateTravelDirection(currentWorldPos);

            bool shouldShowStars = currentWorldPos.y > StarFadeInAltitude;
            if (!shouldShowStars)
            {
                FadeOutPoolForAltitude();
                UpdateRenderableStars();
                return;
            }

            EnsureStars(currentWorldPos);

            for (int i = 0; i < stars.Count; i++)
                UpdateStar(stars[i], currentWorldPos);

            UpdateRenderableStars();

            if (Logger.ShouldLog(enableLogging))
            {
                Logger.Log(
                    $"[StarField] Surface=({currentWorldPos.x:0.0}, {currentWorldPos.y:0.0}, {currentWorldPos.z:0.0}), " +
                    $"Pool={stars.Count}, Renderable={renderableStars.Count}");
            }
        }

        public IVector3 FindRandomPosition(IVector3 newWorldPosition)
        {
            var offset = ShouldSpawnAhead()
                ? GetDirectionalSpawnOffset(GetHalfWorldSpread())
                : GetAmbientSpawnOffset(GetHalfWorldSpread(), preferOutside: false);

            return new EngineVector3
            {
                x = offset.X,
                y = offset.Y,
                z = offset.Z
            };
        }

        public List<OmegaObject3D> GetStars()
        {
            if (renderableStars.Count == 0) return null;
            return renderableStars;
        }

        private void EnsureStars(IVector3 currentWorldPos)
        {
            if (stars.Count == TargetStarCount)
                return;

            int startIndex = stars.Count;
            for (int i = startIndex; i < TargetStarCount; i++)
            {
                bool preferOutside = i >= VisibleStarTarget;
                var offset = preferOutside
                    ? GetAmbientSpawnOffset(GetHalfWorldSpread(), preferOutside: true)
                    : GetVisibleSpawnOffset();

                var state = CreateStarState(currentWorldPos, offset);
                stars.Add(state);
            }
        }

        private StarState CreateStarState(IVector3 currentWorldPos, NumericsVector3 offset)
        {
            var star = Star.CreateStar(ParentSurface, CreateZeroVector(), RandomRange(3.4f, 5.8f));
            star.ObjectName = "Star";
            star.ObjectOffsets = CreateZeroVector();
            star.Movement = null;
            star.HasShadow = false;

            var state = new StarState
            {
                Star = star,
                BaseColor = GetBaseColor(star),
                Opacity = 0f,
                FadeMode = StarFadeMode.FadingIn,
                BaseVertices = CaptureBaseVertices(star)
            };

            PlaceStar(state, currentWorldPos, offset);
            ApplyOpacity(state);
            return state;
        }

        /// <summary>
        /// Snapshots the star's baked geometry. Stars are appended after the per-frame deep copy,
        /// so their triangles must never be scaled in place - the factor would compound each frame.
        /// Every frame rewrites the vertices from this immutable snapshot instead.
        /// </summary>
        private static NumericsVector3[] CaptureBaseVertices(OmegaObject3D star)
        {
            var triangles = star.ObjectParts[0].Triangles;
            var baseVertices = new NumericsVector3[triangles.Count * 3];

            for (int i = 0; i < triangles.Count; i++)
            {
                var tri = triangles[i];
                baseVertices[i * 3] = new NumericsVector3(tri.vert1.x, tri.vert1.y, tri.vert1.z);
                baseVertices[i * 3 + 1] = new NumericsVector3(tri.vert2.x, tri.vert2.y, tri.vert2.z);
                baseVertices[i * 3 + 2] = new NumericsVector3(tri.vert3.x, tri.vert3.y, tri.vert3.z);
            }

            return baseVertices;
        }

        /// <summary>
        /// Rewrites the star from its immutable base mesh and limits its final projected size.
        /// The local depth sign must match the renderer: map Z minus object Z.
        /// </summary>
        private static void ApplyApparentSizeClamp(StarState state, float projectionScale)
        {
            float apparentSize = state.BaseHalfExtent * projectionScale;
            float shrink = apparentSize > MaxApparentSize
                ? MaxApparentSize / apparentSize
                : 1f;

            var triangles = state.Star.ObjectParts[0].Triangles;
            var baseVertices = state.BaseVertices;

            for (int i = 0; i < triangles.Count; i++)
            {
                var tri = triangles[i];
                WriteVertex(tri.vert1, baseVertices[i * 3], shrink);
                WriteVertex(tri.vert2, baseVertices[i * 3 + 1], shrink);
                WriteVertex(tri.vert3, baseVertices[i * 3 + 2], shrink);
            }
        }

        private static void WriteVertex(IVector3 target, NumericsVector3 source, float shrink)
        {
            target.x = source.X * shrink;
            target.y = source.Y * shrink;
            target.z = source.Z * shrink;
        }

        private void UpdateStar(StarState state, IVector3 currentWorldPos)
        {
            if (state.FadeMode == StarFadeMode.FadingOutForRecycle)
            {
                // Keep the final approach size while fading. Recomputing after the star has
                // passed would make it visibly shrink before recycling.
                state.Opacity = Math.Max(0f, state.Opacity - FadeOutStep);
                if (state.Opacity <= 0f)
                    ResetStarForFadeIn(state, currentWorldPos, ShouldSpawnAhead());

                ApplyOpacity(state);
                return;
            }

            if (!TryGetProjectionScale(state, out float projectionScale))
            {
                state.Opacity = 0f;
                ApplyOpacity(state);
                if (ShouldRecycle(state, currentWorldPos))
                    ResetStarForFadeIn(state, currentWorldPos, ShouldSpawnAhead());
                return;
            }

            ApplyApparentSizeClamp(state, projectionScale);
            ApplyPerspectivePosition(state, currentWorldPos, projectionScale);
            bool intersectsViewport = OmegaPerspectiveProjectorFactory.IntersectsViewport(state.Star);

            if (!state.HasEnteredViewport)
            {
                if (!intersectsViewport)
                {
                    state.Opacity = 0f;
                    ApplyOpacity(state);
                    if (ShouldRecycle(state, currentWorldPos))
                        ResetStarForFadeIn(state, currentWorldPos, ShouldSpawnAhead());
                    return;
                }

                state.HasEnteredViewport = true;
            }
            else if (!intersectsViewport)
            {
                state.FadeMode = StarFadeMode.FadingOutForRecycle;
                state.Opacity = Math.Max(0f, state.Opacity - FadeOutStep);
                ApplyOpacity(state);
                return;
            }

            if (state.Opacity < 1f)
            {
                state.Opacity = Math.Min(1f, state.Opacity + FadeInStep);
                state.FadeMode = state.Opacity >= 1f ? StarFadeMode.Visible : StarFadeMode.FadingIn;
                ApplyOpacity(state);
            }
            else if (state.FadeMode != StarFadeMode.Visible)
            {
                state.FadeMode = StarFadeMode.Visible;
                ApplyOpacity(state);
            }
        }

        private void FadeOutPoolForAltitude()
        {
            for (int i = 0; i < stars.Count; i++)
            {
                var state = stars[i];
                state.FadeMode = StarFadeMode.FadingOutForAltitude;
                state.Opacity = Math.Max(0f, state.Opacity - FadeOutStep);
                ApplyOpacity(state);
            }
        }

        private void ResetStarForFadeIn(StarState state, IVector3 currentWorldPos, bool preferAhead)
        {
            var offset = preferAhead && hasTravelDirection
                ? GetDirectionalSpawnOffset(GetHalfWorldSpread())
                : GetAmbientSpawnOffset(GetHalfWorldSpread(), preferOutside: true);

            PlaceStar(state, currentWorldPos, offset);
            state.Opacity = 0f;
            state.FadeMode = StarFadeMode.FadingIn;
            state.HasEnteredViewport = false;
        }

        private static void PlaceStar(StarState state, IVector3 currentWorldPos, NumericsVector3 offset)
        {
            var worldPosition = state.Star.WorldPosition as EngineVector3;
            if (worldPosition == null)
            {
                worldPosition = new EngineVector3();
                state.Star.WorldPosition = worldPosition;
            }

            worldPosition.x = currentWorldPos.x + offset.X;
            worldPosition.y = currentWorldPos.y + offset.Y;
            worldPosition.z = currentWorldPos.z + offset.Z;

            state.Star.ObjectOffsets ??= new EngineVector3();
            state.Star.ObjectOffsets.x = 0f;
            state.Star.ObjectOffsets.y = 0f;
            state.Star.ObjectOffsets.z = 0f;
        }

        private static void ApplyPerspectivePosition(
            StarState state,
            IVector3 currentWorldPos,
            float projectionScale)
        {
            var relative = GetRelativePosition(state, currentWorldPos);
            float baseScale = Math.Max(1f, ScreenSetup.defaultObjectZoom);
            float perspectiveFactor = Math.Clamp(projectionScale / baseScale, 0.1f, 64f);

            state.Star.ObjectOffsets ??= new EngineVector3();
            state.Star.ObjectOffsets.x = relative.X * (perspectiveFactor - 1f);
            state.Star.ObjectOffsets.y = relative.Y * (perspectiveFactor - 1f);
            state.Star.ObjectOffsets.z = 0f;
        }

        private bool ShouldRecycle(StarState state, IVector3 currentWorldPos)
        {
            var relative = GetRelativePosition(state, currentWorldPos);
            float halfWorldSpread = GetHalfWorldSpread();
            float halfWorldHeight = GetHalfWorldHeightSpread();

            if (MathF.Abs(relative.X) > halfWorldSpread + OffscreenMargin)
                return true;
            if (MathF.Abs(relative.Y) > halfWorldHeight + OffscreenMargin)
                return true;
            if (relative.Z < -DepthBehindSpread - OffscreenMargin)
                return true;
            if (relative.Z > DepthAheadSpread + OffscreenMargin)
                return true;

            if (!isMoving || !hasTravelDirection)
                return false;

            float alongTravel = relative.X * travelX + relative.Z * travelZ;
            float lateral = MathF.Abs(relative.X * -travelZ + relative.Z * travelX);

            return alongTravel < -TravelBehindRecycleDistance
                || alongTravel > TravelAheadRecycleDistance
                || lateral > halfWorldSpread + OffscreenMargin;
        }

        private static bool TryGetProjectionScale(StarState state, out float projectionScale)
        {
            projectionScale = 0f;
            if (!ObjectPlacementHelpers.TryGetRenderPosition(
                    state.Star,
                    ScreenSetup.screenSizeX / 2,
                    ScreenSetup.screenSizeY / 2,
                    out double renderX,
                    out double renderY,
                    out double renderZ))
                return false;

            // Stars have their own mesh-size clamp. Use their real depth here so the
            // shrink factor follows the same perspective path as the star centre;
            // clamping depth itself would make an approaching star appear frozen.
            double depth = renderZ;
            if (!ProjectionMath.TryProjectVertex(
                    new EngineVector3(),
                    renderX,
                    renderY,
                    depth,
                    ScreenSetup.perspectiveAdjustment,
                    ScreenSetup.defaultObjectZoom,
                    out var center) ||
                !ProjectionMath.TryProjectVertex(
                    new EngineVector3(1f, 0f, 0f),
                    renderX,
                    renderY,
                    depth,
                    ScreenSetup.perspectiveAdjustment,
                    ScreenSetup.defaultObjectZoom,
                    out var unit))
                return false;

            projectionScale = MathF.Abs((float)(unit.x - center.x));
            return float.IsFinite(projectionScale) && projectionScale > 0f;
        }

        private static NumericsVector3 GetRelativePosition(StarState state, IVector3 currentWorldPos)
        {
            var worldPosition = state.Star.WorldPosition;
            return new NumericsVector3(
                worldPosition.x - currentWorldPos.x,
                worldPosition.y - currentWorldPos.y,
                worldPosition.z - currentWorldPos.z);
        }

        private NumericsVector3 GetVisibleSpawnOffset()
        {
            return new NumericsVector3(
                RandomRange(-GetHalfVisibleSpread(), GetHalfVisibleSpread()),
                RandomRange(-GetHalfVisibleHeight(), GetHalfVisibleHeight()),
                RandomRange(VisibleDepthMin, VisibleDepthMax));
        }

        private NumericsVector3 GetAmbientSpawnOffset(float halfWorldSpread, bool preferOutside)
        {
            if (preferOutside && random.NextDouble() < 0.72d)
            {
                if (random.NextDouble() < 0.45d)
                {
                    float sign = random.NextDouble() < 0.5d ? -1f : 1f;
                    return new NumericsVector3(
                        sign * RandomRange(GetHalfVisibleSpread(), halfWorldSpread),
                        RandomRange(-GetHalfWorldHeightSpread(), GetHalfWorldHeightSpread()),
                        RandomRange(-DepthBehindSpread, DepthAheadSpread));
                }

                float z = random.NextDouble() < 0.5d
                    ? RandomRange(-DepthBehindSpread, VisibleDepthMin)
                    : RandomRange(VisibleDepthMax, DepthAheadSpread);

                return new NumericsVector3(
                    RandomRange(-halfWorldSpread, halfWorldSpread),
                    RandomRange(-GetHalfWorldHeightSpread(), GetHalfWorldHeightSpread()),
                    z);
            }

            return GetVisibleSpawnOffset();
        }

        private NumericsVector3 GetDirectionalSpawnOffset(float halfWorldSpread)
        {
            float forward = RandomRange(DirectionalSpawnAheadMin, DirectionalSpawnAheadMax);
            float lateral = RandomRange(-halfWorldSpread * DirectionalLateralSpreadFactor, halfWorldSpread * DirectionalLateralSpreadFactor);
            float vertical = RandomRange(-GetHalfWorldHeightSpread(), GetHalfWorldHeightSpread());

            float x = travelX * forward + -travelZ * lateral;
            float z = travelZ * forward + travelX * lateral;

            return new NumericsVector3(
                x,
                vertical,
                Math.Clamp(z, -DepthBehindSpread, DepthAheadSpread));
        }

        private bool ShouldSpawnAhead()
        {
            if (!isMoving || !hasTravelDirection)
                return false;

            spawnSequence++;
            return spawnSequence % DirectionalSpawnModulo != 0;
        }

        private void UpdateTravelDirection(IVector3 currentWorldPos)
        {
            if (!hasLastWorldPosition)
            {
                lastWorldX = currentWorldPos.x;
                lastWorldZ = currentWorldPos.z;
                hasLastWorldPosition = true;
                return;
            }

            float dx = currentWorldPos.x - lastWorldX;
            float dz = currentWorldPos.z - lastWorldZ;
            lastWorldX = currentWorldPos.x;
            lastWorldZ = currentWorldPos.z;

            float distance = MathF.Sqrt(dx * dx + dz * dz);
            if (distance <= 0.05f)
            {
                isMoving = false;
                return;
            }

            travelX = dx / distance;
            travelZ = dz / distance;
            isMoving = true;
            hasTravelDirection = true;
        }

        private void UpdateRenderableStars()
        {
            renderableStars.Clear();
            for (int i = 0; i < stars.Count; i++)
            {
                if (stars[i].Opacity > MinRenderOpacity)
                    renderableStars.Add(stars[i].Star);
            }
        }

        private static void ApplyOpacity(StarState state)
        {
            string fadedColor = FadeHexColor(state.BaseColor, state.Opacity);
            var parts = state.Star.ObjectParts;
            for (int partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                var triangles = parts[partIndex].Triangles;
                for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
                    triangles[triangleIndex].Color = fadedColor;
            }
        }

        private static string FadeHexColor(string color, float opacity)
        {
            opacity = Math.Clamp(opacity, 0f, 1f);

            string hex = string.IsNullOrWhiteSpace(color) ? "ffffff" : color.Trim().TrimStart('#');
            if (hex.Length < 6)
                hex = "ffffff";

            int r = Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = Convert.ToInt32(hex.Substring(4, 2), 16);

            return $"{(int)(r * opacity):X2}{(int)(g * opacity):X2}{(int)(b * opacity):X2}";
        }

        private static string GetBaseColor(OmegaObject3D star)
        {
            if (star.ObjectParts.Count == 0)
                return "ffffff";
            if (star.ObjectParts[0].Triangles.Count == 0)
                return "ffffff";

            return star.ObjectParts[0].Triangles[0].Color ?? "ffffff";
        }

        private static EngineVector3 CreateZeroVector()
        {
            return new EngineVector3 { x = 0f, y = 0f, z = 0f };
        }

        private static float GetHalfVisibleSpread() => ScreenSetup.screenSizeX * 0.78f;
        private static float GetHalfWorldSpread() => ScreenSetup.screenSizeX * 2.35f;
        private static float GetHalfVisibleHeight() => ScreenSetup.screenSizeY * 0.76f;
        private static float GetHalfWorldHeightSpread() => ScreenSetup.screenSizeY * 1.35f;

        private float RandomRange(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private sealed class StarState
        {
            public required OmegaObject3D Star;
            public required string BaseColor;
            public float Opacity;
            public StarFadeMode FadeMode;
            public bool HasEnteredViewport;

            // Immutable snapshot of the baked star mesh, plus its largest radius. Used to rewrite
            // the vertices each frame so the apparent-size clamp never compounds.
            public required NumericsVector3[] BaseVertices;

            public float BaseHalfExtent
            {
                get
                {
                    if (_baseHalfExtent > 0f)
                        return _baseHalfExtent;

                    for (int i = 0; i < BaseVertices.Length; i++)
                        _baseHalfExtent = MathF.Max(_baseHalfExtent, BaseVertices[i].Length());

                    return _baseHalfExtent;
                }
            }

            private float _baseHalfExtent;
        }

        private enum StarFadeMode
        {
            FadingIn,
            Visible,
            FadingOutForRecycle,
            FadingOutForAltitude
        }
    }
}
