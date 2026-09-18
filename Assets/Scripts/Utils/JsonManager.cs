using Newtonsoft.Json;
using System.IO;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class JsonManager
{
    static JsonSerializerSettings settings = new()
    {
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        TypeNameHandling = TypeNameHandling.Auto, // 存储类型信息
        SerializationBinder = new GameSaveBinder(),
        Converters = new List<JsonConverter> { new DialogueGraphSaveConverter() },
        Formatting = Formatting.Indented
    };

    public static Dictionary<string, string> Capture;
    public static Dictionary<string, string> Source;
    public static string Serialize(object value) => JsonConvert.SerializeObject(value, settings);
    public static object Deserialize(string json, Type type) => JsonConvert.DeserializeObject(json, type, settings) ?? throw new InvalidDataException("存档内容为空");

    /// <summary>
    /// 保存数据到 Application.persistentDataPath
    /// </summary>
    /// <param name="data">要保存的数据</param>
    /// <param name="fileName">文件名</param>
    public static void SaveData(object data, string loadName, string fileName)
    {
        if (Capture != null) { Capture[fileName] = Serialize(data); return; }
        // 创建存档文件夹
        string folderPath = Application.persistentDataPath + "/" + loadName;
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 确定存储路径
        string path = folderPath + "/" + fileName + ".json";
        // 序列化
        string json = JsonConvert.SerializeObject(data, settings);

        // 将序列化后的字符串写入指定路径的文件中
        SaveRepository.AtomicWrite(path, json);
    }

    /// <summary>
    /// 加载数据 (优先从Application.persistentDataPath中加载，其次从Application.streamingAssetsPath中加载)
    /// </summary>
    /// <typeparam name="T">要加载的数据的类型</typeparam>
    /// <param name="fileName">文件名</param>
    /// <returns>反序列化后的数据类</returns>
    public static T LoadData<T>(string loadName, string fileName) where T : new()
    {
        if (Source != null)
        {
            if (!Source.TryGetValue(fileName, out var json)) throw new InvalidDataException("存档缺少 " + fileName);
            return (T)Deserialize(json, typeof(T));
        }
        // 确定读取路径
        // 先判断persistentDataPath中是否有文件
        string path = Application.persistentDataPath + "/" + loadName + "/" + fileName + ".json";

        // 再判断persistentDataPath中是否有文件
        if (!File.Exists(path))
            path = Application.streamingAssetsPath + "/" + loadName + "/" + fileName + ".json";

        return LoadData<T>(path);
    }

    public static T LoadData<T>(string path) where T : new()
    {
        // 都没有就返回默认值
        if (!File.Exists(path))
            return new T();

        // 反序列化
        string json = File.ReadAllText(path);

        return JsonConvert.DeserializeObject<T>(json, settings);
    }

    public static T DeepCopy<T>(T obj)
    {
        JsonSerializerSettings deepCopySettings = new()
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.All, // 存储类型信息
            Formatting = Formatting.Indented
        };
        return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(obj, deepCopySettings), deepCopySettings);
    }
}

// Only game data types and containers of those types are allowed in shared saves.
public sealed class GameSaveBinder : ISerializationBinder
{
    private readonly DefaultSerializationBinder binder = new();
    public Type BindToType(string assemblyName, string typeName)
    {
        var type = binder.BindToType(assemblyName, typeName);
        if (!Allowed(type)) throw new JsonSerializationException("不支持的存档类型: " + typeName);
        return type;
    }
    private static bool Allowed(Type t)
    {
        if (t == null) return false;
        if (t.IsArray) return Allowed(t.GetElementType());
        if (t.IsGenericType)
        {
            var definition = t.GetGenericTypeDefinition();
            if (definition != typeof(List<>) && definition != typeof(Dictionary<,>) && definition != typeof(HashSet<>) && definition != typeof(Queue<>) && definition != typeof(KeyValuePair<,>) && definition != typeof(Nullable<>)) return false;
            foreach (var argument in t.GetGenericArguments()) if (!Allowed(argument)) return false;
            return true;
        }
        if (t.Assembly == typeof(GameDataManager).Assembly) return !typeof(UnityEngine.Object).IsAssignableFrom(t) && !typeof(Delegate).IsAssignableFrom(t);
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(object) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(UnityEngine.Vector2) || t == typeof(UnityEngine.Vector3) || t == typeof(UnityEngine.Vector2Int);
    }
    public void BindToName(Type serializedType, out string assemblyName, out string typeName) => binder.BindToName(serializedType, out assemblyName, out typeName);
}

// Graph definitions are authored assets, not save-owned Unity objects. Legacy full graphs resolve by name too.
public sealed class DialogueGraphSaveConverter : JsonConverter
{
    public override bool CanConvert(Type type) => type == typeof(GraphData);
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null) { writer.WriteNull(); return; }
        writer.WriteStartObject(); writer.WritePropertyName("name"); writer.WriteValue(((GraphData)value).name); writer.WriteEndObject();
    }
    public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var obj = JObject.Load(reader); string name = (string)obj["name"];
        if (string.IsNullOrEmpty(name) || name.Contains("/") || name.Contains("\\")) throw new JsonSerializationException("对话资源名称无效");
        return Resources.Load<GraphData>("DialogueGraphs/" + name) ?? throw new JsonSerializationException("缺少对话资源: " + name);
    }
}
