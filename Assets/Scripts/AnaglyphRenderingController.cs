using UnityEngine;
using TMPro;

public class AnaglyphRenderingController : MonoBehaviour
{
    private float hueShift = 0f;
    private float saturationShift = 0.5f;
    private float valueShift = 1f;
    private float opacityMultiplier = 0.4f;

    // Sprite Renderers
    private SpriteRenderer[] allSpriteRenderers;
    private Color[] originalSpriteColors;

    // TextMeshPro UI
    private TextMeshProUGUI[] allTMProUI;
    private Color[] originalTMProUIColors;

    void Start()
    {
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalSpriteColors = new Color[allSpriteRenderers.Length];
        for (int i = 0; i < allSpriteRenderers.Length; i++)
        {
            originalSpriteColors[i] = allSpriteRenderers[i].color;
        }

        allTMProUI = GetComponentsInChildren<TextMeshProUGUI>();
        originalTMProUIColors = new Color[allTMProUI.Length];
        for (int i = 0; i < allTMProUI.Length; i++)
        {
            originalTMProUIColors[i] = allTMProUI[i].color;
        }

        ApplyHSVAdjustment();
    }

    public void ApplyHSVAdjustment()
    {
        // Apply to SpriteRenderers
        for (int i = 0; i < allSpriteRenderers.Length; i++)
        {
            if (allSpriteRenderers[i] != null)
            {
                Color original = originalSpriteColors[i];
                Color adjusted = AdjustColorHSV(original, hueShift, saturationShift, valueShift);
                adjusted.a = original.a * opacityMultiplier;
                allSpriteRenderers[i].color = adjusted;
            }
        }

        // Apply to TextMeshProUGUI
        for (int i = 0; i < allTMProUI.Length; i++)
        {
            if (allTMProUI[i] != null)
            {
                Color original = originalTMProUIColors[i];
                Color adjusted = AdjustColorHSV(original, hueShift, saturationShift, valueShift);
                adjusted.a = original.a * opacityMultiplier;
                allTMProUI[i].color = adjusted;
            }
        }
    }

    private Color AdjustColorHSV(Color originalColor, float hueShift, float saturationShift, float valueShift)
    {
        Color.RGBToHSV(originalColor, out float h, out float s, out float v);
        h = Mathf.Repeat(h + (hueShift / 360f), 1f);
        s = Mathf.Clamp01(s + saturationShift);
        v = Mathf.Clamp01(v + valueShift);
        Color adjustedColor = Color.HSVToRGB(h, s, v);
        return adjustedColor;
    }

    public void ApplySinglePlayerColorOverride()
    {
        if (allSpriteRenderers == null)
            allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        if (allSpriteRenderers != null)
        {
            Color overrideColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            foreach (var sr in allSpriteRenderers)
            {
                if (sr != null)
                    sr.color = overrideColor;
            }
        }
    }


    public void SetHue(float hue)
    {
        hueShift = hue;
        ApplyHSVAdjustment();
    }

    public void SetSaturation(float sat)
    {
        saturationShift = sat;
        ApplyHSVAdjustment();
    }

    public void SetValue(float val)
    {
        valueShift = val;
        ApplyHSVAdjustment();
    }

    public void SetOpacity(float opacity)
    {
        opacityMultiplier = Mathf.Clamp01(opacity);
        ApplyHSVAdjustment();
    }
}