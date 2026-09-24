using UnityEngine;

[CreateAssetMenu(menuName = "Config/Climate")]
public class ClimateConfig : ScriptableObject
{
    [Tooltip("首个冰层季开始的世界天数；0为仅手动触发。持续24天。")]
    [Min(0)] public int firstGlacierDay = 11;
    public PassageDefinition[] connections = DefaultConnections();
    public static PassageDefinition[] DefaultConnections() => new[]
    {
        new PassageDefinition("cockpit-power", PlaceEnum.Cockpit, PlaceEnum.PowerCabin, "从驾驶室到动力舱", "从动力舱到驾驶室"),
        new PassageDefinition("cockpit-life", PlaceEnum.Cockpit, PlaceEnum.LifeSupportCabin, "从驾驶室到维生舱", "从维生舱到驾驶室"),
        new PassageDefinition("cockpit-coral", PlaceEnum.Cockpit, PlaceEnum.CoralCoast, "从驾驶室到珊瑚礁海域", "从珊瑚礁海域到驾驶室"),
        new PassageDefinition("coral-tomb", PlaceEnum.CoralCoast, PlaceEnum.PhosphorTomb, "从珊瑚礁海域到织光藻墓园", "从织光藻墓园到珊瑚礁海域"),
        new PassageDefinition("coral-hull", PlaceEnum.CoralCoast, PlaceEnum.SpaceshipOuterHull, "从珊瑚礁海域到飞船外壳", "从飞船外壳到珊瑚礁海域"),
        new PassageDefinition("tomb-grotto", PlaceEnum.PhosphorTomb, PlaceEnum.ShallowGrotto, "从织光藻墓园到浅层岩穴", "从浅层岩穴到织光藻墓园"),
        new PassageDefinition("grotto-hall", PlaceEnum.ShallowGrotto, PlaceEnum.VictimsHall, "从浅层岩穴到遇难者大厅", "从遇难者大厅到浅层岩穴"),
        new PassageDefinition("hall-sanctuary", PlaceEnum.VictimsHall, PlaceEnum.LastSanctuary, "从遇难者大厅到最后庇护所", "从最后庇护所到遇难者大厅")
    };
}

[System.Serializable]
public class PassageDefinition
{
    public string id;
    public PlaceEnum a, b;
    public string cardA, cardB;
    public PassageDefinition(string id, PlaceEnum a, PlaceEnum b, string cardA, string cardB)
    { this.id = id; this.a = a; this.b = b; this.cardA = cardA; this.cardB = cardB; }
}
