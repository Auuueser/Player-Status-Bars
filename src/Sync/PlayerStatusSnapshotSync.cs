using System;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OtherPlayerStatusBars;

/// <summary>
/// Same-version status sync. Each client reports its own health and infection to the host, because
/// only the owner knows those exactly; the host merges the reports into a snapshot for every observer.
/// </summary>
internal static class PlayerStatusSnapshotSync
{
	private const string SnapshotMessageName = "playerstatusbars.status.v1";
	private const string OwnerStatusMessageName = "playerstatusbars.owner-status.v1";
	private const byte SnapshotProtocolVersion = 4;
	private const byte OwnerStatusProtocolVersion = 1;
	private const float SnapshotInterval = 1f;
	private const float SnapshotChangeCheckInterval = 0.1f;
	private const float SnapshotTtl = 3f;
	private const float OwnerStatusHeartbeatInterval = 1f;
	private const float OwnerStatusCheckInterval = 0.1f;
	private const float OwnerStatusTtl = 3f;
	private const int MinimumSnapshotCapacity = 16;
	private const int MinimumOwnerReportCapacity = 16;

	private static readonly StatusReportGate ownerStatusGate = new(OwnerStatusHeartbeatInterval);

	private static readonly HostInfectionPermission hostInfectionPermission = new(SnapshotTtl);

	internal static SlotInheritanceTracker SlotInheritance { get; } = new();

	/// <summary>The host decides for the whole room; clients need a recent yes from a current host.</summary>
	internal static bool IsInfectionDisplayAllowed
	{
		get
		{
			NetworkManager networkManager = NetworkManager.Singleton;
			return networkManager != null && networkManager.IsServer
				? Plugin.Settings.ShowInfectionBars
				: hostInfectionPermission.IsAllowed(Time.unscaledTime);
		}
	}

	private static PlayerStatusSnapshot[] snapshots = Array.Empty<PlayerStatusSnapshot>();
	private static bool[] hasSnapshots = Array.Empty<bool>();
	private static OwnerStatusReport[] ownerReports = Array.Empty<OwnerStatusReport>();
	private static bool[] hasOwnerReports = Array.Empty<bool>();
	private static CustomMessagingManager? registeredMessagingManager;
	private static float nextSnapshotSendTime;
	private static float nextSnapshotCheckTime;
	private static float nextOwnerStatusCheckTime;
	private static bool hasLastSentSnapshot;
	private static uint lastSentSnapshotHash;
	private static int validSnapshotLength;
	private static int validOwnerReportLength;

	public static void Tick(StartOfRound? startOfRound, PlayerControllerB[]? allPlayers)
	{
		NetworkManager networkManager = NetworkManager.Singleton;
		CustomMessagingManager? messagingManager = networkManager != null ? networkManager.CustomMessagingManager : null;
		if (networkManager == null || messagingManager == null || !networkManager.IsListening)
		{
			ClearSnapshots();
			SlotInheritance.Reset();
			hostInfectionPermission.Reset();
			registeredMessagingManager = null;
			return;
		}

		EnsureRegistered(messagingManager);
		ObservePlayerSlots(startOfRound, allPlayers);
		float now = Time.unscaledTime;
		if (now >= nextOwnerStatusCheckTime)
		{
			nextOwnerStatusCheckTime = now + OwnerStatusCheckInterval;
			SendOwnerStatus(networkManager, messagingManager, startOfRound, allPlayers, now);
		}

		if (!networkManager.IsServer || now < nextSnapshotCheckTime)
		{
			return;
		}

		nextSnapshotCheckTime = now + SnapshotChangeCheckInterval;
		SendSnapshotToClients(networkManager, messagingManager, startOfRound, allPlayers, now);
	}

	private static void ObservePlayerSlots(StartOfRound? startOfRound, PlayerControllerB[]? allPlayers)
	{
		if (allPlayers == null)
		{
			return;
		}

		PlayerControllerB? localPlayer = ResolveLocalPlayer(startOfRound);
		for (int slot = 0; slot < allPlayers.Length; slot++)
		{
			PlayerControllerB player = allPlayers[slot];
			if (player == null)
			{
				continue;
			}

			bool occupantChanged = SlotInheritance.Observe(slot, player.actualClientId, player.health, player.criticallyInjured, player.timeSinceTakingDamage);
			if (occupantChanged && player != localPlayer)
			{
				// Our local copy of this slot still holds the previous player's flags. A leftover flag would
				// hide the new player's real critical hit, which the game announces only by setting it.
				player.criticallyInjured = false;
				player.bleedingHeavily = false;

				// The game never clears this after someone leaves mid-round, so the new player would count
				// as gone: no bar here, and no entry in the host's snapshot.
				player.disconnectedMidGame = false;
			}
		}
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

	internal static bool TryGetOwnerReport(PlayerControllerB player, int knownPlayerSlot, out OwnerStatusReport report)
	{
		report = default;
		if (player == null)
		{
			return false;
		}

		if (TryGetOwnerReportAtIndex(knownPlayerSlot, player, out report))
		{
			return true;
		}

		int playerId = (int)player.playerClientId;
		return playerId != knownPlayerSlot && TryGetOwnerReportAtIndex(playerId, player, out report);
	}

	internal static void ClearSnapshots()
	{
		ClearSnapshotValues();
		ClearOwnerReports();
		ownerStatusGate.Reset();
		nextSnapshotSendTime = 0f;
		nextSnapshotCheckTime = 0f;
		nextOwnerStatusCheckTime = 0f;
		hasLastSentSnapshot = false;
	}

	private static void ClearSnapshotValues()
	{
		Array.Clear(hasSnapshots, 0, validSnapshotLength);
		Array.Clear(snapshots, 0, validSnapshotLength);
		validSnapshotLength = 0;
	}

	private static void ClearOwnerReports()
	{
		Array.Clear(hasOwnerReports, 0, validOwnerReportLength);
		Array.Clear(ownerReports, 0, validOwnerReportLength);
		validOwnerReportLength = 0;
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
			registeredMessagingManager.UnregisterNamedMessageHandler(OwnerStatusMessageName);
		}

		messagingManager.RegisterNamedMessageHandler(SnapshotMessageName, HandleSnapshotMessage);
		messagingManager.RegisterNamedMessageHandler(OwnerStatusMessageName, HandleOwnerStatusMessage);
		registeredMessagingManager = messagingManager;
	}

	private static void SendSnapshotToClients(NetworkManager networkManager, CustomMessagingManager messagingManager, StartOfRound? startOfRound, PlayerControllerB[]? allPlayers, float now)
	{
		if (allPlayers == null || allPlayers.Length == 0)
		{
			ClearSnapshotValues();
			hasLastSentSnapshot = false;
			return;
		}

		if (!HasRemoteClients(networkManager))
		{
			ClearSnapshotValues();
			ClearOwnerReports();
			hasLastSentSnapshot = false;
			return;
		}

		PlayerControllerB? hostPlayer = ResolveLocalPlayer(startOfRound);
		using FastBufferWriter writer = new(PlayerStatusWire.MaxSnapshotPayloadBytes, Allocator.Temp);
		byte protocolVersion = SnapshotProtocolVersion;
		byte snapshotCount = 0;
		bool allowInfection = Plugin.Settings.ShowInfectionBars;
		byte hostFlags = allowInfection ? PlayerStatusWire.HostAllowsInfectionFlag : (byte)0;
		uint snapshotHash = PlayerStatusWire.MixSnapshotHash(PlayerStatusWire.SnapshotHashSeed, 0, 0, 0, hostFlags, 0);
		writer.WriteValueSafe(in protocolVersion);
		int countPosition = writer.Position;
		writer.WriteValueSafe(in snapshotCount);
		writer.WriteValueSafe(in hostFlags);

		for (int slot = 0; slot < allPlayers.Length && snapshotCount < PlayerStatusWire.MaxSnapshotEntries; slot++)
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

			bool hasOwnerReport = TryGetOwnerReport(player, slot, out OwnerStatusReport ownerReport);
			bool isHealthExact = hasOwnerReport || player == hostPlayer;
			int health = Mathf.Clamp(hasOwnerReport ? ownerReport.Health : SlotInheritance.ResolveHealth(slot, player.health), 0, 255);
			byte flags = 0;
			if (player.isPlayerDead || health <= 0)
			{
				flags |= PlayerStatusWire.DeadFlag;
			}

			if (player.criticallyInjured)
			{
				flags |= PlayerStatusWire.CriticalFlag;
			}

			if (isHealthExact)
			{
				flags |= PlayerStatusWire.HealthExactFlag;
			}

			bool hasKnownInfectionState = allowInfection && hasOwnerReport && ownerReport.IsInfectionKnown;
			bool isInfectionExact = allowInfection && (hasKnownInfectionState || player == hostPlayer);
			bool hasInfection = hasKnownInfectionState && ownerReport.HasInfection;
			float infectionMeter = hasKnownInfectionState ? ownerReport.InfectionMeter : 0f;
			if (isInfectionExact)
			{
				flags |= PlayerStatusWire.InfectionExactFlag;
			}

			if (allowInfection && !hasKnownInfectionState)
			{
				hasKnownInfectionState = CadaverGrowthInfectionProvider.TryGetInfectionState(player, slot, out hasInfection, out infectionMeter);
			}

			byte infectionPercent = 0;
			if (hasKnownInfectionState)
			{
				flags |= PlayerStatusWire.InfectionKnownFlag;
				infectionPercent = PlayerStatusWire.ToInfectionPercent(infectionMeter);
				if (hasInfection && infectionPercent > 0)
				{
					flags |= PlayerStatusWire.InfectionFlag;
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
			snapshotHash = PlayerStatusWire.MixSnapshotHash(snapshotHash, playerIdByte, actualClientId, healthByte, flags, infectionPercent);
			snapshotCount++;
		}

		if (hasLastSentSnapshot && snapshotHash == lastSentSnapshotHash && now < nextSnapshotSendTime)
		{
			return;
		}

		int endPosition = writer.Position;
		writer.Seek(countPosition);
		writer.WriteValueSafe(in snapshotCount);
		writer.Seek(endPosition);
		messagingManager.SendNamedMessageToAll(SnapshotMessageName, writer, NetworkDelivery.ReliableSequenced);
		hasLastSentSnapshot = true;
		lastSentSnapshotHash = snapshotHash;
		nextSnapshotSendTime = now + SnapshotInterval;
	}

	private static void SendOwnerStatus(NetworkManager networkManager, CustomMessagingManager messagingManager, StartOfRound? startOfRound, PlayerControllerB[]? allPlayers, float now)
	{
		if (!networkManager.IsClient || networkManager.IsServer)
		{
			return;
		}

		PlayerControllerB? localPlayer = ResolveLocalPlayer(startOfRound);
		if (localPlayer == null || allPlayers == null || allPlayers.Length == 0)
		{
			return;
		}

		int playerId = (int)localPlayer.playerClientId;
		if (playerId < 0 || playerId > byte.MaxValue)
		{
			return;
		}

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
			int playerSlot = ResolvePlayerSlot(localPlayer, allPlayers, playerId);
			hasKnownInfectionState = CadaverGrowthInfectionProvider.TryGetInfectionState(localPlayer, playerSlot, out hasInfection, out infectionMeter);
		}

		byte flags = 0;
		byte infectionPercent = 0;
		if (hasKnownInfectionState)
		{
			flags |= PlayerStatusWire.InfectionKnownFlag;
			if (hasInfection)
			{
				infectionPercent = PlayerStatusWire.ToInfectionPercent(infectionMeter);
				if (infectionPercent > 0)
				{
					flags |= PlayerStatusWire.InfectionFlag;
				}
			}
		}

		byte playerIdByte = (byte)playerId;
		ulong actualClientId = localPlayer.actualClientId;
		byte health = (byte)Mathf.Clamp(localPlayer.health, 0, 255);
		if (!ownerStatusGate.ShouldSend(playerIdByte, actualClientId, flags, infectionPercent, health, now))
		{
			return;
		}

		using FastBufferWriter writer = new(PlayerStatusWire.OwnerStatusBytes, Allocator.Temp);
		byte protocolVersion = OwnerStatusProtocolVersion;
		writer.WriteValueSafe(in protocolVersion);
		writer.WriteValueSafe(in playerIdByte);
		writer.WriteValueSafe(in actualClientId);
		writer.WriteValueSafe(in flags);
		writer.WriteValueSafe(in infectionPercent);
		writer.WriteValueSafe(in health);
		messagingManager.SendNamedMessage(OwnerStatusMessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
		ownerStatusGate.MarkSent(playerIdByte, actualClientId, flags, infectionPercent, health, now);
	}

	private static PlayerControllerB? ResolveLocalPlayer(StartOfRound? startOfRound)
	{
		return GameNetworkManager.Instance != null
			? GameNetworkManager.Instance.localPlayerController
			: startOfRound?.localPlayerController;
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

		if (!reader.TryBeginRead(PlayerStatusWire.SnapshotHeaderBytes))
		{
			return;
		}

		byte version = 0;
		byte snapshotCount = 0;
		byte hostFlags = 0;
		reader.ReadValueSafe(out version);
		reader.ReadValueSafe(out snapshotCount);
		reader.ReadValueSafe(out hostFlags);
		if (version != SnapshotProtocolVersion)
		{
			return;
		}

		ClearSnapshotValues();
		float now = Time.unscaledTime;
		hostInfectionPermission.Receive((hostFlags & PlayerStatusWire.HostAllowsInfectionFlag) != 0, now);
		for (int i = 0; i < snapshotCount; i++)
		{
			if (!reader.TryBeginRead(PlayerStatusWire.SnapshotEntryBytes))
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
				(flags & PlayerStatusWire.HealthExactFlag) != 0,
				(flags & PlayerStatusWire.DeadFlag) != 0,
				(flags & PlayerStatusWire.CriticalFlag) != 0,
				(flags & PlayerStatusWire.InfectionKnownFlag) != 0,
				(flags & PlayerStatusWire.InfectionExactFlag) != 0,
				(flags & PlayerStatusWire.InfectionFlag) != 0,
				Mathf.Clamp01(infectionPercent / 100f),
				now);
			hasSnapshots[playerId] = true;
		}
	}

	private static void HandleOwnerStatusMessage(ulong senderClientId, FastBufferReader reader)
	{
		NetworkManager networkManager = NetworkManager.Singleton;
		if (networkManager == null || !networkManager.IsServer)
		{
			return;
		}

		if (!reader.TryBeginRead(PlayerStatusWire.OwnerStatusBytes))
		{
			return;
		}

		byte version = 0;
		byte reportedPlayerId = 0;
		ulong actualClientId = 0;
		byte flags = 0;
		byte infectionPercent = 0;
		byte health = 0;
		reader.ReadValueSafe(out version);
		reader.ReadValueSafe(out reportedPlayerId);
		reader.ReadValueSafe(out actualClientId);
		reader.ReadValueSafe(out flags);
		reader.ReadValueSafe(out infectionPercent);
		reader.ReadValueSafe(out health);
		if (version != OwnerStatusProtocolVersion || actualClientId != senderClientId)
		{
			return;
		}

		if (!TryResolveReportedPlayerId(senderClientId, reportedPlayerId, out int playerId))
		{
			return;
		}

		EnsureOwnerReportCapacity(playerId + 1);
		ownerReports[playerId] = new OwnerStatusReport(
			actualClientId,
			health,
			(flags & PlayerStatusWire.InfectionKnownFlag) != 0,
			(flags & PlayerStatusWire.InfectionFlag) != 0 && infectionPercent > 0,
			Mathf.Clamp01(infectionPercent / 100f),
			Time.unscaledTime);
		hasOwnerReports[playerId] = true;
	}

	private static bool ShouldIncludePlayer(StartOfRound? startOfRound, PlayerControllerB player)
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

	private static bool TryGetOwnerReportAtIndex(int index, PlayerControllerB player, out OwnerStatusReport report)
	{
		report = default;
		if (index < 0 || index >= validOwnerReportLength || !hasOwnerReports[index])
		{
			return false;
		}

		OwnerStatusReport cachedReport = ownerReports[index];
		if (cachedReport.ActualClientId != player.actualClientId || Time.unscaledTime - cachedReport.ReceivedTime > OwnerStatusTtl)
		{
			hasOwnerReports[index] = false;
			return false;
		}

		report = cachedReport;
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

	private static void EnsureOwnerReportCapacity(int requiredLength)
	{
		if (requiredLength <= validOwnerReportLength)
		{
			return;
		}

		if (hasOwnerReports.Length < requiredLength)
		{
			int capacity = GrowCapacity(requiredLength, MinimumOwnerReportCapacity);
			Array.Resize(ref hasOwnerReports, capacity);
			Array.Resize(ref ownerReports, capacity);
		}

		validOwnerReportLength = requiredLength;
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
