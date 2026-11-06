using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;

namespace OtherPlayerStatusBars;

/// <summary>
/// Optional LethalConfig support. LethalConfig types appear only inside method bodies, and callers
/// check that it is installed first, so the mod still loads without it.
/// </summary>
internal static class LethalConfigIntegration
{
	public const string PluginGuid = "ainavt.lc.lethalconfig";

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void Register(StatusBarConfig settings, bool useChinese)
	{
		LethalConfigManager.SkipAutoGen();
		LethalConfigManager.SetModDescription(useChinese ? StatusBarConfigText.ChineseModDescription : StatusBarConfigText.ModDescription);
		AddBool(settings.EnabledEntry, StatusBarConfigText.Enabled, useChinese);
		AddBool(settings.HideInOrbitEntry, StatusBarConfigText.HideInOrbit, useChinese);
		AddFloat(settings.MaxDistanceEntry, StatusBarConfigText.MaxDisplayDistance, useChinese);
		AddBool(settings.ShowInfectionBarsEntry, StatusBarConfigText.ShowInfectionBars, useChinese);
		AddBarStyle(settings.BarStyleEntry, StatusBarConfigText.BarStyle, useChinese);
		AddFloat(settings.HeadOffsetEntry, StatusBarConfigText.HeadOffset, useChinese);
		AddFloat(settings.UiScaleEntry, StatusBarConfigText.UiScale, useChinese);
		AddBool(settings.ShowTextEntry, StatusBarConfigText.ShowText, useChinese);
		AddColor(settings.HealthColorEntry, StatusBarConfigText.HealthBarColor, useChinese);
		AddColor(settings.InfectionColorEntry, StatusBarConfigText.InfectionBarColor, useChinese);
		AddColor(settings.BackgroundColorEntry, StatusBarConfigText.BackgroundColor, useChinese);
		AddBool(settings.DebugLoggingEntry, StatusBarConfigText.DebugLogging, useChinese);
	}

	private static void AddBool(ConfigEntry<bool> entry, in ConfigText text, bool useChinese)
	{
		LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(entry, new BoolCheckBoxOptions
		{
			Section = text.DisplaySection(useChinese),
			Name = text.DisplayName(useChinese),
			Description = text.DisplayDescription(useChinese),
			RequiresRestart = false
		}));
	}

	private static void AddFloat(ConfigEntry<float> entry, in ConfigText text, bool useChinese)
	{
		LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(entry, new FloatSliderOptions
		{
			Section = text.DisplaySection(useChinese),
			Name = text.DisplayName(useChinese),
			Description = text.DisplayDescription(useChinese),
			RequiresRestart = false
		}));
	}

	private static void AddBarStyle(ConfigEntry<StatusBarConfig.BarStyle> entry, in ConfigText text, bool useChinese)
	{
		LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<StatusBarConfig.BarStyle>(entry, new EnumDropDownOptions
		{
			Section = text.DisplaySection(useChinese),
			Name = text.DisplayName(useChinese),
			Description = text.DisplayDescription(useChinese),
			RequiresRestart = false
		}));
	}

	private static void AddColor(ConfigEntry<StatusBarConfig.ColorPreset> entry, in ConfigText text, bool useChinese)
	{
		LethalConfigManager.AddConfigItem(new EnumDropDownConfigItem<StatusBarConfig.ColorPreset>(entry, new EnumDropDownOptions
		{
			Section = text.DisplaySection(useChinese),
			Name = text.DisplayName(useChinese),
			Description = text.DisplayDescription(useChinese),
			RequiresRestart = false
		}));
	}
}
