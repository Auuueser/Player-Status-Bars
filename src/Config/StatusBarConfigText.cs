namespace OtherPlayerStatusBars;

/// <summary>
/// Text for one config entry. Section and Key are what is stored in the .cfg file and always stay
/// English, so turning LC Chinese Project on or off never loses a player's settings.
/// </summary>
internal readonly struct ConfigText
{
	public ConfigText(string section, string key, string description, string chineseSection, string chineseName, string chineseDescription)
	{
		Section = section;
		Key = key;
		Description = description;
		ChineseSection = chineseSection;
		ChineseName = chineseName;
		ChineseDescription = chineseDescription;
	}

	public string Section { get; }

	public string Key { get; }

	public string Description { get; }

	public string ChineseSection { get; }

	public string ChineseName { get; }

	public string ChineseDescription { get; }

	public string DisplaySection(bool useChinese)
	{
		return useChinese ? ChineseSection : Section;
	}

	public string DisplayName(bool useChinese)
	{
		return useChinese ? ChineseName : Key;
	}

	public string DisplayDescription(bool useChinese)
	{
		return useChinese ? ChineseDescription : Description;
	}
}

internal static class StatusBarConfigText
{
	public const string ModDescription = "Shows health and infection bars above other players.";

	public const string ChineseModDescription = "在其他玩家头顶显示血条和感染条。";

	private const string General = "General";

	private const string Layout = "Layout";

	private const string Text = "Text";

	private const string Colors = "Colors";

	private const string Debug = "Debug";

	private const string ChineseGeneral = "常规";

	private const string ChineseLayout = "布局";

	private const string ChineseText = "文字";

	private const string ChineseColors = "颜色";

	private const string ChineseDebug = "调试";

	private const string ChineseColorOptions = "可选值：Green 绿色、Red 红色、Orange 橙色、Yellow 黄色、Blue 蓝色、Cyan 青色、White 白色、Black 黑色、Slate 石板灰。";

	public static readonly ConfigText Enabled = new(
		General,
		"Enabled",
		"Show status bars above other players. Status sync keeps running while this is off, so other players still receive your data.",
		ChineseGeneral,
		"启用状态条",
		"在其他玩家头顶显示状态条。关闭后状态同步仍会运行，其他玩家依然能收到你的数据。");

	public static readonly ConfigText HideInOrbit = new(
		General,
		"Hide In Orbit",
		"Hide status bars while the ship is in orbit.",
		ChineseGeneral,
		"轨道中隐藏",
		"飞船处于轨道时隐藏状态条。");

	public static readonly ConfigText MaxDisplayDistance = new(
		General,
		"Max Display Distance",
		"Hide status bars beyond this distance.",
		ChineseGeneral,
		"最大显示距离",
		"超过此距离时隐藏状态条。");

	public static readonly ConfigText ShowInfectionBars = new(
		General,
		"Show Infection Bars",
		"Host setting for the whole room. Infection bars only appear when the host runs this version of the mod with this turned on; the value on other players has no effect.",
		ChineseGeneral,
		"显示感染条",
		"主机设置，对整个房间生效。只有主机安装了此版本模组并开启此项时才显示感染条，其他玩家的设置不起作用。");

	public static readonly ConfigText BarStyle = new(
		Layout,
		"Bar Style",
		"Look of the status bars. Segmented shows one compact panel with ten-cell bars and animated damage and healing. Classic is the original style.",
		ChineseLayout,
		"状态条样式",
		"状态条的外观。Segmented 为十格样式，使用合并面板，并带有掉血和回血动画；Classic 为经典样式。");

	public static readonly ConfigText HeadOffset = new(
		Layout,
		"Head Offset",
		"Height of the status bars above the player's head.",
		ChineseLayout,
		"头顶高度",
		"状态条距离玩家头顶的高度。");

	public static readonly ConfigText UiScale = new(
		Layout,
		"UI Scale",
		"Size of the status bars.",
		ChineseLayout,
		"界面缩放",
		"状态条的大小。");

	public static readonly ConfigText ShowText = new(
		Text,
		"Show Text",
		"Show health numbers and infection percentage on the bars.",
		ChineseText,
		"显示文字",
		"在状态条上显示血量数字和感染百分比。");

	public static readonly ConfigText HealthBarColor = new(
		Colors,
		"Health Bar Color",
		"Fill color of the health bar.",
		ChineseColors,
		"血条颜色",
		"血条的填充颜色。" + ChineseColorOptions);

	public static readonly ConfigText InfectionBarColor = new(
		Colors,
		"Infection Bar Color",
		"Fill color of the infection bar.",
		ChineseColors,
		"感染条颜色",
		"感染条的填充颜色。" + ChineseColorOptions);

	public static readonly ConfigText BackgroundColor = new(
		Colors,
		"Background Color",
		"Background color of both bars.",
		ChineseColors,
		"背景颜色",
		"血条和感染条的背景颜色。" + ChineseColorOptions);

	public static readonly ConfigText DebugLogging = new(
		Debug,
		"Debug Logging",
		"Write throttled diagnostic logs for bar creation, filtering, visibility, camera state and health display decisions.",
		ChineseDebug,
		"调试日志",
		"写入节流诊断日志，用于排查状态条的创建、过滤、可见性、相机状态和血量显示决策。");

	public static readonly ConfigText[] All =
	{
		Enabled,
		HideInOrbit,
		MaxDisplayDistance,
		ShowInfectionBars,
		BarStyle,
		HeadOffset,
		UiScale,
		ShowText,
		HealthBarColor,
		InfectionBarColor,
		BackgroundColor,
		DebugLogging
	};
}
