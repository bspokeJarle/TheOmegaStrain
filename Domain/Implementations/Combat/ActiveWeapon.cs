using System;
using static Domain.WeaponHelpers;

namespace Domain
{
    public partial class _3dSpecificsImplementations
    {
        public class ActiveWeapon : IActiveWeapon
        {
            public I3dObject WeaponObject { get; set; }
            public DateTime FiredTime { get; set; }
            public float Velocity { get; set; }
            public float Acceleration { get; set; }
            public IVector3 Trajectory { get; set; }
            public IVector3 StartWorldPosition { get; set; }
            public IVector3 CurrentWorldPosition { get; set; }
            public float DistanceTraveled { get; set; }
            public float MaxRange { get; set; }
            public double LifetimeSeconds { get; set; }
            public DateTime LastUpdateUtc { get; set; }
            public WeaponType WeaponType { get; set; }
            IVector3 IActiveWeapon.Velocity { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            IVector3 IActiveWeapon.Acceleration { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }
    }
}
