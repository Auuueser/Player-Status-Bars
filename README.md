# Player Status Bars

Player Status Bars is a client-side BepInEx mod for Lethal Company. It displays compact world-space status bars above other players, showing health and Cadaver infection information without requiring server-side installation.

## Features

- Displays health and infection bars above other players.
- Excludes the local player.
- Uses a world-space UI that follows the player and faces the local camera.
- Reads infection values from `CadaverGrowthAI.playerInfections[playerId].infectionMeter` when Cadaver Growth is present.
- Supports BepInEx configuration and LethalConfig runtime adjustment.
- Includes distance-based visibility, orbit-phase hiding, infection bar display modes, and configurable layout offsets.
- Shows a critical health state for players reported as critically injured, bleeding heavily, or persistently below the low-health threshold.
- Uses a short stabilization window to reduce stale critical-state display for players joining mid-session through late-join mods.
- Uses Chinese critical text when a Chinese localization or translation plugin is detected; otherwise it uses English text.

## Requirements

- Lethal Company
- BepInEx 5
- LethalConfig
- Cadaver Growth is optional. If it is not present, health bars continue to work and infection values are safely unavailable.

## Installation

1. Build or download `PlayerStatusBars.dll`.
2. Place the DLL in the target profile's `BepInEx/plugins` folder.
3. Start the game once to generate the BepInEx configuration file.
4. Adjust settings through the generated config file or LethalConfig.

## Build

The project references game and mod assemblies from a local Lethal Company installation. The project file exposes MSBuild properties so local paths do not need to be committed.

Example:

```powershell
dotnet build PlayerStatusBars.csproj -c Release `
  -p:LethalCompanyDir="D:\Steam\steamapps\common\Lethal Company" `
  -p:BepInExDir="D:\path\to\profile\BepInEx" `
  -p:LethalConfigDir="D:\path\to\profile\BepInEx\plugins\AinaVT-LethalConfig\LethalConfig"
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

Most options are applied at runtime through LethalConfig.

## Known limitations

This is a pure client-side mod. It can only display data that the local client can read from the game state. It does not add custom networking and does not attempt to synchronize hidden owner-only recovery values. Critical health display is therefore based on the remote player state and conservative client-side fallback rules.

## License

Player Status Bars is released under the MIT License. See `LICENSE` for the full license text.

LethalConfig is an independent third-party dependency used to expose runtime configuration controls. The LethalConfig project is published under GPL-3.0; see the LethalConfig project and package pages for its license terms.
