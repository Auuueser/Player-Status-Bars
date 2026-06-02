using System;
using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal static class CadaverGrowthInfectionProvider
{
	private const float SampleInterval = 0.5f;

	private const float AbsentInstanceScanIntervalMin = 1f;

	private const float AbsentInstanceScanIntervalMax = 10f;

	private const int MinimumInfectionCacheCapacity = 16;

	private static readonly SharedInfectionCache sharedInfectionCache = new();

	private static CadaverGrowthAI? cachedCadaverGrowthInstance;

	private static float nextInstanceScanTime;

	private static float nextSampleTime;

	private static float currentAbsentInstanceScanInterval = AbsentInstanceScanIntervalMin;

	public static bool TryGetNormalizedInfection(PlayerControllerB player, out float infectionMeter)
	{
		infectionMeter = 0f;
		if (player == null)
		{
			return false;
		}

		RefreshCache();
		int playerId = (int)player.playerClientId;
		return sharedInfectionCache.TryGet(playerId, out infectionMeter);
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

		sharedInfectionCache.EnsureCapacity(playerInfections.Length);
		for (int i = 0; i < playerInfections.Length; i++)
		{
			PlayerInfection infection = playerInfections[i];
			if (infection == null)
			{
				sharedInfectionCache.Set(i, false, 0f);
				continue;
			}

			float normalizedMeter = Mathf.Clamp01(infection.infectionMeter);
			if (!infection.infected)
			{
				sharedInfectionCache.Set(i, false, 0f);
				continue;
			}

			sharedInfectionCache.Set(i, true, normalizedMeter);
		}
	}

	private static CadaverGrowthAI? ResolveCadaverGrowthInstance()
	{
		if (cachedCadaverGrowthInstance != null)
		{
			if (IsCadaverGrowthInstanceValid(cachedCadaverGrowthInstance))
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
			if (RoundManager.Instance.SpawnedEnemies[i] is CadaverGrowthAI cadaverGrowth)
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
		private bool[] hasValues = Array.Empty<bool>();

		private float[] meters = Array.Empty<float>();

		private int validLength;

		public void EnsureCapacity(int length)
		{
			validLength = length;
			if (hasValues.Length >= length)
			{
				return;
			}

			int capacity = GrowCapacity(length);
			hasValues = new bool[capacity];
			meters = new float[capacity];
		}

		public void Clear()
		{
			Array.Clear(hasValues, 0, validLength);
			Array.Clear(meters, 0, validLength);
			validLength = 0;
		}

		public void Set(int index, bool hasValue, float meter)
		{
			hasValues[index] = hasValue;
			meters[index] = meter;
		}

		public bool TryGet(int index, out float meter)
		{
			meter = 0f;
			if (index < 0 || index >= validLength || !hasValues[index])
			{
				return false;
			}

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
