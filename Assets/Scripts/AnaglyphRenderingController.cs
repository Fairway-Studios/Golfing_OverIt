using UnityEngine;
using UnityEngine.UI;

public class AnaglyphRenderingController : MonoBehaviour
{
    private float hueShift = 0f;
    private float saturationShift = 0.5f;
    private float valueShift = 1f;
    private float opacityMultiplier = 1f;

    private SpriteRenderer[] allRenderers;
    private Color[] originalColors;

    void Start()
    {
        allRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[allRenderers.Length];

        for (int i = 0; i < allRenderers.Length; i++)
        {
            originalColors[i] = allRenderers[i].color;
        }

        ApplyHSVAdjustment();
    }

    public void ApplyHSVAdjustment()
    {

        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
            {
                Color original = originalColors[i];
                Color adjusted = AdjustColorHSV(original, hueShift, saturationShift, valueShift);
                adjusted.a = original.a * opacityMultiplier;

                allRenderers[i].color = adjusted;
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

    public void ResetColors()
    {
        hueShift = 0f;
        saturationShift = 0f;
        valueShift = 0f;
        opacityMultiplier = 1f;
        ApplyHSVAdjustment();
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