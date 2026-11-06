using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OtherPlayerStatusBars;

/// <summary>
/// Ten-cell bar with its label in the first cell and its value in the last.
/// Coloured cells look sunken and empty cells raised. All bars share the same three textures.
/// </summary>
/// <remarks>
/// Text uses TMP's own outline on the full Distance Field shader. The mobile shader most game fonts use
/// needs a keyword for outlines, and its outline did not show up in game.
/// </remarks>
internal sealed class SegmentedBarStrip : MonoBehaviour
{
	public const float Width = 172f;

	public const float Height = 13f;

	private const int TextureScale = 12;

	private const int FrameTexels = 5;

	private const int BevelTexels = 10;

	private const float FrameThickness = (float)FrameTexels / TextureScale;

	private const int CellCount = 10;

	private const float EndCellWidth = 1f / CellCount;

	private const float FontSize = 8f;

	private const string TextShaderName = "TextMeshPro/Distance Field";

	private const float TextOutlineWidth = 0.3f;

	private const float TextOutlineSoftness = 0.12f;

	private const float LightenBlend = 0.55f;

	private static readonly Color TrackColor = new(1f, 1f, 1f, 0.035f);

	private static readonly Color HealthTrailColor = new(0.85f, 0.83f, 0.77f, 1f);

	private static readonly Color FrameColor = new(0.34f, 0.36f, 0.4f, 1f);

	// Measured in game: UI blending and post-processing make light edges far stronger and dark edges
	// weaker than these alphas suggest, so they are set to look balanced on screen.
	private static readonly Color AnimatedBevelTopLeft = new(0f, 0f, 0f, 0.6f);

	private static readonly Color AnimatedBevelBottomRight = new(1f, 1f, 1f, 0.06f);

	private static readonly Color EmptyBevelTopLeft = new(1f, 1f, 1f, 0.07f);

	private static readonly Color EmptyBevelBottomRight = new(0f, 0f, 0f, 0.53f);


	private static readonly Color TextOutlineColor = new(0.03f, 0.04f, 0.05f, 1f);

	private static Texture2D? frameTexture;

	private static readonly Dictionary<Color, (Texture2D Bevel, Texture2D EndEdge)> filledBevelTextures = new();

	private static Texture2D? emptyLightEdgesTexture;

	private static Texture2D? emptyDarkEdgesTexture;

	private static Texture2D? animatedDarkEdgesTexture;

	private static Texture2D? animatedLightEdgesTexture;

	private static Material? outlinedTextMaterial;

	private static Material? outlinedTextBaseMaterial;

	private bool usesInfectionColor;

	private RectTransform fillRect = null!;

	private RectTransform trailRect = null!;

	private RectTransform previewRect = null!;

	private Image fillImage = null!;

	private Image trailImage = null!;

	private Image previewImage = null!;

	private RawImage filledBevel = null!;

	private RawImage animatedDarkEdges = null!;

	private RawImage animatedLightEdges = null!;

	private RawImage fillEndEdge = null!;

	private TextMeshProUGUI labelText = null!;

	private TextMeshProUGUI valueText = null!;

	private float fill = -1f;

	private float trail = -1f;

	private float preview = -1f;

	private string value = string.Empty;

	private bool hasFillColorOverride;

	private Color fillColorOverride;

	private bool dirty = true;

	private int lastAppliedSettingsRevision = -1;

	public void Initialize(string label, bool isInfection)
	{
		usesInfectionColor = isInfection;
		RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
		rectTransform.sizeDelta = new Vector2(Width, Height);
		rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);

		CreateImage("Track", transform).color = TrackColor;

		// Cells and everything that fills them sit inside the frame.
		RectTransform inner = CreateInnerArea("Inner");

		emptyLightEdgesTexture ??= CreateEdgeTexture(EmptyBevelTopLeft, topLeft: true);
		emptyDarkEdgesTexture ??= CreateEdgeTexture(EmptyBevelBottomRight, topLeft: false);
		CreateRawImage("EmptyBevelLight", emptyLightEdgesTexture, inner);
		CreateRawImage("EmptyBevelDark", emptyDarkEdgesTexture, inner);
		trailImage = CreateImage("Trail", inner);
		trailRect = trailImage.rectTransform;
		previewImage = CreateImage("Preview", inner);
		previewRect = previewImage.rectTransform;
		animatedDarkEdgesTexture ??= CreateEdgeTexture(AnimatedBevelTopLeft, topLeft: true);
		animatedLightEdgesTexture ??= CreateEdgeTexture(AnimatedBevelBottomRight, topLeft: false);
		animatedDarkEdges = CreateRawImage("AnimatedBevelDark", animatedDarkEdgesTexture, inner);
		animatedDarkEdges.rectTransform.pivot = new Vector2(0f, 0.5f);
		animatedLightEdges = CreateRawImage("AnimatedBevelLight", animatedLightEdgesTexture, inner);
		animatedLightEdges.rectTransform.pivot = new Vector2(0f, 0.5f);
		fillImage = CreateImage("Fill", inner);
		fillRect = fillImage.rectTransform;

		// Both get their textures in ApplyIfDirty, once the fill colour is known.
		filledBevel = CreateRawImage("FilledBevel", emptyDarkEdgesTexture, inner);
		filledBevel.rectTransform.pivot = new Vector2(0f, 0.5f);
		fillEndEdge = CreateRawImage("FillEndEdge", emptyDarkEdgesTexture, inner);
		fillEndEdge.rectTransform.pivot = new Vector2(1f, 0.5f);
		CreateRawImage("Frame", GetFrameTexture(), transform);

		// Laid out on the cells, not the whole strip, or the frame pushes the text off their centres.
		RectTransform textArea = CreateInnerArea("TextArea");
		labelText = CreateEndCellText("Label", textArea, 0f);
		labelText.text = label;
		valueText = CreateEndCellText("Value", textArea, 1f - EndCellWidth);
	}

	public void SetValues(float fillValue, float trailValue, float previewValue, string valueLabel)
	{
		if (Mathf.Approximately(fill, fillValue)
			&& Mathf.Approximately(trail, trailValue)
			&& Mathf.Approximately(preview, previewValue)
			&& ReferenceEquals(value, valueLabel))
		{
			return;
		}

		fill = fillValue;
		trail = trailValue;
		preview = previewValue;
		value = valueLabel;
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

	public void SetVisible(bool isVisible)
	{
		if (gameObject.activeSelf != isVisible)
		{
			gameObject.SetActive(isVisible);
		}
	}

	public void ApplyIfDirty(StatusBarConfig settings)
	{
		if (!gameObject.activeSelf || (!dirty && settings.Revision == lastAppliedSettingsRevision))
		{
			return;
		}

		Color fillColor = hasFillColorOverride
			? fillColorOverride
			: usesInfectionColor ? settings.GetSegmentedInfectionFillColor() : settings.GetSegmentedHealthFillColor();
		Color lightColor = Color.Lerp(fillColor, Color.white, LightenBlend);
		fillImage.color = fillColor;
		(Texture2D bevel, Texture2D endEdge) = GetFilledBevelTextures(fillColor);
		filledBevel.texture = bevel;
		fillEndEdge.texture = endEdge;
		previewImage.color = lightColor;
		trailImage.color = usesInfectionColor ? lightColor : HealthTrailColor;
		SetWidth(fillRect, fill);
		SetWidth(trailRect, trail);
		SetWidth(previewRect, preview);

		// Bevels are cropped so they stay on the cell boundaries. The fill's is opaque, so it must stop
		// at the fill; the trail and preview beyond it get a translucent one that lets them show.
		float animated = Mathf.Max(fill, Mathf.Max(trail, preview));
		Crop(animatedDarkEdges, animated);
		Crop(animatedLightEdges, animated);
		Crop(filledBevel, fill);
		PlaceFillEndEdge(Mathf.Clamp01(fill / 100f));

		bool showText = settings.ShowText;
		labelText.enabled = showText;
		valueText.enabled = showText;
		if (showText)
		{
			KeepOutline(labelText);
			KeepOutline(valueText);
			valueText.text = value;
		}

		lastAppliedSettingsRevision = settings.Revision;
		dirty = false;
	}

	private static void SetWidth(RectTransform rect, float percent)
	{
		rect.localScale = new Vector3(Mathf.Clamp01(percent / 100f), 1f, 1f);
	}

	private static void Crop(RawImage image, float percent)
	{
		SetWidth(image.rectTransform, percent);
		image.uvRect = new Rect(0f, 0f, Mathf.Clamp01(percent / 100f), 1f);
	}

	/// <summary>The bevel texture only has edges on cell boundaries, so the fill's end carries its own.</summary>
	private void PlaceFillEndEdge(float fillFraction)
	{
		fillEndEdge.enabled = fillFraction > 0f;
		RectTransform rect = fillEndEdge.rectTransform;
		rect.anchorMin = new Vector2(fillFraction, 0f);
		rect.anchorMax = new Vector2(fillFraction, 1f);
		rect.sizeDelta = new Vector2((float)BevelTexels / TextureScale, 0f);
		rect.anchoredPosition = Vector2.zero;
	}

	/// <summary>Font mods can swap a text's font and material after it is created.</summary>
	private static void KeepOutline(TextMeshProUGUI text)
	{
		if (text.fontSharedMaterial != outlinedTextMaterial)
		{
			text.fontSharedMaterial = GetOutlinedTextMaterial(text.fontSharedMaterial);
		}
	}

	private static Material GetOutlinedTextMaterial(Material baseMaterial)
	{
		if (outlinedTextMaterial != null && outlinedTextBaseMaterial == baseMaterial)
		{
			return outlinedTextMaterial;
		}

		ShaderUtilities.GetShaderPropertyIDs();
		outlinedTextBaseMaterial = baseMaterial;
		outlinedTextMaterial = new Material(baseMaterial) { hideFlags = HideFlags.HideAndDontSave };
		Shader fullShader = Shader.Find(TextShaderName);
		if (fullShader != null)
		{
			outlinedTextMaterial.shader = fullShader;
		}
		else
		{
			Plugin.Log.LogWarning($"Shader '{TextShaderName}' not found; bar text may have no outline.");
		}

		outlinedTextMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
		outlinedTextMaterial.SetColor(ShaderUtilities.ID_OutlineColor, TextOutlineColor);
		outlinedTextMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, TextOutlineWidth);
		outlinedTextMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, TextOutlineSoftness);
		return outlinedTextMaterial;
	}

	private static RawImage CreateRawImage(string objectName, Texture2D texture, Transform parent)
	{
		GameObject imageObject = new(objectName, typeof(RectTransform), typeof(RawImage));
		imageObject.transform.SetParent(parent, worldPositionStays: false);
		RawImage image = imageObject.GetComponent<RawImage>();
		image.texture = texture;
		image.raycastTarget = false;
		Stretch(image.rectTransform);
		return image;
	}

	private static Image CreateImage(string objectName, Transform parent)
	{
		GameObject imageObject = new(objectName, typeof(RectTransform), typeof(Image));
		imageObject.transform.SetParent(parent, worldPositionStays: false);
		Image image = imageObject.GetComponent<Image>();
		image.raycastTarget = false;
		Stretch(image.rectTransform);
		image.rectTransform.pivot = new Vector2(0f, 0.5f);
		return image;
	}

	private RectTransform CreateInnerArea(string objectName)
	{
		GameObject areaObject = new(objectName, typeof(RectTransform));
		areaObject.transform.SetParent(transform, worldPositionStays: false);
		RectTransform area = (RectTransform)areaObject.transform;
		Stretch(area);
		area.offsetMin = new Vector2(FrameThickness, FrameThickness);
		area.offsetMax = new Vector2(-FrameThickness, -FrameThickness);
		return area;
	}

	private TextMeshProUGUI CreateEndCellText(string objectName, Transform parent, float cellStart)
	{
		GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
		textObject.transform.SetParent(parent, worldPositionStays: false);
		TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
		RectTransform rect = text.rectTransform;
		rect.anchorMin = new Vector2(cellStart, 0f);
		rect.anchorMax = new Vector2(cellStart + EndCellWidth, 1f);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
		// Centres the letters themselves; plain Center goes by font metrics and sits off to one side.
		text.alignment = TextAlignmentOptions.MidlineGeoAligned;
		text.fontSize = FontSize;
		text.enableWordWrapping = false;
		text.overflowMode = TextOverflowModes.Overflow;
		text.color = Color.white;
		text.raycastTarget = false;
		text.fontSharedMaterial = GetOutlinedTextMaterial(text.fontSharedMaterial);
		return text;
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private static Texture2D GetFrameTexture()
	{
		if (frameTexture != null)
		{
			return frameTexture;
		}

		int width = (int)Width * TextureScale;
		int height = (int)Height * TextureScale;
		Color32[] pixels = new Color32[width * height];
		Color32 frameColor = FrameColor;
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				bool isFrame = x < FrameTexels || x >= width - FrameTexels || y < FrameTexels || y >= height - FrameTexels;
				pixels[y * width + x] = isFrame ? frameColor : default;
			}
		}

		frameTexture = CreateTexture(pixels, width, height);
		return frameTexture;
	}

	/// <summary>Sunken edges in the fill's own hue. There are only a few fill colours, so each is kept.</summary>
	private static (Texture2D Bevel, Texture2D EndEdge) GetFilledBevelTextures(Color fillColor)
	{
		if (filledBevelTextures.TryGetValue(fillColor, out (Texture2D Bevel, Texture2D EndEdge) textures))
		{
			return textures;
		}

		(float R, float G, float B) dark = SegmentedPalette.Dark(fillColor.r, fillColor.g, fillColor.b);
		(float R, float G, float B) light = SegmentedPalette.Light(fillColor.r, fillColor.g, fillColor.b);
		(float R, float G, float B) crossing = SegmentedPalette.Crossing(dark.R, dark.G, dark.B);
		(float R, float G, float B) deepDark = SegmentedPalette.Dark(dark.R, dark.G, dark.B);
		(float R, float G, float B) brightLight = SegmentedPalette.Light(light.R, light.G, light.B);
		Color32 fill = new Color(fillColor.r, fillColor.g, fillColor.b, 1f);
		Color32 darkColor = new Color(dark.R, dark.G, dark.B, 1f);
		Color32 lightColor = new Color(light.R, light.G, light.B, 1f);
		Color32 crossingColor = new Color(crossing.R, crossing.G, crossing.B, 1f);
		Color32 deepDarkColor = new Color(deepDark.R, deepDark.G, deepDark.B, 1f);
		Color32 brightLightColor = new Color(brightLight.R, brightLight.G, brightLight.B, 1f);

		int width = (int)Width * TextureScale - FrameTexels * 2;
		int height = (int)Height * TextureScale - FrameTexels * 2;
		Color32[] pixels = new Color32[width * height];
		for (int y = 0; y < height; y++)
		{
			bool isTop = y >= height - BevelTexels;
			bool isBottom = y < BevelTexels;
			for (int x = 0; x < width; x++)
			{
				int cell = SegmentedCellLayout.CellAt(x, width, CellCount);
				bool isLeft = x < SegmentedCellLayout.CellStart(cell, width, CellCount) + BevelTexels;
				bool isRight = x >= SegmentedCellLayout.CellStart(cell + 1, width, CellCount) - BevelTexels;
				// Opaque, with the fill inside: mipmaps of a transparent middle would dirty the edges.
				pixels[y * width + x] = (isTop || isLeft, isBottom || isRight) switch
				{
					(true, true) => crossingColor,
					(true, false) => isTop && isLeft ? deepDarkColor : darkColor,
					(false, true) => isBottom && isRight ? brightLightColor : lightColor,
					_ => fill
				};
			}
		}

		Color32[] endEdgePixels = new Color32[height];
		for (int y = 0; y < height; y++)
		{
			endEdgePixels[y] = y >= height - BevelTexels ? crossingColor : y < BevelTexels ? brightLightColor : lightColor;
		}

		textures = (CreateTexture(pixels, width, height), CreateTexture(endEdgePixels, 1, height));
		filledBevelTextures[fillColor] = textures;
		return textures;
	}

	/// <summary>
	/// One side's edges of every cell, all in one colour with only the alpha varying. Filtering mixes
	/// colour and alpha separately, so light and dark edges in one texture would leave a bright line
	/// wherever they meet. Each side gets its own texture and the two are layered instead.
	/// </summary>
	private static Texture2D CreateEdgeTexture(Color edge, bool topLeft)
	{
		int width = (int)Width * TextureScale - FrameTexels * 2;
		int height = (int)Height * TextureScale - FrameTexels * 2;
		Color32[] pixels = new Color32[width * height];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				int cell = SegmentedCellLayout.CellAt(x, width, CellCount);
				int cellStart = SegmentedCellLayout.CellStart(cell, width, CellCount);
				int cellEnd = SegmentedCellLayout.CellStart(cell + 1, width, CellCount);
				bool onRow = topLeft ? y >= height - BevelTexels : y < BevelTexels;
				bool onColumn = topLeft ? x < cellStart + BevelTexels : x >= cellEnd - BevelTexels;
				float alpha = SegmentedCellLayout.StackedAlpha(edge.a, (onRow ? 1 : 0) + (onColumn ? 1 : 0));
				pixels[y * width + x] = new Color(edge.r, edge.g, edge.b, alpha);
			}
		}

		return CreateTexture(pixels, width, height);
	}

	private static Texture2D CreateTexture(Color32[] pixels, int width, int height)
	{
		Texture2D texture = new(width, height, TextureFormat.RGBA32, mipChain: true)
		{
			hideFlags = HideFlags.HideAndDontSave,
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		texture.SetPixels32(pixels);
		texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		return texture;
	}
}
