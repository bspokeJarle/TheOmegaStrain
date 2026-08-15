# Using RetroMesh Engine To Build A Game

This document describes a general pattern for building games on RetroMesh
Engine. The Omega Strain is used as the reference implementation, but the same
shape should work for other games that want the RetroMesh projection, collision,
timing, input, and audio foundation.

## Intended Boundary

| Area | Owns | Should Not Own |
| --- | --- | --- |
| `RetroMesh.Engine` | Generic geometry, projection, collision, timing, input primitives, audio abstractions, and reusable runtime helpers. | Game rules, save state, concrete enemies, scoring, achievements, scenes, or store/platform integrations. |
| Game domain | Game-specific objects, progression, player state, world rules, and object meaning. | Generic vector/projection/collision math that can be shared by other games. |
| Game systems | AI, controls, weapons, powerups, balance, mission flow, and game-specific physics tuning. | Low-level engine math that does not know the game. |
| Host/runtime | Windowing, platform startup, overlays, persistence, content loading, renderer binding, Steam or other platform integrations. | Reusable engine primitives that future games need. |
| Adapter layer | Conversion between game types and engine interfaces. | New gameplay behavior unless the adapter is the only reasonable place for the translation. |

The rule of thumb is simple: if the code can be described without mentioning a
specific game concept, it may belong in RetroMesh. If the code needs game nouns
like a named enemy, infection system, checkpoint rule, powerup type, or biome
balance, it belongs in the game.

## Recommended Shape For A New Game

1. Create a game solution with a game domain project.
   Keep all game nouns, rules, campaign state, save data, and progression there.

2. Reference `RetroMesh.Engine`.
   During local development this can be a project reference. After the split,
   consume the engine through a package or a separate repository reference.

3. Create game renderable objects.
   Either implement `IRenderable3dObject` on your own domain objects or create
   a thin adapter/wrapper around `Engine3dObject`.

4. Build object meshes using engine primitives.
   Use engine object parts and triangle mesh interfaces for geometry. Keep
   object pivots and offsets explicit so scaling, rotation, crash boxes, and
   surface placement stay predictable.

5. Add a render-position resolver.
   The game should translate world position, camera offsets, and game-specific
   coordinate rules into `RenderPosition` for the engine projector.

6. Feed projected triangles into the renderer.
   The renderer can be WPF, DirectX, Unity, a tool renderer, or a test harness.
   The engine should not care as long as it can return projected triangle data.

7. Keep gameplay systems outside the engine.
   AI, weapons, mission scripting, checkpoints, score rewards, and UI overlays
   should live in the game projects.

8. Use engine services through interfaces.
   Audio and input should be consumed through abstractions so another game can
   swap implementations without rewriting gameplay.

## Reference Implementation: The Omega Strain

The Omega Strain demonstrates one concrete use of the pattern:

- `RetroMesh.Engine` contains reusable engine primitives.
- `TheOmegaStrain.Domain` contains Omega-specific game objects and progression types.
- `TheOmegaStrain.Common/OmegaEngineAdapters` translates Omega state and objects into
  engine-friendly calls.
- `TheOmegaStrain.Gameplay` owns Omega enemy controls, weapons, effects, and gameplay movement.
- `TheOmegaStrain.Runtime` runs the game loop, persistence hooks, render feed,
  scene transitions, and audio/gameplay orchestration.
- `TheOmegaStrain.Wpf` is the WPF host/frontend.

That shape is not mandatory for every future game. It is a working example of
the most important rule: the game depends on RetroMesh, while RetroMesh does not
depend on the game.

## Adapter Layer Guidance

Adapters should be explicit and boring. Their job is to translate, not to hide
gameplay.

Good adapter responsibilities:

- Convert game vectors/meshes/objects to engine interfaces.
- Resolve game world positions into engine render positions.
- Bridge global game settings into viewport, projection, or audio options.
- Preserve old game-facing method names while implementation moves into engine
  helpers.

Responsibilities that should stay out of adapters:

- Enemy behavior and decision making.
- Scoring, achievements, saves, and scene progression.
- Content generation and asset selection.
- Game balance and difficulty rules.

## Where New Code Should Go

Use this checklist when adding code:

- Does it mention a concrete game object or game rule? Put it in the game.
- Does it operate only on vectors, meshes, colors, crash boxes, timing, or audio
  playback state? Consider the engine.
- Does it translate between game objects and engine interfaces? Put it in an
  adapter near the game.
- Does it rely on WPF drawing APIs? Keep it in the current host or renderer.
- Does it rely on Windows-only input/audio APIs? It can live in an engine
  platform folder, but keep its interface generic.
- Does it save files, submit Steam stats, or read secrets? Keep it out of the
  engine.

## Before Splitting The Repositories

- `RetroMesh.Engine` must build and test without referencing game projects.
- Public engine names should be game-neutral.
- A game should compile by referencing the engine, not by relying on copied
  source files.
- Adapter code should make type conversion explicit.
- Any remaining game-specific helper inside the engine should either move back
  to the game or be renamed/reworked into a genuinely generic helper.
- Documentation, license, and support policy should travel with the engine.

## Minimal Mental Model

RetroMesh Engine prepares generic real-time 3D data. A game decides what that
data means.

That boundary lets The Omega Strain keep its identity while RetroMesh becomes a
usable foundation for the next game.
