using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class StatusBarConfig
{
	public enum InfectionBarDisplayMode
	{
		AlwaysVisible,
		ShowOnlyWhenInfected
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

	public float HeadOffset => headOffset.Value;

	public float BarSpacing => barSpacing.Value;

	public float HealthBarYOffset => healthBarYOffset.Value;

	public float InfectionBarYOffset => infectionBarYOffset.Value;

	public float UiScale => uiScale.Value;

	public bool HideInOrbit => hideInOrbit.Value;

	public bool ShowHealthText => showHealthText.Value;

	public bool ShowInfectionText => showInfectionText.Value;

	public InfectionBarDisplayMode InfectionDisplayMode => infectionDisplayMode.Value;

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
	}

	public Color GetHealthFillColor()
	{
		return ResolveColor(healthColor.Value, 0.95f);
	}

	public Color GetInfectionFillColor()
	{
		return ResolveColor(infectionColor.Value, 0.95f);
	}

	public Color GetBackgroundColor()
	{
		return ResolveColor(backgroundColor.Value, 0.78f);
	}

	public Color GetBorderColor()
	{
		Color background = GetBackgroundColor();
		background.a = 1f;
		return Color.Lerp(background, Color.white, 0.35f);
	}

	private void Subscribe<T>(ConfigEntry<T> entry)
	{
		entry.SettingChanged += (_, _) => NotifySettingsChanged();
	}

	private void NotifySettingsChanged()
	{
		Revision++;
		SettingsChanged?.Invoke();
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

	public static class LethalConfigIntegration
	{
		public static void Register(StatusBarConfig config)
		{
			LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(config.enabled, new BoolCheckBoxOptions
			{
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(config.hideInOrbit, new BoolCheckBoxOptions
			{
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(config.maxDistance, new FloatSliderOptions
			{
				Min = 2f,
				Max = 80f,
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(config.headOffset, new FloatSliderOptions
			{
				Min = 0f,
				Max = 2f,
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(config.barSpacing, new FloatSliderOptions
			{
				Min = 6f,
				Max = 48f,
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(config.healthBarYOffset, new FloatSliderOptions
			{
				Min = -64f,
				Max = 64f,
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(config.infectionBarYOffset, new FloatSliderOptions
			{
				Min = -64f,
				Max = 64f,
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(config.uiScale, new FloatSliderOptions
			{
				Min = 0.003f,
				Max = 0.05f,
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(config.showHealthText, new BoolCheckBoxOptions
			{
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(config.showInfectionText, new BoolCheckBoxOptions
			{
				RequiresRestart = false
			}));
			LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<ColorPreset>(config.healthColor, false));
			LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<ColorPreset>(config.infectionColor, false));
			LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<ColorPreset>(config.backgroundColor, false));
			LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<InfectionBarDisplayMode>(config.infectionDisplayMode, false));
			LethalConfigManager.AddConfigItem(new GenericButtonConfigItem("General", "Refresh all player bars", "Forces all visible player bars to refresh on the next update.", "Refresh", () =>
			{
				config.NotifySettingsChanged();
			}));
		}
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
}
