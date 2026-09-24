using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MoveExploreManager : IManager
{
    public static MoveExploreManager Instance { get; } = new();

    public MoveExploreExtraEffects ExploreExtraEffects { get; private set; } = new();           // 探索额外消耗
    public MoveExploreExtraEffects ExploreInWaterExtraEffects { get; private set; } = new();    // 探索水域额外消耗
    public MoveExploreExtraEffects MoveExtraEffects { get; private set; } = new();              // 移动额外消耗
    public MoveExploreExtraEffects MoveToWaterExtraEffects { get; private set; } = new();       // 移动到水域额外消耗

    // 上次负重
    private int lastLoadLevel;

    private List<(string reason, (float timeMultiplier, Dictionary<PlayerStateEnum, float> playerStateChanges) effect)> extraEffectsCausedByLoad = new()
    {
        { ("", (0f, new() { })) }, // 占位用
        { ("身上有点重", (0.25f, new() { })) },
        { ("身上很重", (1f, new() { { PlayerStateEnum.Health, -3 } })) },
        { ("身上太重了", (0f, new() { })) },
    };

    public void Init()
    {
        lastLoadLevel = StateManager.Instance.PlayerStateDict[PlayerStateEnum.Load].StateLevel;
        InitBehaviourExtraEffects();
        // 监听负重变化
        EventManager.Instance.AddListener<PlayerStateEnum>(EventType.RefreshPlayerState, OnLoadChange);
    }

    public void Reset()
    {
        MoveExtraEffects = new();
        MoveToWaterExtraEffects = new();
        ExploreExtraEffects = new();
        ExploreInWaterExtraEffects = new();
        EventManager.Instance.RemoveListener<PlayerStateEnum>(EventType.RefreshPlayerState, OnLoadChange);
    }

    private void InitBehaviourExtraEffects()
    {
        var data = GameDataManager.Instance.BehaviourExtraEffectsData;
        if (data.init)
        {
            MoveExtraEffects = data.moveExtraEffects;
            MoveToWaterExtraEffects = data.moveToWaterExtraEffects;
            ExploreExtraEffects = data.exploreExtraEffects;
            ExploreInWaterExtraEffects = data.exploreInWaterExtraEffects;
        }
        else
        {
            ExploreInWaterExtraEffects = new()
            {
                extraEffects = new Dictionary<string, (float timeMultiplier, Dictionary<PlayerStateEnum, float> playerStateChanges)>
                {
                    { "未装备氧气面罩", (+0.4f, new() { { PlayerStateEnum.Health, -4 } }) }
                }
            };
        }
    }

    #region 探索
    public void AddExploreExtraEffect(string reason, float timeMultiplier, Dictionary<PlayerStateEnum, float> playerStateChanges)
        => ExploreExtraEffects.AddEffect(reason, timeMultiplier, playerStateChanges);

    public void RemoveExploreExtraEffect(string reason)
        => ExploreExtraEffects.RemoveEffect(reason);

    public void AddMoveExtraEffect(string reason, float timeMultiplier, Dictionary<PlayerStateEnum, float> playerStateChanges)
        => MoveExtraEffects.AddEffect(reason, timeMultiplier, playerStateChanges);

    public void RemoveMoveExtraEffect(string reason)
        => MoveExtraEffects.RemoveEffect(reason);

    public void AddExploreInWaterExtraEffect(string reason, float timeMultiplier, Dictionary<PlayerStateEnum, float> playerStateChanges)
        => ExploreInWaterExtraEffects.AddEffect(reason, timeMultiplier, playerStateChanges);

    public void RemoveExploreInWaterExtraEffect(string reason)
        => ExploreInWaterExtraEffects.RemoveEffect(reason);

    public void AddMoveInWaterExtraEffect(string reason, float timeMultiplier, Dictionary<PlayerStateEnum, float> playerStateChanges)
        => MoveToWaterExtraEffects.AddEffect(reason, timeMultiplier, playerStateChanges);

    public void RemoveMoveInWaterExtraEffect(string reason)
        => MoveToWaterExtraEffects.RemoveEffect(reason);

    /// <summary>
    /// 得到探索当前地点的消耗
    /// </summary>
    /// <returns></returns>
    public (string desc, int time, Dictionary<PlayerStateEnum, float> playerStateChanges) GetExploreEffects()
    {
        var env = GameManager.Instance.CurEnvironmentBag;
        string desc = ExploreExtraEffects.GetDescription();
        int time = ExploreExtraEffects.GetFinalTime(env.PlaceData.exploreTime);
        Dictionary<PlayerStateEnum, float> playerStateChanges = ExploreExtraEffects.GetFinalPlayerStateChanges(new());

        // 对水域的探索额外消耗
        if (env.PlaceData.isInWater)
        {
            desc += ExploreInWaterExtraEffects.GetDescription();
            time = ExploreInWaterExtraEffects.GetFinalTime(time);
            playerStateChanges = ExploreInWaterExtraEffects.GetFinalPlayerStateChanges(playerStateChanges);
        }

        float temperatureMultiplier = ClimateRules.ActionTimeMultiplier(StateManager.Instance.PlayerStateDict[PlayerStateEnum.BodyTemperature].CurValue);
        if (temperatureMultiplier > 1) desc += $"\n体温异常：耗时+{(temperatureMultiplier - 1) * 100:0}%";
        return (desc, Mathf.CeilToInt(time * temperatureMultiplier), playerStateChanges);
    }

    /// <summary>
    /// 得到移动到目标地点的消耗
    /// </summary>
    /// <param name="basicMoveTime"></param>
    /// <param name="targetPlace"></param>
    /// <returns></returns>
    public (string desc, int time, Dictionary<PlayerStateEnum, float> playerStateChanges)
        GetMoveEffects(int basicMoveTime, PlaceEnum targetPlace)
    {
        var targetEnv = GameManager.Instance.EnvironmentBags[targetPlace];

        string desc = MoveExtraEffects.GetDescription();
        int time = MoveExtraEffects.GetFinalTime(basicMoveTime);
        Dictionary<PlayerStateEnum, float> playerStateChanges = MoveExtraEffects.GetFinalPlayerStateChanges(new());

        // 前往水域的额外消耗
        if (targetEnv.PlaceData.isInWater)
        {
            desc += MoveToWaterExtraEffects.GetDescription();
            time = MoveToWaterExtraEffects.GetFinalTime(time);
            playerStateChanges = MoveToWaterExtraEffects.GetFinalPlayerStateChanges(playerStateChanges);
        }

        float temperatureMultiplier = ClimateRules.ActionTimeMultiplier(StateManager.Instance.PlayerStateDict[PlayerStateEnum.BodyTemperature].CurValue);
        if (temperatureMultiplier > 1) desc += $"\n体温异常：耗时+{(temperatureMultiplier - 1) * 100:0}%";
        return (desc, Mathf.CeilToInt(time * temperatureMultiplier), playerStateChanges);
    }

    /// <summary>
    /// 得到地点内移动到目标位置的消耗
    /// </summary>
    /// <param name="targetPosition"></param>
    /// <returns></returns>
    public (string desc, int time, Dictionary<PlayerStateEnum, float> playerStateChanges)
        GetMoveEffects(float targetPosition)
    {
        var dist = Mathf.Abs(Player.Instance.Coordinate.Position - targetPosition);

        var basicMoveTime = Mathf.CeilToInt(dist / Player.Instance.MoveSpeed);
        var env = GameManager.Instance.CurEnvironmentBag;
        var result = GetMoveEffects(basicMoveTime, env.PlaceData.placeType);
        if (env.PlaceData.isInWater && ClimateManager.Instance.Modifications(env)?.towRope.Installed != null)
        {
            result.desc += $"\n牵引绳：移动速度+50%，耐久-{Mathf.CeilToInt(result.time / 5f)}";
            result.time = Mathf.CeilToInt(result.time / 1.5f);
        }
        return result;
    }

    /// <summary>
    /// 载重变化触发的移动探索额外消耗变化
    /// </summary>
    /// <param name="state"></param>
    private void OnLoadChange(PlayerStateEnum state)
    {
        if (state != PlayerStateEnum.Load) return;

        if (lastLoadLevel == StateManager.Instance.PlayerStateDict[PlayerStateEnum.Load].StateLevel) return;

        // 载重等级发生变化，更新额外消耗
        int currentLoadLevel = StateManager.Instance.PlayerStateDict[PlayerStateEnum.Load].StateLevel;

        if (lastLoadLevel != 0)
        {
            // 移除上一个载重等级的额外消耗
            RemoveExploreExtraEffect(extraEffectsCausedByLoad[lastLoadLevel].reason);
            RemoveMoveExtraEffect(extraEffectsCausedByLoad[lastLoadLevel].reason);
        }

        if (currentLoadLevel != 0)
        {
            // 添加当前载重等级的额外消耗
            AddExploreExtraEffect(extraEffectsCausedByLoad[currentLoadLevel].reason, extraEffectsCausedByLoad[currentLoadLevel].effect.timeMultiplier, extraEffectsCausedByLoad[currentLoadLevel].effect.playerStateChanges);
            AddMoveExtraEffect(extraEffectsCausedByLoad[currentLoadLevel].reason, extraEffectsCausedByLoad[currentLoadLevel].effect.timeMultiplier, extraEffectsCausedByLoad[currentLoadLevel].effect.playerStateChanges);
        }

        lastLoadLevel = currentLoadLevel;
    }

    /// <summary>
    /// 能否进行探索移动
    /// </summary>
    /// <returns></returns>
    public bool CanMoveExplore() => StateManager.Instance.PlayerStateDict[PlayerStateEnum.Load].StateLevel < 3; // 负重过高时无法移动和探索

    /// <summary>
    /// 处理探索事件
    /// </summary>
    public void HandleExplore(UnityAction<List<Card>, string> dropExploredCards)
    {
        if (!CanMoveExplore()) return;

        var env = GameManager.Instance.CurEnvironmentBag;

        var disposableDropList = env.DisposableDropList;
        var deepExploreDropList = env.DeepExploreDropList;
        if (disposableDropList.IsEmpty && deepExploreDropList.IsEmpty)
        {
            Debug.Log("探索完全");
            return;
        }

        // 掉落卡牌
        var droppedCards = HandeleExploreDrop(out var tip, disposableDropList, deepExploreDropList);

        (_, int time, Dictionary<PlayerStateEnum, float> playerStateChanges) = GetExploreEffects();

        // 玩家状态变化
        StateManager.Instance.ApplyPlayerStateChanges(playerStateChanges);

        // 消耗时间
        TimeManager.Instance.AddTime(time, () =>
        {
            dropExploredCards?.Invoke(droppedCards, tip);
        });

        EventManager.Instance.TriggerEvent(EventType.DialogueCondition, new SubscribeActionArgs("Click", "Explore"));
    }

    /// <summary>
    /// 处理探索掉落
    /// </summary>
    /// <param name="tip"></param>
    /// <param name="droppedCards"></param>
    private List<Card> HandeleExploreDrop(out string tip, DropList disposableDropList, DeepExploreDropList deepExploreDropList)
    {
        tip = string.Empty;

        // 当一次性探索列表还有剩余
        if (!disposableDropList.IsEmpty)
        {
            // 掉落卡牌
            return disposableDropList.RandomDrop(out tip);
        }

        // 如果还可以重复探索
        if (!deepExploreDropList.IsEmpty)
        {
            return deepExploreDropList.RandomDrop();
        }

        return new();
    }
    #endregion

    #region 移动
    /// <summary>
    /// 移动到目标地点
    /// </summary>
    /// <param name="targetEnv"></param>
    /// <param name="basicMoveTime"></param>
    public void Move(PlaceEnum targetEnv, int basicMoveTime)
    {
        if (!CanMoveExplore()) return;

        // 改变地点
        GameManager.Instance.ChangeEnv(targetEnv);

        // 移动消耗
        (_, int time, Dictionary<PlayerStateEnum, float> playerStateChanges) = GetMoveEffects(basicMoveTime, targetEnv);
        StateManager.Instance.ApplyPlayerStateChanges(playerStateChanges);
        TimeManager.Instance.AddTime(time, () =>
        {
            // 触发事件
            EventManager.Instance.TriggerEvent(EventType.DialogueCondition, new SubscribeActionArgs("EnterEnvironment", targetEnv.ToString()));
        });
    }

    /// <summary>
    /// 地点内移动
    /// </summary>
    /// <param name="targetPosition"></param>
    public void Move(float targetPosition)
    {
        if (!CanMoveExplore()) return;

        // 移动消耗
        (_, int time, Dictionary<PlayerStateEnum, float> playerStateChanges) =
            GetMoveEffects(targetPosition);

        var dist = targetPosition - Player.Instance.Coordinate.Position; // 移动距离
        if (time <= 0 || Mathf.Abs(dist) < 1e-5f) return;
        var env = GameManager.Instance.CurEnvironmentBag;
        var rope = env.PlaceData.isInWater ? ClimateManager.Instance.Modifications(env)?.towRope.Installed : null;
        if (rope != null && rope.TryGetComponent<DurabilityComponent>(out var durability))
        {
            int beforeRope = GetMoveEffects(Mathf.CeilToInt(Mathf.Abs(dist) / Player.Instance.MoveSpeed), env.PlaceData.placeType).time;
            durability.Use(Mathf.CeilToInt(beforeRope / 5f));
        }
        var distPerMin = dist / time;

        void ExecuteMove()
        {
            if (Player.Instance.IsDead) return;
            if (Mathf.Abs(Player.Instance.Coordinate.Position - targetPosition) < 1e-5)
            {
                Player.Instance.MoveTo(targetPosition); // 保证最终位置准确
                return;
            }

            Player.Instance.Move(distPerMin);
            TimeManager.Instance.AddTime(1, ExecuteMove);
        }

        // 执行移动，每分钟移动一段距离
        StateManager.Instance.ApplyPlayerStateChanges(playerStateChanges);
        ExecuteMove();

        // 根据当前地点是否为水域播放不同的移动音效
        string sound = GameManager.Instance.CurEnvironmentBag.PlaceData.isInWater ? "游动音效" : "走路音效";
        SoundManager.Instance.PlaySound(sound, true);
    }
    #endregion
}
