# The Omega Strain Agent Instructions

These instructions are for AI coding agents working in this repository.
The project is a shipped/ship-ready game, so preserve working behavior and
prefer existing patterns over new abstractions.

## Prime Directive

Search for the existing pattern before changing code. If a similar object,
scene, movement controller, persistence path, input path, Steam path, or test
already exists, extend that pattern instead of inventing a parallel solution.

Use `docs/AI_SOURCE_MAP.md` as the first source map for technical work.

For tasks that add or change world objects, scene objects, object factories,
movement/control classes, AI behavior, crash boxes, shadows, or object-specific
tests, use the project skill at `.codex/skills/omega-add-game-object/SKILL.md`.
For tasks that create or extend gameplay scenes, scene directors, scene
registration, default scene objects, biome setup, scene overlays, scene
progression, or scene-specific tests, use
`.codex/skills/omega-create-scene/SKILL.md`.

## Repository Boundary

- `TheOmegaStrain.Wpf`: WPF host, rendering integration, overlays, input device
  adapters, local assets, installer-facing assets.
- `TheOmegaStrain.Game`: scenes, world setup, game objects, object factories,
  biome content, placement helpers.
- `TheOmegaStrain.Gameplay`: controls, weapons, AI, powerups, effects, gameplay
  physics tuning, score/reward rules.
- `TheOmegaStrain.Runtime`: game-loop orchestration, crash detection,
  persistence hooks, scene transitions, audio/runtime services.
- `TheOmegaStrain.Domain`: Omega-specific state, interfaces, enums, game models.
- `TheOmegaStrain.Common`: shared Omega infrastructure, setup constants,
  persistence, adapters to RetroMesh.
- `TheOmegaStrain.Steam`: optional Steam integration. It must no-op when Steam
  or `steam_api64.dll` is unavailable.
- `docs`: technical documentation. Keep AI guidance here, not inside game code.

RetroMesh is the engine. The Omega Strain is the game. Do not move
Omega-specific nouns, balance rules, achievements, save rules, enemies,
powerups, or story content into RetroMesh.

## Required Workflow

1. Inspect the files listed in `docs/AI_SOURCE_MAP.md` for the area being
   changed.
2. Check existing tests before writing new behavior.
3. Make the smallest coherent change that follows the local pattern.
4. Add or update targeted tests when behavior changes.
5. Build or run relevant tests when practical.
6. Remove debug flags, test state, generated scratch artifacts, and temporary
   data before finishing.

## Guardrails

- Do not break existing gameplay to add a new feature.
- Do not rewrite working systems unless the user explicitly asks for a rewrite.
- Do not introduce another persistence route without checking existing save,
  highscore, player progress, Steam, and Supabase fallback behavior.
- Never replace stronger saved progress with weaker progress unless the existing
  code explicitly models a planet reset.
- Saves should happen at explicit checkpoints, not just because the game exits.
- Steam and Supabase must remain optional. Missing network, secrets, Steam
  client, or Steam DLL should not stop local gameplay.
- Movement must be delta-time based and calibrated against the 90 FPS gameplay
  feel unless a file already documents a different baseline.
- Respect the game coordinate system: smaller Y means up, larger Y means down.
- For runtime object instances, understand whether code mutates templates or
  deep copies. Do not mutate shared templates accidentally.
- Scene objects should use placement helpers and platform exclusion checks.
  Plants, houses, towers, tents, enemies, and similar objects should not spawn
  on the platform unless a scene deliberately creates a screen-space test
  object.
- Surface-based objects must use the established surface sync/ground projection
  helpers so they remain visually anchored as they approach the camera.
- Crash boxes should use existing generation/transform helpers. If collision
  visualization looks wrong, inspect both the generated box and the debug
  renderer before changing collision behavior.
- Input should affect gameplay only during actual gameplay. Overlays, intro,
  outro, settings, training pauses, and menus should gate gameplay input.
- Do not leave crashbox debug, performance logging, forced scene selection,
  forced user state, or one-off test objects enabled.
- Keep installer/media/trailer/generated marketing artifacts out of the game
  source unless they are actual shipping assets.

## Code Style

- Prefer explicit, boring adapters over hidden conversions.
- Prefer helper methods already present in the project over ad hoc math.
- Keep comments short and only where they explain a non-obvious rule.
- Use names that describe game intent, not temporary implementation experiments.
- Do not add broad abstractions for a single use unless it matches an existing
  local pattern or clearly removes repeated complexity.

## Line Endings

- Follow `.gitattributes` and `.editorconfig`. Windows/.NET source files use
  CRLF in the working tree; LF in Git's index is normal.
- After every edit, including `apply_patch`, preserve the file's encoding/BOM
  and restore its required working-tree line endings. Never leave mixed LF/CRLF.
- Before finishing, check touched files with `git ls-files --eol` and run
  `git diff --check`. Check newly created files as well as tracked files.
- Fix only files touched by the task. Do not normalize the whole repository,
  change global Git settings, or suppress line-ending warnings to hide the issue.

## Tests To Consider

Use the source map for exact test files. Typical commands:

```powershell
dotnet build .\TheOmegaStrain.sln --no-restore
dotnet test .\TheOmegaStrain.Tests\TheOmegaStrain.Tests.csproj --no-restore
```

Run narrower tests when iterating quickly, then broader tests before a risky
merge.
