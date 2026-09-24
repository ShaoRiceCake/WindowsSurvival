using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 当前危险程度
/// </summary>
public enum DangerLevelEnum
{
    High = 0,
    Low = 1,
    None = 2,
}

public class StateManager : IManager
{
    public static StateManager Instance { get; } = new();

    /// <summary>
    /// 玩家状态
    /// </summary>
    public Dictionary<PlayerStateEnum, State> PlayerStateDict { get; private set; } = new();

    /// <summary>
    /// 飞船水平面高度
    /// </summary>
    public State WaterLevel { get; private set; } = new();

    #region 初始化
    public void Init()
    {
        var stateData = GameDataManager.Instance.StateData;
        if (!stateData.init)
        {
            InitPlayerStates();
            InitWaterLevel();
        }
        else
        {
            WaterLevel = stateData.waterLevel;
            PlayerStateDict = stateData.playerState;
        }

        // 监听回合结算
        UpdateManager.Instance.PlayerUpdate.AddListener(PlayerUpdate);
        UpdateManager.Instance.EnvironmentUpdate.AddListener(EnvironmentUpdate);
        EventManager.Instance.AddListener(EventType.UpdateBegin, OnUpdateBegin);

        // 当环境改变时尝试获取氧气
        EventManager.Instance.AddListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, OnChangeEnv);
        
        // 玩家生命值不高于0时死亡
        EventManager.Instance.AddListener<PlayerStateEnum>(EventType.RefreshPlayerState, CheckPlayerState);

        // 水平面高于20时停止睡眠
        EventManager.Instance.AddListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, CheckEnvState);

        // 评估危险状态，播放音乐
        EvaluateDangerLevel();
    }

    public void Reset()
    {
        gameOver = false;
        IsResting = false;
        _lastDangerLevel = DangerLevelEnum.None;
        WaterLevel = new();
        PlayerStateDict = new();
        UpdateManager.Instance.PlayerUpdate.RemoveListener(PlayerUpdate);
        UpdateManager.Instance.EnvironmentUpdate.RemoveListener(EnvironmentUpdate);
        EventManager.Instance.RemoveListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, OnChangeEnv);
        EventManager.Instance.RemoveListener<PlayerStateEnum>(EventType.RefreshPlayerState, CheckPlayerState);
        EventManager.Instance.RemoveListener(EventType.UpdateBegin, OnUpdateBegin);
        EventManager.Instance.RemoveListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, CheckEnvState);
    }

    private void CheckPlayerState(PlayerStateEnum stateEnum)
    {
        if (PlayerStateDict[PlayerStateEnum.Health].CurValue <= 0) Die();
    }

    private void CheckEnvState(RefreshEnvironmentStateArgs args)
    {
        if (args.stateEnum == EnvironmentStateEnum.WaterLevel)
        {
            // 在地上休息时，若水平面超过20，则停止休息
            if (isRestingOnTheGround && !CanRestOnTheGround(out _)) StopResting();
        }
    }

    private void OnChangeEnv(EnvironmentBag env)
    {
        TryGainOxygenFromEnvironment(env);

        CalcBodyTemperatureChangeRate(env);

        CalcCOPoisoningChangeRate(env);
    }

    #region 初始化玩家状态
    private void InitPlayerStates()
    {
        // 从ScriptableObject配置初始化玩家状态
        var config = Resources.Load<PlayerStatesConfigSO>("Config/PlayerStatesConfig");
        PlayerStateDict = config.CreateAllPlayerStates();

        foreach (var state in PlayerStateDict.Values)
        {
            state.CalcStateLevel();
        }
    }
    #endregion

    private void InitWaterLevel()
    {
        WaterLevel = new(0, 100, lowerIsBetter: true);
    }
    #endregion

    #region 状态变化相关
    /// <summary>
    /// 改变玩家状态
    /// </summary>
    /// <param name="stateEnum"></param>
    /// <param name="delta"></param>
    public void ChangePlayerState(PlayerStateEnum stateEnum, float delta)
    {
        if (!PlayerStateDict.ContainsKey(stateEnum)) return;
        if (stateEnum == PlayerStateEnum.BodyTemperature) delta = ThermalSuit.Protect(delta);
        //记录改值前的危险等级
        var lastStateLevel = PlayerStateDict[stateEnum].StateLevelName;

        // 氧气特殊处理
        if (stateEnum == PlayerStateEnum.Oxygen)
            HandlePlayerOxygenChange(delta);
        else
            PlayerStateDict[stateEnum].AddValue(delta);
        //记录改值前的危险等级
        var curStateLevel = PlayerStateDict[stateEnum].StateLevelName;
        EventManager.Instance.TriggerEvent(EventType.DialogueCondition, new SubscribeActionArgs("Player" + stateEnum, PlayerStateDict[stateEnum].CurValue.ToString()));
        EventManager.Instance.TriggerEvent(EventType.DialogueCondition, new SubscribeActionArgs("Player" + stateEnum, lastStateLevel + "-" + curStateLevel));
        // 刷新前端显示
        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, stateEnum);

        // 判断危险等级，播放音乐
        EvaluateDangerLevel();
        EventManager.Instance.TriggerEvent(EventType.RefreshAnimator, PlayerStateDict);
    }

    public void ChangePlayerStateChangeRate(PlayerStateEnum stateEnum, float delta)
    {
        if (!PlayerStateDict.ContainsKey(stateEnum)) return;
        PlayerStateDict[stateEnum].AddChangeRate(delta);
        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, stateEnum);
    }

    public void SetPlayerStateBasicChangeRate(PlayerStateEnum stateEnum, float value)
    {
        if (!PlayerStateDict.ContainsKey(stateEnum)) return;
        PlayerStateDict[stateEnum].SetBasicChangeRate(value);
        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, stateEnum);
    }

    public void SetPlayerStateTempBasicChangeRate(PlayerStateEnum stateEnum, float value)
    {
        if (!PlayerStateDict.ContainsKey(stateEnum)) return;
        PlayerStateDict[stateEnum].SetTempBasicChangeRate(value);
        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, stateEnum);
    }

    public void ApplyPlayerStateChanges(Dictionary<PlayerStateEnum, float> playerStateChanges)
    {
        if (playerStateChanges.IsNullOrEmpty()) return;

        foreach (var (state, delta) in playerStateChanges)
        {
            ChangePlayerState(state, delta);
        }
    }

    /// <summary>
    /// 尝试从环境中获取氧气
    /// </summary>
    private void TryGainOxygenFromEnvironment(EnvironmentBag env)
    {
        // 没有氧气则返回
        if (!env.StateDict.TryGetValue(EnvironmentStateEnum.Oxygen, out var envOxygen)) return;

        var playerOxygen = PlayerStateDict[PlayerStateEnum.Oxygen];
        var gain = Mathf.Min(playerOxygen.RemainingCapacity, envOxygen.CurValue);
        if (gain > 0)
        {
            playerOxygen.AddValue(gain);
            env.ChangeEnvironmentState(EnvironmentStateEnum.Oxygen, -gain);
        }

        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, PlayerStateEnum.Oxygen);
    }

    private void HandlePlayerOxygenChange(float delta)
    {
        var env = GameManager.Instance.CurEnvironmentBag;
        // 环境没有氧气属性，则直接改变玩家氧气，多余的就浪费
        if (!env.StateDict.TryGetValue(EnvironmentStateEnum.Oxygen, out var envOxygen))
        {
            PlayerStateDict[PlayerStateEnum.Oxygen].AddValue(delta);
            return;
        }

        // 每次玩家的氧气变化之前，都先尝试从环境中获取氧气
        TryGainOxygenFromEnvironment(env);

        // 玩家氧气
        var playerOxygen = PlayerStateDict[PlayerStateEnum.Oxygen];

        // 室内环境
        // 如果是消耗氧气，优先消耗环境
        if (delta < 0)
        {
            delta = -delta;
            // 环境氧气剩余量
            // 要消耗的环境氧气量
            var envConsume = Mathf.Min(envOxygen.CurValue, delta);
            if (envConsume > 0)
            {
                // 消耗环境氧气
                env.ChangeEnvironmentState(EnvironmentStateEnum.Oxygen, -envConsume);
            }
            var playerConsume = delta - envConsume;
            if (playerConsume > 0)
            {
                // 消耗玩家氧气
                playerOxygen.AddValue(-playerConsume);
            }
        }
        // 如果是补充氧气，优先补充到玩家
        else if (delta > 0)
        {
            // 计算玩家能补充多少
            var playerGain = Mathf.Min(playerOxygen.RemainingCapacity, delta);
            if (playerGain > 0)
                // 补充玩家氧气
                playerOxygen.AddValue(playerGain);
            // 计算环境能补充多少
            var envGain = delta - playerGain;
            if (envGain > 0)
                // 补充环境氧气
                env.ChangeEnvironmentState(EnvironmentStateEnum.Oxygen, envGain);
        }
    }

    /// <summary>
    /// 改变玩家的额外状态
    /// </summary>
    /// <param name="stateEnum"></param>
    /// <param name="delta"></param>
    public void ChangePlayerExtraState(PlayerStateEnum stateEnum, float delta)
    {
        if (!PlayerStateDict.ContainsKey(stateEnum)) return;

        if (stateEnum == PlayerStateEnum.Oxygen)
            HandleExtraOxygenChange(delta);
        else
            PlayerStateDict[stateEnum].AddExtraValue(delta);

        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, stateEnum);
    }

    /// <summary>
    /// 处理额外氧气变化
    /// </summary>
    /// <param name="delta"></param>
    private void HandleExtraOxygenChange(float delta)
    {
        var env = GameManager.Instance.CurEnvironmentBag;

        var playerOxygen = PlayerStateDict[PlayerStateEnum.Oxygen];
        // 增加额外氧气
        if (delta > 0)
        {
            // 氧气上限增加
            playerOxygen.AddExtraValue(delta);
            // 尝试从当前环境中补满氧气
            TryGainOxygenFromEnvironment(env);
        }
        // 减少额外氧气
        else
        {
            // 记录原始氧气值
            var value1 = playerOxygen.CurValue;

            // 氧气上限减少
            PlayerStateDict[PlayerStateEnum.Oxygen].AddExtraValue(delta);

            // 记录当前氧气值
            var value2 = playerOxygen.CurValue;

            // 计算额外储存的氧气
            var extraOxygen = value1 - value2;

            // 额外储存氧气大于0
            if (extraOxygen > 0)
            {
                // 释放到环境里
                env.ChangeEnvironmentState(EnvironmentStateEnum.Oxygen, extraOxygen);
            }
        }
    }

    public void ChangePlayerConstState(PlayerStateEnum stateEnum, float delta)
    {
        if (!PlayerStateDict.ContainsKey(stateEnum)) return;

        PlayerStateDict[stateEnum].AddConstValue(delta);
        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, stateEnum);
    }

    public void ChangePlayerMaxState(PlayerStateEnum stateEnum, float delta)
    {
        if (!PlayerStateDict.ContainsKey(stateEnum)) return;

        PlayerStateDict[stateEnum].AddMaxValue(delta);
        EventManager.Instance.TriggerEvent(EventType.RefreshPlayerState, stateEnum);
    }
    #endregion

    #region 水平面相关
    /// <summary>
    /// 改变水平面
    /// </summary>
    /// <param name="delta"></param>
    public void ChangeWaterLevel(float delta)
    {
        WaterLevel.AddValue(delta);
        // 触发水平面变化事件
        //EventManager.Instance.TriggerEvent(EventType.ChangeWaterLevel, WaterLevel.CurValue);
        EventManager.Instance.TriggerEvent(EventType.DialogueCondition, new SubscribeActionArgs("WaterLevel", WaterLevel.CurValue.ToString()));

        if (WaterLevel.CurValue >= 100) Die(); // 水平面升高至100，游戏结束

        // 刷新前端显示
        var env = GameManager.Instance.CurEnvironmentBag;
        EventManager.Instance.TriggerEvent(EventType.RefreshEnvironmentState, new RefreshEnvironmentStateArgs(env.PlaceData.placeType, EnvironmentStateEnum.WaterLevel)
        {
            stateValue = WaterLevel
        });
    }

    public void ChangeWaterLevelChangeRate(float delta)
    {
        WaterLevel.AddChangeRate(delta);

        var env = GameManager.Instance.CurEnvironmentBag;
        EventManager.Instance.TriggerEvent(EventType.RefreshEnvironmentState, new RefreshEnvironmentStateArgs(env.PlaceData.placeType, EnvironmentStateEnum.WaterLevel)
        {
            stateValue = WaterLevel
        });
    }
    #endregion

    #region 定时结算相关
    private float waterLevelChangeRateSnapshot;

    private Dictionary<PlayerStateEnum, float> playerStateChangeRatesSnapshot = new(); // 记录玩家状态的当前变化率，防止玩家状态的结算顺序影响结算结果

    private void OnUpdateBegin()
    {
        // 记录快照
        waterLevelChangeRateSnapshot = WaterLevel.ChangeRate;

        CalcBodyTemperatureChangeRate(GameManager.Instance.CurEnvironmentBag);
        CalcCOPoisoningChangeRate(GameManager.Instance.CurEnvironmentBag);

        playerStateChangeRatesSnapshot.Clear();
        foreach (var (type, state) in PlayerStateDict)
        {
            if (state.ChangeRate != 0)
            {
                playerStateChangeRatesSnapshot.Add(type, state.ChangeRate);
            }
        }
    }
    
    /// <summary>
    /// 定时结算环境状态
    /// </summary>
    private void EnvironmentUpdate()
    {
        ChangeWaterLevel(waterLevelChangeRateSnapshot);
    }

    /// <summary>
    /// 定时结算玩家状态
    /// </summary>
    private void PlayerUpdate()
    {
        ApplyPlayerStateChanges(playerStateChangeRatesSnapshot);
    }

    /// <summary>
    /// 计算室温导致的体温变化
    /// </summary>
    /// <param name="env"></param>
    private void CalcBodyTemperatureChangeRate(EnvironmentBag env)
    {
        if (env == null) return;
        float rate = ClimateRules.BodyDelta(PlayerStateDict[PlayerStateEnum.BodyTemperature].CurValue,
            ClimateManager.Instance.EffectiveTemperature(env), env.PlaceData.isInWater, IsResting);
        SetPlayerStateBasicChangeRate(PlayerStateEnum.BodyTemperature, rate);
    }

    /// <summary>
    /// 计算室内一氧化碳浓度导致的一氧化碳中毒影响
    /// </summary>
    /// <param name="env"></param>
    private void CalcCOPoisoningChangeRate(EnvironmentBag env)
    {
        float basicRate = -0.3f;

        if (!env.StateDict.ContainsKey(EnvironmentStateEnum.COLevel))
        {
            SetPlayerStateBasicChangeRate(PlayerStateEnum.COPoisoning, basicRate);
            return;
        }

        var value = env.StateDict[EnvironmentStateEnum.COLevel].NormedValue;

        float rate;
        if (value <= 0)
        {
            rate = 0f;
        }
        else if (value <= 25)
        {
            rate = +.5f;
        }
        else if (value <= 50)
        {
            rate = +1f;
        }
        else if (value <= 75)
        {
            rate = +1.7f;
        }
        else
        {
            rate = +3f;
        }
        SetPlayerStateBasicChangeRate(PlayerStateEnum.COPoisoning, rate + basicRate);
    }
    #endregion

    #region 休息
    public bool IsResting { get; private set; } = false;

    private bool isRestingOnTheGround = false; // 是否在地上休息

    private List<PlayerStateEnum> playerStateTempChangeRates;

    private const int CAN_NOT_REST_ON_THE_GROUND_WATER_LEVEL_THRESHOLD = 20; // 不能在地上休息的水平面阈值

    public const float SOBRIETY_CHANGE_RATE_WHILE_RESTING_ON_THE_GROUND = +2.8f; // 在地上休息时的清醒度变化率

    public bool CanRestOnTheGround(out string reason)
    {
        reason = string.Empty;
        var env = GameManager.Instance.CurEnvironmentBag;

        if (env.PlaceData.isInWater)
        {
            reason = "不能在水域地点休息";
            return false;
        }

        if (env.PlaceData.isInSpacecraft && WaterLevel.CurValue >= CAN_NOT_REST_ON_THE_GROUND_WATER_LEVEL_THRESHOLD)
        {
            reason = "飞船内水位过高，无法在地上休息";
            return false;
        }

        return true;
    }

    public void RestOnTheGround(int time)
    {
        isRestingOnTheGround = true;
        Rest(time, new() { { PlayerStateEnum.Sobriety, SOBRIETY_CHANGE_RATE_WHILE_RESTING_ON_THE_GROUND } });
    }

    public void Rest(int time, Dictionary<PlayerStateEnum, float> playerStateTempChangeRates)
    {
        void StartRestLogic()
        {
            this.playerStateTempChangeRates = playerStateTempChangeRates.Keys.ToList();

            // 应用临时变化率
            foreach (var (state, tempChangeRate) in playerStateTempChangeRates)
            {
                SetPlayerStateTempBasicChangeRate(state, tempChangeRate);
            }

            IsResting = true;

            // 触发开始睡觉事件
            EventManager.Instance.TriggerEvent(EventType.StartSleeping);

            // 时间增加
            TimeManager.Instance.AddTime(time, StopResting);
        }

        var tween = AnimationManager.Instance.PlaySleepStartEffect(StartRestLogic);
        if (tween == null)
            StartRestLogic();
    }

    public void StopResting()
    {
        if (!IsResting) return;

        // 停止时间增加
        TimeManager.Instance.ShutTimePass();

        // 恢复原来的变化率
        foreach (var state in playerStateTempChangeRates)
        {
            SetPlayerStateTempBasicChangeRate(state, 0);
        }

        playerStateTempChangeRates.Clear();

        IsResting = false;

        if (isRestingOnTheGround) isRestingOnTheGround = false;

        // 触发结束睡觉事件
        EventManager.Instance.TriggerEvent(EventType.StopSleeping);

        AnimationManager.Instance.PlaySleepEndEffect();
    }

    #endregion

    #region 危险状态
    // 缓存上次的危险状态
    private DangerLevelEnum _lastDangerLevel = DangerLevelEnum.None;

    private void EvaluateDangerLevel()
    {
        int curLevel = int.MaxValue;
        curLevel = Mathf.Min(curLevel, (int)PlayerStateDict[PlayerStateEnum.Health].DangerLevel);
        curLevel = Mathf.Min(curLevel, (int)PlayerStateDict[PlayerStateEnum.Hunger].DangerLevel);
        curLevel = Mathf.Min(curLevel, (int)PlayerStateDict[PlayerStateEnum.Hydration].DangerLevel);
        curLevel = Mathf.Min(curLevel, (int)PlayerStateDict[PlayerStateEnum.Sobriety].DangerLevel);
        curLevel = Mathf.Min(curLevel, (int)PlayerStateDict[PlayerStateEnum.Sanity].DangerLevel);
        curLevel = Mathf.Min(curLevel, (int)PlayerStateDict[PlayerStateEnum.Oxygen].DangerLevel);

        DangerLevelEnum danger = (DangerLevelEnum)curLevel;

        //如果上次的状态和这次一致，就不切音乐
        if (danger != _lastDangerLevel)
            PlayDangerLevelMusic(danger);

        _lastDangerLevel = danger;
    }

    //处于危险状态时，就播放心跳声，离开就播放环境音乐
    public void PlayDangerLevelMusic(DangerLevelEnum currentLevel)
    {
        SoundManager.Instance.ApplyDangerEffects(currentLevel);
    }

    #endregion

    #region 死亡逻辑
    private bool gameOver = false;
    private void Die()
    {
        if (gameOver) return;
        gameOver = true;
        // 停止休息
        StopResting();
        // 停止时间流逝
        TimeManager.Instance.ShutTimePass();
        SaveSystem.Die();
    }
    #endregion

    public static string ParsePlayerState(PlayerStateEnum playerState)
    {
        return playerState switch
        {
            PlayerStateEnum.Health => "健康",
            PlayerStateEnum.Hunger => "饱食度",
            PlayerStateEnum.Hydration => "水分",
            PlayerStateEnum.Sanity => "精神",
            PlayerStateEnum.Oxygen => "氧气",
            PlayerStateEnum.Sobriety => "清醒",
            PlayerStateEnum.Load => "载重",
            PlayerStateEnum.BodyTemperature => "体温",
            PlayerStateEnum.COPoisoning => "一氧化碳中毒",
            PlayerStateEnum.Itchiness => "瘙痒",
            PlayerStateEnum.PainLevel => "疼痛",
            _ => playerState.ToString(),
        };
    }
}
