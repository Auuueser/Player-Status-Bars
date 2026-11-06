namespace OtherPlayerStatusBars;

internal static class StatusBarTextCache
{
	private const int CachedValueCount = 101;

	private static readonly string[] HealthLabels = BuildHealthLabels();

	private static readonly string[] InfectionLabels = BuildInfectionLabels();

	private static readonly string[] NumberLabels = BuildNumberLabels();

	private static readonly string[] PercentLabels = BuildPercentLabels();

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

	public static string GetNumberLabel(int value)
	{
		return value >= 0 && value < NumberLabels.Length ? NumberLabels[value] : value.ToString();
	}

	public static string GetPercentLabel(int percent)
	{
		return percent >= 0 && percent < PercentLabels.Length ? PercentLabels[percent] : $"{percent}%";
	}

	private static string[] BuildNumberLabels()
	{
		string[] labels = new string[CachedValueCount];
		for (int i = 0; i < labels.Length; i++)
		{
			labels[i] = i.ToString();
		}

		return labels;
	}

	private static string[] BuildPercentLabels()
	{
		string[] labels = new string[CachedValueCount];
		for (int i = 0; i < labels.Length; i++)
		{
			labels[i] = $"{i}%";
		}

		return labels;
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
