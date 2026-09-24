using System;
using System.Collections.Generic;
using System.Linq;

// Value 2 remains readable for existing saves; all new writes normalize it to Auto.
public enum SaveKind { Auto = 0, Manual = 1, Exit = 2 }

[Serializable]
public class RunData
{
    public int version = 1;
    [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate)]
    public int worldSchemaVersion = SaveDataContract.WorldSchemaVersion;
    [Newtonsoft.Json.JsonIgnore] public bool IsCompatible => worldSchemaVersion == SaveDataContract.WorldSchemaVersion;
    public string id = Guid.NewGuid().ToString("N");
    public string name;
    public bool hardcore;
    public bool skipGuide;
    public bool fromHardcore;
    public bool dead;
    public int legacySlot = -1;
    public DateTime createdUtc = DateTime.UtcNow;
    public DateTime lastPlayedUtc;
    public double totalPlaySeconds;
    public double unsavedSeconds;
    public string latestSnapshotId;
    public List<SavePoint> snapshots = new();
}

[Serializable]
public class SavePoint
{
    public string id = Guid.NewGuid().ToString("N");
    public SaveKind kind;
    public DateTime savedUtc = DateTime.UtcNow;
    public DateTime gameTime;
    public string place;
    public string season;
    public string gameVersion;
    public bool initial;
    public double playSeconds;
    public Dictionary<string, float> states = new();
    public string checksum;
    public string KindLabel => initial ? "初始进度" : kind == SaveKind.Manual ? "手动保存" : "自动保存";
    public string TimeLabel => (string.IsNullOrEmpty(season) ? "" : season + "·") + $"第{Math.Max(1, (gameTime - new DateTime(2020, 1, 1)).Days + 1)}天 {gameTime:HH:mm}";
}

[Serializable]
public class SavePayload
{
    public int version = 1;
    [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate)]
    public int worldSchemaVersion = SaveDataContract.WorldSchemaVersion;
    public Dictionary<string, string> files = new();
}

[Serializable]
public class NewRunOptions
{
    public string name;
    public bool hardcore;
    public bool skipGuide;
}

// Separate from gameplay RNG. These curated names are existing card IDs; no personified-card subjects.
public static class RunNameGenerator
{
    private static readonly Random random = new();
    private static readonly string[] craft = { "废铁刀", "废铁铲", "废铁矛", "冰箱", "氧气罐", "矿石释氧机", "板床", "燃料炉", "捞网", "脚蹼" };
    private static readonly string[] cards = { "白爆矿", "燃素", "止痛药", "塑料袋", "精密元件", "钢锤", "氧烛", "储物箱", "自热烹饪袋", "睡眠脉冲仪" };
    private static readonly string[] ordinary = { "制作了", "捡到了", "正在研究" };
    private static readonly string[] pairs = { "麦麦听说{0}和{1}在谈恋爱", "麦麦把{0}塞进了{1}", "麦麦用{0}换来了{1}", "麦麦带着{0}寻找{1}", "麦麦分不清{0}和{1}", "麦麦为了{0}弄丢了{1}" };
    private static readonly string[] odd = { "残忍地杀害了", "想吃", "梦见了", "弄丢了", "决定相信", "想和{0}交朋友", "把{0}当成了家" };
    public static string Generate(IEnumerable<string> existing = null)
    {
        var used = new HashSet<string>(existing ?? Enumerable.Empty<string>());
        string result = "";
        for (int i = 0; i < 100; i++)
        {
            bool daily = random.Next(10) < 6;
            var pool = daily ? craft : cards.Concat(craft).ToArray();
            string card = pool[random.Next(pool.Length)];
            string verb = (daily ? ordinary : odd)[random.Next(daily ? ordinary.Length : odd.Length)];
            result = "麦麦" + (verb.Contains("{0}") ? string.Format(verb, card) : verb + card);
            if (random.Next(10) < 4)
            {
                var all = cards.Concat(craft).ToArray();
                int first = random.Next(all.Length), second = random.Next(all.Length - 1);
                if (second >= first) second++;
                result = string.Format(pairs[random.Next(pairs.Length)], all[first], all[second]);
            }
            if (result.Length <= 30 && !used.Contains(result)) return result;
        }
        int n = 2;
        while (used.Contains(result + " " + n)) n++;
        return result + " " + n;
    }
}

public static class SavePlayClock
{
    public static bool Advance(RunData run, double seconds, bool active, bool autoSave, int minutes)
    {
        if (!active) return false;
        seconds = Math.Max(0, seconds);
        run.totalPlaySeconds += seconds;
        run.unsavedSeconds += seconds;
        return autoSave && run.unsavedSeconds >= Math.Max(1, minutes) * 60;
    }
}
