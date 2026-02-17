using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public int sceneIndex;
    public bool isMultiplayer;

    public float[] position;  // Player 1 (or Solo)
    public float[] position2; // Player 2

    // Constructor for Singleplayer
    public PlayerData(Transform playerTransform, int currentSceneIndex)
    {
        sceneIndex = currentSceneIndex;
        isMultiplayer = false;

        position = new float[3];
        position[0] = playerTransform.position.x;
        position[1] = playerTransform.position.y;
        position[2] = playerTransform.position.z;
    }

    // Constructor for Multiplayer
    public PlayerData(Transform p1, Transform p2, int currentSceneIndex)
    {
        sceneIndex = currentSceneIndex;
        isMultiplayer = true;

        position = new float[3] { p1.position.x, p1.position.y, p1.position.z };
        position2 = new float[3] { p2.position.x, p2.position.y, p2.position.z };
    }
}