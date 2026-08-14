using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public readonly record struct AngleCosSin(float CosRes, float SinRes);

    public static class GeometryMath
    {
        public static (float X, float Y, float Z) GetHeadingFromDirection(
            float dx,
            float dz,
            float cameraPitchDegrees)
        {
            float len = MathF.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f)
                return (cameraPitchDegrees, 0f, 0f);

            float headingDeg = MathF.Atan2(dz, dx) * (180f / MathF.PI);
            return (cameraPitchDegrees, 0f, headingDeg);
        }

        public static (float X, float Y, float Z) GetHeadingToTarget(
            IVector3 source,
            IVector3 target,
            float cameraPitchDegrees)
        {
            return GetHeadingFromDirection(
                target.x - source.x,
                target.z - source.z,
                cameraPitchDegrees);
        }

        public static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        public static float MoveAngleTowards(float current, float target, float maxDelta)
        {
            float delta = NormalizeAngle(target - current);
            if (MathF.Abs(delta) <= maxDelta)
                return current + delta;

            return current + MathF.Sign(delta) * maxDelta;
        }

        public static float DotNormalized(IVector3 a, IVector3 b)
        {
            float magA = MathF.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
            float magB = MathF.Sqrt(b.x * b.x + b.y * b.y + b.z * b.z);

            if (magA < 1e-6f || magB < 1e-6f)
                return 0f;

            return (a.x * b.x + a.y * b.y + a.z * b.z) / (magA * magB);
        }

        public static double GetDistance(IVector3 point1, IVector3 point2)
        {
            float dx = point1.x - point2.x;
            float dy = point1.y - point2.y;
            float dz = point1.z - point2.z;

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static float GetDistanceSquared(IVector3 point1, IVector3 point2)
        {
            float dx = point1.x - point2.x;
            float dy = point1.y - point2.y;
            float dz = point1.z - point2.z;

            return dx * dx + dy * dy + dz * dz;
        }

        public static AngleCosSin ConvertFromAngleToCosSin(float angle)
        {
            double radian = Math.PI * angle / 180.0;
            return new AngleCosSin(
                (float)Math.Cos(radian),
                (float)Math.Sin(radian));
        }

        public static EngineVector3 CopyVector(IVector3 vector)
        {
            return new EngineVector3(vector.x, vector.y, vector.z);
        }

        public static List<List<IVector3>> CopyCrashboxes(IReadOnlyList<List<IVector3>>? original)
        {
            return CopyCrashboxes(original, CopyVector);
        }

        public static List<List<IVector3>> CopyCrashboxes(
            IReadOnlyList<List<IVector3>>? original,
            Func<IVector3, IVector3> vectorFactory)
        {
            if (original == null || original.Count == 0)
                return new List<List<IVector3>>();

            var result = new List<List<IVector3>>(original.Count);

            for (int boxIndex = 0; boxIndex < original.Count; boxIndex++)
            {
                var box = original[boxIndex];
                var copiedBox = new List<IVector3>(box.Count);

                for (int pointIndex = 0; pointIndex < box.Count; pointIndex++)
                {
                    copiedBox.Add(vectorFactory(box[pointIndex]));
                }

                result.Add(copiedBox);
            }

            return result;
        }

        public static EngineVector3 GetCenterOfBox(IReadOnlyList<IVector3>? points)
        {
            if (points == null || points.Count == 0)
                return new EngineVector3();

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                minX = Math.Min(minX, p.x);
                maxX = Math.Max(maxX, p.x);

                minY = Math.Min(minY, p.y);
                maxY = Math.Max(maxY, p.y);

                minZ = Math.Min(minZ, p.z);
                maxZ = Math.Max(maxZ, p.z);
            }

            return new EngineVector3(
                (minX + maxX) / 2f,
                (minY + maxY) / 2f,
                (minZ + maxZ) / 2f);
        }
    }
}
