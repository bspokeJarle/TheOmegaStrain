namespace Domain
{
    public partial class _3dSpecificsImplementations
    {
        public class Vector3 : EngineVector3
        {
            public Vector3(float xVal = 0, float yVal = 0, float zVal = 0)
                : base(xVal, yVal, zVal)
            {
            }

            public static Vector3 operator -(Vector3 a, Vector3 b)
                => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);

            public static Vector3 operator +(Vector3 a, Vector3 b)
                => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);

            public static Vector3 operator *(Vector3 v, float s)
                => new Vector3(v.x * s, v.y * s, v.z * s);

            public static Vector3 operator *(float s, Vector3 v)
                => new Vector3(v.x * s, v.y * s, v.z * s);
        }
    }
}
