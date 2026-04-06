using UnityEngine;

public enum AchievementCategory 
{
    Singleplayer,
    Multiplayer,
    Hidden
}

[CreateAssetMenu(fileName = "AchievementData", menuName = "Scriptable Objects/AchievementData")]
public class AchievementData : ScriptableObject
{
    public string achievementID; // e.g., "FIRST_SWING"
    public string title;         // e.g., "Fore!"
    [TextArea(3, 5)]
    public string description;   // e.g., "Take your very first swing."
    public Sprite icon;
    public AudioClip unlockSound; // Specific sound for this achievement (optional) 

    [Header("Gallery Settings")]
    [Tooltip("Which tab should this appear under in the Gallery?")]
    public AchievementCategory category;
}
