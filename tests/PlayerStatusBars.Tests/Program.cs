using System.Text.RegularExpressions;

string repoRoot = FindRepoRoot(AppContext.BaseDirectory);
int failures = 0;

Run("PlayerStatusBarView owns billboard rotation without adding StatusBarBillboard", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	AssertDoesNotContain(view, "AddComponent<StatusBarBillboard>");
	AssertContains(view, "SetBillboardRotation");
});

Run("StatusBarBillboard no longer dispatches a per-bar LateUpdate", () =>
{
	string billboard = ReadSource("StatusBarBillboard.cs");
	AssertDoesNotContain(billboard, "private void LateUpdate()");
	AssertContains(billboard, "internal static void ApplyBillboardRotation");
	AssertContains(billboard, "TryResolveBillboardRotation");
});

Run("PlayerStatusBarManager owns per-frame view ticking", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	string view = ReadSource("PlayerStatusBarView.cs");
	AssertDoesNotContain(view, "private void LateUpdate()");
	AssertContains(view, "void Tick(in PlayerStatusFrameContext context)");
	AssertContains(manager, "view.Tick(frameContext)");
	AssertDoesNotContain(view, "GameNetworkManager.Instance");
	AssertDoesNotContain(view, "StatusBarBillboard.ResolveViewCamera()");
	AssertDoesNotContain(view, "ShouldShowGroup()");
	AssertDoesNotContain(view, "new CadaverGrowthInfectionProvider");
});

Run("frame context caches shared hot-path values", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	string context = ReadSource("PlayerStatusFrameContext.cs");
	AssertContains(context, "readonly struct PlayerStatusFrameContext");
	AssertContains(context, "public readonly Quaternion BillboardRotation");
	AssertContains(context, "public readonly float MaxDistanceSqr");
	AssertContains(manager, "PlayerStatusFrameContext frameContext");
	AssertContains(manager, "StatusBarBillboard.TryResolveBillboardRotation");
});

Run("settings precomputes hot-path config values", () =>
{
	string config = ReadSource("StatusBarConfig.cs");
	string context = ReadSource("PlayerStatusFrameContext.cs");
	AssertContains(config, "public float MaxDistanceSqr => maxDistanceSqr;");
	AssertContains(config, "public float AnchorYOffset => anchorYOffset;");
	AssertContains(config, "private Color cachedHealthFillColor;");
	AssertContains(config, "private void UpdateCachedValues()");
	AssertContains(context, "MaxDistanceSqr = settings.MaxDistanceSqr;");
	AssertContains(context, "AnchorYOffset = settings.AnchorYOffset;");
	AssertDoesNotContain(context, "maxDistance * maxDistance");
});

Run("plugin uses playerstatusbars config name and migrates legacy config", () =>
{
	string info = ReadSource("MyPluginInfo.cs");
	string plugin = ReadSource("Plugin.cs");
	AssertContains(info, "PluginGuid = \"playerstatusbars\"");
	AssertContains(info, "PluginName = \"PlayerStatusBars\"");
	AssertDoesNotContain(info, "Codex.OtherPlayerStatusBars");
	AssertContains(plugin, "LegacyConfigFileName = \"Codex.OtherPlayerStatusBars.cfg\"");
	AssertContains(plugin, "MigrateLegacyConfigFile()");
	AssertContains(plugin, "Config.ConfigFilePath");
	AssertContains(plugin, "File.Copy");
	AssertContains(plugin, "Config.Reload()");
});

Run("release metadata uses version 0.2.0", () =>
{
	string info = ReadSource("MyPluginInfo.cs");
	AssertContains(info, "PluginVersion = \"0.2.0\"");
});

Run("distance visibility hot path avoids sqrt", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string method = ExtractMethod(view, "ShouldShowForDistance");
	AssertDoesNotContain(method, "Mathf.Sqrt");
	AssertContains(view, "FormatDebugDistance");
});

Run("production code avoids runtime reflection and type discovery", () =>
{
	foreach (string file in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.TopDirectoryOnly))
	{
		string source = File.ReadAllText(file);
		AssertDoesNotContain(source, "GetType");
		AssertDoesNotContain(source, "GetField");
		AssertDoesNotContain(source, "FieldInfo");
		AssertDoesNotContain(source, "Activator.");
		AssertDoesNotContain(source, "GetAssemblies");
		AssertDoesNotContain(source, "FindObjectOfType");
		AssertDoesNotContain(source, "System.Reflection");
		AssertDoesNotContain(source, ".GetValue(");
	}
});

Run("Cadaver infection provider uses direct cached game types", () =>
{
	string provider = ReadSource("CadaverGrowthInfectionProvider.cs");
	AssertContains(provider, "RoundManager.Instance.SpawnedEnemies");
	AssertContains(provider, "is CadaverGrowthAI");
	AssertContains(provider, "SampleInterval = 0.5f");
	AssertContains(provider, "SharedInfectionCache");
	AssertContains(provider, "RefreshCache");
	AssertDoesNotContain(provider, "Type");
	AssertDoesNotContain(provider, "Reflection");
	AssertDoesNotContain(provider, "FindObject");
});

Run("PlayerStatusBarView avoids per-view CanvasScaler", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	AssertDoesNotContain(view, "AddComponent<CanvasScaler>");
	AssertDoesNotContain(view, "using UnityEngine.UI;");
});

Run("status bar strips apply dirty UI without per-strip LateUpdate", () =>
{
	string strip = ReadSource("PlayerStatusBarStrip.cs");
	string view = ReadSource("PlayerStatusBarView.cs");
	AssertDoesNotContain(strip, "private void LateUpdate()");
	AssertContains(strip, "public void ApplyIfDirty");
	AssertContains(view, "healthStrip.ApplyIfDirty(settings)");
	AssertContains(view, "infectionStrip.ApplyIfDirty(settings)");
});

Run("status bar strip skips text formatting when text is disabled", () =>
{
	string strip = ReadSource("PlayerStatusBarStrip.cs");
	string method = ExtractMethod(strip, "ApplyIfDirty");
	AssertContains(method, "string formattedText = string.Empty;");
	AssertContains(method, "if (showText)");
	AssertBefore(method, "bool showText", "string formattedText = string.Empty;");
	AssertBefore(method, "if (showText)", "formattedText = labelOnly");
});

Run("billboard rotation math is centralized", () =>
{
	foreach (string file in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.TopDirectoryOnly))
	{
		string source = File.ReadAllText(file);
		if (Path.GetFileName(file) == "StatusBarBillboard.cs")
		{
			AssertContains(source, "Quaternion.LookRotation");
		}
		else
		{
			AssertDoesNotContain(source, "Quaternion.LookRotation");
		}
	}
});

Run("manager sleeps when no bars are active", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	AssertContains(manager, "ActiveRefreshInterval = 0.5f");
	AssertContains(manager, "IdleRefreshInterval = 2.5f");
	AssertContains(manager, "activeBarCount == 0 && Time.unscaledTime < nextRefreshTime");
	AssertContains(manager, "ScheduleNextRefresh(createdCount > 0 || existingCount > 0 || allPlayers.Length > ScanSlotsPerRefresh)");
});

Run("manager pools status bar views instead of destroying on normal removal", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	string removeBar = ExtractMethod(manager, "RemoveBar");
	string clearBars = ExtractMethod(manager, "ClearBars");
	AssertContains(manager, "pooledBars");
	AssertContains(manager, "RentBarView");
	AssertContains(manager, "ReleaseBarView");
	AssertDoesNotContain(removeBar, "Destroy(");
	AssertDoesNotContain(clearBars, "Destroy(");
});

Run("status bar view supports reuse reset", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	AssertContains(view, "internal void Bind(PlayerControllerB player)");
	AssertContains(view, "internal void Release()");
	AssertContains(view, "ResetRuntimeState");
	AssertContains(view, "gameObject.SetActive(false)");
});

Run("status bar graphics avoid raycast and layout-width dirties", () =>
{
	string strip = ReadSource("PlayerStatusBarStrip.cs");
	string initialize = ExtractMethod(strip, "Initialize");
	string apply = ExtractMethod(strip, "ApplyIfDirty");
	AssertContains(initialize, "backgroundImage.raycastTarget = false");
	AssertContains(initialize, "fillImage.raycastTarget = false");
	AssertContains(initialize, "text.raycastTarget = false");
	AssertContains(initialize, "fillImage.type = Image.Type.Simple");
	AssertContains(strip, "private RectTransform fillRect");
	AssertContains(apply, "fillRect.localScale = new Vector3(normalizedValue, 1f, 1f)");
	AssertDoesNotContain(apply, "fillImage.fillAmount = normalizedValue");
	AssertDoesNotContain(apply, "SetSizeWithCurrentAnchors");
});

Run("health display follows raw health without low-health clamping", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	AssertContains(tick, "int displayHealthTarget = ResolveDisplayHealth(rawHealth, rawCriticalState, lowHealthSignalChanged, settings.CriticalHealthMode);");
	AssertContains(tick, "int displayHealth = ResolveSteppedHealthDisplay(displayHealthTarget);");
	AssertDoesNotContain(tick, "displayHealth = 20");
	AssertDoesNotContain(view, "LowHealthRecoveryDisplayDelay");
	AssertDoesNotContain(view, "suppressInitialStaleHealthState");
});

Run("health display predicts vanilla low-health regeneration", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	string predict = ExtractMethod(view, "PredictLowHealthFromElapsed");
	AssertContains(view, "LowHealthRegenerationTarget = 20");
	AssertContains(view, "LowHealthRegenerationInterval = 1f");
	AssertContains(view, "predictedLowHealth");
	AssertContains(view, "lowHealthPredictionStartTime");
	AssertContains(resolve, "ResolveLowHealthPredictionDisplay(rawHealth, rawHealthChanged || lowHealthSignalChanged, rawHealth)");
	AssertContains(resolve, "if (rawHealth >= LowHealthRegenerationTarget)");
	AssertContains(predict, "Mathf.FloorToInt");
	AssertContains(predict, "predictionStartHealth + regenerationSteps");
	AssertContains(resolve, "ResetLowHealthPrediction()");
});

Run("raw health changes override low-health prediction for healing compatibility", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	AssertContains(view, "hasObservedRawHealth");
	AssertContains(resolve, "bool rawHealthChanged = !hasObservedRawHealth || rawHealth != lastObservedRawHealth;");
	AssertContains(resolve, "if (rawHealth >= LowHealthRegenerationTarget)");
	AssertContains(resolve, "ResetLowHealthPrediction();");
	AssertContains(resolve, "return rawHealth;");
});

Run("critical status does not cap low-health recovery prediction", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string display = ExtractMethod(view, "ResolvePredictedLowHealthDisplay");
	AssertContains(display, "return PredictLowHealthFromElapsed();");
	AssertDoesNotContain(display, "rawCriticalState");
	AssertDoesNotContain(view, "BleedingPredictionDisplayCap");
});

Run("critical clear shows 20 only for natural recovery and otherwise trusts raw health", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	AssertContains(resolve, "int predictedHealth = PredictLowHealthFromElapsed();");
	AssertContains(resolve, "bool naturalCriticalClear = predictedHealth >= LowHealthRegenerationTarget && !rawHealthChanged;");
	AssertContains(resolve, "BeginNaturalRecoveredLowHealthHold(rawHealth);");
	AssertContains(resolve, "return rawHealth;");
	AssertDoesNotContain(view, "holdRecoveredLowHealthDisplay");
	AssertDoesNotContain(view, "rawHealthAtCriticalClear");
	AssertDoesNotContain(view, "targetPlayer.hasBeenCriticallyInjured");
});

Run("non-critical low health predicts remote vanilla recovery", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	string lowHealthDisplay = ExtractMethod(view, "ResolveLowHealthPredictionDisplay");
	AssertContains(resolve, "if (rawHealth >= LowHealthRegenerationTarget)");
	AssertContains(resolve, "ResolveLowHealthPredictionDisplay(rawHealth, rawHealthChanged || lowHealthSignalChanged, rawHealth)");
	AssertContains(lowHealthDisplay, "bool restartLowHealthPrediction");
	AssertContains(lowHealthDisplay, "BeginNaturalRecoveredLowHealthHold(rawHealth);");
	AssertContains(lowHealthDisplay, "return ResolvePredictedLowHealthDisplay();");
});

Run("stale low health prediction does not restart just because it rises above raw health", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string lowHealthDisplay = ExtractMethod(view, "ResolveLowHealthPredictionDisplay");
	AssertContains(lowHealthDisplay, "bool restartLowHealthPrediction");
	AssertContains(lowHealthDisplay, "if (!isLowHealthPredictionActive || rawHealth != predictionObservedRawHealth || restartLowHealthPrediction)");
	AssertDoesNotContain(lowHealthDisplay, "predictedLowHealth > rawHealth");
});

Run("natural recovery hold persists only while raw health remains stale", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	string begin = ExtractMethod(view, "BeginNaturalRecoveredLowHealthHold");
	AssertContains(view, "holdNaturalRecoveredLowHealthDisplay");
	AssertContains(view, "naturalRecoveredRawHealth");
	AssertContains(resolve, "if (holdNaturalRecoveredLowHealthDisplay)");
	AssertContains(resolve, "if (rawHealth != naturalRecoveredRawHealth || lowHealthSignalChanged)");
	AssertContains(resolve, "return LowHealthRegenerationTarget;");
	AssertContains(begin, "holdNaturalRecoveredLowHealthDisplay = true;");
	AssertContains(begin, "naturalRecoveredRawHealth = rawHealth;");
});

Run("high-health critical status trusts raw health for active-bleed mod compatibility", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	string helper = ExtractMethod(view, "ShouldInferVanillaCriticalHealth");
	AssertContains(tick, "bool rawCriticalState = targetPlayer.criticallyInjured || targetPlayer.bleedingHeavily;");
	AssertContains(tick, "bool lowHealthSignalChanged = damageSignalChanged || criticalSignalChanged;");
	AssertContains(tick, "int displayHealthTarget = ResolveDisplayHealth(rawHealth, rawCriticalState, lowHealthSignalChanged, settings.CriticalHealthMode);");
	AssertContains(resolve, "if (rawHealth >= LowHealthRegenerationTarget)");
	AssertContains(resolve, "return rawHealth;");
	AssertContains(resolve, "ShouldInferVanillaCriticalHealth(rawHealth, rawCriticalState, criticalHealthMode)");
	AssertContains(helper, "rawHealth == LowHealthRegenerationTarget");
	AssertDoesNotContain(resolve, "rawCriticalState && rawHealth >= LowHealthRegenerationTarget");
});

Run("20-health critical status can infer vanilla downed health", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string config = ReadSource("StatusBarConfig.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	AssertContains(config, "public enum CriticalHealthSyncMode");
	AssertContains(config, "VanillaPrediction");
	AssertContains(config, "TrustRawHealthAt20");
	AssertContains(config, "public CriticalHealthSyncMode CriticalHealthMode => criticalHealthMode.Value;");
	AssertContains(view, "InferredCriticalHealth = 5");
	AssertContains(resolve, "ShouldInferVanillaCriticalHealth(rawHealth, rawCriticalState, criticalHealthMode)");
	AssertContains(resolve, "BeginLowHealthPrediction(InferredCriticalHealth, rawHealth)");
	AssertContains(resolve, "ResolveLowHealthPredictionDisplay(rawHealth, rawHealthChanged || lowHealthSignalChanged, InferredCriticalHealth)");
});

Run("20-health critical inference can be disabled for active-bleed compatibility", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string config = ReadSource("StatusBarConfig.cs");
	string tick = ExtractMethod(view, "Tick");
	string helper = ExtractMethod(view, "ShouldInferVanillaCriticalHealth");
	AssertContains(config, "\"Critical Health Sync Mode\"");
	AssertContains(config, "criticalHealthMode");
	AssertContains(tick, "int displayHealthTarget = ResolveDisplayHealth(rawHealth, rawCriticalState, lowHealthSignalChanged, settings.CriticalHealthMode);");
	AssertContains(helper, "criticalHealthMode == StatusBarConfig.CriticalHealthSyncMode.VanillaPrediction");
	AssertContains(helper, "rawHealth == LowHealthRegenerationTarget");
	AssertContains(helper, "rawCriticalState");
});

Run("new damage timestamp breaks stale natural recovery hold", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	AssertContains(view, "lastObservedDamageTimestamp");
	AssertContains(view, "hasObservedDamageTimestamp");
	AssertContains(tick, "bool damageSignalChanged = ResolveDamageSignalChanged(targetPlayer.timeSinceTakingDamage);");
	AssertContains(tick, "bool lowHealthSignalChanged = damageSignalChanged || criticalSignalChanged;");
	AssertContains(resolve, "if (rawHealth != naturalRecoveredRawHealth || lowHealthSignalChanged)");
	AssertContains(resolve, "ResetNaturalRecoveredLowHealthHold();");
	AssertContains(resolve, "ResolveLowHealthPredictionDisplay(rawHealth, rawHealthChanged || lowHealthSignalChanged, rawHealth)");
});

Run("health display steps one percent toward target", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	string stepped = ExtractMethod(view, "ResolveSteppedHealthDisplay");
	AssertContains(view, "HealthDisplayStepInterval");
	AssertContains(view, "nextHealthDisplayStepTime");
	AssertContains(view, "displayedHealth");
	AssertContains(tick, "int displayHealth = ResolveSteppedHealthDisplay(displayHealthTarget);");
	AssertContains(stepped, "displayedHealth += targetHealth > displayedHealth ? 1 : -1;");
	AssertContains(stepped, "nextHealthDisplayStepTime = Time.unscaledTime + HealthDisplayStepInterval;");
});

Run("active bleed compatibility removes trusted critical health cache", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayHealth");
	AssertDoesNotContain(view, "hasTrustedCriticalRawHealth");
	AssertDoesNotContain(view, "trustedCriticalRawHealth");
	AssertDoesNotContain(view, "BeginTrustedCriticalRawHealth");
	AssertDoesNotContain(resolve, "hasTrustedCriticalRawHealth");
});

Run("critical low health still displays numeric health", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	AssertContains(tick, "bool showCriticalHealthBar = displayHealthTarget < LowHealthRegenerationTarget || displayHealth < LowHealthRegenerationTarget;");
	AssertContains(tick, "healthStrip.SetDisplay(\"HP\", displayHealth, 100f, showPercent: false)");
	AssertDoesNotContain(tick, "healthStrip.SetLabelOnly");
});

Run("infection display shows infected state even before meter rises", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	AssertContains(tick, ": isWithinDisplayDistance && hasInfectionValue;");
	AssertDoesNotContain(tick, "hasInfectionValue && infectionMeter > 0f");
});

Run("cadaver provider clears stale exploded infection meters", () =>
{
	string provider = ReadSource("CadaverGrowthInfectionProvider.cs");
	string refresh = ExtractMethod(provider, "RefreshCache");
	AssertContains(refresh, "if (!infection.infected)");
	AssertContains(refresh, "sharedInfectionCache.Set(i, false, 0f);");
	AssertDoesNotContain(refresh, "infection.infectionMeter > 0f || infection.infected");
	AssertDoesNotContain(refresh, "infection.infectionMeter > 0f");
});

Run("cadaver provider invalidates stale round instances", () =>
{
	string provider = ReadSource("CadaverGrowthInfectionProvider.cs");
	string resolve = ExtractMethod(provider, "ResolveCadaverGrowthInstance");
	string valid = ExtractMethod(provider, "IsCadaverGrowthInstanceValid");
	string clear = ExtractMethod(provider, "ClearCachedCadaverGrowthInstance");
	AssertContains(resolve, "IsCadaverGrowthInstanceValid(cachedCadaverGrowthInstance)");
	AssertContains(resolve, "ClearCachedCadaverGrowthInstance();");
	AssertContains(valid, "RoundManager.Instance.SpawnedEnemies");
	AssertContains(valid, "cadaverGrowth.playerInfections.Length == StartOfRound.Instance.allPlayerScripts.Length");
	AssertContains(clear, "cachedCadaverGrowthInstance = null;");
	AssertContains(clear, "sharedInfectionCache.Clear();");
});

Run("infection display predicts smooth 1 to 99 percent", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	string resolve = ExtractMethod(view, "ResolveDisplayInfectionPercent");
	string predict = ExtractMethod(view, "PredictInfectionMeter");
	string percent = ExtractMethod(view, "MeterToInfectionPercent");
	AssertContains(tick, "int infectionPercent = ResolveDisplayInfectionPercent(infectionMeter, hasInfectionValue);");
	AssertContains(view, "InfectionDisplayMinimumPercent = 1");
	AssertContains(view, "InfectionDisplayMaximumPercent = 99");
	AssertContains(view, "lastObservedInfectionMeter");
	AssertContains(view, "infectionPredictionRate");
	AssertContains(predict, "infectionPredictionStartMeter + infectionPredictionRate * elapsed");
	AssertContains(predict, "Mathf.Clamp01");
	AssertContains(percent, "Mathf.FloorToInt(displayMeter * 100f)");
	AssertContains(percent, "InfectionDisplayMinimumPercent");
	AssertContains(percent, "InfectionDisplayMaximumPercent");
});

Run("infection display rises one percent step at a time", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayInfectionPercent");
	string stepped = ExtractMethod(view, "ResolveSteppedInfectionPercent");
	AssertContains(view, "displayedInfectionPercent");
	AssertContains(view, "InfectionDisplayStepInterval = 0.1f");
	AssertContains(view, "nextInfectionDisplayStepTime");
	AssertContains(resolve, "int targetPercent = MeterToInfectionPercent(displayMeter);");
	AssertContains(resolve, "return ResolveSteppedInfectionPercent(targetPercent);");
	AssertContains(stepped, "if (!hasDisplayedInfectionPercent)");
	AssertContains(stepped, "if (Time.unscaledTime < nextInfectionDisplayStepTime)");
	AssertContains(stepped, "displayedInfectionPercent + 1");
	AssertContains(stepped, "Mathf.Min(targetPercent, displayedInfectionPercent + 1)");
	AssertContains(stepped, "nextInfectionDisplayStepTime = Time.unscaledTime + InfectionDisplayStepInterval;");
});

Run("infection display drops immediately for healing and cure", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string stepped = ExtractMethod(view, "ResolveSteppedInfectionPercent");
	AssertContains(stepped, "if (targetPercent < displayedInfectionPercent)");
	AssertContains(stepped, "displayedInfectionPercent = targetPercent;");
	AssertContains(stepped, "return displayedInfectionPercent;");
});

Run("infection prediction rate uses raw sample interval not frame interval", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayInfectionPercent");
	string update = ExtractMethod(view, "UpdateInfectionPredictionRate");
	AssertContains(view, "InfectionPredictionWindow = 0.5f");
	AssertContains(resolve, "if (rawMeter > lastObservedInfectionMeter)");
	AssertContains(resolve, "UpdateInfectionPredictionRate(rawMeter, now);");
	AssertContains(update, "float elapsed = Mathf.Max(InfectionPredictionWindow, now - lastObservedInfectionSampleTime);");
	AssertContains(update, "lastObservedInfectionSampleTime = now;");
	AssertDoesNotContain(resolve, "lastObservedInfectionTime = now;");
	AssertDoesNotContain(view, "lastObservedInfectionTime");
});

Run("infection prediction only extrapolates one sample window", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string predict = ExtractMethod(view, "PredictInfectionMeter");
	AssertContains(predict, "Mathf.Min(Time.unscaledTime - infectionPredictionStartTime, InfectionPredictionWindow)");
	AssertContains(predict, "infectionPredictionStartMeter + infectionPredictionRate * elapsed");
});

Run("infection raw decreases reset prediction for healing and cure", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string resolve = ExtractMethod(view, "ResolveDisplayInfectionPercent");
	AssertContains(resolve, "if (!hasInfectionValue)");
	AssertContains(resolve, "ResetInfectionPrediction();");
	AssertContains(resolve, "rawMeter < lastObservedInfectionMeter");
	AssertContains(resolve, "BeginInfectionPrediction(rawMeter, 0f)");
});

Run("death and revive transitions reset stale infection display state", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string reset = ExtractMethod(view, "ResetRuntimeState");
	string hide = ExtractMethod(view, "HideAll");
	AssertContains(view, "HandleDeathStateTransition");
	AssertContains(view, "ResetInfectionPrediction();");
	AssertContains(view, "ResetDisplayedInfectionPercent();");
	AssertContains(reset, "ResetInfectionPrediction();");
	AssertContains(hide, "ResetInfectionPrediction();");
	AssertContains(view, "lastDisplayedInfectionPercent = int.MinValue;");
});

Run("status bar text uses cached labels", () =>
{
	string cache = ReadSource("StatusBarTextCache.cs");
	string strip = ReadSource("PlayerStatusBarStrip.cs");
	AssertContains(cache, "internal static class StatusBarTextCache");
	AssertContains(cache, "HealthLabels");
	AssertContains(cache, "InfectionLabels");
	AssertContains(strip, "StatusBarTextCache.GetHealthLabel");
	AssertContains(strip, "StatusBarTextCache.GetInfectionLabel");
});

Run("manager uses fixed slot arrays instead of hash collections", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	AssertDoesNotContain(manager, "Dictionary<");
	AssertDoesNotContain(manager, "HashSet<");
	AssertDoesNotContain(manager, "seenPlayerIds");
	AssertContains(manager, "PlayerStatusBarView?[] trackedBarsBySlot");
	AssertContains(manager, "int activeBarCount");
});

Run("manager grows slot cache without exact resize churn", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	string ensure = ExtractMethod(manager, "EnsureSlotCapacity");
	AssertContains(manager, "MinimumPlayerSlotCapacity");
	AssertContains(manager, "GrowSlotCapacity");
	AssertContains(manager, "activePlayerSlotCount");
	AssertContains(ensure, "Array.Copy");
	AssertContains(ensure, "refreshScanCursor >= slotCount");
	AssertDoesNotContain(ensure, "trackedBarsBySlot = new PlayerStatusBarView?[slotCount]");
	AssertContains(manager, "for (int i = 0; i < activePlayerSlotCount; i++)");
});

Run("player discovery uses connected slots instead of only isPlayerControlled", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	string shouldTrack = ExtractMethod(manager, "ShouldTrackPlayer");
	string connected = ExtractMethod(manager, "IsConnectedPlayerSlot");
	AssertContains(manager, "ShouldTrackPlayer(startOfRound, player, localPlayer, slot, allPlayers, out string skipReason)");
	AssertContains(shouldTrack, "IsConnectedPlayerSlot(startOfRound, player, playerKey)");
	AssertContains(connected, "startOfRound.ClientPlayerList.TryGetValue(player.actualClientId, out int mappedSlot)");
	AssertDoesNotContain(shouldTrack, "!player.isPlayerControlled");
});

Run("view visibility does not hide connected players solely on isPlayerControlled", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string isStillValid = ExtractMethod(view, "IsStillValid");
	string ready = ExtractMethod(view, "IsTargetReadyForDisplay");
	AssertDoesNotContain(isStillValid, "isPlayerControlled");
	AssertDoesNotContain(ready, "isPlayerControlled");
	AssertContains(isStillValid, "!targetPlayer.disconnectedMidGame");
	AssertContains(ready, "!targetPlayer.disconnectedMidGame");
});

Run("dead connected players stay tracked and hidden so revive state can reset", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	string view = ReadSource("PlayerStatusBarView.cs");
	string shouldTrack = ExtractMethod(manager, "ShouldTrackPlayer");
	string isStillValid = ExtractMethod(view, "IsStillValid");
	string ready = ExtractMethod(view, "IsTargetReadyForDisplay");
	AssertDoesNotContain(shouldTrack, "player.isPlayerDead");
	AssertDoesNotContain(shouldTrack, "player.health <= 0");
	AssertDoesNotContain(isStillValid, "targetPlayer.isPlayerDead");
	AssertContains(ready, "!targetPlayer.isPlayerDead");
	AssertContains(ready, "targetPlayer.health > 0");
});

Run("death and revive transitions reset stale health display state", () =>
{
	string view = ReadSource("PlayerStatusBarView.cs");
	string tick = ExtractMethod(view, "Tick");
	string transition = ExtractMethod(view, "HandleDeathStateTransition");
	AssertContains(view, "wasTargetDead");
	AssertContains(tick, "bool targetIsDead = targetPlayer.isPlayerDead || targetPlayer.health <= 0;");
	AssertContains(tick, "HandleDeathStateTransition(targetIsDead);");
	AssertContains(transition, "if (targetIsDead)");
	AssertContains(transition, "if (wasTargetDead)");
	AssertContains(transition, "lastDisplayedHealth = int.MinValue;");
	AssertContains(transition, "ResetLowHealthPrediction();");
	AssertContains(transition, "ResetNaturalRecoveredLowHealthHold();");
});

Run("manager slices player refresh work", () =>
{
	string manager = ReadSource("PlayerStatusBarManager.cs");
	AssertContains(manager, "ScanSlotsPerRefresh");
	AssertContains(manager, "RefreshPlayerSlice(startOfRound, localPlayer, allPlayers)");
	AssertContains(manager, "refreshScanCursor");
	AssertDoesNotContain(manager, "for (int i = 0; i < allPlayers.Length; i++)");
});

Run("cadaver lookup backs off while absent", () =>
{
	string provider = ReadSource("CadaverGrowthInfectionProvider.cs");
	AssertContains(provider, "AbsentInstanceScanIntervalMin");
	AssertContains(provider, "AbsentInstanceScanIntervalMax");
	AssertContains(provider, "currentAbsentInstanceScanInterval");
	AssertContains(provider, "IncreaseAbsentInstanceScanBackoff");
	AssertContains(provider, "ResetAbsentInstanceScanBackoff");
});

Run("infection cache grows by capacity instead of exact resize churn", () =>
{
	string provider = ReadSource("CadaverGrowthInfectionProvider.cs");
	AssertContains(provider, "MinimumInfectionCacheCapacity");
	AssertContains(provider, "validLength");
	AssertContains(provider, "GrowCapacity");
	AssertContains(provider, "index >= validLength");
	AssertDoesNotContain(provider, "hasValues.Length == length");
});

if (failures > 0)
{
	Console.Error.WriteLine($"{failures} test(s) failed.");
	return 1;
}

Console.WriteLine("All structural performance tests passed.");
return 0;

void Run(string name, Action test)
{
	try
	{
		test();
		Console.WriteLine($"PASS {name}");
	}
	catch (Exception exception)
	{
		failures++;
		Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
	}
}

string ReadSource(string fileName)
{
	return File.ReadAllText(Path.Combine(repoRoot, fileName));
}

static string FindRepoRoot(string start)
{
	DirectoryInfo? directory = new(start);
	while (directory != null)
	{
		if (File.Exists(Path.Combine(directory.FullName, "PlayerStatusBars.csproj")))
		{
			return directory.FullName;
		}

		directory = directory.Parent;
	}

	throw new DirectoryNotFoundException("Could not find repository root from test output path.");
}

static string ExtractMethod(string source, string methodName)
{
	Match match = Regex.Match(source, $@"(?:private|public|internal) [^{{]+ {Regex.Escape(methodName)}\([^)]*\)\s*\{{", RegexOptions.Multiline);
	if (!match.Success)
	{
		throw new InvalidOperationException($"Could not find method {methodName}.");
	}

	int index = match.Index;
	int braceIndex = source.IndexOf('{', match.Index);
	int depth = 0;
	for (int i = braceIndex; i < source.Length; i++)
	{
		if (source[i] == '{')
		{
			depth++;
		}
		else if (source[i] == '}')
		{
			depth--;
			if (depth == 0)
			{
				return source[index..(i + 1)];
			}
		}
	}

	throw new InvalidOperationException($"Could not extract method {methodName}.");
}

static void AssertContains(string text, string expected)
{
	if (!text.Contains(expected, StringComparison.Ordinal))
	{
		throw new InvalidOperationException($"Expected to contain '{expected}'.");
	}
}

static void AssertDoesNotContain(string text, string unexpected)
{
	if (text.Contains(unexpected, StringComparison.Ordinal))
	{
		throw new InvalidOperationException($"Expected not to contain '{unexpected}'.");
	}
}

static void AssertBefore(string text, string first, string second)
{
	int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
	int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
	if (firstIndex < 0 || secondIndex < 0 || firstIndex >= secondIndex)
	{
		throw new InvalidOperationException($"Expected '{first}' before '{second}'.");
	}
}
