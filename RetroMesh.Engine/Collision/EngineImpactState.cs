namespace RetroMesh.Engine
{
    public class EngineImpactState : IImpactState
    {
        public bool HasExploded { get; set; }
        public bool HasCrashed { get; set; }
        public string ObjectName { get; set; } = string.Empty;
        public ImpactDirection? ImpactDirection { get; set; }
        public string? CrashBoxName { get; set; }
    }
}
