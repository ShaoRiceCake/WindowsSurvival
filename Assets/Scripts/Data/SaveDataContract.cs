using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveDataContract
{
    public static Dictionary<string, Type> Types
    {
        get
        {
            var types = new Dictionary<string, Type>
            {
                ["PlayerBag"] = typeof(PlayerBag), ["LastPlace"] = typeof(int), ["State"] = typeof(StateData),
                ["Audio"] = typeof(AudioData), ["Technology"] = typeof(TechnologyData), ["Equipment"] = typeof(EquipmentBag),
                ["GeneratedChatData"] = typeof(GeneratedChatData), ["TimeData"] = typeof(TimeData), ["WindowsData"] = typeof(WindowsData),
                ["BehaviourExtraEffectsData"] = typeof(BehaviourExtraEffectsData), ["GlobalData"] = typeof(GlobalData),
                ["PlayerData"] = typeof(PlayerData), ["GameEventData"] = typeof(GameEventData),
                ["ElectricPowerData"] = typeof(ElectricPowerData), ["CountData"] = typeof(CountData)
            };
            foreach (PlaceEnum place in Enum.GetValues(typeof(PlaceEnum))) types[place + "Bag"] = typeof(EnvironmentBag);
            return types;
        }
    }
    public static void Validate(SavePayload payload)
    {
        if (payload == null || payload.version != 1 || payload.files == null) throw new InvalidDataException("不支持的存档格式");
        foreach (var pair in Types)
        {
            if (!payload.files.TryGetValue(pair.Key, out var json)) throw new InvalidDataException("存档缺少 " + pair.Key);
            JsonManager.Deserialize(json, pair.Value);
        }
        int place = (int)JsonManager.Deserialize(payload.files["LastPlace"], typeof(int));
        if (!Enum.IsDefined(typeof(PlaceEnum), place)) throw new InvalidDataException("存档地点无效");
    }
    public static SavePayload Legacy(int slot)
    {
        var result = new SavePayload();
        string folder = Path.Combine(Application.persistentDataPath, "GameData" + slot);
        foreach (string required in new[] { "PlayerBag", "LastPlace", "State", "TimeData" })
            if (!File.Exists(Path.Combine(folder, required + ".json"))) throw new InvalidDataException("旧档缺少必要数据 " + required);
        foreach (var pair in Types)
        {
            string path = Path.Combine(folder, pair.Key + ".json");
            // Only legacy migration may materialize missing optional data using old default semantics.
            var value = File.Exists(path) ? JsonManager.Deserialize(File.ReadAllText(path), pair.Value) : Activator.CreateInstance(pair.Value);
            result.files[pair.Key] = JsonManager.Serialize(value);
        }
        Validate(result);
        return result;
    }
    public static SavePayload NewGame()
    {
        var result = new SavePayload();
        foreach (var pair in Types) result.files[pair.Key] = JsonManager.Serialize(Activator.CreateInstance(pair.Value));
        result.files["LastPlace"] = "0";
        return result;
    }
}
