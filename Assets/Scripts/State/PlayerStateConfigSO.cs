using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个玩家状态的配置
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerStateConfig", menuName = "Config/Player State Config")]
public class PlayerStateConfigSO : ScriptableObject
{
    [Header("状态类型")]
    public PlayerStateEnum stateType;

    [Header("基础数值")]
    [Tooltip("初始值")]
    public float initialValue = 100;
    [Tooltip("最大值")]
    public float maxValue = 100;
    public float minValue;
    public int precision = 1;
    [Tooltip("基础变化率（每回合）")]
    public float basicChangeRate = 0;

    [Header("数值特性")]
    [Tooltip("数值越高越好")]
    public bool higherIsBetter = false;
    [Tooltip("数值越低越好")]
    public bool lowerIsBetter = false;
    [Tooltip("自然下降（下降是正常的）")]
    public bool isDecreaseNatural = false;
    [Tooltip("自然上升（上升是正常的）")]
    public bool isIncreaseNatural = false;
    [Tooltip("归一化参数（用于显示）")]
    public float normParam = 0;

    [Header("状态阈值")]
    public List<StateThresholdConfig> thresholds = new();

    [Header("危险等级")]
    [Tooltip("低危险等级对应的阈值索引列表")]
    public List<int> lowDangerLevels = new();
    [Tooltip("高危险等级对应的阈值索引列表")]
    public List<int> highDangerLevels = new();

    /// <summary>
    /// 根据配置创建State实例
    /// </summary>
    public State CreateState()
    {
        var stateThresholds = new List<StateThreshold>();
        var stateEffects = new List<StateEffect>();

        foreach (var config in thresholds)
        {
            stateThresholds.Add(new StateThreshold(config.minValueExclude, config.maxValueInclude, config.levelName)
            { includeMinimum = config.includeMinimum, excludeMaximum = config.excludeMaximum });
            stateEffects.Add(config.effect ?? StateEffect.NoEffect);
        }

        return new State(
            initialValue,
            maxValue,
            basicChangeRate,
            stateThresholds,
            stateEffects,
            new List<int>(lowDangerLevels),
            new List<int>(highDangerLevels),
            higherIsBetter,
            lowerIsBetter,
            isDecreaseNatural,
            isIncreaseNatural,
            normParam, minValue, precision
        );
    }
}

/// <summary>
/// 状态阈值配置（用于Inspector编辑）
/// </summary>
[System.Serializable]
public class StateThresholdConfig
{
    public bool includeMinimum;
    public bool excludeMaximum;
    [Tooltip("最小值（不包含）")]
    public float minValueExclude = -1;
    [Tooltip("最大值（包含）")]
    public float maxValueInclude = 100;
    [Tooltip("等级名称")]
    public string levelName = "正常";
    [Tooltip("该等级的状态效果")]
    public StateEffectConfig effect;
}

/// <summary>
/// 状态效果配置（用于Inspector编辑）
/// </summary>
[System.Serializable]
public class StateEffectConfig
{
    [Header("每回合变化率")]
    public float healthRate;
    public float sanityRate;
    public float fulnessRate;
    public float thirstRate;
    public float sorbrietyRate;
    public float bodyTemperatureRate;
    public float coPoisoningRate;

    [Header("瞬时变化")]
    public float oxygenMax;
    public float painLevelConst;

    public static implicit operator StateEffect(StateEffectConfig config)
    {
        if (config == null) return StateEffect.NoEffect;
        
        return new StateEffect
        {
            healthRate = config.healthRate,
            sanityRate = config.sanityRate,
            fulnessRate = config.fulnessRate,
            thirstRate = config.thirstRate,
            sorbrietyRate = config.sorbrietyRate,
            bodyTemperatureRate = config.bodyTemperatureRate,
            coPoisoningRate = config.coPoisoningRate,
            oxygenMax = config.oxygenMax,
            painLevelConst = config.painLevelConst
        };
    }
}
