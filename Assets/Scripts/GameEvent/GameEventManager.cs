using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 游戏事件管理器
/// </summary>
public class GameEventManager : IManager
{
    public static GameEventManager Instance { get; } = new GameEventManager();

    public Dictionary<string, GameEvent> AllEvents { get; private set; } = new();

    private float sensitivity = 0.3f; // 敏感系数
    public float TrendValue { get; private set; } = 0; // 趋势值
    
    // 趋势值边界
    private const float MAX_TREND_VALUE = 4f;
    private const float MIN_TREND_VALUE = -4f;

    private const float EVENT_TRIGGER_PROB = 1f / 96f; // 每次结算触发事件的基础概率（约1.04%），期望触发间隔为24小时

    public InvasionEventConfig InvasionEventConfig { get; private set; } = new();

    private GameEventManager() { }

    #region 初始化
    public void Init()
    {
        // 读取存档数据
        LoadData();
        // 监听结算事件
        UpdateManager.Instance.GameEventUpdate.AddListener(Update);
    }

    public void Reset()
    {
        TrendValue = 0;
        AllEvents = new();
        InvasionEventConfig = new();
        UpdateManager.Instance.GameEventUpdate.RemoveListener(Update);
    }

    private void LoadData()
    {
        var data = GameDataManager.Instance.GameEventData;
        if (data.init)
        {
            // 读取趋势值
            TrendValue = data.trendValue;
            // 读取所有事件
            AllEvents = data.allEvents;
            // 读取入侵事件配置
            InvasionEventConfig = data.invasionConfig;
        }
        else
        {
            var events = ExcelReader.ReadGameEventConfig("GameEventConfig");
            foreach (var e in events)
            {
                AllEvents.Add(e.GetType().Name, e);
            }
        }
    }
    #endregion

    public bool IsEventOngoing<T>() where T : GameEvent
    {
        if (!AllEvents.TryGetValue(typeof(T).Name, out var e)) return false;

        return e.IsOngoing();
    }

    private void Update()
    {
        UpdateGameEvents();
        TryTriggerEvent();
    }

    private void UpdateGameEvents()
    {
        foreach (var e in AllEvents.Values)
        {
            e.Update();
        }
    }

    /// <summary>
    /// 尝试触发事件
    /// </summary>
    private void TryTriggerEvent()
    {
        // 首先根据基础概率决定是否尝试触发事件
        if (UnityEngine.Random.value > EVENT_TRIGGER_PROB) return;

        Debug.Log("尝试触发事件");

        // 获取可触发的事件候选列表（检查每个事件的独立冷却）
        var candidateEvents = GetCandidateEvents();

        if (candidateEvents.IsNullOrEmpty()) return;

        // 计算每个事件的触发权重
        var eventWeights = CalculateEventWeights(candidateEvents);

        // 根据权重随机选择事件
        var selectedEvent = SelectEventByWeight(eventWeights);

        if (selectedEvent == null) return;

        // 更新趋势值
        UpdateTrendValue(selectedEvent.ThreatLevel);

        // 触发事件
        selectedEvent.Trigger();

        // 根据威胁程度播放对应音效
        PlaySoundByThreatLevel(selectedEvent.ThreatLevel);
    }

    /// <summary>
    /// 获取可触发的事件列表（检查每个事件的独立冷却时间）
    /// </summary>
    private List<GameEvent> GetCandidateEvents()
    {
        return AllEvents.Values.Where(e => e.IsReady()).ToList();
    }

    /// <summary>
    /// 计算所有候选事件的触发权重
    /// </summary>
    private Dictionary<GameEvent, float> CalculateEventWeights(List<GameEvent> candidates)
    {
        var weights = new Dictionary<GameEvent, float>();

        // 首先计算每个事件的分子部分
        var numerators = new Dictionary<GameEvent, float>();
        float denominator = 0f;

        foreach (var gameEvent in candidates)
        {
            // 计算公式分子：基础触发权重 × e^(敏感系数 × 趋势值 × 威胁程度)
            float exponent = sensitivity * TrendValue * gameEvent.ThreatLevel;
            float numerator = gameEvent.BasicTriggerWeight * Mathf.Exp(exponent);

            numerators[gameEvent] = numerator;
            denominator += numerator;
        }

        // 计算最终权重
        foreach ((var e, var numerator) in numerators)
        {
            weights.Add(e, denominator > 0 ? numerator / denominator : 0f);
        }

        return weights;
    }

    /// <summary>
    /// 根据权重随机选择事件
    /// </summary>
    private GameEvent SelectEventByWeight(Dictionary<GameEvent, float> eventWeights)
    {
        // 计算总权重
        float totalWeight = eventWeights.Values.Sum();

        // 随机选择
        float randomValue = UnityEngine.Random.value * totalWeight;
        float currentSum = 0f;

        foreach ((var e, var weight) in eventWeights)
        {
            currentSum += weight;
            if (randomValue <= currentSum)
                return e;
        }

        return eventWeights.Keys.Last();
    }

    /// <summary>
    /// 更新趋势值
    /// </summary>
    private void UpdateTrendValue(int threatLevel)
    {
        TrendValue -= threatLevel;
        TrendValue = Math.Clamp(TrendValue, MIN_TREND_VALUE, MAX_TREND_VALUE);
    }

    // 根据危险程度播放不同音效
    private void PlaySoundByThreatLevel(int threatLevel)
    {
        // 根据事件危险程度不同播不同音效
        if (threatLevel <= -2)
        {
            // 播某某音效（有益事件）
            SoundManager.Instance.PlaySound("有益事件触发音效",true,4.0f); 
        }
        else if (threatLevel >= -1 && threatLevel <= 1)
        {
            // 播某某音效（中立事件）
            SoundManager.Instance.PlaySound("中立事件触发音效",true,4.0f); 
        }
        else if (threatLevel >= 2 && threatLevel <= 3)
        {
            // 播某某音效（威胁事件）
            SoundManager.Instance.PlaySound("威胁事件触发音效",true,4.0f); 
            
        }
        else if (threatLevel == 4)
        {
            // 播某某音效（极高威胁）
            SoundManager.Instance.PlaySound("极高威胁事件触发音效",true,1.2f); 
        }
    }
}