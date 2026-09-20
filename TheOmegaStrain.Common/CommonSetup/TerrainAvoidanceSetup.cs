using System.Collections.Generic;

namespace TheOmegaStrain.Common.CommonSetup
{
    public static class TerrainAvoidanceSetup
    {
        public const float DefaultRecoveryDurationSeconds = 0.75f;
        public const float DefaultLiftSpeed = 220f;
        public const float DefaultHorizontalSpeed = 320f;

        public const float HeavyRecoveryDurationSeconds = 1.15f;
        public const float HeavyLiftSpeed = 170f;
        public const float HeavyHorizontalSpeed = 360f;
        public const float HeavyProactiveAvoidanceDistance = 950f;
        public const float MotherShipSmallProactiveAvoidanceDistance = 350f;

        // These clearances preserve the flight heights already authored for each
        // object type. The local surface tile, not the camera or world origin,
        // decides where the floor is.
        public const float SeederMinimumSurfaceClearance = 200f;
        public const float KamikazeDroneMinimumSurfaceClearance = 120f;
        public const float ZeppelinBomberMinimumSurfaceClearance = 130f;
        public const float SpaceSwanMinimumSurfaceClearance = 120f;
        public const float MotherShipSmallMinimumSurfaceClearance = 25f;
        public const float MotherShipMediumMinimumSurfaceClearance = 105f;
        public const float MotherShipLargeMinimumSurfaceClearance = 75f;

        private static readonly HashSet<string> TerrainObstacleNames = new()
        {
            "Surface",
            "Tower",
            "SnowTower",
            "Tree",
            "LeafTree",
            "LargePalm",
            "SmallPalm",
            "BambooHut",
            "House",
            "SmallIgloo",
            "LargeIgloo"
        };

        private static readonly HashSet<string> AvoidanceCapableAiNames = new()
        {
            "Seeder",
            "KamikazeDrone",
            "ZeppelinBomber",
            "MotherShipSmall",
            "MotherShipMedium",
            "MotherShipLarge",
            "SpaceSwan"
        };

        private static readonly HashSet<string> HeavyAiNames = new()
        {
            "MotherShipSmall",
            "MotherShipMedium",
            "MotherShipLarge"
        };

        public static bool IsTerrainObstacle(string? objectName) =>
            !string.IsNullOrEmpty(objectName) && TerrainObstacleNames.Contains(objectName);

        public static bool IsProactiveTerrainObstacle(string? objectName) =>
            IsTerrainObstacle(objectName) && objectName != "Surface";

        public static bool IsAvoidanceCapableAi(string? objectName) =>
            !string.IsNullOrEmpty(objectName) && AvoidanceCapableAiNames.Contains(objectName);

        public static bool IsHeavyAvoidanceAi(string? objectName) =>
            !string.IsNullOrEmpty(objectName) && HeavyAiNames.Contains(objectName);

        public static float GetProactiveAvoidanceDistance(string? objectName) =>
            objectName == "MotherShipSmall"
                ? MotherShipSmallProactiveAvoidanceDistance
                : HeavyProactiveAvoidanceDistance;

        public static float GetMinimumSurfaceClearance(string? objectName) => objectName switch
        {
            "Seeder" => SeederMinimumSurfaceClearance,
            "KamikazeDrone" => KamikazeDroneMinimumSurfaceClearance,
            "ZeppelinBomber" => ZeppelinBomberMinimumSurfaceClearance,
            "SpaceSwan" => SpaceSwanMinimumSurfaceClearance,
            "MotherShipSmall" => MotherShipSmallMinimumSurfaceClearance,
            "MotherShipMedium" => MotherShipMediumMinimumSurfaceClearance,
            "MotherShipLarge" => MotherShipLargeMinimumSurfaceClearance,
            _ => 0f
        };
    }
}
