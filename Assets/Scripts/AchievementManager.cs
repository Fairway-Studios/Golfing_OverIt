using System.Collections.Generic;
using UnityEngine;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance;

    [Header("References")]
    public AchievementPopupUI popupUI;

    public List<AchievementData> allAchievements;

    // --- NEW: Two Separate Buckets ---
    public HashSet<string> unlockedSingleplayerIDs = new HashSet<string>();
    public HashSet<string> unlockedMultiplayerIDs = new HashSet<string>();

    [HideInInspector] public bool isMultiplayerModeActive = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // TEMPORARY: Un-comment these to wipe your saves while testing
        //PlayerPrefs.DeleteKey("UnlockedAchievements_SP");
        //PlayerPrefs.DeleteKey("UnlockedAchievements_MP");

        LoadUnlockedAchievements();
    }

    public void UnlockAchievement(string achievementID)
    {
        // 1. Find the achievement data first so we know what category it is
        AchievementData unlockedData = allAchievements.Find(a => a.achievementID == achievementID);

        if (unlockedData != null)
        {
            if (isMultiplayerModeActive && unlockedData.category == AchievementCategory.Singleplayer) return;
            // If we are in Singleplayer, completely ignore Multiplayer achievements
            if (!isMultiplayerModeActive && unlockedData.category == AchievementCategory.Multiplayer) return;
            // ------------------------------

            // 2. Determine which bucket this belongs in based on your ScriptableObject setup
            HashSet<string> targetBucket = (unlockedData.category == AchievementCategory.Multiplayer)
                                            ? unlockedMultiplayerIDs
                                            : unlockedSingleplayerIDs;

            // 3. If already unlocked in its specific bucket, ignore it
            if (targetBucket.Contains(achievementID)) return;

            // 4. Mark as unlocked and save
            targetBucket.Add(achievementID);
            SaveUnlockedAchievements();

            // 5. Trigger UI and Sound
            if (popupUI != null)
            {
                popupUI.QueueAchievement(unlockedData);
            }

            Debug.Log($"[{unlockedData.category}] Achievement Triggered: {unlockedData.title}");
        }
        else
        {
            Debug.LogWarning("Achievement ID not found: " + achievementID);
        }
    }

    // --- Save/Load Logic ---
    private void SaveUnlockedAchievements()
    {
        // Save Singleplayer
        string spSaveString = string.Join(",", unlockedSingleplayerIDs);
        PlayerPrefs.SetString("UnlockedAchievements_SP", spSaveString);

        // Save Multiplayer
        string mpSaveString = string.Join(",", unlockedMultiplayerIDs);
        PlayerPrefs.SetString("UnlockedAchievements_MP", mpSaveString);

        PlayerPrefs.Save();
    }

    private void LoadUnlockedAchievements()
    {
        // Load Singleplayer
        string spSavedString = PlayerPrefs.GetString("UnlockedAchievements_SP", "");
        if (!string.IsNullOrEmpty(spSavedString))
        {
            string[] ids = spSavedString.Split(',');
            foreach (string id in ids) unlockedSingleplayerIDs.Add(id);
        }

        // Load Multiplayer
        string mpSavedString = PlayerPrefs.GetString("UnlockedAchievements_MP", "");
        if (!string.IsNullOrEmpty(mpSavedString))
        {
            string[] ids = mpSavedString.Split(',');
            foreach (string id in ids) unlockedMultiplayerIDs.Add(id);
        }
    }

    public void ResetAchievements()
    {
        unlockedSingleplayerIDs.Clear();
        unlockedMultiplayerIDs.Clear();
        PlayerPrefs.DeleteKey("UnlockedAchievements_SP");
        PlayerPrefs.DeleteKey("UnlockedAchievements_MP");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            UnlockAchievement("TEAM_EFFORT"); // Testing your new idea!
        }
    }
}