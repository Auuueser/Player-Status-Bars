using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class PlayerStatusBarManager : MonoBehaviour
{
	private readonly Dictionary<int, PlayerStatusBarView> trackedBars = new();

	private readonly HashSet<int> seenPlayerIds = new();

	private readonly List<int> staleIds = new();

	private float nextRefreshTime;

	private void Update()
	{
		if (!Plugin.Settings.Enabled)
		{
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
			ClearBars();
			return;
		}

		if (ShouldSkipBarsInOrbit(startOfRound))
		{
			ClearBars();
			return;
		}

		seenPlayerIds.Clear();
		PlayerControllerB[] allPlayers = startOfRound.allPlayerScripts;
		for (int i = 0; i < allPlayers.Length; i++)
		{
			PlayerControllerB player = allPlayers[i];
			if (!ShouldTrackPlayer(player, localPlayer, allPlayers))
			{
				continue;
			}

			int playerId = (int)player.playerClientId;
			seenPlayerIds.Add(playerId);
			if (trackedBars.ContainsKey(playerId))
			{
				continue;
			}

			GameObject viewObject = new($"OtherPlayerStatusBar_{playerId}");
			viewObject.transform.SetParent(transform, worldPositionStays: false);
			PlayerStatusBarView view = viewObject.AddComponent<PlayerStatusBarView>();
			if (!view.Initialize(player))
			{
				Destroy(viewObject);
				Plugin.Log.LogWarning($"Failed to create player status bar for playerId={playerId}.");
				continue;
			}

			trackedBars[playerId] = view;
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
	}

	private static bool ShouldTrackPlayer(PlayerControllerB player, PlayerControllerB localPlayer, PlayerControllerB[] allPlayers)
	{
		if (player == null || localPlayer == null)
		{
			return false;
		}

		if (player == localPlayer)
		{
			return false;
		}

		if (!player.isPlayerControlled || player.isPlayerDead)
		{
			return false;
		}

		if (player.health <= 0)
		{
			return false;
		}

		int playerId = (int)player.playerClientId;
		if (playerId < 0 || playerId >= allPlayers.Length || allPlayers[playerId] != player)
		{
			return false;
		}

		return player.gameObject.activeInHierarchy;
	}

	private static bool ShouldSkipBarsInOrbit(StartOfRound startOfRound)
	{
		return Plugin.Settings.HideInOrbit
			&& ((startOfRound.inShipPhase && !startOfRound.shipHasLanded) || startOfRound.shipIsLeaving);
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
	}

	private void ClearBars()
	{
		foreach (KeyValuePair<int, PlayerStatusBarView> pair in trackedBars)
		{
			if (pair.Value != null)
			{
				Destroy(pair.Value.gameObject);
			}
		}

		trackedBars.Clear();
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
}
