using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnityEngine;

namespace OtherPlayerStatusBars;

[BepInDependency("ainavt.lc.lethalconfig", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(MyPluginInfo.PluginGuid, MyPluginInfo.PluginName, MyPluginInfo.PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
	internal static ManualLogSource Log { get; private set; } = null!;

	internal static StatusBarConfig Settings { get; private set; } = null!;

	internal static bool UseChineseCriticalLabel { get; private set; }

	private void Awake()
	{
		Log = Logger;
		Settings = StatusBarConfig.Create(Config);
		UseChineseCriticalLabel = DetectChineseLocalizationMod();
		TryRegisterLethalConfig();
		gameObject.AddComponent<PlayerStatusBarManager>();
		Config.Save();
		Log.LogInfo("OtherPlayerStatusBars loaded.");
	}

	private static bool DetectChineseLocalizationMod()
	{
		foreach (PluginInfo pluginInfo in Chainloader.PluginInfos.Values)
		{
			string guid = pluginInfo.Metadata.GUID ?? string.Empty;
			string name = pluginInfo.Metadata.Name ?? string.Empty;
			string location = pluginInfo.Location ?? string.Empty;
			if (LooksLikeChineseLocalization(guid) || LooksLikeChineseLocalization(name) || LooksLikeChineseLocalization(location))
			{
				return true;
			}
		}

		return false;
	}

	private static bool LooksLikeChineseLocalization(string value)
	{
		return Contains(value, "chinese")
			|| Contains(value, "simplified_chinese")
			|| Contains(value, "zh_cn")
			|| Contains(value, "zh-cn")
			|| Contains(value, "zh_hans")
			|| Contains(value, "zh-hans")
			|| Contains(value, "\u4e2d\u6587")
			|| Contains(value, "\u6c49\u5316")
			|| Contains(value, "FixGameTranslate");
	}

	private static bool Contains(string value, string term)
	{
		return value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static void TryRegisterLethalConfig()
	{
		if (Type.GetType("LethalConfig.LethalConfigManager, LethalConfig", false) == null)
		{
			return;
		}

		try
		{
			StatusBarConfig.LethalConfigIntegration.Register(Settings);
		}
		catch (Exception exception)
		{
			Log.LogWarning($"Failed to register LethalConfig items: {exception}");
		}
	}
}
