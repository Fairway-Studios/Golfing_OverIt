using UnityEngine;

[CreateAssetMenu(fileName = "AchievementData", menuName = "Scriptable Objects/AchievementData")]
public class AchievementData : ScriptableObject
{
    public string achievementID; // e.g., "FIRST_SWING"
    public string title;         // e.g., "Fore!"
    public string description;   // e.g., "Take your very first swing."
    public Sprite icon;
    public AudioClip unlockSound; // Specific sound for this achievement (optional)
}
