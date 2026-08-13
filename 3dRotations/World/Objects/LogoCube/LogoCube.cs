using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects.LogoCube
{
    public class LogoCube
    {
        // ----------------------------------------------------
        //  SIZE / PLACEMENT
        // ----------------------------------------------------
        private static float CubeHalf = 70f;

        // Push decals outward from cube to avoid z-fighting
        private static float LogoPushOut = 1.5f;

        // Scale for all decal coordinate data (adjust if needed)
        private static float LogoScale = 1.00f;

        // 4x4 squares per face (only for +Z/-Z faces)
        private static int SideGrid = 4;

        // ----------------------------------------------------
        //  PALETTE (colors from logo universe)
        // ----------------------------------------------------
        private static readonly string[] CubePalette =
        {
            "0D1020", "0F1C2E", "1A2F45",
            "1D5E20", "2E8B2E", "55C455",
            "0E5F7C", "1FA2C9", "4DD0FF",
            "8C3F12", "C4571C"
        };

        // ----------------------------------------------------
        //  PUBLIC FACTORY
        // ----------------------------------------------------
        public static _3dObject CreateLogoCube()
        {
            // Cube shell: ONLY +Z and -Z faces as 4x4 squares
            // - +Y/-Y omitted entirely (nothing behind Omega/Retro)
            // - +X/-X omitted (nothing behind MusicBy/047)
            var cubeShell = CreateCubeShell_PartialFaces(CubeHalf, SideGrid);

            // -------------------------
            // +Y face: Omega (front)
            // -------------------------
            var omega = BuildLogoFaceDecal_YFace(
                LogoCubeVectorData.OmegaStrainTrianglesData,
                faceY: +CubeHalf + LogoPushOut,
                outwardCenter: new Vector3 { x = 0, y = +CubeHalf, z = 0 },
                scale: LogoScale,
                flipX: false,
                flipZ: false);

            // -------------------------
            // -Y face: Retro (back)
            // IMPORTANT: your world needs Z flip here (not X)
            // -------------------------
            var retro = BuildLogoFaceDecal_YFace(
                LogoCubeVectorData.RetroMeshTrianglesData,
                faceY: -CubeHalf - LogoPushOut,
                outwardCenter: new Vector3 { x = 0, y = -CubeHalf, z = 0 },
                scale: LogoScale,
                flipX: false,
                flipZ: true);

            // -------------------------
            // +X face: MUSIC BY
            // place decals on X face (x constant)
            // -------------------------
            var musicBy = BuildLogoFaceDecal_XFace(
                LogoCubeVectorData.MusicByTrianglesData,
                faceX: +CubeHalf + LogoPushOut,
                outwardCenter: new Vector3 { x = +CubeHalf, y = 0, z = 0 },
                scale: LogoScale,
                flipX: false,
                flipZ: true);

            // -------------------------
            // -X face: -047-
            // FIX: "X flip" on an X-face means flipping the FILE X component,
            // which maps to WORLD Y (because file X -> world Y on X-face).
            // -------------------------
            var zero47 = BuildLogoFaceDecal_XFace(
                LogoCubeVectorData._047TrianglesData,
                faceX: -CubeHalf - LogoPushOut,
                outwardCenter: new Vector3 { x = -CubeHalf, y = 0, z = 0 },
                scale: LogoScale,
                flipX: true,     // <-- this is the missing fix
                flipZ: true);

            var obj = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };

            AddPart(obj, "CubeShell", cubeShell, true);
            AddPart(obj, "OmegaStrainLogo", omega, true);
            AddPart(obj, "RetroMeshLogo", retro, true);
            AddPart(obj, "MusicBy", musicBy, true);
            AddPart(obj, "047", zero47, true);

            obj.ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 };
            obj.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            obj.ObjectName = "LogoCube";
            obj.ImpactStatus = new ImpactStatus();

            obj.CrashBoxes = new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -CubeHalf, y = -CubeHalf, z = -CubeHalf },
                    new Vector3 { x = +CubeHalf, y = +CubeHalf, z = +CubeHalf })
            };

            return obj;
        }

        private static void AddPart(_3dObject obj, string name, List<ITriangleMeshWithColor>? tris, bool visible)
        {
            if (tris == null) return;

            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = name,
                Triangles = tris,
                IsVisible = visible
            });
        }

        // ----------------------------------------------------
        //  CUBE SHELL: only +Z and -Z faces as 4x4 squares
        //  (no +Y/-Y faces, and no solid +X/-X faces to avoid "stuff behind decals")
        // ----------------------------------------------------
        private static List<ITriangleMeshWithColor> CreateCubeShell_PartialFaces(float half, int grid)
        {
            var tris = new List<ITriangleMeshWithColor>();
            var rand = new Random(1337); // stable colors per run

            float x0 = -half, x1 = +half;
            float y0 = -half, y1 = +half;
            float z0 = -half, z1 = +half;

            var center = new Vector3 { x = 0, y = 0, z = 0 };

            void AddGridFace(Vector3 origin, Vector3 axisU, Vector3 axisV)
            {
                for (int u = 0; u < grid; u++)
                {
                    float u0 = (float)u / grid;
                    float u1 = (float)(u + 1) / grid;

                    for (int v = 0; v < grid; v++)
                    {
                        float v0 = (float)v / grid;
                        float v1 = (float)(v + 1) / grid;

                        var p00 = Add(origin, Add(Mul(axisU, u0), Mul(axisV, v0)));
                        var p10 = Add(origin, Add(Mul(axisU, u1), Mul(axisV, v0)));
                        var p11 = Add(origin, Add(Mul(axisU, u1), Mul(axisV, v1)));
                        var p01 = Add(origin, Add(Mul(axisU, u0), Mul(axisV, v1)));

                        string col = CubePalette[rand.Next(CubePalette.Length)];

                        tris.Add(CreateTriangleOutward(p00, p10, p11, center, col));
                        tris.Add(CreateTriangleOutward(p00, p11, p01, center, col));
                    }
                }
            }

            // +Z face only
            AddGridFace(
                origin: new Vector3 { x = x0, y = y0, z = z1 },
                axisU: new Vector3 { x = x1 - x0, y = 0, z = 0 },
                axisV: new Vector3 { x = 0, y = y1 - y0, z = 0 });

            // -Z face only
            AddGridFace(
                origin: new Vector3 { x = x1, y = y0, z = z0 },
                axisU: new Vector3 { x = x0 - x1, y = 0, z = 0 },
                axisV: new Vector3 { x = 0, y = y1 - y0, z = 0 });

            return tris;
        }

        // ----------------------------------------------------
        //  DECAL BUILDERS
        //  Data format per line: x1,z1;x2,z2;x3,z3|RRGGBB
        //  ParseXZ returns (x,z) in Vector3.x and Vector3.z
        // ----------------------------------------------------

        // Place decal on a Y face (y constant)
        private static List<ITriangleMeshWithColor> BuildLogoFaceDecal_YFace(
            string data,
            float faceY,
            Vector3 outwardCenter,
            float scale,
            bool flipX,
            bool flipZ)
        {
            var tris = new List<ITriangleMeshWithColor>();
            if (string.IsNullOrWhiteSpace(data)) return tris;

            var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length < 10) continue;

                string geom = line;
                string hex = "FFFFFF";

                int pipe = line.LastIndexOf('|');
                if (pipe > 0 && pipe < line.Length - 1)
                {
                    geom = line.Substring(0, pipe);
                    hex = line.Substring(pipe + 1).Trim();
                    if (hex.Length != 6) hex = "FFFFFF";
                }

                var parts = geom.Split(';');
                if (parts.Length != 3) continue;

                var a = ParseXZ(parts[0], scale);
                var b = ParseXZ(parts[1], scale);
                var c = ParseXZ(parts[2], scale);

                if (flipZ)
                {
                    a.z = -a.z;
                    b.z = -b.z;
                    c.z = -c.z;
                }

                if (flipX)
                {
                    a.x = -a.x;
                    b.x = -b.x;
                    c.x = -c.x;
                }

                var v1 = new Vector3 { x = a.x, y = faceY, z = a.z };
                var v2 = new Vector3 { x = b.x, y = faceY, z = b.z };
                var v3 = new Vector3 { x = c.x, y = faceY, z = c.z };

                tris.Add(CreateTriangleOutward(v1, v2, v3, outwardCenter, hex));
            }

            return tris;
        }

        // Place decal on an X face (x constant), mapping:
        //   file X -> world Y
        //   file Z -> world Z
        private static List<ITriangleMeshWithColor> BuildLogoFaceDecal_XFace(
            string data,
            float faceX,
            Vector3 outwardCenter,
            float scale,
            bool flipX,
            bool flipZ)
        {
            var tris = new List<ITriangleMeshWithColor>();
            if (string.IsNullOrWhiteSpace(data)) return tris;

            var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length < 10) continue;

                string geom = line;
                string hex = "FFFFFF";

                int pipe = line.LastIndexOf('|');
                if (pipe > 0 && pipe < line.Length - 1)
                {
                    geom = line.Substring(0, pipe);
                    hex = line.Substring(pipe + 1).Trim();
                    if (hex.Length != 6) hex = "FFFFFF";
                }

                var parts = geom.Split(';');
                if (parts.Length != 3) continue;

                var a = ParseXZ(parts[0], scale);
                var b = ParseXZ(parts[1], scale);
                var c = ParseXZ(parts[2], scale);

                // IMPORTANT:
                // X-face mapping means file X becomes world Y.
                // So flipX => flip file X => flips world Y.
                if (flipX)
                {
                    a.x = -a.x;
                    b.x = -b.x;
                    c.x = -c.x;
                }

                if (flipZ)
                {
                    a.z = -a.z;
                    b.z = -b.z;
                    c.z = -c.z;
                }

                // x fixed, file x->world y, file z->world z
                var v1 = new Vector3 { x = faceX, y = a.x, z = a.z };
                var v2 = new Vector3 { x = faceX, y = b.x, z = b.z };
                var v3 = new Vector3 { x = faceX, y = c.x, z = c.z };

                tris.Add(CreateTriangleOutward(v1, v2, v3, outwardCenter, hex));
            }

            return tris;
        }

        private static Vector3 ParseXZ(string token, float scale)
        {
            var p = token.Split(',');
            if (p.Length != 2) return new Vector3();

            float x = float.Parse(p[0], CultureInfo.InvariantCulture) * scale;
            float z = float.Parse(p[1], CultureInfo.InvariantCulture) * scale;

            return new Vector3 { x = x, y = 0, z = z };
        }

        // ----------------------------------------------------
        //  TRIANGLE OUTWARD (Tower style)
        // ----------------------------------------------------
        private static TriangleMeshWithColor CreateTriangleOutward(
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 center,
            string color,
            bool noHidden = false)
        {
            var edge1 = Subtract(v2, v1);
            var edge2 = Subtract(v3, v1);
            var normal = Normalize(Cross(edge1, edge2));

            var mid = new Vector3
            {
                x = (v1.x + v2.x + v3.x) / 3f,
                y = (v1.y + v2.y + v3.y) / 3f,
                z = (v1.z + v2.z + v3.z) / 3f
            };

            var desired = Normalize(Subtract(mid, center));
            float dot = Dot(normal, desired);

            if (dot < 0f)
            {
                var tmp = v2;
                v2 = v3;
                v3 = tmp;
            }

            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = v1,
                vert2 = v2,
                vert3 = v3,
                noHidden = noHidden
            };
        }

        // ----------------------------------------------------
        //  VECTOR HELPERS
        // ----------------------------------------------------
        private static Vector3 Add(Vector3 a, Vector3 b)
            => new Vector3 { x = a.x + b.x, y = a.y + b.y, z = a.z + b.z };

        private static Vector3 Mul(Vector3 v, float s)
            => new Vector3 { x = v.x * s, y = v.y * s, z = v.z * s };

        private static Vector3 Subtract(Vector3 a, Vector3 b)
            => new Vector3 { x = a.x - b.x, y = a.y - b.y, z = a.z - b.z };

        private static Vector3 Cross(Vector3 a, Vector3 b)
            => new Vector3
            {
                x = a.y * b.z - a.z * b.y,
                y = a.z * b.x - a.x * b.z,
                z = a.x * b.y - a.y * b.x
            };

        private static float Dot(Vector3 a, Vector3 b)
            => a.x * b.x + a.y * b.y + a.z * b.z;

        private static Vector3 Normalize(Vector3 v)
        {
            float lenSq = v.x * v.x + v.y * v.y + v.z * v.z;
            if (lenSq <= 1e-6f) return new Vector3 { x = 0, y = 0, z = 0 };

            float inv = 1.0f / (float)Math.Sqrt(lenSq);
            return new Vector3 { x = v.x * inv, y = v.y * inv, z = v.z * inv };
        }
    }
}