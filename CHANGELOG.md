# Changelog

All notable changes to this project are documented here.

## 0.2.0

- Changed the project license from MIT to the GNU General Public License v3.0.
- Added low-overhead player refresh with sliced slot scanning, slot-indexed tracking, and pooled status bar views for larger lobbies.
- Added shared Cadaver Growth infection caching with bounded spawned-enemy lookup and absent-instance backoff.
- Added one-point health display stepping so health changes move smoothly toward the current target.
- Added one-percent infection display stepping from 1% to 99%, with immediate downward updates for cure and infection reduction.
- Improved vanilla low-health display by predicting recovery toward 20 HP when remote clients expose stale low-health values.
- Improved stale 20 HP critical-state handling by inferring vanilla downed health when synced critical or bleeding state indicates a low-health transition.
- Added `Critical Health Sync Mode` with `VanillaPrediction` and `TrustRawHealthAt20` options for active-bleed and custom-injury mod compatibility.
- Improved health and infection reset behavior across death, revive, round transitions, and late-join slot reuse.
- Improved connected-player detection for late-join and extended-lobby scenarios where `isPlayerControlled` is not a sufficient visibility signal.
- Improved low-health visual feedback by switching the health bar to the red low-health state as soon as the target health is below 20 HP.

## 0.1.3

- Reduced per-player frame overhead by folding billboard rotation into the main status bar view update instead of adding a second `LateUpdate` component per bar.
- Reduced UI callback overhead by applying status bar strip updates from the parent view only when data or settings are dirty.
- Removed non-debug distance square-root work from the visibility hot path; real distance is now formatted only after debug logging passes its throttle gate.
- Reworked Cadaver Growth integration to use direct game types and cached spawned enemy lookup instead of runtime reflection, `GetType`, field lookup, or object-wide searches.
- Removed optional LethalConfig reflection integration to keep runtime behavior reflection-free and lower overhead.
- Added structural performance tests that guard against reintroducing runtime reflection, per-strip callbacks, per-bar billboard callbacks, and hot-path distance square roots.

## 0.1.2

- Improved late-join handling for reused player slots so newly joined players no longer inherit stale critical or low-health bar states.
- Improved compatibility with late-join and extended-lobby player slots where the player slot index can differ from the reported player client id.
- Improved spectator and freecam distance handling so player status bars remain visible when the active view is near the target.
- Improved ship-state detection so status bars are not hidden when a client joins after the round has already started.
- Kept status bar management on a persistent runtime host so bar creation and refresh continue across scene transitions and plugin-host lifecycle changes.
- Prevented recovered critical-health players from remaining displayed as 20 HP when remote clients only receive the recovery flag update.
- Hid player status bars during the ship takeoff transition to avoid showing temporary player state while the round is closing.
- Removed an optional LethalConfig refresh button that caused a startup warning on LethalConfig versions without the matching button constructor.
- Reduced optional Cadaver Growth lookup overhead by caching integration metadata and instance discovery across player bars.
- Added optional debug logging for status bar creation, filtering, visibility, and camera diagnostics.

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
