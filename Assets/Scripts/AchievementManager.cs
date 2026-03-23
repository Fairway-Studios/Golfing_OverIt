using System.Collections.Generic;
using UnityEngine;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    [Header("References")]
    [SerializeField] private AchievementPopupUI popupUI;
    [SerializeField] private List<AchievementData> allAchievements;

    // Track unlocked IDs to prevent duplicates
    private HashSet<string> unlockedIDs = new HashSet<string>();

    private void Awake()
    {
        // Simple Singleton pattern
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }

        PlayerPrefs.DeleteKey("UnlockedAchievements");

        LoadUnlockedAchievements();
    }

    public void UnlockAchievement(string achievementID)
    {
        // 1. Check if already unlocked (BYPASS this if it is our test achievement!)
        if (achievementID != "TEST_SWING" && unlockedIDs.Contains(achievementID)) return;

        // 2. Find the achievement data
        AchievementData unlockedData = allAchievements.Find(a => a.achievementID == achievementID);

        if (unlockedData != null)
        {
            // 3. Mark as unlocked and save ONLY if it's a real achievement
            if (achievementID != "TEST_SWING")
            {
                unlockedIDs.Add(achievementID);
                SaveUnlockedAchievements();
            }

            // 4. Trigger UI and Sound
            if (popupUI != null)
            {
                popupUI.QueueAchievement(unlockedData);
            }

            Debug.Log("Achievement Triggered: " + unlockedData.title);
        }
        else
        {
            Debug.LogWarning("Achievement ID not found: " + achievementID);
        }
    }

    // --- Save/Load Logic ---
    private void SaveUnlockedAchievements()
    {
        // Convert HashSet to a comma-separated string for easy PlayerPrefs saving
        string saveString = string.Join(",", unlockedIDs);
        PlayerPrefs.SetString("UnlockedAchievements", saveString);
        PlayerPrefs.Save();
    }

    private void LoadUnlockedAchievements()
    {
        string savedString = PlayerPrefs.GetString("UnlockedAchievements", "");
        if (!string.IsNullOrEmpty(savedString))
        {
            string[] ids = savedString.Split(',');
            foreach (string id in ids)
            {
                unlockedIDs.Add(id);
            }
        }
    }

    // Call this if a player starts a "New Game"
    public void ResetAchievements()
    {
        unlockedIDs.Clear();
        PlayerPrefs.DeleteKey("UnlockedAchievements");
    }

    private void Update()
    {
        // TEMPORARY: Press 'T' to trigger a test achievement
        if (Input.GetKeyDown(KeyCode.T))
        {
            UnlockAchievement("TEST_SWING");
        }
    }
}