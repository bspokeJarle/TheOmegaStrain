using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Reflection;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.MotherShipMediumControls
{
    internal static class MotherShipLargeAi
    {
        // ============================
        // AI CONFIGURATION PARAMETERS
        // ============================
        internal const float GroundHoldSeconds       = 10f;
        internal const float AltitudeHoldSeconds     = 5f;
        internal const float AltitudeRise             = 300f;   // units above ground (negative delta)
        internal const float AscentSpeedUps           = 220f;   // ObjectOffsets delta units per second
        internal const float DescentSpeedUps          = 160f;
        internal const float TiltBackXTarget          = 110f;   // X rotation target when tipped back at altitude

        // Drone spawn config
        internal const int DroneSpawnBase          = 2;
        internal const int DroneSpawnMax            = 5;
        internal const int OpeningsPerExtraDrone    = 3;

        // ============================
        // AI STATE
        // ============================
        internal enum AltitudeCycleState { GroundHold, Ascending, AltitudeHold, WaitForHatchClose, Descending }
        internal enum HatchState { Closed, Opening, Open, Closing }

        internal sealed class CycleState
        {
            public AltitudeCycleState AltCycleState  = AltitudeCycleState.GroundHold;
            public float AltitudeDelta               = 0f;   // 0 = at ground, negative = above ground
            public float AltitudeHoldDelta           = 0f;   // target delta, captured when ascent starts
            public float CycleTimer                  = 0f;
            public HatchState HatchState             = HatchState.Closed;
            public float HatchAngle                  = 0f;
            public int   HatchOpenCount              = 0;
            public int   DronesThisOpening           = DroneSpawnBase;
        }

        // ============================
        // ALTITUDE CYCLE
        // ============================
        internal static void UpdateAltitudeCycle(
            CycleState     state,
            I3dObject      theObject,
            float          deltaSeconds,
            IAudioPlayer?  audio,
            SoundDefinition? imminentSound)
        {
            float aggression = MotherShipDifficultySetup.GetAggression(GameState.GamePlayState.MotherShipLargeAggression);

            switch (state.AltCycleState)
            {
                case AltitudeCycleState.GroundHold:
                    state.AltitudeDelta = 0f;
                    state.CycleTimer += deltaSeconds * aggression;
                    if (state.CycleTimer >= GroundHoldSeconds)
                    {
                        state.CycleTimer      = 0f;
                        state.AltitudeHoldDelta = -AltitudeRise;
                        state.AltCycleState   = AltitudeCycleState.Ascending;
                    }
                    break;

                case AltitudeCycleState.Ascending:
                    // Rise until target altitude delta is reached; tilt-back runs in parallel.
                    state.AltitudeDelta -= MotherShipDifficultySetup.ScaleTravelSpeed(AscentSpeedUps, aggression) * deltaSeconds;
                    if (state.AltitudeDelta <= state.AltitudeHoldDelta)
                    {
                        state.AltitudeDelta  = state.AltitudeHoldDelta;
                        state.AltCycleState  = AltitudeCycleState.AltitudeHold;
                        state.CycleTimer     = 0f;
                        // Open hatch immediately when altitude is reached — tilt animates in parallel.
                        state.HatchState        = HatchState.Opening;
                        state.HatchOpenCount++;
                        state.DronesThisOpening = Math.Min(DroneSpawnMax,
                            DroneSpawnBase + (state.HatchOpenCount - 1) / OpeningsPerExtraDrone);
                        SpawnDrones(state, theObject);
                        if (audio != null && imminentSound != null)
                            audio.Play(imminentSound, AudioPlayMode.OneShot,
                                new AudioPlayOptions { WorldPosition = System.Numerics.Vector3.Zero });
                    }
                    break;

                case AltitudeCycleState.AltitudeHold:
                    state.AltitudeDelta = state.AltitudeHoldDelta;

                    state.CycleTimer += deltaSeconds * aggression;
                    if (state.CycleTimer >= AltitudeHoldSeconds)
                    {
                        state.CycleTimer    = 0f;
                        state.HatchState    = HatchState.Closing;
                        state.AltCycleState = AltitudeCycleState.WaitForHatchClose;
                    }
                    break;

                case AltitudeCycleState.WaitForHatchClose:
                    state.AltitudeDelta = state.AltitudeHoldDelta;
                    if (state.HatchState == HatchState.Closed)
                        state.AltCycleState = AltitudeCycleState.Descending;
                    break;

                case AltitudeCycleState.Descending:
                    state.AltitudeDelta += MotherShipDifficultySetup.ScaleTravelSpeed(DescentSpeedUps, aggression) * deltaSeconds;
                    if (state.AltitudeDelta >= 0f)
                    {
                        state.AltitudeDelta  = 0f;
                        state.HatchAngle     = 0f;
                        state.AltCycleState  = AltitudeCycleState.GroundHold;
                        state.CycleTimer     = 0f;
                    }
                    break;
            }
        }

        // ============================
        // DRONE SPAWNING
        // ============================
        private static void SpawnDrones(CycleState state, I3dObject theObject)
        {
            if (theObject.ParentSurface == null) return;

            int count = state.DronesThisOpening;
            // ObjectOffsets.y at this point is terrain-synced only; AltitudeDelta is applied
            // AFTER UpdateAltitudeCycle returns in MoveObject. Add it here to get true screen position.
            float mothershipOffsetY = (theObject.ObjectOffsets?.y ?? 0f) + state.AltitudeHoldDelta;

            for (int i = 0; i < count; i++)
            {
                var drone = CreateKamikazeDroneObject(theObject.ParentSurface);
                if (drone == null) continue;

                // Spread drones horizontally (x) so they don't stack on top of each other.
                float spreadX = (i - (count - 1) * 0.5f) * 80f;
                drone.WorldPosition = new Vector3
                {
                    x = theObject.WorldPosition?.x ?? 0f,
                    y = 0f,
                    z = theObject.WorldPosition?.z ?? 0f
                };
                // Start just below the hatch (slightly more positive y = lower on screen).
                drone.ObjectOffsets = new Vector3
                {
                    x = spreadX,
                    y = mothershipOffsetY + 40f,
                    z = 400
                };
                drone.Rotation          = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 90 };
                drone.ObjectName        = "KamikazeDrone";
                drone.ImpactStatus      = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth };
                drone.CrashBoxDebugMode = false;
                // Stagger each drone by 0.3 s so they release one by one from the hatch.
                float staggerDelay = i * 0.3f;
                drone.Movement = new GameAiAndControls.Controls.KamikazeDroneControls.HatchDroppedDroneControls(staggerDelay);
                drone.WeaponSystems     = null;
                drone.HasPowerUp        = false;
                drone.IsActive          = true;

                GameState.SurfaceState.AiObjects.Add(drone);
                GameState.PendingWorldObjects.Add(drone);
            }
        }

        private static _3dObject? CreateKamikazeDroneObject(ISurface parentSurface)
        {
            const string typeName = "_3dRotations.World.Objects.KamikazeDrone";
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                if (type == null) continue;
                var method = type.GetMethod("CreateKamikazeDrone",
                    BindingFlags.Public | BindingFlags.Static);
                if (method?.Invoke(null, new object[] { parentSurface }) is _3dObject drone)
                    return drone;
            }
            return null;
        }
    }
}
