using System;
using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal static class CadaverGrowthInfectionProvider
{
	private const float SampleInterval = 0.5f;

	private const float AbsentInstanceScanIntervalMin = 1f;

	private const float AbsentInstanceScanIntervalMax = 10f;

	private const float CachedInstanceMembershipCheckInterval = 2f;

	private const float InfectionMeterEpsilon = 0.0001f;

	private const int MinimumInfectionCacheCapacity = 16;

	private static readonly SharedInfectionCache sharedInfectionCache = new();

	private static CadaverGrowthAI? cachedCadaverGrowthInstance;

	private static float nextInstanceScanTime;

	private static float nextSampleTime;

	private static float nextCachedInstanceMembershipCheckTime;

	private static float currentAbsentInstanceScanInterval = AbsentInstanceScanIntervalMin;

	public static bool TryGetNormalizedInfection(PlayerControllerB player, out float infectionMeter)
	{
		return TryGetNormalizedInfection(player, -1, out infectionMeter);
	}

	public static bool TryGetNormalizedInfection(PlayerControllerB player, int knownPlayerSlot, out float infectionMeter)
	{
		bool hasKnownState = TryGetInfectionState(player, knownPlayerSlot, out bool hasInfection, out infectionMeter);
		return hasKnownState && hasInfection;
	}

	public static bool TryGetInfectionState(PlayerControllerB player, int knownPlayerSlot, out bool hasInfection, out float infectionMeter)
	{
		infectionMeter = 0f;
		hasInfection = false;
		if (player == null)
		{
			return false;
		}

		RefreshCache();
		return TryResolveInfectionIndex(player, knownPlayerSlot, out int infectionIndex)
			&& sharedInfectionCache.TryGet(infectionIndex, player, out hasInfection, out infectionMeter);
	}

	private static void RefreshCache()
	{
		if (Time.unscaledTime < nextSampleTime)
		{
			return;
		}

		nextSampleTime = Time.unscaledTime + SampleInterval;
		CadaverGrowthAI? cadaverGrowth = ResolveCadaverGrowthInstance();
		PlayerInfection[]? playerInfections = cadaverGrowth?.playerInfections;
		if (playerInfections == null)
		{
			sharedInfectionCache.Clear();
			return;
		}

		PlayerControllerB[]? allPlayers = StartOfRound.Instance?.allPlayerScripts;
		if (allPlayers == null || allPlayers.Length != playerInfections.Length)
		{
			sharedInfectionCache.Clear();
			return;
		}

		sharedInfectionCache.EnsureCapacity(playerInfections.Length);
		for (int i = 0; i < playerInfections.Length; i++)
		{
			PlayerControllerB player = allPlayers[i];
			PlayerInfection infection = playerInfections[i];
			if (player == null || infection == null)
			{
				sharedInfectionCache.Set(i, false, 0f);
				continue;
			}

			float normalizedMeter = Mathf.Clamp01(infection.infectionMeter);
			bool hasInfection = infection.infected && normalizedMeter > InfectionMeterEpsilon;
			if (!hasInfection)
			{
				sharedInfectionCache.Set(i, player, false, 0f);
				continue;
			}

			sharedInfectionCache.Set(i, player, true, normalizedMeter);
		}
	}

	private static CadaverGrowthAI? ResolveCadaverGrowthInstance()
	{
		if (cachedCadaverGrowthInstance != null)
		{
			if (IsCachedCadaverGrowthInstanceValid(cachedCadaverGrowthInstance))
			{
				return cachedCadaverGrowthInstance;
			}

			ClearCachedCadaverGrowthInstance();
		}

		if (Time.unscaledTime < nextInstanceScanTime)
		{
			return null;
		}

		nextInstanceScanTime = Time.unscaledTime + currentAbsentInstanceScanInterval;
		if (RoundManager.Instance == null)
		{
			IncreaseAbsentInstanceScanBackoff();
			return null;
		}

		for (int i = 0; i < RoundManager.Instance.SpawnedEnemies.Count; i++)
		{
			if (RoundManager.Instance.SpawnedEnemies[i] is CadaverGrowthAI cadaverGrowth
				&& IsCadaverGrowthInstanceUsable(cadaverGrowth))
			{
				cachedCadaverGrowthInstance = cadaverGrowth;
				ResetAbsentInstanceScanBackoff();
				return cachedCadaverGrowthInstance;
			}
		}

		IncreaseAbsentInstanceScanBackoff();
		return null;
	}

	private static bool IsCadaverGrowthInstanceValid(CadaverGrowthAI? cadaverGrowth)
	{
		return IsCadaverGrowthInstanceUsable(cadaverGrowth)
			&& IsCadaverGrowthInSpawnedEnemies(cadaverGrowth);
	}

	private static bool IsCachedCadaverGrowthInstanceValid(CadaverGrowthAI? cadaverGrowth)
	{
		if (!IsCadaverGrowthInstanceUsable(cadaverGrowth))
		{
			return false;
		}

		if (Time.unscaledTime < nextCachedInstanceMembershipCheckTime)
		{
			return true;
		}

		nextCachedInstanceMembershipCheckTime = Time.unscaledTime + CachedInstanceMembershipCheckInterval;
		return IsCadaverGrowthInSpawnedEnemies(cadaverGrowth);
	}

	private static bool IsCadaverGrowthInstanceUsable(CadaverGrowthAI? cadaverGrowth)
	{
		if (cadaverGrowth == null || cadaverGrowth.isEnemyDead || !cadaverGrowth.gameObject.activeInHierarchy)
		{
			return false;
		}

		if (StartOfRound.Instance == null || cadaverGrowth.playerInfections == null)
		{
			return false;
		}

		bool hasCurrentPlayerInfections = cadaverGrowth.playerInfections.Length == StartOfRound.Instance.allPlayerScripts.Length;
		if (!hasCurrentPlayerInfections)
		{
			return false;
		}

		return true;
	}

	private static bool IsCadaverGrowthInSpawnedEnemies(CadaverGrowthAI? cadaverGrowth)
	{
		if (RoundManager.Instance == null)
		{
			return false;
		}

		for (int i = 0; i < RoundManager.Instance.SpawnedEnemies.Count; i++)
		{
			if (RoundManager.Instance.SpawnedEnemies[i] == cadaverGrowth)
			{
				return true;
			}
		}

		return false;
	}

	private static void ClearCachedCadaverGrowthInstance()
	{
		cachedCadaverGrowthInstance = null;
		sharedInfectionCache.Clear();
		nextInstanceScanTime = 0f;
		nextCachedInstanceMembershipCheckTime = 0f;
	}

	internal static void InvalidateAll()
	{
		cachedCadaverGrowthInstance = null;
		sharedInfectionCache.Clear();
		nextInstanceScanTime = 0f;
		nextSampleTime = 0f;
		nextCachedInstanceMembershipCheckTime = 0f;
		currentAbsentInstanceScanInterval = AbsentInstanceScanIntervalMin;
	}

	private static bool TryResolveInfectionIndex(PlayerControllerB player, int knownPlayerSlot, out int infectionIndex)
	{
		infectionIndex = -1;
		PlayerControllerB[]? allPlayers = StartOfRound.Instance?.allPlayerScripts;
		if (allPlayers == null || allPlayers.Length == 0)
		{
			return false;
		}

		if ((uint)knownPlayerSlot < (uint)allPlayers.Length && allPlayers[knownPlayerSlot] == player)
		{
			infectionIndex = knownPlayerSlot;
			return true;
		}

		int playerId = (int)player.playerClientId;
		if ((uint)playerId < (uint)allPlayers.Length && allPlayers[playerId] == player)
		{
			infectionIndex = playerId;
			return true;
		}

		for (int i = 0; i < allPlayers.Length; i++)
		{
			if (allPlayers[i] == player)
			{
				infectionIndex = i;
				return true;
			}
		}

		return false;
	}

	private static void IncreaseAbsentInstanceScanBackoff()
	{
		currentAbsentInstanceScanInterval = Mathf.Min(currentAbsentInstanceScanInterval * 2f, AbsentInstanceScanIntervalMax);
	}

	private static void ResetAbsentInstanceScanBackoff()
	{
		currentAbsentInstanceScanInterval = AbsentInstanceScanIntervalMin;
	}

	private sealed class SharedInfectionCache
	{
		private bool[] knownValues = Array.Empty<bool>();

		private bool[] infectionValues = Array.Empty<bool>();

		private float[] meters = Array.Empty<float>();

		private ulong[] actualClientIds = Array.Empty<ulong>();

		private int[] playerClientIds = Array.Empty<int>();

		private int validLength;

		public void EnsureCapacity(int length)
		{
			validLength = length;
			if (knownValues.Length >= length)
			{
				return;
			}

			int capacity = GrowCapacity(length);
			knownValues = new bool[capacity];
			infectionValues = new bool[capacity];
			meters = new float[capacity];
			actualClientIds = new ulong[capacity];
			playerClientIds = new int[capacity];
		}

		public void Clear()
		{
			Array.Clear(knownValues, 0, validLength);
			Array.Clear(infectionValues, 0, validLength);
			Array.Clear(meters, 0, validLength);
			Array.Clear(actualClientIds, 0, validLength);
			Array.Clear(playerClientIds, 0, validLength);
			validLength = 0;
		}

		public void Set(int index, bool hasValue, float meter)
		{
			knownValues[index] = false;
			infectionValues[index] = hasValue;
			meters[index] = meter;
			actualClientIds[index] = ulong.MaxValue;
			playerClientIds[index] = -1;
		}

		public void Set(int index, PlayerControllerB player, bool hasValue, float meter)
		{
			knownValues[index] = true;
			infectionValues[index] = hasValue;
			meters[index] = meter;
			actualClientIds[index] = player != null ? player.actualClientId : ulong.MaxValue;
			playerClientIds[index] = player != null ? (int)player.playerClientId : -1;
		}

		public bool TryGet(int index, PlayerControllerB player, out float meter)
		{
			bool hasKnownState = TryGet(index, player, out bool hasInfection, out meter);
			return hasKnownState && hasInfection;
		}

		public bool TryGet(int index, PlayerControllerB player, out bool hasInfection, out float meter)
		{
			meter = 0f;
			hasInfection = false;
			if (index < 0 || index >= validLength || !knownValues[index])
			{
				return false;
			}

			if (player == null || actualClientIds[index] != player.actualClientId || playerClientIds[index] != (int)player.playerClientId)
			{
				knownValues[index] = false;
				infectionValues[index] = false;
				return false;
			}

			hasInfection = infectionValues[index];
			meter = meters[index];
			return true;
		}

		private static int GrowCapacity(int requiredCapacity)
		{
			int capacity = MinimumInfectionCacheCapacity;
			while (capacity < requiredCapacity)
			{
				capacity *= 2;
			}

			return capacity;
		}
	}
}
