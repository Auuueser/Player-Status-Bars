namespace OtherPlayerStatusBars;

/// <summary>
/// Splits a texture row into cells. Cell width is rarely a whole number of texels, so a cell's bounds
/// must agree with <see cref="CellAt"/> or its edges come out with uneven widths.
/// </summary>
internal static class SegmentedCellLayout
{
	public static int CellAt(int x, int width, int cellCount)
	{
		return x * cellCount / width;
	}

	/// <summary>First texel of a cell; passing the next cell gives the end of this one.</summary>
	public static int CellStart(int cell, int width, int cellCount)
	{
		return (cell * width + cellCount - 1) / cellCount;
	}

	/// <summary>Alpha of a translucent edge laid over itself, as happens in the corner of two edges.</summary>
	public static float StackedAlpha(float alpha, int layers)
	{
		float clear = 1f;
		for (int i = 0; i < layers; i++)
		{
			clear *= 1f - alpha;
		}

		return 1f - clear;
	}
}
