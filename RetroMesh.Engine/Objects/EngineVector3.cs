namespace RetroMesh.Engine
{
    public class EngineVector3 : IVector3
    {
        public EngineVector3(float xVal = 0, float yVal = 0, float zVal = 0)
        {
            x = xVal;
            y = yVal;
            z = zVal;
        }

        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public override string ToString() => $"(x={x:F2}, y={y:F2}, z={z:F2})";
    }
}
