using System;

namespace OtherPlayerStatusBars;

/// <summary>
/// Turns the health value readable on this client into the number shown on the bar.
/// Unity-free so its behaviour can be unit tested; callers pass the current time in.
/// </summary>
/// <remarks>
/// The game shares a player's health with other clients on most hits, with two gaps. Regeneration below
/// 20 runs on the owner alone, at one point per second. The hit that makes a player critically injured
/// skips the health RPC and only announces the critical state, which ends exactly when health reaches 20.
/// Values that are not exact can therefore be stale, so those gaps are filled by prediction here.
/// </remarks>
internal sealed class HealthDisplayModel
{
	public const int LowHealthRegenerationTarget = 20;

	/// <summary>A hit that leaves a player with less health than this makes them critically injured.</summary>
	private const int CriticalHealthThreshold = 10;

	private const int MaxCriticalHealth = CriticalHealthThreshold - 1;

	/// <summary>A lethal hit below this damage leaves the player on this much health instead.</summary>
	private const int LethalHitProtectionDamage = 50;

	private const int LethalHitProtectedHealth = 5;

	private const float LowHealthRegenerationInterval = 1f;

	private const float DisplayStepInterval = 0.05f;

	private const float DisplayMaxDuration = 0.4f;

	/// <summary>Fixed hit damage values in the game, weighted by how many attacks use each one.</summary>
	private static readonly (int Damage, int Weight)[] CommonHits =
	{
		(7, 1), (8, 1), (10, 7), (20, 4), (25, 1), (30, 7), (35, 1), (40, 3), (50, 3), (70, 1), (80, 1), (85, 1), (90, 2), (100, 1)
	};

	private bool hasObservedRawHealth;

	private int lastObservedRawHealth;

	private bool hasObservedDamageTimestamp;

	private float lastObservedDamageTimestamp;

	private bool hasObservedCriticalState;

	private bool lastObservedCriticalState;

	private int predictionStartHealth;

	private int predictionUpperStartHealth;

	private float predictionStartTime;

	private bool hasDisplayedHealth;

	private int animationTarget;

	private int animationStartValue;

	private float animationStartTime;

	private float animationDuration;

	public HealthDisplayModel()
	{
		Reset();
	}

	public int TargetHealth { get; private set; }

	public int DisplayedHealth { get; private set; }

	public bool IsPredictionActive { get; private set; }

	public bool IsHoldingRecoveredHealth { get; private set; }

	public bool DamageSignalChanged { get; private set; }

	public bool CriticalSignalChanged { get; private set; }

	public bool IsLowHealth => TargetHealth < LowHealthRegenerationTarget || DisplayedHealth < LowHealthRegenerationTarget;

	/// <summary>
	/// Best guess of the health left by a hit that made a player critical, given their health before it.
	/// Averages the outcomes of the common hits that could have done that; the protected value is the
	/// most likely single outcome, so it is also the fallback for damage the game computes at runtime.
	/// </summary>
	public static int EstimateHealthAfterCriticalHit(int healthBeforeHit)
	{
		int weightedHealth = 0;
		int totalWeight = 0;
		foreach ((int damage, int weight) in CommonHits)
		{
			bool isProtected = healthBeforeHit - damage <= 0 && damage < LethalHitProtectionDamage;
			int healthAfterHit = isProtected ? LethalHitProtectedHealth : healthBeforeHit - damage;
			if (healthAfterHit >= 1 && healthAfterHit <= MaxCriticalHealth)
			{
				weightedHealth += healthAfterHit * weight;
				totalWeight += weight;
			}
		}

		return totalWeight == 0
			? LethalHitProtectedHealth
			: (int)Math.Round((double)weightedHealth / totalWeight);
	}

	public int Update(int rawHealth, bool isExact, bool isCritical, float damageTimestamp, float now)
	{
		bool isFirstCriticalObservation = !hasObservedCriticalState && isCritical;
		DamageSignalChanged = ObserveDamageTimestamp(damageTimestamp);
		CriticalSignalChanged = ObserveCriticalState(isCritical);
		TargetHealth = ResolveTarget(rawHealth, isExact, isCritical, isFirstCriticalObservation, now);
		DisplayedHealth = ResolveAnimatedDisplay(TargetHealth, now);
		return DisplayedHealth;
	}

	public void Reset()
	{
		hasObservedRawHealth = false;
		lastObservedRawHealth = int.MinValue;
		hasObservedDamageTimestamp = false;
		lastObservedDamageTimestamp = 0f;
		hasObservedCriticalState = false;
		lastObservedCriticalState = false;
		DamageSignalChanged = false;
		CriticalSignalChanged = false;
		ResetPrediction();
		IsHoldingRecoveredHealth = false;
		ResetDisplay();
	}

	/// <summary>Makes the next update snap to its target instead of animating towards it.</summary>
	public void ResetDisplay()
	{
		hasDisplayedHealth = false;
		TargetHealth = int.MinValue;
		DisplayedHealth = int.MinValue;
		animationTarget = int.MinValue;
		animationStartValue = 0;
		animationStartTime = 0f;
		animationDuration = 0f;
	}

	private bool ObserveDamageTimestamp(float damageTimestamp)
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

	private bool ObserveCriticalState(bool isCritical)
	{
		if (!hasObservedCriticalState)
		{
			hasObservedCriticalState = true;
			lastObservedCriticalState = isCritical;
			return false;
		}

		if (isCritical == lastObservedCriticalState)
		{
			return false;
		}

		lastObservedCriticalState = isCritical;
		return true;
	}

	private int ResolveTarget(int rawHealth, bool isExact, bool isCritical, bool isFirstCriticalObservation, float now)
	{
		bool rawHealthChanged = !hasObservedRawHealth || rawHealth != lastObservedRawHealth;
		hasObservedRawHealth = true;
		lastObservedRawHealth = rawHealth;

		if (isExact || rawHealth <= 0)
		{
			ResetPrediction();
			IsHoldingRecoveredHealth = false;
			return rawHealth;
		}

		bool becameCritical = CriticalSignalChanged && isCritical;
		bool clearedCritical = CriticalSignalChanged && !isCritical;
		if (becameCritical || (isFirstCriticalObservation && rawHealth > MaxCriticalHealth))
		{
			BeginCriticalEstimate(rawHealthChanged ? rawHealth : ResolveCurrentEstimate(rawHealth, now), now);
		}
		else if (rawHealthChanged || DamageSignalChanged)
		{
			ResetPrediction();
			IsHoldingRecoveredHealth = false;
		}
		else if (clearedCritical)
		{
			IsHoldingRecoveredHealth = HasHadTimeToRecover(now);
			ResetPrediction();
		}

		if (IsHoldingRecoveredHealth)
		{
			return LowHealthRegenerationTarget;
		}

		if (!IsPredictionActive)
		{
			if (rawHealth >= LowHealthRegenerationTarget)
			{
				return rawHealth;
			}

			BeginPrediction(rawHealth, rawHealth, now);
		}

		int predictedHealth = PredictHealth(predictionStartHealth, now);
		if (isCritical)
		{
			return Math.Min(predictedHealth, LowHealthRegenerationTarget - 1);
		}

		if (predictedHealth >= LowHealthRegenerationTarget)
		{
			ResetPrediction();
			IsHoldingRecoveredHealth = true;
			return LowHealthRegenerationTarget;
		}

		return predictedHealth;
	}

	private int ResolveCurrentEstimate(int rawHealth, float now)
	{
		if (IsHoldingRecoveredHealth)
		{
			return LowHealthRegenerationTarget;
		}

		return IsPredictionActive ? PredictHealth(predictionStartHealth, now) : rawHealth;
	}

	private void BeginCriticalEstimate(int healthBeforeHit, float now)
	{
		IsHoldingRecoveredHealth = false;
		BeginPrediction(EstimateHealthAfterCriticalHit(healthBeforeHit), MaxCriticalHealth, now);
	}

	/// <summary>
	/// Whether enough time passed to regenerate to 20 from the highest health the player could have had.
	/// If not, something other than regeneration cleared the critical state, so the raw value is trusted.
	/// </summary>
	private bool HasHadTimeToRecover(float now)
	{
		return IsPredictionActive && PredictHealth(predictionUpperStartHealth, now) >= LowHealthRegenerationTarget - 1;
	}

	private int PredictHealth(int startHealth, float now)
	{
		int regenerationSteps = Math.Max(0, (int)Math.Floor((now - predictionStartTime) / LowHealthRegenerationInterval));
		return Math.Min(LowHealthRegenerationTarget, startHealth + regenerationSteps);
	}

	private void BeginPrediction(int startHealth, int upperStartHealth, float now)
	{
		IsPredictionActive = true;
		predictionStartHealth = startHealth;
		predictionUpperStartHealth = upperStartHealth;
		predictionStartTime = now;
	}

	private void ResetPrediction()
	{
		IsPredictionActive = false;
		predictionStartHealth = int.MinValue;
		predictionUpperStartHealth = int.MinValue;
		predictionStartTime = 0f;
	}

	private int ResolveAnimatedDisplay(int targetHealth, float now)
	{
		if (!hasDisplayedHealth)
		{
			hasDisplayedHealth = true;
			animationTarget = targetHealth;
			return targetHealth;
		}

		if (targetHealth == DisplayedHealth)
		{
			animationTarget = targetHealth;
			return targetHealth;
		}

		if (targetHealth != animationTarget)
		{
			animationTarget = targetHealth;
			animationStartValue = DisplayedHealth;
			animationStartTime = now;
			animationDuration = Math.Min(DisplayMaxDuration, Math.Abs(targetHealth - DisplayedHealth) * DisplayStepInterval);
		}

		float progress = Math.Min(1f, Math.Max(0f, (now - animationStartTime) / animationDuration));
		return (int)Math.Round(animationStartValue + (targetHealth - animationStartValue) * progress);
	}
}
