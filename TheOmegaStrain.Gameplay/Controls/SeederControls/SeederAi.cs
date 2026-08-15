using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using GameAiAndControls.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CommonUtilities.GamePlayHelpers.GamePlayHelpers;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.SeederControls
{
    internal static class SeederAi
    {
        internal static bool enableLogging = false;

        // ============================
        // AI CONFIGURATION PARAMETERS
        // ============================
        // Movement cadence and speed:
        // - SecondsPerScreenCross: Time to traverse one full screen width globally (lower = faster global travel).
        // - LocalTileSpeedFactor: Local movement speed in tiles per second (higher = faster within-screen bio hunting).
        // - GlobalOffscreenSpeedFactor: Multiplier applied to global speed when offscreen (1.0 = same, <1 slower, >1 faster).
        private const float SecondsPerScreenCross = 4f;
        private const float LocalTileSpeedFactor = 0.50f; // moves half a tile per second locally
        private const float GlobalOffscreenSpeedFactor = 0.70f;

        // Step model modifiers:
        // - OffscreenStepFactor: Per-scene multiplier for offscreen seeder speed (tunable in IScene).
        // - MaxLocalSteps: Upper bound on steps allowed to reach a local target (caps how far local hunts can go).
        private static int OffscreenStepFactor => GameState.GamePlayState.SeederOffscreenSpeedFactor;
        private const int MaxLocalSteps = 300;

        // Targeting & infection cadence:
        // - SeedingStallSeconds: How long the seeder visibly pauses after infecting a tile (player can shoot it).
        // - ParticleBurstSeconds: Total duration of particle emission from stall start (covers stall + trail while moving).
        // - LocalRetargetSeconds: Short cooldown for non-seeding transitions (moving between targets).
        // - StallInfectSeconds: Interval between successive infections while stalling over a tile.
        private const float SeedingStallSeconds = 2.0f;
        private const float ParticleBurstSeconds = 8.0f;
        private const float LocalRetargetSeconds = 0.3f;
        private const double StallInfectSeconds = 0.20; // 5 infections per second when stalling

        // Global search heuristics:
        // - SmellRadiusScreens: Radius in screens to evaluate BioTileCount for global decisions.
        // - RoamTiles: Random roam distance in tiles when no good screen is found.
        private const int SmellRadiusScreens = 5;
        private const int RoamTiles = 10;
        private const float TargetArrivalDistance = 0.25f;
        private const double InitialGlobalDecisionDesyncSeconds = 0.45;
        private const double GlobalDecisionDesyncSeconds = 0.30;
        private const double LocalRetargetDesyncSeconds = 0.22;
        private const double StallDesyncSeconds = 0.35;

        // -------------------------
        // AI State (per object)
        // -------------------------
        private sealed class AiState
        {
            public bool IsSearchingGlobally = true;
            public bool IsHuntingLocally = false;

            public bool HasMovementTarget = false;
            public bool TargetIsLocalBio = false;
            public Vector3 TargetWorld;

            public int LocalPickCursor = 0;

            public long NextLocalRetargetTicks = 0;
            public long NextGlobalDecisionTicks = 0;

            public long LastMoveStamp = 0;          // Stopwatch ticks
            public long TargetStartTicks = 0;       // DateTime ticks (for dynamic timeout)

            public Vector3 AuthWorldPos;            // authoritative position for this AI object
            public bool AuthPosInitialized = false;

            // optional throttled logging
            public long LastHeartbeatTicks = 0;
            public long LastWaitLogTicks = 0;
            public long LastDecisionLogTicks = 0;

            public long NextStallParticleTicks = 0;
            public long NextStallInfectTick = 0;
            public int StallTileX = int.MinValue;
            public int StallTileZ = int.MinValue;
            public bool SeededAtCurrentStall = false;

            public bool IsStalling = false;
            public long StallUntilTicks = 0;
            public int StepsRemaining = 0;
            public long ParticleEmitUntilTicks = 0;
        }

        private static readonly Dictionary<int, AiState> _aiStates = new();

        private static AiState GetAiState(int objectId)
        {
            if (!_aiStates.TryGetValue(objectId, out var s))
            {
                s = new AiState();
                long nowTicks = DateTime.Now.Ticks;
                s.NextGlobalDecisionTicks = nowTicks + GetObjectOffsetTicks(objectId, InitialGlobalDecisionDesyncSeconds, salt: 17);
                s.NextLocalRetargetTicks = nowTicks + GetObjectOffsetTicks(objectId, LocalRetargetDesyncSeconds, salt: 29);
                _aiStates[objectId] = s;
            }
            return s;
        }

        // Call this when a Seeder dies/explodes OR when scene resets, to avoid stale states
        public static void RemoveAiState(int objectId)
        {
            _aiStates.Remove(objectId);
        }

        private static void SafeLog(string msg)
        {
            try
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log(msg);
            }
            catch { /* never crash AI because of logging */ }
        }

        private static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private static string V2(Vector3 v) => $"({(int)v.x},{(int)v.z})";

        private static long GetObjectOffsetTicks(int objectId, double maxSeconds, int salt)
        {
            if (maxSeconds <= 0)
                return 0;

            uint hash = unchecked((uint)((objectId * 1103515245) + (salt * 12345)));
            double normalized = (hash % 1000) / 1000.0;
            return (long)(normalized * maxSeconds * TimeSpan.TicksPerSecond);
        }

        // -------------------------
        // New AI movement (per object state)
        // -------------------------
        public static Vector3? MoveWorldPositionAccordingToAi(bool isOnScreen, I3dObject moveThisObject)
        {
            if (IsZeroWorldPos(moveThisObject))
                return (Vector3)moveThisObject.WorldPosition;

            if (moveThisObject == null)
                return null;

            if (moveThisObject.WorldPosition == null)
            {
                SafeLog($"AI:SKIP WorldPosition=null onScreen={isOnScreen} ObjectId:{moveThisObject.ObjectId}");
                return null;
            }

            var surfaceState = GameState.SurfaceState;
            var ecoMap = surfaceState?.ScreenEcoMetas;

            int id = moveThisObject.ObjectId;
            var s = GetAiState(id);

            if (ecoMap == null || surfaceState == null)
                return HoldPositionWhenMapMissing(s, moveThisObject);

            EnsureAuthPositionInitialized(s, isOnScreen, moveThisObject, id);

            Vector3 current = s.AuthWorldPos;

            // Sync the object's WorldPosition with the AI's tracked location so
            // helpers like GetSurfaceAlignedWorldPosition use the current position
            // rather than the stale value copied from the original at deep-copy time.
            moveThisObject.WorldPosition = new Vector3 { x = current.x, y = current.y, z = current.z };

            if (!TryGetDt(s, out float dt))
                return s.AuthWorldPos;

            long nowTicks = DateTime.Now.Ticks;

            ComputeStepModel(isOnScreen, s, out float localSpeed, out float speed, out float stepBaseline, out float stepOff, out float step);

            LogHeartbeatIfNeeded(isOnScreen, s, id, nowTicks, dt, stepBaseline, stepOff, OffscreenStepFactor, current);

            if (isOnScreen && nowTicks < s.ParticleEmitUntilTicks)
                moveThisObject.Movement.ReleaseParticles(moveThisObject);

            if (s.HasMovementTarget)
                return HandleMoveTowardTarget(isOnScreen, moveThisObject, s, id, nowTicks, dt, stepBaseline, step, OffscreenStepFactor, current);

            if (s.IsSearchingGlobally)
                return HandleGlobalSearch(isOnScreen, moveThisObject, s, id, nowTicks, ecoMap, current);

            if (s.IsHuntingLocally)
                return HandleLocalHunt(isOnScreen, moveThisObject, s, id, nowTicks, surfaceState, localSpeed, MaxLocalSteps, current);

            return HandleFallback(isOnScreen, s, id, nowTicks, current);
        }

        private static bool HandleStallSeeding(int id, AiState s, float dt, bool isOnScreen, I3dObject theObject)
        {
            var surfaceState = GameState.SurfaceState;
            if (surfaceState?.Global2DMap == null) return false;

            // AuthWorldPos may have been updated by HandleMoveTowardTarget (arrival snap)
            // since the top-of-AI sync, so refresh the object before visual helpers.
            theObject.WorldPosition = s.AuthWorldPos;

            // Tile indices use raw world coordinates to match the tile under the
            // seeder on the planet map.
            // Do NOT use GetSurfaceAlignedWorldPosition here — it adds visual
            // offsets (surfOO − seederOO) that shift the lookup by several tiles.
            int tileX = MapCoordinateHelpers.WorldXToTileIndex(s.AuthWorldPos.x, surfaceState.Global2DMap);
            int tileZ = MapCoordinateHelpers.WorldZToTileIndex(s.AuthWorldPos.z, surfaceState.Global2DMap);
            // Bounds
            if (tileZ < 0 || tileX < 0 ||
                tileZ >= surfaceState.Global2DMap.GetLength(0) ||
                tileX >= surfaceState.Global2DMap.GetLength(1))
                return false;

            long now = DateTime.Now.Ticks;

            // Infect work (keep your throttle so we don't spam writes/logs)
            if (now >= s.NextStallInfectTick)
            {
                s.NextStallInfectTick = now + (long)(StallInfectSeconds * TimeSpan.TicksPerSecond);

                bool tileChanged = (tileX != s.StallTileX) || (tileZ != s.StallTileZ);
                if (tileChanged)
                {
                    s.StallTileX = tileX;
                    s.StallTileZ = tileZ;
                    SafeLog($"AI:STALL_TILE onScreen={isOnScreen} tile=({tileX},{tileZ}) pos={V2(s.AuthWorldPos)} ObjectId:{id}");
                }

                int mapWidth = surfaceState.Global2DMap.GetLength(1);
                int mapHeight = surfaceState.Global2DMap.GetLength(0);
                int tilesPerScreen = SurfaceSetup.viewPortSize;
                int tileSz = SurfaceSetup.tileSize;

                // Shared helper: infect a single tile if valid, bio-capable, and not already infected.
                // Also removes the tile from the BioTiles list so seeders move past it.
                bool TryInfectTile(int tx, int tz, string label)
                {
                    tx = MapCoordinateHelpers.WrapIndex(tx, mapWidth);
                    tz = MapCoordinateHelpers.WrapIndex(tz, mapHeight);
                    var t = surfaceState.Global2DMap[tz, tx];
                    if (!SeederMovementHelpers.IsBioTerrain(t))
                    {
                        SeederMovementHelpers.ClearInfectionIfTerrainCannotHostIt(surfaceState, tz, tx);
                        return false;
                    }

                    if (t.isInfected) return false;

                    GameState.GamePlayState.InfectionLevel += 1;
                    t.isInfected = true;
                    surfaceState.Global2DMap[tz, tx] = t;
                    surfaceState.DirtyTiles.Add(new Vector3 { x = tx, y = 0, z = tz });
                    var cnt = SeederMovementHelpers.DecrementBioCountForTile(surfaceState, tz, tx);
                    s.SeededAtCurrentStall = true;

                    // Remove from the screen's BioTiles list so seeders skip this tile
                    int scrY = tz / tilesPerScreen;
                    int scrX = tx / tilesPerScreen;
                    var eco = surfaceState.ScreenEcoMetas;
                    if ((uint)scrY < (uint)eco.GetLength(0) && (uint)scrX < (uint)eco.GetLength(1))
                    {
                        var bioList = eco[scrY, scrX].BioTiles;
                        int worldX = tx * tileSz;
                        int worldZ = tz * tileSz;
                        for (int bi = bioList.Count - 1; bi >= 0; bi--)
                        {
                            if (bioList[bi].X == worldX && bioList[bi].Y == worldZ)
                            { bioList.RemoveAt(bi); break; }
                        }
                    }

                    SafeLog($"AI:{label} tile=({tx},{tz}) onScreen={isOnScreen} RemainingBioTileCount:{cnt} ObjectId:{id}");

                    // Queue for delayed local spread — cascading infection from this tile
                    surfaceState.PendingLocalInfectionSpread.Add((tx, tz, DateTime.Now.Ticks));

                    return true;
                }

                // Ring 1: immediate neighbors (8 tiles)
                int[][] ring1Cardinals = [[0, -1], [1, 0], [0, 1], [-1, 0]];
                int[][] ring1Diagonals = [[-1, -1], [1, -1], [1, 1], [-1, 1]];
                // Ring 2: distance-2 tiles (16 tiles)
                int[][] ring2 = [
                    [-2, -2], [-1, -2], [0, -2], [1, -2], [2, -2],
                    [2, -1], [2, 0], [2, 1],
                    [2, 2], [1, 2], [0, 2], [-1, 2], [-2, 2],
                    [-2, 1], [-2, 0], [-2, -1]
                ];
                // Ring 3: distance-3 tiles (24 tiles)
                int[][] ring3 = [
                    [-3, -3], [-2, -3], [-1, -3], [0, -3], [1, -3], [2, -3], [3, -3],
                    [3, -2], [3, -1], [3, 0], [3, 1], [3, 2],
                    [3, 3], [2, 3], [1, 3], [0, 3], [-1, 3], [-2, 3], [-3, 3],
                    [-3, 2], [-3, 1], [-3, 0], [-3, -1], [-3, -2]
                ];

                var tile = surfaceState.Global2DMap[tileZ, tileX];
                if (!SeederMovementHelpers.IsBioTerrain(tile))
                {
                    SeederMovementHelpers.ClearInfectionIfTerrainCannotHostIt(surfaceState, tileZ, tileX);
                }
                else if (tile.isInfected)
                {
                    SafeLog($"AI:STALL_ALREADY_INFECTED tile=({tileX},{tileZ}) onScreen={isOnScreen} ObjectId:{id}");
                    return true;
                }
                {
                    if (TryInfectTile(tileX, tileZ, "INFECT"))
                    {
                        // Spread to additional edge tiles based on InfectionSpreadRate
                        int extraSpread = GameState.GamePlayState.InfectionSpreadRate - 1;
                        if (extraSpread > 0)
                        {
                            int spread = 0;
                            // Ring 1: cardinals then diagonals
                            foreach (var d in ring1Cardinals)
                            {
                                if (spread >= extraSpread) break;
                                if (TryInfectTile(tileX + d[0], tileZ + d[1], "INFECT_SPREAD"))
                                    spread++;
                            }
                            foreach (var d in ring1Diagonals)
                            {
                                if (spread >= extraSpread) break;
                                if (TryInfectTile(tileX + d[0], tileZ + d[1], "INFECT_SPREAD"))
                                    spread++;
                            }
                            // Ring 2: distance-2 tiles for higher rates
                            foreach (var d in ring2)
                            {
                                if (spread >= extraSpread) break;
                                if (TryInfectTile(tileX + d[0], tileZ + d[1], "INFECT_SPREAD"))
                                    spread++;
                            }
                            // Ring 3: distance-3 tiles for rates > 24
                            foreach (var d in ring3)
                            {
                                if (spread >= extraSpread) break;
                                if (TryInfectTile(tileX + d[0], tileZ + d[1], "INFECT_SPREAD"))
                                    spread++;
                            }
                        }
                    }
                    else
                    {
                        // Primary tile not bio-capable: try infecting a neighbor instead
                        bool infectedAny = false;
                        foreach (var d in ring1Cardinals)
                        {
                            if (TryInfectTile(tileX + d[0], tileZ + d[1], "INFECT_NEIGHBOR"))
                            { infectedAny = true; break; }
                        }
                        if (!infectedAny)
                        {
                            foreach (var d in ring1Diagonals)
                            {
                                if (TryInfectTile(tileX + d[0], tileZ + d[1], "INFECT_NEIGHBOR"))
                                { infectedAny = true; break; }
                            }
                        }
                        if (!infectedAny)
                        {
                            SafeLog($"AI:INFECT_SURROUNDINGS_NONE onScreen={isOnScreen} center=({tileX},{tileZ}) ObjectId:{id}");
                        }
                    }
                }
            }
            return false;
        }
        private static bool IsZeroWorldPos(I3dObject obj)
        {
            // Same special-case as you had
            return obj.WorldPosition.x == 0 && obj.WorldPosition.y == 0 && obj.WorldPosition.z == 0;
        }

        private static Vector3 HoldPositionWhenMapMissing(AiState s, I3dObject moveThisObject)
        {
            if (!s.AuthPosInitialized)
            {
                s.AuthWorldPos = (Vector3)moveThisObject.WorldPosition;
                s.AuthPosInitialized = true;
            }
            return s.AuthWorldPos;
        }

        private static void EnsureAuthPositionInitialized(AiState s, bool isOnScreen, I3dObject moveThisObject, int id)
        {
            if (!s.AuthPosInitialized)
            {
                s.AuthWorldPos = (Vector3)moveThisObject.WorldPosition;
                s.AuthPosInitialized = true;
                SafeLog($"AI:POS_INIT onScreen={isOnScreen} pos={V2(s.AuthWorldPos)} ObjectId:{id}");
            }
        }

        private static bool TryGetDt(AiState s, out float dt)
        {
            long stamp = Stopwatch.GetTimestamp();
            if (s.LastMoveStamp == 0)
            {
                s.LastMoveStamp = stamp;
                dt = 0;
                return false; // caller returns AuthWorldPos (same as before)
            }

            dt = (stamp - s.LastMoveStamp) / (float)Stopwatch.Frequency;
            s.LastMoveStamp = stamp;

            if (dt > 1.0f) dt = 1.0f;
            if (dt < 1f / 240f) dt = 1f / 240f;

            return true;
        }

        private static void ComputeStepModel(
            bool isOnScreen,
            AiState s,
            out float localSpeed,
            out float speed,
            out float stepBaseline,
            out float stepOff,
            out float step)
        {
            float screenWorldWidth = SurfaceSetup.viewPortSize * SurfaceSetup.tileSize;

            // Global movement speed in world units per second
            float globalSpeed = screenWorldWidth / SecondsPerScreenCross;
            // Local movement speed in world units per second (tileSize * factor)
            localSpeed = SurfaceSetup.tileSize * LocalTileSpeedFactor;

            // Choose speed based on target type
            speed = s.TargetIsLocalBio ? localSpeed : globalSpeed;

            // Adjust global speed when offscreen
            if (!s.TargetIsLocalBio && GlobalOffscreenSpeedFactor != 1.0f)
                speed *= GlobalOffscreenSpeedFactor;

            // Base step at the gameplay baseline FPS.
            stepBaseline = speed / GameState.GameplayBaselineFps;
            // Offscreen acceleration
            stepOff = stepBaseline * OffscreenStepFactor;
            // Final step depends on onscreen/offscreen (same formula, but uses above factors)
            step = isOnScreen ? stepBaseline : stepOff;
        }

        private static void LogHeartbeatIfNeeded(
            bool isOnScreen,
            AiState s,
            int id,
            long nowTicks,
            float dt,
            float stepBaseline,
            float stepOff,
            int offscreenStepFactor,
            Vector3 current)
        {
            if (nowTicks - s.LastHeartbeatTicks < TimeSpan.TicksPerSecond)
                return;

            s.LastHeartbeatTicks = nowTicks;

            string stateName = s.IsSearchingGlobally ? "GLOBAL" : (s.IsHuntingLocally ? "LOCAL" : "NONE");
            string targetStr = s.HasMovementTarget ? V2(s.TargetWorld) : "(none)";

            string gNext = s.NextGlobalDecisionTicks == 0 ? "n/a"
                : $"{(s.NextGlobalDecisionTicks - nowTicks) / (double)TimeSpan.TicksPerSecond:0.00}s";
            string lNext = s.NextLocalRetargetTicks == 0 ? "n/a"
                : $"{(s.NextLocalRetargetTicks - nowTicks) / (double)TimeSpan.TicksPerSecond:0.00}s";

            string stepsStr = s.StepsRemaining > 0 ? s.StepsRemaining.ToString() : "0";

            SafeLog(
                $"AI:HB onScreen={isOnScreen} state={stateName} " +
                $"hasTarget={s.HasMovementTarget} localTarget={s.TargetIsLocalBio} " +
                $"dt={dt:0.0000} step90={stepBaseline:0.00} stepOff={stepOff:0.00} factor={offscreenStepFactor} " +
                $"pos={V2(current)} target={targetStr} stepsRemaining={stepsStr} " +
                $"cooldowns globalNext={gNext} localNext={lNext} ObjectId:{id}"
            );
        }

        private static Vector3 HandleMoveTowardTarget(
            bool isOnScreen,
            I3dObject moveThisObject,
            AiState s,
            int id,
            long nowTicks,
            float dt,
            float stepBaseline,
            float step,
            int offscreenStepFactor,
            Vector3 current)
        {
            // Initialize steps on first move toward this target
            if (s.StepsRemaining <= 0)
            {
                float dist = DistanceXZ(current, s.TargetWorld);
                int stepsNeeded = (int)Math.Ceiling(dist / Math.Max(0.0001f, stepBaseline));
                if (stepsNeeded < 1) stepsNeeded = 1;

                s.StepsRemaining = stepsNeeded;

                SafeLog(
                    $"AI:STEPS_INIT onScreen={isOnScreen} localTarget={s.TargetIsLocalBio} " +
                    $"dist={dist:0.0} step90={stepBaseline:0.00} stepsNeeded={stepsNeeded} " +
                    $"pos={V2(current)} target={V2(s.TargetWorld)} ObjectId:{id}"
                );
            }

            // Frame-rate compensation: scale step and decrement by dt*baseline FPS
            // so the seeder covers the same distance per second at any FPS.
            float dtScale = dt * GameState.GameplayBaselineFps;
            float actualStep = step * dtScale;

            Vector3 next = SeederMovementHelpers.StepTowardTargetWorldXZ(current, s.TargetWorld, actualStep);

            int baseDec = isOnScreen ? 1 : offscreenStepFactor;
            int dec = Math.Max(1, (int)Math.Round(baseDec * dtScale));
            s.StepsRemaining -= dec;

            bool reachedTarget = DistanceXZ(next, s.TargetWorld) <= TargetArrivalDistance;
            if (!reachedTarget && s.StepsRemaining <= 0)
                s.StepsRemaining = 1;

            SafeLog(
                $"AI:MOVE({(isOnScreen ? "onscreen" : "offscreen")}) " +
                $"localTarget={s.TargetIsLocalBio} step={actualStep:0.00} dec={dec} stepsLeft={s.StepsRemaining} " +
                $"cur={V2(current)} next={V2(next)} target={V2(s.TargetWorld)} ObjectId:{id}"
            );

            s.AuthWorldPos = next;

            // ARRIVAL: switch modes only after movement actually reaches the target.
            if (reachedTarget)
            {
                s.AuthWorldPos = s.TargetWorld; // snap exactly
                next = s.AuthWorldPos;

                if (s.TargetIsLocalBio)
                    SafeLog($"AI:REACHED_LOCAL_TARGET onScreen={isOnScreen} pos={V2(next)} ObjectId:{id}");
                else
                    SafeLog($"AI:REACHED_SCREEN_CENTER onScreen={isOnScreen} pos={V2(next)} ObjectId:{id}");

                s.HasMovementTarget = false;
                s.StepsRemaining = 0;

                if (!s.TargetIsLocalBio)
                {
                    s.IsSearchingGlobally = false;
                    s.IsHuntingLocally = true;
                    s.NextLocalRetargetTicks = nowTicks;
                    SafeLog($"AI:MODE_SWITCH GLOBAL->LOCAL onScreen={isOnScreen} ObjectId:{id}");
                }
                else
                {
                    s.SeededAtCurrentStall = false;
                    s.NextLocalRetargetTicks = nowTicks +
                                               (long)(SeedingStallSeconds * TimeSpan.TicksPerSecond) +
                                               GetObjectOffsetTicks(id, StallDesyncSeconds, salt: 67);
                    s.ParticleEmitUntilTicks = nowTicks + (long)(ParticleBurstSeconds * TimeSpan.TicksPerSecond);
                    SafeLog($"AI:LOCAL_COOLDOWN set={SeedingStallSeconds:0.00}s particles={ParticleBurstSeconds:0.00}s onScreen={isOnScreen} ObjectId:{id}");

                    // Do a stall tick straight away (same as before)
                    bool retargetImmediately = HandleStallSeeding(id, s, dt, isOnScreen, moveThisObject);
                    if (retargetImmediately || !s.SeededAtCurrentStall)
                    {
                        s.NextLocalRetargetTicks = nowTicks;
                        s.ParticleEmitUntilTicks = nowTicks;
                        SafeLog($"AI:LOCAL_COOLDOWN skipped, target no longer seedable onScreen={isOnScreen} ObjectId:{id}");
                    }
                }
            }

            return s.AuthWorldPos;
        }

        private static Vector3 HandleGlobalSearch(
            bool isOnScreen,
            I3dObject moveThisObject,
            AiState s,
            int id,
            long nowTicks,
            ScreenEcoMeta[,] ecoMap,
            Vector3 current)
        {
            if (nowTicks < s.NextGlobalDecisionTicks)
            {
                if (nowTicks - s.LastWaitLogTicks > (TimeSpan.TicksPerSecond / 2))
                {
                    s.LastWaitLogTicks = nowTicks;
                    SafeLog($"AI:GLOBAL_WAIT onScreen={isOnScreen} cooldown={(s.NextGlobalDecisionTicks - nowTicks) / (double)TimeSpan.TicksPerSecond:0.00}s pos={V2(current)} ObjectId:{id}");
                }
                return s.AuthWorldPos;
            }

            s.NextGlobalDecisionTicks = nowTicks +
                                        (TimeSpan.TicksPerSecond / 2) +
                                        GetObjectOffsetTicks(id, GlobalDecisionDesyncSeconds, salt: 41);

            // Screen indices use raw world coordinates to match the meta map.
            SeederMovementHelpers.GetScreenIndexFromWorldXZ(current, out int curSY, out int curSX);

            if (nowTicks - s.LastDecisionLogTicks > (TimeSpan.TicksPerSecond / 2))
            {
                s.LastDecisionLogTicks = nowTicks;
                SafeLog($"AI:GLOBAL_DECIDE onScreen={isOnScreen} from=[{curSY},{curSX}] smellRadius={SmellRadiusScreens} roamTiles={RoamTiles} ObjectId:{id}");
            }

            if (SeederMovementHelpers.TryFindBestScreenInRadius(ecoMap, curSY, curSX, SmellRadiusScreens, out int bestSY, out int bestSX))
            {
                int stepY = bestSY == curSY ? 0 : (bestSY > curSY ? 1 : -1);
                int stepX = bestSX == curSX ? 0 : (bestSX > curSX ? 1 : -1);

                int nextSY = curSY + stepY;
                int nextSX = curSX + stepX;

                if (nextSY < 0) nextSY = 0;
                if (nextSX < 0) nextSX = 0;
                if (nextSY >= ecoMap.GetLength(0)) nextSY = ecoMap.GetLength(0) - 1;
                if (nextSX >= ecoMap.GetLength(1)) nextSX = ecoMap.GetLength(1) - 1;

                s.TargetWorld = SeederMovementHelpers.GetScreenCenterWorldXZ(nextSY, nextSX, current.y);
                s.HasMovementTarget = true;
                s.TargetIsLocalBio = false;
                s.TargetStartTicks = nowTicks;
                s.StepsRemaining = 0;

                SafeLog($"AI:GLOBAL_STEP onScreen={isOnScreen} from=[{curSY},{curSX}] best=[{bestSY},{bestSX}] next=[{nextSY},{nextSX}] smell={ecoMap[bestSY, bestSX].BioTileCount} target={V2(s.TargetWorld)} ObjectId:{id}");
                return s.AuthWorldPos;
            }

            s.TargetWorld = SeederMovementHelpers.GetRandomRoamTargetWorldXZ(current, RoamTiles);
            s.HasMovementTarget = true;
            s.TargetIsLocalBio = false;
            s.TargetStartTicks = nowTicks;
            s.StepsRemaining = 0;

            SafeLog($"AI:GLOBAL_ROAM onScreen={isOnScreen} target={V2(s.TargetWorld)} ObjectId:{id}");
            return s.AuthWorldPos;
        }

        private static Vector3 HandleLocalHunt(
            bool isOnScreen,
            I3dObject moveThisObject,
            AiState s,
            int id,
            long nowTicks,
            SurfaceState surfaceState,
            float localSpeed,
            int maxLocalSteps,
            Vector3 current)
        {
            // During local cooldown, we STALL
            if (nowTicks < s.NextLocalRetargetTicks)
            {
                if (nowTicks - s.LastWaitLogTicks > (TimeSpan.TicksPerSecond / 2))
                {
                    s.LastWaitLogTicks = nowTicks;
                    SafeLog($"AI:LOCAL_WAIT onScreen={isOnScreen} cooldown={(s.NextLocalRetargetTicks - nowTicks) / (double)TimeSpan.TicksPerSecond:0.00}s pos={V2(current)} cursor={s.LocalPickCursor} ObjectId:{id}");
                }

                if (!s.HasMovementTarget && s.TargetIsLocalBio)
                {
                    bool retargetImmediately = HandleStallSeeding(id, s, 0f, isOnScreen, moveThisObject);
                    if ((retargetImmediately || !s.SeededAtCurrentStall) && !s.HasMovementTarget && s.TargetIsLocalBio)
                    {
                        s.NextLocalRetargetTicks = nowTicks;
                        SafeLog($"AI:STALL_SKIP_COOLDOWN no valid seeding this stall, retargeting immediately ObjectId:{id}");
                    }
                }

                return s.AuthWorldPos;
            }

            s.NextLocalRetargetTicks = nowTicks +
                                       (long)(LocalRetargetSeconds * TimeSpan.TicksPerSecond) +
                                       GetObjectOffsetTicks(id, LocalRetargetDesyncSeconds, salt: 53);

            TerrainType GetTerrainTypeFromSurfaceData(SurfaceData sd) =>
                GamePlayHelpers.GetTerrainType(sd.mapDepth, MapSetup.maxHeight);

            SafeLog($"AI:LOCAL_PICK onScreen={isOnScreen} cursor={s.LocalPickCursor} attempts=32 validateMap=true ObjectId:{id}");

            int cursor = s.LocalPickCursor;
            if (SeederMovementHelpers.TryPickLocalBioTargetWorldXZ(
                surfaceState,
                moveThisObject,
                GetTerrainTypeFromSurfaceData,
                out Vector3 localTarget,
                ref cursor,
                maxAttemptsPerCall: 32,
                validateAgainstMap: true))
            {
                s.LocalPickCursor = cursor;

                var candidate = new Vector3 { x = localTarget.x, y = current.y, z = localTarget.z };

                float localStepBaseline = localSpeed / GameState.GameplayBaselineFps;
                float effectiveStep = isOnScreen ? localStepBaseline : localStepBaseline * OffscreenStepFactor;
                float dist = DistanceXZ(current, candidate);
                int stepsNeeded = (int)Math.Ceiling(dist / Math.Max(0.0001f, effectiveStep));
                if (stepsNeeded < 1) stepsNeeded = 1;

                if (stepsNeeded > maxLocalSteps)
                {
                    SafeLog(
                        $"AI:LOCAL_TARGET_REJECT tooFar stepsNeeded={stepsNeeded} max={maxLocalSteps} dist={dist:0.0} " +
                        $"pos={V2(current)} cand={V2(candidate)} cursor={s.LocalPickCursor} ObjectId:{id}"
                    );

                    s.NextLocalRetargetTicks = nowTicks;
                    s.HasMovementTarget = false;
                    s.TargetIsLocalBio = true;
                    return s.AuthWorldPos;
                }

                s.TargetWorld = candidate;
                s.HasMovementTarget = true;
                s.TargetIsLocalBio = true;
                s.TargetStartTicks = nowTicks;
                s.StepsRemaining = 0;

                SafeLog($"AI:LOCAL_TARGET onScreen={isOnScreen} target={V2(s.TargetWorld)} stepsNeeded={stepsNeeded} cursor={s.LocalPickCursor} ObjectId:{id}");
                return s.AuthWorldPos;
            }

            s.LocalPickCursor = cursor;

            s.IsHuntingLocally = false;
            s.IsSearchingGlobally = true;
            s.HasMovementTarget = false;
            s.TargetIsLocalBio = false;
            s.NextGlobalDecisionTicks = nowTicks;
            s.StepsRemaining = 0;

            SafeLog($"AI:LOCAL_DEPLETED -> GLOBAL_SEARCH onScreen={isOnScreen} cursor={s.LocalPickCursor} ObjectId:{id}");
            return s.AuthWorldPos;
        }

        private static Vector3 HandleFallback(bool isOnScreen, AiState s, int id, long nowTicks, Vector3 current)
        {
            SafeLog($"AI:FALLBACK resetState onScreen={isOnScreen} pos={V2(current)} ObjectId:{id}");
            s.IsSearchingGlobally = true;
            s.IsHuntingLocally = false;
            s.HasMovementTarget = false;
            s.TargetIsLocalBio = false;
            s.NextGlobalDecisionTicks = nowTicks;
            s.StepsRemaining = 0;
            return s.AuthWorldPos;
        }

    }
}
