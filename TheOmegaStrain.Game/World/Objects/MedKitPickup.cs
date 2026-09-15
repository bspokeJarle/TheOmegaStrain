using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Ai;
using static TheOmegaStrain.Game.Helpers.OmegaObject3DHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    /// <summary>
    /// A floating first-aid "med-kit" healing pickup: a rounded case with a raised
    /// red cross, a carry handle, latches, a hinge, corner rivets and a band-aid laid
    /// across the lid. Geometry only, in the same style as <see cref="PowerUp"/>; it is
    /// not wired into gameplay. Local axes follow the game convention: +X forward,
    /// Y width, +Z up; pivot at the geometric centre. Colour strings are RGB hex.
    /// </summary>
    public static class MedKitPickup
    {
        private const float ZoomRatio = 1f;

        // Case shell (cream/white first-aid case).
        private const string ShellLight = "EDEFF2";
        private const string ShellMid = "CDD3DB";
        private const string ShellDark = "9AA3AE";
        private const string ShellUnder = "6E7682";
        private const string SeamDark = "3A4048";

        // Red cross emblem.
        private const string CrossTop = "D83A2E";
        private const string CrossSide = "B02A20";

        // Band-aid / plaster (the tribute).
        private const string BandTop = "E6C79E";
        private const string BandSide = "CBA97E";
        private const string PadColor = "F4E9D6";

        // Hardware (handle, latches, hinge, rivets).
        private const string MetalDark = "3E444C";
        private const string MetalMid = "8B919B";

        // Case half-extents (pre-scale, model units).
        private const float CaseHalfX = 40f;
        private const float CaseHalfY = 27f;
        private const float CaseHalfZ = 17f;
        private const float CaseCornerR = 9f;
        private const int CaseCornerSegs = 6;   // -> 4*(segs+1) points per ring

        public static OmegaObject3D CreateMedKit(ISurface parentSurface)
        {
            var medkit = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                Particles = new ParticlesAI(),
                ParentSurface = parentSurface,
                ObjectName = "MedKitPickup",
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "MedKitPickup" },
                HasShadow = true
            };

            AddPart(medkit, "MedKitCase", BuildCase());
            AddPart(medkit, "MedKitCross", BuildCross());
            AddPart(medkit, "MedKitBandAid", BuildBandAid());
            AddPart(medkit, "MedKitHandle", BuildHandle());
            AddPart(medkit, "MedKitHardware", BuildHardware());

            medkit.CrashBoxes = BuildCrashBoxes();

            OmegaObject3DHelpers.ApplyScaleToObject(medkit, ZoomRatio);
            OmegaObject3DHelpers.AddSimplifiedShadowPart(medkit, useFlatQuad: true);

            return medkit;
        }

        // ----------------------------------------------------------------------
        //  CASE  (rounded box built from stacked rounded-rectangle rings)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildCase()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            var centre = V(0, 0, 0);

            // Vertical profile: (z, footprint scale). Ends are inset to chamfer the
            // top/bottom edges; the middle carries the lid seam colour.
            float[] z = { -CaseHalfZ, -16f, -13f, 0f, 13f, 16f, CaseHalfZ };
            float[] s = { 0.80f, 0.93f, 1.00f, 1.00f, 1.00f, 0.93f, 0.80f };

            var rings = new List<List<Vector3>>();
            for (int i = 0; i < z.Length; i++)
                rings.Add(RoundedRectLoop(0, 0, z[i],
                    CaseHalfX * s[i], CaseHalfY * s[i], CaseCornerR * s[i],
                    CaseCornerSegs));

            for (int i = 0; i < rings.Count - 1; i++)
            {
                // The band straddling z = 0 reads as the lid split.
                bool seam = z[i] < 0f && z[i + 1] > 0f;
                string cA = seam ? SeamDark : (i < 3 ? ShellUnder : ShellLight);
                string cB = seam ? SeamDark : (i < 3 ? ShellDark : ShellMid);
                AddLoopBand(t, rings[i], rings[i + 1], centre, cA, cB);
            }

            AddCapFan(t, rings[0], V(0, 0, z[0]), centre, ShellUnder);
            AddCapFan(t, rings[^1], V(0, 0, z[^1]), centre, ShellLight);
            return t;
        }

        // ----------------------------------------------------------------------
        //  RED CROSS  (two rounded slabs -> convex parts, valid one-centre winding)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildCross()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            const float cx = -13f, cy = 4f;          // back-left of the lid
            const float zBot = 16.5f, zTop = 22f;
            AddSlab(t, cx, cy, zBot, zTop, 13f, 4f, 1.5f, 3, 0f, CrossTop, CrossSide);
            AddSlab(t, cx, cy, zBot, zTop, 4f, 13f, 1.5f, 3, 0f, CrossTop, CrossSide);
            return t;
        }

        // ----------------------------------------------------------------------
        //  BAND-AID  (thin rounded slab laid diagonally on the lid, with a pad)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildBandAid()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            const float cx = 15f, cy = -6f;          // front-right of the lid
            float angle = (float)(30.0 * Math.PI / 180.0);
            AddSlab(t, cx, cy, 16.3f, 18.4f, 22f, 7f, 6f, 5, angle, BandTop, BandSide);
            // Central absorbent pad, slightly proud of the strip.
            AddSlab(t, cx, cy, 18.4f, 19.0f, 9f, 4.5f, 3f, 3, angle, PadColor, PadColor);
            return t;
        }

        // ----------------------------------------------------------------------
        //  HANDLE  (semicircular tube arch across the top, spanning X)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildHandle()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            const float spanX = 18f;   // half-span along X
            const float baseZ = 16f;   // roots sit on the lid
            const float archH = 14f;   // rise above the roots
            const float tubeR = 2.6f;
            const int arcSegs = 12;
            const int ringSegs = 8;

            var rings = new List<List<Vector3>>();
            var axis = new List<Vector3>();
            for (int a = 0; a <= arcSegs; a++)
            {
                double th = Math.PI * a / arcSegs;          // 0 -> PI
                float ax = spanX * (float)Math.Cos(th);      // +spanX -> -spanX
                float az = baseZ + archH * (float)Math.Sin(th);
                axis.Add(V(ax, 0, az));

                // Tangent in the XZ plane; ring lives in (Nperp, Y).
                float tx = -spanX * (float)Math.Sin(th);
                float tz = archH * (float)Math.Cos(th);
                float tl = (float)Math.Sqrt(tx * tx + tz * tz);
                if (tl < 1e-4f) tl = 1e-4f;
                float nx = -tz / tl, nz = tx / tl;           // normalize(cross(T, Y))
                var ring = new List<Vector3>(ringSegs);
                for (int r = 0; r < ringSegs; r++)
                {
                    double ph = 2 * Math.PI * r / ringSegs;
                    float cos = (float)Math.Cos(ph), sin = (float)Math.Sin(ph);
                    ring.Add(V(ax + tubeR * cos * nx,
                               0 + tubeR * sin,
                               az + tubeR * cos * nz));
                }
                rings.Add(ring);
            }

            for (int a = 0; a < rings.Count - 1; a++)
                AddLoopBand(t, rings[a], rings[a + 1], axis[a], MetalDark, MetalMid);

            AddCapFan(t, rings[0], axis[0], axis[0], MetalDark);
            AddCapFan(t, rings[^1], axis[^1], axis[^1], MetalDark);

            // Roots anchoring the handle to the lid.
            AddBox(t, V(spanX, 0, baseZ - 1f), V(7f, 8f, 4f), MetalDark);
            AddBox(t, V(-spanX, 0, baseZ - 1f), V(7f, 8f, 4f), MetalDark);
            return t;
        }

        // ----------------------------------------------------------------------
        //  HARDWARE  (front latches, back hinge, corner rivets)
        // ----------------------------------------------------------------------

        private static List<ITriangleMeshWithColorAndTexture> BuildHardware()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();

            // Two latches on the +X front face, straddling the seam.
            AddBox(t, V(38f, 12f, 0f), V(6f, 7f, 9f), MetalMid);
            AddBox(t, V(38f, -12f, 0f), V(6f, 7f, 9f), MetalMid);

            // Hinge barrel along Y on the -X back face.
            AddCylinderY(t, -38.5f, 0f, -13f, 13f, 2.6f, 8, MetalDark);

            // Corner rivets (four top, four bottom).
            foreach (float sx in new[] { 32f, -32f })
                foreach (float sy in new[] { 20f, -20f })
                    foreach (float sz in new[] { 14f, -14f })
                        AddBox(t, V(sx, sy, sz), V(3.4f, 3.4f, 3.4f), MetalMid);

            return t;
        }

        // ----------------------------------------------------------------------
        //  CRASH BOX  (single AABB around the body, padded for easy pickup)
        // ----------------------------------------------------------------------

        private const float PickupPadding = 14f;

        private static List<List<IVector3>> BuildCrashBoxes()
        {
            var min = V(-CaseHalfX - PickupPadding, -CaseHalfY - PickupPadding, -CaseHalfZ - PickupPadding);
            var max = V(CaseHalfX + PickupPadding, CaseHalfY + PickupPadding, 32f + PickupPadding);
            return new List<List<IVector3>>
            {
                OmegaObject3DHelpers.GenerateCrashBoxCorners(min, max)
            };
        }

        // ----------------------------------------------------------------------
        //  GEOMETRY HELPERS
        // ----------------------------------------------------------------------

        // A closed loop of points tracing a rounded rectangle in the plane z, centred
        // on (cx, cy) and optionally rotated by angleRad about that centre.
        // Vertex count is constant for a given cornerSegs: 4 * (cornerSegs + 1).
        private static List<Vector3> RoundedRectLoop(
            float cx, float cy, float z,
            float halfX, float halfY, float r, int cornerSegs, float angleRad = 0f)
        {
            r = Math.Min(r, Math.Min(halfX, halfY));
            float ix = halfX - r, iy = halfY - r;
            var corners = new (float x, float y, double a0)[]
            {
                (ix, iy, 0.0),
                (-ix, iy, Math.PI / 2),
                (-ix, -iy, Math.PI),
                (ix, -iy, 3 * Math.PI / 2)
            };
            float ca = (float)Math.Cos(angleRad), sa = (float)Math.Sin(angleRad);
            var pts = new List<Vector3>(4 * (cornerSegs + 1));
            foreach (var c in corners)
            {
                for (int s = 0; s <= cornerSegs; s++)
                {
                    double a = c.a0 + (Math.PI / 2) * s / cornerSegs;
                    float lx = c.x + r * (float)Math.Cos(a);
                    float ly = c.y + r * (float)Math.Sin(a);
                    pts.Add(V(cx + (lx * ca - ly * sa), cy + (lx * sa + ly * ca), z));
                }
            }
            return pts;
        }

        // Connects two equal-length loops with a quad band. Winding is enforced
        // outward from the supplied interior centre.
        private static void AddLoopBand(
            List<ITriangleMeshWithColorAndTexture> t,
            List<Vector3> lower, List<Vector3> upper, Vector3 centre,
            string cA, string cB)
        {
            int n = lower.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                AddQuadOutward(t, lower[i], lower[j], upper[j], upper[i], centre,
                    i % 2 == 0 ? cA : cB);
            }
        }

        // Triangulates a loop as a fan to an apex; winding enforced from centre.
        private static void AddCapFan(
            List<ITriangleMeshWithColorAndTexture> t,
            List<Vector3> loop, Vector3 apex, Vector3 centre, string color)
        {
            int n = loop.Count;
            for (int i = 0; i < n; i++)
                t.Add(CreateTriangleOutward(loop[i], loop[(i + 1) % n], apex, centre, color));
        }

        // A rounded-rectangle slab: top + bottom caps and a side band. Convex, so a
        // single centre gives correct outward winding on every face.
        private static void AddSlab(
            List<ITriangleMeshWithColorAndTexture> t,
            float cx, float cy, float zBot, float zTop,
            float halfX, float halfY, float r, int cornerSegs, float angleRad,
            string topColor, string sideColor)
        {
            var top = RoundedRectLoop(cx, cy, zTop, halfX, halfY, r, cornerSegs, angleRad);
            var bot = RoundedRectLoop(cx, cy, zBot, halfX, halfY, r, cornerSegs, angleRad);
            var centre = V(cx, cy, (zBot + zTop) / 2f);
            AddLoopBand(t, bot, top, centre, sideColor, sideColor);
            AddCapFan(t, top, V(cx, cy, zTop), centre, topColor);
            AddCapFan(t, bot, V(cx, cy, zBot), centre, sideColor);
        }

        // 12-triangle axis-aligned box centred on c with full sizes s.
        private static void AddBox(
            List<ITriangleMeshWithColorAndTexture> t, Vector3 c, Vector3 s, string color)
        {
            float x = s.x / 2f, y = s.y / 2f, z = s.z / 2f;
            var a = V(c.x - x, c.y - y, c.z - z);
            var b = V(c.x + x, c.y - y, c.z - z);
            var d = V(c.x + x, c.y + y, c.z - z);
            var e = V(c.x - x, c.y + y, c.z - z);
            var f = V(c.x - x, c.y - y, c.z + z);
            var g = V(c.x + x, c.y - y, c.z + z);
            var h = V(c.x + x, c.y + y, c.z + z);
            var i = V(c.x - x, c.y + y, c.z + z);
            AddQuadOutward(t, a, b, d, e, c, color);
            AddQuadOutward(t, f, i, h, g, c, color);
            AddQuadOutward(t, a, f, g, b, c, color);
            AddQuadOutward(t, b, g, h, d, c, color);
            AddQuadOutward(t, d, h, i, e, c, color);
            AddQuadOutward(t, f, a, e, i, c, color);
        }

        // A short cylinder whose axis runs along Y, centred in X/Z on (cx, cz).
        private static void AddCylinderY(
            List<ITriangleMeshWithColorAndTexture> t,
            float cx, float cz, float y0, float y1, float radius, int seg, string color)
        {
            var centre = V(cx, (y0 + y1) / 2f, cz);
            var lo = new List<Vector3>(seg);
            var hi = new List<Vector3>(seg);
            for (int i = 0; i < seg; i++)
            {
                double a = 2 * Math.PI * i / seg;
                float ox = radius * (float)Math.Cos(a);
                float oz = radius * (float)Math.Sin(a);
                lo.Add(V(cx + ox, y0, cz + oz));
                hi.Add(V(cx + ox, y1, cz + oz));
            }
            AddLoopBand(t, lo, hi, centre, color, color);
            AddCapFan(t, lo, V(cx, y0, cz), centre, color);
            AddCapFan(t, hi, V(cx, y1, cz), centre, color);
        }

        private static void AddPart(OmegaObject3D o, string name, List<ITriangleMeshWithColorAndTexture> tris)
        {
            o.ObjectParts.Add(new OmegaObjectPart3D { PartName = name, Triangles = tris, IsVisible = true });
        }

        private static Vector3 V(float x, float y, float z) => new Vector3 { x = x, y = y, z = z };
    }
}
