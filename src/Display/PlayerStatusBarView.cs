using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.UI;

namespace OtherPlayerStatusBars;

/// <summary>
/// World-space bars for one player. Picks the best available status source and applies the result to
/// the UI; the number shown is decided by <see cref="HealthDisplayModel"/> and <see cref="InfectionDisplayModel"/>.
/// </summary>
internal sealed class PlayerStatusBarView : MonoBehaviour
{
	private static readonly Color CriticalHealthBarColor = new(0.88f, 0.15f, 0.12f, 0.95f);

	private const float InfectionStripYOffset = 25f;

	private const float SegmentedStripGap = 1.25f;

	private const float SegmentedPanelPadding = 1.67f;

	private const float SegmentedPanelBrightness = 1.25f;

	private const float DebugVisibilityLogInterval = 5f;

	private readonly HealthDisplayModel healthDisplay = new();

	private readonly InfectionDisplayModel infectionDisplay = new();

	private readonly BarValueAnimation healthBarAnimation = new();

	private readonly BarValueAnimation infectionBarAnimation = new();

	private readonly BarPositionStabilizer positionStabilizer = new();

	private PlayerControllerB targetPlayer = null!;

	private Transform? anchor;

	private ulong boundActualClientId = ulong.MaxValue;

	private int boundPlayerKey = -1;

	private PlayerStatusBarStrip healthStrip = null!;

	private PlayerStatusBarStrip infectionStrip = null!;

	private StatusBarConfig.BarStyle appliedBarStyle = StatusBarConfig.BarStyle.Classic;

	private SegmentedBarStrip? segmentedHealthStrip;

	private SegmentedBarStrip? segmentedInfectionStrip;

	private Image? segmentedPanel;

	private bool isSegmentedPanelLaidOut;

	private bool lastSegmentedPanelShowedInfection;

	private int lastSegmentedPanelSettingsRevision = -1;

	private Canvas worldCanvas = null!;

	private RectTransform canvasRect = null!;

	private int lastDisplayedHealth = int.MinValue;

	private int lastDisplayedInfectionPercent = int.MinValue;

	private bool wasHealthVisible;

	private bool wasInfectionVisible;

	private bool lastCanvasEnabled = true;

	private Camera? lastAppliedCanvasCamera;

	private float lastAppliedUiScale = -1f;

	private bool wasTargetDead;

	private bool wasUsingSnapshot;

	private string lastDebugVisibilityState = string.Empty;

	private float nextDebugVisibilityLogTime;

	private int lastDebugRawHealth = int.MinValue;

	private bool lastDebugRawHealthExact;

	private bool lastDebugRawCriticalState;

	public int PlayerId => boundPlayerKey >= 0 ? boundPlayerKey : targetPlayer != null ? (int)targetPlayer.playerClientId : -1;

	private bool IsSegmented => appliedBarStyle == StatusBarConfig.BarStyle.Segmented;

	public bool Initialize(PlayerControllerB player, int playerKey)
	{
		worldCanvas = gameObject.AddComponent<Canvas>();
		worldCanvas.renderMode = RenderMode.WorldSpace;
		worldCanvas.overrideSorting = true;
		worldCanvas.sortingOrder = 50;
		canvasRect = worldCanvas.GetComponent<RectTransform>();
		canvasRect.sizeDelta = new Vector2(220f, 84f);

		healthStrip = CreateStrip("HealthStrip", PlayerStatusBarStrip.StripType.Health, 0f);
		infectionStrip = CreateStrip("InfectionStrip", PlayerStatusBarStrip.StripType.Infection, InfectionStripYOffset);

		Bind(player, playerKey);
		return true;
	}

	internal void Bind(PlayerControllerB player, int playerKey)
	{
		targetPlayer = player;
		boundPlayerKey = playerKey;
		boundActualClientId = player.actualClientId;
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

		if (healthStrip != null && infectionStrip != null)
		{
			HideStrips();
		}

		targetPlayer = null!;
		anchor = null;
		boundPlayerKey = -1;
		boundActualClientId = ulong.MaxValue;
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

	public bool MatchesPlayer(PlayerControllerB player, PlayerControllerB localPlayer, int playerKey)
	{
		return IsStillValid(localPlayer)
			&& targetPlayer == player
			&& boundPlayerKey == playerKey
			&& boundActualClientId == player.actualClientId;
	}

	internal void Tick(in PlayerStatusFrameContext context)
	{
		if (targetPlayer == null)
		{
			return;
		}

		StatusBarConfig settings = context.Settings;
		ApplyBarStyle(settings.Style);
		bool hasSyncedSnapshot = PlayerStatusSnapshotSync.TryGetSnapshot(targetPlayer, out PlayerStatusSnapshot syncedSnapshot);
		HandleSnapshotSourceTransition(hasSyncedSnapshot);
		bool targetIsDead = hasSyncedSnapshot
			? syncedSnapshot.IsDead || syncedSnapshot.Health <= 0
			: targetPlayer.isPlayerDead || PlayerStatusSnapshotSync.SlotInheritance.ResolveHealth(boundPlayerKey, targetPlayer.health) <= 0;
		HandleDeathStateTransition(targetIsDead);
		if (!IsTargetReadyForDisplay(targetIsDead))
		{
			HideAll(settings, context.ViewCamera);
			return;
		}

		// Keep following health while out of range: the critical hit and its clear happen only once.
		float now = Time.unscaledTime;
		bool hasOwnerReport = PlayerStatusSnapshotSync.TryGetOwnerReport(targetPlayer, boundPlayerKey, out OwnerStatusReport ownerReport);
		int displayHealth = ResolveDisplayHealth(hasSyncedSnapshot, syncedSnapshot, hasOwnerReport, ownerReport, now);

		anchor ??= ResolveAnchor(targetPlayer);
		Vector3 anchorPosition = anchor != null ? anchor.position : targetPlayer.transform.position;
		transform.position = ResolveStablePosition(anchorPosition + Vector3.up * context.AnchorYOffset, context.ViewCamera);
		if (context.HasBillboardRotation)
		{
			transform.rotation = context.BillboardRotation;
		}
		ApplyCanvasCamera(context.ViewCamera);
		ApplyCanvasScale(context.UiScale);
		if (!ShouldShowForDistance(context, out float displayDistanceSqr))
		{
			HideForDistance(context.ViewCamera, displayDistanceSqr);
			return;
		}

		UpdateHealthStrip(displayHealth, now);
		LogDebugVisibility("visible", context.ViewCamera, displayDistanceSqr);
		bool showInfection = UpdateInfectionStrip(hasSyncedSnapshot, syncedSnapshot, hasOwnerReport, ownerReport, now);
		UpdateSegmentedPanel(settings, showInfection);
		SetCanvasEnabled(true);
		ApplyStripsIfDirty(settings);
		wasHealthVisible = true;
		wasInfectionVisible = showInfection;
	}

	/// <summary>Ignores small sway, then aligns to the screen's pixels so thin lines don't shimmer.</summary>
	private Vector3 ResolveStablePosition(Vector3 targetPosition, Camera? viewCamera)
	{
		positionStabilizer.Update(targetPosition.x, targetPosition.y, targetPosition.z);
		Vector3 stablePosition = new(positionStabilizer.X, positionStabilizer.Y, positionStabilizer.Z);
		if (viewCamera == null)
		{
			return stablePosition;
		}

		Vector3 screenPosition = viewCamera.WorldToScreenPoint(stablePosition);
		if (screenPosition.z <= 0f)
		{
			return stablePosition;
		}

		screenPosition.x = Mathf.Round(screenPosition.x);
		screenPosition.y = Mathf.Round(screenPosition.y);
		return viewCamera.ScreenToWorldPoint(screenPosition);
	}

	private int ResolveDisplayHealth(bool hasSyncedSnapshot, in PlayerStatusSnapshot syncedSnapshot, bool hasOwnerReport, in OwnerStatusReport ownerReport, float now)
	{
		int rawHealth;
		bool isHealthExact;
		bool isCritical;
		if (hasSyncedSnapshot)
		{
			rawHealth = syncedSnapshot.Health;
			isHealthExact = syncedSnapshot.IsHealthExact;
			isCritical = syncedSnapshot.IsCritical;
		}
		else
		{
			rawHealth = hasOwnerReport ? ownerReport.Health : PlayerStatusSnapshotSync.SlotInheritance.ResolveHealth(boundPlayerKey, targetPlayer.health);
			isHealthExact = hasOwnerReport;
			isCritical = targetPlayer.criticallyInjured;
		}

		rawHealth = Mathf.Clamp(rawHealth, 0, 100);
		lastDebugRawHealth = rawHealth;
		lastDebugRawHealthExact = isHealthExact;
		lastDebugRawCriticalState = isCritical;
		return healthDisplay.Update(rawHealth, isHealthExact, isCritical, targetPlayer.timeSinceTakingDamage, now);
	}

	private void UpdateHealthStrip(int displayHealth, float now)
	{
		if (IsSegmented && segmentedHealthStrip != null)
		{
			healthBarAnimation.Update(healthDisplay.TargetHealth, now);
			string healthLabel = StatusBarTextCache.GetNumberLabel(Mathf.RoundToInt(healthBarAnimation.Fill));
			segmentedHealthStrip.SetFillColorOverride(healthDisplay.IsLowHealth, StatusBarConfig.SegmentedCriticalColor);
			segmentedHealthStrip.SetValues(healthBarAnimation.Fill, healthBarAnimation.Trail, healthBarAnimation.Target, healthLabel);
			segmentedHealthStrip.SetVisible(true);
			return;
		}

		healthStrip.SetFillColorOverride(healthDisplay.IsLowHealth, CriticalHealthBarColor);
		if (!wasHealthVisible || displayHealth != lastDisplayedHealth)
		{
			healthStrip.SetDisplay(displayHealth, 100f, showPercent: false);
			lastDisplayedHealth = displayHealth;
		}
		healthStrip.SetVisible(true);
	}

	private bool UpdateInfectionStrip(bool hasSyncedSnapshot, in PlayerStatusSnapshot syncedSnapshot, bool hasOwnerReport, in OwnerStatusReport ownerReport, float now)
	{
		if (!PlayerStatusSnapshotSync.IsInfectionDisplayAllowed)
		{
			HideInfectionStrip();
			return false;
		}

		bool hasInfection;
		bool isInfectionExact;
		float infectionMeter;
		if (hasSyncedSnapshot && syncedSnapshot.IsInfectionKnown)
		{
			hasInfection = syncedSnapshot.HasInfection;
			isInfectionExact = syncedSnapshot.IsInfectionExact;
			infectionMeter = syncedSnapshot.InfectionMeter;
		}
		else if (hasOwnerReport && ownerReport.IsInfectionKnown)
		{
			hasInfection = ownerReport.HasInfection;
			isInfectionExact = true;
			infectionMeter = ownerReport.InfectionMeter;
		}
		else
		{
			hasInfection = CadaverGrowthInfectionProvider.TryGetNormalizedInfection(targetPlayer, boundPlayerKey, out infectionMeter);
			isInfectionExact = false;
		}

		if (!hasInfection)
		{
			if (TryShowInfectionClearing(now))
			{
				return true;
			}

			HideInfectionStrip();
			return false;
		}

		int infectionPercent = infectionDisplay.Update(infectionMeter, isInfectionExact, now);
		if (IsSegmented && segmentedInfectionStrip != null)
		{
			infectionBarAnimation.Update(infectionPercent, now);
			float fill = infectionBarAnimation.Fill;
			segmentedInfectionStrip.SetValues(fill, infectionBarAnimation.Trail, fill, StatusBarTextCache.GetPercentLabel(Mathf.RoundToInt(fill)));
			segmentedInfectionStrip.SetVisible(true);
			return true;
		}

		if (!wasInfectionVisible || infectionPercent != lastDisplayedInfectionPercent)
		{
			infectionStrip.SetDisplay(infectionPercent, 100f, showPercent: true);
			lastDisplayedInfectionPercent = infectionPercent;
		}
		infectionStrip.SetVisible(true);
		return true;
	}

	/// <summary>Keeps a cured player's infection bar up until its trail has run out.</summary>
	private bool TryShowInfectionClearing(float now)
	{
		if (!IsSegmented || segmentedInfectionStrip == null || !wasInfectionVisible)
		{
			return false;
		}

		infectionBarAnimation.Update(0, now);
		if (infectionBarAnimation.Trail <= 0f)
		{
			return false;
		}

		segmentedInfectionStrip.SetValues(0f, infectionBarAnimation.Trail, 0f, StatusBarTextCache.GetPercentLabel(0));
		return true;
	}

	private void HideInfectionStrip()
	{
		infectionStrip.SetVisible(false);
		segmentedInfectionStrip?.SetVisible(false);
		ResetInfectionDisplay();
	}

	private void HideForDistance(Camera? viewCamera, float distanceSqr)
	{
		LogDebugVisibility("distance-hidden", viewCamera, distanceSqr);
		SetCanvasEnabled(false);
		HideStrips();
		if (wasHealthVisible)
		{
			healthDisplay.ResetDisplay();
			healthBarAnimation.Reset();
		}

		if (wasInfectionVisible)
		{
			ResetInfectionDisplay();
		}

		wasHealthVisible = false;
		wasInfectionVisible = false;
	}

	private void HideAll(StatusBarConfig settings, Camera? viewCamera)
	{
		LogDebugVisibility("target-not-ready", viewCamera, -1f);
		SetCanvasEnabled(false);
		HideStrips();
		wasHealthVisible = false;
		wasInfectionVisible = false;
		ResetInfectionDisplay();
		ApplyStripsIfDirty(settings);
	}

	private void HideStrips()
	{
		healthStrip.SetVisible(false);
		infectionStrip.SetVisible(false);
		segmentedHealthStrip?.SetVisible(false);
		segmentedInfectionStrip?.SetVisible(false);
	}

	private void ApplyBarStyle(StatusBarConfig.BarStyle style)
	{
		if (style == appliedBarStyle)
		{
			return;
		}

		appliedBarStyle = style;
		if (IsSegmented && segmentedPanel == null)
		{
			CreateSegmentedBars();
		}

		HideStrips();
		segmentedPanel?.gameObject.SetActive(IsSegmented);
		wasHealthVisible = false;
		wasInfectionVisible = false;
		lastDisplayedHealth = int.MinValue;
		lastDisplayedInfectionPercent = int.MinValue;
		healthBarAnimation.Reset();
		infectionBarAnimation.Reset();
	}

	private void CreateSegmentedBars()
	{
		GameObject panelObject = new("SegmentedPanel", typeof(RectTransform), typeof(Image));
		panelObject.transform.SetParent(worldCanvas.transform, worldPositionStays: false);
		panelObject.transform.SetAsFirstSibling();
		segmentedPanel = panelObject.GetComponent<Image>();
		segmentedPanel.raycastTarget = false;
		segmentedHealthStrip = CreateSegmentedStrip("SegmentedHealthStrip", "HP", false, 0f);
		segmentedInfectionStrip = CreateSegmentedStrip("SegmentedInfectionStrip", "INF", true, SegmentedBarStrip.Height + SegmentedStripGap);
	}

	private SegmentedBarStrip CreateSegmentedStrip(string stripName, string label, bool isInfection, float yOffset)
	{
		GameObject stripObject = new(stripName, typeof(RectTransform));
		stripObject.transform.SetParent(worldCanvas.transform, worldPositionStays: false);
		SegmentedBarStrip strip = stripObject.AddComponent<SegmentedBarStrip>();
		strip.Initialize(label, isInfection);
		strip.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, yOffset);
		return strip;
	}

	private void UpdateSegmentedPanel(StatusBarConfig settings, bool showInfection)
	{
		if (!IsSegmented || segmentedPanel == null)
		{
			return;
		}

		if (settings.Revision != lastSegmentedPanelSettingsRevision)
		{
			lastSegmentedPanelSettingsRevision = settings.Revision;
			// Dark colours lose brightness on screen, so the panel is lifted to look like its preset.
			Color panelColor = settings.GetBackgroundColor() * SegmentedPanelBrightness;
			panelColor.a = 1f;
			segmentedPanel.color = panelColor;
		}

		if (isSegmentedPanelLaidOut && showInfection == lastSegmentedPanelShowedInfection)
		{
			return;
		}

		isSegmentedPanelLaidOut = true;
		lastSegmentedPanelShowedInfection = showInfection;
		float infectionHeight = showInfection ? SegmentedBarStrip.Height + SegmentedStripGap : 0f;
		RectTransform panelRect = segmentedPanel.rectTransform;
		panelRect.sizeDelta = new Vector2(
			SegmentedBarStrip.Width + SegmentedPanelPadding * 2f,
			SegmentedBarStrip.Height + infectionHeight + SegmentedPanelPadding * 2f);
		panelRect.anchoredPosition = new Vector2(0f, infectionHeight * 0.5f);
	}

	private bool IsTargetReadyForDisplay(bool targetIsDead)
	{
		return targetPlayer != null
			&& !targetPlayer.disconnectedMidGame
			&& !targetIsDead
			&& targetPlayer.gameObject.activeInHierarchy;
	}

	private void ResetRuntimeState()
	{
		wasHealthVisible = false;
		wasInfectionVisible = false;
		wasTargetDead = false;
		wasUsingSnapshot = false;
		lastAppliedCanvasCamera = null;
		lastAppliedUiScale = -1f;
		lastDebugVisibilityState = string.Empty;
		nextDebugVisibilityLogTime = 0f;
		lastDebugRawHealth = int.MinValue;
		lastDebugRawHealthExact = false;
		lastDebugRawCriticalState = false;
		positionStabilizer.Reset();
		ResetStatusDisplay();
	}

	private void ResetStatusDisplay()
	{
		lastDisplayedHealth = int.MinValue;
		healthDisplay.Reset();
		healthBarAnimation.Reset();
		ResetInfectionDisplay();
	}

	private void ResetInfectionDisplay()
	{
		lastDisplayedInfectionPercent = int.MinValue;
		infectionDisplay.Reset();
		infectionBarAnimation.Reset();
	}

	private void HandleSnapshotSourceTransition(bool hasSyncedSnapshot)
	{
		if (wasUsingSnapshot == hasSyncedSnapshot)
		{
			return;
		}

		wasUsingSnapshot = hasSyncedSnapshot;
		ResetStatusDisplay();
	}

	private void HandleDeathStateTransition(bool targetIsDead)
	{
		if (wasTargetDead == targetIsDead)
		{
			return;
		}

		wasTargetDead = targetIsDead;
		ResetStatusDisplay();
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

	private void ApplyStripsIfDirty(StatusBarConfig settings)
	{
		healthStrip.ApplyIfDirty(settings);
		infectionStrip.ApplyIfDirty(settings);
		segmentedHealthStrip?.ApplyIfDirty(settings);
		segmentedInfectionStrip?.ApplyIfDirty(settings);
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

	private PlayerStatusBarStrip CreateStrip(string stripName, PlayerStatusBarStrip.StripType stripType, float yOffset)
	{
		GameObject stripObject = new(stripName, typeof(RectTransform));
		stripObject.transform.SetParent(worldCanvas.transform, worldPositionStays: false);
		stripObject.transform.localPosition = Vector3.zero;
		stripObject.transform.localRotation = Quaternion.identity;

		PlayerStatusBarStrip strip = stripObject.AddComponent<PlayerStatusBarStrip>();
		strip.Initialize(stripType);
		strip.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, yOffset);
		return strip;
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
		nextDebugVisibilityLogTime = Time.unscaledTime + DebugVisibilityLogInterval;
		string distanceText = FormatDebugDistance(distanceSqr);
		Plugin.LogDebug($"View playerId={PlayerId} clientId={targetPlayer.playerClientId} actualClientId={targetPlayer.actualClientId} boundActualClientId={boundActualClientId} snapshot={wasUsingSnapshot} name='{targetPlayer.playerUsername}' state={state} health={targetPlayer.health} rawHealth={lastDebugRawHealth} rawHealthExact={lastDebugRawHealthExact} displayHealthTarget={healthDisplay.TargetHealth} displayHealth={healthDisplay.DisplayedHealth} rawCritical={lastDebugRawCriticalState} damageSignalChanged={healthDisplay.DamageSignalChanged} criticalSignalChanged={healthDisplay.CriticalSignalChanged} predictionActive={healthDisplay.IsPredictionActive} naturalHold={healthDisplay.IsHoldingRecoveredHealth} controlled={targetPlayer.isPlayerControlled} dead={targetPlayer.isPlayerDead} distance={distanceText} canvasEnabled={worldCanvas.enabled} camera='{(viewCamera != null ? viewCamera.name : "none")}' anchor='{(anchor != null ? anchor.name : "none")}' position={FormatVector(transform.position)} target={FormatVector(targetPlayer.transform.position)}.");
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
