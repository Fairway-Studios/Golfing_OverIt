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

        Debug.Log($"[{gameObject.name}] Found {allRenderers.Length} SpriteRenderers");

        for (int i = 0; i < allRenderers.Length; i++)
        {
            originalColors[i] = allRenderers[i].color;
            Debug.Log($"[{gameObject.name}] Renderer {i}: {allRenderers[i].name}, Original Color: {originalColors[i]}");
        }

        ApplyHSVAdjustment();
    }

    public void ApplyHSVAdjustment()
    {
        Debug.Log($"[{gameObject.name}] Applying HSV - Hue: {hueShift}, Sat: {saturationShift}, Val: {valueShift}, Opacity: {opacityMultiplier}");

        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
            {
                Color original = originalColors[i];
                Color adjusted = AdjustColorHSV(original, hueShift, saturationShift, valueShift);
                adjusted.a = original.a * opacityMultiplier;

                allRenderers[i].color = adjusted;

                Debug.Log($"[{gameObject.name}] Renderer {i} adjusted to: {adjusted}");
            }
        }
    }


    private Color AdjustColorHSV(Color originalColor, float hueShift, float saturationShift, float valueShift)
    {
        Color.RGBToHSV(originalColor, out float h, out float s, out float v);

        Debug.Log($"Original HSV: H={h}, S={s}, V={v}");

        h = Mathf.Repeat(h + (hueShift / 360f), 1f);
        s = Mathf.Clamp01(s + saturationShift);
        v = Mathf.Clamp01(v + valueShift);

        Debug.Log($"Adjusted HSV: H={h}, S={s}, V={v}");

        Color adjustedColor = Color.HSVToRGB(h, s, v);
        return adjustedColor;
    }

    public void ResetColors()
    {
        Debug.Log($"[{gameObject.name}] Resetting colors");
        hueShift = 0f;
        saturationShift = 0f;
        valueShift = 0f;
        opacityMultiplier = 1f;
        ApplyHSVAdjustment();
    }

    public void SetHue(float hue)
    {
        Debug.Log($"[{gameObject.name}] SetHue called with: {hue}");
        hueShift = hue;
        ApplyHSVAdjustment();
    }

    public void SetSaturation(float sat)
    {
        Debug.Log($"[{gameObject.name}] SetSaturation called with: {sat}");
        saturationShift = sat;
        ApplyHSVAdjustment();
    }

    public void SetValue(float val)
    {
        Debug.Log($"[{gameObject.name}] SetValue called with: {val}");
        valueShift = val;
        ApplyHSVAdjustment();
    }

    public void SetOpacity(float opacity)
    {
        Debug.Log($"[{gameObject.name}] SetOpacity called with: {opacity}");
        opacityMultiplier = Mathf.Clamp01(opacity);
        ApplyHSVAdjustment();
    }
}