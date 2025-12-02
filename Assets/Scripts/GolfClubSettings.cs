using UnityEngine;

[CreateAssetMenu(fileName = "GolfClub", menuName = "Golf/Club Settings")]
public class GolfClubSettings : ScriptableObject
{
    public string clubName;
    public float impulseMultiplier = 1f;
    public float upwardBias = 0.5f;
}