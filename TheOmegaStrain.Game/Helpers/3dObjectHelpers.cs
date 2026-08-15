using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Game.Helpers
{
    public static class _3dObjectHelpers
    {
        public const string ShadowColorHex = MeshGeometryOperations.ShadowColorHex;
        public static bool _localLoggingEnabled = false;

        public static void ApplyScaleToTriangles(List<ITriangleMeshWithColor> triangles, float scale)
        {
            MeshGeometryOperations.ApplyScaleToTriangles(triangles, scale);
        }

        public static void ApplyScaleToObject(I3dObject actualObject, float scale)
        {
            MeshGeometryOperations.ApplyScaleToObject(actualObject, scale, CopyVectorAsInterface);
        }

        public static void NormalizeSurfaceFootprintPivot(I3dObject actualObject)
        {
            MeshGeometryOperations.NormalizeSurfaceFootprintPivot(actualObject, CopyVectorAsInterface);
        }

        public static void AddSimplifiedShadowPart(I3dObject actualObject, bool useFlatQuad = false, int layers = 2)
        {
            MeshGeometryOperations.AddSimplifiedShadowPart(
                actualObject,
                static () => new _3dObjectPart(),
                static () => new TriangleMeshWithColor(),
                static (x, y, z) => new Vector3(x, y, z),
                useFlatQuad,
                layers);
        }

        public static void AddCustomShadowPart(I3dObject actualObject, List<ITriangleMeshWithColor> triangles)
        {
            MeshGeometryOperations.AddCustomShadowPart(
                actualObject,
                triangles,
                static () => new _3dObjectPart());
        }

        public static List<IVector3> GenerateAabbCrashBoxFromRotated(List<IVector3> rotatedPoints)
        {
            return MeshGeometryOperations.GenerateAabbCrashBoxFromRotated(
                rotatedPoints,
                static (x, y, z) => (IVector3)new Vector3(x, y, z));
        }

        public static List<IVector3> GenerateCrashBoxCorners(Vector3 min, Vector3 max)
        {
            return MeshGeometryOperations.GenerateCrashBoxCorners(
                min,
                max,
                static (x, y, z) => (IVector3)new Vector3(x, y, z));
        }

        public static double GetDistance(Vector3 point1, Vector3 point2)
        {
            return GeometryMath.GetDistance(point1, point2);
        }

        public struct CosSin
        {
            public float CosRes { get; set; }
            public float SinRes { get; set; }
        }

        public static bool CheckCollisionBoxVsBox(
            List<Vector3> boxA,
            List<Vector3> boxB,
            string? nameA = null,
            string? nameB = null)
        {
            float marginX = -GameSetup.CollisionMarginX;
            float marginY = GameSetup.CollisionMarginY;
            float marginZ = GameSetup.CollisionMarginZ;

            bool overlaps = MeshGeometryOperations.CheckAabbOverlap(
                boxA,
                boxB,
                marginX,
                marginY,
                marginZ,
                out var boundsA,
                out var boundsB);

            if (Logger.ShouldLog(_localLoggingEnabled) && nameA != null && nameB != null)
            {
                Logger.Log(
                    $"AABBCHK {nameA} vs {nameB} | " +
                    $"X:{RangesOverlap(boundsA.MinX, boundsA.MaxX, boundsB.MinX, boundsB.MaxX, marginX)} " +
                    $"Y:{RangesOverlap(boundsA.MinY, boundsA.MaxY, boundsB.MinY, boundsB.MaxY, marginY)} " +
                    $"Z:{RangesOverlap(boundsA.MinZ, boundsA.MaxZ, boundsB.MinZ, boundsB.MaxZ, marginZ)} | " +
                    $"A[min=({boundsA.MinX:0.#},{boundsA.MinY:0.#},{boundsA.MinZ:0.#}) max=({boundsA.MaxX:0.#},{boundsA.MaxY:0.#},{boundsA.MaxZ:0.#})] " +
                    $"B[min=({boundsB.MinX:0.#},{boundsB.MinY:0.#},{boundsB.MinZ:0.#}) max=({boundsB.MaxX:0.#},{boundsB.MaxY:0.#},{boundsB.MaxZ:0.#})]");
            }

            return overlaps;
        }

        public static List<ITriangleMeshWithColor> ConvertToTrianglesWithColor(List<TriangleMesh> triangles, string color)
        {
            return MeshGeometryOperations.ConvertToTrianglesWithColor(
                triangles,
                color,
                static () => new TriangleMeshWithColor(),
                CopyVectorAsInterface);
        }

        public static void AddQuadOutward(
            List<ITriangleMeshWithColor> tris,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 v4,
            Vector3 center,
            string color,
            bool noHidden = false)
        {
            MeshGeometryOperations.AddQuadOutward(
                tris,
                v1,
                v2,
                v3,
                v4,
                center,
                color,
                static () => new TriangleMeshWithColor(),
                noHidden);
        }

        public static TriangleMeshWithColor CreateTriangleOutward(
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 center,
            string color,
            bool noHidden = false)
        {
            return MeshGeometryOperations.CreateTriangleOutward(
                v1,
                v2,
                v3,
                center,
                color,
                static () => new TriangleMeshWithColor(),
                noHidden);
        }

        public static Vector3 Subtract(Vector3 a, Vector3 b)
        {
            return ToVector3(MeshGeometryOperations.Subtract(a, b));
        }

        public static Vector3 Add(Vector3 a, Vector3 b)
        {
            return ToVector3(MeshGeometryOperations.Add(a, b));
        }

        public static Vector3 Scale(Vector3 v, float s)
        {
            return ToVector3(MeshGeometryOperations.Scale(v, s));
        }

        public static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return ToVector3(MeshGeometryOperations.Cross(a, b));
        }

        public static float Dot(Vector3 a, Vector3 b)
        {
            return MeshGeometryOperations.Dot(a, b);
        }

        public static Vector3 Normalize(Vector3 v)
        {
            return ToVector3(MeshGeometryOperations.Normalize(v));
        }

        private static bool RangesOverlap(float minA, float maxA, float minB, float maxB, float margin)
        {
            return (maxA + margin) >= (minB - margin) && (minA - margin) <= (maxB + margin);
        }

        private static IVector3 CopyVectorAsInterface(IVector3 vector)
        {
            return new Vector3(vector.x, vector.y, vector.z);
        }

        private static Vector3 ToVector3(IVector3 vector)
        {
            return new Vector3(vector.x, vector.y, vector.z);
        }
    }
}
