using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class PlayerStatusBarManager : MonoBehaviour
{
	private const float DebugSummaryInterval = 5f;

	private const float ActiveRefreshInterval = 0.5f;

	private const float IdleRefreshInterval = 2.5f;

	private const int ScanSlotsPerRefresh = 4;

	private const int MinimumPlayerSlotCapacity = 16;

	private PlayerStatusBarView?[] trackedBarsBySlot = Array.Empty<PlayerStatusBarView?>();

	private PlayerStatusBarView?[] activeBars = Array.Empty<PlayerStatusBarView?>();

	private int activeBarCount;

	private int activePlayerSlotCount;

	private readonly Stack<PlayerStatusBarView> pooledBars = new();

	private float nextRefreshTime;

	private float nextDebugSummaryTime;

	private int refreshScanCursor;

	private void Start()
	{
		Plugin.LogDebug($"Runtime manager started active={gameObject.activeInHierarchy}.");
	}

	private void Update()
	{
		StatusBarConfig settings = Plugin.Settings;
		if (!settings.Enabled)
		{
			if (activeBarCount == 0 && Time.unscaledTime < nextRefreshTime)
			{
				return;
			}

			LogDebugBlocked("disabled");
			ClearBars();
			PlayerStatusSnapshotSync.ClearSnapshots();
			CadaverGrowthInfectionProvider.InvalidateAll();
			ScheduleNextRefresh(hasActiveBars: false);
			return;
		}

		StartOfRound? startOfRound = StartOfRound.Instance;
		PlayerControllerB? localPlayer = GameNetworkManager.Instance != null
			? GameNetworkManager.Instance.localPlayerController
			: startOfRound?.localPlayerController;
		PlayerControllerB[]? allPlayers = startOfRound?.allPlayerScripts;
		if (startOfRound == null || localPlayer == null || allPlayers == null)
		{
			if (Time.unscaledTime >= nextRefreshTime)
			{
				ScheduleNextRefresh(hasActiveBars: false);
				LogDebugBlocked($"not-ready startOfRound={startOfRound != null} localPlayer={localPlayer != null} allPlayers={allPlayers != null}");
				ClearBars();
				PlayerStatusSnapshotSync.ClearSnapshots();
				CadaverGrowthInfectionProvider.InvalidateAll();
			}

			return;
		}

		EnsureSlotCapacity(allPlayers.Length);
		bool canShowGroup = !ShouldSkipBarsForShipState(startOfRound);
		if (!canShowGroup)
		{
			if (Time.unscaledTime >= nextRefreshTime)
			{
				LogDebugBlocked(
					$"orbit-hidden inShipPhase={startOfRound.inShipPhase} shipHasLanded={startOfRound.shipHasLanded} shipDoorsEnabled={startOfRound.shipDoorsEnabled} shipIsLeaving={startOfRound.shipIsLeaving} dayStarted={TimeOfDay.Instance?.currentDayTimeStarted}");
			}

			ClearBars();
			PlayerStatusSnapshotSync.ClearSnapshots();
			CadaverGrowthInfectionProvider.InvalidateAll();
			ScheduleNextRefresh(hasActiveBars: false);
			return;
		}

		PlayerStatusSnapshotSync.Tick(startOfRound, allPlayers);
		if (activeBarCount == 0 && Time.unscaledTime < nextRefreshTime)
		{
			return;
		}

		if (Time.unscaledTime >= nextRefreshTime)
		{
			RefreshPlayerSlice(startOfRound, localPlayer, allPlayers);
		}

		if (activeBarCount == 0)
		{
			return;
		}

		PlayerStatusFrameContext frameContext = CreateFrameContext(settings, localPlayer, canShowGroup);
		TickTrackedBars(frameContext);
	}

	private void RefreshPlayerSlice(StartOfRound startOfRound, PlayerControllerB localPlayer, PlayerControllerB[] allPlayers)
	{
		bool logDetails = Plugin.Settings.DebugLogging && Time.unscaledTime >= nextDebugSummaryTime;
		int createdCount = 0;
		int existingCount = activeBarCount;
		int skippedCount = 0;
		if (allPlayers.Length == 0)
		{
			ClearBars();
			LogDebugSummary(startOfRound, localPlayer, 0, createdCount, existingCount, skippedCount);
			ScheduleNextRefresh(createdCount > 0 || existingCount > 0 || allPlayers.Length > ScanSlotsPerRefresh);
			return;
		}

		int slotsToScan = Mathf.Min(ScanSlotsPerRefresh, allPlayers.Length);
		for (int scanned = 0; scanned < slotsToScan; scanned++)
		{
			int slot = refreshScanCursor;
			refreshScanCursor = (refreshScanCursor + 1) % allPlayers.Length;
			PlayerControllerB player = allPlayers[slot];
			if (!ShouldTrackPlayer(startOfRound, player, localPlayer, slot, allPlayers, out string skipReason))
			{
				skippedCount++;
				if (logDetails)
				{
					LogDebugPlayerSkip(slot, player, skipReason);
				}
				RemoveBar(slot);
				continue;
			}

			int playerKey = ResolvePlayerKey(player, slot, allPlayers);
			PlayerStatusBarView? existingView = trackedBarsBySlot[slot];
			if (existingView != null && existingView.MatchesPlayer(player, localPlayer, playerKey))
			{
				continue;
			}

			if (existingView != null)
			{
				RemoveBar(slot);
			}

			PlayerStatusBarView? view = RentBarView(playerKey, player);
			if (view == null)
			{
				Plugin.Log.LogWarning($"Failed to create player status bar for playerKey={playerKey}.");
				continue;
			}

			trackedBarsBySlot[slot] = view;
			AddActiveBar(view);
			createdCount++;
			Plugin.LogDebug($"Created bar playerKey={playerKey} slot={slot} clientId={player.playerClientId} actualClientId={player.actualClientId} name='{player.playerUsername}' health={player.health} active={player.gameObject.activeInHierarchy}.");
		}

		LogDebugSummary(startOfRound, localPlayer, allPlayers.Length, createdCount, existingCount, skippedCount);
		ScheduleNextRefresh(createdCount > 0 || existingCount > 0 || allPlayers.Length > ScanSlotsPerRefresh);
	}

	private void EnsureSlotCapacity(int slotCount)
	{
		if (slotCount < activePlayerSlotCount)
		{
			ReleaseBarsFrom(slotCount);
		}

		activePlayerSlotCount = slotCount;
		if (slotCount == 0 || refreshScanCursor >= slotCount)
		{
			refreshScanCursor = 0;
		}

		if (trackedBarsBySlot.Length >= slotCount)
		{
			return;
		}

		int previousCapacity = trackedBarsBySlot.Length;
		PlayerStatusBarView?[] resizedBars = new PlayerStatusBarView?[GrowSlotCapacity(slotCount)];
		if (previousCapacity > 0)
		{
			Array.Copy(trackedBarsBySlot, resizedBars, previousCapacity);
		}

		trackedBarsBySlot = resizedBars;
	}

	private static int GrowSlotCapacity(int requiredCapacity)
	{
		int capacity = MinimumPlayerSlotCapacity;
		while (capacity < requiredCapacity)
		{
			capacity *= 2;
		}

		return capacity;
	}

	private void ScheduleNextRefresh(bool hasActiveBars)
	{
		nextRefreshTime = Time.unscaledTime + (hasActiveBars ? ActiveRefreshInterval : IdleRefreshInterval);
	}

	private PlayerStatusBarView? RentBarView(int playerKey, PlayerControllerB player)
	{
		PlayerStatusBarView view;
		if (pooledBars.Count > 0)
		{
			view = pooledBars.Pop();
			view.transform.SetParent(transform, worldPositionStays: false);
			view.name = $"OtherPlayerStatusBar_{playerKey}";
			view.Bind(player, playerKey);
			return view;
		}

		GameObject viewObject = new($"OtherPlayerStatusBar_{playerKey}");
		viewObject.transform.SetParent(transform, worldPositionStays: false);
		view = viewObject.AddComponent<PlayerStatusBarView>();
		if (view.Initialize(player, playerKey))
		{
			return view;
		}

		Destroy(viewObject);
		return null;
	}

	private void ReleaseBarView(PlayerStatusBarView view)
	{
		view.Release();
		view.transform.SetParent(transform, worldPositionStays: false);
		pooledBars.Push(view);
	}

	private void TickTrackedBars(in PlayerStatusFrameContext frameContext)
	{
		for (int i = 0; i < activeBarCount; i++)
		{
			PlayerStatusBarView? view = activeBars[i];
			if (view != null)
			{
				view.Tick(frameContext);
			}
		}
	}

	private void AddActiveBar(PlayerStatusBarView view)
	{
		EnsureActiveBarCapacity(activeBarCount + 1);
		activeBars[activeBarCount] = view;
		activeBarCount++;
	}

	private void RemoveActiveBar(PlayerStatusBarView view)
	{
		for (int i = 0; i < activeBarCount; i++)
		{
			if (activeBars[i] != view)
			{
				continue;
			}

			int lastIndex = activeBarCount - 1;
			activeBars[i] = activeBars[lastIndex];
			activeBars[lastIndex] = null;
			activeBarCount = lastIndex;
			return;
		}
	}

	private void EnsureActiveBarCapacity(int requiredCapacity)
	{
		if (activeBars.Length >= requiredCapacity)
		{
			return;
		}

		PlayerStatusBarView?[] resizedBars = new PlayerStatusBarView?[GrowSlotCapacity(requiredCapacity)];
		if (activeBarCount > 0)
		{
			Array.Copy(activeBars, resizedBars, activeBarCount);
		}

		activeBars = resizedBars;
	}

	private static PlayerStatusFrameContext CreateFrameContext(StatusBarConfig settings, PlayerControllerB localPlayer, bool canShowGroup)
	{
		Camera? viewCamera = null;
		bool hasBillboardRotation = false;
		Quaternion billboardRotation = Quaternion.identity;
		Vector3 observerPosition = localPlayer.transform.position;
		if (canShowGroup)
		{
			viewCamera = StatusBarBillboard.ResolveViewCamera();
			hasBillboardRotation = StatusBarBillboard.TryResolveBillboardRotation(viewCamera, out billboardRotation);
			observerPosition = ResolveObserverPosition(localPlayer, viewCamera);
		}

		return new PlayerStatusFrameContext(
			settings,
			localPlayer,
			viewCamera,
			hasBillboardRotation,
			billboardRotation,
			canShowGroup,
			observerPosition);
	}

	private static Vector3 ResolveObserverPosition(PlayerControllerB localPlayer, Camera? viewCamera)
	{
		if (viewCamera != null)
		{
			return viewCamera.transform.position;
		}

		PlayerControllerB? spectatedPlayer = localPlayer.spectatedPlayerScript;
		if (localPlayer.isPlayerDead && spectatedPlayer != null && !spectatedPlayer.isPlayerDead)
		{
			return spectatedPlayer.transform.position;
		}

		return localPlayer.transform.position;
	}

	private static bool ShouldTrackPlayer(StartOfRound startOfRound, PlayerControllerB player, PlayerControllerB localPlayer, int slot, PlayerControllerB[] allPlayers, out string skipReason)
	{
		if (player == null || localPlayer == null)
		{
			skipReason = "null-player-or-local";
			return false;
		}

		if (player == localPlayer)
		{
			skipReason = "local-player";
			return false;
		}

		if (!player.gameObject.activeInHierarchy)
		{
			skipReason = "inactive-gameobject";
			return false;
		}

		int playerKey = ResolvePlayerKey(player, slot, allPlayers);
		if (!IsConnectedPlayerSlot(startOfRound, player, playerKey))
		{
			skipReason = "not-connected";
			return false;
		}

		skipReason = string.Empty;
		return true;
	}

	private static bool IsConnectedPlayerSlot(StartOfRound startOfRound, PlayerControllerB player, int playerKey)
	{
		if (startOfRound.ClientPlayerList.TryGetValue(player.actualClientId, out int mappedSlot))
		{
			return mappedSlot == playerKey;
		}

		return player.isPlayerControlled && !player.disconnectedMidGame;
	}

	private static int ResolvePlayerKey(PlayerControllerB player, int slotIndex, PlayerControllerB[] allPlayers)
	{
		int playerId = (int)player.playerClientId;
		if (playerId >= 0 && playerId < allPlayers.Length && allPlayers[playerId] == player)
		{
			return playerId;
		}

		return slotIndex;
	}

	internal static bool ShouldSkipBarsForShipState(StartOfRound startOfRound)
	{
		if (!Plugin.Settings.HideInOrbit)
		{
			return false;
		}

		if (startOfRound.shipIsLeaving)
		{
			return true;
		}

		if (TimeOfDay.Instance != null && TimeOfDay.Instance.currentDayTimeStarted)
		{
			return false;
		}

		return startOfRound.inShipPhase && !startOfRound.shipHasLanded;
	}

	private void RemoveBar(int playerId)
	{
		if (playerId < 0 || playerId >= trackedBarsBySlot.Length)
		{
			return;
		}

		PlayerStatusBarView? view = trackedBarsBySlot[playerId];
		if (view == null)
		{
			return;
		}

		trackedBarsBySlot[playerId] = null;
		RemoveActiveBar(view);
		if (view != null)
		{
			ReleaseBarView(view);
		}
		Plugin.LogDebug($"Removed bar playerKey={playerId}.");
	}

	private void ClearBars()
	{
		int clearedCount = activeBarCount;
		for (int i = 0; i < trackedBarsBySlot.Length; i++)
		{
			PlayerStatusBarView? view = trackedBarsBySlot[i];
			if (view != null)
			{
				trackedBarsBySlot[i] = null;
				ReleaseBarView(view);
			}
		}

		Array.Clear(activeBars, 0, activeBarCount);
		activeBarCount = 0;
		if (clearedCount > 0)
		{
			CadaverGrowthInfectionProvider.InvalidateAll();
			Plugin.LogDebug($"Cleared all bars count={clearedCount}.");
		}
	}

	private void ReleaseBarsFrom(int firstSlot)
	{
		for (int i = firstSlot; i < trackedBarsBySlot.Length; i++)
		{
			RemoveBar(i);
		}
	}

	private void OnDestroy()
	{
		Plugin.LogDebug($"Runtime manager destroyed tracked={activeBarCount}.");
		PlayerStatusSnapshotSync.ClearSnapshots();
		CadaverGrowthInfectionProvider.InvalidateAll();
		DestroyAllBars();
	}

	private void DestroyAllBars()
	{
		for (int i = 0; i < trackedBarsBySlot.Length; i++)
		{
			PlayerStatusBarView? view = trackedBarsBySlot[i];
			if (view != null)
			{
				Destroy(view.gameObject);
			}
		}

		trackedBarsBySlot = Array.Empty<PlayerStatusBarView?>();
		activeBars = Array.Empty<PlayerStatusBarView?>();
		activeBarCount = 0;
		while (pooledBars.Count > 0)
		{
			PlayerStatusBarView view = pooledBars.Pop();
			if (view != null)
			{
				Destroy(view.gameObject);
			}
		}
	}

	private void OnEnable()
	{
		Plugin.LogDebug("Runtime manager enabled.");
		Plugin.Settings.SettingsChanged += HandleSettingsChanged;
	}

	private void OnDisable()
	{
		Plugin.LogDebug($"Runtime manager disabled tracked={activeBarCount}.");
		Plugin.Settings.SettingsChanged -= HandleSettingsChanged;
	}

	private void HandleSettingsChanged()
	{
		nextRefreshTime = 0f;
	}

	private void LogDebugBlocked(string reason)
	{
		if (!Plugin.Settings.DebugLogging || Time.unscaledTime < nextDebugSummaryTime)
		{
			return;
		}

		nextDebugSummaryTime = Time.unscaledTime + DebugSummaryInterval;
		Plugin.LogDebug($"Refresh blocked reason={reason} tracked={activeBarCount}.");
	}

	private static void LogDebugPlayerSkip(int slot, PlayerControllerB player, string reason)
	{
		if (!Plugin.Settings.DebugLogging || reason == "local-player")
		{
			return;
		}

		Plugin.LogDebug($"Skipped slot={slot} reason={reason} clientId={(player != null ? player.playerClientId.ToString() : "null")} name='{(player != null ? player.playerUsername : "null")}' controlled={(player != null && player.isPlayerControlled)} dead={(player != null && player.isPlayerDead)} health={(player != null ? player.health : -1)} active={(player != null && player.gameObject.activeInHierarchy)}.");
	}

	private void LogDebugSummary(StartOfRound startOfRound, PlayerControllerB localPlayer, int playerSlots, int createdCount, int existingCount, int skippedCount)
	{
		if (!Plugin.Settings.DebugLogging || Time.unscaledTime < nextDebugSummaryTime)
		{
			return;
		}

		nextDebugSummaryTime = Time.unscaledTime + DebugSummaryInterval;
		Camera? viewCamera = StatusBarBillboard.ResolveViewCamera();
		Plugin.LogDebug($"Refresh summary slots={playerSlots} tracked={activeBarCount} created={createdCount} existing={existingCount} skipped={skippedCount} localClient={localPlayer.playerClientId} inShipPhase={startOfRound.inShipPhase} shipHasLanded={startOfRound.shipHasLanded} shipDoorsEnabled={startOfRound.shipDoorsEnabled} shipIsLeaving={startOfRound.shipIsLeaving} dayStarted={TimeOfDay.Instance?.currentDayTimeStarted} camera='{(viewCamera != null ? viewCamera.name : "none")}'.");
	}
}
