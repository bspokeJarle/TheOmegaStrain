namespace RetroMesh.Engine
{
    public interface IImpactState
    {
        bool HasExploded { get; set; }
        bool HasCrashed { get; set; }
        string ObjectName { get; set; }
        ImpactDirection? ImpactDirection { get; set; }
        string? CrashBoxName { get; set; }
    }
}
