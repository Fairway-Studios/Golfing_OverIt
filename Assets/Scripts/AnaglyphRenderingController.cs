using UnityEngine;
using TMPro;

public class AnaglyphRenderingController : MonoBehaviour
{
    [SerializeField] private AnaglyphColorSettings colorSettings;

    private SpriteRenderer[] allSpriteRenderers;
    private Color[] originalSpriteColors;

    private TextMeshProUGUI[] allTMProUI;
    private Color[] originalTMProUIColors;

    void Awake()
    {
        CacheRenderers();
    }

    void OnEnable()
    {
        colorSettings?.Subscribe(ApplyHSVAdjustment);
    }

    void OnDisable()
    {
        colorSettings?.Unsubscribe(ApplyHSVAdjustment);
    }

    void Start()
    {
        ApplyHSVAdjustment();
    }

    public void ApplySinglePlayerColorOverride()
    {
        if (allSpriteRenderers == null)
            CacheRenderers();

        Color overrideColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        foreach (var sr in allSpriteRenderers)
        {
            if (sr != null)
                sr.color = overrideColor;
        }
    }

    private void CacheRenderers()
    {
        allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalSpriteColors = new Color[allSpriteRenderers.Length];
        for (int i = 0; i < allSpriteRenderers.Length; i++)
            originalSpriteColors[i] = allSpriteRenderers[i].color;

        allTMProUI = GetComponentsInChildren<TextMeshProUGUI>();
        originalTMProUIColors = new Color[allTMProUI.Length];
        for (int i = 0; i < allTMProUI.Length; i++)
            originalTMProUIColors[i] = allTMProUI[i].color;
    }

    public void ApplyHSVAdjustment()
    {
        if (colorSettings == null)
        {
            return;
        }

        for (int i = 0; i < allSpriteRenderers.Length; i++)
        {
            if (allSpriteRenderers[i] == null) continue;

            Color adjusted = AdjustColorHSV(originalSpriteColors[i]);
            adjusted.a = originalSpriteColors[i].a * colorSettings.OpacityMultiplier;
            allSpriteRenderers[i].color = adjusted;
        }

        for (int i = 0; i < allTMProUI.Length; i++)
        {
            if (allTMProUI[i] == null) continue;

            Color adjusted = AdjustColorHSV(originalTMProUIColors[i]);
            adjusted.a = originalTMProUIColors[i].a * colorSettings.OpacityMultiplier;
            allTMProUI[i].color = adjusted;
        }
    }

    private Color AdjustColorHSV(Color original)
    {
        Color.RGBToHSV(original, out float h, out float s, out float v);
        h = Mathf.Repeat(h + (colorSettings.HueShift / 360f), 1f);
        s = Mathf.Clamp01(s + colorSettings.SaturationShift);
        v = Mathf.Clamp01(v + colorSettings.ValueShift);
        return Color.HSVToRGB(h, s, v);
    }
}