using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.UI;

namespace OtherPlayerStatusBars;

internal sealed class PlayerStatusBarView : MonoBehaviour
{
	private static readonly Color CriticalHealthBarColor = new(0.88f, 0.15f, 0.12f, 0.95f);

	private const float LowHealthRecoveryDisplayDelay = 15f;

	private const float LowHealthConfirmDelay = 0.5f;

	private const float InitialStateStabilizationDelay = 3f;

	private readonly CadaverGrowthInfectionProvider infectionProvider = new();

	private PlayerControllerB targetPlayer = null!;

	private PlayerControllerB? localPlayer;

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

	private bool isLowHealthRecoveryTiming;

	private float lowHealthRecoveryTimer;

	private bool isLowHealthConfirming;

	private float lowHealthConfirmTimer;

	private string lastDisplayedHealthLabel = string.Empty;

	private bool lastCanvasEnabled = true;

	private Camera? lastAppliedCanvasCamera;

	private string lastDebugVisibilityState = string.Empty;

	private float nextDebugVisibilityLogTime;

	private float lastAppliedUiScale = -1f;

	private float lastAppliedHealthBarYOffset = float.NaN;

	private float lastAppliedInfectionBarYOffset = float.NaN;

	private float initializedAtTime;

	private bool suppressInitialStaleCriticalState;

	private bool suppressInitialStaleHealthState;

	private bool hasCompletedInitialStateStabilization;

	private bool hasObservedStableNonCriticalState;

	public int PlayerId => targetPlayer != null ? (int)targetPlayer.playerClientId : -1;

	public bool Initialize(PlayerControllerB player)
	{
		targetPlayer = player;
		anchor = ResolveAnchor(player);
		initializedAtTime = Time.unscaledTime;
		gameObject.AddComponent<StatusBarBillboard>();
		worldCanvas = gameObject.AddComponent<Canvas>();
		worldCanvas.renderMode = RenderMode.WorldSpace;
		worldCanvas.overrideSorting = true;
		worldCanvas.sortingOrder = 50;
		gameObject.AddComponent<CanvasScaler>();
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

		name = $"PlayerStatusBar_{player.playerClientId}";
		return true;
	}

	public bool IsStillValid(PlayerControllerB localPlayer)
	{
		if (targetPlayer == null || localPlayer == null)
		{
			return false;
		}

		if (targetPlayer == localPlayer || !targetPlayer.isPlayerControlled || targetPlayer.isPlayerDead)
		{
			return false;
		}

		return targetPlayer.gameObject.activeInHierarchy;
	}

	private void LateUpdate()
	{
		if (targetPlayer == null)
		{
			return;
		}

		localPlayer = GameNetworkManager.Instance != null
			? GameNetworkManager.Instance.localPlayerController
			: StartOfRound.Instance?.localPlayerController;
		Camera? viewCamera = StatusBarBillboard.ResolveViewCamera();
		ApplyCanvasCamera(viewCamera);
		bool canShowGroup = ShouldShowGroup();

		anchor ??= ResolveAnchor(targetPlayer);
		float stripSpacing = Plugin.Settings.BarSpacing;
		Vector3 anchorPosition = anchor != null ? anchor.position : targetPlayer.transform.position;
		transform.position = anchorPosition + Vector3.up * (Plugin.Settings.HeadOffset + stripSpacing * Plugin.Settings.UiScale);
		ApplyCanvasScale(Plugin.Settings.UiScale);
		SetCanvasEnabled(canShowGroup);
		bool targetReady = IsTargetReadyForDisplay();
		if (!canShowGroup || !targetReady)
		{
			LogDebugVisibility(!canShowGroup ? "group-hidden" : "target-not-ready", viewCamera, -1f);
			healthStrip.SetVisible(false);
			infectionStrip.SetVisible(false);
			wasHealthVisible = false;
			wasInfectionVisible = false;
			ResetLowHealthFallbackState();
			ResetInitialStateStabilization();
			return;
		}
		bool isWithinDisplayDistance = ShouldShowForDistance(viewCamera, out float displayDistance);
		ApplyStripLayoutOffsets();

		int rawHealth = Mathf.Clamp(targetPlayer.health, 0, 100);
		bool isCriticallyInjured = targetPlayer.criticallyInjured;
		bool isBleedingHeavily = targetPlayer.bleedingHeavily;
		bool rawCriticalState = isCriticallyInjured || isBleedingHeavily;
		UpdateInitialStateStabilization(rawCriticalState, rawHealth);
		bool isInCriticalState = rawCriticalState && !suppressInitialStaleCriticalState;
		bool isInitialStaleHealthState = suppressInitialStaleHealthState && rawHealth <= 20 && !isInCriticalState;
		int displayHealth = isInitialStaleHealthState ? 100 : rawHealth;
		bool showLowHealthFallback = false;
		if (!isInitialStaleHealthState && !isInCriticalState && rawHealth < 20 && !targetPlayer.isPlayerDead)
		{
			if (!isLowHealthRecoveryTiming)
			{
				if (!isLowHealthConfirming)
				{
					isLowHealthConfirming = true;
					lowHealthConfirmTimer = 0f;
				}
				else
				{
					lowHealthConfirmTimer += Time.unscaledDeltaTime;
				}

				if (lowHealthConfirmTimer >= LowHealthConfirmDelay)
				{
					isLowHealthRecoveryTiming = true;
					lowHealthRecoveryTimer = 0f;
					isLowHealthConfirming = false;
					lowHealthConfirmTimer = 0f;
				}
			}
			else
			{
				lowHealthRecoveryTimer += Time.unscaledDeltaTime;
			}

			if (isLowHealthRecoveryTiming && lowHealthRecoveryTimer < LowHealthRecoveryDisplayDelay)
			{
				showLowHealthFallback = true;
			}
			else if (isLowHealthRecoveryTiming)
			{
				displayHealth = 20;
			}
		}
		else if (isLowHealthRecoveryTiming || lowHealthRecoveryTimer > 0f || isLowHealthConfirming || lowHealthConfirmTimer > 0f)
		{
			ResetLowHealthFallbackState();
		}

		bool showHealth = isWithinDisplayDistance;
		LogDebugVisibility(showHealth ? "visible" : "distance-hidden", viewCamera, displayDistance);
		bool showCriticalHealthBar = isInCriticalState || showLowHealthFallback;
		string healthLabel = showCriticalHealthBar ? GetCriticalHealthLabel() : "HP";
		healthStrip.SetFillColorOverride(showCriticalHealthBar, CriticalHealthBarColor);
		if (showHealth && (!wasHealthVisible || displayHealth != lastDisplayedHealth || healthLabel != lastDisplayedHealthLabel))
		{
			if (showCriticalHealthBar)
			{
				healthStrip.SetLabelOnly(healthLabel, 100f, 100f);
			}
			else
			{
				healthStrip.SetDisplay(healthLabel, displayHealth, 100f, showPercent: false);
			}
			lastDisplayedHealth = displayHealth;
			lastDisplayedHealthLabel = healthLabel;
		}
		healthStrip.SetVisible(showHealth);
		wasHealthVisible = showHealth;

		float infectionMeter = 0f;
		bool hasInfectionValue = false;
		if (isWithinDisplayDistance)
		{
			hasInfectionValue = infectionProvider.TryGetNormalizedInfection(targetPlayer, out infectionMeter);
		}

		bool showInfection = Plugin.Settings.InfectionDisplayMode == StatusBarConfig.InfectionBarDisplayMode.AlwaysVisible
			? isWithinDisplayDistance
			: isWithinDisplayDistance && hasInfectionValue && infectionMeter > 0f;

		if (showInfection)
		{
			int infectionPercent = Mathf.Clamp(Mathf.RoundToInt(infectionMeter * 100f), 0, 100);
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
		}
	}

	private bool IsTargetReadyForDisplay()
	{
		return targetPlayer != null && targetPlayer.isPlayerControlled && !targetPlayer.isPlayerDead && targetPlayer.health > 0;
	}

	private void ResetLowHealthFallbackState()
	{
		isLowHealthRecoveryTiming = false;
		lowHealthRecoveryTimer = 0f;
		isLowHealthConfirming = false;
		lowHealthConfirmTimer = 0f;
	}

	private static string GetCriticalHealthLabel()
	{
		return Plugin.UseChineseCriticalLabel ? "\u91cd\u4f24" : "CRITICAL";
	}

	private void UpdateInitialStateStabilization(bool rawCriticalState, int rawHealth)
	{
		if (!rawCriticalState && rawHealth > 20)
		{
			hasObservedStableNonCriticalState = true;
		}

		if (!hasCompletedInitialStateStabilization)
		{
			float age = Time.unscaledTime - initializedAtTime;
			if (age <= InitialStateStabilizationDelay && !hasObservedStableNonCriticalState)
			{
				// Late-join and extended-player mods can briefly expose cloned/default player state.
				// Keep that initial stale low-health or critical flag from becoming a persistent bar state.
				if (rawHealth <= 20)
				{
					suppressInitialStaleHealthState = true;
				}

				if (rawCriticalState && rawHealth >= 20)
				{
					suppressInitialStaleCriticalState = true;
					suppressInitialStaleHealthState = true;
				}
			}
			else if (age > InitialStateStabilizationDelay)
			{
				hasCompletedInitialStateStabilization = true;
			}
		}

		if ((suppressInitialStaleCriticalState || suppressInitialStaleHealthState)
			&& (targetPlayer.isPlayerDead || (!rawCriticalState && rawHealth > 20)))
		{
			suppressInitialStaleCriticalState = false;
			suppressInitialStaleHealthState = false;
		}
	}

	private void ResetInitialStateStabilization()
	{
		initializedAtTime = Time.unscaledTime;
		suppressInitialStaleCriticalState = false;
		suppressInitialStaleHealthState = false;
		hasCompletedInitialStateStabilization = false;
		hasObservedStableNonCriticalState = false;
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

	private void ApplyStripLayoutOffsets()
	{
		float healthYOffset = Plugin.Settings.HealthBarYOffset;
		if (!Mathf.Approximately(lastAppliedHealthBarYOffset, healthYOffset))
		{
			healthStripRect.anchoredPosition = new Vector2(0f, healthYOffset);
			lastAppliedHealthBarYOffset = healthYOffset;
		}

		float infectionYOffset = Plugin.Settings.InfectionBarYOffset;
		if (!Mathf.Approximately(lastAppliedInfectionBarYOffset, infectionYOffset))
		{
			infectionStripRect.anchoredPosition = new Vector2(0f, infectionYOffset);
			lastAppliedInfectionBarYOffset = infectionYOffset;
		}
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

	private bool ShouldShowForDistance(Camera? viewCamera, out float distance)
	{
		float maxDistance = Plugin.Settings.MaxDistance;
		float maxDistanceSqr = maxDistance * maxDistance;
		Vector3 offset = ResolveObserverPosition(viewCamera) - targetPlayer.transform.position;
		float distanceSqr = offset.sqrMagnitude;
		distance = Mathf.Sqrt(distanceSqr);
		return distanceSqr <= maxDistanceSqr;
	}

	private Vector3 ResolveObserverPosition(Camera? viewCamera)
	{
		if (viewCamera != null)
		{
			return viewCamera.transform.position;
		}

		if (localPlayer != null)
		{
			PlayerControllerB? spectatedPlayer = localPlayer.spectatedPlayerScript;
			if (localPlayer.isPlayerDead && spectatedPlayer != null && !spectatedPlayer.isPlayerDead)
			{
				return spectatedPlayer.transform.position;
			}
		}

		return localPlayer != null ? localPlayer.transform.position : targetPlayer.transform.position;
	}

	private bool ShouldShowGroup()
	{
		if (!Plugin.Settings.HideInOrbit)
		{
			return true;
		}

		StartOfRound? startOfRound = StartOfRound.Instance;
		if (startOfRound == null)
		{
			return true;
		}

		return !PlayerStatusBarManager.ShouldSkipBarsForShipState(startOfRound);
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

	private void LogDebugVisibility(string state, Camera? viewCamera, float distance)
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
		string distanceText = distance >= 0f ? $"{distance:0.0}/{Plugin.Settings.MaxDistance:0.0}" : "n/a";
		Plugin.LogDebug($"View playerId={PlayerId} name='{targetPlayer.playerUsername}' state={state} health={targetPlayer.health} controlled={targetPlayer.isPlayerControlled} dead={targetPlayer.isPlayerDead} distance={distanceText} canvasEnabled={worldCanvas.enabled} camera='{(viewCamera != null ? viewCamera.name : "none")}' anchor='{(anchor != null ? anchor.name : "none")}' position={FormatVector(transform.position)} target={FormatVector(targetPlayer.transform.position)}.");
	}

	private static string FormatVector(Vector3 value)
	{
		return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
	}
}
