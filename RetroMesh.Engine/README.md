# RetroMesh Engine

RetroMesh Engine is a reusable retro 3D engine for polygon-based games and
tools. It provides lightweight projection, deterministic geometry helpers,
collision primitives, frame timing, input abstractions, and audio playback
without owning the rules of any specific game.

The engine originated inside The Omega Strain, but it is not intended to be
limited to that game. The Omega Strain is the reference implementation: a real
game proving the engine contracts, adapter layer, renderer feed, input handling,
and audio runtime under gameplay pressure.

The engine is intentionally small. It should know how to move, project, shade,
collide, time, clone, and play generic game objects. It should not know what a
seeder, mothership, powerup, planet, highscore, Steam achievement, or campaign
checkpoint is.

## What The Engine Provides

- Object primitives: `IRenderable3dObject`, `Engine3dObject`,
  `I3dObjectPart`, `ITriangleMesh`, `ITriangleMeshWithColor`, and `IVector3`.
- Geometry helpers: mesh rotation, vector math, bounds, ground projection,
  surface footprint alignment, and shadow projection.
- Rendering preparation: perspective projection from world objects to projected
  triangle data through `IWorldProjector` and `PerspectiveWorldProjector`.
- Collision helpers: crash boxes, collision pair scanning, impact state, radial
  object scanning, and collision direction math.
- World helpers: grid coordinate math, tile rectangles, object placement math,
  world position math, world view setup, and surface geometry caching.
- Frame timing: frame phase timers, frame performance tracking, and helpers for
  frame-rate independent movement.
- Input primitives: keyboard mapping enums plus Windows mouse and Xbox
  controller input readers.
- Audio runtime: a generic audio player abstraction with an NAudio-backed
  implementation for one-shots, segmented loops, music, pan, playback speed,
  and runtime volume control.
- Collection and lifecycle helpers used by real-time render loops.

## What Belongs Outside The Engine

Keep game-specific behavior in the game project:

- Game rules, scoring, achievements, persistence, save checkpoints, and Steam.
- Concrete enemy types, AI behavior, weapon balance, mission flow, and overlays.
- Biome definitions, campaign scenes, map generation rules, and story text.
- Game-specific assets, installers, trailers, store material, and local secrets.

If a type or method has to mention a concrete game concept, it belongs outside
`RetroMesh.Engine`. If it can operate on generic vectors, meshes, triangles,
objects, inputs, audio, timing, or collision state, it is a good engine
candidate.

## Project Setup

Reference the engine project directly while developing:

```xml
<ProjectReference Include="..\RetroMesh.Engine\RetroMesh.Engine.csproj" />
```

After the engine is split into its own repository/package, consume it as a
NuGet package instead. The game should only depend on the public engine
abstractions and should keep adapter code close to the game project.

## Building A Game With RetroMesh

1. Create a game-specific domain project.
   Define the objects and rules that make your game unique. Domain models may
   implement `IRenderable3dObject` directly or wrap/derive from
   `Engine3dObject`.

2. Build meshes from object parts and triangles.
   Use `I3dObjectPart` and `ITriangleMeshWithColor` to describe visual geometry.
   Keep meshes centered around sensible pivots, especially for surface-based
   objects that should stand on terrain.

3. Keep world state in the game.
   The engine can project and scan a list of objects, but the game owns which
   objects exist, when they spawn, which are active, and what gameplay state
   they represent.

4. Resolve render positions through an adapter.
   `PerspectiveWorldProjector` asks the game to resolve an object's render
   position. This keeps camera rules, world offsets, and game-specific Z/Y
   conventions outside the engine.

5. Run frame updates with elapsed-time scaling.
   Use engine timing helpers to keep movement consistent across 60 Hz, 90 Hz,
   and other refresh rates.

6. Feed projected triangles to a renderer.
   RetroMesh prepares projected triangle data. The host game decides whether to
   draw those triangles with WPF, DirectX, Unity, a test renderer, or something
   else.

7. Keep audio and input behind interfaces.
   The engine provides useful Windows implementations today, but game code
   should depend on `IAudioPlayer` and input abstractions so those parts can be
   replaced later.

## Minimal Projection Flow

```csharp
var projector = new PerspectiveWorldProjector<MyGameObject, MyProjectedTriangle>(
    viewport,
    triangleFactory: () => new MyProjectedTriangle(),
    tryResolveRenderPosition: MyRenderPositionResolver.TryResolve);

List<MyProjectedTriangle> triangles = projector.ProjectToTriangles(
    world.RenderableObjects,
    currentFrame,
    reusableTriangleBuffer);

renderer.Draw(triangles);
```

The important pattern is that `MyGameObject`, `MyProjectedTriangle`, the world,
and the renderer remain game-owned. RetroMesh handles the reusable projection
work between them.

## Build And Test

From a standalone engine checkout or from the current combined repository:

```powershell
dotnet build .\RetroMesh.Engine.slnx --no-restore
dotnet test .\RetroMesh.Engine.slnx --no-restore
```

When changing engine contracts inside the current combined repository, also run
the reference game build:

```powershell
dotnet build .\TheOmegaStrain.sln --no-restore
```

## Design Rules

- Keep `RetroMesh.Engine` free from references back to game projects.
- Prefer interfaces and generic helpers at the engine boundary.
- Keep adapter code in the consuming game until a second game proves the
  abstraction belongs in the engine.
- Avoid hidden dependencies on any single game's coordinate conventions. Pass
  those conventions in through resolvers or options.
- Keep allocations low in frame loops by reusing buffers where the API supports
  it.

## License And Support

RetroMesh Engine is released under the MIT License. See `LICENSE`.

The engine is provided as-is. Custom changes, ports, integrations, performance
tuning, or game-specific feature work are handled as consulting work. See
`SUPPORT.md`.
