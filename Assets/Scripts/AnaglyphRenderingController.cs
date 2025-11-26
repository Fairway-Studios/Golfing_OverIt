using UnityEngine;

public class PlayerColorController : MonoBehaviour
{
    [Header("HSV Adjustment")]
    [Range(0f, 360f)]
    public float hueShift = 0f;

    [Range(0f, 1f)]
    public float saturationShift = 0f;

    [Range(0f, 1f)]
    public float valueShift = 0f;

    [Range(0f, 1f)]
    public float opacity = 1f;

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
    }

    void Update()
    {
        ApplyHSVAdjustment();
    }

    public void ApplyHSVAdjustment()
    {
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] != null)
            {
                Color adjustedColor = AdjustColorHSV(originalColors[i], hueShift, saturationShift, valueShift);
                adjustedColor.a = opacity;
                allRenderers[i].color = adjustedColor;
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
        opacity = 1f;
        ApplyHSVAdjustment();
    }

    public void SetOpacity(float newOpacity)
    {
        opacity = Mathf.Clamp01(newOpacity);
        ApplyHSVAdjustment();
    }
}