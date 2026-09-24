using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家状态类
/// </summary>
public class State
{
    [JsonProperty] private float extraValue;                        // 额外值
    [JsonProperty] private float maxValue;                          // 最大值
    [JsonProperty] private float minValue;
    [JsonProperty] private int precision = 1;
    [JsonProperty] private Dictionary<string, float> constValueDict = new(); // 固定值字典
    [JsonProperty] private float variableValue;                     // 可变值
    [JsonProperty] private List<StateThreshold> thresholds = new(); // 状态阈值列表
    [JsonProperty] private List<StateEffect> effects = new();       // 达到某种阈值时的状态效果列表
    [JsonProperty] private int stateLevel = -1;                     // 当前状态等级索引
    [JsonProperty] private float basicChangeRate;                   // 基础变化率
    [JsonProperty] private float extraChangeRate;                   // 额外变化率
    [JsonProperty] private List<int> lowDangerLevels = new();       // 低危险等级对应的状态等级索引
    [JsonProperty] private List<int> highDangerLevels = new();      // 高危险等级对应的状态等级索引
    [JsonProperty] private float normParam = 0;                     // 归一化参数
    [JsonProperty] private bool higherIsBetter;                     // 数值越高越好
    [JsonProperty] private bool lowerIsBetter;                      // 数值越低越好
    [JsonProperty] private bool isDecreaseNatural;                  // 是否自然下降，即下降是否是符合尝试的
    [JsonProperty] private bool isIncreaseNatural;                  // 是否自然上升，即上升是否是符合尝试的

    private float tempBasicChangeRate;                              // 临时基础变化率

    [JsonIgnore] public string StateLevelName => stateLevel >= 0 && stateLevel < thresholds.Count ? thresholds[stateLevel].levelName : "";
    [JsonIgnore] public int StateLevel => stateLevel;
    [JsonIgnore] public float CurValue => Mathf.Clamp(variableValue + ConstValue, minValue, MaxValue);
    [JsonIgnore] public float MinValue => minValue;
    [JsonIgnore] public float NormedValue => CurValue + normParam;
    [JsonIgnore] public float ExtraValue => extraValue;
    [JsonIgnore] public float MaxValue => maxValue + extraValue;
    [JsonIgnore] public float RemainingCapacity => MaxValue - CurValue;
    [JsonIgnore] public float ChangeRate => tempBasicChangeRate == 0 ? basicChangeRate + extraChangeRate : tempBasicChangeRate + extraChangeRate;
    [JsonIgnore] public bool HigherIsBetter => higherIsBetter;
    [JsonIgnore] public bool LowerIsBetter => lowerIsBetter;
    [JsonIgnore] public bool IsDecreaseNatural => isDecreaseNatural;
    [JsonIgnore] public bool IsIncreaseNatural => isIncreaseNatural;
    [JsonIgnore]
    public float ConstValue
    {
        get
        {
            float totalConstValue = 0;
            foreach (var val in constValueDict.Values)
            {
                totalConstValue += val;
            }
            return totalConstValue;
        }
    }
    [JsonIgnore]
    public DangerLevelEnum DangerLevel
    {
        get
        {
            if (highDangerLevels.Contains(stateLevel)) return DangerLevelEnum.High;
            if (lowDangerLevels.Contains(stateLevel)) return DangerLevelEnum.Low;
            return DangerLevelEnum.None;
        }
    }

    private void ClampVariableValue()
    {
        variableValue = Mathf.Clamp(variableValue, minValue, MaxValue);
        variableValue = System.MathF.Round(variableValue, precision);
    }

    public void AddValue(float delta)
    {
        variableValue += delta;
        ClampVariableValue();
        CalcStateLevel();
    }

    public void AddExtraValue(float delta)
    {
        extraValue += delta;
        ClampVariableValue();
        CalcStateLevel();
    }

    public void AddConstValue(string key, float delta)
    {
        if (!constValueDict.ContainsKey(key))
            constValueDict.Add(key, 0);
        constValueDict[key] += delta;
        CalcStateLevel();
    }

    public void AddConstValue(float delta)
    {
        AddConstValue("default", delta);
    }

    public void AddMaxValue(float delta)
    {
        maxValue += delta;
        CalcStateLevel();
    }

    public void SetConstValue(string key, float value)
    {
        if (!constValueDict.ContainsKey(key))
            constValueDict.Add(key, 0);
        constValueDict[key] = value;
        CalcStateLevel();
    }

    /// <summary>
    /// 得到下一次变化后的预测值
    /// </summary>
    /// <returns></returns>
    public float GetPredictedVariableValue()
    {
        return variableValue + basicChangeRate + extraChangeRate;
    }

    public void CalcStateLevel()
    {
        for (int i = 0; i < thresholds.Count; i++)
        {
            if (thresholds[i].Contains(CurValue))
            {
                // 如果状态等级发生了变化
                if (stateLevel != i)
                {
                    var oLevel = stateLevel; // 原来处于哪个等级
                    stateLevel = i; // 更新当前等级

                    if (effects.IsNullOrEmpty()) break;

                    // 离开stateLevel事件
                    if (oLevel != -1)
                        effects[oLevel].Revoke();
                    // 进入i事件
                    effects[stateLevel].Apply();
                }
                break;
            }
        }
    }

    public void AddChangeRate(float delta)
    {
        extraChangeRate += delta;
    }

    public void SetBasicChangeRate(float value)
    {
        basicChangeRate = value;
    }

    public void SetTempBasicChangeRate(float value)
    {
        tempBasicChangeRate = value;
    }

    public State() { }

    public State(float value, float maxValue, float basicChangeRate,
        List<StateThreshold> thresholds, List<StateEffect> effects,
        List<int> lowDangerLevels, List<int> highDangerLevels,
        bool higherIsBetter = false, bool lowerIsBetter = false,
        bool isDecreaseNatural = false, bool isIncreaseNatural = false,
        float normParam = 0, float minValue = 0, int precision = 1)
    {
        extraValue = 0;
        variableValue = value;
        this.maxValue = maxValue;
        this.higherIsBetter = higherIsBetter;
        this.lowerIsBetter = lowerIsBetter;
        this.thresholds = thresholds;
        this.effects = effects;
        this.basicChangeRate = basicChangeRate;
        extraChangeRate = 0;
        this.lowDangerLevels = lowDangerLevels;
        this.highDangerLevels = highDangerLevels;
        this.isDecreaseNatural = isDecreaseNatural;
        this.isIncreaseNatural = isIncreaseNatural;
        this.normParam = normParam;
        this.minValue = minValue;
        this.precision = precision;
    }

    public State(float value, float maxValue, float basicChangeRate = 0,
        bool higherIsBetter = false, bool lowerIsBetter = false,
        bool isDecreaseNatural = false, bool isIncreaseNatural = false,
        float normParam = 0, float minValue = 0, int precision = 1)
        : this(value, maxValue, basicChangeRate, new(), new(), new(), new(), higherIsBetter, lowerIsBetter, isDecreaseNatural, isIncreaseNatural, normParam, minValue, precision) { }
}

// 状态阈值配置
[System.Serializable]
public class StateThreshold
{
    public float minValueExclude;
    public float maxValueInclude;
    public string levelName;
    public bool includeMinimum;
    public bool excludeMaximum;

    public bool Contains(float value) => (includeMinimum ? value >= minValueExclude : value > minValueExclude)
        && (excludeMaximum ? value < maxValueInclude : value <= maxValueInclude);

    public StateThreshold(float minValueExclude, float maxValueInclude, string levelName)
    {
        this.minValueExclude = minValueExclude;
        this.maxValueInclude = maxValueInclude;
        this.levelName = levelName;
    }
}

// 状态效果配置
[System.Serializable]
public class StateEffect
{
    public static StateEffect NoEffect = new();

    // 每回合变化
    public float healthRate;            // 健康影响
    public float sanityRate;            // 精神影响
    public float fulnessRate;           // 饱食影响
    public float thirstRate;            // 水分影响
    public float sorbrietyRate;         // 清醒度影响
    public float bodyTemperatureRate;   // 体温影响
    public float coPoisoningRate;       // 一氧化碳中毒影响

    // 瞬间变化
    public float oxygenMax;             // 氧气上限
    public float painLevelConst;        // 疼痛固定值

    private void ApplyEffects(bool forward)
    {
        int signal = forward ? 1 : -1;

        // 每回合变化
        if (healthRate != 0) StateManager.Instance.ChangePlayerStateChangeRate(PlayerStateEnum.Health, healthRate * signal);
        if (sanityRate != 0) StateManager.Instance.ChangePlayerStateChangeRate(PlayerStateEnum.Sanity, sanityRate * signal);
        if (fulnessRate != 0) StateManager.Instance.ChangePlayerStateChangeRate(PlayerStateEnum.Hunger, fulnessRate * signal);
        if (thirstRate != 0) StateManager.Instance.ChangePlayerStateChangeRate(PlayerStateEnum.Hydration, thirstRate * signal);
        if (sorbrietyRate != 0) StateManager.Instance.ChangePlayerStateChangeRate(PlayerStateEnum.Sobriety, sorbrietyRate * signal);
        if (coPoisoningRate != 0) StateManager.Instance.ChangePlayerStateChangeRate(PlayerStateEnum.COPoisoning, coPoisoningRate * signal);
        if (bodyTemperatureRate != 0) StateManager.Instance.ChangePlayerStateChangeRate(PlayerStateEnum.BodyTemperature, bodyTemperatureRate * signal);

        // 瞬时变化
        if (oxygenMax != 0) StateManager.Instance.ChangePlayerMaxState(PlayerStateEnum.Oxygen, oxygenMax * signal);
        if (painLevelConst != 0) StateManager.Instance.ChangePlayerConstState(PlayerStateEnum.PainLevel, painLevelConst * signal);
    }

    public void Apply()
    {
        ApplyEffects(true);
    }

    public void Revoke()
    {
        ApplyEffects(false);
    }
}
