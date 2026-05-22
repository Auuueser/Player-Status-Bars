using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class PlayerStatusBarManager : MonoBehaviour
{
	private const float DebugSummaryInterval = 5f;

	private readonly Dictionary<int, PlayerStatusBarView> trackedBars = new();

	private readonly HashSet<int> seenPlayerIds = new();

	private readonly List<int> staleIds = new();

	private float nextRefreshTime;

	private float nextDebugSummaryTime;

	private void Update()
	{
		if (!Plugin.Settings.Enabled)
		{
			LogDebugBlocked("disabled");
			ClearBars();
			return;
		}

		if (Time.unscaledTime < nextRefreshTime)
		{
			return;
		}

		nextRefreshTime = Time.unscaledTime + 0.5f;
		RefreshBars();
	}

	private void RefreshBars()
	{
		StartOfRound? startOfRound = StartOfRound.Instance;
		PlayerControllerB? localPlayer = GameNetworkManager.Instance != null
			? GameNetworkManager.Instance.localPlayerController
			: startOfRound?.localPlayerController;

		if (startOfRound == null || localPlayer == null || startOfRound.allPlayerScripts == null)
		{
			LogDebugBlocked($"not-ready startOfRound={startOfRound != null} localPlayer={localPlayer != null} allPlayers={startOfRound?.allPlayerScripts != null}");
			ClearBars();
			return;
		}

		if (ShouldSkipBarsForShipState(startOfRound))
		{
			LogDebugBlocked($"orbit-hidden inShipPhase={startOfRound.inShipPhase} shipHasLanded={startOfRound.shipHasLanded} shipDoorsEnabled={startOfRound.shipDoorsEnabled} shipIsLeaving={startOfRound.shipIsLeaving} dayStarted={TimeOfDay.Instance?.currentDayTimeStarted}");
			ClearBars();
			return;
		}

		seenPlayerIds.Clear();
		PlayerControllerB[] allPlayers = startOfRound.allPlayerScripts;
		bool logDetails = Plugin.Settings.DebugLogging && Time.unscaledTime >= nextDebugSummaryTime;
		int createdCount = 0;
		int existingCount = 0;
		int skippedCount = 0;
		for (int i = 0; i < allPlayers.Length; i++)
		{
			PlayerControllerB player = allPlayers[i];
			if (!ShouldTrackPlayer(player, localPlayer, out string skipReason))
			{
				skippedCount++;
				if (logDetails)
				{
					LogDebugPlayerSkip(i, player, skipReason);
				}
				continue;
			}

			int playerKey = ResolvePlayerKey(player, i, allPlayers);
			seenPlayerIds.Add(playerKey);
			if (trackedBars.ContainsKey(playerKey))
			{
				existingCount++;
				continue;
			}

			GameObject viewObject = new($"OtherPlayerStatusBar_{playerKey}");
			viewObject.transform.SetParent(transform, worldPositionStays: false);
			PlayerStatusBarView view = viewObject.AddComponent<PlayerStatusBarView>();
			if (!view.Initialize(player))
			{
				Destroy(viewObject);
				Plugin.Log.LogWarning($"Failed to create player status bar for playerKey={playerKey}.");
				continue;
			}

			trackedBars[playerKey] = view;
			createdCount++;
			Plugin.LogDebug($"Created bar playerKey={playerKey} slot={i} clientId={player.playerClientId} name='{player.playerUsername}' health={player.health} active={player.gameObject.activeInHierarchy}.");
		}

		staleIds.Clear();
		foreach (KeyValuePair<int, PlayerStatusBarView> pair in trackedBars)
		{
			if (!seenPlayerIds.Contains(pair.Key) || pair.Value == null || !pair.Value.IsStillValid(localPlayer))
			{
				staleIds.Add(pair.Key);
			}
		}

		for (int i = 0; i < staleIds.Count; i++)
		{
			RemoveBar(staleIds[i]);
		}

		LogDebugSummary(startOfRound, localPlayer, allPlayers.Length, createdCount, existingCount, skippedCount);
	}

	private static bool ShouldTrackPlayer(PlayerControllerB player, PlayerControllerB localPlayer, out string skipReason)
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

		if (!player.isPlayerControlled)
		{
			skipReason = "not-controlled";
			return false;
		}

		if (player.isPlayerDead)
		{
			skipReason = "dead";
			return false;
		}

		if (player.health <= 0)
		{
			skipReason = "non-positive-health";
			return false;
		}

		if (!player.gameObject.activeInHierarchy)
		{
			skipReason = "inactive-gameobject";
			return false;
		}

		skipReason = string.Empty;
		return true;
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

		if (startOfRound.shipDoorsEnabled)
		{
			return false;
		}

		return startOfRound.inShipPhase && !startOfRound.shipHasLanded;
	}

	private void RemoveBar(int playerId)
	{
		if (!trackedBars.TryGetValue(playerId, out PlayerStatusBarView? view))
		{
			return;
		}

		trackedBars.Remove(playerId);
		if (view != null)
		{
			Destroy(view.gameObject);
		}
		Plugin.LogDebug($"Removed bar playerKey={playerId}.");
	}

	private void ClearBars()
	{
		int clearedCount = trackedBars.Count;
		foreach (KeyValuePair<int, PlayerStatusBarView> pair in trackedBars)
		{
			if (pair.Value != null)
			{
				Destroy(pair.Value.gameObject);
			}
		}

		trackedBars.Clear();
		if (clearedCount > 0)
		{
			Plugin.LogDebug($"Cleared all bars count={clearedCount}.");
		}
	}

	private void OnDestroy()
	{
		ClearBars();
	}

	private void OnEnable()
	{
		Plugin.Settings.SettingsChanged += HandleSettingsChanged;
	}

	private void OnDisable()
	{
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
		Plugin.LogDebug($"Refresh blocked reason={reason} tracked={trackedBars.Count}.");
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
		Plugin.LogDebug($"Refresh summary slots={playerSlots} tracked={trackedBars.Count} created={createdCount} existing={existingCount} skipped={skippedCount} localClient={localPlayer.playerClientId} inShipPhase={startOfRound.inShipPhase} shipHasLanded={startOfRound.shipHasLanded} shipDoorsEnabled={startOfRound.shipDoorsEnabled} shipIsLeaving={startOfRound.shipIsLeaving} dayStarted={TimeOfDay.Instance?.currentDayTimeStarted} camera='{(viewCamera != null ? viewCamera.name : "none")}'.");
	}
}
