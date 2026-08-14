using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Runtime.Rendering
{
    public class WeaponsManager
    {
        public void HandleWeapons(_3dObject inhabitant, List<_3dObject> weaponObjectList)
        {
            if (inhabitant == null)
                return;

            if (inhabitant.WeaponSystems == null)
                return;

            var weaponSystem = inhabitant.WeaponSystems;

            //Get finished weapons from the weapon system
            foreach (var obj in weaponSystem.Get3DObjects())
            {
                if (obj is not _3dObject weapon)
                    continue;
                // Match ParentSurface til skipet dersom den mangler
                if (weapon.ParentSurface == null)
                    weapon.ParentSurface = inhabitant.ParentSurface;

                weaponObjectList.Add(weapon);
            }
        }
    }
}

