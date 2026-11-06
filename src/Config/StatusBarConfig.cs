using System;
using BepInEx.Configuration;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class StatusBarConfig
{
	public enum BarStyle
	{
		Classic,
		Segmented
	}

	public enum ColorPreset
	{
		Green,
		Red,
		Orange,
		Yellow,
		Blue,
		Cyan,
		White,
		Black,
		Slate
	}

	/// <summary>Low health colour of segmented bars, muted like <see cref="ResolveSegmentedColor"/>.</summary>
	public static readonly Color SegmentedCriticalColor = new(0.74f, 0.32f, 0.3f, 1f);

	/// <summary>Fixed part of the bar height, in UI units so it scales with the bars.</summary>
	private const float AnchorSpacingUnits = 14f;

	private readonly bool useChineseDescriptions;

	private Color cachedHealthFillColor;

	private Color cachedInfectionFillColor;

	private Color cachedSegmentedHealthFillColor;

	private Color cachedSegmentedInfectionFillColor;

	private Color cachedBackgroundColor;

	private Color cachedBorderColor;

	private float maxDistanceSqr;

	private float anchorYOffset;

	private StatusBarConfig(ConfigFile configFile, bool useChineseDescriptions)
	{
		this.useChineseDescriptions = useChineseDescriptions;
		EnabledEntry = Bind(configFile, StatusBarConfigText.Enabled, true);
		HideInOrbitEntry = Bind(configFile, StatusBarConfigText.HideInOrbit, true);
		MaxDistanceEntry = Bind(configFile, StatusBarConfigText.MaxDisplayDistance, 18f, new AcceptableValueRange<float>(2f, 80f));
		ShowInfectionBarsEntry = Bind(configFile, StatusBarConfigText.ShowInfectionBars, true);
		BarStyleEntry = Bind(configFile, StatusBarConfigText.BarStyle, BarStyle.Segmented);
		HeadOffsetEntry = Bind(configFile, StatusBarConfigText.HeadOffset, 0.65f, new AcceptableValueRange<float>(0f, 2f));
		UiScaleEntry = Bind(configFile, StatusBarConfigText.UiScale, 0.0085f, new AcceptableValueRange<float>(0.003f, 0.05f));
		ShowTextEntry = Bind(configFile, StatusBarConfigText.ShowText, true);
		HealthColorEntry = Bind(configFile, StatusBarConfigText.HealthBarColor, ColorPreset.Green);
		InfectionColorEntry = Bind(configFile, StatusBarConfigText.InfectionBarColor, ColorPreset.Orange);
		BackgroundColorEntry = Bind(configFile, StatusBarConfigText.BackgroundColor, ColorPreset.Slate);
		DebugLoggingEntry = Bind(configFile, StatusBarConfigText.DebugLogging, false);
		UpdateCachedValues();
	}

	public event Action? SettingsChanged;

	public ConfigEntry<bool> EnabledEntry { get; }

	public ConfigEntry<bool> HideInOrbitEntry { get; }

	public ConfigEntry<float> MaxDistanceEntry { get; }

	public ConfigEntry<bool> ShowInfectionBarsEntry { get; }

	public ConfigEntry<BarStyle> BarStyleEntry { get; }

	public ConfigEntry<float> HeadOffsetEntry { get; }

	public ConfigEntry<float> UiScaleEntry { get; }

	public ConfigEntry<bool> ShowTextEntry { get; }

	public ConfigEntry<ColorPreset> HealthColorEntry { get; }

	public ConfigEntry<ColorPreset> InfectionColorEntry { get; }

	public ConfigEntry<ColorPreset> BackgroundColorEntry { get; }

	public ConfigEntry<bool> DebugLoggingEntry { get; }

	public bool Enabled => EnabledEntry.Value;

	public bool HideInOrbit => HideInOrbitEntry.Value;

	public float MaxDistance => MaxDistanceEntry.Value;

	public float MaxDistanceSqr => maxDistanceSqr;

	public float AnchorYOffset => anchorYOffset;

	public float UiScale => UiScaleEntry.Value;

	public bool ShowText => ShowTextEntry.Value;

	public BarStyle Style => BarStyleEntry.Value;

	public bool ShowInfectionBars => ShowInfectionBarsEntry.Value;

	public bool DebugLogging => DebugLoggingEntry.Value;

	public int Revision { get; private set; }

	public static StatusBarConfig Create(ConfigFile configFile, bool useChineseDescriptions)
	{
		return new StatusBarConfig(configFile, useChineseDescriptions);
	}

	public Color GetHealthFillColor()
	{
		return cachedHealthFillColor;
	}

	public Color GetInfectionFillColor()
	{
		return cachedInfectionFillColor;
	}

	public Color GetSegmentedHealthFillColor()
	{
		return cachedSegmentedHealthFillColor;
	}

	public Color GetSegmentedInfectionFillColor()
	{
		return cachedSegmentedInfectionFillColor;
	}

	public Color GetBackgroundColor()
	{
		return cachedBackgroundColor;
	}

	public Color GetBorderColor()
	{
		return cachedBorderColor;
	}

	private ConfigEntry<T> Bind<T>(ConfigFile configFile, in ConfigText text, T defaultValue, AcceptableValueBase? acceptableValues = null)
	{
		ConfigDescription description = new(text.DisplayDescription(useChineseDescriptions), acceptableValues);
		ConfigEntry<T> entry = configFile.Bind(text.Section, text.Key, defaultValue, description);
		entry.SettingChanged += (_, _) => NotifySettingsChanged();
		return entry;
	}

	private void NotifySettingsChanged()
	{
		UpdateCachedValues();
		Revision++;
		SettingsChanged?.Invoke();
	}

	private void UpdateCachedValues()
	{
		cachedHealthFillColor = ResolveColor(HealthColorEntry.Value, 0.95f);
		cachedInfectionFillColor = ResolveColor(InfectionColorEntry.Value, 0.95f);
		cachedSegmentedHealthFillColor = ResolveSegmentedColor(HealthColorEntry.Value);
		cachedSegmentedInfectionFillColor = ResolveSegmentedColor(InfectionColorEntry.Value);
		cachedBackgroundColor = ResolveColor(BackgroundColorEntry.Value, 0.78f);
		cachedBorderColor = cachedBackgroundColor;
		cachedBorderColor.a = 1f;
		cachedBorderColor = Color.Lerp(cachedBorderColor, Color.white, 0.35f);

		float distance = MaxDistanceEntry.Value;
		maxDistanceSqr = distance * distance;
		anchorYOffset = HeadOffsetEntry.Value + AnchorSpacingUnits * UiScaleEntry.Value;
	}

	/// <summary>
	/// Muted presets for segmented bars. The game's post-processing boosts saturation by roughly 1.4x on
	/// screen, so these are chosen below the intended colours to land on them after that boost.
	/// </summary>
	private static Color ResolveSegmentedColor(ColorPreset preset)
	{
		return preset switch
		{
			ColorPreset.Green => new Color(0.44f, 0.64f, 0.46f, 1f),
			ColorPreset.Red => SegmentedCriticalColor,
			ColorPreset.Orange => new Color(0.755f, 0.512f, 0.305f, 1f),
			ColorPreset.Yellow => new Color(0.78f, 0.7f, 0.36f, 1f),
			ColorPreset.Blue => new Color(0.38f, 0.54f, 0.76f, 1f),
			ColorPreset.Cyan => new Color(0.36f, 0.68f, 0.7f, 1f),
			ColorPreset.White => new Color(0.85f, 0.85f, 0.85f, 1f),
			ColorPreset.Black => new Color(0.12f, 0.12f, 0.12f, 1f),
			_ => new Color(0.22f, 0.25f, 0.3f, 1f)
		};
	}

	private static Color ResolveColor(ColorPreset preset, float alpha)
	{
		Color color = preset switch
		{
			ColorPreset.Green => new Color(0.18f, 0.86f, 0.32f, alpha),
			ColorPreset.Red => new Color(0.88f, 0.25f, 0.22f, alpha),
			ColorPreset.Orange => new Color(0.93f, 0.47f, 0.16f, alpha),
			ColorPreset.Yellow => new Color(0.95f, 0.82f, 0.18f, alpha),
			ColorPreset.Blue => new Color(0.28f, 0.58f, 0.92f, alpha),
			ColorPreset.Cyan => new Color(0.18f, 0.83f, 0.88f, alpha),
			ColorPreset.White => new Color(0.95f, 0.95f, 0.95f, alpha),
			ColorPreset.Black => new Color(0.08f, 0.08f, 0.08f, alpha),
			_ => new Color(0.12f, 0.15f, 0.2f, alpha)
		};

		color.a = alpha;
		return color;
	}
}
