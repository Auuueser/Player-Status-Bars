using System;

namespace OtherPlayerStatusBars;

/// <summary>
/// Keeps a bar still while its anchor sways slightly, such as a head bone during idle animation. At the
/// game's low render resolution even tiny movement makes thin lines shimmer. Beyond the dead zone the bar
/// is pulled along by the zone's edge, so it follows without jumping.
/// Unity-free so its behaviour can be unit tested.
/// </summary>
internal sealed class BarPositionStabilizer
{
	private const float DeadZoneMeters = 0.04f;

	private bool hasPosition;

	public float X { get; private set; }

	public float Y { get; private set; }

	public float Z { get; private set; }

	public void Update(float targetX, float targetY, float targetZ)
	{
		if (!hasPosition)
		{
			hasPosition = true;
			X = targetX;
			Y = targetY;
			Z = targetZ;
			return;
		}

		float deltaX = targetX - X;
		float deltaY = targetY - Y;
		float deltaZ = targetZ - Z;
		float distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);
		if (distance <= DeadZoneMeters)
		{
			return;
		}

		float pull = (distance - DeadZoneMeters) / distance;
		X += deltaX * pull;
		Y += deltaY * pull;
		Z += deltaZ * pull;
	}

	public void Reset()
	{
		hasPosition = false;
	}
}
