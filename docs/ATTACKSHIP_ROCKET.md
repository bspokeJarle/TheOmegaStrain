# AttackShip and Rocket

The enemy uses the same controller/weapon/rendering split as the other Omega
objects. This document describes the integrated weapon, not the original
fixed-direction workshop exercise.

## Where to read the code

| Responsibility | File |
| --- | --- |
| Hull, scale, crash boxes and hidden muzzle/exhaust guides | `TheOmegaStrain.Game/World/Objects/AttackShip.cs`, `Rocket.cs` |
| Pursuit, firing distance and per-enemy reload timer | `TheOmegaStrain.Gameplay/Controls/AttackShipControls.cs` |
| Launch, guidance, motor, physics fall and explosion | `TheOmegaStrain.Gameplay/Controls/Weapons.Rocket.cs` |
| Exhaust emission using already-rotated weapon guides | `TheOmegaStrain.Gameplay/Controls/RocketControls.cs` |
| One launch's state | `TheOmegaStrain.Domain/Implementations/Combat/ActiveWeapon.cs`, `RocketFlightState.cs` |
| World/local targeting and minimap alignment | `TheOmegaStrain.Common/OmegaEngineAdapters/SurfacePositionSyncHelpers.cs` |
| Collision ownership and surface checks | `TheOmegaStrain.Runtime/CrashDetection/CrashDetection.Main.cs` |
| Collecting weapon and particle meshes | `TheOmegaStrain.Runtime/Rendering/WeaponsManager.cs` |
| Rocket render admission | `TheOmegaStrain.Game/Projection/OmegaPerspectiveProjectorFactory.cs` |

## Flight and cleanup

1. AttackShip approaches a horizontal firing ring around Ship and matches its
   height. The render/AI system supplies `IsOnScreen`; the controller never
   forces visibility. Launch also requires range, bound guides, no active rocket
   and an elapsed reload delay.
2. `LaunchRocket` copies the geometry template and creates separate physics,
   exhaust and flight state. A failed launch does not consume a reload.
3. An enemy rocket updates its aim until it comes within 100 units of Ship.
   It then locks permanently, so Ship can escape the final approach. Player
   rockets do not use enemy guidance.
4. The motor runs for three seconds of clamped movement time. A frame crossing
   that boundary is split between powered flight and falling. New motor
   particles stop; existing particles finish naturally.
5. At motor stop, velocity is transferred once to the regular `IPhysics` instance.
   `ApplyForces` then provides gravity with steps no longer than the 90 FPS
   baseline. Momentum is retained; there is no second custom falling simulation.
6. Collision starts the normal explosion. A spent rocket outside the buffered
   screen bounds also explodes for cleanup. Range and render depth do not stop
   its motor. Surface contact is checked every frame, including tiles far from
   the surface object's centre.
7. The exploding rocket leaves `ActiveWeapons`, allowing the owner's reload
   timer to start. Its debris and particles remain in `_explodingRockets` until
   the effect finishes. World/scene teardown follows the existing owner cleanup.

## Tuning

Keep balance values in the existing setup classes; scale belongs to the factories.

| Setting | Current value | Location |
| --- | --- | --- |
| AttackShip health / ram damage | 155 / 50 | `EnemySetup` |
| Firing-ring radius | 500 × screen X scale | `EnemySetup.AttackShipFiringDistance` |
| Reload after previous rocket removal | 10 seconds by default | `EnemySetup.AttackShipRocketReloadDelaySeconds`; override `AttackShipControls.RocketReloadDelaySeconds` per instance |
| Maximum distance at launch | 1500 units | `WeaponSetup.RocketMaxRange` (not flight range) |
| Powered speed | 750 units/second | `WeaponSetup.RocketVelocity` |
| Final-approach lock distance | 100 units | `WeaponSetup.RocketGuidanceLockDistance` |
| Fuel duration | 3 seconds | `WeaponSetup.RocketFuelSeconds` |
| Falling gravity | 300 units/second², positive Y downward | `WeaponSetup.RocketGravityStrength` |
| Rocket damage | 105 | `WeaponSetup.RocketDamage` |
| AttackShip / Rocket uniform scale | 1.5 / 3.5 | Respective factory `ZoomRatio` constants |

## Coordinate and frame-copy rules

- The controller and weapon system are shared between render-frame copies.
  Persistent enemy state belongs in `AttackShipControls`; launch state belongs
  in `ActiveWeapon`/`RocketFlightState`, never in the geometry template.
- Rendered X/Y use `world - map + offset`; rendered Z uses
  `map - world + offset`. Use the shared conversion helpers instead of manually
  subtracting screen-centre coordinates from Ship's world position.
- Minimap markers use the rotated collision centre and offsets relative to Ship,
  consistently for all object names.
- A flying rocket keeps its launch `WorldPosition` and moves through
  `ObjectOffsets`. Its launch anchor can leave the world visibility radius while
  the rocket is still on screen. The Omega projector therefore leaves rockets
  and their particles to the normal screen/depth filters. Do not reinstate
  launch-anchor distance culling or change their coordinates to solve visibility.
- Surface-pitch correction belongs to the main enemy object. Muzzle positions
  already include its offsets; do not independently correct projectiles or guides.

## Pure workshop helpers versus runtime

`RocketFireHelpers.CanFire` retains the original exercise's minimum ten-second
cooldown measured from a successful launch. The integrated controller uses
`CanFireAfterReload` with a configurable delay measured from removal instead.

`RocketFlightHelpers.CalculateLaunchSolution` and `HasFuel` are reused by the
live weapon. `CalculateStep` remains a pure fixed-direction exercise: it is not
called in addition to runtime physics. Its returned vertical velocity is the
gravity contribution beyond the launch direction's vertical component.
`ShouldEmitMotorParticles` reports whether any powered time occurred in that
step, including a step that crosses the fuel boundary.

`RocketLifecycleHelpers.Classify` remains a side-effect-free exercise with
optional range/lifetime limits. Runtime intentionally uses collision and spent
off-screen cleanup instead of expiring a rocket silently at a fixed age/range.

## Verification

`AttackShipWeaponTests` (split across weapon, lifecycle and projection test files)
covers launch/reload isolation, guidance lock, fuel transition, physics,
collision, particles and the positions recorded when a falling rocket previously
disappeared. `FlyingEnemyCollisionTests` covers both camera angles, firing
distance and actual transformed crash boxes. Guide tests verify hidden anchors,
uniform scaling and hull-only shadows. Surface-position tests cover the shared
minimap mapping.

```powershell
dotnet test TheOmegaStrain.Tests/TheOmegaStrain.Tests.csproj --no-restore --configuration Release -m:1 --filter "FullyQualifiedName~AttackShip|FullyQualifiedName~RocketHelpersTests|FullyQualifiedName~FlyingEnemyCollisionTests|FullyQualifiedName~LazerCrashDetectionTests|FullyQualifiedName~ShipWeaponAudioTests"
dotnet build TheOmegaStrain.sln --no-restore --configuration Release -m:1
```

These focused checks cover this feature; they do not replace manual gameplay testing.
