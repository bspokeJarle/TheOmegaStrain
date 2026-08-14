using System;

namespace Domain
{
    public static class WeaponHelpers
    {
        public enum WeaponType
        {
            Bullet,
            Missile,
            Lazer,
            Rocket
        }
        public enum WeaponDamageType
        {
            Kinetic,
            Explosive,
            Energy
        }
        public static WeaponDamageType GetWeaponDamageType(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Bullet => WeaponDamageType.Kinetic,
                WeaponType.Missile => WeaponDamageType.Explosive,
                WeaponType.Lazer => WeaponDamageType.Energy,
                _ => throw new ArgumentOutOfRangeException(nameof(weaponType), $"Not expected weapon type value: {weaponType}"),
            };
        }
        public static int GetWeaponBaseDamage(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Bullet => 10,
                WeaponType.Missile => 50,
                WeaponType.Lazer => 25,
                _ => throw new ArgumentOutOfRangeException(nameof(weaponType), $"Not expected weapon type value: {weaponType}"),
            };
        }
    }
}
