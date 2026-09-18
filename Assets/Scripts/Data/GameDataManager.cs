using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameDataManager
{
    private static GameDataManager instance = new();
    public static GameDataManager Instance => instance;

    public int curLoadIndex; // Legacy editor commands only.
    public string CurLoadName => "GameData" + curLoadIndex;
    public Load CurLoad => new Load(TimeData?.curTime ?? new DateTime(2020, 1, 1), SaveSystem.Current?.skipGuide ?? true);

    private GameDataManager()
    {
        if (SceneManager.GetActiveScene().buildIndex != 0) LoadAllData(0);
    }

    public void LoadSnapshot(SavePayload payload)
    {
        SaveDataContract.Validate(payload);
        JsonManager.Source = payload.files;
        try { LoadAllData(0); }
        finally { JsonManager.Source = null; }
    }

    public void LoadAllData(int index)
    {
        curLoadIndex = index;
        // 玩家背包
        playerBagData = JsonManager.LoadData<PlayerBag>(CurLoadName, "PlayerBag");
        // 上次地点
        lastPlace = JsonManager.LoadData<int>(CurLoadName, "LastPlace");
        // 环境
        environmentBagDataDict = new();
        foreach (PlaceEnum placeType in Enum.GetValues(typeof(PlaceEnum)))
        {
            var env = JsonManager.LoadData<EnvironmentBag>(CurLoadName, placeType.ToString() + "Bag");
            env.SetPlaceType(placeType);
            environmentBagDataDict.Add(placeType, env);
        }
        // 状态数据 
        stateData = JsonManager.LoadData<StateData>(CurLoadName, "State");
        // 音频数据
        audioData = GameSettings.Current.audio;
        // 科技数据
        technologyData = JsonManager.LoadData<TechnologyData>(CurLoadName, "Technology");
        // 装备数据
        equipmentData = JsonManager.LoadData<EquipmentBag>(CurLoadName, "Equipment");
        // 已生成的对话
        generatedChatData = JsonManager.LoadData<GeneratedChatData>(CurLoadName, "GeneratedChatData");
        // 时间数据
        timeData = JsonManager.LoadData<TimeData>(CurLoadName, "TimeData");
        // 窗口数据
        windowsData = JsonManager.LoadData<WindowsData>(CurLoadName, "WindowsData");
        // 探索移动额外消耗数据
        behaviourExtraEffectsData = JsonManager.LoadData<BehaviourExtraEffectsData>(CurLoadName, "BehaviourExtraEffectsData");
        // 全局数据
        globalData = JsonManager.LoadData<GlobalData>(CurLoadName, "GlobalData");
        // 玩家数据
        playerData = JsonManager.LoadData<PlayerData>(CurLoadName, "PlayerData");
        // 游戏事件数据
        gameEventData = JsonManager.LoadData<GameEventData>(CurLoadName, "GameEventData");
        // 电力数据
        electricPowerData = JsonManager.LoadData<ElectricPowerData>(CurLoadName, "ElectricPowerData");
        // 计数数据
        countData = JsonManager.LoadData<CountData>(CurLoadName, "CountData");
    }

    public void SaveAllData() => SaveSystem.RequestSave(SaveKind.Manual);

    public SavePayload CaptureSnapshot()
    {
        var payload = new SavePayload();
        JsonManager.Capture = payload.files;
        try { CaptureAllData(); }
        finally { JsonManager.Capture = null; }
        SaveDataContract.Validate(payload);
        return payload;
    }

    private void CaptureAllData()
    {
        // 玩家背包
        SavePlayerBag();
        // 上次地点
        SaveLastPlace();
        // 环境
        SaveEnvironmentBag();
        // 状态
        SaveStateData();
        // 音频数据
        // Audio preferences are global and excluded from timeline snapshots; compatibility key only.
        JsonManager.SaveData(new AudioData(), CurLoadName, "Audio");
        // 科技数据
        SaveTechnologyData();
        // 装备数据
        SaveEquipmentData();
        // 已生成的对话
        SaveGeneratedChatData();
        // 时间数据
        SaveTimeData();
        // 窗口数据
        SaveWindowsData();
        // 探索移动额外消耗数据
        SaveBehaviourExtraEffectsData();
        // 全局数据
        SaveGlobalData();
        // 玩家数据
        SavePlayerData();
        // 游戏事件数据
        SaveGameEventData();
        // 保存电力数据
        SaveElectricPowerData();
        // 保存计数数据
        SaveCountData();

    }

    #region 存档数据

    private LoadData loadData;

    public LoadData LoadData => loadData;

    public void SaveLoadData()
    {
        JsonManager.SaveData(loadData, "LoadData", "LoadData");
    }

    public void LoadLoadData()
    {
        loadData = JsonManager.LoadData<LoadData>("LoadData", "LoadData");
    }

    public void CreateNewLoad(int index, bool skipGuide)
    {
        loadData.loads[index] = new Load(new DateTime(2020, 1, 1, 0, 0, 0), skipGuide);
        SaveLoadData();
    }
    public void ClearLoadData()
    {
        loadData= new LoadData();
    }

    #endregion

    #region 玩家背包

    private PlayerBag playerBagData;

    public PlayerBag PlayerBagData => playerBagData;

    public void SavePlayerBag()
    {
        JsonManager.SaveData(playerBagData, CurLoadName, "PlayerBag");
    }

    public void LoadPlayerBag()
    {
        playerBagData = JsonManager.LoadData<PlayerBag>(CurLoadName, "PlayerBag");
    }

    #endregion

    #region 地点

    // 最后一次玩家出现时的地点
    private int lastPlace = -1;

    public PlaceEnum LastPlace => (PlaceEnum)lastPlace;

    public void SaveLastPlace()
    {
        JsonManager.SaveData(GameManager.Instance.CurEnvironmentBag.PlaceData.placeType, CurLoadName, "LastPlace");
    }

    public void LoadLastPlace()
    {
        lastPlace = JsonManager.LoadData<int>(CurLoadName, "LastPlace");
    }

    #endregion

    #region 环境背包

    private Dictionary<PlaceEnum, EnvironmentBag> environmentBagDataDict = new();

    public Dictionary<PlaceEnum, EnvironmentBag> EnvironmentBagDataDict => environmentBagDataDict;

    public EnvironmentBag GetEnvironmentBagDataByPlace(PlaceEnum place)
    {
        return environmentBagDataDict[place];
    }

    public void LoadEnvironmentBag()
    {
        environmentBagDataDict = new();
        foreach (PlaceEnum place in Enum.GetValues(typeof(PlaceEnum)))
        {
            environmentBagDataDict.Add(place,
                JsonManager.LoadData<EnvironmentBag>(CurLoadName, place.ToString() + "Bag"));
        }
    }

    /// <summary>
    /// 保存所有环境背包的数据
    /// </summary>
    public void SaveEnvironmentBag()
    {
        foreach (var (place, bag) in environmentBagDataDict)
        {
            //EnvironmentBag data = new()
            //{
            //    init = true,
            //    // 保存掉落列表
            //    disposableDropList = bag.DisposableDropList,
            //    repeatableDropList = bag.RepeatableDropList,
            //    // 保存背包中的卡牌
            //    cardSlots = new(),
            //    // 保存铺设电缆状态
            //    hasCable = bag.HasCable,
            //    // 保存压强状态
            //    pressureLevel = bag.PressureLevel,
            //    // 保存其他状态
            //    environmentStateDict = bag.StateDict
            //};
            //foreach (var slot in bag.Slots)
            //{
            //    //data.cardSlots.Add(new() { cardList = slot.Cards });
            //    data.cardSlots.Add(new List<Card>(slot.Cards));
            //}

            JsonManager.SaveData(bag, CurLoadName, place.ToString() + "Bag");
        }
    }

    #endregion

    #region 音频

    private AudioData audioData;
    public AudioData AudioData => audioData;

    public UnityEvent onBGMVolumeChanged = new();

    public void SetMasterVolume(float volume)
    {
        audioData.masterVolume = Mathf.Clamp01(volume);
        GameSettings.Save();
        onBGMVolumeChanged?.Invoke();
    }

    public void SetBGMVolume(float volume)
    {
        audioData.bgmVolume = Mathf.Clamp01(volume);
        GameSettings.Save();
        onBGMVolumeChanged?.Invoke();
    }

    public void SetSFXVolume(float volume)
    {
        audioData.sfxVolume = Mathf.Clamp01(volume);
        GameSettings.Save();
    }

    public void SaveAudioData()
    {
        GameSettings.Save();
    }

    public void LoadAudioData()
    {
        audioData = GameSettings.Current.audio;
    }

    #endregion

    #region 科技

    private TechnologyData technologyData;

    public TechnologyData TechnologyData => technologyData;

    public void SaveTechnologyData()
    {
        technologyData = new()
        {
            isStudying = TechnologyManager.Instance.IsStudying,
            studyQueue = TechnologyManager.Instance.StudyQueue,
            studyProgressDict = TechnologyManager.Instance.StudyProgressDict
        };
        JsonManager.SaveData(technologyData, CurLoadName, "Technology");
    }

    public void LoadTechnologyData()
    {
        technologyData = JsonManager.LoadData<TechnologyData>(CurLoadName, "Technology");
    }

    #endregion

    #region 装备

    private EquipmentBag equipmentData;
    public EquipmentBag EquipmentData => equipmentData;

    public void SaveEquipmentData()
    {
        //EquipmentBag bag = GameManager.Instance.EquipmentBag;
        //equipmentData = new()
        //{
        //    cardSlots = new()
        //};
        //foreach (var slot in bag.Slots)
        //{
        //    equipmentData.cardSlots.Add(new List<Card>(slot.Cards));
        //}

        JsonManager.SaveData(equipmentData, CurLoadName, "Equipment");
    }

    public void LoadEquipmentData()
    {
        equipmentData = JsonManager.LoadData<EquipmentBag>(CurLoadName, "Equipment");
    }

    #endregion

    #region 已生成的对话

    private GeneratedChatData generatedChatData;
    public GeneratedChatData GeneratedChatData => generatedChatData;

    public void SaveGeneratedChatData()
    {
        generatedChatData.ParagraphConditionsToTrigger =
            new List<ParagraphData>(ChatConditionManager.Instance.ParagraphConditionsToTrigger);
        generatedChatData.GeneratedChatDataList = new List<ChatData>(ChatManager.Instance.GeneratedChatDataList);
        generatedChatData.ParagraphToTriggeer = new List<string>(ChatManager.Instance.ParagraphToTriggeer);
        generatedChatData.ChoosedChatData = ChatManager.Instance.ChoosedChatData;
        generatedChatData.inParagraph = ChatManager.Instance.inParagraph;
        generatedChatData.InterruptParagraphData = ChatManager.Instance.InterruptParagraphData;
        generatedChatData.Choosing = ChatManager.Instance.Choosing;
        generatedChatData.CurrentNodeData = ReadChatParagraph.Instance.CurNode;
        generatedChatData.CurrentGraphData = ReadChatParagraph.Instance.CurGraphData;
        if (!generatedChatData.init) generatedChatData.init = true;
        JsonManager.SaveData(generatedChatData, CurLoadName, "GeneratedChatData");
    }

    public void LoadGeneratedChatData()
    {
        generatedChatData = JsonManager.LoadData<GeneratedChatData>(CurLoadName, "GeneratedChatData");
        //ChatManager.Instance.GeneratedChatDataList = generatedChatData.GeneratedChatDataList;
    }

    #endregion

    #region 游戏时间数据

    private TimeData timeData;
    public TimeData TimeData => timeData;

    public void SaveTimeData()
    {
        timeData.init = true;
        timeData.curTime = TimeManager.Instance.CurTime;
        timeData.curIntervel = TimeManager.Instance.CurInterval;
        JsonManager.SaveData(timeData, CurLoadName, "TimeData");
    }

    public void LoadGame()
    {
        timeData = JsonManager.LoadData<TimeData>(CurLoadName, "TimeData");
    }

    #endregion

    #region 状态数据

    private StateData stateData;
    public StateData StateData => stateData;

    public void LoadStateData()
    {
        stateData = JsonManager.LoadData<StateData>(CurLoadName, "State");
    }

    public void SaveStateData()
    {
        stateData = new StateData
        {
            init = true,
            waterLevel = StateManager.Instance.WaterLevel,
            playerState = StateManager.Instance.PlayerStateDict,
        };
        JsonManager.SaveData(stateData, CurLoadName, "State");
    }

    #endregion

    #region 窗口数据

    private WindowsData windowsData;

    public WindowsData WindowsData => windowsData;

    public void SaveWindowsData()
    {
        windowsData = new();

        windowsData.currentPresetIndex = WindowsManager.Instance.CurrentPresetIndex;

        windowsData.unlockedShortcuts = WindowsManager.Instance.GetUnlockedShortcuts();

        var f = WindowsManager.Instance.GetCurrentFocusedWindow();
        windowsData.focusedWindow = f == null ? string.Empty : f.AppName;

        foreach (var (name, window) in WindowsManager.Instance.GetOpenedWindows())
        {
            if (window.IgnoreThisWhenSave) continue;

            var rectTransform = window.transform as RectTransform;
            windowsData.openedWindows.Add(name, new()
            {
                position = rectTransform.anchoredPosition,
                scale = rectTransform.localScale,
                sizeDelta = rectTransform.sizeDelta,
                lastState = window.LastState,
                state = window.State,
                lastPosition = window.LastPosition,
                lastSizeDelta = window.LastSizeDelta,
                isModal = window.IsModal,
            });
        }

        JsonManager.SaveData(windowsData, CurLoadName, "WindowsData");
    }

    #endregion

    #region 探索移动额外消耗
    private BehaviourExtraEffectsData behaviourExtraEffectsData;

    public BehaviourExtraEffectsData BehaviourExtraEffectsData => behaviourExtraEffectsData;

    public void SaveBehaviourExtraEffectsData()
    {
        behaviourExtraEffectsData = new()
        {
            init = true,
            moveExtraEffects = MoveExploreManager.Instance.MoveExtraEffects,
            moveToWaterExtraEffects = MoveExploreManager.Instance.MoveToWaterExtraEffects,
            exploreExtraEffects = MoveExploreManager.Instance.ExploreExtraEffects,
            exploreInWaterExtraEffects = MoveExploreManager.Instance.ExploreInWaterExtraEffects
        };
        JsonManager.SaveData(behaviourExtraEffectsData, CurLoadName, "BehaviourExtraEffectsData");
    }
    #endregion

    #region 全局数据
    private GlobalData globalData;

    public GlobalData GlobalData => globalData;

    public void SaveGlobalData()
    {
        JsonManager.SaveData(globalData, CurLoadName, "GlobalData");
    }
    #endregion

    #region 玩家数据
    private PlayerData playerData;

    public PlayerData PlayerData => playerData;

    public void SavePlayerData()
    {
        playerData = new()
        {
            moveSpeedMultiplier = Player.Instance.MoveSpeedMultiplier,
            coordinate = Player.Instance.Coordinate,
        };
        JsonManager.SaveData(playerData, CurLoadName, "PlayerData");
    }
    #endregion

    #region 游戏事件数据
    private GameEventData gameEventData;

    public GameEventData GameEventData => gameEventData;

    public void SaveGameEventData()
    {
        gameEventData = new()
        {
            init = true,
            trendValue = GameEventManager.Instance.TrendValue,
            invasionConfig = GameEventManager.Instance.InvasionEventConfig,
            allEvents = GameEventManager.Instance.AllEvents,
        };
        JsonManager.SaveData(gameEventData, CurLoadName, "GameEventData");
    }
    #endregion

    #region 电力数据
    private ElectricPowerData electricPowerData;

    public ElectricPowerData ElectricPowerData => electricPowerData;

    public void SaveElectricPowerData()
    {
        electricPowerData = new()
        {
            init = true,
            connectedAppliances = ElectricPowerManager.Instance.SortedConnectedAppliances,
            power = ElectricPowerManager.Instance.Power
        };
        JsonManager.SaveData(electricPowerData, CurLoadName, "ElectricPowerData");
    }
    #endregion

    #region 计数数据
    private CountData countData;

    public CountData CountData => countData;

    public void SaveCountData()
    {
        if (CountManager.Instance == null) return;
        
        countData = new()
        {
            init = true,
            counts = new Dictionary<string, int>(CountManager.Instance.Counts)
        };
        JsonManager.SaveData(countData, CurLoadName, "CountData");
    }

    public void LoadCountData()
    {
        countData = JsonManager.LoadData<CountData>(CurLoadName, "CountData");
    }
    #endregion
}