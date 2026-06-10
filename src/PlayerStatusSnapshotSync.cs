using System;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal static class PlayerStatusSnapshotSync
{
	private const string SnapshotMessageName = "playerstatusbars.status.v1";
	private const string InfectionReportMessageName = "playerstatusbars.infection-report.v1";
	private const byte ProtocolVersion = 2;
	private const byte InfectionReportProtocolVersion = 1;
	private const byte DeadFlag = 1 << 0;
	private const byte CriticalFlag = 1 << 1;
	private const byte InfectionFlag = 1 << 2;
	private const byte InfectionKnownFlag = 1 << 3;
	private const float SnapshotInterval = 1f;
	private const float SnapshotTtl = 3f;
	private const float InfectionReportHeartbeatInterval = 0.5f;
	private const float InfectionReportTtl = 3f;
	private const int MinimumSnapshotCapacity = 16;
	private const int MinimumInfectionReportCapacity = 16;
	private const int SnapshotHeaderBytes = 2;
	private const int SnapshotEntryBytes = 12;
	private const int InfectionReportBytes = 12;
	private const int MaxSnapshotPayloadBytes = 2048;
	private const int MaxSnapshotEntries = (MaxSnapshotPayloadBytes - SnapshotHeaderBytes) / SnapshotEntryBytes;

	private static PlayerStatusSnapshot[] snapshots = Array.Empty<PlayerStatusSnapshot>();
	private static bool[] hasSnapshots = Array.Empty<bool>();
	private static bool[] hasInfectionReports = Array.Empty<bool>();
	private static ulong[] infectionReportActualClientIds = Array.Empty<ulong>();
	private static bool[] infectionReportHasInfection = Array.Empty<bool>();
	private static float[] infectionReportMeters = Array.Empty<float>();
	private static float[] infectionReportReceivedTimes = Array.Empty<float>();
	private static CustomMessagingManager? registeredMessagingManager;
	private static float nextSnapshotSendTime;
	private static float nextLocalInfectionReportSendTime;
	private static bool hasLocalInfectionReportState;
	private static byte lastLocalInfectionReportPlayerId;
	private static ulong lastLocalInfectionReportActualClientId;
	private static byte lastLocalInfectionReportFlags;
	private static byte lastLocalInfectionReportPercent;
	private static int validSnapshotLength;
	private static int validInfectionReportLength;

	public static void Tick(StartOfRound startOfRound, PlayerControllerB[] allPlayers)
	{
		NetworkManager networkManager = NetworkManager.Singleton;
		CustomMessagingManager? messagingManager = networkManager != null ? networkManager.CustomMessagingManager : null;
		if (networkManager == null || messagingManager == null || !networkManager.IsListening)
		{
			ClearSnapshots();
			ClearInfectionReports();
			ResetLocalInfectionReportState();
			registeredMessagingManager = null;
			return;
		}

		EnsureRegistered(messagingManager);
		SendLocalInfectionReport(networkManager, messagingManager, startOfRound, allPlayers);
		if (!networkManager.IsServer || Time.unscaledTime < nextSnapshotSendTime)
		{
			return;
		}

		nextSnapshotSendTime = Time.unscaledTime + SnapshotInterval;
		SendSnapshotToClients(networkManager, messagingManager, startOfRound, allPlayers);
	}

	public static bool TryGetSnapshot(PlayerControllerB player, out PlayerStatusSnapshot snapshot)
	{
		snapshot = default;
		if (player == null)
		{
			return false;
		}

		NetworkManager networkManager = NetworkManager.Singleton;
		if (networkManager != null && networkManager.IsServer)
		{
			return false;
		}

		int playerId = (int)player.playerClientId;
		if (playerId < 0 || playerId >= validSnapshotLength || !hasSnapshots[playerId])
		{
			return false;
		}

		PlayerStatusSnapshot cachedSnapshot = snapshots[playerId];
		if (cachedSnapshot.ActualClientId != player.actualClientId || Time.unscaledTime - cachedSnapshot.ReceivedTime > SnapshotTtl)
		{
			hasSnapshots[playerId] = false;
			return false;
		}

		snapshot = cachedSnapshot;
		return true;
	}

	internal static void ClearSnapshots()
	{
		ClearSnapshotValues();
		ClearInfectionReports();
		ResetLocalInfectionReportState();
		nextSnapshotSendTime = 0f;
	}

	private static void ClearSnapshotValues()
	{
		Array.Clear(hasSnapshots, 0, validSnapshotLength);
		Array.Clear(snapshots, 0, validSnapshotLength);
		validSnapshotLength = 0;
	}

	private static void EnsureRegistered(CustomMessagingManager messagingManager)
	{
		if (registeredMessagingManager == messagingManager)
		{
			return;
		}

		if (registeredMessagingManager != null)
		{
			registeredMessagingManager.UnregisterNamedMessageHandler(SnapshotMessageName);
			registeredMessagingManager.UnregisterNamedMessageHandler(InfectionReportMessageName);
		}

		messagingManager.RegisterNamedMessageHandler(SnapshotMessageName, HandleSnapshotMessage);
		messagingManager.RegisterNamedMessageHandler(InfectionReportMessageName, HandleInfectionReportMessage);
		registeredMessagingManager = messagingManager;
	}

	private static void SendSnapshotToClients(NetworkManager networkManager, CustomMessagingManager messagingManager, StartOfRound startOfRound, PlayerControllerB[] allPlayers)
	{
		if (allPlayers == null || allPlayers.Length == 0)
		{
			ClearSnapshotValues();
			return;
		}

		if (!HasRemoteClients(networkManager))
		{
			ClearSnapshotValues();
			ClearInfectionReports();
			return;
		}

		using FastBufferWriter writer = new(MaxSnapshotPayloadBytes, Allocator.Temp);
		byte protocolVersion = ProtocolVersion;
		byte snapshotCount = 0;
		writer.WriteValueSafe(in protocolVersion);
		int countPosition = writer.Position;
		writer.WriteValueSafe(in snapshotCount);

		for (int slot = 0; slot < allPlayers.Length && snapshotCount < MaxSnapshotEntries; slot++)
		{
			PlayerControllerB player = allPlayers[slot];
			if (!ShouldIncludePlayer(startOfRound, player))
			{
				continue;
			}

			int playerId = (int)player.playerClientId;
			if (playerId < 0 || playerId > byte.MaxValue)
			{
				continue;
			}

			int health = Mathf.Clamp(player.health, 0, 255);
			byte flags = 0;
			if (player.isPlayerDead || health <= 0)
			{
				flags |= DeadFlag;
			}

			if (player.criticallyInjured || player.bleedingHeavily)
			{
				flags |= CriticalFlag;
			}

			bool hasKnownInfectionState = TryGetReportedInfection(player, slot, out bool hasInfection, out float infectionMeter);
			if (!hasKnownInfectionState)
			{
				hasKnownInfectionState = CadaverGrowthInfectionProvider.TryGetInfectionState(player, slot, out hasInfection, out infectionMeter);
			}

			byte infectionPercent = 0;
			if (hasKnownInfectionState)
			{
				flags |= InfectionKnownFlag;
				infectionPercent = (byte)Mathf.Clamp(Mathf.RoundToInt(infectionMeter * 100f), 0, 100);
				if (hasInfection && infectionPercent > 0)
				{
					flags |= InfectionFlag;
				}
			}

			byte playerIdByte = (byte)playerId;
			ulong actualClientId = player.actualClientId;
			byte healthByte = (byte)health;
			writer.WriteValueSafe(in playerIdByte);
			writer.WriteValueSafe(in actualClientId);
			writer.WriteValueSafe(in healthByte);
			writer.WriteValueSafe(in flags);
			writer.WriteValueSafe(in infectionPercent);
			snapshotCount++;
		}

		int endPosition = writer.Position;
		writer.Seek(countPosition);
		writer.WriteValueSafe(in snapshotCount);
		writer.Seek(endPosition);
		messagingManager.SendNamedMessageToAll(SnapshotMessageName, writer, NetworkDelivery.ReliableSequenced);
	}

	private static void SendLocalInfectionReport(NetworkManager networkManager, CustomMessagingManager messagingManager, StartOfRound startOfRound, PlayerControllerB[] allPlayers)
	{
		if (!networkManager.IsClient || networkManager.IsServer)
		{
			return;
		}

		PlayerControllerB? localPlayer = GameNetworkManager.Instance != null
			? GameNetworkManager.Instance.localPlayerController
			: startOfRound?.localPlayerController;
		if (localPlayer == null || allPlayers == null || allPlayers.Length == 0)
		{
			return;
		}

		int playerId = (int)localPlayer.playerClientId;
		if (playerId < 0 || playerId > byte.MaxValue)
		{
			return;
		}

		int playerSlot = ResolvePlayerSlot(localPlayer, allPlayers, playerId);
		bool hasKnownInfectionState;
		bool hasInfection;
		float infectionMeter;
		if (localPlayer.isPlayerDead || localPlayer.health <= 0)
		{
			hasKnownInfectionState = true;
			hasInfection = false;
			infectionMeter = 0f;
		}
		else
		{
			hasKnownInfectionState = CadaverGrowthInfectionProvider.TryGetInfectionState(localPlayer, playerSlot, out hasInfection, out infectionMeter);
		}

		byte flags = 0;
		if (hasKnownInfectionState)
		{
			flags |= InfectionKnownFlag;
		}

		byte infectionPercent = 0;
		if (hasKnownInfectionState && hasInfection)
		{
			infectionPercent = (byte)Mathf.Clamp(Mathf.RoundToInt(infectionMeter * 100f), 0, 100);
			if (infectionPercent > 0)
			{
				flags |= InfectionFlag;
			}
		}

		byte playerIdByte = (byte)playerId;
		ulong actualClientId = localPlayer.actualClientId;
		if (!ShouldSendLocalInfectionReport(playerIdByte, actualClientId, flags, infectionPercent))
		{
			return;
		}

		using FastBufferWriter writer = new(InfectionReportBytes, Allocator.Temp);
		byte protocolVersion = InfectionReportProtocolVersion;
		writer.WriteValueSafe(in protocolVersion);
		writer.WriteValueSafe(in playerIdByte);
		writer.WriteValueSafe(in actualClientId);
		writer.WriteValueSafe(in flags);
		writer.WriteValueSafe(in infectionPercent);
		messagingManager.SendNamedMessage(InfectionReportMessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);

		hasLocalInfectionReportState = true;
		lastLocalInfectionReportPlayerId = playerIdByte;
		lastLocalInfectionReportActualClientId = actualClientId;
		lastLocalInfectionReportFlags = flags;
		lastLocalInfectionReportPercent = infectionPercent;
		nextLocalInfectionReportSendTime = Time.unscaledTime + InfectionReportHeartbeatInterval;
	}

	private static bool ShouldSendLocalInfectionReport(byte playerId, ulong actualClientId, byte flags, byte infectionPercent)
	{
		if (!hasLocalInfectionReportState)
		{
			return (flags & InfectionKnownFlag) != 0;
		}

		if (playerId != lastLocalInfectionReportPlayerId
			|| actualClientId != lastLocalInfectionReportActualClientId
			|| flags != lastLocalInfectionReportFlags
			|| infectionPercent != lastLocalInfectionReportPercent)
		{
			return true;
		}

		return Time.unscaledTime >= nextLocalInfectionReportSendTime;
	}

	private static bool HasRemoteClients(NetworkManager networkManager)
	{
		foreach (ulong clientId in networkManager.ConnectedClientsIds)
		{
			if (clientId != NetworkManager.ServerClientId)
			{
				return true;
			}
		}

		return false;
	}

	private static void HandleSnapshotMessage(ulong senderClientId, FastBufferReader reader)
	{
		NetworkManager networkManager = NetworkManager.Singleton;
		if (networkManager == null || senderClientId != NetworkManager.ServerClientId)
		{
			return;
		}

		if (!reader.TryBeginRead(2))
		{
			return;
		}

		byte version = 0;
		byte snapshotCount = 0;
		reader.ReadValueSafe(out version);
		reader.ReadValueSafe(out snapshotCount);
		if (version != ProtocolVersion)
		{
			return;
		}

		ClearSnapshotValues();
		float now = Time.unscaledTime;
		for (int i = 0; i < snapshotCount; i++)
		{
			if (!reader.TryBeginRead(SnapshotEntryBytes))
			{
				return;
			}

			byte playerId = 0;
			ulong actualClientId = 0;
			byte health = 0;
			byte flags = 0;
			byte infectionPercent = 0;
			reader.ReadValueSafe(out playerId);
			reader.ReadValueSafe(out actualClientId);
			reader.ReadValueSafe(out health);
			reader.ReadValueSafe(out flags);
			reader.ReadValueSafe(out infectionPercent);

			EnsureSnapshotCapacity(playerId + 1);
			snapshots[playerId] = new PlayerStatusSnapshot(
				actualClientId,
				health,
				(flags & DeadFlag) != 0,
				(flags & CriticalFlag) != 0,
				(flags & InfectionKnownFlag) != 0,
				(flags & InfectionFlag) != 0,
				Mathf.Clamp01(infectionPercent / 100f),
				now);
			hasSnapshots[playerId] = true;
		}
	}

	private static void HandleInfectionReportMessage(ulong senderClientId, FastBufferReader reader)
	{
		NetworkManager networkManager = NetworkManager.Singleton;
		if (networkManager == null || !networkManager.IsServer)
		{
			return;
		}

		if (!reader.TryBeginRead(InfectionReportBytes))
		{
			return;
		}

		byte version = 0;
		byte reportedPlayerId = 0;
		ulong actualClientId = 0;
		byte flags = 0;
		byte infectionPercent = 0;
		reader.ReadValueSafe(out version);
		reader.ReadValueSafe(out reportedPlayerId);
		reader.ReadValueSafe(out actualClientId);
		reader.ReadValueSafe(out flags);
		reader.ReadValueSafe(out infectionPercent);
		if (version != InfectionReportProtocolVersion || actualClientId != senderClientId)
		{
			return;
		}

		if (!TryResolveReportedPlayerId(senderClientId, reportedPlayerId, out int playerId))
		{
			return;
		}

		EnsureInfectionReportCapacity(playerId + 1);
		if ((flags & InfectionKnownFlag) == 0)
		{
			hasInfectionReports[playerId] = false;
			return;
		}

		infectionReportActualClientIds[playerId] = actualClientId;
		infectionReportHasInfection[playerId] = (flags & InfectionFlag) != 0 && infectionPercent > 0;
		infectionReportMeters[playerId] = Mathf.Clamp01(infectionPercent / 100f);
		infectionReportReceivedTimes[playerId] = Time.unscaledTime;
		hasInfectionReports[playerId] = true;
	}

	private static bool ShouldIncludePlayer(StartOfRound startOfRound, PlayerControllerB player)
	{
		if (startOfRound == null || player == null || player.disconnectedMidGame || !player.gameObject.activeInHierarchy)
		{
			return false;
		}

		if (player.isPlayerControlled)
		{
			return true;
		}

		return startOfRound.ClientPlayerList.ContainsKey(player.actualClientId);
	}

	private static int ResolvePlayerSlot(PlayerControllerB player, PlayerControllerB[] allPlayers, int fallbackPlayerId)
	{
		if ((uint)fallbackPlayerId < (uint)allPlayers.Length && allPlayers[fallbackPlayerId] == player)
		{
			return fallbackPlayerId;
		}

		for (int i = 0; i < allPlayers.Length; i++)
		{
			if (allPlayers[i] == player)
			{
				return i;
			}
		}

		return fallbackPlayerId;
	}

	private static bool TryResolveReportedPlayerId(ulong senderClientId, byte reportedPlayerId, out int playerId)
	{
		playerId = -1;
		StartOfRound? startOfRound = StartOfRound.Instance;
		PlayerControllerB[]? allPlayers = startOfRound?.allPlayerScripts;
		if (startOfRound == null || allPlayers == null)
		{
			return false;
		}

		if (startOfRound.ClientPlayerList.TryGetValue(senderClientId, out int mappedSlot)
			&& (uint)mappedSlot < (uint)allPlayers.Length
			&& allPlayers[mappedSlot] != null
			&& allPlayers[mappedSlot].actualClientId == senderClientId)
		{
			playerId = mappedSlot;
			return true;
		}

		if (reportedPlayerId < allPlayers.Length
			&& allPlayers[reportedPlayerId] != null
			&& allPlayers[reportedPlayerId].actualClientId == senderClientId)
		{
			playerId = reportedPlayerId;
			return true;
		}

		return false;
	}

	internal static bool TryGetReportedInfection(PlayerControllerB player, int knownPlayerSlot, out bool hasInfection, out float infectionMeter)
	{
		hasInfection = false;
		infectionMeter = 0f;
		if (player == null)
		{
			return false;
		}

		if (TryGetReportedInfectionAtIndex(knownPlayerSlot, player, out hasInfection, out infectionMeter))
		{
			return true;
		}

		int playerId = (int)player.playerClientId;
		return playerId != knownPlayerSlot && TryGetReportedInfectionAtIndex(playerId, player, out hasInfection, out infectionMeter);
	}

	private static bool TryGetReportedInfectionAtIndex(int index, PlayerControllerB player, out bool hasInfection, out float infectionMeter)
	{
		hasInfection = false;
		infectionMeter = 0f;
		if (index < 0 || index >= validInfectionReportLength || !hasInfectionReports[index])
		{
			return false;
		}

		if (infectionReportActualClientIds[index] != player.actualClientId || Time.unscaledTime - infectionReportReceivedTimes[index] > InfectionReportTtl)
		{
			hasInfectionReports[index] = false;
			return false;
		}

		hasInfection = infectionReportHasInfection[index];
		infectionMeter = infectionReportMeters[index];
		return true;
	}

	private static void EnsureSnapshotCapacity(int requiredLength)
	{
		if (requiredLength <= validSnapshotLength)
		{
			return;
		}

		if (hasSnapshots.Length < requiredLength)
		{
			int capacity = GrowCapacity(requiredLength, MinimumSnapshotCapacity);
			Array.Resize(ref hasSnapshots, capacity);
			Array.Resize(ref snapshots, capacity);
		}

		validSnapshotLength = requiredLength;
	}

	private static void EnsureInfectionReportCapacity(int requiredLength)
	{
		if (requiredLength <= validInfectionReportLength)
		{
			return;
		}

		if (hasInfectionReports.Length < requiredLength)
		{
			int capacity = GrowCapacity(requiredLength, MinimumInfectionReportCapacity);
			Array.Resize(ref hasInfectionReports, capacity);
			Array.Resize(ref infectionReportActualClientIds, capacity);
			Array.Resize(ref infectionReportHasInfection, capacity);
			Array.Resize(ref infectionReportMeters, capacity);
			Array.Resize(ref infectionReportReceivedTimes, capacity);
		}

		validInfectionReportLength = requiredLength;
	}

	private static void ClearInfectionReports()
	{
		Array.Clear(hasInfectionReports, 0, validInfectionReportLength);
		Array.Clear(infectionReportActualClientIds, 0, validInfectionReportLength);
		Array.Clear(infectionReportHasInfection, 0, validInfectionReportLength);
		Array.Clear(infectionReportMeters, 0, validInfectionReportLength);
		Array.Clear(infectionReportReceivedTimes, 0, validInfectionReportLength);
		validInfectionReportLength = 0;
	}

	private static void ResetLocalInfectionReportState()
	{
		nextLocalInfectionReportSendTime = 0f;
		hasLocalInfectionReportState = false;
		lastLocalInfectionReportPlayerId = 0;
		lastLocalInfectionReportActualClientId = 0;
		lastLocalInfectionReportFlags = 0;
		lastLocalInfectionReportPercent = 0;
	}

	private static int GrowCapacity(int requiredCapacity, int minimumCapacity)
	{
		int capacity = minimumCapacity;
		while (capacity < requiredCapacity)
		{
			capacity *= 2;
		}

		return capacity;
	}

}
