using System;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using UnityEngine;

namespace OtherPlayerStatusBars;

// Soft dependencies only order loading: both mods stay optional, and no version is required of either.
[BepInPlugin(MyPluginInfo.PluginGuid, MyPluginInfo.PluginName, MyPluginInfo.PluginVersion)]
[BepInDependency(LethalConfigIntegration.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ChineseProjectDetection.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ChineseProjectDetection.LegacyPluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
	private const string RuntimeObjectName = "PlayerStatusBars.Runtime";

	private const string LegacyConfigFileName = "Codex.OtherPlayerStatusBars.cfg";

	private static GameObject? runtimeObject;

	private static PlayerStatusBarManager? runtimeManager;

	internal static ManualLogSource Log { get; private set; } = null!;

	internal static StatusBarConfig Settings { get; private set; } = null!;

	private void Awake()
	{
		Log = Logger;
		MigrateLegacyConfigFile();
		RemoveObsoleteConfigKeys();
		bool useChinese = DetectChineseProject();
		Settings = StatusBarConfig.Create(Config, useChinese);
		RegisterLethalConfig(useChinese);
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

	private void RemoveObsoleteConfigKeys()
	{
		string configPath = Config.ConfigFilePath;
		if (!File.Exists(configPath))
		{
			return;
		}

		try
		{
			if (LegacyConfigCleaner.TryRemoveObsoleteKeys(File.ReadAllLines(configPath), out string[] cleanedLines))
			{
				File.WriteAllLines(configPath, cleanedLines);
				Config.Reload();
				Log.LogInfo("Removed obsolete settings from the config file.");
			}
		}
		catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
		{
			Log.LogWarning($"Could not remove obsolete settings: {exception.Message}");
		}
	}

	private static void RegisterLethalConfig(bool useChinese)
	{
		if (!Chainloader.PluginInfos.ContainsKey(LethalConfigIntegration.PluginGuid))
		{
			return;
		}

		try
		{
			LethalConfigIntegration.Register(Settings, useChinese);
		}
		catch (Exception exception)
		{
			Log.LogWarning($"LethalConfig registration failed: {exception.Message}");
		}
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

	private static bool DetectChineseProject()
	{
		foreach (PluginInfo pluginInfo in Chainloader.PluginInfos.Values)
		{
			if (ChineseProjectDetection.Matches(pluginInfo.Metadata.GUID, pluginInfo.Location))
			{
				return true;
			}
		}

		return HasChineseProjectFiles();
	}

	/// <summary>Covers a future GUID change, when the soft dependency could no longer order loading.</summary>
	private static bool HasChineseProjectFiles()
	{
		try
		{
			string packageDirectory = Path.Combine(Paths.PluginPath, ChineseProjectDetection.ThunderstoreDirectoryName);
			if (Directory.Exists(packageDirectory) && Directory.GetFiles(packageDirectory, "*.dll", SearchOption.AllDirectories).Length > 0)
			{
				return true;
			}

			return Directory.GetFiles(Paths.PluginPath, ChineseProjectDetection.PluginFileName, SearchOption.AllDirectories).Length > 0;
		}
		catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
		{
			Log.LogWarning($"Could not check for LC Chinese Project: {exception.Message}");
			return false;
		}
	}
}
