using UnityEngine;
using System.IO;

public static class SaveSystem
{
    // Saves the file to: C:/Users/[User]/AppData/LocalLow/FairwayStudios/GolfingOverIt/player.json
    public static void SavePlayer(Transform player, int sceneIndex)
    {
        PlayerData data = new PlayerData(player, sceneIndex);
        string json = JsonUtility.ToJson(data);
        string path = Application.persistentDataPath + "/player.json";

        File.WriteAllText(path, json);
        Debug.Log("Game Saved to: " + path);
    }

    public static PlayerData LoadPlayer()
    {
        string path = Application.persistentDataPath + "/player.json";
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            PlayerData data = JsonUtility.FromJson<PlayerData>(json);
            return data;
        }
        else
        {
            Debug.LogError("Save file not found in " + path);
            return null;
        }
    }

    public static bool HasSaveFile()
    {
        string path = Application.persistentDataPath + "/player.json";
        return File.Exists(path);
    }

    public static void DeleteSaveFile()
    {
        string path = Application.persistentDataPath + "/player.json";
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Save file deleted.");
        }
    }
}