using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Ai;
using static TheOmegaStrain.Game.Helpers.OmegaObject3DHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    /// <summary>
    /// A low-poly sitting plush teddy bear: rounded belly, big round head, stubby
    /// arms and legs, button eyes, a tan muzzle with a stitched nose, and a little
    /// bow at the neck. Geometry only, in the same self-contained style as
    /// <see cref="PolarBear"/>; a static land-based prop, not an AI object.
    ///
    /// Local axes follow the game convention used by ground props:
    ///  +X = forward (the bear faces +X)
    ///   Y = width (left/right, symmetric)
    ///  +Z = up, footprint at z = 0.
    /// Colour strings are RGB hex.
    /// </summary>
    public static class TeddyBear
    {
        private const float Scale = 1.15f;

        // Plush fur (classic warm brown), shaded by facet.
        private const string FurLight = "C8935A";
        private const string FurMid = "A9773F";
        private const string FurDark = "875E30";
        private const string FurShadow = "6B4A26";

        // Muzzle, inner ears and paw pads (light tan).
        private const string PadLight = "E8CFA6";
        private const string PadMid = "D3B584";

        // Face hardware.
        private const string NoseBlack = "1A1410";
        private const string EyeBlack = "0A0806";

        // Neck bow (cheerful red).
        private const string BowTop = "C4413B";
        private const string BowSide = "9E302B";

        // Body footprint half-extents, pre-scale, for crash box / shadow.
        private const float BodyHalfX = 22f;
        private const float BodyHalfY = 24f;

        public static OmegaObject3D CreateTeddyBear(ISurface parentSurface)
        {
            var bear = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "TeddyBear",
                HasShadow = true,
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                Particles = new ParticlesAI(),
                ParentSurface = parentSurface,
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "TeddyBear" }
            };

            AddPart(bear, "TeddyBearBody", BuildBody());
            AddPart(bear, "TeddyBearLegs", BuildLegs());
            AddPart(bear, "TeddyBearArms", BuildArms());
            AddPart(bear, "TeddyBearHead", BuildHead());
            AddPart(bear, "TeddyBearEars", BuildEars());
            AddPart(bear, "TeddyBearMuzzle", BuildMuzzle());
            AddPart(bear, "TeddyBearFace", BuildFace());
            AddPart(bear, "TeddyBearBow", BuildBow());

            bear.CrashBoxes = BuildCrashBoxes();

            OmegaObject3DHelpers.ApplyScaleToObject(bear, Scale);
            OmegaObject3DHelpers.AddSimplifiedShadowPart(bear, useFlatQuad: true);
            OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(bear);

            return bear;
        }

        // ----------------------------------------------------------------------
        //  BODY  (sitting belly ellipsoid)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildBody()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            // Wide at the base so it reads as sitting; centre lifted off the ground.
            AddEllipsoid(t, cx: -1f, cy: 0f, cz: 27f, rx: 20f, ry: 23f, rz: 25f,
                stacks: 8, segments: 12, FurByFacet);
            return t;
        }

        // ----------------------------------------------------------------------
        //  HEAD  (large round head set forward and up)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildHead()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            AddEllipsoid(t, cx: 6f, cy: 0f, cz: 62f, rx: 19f, ry: 19f, rz: 18f,
                stacks: 8, segments: 12, FurByFacet);
            return t;
        }

        // ----------------------------------------------------------------------
        //  EARS  (two rounded discs, tan inner ear)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildEars()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            AddEar(t, -1f);
            AddEar(t, 1f);
            return t;
        }

        private static void AddEar(List<ITriangleMeshWithColorAndTexture> t, float side)
        {
            // Outer ear.
            AddEllipsoid(t, cx: 2f, cy: side * 14f, cz: 77f, rx: 5f, ry: 8.5f, rz: 8.5f,
                stacks: 6, segments: 10, (_, _) => FurMid);
            // Tan inner-ear button, pushed slightly forward.
            AddEllipsoid(t, cx: 8f, cy: side * 14f, cz: 77f, rx: 2.5f, ry: 5f, rz: 5f,
                stacks: 5, segments: 8, (_, _) => PadLight);
        }

        // ----------------------------------------------------------------------
        //  MUZZLE  (tan snout blister on the face front)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildMuzzle()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            AddEllipsoid(t, cx: 22f, cy: 0f, cz: 57f, rx: 8f, ry: 10f, rz: 8f,
                stacks: 6, segments: 10, (_, _) => PadLight);
            return t;
        }

        // ----------------------------------------------------------------------
        //  FACE  (stitched nose + two button eyes)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildFace()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();

            // Nose: a small dark rounded blob at the muzzle tip.
            AddEllipsoid(t, cx: 30f, cy: 0f, cz: 59f, rx: 2.6f, ry: 4f, rz: 3.2f,
                stacks: 5, segments: 8, (_, _) => NoseBlack);

            // Eyes: small dark buttons set above and beside the muzzle.
            AddEllipsoid(t, cx: 22f, cy: -8f, cz: 66f, rx: 2.4f, ry: 2.8f, rz: 3.2f,
                stacks: 5, segments: 8, (_, _) => EyeBlack);
            AddEllipsoid(t, cx: 22f, cy: 8f, cz: 66f, rx: 2.4f, ry: 2.8f, rz: 3.2f,
                stacks: 5, segments: 8, (_, _) => EyeBlack);

            return t;
        }

        // ----------------------------------------------------------------------
        //  ARMS  (stubby ellipsoids on the sides, tan paw pad facing forward)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildArms()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            AddArm(t, -1f);
            AddArm(t, 1f);
            return t;
        }

        private static void AddArm(List<ITriangleMeshWithColorAndTexture> t, float side)
        {
            AddEllipsoid(t, cx: 8f, cy: side * 22f, cz: 33f, rx: 9f, ry: 8f, rz: 13f,
                stacks: 7, segments: 10, FurByFacet);
            // Paw pad blister at the front of the paw.
            AddEllipsoid(t, cx: 15f, cy: side * 22f, cz: 30f, rx: 3.5f, ry: 5f, rz: 5f,
                stacks: 5, segments: 8, (_, _) => PadMid);
        }

        // ----------------------------------------------------------------------
        //  LEGS  (stubby ellipsoids splayed forward, tan foot pads up front)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildLegs()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            AddLeg(t, -1f);
            AddLeg(t, 1f);
            return t;
        }

        private static void AddLeg(List<ITriangleMeshWithColorAndTexture> t, float side)
        {
            AddEllipsoid(t, cx: 18f, cy: side * 12f, cz: 11f, rx: 16f, ry: 10f, rz: 10f,
                stacks: 7, segments: 10, FurByFacet);
            // Foot pad on the sole-front.
            AddEllipsoid(t, cx: 31f, cy: side * 12f, cz: 10f, rx: 3.5f, ry: 6f, rz: 6f,
                stacks: 5, segments: 8, (_, _) => PadLight);
        }

        // ----------------------------------------------------------------------
        //  BOW  (two triangular loops + a knot at the neck)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildBow()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            const float nx = 16f;   // forward, on the chest/neck
            const float nz = 46f;   // just under the chin

            // Central knot.
            AddEllipsoid(t, cx: nx + 2f, cy: 0f, cz: nz, rx: 3f, ry: 3.5f, rz: 3.5f,
                stacks: 5, segments: 8, (_, _) => BowTop);

            // Two loops as flat wedges to each side of the knot.
            AddBowLoop(t, nx, nz, -1f);
            AddBowLoop(t, nx, nz, 1f);
            return t;
        }

        private static void AddBowLoop(List<ITriangleMeshWithColorAndTexture> t, float nx, float nz, float side)
        {
            var knot = V(nx + 2f, side * 2f, nz);
            var outerTop = V(nx, side * 11f, nz + 5f);
            var outerBot = V(nx, side * 11f, nz - 5f);
            var centre = V(nx, side * 6f, nz);

            // Front and back faces of the loop wedge.
            t.Add(CreateTriangleOutward(knot, outerTop, outerBot, centre, BowTop));
            t.Add(CreateTriangleOutward(knot, outerBot, outerTop, centre, BowSide));
        }

        // ----------------------------------------------------------------------
        //  CRASH BOXES  (body + head)
        // ----------------------------------------------------------------------

        private static List<List<IVector3>> BuildCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                // Belly, arms and legs.
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    V(-24f, -BodyHalfY, 0f),
                    V(34f, BodyHalfY, 52f)),

                // Head and ears.
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    V(-13f, -18f, 44f),
                    V(33f, 18f, 82f))
            };
        }

        // ----------------------------------------------------------------------
        //  GEOMETRY HELPERS
        // ----------------------------------------------------------------------

        // A UV ellipsoid centred on (cx, cy, cz) with per-axis radii, built from
        // latitude rings stacked along Z plus a pole fan at each end. Winding is
        // enforced outward from the centre, so the surface is valid from any angle.
        // colorFn receives (latitudeIndex, segmentIndex) so callers can shade facets.
        private static void AddEllipsoid(
            List<ITriangleMeshWithColorAndTexture> t,
            float cx, float cy, float cz,
            float rx, float ry, float rz,
            int stacks, int segments,
            Func<int, int, string> colorFn)
        {
            var centre = V(cx, cy, cz);
            var bottom = V(cx, cy, cz - rz);
            var top = V(cx, cy, cz + rz);

            // Interior latitude rings (exclude the exact poles).
            var rings = new List<List<Vector3>>();
            for (int i = 1; i < stacks; i++)
            {
                double phi = -Math.PI / 2 + Math.PI * i / stacks;
                float ringScale = (float)Math.Cos(phi);
                float z = cz + rz * (float)Math.Sin(phi);
                var ring = new List<Vector3>(segments);
                for (int s = 0; s < segments; s++)
                {
                    double th = 2 * Math.PI * s / segments;
                    ring.Add(V(
                        cx + rx * ringScale * (float)Math.Cos(th),
                        cy + ry * ringScale * (float)Math.Sin(th),
                        z));
                }
                rings.Add(ring);
            }

            // Bottom pole fan.
            var first = rings[0];
            for (int s = 0; s < segments; s++)
                t.Add(CreateTriangleOutward(
                    first[s], first[(s + 1) % segments], bottom, centre, colorFn(0, s)));

            // Latitude bands.
            for (int i = 0; i < rings.Count - 1; i++)
            {
                var lower = rings[i];
                var upper = rings[i + 1];
                for (int s = 0; s < segments; s++)
                {
                    int n = (s + 1) % segments;
                    AddQuadOutward(t, lower[s], lower[n], upper[n], upper[s], centre,
                        colorFn(i + 1, s));
                }
            }

            // Top pole fan.
            var last = rings[^1];
            for (int s = 0; s < segments; s++)
                t.Add(CreateTriangleOutward(
                    last[s], top, last[(s + 1) % segments], centre, colorFn(stacks, s)));
        }

        // Facet shading that cycles the four fur tones for gentle low-poly variation.
        private static string FurByFacet(int lat, int seg)
        {
            switch ((lat + seg) % 4)
            {
                case 0: return FurLight;
                case 1: return FurMid;
                case 2: return FurDark;
                default: return FurShadow;
            }
        }

        private static void AddPart(OmegaObject3D o, string name, List<ITriangleMeshWithColorAndTexture> tris)
        {
            o.ObjectParts.Add(new OmegaObjectPart3D { PartName = name, Triangles = tris, IsVisible = true });
        }

        private static Vector3 V(float x, float y, float z) => new Vector3 { x = x, y = y, z = z };
    }
}
