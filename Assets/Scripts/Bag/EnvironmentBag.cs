using Newtonsoft.Json;
using System;
using System.Collections.Generic;

public class EnvironmentBag : Bag
{
    [JsonProperty] private PlaceEnum placeType;
    [JsonProperty] private bool hasCable;
    [JsonProperty] private PressureLevel pressureLevel;
    [JsonProperty] private DropList disposableDropList = new();
    [JsonProperty] private DeepExploreDropList deepExploreDropList = new();
    [JsonProperty] private Dictionary<EnvironmentStateEnum, State> stateDict = new();

    [JsonIgnore] public bool HasCable => hasCable;
    [JsonIgnore] public PressureLevel PressureLevel => pressureLevel;
    [JsonIgnore] public string PlaceName => GameManager.Instance.PlaceDataDict[placeType].placeName;
    [JsonIgnore] public DropList DisposableDropList => disposableDropList;
    [JsonIgnore] public DeepExploreDropList DeepExploreDropList => deepExploreDropList;
    [JsonIgnore] public Dictionary<EnvironmentStateEnum, State> StateDict => stateDict;
    [JsonIgnore] public PlaceData PlaceData => GameManager.Instance.PlaceDataDict[placeType];
    [JsonIgnore] public float DiscoveryDegree => 1 - DisposableDropList.RemainingDropsRate;
    [JsonIgnore] public bool ExploreCompleted => DisposableDropList.IsEmpty && DeepExploreDropList.IsEmpty;
    [JsonIgnore] public List<IEntity> AllEntities { get; private set; } = new();

    public void SetPlaceType(PlaceEnum placeType)
    {
        this.placeType = placeType;
    }

    public void SetCable(bool installed)
    {
        hasCable = installed;
        if (!installed)
            foreach (var card in GetAllCards(false))
                if (card.TryGetComponent<PowerConsumptionComponent>(out var power)) power.DisconnectPower();
        EventManager.Instance.TriggerEvent(EventType.RefreshEnvironmentState,
            new RefreshEnvironmentStateArgs(placeType, EnvironmentStateEnum.HasCable) { hasCable = installed });
    }

    #region Init
    protected override void FirstInit()
    {
        AddSlot(9);
        FirstInitState();
        FirstInitDropList();
        FirstInitContainedCards();
    }

    public override void Init()
    {
        base.Init();
        RemoveObsoleteSaltDrops();
        DeepExploreDropList.Init();
        EventManager.Instance.AddListener(EventType.UpdateBegin, OnEnvUpdateBegin);
        EventManager.Instance.AddListener<float>(EventType.UpdateSunlight, OnUpdateSunlight);
        // 每回合结算地点状态
        UpdateManager.Instance.EnvironmentUpdate.AddListener(EnvUpdate);
    }

    private void FirstInitState()
    {
        // 是否铺设电缆
        hasCable = PlaceData.initialBagStateConfig.hasCable;

        // 压强等级
        pressureLevel = PlaceData.initialBagStateConfig.pressureLevel;

        // 在室内且非水域显示氧气
        // 在室内且非水域显示一氧化碳
        if (PlaceData.isIndoor && !PlaceData.isInWater)
        {
            StateDict.Add(EnvironmentStateEnum.Oxygen, new State(UnityEngine.Random.Range(400, 600), 1000, higherIsBetter: true));
            StateDict.Add(EnvironmentStateEnum.COLevel, new State(0, 100, -0.5f, lowerIsBetter: true));
        }

        // 室温
        stateDict.Add(EnvironmentStateEnum.RoomTemperature,
            new State(PlaceData.initialBagStateConfig.roomTemperature, 200, minValue: -100, precision: 3));

        // 光照
        var thresholds = new List<StateThreshold>
        {
            new (-1, 0, "漆黑"),
            new (0, 20, "昏暗"),
            new (20, 40, "柔和"),
            new (40, 60, "微光"),
            new (60, 80, "明亮"),
            new (80, 100, "耀眼"),
        };
        var state = new State(0, 100, 0, thresholds, new(), new(), new());
        state.SetConstValue("基础光照",  PlaceData.initialBagStateConfig.brightness);
        state.SetConstValue("恒星光照", SunlightManager.Instance.Sunlight * PlaceData.sunlightInfluenceFactor);
        stateDict.Add(EnvironmentStateEnum.Brightness, state);
    }

    // Remove only the obsolete V6 trial drop source, never salt already owned by the player.
    private void RemoveObsoleteSaltDrops()
    {
        if (placeType != PlaceEnum.CoralCoast && placeType != PlaceEnum.PhosphorTomb && placeType != PlaceEnum.SpaceshipOuterHull) return;
        int removed = disposableDropList.dropList.RemoveAll(drop => drop.dropConfig.Count == 1 && drop.dropConfig[0].ContainsCard("盐"));
        if (removed > 0) disposableDropList.maxCount = Math.Max(1, disposableDropList.maxCount - removed);
        deepExploreDropList.populationList.RemoveAll(population => population.cardTemplate?.CardId == "盐");
    }

    private void FirstInitDropList()
    {
        disposableDropList = ExcelReader.ReadDisposableDropListConfig(placeType);
        deepExploreDropList = ExcelReader.ReadDeepExploreDropListConfig(placeType);
        if (!string.IsNullOrEmpty(PlaceData.climateMaterialId))
        {
            for (int i = 0; i < PlaceData.climateMaterialCaches; i++)
                disposableDropList.dropList.Add(new Drop(10, PlaceData.climateMaterialId, PlaceData.climateMaterialPerCache));
            disposableDropList.maxCount = disposableDropList.dropList.Count;
            deepExploreDropList.populationList.Add(new Population
            {
                cardTemplate = CardFactory.CreateCard(PlaceData.climateMaterialId), dropNum = 2,
                curSize = 12, maxSize = 12, sizeChangePerRound = 1, sizeChangeOnCaught = -6, trappable = false
            });
        }
    }

    private void FirstInitContainedCards()
    {
        foreach (var cardId in PlaceData.initialBagStateConfig.containedCards)
        {
            GameManager.Instance.AddCard(CardFactory.CreateCard(cardId), this);
        }
    }

    private void OnUpdateSunlight(float sunlight)
    {
        SetBrightnessConstValue("恒星光照", sunlight * PlaceData.sunlightInfluenceFactor);
    }
    #endregion

    #region Update
    private Dictionary<EnvironmentStateEnum, float> envStateChangeRatesSnapshot = new(); // 记录地点状态的当前变化率，防止地点状态的结算顺序影响结算结果

    private void OnEnvUpdateBegin()
    {
        // 记录所有状态变化率的快照
        envStateChangeRatesSnapshot.Clear();
        foreach (var (type, state) in stateDict)
        {
            if (state.ChangeRate != 0)
            {
                envStateChangeRatesSnapshot.Add(type, state.ChangeRate);
            }
        }
    }

    private void EnvUpdate()
    {
        ApplyEnvStateChanges(envStateChangeRatesSnapshot);
    }
    #endregion

    #region 状态变化
    /// <summary>
    /// 改变环境状态
    /// </summary>
    /// <param name="stateEnum"></param>
    /// <param name="delta"></param>
    public void ChangeEnvironmentState(EnvironmentStateEnum stateEnum, float delta)
    {
        switch (stateEnum)
        {
            case EnvironmentStateEnum.Electricity:
                ElectricPowerManager.Instance.ChangePower(delta); // 电力变化转发到ElectricPowerManager处理
                break;
            case EnvironmentStateEnum.WaterLevel:
                StateManager.Instance.ChangeWaterLevel(delta); // 水平面变化转发到StateManager处理
                break;
            case EnvironmentStateEnum.HasCable:
            case EnvironmentStateEnum.PressureLevel:
                throw new ArgumentException("修改是否铺设电缆或压强请通过ChangeHasCable/ChangePressureLevel方法");
            default:
                // 没有这个状态不处理
                if (!StateDict.ContainsKey(stateEnum)) return;
                var state = StateDict[stateEnum];
                state.AddValue(delta);
                // 刷新前端显示
                EventManager.Instance.TriggerEvent(EventType.RefreshEnvironmentState, new RefreshEnvironmentStateArgs(PlaceData.placeType, stateEnum)
                {
                    stateValue = state
                });
                break;
        }
    }

    /// <summary>
    /// 改变环境状态的变化率
    /// </summary>
    /// <param name="stateEnum"></param>
    /// <param name="delta"></param>
    public void ChangeEnvironmentStateChangeRate(EnvironmentStateEnum stateEnum, float delta)
    {
        switch (stateEnum)
        {
            case EnvironmentStateEnum.Electricity:
            case EnvironmentStateEnum.WaterLevel:
                // 不做处理
                break;
            case EnvironmentStateEnum.HasCable:
            case EnvironmentStateEnum.PressureLevel:
                throw new ArgumentException("修改是否铺设电缆或压强请通过ChangeHasCableChangeRate/ChangePressureLevelChangeRate方法");
            default:
                // 没有这个状态不处理
                if (!StateDict.ContainsKey(stateEnum)) return;
                var state = StateDict[stateEnum];
                state.AddChangeRate(delta);
                // 刷新前端显示
                EventManager.Instance.TriggerEvent(EventType.RefreshEnvironmentState, new RefreshEnvironmentStateArgs(PlaceData.placeType, stateEnum)
                {
                    stateValue = state
                });
                break;
        }
    }

    public void ApplyEnvStateChanges(Dictionary<EnvironmentStateEnum, float> envStateChanges)
    {
        if (envStateChanges.IsNullOrEmpty()) return;

        foreach (var (state, delta) in envStateChanges)
        {
            ChangeEnvironmentState(state, delta);
        }
    }

    public void SetBrightnessConstValue(string key, float value)
    {
        if (!StateDict.ContainsKey(EnvironmentStateEnum.Brightness)) return;

        var state = StateDict[EnvironmentStateEnum.Brightness];
        state.SetConstValue(key, value);
        // 刷新前端显示
        EventManager.Instance.TriggerEvent(EventType.RefreshEnvironmentState, new RefreshEnvironmentStateArgs(PlaceData.placeType, EnvironmentStateEnum.Brightness)
        {
            stateValue = state
        });
    }
    #endregion

    public override bool CanAddCard(Card card, out string tip)
    {
        tip = string.Empty;
        return true;
    }

    public override void AddCard(Card card)
    {
        // 如果放不下，就新增格子
        if (!base.CanAddCard(card, out _))
        {
            // 暂定每次新增3个格子
            AddSlot(3);
        }

        base.AddCard(card);

        // 如果剩余格子数量小于3个
        if (EmptySlotCount < 3)
        {
            // 暂定每次新增3个格子
            AddSlot(3);
        }
    }

    public override bool CompactCards()
    {
        var hasChanged = base.CompactCards();
        while (Slots.Count - 3 >= 9 && EmptySlotCount - 3 >= 3)
        {
            RemoveSlot(Slots[^1]);
            RemoveSlot(Slots[^1]);
            RemoveSlot(Slots[^1]);
        }
        //if (Window != null) Window.RefreshDisplay();
        return hasChanged;
    }

    public void AddEntity(IEntity entity)
    {
        if (AllEntities.Contains(entity)) return;

        // 设置当前所在地点
        entity.Coordinate.SetLocation(this);
        // 将实体加入实体列表
        AllEntities.Add(entity);
    }

    public void RemoveEntity(IEntity entity)
    {
        if (!AllEntities.Contains(entity)) return;

        entity.Coordinate.SetLocation(null);
        AllEntities.Remove(entity);
    }

    public override void OnAddCard(Card card)
    {
        base.OnAddCard(card);

        if (card is IEntity entity)
        {
            AddEntity(entity);
        }
        else if (card.TryGetComponent<CoordinateComponent>(out var c))
        {
            c.coordinate.SetLocation(this);
            c.coordinate.SetPosition(ClimateRules.IsHeatSource(card.CardId) && Player.Instance.Coordinate.Location == this
                ? Player.Instance.Coordinate.Position : c.initialPosition);
        }
    }

    public override void OnRemoveCard(Card card)
    {
        base.OnRemoveCard(card);

        if (card is IEntity entity)
        {
            RemoveEntity(entity);
        }
        else if (card.TryGetComponent<CoordinateComponent>(out var c))
        {
            c.coordinate.SetLocation(null);
        }
    }
}
