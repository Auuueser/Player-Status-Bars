namespace OtherPlayerStatusBars;

/// <summary>Decides when a client sends its own status: on any change, otherwise on a heartbeat.</summary>
internal sealed class StatusReportGate
{
	private readonly float heartbeatInterval;

	private bool hasSentReport;

	private byte lastPlayerId;

	private ulong lastActualClientId;

	private byte lastFlags;

	private byte lastInfectionPercent;

	private byte lastHealth;

	private float nextHeartbeatTime;

	public StatusReportGate(float heartbeatInterval)
	{
		this.heartbeatInterval = heartbeatInterval;
	}

	public bool ShouldSend(byte playerId, ulong actualClientId, byte flags, byte infectionPercent, byte health, float now)
	{
		return !hasSentReport
			|| playerId != lastPlayerId
			|| actualClientId != lastActualClientId
			|| flags != lastFlags
			|| infectionPercent != lastInfectionPercent
			|| health != lastHealth
			|| now >= nextHeartbeatTime;
	}

	public void MarkSent(byte playerId, ulong actualClientId, byte flags, byte infectionPercent, byte health, float now)
	{
		hasSentReport = true;
		lastPlayerId = playerId;
		lastActualClientId = actualClientId;
		lastFlags = flags;
		lastInfectionPercent = infectionPercent;
		lastHealth = health;
		nextHeartbeatTime = now + heartbeatInterval;
	}

	public void Reset()
	{
		hasSentReport = false;
		nextHeartbeatTime = 0f;
	}
}
