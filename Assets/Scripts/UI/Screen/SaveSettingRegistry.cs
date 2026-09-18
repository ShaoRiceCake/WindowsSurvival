using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SaveSettingDefinition
{
    public string id, category, label, unit, description;
    public Func<bool> toggle, enabled;
    public Func<string> value;
    public Action change;
    public Action reset;
    public Func<float> number;
    public Action<float> setNumber;
    public float min, max;
    public bool whole;
}

// Register a setting once; the UI renders it without another hard-coded panel branch.
public static class SaveSettingRegistry
{
    public static readonly List<SaveSettingDefinition> Items = new();
    private static bool initialized;
    public static void Register(SaveSettingDefinition setting)
    {
        Items.RemoveAll(s => s.id == setting.id); Items.Add(setting);
    }
    public static void Initialize()
    {
        if (initialized) return; initialized = true;
        Number("master", "声音", "总音量", () => GameSettings.Current.audio.masterVolume, v => GameSettings.Current.audio.masterVolume = v, 0, 1);
        Number("music", "声音", "音乐音量", () => GameSettings.Current.audio.bgmVolume, v => GameSettings.Current.audio.bgmVolume = v, 0, 1);
        Number("sfx", "声音", "音效音量", () => GameSettings.Current.audio.sfxVolume, v => GameSettings.Current.audio.sfxVolume = v, 0, 1);
        Switch("mute", "声音", "静音", () => GameSettings.Current.muted, v => GameSettings.Current.muted = v);
        Switch("auto", "存档", "自动保存", () => GameSettings.Current.autoSave, v => GameSettings.Current.autoSave = v);
        Number("minutes", "存档", "自动保存间隔", () => GameSettings.Current.autoMinutes, v => GameSettings.Current.autoMinutes = (int)v, 1, 60, true);
        Number("keep", "存档", "自动记录上限", () => GameSettings.Current.autoKeep, v => GameSettings.Current.autoKeep = (int)v, 1, 50, true);
        Items.Find(s=>s.id=="minutes").unit="分钟";
        Items.Find(s=>s.id=="minutes").description="按实际游玩时间计算";
        Items.Find(s=>s.id=="minutes").enabled=()=>GameSettings.Current.autoSave;
        Items.Find(s=>s.id=="keep").unit="条";
        Register(new SaveSettingDefinition { id = "folder", category = "存档", label = "打开存档目录", value = () => "打开", change = () => Application.OpenURL(new Uri(SaveSystem.Repository.Root).AbsoluteUri) });
        Register(new SaveSettingDefinition { id = "speed", category = "阅读", label = "对话默认速度", value = () => "×" + GameSettings.Current.dialogueSpeed, change = () => { var s = GameSettings.Current; s.dialogueSpeed = s.dialogueSpeed == 1 ? 3 : s.dialogueSpeed == 3 ? 10 : 1; GameSettings.Save(); } });
    }
    private static void Switch(string id, string category, string label, Func<bool> get, Action<bool> set)
        => Register(new SaveSettingDefinition { id = id, category = category, label = label, toggle = get, value = () => get() ? "开启" : "关闭", change = () => { set(!get()); GameSettings.Save(); } });
    private static void Number(string id, string category, string label, Func<float> get, Action<float> set, float min, float max, bool whole = false)
        => Register(new SaveSettingDefinition { id = id, category = category, label = label, number = get, setNumber = v => { set(v); GameSettings.Save(); }, min = min, max = max, whole = whole });
    public static void Reset(string category)
    {
        var s = GameSettings.Current;
        switch (category)
        {
            case "声音": s.audio.masterVolume = .5f; s.audio.bgmVolume = 1; s.audio.sfxVolume = 1; s.muted = false; break;
            case "存档": s.autoSave = true; s.autoMinutes = 10; s.autoKeep = 10; break;
            case "阅读": s.dialogueSpeed = 1; break;
        }
        foreach (var setting in Items) if (setting.category == category) setting.reset?.Invoke();
        GameSettings.Save();
    }
}
