using System;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using static TheOmegaStrain.Domain.WeaponHelpers;

namespace TheOmegaStrain.Common.CommonGlobalState.States
{
    public enum GamePhase
    {
        Intro = 0,
        Playing = 1,
        Paused = 2,
        GameOver = 3,
        Outro = 4
    }

    /// <summary>
    /// TODO: Temporary gameplay state
    /// Keep this as pure data + tiny convenience methods.
    /// </summary>
    public sealed class GamePlayState
    {
        // -----------------------------
        // High-level game flow
        // -----------------------------
        public GamePhase Phase { get; set; } = GamePhase.Intro;
        public bool IsVictoryRewardPauseActive { get; set; } = false;

        public bool IsPaused => Phase == GamePhase.Paused;
        public bool IsPlaying => Phase == GamePhase.Playing;

        // -----------------------------
        // Player / ship core stats
        // -----------------------------
        public string PlayerName { get; set; } = "";
        public int SceneIndex { get; set; } = 0;
        public SceneTypes CurrentSceneType { get; set; } = SceneTypes.Intro;
        public int Lives { get; set; } = 3;

        // Simulation round counter — increments each time the player enters the simulation after the outro
        public int SimulationRound { get; set; } = 0;

        public SceneBiomeTypes CurrentSceneBiome { get; set; } = SceneBiomeTypes.HillsWoods;

        // Keep as float to allow smooth damage later (e.g., collision damage scaling)
        public float Health { get; set; } = 100f;
        public float Alt { get; set; } = 0f;
        public float Thrust { get; set; } = 0f;
        public float MaxHealth { get; set; } = 100f;

        public bool IsDead => Health <= 0f;

        // Optional: invulnerability after hit
        public float InvulnerableSecondsLeft { get; set; } = 0f;

        // -----------------------------
        // Score / progression
        // -----------------------------
        public long Score { get; set; } = 0;
        public int PlanetStyleBonusScore { get; set; } = 0;
        public int PlanetStyleBonusSceneIndex { get; set; } = 0;

        // Wave/level can drive seeder spawn intensity later
        public int WaveNumber { get; set; } = 1;

        // -----------------------------
        // Combat statistics
        // -----------------------------
        public int TotalShotsFired { get; set; } = 0;
        public int TotalKills { get; set; } = 0;
        public int TotalDeaths { get; set; } = 0;
        public float Accuracy => TotalShotsFired > 0 ? (float)TotalKills / TotalShotsFired : 0f;

        // -----------------------------
        // Checkpoint state (auto-saved on powerup/mothership kill)
        // -----------------------------

        /// <summary>
        /// Lightweight value type holding all checkpoint fields.
        /// Used to preserve checkpoint data across scene resets.
        /// </summary>
        public readonly record struct CheckpointSnapshot(
            long Score, int Lives, float Health, int PowerUpsCollected, int SpeedPowerUpLevel,
            int SeedersRemaining, int DronesRemaining, int MotherShipsRemaining,
            int TotalShotsFired, int TotalKills, int TotalDeaths,
            float InfectionLevel, int WaveNumber, int SceneIndex, int SimulationRound, SceneBiomeTypes SceneBiome,
            int InitialSeeders, int InitialDrones, int InitialMotherShips,
            int PlanetStyleBonusScore, int PlanetStyleBonusSceneIndex);

        public bool HasCheckpoint { get; set; } = false;
        public long CheckpointScore { get; set; } = 0;
        public int CheckpointLives { get; set; } = 3;
        public float CheckpointHealth { get; set; } = 100f;
        public int CheckpointPowerUpsCollected { get; set; } = 0;
        public int CheckpointSpeedPowerUpLevel { get; set; } = 0;
        public int CheckpointSeedersRemaining { get; set; } = 0;
        public int CheckpointDronesRemaining { get; set; } = 0;
        public int CheckpointMotherShipsRemaining { get; set; } = 0;
        public int CheckpointTotalShotsFired { get; set; } = 0;
        public int CheckpointTotalKills { get; set; } = 0;
        public int CheckpointTotalDeaths { get; set; } = 0;
        public float CheckpointInfectionLevel { get; set; } = 0f;
        public int CheckpointWaveNumber { get; set; } = 1;
        public int CheckpointSceneIndex { get; set; } = 0;
        public int CheckpointSimulationRound { get; set; } = 0;
        public SceneBiomeTypes CheckpointSceneBiome { get; set; } = SceneBiomeTypes.HillsWoods;
        public int CheckpointInitialSeeders { get; set; } = 0;
        public int CheckpointInitialDrones { get; set; } = 0;
        public int CheckpointInitialMotherShips { get; set; } = 0;
        public int CheckpointPlanetStyleBonusScore { get; set; } = 0;
        public int CheckpointPlanetStyleBonusSceneIndex { get; set; } = 0;

        public bool HasPlanetStartSnapshot { get; set; } = false;
        public int PlanetStartSceneIndex { get; set; } = 0;
        public long PlanetStartScore { get; set; } = 0;
        public int PlanetStartLives { get; set; } = 3;
        public float PlanetStartHealth { get; set; } = 100f;
        public int PlanetStartPowerUpsCollected { get; set; } = 0;
        public int PlanetStartSpeedPowerUpLevel { get; set; } = 0;
        public int PlanetStartSeedersRemaining { get; set; } = 0;
        public int PlanetStartDronesRemaining { get; set; } = 0;
        public int PlanetStartMotherShipsRemaining { get; set; } = 0;
        public int PlanetStartTotalShotsFired { get; set; } = 0;
        public int PlanetStartTotalKills { get; set; } = 0;
        public int PlanetStartTotalDeaths { get; set; } = 0;
        public float PlanetStartInfectionLevel { get; set; } = 0f;
        public int PlanetStartWaveNumber { get; set; } = 1;
        public int PlanetStartSimulationRound { get; set; } = 0;
        public SceneBiomeTypes PlanetStartSceneBiome { get; set; } = SceneBiomeTypes.HillsWoods;
        public int PlanetStartInitialSeeders { get; set; } = 0;
        public int PlanetStartInitialDrones { get; set; } = 0;
        public int PlanetStartInitialMotherShips { get; set; } = 0;
        public int PlanetStartPlanetStyleBonusScore { get; set; } = 0;
        public int PlanetStartPlanetStyleBonusSceneIndex { get; set; } = 0;

        /// <summary>
        /// Captures the current checkpoint fields into a snapshot value type.
        /// Call before ResetForNewGame to preserve checkpoint data.
        /// </summary>
        public CheckpointSnapshot CaptureCheckpointSnapshot() => new(
            CheckpointScore, CheckpointLives, CheckpointHealth, CheckpointPowerUpsCollected, CheckpointSpeedPowerUpLevel,
            CheckpointSeedersRemaining, CheckpointDronesRemaining, CheckpointMotherShipsRemaining,
            CheckpointTotalShotsFired, CheckpointTotalKills, CheckpointTotalDeaths,
            CheckpointInfectionLevel, CheckpointWaveNumber, CheckpointSceneIndex > 0 ? CheckpointSceneIndex : SceneIndex, CheckpointSimulationRound, CheckpointSceneBiome,
            CheckpointInitialSeeders, CheckpointInitialDrones, CheckpointInitialMotherShips,
            CheckpointPlanetStyleBonusScore, CheckpointPlanetStyleBonusSceneIndex);

        public CheckpointSnapshot CapturePlanetStartSnapshot() => new(
            PlanetStartScore, PlanetStartLives, PlanetStartHealth, PlanetStartPowerUpsCollected, PlanetStartSpeedPowerUpLevel,
            PlanetStartSeedersRemaining, PlanetStartDronesRemaining, PlanetStartMotherShipsRemaining,
            PlanetStartTotalShotsFired, PlanetStartTotalKills, PlanetStartTotalDeaths,
            PlanetStartInfectionLevel, PlanetStartWaveNumber, PlanetStartSceneIndex, PlanetStartSimulationRound, PlanetStartSceneBiome,
            PlanetStartInitialSeeders, PlanetStartInitialDrones, PlanetStartInitialMotherShips,
            PlanetStartPlanetStyleBonusScore, PlanetStartPlanetStyleBonusSceneIndex);

        /// <summary>
        /// Restores both current gameplay state and checkpoint fields from a snapshot.
        /// Deducts one life and increments TotalDeaths for the death that triggered the restart.
        /// </summary>
        public void ApplyCheckpointRestart(CheckpointSnapshot cp)
        {
            HasCheckpoint = true;

            // Restore current state from checkpoint (with death penalty)
            Score = cp.Score;
            Lives = Math.Max(0, cp.Lives - 1);
            Health = cp.Health;
            PowerUpsCollected = cp.PowerUpsCollected;
            SpeedPowerUpLevel = cp.SpeedPowerUpLevel;
            TotalShotsFired = cp.TotalShotsFired;
            TotalKills = cp.TotalKills;
            TotalDeaths = cp.TotalDeaths + 1;
            InfectionLevel = cp.InfectionLevel;
            WaveNumber = cp.WaveNumber;
            SceneIndex = cp.SceneIndex;
            SimulationRound = cp.SimulationRound;
            CurrentSceneBiome = cp.SceneBiome;
            InitialSeeders = cp.InitialSeeders;
            InitialDrones = cp.InitialDrones;
            InitialMotherShips = cp.InitialMotherShips;
            PlanetStyleBonusScore = cp.PlanetStyleBonusScore;
            PlanetStyleBonusSceneIndex = cp.PlanetStyleBonusSceneIndex;

            // Preserve checkpoint fields for future deaths
            CheckpointScore = cp.Score;
            CheckpointLives = cp.Lives;
            CheckpointHealth = cp.Health;
            CheckpointPowerUpsCollected = cp.PowerUpsCollected;
            CheckpointSpeedPowerUpLevel = cp.SpeedPowerUpLevel;
            CheckpointSeedersRemaining = cp.SeedersRemaining;
            CheckpointDronesRemaining = cp.DronesRemaining;
            CheckpointMotherShipsRemaining = cp.MotherShipsRemaining;
            CheckpointTotalShotsFired = cp.TotalShotsFired;
            CheckpointTotalKills = cp.TotalKills;
            CheckpointTotalDeaths = cp.TotalDeaths;
            CheckpointInfectionLevel = cp.InfectionLevel;
            CheckpointWaveNumber = cp.WaveNumber;
            CheckpointSceneIndex = cp.SceneIndex;
            CheckpointSimulationRound = cp.SimulationRound;
            CheckpointSceneBiome = cp.SceneBiome;
            CheckpointInitialSeeders = cp.InitialSeeders;
            CheckpointInitialDrones = cp.InitialDrones;
            CheckpointInitialMotherShips = cp.InitialMotherShips;
            CheckpointPlanetStyleBonusScore = cp.PlanetStyleBonusScore;
            CheckpointPlanetStyleBonusSceneIndex = cp.PlanetStyleBonusSceneIndex;
        }

        public void SavePlanetStartSnapshot()
        {
            EnsurePlanetStyleBonusScene();

            HasPlanetStartSnapshot = true;
            PlanetStartSceneIndex = SceneIndex;
            PlanetStartScore = Score;
            PlanetStartLives = Lives;
            PlanetStartHealth = Health;
            PlanetStartPowerUpsCollected = PowerUpsCollected;
            PlanetStartSpeedPowerUpLevel = SpeedPowerUpLevel;
            PlanetStartSeedersRemaining = SeedersRemaining;
            PlanetStartDronesRemaining = DronesRemaining;
            PlanetStartMotherShipsRemaining = MotherShipsRemaining;
            PlanetStartTotalShotsFired = TotalShotsFired;
            PlanetStartTotalKills = TotalKills;
            PlanetStartTotalDeaths = TotalDeaths;
            PlanetStartInfectionLevel = InfectionLevel;
            PlanetStartWaveNumber = WaveNumber;
            PlanetStartSimulationRound = SimulationRound;
            PlanetStartSceneBiome = CurrentSceneBiome;
            PlanetStartInitialSeeders = InitialSeeders;
            PlanetStartInitialDrones = InitialDrones;
            PlanetStartInitialMotherShips = InitialMotherShips;
            PlanetStartPlanetStyleBonusScore = PlanetStyleBonusScore;
            PlanetStartPlanetStyleBonusSceneIndex = PlanetStyleBonusSceneIndex;
        }

        public void RestorePlanetStartSnapshotFields(CheckpointSnapshot snapshot, int sceneIndex)
        {
            HasPlanetStartSnapshot = true;
            PlanetStartSceneIndex = sceneIndex;
            PlanetStartScore = snapshot.Score;
            PlanetStartLives = snapshot.Lives;
            PlanetStartHealth = snapshot.Health;
            PlanetStartPowerUpsCollected = snapshot.PowerUpsCollected;
            PlanetStartSpeedPowerUpLevel = snapshot.SpeedPowerUpLevel;
            PlanetStartSeedersRemaining = snapshot.SeedersRemaining;
            PlanetStartDronesRemaining = snapshot.DronesRemaining;
            PlanetStartMotherShipsRemaining = snapshot.MotherShipsRemaining;
            PlanetStartTotalShotsFired = snapshot.TotalShotsFired;
            PlanetStartTotalKills = snapshot.TotalKills;
            PlanetStartTotalDeaths = snapshot.TotalDeaths;
            PlanetStartInfectionLevel = snapshot.InfectionLevel;
            PlanetStartWaveNumber = snapshot.WaveNumber;
            PlanetStartSimulationRound = snapshot.SimulationRound;
            PlanetStartSceneBiome = snapshot.SceneBiome;
            PlanetStartInitialSeeders = snapshot.InitialSeeders;
            PlanetStartInitialDrones = snapshot.InitialDrones;
            PlanetStartInitialMotherShips = snapshot.InitialMotherShips;
            PlanetStartPlanetStyleBonusScore = snapshot.PlanetStyleBonusScore;
            PlanetStartPlanetStyleBonusSceneIndex = snapshot.PlanetStyleBonusSceneIndex;
        }

        public void ApplyPlanetStartSnapshot()
        {
            if (!HasPlanetStartSnapshot)
                return;

            Score = PlanetStartScore;
            Lives = PlanetStartLives;
            Health = PlanetStartHealth;
            PowerUpsCollected = PlanetStartPowerUpsCollected;
            SpeedPowerUpLevel = PlanetStartSpeedPowerUpLevel;
            SeedersRemaining = PlanetStartSeedersRemaining;
            DronesRemaining = PlanetStartDronesRemaining;
            MotherShipsRemaining = PlanetStartMotherShipsRemaining;
            TotalShotsFired = PlanetStartTotalShotsFired;
            TotalKills = PlanetStartTotalKills;
            TotalDeaths = PlanetStartTotalDeaths;
            InfectionLevel = PlanetStartInfectionLevel;
            WaveNumber = PlanetStartWaveNumber;
            SceneIndex = PlanetStartSceneIndex;
            SimulationRound = PlanetStartSimulationRound;
            CurrentSceneBiome = PlanetStartSceneBiome;
            InitialSeeders = PlanetStartInitialSeeders;
            InitialDrones = PlanetStartInitialDrones;
            InitialMotherShips = PlanetStartInitialMotherShips;
            PlanetStyleBonusScore = PlanetStartPlanetStyleBonusScore;
            PlanetStyleBonusSceneIndex = PlanetStartPlanetStyleBonusSceneIndex;
            ClearCheckpoint();
        }

        public void ClearCheckpoint()
        {
            HasCheckpoint = false;
            CheckpointScore = 0;
            CheckpointLives = 3;
            CheckpointHealth = 100f;
            CheckpointPowerUpsCollected = 0;
            CheckpointSpeedPowerUpLevel = 0;
            CheckpointSeedersRemaining = 0;
            CheckpointDronesRemaining = 0;
            CheckpointMotherShipsRemaining = 0;
            CheckpointTotalShotsFired = 0;
            CheckpointTotalKills = 0;
            CheckpointTotalDeaths = 0;
            CheckpointInfectionLevel = 0f;
            CheckpointWaveNumber = 1;
            CheckpointSceneIndex = 0;
            CheckpointSimulationRound = 0;
            CheckpointSceneBiome = SceneBiomeTypes.HillsWoods;
            CheckpointInitialSeeders = 0;
            CheckpointInitialDrones = 0;
            CheckpointInitialMotherShips = 0;
            CheckpointPlanetStyleBonusScore = 0;
            CheckpointPlanetStyleBonusSceneIndex = SceneIndex;
        }

        public void ClearPlanetStartSnapshot()
        {
            HasPlanetStartSnapshot = false;
            PlanetStartSceneIndex = SceneIndex;
            PlanetStartScore = 0;
            PlanetStartLives = 3;
            PlanetStartHealth = 100f;
            PlanetStartPowerUpsCollected = 0;
            PlanetStartSpeedPowerUpLevel = 0;
            PlanetStartSeedersRemaining = 0;
            PlanetStartDronesRemaining = 0;
            PlanetStartMotherShipsRemaining = 0;
            PlanetStartTotalShotsFired = 0;
            PlanetStartTotalKills = 0;
            PlanetStartTotalDeaths = 0;
            PlanetStartInfectionLevel = 0f;
            PlanetStartWaveNumber = 1;
            PlanetStartSimulationRound = 0;
            PlanetStartSceneBiome = SceneBiomeTypes.HillsWoods;
            PlanetStartInitialSeeders = 0;
            PlanetStartInitialDrones = 0;
            PlanetStartInitialMotherShips = 0;
            PlanetStartPlanetStyleBonusScore = 0;
            PlanetStartPlanetStyleBonusSceneIndex = SceneIndex;
        }

        // -----------------------------
        // Enemy tracking (alive counts for HUD)
        // -----------------------------
        public int DronesRemaining { get; set; } = 0;
        public int SeedersRemaining { get; set; } = 0;
        public int MotherShipsRemaining { get; set; } = 0;
        public int InitialDrones { get; set; } = 0;
        public int InitialSeeders { get; set; } = 0;
        public int InitialMotherShips { get; set; } = 0;

        // -----------------------------
        // Infection / lose condition (core to Omega Strain)
        // -----------------------------
        /// <summary>
        /// Raw count of infected tiles. Incremented by 1 per tile infected.
        /// </summary>
        public float InfectionLevel { get; set; } = 0f;

        /// <summary>
        /// Total number of bio tiles on the map (Grassland + Highlands).
        /// Set during scene loading after eco map generation.
        /// </summary>
        public int TotalBioTiles { get; set; } = 0;

        /// <summary>
        /// Current infection as a percentage (0..100).
        /// </summary>
        public float InfectionPercent => TotalBioTiles > 0 ? (InfectionLevel / TotalBioTiles) * 100f : 0f;

        public const float BiomassCriticalWarningRatio = 0.80f;
        public const float BiomassAbortWarningRatio = 0.96f;

        /// <summary>
        /// Infection threshold percentage (0..100). When InfectionPercent
        /// reaches this value, the planet is lost. Configurable per level.
        /// </summary>
        public float InfectionCriticalMass { get; set; } = 100f;

        public float InfectionCriticalProgress
        {
            get
            {
                if (TotalBioTiles <= 0)
                    return 0f;

                if (InfectionCriticalMass <= 0f)
                    return InfectionLevel > 0f ? 1f : 0f;

                return InfectionPercent / InfectionCriticalMass;
            }
        }

        public bool IsBiomassCriticalWarning => InfectionCriticalProgress >= BiomassCriticalWarningRatio;
        public bool IsBiomassAbortWarning => InfectionCriticalProgress >= BiomassAbortWarningRatio;

        public bool IsInfectionCritical => TotalBioTiles > 0 && InfectionPercent >= InfectionCriticalMass;

        /// <summary>
        /// Number of tiles infected per infection event. 1 = primary tile only
        /// (no extra spread). Higher values cause each infection to also spread
        /// to neighboring edge tiles. Configurable per scene for difficulty.
        /// </summary>
        public int InfectionSpreadRate { get; set; } = 1;

        /// <summary>
        /// Offscreen seeder speed multiplier. Configured per scene.
        /// </summary>
        public int SeederOffscreenSpeedFactor { get; set; } = 6;

        /// <summary>
        /// Delay in seconds before a seeder-infected tile cascades infection
        /// to its neighbors. Configured per scene. 0 or negative = disabled.
        /// </summary>
        public float LocalInfectionSpreadDelaySec { get; set; } = 1.0f;

        /// <summary>
        /// Maximum world-unit distance from an alive seeder for cascading
        /// infection to continue. 0 or negative = unlimited range.
        /// </summary>
        public float LocalInfectionSpreadRadius { get; set; } = 10000f;

        // Per-scene mothership aggression tuning
        public float MotherShipSmallAggression { get; set; } = 1.0f;
        public float MotherShipMediumAggression { get; set; } = 1.0f;
        public float MotherShipLargeAggression { get; set; } = 1.0f;

        // -----------------------------
        // Weapons (simple, but practical)
        // -----------------------------
        public WeaponType SelectedWeapon { get; set; } = WeaponType.Bullet;
        public string ActivePowerup { get; set; } = "BULLET";

        // -----------------------------
        // MotherShip health bar (in-world, follows the object)
        // -----------------------------
        public float MotherShipHealthPercent { get; set; } = 1f;
        public float MotherShipScreenX { get; set; } = 0f;
        public float MotherShipScreenY { get; set; } = 0f;
        public bool ShowMotherShipHealthBar { get; set; } = false;
        public bool MotherShipIsOnScreen { get; set; } = false;

        // MotherShip ram warning (flashing reticle before charge)
        public bool MotherShipRamWarningActive { get; set; } = false;
        public float MotherShipRamWarningScreenX { get; set; } = 0f;
        public float MotherShipRamWarningScreenY { get; set; } = 0f;

        // Aim assist target indicator (white reticle around locked target)
        public bool AimAssistTargetActive { get; set; } = false;
        public float AimAssistTargetScreenX { get; set; } = 0f;
        public float AimAssistTargetScreenY { get; set; } = 0f;

        // PowerUp progression: each collected PowerUp unlocks the next weapon tier
        public int PowerUpsCollected { get; set; } = 0;
        public bool IsDecoyUnlocked => PowerUpsCollected >= 1;
        public bool IsLazerUnlocked => PowerUpsCollected >= 2;

        private int _speedPowerUpLevel;
        public int SpeedPowerUpLevel
        {
            get => _speedPowerUpLevel;
            set => _speedPowerUpLevel = Math.Clamp(value, 0, 2);
        }

        public float TravelSpeedMultiplier => SpeedPowerUpLevel switch
        {
            1 => 1.20f,
            >= 2 => 1.50f,
            _ => 1f
        };

        public bool ApplySpeedPowerUp(PowerUpType powerUpType)
        {
            int requestedLevel = powerUpType switch
            {
                PowerUpType.TravelSpeedLevel1 => 1,
                PowerUpType.TravelSpeedLevel2 => 2,
                _ => 0
            };

            if (requestedLevel <= SpeedPowerUpLevel)
                return false;

            SpeedPowerUpLevel = requestedLevel;
            return true;
        }

        public int LaserAmmo { get; set; } = -1;   // -1 means infinite
        public int RocketAmmo { get; set; } = 10;
        public int BulletAmmo { get; set; } = -1;  // -1 means infinite

        // Cooldowns (seconds)
        public float LaserCooldownLeft { get; set; } = 0f;
        public float RocketCooldownLeft { get; set; } = 0f;
        public float BulletCooldownLeft { get; set; } = 0f;

        public float LaserCooldownSeconds { get; set; } = 0.333f;
        public float RocketCooldownSeconds { get; set; } = 0.65f;
        public float BulletCooldownSeconds { get; set; } = 0.15f;

        public bool CanFireLaser => !IsPaused && LaserCooldownLeft <= 0f && (LaserAmmo != 0);
        public bool CanFireRocket => !IsPaused && RocketCooldownLeft <= 0f && RocketAmmo > 0;
        public bool CanFireBullet => !IsPaused && BulletCooldownLeft <= 0f && (BulletAmmo != 0);

        // -----------------------------
        // Convenience / deterministic update
        // -----------------------------
        public void Update(float dtSeconds)
        {
            if (dtSeconds <= 0f) return;

            if (InvulnerableSecondsLeft > 0f)
            {
                InvulnerableSecondsLeft -= dtSeconds;
                if (InvulnerableSecondsLeft < 0f) InvulnerableSecondsLeft = 0f;
            }

            if (LaserCooldownLeft > 0f)
            {
                LaserCooldownLeft -= dtSeconds;
                if (LaserCooldownLeft < 0f) LaserCooldownLeft = 0f;
            }

            if (RocketCooldownLeft > 0f)
            {
                RocketCooldownLeft -= dtSeconds;
                if (RocketCooldownLeft < 0f) RocketCooldownLeft = 0f;
            }

            if (BulletCooldownLeft > 0f)
            {
                BulletCooldownLeft -= dtSeconds;
                if (BulletCooldownLeft < 0f) BulletCooldownLeft = 0f;
            }

            // If you want infection to tick automatically during play:
            // if (Phase == GamePhase.Playing) InfectionLevel = MathF.Min(1f, InfectionLevel + dtSeconds * 0.001f);
        }

        public void ApplyDamage(float amount, float invulnerableSeconds = 0.25f)
        {
            if (amount <= 0f) return;
            if (InvulnerableSecondsLeft > 0f) return;
            if (Phase == GamePhase.GameOver || Phase == GamePhase.Outro) return;

            Health -= amount;
            if (Health < 0f) Health = 0f;

            InvulnerableSecondsLeft = Math.Max(InvulnerableSecondsLeft, invulnerableSeconds);
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            Health += amount;
            if (Health > MaxHealth) Health = MaxHealth;
        }

        public void ConsumeLifeAndRespawn()
        {
            TotalDeaths++;
            Score = Math.Max(0, Score - GameSetup.DeathScorePenalty);

            if (HasCheckpoint)
            {
                RestoreCheckpoint();
            }

            if (Lives > 0) Lives--;

            Health = MaxHealth;
            InvulnerableSecondsLeft = 1.0f;

            if (Lives < 0) Lives = 0;
        }

        public void TriggerGameOver()
        {
            Phase = GamePhase.GameOver;
        }

        public bool TryFireSelectedWeapon()
        {
            if (SelectedWeapon == WeaponType.Lazer && CanFireLaser)
            {
                LaserCooldownLeft = LaserCooldownSeconds;
                if (LaserAmmo > 0) LaserAmmo--;
                TotalShotsFired++;
                return true;
            }

            if (SelectedWeapon == WeaponType.Rocket && CanFireRocket)
            {
                RocketCooldownLeft = RocketCooldownSeconds;
                RocketAmmo--;
                if (RocketAmmo < 0) RocketAmmo = 0;
                TotalShotsFired++;
                return true;
            }

            if (SelectedWeapon == WeaponType.Bullet && CanFireBullet)
            {
                BulletCooldownLeft = BulletCooldownSeconds;
                if (BulletAmmo > 0) BulletAmmo--;
                TotalShotsFired++;
                return true;
            }

            return false;
        }

        public void ResetForNewGame()
        {
            Phase = GamePhase.Intro;
            IsVictoryRewardPauseActive = false;
            CurrentSceneType = SceneTypes.Intro;
            CurrentSceneBiome = SceneBiomeTypes.HillsWoods;

            Lives = 3;
            MaxHealth = 100f;
            Health = 100f;
            InvulnerableSecondsLeft = 0f;

            Score = 0;
            PlanetStyleBonusScore = 0;
            PlanetStyleBonusSceneIndex = SceneIndex;
            WaveNumber = 1;

            TotalShotsFired = 0;
            TotalKills = 0;
            TotalDeaths = 0;

            ClearCheckpoint();
            ClearPlanetStartSnapshot();

            DronesRemaining = 0;
            SeedersRemaining = 0;
            MotherShipsRemaining = 0;
            InitialDrones = 0;
            InitialSeeders = 0;
            InitialMotherShips = 0;

            InfectionLevel = 0f;
            TotalBioTiles = 0;
            InfectionCriticalMass = 100f; // safe default; overridden by scene threshold
            InfectionSpreadRate = 1;
            SeederOffscreenSpeedFactor = 6;
            LocalInfectionSpreadDelaySec = 1.0f;
            LocalInfectionSpreadRadius = 10000f;
            MotherShipSmallAggression = 1.0f;
            MotherShipMediumAggression = 1.0f;
            MotherShipLargeAggression = 1.0f;

            MotherShipHealthPercent = 1f;
            MotherShipScreenX = 0f;
            MotherShipScreenY = 0f;
            ShowMotherShipHealthBar = false;
            MotherShipIsOnScreen = false;

            MotherShipRamWarningActive = false;
            MotherShipRamWarningScreenX = 0f;
            MotherShipRamWarningScreenY = 0f;

            AimAssistTargetActive = false;
            AimAssistTargetScreenX = 0f;
            AimAssistTargetScreenY = 0f;

            SelectedWeapon = WeaponType.Bullet;
            ActivePowerup = "BULLET";
            PowerUpsCollected = 0;
            SpeedPowerUpLevel = 0;
            LaserAmmo = -1;
            RocketAmmo = 10;
            BulletAmmo = -1;

            LaserCooldownLeft = 0f;
            RocketCooldownLeft = 0f;
            BulletCooldownLeft = 0f;
        }

        /// <summary>
        /// Awards score for killing an enemy and increments the kill counter.
        /// </summary>
        public void RecordKill(string enemyType)
        {
            TotalKills++;
            Score += GameSetup.GetKillScore(enemyType);
        }

        public int AwardStyleBonus(int requestedScore)
        {
            if (requestedScore <= 0)
                return 0;

            EnsurePlanetStyleBonusScene();

            int remaining = Math.Max(0, GameSetup.PlanetStyleBonusScoreCap - PlanetStyleBonusScore);
            int awarded = Math.Min(requestedScore, remaining);
            if (awarded <= 0)
                return 0;

            PlanetStyleBonusScore += awarded;
            return awarded;
        }

        public int PlanetStyleBonusRemaining
        {
            get
            {
                EnsurePlanetStyleBonusScene();
                return Math.Max(0, GameSetup.PlanetStyleBonusScoreCap - PlanetStyleBonusScore);
            }
        }

        private void EnsurePlanetStyleBonusScene()
        {
            if (PlanetStyleBonusSceneIndex == SceneIndex)
                return;

            PlanetStyleBonusSceneIndex = SceneIndex;
            PlanetStyleBonusScore = 0;
        }

        /// <summary>
        /// Snapshots the current game state as a checkpoint.
        /// Triggered when a powerup enemy or MotherShip is killed.
        /// </summary>
        public void SaveCheckpoint()
        {
            EnsurePlanetStyleBonusScene();

            HasCheckpoint = true;
            CheckpointScore = Score;
            CheckpointLives = Lives;
            CheckpointHealth = Health;
            CheckpointPowerUpsCollected = PowerUpsCollected;
            CheckpointSpeedPowerUpLevel = SpeedPowerUpLevel;
            CheckpointSeedersRemaining = SeedersRemaining;
            CheckpointDronesRemaining = DronesRemaining;
            CheckpointMotherShipsRemaining = MotherShipsRemaining;
            CheckpointTotalShotsFired = TotalShotsFired;
            CheckpointTotalKills = TotalKills;
            CheckpointTotalDeaths = TotalDeaths;
            CheckpointInfectionLevel = InfectionLevel;
            CheckpointWaveNumber = WaveNumber;
            CheckpointSceneIndex = SceneIndex;
            CheckpointSimulationRound = SimulationRound;
            CheckpointSceneBiome = CurrentSceneBiome;
            CheckpointInitialSeeders = InitialSeeders;
            CheckpointInitialDrones = InitialDrones;
            CheckpointInitialMotherShips = InitialMotherShips;
            CheckpointPlanetStyleBonusScore = PlanetStyleBonusScore;
            CheckpointPlanetStyleBonusSceneIndex = PlanetStyleBonusSceneIndex;
        }

        /// <summary>
        /// Restores game progress to the last checkpoint.
        /// TotalDeaths is NOT restored so the death count persists.
        /// Called automatically from <see cref="ConsumeLifeAndRespawn"/>.
        /// </summary>
        public void RestoreCheckpoint()
        {
            if (!HasCheckpoint) return;

            Score = CheckpointScore;
            Lives = CheckpointLives;
            PowerUpsCollected = CheckpointPowerUpsCollected;
            SpeedPowerUpLevel = CheckpointSpeedPowerUpLevel;
            SeedersRemaining = CheckpointSeedersRemaining;
            DronesRemaining = CheckpointDronesRemaining;
            MotherShipsRemaining = CheckpointMotherShipsRemaining;
            TotalShotsFired = CheckpointTotalShotsFired;
            TotalKills = CheckpointTotalKills;
            InfectionLevel = CheckpointInfectionLevel;
            WaveNumber = CheckpointWaveNumber;
            SceneIndex = CheckpointSceneIndex > 0 ? CheckpointSceneIndex : SceneIndex;
            SimulationRound = CheckpointSimulationRound;
            CurrentSceneBiome = CheckpointSceneBiome;
            PlanetStyleBonusScore = CheckpointPlanetStyleBonusScore;
            PlanetStyleBonusSceneIndex = CheckpointPlanetStyleBonusSceneIndex;
        }

        /// <summary>
        /// Calculates the final score including an accuracy bonus.
        /// Call at game end (victory or final game over) for highscore submission.
        /// </summary>
        public long CalculateFinalScore()
        {
            long accuracyBonus = (long)(Score * Accuracy * GameSetup.AccuracyBonusMultiplier);
            return Math.Max(0, Score + accuracyBonus);
        }

        public void UpdateAltitude(float height, float minHeight, float maxHeight)
        {
            if (maxHeight <= minHeight)
            {
                Alt = 0f;
                return;
            }

            float normalized = (height - minHeight) / (maxHeight - minHeight);
            Alt = MathF.Min(MathF.Max(normalized, 0f), 1f);
        }
    }
}
