using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class OutroAstronaut
    {
        public const string ObjectName = "OutroAstronaut";
        public const string WavingArmPartName = "AstronautWavingArm";

        private const float FigureY = -12f;
        private const float FigureDepth = 2.2f;
        private const string SuitLight = "F4F7F8";
        private const string SuitShadow = "AEB8C2";
        public const string VisorColor = "14334A";
        private const string GloveColor = "FFFFFF";

        public static _3dObject CreateAstronaut(ISurface? parentSurface)
        {
            var astronaut = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            astronaut.ObjectName = ObjectName;
            astronaut.ParentSurface = parentSurface;
            astronaut.WorldPosition = new Vector3();
            astronaut.ObjectOffsets = new Vector3();
            astronaut.Rotation = new Vector3();
            astronaut.SurfaceBasedId = null;
            astronaut.CrashBoxes = new List<List<IVector3>>();
            astronaut.CrashBoxDebugMode = false;
            astronaut.CrashBoxesFollowRotation = false;
            astronaut.ImpactStatus = new ImpactStatus();
            var movement = new OutroAstronautControls(WavingArmPartName);
            astronaut.Movement = movement;
            astronaut.ZSortBias = OutroLandingShipControls.LandingZSortBias + 65f;

            AddPart(astronaut, "AstronautBody", CreateBody(), true);
            AddPart(astronaut, "AstronautHelmet", CreateHelmet(), true);
            AddPart(astronaut, "AstronautLeftArm", CreateLeftArm(), true);
            AddPart(astronaut, WavingArmPartName, CreateWavingArm(), true);
            movement.PrepareInitialPose(astronaut);

            return astronaut;
        }

        private static List<ITriangleMeshWithColor> CreateBody()
        {
            var tris = new List<ITriangleMeshWithColor>();
            AddRectXZ(tris, -9f, 4f, 9f, 25f, FigureY, SuitLight);
            AddRectXZ(tris, -6f, 8f, 6f, 19f, FigureY - FigureDepth, SuitShadow);
            AddRectXZ(tris, -8f, -9f, -1f, 4f, FigureY, SuitShadow);
            AddRectXZ(tris, 1f, -9f, 8f, 4f, FigureY, SuitShadow);
            return tris;
        }

        private static List<ITriangleMeshWithColor> CreateHelmet()
        {
            var tris = new List<ITriangleMeshWithColor>();
            AddOctagon(tris, 0f, 36f, 10f, FigureY, SuitLight);
            AddRectXZ(tris, -6f, 33f, 6f, 38f, FigureY - FigureDepth, VisorColor);
            return tris;
        }

        private static List<ITriangleMeshWithColor> CreateLeftArm()
        {
            var tris = new List<ITriangleMeshWithColor>();
            AddSegmentXZ(tris, -8f, 22f, -21f, 12f, 4f, FigureY, SuitShadow);
            AddOctagon(tris, -23f, 10f, 4f, FigureY, GloveColor);
            return tris;
        }

        private static List<ITriangleMeshWithColor> CreateWavingArm()
        {
            var tris = new List<ITriangleMeshWithColor>();
            AddSegmentXZ(tris, 8f, 23f, 22f, 36f, 4f, FigureY, SuitShadow);
            AddSegmentXZ(tris, 22f, 36f, 31f, 49f, 4f, FigureY, SuitLight);
            AddOctagon(tris, 33f, 52f, 4.5f, FigureY, GloveColor);
            return tris;
        }

        private static void AddRectXZ(List<ITriangleMeshWithColor> tris, float left, float bottom, float right, float top, float y, string color)
        {
            var a = new Vector3(left, y, bottom);
            var b = new Vector3(right, y, bottom);
            var c = new Vector3(right, y, top);
            var d = new Vector3(left, y, top);
            tris.Add(CreateTri(a, b, c, color));
            tris.Add(CreateTri(a, c, d, color));
        }

        private static void AddSegmentXZ(List<ITriangleMeshWithColor> tris, float x1, float z1, float x2, float z2, float width, float y, string color)
        {
            float dx = x2 - x1;
            float dz = z2 - z1;
            float length = MathF.Sqrt((dx * dx) + (dz * dz));
            if (length <= 0.001f)
                return;

            float px = -dz / length * width * 0.5f;
            float pz = dx / length * width * 0.5f;
            var a = new Vector3(x1 - px, y, z1 - pz);
            var b = new Vector3(x2 - px, y, z2 - pz);
            var c = new Vector3(x2 + px, y, z2 + pz);
            var d = new Vector3(x1 + px, y, z1 + pz);
            tris.Add(CreateTri(a, b, c, color));
            tris.Add(CreateTri(a, c, d, color));
        }

        private static void AddOctagon(List<ITriangleMeshWithColor> tris, float centerX, float centerZ, float radius, float y, string color)
        {
            var center = new Vector3(centerX, y, centerZ);
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * MathF.PI * 0.25f;
                float a1 = (i + 1) * MathF.PI * 0.25f;
                var p0 = new Vector3(centerX + MathF.Cos(a0) * radius, y, centerZ + MathF.Sin(a0) * radius);
                var p1 = new Vector3(centerX + MathF.Cos(a1) * radius, y, centerZ + MathF.Sin(a1) * radius);
                tris.Add(CreateTri(center, p0, p1, color));
            }
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

        private static void AddPart(_3dObject obj, string name, List<ITriangleMeshWithColor> triangles, bool visible)
        {
            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = name,
                Triangles = triangles,
                IsVisible = visible
            });
        }
    }
}
