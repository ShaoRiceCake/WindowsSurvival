using System;
using System.Linq;
using UnityEngine;

public class ClimateManager : IManager
{
    public static ClimateManager Instance { get; } = new();
    public ClimateData Data { get; private set; }
    public int GlacierDay => Data == null || Data.glacierStartDay <= 0 ? 0 :
        TimeManager.Instance.Days >= Data.glacierStartDay && TimeManager.Instance.Days < Data.glacierStartDay + 24
            ? TimeManager.Instance.Days - Data.glacierStartDay + 1 : 0;
    public string SeasonName => GlacierDay == 0 ? "温和季" : GlacierDay <= 6 ? "冰层季·初临期" : GlacierDay <= 18 ? "冰层季·极寒期" : "冰层季·回暖期";
    public void Init()
    {
        Data = GameDataManager.Instance.ClimateData;
        if (!Data.initialized)
        {
            Data.weatherSeed = UnityEngine.Random.Range(1, int.MaxValue);
            Data.glacierStartDay = Resources.Load<ClimateConfig>("Config/ClimateConfig")?.firstGlacierDay ?? 11;
            foreach (var pair in GameManager.Instance.EnvironmentBags)
            {
                var mods = new PlaceModifications { place = pair.Key };
                Data.places.Add(pair.Key, mods);
                mods.Init();
                if (pair.Value.PlaceData.initialBagStateConfig.hasCable)
                {
                    var cable = CardFactory.CreateCard("防水电缆");
                    cable.TryGetComponent<DurabilityComponent>(out var d);
                    d.SetValue(UnityEngine.Random.Range(200, 601));
                    mods.cable.Install(cable);
                }
            }
            var definitions = Resources.Load<ClimateConfig>("Config/ClimateConfig")?.connections ?? ClimateConfig.DefaultConnections();
            var ids = new System.Collections.Generic.HashSet<string>();
            var cards = new System.Collections.Generic.HashSet<string>();
            foreach (var definition in definitions)
            {
                if (!ids.Add(definition.id) || !cards.Add(definition.cardA) || !cards.Add(definition.cardB))
                    throw new InvalidOperationException("通道连接ID与方向卡绑定必须唯一：" + definition.id);
                Data.connections.Add(new PassageConnection { id = definition.id, a = definition.a, b = definition.b, cardA = definition.cardA, cardB = definition.cardB });
            }
            Data.initialized = true;
        }
        else
        {
            // Correct the previous trial's future default, without restarting an active/manual season.
            if (Data.glacierStartDay == 25 && TimeManager.Instance.Days < 25 && Resources.Load<ClimateConfig>("Config/ClimateConfig")?.firstGlacierDay == 11)
                Data.glacierStartDay = 11;
            foreach (var mods in Data.places.Values) mods.Init();
        }
        foreach (var connection in Data.connections) { connection.buoy.Init(); connection.buoy.changed = connection.Refresh; }
        SaveSystem.SeasonNameProvider = _ => SeasonName;
    }
    public void Reset() { Data = null; SaveSystem.SeasonNameProvider = null; }
    public PassageConnection ConnectionForCard(string cardId, PlaceEnum place) => Data?.connections.FirstOrDefault(c =>
        c.cardA == cardId && c.a == place || c.cardB == cardId && c.b == place);
    public void StartGlacierSeason() { Data.glacierStartDay = TimeManager.Instance.Days; }
    public PassageConnection Connection(PlaceEnum a, PlaceEnum b) => Data?.connections.FirstOrDefault(c => c.a == a && c.b == b || c.a == b && c.b == a);
    public PlaceModifications Modifications(EnvironmentBag env) => Data != null && Data.places.TryGetValue(env.PlaceData.placeType, out var value) ? value : null;
    public int InsulationLevel(EnvironmentBag env) => Mathf.Clamp(env.PlaceData.insulationLevel + (Modifications(env)?.insulation.Installed != null ? 1 : 0), 0, 5);
    public float SeasonOffset
    {
        get
        {
            if (GlacierDay == 0) return 0;
            int day = TimeManager.Instance.Days;
            if (!Data.weatherByDay.TryGetValue(day, out var weather))
            {
                var roll = new System.Random(unchecked(Data.weatherSeed ^ day * 73856093)).Next(100);
                weather = roll < 50 ? 0 : roll < 80 ? -4 : -8;
                Data.weatherByDay[day] = weather;
            }
            return ClimateRules.SeasonCorrection(GlacierDay, weather);
        }
    }
    public float NaturalTemperature(EnvironmentBag env) => env.PlaceData.initialBagStateConfig.roomTemperature +
        SeasonOffset * env.PlaceData.seasonInfluence + ClimateRules.DayCorrection((float)TimeManager.Instance.CurTime.TimeOfDay.TotalHours) * env.PlaceData.dayInfluence +
        (Modifications(env)?.eventTemperatureOffset ?? 0);
    public (float heat, float local) Heat(EnvironmentBag env)
    {
        float total = 0, local = 0;
        var cards = env.GetAllCards(false);
        if (env == GameManager.Instance.CurEnvironmentBag) cards.AddRange(GameManager.Instance.PlayerBag.GetAllCards(false).Where(c => c.CardId == "点燃的氧烛"));
        foreach (var card in cards)
        {
            var h = ClimateRules.Heat(card.CardId);
            if (h.heat == 0 || card.Destroyed) continue;
            if (card.CardId != "点燃的氧烛" && (!card.TryGetComponent<FuelStorageComponent>(out var fuel) || !fuel.isBurning || fuel.value < fuel.FuelConsumption)) continue;
            if (card.CardId == "燃料蒸馏器" && (!card.TryGetComponent<SalineWaterStorageComponent>(out var saline) || saline.value < 1 ||
                card.TryGetComponent<FreshWaterStorageComponent>(out var fresh) && fresh.value >= fresh.maxValue)) continue;
            total += h.heat;
            if (env != GameManager.Instance.CurEnvironmentBag) continue;
            float position = card.Bag is PlayerBag ? Player.Instance.Coordinate.Position : card.TryGetComponent<CoordinateComponent>(out var coord) ? coord.Position : env.PlaceData.maxCoord / 2;
            local += h.local * Mathf.Clamp01(1 - Mathf.Abs(Player.Instance.Coordinate.Position - position) / h.radius);
        }
        return (total, Mathf.Min(15, local));
    }
    public float EffectiveTemperature(EnvironmentBag env) => env.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue + Heat(env).local;
    public string EnvironmentDescription(EnvironmentBag env)
    {
        float current = env.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue, natural = NaturalTemperature(env), heat = Heat(env).heat;
        int level = InsulationLevel(env);
        float delta = ClimateRules.EnvironmentDelta(current, natural, heat, env.PlaceData.maxCoord, level);
        float day = ClimateRules.DayCorrection((float)TimeManager.Instance.CurTime.TimeOfDay.TotalHours) * env.PlaceData.dayInfluence;
        return $"环境温度：{current:F1}℃（预计{delta:+0.00;-0.00;0}℃/15分钟）\n自然温度：{natural:F1}℃\n" +
            $"地点面积：{env.PlaceData.maxCoord:0}\n隔热：{level}级（{ClimateRules.InsulationRate(level) * 100:0}%）\n热源：{heat:0}热量/15分钟\n" +
            $"{SeasonName}　季节修正：{SeasonOffset * env.PlaceData.seasonInfluence:+0.0;-0.0;0}℃\n昼夜修正：{day:+0.0;-0.0;0}℃";
    }
    public void Tick()
    {
        if (Data == null) return;
        foreach (var env in GameManager.Instance.EnvironmentBags.Values)
        {
            var state = env.StateDict[EnvironmentStateEnum.RoomTemperature];
            var delta = ClimateRules.EnvironmentDelta(state.CurValue, NaturalTemperature(env), Heat(env).heat, env.PlaceData.maxCoord, InsulationLevel(env));
            env.ChangeEnvironmentState(EnvironmentStateEnum.RoomTemperature, delta);
            var cable = Modifications(env)?.cable.Installed;
            if (cable != null && UnityEngine.Random.value < .5f && cable.TryGetComponent<DurabilityComponent>(out var d)) d.Use(1);
        }
        foreach (var connection in Data.connections) connection.Tick();
    }
}
