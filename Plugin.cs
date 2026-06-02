using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnityEngine;

namespace OtherPlayerStatusBars;

[BepInPlugin(MyPluginInfo.PluginGuid, MyPluginInfo.PluginName, MyPluginInfo.PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
	private const string RuntimeObjectName = "PlayerStatusBars.Runtime";

	private const string LegacyConfigFileName = "Codex.OtherPlayerStatusBars.cfg";

	private static GameObject? runtimeObject;

	private static PlayerStatusBarManager? runtimeManager;

	internal static ManualLogSource Log { get; private set; } = null!;

	internal static StatusBarConfig Settings { get; private set; } = null!;

	internal static bool UseChineseCriticalLabel { get; private set; }

	private void Awake()
	{
		Log = Logger;
		MigrateLegacyConfigFile();
		Settings = StatusBarConfig.Create(Config);
		UseChineseCriticalLabel = DetectChineseLocalizationMod();
		EnsureRuntimeManager();
		Config.Save();
		Log.LogInfo("PlayerStatusBars loaded.");
		LogDebug("Debug logging is enabled.");
	}

	private void OnDestroy()
	{
		LogDebug("Plugin component destroyed; runtime manager remains active.");
	}

	internal static void LogDebug(string message)
	{
		if (Settings?.DebugLogging == true)
		{
			Log.LogInfo($"[Debug] {message}");
		}
	}

	private void MigrateLegacyConfigFile()
	{
		string configPath = Config.ConfigFilePath;
		string? configDirectory = Path.GetDirectoryName(configPath);
		if (string.IsNullOrEmpty(configDirectory))
		{
			return;
		}

		string legacyPath = Path.Combine(configDirectory, LegacyConfigFileName);
		if (!File.Exists(legacyPath) || File.Exists(configPath))
		{
			return;
		}

		File.Copy(legacyPath, configPath);
		Config.Reload();
		Log.LogInfo($"Migrated legacy config from {LegacyConfigFileName} to {Path.GetFileName(configPath)}.");
	}

	private static void EnsureRuntimeManager()
	{
		if (runtimeManager != null)
		{
			GameObject existingObject = runtimeManager.gameObject;
			if (existingObject != null && !existingObject.activeSelf)
			{
				existingObject.SetActive(true);
			}

			LogDebug("Runtime manager already active.");
			return;
		}

		if (runtimeObject == null)
		{
			runtimeObject = new GameObject(RuntimeObjectName);
			runtimeObject.hideFlags = HideFlags.HideAndDontSave;
			DontDestroyOnLoad(runtimeObject);
		}
		else if (!runtimeObject.activeSelf)
		{
			runtimeObject.SetActive(true);
		}

		runtimeManager = runtimeObject.GetComponent<PlayerStatusBarManager>();
		if (runtimeManager == null)
		{
			runtimeManager = runtimeObject.AddComponent<PlayerStatusBarManager>();
		}

		LogDebug("Runtime manager created.");
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

}
