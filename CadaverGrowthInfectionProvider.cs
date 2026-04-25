using System;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class CadaverGrowthInfectionProvider
{
	private Type? cadaverGrowthType;

	private FieldInfo? playerInfectionsField;

	private FieldInfo? infectionMeterField;

	private UnityEngine.Object? cachedCadaverGrowthInstance;

	private float nextFindTime;

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
			nextFindTime = Time.unscaledTime + 1f;
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
		if (cadaverGrowthType != null && playerInfectionsField != null && infectionMeterField != null)
		{
			return true;
		}

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
		return infectionMeterField != null;
	}
}
