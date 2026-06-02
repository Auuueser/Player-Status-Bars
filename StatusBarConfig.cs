using System;
using BepInEx.Configuration;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class StatusBarConfig
{
	public enum InfectionBarDisplayMode
	{
		AlwaysVisible,
		ShowOnlyWhenInfected
	}

	public enum CriticalHealthSyncMode
	{
		VanillaPrediction,
		TrustRawHealthAt20
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

	public bool Enabled => enabled.Value;

	public float MaxDistance => maxDistance.Value;

	public float MaxDistanceSqr => maxDistanceSqr;

	public float HeadOffset => headOffset.Value;

	public float AnchorYOffset => anchorYOffset;

	public float BarSpacing => barSpacing.Value;

	public float HealthBarYOffset => healthBarYOffset.Value;

	public float InfectionBarYOffset => infectionBarYOffset.Value;

	public float UiScale => uiScale.Value;

	public bool HideInOrbit => hideInOrbit.Value;

	public bool ShowHealthText => showHealthText.Value;

	public bool ShowInfectionText => showInfectionText.Value;

	public bool DebugLogging => debugLogging.Value;

	public InfectionBarDisplayMode InfectionDisplayMode => infectionDisplayMode.Value;

	public CriticalHealthSyncMode CriticalHealthMode => criticalHealthMode.Value;

	public int Revision { get; private set; }

	public event Action? SettingsChanged;

	public static StatusBarConfig Create(ConfigFile configFile)
	{
		return new StatusBarConfig(configFile);
	}

	private StatusBarConfig(ConfigFile configFile)
	{
		enabled = configFile.Bind("General", "Enabled", true, "Enable other player status bars.");
		hideInOrbit = configFile.Bind("General", "Hide In Orbit", true, "Hide player status bars while the ship is still in orbit.");
		maxDistance = configFile.Bind("General", "Max Display Distance", 18f, new ConfigDescription("Hide other player status bars beyond this distance.", new AcceptableValueRange<float>(2f, 80f)));
		headOffset = configFile.Bind("Layout", "Head Offset", 0.65f, new ConfigDescription("Vertical world-space offset above the player head anchor.", new AcceptableValueRange<float>(0f, 2f)));
		barSpacing = configFile.Bind("Layout", "Bar Spacing", 14f, new ConfigDescription("Vertical spacing between the health bar and infection bar.", new AcceptableValueRange<float>(6f, 48f)));
		healthBarYOffset = configFile.Bind("Layout", "Health Bar Y Offset", 0f, new ConfigDescription("Local UI Y offset for the health bar.", new AcceptableValueRange<float>(-64f, 64f)));
		infectionBarYOffset = configFile.Bind("Layout", "Infection Bar Y Offset", 25f, new ConfigDescription("Local UI Y offset for the infection bar.", new AcceptableValueRange<float>(-64f, 64f)));
		uiScale = configFile.Bind("Layout", "UI Scale", 0.0085f, new ConfigDescription("World-space scale of the player status bar canvas.", new AcceptableValueRange<float>(0.003f, 0.05f)));
		showHealthText = configFile.Bind("Text", "Show Health Text", true, "Show health numbers on the health bar.");
		showInfectionText = configFile.Bind("Text", "Show Infection Text", true, "Show infection percentage on the infection bar.");
		healthColor = configFile.Bind("Colors", "Health Bar Color", ColorPreset.Green, "Fill color preset for the health bar.");
		infectionColor = configFile.Bind("Colors", "Infection Bar Color", ColorPreset.Orange, "Fill color preset for the infection bar.");
		backgroundColor = configFile.Bind("Colors", "Background Color", ColorPreset.Slate, "Background color preset for both bars.");
		infectionDisplayMode = configFile.Bind("General", "Infection Bar Display Mode", InfectionBarDisplayMode.ShowOnlyWhenInfected, "Always show the infection bar, or only show it when infection is above zero.");
		criticalHealthMode = configFile.Bind("Compatibility", "Critical Health Sync Mode", CriticalHealthSyncMode.VanillaPrediction, "VanillaPrediction infers 5 HP when remote vanilla clients expose critical state at stale 20 HP. TrustRawHealthAt20 keeps 20 HP for active-bleed or custom-injury mods.");
		debugLogging = configFile.Bind("Debug", "Debug Logging", false, "Write throttled diagnostic logs for player status bar creation, filtering, visibility, and camera state.");

		Subscribe(enabled);
		Subscribe(hideInOrbit);
		Subscribe(maxDistance);
		Subscribe(headOffset);
		Subscribe(barSpacing);
		Subscribe(healthBarYOffset);
		Subscribe(infectionBarYOffset);
		Subscribe(uiScale);
		Subscribe(showHealthText);
		Subscribe(showInfectionText);
		Subscribe(healthColor);
		Subscribe(infectionColor);
		Subscribe(backgroundColor);
		Subscribe(infectionDisplayMode);
		Subscribe(criticalHealthMode);
		Subscribe(debugLogging);
		UpdateCachedValues();
	}

	public Color GetHealthFillColor()
	{
		return cachedHealthFillColor;
	}

	public Color GetInfectionFillColor()
	{
		return cachedInfectionFillColor;
	}

	public Color GetBackgroundColor()
	{
		return cachedBackgroundColor;
	}

	public Color GetBorderColor()
	{
		return cachedBorderColor;
	}

	private void Subscribe<T>(ConfigEntry<T> entry)
	{
		entry.SettingChanged += (_, _) => NotifySettingsChanged();
	}

	private void NotifySettingsChanged()
	{
		UpdateCachedValues();
		Revision++;
		SettingsChanged?.Invoke();
	}

	private void UpdateCachedValues()
	{
		cachedHealthFillColor = ResolveColor(healthColor.Value, 0.95f);
		cachedInfectionFillColor = ResolveColor(infectionColor.Value, 0.95f);
		cachedBackgroundColor = ResolveColor(backgroundColor.Value, 0.78f);
		cachedBorderColor = cachedBackgroundColor;
		cachedBorderColor.a = 1f;
		cachedBorderColor = Color.Lerp(cachedBorderColor, Color.white, 0.35f);

		float distance = maxDistance.Value;
		maxDistanceSqr = distance * distance;
		anchorYOffset = headOffset.Value + barSpacing.Value * uiScale.Value;
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

	private readonly ConfigEntry<bool> enabled;

	private readonly ConfigEntry<bool> hideInOrbit;

	private readonly ConfigEntry<float> maxDistance;

	private readonly ConfigEntry<float> headOffset;

	private readonly ConfigEntry<float> barSpacing;

	private readonly ConfigEntry<float> healthBarYOffset;

	private readonly ConfigEntry<float> infectionBarYOffset;

	private readonly ConfigEntry<float> uiScale;

	private readonly ConfigEntry<bool> showHealthText;

	private readonly ConfigEntry<bool> showInfectionText;

	private readonly ConfigEntry<ColorPreset> healthColor;

	private readonly ConfigEntry<ColorPreset> infectionColor;

	private readonly ConfigEntry<ColorPreset> backgroundColor;

	private readonly ConfigEntry<InfectionBarDisplayMode> infectionDisplayMode;

	private readonly ConfigEntry<CriticalHealthSyncMode> criticalHealthMode;

	private readonly ConfigEntry<bool> debugLogging;

	private Color cachedHealthFillColor;

	private Color cachedInfectionFillColor;

	private Color cachedBackgroundColor;

	private Color cachedBorderColor;

	private float maxDistanceSqr;

	private float anchorYOffset;
}
