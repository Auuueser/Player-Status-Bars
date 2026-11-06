using System;

namespace OtherPlayerStatusBars;

/// <summary>
/// Motion of a segmented bar. A drop lowers the fill at once and leaves a trail that catches up after a
/// short pause; a rise marks the new value at once and the fill climbs to it.
/// Unity-free so its behaviour can be unit tested; callers pass the current time in.
/// </summary>
internal sealed class BarValueAnimation
{
	private const float TrailHoldSeconds = 0.4f;

	private const float TrailPointsPerSecond = 66f;

	private const float RisePointsPerSecond = 54f;

	private bool hasValue;

	private float lastUpdateTime;

	private float trailHoldEndTime;

	public float Fill { get; private set; }

	public float Trail { get; private set; }

	public int Target { get; private set; }

	public void Update(int targetValue, float now)
	{
		if (!hasValue)
		{
			hasValue = true;
			Fill = targetValue;
			Trail = targetValue;
			Target = targetValue;
			lastUpdateTime = now;
			return;
		}

		float elapsed = Math.Max(0f, now - lastUpdateTime);
		lastUpdateTime = now;
		Target = targetValue;
		if (targetValue < Fill)
		{
			Trail = Math.Max(Trail, Fill);
			Fill = targetValue;
			trailHoldEndTime = now + TrailHoldSeconds;
		}
		else if (Fill < targetValue)
		{
			Fill = Math.Min(targetValue, Fill + RisePointsPerSecond * elapsed);
		}

		if (now >= trailHoldEndTime)
		{
			Trail -= TrailPointsPerSecond * elapsed;
		}

		Trail = Math.Max(Trail, Fill);
	}

	public void Reset()
	{
		hasValue = false;
		Fill = 0f;
		Trail = 0f;
		Target = 0;
		lastUpdateTime = 0f;
		trailHoldEndTime = 0f;
	}
}
