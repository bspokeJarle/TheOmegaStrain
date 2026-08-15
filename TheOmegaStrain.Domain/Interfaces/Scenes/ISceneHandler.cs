using RetroMesh.Engine;
namespace TheOmegaStrain.Domain
{
    public interface ISceneHandler
    {
        void SetupActiveScene(I3dWorld world);
        void ResetActiveScene(I3dWorld world);
        void ResetActiveSceneToPlanetStart(I3dWorld world);
        void NextScene(I3dWorld world);
        IScene GetActiveScene();
        void HandleKeyPress(GameInputKey key, I3dWorld world);
        void HandleOverlayActivation(I3dWorld world);
        void UpdateFrame(I3dWorld world);
    }

    public interface IScene
    {
        SceneTypes SceneType { get; }
        SceneBiomeTypes SceneBiome { get; }
        GameModes GameMode { get; }
        void SetupScene(I3dWorld world);
        string SceneMusic { get; }
        void SetupSceneOverlay();
        void SetupGameOverlay();
        void SetupVideoOverlay(string fileName);

        /// <summary>
        /// Infection threshold percentage (0..100). When the percentage of
        /// infected bio tiles reaches this value the planet is lost.
        /// Later / harder scenes should return a lower value.
        /// </summary>
        float InfectionThresholdPercent => 12f;

        /// <summary>
        /// Infection spread rate per infection event. 1 = primary tile only
        /// (no extra spread). 2 = primary + 1 edge tile, 3 = primary + 2, etc.
        /// Higher values make infection spread faster (harder difficulty).
        /// </summary>
        int InfectionSpreadRate => 1;

        /// <summary>
        /// Offscreen seeder speed multiplier. With dt-scaling, seeders already
        /// move at onscreen speed regardless of offscreen FPS; this factor makes
        /// them move N× faster than onscreen when not visible.
        /// Higher = faster offscreen infection spread. Tune per scene.
        /// </summary>
        int SeederOffscreenSpeedFactor => 6;

        /// <summary>
        /// Delay in seconds before a seeder-infected tile spreads infection
        /// to its immediate neighbors. Lower values = faster cascading spread
        /// (harder difficulty). Set to 0 or negative to disable local spread.
        /// </summary>
        float LocalInfectionSpreadDelaySec => 1.0f;

        /// <summary>
        /// Maximum distance (world units) from an alive seeder for cascading
        /// infection to continue spreading. Tiles beyond this range stop
        /// spreading, so killing seeders halts local infection in that area.
        /// 0 or negative = no range limit.
        /// </summary>
        float LocalInfectionSpreadRadius => 10000f;

        /// <summary>
        /// Optional per-scene gameplay director that centralizes phase
        /// transitions, enemy activation, victory/defeat conditions, and
        /// checkpoint logic. Scenes without a director return null and
        /// rely on the default game-loop behaviour.
        /// </summary>
        ISceneDirector? Director => null;

        /// <summary>
        /// Per-scene aggression multiplier for MotherShipSmall behavior.
        /// 1.0 = baseline; higher means faster movement, quicker attacks and tighter tracking.
        /// </summary>
        float MotherShipSmallAggression => 1.0f;

        /// <summary>
        /// Per-scene aggression multiplier for MotherShipMedium behavior.
        /// 1.0 = baseline; higher means faster movement, quicker attacks and tighter tracking.
        /// </summary>
        float MotherShipMediumAggression => 1.0f;

        /// <summary>
        /// Per-scene aggression multiplier for MotherShipLarge behavior.
        /// 1.0 = baseline; higher means faster movement, quicker attacks and tighter tracking.
        /// </summary>
        float MotherShipLargeAggression => 1.0f;
    }
}
