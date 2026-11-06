using System;

namespace OtherPlayerStatusBars;

/// <summary>Wire format shared by the host snapshot and owner status messages.</summary>
internal static class PlayerStatusWire
{
	public const byte DeadFlag = 1 << 0;

	public const byte CriticalFlag = 1 << 1;

	public const byte InfectionFlag = 1 << 2;

	public const byte InfectionKnownFlag = 1 << 3;

	public const byte HealthExactFlag = 1 << 4;

	public const byte InfectionExactFlag = 1 << 5;

	public const byte HostAllowsInfectionFlag = 1 << 0;

	public const int SnapshotHeaderBytes = 3;

	public const int SnapshotEntryBytes = 12;

	public const int OwnerStatusBytes = 13;

	public const int MaxSnapshotPayloadBytes = 2048;

	public const int MaxSnapshotEntries = (MaxSnapshotPayloadBytes - SnapshotHeaderBytes) / SnapshotEntryBytes;

	public const uint SnapshotHashSeed = 2166136261u;

	private const uint SnapshotHashPrime = 16777619u;

	public static byte ToInfectionPercent(float meter)
	{
		return (byte)Math.Min(100, Math.Max(0, (int)Math.Round(meter * 100f)));
	}

	public static uint MixSnapshotHash(uint hash, byte playerId, ulong actualClientId, byte health, byte flags, byte infectionPercent)
	{
		unchecked
		{
			hash = (hash ^ playerId) * SnapshotHashPrime;
			hash = (hash ^ (uint)actualClientId) * SnapshotHashPrime;
			hash = (hash ^ (uint)(actualClientId >> 32)) * SnapshotHashPrime;
			hash = (hash ^ health) * SnapshotHashPrime;
			hash = (hash ^ flags) * SnapshotHashPrime;
			return (hash ^ infectionPercent) * SnapshotHashPrime;
		}
	}
}
