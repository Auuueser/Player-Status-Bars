namespace OtherPlayerStatusBars;

internal static class StatusBarTextCache
{
	private const int CachedValueCount = 101;

	private static readonly string[] HealthLabels = BuildHealthLabels();

	private static readonly string[] InfectionLabels = BuildInfectionLabels();

	public static string GetHealthLabel(int currentHealth, int maxHealth)
	{
		if (maxHealth == 100 && currentHealth >= 0 && currentHealth < HealthLabels.Length)
		{
			return HealthLabels[currentHealth];
		}

		return $"HP {currentHealth}/{maxHealth}";
	}

	public static string GetInfectionLabel(int percent)
	{
		if (percent >= 0 && percent < InfectionLabels.Length)
		{
			return InfectionLabels[percent];
		}

		return $"INF {percent}%";
	}

	private static string[] BuildHealthLabels()
	{
		string[] labels = new string[CachedValueCount];
		for (int i = 0; i < labels.Length; i++)
		{
			labels[i] = $"HP {i}/100";
		}

		return labels;
	}

	private static string[] BuildInfectionLabels()
	{
		string[] labels = new string[CachedValueCount];
		for (int i = 0; i < labels.Length; i++)
		{
			labels[i] = $"INF {i}%";
		}

		return labels;
	}
}
