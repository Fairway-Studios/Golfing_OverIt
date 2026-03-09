using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Anaglyph/Color Settings", fileName = "AnaglyphColorSettings")]
public class AnaglyphColorSettings : ScriptableObject
{

    [Header("Defaults (overwritten by saved values at runtime)")]
    [Range(0f, 360f)] public float HueShift = 0f;
    [Range(0f, 1f)] public float SaturationShift = 0.5f;
    [Range(0f, 1f)] public float ValueShift = 1f;
    [Range(0f, 1f)] public float OpacityMultiplier = 0.4f;

    private event Action OnSettingsChanged;
    public void SetHue(float value) { HueShift = value; NotifyChanged(); }
    public void SetSaturation(float value) { SaturationShift = value; NotifyChanged(); }
    public void SetValue(float value) { ValueShift = value; NotifyChanged(); }
    public void SetOpacity(float value) { OpacityMultiplier = Mathf.Clamp01(value); NotifyChanged(); }

    private string Key(string property) => $"Anaglyph_{name}_{property}";

    void OnEnable()
    {
        LoadFromPrefs();
    }

    public void Subscribe(Action listener)
    {
        OnSettingsChanged -= listener;
        OnSettingsChanged += listener;
    }

    public void Unsubscribe(Action listener)
    {
        OnSettingsChanged -= listener;
    }

    public void NotifyChanged()
    {
        SaveToPrefs();
        FireSafely();
    }

    public void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(Key("HueShift"));
        PlayerPrefs.DeleteKey(Key("SaturationShift"));
        PlayerPrefs.DeleteKey(Key("ValueShift"));
        PlayerPrefs.DeleteKey(Key("OpacityMultiplier"));
        PlayerPrefs.Save();

        LoadFromPrefs();
        FireSafely();
    }

    private void FireSafely()
    {
        if (OnSettingsChanged == null)
            return;

        foreach (Action listener in OnSettingsChanged.GetInvocationList())
        {
            try
            {
                if (listener.Target is UnityEngine.Object unityTarget && unityTarget == null)
                {
                    OnSettingsChanged -= listener;
                    continue;
                }

                listener.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AnaglyphColorSettings] Listener threw an exception and has been removed: {e.Message}");
                OnSettingsChanged -= listener;
            }
        }
    }

    private void SaveToPrefs()
    {
        PlayerPrefs.SetFloat(Key("HueShift"), HueShift);
        PlayerPrefs.SetFloat(Key("SaturationShift"), SaturationShift);
        PlayerPrefs.SetFloat(Key("ValueShift"), ValueShift);
        PlayerPrefs.SetFloat(Key("OpacityMultiplier"), OpacityMultiplier);
        PlayerPrefs.Save();
    }

    private void LoadFromPrefs()
    {
        HueShift = PlayerPrefs.GetFloat(Key("HueShift"), HueShift);
        SaturationShift = PlayerPrefs.GetFloat(Key("SaturationShift"), SaturationShift);
        ValueShift = PlayerPrefs.GetFloat(Key("ValueShift"), ValueShift);
        OpacityMultiplier = PlayerPrefs.GetFloat(Key("OpacityMultiplier"), OpacityMultiplier);
    }
}