# Steam integration

This project isolates optional Steamworks.NET support from the game runtime.

The game should keep working when Steam is unavailable, the Steam client is not running,
or `steam_api64.dll` is missing. `SteamManager.Initialize` returns `false` in those cases
and the wrapper classes no-op instead of throwing into gameplay code.

Debug builds copy `steam_appid.txt` with The Omega Strain Steam AppID (`4952000`) so
local runs exercise the same backend as Steam launches. Do not publish or ship that file.
Release/publish builds should use the real Steam AppID assigned to The Omega Strain
(`4952000`), set in `SteamGameConfig.ProductionAppId`.

Call `SteamManager.RunCallbacks` from the normal game loop when Steam is initialized.
Leaderboard calls complete only while callbacks are pumped.

Keep Steam API names in `SteamGameConfig` so achievements, stats, and leaderboards do not
become string literals spread through gameplay code.

## Release review checks

Publish `TheOmegaStrain.Wpf` with `FolderProfile` in Release. This profile bundles
.NET for Windows x64. Upload the complete `bin/Release/net10.0-windows7.0/publish`
folder, not the ordinary build output. Verify `coreclr.dll`, `hostfxr.dll` and
`PresentationFramework.dll` are included; test on a machine without .NET installed.

Before submitting the new build to Steam, check these together with a controller:

- On first launch, choose USE THIS CALLSIGN, or NEW NAME SUGGESTION, using Up/Down
  and A. No typing is required. SAVED PILOTS opens the existing local profiles.
- Manual callsign typing is enabled only when Keyboard is the selected scheme.
- Open Steam Overlay during gameplay: gameplay and game-menu input must stop.
  Close it, release the buttons, then press Menu to resume deliberately.
- Disconnect the active controller during gameplay: the game must pause and stay
  paused after reconnection until Menu is pressed. Keyboard Ctrl can also resume.
- Verify settings, training, starting a game and quitting without keyboard/mouse.
- Publish the Developer's Recommended Configuration in Steamworks separately;
  this is not set by the executable or by the self-contained publish profile.
