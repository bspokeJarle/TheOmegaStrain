using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using static TheOmegaStrain.Game.Helpers.OmegaObject3DHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class AttackShip
    {
        private const float ZoomRatio = 1f;
        // Expand each collision box equally without shifting the navigation/map centre.
        private const float CrashBoxPadding = 12f;
        private const float EngineNozzleCapX = -54.2f;
        private const float EngineParticleStartX = -100f;
        private const float EngineParticleGuideX = -114f;
        private const float EngineParticleLateralOffsetY = 28.5f;
        private const string BodyLight = "B8BDC6", BodyMid = "8B919B", BodyDark = "565C66", Underside = "343941";
        private const string Cockpit = "0B1118", CockpitSoft = "16212B", Engine = "FF9D2E", EngineDark = "8A4312", Wing = "6E7682", Accent = "C33A2C";
        private static readonly Vector3 BodyCenter = V(-2, 0, 0);

        public static OmegaObject3D CreateAttackShip(ISurface parentSurface)
        {
            var ship = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "AttackShip",
                Rotation = V(0, 0, 0),
                ParentSurface = parentSurface,
                HasShadow = true
            };
            AddPart(ship, "AttackShipNose", BuildNose(), true);
            AddPart(ship, "AttackShipFuselage", BuildFuselage(), true);
            AddPart(ship, "AttackShipCockpit", BuildCockpit(), true);
            AddPart(ship, "AttackShipWings", BuildWings(), true);
            AddPart(ship, "AttackShipEngines", BuildEngines(), true);
            AddPart(ship, "AttackShipRearFins", BuildRearFins(), true);
            AddPart(ship, "AttackShipDetails", BuildDetails(), true);
            AddPart(ship, "AttackShipLeftEngineStartGuide", BuildEngineStartGuide(EngineParticleLateralOffsetY), false);
            AddPart(ship, "AttackShipLeftEngineDirectionGuide", BuildEngineDirectionGuide(EngineParticleLateralOffsetY), false);
            AddPart(ship, "AttackShipRightEngineStartGuide", BuildEngineStartGuide(-EngineParticleLateralOffsetY), false);
            AddPart(ship, "AttackShipRightEngineDirectionGuide", BuildEngineDirectionGuide(-EngineParticleLateralOffsetY), false);
            // Three crash boxes approximate the fuselage and both wing/engine sections.
            ship.CrashBoxes = BuildCrashBoxes();
            // Exhaust styling, mirroring ShipControls.ApplyThrustParticleStyle: the default
            // gravity would overpower the plume and drag it under the hull.
            ship.Particles = new ParticlesAI
            {
                GravityStrength = 34f,
                LifeMultiplier = 0.72f,
                SizeMultiplier = 1.55f,
                ThrottleDurationFactor = 0.2f,
                ColorStartOverride = "fff8c8",
                ColorMidOverride = "ff8a20",
                ColorEndOverride = "5a1800"
            };

            OmegaObject3DHelpers.ApplyScaleToObject(ship, ZoomRatio);
            OmegaObject3DHelpers.AddSimplifiedShadowPart(ship, useFlatQuad: true);
            return ship;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildNose()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>(); const int n = 12;
            var tip = V(48, 0, 0); var r1 = Ring(n, 40, 4, 3.4f); var r2 = Ring(n, 30, 7, 5.2f); var r3 = Ring(n, 18, 10.5f, 6.4f);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; var c1 = i % 2 == 0 ? BodyLight : BodyMid; var c2 = i % 2 == 0 ? BodyMid : BodyDark;
                t.Add(CreateTriangleOutward(tip, r1[j], r1[i], BodyCenter, c1));
                AddQuadOutward(t, r1[i], r1[j], r2[j], r2[i], BodyCenter, c1);
                AddQuadOutward(t, r2[i], r2[j], r3[j], r3[i], BodyCenter, c2);
            }
            return t;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildFuselage()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            var r0 = Ring(12, 18, 10.5f, 6.4f); var r1 = Ring(12, 2, 14, 7); var r2 = Ring(12, -20, 17, 6.2f); var r3 = Ring(12, -38, 15, 5.5f);
            Connect(t, r0, r1, BodyMid, BodyLight, BodyCenter); Connect(t, r1, r2, BodyMid, BodyDark, BodyCenter); Connect(t, r2, r3, BodyDark, BodyDark, BodyCenter);
            var rear = V(-38, 0, 0); for (int i = 0; i < 12; i++) t.Add(CreateTriangleOutward(r3[i], r3[(i + 1) % 12], rear, BodyCenter, BodyDark));
            return t;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildCockpit()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            float[] x = { 19, 14, 7, 0, -8 }, hw = { 3, 5, 6.2f, 5.7f, 4.4f }, bz = { 5.8f, 6.5f, 7.5f, 7.7f, 7 }, tz = { 8.2f, 10.2f, 12.4f, 11.4f, 9.3f };
            var s = new List<List<Vector3>>();
            for (int k = 0; k < x.Length; k++) s.Add(new List<Vector3>{
                V(x[k],-hw[k],bz[k]),V(x[k],-hw[k]*.55f,(bz[k]+tz[k])*.52f),V(x[k],0,tz[k]),
                V(x[k],hw[k]*.55f,(bz[k]+tz[k])*.52f),V(x[k],hw[k],bz[k]),V(x[k],0,bz[k]+.15f)});
            var c = V(5, 0, 9);
            for (int k = 0; k < s.Count - 1; k++) for (int i = 0; i < 4; i++)
                AddQuadOutward(t, s[k][i], s[k][i + 1], s[k + 1][i + 1], s[k + 1][i], c, k < 2 ? Cockpit : CockpitSoft);
            for (int i = 0; i < 4; i++)
            {
                t.Add(CreateTriangleOutward(s[0][5], s[0][i + 1], s[0][i], c, Cockpit));
                t.Add(CreateTriangleOutward(s[^1][5], s[^1][i], s[^1][i + 1], c, CockpitSoft));
            }
            return t;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildWings() { var t = new List<ITriangleMeshWithColorAndTexture>(); AddWing(t, 1); AddWing(t, -1); return t; }
        private static void AddWing(List<ITriangleMeshWithColorAndTexture> t, float s)
        {
            var c = V(-11, s * 23, .5f); float y1 = s * 10, y2 = s * 14.5f, yt = s * 36;
            var a = V(8, y1, 2.8f); var b = V(-10, y2, 2.8f); var d = V(-8, yt, .8f); var e = V(-30, yt, 1.2f);
            var a2 = V(8, y1, -1.5f); var b2 = V(-10, y2, -1.5f); var d2 = V(-8, yt, -.8f); var e2 = V(-30, yt, -1);
            AddQuadOutward(t, a, b, e, d, c, Wing); AddQuadOutward(t, a2, d2, e2, b2, c, Underside); AddQuadOutward(t, a, a2, b2, b, c, BodyDark);
            AddQuadOutward(t, b, b2, e2, e, c, BodyDark); AddQuadOutward(t, e, e2, d2, d, c, BodyDark); AddQuadOutward(t, d, d2, a2, a, c, BodyMid);
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildEngines() { var t = new List<ITriangleMeshWithColorAndTexture>(); AddEngine(t, 18.5f); AddEngine(t, -18.5f); return t; }
        private static void AddEngine(List<ITriangleMeshWithColorAndTexture> t, float y)
        {
            var c = V(-37, y, 0); var r0 = Ring(10, -18, 5.3f, 5.3f, y); var r1 = Ring(10, -40, 6, 6, y); var r2 = Ring(10, -49, 5.8f, 5.8f, y);
            var r3 = Ring(10, -53, 4.3f, 4.3f, y); var r4 = Ring(10, EngineNozzleCapX, 3.1f, 3.1f, y);
            Connect(t, r0, r1, BodyDark, BodyMid, c); Connect(t, r1, r2, EngineDark, BodyDark, c); Connect(t, r2, r3, BodyDark, EngineDark, c); Connect(t, r3, r4, EngineDark, EngineDark, c);
            var nc = V(EngineNozzleCapX, y, 0); for (int i = 0; i < 10; i++) t.Add(CreateTriangleOutward(r4[i], r4[(i + 1) % 10], nc, c, Engine));
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildRearFins() { var t = new List<ITriangleMeshWithColorAndTexture>(); AddFin(t, 18.5f); AddFin(t, -18.5f); return t; }
        private static void AddFin(List<ITriangleMeshWithColorAndTexture> t, float y)
        {
            float h = 1.3f; var c = V(-39, y, 9); var a = V(-30, y - h, 5); var b = V(-44, y - h, 5.2f); var e = V(-46, y - h, 15); var d = V(-34, y - h, 11.5f);
            var a2 = V(-30, y + h, 5); var b2 = V(-44, y + h, 5.2f); var e2 = V(-46, y + h, 15); var d2 = V(-34, y + h, 11.5f);
            AddQuadOutward(t, a, b, e, d, c, Wing); AddQuadOutward(t, a2, d2, e2, b2, c, BodyDark); AddQuadOutward(t, a, a2, b2, b, c, BodyDark);
            AddQuadOutward(t, b, b2, e2, e, c, BodyDark); AddQuadOutward(t, e, e2, d2, d, c, BodyDark); AddQuadOutward(t, d, d2, a2, a, c, BodyMid);
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildDetails()
        {
            var t = new List<ITriangleMeshWithColorAndTexture>();
            AddBox(t, V(-4, 26, -2.5f), V(15, 2.3f, 2.2f), BodyDark); AddBox(t, V(-4, -26, -2.5f), V(15, 2.3f, 2.2f), BodyDark);
            AddBox(t, V(12, 12.8f, 0), V(7, 1.4f, 1.4f), Accent); AddBox(t, V(12, -12.8f, 0), V(7, 1.4f, 1.4f), Accent);
            AddBox(t, V(2, 13.6f, .5f), V(8, 1, 2.2f), BodyDark); AddBox(t, V(2, -13.6f, .5f), V(8, 1, 2.2f), BodyDark);
            return t;
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildEngineStartGuide(float y)
        {
            // Clear of the nozzle cap so large exhaust particles are born behind the hull.
            return new List<ITriangleMeshWithColorAndTexture>
            {
                new TriangleMeshWithColor
                {
                    Color = "00ff00",
                    vert1 = new Vector3 { x = EngineParticleStartX, y = y + 2.5f, z = -2.5f },
                    vert2 = new Vector3 { x = EngineParticleStartX, y = y - 2.5f, z = -2.5f },
                    vert3 = new Vector3 { x = EngineParticleStartX, y = y, z = 2.5f },
                    noHidden = true
                }
            };
        }

        private static List<ITriangleMeshWithColorAndTexture> BuildEngineDirectionGuide(float y)
        {
            // Behind the nozzle, along the exhaust axis (-X), away from the nose.
            return new List<ITriangleMeshWithColorAndTexture>
            {
                new TriangleMeshWithColor
                {
                    Color = "ff0000",
                    vert1 = new Vector3 { x = EngineParticleGuideX, y = y + 3f, z = 0f },
                    vert2 = new Vector3 { x = EngineParticleGuideX, y = y - 3f, z = 0f },
                    vert3 = new Vector3 { x = EngineParticleGuideX, y = y, z = 6f },
                    noHidden = true
                }
            };
        }

        private static List<List<IVector3>> BuildCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                // Main fuselage, nose and cockpit.
                CreatePaddedCrashBox(
                    V(-38f, -15f, -7f),
                    V(48f, 15f, 12.5f)),

                // +Y wing and engine.
                CreatePaddedCrashBox(
                    V(-54.5f, 10f, -6.5f),
                    V(8f, 36f, 15f)),

                // -Y wing and engine.
                CreatePaddedCrashBox(
                    V(-54.5f, -36f, -6.5f),
                    V(8f, -10f, 15f))
            };
        }

        private static List<IVector3> CreatePaddedCrashBox(Vector3 min, Vector3 max)
        {
            return OmegaObject3DHelpers.GenerateCrashBoxCorners(
                V(min.x - CrashBoxPadding, min.y - CrashBoxPadding, min.z - CrashBoxPadding),
                V(max.x + CrashBoxPadding, max.y + CrashBoxPadding, max.z + CrashBoxPadding));
        }

        private static List<Vector3> Ring(int n, float x, float ry, float rz, float yo = 0, float zo = 0) { var p = new List<Vector3>(n); for (int i = 0; i < n; i++) { float a = (float)(2 * Math.PI * i / n); p.Add(V(x, yo + ry * (float)Math.Cos(a), zo + rz * (float)Math.Sin(a))); } return p; }
        private static void Connect(List<ITriangleMeshWithColorAndTexture> t, List<Vector3> a, List<Vector3> b, string c1, string c2, Vector3 c) { for (int i = 0; i < a.Count; i++) { int n = (i + 1) % a.Count; AddQuadOutward(t, a[i], a[n], b[n], b[i], c, i % 2 == 0 ? c1 : c2); } }
        private static void AddBox(List<ITriangleMeshWithColorAndTexture> t, Vector3 c, Vector3 s, string color) { float x = s.x / 2, y = s.y / 2, z = s.z / 2; var a = V(c.x - x, c.y - y, c.z - z); var b = V(c.x + x, c.y - y, c.z - z); var d = V(c.x + x, c.y + y, c.z - z); var e = V(c.x - x, c.y + y, c.z - z); var f = V(c.x - x, c.y - y, c.z + z); var g = V(c.x + x, c.y - y, c.z + z); var h = V(c.x + x, c.y + y, c.z + z); var i = V(c.x - x, c.y + y, c.z + z); AddQuadOutward(t, a, b, d, e, c, color); AddQuadOutward(t, f, i, h, g, c, color); AddQuadOutward(t, a, f, g, b, c, color); AddQuadOutward(t, b, g, h, d, c, color); AddQuadOutward(t, d, h, i, e, c, color); AddQuadOutward(t, f, a, e, i, c, color); }
        private static void AddPart(OmegaObject3D o, string n, List<ITriangleMeshWithColorAndTexture>? t, bool v) { if (t == null) return; o.ObjectParts.Add(new OmegaObjectPart3D { PartName = n, Triangles = t, IsVisible = v }); }
        private static Vector3 V(float x, float y, float z) => new Vector3 { x = x, y = y, z = z };
    }
}
