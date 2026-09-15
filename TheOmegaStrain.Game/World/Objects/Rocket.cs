using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using static TheOmegaStrain.Game.Helpers.OmegaObject3DHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    /// <summary>
    /// Air-to-air / ship-to-ship rocket.
    ///
    /// Coordinate convention:
    /// +X = nose / forward
    /// -X = engine / exhaust
    ///  Y = lateral
    /// +Z = up
    ///
    /// Authored length is ~21 units, before the factory's uniform scale.
    /// </summary>
    public static class Rocket
    {
        private const float ZoomRatio = 3.5f;

        private const string BodyColor = "B8BDC6";
        private const string BodyMid = "8B919B";
        private const string BodyDark = "4A5059";
        private const string NoseColor = "D7DADF";
        private const string SeekerColor = "171C22";
        private const string FinColor = "666E78";
        private const string EngineColor = "FF9D2E";
        private const string EngineDark = "8A4312";

        private static readonly Vector3 BodyCenter =
            new Vector3 { x = 0f, y = 0f, z = 0f };

        public static OmegaObject3D CreateRocket(ISurface parentSurface)
            => CreateInternal(parentSurface, false);

        public static OmegaObject3D CreateHeatSeekingRocket(ISurface parentSurface)
            => CreateInternal(parentSurface, true);

        private static OmegaObject3D CreateInternal(ISurface parentSurface, bool heatSeeking)
        {
            var rocket = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = heatSeeking ? "HeatSeekingRocket" : "Rocket",
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                HasShadow = false
            };

            AddPart(rocket, "RocketNose", BuildNose(), true);
            AddPart(rocket, "RocketBody", BuildBody(), true);
            AddPart(rocket, "RocketSeeker", BuildSeekerCollar(), true);
            AddPart(rocket, "RocketFins", BuildFins(), true);
            AddPart(rocket, "RocketEngine", BuildEngine(), true);

            AddPart(rocket, "RocketParticlesDirectionGuide", BuildParticlesDirectionGuide(), false);
            AddPart(rocket, "RocketParticlesStartGuide", BuildParticlesStartGuide(), false);

            rocket.CrashBoxes = BuildCrashBoxes();

            // Geometry template only. Weapons creates independent movement, physics
            // and particles per launch; the heatSeeking variant is only a model name.

            ApplyScaleToObject(rocket, ZoomRatio);
            return rocket;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildNose()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();
            const int segments = 8;

            var tip = V(11f, 0f, 0f);
            var r0 = Ring(segments, 8.8f, 0.75f, 0.75f);
            var r1 = Ring(segments, 6.5f, 1.05f, 1.05f);

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris.Add(CreateTriangleOutward(
                    tip, r0[next], r0[i],
                    BodyCenter, NoseColor));

                AddQuadOutward(
                    tris,
                    r0[i], r0[next], r1[next], r1[i],
                    BodyCenter,
                    i % 2 == 0 ? BodyColor : BodyMid);
            }

            return tris;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildBody()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

            var r0 = Ring(8, 6.5f, 1.05f, 1.05f);
            var r1 = Ring(8, -4.5f, 1.15f, 1.15f);
            var r2 = Ring(8, -7.5f, 1.25f, 1.25f);

            ConnectRings(tris, r0, r1, BodyColor, BodyMid, BodyCenter);
            ConnectRings(tris, r1, r2, BodyDark, BodyDark, BodyCenter);

            return tris;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildSeekerCollar()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

            // Dark IR seeker / guidance collar directly behind the nose.
            var r0 = Ring(8, 7.1f, 1.07f, 1.07f);
            var r1 = Ring(8, 6.4f, 1.08f, 1.08f);

            ConnectRings(tris, r0, r1, SeekerColor, SeekerColor, V(6.75f, 0, 0));
            return tris;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildEngine()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

            var r0 = Ring(8, -7.5f, 1.25f, 1.25f);
            var r1 = Ring(8, -9.0f, 0.85f, 0.85f);
            var r2 = Ring(8, -10.0f, 0.55f, 0.55f);

            var center = V(-8.8f, 0, 0);

            ConnectRings(tris, r0, r1, EngineDark, EngineDark, center);
            ConnectRings(tris, r1, r2, EngineDark, EngineDark, center);

            var nozzle = V(-10f, 0, 0);
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                tris.Add(CreateTriangleOutward(
                    r2[i], r2[next], nozzle,
                    center,
                    EngineColor));
            }

            return tris;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildFins()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

            AddHorizontalFin(tris, 1f);
            AddHorizontalFin(tris, -1f);
            AddVerticalFin(tris, 1f);
            AddVerticalFin(tris, -1f);

            return tris;
        }

        private static void AddHorizontalFin(
            List<ITriangleMeshWithColorAndTexture> tris,
            float sign)
        {
            float y0 = sign * 1.0f;
            float y1 = sign * 3.0f;

            var center = V(-5.5f, sign * 2f, 0);

            var a = V(-3.0f, y0, 0.18f);
            var b = V(-7.0f, y0, 0.18f);
            var c = V(-8.3f, y1, 0.18f);
            var d = V(-4.2f, y1, 0.18f);

            var a2 = V(-3.0f, y0, -0.18f);
            var b2 = V(-7.0f, y0, -0.18f);
            var c2 = V(-8.3f, y1, -0.18f);
            var d2 = V(-4.2f, y1, -0.18f);

            AddFinPrism(tris, a, b, c, d, a2, b2, c2, d2, center);
        }

        private static void AddVerticalFin(
            List<ITriangleMeshWithColorAndTexture> tris,
            float sign)
        {
            float z0 = sign * 1.0f;
            float z1 = sign * 3.0f;

            var center = V(-5.5f, 0, sign * 2f);

            var a = V(-3.0f, 0.18f, z0);
            var b = V(-7.0f, 0.18f, z0);
            var c = V(-8.3f, 0.18f, z1);
            var d = V(-4.2f, 0.18f, z1);

            var a2 = V(-3.0f, -0.18f, z0);
            var b2 = V(-7.0f, -0.18f, z0);
            var c2 = V(-8.3f, -0.18f, z1);
            var d2 = V(-4.2f, -0.18f, z1);

            AddFinPrism(tris, a, b, c, d, a2, b2, c2, d2, center);
        }

        private static void AddFinPrism(
            List<ITriangleMeshWithColorAndTexture> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 a2, Vector3 b2, Vector3 c2, Vector3 d2,
            Vector3 center)
        {
            AddQuadOutward(tris, a, b, c, d, center, FinColor);
            AddQuadOutward(tris, a2, d2, c2, b2, center, BodyDark);
            AddQuadOutward(tris, a, a2, b2, b, center, BodyDark);
            AddQuadOutward(tris, b, b2, c2, c, center, BodyDark);
            AddQuadOutward(tris, c, c2, d2, d, center, BodyDark);
            AddQuadOutward(tris, d, d2, a2, a, center, BodyMid);
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildParticlesStartGuide()
        {
            // Clear of the nozzle (nozzle ends at x = -10) so exhaust is not born in the body.
            return new List<ITriangleMeshWithColorAndTexture>
            {
                CreateTriangleOutward(
                    V(-13.0f,  0.55f,  0.30f),
                    V(-13.0f, -0.55f,  0.30f),
                    V(-13.0f,  0.00f, -0.30f),
                    V(-13.0f,  0.00f,  0.00f),
                    "FFFFFF",
                    true)
            };
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildParticlesDirectionGuide()
        {
            // Continues directly behind StartGuide and points along local -X.
            return new List<ITriangleMeshWithColorAndTexture>
            {
                CreateTriangleOutward(
                    V(-15.5f,  0.55f,  0.30f),
                    V(-15.5f, -0.55f,  0.30f),
                    V(-15.5f,  0.00f, -0.30f),
                    V(-15.5f,  0.00f,  0.00f),
                    "FFFFFF",
                    true)
            };
        }

        private static List<List<IVector3>> BuildCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    V(-9.5f,-1.4f,-1.4f),
                    V(11f,1.4f,1.4f))
            };
        }

        private static List<Vector3> Ring(
            int segments,
            float x,
            float radiusY,
            float radiusZ)
        {
            var points = new List<Vector3>(segments);
            float step = (float)(2 * Math.PI / segments);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * step;
                points.Add(V(
                    x,
                    radiusY * (float)Math.Cos(angle),
                    radiusZ * (float)Math.Sin(angle)));
            }

            return points;
        }

        private static void ConnectRings(
            List<ITriangleMeshWithColorAndTexture> tris,
            List<Vector3> a,
            List<Vector3> b,
            string colorA,
            string colorB,
            Vector3 center)
        {
            for (int i = 0; i < a.Count; i++)
            {
                int next = (i + 1) % a.Count;
                AddQuadOutward(
                    tris,
                    a[i], a[next], b[next], b[i],
                    center,
                    i % 2 == 0 ? colorA : colorB);
            }
        }

        private static void AddPart(
            OmegaObject3D obj,
            string name,
            List<ITriangleMeshWithColorAndTexture>? tris,
            bool visible)
        {
            if (tris == null) return;

            obj.ObjectParts.Add(new OmegaObjectPart3D
            {
                PartName = name,
                Triangles = tris,
                IsVisible = visible
            });
        }

        private static Vector3 V(float x, float y, float z) =>
            new Vector3 { x = x, y = y, z = z };
    }
}
