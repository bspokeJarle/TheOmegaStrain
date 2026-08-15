# The Omega Strain

The Omega Strain is a retro 3D action game built around low-poly worlds,
physics-driven flight, hostile alien seeders, drones, mothership fights,
powerups, score chasing, Steam achievements, and biome-specific atmosphere.

The game is inspired by old-school polygon graphics and classics like
Zarch/Virus, but it is now backed by RetroMesh: a reusable engine extracted from
this codebase for rendering, geometry, projection, collision, physics helpers,
audio foundations, timing, and other shared game-loop functionality.

## Repository Layout

- `TheOmegaStrain.Wpf/`: WPF host application and game assets.
- `TheOmegaStrain.Game/`: scenes, world setup, game objects, and game content.
- `TheOmegaStrain.Gameplay/`: controls, weapons, AI, effects, and gameplay rules.
- `TheOmegaStrain.Runtime/`: game loop orchestration, persistence hooks, audio
  wiring, scene transitions, and runtime services.
- `TheOmegaStrain.Domain/`: Omega-specific domain models and state.
- `TheOmegaStrain.Common/`: shared Omega infrastructure and engine adapters.
- `TheOmegaStrain.Steam/`: optional Steamworks integration.
- `TheOmegaStrain.Tests/`: automated tests.
- `TheOmegaStrain.Benchmarks/`: benchmark and performance test tooling.
- `Tools/`: small local tools such as controller probing.
- `docs/`: architecture and engine usage documentation.
- `installer/`: installer setup and related notes.

## RetroMesh Engine

The Omega Strain consumes RetroMesh as a local NuGet package:

```text
RetroMesh/artifacts/packages
```

For local development, run the bootstrap script from a fresh checkout. It clones
RetroMesh into `RetroMesh/` if needed, builds the package version configured in
`Directory.Build.props`, and can restore Omega afterwards:

```powershell
.\build\Prepare-RetroMeshPackage.ps1 -RestoreOmega
```

The package version is controlled by `RetroMeshEnginePackageVersion` in
`Directory.Build.props`.

The general engine/game boundary is documented in
`docs/USING_RETROMESH_ENGINE.md`.

## Build

Requirements:

- Windows
- .NET 10 SDK
- x64 runtime

Build the solution:

```powershell
dotnet build .\TheOmegaStrain.sln
```

Run the game locally:

```powershell
dotnet run --project .\TheOmegaStrain.Wpf\TheOmegaStrain.Wpf.csproj
```

Run tests:

```powershell
dotnet test .\TheOmegaStrain.sln
```

## Optional Services

Steam support is optional and isolated in `TheOmegaStrain.Steam/`. The game
should continue to run without Steam, without the Steam client, and without the
Steam DLL.

Supabase/highscore configuration is optional as well. When cloud setup or
network access is unavailable, the game should fall back to local persistence.

## Related Repositories

- `RetroMesh`: the reusable engine.
- `RetroMesh.GameTemplate`: a minimal game template using RetroMesh and the
  copied LogoCube intro scene as a starting point.

## Project Direction

The short-term goal is to keep The Omega Strain stable and shippable while
RetroMesh becomes a clean framework for future games. Game-specific behavior
should stay in the Omega projects; reusable rendering, geometry, projection,
collision, timing, and engine services should live in RetroMesh.
