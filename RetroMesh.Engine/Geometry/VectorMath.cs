using System;

namespace RetroMesh.Engine
{
    public static class VectorMath
    {
        public static float Clamp(float value, float min, float max)
        {
            return MathF.Min(MathF.Max(value, min), max);
        }

        public static int Clamp(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }

        public static EngineVector3 Subtract(IVector3 a, IVector3 b)
        {
            return new EngineVector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static EngineVector3 Add(IVector3 a, IVector3 b)
        {
            return new EngineVector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static EngineVector3 Multiply(IVector3 vector, float scalar)
        {
            return new EngineVector3(
                vector.x * scalar,
                vector.y * scalar,
                vector.z * scalar);
        }

        public static float Dot(IVector3 a, IVector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        public static float Length(IVector3 vector)
        {
            return MathF.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
        }

        public static double GetLength(IVector3 point1, IVector3 point2)
        {
            float dx = point1.x - point2.x;
            float dy = point1.y - point2.y;
            float dz = point1.z - point2.z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static EngineVector3 Normalize(IVector3 vector)
        {
            float length = Length(vector);
            return length <= 1e-6f
                ? new EngineVector3()
                : new EngineVector3(vector.x / length, vector.y / length, vector.z / length);
        }

        public static EngineVector3 ClampMagnitude(IVector3 vector, float maxLength)
        {
            float length = Length(vector);
            if (length <= maxLength)
                return new EngineVector3(vector.x, vector.y, vector.z);

            float scale = maxLength / length;
            return Multiply(vector, scale);
        }

        public static EngineVector3 ReflectVelocity(IVector3 velocity, IVector3 normal, float bounceFactor)
        {
            float dot = Dot(velocity, normal);
            var reflected = Subtract(velocity, Multiply(normal, 2 * dot));
            return Multiply(reflected, bounceFactor);
        }

        public static EngineVector3 GetTriangleCenter(ITriangleMeshWithColor triangle)
        {
            return new EngineVector3(
                (triangle.vert1.x + triangle.vert2.x + triangle.vert3.x) / 3f,
                (triangle.vert1.y + triangle.vert2.y + triangle.vert3.y) / 3f,
                (triangle.vert1.z + triangle.vert2.z + triangle.vert3.z) / 3f);
        }

        public static EngineVector3 RandomUnitVector(Random random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            var vector = new EngineVector3(
                (float)(random.NextDouble() * 2 - 1),
                (float)(random.NextDouble() * 2 - 1),
                (float)(random.NextDouble() * 2 - 1));

            return Normalize(vector);
        }

        public static float RandomFloat(Random random, float min, float max)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            return (float)(random.NextDouble() * (max - min) + min);
        }

        public static EngineVector3 RotateAroundAxis(
            IVector3 point,
            IVector3 axis,
            float angleDegrees,
            IVector3 origin)
        {
            float angleRad = angleDegrees * (MathF.PI / 180f);
            var normalizedAxis = Normalize(axis);
            var translated = Subtract(point, origin);

            float cos = MathF.Cos(angleRad);
            float sin = MathF.Sin(angleRad);

            var rotated = new EngineVector3
            {
                x = (cos + (1 - cos) * normalizedAxis.x * normalizedAxis.x) * translated.x +
                    ((1 - cos) * normalizedAxis.x * normalizedAxis.y - normalizedAxis.z * sin) * translated.y +
                    ((1 - cos) * normalizedAxis.x * normalizedAxis.z + normalizedAxis.y * sin) * translated.z,

                y = ((1 - cos) * normalizedAxis.y * normalizedAxis.x + normalizedAxis.z * sin) * translated.x +
                    (cos + (1 - cos) * normalizedAxis.y * normalizedAxis.y) * translated.y +
                    ((1 - cos) * normalizedAxis.y * normalizedAxis.z - normalizedAxis.x * sin) * translated.z,

                z = ((1 - cos) * normalizedAxis.z * normalizedAxis.x - normalizedAxis.y * sin) * translated.x +
                    ((1 - cos) * normalizedAxis.z * normalizedAxis.y + normalizedAxis.x * sin) * translated.y +
                    (cos + (1 - cos) * normalizedAxis.z * normalizedAxis.z) * translated.z
            };

            return Add(rotated, origin);
        }

        public static EngineVector3 CalculateTriangleGeometryCenter(IRenderable3dObject obj)
        {
            if (obj?.ObjectParts == null || obj.ObjectParts.Count == 0)
                return new EngineVector3();

            float sumX = 0f;
            float sumY = 0f;
            float sumZ = 0f;
            int count = 0;

            foreach (var part in obj.ObjectParts)
            {
                if (part?.Triangles == null)
                    continue;

                foreach (var triangle in part.Triangles)
                {
                    var center = GetTriangleCenter(triangle);
                    sumX += center.x;
                    sumY += center.y;
                    sumZ += center.z;
                    count++;
                }
            }

            return count == 0
                ? new EngineVector3()
                : new EngineVector3(sumX / count, sumY / count, sumZ / count);
        }
    }
}
