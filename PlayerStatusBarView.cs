using GameNetcodeStuff;
using UnityEngine;

namespace OtherPlayerStatusBars;

internal sealed class PlayerStatusBarView : MonoBehaviour
{
	private static readonly Color CriticalHealthBarColor = new(0.88f, 0.15f, 0.12f, 0.95f);

	private const int LowHealthRegenerationTarget = 20;

	private const int InferredCriticalHealth = 5;

	private const float LowHealthRegenerationInterval = 1f;

	private const float HealthDisplayStepInterval = 0.05f;

	private const int InfectionDisplayMinimumPercent = 1;

	private const int InfectionDisplayMaximumPercent = 99;

	private const float InfectionPredictionWindow = 0.5f;

	private const float InfectionDisplayStepInterval = 0.1f;

	private PlayerControllerB targetPlayer = null!;

	private Transform? anchor;

	private PlayerStatusBarStrip healthStrip = null!;

	private PlayerStatusBarStrip infectionStrip = null!;

	private RectTransform healthStripRect = null!;

	private RectTransform infectionStripRect = null!;

	private Canvas worldCanvas = null!;

	private RectTransform canvasRect = null!;

	private int lastDisplayedHealth = int.MinValue;

	private int lastDisplayedInfectionPercent = int.MinValue;

	private bool wasHealthVisible;

	private bool wasInfectionVisible;

	private string lastDisplayedHealthLabel = string.Empty;

	private bool lastCanvasEnabled = true;

	private Camera? lastAppliedCanvasCamera;

	private string lastDebugVisibilityState = string.Empty;

	private float nextDebugVisibilityLogTime;

	private float lastAppliedUiScale = -1f;

	private float lastAppliedHealthBarYOffset = float.NaN;

	private float lastAppliedInfectionBarYOffset = float.NaN;

	private bool isLowHealthPredictionActive;

	private bool wasTargetDead;

	private bool hasObservedRawHealth;

	private bool hasObservedDamageTimestamp;

	private bool hasObservedRawCriticalState;

	private bool lastObservedRawCriticalState;

	private bool holdNaturalRecoveredLowHealthDisplay;

	private bool isInfectionPredictionActive;

	private bool hasDisplayedHealth;

	private bool hasDisplayedInfectionPercent;

	private int lastObservedRawHealth = int.MinValue;

	private int predictionObservedRawHealth = int.MinValue;

	private int predictionStartHealth = int.MinValue;

	private int predictedLowHealth = int.MinValue;

	private int displayedHealth = int.MinValue;

	private int healthDisplayTarget = int.MinValue;

	private int naturalRecoveredRawHealth = int.MinValue;

	private int displayedInfectionPercent = int.MinValue;

	private float lowHealthPredictionStartTime;

	private float lastObservedDamageTimestamp;

	private float lastObservedInfectionMeter;

	private float lastObservedInfectionSampleTime;

	private float infectionPredictionStartMeter;

	private float infectionPredictionStartTime;

	private float infectionPredictionRate;

	private float nextHealthDisplayStepTime;

	private float nextInfectionDisplayStepTime;

	public int PlayerId => targetPlayer != null ? (int)targetPlayer.playerClientId : -1;

	public bool Initialize(PlayerControllerB player)
	{
		worldCanvas = gameObject.AddComponent<Canvas>();
		worldCanvas.renderMode = RenderMode.WorldSpace;
		worldCanvas.overrideSorting = true;
		worldCanvas.sortingOrder = 50;
		canvasRect = worldCanvas.GetComponent<RectTransform>();
		canvasRect.sizeDelta = new Vector2(220f, 84f);

		if (!TryCreateStrip("HealthStrip", out healthStrip))
		{
			return false;
		}
		healthStripRect = healthStrip.GetComponent<RectTransform>();

		if (!TryCreateStrip("InfectionStrip", out infectionStrip))
		{
			return false;
		}
		infectionStripRect = infectionStrip.GetComponent<RectTransform>();

		Bind(player);
		return true;
	}

	internal void Bind(PlayerControllerB player)
	{
		targetPlayer = player;
		anchor = ResolveAnchor(player);
		ResetRuntimeState();
		name = $"PlayerStatusBar_{player.playerClientId}";
		gameObject.SetActive(true);
	}

	internal void Release()
	{
		if (worldCanvas != null)
		{
			SetCanvasEnabled(false);
		}

		if (healthStrip != null)
		{
			healthStrip.SetVisible(false);
		}

		if (infectionStrip != null)
		{
			infectionStrip.SetVisible(false);
		}

		targetPlayer = null!;
		anchor = null;
		ResetRuntimeState();
		gameObject.SetActive(false);
	}

	public bool IsStillValid(PlayerControllerB localPlayer)
	{
		if (targetPlayer == null || localPlayer == null)
		{
			return false;
		}

		if (targetPlayer == localPlayer)
		{
			return false;
		}

		return !targetPlayer.disconnectedMidGame && targetPlayer.gameObject.activeInHierarchy;
	}

	internal void Tick(in PlayerStatusFrameContext context)
	{
		if (targetPlayer == null)
		{
			return;
		}

		StatusBarConfig settings = context.Settings;
		if (!context.CanShowGroup)
		{
			HideAll(settings, "group-hidden", context.ViewCamera, -1f);
			return;
		}

		bool targetIsDead = targetPlayer.isPlayerDead || targetPlayer.health <= 0;
		HandleDeathStateTransition(targetIsDead);
		bool targetReady = IsTargetReadyForDisplay();
		if (!targetReady)
		{
			HideAll(settings, "target-not-ready", context.ViewCamera, -1f);
			return;
		}

		anchor ??= ResolveAnchor(targetPlayer);
		Vector3 anchorPosition = anchor != null ? anchor.position : targetPlayer.transform.position;
		transform.position = anchorPosition + Vector3.up * context.AnchorYOffset;
		if (context.HasBillboardRotation)
		{
			SetBillboardRotation(context.BillboardRotation);
		}
		ApplyCanvasCamera(context.ViewCamera);
		ApplyCanvasScale(context.UiScale);
		bool isWithinDisplayDistance = ShouldShowForDistance(context, out float displayDistanceSqr);
		ApplyStripLayoutOffsets(settings);

		int rawHealth = Mathf.Clamp(targetPlayer.health, 0, 100);
		bool rawCriticalState = targetPlayer.criticallyInjured || targetPlayer.bleedingHeavily;
		bool damageSignalChanged = ResolveDamageSignalChanged(targetPlayer.timeSinceTakingDamage);
		bool criticalSignalChanged = ResolveCriticalSignalChanged(rawCriticalState);
		bool lowHealthSignalChanged = damageSignalChanged || criticalSignalChanged;
		int displayHealthTarget = ResolveDisplayHealth(rawHealth, rawCriticalState, lowHealthSignalChanged, settings.CriticalHealthMode);
		int displayHealth = ResolveSteppedHealthDisplay(displayHealthTarget);

		bool showHealth = isWithinDisplayDistance;
		LogDebugVisibility(showHealth ? "visible" : "distance-hidden", context.ViewCamera, displayDistanceSqr);
		bool showCriticalHealthBar = displayHealthTarget < LowHealthRegenerationTarget || displayHealth < LowHealthRegenerationTarget;
		healthStrip.SetFillColorOverride(showCriticalHealthBar, CriticalHealthBarColor);
		if (showHealth && (!wasHealthVisible || displayHealth != lastDisplayedHealth || lastDisplayedHealthLabel != "HP"))
		{
			healthStrip.SetDisplay("HP", displayHealth, 100f, showPercent: false);
			lastDisplayedHealth = displayHealth;
			lastDisplayedHealthLabel = "HP";
		}
		healthStrip.SetVisible(showHealth);
		wasHealthVisible = showHealth;

		float infectionMeter = 0f;
		bool hasInfectionValue = false;
		if (isWithinDisplayDistance)
		{
			hasInfectionValue = CadaverGrowthInfectionProvider.TryGetNormalizedInfection(targetPlayer, out infectionMeter);
		}

		bool showInfection = settings.InfectionDisplayMode == StatusBarConfig.InfectionBarDisplayMode.AlwaysVisible
			? isWithinDisplayDistance
			: isWithinDisplayDistance && hasInfectionValue;

		if (showInfection)
		{
			int infectionPercent = ResolveDisplayInfectionPercent(infectionMeter, hasInfectionValue);
			if (!wasInfectionVisible || infectionPercent != lastDisplayedInfectionPercent)
			{
				infectionStrip.SetDisplay("INF", infectionPercent, 100f, showPercent: true);
				lastDisplayedInfectionPercent = infectionPercent;
			}
			infectionStrip.SetVisible(true);
			wasInfectionVisible = true;
		}
		else
		{
			infectionStrip.SetVisible(false);
			wasInfectionVisible = false;
			ResetInfectionPrediction();
		}

		SetCanvasEnabled(showHealth || showInfection);
		ApplyStripsIfDirty(settings);
	}

	private bool IsTargetReadyForDisplay()
	{
		return targetPlayer != null
			&& !targetPlayer.disconnectedMidGame
			&& !targetPlayer.isPlayerDead
			&& targetPlayer.health > 0
			&& targetPlayer.gameObject.activeInHierarchy;
	}

	private void ResetRuntimeState()
	{
		lastDisplayedHealth = int.MinValue;
		lastDisplayedInfectionPercent = int.MinValue;
		wasHealthVisible = false;
		wasInfectionVisible = false;
		lastDisplayedHealthLabel = string.Empty;
		lastDebugVisibilityState = string.Empty;
		nextDebugVisibilityLogTime = 0f;
		lastAppliedCanvasCamera = null;
		lastAppliedUiScale = -1f;
		lastAppliedHealthBarYOffset = float.NaN;
		lastAppliedInfectionBarYOffset = float.NaN;
		wasTargetDead = false;
		hasObservedRawHealth = false;
		ResetLowHealthSignals();
		ResetLowHealthPrediction();
		ResetNaturalRecoveredLowHealthHold();
		ResetDisplayedHealth();
		ResetInfectionPrediction();
		ResetDisplayedInfectionPercent();
	}

	private void HandleDeathStateTransition(bool targetIsDead)
	{
		if (targetIsDead)
		{
			if (!wasTargetDead)
			{
				wasTargetDead = true;
				lastDisplayedHealth = int.MinValue;
				lastDisplayedHealthLabel = string.Empty;
				ResetLowHealthSignals();
				ResetLowHealthPrediction();
				ResetNaturalRecoveredLowHealthHold();
				ResetDisplayedHealth();
				ResetInfectionPrediction();
				ResetDisplayedInfectionPercent();
			}

			return;
		}

		if (wasTargetDead)
		{
			wasTargetDead = false;
			lastDisplayedHealth = int.MinValue;
			lastDisplayedHealthLabel = string.Empty;
			ResetLowHealthSignals();
			ResetLowHealthPrediction();
			ResetNaturalRecoveredLowHealthHold();
			ResetDisplayedHealth();
			ResetInfectionPrediction();
			ResetDisplayedInfectionPercent();
		}
	}

	private bool ResolveDamageSignalChanged(float damageTimestamp)
	{
		if (!hasObservedDamageTimestamp)
		{
			hasObservedDamageTimestamp = true;
			lastObservedDamageTimestamp = damageTimestamp;
			return false;
		}

		if (damageTimestamp == lastObservedDamageTimestamp)
		{
			return false;
		}

		lastObservedDamageTimestamp = damageTimestamp;
		return true;
	}

	private bool ResolveCriticalSignalChanged(bool rawCriticalState)
	{
		if (!hasObservedRawCriticalState)
		{
			hasObservedRawCriticalState = true;
			lastObservedRawCriticalState = rawCriticalState;
			return false;
		}

		if (rawCriticalState == lastObservedRawCriticalState)
		{
			return false;
		}

		lastObservedRawCriticalState = rawCriticalState;
		return true;
	}

	private int ResolveDisplayHealth(int rawHealth, bool rawCriticalState, bool lowHealthSignalChanged, StatusBarConfig.CriticalHealthSyncMode criticalHealthMode)
	{
		bool rawHealthChanged = !hasObservedRawHealth || rawHealth != lastObservedRawHealth;
		if (rawHealthChanged)
		{
			hasObservedRawHealth = true;
			lastObservedRawHealth = rawHealth;
		}

		if (rawHealth <= 0)
		{
			ResetLowHealthPrediction();
			ResetNaturalRecoveredLowHealthHold();
			return rawHealth;
		}

		if (holdNaturalRecoveredLowHealthDisplay)
		{
			if (rawHealth != naturalRecoveredRawHealth || lowHealthSignalChanged)
			{
				ResetNaturalRecoveredLowHealthHold();
			}
			else
			{
				return LowHealthRegenerationTarget;
			}
		}

		if (ShouldInferVanillaCriticalHealth(rawHealth, rawCriticalState, criticalHealthMode))
		{
			if (!isLowHealthPredictionActive || rawHealth != predictionObservedRawHealth)
			{
				BeginLowHealthPrediction(InferredCriticalHealth, rawHealth);
			}

			return ResolveLowHealthPredictionDisplay(rawHealth, rawHealthChanged || lowHealthSignalChanged, InferredCriticalHealth);
		}

		if (rawHealth >= LowHealthRegenerationTarget)
		{
			if (isLowHealthPredictionActive)
			{
				int predictedHealth = PredictLowHealthFromElapsed();
				bool naturalCriticalClear = predictedHealth >= LowHealthRegenerationTarget && !rawHealthChanged;
				ResetLowHealthPrediction();
				if (naturalCriticalClear)
				{
					BeginNaturalRecoveredLowHealthHold(rawHealth);
					return LowHealthRegenerationTarget;
				}
			}

			return rawHealth;
		}

		ResetNaturalRecoveredLowHealthHold();
		return ResolveLowHealthPredictionDisplay(rawHealth, rawHealthChanged || lowHealthSignalChanged, rawHealth);
	}

	private static bool ShouldInferVanillaCriticalHealth(int rawHealth, bool rawCriticalState, StatusBarConfig.CriticalHealthSyncMode criticalHealthMode)
	{
		return criticalHealthMode == StatusBarConfig.CriticalHealthSyncMode.VanillaPrediction
			&& rawCriticalState
			&& rawHealth == LowHealthRegenerationTarget;
	}

	private int ResolveLowHealthPredictionDisplay(int rawHealth, bool restartLowHealthPrediction, int predictionStartHealth)
	{
		if (!isLowHealthPredictionActive || rawHealth != predictionObservedRawHealth || restartLowHealthPrediction)
		{
			BeginLowHealthPrediction(predictionStartHealth, rawHealth);
		}

		int predictedHealth = PredictLowHealthFromElapsed();
		if (predictedHealth >= LowHealthRegenerationTarget && !restartLowHealthPrediction)
		{
			ResetLowHealthPrediction();
			BeginNaturalRecoveredLowHealthHold(rawHealth);
			return LowHealthRegenerationTarget;
		}

		return ResolvePredictedLowHealthDisplay();
	}

	private int ResolveSteppedHealthDisplay(int targetHealth)
	{
		if (!hasDisplayedHealth)
		{
			hasDisplayedHealth = true;
			displayedHealth = targetHealth;
			healthDisplayTarget = targetHealth;
			nextHealthDisplayStepTime = Time.unscaledTime + HealthDisplayStepInterval;
			return displayedHealth;
		}

		if (targetHealth == displayedHealth)
		{
			healthDisplayTarget = targetHealth;
			return displayedHealth;
		}

		if (targetHealth != healthDisplayTarget)
		{
			healthDisplayTarget = targetHealth;
			nextHealthDisplayStepTime = 0f;
		}

		if (Time.unscaledTime < nextHealthDisplayStepTime)
		{
			return displayedHealth;
		}

		displayedHealth += targetHealth > displayedHealth ? 1 : -1;
		nextHealthDisplayStepTime = Time.unscaledTime + HealthDisplayStepInterval;
		return displayedHealth;
	}

	private void BeginLowHealthPrediction(int displayHealth, int observedRawHealth)
	{
		isLowHealthPredictionActive = true;
		predictionObservedRawHealth = observedRawHealth;
		predictionStartHealth = displayHealth;
		predictedLowHealth = displayHealth;
		lowHealthPredictionStartTime = Time.time;
	}

	private int ResolvePredictedLowHealthDisplay()
	{
		return PredictLowHealthFromElapsed();
	}

	private int PredictLowHealthFromElapsed()
	{
		float elapsed = Time.time - lowHealthPredictionStartTime;
		int regenerationSteps = Mathf.Max(0, Mathf.FloorToInt(elapsed / LowHealthRegenerationInterval));
		predictedLowHealth = Mathf.Min(LowHealthRegenerationTarget, predictionStartHealth + regenerationSteps);
		return predictedLowHealth;
	}

	private void BeginNaturalRecoveredLowHealthHold(int rawHealth)
	{
		holdNaturalRecoveredLowHealthDisplay = true;
		naturalRecoveredRawHealth = rawHealth;
	}

	private void ResetLowHealthPrediction()
	{
		isLowHealthPredictionActive = false;
		predictionObservedRawHealth = int.MinValue;
		predictionStartHealth = int.MinValue;
		predictedLowHealth = int.MinValue;
		lowHealthPredictionStartTime = 0f;
	}

	private void ResetLowHealthSignals()
	{
		hasObservedDamageTimestamp = false;
		lastObservedDamageTimestamp = 0f;
		hasObservedRawCriticalState = false;
		lastObservedRawCriticalState = false;
	}

	private void ResetDisplayedHealth()
	{
		hasDisplayedHealth = false;
		displayedHealth = int.MinValue;
		healthDisplayTarget = int.MinValue;
		nextHealthDisplayStepTime = 0f;
	}

	private void ResetNaturalRecoveredLowHealthHold()
	{
		holdNaturalRecoveredLowHealthDisplay = false;
		naturalRecoveredRawHealth = int.MinValue;
	}

	private int ResolveDisplayInfectionPercent(float rawMeter, bool hasInfectionValue)
	{
		if (!hasInfectionValue)
		{
			ResetInfectionPrediction();
			ResetDisplayedInfectionPercent();
			return 0;
		}

		rawMeter = Mathf.Clamp01(rawMeter);
		float now = Time.unscaledTime;
		if (!isInfectionPredictionActive)
		{
			BeginInfectionPrediction(rawMeter, 0f);
			lastObservedInfectionMeter = rawMeter;
			lastObservedInfectionSampleTime = now;
			return ResolveSteppedInfectionPercent(MeterToInfectionPercent(rawMeter));
		}

		if (rawMeter < lastObservedInfectionMeter)
		{
			BeginInfectionPrediction(rawMeter, 0f);
		}
		else if (rawMeter > lastObservedInfectionMeter)
		{
			UpdateInfectionPredictionRate(rawMeter, now);
		}

		lastObservedInfectionMeter = rawMeter;
		float displayMeter = PredictInfectionMeter();
		int targetPercent = MeterToInfectionPercent(displayMeter);
		return ResolveSteppedInfectionPercent(targetPercent);
	}

	private int ResolveSteppedInfectionPercent(int targetPercent)
	{
		if (!hasDisplayedInfectionPercent)
		{
			hasDisplayedInfectionPercent = true;
			displayedInfectionPercent = targetPercent;
			nextInfectionDisplayStepTime = Time.unscaledTime + InfectionDisplayStepInterval;
			return displayedInfectionPercent;
		}

		if (targetPercent < displayedInfectionPercent)
		{
			displayedInfectionPercent = targetPercent;
			nextInfectionDisplayStepTime = Time.unscaledTime + InfectionDisplayStepInterval;
			return displayedInfectionPercent;
		}

		if (Time.unscaledTime < nextInfectionDisplayStepTime)
		{
			return displayedInfectionPercent;
		}

		displayedInfectionPercent = Mathf.Min(targetPercent, displayedInfectionPercent + 1);
		nextInfectionDisplayStepTime = Time.unscaledTime + InfectionDisplayStepInterval;
		return displayedInfectionPercent;
	}

	private void UpdateInfectionPredictionRate(float rawMeter, float now)
	{
		float elapsed = Mathf.Max(InfectionPredictionWindow, now - lastObservedInfectionSampleTime);
		float rate = (rawMeter - lastObservedInfectionMeter) / elapsed;
		BeginInfectionPrediction(rawMeter, rate);
		lastObservedInfectionSampleTime = now;
	}

	private void BeginInfectionPrediction(float rawMeter, float rate)
	{
		isInfectionPredictionActive = true;
		infectionPredictionStartMeter = rawMeter;
		infectionPredictionStartTime = Time.unscaledTime;
		infectionPredictionRate = rate;
	}

	private float PredictInfectionMeter()
	{
		float elapsed = Mathf.Min(Time.unscaledTime - infectionPredictionStartTime, InfectionPredictionWindow);
		return Mathf.Clamp01(infectionPredictionStartMeter + infectionPredictionRate * elapsed);
	}

	private static int MeterToInfectionPercent(float meter)
	{
		float displayMeter = Mathf.Clamp01(meter);
		int percent = Mathf.FloorToInt(displayMeter * 100f);
		return Mathf.Clamp(percent, InfectionDisplayMinimumPercent, InfectionDisplayMaximumPercent);
	}

	private void ResetInfectionPrediction()
	{
		isInfectionPredictionActive = false;
		lastObservedInfectionMeter = 0f;
		lastObservedInfectionSampleTime = 0f;
		infectionPredictionStartMeter = 0f;
		infectionPredictionStartTime = 0f;
		infectionPredictionRate = 0f;
	}

	private void ResetDisplayedInfectionPercent()
	{
		hasDisplayedInfectionPercent = false;
		displayedInfectionPercent = int.MinValue;
		nextInfectionDisplayStepTime = 0f;
	}

	private void ApplyCanvasScale(float uiScale)
	{
		if (Mathf.Approximately(lastAppliedUiScale, uiScale))
		{
			return;
		}

		canvasRect.localScale = Vector3.one * uiScale;
		lastAppliedUiScale = uiScale;
	}

	private void ApplyStripLayoutOffsets(StatusBarConfig settings)
	{
		float healthYOffset = settings.HealthBarYOffset;
		if (!Mathf.Approximately(lastAppliedHealthBarYOffset, healthYOffset))
		{
			healthStripRect.anchoredPosition = new Vector2(0f, healthYOffset);
			lastAppliedHealthBarYOffset = healthYOffset;
		}

		float infectionYOffset = settings.InfectionBarYOffset;
		if (!Mathf.Approximately(lastAppliedInfectionBarYOffset, infectionYOffset))
		{
			infectionStripRect.anchoredPosition = new Vector2(0f, infectionYOffset);
			lastAppliedInfectionBarYOffset = infectionYOffset;
		}
	}

	private void SetBillboardRotation(Quaternion billboardRotation)
	{
		transform.rotation = billboardRotation;
	}

	private void ApplyStripsIfDirty(StatusBarConfig settings)
	{
		healthStrip.ApplyIfDirty(settings);
		infectionStrip.ApplyIfDirty(settings);
	}

	private void SetCanvasEnabled(bool enabled)
	{
		if (lastCanvasEnabled == enabled && worldCanvas.enabled == enabled)
		{
			return;
		}

		worldCanvas.enabled = enabled;
		lastCanvasEnabled = enabled;
	}

	private void ApplyCanvasCamera(Camera? viewCamera)
	{
		if (lastAppliedCanvasCamera == viewCamera && worldCanvas.worldCamera == viewCamera)
		{
			return;
		}

		worldCanvas.worldCamera = viewCamera;
		lastAppliedCanvasCamera = viewCamera;
	}

	private void HideAll(StatusBarConfig settings, string reason, Camera? viewCamera, float distanceSqr)
	{
		LogDebugVisibility(reason, viewCamera, distanceSqr);
		SetCanvasEnabled(false);
		healthStrip.SetVisible(false);
		infectionStrip.SetVisible(false);
		wasHealthVisible = false;
		wasInfectionVisible = false;
		ResetInfectionPrediction();
		ResetDisplayedInfectionPercent();
		ApplyStripsIfDirty(settings);
	}

	private bool TryCreateStrip(string stripName, out PlayerStatusBarStrip strip)
	{
		strip = null!;
		GameObject stripObject = new(stripName, typeof(RectTransform));
		stripObject.transform.SetParent(worldCanvas.transform, worldPositionStays: false);
		stripObject.name = stripName;
		stripObject.transform.localPosition = Vector3.zero;
		stripObject.transform.localRotation = Quaternion.identity;

		strip = stripObject.AddComponent<PlayerStatusBarStrip>();
		strip.Initialize(stripName == "HealthStrip" ? PlayerStatusBarStrip.StripType.Health : PlayerStatusBarStrip.StripType.Infection);
		return true;
	}

	private bool ShouldShowForDistance(in PlayerStatusFrameContext context, out float distanceSqr)
	{
		Vector3 offset = context.ObserverPosition - targetPlayer.transform.position;
		distanceSqr = offset.sqrMagnitude;
		return distanceSqr <= context.MaxDistanceSqr;
	}

	private static Transform? ResolveAnchor(PlayerControllerB player)
	{
		if (player.playerGlobalHead != null)
		{
			return player.playerGlobalHead;
		}

		if (player.bodyParts != null && player.bodyParts.Length > 0 && player.bodyParts[0] != null)
		{
			return player.bodyParts[0];
		}

		return player.transform;
	}

	private void LogDebugVisibility(string state, Camera? viewCamera, float distanceSqr)
	{
		if (!Plugin.Settings.DebugLogging)
		{
			return;
		}

		if (state == lastDebugVisibilityState && Time.unscaledTime < nextDebugVisibilityLogTime)
		{
			return;
		}

		lastDebugVisibilityState = state;
		nextDebugVisibilityLogTime = Time.unscaledTime + 5f;
		string distanceText = FormatDebugDistance(distanceSqr);
		Plugin.LogDebug($"View playerId={PlayerId} name='{targetPlayer.playerUsername}' state={state} health={targetPlayer.health} controlled={targetPlayer.isPlayerControlled} dead={targetPlayer.isPlayerDead} distance={distanceText} canvasEnabled={worldCanvas.enabled} camera='{(viewCamera != null ? viewCamera.name : "none")}' anchor='{(anchor != null ? anchor.name : "none")}' position={FormatVector(transform.position)} target={FormatVector(targetPlayer.transform.position)}.");
	}

	private static string FormatDebugDistance(float distanceSqr)
	{
		return distanceSqr >= 0f ? $"{Mathf.Sqrt(distanceSqr):0.0}/{Plugin.Settings.MaxDistance:0.0}" : "n/a";
	}

	private static string FormatVector(Vector3 value)
	{
		return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
	}
}
