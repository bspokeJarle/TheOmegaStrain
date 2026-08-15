using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheOmegaStrain.Common.CommonSetup
{
    //TODO: Expand this as needed, common enemy properties can go here
    public static class EnemySetup
    {
        public const int SeederHealth = 55;
        public const int KamikazeDroneHealth = 55;
        public const int KamikazeDroneCollisionDamage = 50;
        public const int DroneDecoyCollisionDamage = 55;
        public const int BomberBombCollisionDamage = 75;
        public const int MotherShipSmallHealth = 1430;
        public const int MotherShipSmallCollisionDamage = 80;
        public const int MotherShipMediumHealth = 1926;
        public const int MotherShipMediumCollisionDamage = 150;
        public const int MotherShipLargeHealth = 3300;
        public const int MotherShipLargeCollisionDamage = 250;
        public const int SpaceSwanHealth = 300;
        public const int ZeppelinBomberHealth = 105;

        public static List<string> EnemyTypes = new List<string>
        {
            "Seeder",
            "KamikazeDrone",
            "ZeppelinBomber",
            "BomberBomb",
            "AttackShip",
            "Endboss1",
            "Endboss2",
            "MotherShipSmall",
            "MotherShipMedium",
            "MotherShipLarge",
            "SpaceSwan",
            "PolarBear"
        };

        public static bool IsEnemyTypeValid(string objectName)
        {
            return EnemyTypes.Contains(objectName);
        }

        public static bool IsMotherShipType(string? objectName)
        {
            return objectName == "MotherShipSmall"
                || objectName == "MotherShipMedium"
                || objectName == "MotherShipLarge";
        }

        public static int GetMotherShipBaseHealth(string? objectName)
        {
            return objectName switch
            {
                "MotherShipLarge" => MotherShipLargeHealth,
                "MotherShipMedium" => MotherShipMediumHealth,
                _ => MotherShipSmallHealth
            };
        }

        public static int GetMotherShipHealth(string? objectName, float aggression)
        {
            return MotherShipDifficultySetup.ScaleHealth(GetMotherShipBaseHealth(objectName), aggression);
        }
    }
}
    
