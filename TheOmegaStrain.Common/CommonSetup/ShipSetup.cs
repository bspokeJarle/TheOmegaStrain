namespace TheOmegaStrain.Common.CommonSetup
{
    public static class ShipSetup
    {
        public const int DefaultShipHealth = EnemySetup.KamikazeDroneCollisionDamage * 2;

        // Health restored when the ship collects a MedKit pickup. Healing is capped at
        // DefaultShipHealth so a pickup can never overheal past the starting maximum.
        public const int MedKitHealAmount = 50;
    }
}
