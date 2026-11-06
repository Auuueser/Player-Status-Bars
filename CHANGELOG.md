# Changelog

<details>
<summary><strong>中文</strong></summary>

<details>
<summary>0.3.0</summary>

- 新增十格样式。
- 修复中途加入的玩家显示上一位玩家的血量或重伤状态。
- 感染条改为由主机控制，需要主机安装此版本并开启。
- 配置精简，并支持 LethalConfig；安装 LC Chinese Project 时显示中文。

</details>

<details>
<summary>0.2.1</summary>

- 新增同版本状态同步，客户端本地的尸体生长感染状态可上报给主机，并同步给其他观察者。
- 改进主机端感染显示，解决被观察玩家的感染值只在其本人客户端准确的问题。
- 区分未知、确认无感染和感染中三种状态，减少治愈、死亡、复活和换回合后残留的感染条。
- 改进中途加入和回合切换时的清理：状态条关闭、轨道隐藏或等待游戏状态时，清除状态快照和感染缓存。
- 同步保持低开销，使用固定大小的数据、按槽位索引的缓存、变化时才上报的客户端报告，以及原有的主机快照节奏。

</details>

<details>
<summary>0.2.0</summary>

- 项目许可从 MIT 改为 GNU GPL v3.0。
- 新增低开销玩家刷新：分片扫描槽位、按槽位索引跟踪，并使用对象池复用状态条，适合大房间。
- 新增共享的尸体生长感染缓存，限制敌人查找次数，并在实例不存在时退避。
- 新增血量显示逐点变化，使血量平滑走向当前目标。
- 新增感染显示从 1% 到 99% 逐点变化，治愈和感染减少时立即下降。
- 改进原版低血量显示，在远端客户端数据过期时预测回血到 20 血。
- 改进过期的 20 血重伤状态处理，根据同步的重伤或流血状态推断原版倒地血量。
- 新增 `Critical Health Sync Mode`，提供 `VanillaPrediction` 和 `TrustRawHealthAt20` 两个选项，兼容主动流血和自定义重伤模组。
- 改进死亡、复活、回合切换和中途加入复用槽位时的血量与感染重置。
- 改进中途加入和扩展房间下的在线玩家检测，这些情况下仅靠 `isPlayerControlled` 不足以判断可见性。
- 改进低血量提示，目标血量低于 20 时血条立即变红。

</details>

<details>
<summary>0.1.3</summary>

- 将朝向相机的计算合并进状态条主更新，不再为每个状态条增加一个 `LateUpdate` 组件，降低每玩家每帧开销。
- 由父视图在数据或设置变化时才更新状态条，降低 UI 回调开销。
- 移除非调试模式下可见性热路径中的距离开方运算，真实距离只在调试日志通过节流后才计算。
- 重写尸体生长集成，改用直接的游戏类型和缓存的敌人查找，不再使用运行时反射、`GetType`、字段查找或全局对象搜索。
- 移除可选的 LethalConfig 反射集成，保持运行时无反射并降低开销。
- 新增结构性能测试，防止重新引入运行时反射、逐条回调、逐条朝向回调和热路径开方运算。

</details>

<details>
<summary>0.1.2</summary>

- 改进复用槽位的中途加入处理，新加入的玩家不再继承过期的重伤或低血量状态。
- 改进中途加入和扩展房间下槽位索引与玩家客户端 ID 不一致时的兼容性。
- 改进观战和自由视角的距离处理，视角靠近目标时状态条保持可见。
- 改进飞船状态检测，回合开始后才加入的客户端不再隐藏状态条。
- 将状态条管理放在常驻运行时对象上，场景切换和插件宿主变化后仍能继续创建和刷新。
- 防止已恢复的重伤玩家在远端客户端只收到恢复标记时一直显示 20 血。
- 飞船起飞过渡时隐藏状态条，避免回合结束时显示临时状态。
- 移除可选的 LethalConfig 刷新按钮，该按钮在缺少对应构造函数的版本上会产生启动警告。
- 缓存集成元数据和实例查找结果，降低可选的尸体生长查找开销。
- 新增可选调试日志，用于排查状态条创建、过滤、可见性和相机问题。

</details>

<details>
<summary>0.1.1</summary>

- 加固 MoreCompany 和 LTC Lobby Control 类型的槽位变化下的中途加入初始化。
- LethalConfig 支持改为可选的反射式运行时集成，移除编译期引用。
- 补充可选 LethalConfig 支持和已检查兼容性的中途加入模组的第三方许可说明。

</details>

<details>
<summary>0.1.0</summary>

- 新增其他玩家的世界空间状态条。
- 新增血量和感染显示。
- 新增通过尸体生长的 `infectionMeter` 读取感染值。
- 新增 BepInEx 配置和可选的 LethalConfig 集成。
- 新增显示距离限制和轨道阶段隐藏。
- 新增血条和感染条各自独立的 Y 偏移。
- 新增感染条显示模式。
- 新增同步的重伤和流血状态显示。
- 新增恢复数据未完全同步时的客户端低血量回退处理。
- 新增中途加入玩家的稳定处理，减少过期的重伤状态显示。
- 新增根据已安装的本地化插件选择中文或英文重伤文字。

</details>

</details>

<details>
<summary><strong>English</strong></summary>

<details>
<summary>0.3.0</summary>

- Added a segmented bar style.
- Fixed players joining mid-game showing the previous player's health or critical state.
- Infection bars are now controlled by the host, which must run this version with them turned on.
- Simplified the config and added LethalConfig support, in Chinese when LC Chinese Project is installed.

</details>

<details>
<summary>0.2.1</summary>

- Added compact same-version status synchronization so client-local Cadaver Growth infection state can be reported to the host and reflected for other observers.
- Improved host-side infection display when the observed player's infection meter is only accurate on that player's own client.
- Improved infection state handling by distinguishing unknown, confirmed clear, and active infection values, reducing stale infection bars after cure, death, revive, and round changes.
- Improved late-join and round-transition cleanup by clearing status snapshots and infection caches when bars are disabled, hidden in orbit, or waiting for game state.
- Kept the synchronization path low-overhead with fixed-size payloads, slot-indexed caches, change-based client reports, and the existing throttled host snapshot cadence.

</details>

<details>
<summary>0.2.0</summary>

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

</details>

<details>
<summary>0.1.3</summary>

- Reduced per-player frame overhead by folding billboard rotation into the main status bar view update instead of adding a second `LateUpdate` component per bar.
- Reduced UI callback overhead by applying status bar strip updates from the parent view only when data or settings are dirty.
- Removed non-debug distance square-root work from the visibility hot path; real distance is now formatted only after debug logging passes its throttle gate.
- Reworked Cadaver Growth integration to use direct game types and cached spawned enemy lookup instead of runtime reflection, `GetType`, field lookup, or object-wide searches.
- Removed optional LethalConfig reflection integration to keep runtime behavior reflection-free and lower overhead.
- Added structural performance tests that guard against reintroducing runtime reflection, per-strip callbacks, per-bar billboard callbacks, and hot-path distance square roots.

</details>

<details>
<summary>0.1.2</summary>

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

</details>

<details>
<summary>0.1.1</summary>

- Hardened late-join initialization for MoreCompany and LTC Lobby Control style player-slot changes.
- Changed LethalConfig support to optional reflection-based runtime integration and removed the compile-time LethalConfig reference.
- Documented third-party license notices for optional LethalConfig support and compatibility-reviewed late-join mods.

</details>

<details>
<summary>0.1.0</summary>

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

</details>

</details>
