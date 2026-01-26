using System;

[System.Serializable]
public class PlayerData
{
    public float[] position;
    public int sceneIndex;

    // Constructor: Converts the Game World data into Save Data
    public PlayerData(UnityEngine.Transform playerTransform, int currentSceneIndex)
    {
        sceneIndex = currentSceneIndex;

        position = new float[3];
        position[0] = playerTransform.position.x;
        position[1] = playerTransform.position.y;
        position[2] = playerTransform.position.z;
    }
}