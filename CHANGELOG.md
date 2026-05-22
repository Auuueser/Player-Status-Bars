# Changelog

All notable changes to this project are documented here.

## 0.1.2

- Improved late-join handling for reused player slots so newly joined players no longer inherit stale critical or low-health bar states.
- Improved compatibility with late-join and extended-lobby player slots where the player slot index can differ from the reported player client id.
- Improved spectator and freecam distance handling so player status bars remain visible when the active view is near the target.
- Prevented recovered critical-health players from remaining displayed as 20 HP when remote clients only receive the recovery flag update.
- Hid player status bars during the ship takeoff transition to avoid showing temporary player state while the round is closing.
- Removed an optional LethalConfig refresh button that caused a startup warning on LethalConfig versions without the matching button constructor.
- Reduced optional Cadaver Growth lookup overhead by caching integration metadata and instance discovery across player bars.

## 0.1.1

- Hardened late-join initialization for MoreCompany and LTC Lobby Control style player-slot changes.
- Changed LethalConfig support to optional reflection-based runtime integration and removed the compile-time LethalConfig reference.
- Documented third-party license notices for optional LethalConfig support and compatibility-reviewed late-join mods.

## 0.1.0

- Added world-space status bars for other players.
- Added health and infection display.
- Added infection reading through Cadaver Growth's `infectionMeter` value when available.
- Added BepInEx configuration and optional LethalConfig integration.
- Added display distance limiting and orbit-phase hiding.
- Added independent Y offsets for health and infection bars.
- Added infection bar display modes.
- Added critical health display for synced critical injury and bleeding states.
- Added client-side low-health fallback handling for cases where recovery data is not fully synchronized.
- Added stabilization handling for late-joining players to reduce stale critical-state display.
- Added Chinese or English critical-state text selection based on installed localization plugins.
