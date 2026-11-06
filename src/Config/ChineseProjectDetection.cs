using System;

namespace OtherPlayerStatusBars;

/// <summary>
/// Recognises LC Chinese Project (https://github.com/Auuueser/LC-Chinese-Project).
/// Matching never looks at its version, because that mod keeps updating independently.
/// </summary>
internal static class ChineseProjectDetection
{
	public const string PluginGuid = "Aueser.LCChineseProject";

	public const string LegacyPluginGuid = "cn.codex.v81testchn";

	public const string ThunderstoreDirectoryName = "Aueser-LC_Chinese_Project";

	public const string PluginFileName = "V81TestChn.dll";

	public static bool Matches(string? guid, string? location)
	{
		return string.Equals(guid, PluginGuid, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(guid, LegacyPluginGuid, StringComparison.OrdinalIgnoreCase)
			|| Contains(location, ThunderstoreDirectoryName)
			|| Contains(location, PluginFileName);
	}

	private static bool Contains(string? value, string term)
	{
		return value != null && value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
	}
}
