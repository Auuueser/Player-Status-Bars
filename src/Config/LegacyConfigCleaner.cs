using System;
using System.Collections.Generic;

namespace OtherPlayerStatusBars;

/// <summary>
/// Removes settings this mod no longer has. BepInEx keeps unknown keys in the .cfg file forever,
/// so without this they would stay visible in config editors with no effect.
/// </summary>
internal static class LegacyConfigCleaner
{
	private static readonly string[] ObsoleteKeys =
	{
		"Bar Spacing",
		"Health Bar Y Offset",
		"Infection Bar Y Offset",
		"Infection Bar Display Mode",
		"Critical Health Sync Mode",
		"Show Health Text",
		"Show Infection Text",
		"Enable Ten Health Fallback",
		"Ten Health Fallback Delay Seconds"
	};

	public static bool TryRemoveObsoleteKeys(string[] lines, out string[] cleanedLines)
	{
		List<string> keptLines = new(lines.Length);
		foreach (string line in lines)
		{
			if (!IsObsoleteEntry(line))
			{
				keptLines.Add(line);
			}
		}

		cleanedLines = keptLines.ToArray();
		return cleanedLines.Length != lines.Length;
	}

	private static bool IsObsoleteEntry(string line)
	{
		int separator = line.IndexOf('=');
		if (separator <= 0 || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
		{
			return false;
		}

		string key = line.Substring(0, separator).Trim();
		return Array.IndexOf(ObsoleteKeys, key) >= 0;
	}
}
