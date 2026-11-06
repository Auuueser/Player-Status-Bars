using System;

namespace OtherPlayerStatusBars;

/// <summary>
/// Edge colours for a coloured cell, derived from its fill. UI alpha blends in linear light and the
/// game's post-processing adds contrast (about 1.42 * x^1.23 on greys) and saturation, so translucent
/// edges cannot land on the approved colours. These are solid colours, fitted to on-screen pixels.
/// Green edges are also thin enough to pick up the frame's grey at game resolution, hence the boost.
/// </summary>
internal static class SegmentedPalette
{
	private const float DarkScale = 0.64f;

	private const float GreenDarkScaleBoost = 0.33f;

	private const float GreenSaturationBoost = 2f;

	private const float LightBlend = 0.12f;

	private const float CrossingWhite = 0.2f;

	private const float CrossingGreenPull = 0.25f;

	public static (float R, float G, float B) Dark(float r, float g, float b)
	{
		float greenLead = Math.Max(0f, g - Math.Max(r, b));
		float scale = DarkScale + GreenDarkScaleBoost * greenLead;
		float saturation = 1f + GreenSaturationBoost * greenLead;
		float scaledR = r * scale, scaledG = g * scale, scaledB = b * scale;
		float luma = 0.2126f * scaledR + 0.7152f * scaledG + 0.0722f * scaledB;
		return (Saturate(scaledR, luma, saturation), Saturate(scaledG, luma, saturation), Saturate(scaledB, luma, saturation));
	}

	public static (float R, float G, float B) Light(float r, float g, float b)
	{
		return (r + (1f - r) * LightBlend, g + (1f - g) * LightBlend, b + (1f - b) * LightBlend);
	}

	/// <summary>
	/// Top right and bottom left, where the light edge lies over the dark one. As in the design this
	/// whitens the dark edge rather than mixing in the light one, so the corner comes out greyer than both.
	/// </summary>
	public static (float R, float G, float B) Crossing(float darkR, float darkG, float darkB)
	{
		float r = darkR + (1f - darkR) * CrossingWhite;
		float g = darkG + (1f - darkG) * CrossingWhite;
		float b = darkB + (1f - darkB) * CrossingWhite;
		return (r, g - CrossingGreenPull * Math.Max(0f, g - Math.Max(r, b)), b);
	}

	private static float Saturate(float channel, float luma, float saturation)
	{
		return Math.Min(1f, Math.Max(0f, luma + (channel - luma) * saturation));
	}
}
