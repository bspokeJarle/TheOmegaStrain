using System.Collections.Generic;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Runtime.Rendering
{
    public class WeaponsManager
    {
        private readonly ParticleManager _particleManager = new();

        public void HandleWeapons(OmegaObject3D inhabitant, List<OmegaObject3D> weaponObjectList,
            List<OmegaObject3D>? particleObjectList = null)
        {
            if (inhabitant == null)
                return;

            if (inhabitant.WeaponSystems == null)
                return;

            var weaponSystem = inhabitant.WeaponSystems;

            // Weapons owns flight/cleanup; this adapter only collects meshes and effects.
            foreach (var obj in weaponSystem.Get3DObjects())
            {
                if (obj is not OmegaObject3D weapon)
                    continue;
                // Projectile collision and particle shadows use the owner's surface.
                if (weapon.ParentSurface == null)
                    weapon.ParentSurface = inhabitant.ParentSurface;

                weaponObjectList.Add(weapon);
                if (particleObjectList != null)
                    _particleManager.HandleParticles(weapon, particleObjectList);
            }
        }
    }
}
