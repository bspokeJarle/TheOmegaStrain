using RetroMesh.Engine;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain.WeaponHelpers;

namespace TheOmegaStrain.Domain
{
    public interface IWeapon
    {
        IWeapon FireWeapon(IVector3 Trajectory, IVector3 StartPosition, IVector3 WorldPosition, WeaponType weaponType, I3dObject parentShip, int tilt);
        IObjectMovement ParentShip { get; set; }
        void MoveWeapon(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry);
        IEnumerable<I3dObject> Get3DObjects();
        List<List<IVector3>> GetCrashBoxes();
        List<IActiveWeapon> ActiveWeapons { get; set; }
    }

    public interface IActiveWeapon
    {
        I3dObject WeaponObject { get; set; }
        DateTime FiredTime { get; set; }
        IVector3 Velocity { get; set; }
        IVector3 Acceleration { get; set; }
    }
}
