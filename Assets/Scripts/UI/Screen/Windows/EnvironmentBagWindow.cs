using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnvironmentBagWindow : BagWindow
{
    [SerializeField] private UIStateSlider discoveryDegreeSlider;   // 探索度显示
    [SerializeField] private Text placeNameText;                    // 环境名称
    [SerializeField] private Image environmentImage;                // 环境图片
    [SerializeField] private HoverableButton exploreButton;         // 探索按钮
    [SerializeField] private HoverableButton modificationsButton;
    [SerializeField] private RectTransform stateLayout;
    [SerializeField] private RectTransform envCardTransform;

    [SerializeField] private UIStateToggle hasCabble;               // 是否铺设电缆
    [SerializeField] private UIPressureLevel pressureLevel;         // 压强等级
    [HideInInspector] public Dictionary<EnvironmentStateEnum, UIStateSlider> continuousValueStates = new(); // 环境状态显示

    [SerializeField] private Slider currentCoordSlider;
    [SerializeField] private Slider targetCoordSlider;
    [SerializeField] private HoverableButton moveLeftButton;
    [SerializeField] private HoverableButton moveRightButton;
    [SerializeField] private HoverableButton executeMoveButton;
    [SerializeField] private Text targetPosition;
    [SerializeField] private Text currentPosition;
    [SerializeField] private Text deltaPosition;
    [SerializeField] private Image fillBetween;

    private const float MOVE_DIST_RESOLUTION = 0.5f; // 移动距离分辨率
    private float valueTransition = 0.3f;

    private HoverTipController exploreTipController;
    private HoverTipController moveTipController;

    private EnvironmentBag CurEnv => GameManager.Instance.CurEnvironmentBag;
    private float TargetPosition => targetCoordSlider.value * MOVE_DIST_RESOLUTION;
    private float DeltaPosition => TargetPosition - Player.Instance.Coordinate.Position;

    protected override void Awake()
    {
        base.Awake();

        // 探索按钮事件
        exploreButton.onClick.AddListener(Explore);
        if (modificationsButton != null) modificationsButton.onClick.AddListener(() =>
            (WindowsManager.Instance.OpenWindow("Details") as DetailsWindow).DisplayEnvironment(CurEnv));

        // 探索消耗显示
        exploreTipController = exploreButton.gameObject.AddComponent<HoverTipController>();
        exploreTipController.onPointerEnter = () =>
        {
            if (MoveExploreManager.Instance.CanMoveExplore())
            {
                var (desc, time, playerStateChanges) = MoveExploreManager.Instance.GetExploreEffects();
                desc = (CurEnv.DiscoveryDegree >= 1 ? "深入探索该地点" : "探索该地点") + desc;
                exploreTipController.SetTip(desc, time, playerStateChanges, null);
            }
            else
                exploreTipController.SetTip("身上太重了，无法探索");
        };

        // 移动消耗显示
        moveTipController = executeMoveButton.gameObject.AddComponent<HoverTipController>();
        moveTipController.onPointerEnter = () =>
        {
            if (MoveExploreManager.Instance.CanMoveExplore())
            {
                var (desc, time, playerStateChanges) = MoveExploreManager.Instance.GetMoveEffects(TargetPosition);
                desc = $"前往坐标 {ColorManager.ColorizeNumber(TargetPosition, ColorManager.Cyan)} 处" + desc;
                moveTipController.SetTip(desc, time, playerStateChanges, null);
            }
            else
                moveTipController.SetTip("身上太重了，无法移动");
        };

        // 当前坐标显示
        currentCoordSlider.onValueChanged.AddListener((_) =>
        {
            currentPosition.text = Player.Instance.Coordinate.Position.ToString("0.0");
            FillBetween();
        });

        // 选择距离
        targetCoordSlider.onValueChanged.AddListener((_) =>
        {
            targetPosition.text = TargetPosition.ToString("0.0");
            deltaPosition.text = DeltaPosition.ToString("0.0");
            FillBetween();
            if (playSound)
                SoundManager.Instance.PlaySound("简单点击_01", true);
        });
        moveLeftButton.onClick.AddListener(() =>
        {
            targetCoordSlider.value--;
        });
        moveRightButton.onClick.AddListener(() =>
        {
            targetCoordSlider.value++;
        });

        // 执行移动
        executeMoveButton.onClick.AddListener(ExecuteMove);

        // 注册状态
        foreach (Transform c in stateLayout)
        {
            if (c.TryGetComponent<UIStateSlider>(out var s))
                continuousValueStates.Add((EnvironmentStateEnum)Enum.Parse(typeof(EnvironmentStateEnum), c.name), s);
        }

        // 注册地点改变事件
        EventManager.Instance.AddListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, DisplayBag);
        // 注册环境状态变化事件
        EventManager.Instance.AddListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, OnEnvironmentStateChanged);
        // 注册负重变化事件
        EventManager.Instance.AddListener<PlayerStateEnum>(EventType.RefreshPlayerState, OnLoadChanged);
        // 玩家移动
        EventManager.Instance.AddListener(EventType.PlayerMove, RefreshPlayerPosition);
    }

    private void OnDestroy()
    {
        EventManager.Instance.RemoveListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, DisplayBag);
        EventManager.Instance.RemoveListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, OnEnvironmentStateChanged);
        EventManager.Instance.RemoveListener<PlayerStateEnum>(EventType.RefreshPlayerState, OnLoadChanged);
        EventManager.Instance.RemoveListener(EventType.PlayerMove, RefreshPlayerPosition);
    }

    /// <summary>
    /// 监听负重变化，负重变化会导致探索按钮的提示变化
    /// </summary>
    private void OnLoadChanged(PlayerStateEnum state)
    {
        if (state != PlayerStateEnum.Load) return;

        DisplayDiscoveryDegree(CurEnv.DiscoveryDegree, CurEnv.ExploreCompleted, true);
        executeMoveButton.Interactable = MoveExploreManager.Instance.CanMoveExplore();
    }

    protected override void Init()
    {
        DisplayBag(GameManager.Instance.CurEnvironmentBag);
    }

    /// <summary>
    /// 探索
    /// </summary>
    private void Explore()
    {
        MoveExploreManager.Instance.HandleExplore((droppedCards, tip) =>
        {
            AnimationManager.Instance.PlayDropCards(envCardTransform, () =>
            {
                if (droppedCards.IsNullOrEmpty())
                {
                    AnimationManager.Instance.ShowFloatingTipAbove(exploreButton.transform, "地点资源缺乏，什么都没找到", 1.4f);
                    SoundManager.Instance.PlaySound("错误提示");
                    return;
                }

                SoundManager.Instance.PlaySound("抽卡", true);
                AnimationManager.Instance.ShowFloatingTipAbove(exploreButton.transform, tip, 1.4f);
                DisplayDiscoveryDegree(CurEnv.DiscoveryDegree, CurEnv.ExploreCompleted, true);
                GameManager.Instance.AddCardsWithTween(droppedCards, false, envCardTransform.position);
            });
        });
    }

    public override void DisplayBag(Bag bag)
    {
        base.DisplayBag(bag);

        var env = bag as EnvironmentBag;

        foreach (var s in continuousValueStates.Values)
        {
            s.gameObject.SetActive(false);
        }

        // 压强都显示
        pressureLevel.SetValue(env.PressureLevel);

        // 是否铺设电缆都显示
        hasCabble.SetValue(env.HasCable);

        // 铺设电缆才显示电力
        if (env.HasCable)
        {
            continuousValueStates[EnvironmentStateEnum.Electricity].gameObject.SetActive(true);
            continuousValueStates[EnvironmentStateEnum.Electricity].SetValue(ElectricPowerManager.Instance.Power, false);
        }

        // 在飞船内显示水平面高度
        if (env.PlaceData.isInSpacecraft)
        {
            continuousValueStates[EnvironmentStateEnum.WaterLevel].gameObject.SetActive(true);
            continuousValueStates[EnvironmentStateEnum.WaterLevel].SetValue(StateManager.Instance.WaterLevel, false);
        }

        // 其他状态显示
        foreach (var (state, value) in env.StateDict)
        {
            if (!continuousValueStates.ContainsKey(state)) continue;
            continuousValueStates[state].gameObject.SetActive(true);
            continuousValueStates[state].SetValue(value, false);
        }

        MonoUtility.UpdateLayoutSize(stateLayout.GetComponent<VerticalLayoutGroup>());

        // 显示环境名称
        placeNameText.text = $"{env.PlaceData.placeName}";

        // 探索事件
        DisplayDiscoveryDegree(env.DiscoveryDegree, env.ExploreCompleted, false);

        // 显示图片
        environmentImage.sprite = env.PlaceData.placeImage;
        environmentImage.SetNativeSize();

        // 显示坐标系
        DisplayCoordinateSystem();

        // 显示玩家位置
        DisplayPlayerPosition(false);
    }

    /// <summary>
    /// 单个环境状态变化UI刷新
    /// </summary>
    private void OnEnvironmentStateChanged(RefreshEnvironmentStateArgs args)
    {
        // 不是当前地点的值变化不显示
        if (args.place != CurEnv.PlaceData.placeType) return;

        switch (args.stateEnum)
        {
            case EnvironmentStateEnum.HasCable:
                hasCabble.SetValue(args.hasCable);
                if (continuousValueStates.TryGetValue(EnvironmentStateEnum.Electricity, out var power))
                {
                    power.gameObject.SetActive(args.hasCable);
                    if (args.hasCable) power.SetValue(ElectricPowerManager.Instance.Power, false);
                    MonoUtility.UpdateLayoutSize(stateLayout.GetComponent<VerticalLayoutGroup>());
                }
                break;
            case EnvironmentStateEnum.PressureLevel:
                pressureLevel.SetValue(args.pressureLevel);
                break;
            default:
                // 不存在这个状态不显示
                if (!continuousValueStates.ContainsKey(args.stateEnum)) return;
                continuousValueStates[args.stateEnum].GetComponent<UIStateSlider>().SetValue(args.stateValue, true);
                break;
        }
    }

    private void DisplayDiscoveryDegree(float degree, bool completed, bool playAnim)
    {
        // 显示探索度
        discoveryDegreeSlider.SetValue(degree, 1, playAnim);

        var text = exploreButton.text;
        if (completed)
        {
            exploreButton.image.gameObject.SetActive(false);
            exploreButton.Interactable = false;
            text.text = "完成";
            text.color = ColorManager.Cyan;

            // 不再显示探索提示
            exploreTipController.enabled = false;
        }
        else if (degree == 1)
        {
            // 深入探索
            exploreButton.image.gameObject.SetActive(true);
            exploreButton.Interactable = true;
            text.text = "深探";
            text.color = ColorManager.White;

            exploreTipController.enabled = true;
        }
        else
        {
            exploreButton.image.gameObject.SetActive(true);
            exploreButton.Interactable = true;
            text.text = "探索";
            text.color = ColorManager.White;

            exploreTipController.enabled = true;
        }

        // 按钮是否能够交互
        exploreButton.Interactable = exploreButton.Interactable && MoveExploreManager.Instance.CanMoveExplore();
    }

    /// <summary>
    /// 显示地点坐标系
    /// </summary>
    private void DisplayCoordinateSystem()
    {
        currentCoordSlider.maxValue = targetCoordSlider.maxValue = CurEnv.PlaceData.maxCoord / MOVE_DIST_RESOLUTION;
    }

    /// <summary>
    /// 用白色填充 current coord slider handle 和 target corrd slider handle 之间的部分
    /// </summary>
    private void FillBetween()
    {
        fillBetween.transform.position = new((currentCoordSlider.handleRect.position.x + targetCoordSlider.handleRect.position.x) / 2, fillBetween.transform.position.y);
        fillBetween.rectTransform.sizeDelta = new(Mathf.Abs(currentCoordSlider.handleRect.position.x - targetCoordSlider.handleRect.position.x), fillBetween.rectTransform.sizeDelta.y);
    }

    private bool playSound;
    /// <summary>
    /// 显示玩家位置
    /// </summary>
    private void DisplayPlayerPosition(bool playAnim)
    {
        var position = Player.Instance.Coordinate.Position;
        var endValue = position / MOVE_DIST_RESOLUTION;
        playSound = false;
        targetCoordSlider.value = endValue;
        targetPosition.text = position.ToString("0.0");
        playSound = true;

        if (playAnim)
        {
            currentCoordSlider.DOKill();
            currentCoordSlider.DOValue(endValue, valueTransition).OnUpdate(() =>
            {
                var curPos = currentCoordSlider.value * MOVE_DIST_RESOLUTION;
                currentPosition.text = curPos.ToString("0.0");
                deltaPosition.text = (position - curPos).ToString("0.0");
                FillBetween();
            });
        }
        else
        {

            currentCoordSlider.value = endValue;
            currentPosition.text = position.ToString("0.0");
            deltaPosition.text = "0.0";
            FillBetween();
        }
    }

    private void RefreshPlayerPosition()
    {
        DisplayPlayerPosition(true);
    }

    /// <summary>
    /// 执行移动
    /// </summary>
    private void ExecuteMove()
    {
        if (Mathf.Abs(TargetPosition - Player.Instance.Coordinate.Position) < MOVE_DIST_RESOLUTION) return;
        MoveExploreManager.Instance.Move(TargetPosition);
    }
}
