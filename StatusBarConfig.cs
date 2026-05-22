using System;
using System.Collections.Generic;
using System.Reflection;
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

	public bool DebugLogging => debugLogging.Value;

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
		Subscribe(debugLogging);
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
			AddBool(config.enabled);
			AddBool(config.hideInOrbit);
			AddFloat(config.maxDistance, 2f, 80f);
			AddFloat(config.headOffset, 0f, 2f);
			AddFloat(config.barSpacing, 6f, 48f);
			AddFloat(config.healthBarYOffset, -64f, 64f);
			AddFloat(config.infectionBarYOffset, -64f, 64f);
			AddFloat(config.uiScale, 0.003f, 0.05f);
			AddBool(config.showHealthText);
			AddBool(config.showInfectionText);
			AddEnum(config.healthColor);
			AddEnum(config.infectionColor);
			AddEnum(config.backgroundColor);
			AddEnum(config.infectionDisplayMode);
			AddBool(config.debugLogging);
		}

		private static void AddBool(ConfigEntry<bool> entry)
		{
			AddConfigItem(CreateItem("LethalConfig.ConfigItems.BoolCheckBoxConfigItem", entry, CreateOptions("LethalConfig.ConfigItems.Options.BoolCheckBoxOptions",
				("RequiresRestart", false))));
		}

		private static void AddFloat(ConfigEntry<float> entry, float min, float max)
		{
			AddConfigItem(CreateItem("LethalConfig.ConfigItems.FloatSliderConfigItem", entry, CreateOptions("LethalConfig.ConfigItems.Options.FloatSliderOptions",
				("Min", min),
				("Max", max),
				("RequiresRestart", false))));
		}

		private static void AddEnum<T>(ConfigEntry<T> entry)
		{
			Type itemType = RequireType("LethalConfig.ConfigItems.EnumDropDownConfigItem`1").MakeGenericType(typeof(T));
			AddConfigItem(CreateItem(itemType, entry, false));
		}

		private static object CreateOptions(string typeName, params (string Name, object Value)[] values)
		{
			object options = Activator.CreateInstance(RequireType(typeName)) ?? throw new InvalidOperationException($"Failed to create {typeName}.");
			Type optionsType = options.GetType();
			foreach ((string name, object value) in values)
			{
				PropertyInfo? property = optionsType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
				if (property == null || !property.CanWrite)
				{
					throw new MissingMemberException(optionsType.FullName, name);
				}

				property.SetValue(options, value);
			}

			return options;
		}

		private static object CreateItem(string typeName, params object[] args)
		{
			return CreateItem(RequireType(typeName), args);
		}

		private static object CreateItem(Type type, params object[] args)
		{
			return Activator.CreateInstance(type, args) ?? throw new InvalidOperationException($"Failed to create {type.FullName}.");
		}

		private static void AddConfigItem(object configItem)
		{
			Type managerType = RequireType("LethalConfig.LethalConfigManager");
			foreach (MethodInfo method in managerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				ParameterInfo[] parameters = method.GetParameters();
				if (method.Name == "AddConfigItem"
					&& parameters.Length == 2
					&& parameters[0].ParameterType.IsInstanceOfType(configItem)
					&& parameters[1].ParameterType == typeof(Assembly))
				{
					method.Invoke(null, new object[] { configItem, Assembly.GetExecutingAssembly() });
					return;
				}
			}

			foreach (MethodInfo method in managerType.GetMethods(BindingFlags.Public | BindingFlags.Static))
			{
				ParameterInfo[] parameters = method.GetParameters();
				if (method.Name == "AddConfigItem" && parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(configItem))
				{
					method.Invoke(null, new[] { configItem });
					return;
				}
			}

			throw new MissingMethodException(managerType.FullName, "AddConfigItem");
		}

		private static Type RequireType(string typeName)
		{
			return Type.GetType($"{typeName}, LethalConfig", false)
				?? throw new TypeLoadException($"Could not find optional LethalConfig type '{typeName}'.");
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

	private readonly ConfigEntry<bool> debugLogging;
}
