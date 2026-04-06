using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AchievementCardUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text titleText;
    public TMP_Text descriptionText;

    [Tooltip("Used to fade out the entire card if locked")]
    public CanvasGroup canvasGroup;

    public void Setup(AchievementData data, bool isUnlocked)
    {
        // 1. Populate the data
        titleText.text = isUnlocked ? data.title : "???";
        descriptionText.text = isUnlocked ? data.description : "Keep playing to discover this achievement.";

        if (data.icon != null)
        {
            iconImage.sprite = data.icon;
        }

        // 2. Handle the Locked vs. Unlocked visuals
        if (isUnlocked)
        {
            // Fully visible, bright colors
            canvasGroup.alpha = 1f;
            iconImage.color = Color.white;
        }
        else
        {
            // Faded out, silhouette icon
            canvasGroup.alpha = 0.6f;
            iconImage.color = Color.black;
        }
    }
}