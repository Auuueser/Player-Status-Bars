# Player Status Bars

Player Status Bars is a client-side BepInEx mod for Lethal Company. It displays compact world-space status bars above other players, showing health and Cadaver infection information without requiring server-side installation.

## Features

- Displays health and infection bars above other players.
- Excludes the local player.
- Uses a world-space UI that follows the player and faces the local camera.
- Reads infection values from `CadaverGrowthAI.playerInfections[playerId].infectionMeter` when Cadaver Growth is present.
- Supports BepInEx configuration and optional LethalConfig runtime adjustment.
- Includes distance-based visibility, orbit-phase hiding, infection bar display modes, and configurable layout offsets.
- Shows a critical health state for players reported as critically injured, bleeding heavily, or persistently below the low-health threshold.
- Uses a short stabilization window to reduce stale critical or low-health display for players joining mid-session through late-join mods.
- Uses the active gameplay, spectator, or freecam view as the distance reference when deciding whether bars are visible.
- Hides bars during the ship takeoff transition to avoid displaying transient player states.
- Uses Chinese critical text when a Chinese localization or translation plugin is detected; otherwise it uses English text.

## Requirements

- Lethal Company
- BepInEx 5
- LethalConfig is optional.
- Cadaver Growth is optional. If it is not present, health bars continue to work and infection values are safely unavailable.

## Installation

1. Build or download `PlayerStatusBars.dll`.
2. Place the DLL in the target profile's `BepInEx/plugins` folder.
3. Start the game once to generate the BepInEx configuration file.
4. Adjust settings through the generated config file, or through LethalConfig when it is installed.

## Build

The project references game and mod assemblies from a local Lethal Company installation. The project file exposes MSBuild properties so local paths do not need to be committed.

Example:

```powershell
dotnet build PlayerStatusBars.csproj -c Release `
  -p:LethalCompanyDir="D:\Steam\steamapps\common\Lethal Company" `
  -p:BepInExDir="D:\path\to\profile\BepInEx"
```

The compiled DLL is emitted under the configured build output directory.

## Configuration

The mod exposes settings for:

- Enable or disable the mod.
- Maximum display distance.
- Hide status bars while the ship is in orbit.
- Head offset, UI scale, and per-bar Y offsets.
- Health and infection bar text visibility.
- Health, infection, and background color presets.
- Infection bar display mode: always visible or only visible when infected.

Most options apply at runtime. If LethalConfig is installed, Player Status Bars registers matching in-game controls through an optional reflection-based integration. If LethalConfig is not installed, all settings remain available through the generated BepInEx configuration file.

## Known limitations

This is a pure client-side mod. It can only display data that the local client can read from the game state. It does not add custom networking and does not attempt to synchronize hidden owner-only recovery values. Critical and low-health display is therefore based on the remote player state and conservative client-side fallback rules, with additional stabilization for late-join and ship-transition states.

## License

Player Status Bars is released under the MIT License. See `LICENSE` for the full license text.

## Third-party notices

Player Status Bars does not bundle, redistribute, or require LethalConfig, MoreCompany, or LTC Lobby Control.

- LethalConfig is an independent GPL-3.0 project. When installed by the user, Player Status Bars can integrate with it at runtime through reflection; this project does not compile against or include LethalConfig code.
- MoreCompany is an independent MIT-licensed project. Its player-slot expansion behavior was reviewed for compatibility hardening; this project does not include MoreCompany code.
- LTC Lobby Control is an independent MIT-licensed project. Its late-join behavior was reviewed for compatibility hardening; this project does not include LTC Lobby Control code.
