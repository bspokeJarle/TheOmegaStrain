using TheOmegaStrain.Game.World;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheOmegaStrain.Runtime.Rendering
{
    public class ParticleManager
    {
        private readonly OmegaMeshRotation Rotate3d = new();

        // =====================================================================
        // PARTICLE SHADOW TUNING KNOBS — mirrors the pattern used by
        // ObjectShadowManager. `public static` so you can tweak live from the
        // debugger / Immediate window without rebuilding.
        //
        //   ParticleShadowSize       - half-extent of the flat shadow blob
        //                              (model-space units, at ground z=0)
        //   BaseProjectedScale       - overall size multiplier
        //   MinProjectedScale        - lower clamp as altitude grows
        //   AltitudeShrinkFactor     - how fast shadow shrinks with altitude
        //   ParticleShadowLift       - tiny -Y lift so blob sits above the
        //                              tile and doesn't z-fight with it
        // =====================================================================
        public static string ShadowColor = "000000";
        public static float ParticleShadowSize = 6.0f;
        public static float BaseProjectedScale = 1.0f;
        public static float MinProjectedScale = 0.3f;
        public static float AltitudeShrinkFactor = 0.003f;
        public static float ParticleShadowLift = 2f;

        // How strongly altitude displaces the shadow along the light direction.
        // Kept intentionally small (and clamped) because particle altitude is
        // measured in screen units, which is ~10x the magnitude of a model-
        // space vertex z used by ObjectShadowManager. A full 1.0 here would
        // launch the shadow hundreds of units off the ground point.
        public static float ParticleAltitudeProjection = 0.15f;
        public static float MaxParticleAltitudeForProjection = 120f;
        public static float ParticleShadowMinAltitude = 12f;

        // Cached surface-tilt trig — mirrors ObjectShadowManager so particle
        // shadows rotate onto the ground plane the same way.

        public void HandleParticles(OmegaObject3D inhabitant, List<OmegaObject3D> particleObjectList)
        {
            if (inhabitant.Particles?.Particles is null || inhabitant.Particles.Particles.Count == 0)
                return;

            List<Particle> particles;
            lock (inhabitant.Particles)
            {
                particles = inhabitant.Particles.Particles.OfType<Particle>().Where(p => p.Visible).ToList();
            }

            var surfaceObj = GameState.SurfaceState.SurfaceViewportObject;
            bool canRenderGroundShadow = surfaceObj?.ObjectOffsets != null;

            float surfaceY = canRenderGroundShadow ? surfaceObj!.ObjectOffsets.y : 0f;
            float surfaceX = canRenderGroundShadow ? surfaceObj!.ObjectOffsets.x : 0f;

            // Tile cache for ground projection.
            var rotatedTiles = canRenderGroundShadow ? inhabitant.ParentSurface?.RotatedSurfaceTriangles : null;

            // Pull the same light direction used by ObjectShadowManager so all
            // shadows (objects + particles) cast in one consistent direction.
            // We intentionally do NOT multiply by VertexStretchBoost here —
            // that boost is tuned for model-space vertex z values (~10–60),
            // whereas particle altitude is a much larger screen-space number.
            float slopeX = ObjectShadowManager.ShadowSlopeX;
            float slopeY = ObjectShadowManager.ShadowSlopeY;

            foreach (var particle in particles)
            {
                var particleTriangle = RotateParticle(particle.ParticleTriangle, particle.Rotation as Vector3);

                if (inhabitant.ObjectOffsets == null)
                    continue;

                float particleOffsetX = inhabitant.ObjectOffsets.x + particle.Position.x;
                float particleOffsetY = inhabitant.ObjectOffsets.y + particle.Position.y;
                float particleOffsetZ = inhabitant.ObjectOffsets.z + particle.Position.z;

                // Original particle — rendered as its actual colored triangle in 3D space
                particleObjectList.Add(new OmegaObject3D
                {
                    ObjectId = GameState.ObjectIdCounter++,
                    ObjectName = "Particle",
                    WorldPosition = particle.WorldPosition,
                    SurfaceBasedId = inhabitant.SurfaceBasedId.HasValue && inhabitant.SurfaceBasedId.Value > 0
                        ? inhabitant.SurfaceBasedId
                        : null,
                    ParentSurface = inhabitant.ParentSurface,
                    ObjectParts = new List<I3dObjectPart>
                    {
                        new OmegaObjectPart3D { Triangles = new List<ITriangleMeshWithColor> { particleTriangle }, PartName = "Particle", IsVisible = true }
                    },
                    ObjectOffsets = new Vector3
                    {
                        x = particleOffsetX,
                        y = particleOffsetY,
                        z = particleOffsetZ
                    },
                    ZSortBias = inhabitant.ZSortBias,
                    CrashBoxes = CreateCrashBoxFromTriangle(particleTriangle),
                    ImpactStatus = new ImpactStatus { HasCrashed = false, SourceParticle = particle, HasExploded = false, ObjectName = inhabitant.ObjectName },
                    Rotation = particle.Rotation
                });

                if (!canRenderGroundShadow)
                    continue;

                // ---------------------------------------------------------
                // GROUND-PARENTED PARTICLE SHADOW
                //
                // The shadow belongs to the GROUND, not the particle.
                // Approach mirrors ObjectShadowManager:
                //   1. Find the ground point under the particle's X/Z.
                //   2. Compute an "altitude" = how high above the surface
                //      the particle is on screen (surfaceY - particleY).
                //   3. Project along the global light direction, using
                //      altitude as the effective Z-displacement. Taller
                //      particles cast further away from their ground point.
                //   4. Rotate the projected offset through the surface tilt
                //      so the shadow lies in the tilted ground plane.
                //   5. Bake result into model-space verts (with a small
                //      fixed silhouette, NOT the rotated particle tri), set
                //      Rotation=(0,0,0), parent to surface.ObjectOffsets.
                // ---------------------------------------------------------

                // Find the ground point under the particle. Mirrors the
                // free-flying branch in ObjectShadowManager: after the X=70°
                // surface tilt, tile centers are laid out primarily in the
                // X-Z plane (X = lateral, Z = scroll axis), while Y is the
                // small ground-depth component. So we match tiles by
                // (X, Z), not (X, Y) — matching by Y would snap shadows to
                // whatever tile has a Y near the particle's screen-Y and
                // spread them across the bottom of the screen.
                //   - X from the PARTICLE (continuous, not snapped)
                //   - Z from the PARTICLE (continuous, not snapped)
                //   - Y interpolated from the surface triangle below
                float targetX = particleOffsetX - surfaceX;
                float targetZ = particleOffsetZ - surfaceObj!.ObjectOffsets.z;
                float groundLocalX = targetX;
                float groundLocalY = 0f;
                float groundLocalZ = targetZ;
                bool grounded = false;

                if (rotatedTiles != null && rotatedTiles.Count > 0)
                {
                    grounded = SurfaceGroundProjectionHelpers.TryGetSurfaceGroundPoint(
                        rotatedTiles,
                        targetX,
                        targetZ,
                        out groundLocalX,
                        out groundLocalY,
                        out groundLocalZ);
                }

                if (!grounded) continue;

                // Altitude = how far the particle is ABOVE the surface on screen.
                // (In this project +Y is down-screen, so higher = smaller Y.)
                // Clamped so very-high particles (e.g. bombers, explosions) still
                // produce visible shadows near their ground point.
                float groundScreenY = surfaceY + groundLocalY;
                if (!ShouldRenderParticleShadow(inhabitant.ObjectName, particleOffsetY, groundScreenY))
                    continue;

                var shadowProjection = GroundShadowProjectionMath.ProjectTriangleShadow(
                    particleOffsetY,
                    groundScreenY,
                    groundLocalX,
                    groundLocalY,
                    groundLocalZ,
                    CreateParticleShadowProjectionOptions(slopeX, slopeY));

                // Rotate the flat (projX, projY) offset through the surface
                // tilt (X=70°) so it lies on the tilted ground plane.
                //   (x, y, 0) rotated about X -> (x, y*cos, y*sin)

                // Small fixed silhouette — a flat triangle at z=0 in model
                // space, sized by `scale`. We DO NOT use the rotated
                // particle triangle: a tumbling particle would produce a
                // wildly flickering shadow. The silhouette is a tiny blob.
                var shadowTriangle = new TriangleMeshWithColor
                {
                    Color = ShadowColor,
                    vert1 = ToVector3(shadowProjection.Vertex1),
                    vert2 = ToVector3(shadowProjection.Vertex2),
                    vert3 = ToVector3(shadowProjection.Vertex3),
                    noHidden = true
                };

                particleObjectList.Add(new OmegaObject3D
                {
                    ObjectId = GameState.ObjectIdCounter++,
                    ObjectName = "ParticleShadow",
                    WorldPosition = new Vector3(),
                    ParentSurface = inhabitant.ParentSurface,
                    ObjectParts = new List<I3dObjectPart>
                    {
                        new OmegaObjectPart3D { Triangles = new List<ITriangleMeshWithColor> { shadowTriangle }, PartName = "ParticleShadow", IsVisible = true }
                    },
                    ObjectOffsets = new Vector3
                    {
                        x = surfaceObj!.ObjectOffsets.x,
                        y = surfaceObj!.ObjectOffsets.y,
                        z = surfaceObj!.ObjectOffsets.z
                    },
                    // Tilt is already baked into the verts — keep Rotation
                    // at zero so LiveGameLoop doesn't rotate them AGAIN.
                    Rotation = new Vector3 { x = 0, y = 0, z = 0 }
                });
            }
        }

        public static bool ShouldRenderParticleShadow(string? sourceObjectName, float particleScreenY, float groundScreenY)
        {
            if (string.Equals(sourceObjectName, "JumpingFish", StringComparison.Ordinal))
                return false;

            return groundScreenY - particleScreenY > ParticleShadowMinAltitude;
        }

        private static GroundShadowProjectionOptions CreateParticleShadowProjectionOptions(float slopeX, float slopeY)
        {
            return new GroundShadowProjectionOptions
            {
                ShadowSize = ParticleShadowSize,
                BaseProjectedScale = BaseProjectedScale,
                MinProjectedScale = MinProjectedScale,
                AltitudeShrinkFactor = AltitudeShrinkFactor,
                AltitudeProjection = ParticleAltitudeProjection,
                MaxAltitudeForProjection = MaxParticleAltitudeForProjection,
                ShadowLift = ParticleShadowLift,
                ShadowSlopeX = slopeX,
                ShadowSlopeY = slopeY,
                SurfaceTiltDegrees = WorldViewSetup.SurfacePitchDegrees
            };
        }

        //Creates a crashbox from a triangle
        private List<List<IVector3>> CreateCrashBoxFromTriangle(ITriangleMeshWithColor triangle)
        {
            return MeshGeometryOperations.GenerateTriangleAabbCrashBox(
                triangle,
                static (x, y, z) => new Vector3 { x = x, y = y, z = z });
        }

        private ITriangleMeshWithColor RotateParticle(ITriangleMeshWithColor particleTriangle, Vector3 rotation)
        {
            var triangleCopy = CopyParticleTriangle(particleTriangle);

            return Rotate3d.RotateXMesh(
                Rotate3d.RotateYMesh(
                    Rotate3d.RotateZMesh(new List<ITriangleMeshWithColor> { triangleCopy }, rotation.z),
                    rotation.y
                ),
                rotation.x
            ).First();
        }

        private static TriangleMeshWithColor CopyParticleTriangle(ITriangleMeshWithColor triangle)
        {
            return new TriangleMeshWithColor
            {
                Color = triangle.Color,
                angle = triangle.angle,
                landBasedPosition = triangle.landBasedPosition,
                noHidden = triangle.noHidden,
                normal1 = CopyVector(triangle.normal1),
                normal2 = CopyVector(triangle.normal2),
                normal3 = CopyVector(triangle.normal3),
                vert1 = CopyVector(triangle.vert1),
                vert2 = CopyVector(triangle.vert2),
                vert3 = CopyVector(triangle.vert3)
            };
        }

        private static Vector3 CopyVector(IVector3 vector)
        {
            return new Vector3
            {
                x = vector.x,
                y = vector.y,
                z = vector.z
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
