# Player Status Bars

Player Status Bars is a BepInEx mod for Lethal Company. It displays compact world-space status bars above other players, showing health and Cadaver Growth infection state with a low-overhead runtime model.

The mod can run from a single client using locally readable game state. When the host and clients install the same version, it also uses a small status synchronization path to correct client-local infection values for other observers.

The project is designed for low runtime overhead. Status bars are updated from a single manager, player discovery is sliced across refresh intervals, player views are pooled, UI writes are applied only when values change, and Cadaver Growth data is read through direct typed game state with a shared cache.

## Features

- Displays health and infection bars above other players.
- Excludes the local player.
- Uses a world-space UI that follows players and faces the active camera.
- Supports the active gameplay camera, spectator camera, and freecam camera as visibility references.
- Hides bars by distance and, by default, while the ship is still in orbit.
- Keeps connected players tracked while dead so revive state can reset cleanly.
- Hides bars during ship takeoff and round closing transitions.
- Supports late-join and extended-lobby slot mappings without relying only on `isPlayerControlled`.
- Reads Cadaver Growth infection values directly from `CadaverGrowthAI.playerInfections`.
- Synchronizes client-local infection state through compact same-version status messages when multiple players have the mod installed.
- Shows infection progress from 1% to 99% with one-percent display stepping.
- Clears stale infection display state on cure, death, revive, and unavailable Cadaver Growth data.
- Displays numeric health and highlights low health in red.
- Smoothly steps health display one point at a time toward the current target.
- Predicts vanilla low-health recovery toward 20 HP when remote clients expose stale low-health values.
- Includes a compatibility mode for 20 HP critical-state synchronization.
- Provides BepInEx configuration for layout, distance, text, colors, infection visibility, compatibility behavior, and debug logging.

## Performance Model

The runtime avoids expensive discovery and allocation patterns on the normal per-frame path:

- No runtime reflection, `GetType` field access, or object-wide Unity searches are used by production code.
- One persistent manager ticks active bars instead of adding independent per-bar update components.
- Player refresh work is sliced across a small number of slots per refresh.
- Tracked players use slot-indexed arrays rather than hash collections.
- Status bar views are pooled and reused.
- Distance checks use squared distance on the hot path.
- Settings that are used every frame are cached when configuration changes.
- Text labels are prebuilt for common health and infection values.
- UI graphics are marked dirty and applied only when display values or settings change.
- Cadaver Growth lookup is cached and backs off while the enemy instance is absent.
- Multiplayer status synchronization uses fixed-size payloads, slot-indexed caches, change-based client infection reports, and a throttled host snapshot cadence.

## Requirements

- Lethal Company
- BepInEx 5
- Cadaver Growth is optional. If it is not present, health display remains available and infection values are safely unavailable.

## Installation

1. Build or download `PlayerStatusBars.dll`.
2. Place the DLL in the target profile's `BepInEx/plugins/PlayerStatusBars` folder.
3. Start the game once to generate `BepInEx/config/playerstatusbars.cfg`.
4. Adjust settings through the generated configuration file as needed.

## Build

The project references game and BepInEx assemblies from a local Lethal Company installation. The project file exposes MSBuild properties so local paths do not need to be committed.

Example:

```powershell
dotnet build PlayerStatusBars.csproj -c Release `
  -p:LethalCompanyDir="D:\Steam\steamapps\common\Lethal Company" `
  -p:BepInExDir="D:\path\to\profile\BepInEx"
```

The compiled DLL is emitted under `bin/Release/netstandard2.1`.

## Configuration

The generated BepInEx config file is `BepInEx/config/playerstatusbars.cfg`.

Available settings include:

- Enable or disable the mod.
- Maximum display distance.
- Hide status bars while the ship is in orbit.
- Head offset, UI scale, and per-bar Y offsets.
- Health and infection text visibility.
- Health, infection, and background color presets.
- Infection bar display mode: always visible or only visible when infected.
- Critical health sync mode:
  - `VanillaPrediction` keeps the default vanilla-focused fallback for stale 20 HP critical-state synchronization.
  - `TrustRawHealthAt20` trusts the reported 20 HP value for modded active-bleed or custom-injury behavior.
- Debug logging for status bar creation, filtering, visibility, and camera diagnostics.

Most options apply at runtime through the generated configuration file.

## Compatibility Notes

The mod remains usable as a client-side display mod, but some remote values in Lethal Company can be stale or owner-local. When only one player installs the mod, display accuracy is limited to the game state that client can read. When the host and clients install the same version, the mod can exchange compact status messages to improve infection display consistency for observers.

For health, the display combines synced raw health, synced critical or bleeding state, damage timestamp changes, and conservative vanilla recovery prediction. This improves common vanilla low-health cases while keeping a compatibility option for mods that intentionally set bleeding or injury states without changing health.

For infection, the display depends on Cadaver Growth's `playerInfections` data. In multiplayer, same-version clients can report their locally accurate Cadaver Growth infection state to the host, which then reflects that state to other same-version observers. Mods that implement a separate infection system without updating Cadaver Growth state remain outside the readable data surface of this mod.

## Package Layout

Repository layout:

```text
src/
  Runtime source files
PlayerStatusBars.csproj
README.md
CHANGELOG.md
LICENSE
```

Thunderstore package layout:

```text
manifest.json
README.md
CHANGELOG.md
LICENSE
icon.png
BepInEx/plugins/PlayerStatusBars/PlayerStatusBars.dll
```

## License

Player Status Bars is released under the GNU General Public License v3.0. See `LICENSE` for the full license text.

## Third-party Notices

Player Status Bars does not bundle, redistribute, or require MoreCompany or LTC Lobby Control.

- MoreCompany is an independent MIT-licensed project. Its player-slot expansion behavior was reviewed for compatibility hardening; this project does not include MoreCompany code.
- LTC Lobby Control is an independent MIT-licensed project. Its late-join behavior was reviewed for compatibility hardening; this project does not include LTC Lobby Control code.
