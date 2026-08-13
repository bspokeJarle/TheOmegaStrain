namespace Domain
{
    public partial class _3dSpecificsImplementations
    {
        public class _3dObject : Engine3dObject, I3dObject
        {
            public IObjectMovement? Movement { get; set; }
            public IParticles? Particles { get; set; }
            public IImpactStatus? ImpactStatus { get; set; }
            public int? Mass { get; set; }
            public ISurface? ParentSurface { get; set; }
            public int? SurfaceBasedId { get; set; }
            public bool? CrashBoxDebugMode { get; set; }
            public IWeapon? WeaponSystems { get; set; }
            public bool HasPowerUp { get; set; } = false;
            public PowerUpType PowerUpType { get; set; } = PowerUpType.Standard;
        }
    }
}
