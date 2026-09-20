using RetroMesh.Engine;
using System;

namespace TheOmegaStrain.Domain
{
    /// <summary>One launch's state. Owned by ActiveWeapon, never by the shared mesh template.</summary>
    public sealed class RocketFlightState
    {
        // Seeded once when the motor stops; the regular physics system owns the fall.
        public IPhysics Physics { get; set; } = null!;
        // Accumulated, clamped movement time rather than age since launch.
        public float PoweredSeconds { get; set; }
        public bool IsFalling { get; set; }
        public bool IsExploding { get; set; }
        public DateTime ExplosionStarted { get; set; }
        // One loop per launch. ActiveWeapon state is shared across frame copies.
        public IAudioInstance? FlightSoundInstance { get; set; }
    }
}
