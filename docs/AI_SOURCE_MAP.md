# AI Source Map For The Omega Strain

This file tells AI agents where to look before changing behavior. The goal is
to preserve the existing architecture and reuse established patterns.

## First Checks

Open these before making broad changes:

| Topic | Files |
| --- | --- |
| Repository layout | `README.md`, `docs/USING_RETROMESH_ENGINE.md` |
| Object contract | `TheOmegaStrain.Domain/Interfaces/Objects/I3dObject.cs` |
| Movement contract | `TheOmegaStrain.Domain/Interfaces/Movement/IObjectMovement.cs` |
| World object list | `TheOmegaStrain.Game/World/GameWorld.cs` |
| Global state | `TheOmegaStrain.Common/CommonGlobalState/GameState.cs` |
| Engine boundary | `TheOmegaStrain.Common/OmegaEngineAdapters/*`, `docs/USING_RETROMESH_ENGINE.md` |

Rule of thumb: if code needs Omega nouns like seeder, infection, mothership,
powerup, checkpoint, biome balance, Hal-E, or Steam achievement names, it
belongs in The Omega Strain, not RetroMesh.

## Project Skills

Use `.codex/skills/omega-add-game-object/SKILL.md` when adding or changing
world objects, object factories, movement/control classes, object AI, placement,
crash boxes, shadows, or object-specific tests.

Use `.codex/skills/omega-create-scene/SKILL.md` when creating or extending
gameplay scenes, scene directors, scene registration, default scene objects,
biome setup, scene overlays, scene progression, or scene-specific tests.

## Creating Or Extending Gameplay Scenes

Read these first:

- `TheOmegaStrain.Domain/Interfaces/Scenes/ISceneHandler.cs`
- `TheOmegaStrain.Domain/Interfaces/Scenes/ISceneDirector.cs`
- `TheOmegaStrain.Game/SceneManagement/SceneHandler.cs`
- `TheOmegaStrain.Game/Scenes/Scene1/Scene1.cs`
- `TheOmegaStrain.Game/Scenes/Scene1/Scene1Director.cs`
- `TheOmegaStrain.Game/Scenes/Scene6/Scene6.cs`
- `TheOmegaStrain.Game/Scenes/Scene6/Scene6Director.cs`
- `TheOmegaStrain.Game/Scenes/SceneSimulation/SceneSimulation.cs`
- `TheOmegaStrain.Game/Scenes/Tutorial/TutorialScene.cs`

Default gameplay scenes should normally include a ship with weapons, a
`SeederGuidanceArrow`, seeders, a surface viewport assigned to
`GameState.SurfaceState.SurfaceViewportObject`, scene overlay setup, game
overlay setup, and a director when phase activation/checkpoint/victory logic is
needed.

When adding a campaign scene, update `SceneHandler.cs`, scene index comments,
scene order tests, infection tuning tests, and any biome-specific asset tests.

## Adding Or Changing A Scene Object

Read these first:

- `TheOmegaStrain.Game/Scenes/Tutorial/TutorialScene.cs`
- `TheOmegaStrain.Game/Scenes/Scene6/Scene6.cs`
- `TheOmegaStrain.Game/Scenes/Scene6/Scene6Director.cs`
- `TheOmegaStrain.Game/World/GameWorld.cs`
- `TheOmegaStrain.Game/Helpers/ObjectPlacementHelpers.cs`
- `TheOmegaStrain.Game/Helpers\SeederPlacementHelpers.cs`
- `TheOmegaStrain.Common/OmegaEngineAdapters/SurfacePositionSyncHelpers.cs`

Useful tests:

- `TheOmegaStrain.Tests/SceneManagement/TutorialSceneTests.cs`
- `TheOmegaStrain.Tests/SceneManagement/SeederPlacementHelpersTests.cs`
- `TheOmegaStrain.Tests/WorldObjects/DesertSurfaceGenerationTests.cs`
- `TheOmegaStrain.Tests/OmegaEngineAdapters/SurfacePositionSyncHelpersTests.cs`

Established pattern:

1. Create the object with its factory method where possible.
2. Assign `WorldPosition` for map/world placement.
3. Assign `ObjectOffsets` for object-local placement.
4. Assign `Movement`, `ImpactStatus`, powerup flags, active flags, and shadow
   behavior explicitly.
5. Add visible/runtime objects to `world.WorldInhabitants`.
6. Add AI/surface-state objects to `GameState.SurfaceState.AiObjects` when the
   existing scene pattern does so.
7. Use placement helpers for platform exclusion and distance rules.

Do not scatter ad hoc placement math through scenes unless no helper exists.
When no helper exists, create a small helper near the existing placement code
and cover it with focused tests.

## Object Geometry And Crash Boxes

Read these first:

- `TheOmegaStrain.Game/World/Objects/KamikazeDrone.cs`
- `TheOmegaStrain.Game/World/Objects\Seeder.cs`
- `TheOmegaStrain.Game/World/Objects/PowerUp.cs`
- `TheOmegaStrain.Game/World/Objects/Surface.cs`
- `TheOmegaStrain.Game/Helpers/OmegaObject3DHelpers.cs`
- `TheOmegaStrain.Runtime/CrashDetection/CrashDetection.Main.cs`
- `TheOmegaStrain.Runtime/CrashDetection/CrashDetection.Collision.cs`
- `TheOmegaStrain.Runtime/CrashDetection/CrashDetection.Cache.cs`

Useful tests:

- `TheOmegaStrain.Tests/Physics/CollisionBoxMathTests.cs`
- `TheOmegaStrain.Tests/Physics/CollisionDirectionMathTests.cs`
- `TheOmegaStrain.Tests/Physics/LazerCrashDetectionTests.cs`
- `TheOmegaStrain.Tests/Physics/ExplosionPhysicsTests.cs`

Pattern:

- Build visible geometry from object parts/faces.
- Use helper-generated crash boxes for larger formations and complex objects.
- Keep hidden guide parts only when an existing renderer/control pattern needs
  them.
- Keep crashbox debug disabled unless actively diagnosing collision.

Coordinate reminder:

```text
smaller Y = up
larger Y  = down
```

If an object is visually low in terrain after scaling, adjust the surface
anchor/offset using existing helpers. Do not "fix" it by changing unrelated
projection math.

## Movement Controllers And AI

Read these first:

- `TheOmegaStrain.Domain/Interfaces/Movement/IObjectMovement.cs`
- `TheOmegaStrain.Gameplay/Controls/KamikazeDroneControls/KamikazeDroneControls.cs`
- `TheOmegaStrain.Gameplay/Controls/KamikazeDroneControls/KamikazeDroneAi.cs`
- `TheOmegaStrain.Gameplay/Controls/KamikazeDroneControls/KamikazeDroneMovementHelpers.cs`
- `TheOmegaStrain.Gameplay/Controls/SeederControls/SeederControls.cs`
- `TheOmegaStrain.Gameplay/Controls/SeederControls/SeederAi.cs`
- `TheOmegaStrain.Gameplay/Controls/SeederControls/SeederMovementHelpers.cs`
- `TheOmegaStrain.Gameplay/Controls/TutorialSeederControls.cs`

Useful tests:

- `TheOmegaStrain.Tests/Controls/KamikazeDroneControlsHuntTimingTests.cs`
- `TheOmegaStrain.Tests/Controls\SeederAiMovementTests.cs`
- `TheOmegaStrain.Tests/Controls\SeederControlsParticleGuideTests.cs`
- `TheOmegaStrain.Tests/Controls/TutorialSeederControlsTests.cs`

Every moving object should follow the movement contract:

```csharp
void MoveObject(I3dObject object3d, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry);
```

Basic pursuit formula used by drone-style AI:

```text
direction       = target - current
distance        = |direction|
unitDirection   = direction / distance
speedPerSecond  = screenWidth / secondsPerScreen
moveDistance    = speedPerSecond * deltaSeconds
current        += unitDirection * moveDistance
```

Frame-rate rule:

```text
movementThisFrame = speedPerSecond * deltaSeconds
```

Do not use "per frame" increments for gameplay speed unless the value is
deliberately converted from the 90 FPS baseline.

Stateful AI pattern:

- Keep per-object AI state keyed by object identity.
- Desynchronize behavior where many objects would otherwise act in lockstep.
- Sync authoritative state back to the object list that the rest of the game
  uses.
- In training/tutorial, keep special AI behavior local to tutorial controls.

## Ship Controls, Input, Pause, And Overlays

Read these first:

- `TheOmegaStrain.Gameplay/Controls/ShipControls.cs`
- `TheOmegaStrain.Common/InputHelpers/InputManager.cs`
- `TheOmegaStrain.Wpf/Input/WpfGameInputKeyMapper.cs`
- `TheOmegaStrain.Wpf/MainWindowClasses/Overlays/OverlayManager.cs`
- `TheOmegaStrain.Wpf/MainWindowClasses/MainWindowOverlayHandler.cs`
- `TheOmegaStrain.Wpf/MainWindowClasses/HudOverlayHandler.cs`
- `TheOmegaStrain.Common/CommonGlobalState/States/OverlayState.cs`

Useful tests:

- `TheOmegaStrain.Tests/Controls/WpfGameInputKeyMapperTests.cs`
- `TheOmegaStrain.Tests/SceneManagement/OverlayAutoPagingTests.cs`
- `TheOmegaStrain.Tests/SceneManagement/OverlayLayoutTests.cs`
- `TheOmegaStrain.Tests/SceneManagement/OverlayTextSafetyTests.cs`
- `TheOmegaStrain.Tests/SceneManagement/SettingsOverlayTests.cs`

Guardrails:

- Gameplay input should be consumed only during gameplay.
- Intro, outro, menu, settings, training overlays, and pause overlays should
  block gameplay input.
- When leaving overlay pause, preserve or restore the object state according to
  the existing pause pattern.
- Mouse, keyboard, and controller modes should not all drive gameplay at the
  same time.

## Weapons And Projectiles

Read these first:

- `TheOmegaStrain.Gameplay/Controls/Weapons.cs`
- `TheOmegaStrain.Gameplay/Controls/ShipControls.cs`
- `TheOmegaStrain.Common/CommonSetup/WeaponSetup.cs`
- `TheOmegaStrain.Runtime/Rendering/WeaponsManager.cs`
- `TheOmegaStrain.Runtime/CrashDetection/CrashDetection.Collision.cs`

Useful tests:

- `TheOmegaStrain.Tests/Controls/WeaponsAimAssistTests.cs`
- `TheOmegaStrain.Tests/Physics/LazerCrashDetectionTests.cs`
- `TheOmegaStrain.Tests/Physics/ExplosionPhysicsTests.cs`

Projectile update pattern:

```text
dt = now - lastUpdate
velocity += acceleration * dt

deltaProjectile = trajectory * velocity * dt
deltaParent     = parentVelocityLocal * dt
newLocal        = currentLocal + deltaProjectile + deltaParent

distanceTraveled += |deltaProjectile|
```

Aim-assist pattern:

```text
toEnemy    = enemyTarget - projectileStart
distance   = |toEnemy|
targetDir  = Normalize(toEnemy)
dot        = Dot(projectileDir, targetDir)
insideCone = dot >= coneThreshold
result     = Normalize(projectileDir * (1 - strength) + targetDir * strength)
```

Use existing weapon setup/constants before introducing new weapon-specific
fields. Pure score for kills can be direct; bonus/reward score should follow the
existing planet reward flow.

## Physics And Biome Feel

Read these first:

- `TheOmegaStrain.Gameplay/Physics/Physics.cs`
- `TheOmegaStrain.Gameplay/Helpers/PhysicsHelpers.cs`
- `TheOmegaStrain.Common/CommonSetup/BiomePhysicsSetup.cs`
- `TheOmegaStrain.Domain/Interfaces/Physics/IPhysics.cs`
- RetroMesh package/source: `RetroMesh.Engine/Physics/PhysicsMotionMath.cs`
- RetroMesh package/source: `RetroMesh.Engine/Geometry/VectorMath.cs`
- RetroMesh package/source: `RetroMesh.Engine/Loop/FrameTimingMath.cs`

Useful tests:

- `TheOmegaStrain.Tests/Physics/BiomePhysicsSetupTests.cs`
- `TheOmegaStrain.Tests/Physics/ShipSurfaceLandingTests.cs`
- `TheOmegaStrain.Tests/Physics/ParticleSurfaceBounceTests.cs`
- `TheOmegaStrain.Tests/Physics/ParticleShadowTests.cs`

Common equations:

```text
frameScale = deltaTime * baselineFps
drag       = dragPerFrame ^ frameScale

tiltRad     = tiltDegrees * degToRad
rotationRad = rotationDegrees * degToRad

upwardFactor  = cos(tiltRad)
forwardFactor = sin(tiltRad)
dirX          = sin(rotationRad)
dirZ          = cos(rotationRad)

horizontalForce = thrust * forwardFactor * dt
verticalThrust  = thrust * upwardFactor * dt
gravityPull     = gravityAcceleration * gravityMultiplier * dt
```

Biome differences should be subtle and testable. Preserve the default biome
feel unless the change specifically targets biome tuning.

## Surface, Projection, Shadows, And Minimap Positions

Read these first:

- `TheOmegaStrain.Common/OmegaEngineAdapters/SurfacePositionSyncHelpers.cs`
- `TheOmegaStrain.Common/OmegaEngineAdapters/SurfaceGroundProjectionHelpers.cs`
- `TheOmegaStrain.Common/CommonSetup/SurfaceSetup.cs`
- `TheOmegaStrain.Common/CommonSetup/SurfaceAnimationSetup.cs`
- `TheOmegaStrain.Gameplay/Controls/SeederGuidanceArrowControl.cs`
- `TheOmegaStrain.Common/CommonGlobalState/States/SurfaceMapPixelBuffer.cs`
- RetroMesh package/source: `RetroMesh.Engine/Rendering/PerspectiveWorldProjector.cs`

Useful tests:

- `TheOmegaStrain.Tests/OmegaEngineAdapters/SurfacePositionSyncHelpersTests.cs`
- `TheOmegaStrain.Tests/Controls/SeederGuidanceArrowControlTests.cs`
- `TheOmegaStrain.Tests/MapAndOverlay/MinimapMarkerContrastTests.cs`
- `TheOmegaStrain.Tests/WorldObjects/SurfaceViewportPoolingTests.cs`

Patterns:

- Use shared conversion helpers for world-to-map positions.
- Keep guidance arrow and minimap marker math consistent.
- Surface-based objects should be anchored to their bottom/surface point, not
  visually scaled down into the ground.
- Flying object shadows should be projected to the surface using the established
  ground projection pattern.

## Persistence, Highscore, Checkpoints, And Progress

Read these first:

- `TheOmegaStrain.Common/Persistence/GameStatePersistence.cs`
- `TheOmegaStrain.Common/Persistence/SavedGameState.cs`
- `TheOmegaStrain.Common/Persistence/HighscoreService.cs`
- `TheOmegaStrain.Common/Persistence/HighscoreData.cs`
- `TheOmegaStrain.Common/Persistence/PlayerProgressService.cs`
- `TheOmegaStrain.Common/Persistence/PersistenceSetup.cs`
- `TheOmegaStrain.Common/Persistence/TutorialProgressService.cs`
- `TheOmegaStrain.Common/Persistence/SupabaseHighscoreClient.cs`
- `TheOmegaStrain.Common/Persistence/PlayerCallsignService.cs`
- `TheOmegaStrain.Common/Persistence/SupabaseCallsignClient.cs`

Useful tests:

- `TheOmegaStrain.Tests/Persistence/GameStatePersistenceIsolationTests.cs`
- `TheOmegaStrain.Tests/Persistence/HighscoreServiceConsistencyTests.cs`
- `TheOmegaStrain.Tests/Persistence/HighscoreRemoteFallbackTests.cs`
- `TheOmegaStrain.Tests/Persistence/PlayerCallsignServiceTests.cs`
- `TheOmegaStrain.Tests/Persistence/PowerUpCheckpointPersistenceTests.cs`
- `TheOmegaStrain.Tests/Persistence/TutorialProgressServiceTests.cs`

Guardrails:

- Save at explicit checkpoints: powerup, mothership enter, mothership killed,
  and scene completion/next scene.
- Do not save arbitrary current state on exit if that bypasses checkpoint
  design.
- Do not replace stronger progress with weaker progress by accident.
- Highscore, kills, powerups, speed upgrades, achievements, and tutorial status
  should not be reset by infection/planet reset unless a specific reset rule
  says so.
- Supabase should be used when configured and online; local fallback should keep
  the game playable offline.
- Callsign reservation is optional: local duplicate checks should always run,
  while Supabase registry failures should not block offline play.

## Steam Integration

Read these first:

- `TheOmegaStrain.Steam/README.md`
- `TheOmegaStrain.Steam/SteamManager.cs`
- `TheOmegaStrain.Steam/SteamGameConfig.cs`
- `TheOmegaStrain.Steam/SteamGameplaySync.cs`
- `TheOmegaStrain.Steam/SteamAchievements.cs`
- `TheOmegaStrain.Steam/SteamStats.cs`
- `TheOmegaStrain.Steam/SteamLeaderboards.cs`
- `TheOmegaStrain.Steam/SteamDiagnostics.cs`

Useful tests:

- `TheOmegaStrain.Tests/Steam/SteamTests.cs`

Guardrails:

- Steam must be optional.
- Missing Steam client, AppID text file, Steam DLL, or configured backend should
  produce diagnostics/no-op behavior, not gameplay crashes.
- Keep Steam API names centralized in `SteamGameConfig`.
- Pump callbacks from the normal game loop only when Steam is initialized.
- Avoid duplicate event submissions when an achievement/stat has already been
  unlocked or sent.

## Effects, Particles, Audio, And Graphics Settings

Read these first:

- `TheOmegaStrain.Gameplay/Controls/Weapons.cs`
- `TheOmegaStrain.Gameplay/Physics/Physics.cs`
- `TheOmegaStrain.Common/CommonSetup/GameOverlaySetup.cs`
- `TheOmegaStrain.Common/CommonSetup/BiomePhysicsSetup.cs`
- `TheOmegaStrain.Runtime/Rendering/WeaponsManager.cs`
- `TheOmegaStrain.Wpf/MainWindowClasses/HudOverlayHandler.cs`

Useful tests:

- `TheOmegaStrain.Tests/Physics/ParticleSurfaceBounceTests.cs`
- `TheOmegaStrain.Tests/Physics/ParticleShadowTests.cs`
- `TheOmegaStrain.Tests/MapAndOverlay/HudInfectionMeterTests.cs`

Guardrails:

- Default graphics should preserve the pre-settings look unless the user
  explicitly asks to change default visuals.
- Low graphics settings should disable optional new effects first.
- Audio effects should stop when leaving a planet except for music, following
  existing runtime audio rules.
- Do not make particles or effects frame-rate dependent.

## Installer, Store Assets, Trailer Assets

Read these first:

- `installer/`
- `TheOmegaStrain.Wpf/GameGraphics/SteamLibraryAssets/README.md`
- `TheOmegaStrain.Wpf/GameGraphics/SteamAchievementIcons/README.md`

Guardrails:

- Shipping installer assets belong in the project.
- Raw trailer editing experiments and generated scratch outputs should live
  outside the game repo unless explicitly promoted to shipping assets.
- Keep `secrets.json` handling intentional. Do not commit real secrets.

## Suggested Test Commands

Fast targeted examples:

```powershell
dotnet test .\TheOmegaStrain.Tests\TheOmegaStrain.Tests.csproj --no-restore --filter FullyQualifiedName~Persistence
dotnet test .\TheOmegaStrain.Tests\TheOmegaStrain.Tests.csproj --no-restore --filter FullyQualifiedName~Steam
dotnet test .\TheOmegaStrain.Tests\TheOmegaStrain.Tests.csproj --no-restore --filter FullyQualifiedName~Seeder
dotnet test .\TheOmegaStrain.Tests\TheOmegaStrain.Tests.csproj --no-restore --filter FullyQualifiedName~Physics
dotnet test .\TheOmegaStrain.Tests\TheOmegaStrain.Tests.csproj --no-restore --filter FullyQualifiedName~Overlay
```

Broader check:

```powershell
dotnet build .\TheOmegaStrain.sln --no-restore
dotnet test .\TheOmegaStrain.sln --no-restore
```

Do not claim a test passed unless it was actually run.
