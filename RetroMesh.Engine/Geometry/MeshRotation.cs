using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Domain
{
    public class MeshRotation
    {
        // Kept in sync with object shadow projection defaults.
        private static readonly System.Numerics.Vector3 LightVector = new(37.5f, 137.5f, 250f);
        private static readonly System.Numerics.Vector3 LightDir = System.Numerics.Vector3.Normalize(LightVector);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RotateToVector(IVector3 source, IVector3 target, float cosRes, float sinRes, char axis)
        {
            float x = source.x;
            float y = source.y;
            float z = source.z;

            switch (axis)
            {
                case 'X':
                    target.x = x;
                    target.y = y * cosRes - z * sinRes;
                    target.z = z * cosRes + y * sinRes;
                    break;
                case 'Y':
                    target.x = x * cosRes + z * sinRes;
                    target.y = y;
                    target.z = z * cosRes - x * sinRes;
                    break;
                case 'Z':
                    target.x = x * cosRes - y * sinRes;
                    target.y = y * cosRes + x * sinRes;
                    target.z = z;
                    break;
                default:
                    target.x = x;
                    target.y = y;
                    target.z = z;
                    break;
            }
        }

        private static ITriangleMeshWithColor RotateTriangle(ITriangleMeshWithColor coord, float cosRes, float sinRes, char axis)
        {
            RotateToVector(coord.vert1, coord.vert1, cosRes, sinRes, axis);
            RotateToVector(coord.vert2, coord.vert2, cosRes, sinRes, axis);
            RotateToVector(coord.vert3, coord.vert3, cosRes, sinRes, axis);

            return CalculateNormalAndAngle(coord);
        }

        private static ITriangleMeshWithColor CalculateNormalAndAngle(ITriangleMeshWithColor coord)
        {
            var v1 = coord.vert1;
            var v2 = coord.vert2;
            var v3 = coord.vert3;

            float ux = v2.x - v1.x;
            float uy = v2.y - v1.y;
            float uz = v2.z - v1.z;

            float vx = v3.x - v1.x;
            float vy = v3.y - v1.y;
            float vz = v3.z - v1.z;

            float nx = uy * vz - uz * vy;
            float ny = uz * vx - ux * vz;
            float nz = ux * vy - uy * vx;

            float normalLength = MathF.Max(1e-6f, MathF.Sqrt(nx * nx + ny * ny + nz * nz));
            float invLength = 1f / normalLength;

            nx *= invLength;
            ny *= invLength;
            nz *= invLength;

            coord.normal1.x = nx;
            coord.normal1.y = ny;
            coord.normal1.z = nz;
            coord.angle = (LightDir.X * nx) + (LightDir.Y * ny) + (LightDir.Z * nz);

            return coord;
        }

        public ITriangleMeshWithColor RotateOnX(float cosRes, float sinRes, ITriangleMeshWithColor coord) =>
            RotateTriangle(coord, cosRes, sinRes, 'X');

        public ITriangleMeshWithColor RotateOnY(float cosRes, float sinRes, ITriangleMeshWithColor coord) =>
            RotateTriangle(coord, cosRes, sinRes, 'Y');

        public ITriangleMeshWithColor RotateOnZ(float cosRes, float sinRes, ITriangleMeshWithColor coord) =>
            RotateTriangle(coord, cosRes, sinRes, 'Z');

        public List<ITriangleMeshWithColor> RotateMesh(List<ITriangleMeshWithColor> mesh, double angle, char axis)
        {
            double radian = Math.PI * angle / 180.0;
            float cosRes = (float)Math.Cos(radian);
            float sinRes = (float)Math.Sin(radian);

            for (int i = 0; i < mesh.Count; i++)
            {
                RotateTriangle(mesh[i], cosRes, sinRes, axis);
            }

            return mesh;
        }

        public List<ITriangleMeshWithColor> RotateXMesh(List<ITriangleMeshWithColor> mesh, double angle) =>
            RotateMesh(mesh, angle, 'X');

        public List<ITriangleMeshWithColor> RotateYMesh(List<ITriangleMeshWithColor> mesh, double angle) =>
            RotateMesh(mesh, angle, 'Y');

        public List<ITriangleMeshWithColor> RotateZMesh(List<ITriangleMeshWithColor> mesh, double angle) =>
            RotateMesh(mesh, angle, 'Z');

        public EngineVector3 RotatePoint(double angleInDegrees, IVector3 coord, char axis)
        {
            double radians = Math.PI * angleInDegrees / 180.0;
            float cosRes = (float)Math.Cos(radians);
            float sinRes = (float)Math.Sin(radians);
            var result = new EngineVector3();
            RotateToVector(coord, result, cosRes, sinRes, axis);
            return result;
        }
    }
}
