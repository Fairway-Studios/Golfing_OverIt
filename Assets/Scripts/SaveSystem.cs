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

    public static void SaveMultiplayer(Transform p1, Transform p2, int sceneIndex)
    {
        PlayerData data = new PlayerData(p1, p2, sceneIndex);
        string json = JsonUtility.ToJson(data);
        string path = Application.persistentDataPath + "/multiplayer.json";

        File.WriteAllText(path, json);
        Debug.Log("Multiplayer Game Saved to: " + path);
    }

    public static PlayerData LoadMultiplayer()
    {
        string path = Application.persistentDataPath + "/multiplayer.json";
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<PlayerData>(json);
        }
        return null;
    }

    public static bool HasMultiplayerSaveFile()
    {
        return File.Exists(Application.persistentDataPath + "/multiplayer.json");
    }

    public static void DeleteMultiplayerSaveFile()
    {
        string path = Application.persistentDataPath + "/multiplayer.json";
        if (File.Exists(path)) File.Delete(path);
    }
}