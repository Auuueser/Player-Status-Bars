using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OtherPlayerStatusBars;

internal sealed class PlayerStatusBarStrip : MonoBehaviour
{
	internal enum StripType
	{
		Health,
		Infection
	}

	private const float Width = 172f;

	private const float Height = 20f;

	private bool visible = true;

	private string label = string.Empty;

	private float currentValue;

	private float maxValue = 1f;

	private bool renderAsPercent;

	private bool labelOnly;

	private StripType stripType;

	private RectTransform rectTransform = null!;

	private Image backgroundImage = null!;

	private Image fillImage = null!;

	private TextMeshProUGUI text = null!;

	private Outline border = null!;

	private RectTransform fillRect = null!;

	private bool dirty = true;

	private bool lastAppliedVisible = true;

	private float lastAppliedNormalized = -1f;

	private string lastAppliedText = string.Empty;

	private bool lastAppliedTextEnabled = true;

	private Color lastAppliedBackgroundColor = Color.clear;

	private Color lastAppliedFillColor = Color.clear;

	private Color lastAppliedBorderColor = Color.clear;

	private int lastAppliedSettingsRevision = -1;

	private bool hasFillColorOverride;

	private Color fillColorOverride;

	public void Initialize(StripType type)
	{
		stripType = type;
		rectTransform = gameObject.GetComponent<RectTransform>();
		if (rectTransform == null)
		{
			rectTransform = gameObject.AddComponent<RectTransform>();
		}

		rectTransform.sizeDelta = new Vector2(Width, Height);
		rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);

		GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image), typeof(Outline));
		backgroundObject.transform.SetParent(transform, worldPositionStays: false);
		RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
		backgroundRect.anchorMin = Vector2.zero;
		backgroundRect.anchorMax = Vector2.one;
		backgroundRect.offsetMin = Vector2.zero;
		backgroundRect.offsetMax = Vector2.zero;
		backgroundImage = backgroundObject.GetComponent<Image>();
		border = backgroundObject.GetComponent<Outline>();

		GameObject fillObject = new("Fill", typeof(RectTransform), typeof(Image));
		fillObject.transform.SetParent(backgroundObject.transform, worldPositionStays: false);
		RectTransform fillRect = fillObject.GetComponent<RectTransform>();
		fillRect.anchorMin = new Vector2(0f, 0f);
		fillRect.anchorMax = new Vector2(0f, 1f);
		fillRect.pivot = new Vector2(0f, 0.5f);
		fillRect.offsetMin = new Vector2(1f, 1f);
		fillRect.offsetMax = new Vector2(-1f, -1f);
		fillImage = fillObject.GetComponent<Image>();
		this.fillRect = fillRect;

		GameObject textObject = new("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(backgroundObject.transform, worldPositionStays: false);
		RectTransform textRect = textObject.GetComponent<RectTransform>();
		textRect.anchorMin = Vector2.zero;
		textRect.anchorMax = Vector2.one;
		textRect.offsetMin = new Vector2(6f, 1f);
		textRect.offsetMax = new Vector2(-6f, -1f);
		text = textObject.GetComponent<TextMeshProUGUI>();
		text.alignment = TextAlignmentOptions.Center;
		text.fontSize = 12f;
		text.enableWordWrapping = false;
		text.color = Color.white;
	}

	public void SetDisplay(string displayLabel, float value, float max, bool showPercent)
	{
		float clampedValue = Mathf.Clamp(value, 0f, max <= 0f ? 1f : max);
		float clampedMax = Mathf.Max(max, 0.0001f);
		if (label == displayLabel && Mathf.Approximately(currentValue, clampedValue) && Mathf.Approximately(maxValue, clampedMax) && renderAsPercent == showPercent && !labelOnly)
		{
			visible = true;
			return;
		}

		label = displayLabel;
		currentValue = clampedValue;
		maxValue = clampedMax;
		renderAsPercent = showPercent;
		labelOnly = false;
		visible = true;
		dirty = true;
	}

	public void SetLabelOnly(string displayLabel, float fillValue, float max)
	{
		float clampedValue = Mathf.Clamp(fillValue, 0f, max <= 0f ? 1f : max);
		float clampedMax = Mathf.Max(max, 0.0001f);
		if (label == displayLabel && Mathf.Approximately(currentValue, clampedValue) && Mathf.Approximately(maxValue, clampedMax) && labelOnly)
		{
			visible = true;
			return;
		}

		label = displayLabel;
		currentValue = clampedValue;
		maxValue = clampedMax;
		renderAsPercent = false;
		labelOnly = true;
		visible = true;
		dirty = true;
	}

	public void SetVisible(bool isVisible)
	{
		if (visible == isVisible && gameObject.activeSelf == isVisible)
		{
			return;
		}

		visible = isVisible;
		gameObject.SetActive(isVisible);
		dirty = true;
	}

	public void SetFillColorOverride(bool enabled, Color color)
	{
		if (hasFillColorOverride == enabled && (!enabled || fillColorOverride == color))
		{
			return;
		}

		hasFillColorOverride = enabled;
		fillColorOverride = color;
		dirty = true;
	}

	private void LateUpdate()
	{
		if (backgroundImage == null || fillImage == null || text == null)
		{
			return;
		}

		if (!visible)
		{
			return;
		}

		StatusBarConfig settings = Plugin.Settings;
		if (!dirty && settings.Revision == lastAppliedSettingsRevision)
		{
			return;
		}

		float normalizedValue = Mathf.Clamp01(currentValue / maxValue);
		Color fillColor = stripType == StripType.Health
			? settings.GetHealthFillColor()
			: settings.GetInfectionFillColor();
		if (hasFillColorOverride)
		{
			fillColor = fillColorOverride;
		}
		Color backgroundColor = settings.GetBackgroundColor();
		Color borderColor = settings.GetBorderColor();
		bool showText = stripType == StripType.Health ? settings.ShowHealthText : settings.ShowInfectionText;
		string formattedText = labelOnly
			? label
			: renderAsPercent
			? $"{label} {Mathf.RoundToInt(normalizedValue * 100f)}%"
			: $"{label} {Mathf.RoundToInt(currentValue)}/{Mathf.RoundToInt(maxValue)}";

		bool styleChanged = backgroundColor != lastAppliedBackgroundColor
			|| fillColor != lastAppliedFillColor
			|| borderColor != lastAppliedBorderColor;
		bool fillChanged = !Mathf.Approximately(normalizedValue, lastAppliedNormalized);
		bool textVisibilityChanged = showText != lastAppliedTextEnabled;
		bool textChanged = formattedText != lastAppliedText;
		bool visibleChanged = visible != lastAppliedVisible;

		if (!dirty && !styleChanged && !fillChanged && !textVisibilityChanged && !textChanged && !visibleChanged)
		{
			return;
		}

		if (styleChanged)
		{
			backgroundImage.color = backgroundColor;
			border.effectColor = borderColor;
			border.effectDistance = new Vector2(1f, -1f);
			fillImage.color = fillColor;
			lastAppliedBackgroundColor = backgroundColor;
			lastAppliedFillColor = fillColor;
			lastAppliedBorderColor = borderColor;
		}

		if (dirty || fillChanged)
		{
			fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Lerp(0f, Width - 2f, normalizedValue));
			lastAppliedNormalized = normalizedValue;
		}

		if (dirty || textVisibilityChanged)
		{
			text.enabled = showText;
			lastAppliedTextEnabled = showText;
		}

		if (showText && (dirty || textChanged))
		{
			text.text = formattedText;
			lastAppliedText = formattedText;
		}

		lastAppliedVisible = visible;
		lastAppliedSettingsRevision = settings.Revision;
		dirty = false;
	}
}
