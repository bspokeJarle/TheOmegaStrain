using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Projection;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Runtime.Rendering
{
    public class ObjectShadowManager
    {
        // =====================================================================
        // TUNING KNOBS — all live values grouped here so they are easy to tweak
        // from the debugger / Immediate window without rebuilding. Change any
        // of these at runtime (e.g. `ObjectShadowManager.TowerShadowNudgeX = -7;`)
        // and the next frame's shadows pick up the new value.
        //
        // Quick map:
        //   ShadowColor              - silhouette fill color ("000000" = black)
        //   StaticOffsetX/Y/Z        - legacy push for ship / surface-bound shadows
        //   BaseScale                - overall silhouette size multiplier
        //   FreeFlyingShadowScale    - extra size boost for airborne enemies
        //   ShipShadowSizeMultiplier - shared Ship/Seeder reference size adjustment
        //   AltitudeShrinkFactor     - how fast shadow shrinks as object climbs
        //   MinScale                 - lower clamp for the shrink
        //   TowerShadowSurfaceLift   - pull tower/tree shadow toward camera (-Y)
        //                              so it doesn't z-fight with the tile
        //   UniversalShadowLift      - small surface-local Y lift for all shadows
        //   ShadowSlopeX / SlopeY    - planar-projection light direction
        //                              (-X = lean left, -Y = fall behind)
        //   VertexStretchBoost       - how strongly tall silhouettes elongate
        //   TowerShadowNudgeX/Y/Z    - tower-like branch fine-tune (matched tile)
        // =====================================================================
        public static string ShadowColor = "000000";

        public static float StaticOffsetX = SurfaceGroundProjectionHelpers.DefaultShadowStaticOffsetX;
        public static float StaticOffsetY = SurfaceGroundProjectionHelpers.DefaultShadowStaticOffsetY;
        public static float StaticOffsetZ = SurfaceGroundProjectionHelpers.DefaultShadowStaticOffsetZ;

        public static float BaseScale = SurfaceGroundProjectionHelpers.DefaultShadowBaseScale;
        public static float FreeFlyingShadowScale = 1.8f;
        public static float SpaceSwanShadowScale = 0.9f;
        public static float MotherShipShadowSizeMultiplier = 0.65f;
        public static float ShipShadowSizeMultiplier = 1.15f;
        // Shared reference stays just below Seeder's 48-unit body radius even
        // with the multiplier (40 * 1.15 = 46), and below Ship's footprint.
        public const float ComparableShadowBaseRadius = 40f;
        // Clear local terrain variations across the flat silhouette, not just
        // its centre. This changes only shadow Y, never the caster's position.
        public const float FreeFlyingShadowSurfaceLift = 20f;
        // Shared flying-shadow placement in rotated Surface vertex units. Ship
        // and surface-bound objects keep their separate, static anchoring.
        // Positive inward offset subtracts Surface-vertex Z, away from the lower
        // screen edge. Vertex Z has the opposite sign to an object's render Z.
        public static float TerrainShadowInwardOffset = 230f;
        public static float TerrainShadowSideOffset = -12f;
        public static float TerrainShadowSurfaceLift = 10f;
        private static readonly OmegaMeshRotation ShadowRotation = new();
        // Gentle height response: 300 units now shrinks the base scale by 6%,
        // instead of 60%. Perspective still handles apparent camera distance.
        public static float AltitudeShrinkFactor = 0.0002f;
        public static float MinScale = SurfaceGroundProjectionHelpers.DefaultShadowMinScale;

        public static float TowerShadowSurfaceLift = 10f;
        public static float UniversalShadowLift = 10f;

        // Hard ceiling for how far up-screen ANY shadow anchor may travel.
        // The rotated tile grid has a minimum Y (the horizon row, furthest from
        // the camera). Lifts and per-object ShadowOffsets are applied on top of
        // the ground lookup and can push the anchor PAST that row, at which
        // point the shadow renders above the terrain silhouette and reads as a
        // dark blob floating in the sky (very visible during lightning flashes).
        // The anchor is clamped to (horizonY + this margin) so a shadow can get
        // close to the horizon but never above it. Increase to allow shadows
        // nearer the horizon, decrease to keep them further down the surface.
        public static float ShadowHorizonMargin = 4f;

        // Ship-only lift. The ship anchors to the frontmost ground tile, which is
        // always at ground level. On raised geometry (landing platform) the ship
        // shadow therefore ends up UNDER the platform surface and is hidden. This
        // pulls the ship shadow toward the camera (-Y = up-screen after the tilt)
        // so it clears the platform. Increase if it still hides, decrease if the
        // shadow floats too high above flat ground.
        public static float ShipShadowSurfaceLift = 36f;

        // Tower-like per-axis nudge applied AFTER the matched-tile anchor, so
        // the tower trunk and its shadow line up visually. These are the values
        // you've been iterating on — tweak freely.
        public static float TowerShadowNudgeX = -8f; // +right / -left in surface-local X
        public static float TowerShadowNudgeY = 0f;  // +down-screen / -up-screen (after tilt)
        public static float TowerShadowNudgeZ = 0f; // +further in / -closer along scroll axis

        // Per-vertex projection stretch. Base silhouette verts (z=0) stay put;
        // upper verts (z>0) are displaced along the light direction by
        // slope * boost. Larger = longer cast shadow.
        public static float VertexStretchBoost = SurfaceGroundProjectionHelpers.DefaultShadowVertexStretchBoost;

        // Global directional light (a "sun") used for proper planar projection of the
        // pre-built Shadow silhouette onto the ground plane (surface-local z = 0).
        //
        // Projection: given light direction L = (Lx, Ly, Lz) with Lz < 0 (light shining
        // downward), a model vertex v projects onto the ground plane via
        //     v' = v - L * (v.z / Lz)
        // which in component form becomes
        //     v'.x = v.x + v.z * ShadowSlopeX   where ShadowSlopeX = -Lx / Lz
        //     v'.y = v.y + v.z * ShadowSlopeY   where ShadowSlopeY = -Ly / Lz
        //     v'.z = 0
        // Ship and surface-bound shadows retain this directional-light projection.
        // Free-flying shadows instead use zero slopes to stay under their caster.
        //
        // With Lx=0.35, Ly=0, Lz=-1 the sun leans slightly to the right and straight
        // down in surface-local space, so every shadow falls the same short distance
        // to the right of its base, regardless of screen position.
        // Shadow projection slopes. A model vertex at height z projects onto the
        // ground plane at model-space offsets (z*ShadowSlopeX, z*ShadowSlopeY, 0).
        // Those offsets are then rotated by the surface tilt (X=70°), so the
        // shadow-space Y offset translates to screen (-Y + Z) on the ground plane:
        //   +Y offset (model)  -> in front of the object  (toward camera, down on screen)
        //   -Y offset (model)  -> BEHIND the object       (away from camera, up on screen)
        //   +X offset (model)  -> to the right of the object
        //   -X offset (model)  -> to the left of the object
        //
        // We want the tip of a tall object's shadow to fall BEHIND and slightly to
        // one side — i.e. negative Y and a small X component. That means the light
        // source is above, behind the camera's shoulder, shining forward and down.
        public static float ShadowSlopeX = SurfaceGroundProjectionHelpers.DefaultShadowSlopeX; // shadow leans slightly to the left
        public static float ShadowSlopeY = SurfaceGroundProjectionHelpers.DefaultShadowSlopeY; // shadow falls behind (away from camera)

        // Surface tilt. The ground plane is rotated X=70° (so tiles lean toward the
        // camera). Shadow triangles are built in surface-local space using tile
        // coordinates that are ALREADY rotated, so we must bake that same tilt into
        // the projected silhouette offsets and leave the shadow object's own
        // Rotation at zero — otherwise LiveGameLoop rotates the (base + offset)
        // vertex a SECOND time and the silhouette pops back up off the ground.

        /// <summary>
        /// Creates a black flattened shadow projected onto the surface.
        /// The shadow shares the surface's ObjectOffsets so it scrolls with the terrain.
        /// Shadow geometry is translated to the sampled surface point in surface-local space.
        /// </summary>
        public void HandleObjectShadow(OmegaObject3D inhabitant, List<OmegaObject3D> shadowList)
        {
            if (!inhabitant.HasShadow)
                return;

            var surfaceObj = GameState.SurfaceState.SurfaceViewportObject;
            if (surfaceObj?.ObjectOffsets == null)
                return;

            var rotatedTiles = inhabitant.ParentSurface?.RotatedSurfaceTriangles;
            if (rotatedTiles == null || rotatedTiles.Count == 0)
                return;

            // Render-position offsets before perspective projection.
            //
            // Flying enemies (seeders, drones, bomber, swan...) move via WorldPosition
            // — the AI only updates WorldPosition, not ObjectOffsets.x/z. The renderer
            // places them at screen X = screenCenter - localWorld.x + ObjectOffsets.x
            // (see ObjectPlacementHelpers.TryGetRenderPosition).
            //
            // These translations are added AFTER perspective scaling. Free-flying
            // shadow anchors must be converted to Surface vertex space below;
            // copying the translation into a vertex would scale it a second time.
            //
            // For objects without a WorldPosition (player ship, towers), localWorld
            // is null and objectScreenX collapses to ObjectOffsets.x.
            var localWorld = inhabitant.GetLocalWorldPosition();
            float objScreenX = (localWorld != null ? -localWorld.x : 0f)
                               + (inhabitant.ObjectOffsets?.x ?? 0f);
            float objScreenY = (localWorld != null ? -localWorld.y : 0f)
                               + (inhabitant.ObjectOffsets?.y ?? 0f);
            // Object depth and vertex depth have opposite signs in ProjectionMath:
            // the denominator is perspective + objectScreenZ - vertex.z.
            float objScreenZ = (localWorld != null ? localWorld.z : 0f)
                               + (inhabitant.ObjectOffsets?.z ?? 0f);
            float surfScreenX = surfaceObj.ObjectOffsets.x;
            float surfScreenY = surfaceObj.ObjectOffsets.y;
            float surfScreenZ = surfaceObj.ObjectOffsets.z;
            float targetX = objScreenX - surfScreenX;
            float targetZ = objScreenZ - surfScreenZ;

            // Classify object once (avoid repeated string allocations / contains checks).
            // IMPORTANT: use exact equality for "Ship" — substring match would also
            // catch names like "MotherShipSmall" and wrongly route it to the ship
            // branch (which anchors to the frontmost platform tile). Tower classifier
            // stays substring-based because it's genuinely tower-like whenever the
            // name contains "tower" or a SurfaceBasedId is set.
            string name = inhabitant.ObjectName ?? string.Empty;
            bool isShip = name.Equals("Ship", StringComparison.OrdinalIgnoreCase);
            bool isSeeder = name.Equals("Seeder", StringComparison.OrdinalIgnoreCase);
            bool isSpaceSwan = name.Equals("SpaceSwan", StringComparison.OrdinalIgnoreCase);
            bool hasComparableShadow = isShip || isSeeder;
            bool isTowerLike = !isShip && (inhabitant.SurfaceBasedId != null
                                           || name.IndexOf("tower", StringComparison.OrdinalIgnoreCase) >= 0);
            bool isFreeFlying = !isShip && !isTowerLike;
            // Per-vertex terrain conformity is still a Seeder-only trial.
            bool conformsToTerrain = isSeeder && isFreeFlying;

            // Compute only the shadow base actually needed for this object type.
            // All *BaseX/Y/Z are in SURFACE-LOCAL space because the shadow OmegaObject3D
            // is parented to surface.ObjectOffsets; the renderer adds surfaceX/Y/Z.
            // targetX = objScreenX - surfScreenX is the object's X in surface space.
            float shadowBaseX = targetX;
            float shadowBaseY;
            float shadowBaseZ = 0f;
            float flyingAltitude = 0f;

            if (isShip)
            {
                if (!SurfaceGroundProjectionHelpers.TryGetFrontmostSurfaceGroundPoint(
                        rotatedTiles,
                        targetX,
                        out shadowBaseX,
                        out shadowBaseY,
                        out shadowBaseZ))
                    return;

                // Use the same local ground-clearance measurement as flying
                // enemies, but keep Ship's existing shadow placement untouched.
                if (OmegaPerspectiveProjectorFactory.TryGetSurfaceLocalShadowAnchor(
                        new RenderPosition(objScreenX, objScreenY, objScreenZ),
                        new RenderPosition(surfScreenX, surfScreenY, surfScreenZ),
                        out float shipLocalX, out float shipLocalY, out float shipLocalZ)
                    && TryGetSurfaceGroundPoint(rotatedTiles, shipLocalX, shipLocalZ, out _, out float shipGroundY, out _))
                    flyingAltitude = MathF.Max(0f, shipGroundY - shipLocalY);

                // Lift the ship shadow up-screen so it stays visible on top of
                // raised geometry (landing platform) instead of being occluded
                // by it. On flat ground the lift is small enough to still read
                // as a shadow sitting on the surface.
                shadowBaseY -= ShipShadowSurfaceLift;
            }
            else if (isTowerLike)
            {
                // Direct tile lookup by SurfaceBasedId (O(N) scan, single pass, no closure)
                ITriangleMeshWithColorAndTexture matchedTile = null;
                if (inhabitant.SurfaceBasedId != null)
                {
                    long sid = (long)inhabitant.SurfaceBasedId;
                    for (int i = 0; i < rotatedTiles.Count; i++)
                    {
                        var t = rotatedTiles[i];
                        if (t.landBasedPosition.HasValue && t.landBasedPosition.Value == sid)
                        {
                            matchedTile = t;
                            break;
                        }
                    }
                }

                if (matchedTile != null)
                {
                    // Learn from the ship branch: tile provides ground Y and Z
                    // (so the shadow sits on the surface), but X is shifted by
                    // the OO.x delta between the tower and the surface so the
                    // shadow lines up under the tower's visible trunk. Same
                    // trick the ship uses via targetX.
                    float tileCenterX = (matchedTile.vert1.x + matchedTile.vert2.x + matchedTile.vert3.x) / 3f;
                    shadowBaseX = tileCenterX + targetX + TowerShadowNudgeX;
                    shadowBaseY = (matchedTile.vert1.y + matchedTile.vert2.y + matchedTile.vert3.y) / 3f
                                  - TowerShadowSurfaceLift + TowerShadowNudgeY;
                    shadowBaseZ = (matchedTile.vert1.z + matchedTile.vert2.z + matchedTile.vert3.z) / 3f
                                  + TowerShadowNudgeZ;
                }
                else
                {
                    // Fallback: tile with center X closest to object's X
                    float nearestTileY = 0f;
                    float minDistX = float.MaxValue;
                    for (int i = 0; i < rotatedTiles.Count; i++)
                    {
                        var tile = rotatedTiles[i];
                        float tileCenterX = (tile.vert1.x + tile.vert2.x + tile.vert3.x) / 3f;
                        float dx = MathF.Abs(tileCenterX - targetX);
                        if (dx < minDistX)
                        {
                            minDistX = dx;
                            nearestTileY = (tile.vert1.y + tile.vert2.y + tile.vert3.y) / 3f;
                        }
                    }
                    shadowBaseY = nearestTileY;
                }
            }
            else
            {
                // Match the caster AFTER perspective projection. Surface uses its
                // own render anchor, so convert the caster's position into its
                // vertex space before choosing a ground tile. In particular, Z
                // must run the opposite way to object-position Z.
                if (!OmegaPerspectiveProjectorFactory.TryGetSurfaceLocalShadowAnchor(
                        new RenderPosition(objScreenX, objScreenY, objScreenZ),
                        new RenderPosition(surfScreenX, surfScreenY, surfScreenZ),
                        out shadowBaseX, out float casterLocalY, out shadowBaseZ))
                    return;

                if (!TryGetSurfaceGroundPoint(rotatedTiles, shadowBaseX, shadowBaseZ, out _, out float groundY, out _))
                    return;

                shadowBaseY = groundY;
                // Both values are in Surface vertex units. Do not subtract the
                // caster's screen-offset Y from unprojected ground Y. Measure
                // before the shadow-only lift/nudge so those cannot change size.
                flyingAltitude = MathF.Max(0f, groundY - casterLocalY);

                // Every free-flying caster uses the same nudge before scaling
                // and projection. Ship never enters this branch. Height scaling
                // still uses the original clearance, not the nudged terrain.
                shadowBaseX += TerrainShadowSideOffset;
                shadowBaseZ -= TerrainShadowInwardOffset;
                // The sampler falls back to the nearest tile outside the mesh;
                // reject off-surface anchors rather than snapping to an edge.
                if (!IsWithinSurfaceBounds(rotatedTiles, shadowBaseX, shadowBaseZ))
                    return;
                if ((TerrainShadowSideOffset != 0f || TerrainShadowInwardOffset != 0f)
                    && !TryGetSurfaceGroundPoint(rotatedTiles, shadowBaseX, shadowBaseZ, out _, out shadowBaseY, out _))
                    return;
            }

            // Ship and Seeder share a height curve and reference footprint.
            // Other objects retain their existing size tuning.
            float altitude = !isTowerLike ? flyingAltitude : MathF.Max(0f, surfScreenY - objScreenY);
            float baseScale = isSpaceSwan ? BaseScale * SpaceSwanShadowScale
                : (hasComparableShadow || isTowerLike) ? BaseScale : BaseScale * FreeFlyingShadowScale;
            float scale = MathF.Max(MinScale, baseScale - altitude * AltitudeShrinkFactor);
            if (hasComparableShadow)
                scale *= ShipShadowSizeMultiplier;
            // Apply after height scaling/clamping: all three mothership shadows
            // become 35% smaller without moving their ground anchors.
            if (name.Equals("MotherShipSmall", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MotherShipMedium", StringComparison.OrdinalIgnoreCase)
                || name.Equals("MotherShipLarge", StringComparison.OrdinalIgnoreCase))
                scale *= MotherShipShadowSizeMultiplier;

            // Reuse the pre-built silhouette and the engine's planar projection.

            var shadowParts = new List<I3dObjectPart>(1);

            // Performance: only objects with a pre-built low-poly "Shadow" part
            // (IsVisible = false, added at object creation) get a shadow. No
            // fallback to projecting full meshes — that cost is forbidden.
            I3dObjectPart simplifiedShadowPart = null;
            for (int i = 0; i < inhabitant.ObjectParts.Count; i++)
            {
                if (inhabitant.ObjectParts[i].PartName == "Shadow")
                {
                    simplifiedShadowPart = inhabitant.ObjectParts[i];
                    break;
                }
            }

            if (simplifiedShadowPart == null
                || simplifiedShadowPart.Triangles == null
                || simplifiedShadowPart.Triangles.Count == 0)
                return;

            // Keep the existing ship/surface-bound tuning. Airborne shadows need
            // no sideways push; only a tiny Y lift above the sampled ground.
            float shadowOffsetX = isShip ? StaticOffsetX : 0f;
            float shadowOffsetY = isShip ? StaticOffsetY : 0f;
            float shadowOffsetZ = isFreeFlying ? 0f : StaticOffsetZ;
            if (isFreeFlying)
                shadowBaseY -= FreeFlyingShadowSurfaceLift;

            // Geometry is now in Surface vertex space, including the perspective
            // conversion for flying casters. Render it with Surface's own offsets.
            Vector3 shadowObjectOffsets = new Vector3
            {
                x = surfaceObj.ObjectOffsets.x,
                y = surfaceObj.ObjectOffsets.y,
                z = surfaceObj.ObjectOffsets.z
            };

            // Per-object fine-tuning. Any object can set ShadowOffset (in
            // surface-local X/Y/Z) to nudge its shadow anchor. Positive X = right,
            // positive Y = up-screen (further from camera after the tilt),
            // positive Z = farther up the scroll axis. Keep values small —
            // typically a few units — for subtle alignment corrections.
            if (inhabitant.ShadowOffset != null)
            {
                shadowBaseX += inhabitant.ShadowOffset.x;
                shadowBaseY += inhabitant.ShadowOffset.y;
                shadowBaseZ += inhabitant.ShadowOffset.z;
            }

            // Final safety clamp, applied AFTER every lift and per-object offset.
            // Measure the actual top of the terrain (smallest Y in the rotated
            // tile grid = the horizon row) and refuse to place the shadow anchor
            // above it. Without this, the accumulated lifts can drive the anchor
            // off the surface and the shadow appears as a floating shape in the
            // sky behind the terrain. Clamping instead of discarding keeps the
            // shadow present but pinned to the far edge of the ground.
            if (!conformsToTerrain && TryGetSurfaceHorizonY(rotatedTiles, out float horizonY))
            {
                float minAllowedY = horizonY + ShadowHorizonMargin;
                if (shadowBaseY < minAllowedY)
                    shadowBaseY = minAllowedY;
            }

            {
                var part = simplifiedShadowPart;

                var shadowTriangles = new List<ITriangleMeshWithColorAndTexture>(part.Triangles.Count);
                var projectionOptions = CreateObjectShadowProjectionOptions(
                    shadowBaseX,
                    shadowBaseY,
                    shadowBaseZ,
                    shadowOffsetX,
                    shadowOffsetY,
                    shadowOffsetZ,
                    scale,
                    isFreeFlying);

                for (int i = 0; i < part.Triangles.Count; i++)
                {
                    var tri = part.Triangles[i];
                    if (isFreeFlying)
                        tri = RemoveSurfacePitch(tri);
                    var projected = ObjectShadowProjectionMath.ProjectModelTriangleShadow(tri, projectionOptions);

                    // 1. Project each vertex onto the model-space ground plane (z=0)
                    //    along the global light direction, using the boosted slopes
                    //    so tall silhouettes (tower/tree prisms) actually stretch.
                    //    Verts at z=0 stay put; verts at z=H land at
                    //    (x + H*vStretchX, y + H*vStretchY, 0).

                    // 2. Rotate that flat silhouette by the surface tilt (X = 70°)
                    //    so it lies in the tilted ground plane. A point (x, y, 0)
                    //    rotated about X becomes (x, y*cos, y*sin).
                    //    The silhouette is scaled, then added to shadowBase (which
                    //    comes from the already-rotated tile mesh). shadow.Rotation
                    //    is (0,0,0) so LiveGameLoop does NOT rotate these again.
                    shadowTriangles.Add(new TriangleMeshWithColor
                    {
                        Color = ShadowColor,
                        vert1 = ToVector3(projected.Vertex1),
                        vert2 = ToVector3(projected.Vertex2),
                        vert3 = ToVector3(projected.Vertex3),
                        noHidden = true
                    });
                }

                if (hasComparableShadow)
                    NormalizeShadowSize(shadowTriangles,
                        new Vector3(shadowBaseX + shadowOffsetX, shadowBaseY + shadowOffsetY, shadowBaseZ + shadowOffsetZ),
                        ComparableShadowBaseRadius * scale);

                // Size and rotation must be final BEFORE sampling terrain. Only
                // generated shadow vertices change, never the caster or its offsets.
                if (conformsToTerrain)
                {
                    TerrainShadowProjectionHelpers.ConformToSurface(
                        shadowTriangles, rotatedTiles, TerrainShadowSurfaceLift);
                    if (shadowTriangles.Count == 0)
                        return;
                }

                shadowParts.Add(new OmegaObjectPart3D
                {
                    PartName = "ObjectShadow",
                    Triangles = shadowTriangles,
                    IsVisible = true
                });
            }

            // Shadow uses the surface's ObjectOffsets and WorldPosition so it scrolls with the terrain
            shadowList.Add(new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "ObjectShadow",
                WorldPosition = new Vector3(),
                ParentSurface = inhabitant.ParentSurface,
                ObjectParts = shadowParts,
                ObjectOffsets = shadowObjectOffsets,
                Rotation = new Vector3 { x = 0, y = 0, z = 0 }
            });
        }

        private static void NormalizeShadowSize(List<ITriangleMeshWithColorAndTexture> triangles, Vector3 anchor, float radius)
        {
            // Normalize only the generated shadow around its existing anchor.
            // A radial measure does not change as the object turns; silhouette
            // proportions are preserved, and the caster's mesh is never touched.
            float radiusSquared = 0f;
            foreach (var triangle in triangles)
            {
                radiusSquared = MathF.Max(radiusSquared, GeometryMath.GetDistanceSquared(triangle.vert1, anchor));
                radiusSquared = MathF.Max(radiusSquared, GeometryMath.GetDistanceSquared(triangle.vert2, anchor));
                radiusSquared = MathF.Max(radiusSquared, GeometryMath.GetDistanceSquared(triangle.vert3, anchor));
            }
            if (radiusSquared <= 0.0001f)
                return;
            float factor = radius / MathF.Sqrt(radiusSquared);
            foreach (var triangle in triangles)
            {
                triangle.vert1 = Resize(triangle.vert1);
                triangle.vert2 = Resize(triangle.vert2);
                triangle.vert3 = Resize(triangle.vert3);
            }
            Vector3 Resize(IVector3 vertex) => ToVector3(VectorMath.Add(anchor,
                VectorMath.Multiply(VectorMath.Subtract(vertex, anchor), factor)));
        }

        internal static bool TryGetSurfaceGroundPoint(
            IReadOnlyList<ITriangleMeshWithColorAndTexture> rotatedTiles,
            float targetX,
            float targetZ,
            out float groundX,
            out float groundY,
            out float groundZ)
        {
            return SurfaceGroundProjectionHelpers.TryGetSurfaceGroundPoint(
                rotatedTiles,
                targetX,
                targetZ,
                out groundX,
                out groundY,
                out groundZ);
        }

        /// <summary>
        /// True when (targetX, targetZ) lies inside the axis-aligned bounds of the
        /// rotated tile grid. Used to reject shadow casters that are outside the
        /// terrain, where the ground lookup would otherwise silently fall back to
        /// the nearest edge tile and misplace the shadow.
        /// </summary>
        internal static bool IsWithinSurfaceBounds(
            IReadOnlyList<ITriangleMeshWithColorAndTexture> rotatedTiles,
            float targetX,
            float targetZ)
        {
            if (rotatedTiles == null || rotatedTiles.Count == 0)
                return false;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < rotatedTiles.Count; i++)
            {
                var tile = rotatedTiles[i];
                AccumulateBounds(tile.vert1, ref minX, ref maxX, ref minZ, ref maxZ);
                AccumulateBounds(tile.vert2, ref minX, ref maxX, ref minZ, ref maxZ);
                AccumulateBounds(tile.vert3, ref minX, ref maxX, ref minZ, ref maxZ);
            }

            return targetX >= minX && targetX <= maxX
                && targetZ >= minZ && targetZ <= maxZ;
        }

        /// <summary>
        /// Smallest Y across the rotated tile grid, i.e. the horizon row that is
        /// furthest from the camera after the surface tilt. Shadow anchors above
        /// this value would render off the terrain and float in the sky.
        /// </summary>
        internal static bool TryGetSurfaceHorizonY(
            IReadOnlyList<ITriangleMeshWithColorAndTexture> rotatedTiles,
            out float horizonY)
        {
            horizonY = 0f;

            if (rotatedTiles == null || rotatedTiles.Count == 0)
                return false;

            float minY = float.MaxValue;
            for (int i = 0; i < rotatedTiles.Count; i++)
            {
                var tile = rotatedTiles[i];
                if (tile.vert1.y < minY) minY = tile.vert1.y;
                if (tile.vert2.y < minY) minY = tile.vert2.y;
                if (tile.vert3.y < minY) minY = tile.vert3.y;
            }

            horizonY = minY;
            return true;
        }

        private static void AccumulateBounds(
            IVector3 vertex,
            ref float minX,
            ref float maxX,
            ref float minZ,
            ref float maxZ)
        {
            if (vertex.x < minX) minX = vertex.x;
            if (vertex.x > maxX) maxX = vertex.x;
            if (vertex.z < minZ) minZ = vertex.z;
            if (vertex.z > maxZ) maxZ = vertex.z;
        }

        private static ObjectShadowProjectionOptions CreateObjectShadowProjectionOptions(
            float shadowBaseX,
            float shadowBaseY,
            float shadowBaseZ,
            float shadowOffsetX,
            float shadowOffsetY,
            float shadowOffsetZ,
            float scale,
            bool isFreeFlying)
        {
            return new ObjectShadowProjectionOptions
            {
                ShadowBaseX = shadowBaseX,
                ShadowBaseY = shadowBaseY,
                ShadowBaseZ = shadowBaseZ,
                ShadowOffsetX = shadowOffsetX,
                ShadowOffsetY = shadowOffsetY,
                ShadowOffsetZ = shadowOffsetZ,
                Scale = scale,
                ShadowSlopeX = isFreeFlying ? 0f : ShadowSlopeX,
                ShadowSlopeY = isFreeFlying ? 0f : ShadowSlopeY,
                VertexStretchBoost = VertexStretchBoost,
                SurfaceTiltDegrees = WorldViewSetup.SurfacePitchDegrees
            };
        }

        private static ITriangleMeshWithColorAndTexture RemoveSurfacePitch(ITriangleMeshWithColorAndTexture triangle)
        {
            // LiveGameLoop already rotated every part, including the hidden Shadow.
            // The engine projector expects an UNTILTED footprint and adds surface pitch
            // itself. Undo only that pitch, keeping the object's heading/bank. Work on
            // copies: never rotate the object's geometry, guides or crash boxes here.
            float undoPitch = -WorldViewSetup.SurfacePitchDegrees;
            return new TriangleMeshWithColor
            {
                vert1 = ShadowRotation.RotatePoint(undoPitch, triangle.vert1, 'X'),
                vert2 = ShadowRotation.RotatePoint(undoPitch, triangle.vert2, 'X'),
                vert3 = ShadowRotation.RotatePoint(undoPitch, triangle.vert3, 'X')
            };
        }

        private static Vector3 ToVector3(IVector3 vector)
        {
            return new Vector3
            {
                x = vector.x,
                y = vector.y,
                z = vector.z
            };
        }
    }
}
