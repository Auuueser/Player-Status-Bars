# Changelog

All notable changes to this project are documented here.

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
