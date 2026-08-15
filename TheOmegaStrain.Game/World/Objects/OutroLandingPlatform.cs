using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class OutroLandingPlatform
    {
        private const float HalfWidth = 330f;
        private const float HalfDepth = 210f;
        private const float PadBottomZ = 28f;
        private const float PadTopZ = 120f;
        private const float MarkingZ = 168f;
        private const float LandingMarkLength = 310f;
        private const float LandingMarkWidth = 30f;
        private const float CrashBoxBottomZ = PadTopZ - 12f;
        private const float CrashBoxTopZ = PadTopZ + 22f;
        private const string PadColor = "4E5852";
        private const string PadSideColor = "343D39";
        private const string MarkingColor = "F4D35E";

        public static _3dObject CreatePlatform(ISurface parentSurface)
        {
            var platform = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            platform.ObjectName = "OutroLandingPlatform";
            platform.ParentSurface = parentSurface;
            platform.WorldPosition = new Vector3();
            platform.ObjectOffsets = new Vector3();
            platform.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
            platform.CrashBoxes = LandingPlatformCrashBoxes();
            platform.CrashBoxNames = new List<string?> { "LandingPad" };
            platform.CrashBoxesFollowRotation = true;
            platform.CrashBoxDebugMode = false;
            platform.ImpactStatus = new ImpactStatus();
            platform.ZSortBias = 35f;

            platform.ObjectParts.Add(new _3dObjectPart
            {
                PartName = "LandingPlatformPad",
                Triangles = CreatePad(),
                IsVisible = true
            });

            platform.ObjectParts.Add(new _3dObjectPart
            {
                PartName = "LandingPlatformMarkings",
                Triangles = CreateMarkings(),
                IsVisible = true
            });

            return platform;
        }

        private static List<ITriangleMeshWithColor> CreatePad()
        {
            var tris = new List<ITriangleMeshWithColor>();
            AddRect(tris, -HalfWidth, -HalfDepth, HalfWidth, HalfDepth, PadTopZ, PadColor);
            AddSlabSides(tris);
            return tris;
        }

        private static List<ITriangleMeshWithColor> CreateMarkings()
        {
            var tris = new List<ITriangleMeshWithColor>();
            AddRotatedRect(tris, 0f, 0f, LandingMarkLength, LandingMarkWidth, 42f, MarkingZ, MarkingColor);
            AddRotatedRect(tris, 0f, 0f, LandingMarkLength, LandingMarkWidth, -42f, MarkingZ, MarkingColor);
            return tris;
        }

        public static List<List<IVector3>> LandingPlatformCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -HalfWidth, y = -HalfDepth, z = CrashBoxBottomZ },
                    new Vector3 { x = HalfWidth, y = HalfDepth, z = CrashBoxTopZ })
            };
        }

        private static void AddSlabSides(List<ITriangleMeshWithColor> triangles)
        {
            AddVerticalRect(triangles,
                new Vector3(-HalfWidth, -HalfDepth, PadBottomZ),
                new Vector3(HalfWidth, -HalfDepth, PadBottomZ),
                new Vector3(HalfWidth, -HalfDepth, PadTopZ),
                new Vector3(-HalfWidth, -HalfDepth, PadTopZ),
                PadSideColor);

            AddVerticalRect(triangles,
                new Vector3(HalfWidth, -HalfDepth, PadBottomZ),
                new Vector3(HalfWidth, HalfDepth, PadBottomZ),
                new Vector3(HalfWidth, HalfDepth, PadTopZ),
                new Vector3(HalfWidth, -HalfDepth, PadTopZ),
                PadSideColor);

            AddVerticalRect(triangles,
                new Vector3(HalfWidth, HalfDepth, PadBottomZ),
                new Vector3(-HalfWidth, HalfDepth, PadBottomZ),
                new Vector3(-HalfWidth, HalfDepth, PadTopZ),
                new Vector3(HalfWidth, HalfDepth, PadTopZ),
                PadSideColor);

            AddVerticalRect(triangles,
                new Vector3(-HalfWidth, HalfDepth, PadBottomZ),
                new Vector3(-HalfWidth, -HalfDepth, PadBottomZ),
                new Vector3(-HalfWidth, -HalfDepth, PadTopZ),
                new Vector3(-HalfWidth, HalfDepth, PadTopZ),
                PadSideColor);
        }

        private static void AddRect(List<ITriangleMeshWithColor> triangles, float left, float top, float right, float bottom, float z, string color)
        {
            var topLeft = new Vector3(left, top, z);
            var topRight = new Vector3(right, top, z);
            var bottomRight = new Vector3(right, bottom, z);
            var bottomLeft = new Vector3(left, bottom, z);

            triangles.Add(CreateTri(topLeft, topRight, bottomRight, color));
            triangles.Add(CreateTri(topLeft, bottomRight, bottomLeft, color));
        }

        private static void AddRotatedRect(List<ITriangleMeshWithColor> triangles, float centerX, float centerY, float length, float width, float angleDegrees, float z, string color)
        {
            float radians = angleDegrees * (MathF.PI / 180f);
            float dx = MathF.Cos(radians) * length * 0.5f;
            float dy = MathF.Sin(radians) * length * 0.5f;
            float px = -MathF.Sin(radians) * width * 0.5f;
            float py = MathF.Cos(radians) * width * 0.5f;

            var a = new Vector3(centerX - dx - px, centerY - dy - py, z);
            var b = new Vector3(centerX + dx - px, centerY + dy - py, z);
            var c = new Vector3(centerX + dx + px, centerY + dy + py, z);
            var d = new Vector3(centerX - dx + px, centerY - dy + py, z);

            triangles.Add(CreateTri(a, b, c, color));
            triangles.Add(CreateTri(a, c, d, color));
        }

        private static void AddVerticalRect(List<ITriangleMeshWithColor> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, string color)
        {
            triangles.Add(CreateTri(a, b, c, color));
            triangles.Add(CreateTri(a, c, d, color));
        }

        private static TriangleMeshWithColor CreateTri(Vector3 a, Vector3 b, Vector3 c, string color)
        {
            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = a,
                vert2 = b,
                vert3 = c,
                normal1 = new Vector3 { z = 1 },
                normal2 = new Vector3 { z = 1 },
                normal3 = new Vector3 { z = 1 },
                noHidden = true
            };
        }
    }
}
