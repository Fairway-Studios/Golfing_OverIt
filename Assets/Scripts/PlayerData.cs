using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public float[] playerPosition;
    public float[] ballPosition;
    public int sceneIndex;
    public int strokes;
    public float time;

    // Constructor saves both positions exactly as they are
    public PlayerData(Transform playerTransform, Transform ballTransform, int currentSceneIndex, int currentStrokes, float currentTime)
    {
        sceneIndex = currentSceneIndex;
        strokes = currentStrokes;
        time = currentTime;

        playerPosition = new float[3] { playerTransform.position.x, playerTransform.position.y, playerTransform.position.z };
        ballPosition = new float[3] { ballTransform.position.x, ballTransform.position.y, ballTransform.position.z };
    }
}