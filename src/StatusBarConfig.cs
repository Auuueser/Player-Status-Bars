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
		return Create(configFile, useChineseDescriptions: false);
	}

	public static StatusBarConfig Create(ConfigFile configFile, bool useChineseDescriptions)
	{
		return new StatusBarConfig(configFile, useChineseDescriptions);
	}

	private StatusBarConfig(ConfigFile configFile, bool useChineseDescriptions)
	{
		this.useChineseDescriptions = useChineseDescriptions;
		enabled = configFile.Bind("General", "Enabled", true, Description("Enable other player status bars.", "启用其他玩家状态条。"));
		hideInOrbit = configFile.Bind("General", "Hide In Orbit", true, Description("Hide player status bars while the ship is still in orbit.", "飞船仍在轨道阶段时隐藏玩家状态条。"));
		maxDistance = configFile.Bind("General", "Max Display Distance", 18f, new ConfigDescription(Description("Hide other player status bars beyond this distance.", "超过此距离时隐藏其他玩家状态条。"), new AcceptableValueRange<float>(2f, 80f)));
		headOffset = configFile.Bind("Layout", "Head Offset", 0.65f, new ConfigDescription(Description("Vertical world-space offset above the player head anchor.", "相对于玩家头部锚点的世界空间垂直偏移。"), new AcceptableValueRange<float>(0f, 2f)));
		barSpacing = configFile.Bind("Layout", "Bar Spacing", 14f, new ConfigDescription(Description("Vertical spacing between the health bar and infection bar.", "血条与感染条之间的垂直间距。"), new AcceptableValueRange<float>(6f, 48f)));
		healthBarYOffset = configFile.Bind("Layout", "Health Bar Y Offset", 0f, new ConfigDescription(Description("Local UI Y offset for the health bar.", "血条的本地 UI Y 轴偏移。"), new AcceptableValueRange<float>(-64f, 64f)));
		infectionBarYOffset = configFile.Bind("Layout", "Infection Bar Y Offset", 25f, new ConfigDescription(Description("Local UI Y offset for the infection bar.", "感染条的本地 UI Y 轴偏移。"), new AcceptableValueRange<float>(-64f, 64f)));
		uiScale = configFile.Bind("Layout", "UI Scale", 0.0085f, new ConfigDescription(Description("World-space scale of the player status bar canvas.", "玩家状态条画布的世界空间缩放。"), new AcceptableValueRange<float>(0.003f, 0.05f)));
		showHealthText = configFile.Bind("Text", "Show Health Text", true, Description("Show health numbers on the health bar.", "在血条上显示血量数字。"));
		showInfectionText = configFile.Bind("Text", "Show Infection Text", true, Description("Show infection percentage on the infection bar.", "在感染条上显示感染百分比。"));
		healthColor = configFile.Bind("Colors", "Health Bar Color", ColorPreset.Green, Description("Fill color preset for the health bar.", "血条填充颜色预设。"));
		infectionColor = configFile.Bind("Colors", "Infection Bar Color", ColorPreset.Orange, Description("Fill color preset for the infection bar.", "感染条填充颜色预设。"));
		backgroundColor = configFile.Bind("Colors", "Background Color", ColorPreset.Slate, Description("Background color preset for both bars.", "血条和感染条的背景颜色预设。"));
		infectionDisplayMode = configFile.Bind("General", "Infection Bar Display Mode", InfectionBarDisplayMode.ShowOnlyWhenInfected, Description("Always show the infection bar, or only show it when infection is above zero.", "始终显示感染条，或仅在感染值大于零时显示。"));
		criticalHealthMode = configFile.Bind("Compatibility", "Critical Health Sync Mode", CriticalHealthSyncMode.VanillaPrediction, Description("VanillaPrediction infers 5 HP when remote vanilla clients expose critical state at stale 20 HP. TrustRawHealthAt20 keeps 20 HP for active-bleed or custom-injury mods.", "VanillaPrediction 会在远端原版客户端显示 20 血但处于重伤状态时推断为 5 血；TrustRawHealthAt20 会信任 20 血，适合主动流血或自定义重伤模组。"));
		debugLogging = configFile.Bind("Debug", "Debug Logging", false, Description("Write throttled diagnostic logs for player status bar creation, filtering, visibility, camera state, and health display decisions.", "写入节流诊断日志，用于排查玩家状态条创建、过滤、可见性、相机状态和血量显示决策。"));

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

	private string Description(string english, string chinese)
	{
		return useChineseDescriptions ? chinese : english;
	}

	private readonly ConfigEntry<bool> enabled;

	private readonly bool useChineseDescriptions;

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
