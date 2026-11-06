using System;

namespace OtherPlayerStatusBars;

/// <summary>
/// Turns sampled infection meter values into the percentage shown on the bar: it climbs one percent at
/// a time and drops immediately on healing or cure. Unity-free so its behaviour can be unit tested;
/// callers pass the current time in.
/// </summary>
/// <remarks>
/// Exact values come from the infected player and change smoothly, so they only need a short look-ahead.
/// Other values are what the game shares, which is one update per roughly 5% of growth. Those are
/// extrapolated at the last observed rate until the next update is due, and a rising update never moves
/// the bar backwards, so the estimate cannot visibly overshoot and snap back.
/// </remarks>
internal sealed class InfectionDisplayModel
{
	private const int MinimumPercent = 1;

	private const int MaximumPercent = 99;

	private const float ExactPredictionWindow = 0.5f;

	private const float MaxSmoothedPredictionWindow = 60f;

	private const float DisplayStepInterval = 0.1f;

	private bool isPredictionActive;

	private float lastObservedMeter;

	private float lastObservedSampleTime;

	private float predictionStartMeter;

	private float predictionStartTime;

	private float predictionRate;

	private float predictionWindow;

	private bool hasDisplayedPercent;

	private int displayedPercent;

	private float nextDisplayStepTime;

	public int Update(float rawMeter, bool isExact, float now)
	{
		rawMeter = Clamp01(rawMeter);
		if (!isPredictionActive)
		{
			BeginPrediction(rawMeter, 0f, ExactPredictionWindow, now);
			lastObservedMeter = rawMeter;
			lastObservedSampleTime = now;
			return ResolveSteppedPercent(ToPercent(rawMeter), true, now);
		}

		bool meterDropped = rawMeter < lastObservedMeter;
		if (meterDropped)
		{
			BeginPrediction(rawMeter, 0f, ExactPredictionWindow, now);
			lastObservedSampleTime = now;
		}
		else if (rawMeter > lastObservedMeter)
		{
			float sampleInterval = Math.Max(ExactPredictionWindow, now - lastObservedSampleTime);
			float window = isExact ? ExactPredictionWindow : Math.Min(MaxSmoothedPredictionWindow, sampleInterval);
			BeginPrediction(rawMeter, (rawMeter - lastObservedMeter) / sampleInterval, window, now);
			lastObservedSampleTime = now;
		}

		lastObservedMeter = rawMeter;
		return ResolveSteppedPercent(ToPercent(PredictMeter(now)), isExact || meterDropped, now);
	}

	public void Reset()
	{
		isPredictionActive = false;
		lastObservedMeter = 0f;
		lastObservedSampleTime = 0f;
		predictionStartMeter = 0f;
		predictionStartTime = 0f;
		predictionRate = 0f;
		predictionWindow = 0f;
		hasDisplayedPercent = false;
		displayedPercent = int.MinValue;
		nextDisplayStepTime = 0f;
	}

	private void BeginPrediction(float rawMeter, float rate, float window, float now)
	{
		isPredictionActive = true;
		predictionStartMeter = rawMeter;
		predictionStartTime = now;
		predictionRate = rate;
		predictionWindow = window;
	}

	private float PredictMeter(float now)
	{
		float elapsed = Math.Min(now - predictionStartTime, predictionWindow);
		return Clamp01(predictionStartMeter + predictionRate * elapsed);
	}

	private int ResolveSteppedPercent(int targetPercent, bool canDrop, float now)
	{
		if (!hasDisplayedPercent)
		{
			hasDisplayedPercent = true;
			displayedPercent = targetPercent;
			nextDisplayStepTime = now + DisplayStepInterval;
			return displayedPercent;
		}

		if (targetPercent < displayedPercent)
		{
			if (canDrop)
			{
				displayedPercent = targetPercent;
				nextDisplayStepTime = now + DisplayStepInterval;
			}

			return displayedPercent;
		}

		if (now < nextDisplayStepTime)
		{
			return displayedPercent;
		}

		displayedPercent = Math.Min(targetPercent, displayedPercent + 1);
		nextDisplayStepTime = now + DisplayStepInterval;
		return displayedPercent;
	}

	private static int ToPercent(float meter)
	{
		int percent = (int)Math.Floor(Clamp01(meter) * 100f);
		return Math.Min(MaximumPercent, Math.Max(MinimumPercent, percent));
	}

	private static float Clamp01(float value)
	{
		return Math.Min(1f, Math.Max(0f, value));
	}
}
