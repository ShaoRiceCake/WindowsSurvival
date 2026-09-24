using DG.Tweening;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 卡牌基类
/// </summary>
public abstract class Card : IComparable<Card>
{
    #region 属性
    [JsonProperty] public string CardId { get; private set; } // 卡牌ID

    [JsonProperty] public string Uuid { get; private set; } // Uuid

    [JsonProperty] protected Dictionary<Type, CardComponent> components = new();

    [JsonIgnore] public List<CardEvent> Events { get; protected set; } = new(); // 可交互事件

    [JsonIgnore] public string CardName => CardFactory.GetCardName(CardId);

    [JsonIgnore]
    public virtual string ExtraInfo
    {
        get
        {
            if (stateMachine != null)
            {
                return stateMachine.CurrentState.extraInfo;
            }
            else
            {
                return CardFactory.GetExtraInfo(CardId);
            }
        }
    }

    [JsonIgnore] public virtual string CardDesc => CardFactory.GetCardDesc(CardId);

    [JsonIgnore] public CardType CardType => CardFactory.GetCardType(CardId);

    [JsonIgnore] public int MaxStackNum => CardFactory.GetMaxStackNum(CardId);

    [JsonIgnore] public virtual bool Moveable => CardFactory.GetMoveable(CardId);

    [JsonIgnore] public CardTextureType TextureType => CardFactory.GetCardTextureType(CardId);

    [JsonIgnore]
    public float Weight
    {
        get
        {
            // 卡牌重量 = 自身重量 + 内容物重量 * 减重率
            float weight = CardFactory.GetWeight(CardId);

            if (TryGetComponent<InnerContentsComponent>(out var component))
            {
                foreach (var slot in component.bag.Slots)
                {
                    foreach (var card in slot.Cards)
                    {
                        weight += card.Weight * (1 - component.weightLossRate);
                    }
                }
            }
            return weight;
        }
    }

    [JsonIgnore] public List<CardTag> Tags => CardFactory.GetTags(CardId);

    [JsonIgnore]
    public Sprite CardImage
    {
        get
        {
            if (stateMachine != null && !string.IsNullOrEmpty(stateMachine.CurrentState.imagePath))
            {
                return CardFactory.GetCardImage(CardId, stateMachine.CurrentState.imagePath);
            }
            else
            {
                return CardFactory.GetCardImage(CardId);
            }
        }
    }

    [JsonIgnore] public bool IsBigIcon
    {
        get
        {
            if (stateMachine != null)
            {
                return stateMachine.CurrentState.isBigIcon;
            }
            else
            {
                return CardFactory.GetIsBigIcon(CardId);
            }
        }
    }

    [JsonIgnore] public virtual bool HasLoopSound => false;

    [JsonIgnore] public SlotCards SlotCards { get; protected set; } = null;

    [JsonIgnore] public Bag Bag => SlotCards?.Bag;

    [JsonIgnore] public CardSlot Slot => SlotCards?.CardSlot;

    [JsonIgnore] public Card ParentCard
    {
        get
        {
            if (Bag is InnerBag innerBag) return innerBag.BelongedCard;
            return null;
        }
    }

    // 卡牌的临时位置，用来处理从临时位置处发出一张卡牌的动效，例如从详情窗口的slot处
    protected Transform transform;

    [JsonIgnore]
    public Transform Transform
    {
        get
        {
            if (transform != null) return transform;

            if (SlotTransform != null) return SlotTransform;

            if (ParentTransform != null) return ParentTransform;

            return null;
        }
        set
        {
            transform = value;
        }
    }

    [JsonIgnore]
    public Transform SlotTransform
    {
        get
        {
            if (Slot != null) return Slot.transform;
            return null;
        }
    }

    [JsonIgnore] public Transform ParentTransform => ParentCard?.SlotTransform;
    #endregion

    #region BasicMethod
    public void SetCardId(string cardId)
    {
        CardId = cardId;
    }

    public void SetSlotCards(SlotCards slotCards)
    {
        SlotCards = slotCards;
    }

    public virtual void OnAdd(Bag bag) { }
    public virtual void OnRemove(Bag bag) { }

    /// <summary>
    /// 进入当前环境时（如玩家进入该卡牌所在地点）（通常用于播放卡牌对应的循环音）
    /// </summary>
    public virtual void OnEnterEnvironment() { }

    /// <summary>
    /// 离开当前环境时（如玩家离开该卡牌所在地点）
    /// </summary>
    public virtual void OnLeaveEnvironment() { }

    /// <summary>
    /// 打开卡牌详情时（通常用于调大卡牌对应的循环音）
    /// </summary>
    public virtual void OnDetailOpen() { }

    /// <summary>
    /// 关闭卡牌详情时（通常用于调小卡牌对应的循环音）
    /// </summary>
    public virtual void OnDetailClose() { }

    public int CompareTo(Card other)
    {
        if (other.GetType() != GetType()) return 0;
        if (TryGetComponent<FreshnessComponent>(out var a))
        {
            // 新鲜度低的优先
            other.TryGetComponent<FreshnessComponent>(out var o);
            return Mathf.CeilToInt(a.value - o.value);
        }
        else if (TryGetComponent<ProgressComponent>(out var b))
        {
            // 产物进度高的优先
            other.TryGetComponent<ProgressComponent>(out var o);
            return Mathf.CeilToInt(o.value - b.value);
        }
        else if (TryGetComponent<DurabilityComponent>(out var c))
        {
            // 耐久度低的优先
            other.TryGetComponent<DurabilityComponent>(out var o);
            return Mathf.CeilToInt(c.value - o.value);
        }
        else if (other.TryGetComponent<PlantGrowthComponent>(out var p))
        {
            // 生长度高的优先
            other.TryGetComponent<PlantGrowthComponent>(out var o);
            return Mathf.CeilToInt(o.value - p.value);
        }
        else if (other.TryGetComponent<GrowthComponent>(out var g))
        {
            // 生长度高的优先
            other.TryGetComponent<GrowthComponent>(out var o);
            return Mathf.CeilToInt(o.value - g.value);
        }
        else
        {
            return 0;
        }
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"卡牌名称: {CardName}");
        sb.AppendLine($"卡牌描述: {CardDesc}");
        sb.AppendLine($"卡牌类型: {CardType}");
        sb.AppendLine($"最大堆叠数: {MaxStackNum}");
        sb.AppendLine($"可移动: {Moveable}");
        sb.AppendLine($"重量: {Weight}");
        sb.AppendLine($"标签: {string.Join(", ", Tags)}");
        sb.AppendLine($"事件数量: {Events.Count}");
        foreach (var ev in Events)
        {
            sb.AppendLine($"  - 事件名称: {ev.Name}");
        }
        sb.AppendLine($"组件数量: {components.Count}");
        foreach (var kvp in components)
        {
            sb.AppendLine($"  - 组件类型: {kvp.Key.Name}, 实例: {kvp.Value}");
        }
        return sb.ToString();
    }
    #endregion

    #region Init
    private bool init = false; // 是否已初始化

    /// <summary>
    /// 用于在卡牌实例化后进行额外的初始化操作，主要用于为卡牌手动添加组件或者对组件的数值进行修改
    /// 仅在CardFactory.CreateCard()的最后执行一次
    /// </summary>
    public void LateConstrcutor()
    {
        // 分配组件值
        AssignComponentValues();
        // 派生类的构造逻辑
        OnLateConstructor();
    }

    protected virtual void OnLateConstructor() { }

    /// <summary>
    /// 注册交互事件
    /// </summary>
    protected virtual void RegisterCardEvents() { }

    private void GenerateUuid()
    {
        if (!string.IsNullOrEmpty(Uuid)) return;

        // 设置uuid
        Uuid = CardId + "_" + Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 卡牌创建完成并加入背包后调用，或者背包初始化时调用。
    /// 主要用于处理组件的事件监听 和 游戏内事件的监听等 无法序列化的部分
    /// </summary>
    public void Init()
    {
        if (init) return;

        init = true;

        GenerateUuid();

        // 分配组件值，方便后续调用
        AssignComponentValues();

        // 记录全局数量
        GlobalDataManager.Instance.CreateCard(this);

        // 监听事件
        EventManager.Instance.AddListener(EventType.UpdateBegin, OnUpdateBegin);
        UpdateManager.Instance.AddCardUpdateListener(ref updateOrder, Update);

        // 初始化坐标
        InitCoordinate();
        // 初始化内容物
        InitInnerContents();
        // 初始化电力消耗
        InitPowerConsumption();
        // 初始化燃料存储
        InitFuelStorage();

        // 派生类初始化
        OnInit();

        // 注册卡牌事件
        RegisterCardEvents();

        // 如果玩家当前就在该卡牌所在的环境，并且该卡牌声明有循环音效，则触发进入环境回调
        // 仅在 HasLoopSound 为 true 时调用可避免在大量无声卡牌上触发不必要的逻辑（减少加载峰值开销）
        if (HasLoopSound && Bag != null && GameManager.Instance != null && GameManager.Instance.IsCurrentEnvironment(Bag))
            OnEnterEnvironment();
    }

    private void InitCoordinate()
    {
        if (coordinate == null) return;

        // 设置卡牌坐标地点
        coordinate.coordinate.SetLocation(Bag as EnvironmentBag);
        // 监听玩家移动
        EventManager.Instance.AddListener(EventType.PlayerMove, RefreshSlot);
    }

    private void InitInnerContents()
    {
        if (innerContents == null) return;

        // 处理内容物的过滤器
        innerContents.contentFilter = ReflectionUtility.BindToDelegate<CardFilterDelegate>(this, "ContentFilter", true);
        // 内容物初始化
        innerContents.Init();
    }

    private void InitPowerConsumption()
    {
        if (powerConsumption == null) return;

        // 注册接电断电事件
        powerConsumption.powerOn = ReflectionUtility.BindToDelegate<UnityAction>(this, "PowerOn", true);
        powerConsumption.powerOff = ReflectionUtility.BindToDelegate<UnityAction>(this, "PowerOff", true);
        powerConsumption.RegisterPowerOnOffActions();
    }

    private void InitFuelStorage()
    {
        if (fuelStorage == null) return;

        fuelStorage.onIgnite = ReflectionUtility.BindToDelegate<UnityAction>(this, "OnIgnite", true);
        fuelStorage.onExtinguish = ReflectionUtility.BindToDelegate<UnityAction>(this, "OnExtinguish", true);
        fuelStorage.onBurning = ReflectionUtility.BindToDelegate<UnityAction>(this, "OnBurning", true);
        fuelStorage.onNotBurning = ReflectionUtility.BindToDelegate<UnityAction>(this, "OnNotBurning", true);
    }

    protected virtual void OnInit() { }
    #endregion

    #region Update
    [JsonProperty] protected int updateOrder = -1;

    [JsonProperty] protected bool isUpdateFreezed = false; // 是否暂停每回合更新

    private void Update()
    {
        if (isUpdateFreezed || Locked || Destroyed) return;

        // 更新组件
        foreach (var component in components.Values)
        {
            if (component is IUpdate iu) iu.Update();
        }

        OnUpdate();
    }

    private void OnUpdateBegin()
    {
        if (isUpdateFreezed || Locked || Destroyed) return;

        foreach (var component in components.Values)
        {
            if (component is IUpdate iu) iu.OnUpdateBegin();
        }
    }

    /// <summary>
    /// 每回合结算时执行
    /// </summary>
    protected virtual void OnUpdate() { }

    /// <summary>
    /// 暂停更新
    /// </summary>
    public void FreezeUpdate(bool includeInnerContents = true)
    {
        isUpdateFreezed = true;
        if (includeInnerContents)
            // 内容物也暂停更新
            innerContents?.FreezeUpdate();
    }

    /// <summary>
    /// 恢复更新
    /// </summary>
    public void UnfreezeUpdate(bool includeInnerContents = true)
    {
        isUpdateFreezed = false;
        if (includeInnerContents)
            innerContents?.UnfreezeUpdate();
    }
    #endregion

    #region Destroy
    [JsonIgnore] public bool Destroyed { get; private set; } = false;

    [JsonIgnore] public bool Locked { get; private set; } = false;

    public void DestroyThis()
    {
        if (Destroyed) return;

        Destroyed = true;

        GlobalDataManager.Instance.DestroyCard(this);

        SlotCards.RemoveCard(this);

        // 自动断电
        powerConsumption?.DisconnectPower();
        // 自动熄灭
        fuelStorage?.Extinguish();

        OnDestroy();

        OnLeaveEnvironment();

        EventManager.Instance.RemoveListener(EventType.UpdateBegin, OnUpdateBegin);
        UpdateManager.Instance.RemoveCardUpdateListener(updateOrder);
        EventManager.Instance.RemoveListener(EventType.PlayerMove, RefreshSlot);
    }

    protected virtual void OnDestroy() { }

    /// <summary>
    /// 锁定这张卡牌，使其暂停更新，并且不可以作为其他卡牌的选择对象
    /// 一般是配合DestroyThis使用。某些卡牌事件需要在时间流逝结束后对当前卡牌进行一些操作
    /// 为了避免在时间流逝过程中，这张卡牌受到其他卡牌的影响，需要在时间流逝开始前锁定它
    /// </summary>
    public void LockThis(bool includeInnerContents = true)
    {
        Locked = true;
        if (includeInnerContents)
            innerContents?.LockThis();
    }

    public void UnlockThis(bool includeInnerContents = true)
    {
        Locked = false;
        if (includeInnerContents)
            innerContents?.UnlockThis();
    }
    #endregion

    #region Component
    protected FreshnessComponent freshness;
    protected GrowthComponent growth;
    protected ProgressComponent progress;
    protected EquipmentComponent equipment;
    protected ToolComponent tool;
    protected DurabilityComponent durability;
    protected InnerContentsComponent innerContents;
    protected FoodPropertyComponent foodProperty;
    protected FuelComponent fuel;
    protected PassageComponent passage;
    protected ConstructionComponent construction;
    protected CookComponent cook;
    protected StateMachineComponent stateMachine;
    protected PlantGrowthComponent plantGrowth;
    protected FreshWaterStorageComponent freshWaterStorage;
    protected SalineWaterStorageComponent salineWaterStorage;
    protected OxygenStorageComponent oxygenStorage;
    protected TemperatureComponent temperature;
    protected FuelStorageComponent fuelStorage;
    protected EntityComponent entity;
    protected CoordinateComponent coordinate;
    protected WeaponComponent weapon;
    protected PowerConsumptionComponent powerConsumption;

    public void AssignComponentValues()
    {
        TryGetComponent(out freshness);
        TryGetComponent(out growth);
        TryGetComponent(out progress);
        TryGetComponent(out equipment);
        TryGetComponent(out tool);
        TryGetComponent(out durability);
        TryGetComponent(out innerContents);
        TryGetComponent(out foodProperty);
        TryGetComponent(out fuel);
        TryGetComponent(out passage);
        TryGetComponent(out construction);
        TryGetComponent(out cook);
        TryGetComponent(out stateMachine);
        TryGetComponent(out plantGrowth);
        TryGetComponent(out freshWaterStorage);
        TryGetComponent(out salineWaterStorage);
        TryGetComponent(out oxygenStorage);
        TryGetComponent(out temperature);
        TryGetComponent(out fuelStorage);
        TryGetComponent(out entity);
        TryGetComponent(out coordinate);
        TryGetComponent(out weapon);
        TryGetComponent(out powerConsumption);
        // 这里没有处理TimerComponent，是因为它是临时的

        foreach (var c in components.Values)
        {
            c.SetBelongedCard(this);
        }
    }

    public virtual void Use(float durabilityConsumption = 1)
    {
        if (TryGetComponent<DurabilityComponent>(out var component))
        {
            component.Use(durabilityConsumption);
        }
    }

    /// <summary>
    /// 获取卡牌的组件
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="component"></param>
    /// <returns></returns>
    public bool TryGetComponent<T>(out T component) where T : CardComponent
    {
        if (components.TryGetValue(typeof(T), out var comp))
        {
            component = (T)comp;
            return true;
        }

        component = default;
        return false;
    }

    public void AddComponent(CardComponent newComponent)
    {
        if (components.ContainsKey(newComponent.GetType())) return;

        components.Add(newComponent.GetType(), newComponent);
        newComponent.SetBelongedCard(this);
    }

    public void RemoveComponent<T>() where T : CardComponent
    {
        if (!components.ContainsKey(typeof(T))) return;

        components.Remove(typeof(T));
    }

    /// <summary>
    /// 继承其他卡牌的组件
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="other"></param>
    public bool InheritComponent<T>(Card other, out T newComponent) where T : CardComponent
    {
        if (other.TryGetComponent(out newComponent) && TryGetComponent<T>(out _))
        {
            other.RemoveComponent<T>();
            RemoveComponent<T>();
            AddComponent(newComponent);
            newComponent.SetBelongedCard(this);
            return true;
        }
        newComponent = null;
        return false;
    }

    #endregion

    #region UI
    public void RefreshSlot()
    {
        if (Slot != null) Slot.RefreshDisplay();
        EventManager.Instance.TriggerEvent(EventType.ChangeCardProperty, this);
    }

    public void DisplayComponentValueChange(Type componentType, float delta)
    {
        if (Slot != null) Slot.DisplayComponentValueChange(componentType, delta);
        if (transform != null && transform.TryGetComponent<CardSlot>(out var slot))
            slot.DisplayComponentValueChange(componentType, delta);
    }

    public void ShowTip(string tip)
    {
        if (Transform != null)
            AnimationManager.Instance.ShowFloatingTipAbove(Transform, tip);
    }

    #endregion

    #region QuickInteract
    /// <summary>
    /// 能否拖动交互
    /// </summary>
    /// <param name="card">被拿起的卡牌</param>
    /// <returns></returns>
    public virtual bool CanQuickInteract(Card card, out string tip)
    {
        tip = string.Empty;
        return false;
    }

    /// <summary>
    /// 拖动交互的具体逻辑
    /// </summary>
    /// <param name="slot">被拿起的卡牌对应的SlotCards</param>
    /// <param name="count">需要快捷交互的卡牌数量</param>
    public virtual void QuickIneract(SlotCards slot, int count) { }
    #endregion

    #region AddCard
    public Tween AddCard(string cardId, bool toPlayerBag)
    {
        return GameManager.Instance.AddCardWithTween(CardFactory.CreateCard(cardId), toPlayerBag, Transform.position);
    }

    public Tween AddCards(string cardId, int num, bool toPlayerBag)
    {
        return GameManager.Instance.AddCardsWithTween(CardFactory.CreateCards(cardId, num), toPlayerBag, Transform.position);
    }

    public Tween AddCards(List<Card> cards, bool toPlayerBag)
    {
        return GameManager.Instance.AddCardsWithTween(cards, toPlayerBag, Transform.position);
    }

    /// <summary>
    /// 掉落卡牌
    /// </summary>
    /// <returns></returns>
    public void DropCards(List<Card> cards, UnityAction onDrop)
    {
        if (cards.IsNullOrEmpty()) return;

        if (CardType != CardType.ResourcePoint || Transform == null)
        {
            AddCards(cards, true);
        }
        else
        {
            AnimationManager.Instance.PlayDropCards(Transform, () =>
            {
                onDrop?.Invoke();
                AddCards(cards, true);
            });
        }
    }

    public void RandomDrop(DropList dropList, int times = 1, UnityAction onDrop = null)
    {
        string tip = string.Empty;
        var droppedCards = new List<Card>();
        for (int i = 0; i < times; i++)
        {
            droppedCards.AddRange(dropList.RandomDrop(out tip));
        }
        DropCards(droppedCards, onDrop);
        ShowTip(tip);
    }

    /// <summary>
    /// 添加卡牌到背包(优先添加到preferredBag)
    /// </summary>
    /// <param name="card"></param>
    /// <param name="preferredBag">优先添加到的背包(不能保证一定添加到这个背包里)</param>
    /// <param name="playAnim"></param>
    public void AddCard(Card card, Bag preferredBag, bool playAnim = true, bool forceAdd = false)
    {
        // 尝试放在targetBag里
        if (preferredBag.CanAddCard(card, out _) || forceAdd)
        {
            // 成功放置
            GameManager.Instance.AddCard(card, preferredBag);

            var trans = Transform;
            if (!playAnim || trans == null || card.SlotTransform == null) return;

            AnimationManager.Instance.PlayAddCard(card, trans.position);
        }
        // 放不下看targetBag是不是内容物背包
        else if (preferredBag is InnerBag innerBag)
        {
            // 是的话尝试放在内容物背包的父物体所在的背包里
            AddCard(card, innerBag.BelongedCard.Bag, playAnim);
        }
        // 否则放在当前地点里
        else
        {
            AddCard(card, GameManager.Instance.CurEnvironmentBag, playAnim);
        }
    }

    public void AddCard(string cardId, Bag preferredBag)
    {
        AddCard(CardFactory.CreateCard(cardId), preferredBag);
    }

    /// <summary>
    /// 将自身变成另一张卡
    /// </summary>
    /// <param name="targetCard"></param>
    /// <param name="targetBag"></param>
    public void TurnTo(Card targetCard, Bag targetBag)
    {
        // 销毁自身
        DestroyThis();
        // 添加目标卡牌到包中
        AddCard(targetCard, targetBag, false);

        var trans = Transform;
        if (trans == null || targetCard.SlotTransform == null) return;

        // 动效
        if (trans == ParentTransform)
            AnimationManager.Instance.PlayAddCard(targetCard, trans.position);
        else
            AnimationManager.Instance.PlayTurnTo(this, targetCard);
    }

    public void TurnTo(string cardId, Bag targetBag)
    {
        TurnTo(CardFactory.CreateCard(cardId), targetBag);
    }

    /// <summary>
    /// 解析配置并掉落卡牌
    /// </summary>
    /// <param name="configStr">格式为：卡牌ID * 数量 + 卡牌ID * 数量 + ...</param>
    /// <returns></returns>
    public void ParseAndDrop(string configStr)
    {
        // 格式为：卡牌ID * 数量 + 卡牌ID * 数量 + ...
        string[] config;
        foreach (var str in configStr.Replace(" ", "").Split('+'))
        {
            config = str.Split('*');
            AddCards(config[0], int.Parse(config[1]), false);
        }
    }

    #endregion

    #region OtherUtils
    protected void AddCardEvent(
        string name,
        string description,
        UnityAction<CardEvent> action,
        OutStringFunc<bool> condition,
        Func<int> getTimeChange = null,
        Func<Dictionary<PlayerStateEnum, float>> getPlayerStateChanges = null,
        Func<Dictionary<EnvironmentStateEnum, float>> getEnvStateChanges = null,
        string sound = null,
        Func<bool> shouldHideThis = null)
    {
        Events.Add(new(name, description, action, condition, getTimeChange, getPlayerStateChanges, getEnvStateChanges, sound, shouldHideThis));
    }

    protected void AddCardEvent(
        string name,
        Func<string> getDescription,
        UnityAction<CardEvent> action,
        OutStringFunc<bool> condition,
        Func<int> getTimeChange = null,
        Func<Dictionary<PlayerStateEnum, float>> getPlayerStateChanges = null,
        Func<Dictionary<EnvironmentStateEnum, float>> getEnvStateChanges = null,
        string sound = null,
        Func<bool> shouldHideThis = null)
    {
        Events.Add(new(name, getDescription, action, condition, getTimeChange, getPlayerStateChanges, getEnvStateChanges, sound, shouldHideThis));
    }

    protected void EasyEvent_Destroy(CardEvent e)
    {
        DestroyThis();
        ApplyEventEffects(e);
    }

    protected void EasyEvent_DontDestroy(CardEvent e)
    {
        ApplyEventEffects(e);
    }

    protected void EasyEvent_Use(CardEvent e)
    {
        // TODO: 这里的Use还有上面的Destroy需不需要放到onEnd里
        Use();
        ApplyEventEffects(e);
    }

    protected void ApplyEventEffects(CardEvent e, UnityAction onEnd = null)
    {
        LockThis();
        // 应用状态变化
        GameManager.Instance.CurEnvironmentBag.ApplyEnvStateChanges(e.GetEnvStateChanges());
        StateManager.Instance.ApplyPlayerStateChanges(e.GetPlayerStateChanges());
        // 消耗时间
        TimeManager.Instance.AddTime(e.GetTimeChange(), () =>
        {
            UnlockThis();
            onEnd?.Invoke();
        });
    }

    protected void PlaySound(string sound, bool randomVolume = false)
    {
        // 播放音效
        if (!string.IsNullOrEmpty(sound) && SoundManager.Instance != null)
            SoundManager.Instance.PlaySound(sound, randomVolume);
    }

    public bool IsInSameBag(Card other) => Bag != null && Bag == other.Bag;
    #endregion
}
