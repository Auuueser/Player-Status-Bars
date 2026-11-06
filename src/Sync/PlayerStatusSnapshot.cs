namespace OtherPlayerStatusBars;

internal readonly struct PlayerStatusSnapshot
{
	public PlayerStatusSnapshot(ulong actualClientId, int health, bool isHealthExact, bool isDead, bool isCritical, bool isInfectionKnown, bool isInfectionExact, bool hasInfection, float infectionMeter, float receivedTime)
	{
		ActualClientId = actualClientId;
		Health = health;
		IsHealthExact = isHealthExact;
		IsDead = isDead;
		IsCritical = isCritical;
		IsInfectionKnown = isInfectionKnown;
		IsInfectionExact = isInfectionExact;
		HasInfection = hasInfection;
		InfectionMeter = infectionMeter;
		ReceivedTime = receivedTime;
	}

	public ulong ActualClientId { get; }

	public int Health { get; }

	public bool IsHealthExact { get; }

	public bool IsDead { get; }

	public bool IsCritical { get; }

	public bool IsInfectionKnown { get; }

	public bool IsInfectionExact { get; }

	public bool HasInfection { get; }

	public float InfectionMeter { get; }

	public float ReceivedTime { get; }
}
