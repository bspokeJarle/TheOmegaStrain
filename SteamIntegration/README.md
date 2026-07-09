# Steam integration

This project isolates optional Steamworks.NET support from the game runtime.

The game should keep working when Steam is unavailable, the Steam client is not running,
or `steam_api64.dll` is missing. `SteamManager.Initialize` returns `false` in those cases
and the wrapper classes no-op instead of throwing into gameplay code.

Debug builds copy `steam_appid.txt` with the Steamworks SpaceWar development AppID (`480`).
Do not publish or ship that file. Release/publish builds should use the real Steam AppID
assigned to The Omega Strain. Set that ID in `SteamGameConfig.ProductionAppId`.

Call `SteamManager.RunCallbacks` from the normal game loop when Steam is initialized.
Leaderboard calls complete only while callbacks are pumped.

Keep Steam API names in `SteamGameConfig` so achievements, stats, and leaderboards do not
become string literals spread through gameplay code.
