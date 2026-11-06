namespace OtherPlayerStatusBars;

/// <summary>Status a player reported about themselves, as held by the host.</summary>
internal readonly struct OwnerStatusReport
{
	public OwnerStatusReport(ulong actualClientId, int health, bool isInfectionKnown, bool hasInfection, float infectionMeter, float receivedTime)
	{
		ActualClientId = actualClientId;
		Health = health;
		IsInfectionKnown = isInfectionKnown;
		HasInfection = hasInfection;
		InfectionMeter = infectionMeter;
		ReceivedTime = receivedTime;
	}

	public ulong ActualClientId { get; }

	public int Health { get; }

	public bool IsInfectionKnown { get; }

	public bool HasInfection { get; }

	public float InfectionMeter { get; }

	public float ReceivedTime { get; }
}
