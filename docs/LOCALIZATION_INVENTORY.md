# The Omega Strain localization inventory

Scope: player-visible game text in the current repository. Spoken HAL-E/audio remains English. The supported text languages are English, Norwegian, German, French and Polish.

## Current coverage

- The language choice, five flag markers, persistence, and English fallback are wired on `feature/enable-languages`.
- Main menu, pilot selection, settings, Intro pages, training cards, campaign and simulation briefings, victory/loss/reward overlays, highscore pages and Outro pages use `GameText` in all five catalogs.
- Dynamic text can use named JSON placeholders (for example `{callsign}`) through `GameText.Format`; the runtime value stays in code.
- Spoken HAL-E, the HUD PNG labels, the 3D outro banner and Steamworks copy are separate from text localization. Controller button names and game-specific nouns (Seeder, Decoy, PowerUp, AttackShip) remain recognizable across languages.

## Player-visible surfaces

| Area | Source | Status | Notes |
| --- | --- | --- | --- |
| Main menu | `TheOmegaStrain.Game/Scenes/Intro/Intro.cs`, `TheOmegaStrain.Common/Localization/*`, `TheOmegaStrain.Wpf/MainWindowClasses/MainWindowOverlayHandler.cs` | Translated | Menu choice is keyboard/controller-accessible. Flag row is drawn as WPF shapes rather than emoji. |
| Intro briefing and field manual | `TheOmegaStrain.Game/Scenes/Intro/Intro.cs` | Translated | Story page, five tips/HUD/controls pages, headers, page titles and info footer use the catalogs. Final highscore page is translated. |
| Pilot/callsign and saved-pilot selection | `TheOmegaStrain.Common/CommonGlobalState/States/OverlayState.cs`, `TheOmegaStrain.Game/SceneManagement/SceneHandler.cs` | Translated | Prompts, validation, action choices, navigation hints and quit confirmation. Existing player names and generated callsigns are identity data; do not translate saved names. |
| Settings | `TheOmegaStrain.Common/CommonGlobalState/GameSettingsOverlayFormatter.cs`, `TheOmegaStrain.Game/SceneManagement/SceneHandler.cs`, `TheOmegaStrain.Common/CommonGlobalState/States/OverlayState.cs` | Translated | Audio, graphics, controls and flight panels: titles, descriptions, field labels, enum values, device names and button hints. Preserve input mapping and setting indices. |
| Training | `TheOmegaStrain.Game/Scenes/Tutorial/TutorialScene.cs`, `TheOmegaStrain.Gameplay/Controls/TutorialVoicePromptControls.cs`, `TheOmegaStrain.Wpf/MainWindow.xaml.cs` | Written text translated | Six HAL-E instruction cards and changing footer are translated; voice remains English. Written instruction timing must still match the English voice cues. |
| Campaign briefings | `TheOmegaStrain.Game/Scenes/Scene1/Scene1.cs` through `Scene8/Scene8.cs` | Translated | Eight scene-specific headers, planet/phase titles, story bodies and launch footers. Enemy counts and thresholds are embedded in prose; verify translations against actual scene tuning. |
| Simulation briefing | `TheOmegaStrain.Game/Scenes/SceneSimulation/SceneSimulation.cs` | Translated | Dynamic round, biome, enemy counts, carrier class, threshold/delay and directive. Translate templates/labels, not numeric values. |
| Startup, victory and defeat | `TheOmegaStrain.Game/SceneManagement/SceneHandler.cs`, `TheOmegaStrain.Runtime/Loops/LiveGameLoop.cs` | Translated | Warmup countdown, mission-reward card, next-sector message, planet-lost choices and hints. Avoid changing gameplay state while replacing display text. |
| Reward breakdown | `TheOmegaStrain.Common/GamePlayHelpers/PlanetRewardCalculator.cs`, `TheOmegaStrain.Runtime/Loops/LiveGameLoop.cs` | Translated | Seven bonus labels, total and count-up footer. Fixed-width `PadRight(24)` needs layout checks for longer German/Polish text. Keep score calculation independent from labels. |
| Highscores | `TheOmegaStrain.Common/Persistence/HighscoreOverlayFormatter.cs`, Intro and Outro | Translated | Empty-state copy, columns and titles. Keep callsigns untouched. Refresh recognizes the localized highscore titles. |
| Outro | `TheOmegaStrain.Game/Scenes/Outtro/OutroDirector.cs` | Translated | Victory and simulation handoff use the catalogs; leaderboard is translated. |
| 3D outro banner | `TheOmegaStrain.Game/World/Objects/OutroLandingBanner.cs` | Special case | Two English lines are geometry, not overlay text. The custom glyph set and fixed banner width need separate design/character checks before translation. |
| Overlay navigation hints | `TheOmegaStrain.Wpf/MainWindowClasses/MainWindowOverlayHandler.cs`, `TheOmegaStrain.Common/CommonGlobalState/States/OverlayState.cs` | Translated | Page indicator and generic preset text are generated separately from scene content. Controller and keyboard wording must both be covered. |
| HUD | `TheOmegaStrain.Wpf/MainWindowClasses/HudOverlayHandler.cs`, `TheOmegaStrain.Wpf/GameGraphics/HudOverlay.png` | Special case | `HIGHSCORE` is live text. `FPS`, `TRI`, `P`, `ALT`, `THR`, `BIO-LEVEL` and the logo are baked into the PNG. Decide whether these abbreviations remain universal or require localized HUD assets. |
| Window/debug labels | `TheOmegaStrain.Wpf/MainWindow.xaml`, `TheOmegaStrain.Wpf/MainWindow.xaml.cs` | Low priority | Borderless window title and FPS/debug output are not normal narrative/UI copy. Do not treat object IDs, sound IDs or log strings as translations. |
| Steam store and achievements | Steamworks configuration, outside this repository | Separate work | Store-page copy and achievement display names/descriptions are not sourced from the game's `GameText` catalogs. Steam API identifiers in `TheOmegaStrain.Steam/SteamGameConfig.cs` must remain unchanged. |

## Remaining QA and special cases

1. Highscore refresh still recognizes page titles rather than a semantic page ID, but now checks every supported localized title. Avoid renaming a highscore title without updating the shared catalog.
2. Startup warmup compares its header with the current translated catalog value. A later refactor can replace this with a semantic marker; language selection is not available during warmup.
3. `OverlayTextSafetyTests` protects the English scene overlays. Catalog tests check key and placeholder parity across all five languages; visual line wrapping still needs in-game review.
4. Long German, French and Polish strings may wrap differently in settings, pilot selection and the reward table. Check supported resolutions and controller hints.
5. Spoken voice remains English. Translated training cards must stay synchronized with its cues.
6. HUD PNG labels and the 3D outro banner need asset/geometry work if those are to be translated. Do not rewrite them as part of a text-only pass.

Before listing any language as supported on Steam, play through a complete campaign path in that language (including training, loss/retry, settings and Outro) and review the machine-assisted translations with a fluent reader where possible.
