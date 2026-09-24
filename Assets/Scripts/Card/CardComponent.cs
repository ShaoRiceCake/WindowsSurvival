using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

public interface IUpdate
{
    void OnUpdateBegin();
    void Update();
}

/// <summary>
/// 组件接口
/// </summary>
public abstract class CardComponent
{
    public Card BelongedCard { get; private set; }

    public virtual void SetBelongedCard(Card card)
    {
        BelongedCard = card;
    }

    public void RefreshSlot() => BelongedCard?.RefreshSlot();

    public void ShowTip(string tip) => BelongedCard?.ShowTip(tip);
}

#region 连续值组件
public abstract class ContinuousValueComponent : CardComponent
{
    [JsonProperty] public float value { get; protected set; }
    [JsonProperty] public float maxValue { get; protected set; }

    public ContinuousValueComponent() { }

    public ContinuousValueComponent(float value, float maxValue)
    {
        this.value = value;
        this.maxValue = maxValue;
    }

    public virtual void AddValue(float delta)
    {
        value += delta;
        value = Mathf.Clamp(value, 0, maxValue);
        RefreshSlot();
    }

    public void SetValue(float value)
    {
        this.value = value;
        RefreshSlot();
    }

    public void SetMaxValue(float maxValue)
    {
        this.maxValue = maxValue;
        value = Mathf.Clamp(value, 0, maxValue);
        RefreshSlot();
    }

    public void ResetValue()
    {
        SetValue(0);
    }
}
#endregion

#region 新鲜度组件
public class FreshnessComponent : ContinuousValueComponent, IUpdate
{
    public float updateRate = 1.0f;

    [JsonIgnore] public UnityAction onRotton;

    public FreshnessComponent() { }

    public FreshnessComponent(float maxFreshness) : base(maxFreshness, maxFreshness)
    {
    }

    private float updateRateSnapshot;

    public void OnUpdateBegin()
    {
        updateRateSnapshot = updateRate;
    }

    public void Update()
    {
        if (value <= 0) return;

        // 随时间自动减少新鲜度
        AddValue(-TimeManager.SETTLEMENT_INTERVAL * updateRateSnapshot);

        if (value <= 0)
        {
            if (BelongedCard.CardType == CardType.Food)
                ShowTip($"{BelongedCard.CardName}腐烂了");
            else if (BelongedCard.CardType == CardType.Medicine)
                ShowTip($"{BelongedCard.CardName}过期了");

            if (BelongedCard.CardId == "磁性触手" || BelongedCard.CardId == "熟触手")
                BelongedCard.TurnTo("废金属", BelongedCard.Bag);
            else
                BelongedCard.TurnTo("腐烂物", BelongedCard.Bag);

            onRotton?.Invoke();
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"新鲜度: {value}/{maxValue}\t");
        sb.Append($"更新速率: {updateRate}");
        return sb.ToString();
    }
}
#endregion

#region 生长度组件
public class GrowthComponent : ContinuousValueComponent, IUpdate
{
    public float updateRate = 1.0f;

    [JsonIgnore] public UnityAction onGrownUp;

    public GrowthComponent() { }

    public GrowthComponent(int maxGrowth) : base(0, maxGrowth)
    {
    }

    private float updateRateSnapshot;

    public void OnUpdateBegin()
    {
        updateRateSnapshot = updateRate;
    }

    public void Update()
    {
        if (value >= maxValue) return;

        // 随时间自动增加生长度
        AddValue(TimeManager.SETTLEMENT_INTERVAL * updateRateSnapshot);

        if (value >= maxValue)
        {
            BelongedCard.DestroyThis();
            onGrownUp?.Invoke();
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"生长度: {value}/{maxValue}\t");
        sb.Append($"更新速率: {updateRate}");
        return sb.ToString();
    }
}
#endregion

#region 产物进度组件
public class ProgressComponent : ContinuousValueComponent, IUpdate
{
    public float updateRate = 1.0f;

    [JsonIgnore] public UnityAction onProgressFull;

    public ProgressComponent() { }

    public ProgressComponent(int maxProgress) : base(0, maxProgress)
    {
    }

    private float updateRateSnapshot;

    public void OnUpdateBegin()
    {
        updateRateSnapshot = updateRate;
    }

    public void Update()
    {
        if (value >= maxValue) return;

        // 随时间自动增加产物进度
        AddValue(TimeManager.SETTLEMENT_INTERVAL * updateRateSnapshot);

        if (value >= maxValue)
        {
            BelongedCard.TurnTo($"有产物的{BelongedCard.CardName}", BelongedCard.Bag);
            onProgressFull?.Invoke();
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"产物进度: {value}/{maxValue}\t");
        sb.Append($"更新速率: {updateRate}");
        return sb.ToString();
    }
}
#endregion

#region 装备组件
public enum EquipmentType
{
    Head = 0,
    Body = 1,
    Back = 2,
    Leg = 3,
}

public class EquipmentComponent : CardComponent
{
    public EquipmentType equipmentType;
    public bool isEquipped;

    public EquipmentComponent() { }

    public EquipmentComponent(EquipmentType equipmentType)
    {
        isEquipped = false;
        this.equipmentType = equipmentType;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"装备类型: {equipmentType}\t");
        sb.Append($"是否装备: {isEquipped}");
        return sb.ToString();
    }
}
#endregion

#region 工具组件
public enum ToolType
{
    Cut,//切割
    Dig,//挖掘
    Hammer,//锤击
}

public class ToolComponent : CardComponent
{
    public List<ToolType> toolTypes = new();

    public ToolComponent() { }

    public ToolComponent(List<ToolType> toolTypes)
    {
        this.toolTypes = toolTypes;
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("工具类型: \t");
        foreach (var type in toolTypes)
        {
            sb.Append($"- {type}\t");
        }
        return sb.ToString();
    }
}
#endregion

#region 耐久度组件
public class DurabilityComponent : ContinuousValueComponent
{
    [JsonIgnore] public UnityAction onBroken;

    public DurabilityComponent() { }

    public DurabilityComponent(int maxDurability) : base(maxDurability, maxDurability)
    {
    }

    public void Use(float durabilityConsumption)
    {
        if (value <= 0) return;

        AddValue(-durabilityConsumption);

        StackTrace stackTrace = new();
        MethodBase callerMethod = stackTrace.GetFrame(2).GetMethod();

        if (callerMethod.Name != "OnUpdate")
            BelongedCard.DisplayComponentValueChange(typeof(DurabilityComponent), -durabilityConsumption);

        if (value <= 0)
        {
            if (BelongedCard.CardType == CardType.Tool || BelongedCard.CardType == CardType.Equipment)
                ShowTip($"{BelongedCard.CardName}损坏了");

            BelongedCard.DestroyThis();
            onBroken?.Invoke();
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append($"耐久度: {value}/{maxValue}");
        return sb.ToString();
    }
}
#endregion

#region 内容物组件
public delegate bool CardFilterDelegate(Card card, out string s);

public class InnerContentsComponent : CardComponent
{
    public float weightLossRate = 1f;           // 减重率
    public bool isCraftMaterialSource = false;  // 是否作为配方材料来源
    public InnerBag bag = new();

    public bool display = true;                 // 是否显示内容物
    public bool allowAdd = true;                // 是否允许添加内容物
    public bool allowRemove = true;             // 是否允许移除内容物

    public string notAllowRemoveReason = "";    // 不允许移除内容物的原因
    public string notAllowAddReason = "";       // 不允许放入内容物的原因

    [JsonIgnore] public CardFilterDelegate contentFilter;
    [JsonIgnore] public UnityAction<Card> onAddCard;
    [JsonIgnore] public UnityAction<Card> onRemoveCard;

    public InnerContentsComponent() { }

    public InnerContentsComponent(int slotCount, bool isCraftMaterialSource)
    {
        this.isCraftMaterialSource = isCraftMaterialSource;
        bag.AddSlot(slotCount);
    }

    public override void SetBelongedCard(Card card)
    {
        base.SetBelongedCard(card);
        bag.SetComponent(this);
    }

    public void Init() => bag.Init();

    public void Clear() => bag.Clear();

    public int GetTotalCountByCardId(string cardId) => bag.GetTotalCountByCardId(cardId);

    public int DestroyCardsByCardId(string cardId, int count) => bag.DestroyCardsByCardId(cardId, count);

    public List<Card> GetAllCards(bool excludeLocked = true) => bag.GetAllCards(excludeLocked);

    public bool CanQuickInteract(Card card, out string tip)
    {
        tip = string.Empty;
        if (card.Moveable && card.Bag != bag && bag.CanAddCard(card, out _))
        {
            tip = "放入内容物";
            return true;
        }
        return false;
    }

    public void QuickIneract(SlotCards slot, int count)
    {
        string tip = string.Empty;
        for (int i = 0; i < count; i++)
        {
            if (!bag.CanAddCard(slot.PeekCard(), out tip)) break;
            var toAdd = slot.RemoveCard();
            bag.AddCard(toAdd);
            toAdd.RefreshSlot();

        }
        ShowTip(tip);
    }

    public void AddCard(Card card) => bag.AddCard(card);

    public void FreezeUpdate()
    {
        ForEachCard(c => c.FreezeUpdate());
    }

    public void UnfreezeUpdate()
    {
        ForEachCard(c => c.UnfreezeUpdate());
    }

    public void LockThis()
    {
        ForEachCard(c => c.LockThis());
    }

    public void UnlockThis()
    {
        ForEachCard(c => c.UnlockThis());
    }

    public void ForEachCard(UnityAction<Card> action)
    {
        foreach (var slot in bag.Slots)
        {
            foreach (var card in slot.Cards)
            {
                action?.Invoke(card);
            }
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append($"内容物槽位数: {bag.SlotCount}\t");
        sb.Append("内容物: \n");
        for (int i = 0; i < bag.SlotCount; i++)
        {
            sb.Append($"槽位 {i}: ");
            if (bag[i].IsEmpty)
            {
                sb.Append("空");
            }
            else
            {
                foreach (var card in bag.Slots[i].Cards)
                {
                    sb.Append($"{card.CardId} ");
                }
            }
            sb.Append("\n");
        }
        return sb.ToString();
    }
}
#endregion

#region 食物属性
public enum FoodProperty
{
    EatableDegree,     // 可食用度
    UneatableDegree,   // 不可食用度
    Meatiness,         // 肉度
    Fishiness,         // 鱼度
    Shellfishiness,    // 贝度
    Wateriness,        // 水度
    Vegetableness,     // 菜度
    Fruitiness,        // 果度
    FoulSmellingDegree // 恶臭度
}

public class FoodPropertyComponent : CardComponent
{
    public Dictionary<FoodProperty, int> foodPropertyDict;

    public FoodPropertyComponent() { }

    public FoodPropertyComponent(Dictionary<FoodProperty, int> foodPropertyDict)
    {
        this.foodPropertyDict = foodPropertyDict;
    }
}
#endregion

#region 可燃物组件
public class FuelComponent : CardComponent
{
    public int fuelValue; // 燃料值

    public FuelComponent() { }

    public FuelComponent(int fuelValue)
    {
        this.fuelValue = fuelValue;
    }
}
#endregion

#region 通道组件
public class PassageComponent : CardComponent
{
    public PlaceEnum targetPlace;
    public int time;
    public string audioClip;

    public PassageComponent() { }

    public PassageComponent(PlaceEnum targetPlace, int time, string audioClip)
    {
        this.targetPlace = targetPlace;
        this.time = time;
        this.audioClip = audioClip;
    }
}
#endregion

#region 建筑组件
public class ConstructionComponent : CardComponent
{
    public bool onlyInWater;
    public bool onlyOutWater;
    public bool onlyInDoor;
    public bool onlyOutDoor;
    public bool needCable;

    public bool canBeDemolished; // 能否被拆毁
    public string demolitionDrops; // 拆毁后产物ID

    public ConstructionComponent() { }

    public ConstructionComponent(bool onlyInWater, bool onlyOutWater, bool onlyInDoor, bool onlyOutDoor, bool needCable, bool canBeDemolished, string demolitionDebris)
    {
        this.onlyInWater = onlyInWater;
        this.onlyOutWater = onlyOutWater;
        this.onlyInDoor = onlyInDoor;
        this.onlyOutDoor = onlyOutDoor;
        this.needCable = needCable;
        this.canBeDemolished = canBeDemolished;
        this.demolitionDrops = demolitionDebris;
    }
}
#endregion

#region 烹饪组件
public class CookComponent : CardComponent
{
    public int totalCookTime;
    public int leftCookTime;
    public string outcomeCardId;

    [JsonIgnore] public UnityAction onCooked;

    public CookComponent() { }

    public CookComponent(int totalCookTime, string outcomeCardId)
    {
        this.totalCookTime = leftCookTime = totalCookTime;
        this.outcomeCardId = outcomeCardId;
    }

    public void Cook()
    {
        if (leftCookTime <= 0) return;

        leftCookTime -= TimeManager.SETTLEMENT_INTERVAL;

        if (leftCookTime <= 0)
        {
            leftCookTime = 0;
            // 处理煮熟的逻辑
            HandleCookComplete();
        }
    }

    public void HandleCookComplete()
    {
        leftCookTime = 0;

        if (outcomeCardId == "烧焦的食物")
            ShowTip($"{BelongedCard.CardName}烧焦了");
        else if (outcomeCardId == "盐")
            ShowTip("盐水蒸干了，获得盐");
        else
            ShowTip($"{BelongedCard.CardName}熟了");

        BelongedCard.TurnTo(outcomeCardId, BelongedCard.Bag);
        onCooked?.Invoke();
    }
}
#endregion

#region 状态机组件
public class CardState
{
    public string stateName;    // 状态名称
    public string extraInfo;    // 对外显示的名称
    public string imagePath;    // 图片路径
    public bool isBigIcon;      // 是否是大图

    public CardState() { }

    public CardState(string stateName, string extraInfo, string imagePath, bool isBigIcon)
    {
        this.stateName = stateName;
        this.extraInfo = extraInfo;
        this.imagePath = imagePath;
        this.isBigIcon = isBigIcon;
    }
}

public class StateMachineComponent : CardComponent
{
    public string currentStateName;
    public Dictionary<string, CardState> stateDict = new();

    [JsonIgnore]
    public CardState CurrentState => stateDict[currentStateName];

    public StateMachineComponent() { }

    public StateMachineComponent(string initialStateName, List<CardState> states)
    {
        currentStateName = initialStateName;
        foreach (var state in states)
        {
            stateDict.Add(state.stateName, state);
        }
    }

    public StateMachineComponent(List<CardState> states) : this(string.Empty, states)
    {
        currentStateName = states[0].stateName;
    }

    public void ChangeState(string newStateName)
    {
        if (!stateDict.ContainsKey(newStateName)) return;

        if (currentStateName == newStateName) return;

        currentStateName = newStateName;
        RefreshSlot();
    }
}
#endregion

#region 植物生长组件
public class PlantGrowthComponent : ContinuousValueComponent, IUpdate
{
    private const int MAX_GROWTH = 100; // 最大生长度

    public float growthRate;            // 生长速率
    public int survivalPressure;        // 生存压力，0～8
    public float minConfortTempreture;  // 最低舒适温度
    public float maxConfortTempreture;  // 最高舒适温度
    public float minGrowTempture;       // 最低生长温度
    public float maxGrowTempture;       // 最高生长温度
    public float minLiveTempture;       // 最低存活温度
    public float maxLiveTempture;       // 最高存活温度
    public string deadCardId;           // 死亡后变成的卡牌ID 
    public List<PressureLevel> pressureList = new();    // 适合生长的压强
    public bool growStopped = false;    // 是否停止生长

    [JsonIgnore] public UnityAction onDead;
    [JsonIgnore] public bool IsRipe => value >= maxValue; // 是否成熟

    public PlantGrowthComponent(float growthRate, float minConfortTempreture, float maxConfortTempreture, float minGrowTempture, float maxGrowTempture,
        float minLiveTempture, float maxLiveTempture, string deadCardId, List<PressureLevel> pressureList) : base(MAX_GROWTH, MAX_GROWTH)
    {
        this.growthRate = growthRate;
        this.minConfortTempreture = minConfortTempreture;
        this.maxConfortTempreture = maxConfortTempreture;
        this.minGrowTempture = minGrowTempture;
        this.maxGrowTempture = maxGrowTempture;
        this.minLiveTempture = minLiveTempture;
        this.maxLiveTempture = maxLiveTempture;
        this.deadCardId = deadCardId;
        this.pressureList = pressureList;
        if (!(minLiveTempture <= minGrowTempture && minGrowTempture <= minConfortTempreture &&
            minConfortTempreture <= maxConfortTempreture && maxConfortTempreture <= maxGrowTempture && maxGrowTempture <= maxLiveTempture))
            throw new System.ArgumentException("作物温区必须满足：存活下限≤生长下限≤舒适下限≤舒适上限≤生长上限≤存活上限");
    }

    public override void AddValue(float delta)
    {
        base.AddValue(delta);

        StackTrace stackTrace = new();
        MethodBase callerMethod = stackTrace.GetFrame(2).GetMethod();

        if (callerMethod.Name != nameof(Grow))
            BelongedCard.DisplayComponentValueChange(typeof(PlantGrowthComponent), delta);
    }

    private PressureLevel pressureLevelSnapshot;
    private float envTemptureSnapshot;

    public void OnUpdateBegin()
    {
        var env = BelongedCard.Bag as EnvironmentBag;
        if (env == null) return;
        pressureLevelSnapshot = env.PressureLevel;
        envTemptureSnapshot = GetEnvTempreture(env);
    }

    public void Update()
    {
        if (BelongedCard.Bag is not EnvironmentBag || BelongedCard.Destroyed) return;

        HandleGrowth();

        HandleDeath();
    }

    private void HandleGrowth()
    {
        float t = envTemptureSnapshot;
        bool comfortable = t >= minConfortTempreture && t <= maxConfortTempreture;
        bool growing = t >= minGrowTempture && t <= maxGrowTempture;
        bool alive = t >= minLiveTempture && t <= maxLiveTempture;
        int pressure = growing ? -2 : alive ? -1 : Mathf.Max(minLiveTempture - t, t - maxLiveTempture) <= 5 ? 1 : 2;
        bool suitablePressure = pressureList.Contains(pressureLevelSnapshot);
        if (!suitablePressure) pressure = Mathf.Max(1, pressure);
        survivalPressure = Mathf.Clamp(survivalPressure + pressure, 0, 8);
        if (growing && suitablePressure) Grow(growthRate * (comfortable ? 1.2f : 1));
        RefreshSlot();
    }

    private float GetEnvTempreture(EnvironmentBag env)
    {
        // 获取当前地点的温度
        if (env.StateDict.TryGetValue(EnvironmentStateEnum.RoomTemperature, out var roomTemperature))
        {
            return roomTemperature.NormedValue;
        }
        else
        {
            UnityEngine.Debug.LogWarning("当前地点没有环境温度信息，使用默认环境温度25度");
            return 25;
        }
    }

    private void HandleDeath()
    {
        if (survivalPressure >= 8)
        {
            // 输出死亡调试信息
            var env = BelongedCard.Bag as EnvironmentBag;
            string reason = "未知原因";
            if (env != null)
            {
                if (!pressureList.Contains(env.PressureLevel))
                    reason = $"压强不合适（当前压强：{env.PressureLevel}，需要压强：{string.Join("、", pressureList)}）";
                else
                {
                    float temp = GetEnvTempreture(env);
                    if (temp > maxLiveTempture)
                        reason = $"温度过高（当前温度：{temp:F1}°C，最高存活温度：{maxLiveTempture:F1}°C）";
                    else if (temp < minLiveTempture)
                        reason = $"温度过低（当前温度：{temp:F1}°C，最低存活温度：{minLiveTempture:F1}°C）";
                }
            }
            UnityEngine.Debug.Log($"[作物死亡] {BelongedCard.CardName} 死亡了。死亡原因：{reason}");

            ShowTip($"{BelongedCard.CardName}死亡了");
            survivalPressure = 8;
            BelongedCard.DestroyThis();
            // 掉落死亡掉落物
            BelongedCard.AddCard(deadCardId, BelongedCard.Bag);
            onDead?.Invoke();
        }
    }

    private void Grow(float delta)
    {
        if (IsRipe || growStopped) return;

        AddValue(delta);
    }
}
#endregion

#region 计时器组件
public class TimerComponent : ContinuousValueComponent
{
    public string tipText;

    public TimerComponent() { }

    public TimerComponent(float maxValue) : base(maxValue, maxValue) { }

    public TimerComponent(float value, float maxValue) : base(value, maxValue) { }
}
#endregion

#region 淡水组件
public class FreshWaterStorageComponent : ContinuousValueComponent
{
    public FreshWaterStorageComponent(float maxValue) : base(0, maxValue) { }
}
#endregion

#region 盐水组件
public class SalineWaterStorageComponent : ContinuousValueComponent
{
    public SalineWaterStorageComponent(float maxValue) : base(0, maxValue) { }
}
#endregion

#region 氧气存储组件
public class OxygenStorageComponent : ContinuousValueComponent
{
    public OxygenStorageComponent(float maxValue) : base(0, maxValue) { }

    public bool Judge_GetOxygen(out string hint)
    {
        hint = string.Empty;
        // 玩家氧气剩余容量大于0，并且氧气储量大于0时可获取
        var remainingCapacity = StateManager.Instance.PlayerStateDict[PlayerStateEnum.Oxygen].RemainingCapacity;
        if (remainingCapacity == 0)
        {
            hint = "麦麦的氧气已满";
            return false;
        }
        var toRelease = Mathf.Min(value, remainingCapacity);
        if (toRelease == 0)
        {
            hint = "氧气存储不足";
            return false;
        }
        return true;
    }

    public void Event_GetOxygen(CardEvent e)
    {
        // 玩家氧气剩余容量
        var remainingCapacity = StateManager.Instance.PlayerStateDict[PlayerStateEnum.Oxygen].RemainingCapacity;
        // 计算释放量
        var toRelease = Mathf.Min(value, remainingCapacity);
        if (toRelease > 0)
        {
            // 释放氧气
            StateManager.Instance.ChangePlayerState(PlayerStateEnum.Oxygen, toRelease);
            // 氧气存量减少
            AddValue(-toRelease);
        }
    }
}
#endregion

#region 温度组件
public class TemperatureComponent : ContinuousValueComponent
{
    public TemperatureComponent(float value, float maxValue) : base(value, maxValue) { }
}
#endregion

#region 燃料存储组件
public class FuelStorageComponent : ContinuousValueComponent, IUpdate
{
    [JsonProperty] public bool isBurning { get; private set; } // 是否正在燃烧

    public int basicFuelConsumption;                       // 基础燃料消耗
    public int extraFuelConsumptionWhenWinter;             // 冰层季导致的额外燃料消耗
    public int extraFuelConsumptionWhenWaterLevelHigh;     // 水平面高时导致的额外燃料消耗
    public int autoExtinguishWaterLevelThreshold;          // 导致自动熄灭的水平面高度
    public float oxygenConsumptionOnBurning;               // 燃烧时导致的地点氧气减少
    public float coProductionOnBurning;                    // 燃烧时导致的地点一氧化碳增加

    [JsonIgnore] public UnityAction onBurning;          // 燃烧时每回合处理
    [JsonIgnore] public UnityAction onNotBurning;       // 非燃烧时每回合处理
    [JsonIgnore] public UnityAction onIgnite;           // 点燃时触发
    [JsonIgnore] public UnityAction onExtinguish;       // 熄灭时触发

    [JsonIgnore]
    public int FuelConsumption
    {
        get
        {
            int consume = basicFuelConsumption;
            if (StateManager.Instance.WaterLevel.CurValue > 0)
                consume += extraFuelConsumptionWhenWaterLevelHigh;
            // TODO: 冰层季的额外消耗

            return consume;
        }
    }

    public FuelStorageComponent(
        float maxValue,
        int basicFuelConsumption = 1,
        int extraFuelConsumptionWhenWaterLevelHigh = 2,
        int extraFuelConsumptionWhenWinter = 4,
        int autoExtinguishWaterLevelThreshold = 30,
        float oxygenConsumptionWhileBurning = 4,
        float coProductionWhileBurning = 2) : base(0, maxValue)
    {
        this.basicFuelConsumption = basicFuelConsumption;
        this.extraFuelConsumptionWhenWinter = extraFuelConsumptionWhenWinter;
        this.extraFuelConsumptionWhenWaterLevelHigh = extraFuelConsumptionWhenWaterLevelHigh;
        this.autoExtinguishWaterLevelThreshold = autoExtinguishWaterLevelThreshold;
        this.oxygenConsumptionOnBurning = oxygenConsumptionWhileBurning;
        this.coProductionOnBurning = coProductionWhileBurning;
    }

    /// <summary>
    /// 能否点燃
    /// </summary>
    public bool CanIgnite(out string tip)
    {
        tip = string.Empty;

        if (value < FuelConsumption)
        {
            tip = "燃料不足";
            return false;
        }

        if (StateManager.Instance.WaterLevel.CurValue >= autoExtinguishWaterLevelThreshold)
        {
            tip = "飞船内水位过高";
            return false;
        }

        return !isBurning;
    }

    /// <summary>
    /// 能否熄灭
    /// </summary>
    /// <returns></returns>
    public bool CanExtinguish(out string tip)
    {
        tip = string.Empty;
        return isBurning;
    }

    /// <summary>
    /// 点燃
    /// </summary>
    public void Ignite(CardEvent e = null)
    {
        if (isBurning) return;

        isBurning = true;

        var env = BelongedCard.Bag as EnvironmentBag;
        // 氧气减少，一氧化碳增加
        env.ChangeEnvironmentStateChangeRate(EnvironmentStateEnum.Oxygen, -oxygenConsumptionOnBurning);
        env.ChangeEnvironmentStateChangeRate(EnvironmentStateEnum.COLevel, coProductionOnBurning);

        onIgnite?.Invoke();

        //SoundManager.Instance.PlaySound("点火_01", true);

        RefreshSlot();
    }

    /// <summary>
    /// 熄灭
    /// </summary>
    public void Extinguish(CardEvent e = null)
    {
        if (!isBurning) return;

        isBurning = false;

        var env = BelongedCard.Bag as EnvironmentBag;
        env.ChangeEnvironmentStateChangeRate(EnvironmentStateEnum.Oxygen, oxygenConsumptionOnBurning);
        env.ChangeEnvironmentStateChangeRate(EnvironmentStateEnum.COLevel, -coProductionOnBurning);

        onExtinguish?.Invoke();

        //SoundManager.Instance.PlaySound("熄灭", true);

        RefreshSlot();
    }

    public bool CanQuickInteract(Card card)
    {
        return card.TryGetComponent<FuelComponent>(out _) && value < maxValue;
    }

    public void QuickIneract(SlotCards slot, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (value >= maxValue) break;
            var card = slot.PeekCard();
            card.TryGetComponent<FuelComponent>(out var burnableComponent);
            card.DestroyThis();
            AddValue(burnableComponent.fuelValue);
            SoundManager.Instance.PlaySound("添燃料", true);
        }
    }

    private float fuelConsumptionSnapshot;

    public void OnUpdateBegin()
    {
        fuelConsumptionSnapshot = FuelConsumption;
    }

    public void Update()
    {
        if (!isBurning)
        {
            // 非燃烧时每回合处理
            onNotBurning?.Invoke();
            return;
        }

        // 燃烧时每回合处理
        onBurning?.Invoke();

        // 燃料减少
        AddValue(-fuelConsumptionSnapshot);

        // 自动熄灭
        if (!CanIgnite(out var tip) && !string.IsNullOrEmpty(tip)) // tip不为空说明不是因为正在燃烧中而导致无法点燃
        {
            Extinguish();
            ShowTip($"{tip}，{BelongedCard.CardName}已自动熄灭");
        }
    }
}
#endregion

#region 实体组件
/// <summary>
/// 行为倾向
/// </summary>
public enum BehavioralTendency
{
    /// <summary>
    /// 友善
    /// </summary>
    Friendly,
    /// <summary>
    /// 中立
    /// </summary>
    Neutral,
    /// <summary>
    /// 敌对
    /// </summary>
    Hostile
}

public class EntityComponent : ContinuousValueComponent
{
    public float atk; // 攻击力
    public float moveDistPerMin; // 每分钟移动距离
    public BehavioralTendency behavioralTendency; // 行为倾向
    public string deadDrops; // 死亡凋落物

    public int aiRefreshInterval; // ai刷新间隔

    [JsonIgnore] public UnityAction onDead;

    public EntityComponent() { }

    public EntityComponent(float maxHealth, float atk, float moveDistPerMin, int aiRefreshInterval, BehavioralTendency behavioralTendency, string deadDrops) : base(maxHealth, maxHealth)
    {
        this.atk = atk;
        this.moveDistPerMin = moveDistPerMin;
        this.aiRefreshInterval = aiRefreshInterval;
        this.behavioralTendency = behavioralTendency;
        this.deadDrops = deadDrops;
    }

    public void TakeDamage(float damage, IEntity damageDealer)
    {
        if (value <= 0) return;

        UnityEngine.Debug.Log($"{BelongedCard.CardName}受到伤害！伤害值：{damage}，伤害者：{damageDealer}");

        AddValue(-damage);

        BelongedCard.DisplayComponentValueChange(GetType(), -damage);

        if (value <= 0)
        {
            ShowTip($"{BelongedCard.CardName}死亡了");
            BelongedCard.DestroyThis();
            // 掉落死亡掉落物
            BelongedCard.ParseAndDrop(deadDrops);
            onDead?.Invoke();
        }
    }
}
#endregion

#region 坐标组件
public class CoordinateComponent : CardComponent
{
    public Coordinate coordinate = new();

    public float initialPosition;

    [JsonIgnore] public float Position => coordinate.Position;
    [JsonIgnore] public EnvironmentBag Location => coordinate.Location;

    public CoordinateComponent() { }

    public CoordinateComponent(float initialPosition)
    {
        this.initialPosition = initialPosition;
    }

    public float DistanceTo(IEntity other) => coordinate.DistanceTo(other.Coordinate);
    public bool IsInSameLocation(IEntity other) => coordinate.IsInSameLocation(other.Coordinate);
    public void Move(float dist)
    {
        coordinate.Move(dist);
        RefreshSlot();
    }
    public void MoveTowards(IEntity other, float dist, bool stopAfterReach = true)
    {
        coordinate.MoveTowards(other.Coordinate, dist, stopAfterReach);
        RefreshSlot();
    }
    public void MoveAwayFrom(IEntity other, float dist)
    {
        coordinate.MoveAwayFrom(other.Coordinate, dist);
        RefreshSlot();
    }
}
#endregion

#region 武器组件
public enum AttackForm
{
    /// <summary>
    /// 单体攻击
    /// </summary>
    Single,
    /// <summary>
    /// 范围攻击
    /// </summary>
    AOE
}

public class WeaponComponent : CardComponent
{
    private const string DEFAULT_ATTACK_SOUND = "默认攻击声";

    public float atk;             // 攻击力
    public float minAtkDist;      // 最小攻击距离
    public float maxAtkDist;      // 最大攻击距离
    public AttackForm attackForm; // 攻击方式
    public int attackTime;        // 攻击时间(分钟)
    public string attackSound;    // 攻击音效

    public WeaponComponent() { }

    public WeaponComponent(float atk, float minAtkDist, float maxAtkDist, AttackForm attackForm, int attackTime, string attackSound)
    {
        this.atk = atk;
        this.minAtkDist = minAtkDist;
        this.maxAtkDist = maxAtkDist;
        this.attackForm = attackForm;
        this.attackTime = attackTime;
        this.attackSound = string.IsNullOrEmpty(attackSound) ? DEFAULT_ATTACK_SOUND : attackSound;
    }

    public void DealDamage(IEntity target)
    {
        // TODO: 范围伤害武器
        // 播放攻击音效
        SoundManager.Instance.PlaySound(attackSound, true);
        // 消耗武器耐久
        BelongedCard.Use();
        // 造成伤害
        target.TakeDamage(atk, Player.Instance);
        // 消耗时间
        TimeManager.Instance.AddTime(attackTime);
    }

    public bool CanAttack(IEntity target, out string reason)
    {
        reason = string.Empty;
        var dist = target.DistanceTo(Player.Instance);
        if (dist > maxAtkDist)
        {
            reason = "距离目标太远";
            return false;
        }

        if (dist < minAtkDist)
        {
            reason = "距离目标太近";
            return false;
        }

        return true;
    }
}
#endregion

#region 用电组件
public class PowerConsumptionComponent : CardComponent
{
    public float consumptionRate;

    [JsonIgnore] public bool Connected => ElectricPowerManager.Instance.IsAlreadyConnected(BelongedCard.Uuid);

    [JsonIgnore] public UnityAction powerOn;
    [JsonIgnore] public UnityAction powerOff;

    public PowerConsumptionComponent() { }

    public PowerConsumptionComponent(float consumptionRate)
    {
        this.consumptionRate = consumptionRate;
    }

    public void PowerOn()
    {
        powerOn?.Invoke();
        RefreshSlot();
    }

    public void PowerOff()
    {
        powerOff?.Invoke();
        RefreshSlot();

        // 主动断电: this.DisconnectPower -> ElectricPowerManager.Instance.DisconnectPower -> this.PowerOff
        // 被动断电: ElectricPowerManager.Instance.AutoDisconnectPower -> ElectricPowerManager.Instance.DisconnectPower -> this.PowerOff
        StackTrace stackTrace = new();
        MethodBase callerMethod = stackTrace.GetFrame(2).GetMethod();
        if (callerMethod.Name == "AutoDisconnectPower")
            ShowTip($"{BelongedCard.CardName}已自动断电");
    }

    public void ConnectPower(CardEvent e = null)
    {
        if (!CanConnectPower(out _)) return;

        ElectricPowerManager.Instance.ConnectPower(BelongedCard.Uuid, consumptionRate);
    }

    public void DisconnectPower(CardEvent e = null)
    {
        if (!Connected) return;

        ElectricPowerManager.Instance.DisconnectPower(BelongedCard.Uuid);
    }

    public void RegisterPowerOnOffActions()
    {
        ElectricPowerManager.Instance.RegisterPowerOnOffActions(BelongedCard.Uuid, PowerOn, PowerOff);
    }

    public bool CanConnectPower(out string reason)
    {
        reason = string.Empty;
        if (BelongedCard.Bag is not EnvironmentBag { HasCable: true })
        {
            reason = "该地点尚未铺设可用的防水电缆";
            return false;
        }
        return !Connected && ElectricPowerManager.Instance.CanConnectPower(consumptionRate, out reason);
    }

    public bool CanDisconnectPower(out string reason)
    {
        reason = string.Empty;
        return Connected;
    }
}
#endregion
