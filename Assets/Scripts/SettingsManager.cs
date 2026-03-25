using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    [Header("Input References")]
    [SerializeField] private InputController player1Input;
    [SerializeField] private InputController player2Input;

    [Header("Input UI")]
    [SerializeField] private Slider sensitivitySliderP1;
    [SerializeField] private Slider sensitivitySliderP2;
    [SerializeField] private Toggle invertSticksToggleP1;
    [SerializeField] private Toggle invertSticksToggleP2;

    [Header("Anaglyph References")]
    [SerializeField] private AnaglyphColorSettings blueSettings;
    [SerializeField] private AnaglyphColorSettings redSettings;

    [Header("Anaglyph UI")]
    [SerializeField] private Slider blueHueSlider;
    [SerializeField] private Slider blueSatSlider;
    [SerializeField] private Slider blueOpacitySlider;
    [SerializeField] private Slider redHueSlider;
    [SerializeField] private Slider redSatSlider;
    [SerializeField] private Slider redOpacitySlider;

    [Header("Audio UI")]
    [SerializeField] private Slider masterVolumeSlider;

    public void SetBlueHue(float value) => blueSettings.SetHue(value);
    public void SetBlueSaturation(float value) => blueSettings.SetSaturation(value);
    public void SetBlueOpacity(float value) => blueSettings.SetOpacity(value);

    public void SetRedHue(float value) => redSettings.SetHue(value);
    public void SetRedSaturation(float value) => redSettings.SetSaturation(value);
    public void SetRedOpacity(float value) => redSettings.SetOpacity(value);

    public void SetMasterVolume(float value)
    {
        if (SFXManager.Instance != null)
            SFXManager.Instance.SetMasterVolume(value);
    }

    void OnEnable()
    {
        PopulateUI();
    }

    private void PopulateUI()
    {

        sensitivitySliderP1.SetValueWithoutNotify(PlayerPrefs.GetFloat("Input_P0_Sensitivity", 1f));
        sensitivitySliderP2.SetValueWithoutNotify(PlayerPrefs.GetFloat("Input_P1_Sensitivity", 1f));

        invertSticksToggleP1.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Input_P0_InvertSticks", 0) == 1);
        invertSticksToggleP2.SetIsOnWithoutNotify(PlayerPrefs.GetInt("Input_P1_InvertSticks", 0) == 1);

        blueHueSlider.SetValueWithoutNotify(blueSettings.HueShift);
        blueSatSlider.SetValueWithoutNotify(blueSettings.SaturationShift);
        blueOpacitySlider.SetValueWithoutNotify(blueSettings.OpacityMultiplier);

        redHueSlider.SetValueWithoutNotify(redSettings.HueShift);
        redSatSlider.SetValueWithoutNotify(redSettings.SaturationShift);
        redOpacitySlider.SetValueWithoutNotify(redSettings.OpacityMultiplier);

        masterVolumeSlider.onValueChanged.RemoveAllListeners();

        float savedVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        masterVolumeSlider.SetValueWithoutNotify(savedVolume);

        masterVolumeSlider.onValueChanged.AddListener(delegate {
            if (SFXManager.Instance != null)
            {
                SFXManager.Instance.SetMasterVolume(masterVolumeSlider.value);
            }
        });
    }
}