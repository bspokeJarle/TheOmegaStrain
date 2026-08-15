using System;
using System.Collections.Generic;

namespace Domain
{
    public interface IParticles
    {
        IObjectMovement ParentShip { get; set; }
        List<IParticle> Particles { get; set; }

        /// <summary>
        /// Multiplier applied to particle lifetime. Default 1.0 (no change).
        /// </summary>
        float LifeMultiplier { get; set; }

        /// <summary>
        /// When greater than 0, overrides the built-in max particle cap.
        /// Default 0 means use the built-in constant.
        /// </summary>
        int MaxParticlesOverride { get; set; }

        void ReleaseParticles(ITriangleMeshWithColor Trajectory, ITriangleMeshWithColor StartPosition, IVector3 WorldPosition, IObjectMovement ParentShip, int Thrust, bool? explosion, float upwardVelocityBoost = 0f);
        void MoveParticles();
    }

    public interface IParticle
    {
        ITriangleMeshWithColor ParticleTriangle { get; set; }
        IVector3 Velocity { get; set; }
        IVector3 Acceleration { get; set; }
        long VariedStart { get; set; }
        float Life { get; set; }
        float Size { get; set; }
        string Color { get; set; }
        DateTime BirthTime { get; set; }
        bool IsRotated { get; set; }
        IVector3 Position { get; set; }
        IVector3 WorldPosition { get; set; }
        IVector3? Rotation { get; set; }
        IVector3? RotationSpeed { get; set; }
        bool? NoShading { get; set; }
        bool Visible { get; set; }
        IImpactStatus? ImpactStatus { get; set; }
        IPhysics? Physics { get; set; }
    }
}
