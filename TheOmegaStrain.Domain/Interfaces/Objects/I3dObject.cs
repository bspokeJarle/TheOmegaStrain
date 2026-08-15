using RetroMesh.Engine;
namespace TheOmegaStrain.Domain
{
    public interface I3dObject : IRenderable3dObject
    {
        IObjectMovement? Movement { get; set; }
        IParticles? Particles { get; set; }
        IWeapon? WeaponSystems { get; set; }
        IImpactStatus? ImpactStatus { get; set; }
        int? Mass { get; set; }
        ISurface? ParentSurface { get; set; }
        int? SurfaceBasedId { get; set; }
        bool? CrashBoxDebugMode { get; set; }
        bool HasPowerUp { get; set; }
        PowerUpType PowerUpType { get; set; }
    }
}
