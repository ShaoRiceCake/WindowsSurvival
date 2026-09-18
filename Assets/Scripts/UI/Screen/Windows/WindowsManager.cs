using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WindowsManager : MonoBehaviour
{
    private static WindowsManager instance;
    public static WindowsManager Instance => instance;

    [SerializeField] private ShortcutsController shortcutsController; // 管理快捷方式

    [SerializeField] private WindowGroup windowGroup; // 所有窗口作为其子物体，由该脚本控制窗口的渲染顺序

    [SerializeField] private RectTransform hoverTipLayer;
    [SerializeField] private RectTransform tempCardSlotLayer;
    [SerializeField] private RectTransform floatingCardSlotLayer;
    [SerializeField] private RectTransform chatTipLayer;

    public RectTransform HoverTipLayer => hoverTipLayer;
    public RectTransform TempCardSlotLayer => tempCardSlotLayer;
    public RectTransform FloatingTipLayer => floatingCardSlotLayer;
    public RectTransform ChatTipLayer => chatTipLayer;

    [SerializeField] private RectTransform desktop;

    public RectTransform Desktop => desktop;

    private Dictionary<string, WindowBase> openedWindows = new(); // 当前所有打开的窗口，最小化的窗口也算打开的
    private WindowBase currentFocusedWindow; // 当前持有焦点的窗口，可能是openWindows[0]，可能是null

    [SerializeField] private HoverableButton settingsButton;
    [SerializeField] private HoverableButton restButton;

    private HoverTipController restButtonTipController;

    [SerializeField] private List<HoverableButton> presetButtons = new(); // 预设按钮
    [SerializeField] private List<WindowsLayoutPreset> presets = new(); // 预设配置

    private Dictionary<string, PositionAndSizeDelta> defaultPositionAndSizeDeltas = new();

    private int currentPresetIndex;
    public int CurrentPresetIndex => currentPresetIndex;

    private void Awake()
    {
        instance = this;
        pointerData = new(EventSystem.current);
    }

    #region Start
    private void Start()
    {
        settingsButton.onClick.AddListener(() => SaveHubUI.Instance.ShowGameMenu());

        // 初始化休息按钮
        InitRestButton();

        // 初始化预设按钮
        InitPresetButtons();

        // 恢复窗口
        LoadWindowsData();

        // 应用预设
        currentPresetIndex = GameDataManager.Instance.WindowsData.currentPresetIndex;
        ApplyPreset(currentPresetIndex);

        EventManager.Instance.AddListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, OnChangeEnv);
        EventManager.Instance.AddListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, OnChangeWaterLevel);
    }

    private void OnDestroy()
    {
        EventManager.Instance.RemoveListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, OnChangeEnv);
        EventManager.Instance.RemoveListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, OnChangeWaterLevel);
    }

    private void OnChangeEnv(EnvironmentBag env) => DisplayRestButton();

    private void OnChangeWaterLevel(RefreshEnvironmentStateArgs args)
    {
        if (args.stateEnum != EnvironmentStateEnum.WaterLevel) return;

        DisplayRestButton();
    }

    private void InitRestButton()
    {
        restButton.onClick.AddListener(HandleRestOnTheGround);

        restButtonTipController = restButton.GetComponent<HoverTipController>();
        if (restButtonTipController == null)
            restButtonTipController = restButton.gameObject.AddComponent<HoverTipController>();

        DisplayRestButton();
    }

    private void HandleRestOnTheGround()
    {
        var window = (OpenWindow("TimeSelect", true) as TimeSelectWindow);
        window.SetTimeRange(1, 24 * 60); // 休息 1 分钟到 24 小时
        //window.canConfirm = StateManager.Instance.CanRestOnTheGround;
        window.onConfirm = StateManager.Instance.RestOnTheGround;
        window.getConfirmEffects = (t) =>
        {
            Dictionary<PlayerStateEnum, float> p = null;
            float sobrietyChange = t / TimeManager.SETTLEMENT_INTERVAL * StateManager.SOBRIETY_CHANGE_RATE_WHILE_RESTING_ON_THE_GROUND;
            if (sobrietyChange > 0)
            {
                p = new()
                    {
                        { PlayerStateEnum.Sobriety, sobrietyChange }
                    };
            }
            return ($"休息 {t} 分钟", t, p, null);
        };
    }

    private void DisplayRestButton()
    {
        restButton.Interactable = StateManager.Instance.CanRestOnTheGround(out var reason);

        if (restButton.Interactable)
        {
            restButton.text.color = ColorManager.White;
            restButtonTipController.SetTip("在地上休息");
        }
        else
        {
            restButton.text.color = ColorManager.DarkGrey;
            restButtonTipController.SetTip(reason);
        }
    }

    private void InitPresetButtons()
    {
        for (int i = 0; i < presetButtons.Count; i++)
        {
            var button = presetButtons[i];
            int index = i; // 创建局部变量拷贝
            string buttonText = button.GetComponentInChildren<Text>().text; // 提前捕获文本

            button.onClick.AddListener(() =>
            {
                var window = (OpenWindow("Custom", true) as CustomWindow);
                window.SetContent("是否要应用窗口布局预设" + buttonText + "？"); // 使用局部变量
                window.ConfirmAndCancel(() =>
                {
                    ApplyPreset(index); // 使用局部变量 index
                    ResetWindowsPositionAndSizeDelta();
                });
            });
        }
    }

    private void LoadWindowsData()
    {
        foreach (var (name, data) in GameDataManager.Instance.WindowsData.openedWindows)
        {
            // 如果窗口不在closedGroup里，则创建实例
            if (!windowGroup.TeyGetWindowInClosedGroup(name, out var window))
            {
                // 实例化窗口对象
                GameObject windowPrefab = Resources.Load<GameObject>($"Prefabs/UI/Windows/{name}Window");
                window = Instantiate(windowPrefab, windowGroup.transform).GetComponent<WindowBase>();
            }

            window.InitFromWindowData(data);

            if (data.isModal)
                windowGroup.SetModal(window);
            else if (data.state == WindowState.Minimized)
                windowGroup.SetMinimized(window);
            else
                windowGroup.SetOpened(window);

            openedWindows.Add(name, window);

            shortcutsController.SetOpened(name, true);
        }

        FocusWindow(GameDataManager.Instance.WindowsData.focusedWindow);
    }

    /// <summary>
    /// 应用窗口布局预设
    /// </summary>
    /// <param name="index"></param>
    private void ApplyPreset(int index)
    {
        currentPresetIndex = index;

        var preset = presets[index];

        defaultPositionAndSizeDeltas = new()
        {
            { "State", preset.stateWindow },
            { "Camera", preset.cameraWindow },
            //{ "Chat", preset.chatWindow },
            //{ "Craft", preset.craftWindow },
            { "Details", preset.detailsWindow },
            { "EnvironmentBag", preset.envBagWindow },
            { "PlayerBag", preset.playerBagWindow },
            { "Equipment", preset.equipmentWindow },
        };
    }

    private void ResetWindowsPositionAndSizeDelta()
    {
        var focus = currentFocusedWindow;
        foreach (var appName in GetUnlockedShortcuts())
        {
            #region 临时
            if (appName == "Rest") continue;
            #endregion

            if (defaultPositionAndSizeDeltas.ContainsKey(appName))
                OpenWindow(appName).ForceSetPositionAndSizeDelta(defaultPositionAndSizeDeltas[appName]);
        }

        FocusWindow(focus);
    }
    #endregion

    public WindowBase OpenWindow(string appName, bool isModal = false)
    {
        WindowBase window;
        // 窗口没有打开
        if (!IsWindowOpen(appName))
        {
            // 如果窗口不在closedGroup里，则创建实例
            if (!windowGroup.TeyGetWindowInClosedGroup(appName, out window))
            {
                // 实例化窗口对象
                GameObject windowPrefab = Resources.Load<GameObject>($"Prefabs/UI/Windows/{appName}Window");
                window = Instantiate(windowPrefab, windowGroup.transform).GetComponent<WindowBase>();
                windowGroup.SetClosed(window);
            }

            // 添加到已打开窗口中
            openedWindows.Add(appName, window);

            // 底边栏的快捷方式变亮
            shortcutsController.SetOpened(appName, true);

            // 设置窗口的默认位置
            if (defaultPositionAndSizeDeltas.ContainsKey(appName))
            {
                window.SetPositionAndSizeDelta(defaultPositionAndSizeDeltas[appName]);
            }

            if (SoundManager.Instance != null)
                SoundManager.Instance.PlaySound("万能泡泡音", true);
        }
        else
        {
            window = openedWindows[appName];
        }

        if (window.IsPlayingAnim) return window;

        // 设置是否模态
        window.SetModal(isModal);

        // 打开窗口
        window.Open();
        
        // 让窗口获得焦点
        FocusWindow(window);
        
        return window;
    }

    public void CloseWindow(string appName)
    {
        // 窗口必须已经打开
        if (!IsWindowOpen(appName)) return;

        WindowBase window = openedWindows[appName];
        openedWindows.Remove(appName);

        // 设置为未聚焦
        window.SetFocused(false);

        window.Close();

        // 将窗口从渲染层级中移除
        windowGroup.SetClosed(window);

        // 底边栏的快捷方式变暗
        shortcutsController.SetOpened(appName, false);

        // 设置获得焦点的窗口是渲染层级最靠前的窗口
        // 或者是null
        FocusWindow(windowGroup.GetTheFrontWindow());
    }

    //public void MaximizeWindow(string appName)
    //{
    //    if (!IsWindowOpen(appName)) return;

    //    WindowBase window = openedWindows[appName];
    //    // 最大化窗口
    //    window.Maximize();

    //    // 让窗口获得焦点
    //    FocusWindow(window);
    //}

    public void MinimizeWindow(string appName)
    {
        if (!IsWindowOpen(appName)) return;

        WindowBase window = openedWindows[appName];

        if (window.IsPlayingAnim) return;
        // 最小化窗口
        window.Minimize(shortcutsController[appName].transform);

        // 将window暂停渲染
        windowGroup.SetMinimized(window);

        // 设置获得焦点的窗口是渲染层级最靠前的窗口
        // 或者是null
        FocusWindow(windowGroup.GetTheFrontWindow());
    }

    public void FocusWindow(WindowBase window)
    {
        // 如果当前窗口已经获取焦点，则直接返回
        if (IsWindowFocused(window)) return;

        // 设置window的聚焦状态
        if (currentFocusedWindow != null)
            currentFocusedWindow.SetFocused(false);

        if (window != null)
            window.SetFocused(true);

        currentFocusedWindow = window;

        // 调整window的渲染顺序为最高
        windowGroup.SetFocused(currentFocusedWindow);

        if (window != null)
            // 选中底边栏中的快捷方式
            shortcutsController.SelectAppShortcut(window.AppName);
        else
            shortcutsController.ClearSelection();
    }

    public void FocusWindow(string appName)
    {
        if (string.IsNullOrEmpty(appName)) return;

        if (!IsWindowOpen(appName)) return;

        FocusWindow(openedWindows[appName]);
    }

    public WindowBase GetCurrentFocusedWindow() => currentFocusedWindow;
    public void SetSettingsOpen(bool value)
    {
        if (shortcutsController != null && shortcutsController.isActiveAndEnabled)
            shortcutsController.SetSettingsOpen(value);
    }

    public bool IsWindowOpen(string appName)
    {
        return openedWindows.ContainsKey(appName);
    }

    public bool IsWindowFocused(WindowBase window)
    {
        return currentFocusedWindow == window;
    }

    public void UnlockShortcut(string appName, bool blink = true)
    {
        shortcutsController.SetLocked(appName, false, blink);
    }

    public List<string> GetUnlockedShortcuts()
    {
        return shortcutsController.GetUnlockedShortcuts();
    }

    public Dictionary<string, WindowBase> GetOpenedWindows(bool excludeMinimized = false)
    {
        if (!excludeMinimized) return new(openedWindows);

        var result = new Dictionary<string, WindowBase>();

        foreach (var (name, window) in openedWindows)
        {
            if (window.State != WindowState.Minimized) result.Add(name, window);
        }

        return result;
    }

    public bool TryGetOpenedWindow(string appName, out WindowBase window, bool excludeMinimized = false)
    {
        if (!openedWindows.TryGetValue(appName, out window)) return false;

        if (excludeMinimized && window.State == WindowState.Minimized)
        {
            window = null;
            return false;
        }

        return true;
    }

    PointerEventData pointerData;
    List<RaycastResult> results;
    private void Update()
    {
        // A modal owns the entire pointer interaction, including window focusing.
        if (SaveHubUI.Instance != null && SaveHubUI.Instance.IsOpen ||
            SaveRuntime.Instance != null && SaveRuntime.Instance.TransitionActive) return;
        if (Input.GetMouseButtonDown(0)) // 检测鼠标左键点击
        {
            pointerData.position = Input.mousePosition;
            
            // 创建接收结果的列表
            results = new List<RaycastResult>();

            // 执行射线检测
            EventSystem.current.RaycastAll(pointerData, results);

            // 处理检测结果
            if (results.Count > 0)
            {
                foreach (var result in results)
                {
                    if (result.gameObject.name == "Modal") return;
                    if (result.gameObject.TryGetComponent<WindowBase>(out var window)
                        && window.State != WindowState.Closed
                        && window.State != WindowState.Minimized)
                    {
                        FocusWindow(window);
                        return;
                    }
                }
            }
        }
    }
}
