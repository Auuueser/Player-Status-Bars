namespace OtherPlayerStatusBars;

/// <summary>
/// Whether the host currently allows infection bars, as seen by a client. Hosts without this version
/// never send it, so the answer stays no and clients show no infection bars in their rooms.
/// </summary>
internal sealed class HostInfectionPermission
{
	private readonly float validSeconds;

	private bool isAllowed;

	private float receivedTime;

	public HostInfectionPermission(float validSeconds)
	{
		this.validSeconds = validSeconds;
	}

	public void Receive(bool allowed, float now)
	{
		isAllowed = allowed;
		receivedTime = now;
	}

	public bool IsAllowed(float now)
	{
		return isAllowed && now - receivedTime <= validSeconds;
	}

	public void Reset()
	{
		isAllowed = false;
		receivedTime = 0f;
	}
}
