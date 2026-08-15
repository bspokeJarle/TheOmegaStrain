using System.Collections.Generic;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Runtime.Rendering
{
    public class WeaponsManager
    {
        public void HandleWeapons(OmegaObject3D inhabitant, List<OmegaObject3D> weaponObjectList)
        {
            if (inhabitant == null)
                return;

            if (inhabitant.WeaponSystems == null)
                return;

            var weaponSystem = inhabitant.WeaponSystems;

            //Get finished weapons from the weapon system
            foreach (var obj in weaponSystem.Get3DObjects())
            {
                if (obj is not OmegaObject3D weapon)
                    continue;
                // Match ParentSurface til skipet dersom den mangler
                if (weapon.ParentSurface == null)
                    weapon.ParentSurface = inhabitant.ParentSurface;

                weaponObjectList.Add(weapon);
            }
        }
    }
}

