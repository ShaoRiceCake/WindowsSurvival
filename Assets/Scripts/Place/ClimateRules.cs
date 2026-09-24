using UnityEngine;

/// <summary>Pure 15-minute climate rules. Furnace processing temperatures are independent.</summary>
public static class ClimateRules
{
    private static readonly float[] Insulation = { 0, .2f, .35f, .5f, .65f, .8f };
    private static readonly float[] DayCurve = { -5, -6, -2, 3, 1, -2, -5 };
    public static float InsulationRate(int level) => Insulation[Mathf.Clamp(level, 0, 5)];
    public static float EnvironmentDelta(float current, float natural, float heat, float area, int level) =>
        Mathf.Clamp((natural - current) * .08f * (1 - InsulationRate(level)) + heat / Mathf.Max(1, area), -4, 4);
    public static float DayCorrection(float hour)
    {
        hour = Mathf.Repeat(hour, 24);
        int i = Mathf.FloorToInt(hour / 4);
        return Mathf.Lerp(DayCurve[i], DayCurve[i + 1], (hour - i * 4) / 4);
    }
    public static float SeasonCorrection(int glacierDay, float weather)
    {
        if (glacierDay < 1 || glacierDay > 24) return 0;
        if (glacierDay <= 6) return -2 * glacierDay;
        if (glacierDay <= 18) return -18 + weather;
        return -15 + 3 * (glacierDay - 19);
    }
    public static float BodyDelta(float body, float effective, bool water, bool resting)
    {
        float exchange = water ? 1.35f : 1;
        if (effective < 10) return -Mathf.Min(.8f, (10 - effective) * .02f) * exchange * (resting ? 1.25f : 1);
        if (effective > 30) return Mathf.Min(.6f, (effective - 30) * .015f) * exchange;
        return Mathf.Clamp(36.5f - body, -.15f, .15f);
    }
    public static float ActionTimeMultiplier(float body) => body < 32 ? 1.5f : body < 35 ? 1.25f :
        body < 35.5f ? 1.1f : body >= 40 ? 1.4f : body >= 38.5f ? 1.15f : 1;
    public static float AbsorbCold(float loss, float durability) => Mathf.Min(loss * .85f, durability / 10);
    public static int NaturalIceDelta(float temperature, int freezingRoll) => temperature < 0 ?
        Mathf.Clamp(freezingRoll, 2, 10) : temperature > 0 ? -Mathf.Clamp(Mathf.CeilToInt(temperature / 2), 1, 10) : 0;
    public static bool IsHeatSource(string id) => Heat(id).heat > 0;
    public static (float heat, float radius, float local) Heat(string id) => id switch
    {
        "野炊营火" => (18, 6, 12), "燃料炉" => (36, 4, 8), "燃料蒸馏器" => (12, 3, 4),
        "燃料发电机" => (16, 3, 5), "点燃的氧烛" => (3, 2, 2), _ => (0, 0, 0)
    };
}
