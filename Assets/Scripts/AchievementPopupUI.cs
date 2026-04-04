using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AchievementPopupUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip defaultUnlockSound;

    [Header("Animation Settings")]
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float slideSpeed = 500f;

    private RectTransform panelRect;
    private Vector2 hiddenPosition;
    private Vector2 visiblePosition;
    private Queue<AchievementData> unlockQueue = new Queue<AchievementData>();
    private bool isDisplaying = false;

    void Start()
    {
        panelRect = popupPanel.GetComponent<RectTransform>();

        // Assuming anchor is top-center. 
        visiblePosition = new Vector2(0, -50); // 50 pixels down from top
        hiddenPosition = new Vector2(0, panelRect.rect.height + 50); // Hidden above screen

        panelRect.anchoredPosition = hiddenPosition;
    }

    public void QueueAchievement(AchievementData data)
    {
        unlockQueue.Enqueue(data);
        if (!isDisplaying)
        {
            StartCoroutine(DisplayNextAchievement());
        }
    }

    private IEnumerator DisplayNextAchievement()
    {
        // 1. Lock the gate so we don't trigger multiple slides at once
        isDisplaying = true;

        while (unlockQueue.Count > 0)
        {
            AchievementData nextData = unlockQueue.Dequeue();

            // 2. Set UI Data
            titleText.text = nextData.title;
            descriptionText.text = nextData.description;
            if (nextData.icon != null) iconImage.sprite = nextData.icon;

            // 3. Play Sound
            AudioClip clipToPlay = nextData.unlockSound != null ? nextData.unlockSound : defaultUnlockSound;
            if (clipToPlay != null && audioSource != null)
            {
                audioSource.PlayOneShot(clipToPlay, 0.25f);
            }

            // 4. Slide Down (Using Distance to prevent math stalls)
            while (Vector2.Distance(panelRect.anchoredPosition, visiblePosition) > 0.5f)
            {
                panelRect.anchoredPosition = Vector2.MoveTowards(panelRect.anchoredPosition, visiblePosition, slideSpeed * Time.deltaTime);
                yield return null;
            }
            panelRect.anchoredPosition = visiblePosition; // Snap exactly to target

            // 5. Wait
            yield return new WaitForSeconds(displayDuration);

            // 6. Slide Up (Using Distance)
            while (Vector2.Distance(panelRect.anchoredPosition, hiddenPosition) > 0.5f)
            {
                panelRect.anchoredPosition = Vector2.MoveTowards(panelRect.anchoredPosition, hiddenPosition, slideSpeed * Time.deltaTime);
                yield return null;
            }
            panelRect.anchoredPosition = hiddenPosition; // Snap exactly back to ceiling
        }

        // 7. Unlock the gate for the next achievement!
        isDisplaying = false;
    }
}