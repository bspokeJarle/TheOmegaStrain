using TheOmegaStrain.Domain;
using System;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Gameplay.Helpers
{
    public static class PhysicsHelpers
    {
        private static readonly Random _random = new();

        public static float Clamp(float value, float min, float max)
        {
            return VectorMath.Clamp(value, min, max);
        }

        public static int Clamp(int value, int min, int max)
        {
            return VectorMath.Clamp(value, min, max);
        }

        public static string LerpColorHex(string hexFrom, string hexTo, float t)
        {
            return ColorMath.LerpColorHex(hexFrom, hexTo, t);
        }

        public static int ClampColor(int value)
        {
            return ColorMath.ClampColor(value);
        }

        public static IVector3 Subtract(IVector3 a, IVector3 b)
        {
            return ToVector3(VectorMath.Subtract(a, b));
        }

        public static IVector3 Add(IVector3 a, IVector3 b)
        {
            return ToVector3(VectorMath.Add(a, b));
        }

        public static IVector3 Multiply(IVector3 v, float scalar)
        {
            return ToVector3(VectorMath.Multiply(v, scalar));
        }

        public static IVector3 Normalize(IVector3 v)
        {
            return ToVector3(VectorMath.Normalize(v));
        }

        public static double GetLength(Vector3 point1, Vector3 point2)
        {
            return VectorMath.GetLength(point1, point2);
        }

        public static float Dot(IVector3 a, IVector3 b)
        {
            return VectorMath.Dot(a, b);
        }

        public static float Length(IVector3 v)
        {
            return VectorMath.Length(v);
        }

        public static IVector3 ClampMagnitude(IVector3 v, float maxLength)
        {
            return ToVector3(VectorMath.ClampMagnitude(v, maxLength));
        }

        public static IVector3 ReflectVelocity(IVector3 velocity, IVector3 normal, float bounceFactor)
        {
            return ToVector3(VectorMath.ReflectVelocity(velocity, normal, bounceFactor));
        }

        public static IVector3 GetTriangleCenter(TriangleMeshWithColor tri)
        {
            return ToVector3(VectorMath.GetTriangleCenter(tri));
        }

        public static IVector3 RandomUnitVector()
        {
            return ToVector3(VectorMath.RandomUnitVector(_random));
        }

        public static IVector3 RotateAroundAxis(IVector3 point, IVector3 axis, float angleDegrees, IVector3 origin)
        {
            return ToVector3(VectorMath.RotateAroundAxis(point, axis, angleDegrees, origin));
        }

        public static IVector3 CalculateTriangleGeometryCenter(I3dObject obj)
        {
            return ToVector3(VectorMath.CalculateTriangleGeometryCenter(obj));
        }

        private static Vector3 ToVector3(IVector3 vector)
        {
            return new Vector3(vector.x, vector.y, vector.z);
        }

        public static class RandomHelper
        {
            private static readonly Random _random = new();

            public static float Float(float min, float max)
            {
                return VectorMath.RandomFloat(_random, min, max);
            }
        }
    }
}
