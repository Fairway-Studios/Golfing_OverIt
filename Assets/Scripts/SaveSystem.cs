using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;

public static class SaveSystem
{
    // --- SAVING TO THE CLOUD ---
    public static void SavePlayer(Transform player, Transform ball, int sceneIndex, int strokes, float time)
    {
        PerlinMountain2D pcg = UnityEngine.Object.FindFirstObjectByType<PerlinMountain2D>();

        // Package the data exactly as before
        PlayerData data = new PlayerData(player, ball, sceneIndex, strokes, time, pcg);
        string json = JsonUtility.ToJson(data);

        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { "SaveGame", json }
            }
        };

        PlayFabClientAPI.UpdateUserData(request,
            result => Debug.Log("Game successfully saved to PlayFab Cloud!"),
            error => Debug.LogError("Failed to save to cloud: " + error.GenerateErrorReport())
        );
    }

    // --- LOADING FROM THE CLOUD ---
    // Uses an Action (callback) because fetching from the server takes a few milliseconds
    public static void LoadPlayer(Action<PlayerData> onSuccess, Action onFailure = null)
    {
        PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
            result =>
            {
                if (result.Data != null && result.Data.ContainsKey("SaveGame"))
                {
                    string json = result.Data["SaveGame"].Value;
                    PlayerData data = JsonUtility.FromJson<PlayerData>(json);

                    // Give the data back to the GameManager
                    onSuccess?.Invoke(data);
                }
                else
                {
                    Debug.Log("No save data found on PlayFab for this user.");
                    onFailure?.Invoke();
                }
            },
            error =>
            {
                Debug.LogError("Error loading from cloud: " + error.GenerateErrorReport());
                onFailure?.Invoke();
            }
        );
    }

    // --- CHECKING FOR A SAVE FILE ---
    public static void CheckHasSaveFile(Action<bool> onResult)
    {
        PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
            result =>
            {
                bool hasSave = result.Data != null && result.Data.ContainsKey("SaveGame");
                onResult?.Invoke(hasSave);
            },
            error => onResult?.Invoke(false)
        );
    }

    // --- DELETING A SAVE FILE ---
    public static void DeleteSaveFile()
    {
        var request = new UpdateUserDataRequest
        {
            KeysToRemove = new List<string> { "SaveGame" }
        };

        PlayFabClientAPI.UpdateUserData(request,
            result => Debug.Log("Save file deleted from PlayFab."),
            error => Debug.LogError("Failed to delete save: " + error.GenerateErrorReport())
        );
    }
}