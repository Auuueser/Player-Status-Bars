using System;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class CadaverGrowthInfectionProvider
{
	private const float MetadataRetryDelay = 5f;

	private const float InstanceRetryDelay = 1f;

	private static Type? cadaverGrowthType;

	private static FieldInfo? playerInfectionsField;

	private static FieldInfo? infectionMeterField;

	private static UnityEngine.Object? cachedCadaverGrowthInstance;

	private static bool metadataResolved;

	private static float nextMetadataResolveTime;

	private static float nextFindTime;

	public bool TryGetNormalizedInfection(PlayerControllerB player, out float infectionMeter)
	{
		infectionMeter = 0f;
		if (player == null)
		{
			return false;
		}

		if (!TryResolveCadaverGrowthInstance(out object instance))
		{
			return false;
		}

		Array? playerInfections = playerInfectionsField?.GetValue(instance) as Array;
		if (playerInfections == null)
		{
			return false;
		}

		int playerId = (int)player.playerClientId;
		if (playerId < 0 || playerId >= playerInfections.Length)
		{
			return false;
		}

		object? infectionEntry = playerInfections.GetValue(playerId);
		if (infectionEntry == null || infectionMeterField == null)
		{
			return false;
		}

		object? rawValue = infectionMeterField.GetValue(infectionEntry);
		if (rawValue is not float infectionValue)
		{
			return false;
		}

		infectionMeter = Mathf.Clamp01(infectionValue);
		return true;
	}

	private bool TryResolveCadaverGrowthInstance(out object instance)
	{
		instance = null!;
		if (!TryResolveCadaverGrowthMetadata())
		{
			return false;
		}

		if (cachedCadaverGrowthInstance == null && Time.unscaledTime >= nextFindTime)
		{
			nextFindTime = Time.unscaledTime + InstanceRetryDelay;
			cachedCadaverGrowthInstance = UnityEngine.Object.FindObjectOfType(cadaverGrowthType!);
		}

		if (cachedCadaverGrowthInstance == null)
		{
			return false;
		}

		instance = cachedCadaverGrowthInstance;
		return true;
	}

	private bool TryResolveCadaverGrowthMetadata()
	{
		if (metadataResolved)
		{
			return true;
		}

		if (Time.unscaledTime < nextMetadataResolveTime)
		{
			return false;
		}

		nextMetadataResolveTime = Time.unscaledTime + MetadataRetryDelay;
		cadaverGrowthType = AppDomain.CurrentDomain.GetAssemblies()
			.Select(assembly => assembly.GetType("CadaverGrowthAI", throwOnError: false))
			.FirstOrDefault(type => type != null);
		if (cadaverGrowthType == null)
		{
			return false;
		}

		playerInfectionsField = cadaverGrowthType.GetField("playerInfections", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (playerInfectionsField == null)
		{
			return false;
		}

		Type? playerInfectionType = cadaverGrowthType.GetNestedType("PlayerInfection", BindingFlags.Public | BindingFlags.NonPublic);
		if (playerInfectionType == null)
		{
			Type? arrayElementType = playerInfectionsField.FieldType.GetElementType();
			playerInfectionType = arrayElementType;
		}

		if (playerInfectionType == null)
		{
			return false;
		}

		infectionMeterField = playerInfectionType.GetField("infectionMeter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		metadataResolved = infectionMeterField != null;
		return metadataResolved;
	}
}
