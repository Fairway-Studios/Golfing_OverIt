using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GalleryManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject achievementCardPrefab;

    [Header("Scroll View Content Holders")]
    public Transform singleplayerContent;
    public Transform multiplayerContent;

    [Header("Tab Views")]
    public GameObject singleplayerScrollView;
    public GameObject multiplayerScrollView;

    void Start()
    {
        // 1. Ensure AchievementManager exists (it should carry over from the Main Menu)
        if (AchievementManager.Instance == null)
        {
            Debug.LogError("AchievementManager is missing! Make sure you loaded from the Main Menu.");
            return;
        }

        // 2. Populate the Gallery
        PopulateGallery();

        // 3. Open Singleplayer tab by default
        ShowSingleplayerTab();
    }

    void PopulateGallery()
    {
        // Loop through the master dictionary
        foreach (AchievementData data in AchievementManager.Instance.allAchievements)
        {
            // Check if it is unlocked
            bool isUnlocked = AchievementManager.Instance.unlockedIDs.Contains(data.achievementID);

            // Determine which Content transform to parent it to
            Transform targetContent = null;
            if (data.category == AchievementCategory.Singleplayer) targetContent = singleplayerContent;
            else if (data.category == AchievementCategory.Multiplayer) targetContent = multiplayerContent;

            // Spawn the card and set it up if it belongs in a tab
            if (targetContent != null)
            {
                GameObject newCard = Instantiate(achievementCardPrefab, targetContent);
                AchievementCardUI cardUI = newCard.GetComponent<AchievementCardUI>();

                if (cardUI != null)
                {
                    cardUI.Setup(data, isUnlocked);
                }
            }
        }
    }

    // --- Button Methods ---

    public void ShowSingleplayerTab()
    {
        singleplayerScrollView.SetActive(true);
        multiplayerScrollView.SetActive(false);
    }

    public void ShowMultiplayerTab()
    {
        singleplayerScrollView.SetActive(false);
        multiplayerScrollView.SetActive(true);
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("MainMenu"); // Make sure this matches your Main Menu scene name
    }
}