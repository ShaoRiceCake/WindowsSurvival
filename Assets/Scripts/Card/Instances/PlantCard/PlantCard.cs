using Newtonsoft.Json;

public abstract class PlantCard : Card
{
    [JsonIgnore] public bool IsRipe => plantGrowth.IsRipe;
    public override string CardDesc
    {
        get
        {
            var p = plantGrowth;
            if (p == null) return base.CardDesc;
            var env = Bag as EnvironmentBag;
            float t = env?.StateDict[EnvironmentStateEnum.RoomTemperature].CurValue ?? 0;
            string condition = env == null ? "未种植" : t < p.minLiveTempture ? "低温受损" : t > p.maxLiveTempture ? "高温受损" :
                t >= p.minConfortTempreture && t <= p.maxConfortTempreture ? "舒适" : t >= p.minGrowTempture && t <= p.maxGrowTempture ? "生长" : "休眠";
            return base.CardDesc + $"\n\n当前温度状态：{condition}｜{t:F1}℃\n舒适：{p.minConfortTempreture}～{p.maxConfortTempreture}℃" +
                $"\n生长：{p.minGrowTempture}～{p.maxGrowTempture}℃\n存活：{p.minLiveTempture}～{p.maxLiveTempture}℃\n生存压力：{p.survivalPressure}/8";
        }
    }

    protected override void OnLateConstructor()
    {
        UpdatePlantState();
    }

    protected virtual void UpdatePlantState() { }

    public void AddPlantGrowth(float delta)
    {
        plantGrowth.AddValue(delta);
        UpdatePlantState();
    }
}
