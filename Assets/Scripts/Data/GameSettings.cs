using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class GameSettingsData
{
    public AudioData audio = new();
    public bool muted;
    public bool autoSave = true;
    public int autoMinutes = 10;
    public int autoKeep = 10;
    public int dialogueSpeed = 1;
    public bool fullscreen = true;
    public int width = 1920;
    public int height = 1080;
    public int fps = 60;
    public bool audioMigrated;
}

public static class GameSettings
{
    public static string StorageRootOverride;
    private static GameSettingsData current;
    public static event Action Changed;
    private static string PathName => Path.Combine(StorageRootOverride ?? Application.persistentDataPath, "Settings", "settings.json");
    public static GameSettingsData Current
    {
        get
        {
            if (current != null) return current;
            try { current = File.Exists(PathName) ? JsonConvert.DeserializeObject<GameSettingsData>(File.ReadAllText(PathName)) : new(); }
            catch (Exception e) { Debug.LogWarning("设置读取失败，使用默认值: " + e.Message); }
            current ??= new(); current.audio ??= new();
            current.autoMinutes = Mathf.Clamp(current.autoMinutes, 1, 60);
            current.autoKeep = Mathf.Clamp(current.autoKeep, 1, 50);
            return current;
        }
    }
    public static void Save()
    {
        Current.audioMigrated = true;
        SaveRepository.AtomicWrite(PathName, JsonConvert.SerializeObject(Current, Formatting.Indented));
        ApplyAudio(); Changed?.Invoke();
    }
    public static void ApplyAudio() { AudioListener.pause = Current.muted; AudioListener.volume = Mathf.Clamp01(Current.audio.masterVolume); }
    public static void ApplyDisplay()
    {
        Screen.SetResolution(Current.width, Current.height, Current.fullscreen);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Current.fps;
    }
    public static void MigrateAudio(AudioData audio)
    {
        if (Current.audioMigrated) return;
        Current.audio = audio ?? new(); Current.audioMigrated = true; Save();
    }
}
