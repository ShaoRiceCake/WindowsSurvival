using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 卡牌格
/// </summary>
public class CardSlot : MonoBehaviour
{
    private const int BIG_ICON_IMAGE_SIZE = 92;                     // 卡牌大图尺寸
    private const int SMALL_ICON_SIZE = 64;                         // 卡牌小图尺寸
    private const int BIG_ICON_ANCHOR_POS = 16;                     // 大图锚点位置
    private const int SMALL_ICON_ANCHOR_POS = 30;                   // 小图锚点位置
    private const int BIG_ICON_MIDDLE_COMPONENT_LAYOUT_POSY = 57;   // 大图的组件摆放位置Y
    private const int SMALL_ICON_MIDDLE_COMPONENT_LAYOUT_POSY = 65; // 小图的组件摆放位置Y

    [SerializeField] private Image iconImage;                       // 卡牌图
    [SerializeField] private Text nameText;                         // 卡牌名称
    [SerializeField] private GameObject stackObject;                // 控制是否显示堆叠
    [SerializeField] private Text stackNumText;                     // 显示数量
    [SerializeField] private Image maxStackNumImage;                // 显示最大堆叠数量的图标
    [SerializeField] private CanvasGroup canvasGroup;               // 整体的canvasGroup
    [SerializeField] private CanvasGroup cardCanvasGroup;           // 卡面的canvasGroup
    [SerializeField] private Text moreInfoText;                     // 额外信息
    [SerializeField] private RectTransform particleDisplayRect;     // 显示粒子的区域
    [SerializeField] private GameObject mask;

    [SerializeField] private RectTransform middle;                  // 用于显示新鲜度、耐久等组件的布局
    [SerializeField] private RectTransform left;                    // 用于显示计时器和盐水
    [SerializeField] private RectTransform right;                   // 用于显示温度和淡水
    [SerializeField] private RectTransform top;                     // 用于显示实体的坐标
    [SerializeField] private RectTransform innerContentsComponent;  // 用于显示内容物组件
    [SerializeField] private GameObject iconLayout;                 // 用于显示图标的布局
    [SerializeField] private Image fireIcon;                        // 图标上的火焰
    [SerializeField] private Image flashIcon;                       // 图标上的闪电
    [SerializeField] private Image intentionIcon;                   // 实体意图图标
    [SerializeField] private List<Sprite> intentionSprites;         // 实体意图图标列表
    [SerializeField] private List<Sprite> intentionSpritesReversedColor;    // 反色实体意图图标列表

    [SerializeField] private Animator cardAnimator;                 // 卡图动画机
    [SerializeField] private Animator intentionAnimator;            // 意图动画机

    [SerializeField] private HoverTipController tipController;      // 显示快捷交互内容

    [SerializeField] private bool onlyDisplay = false;

    private Dictionary<Type, float> lastComponentValues = new();
    private Dictionary<Type, UIStateSlider> componentSliders = new();   // 用于存储组件的滑动条
    private Dictionary<string, Sprite> intentionSpriteLookup = new();   // 实体意图图标对照表
    private Dictionary<string, Sprite> intentionSpriteReversedInColorLookup = new();   // 反色实体意图图标对照表
    private bool? originalMaskRaycastTarget;                         // 保存 mask 的原始 raycastTarget 值

    private Coroutine switchIntentionCo;

    public SlotCards Cards { get; protected set; }
    public bool IsEmpty => Cards.IsEmpty;
    public int StackNum => Cards.StackNum;

    public bool Interactable { get; protected set; }

    private bool dontRefresh;

    public bool DontRefresh
    {
        get => dontRefresh;
        set
        {
            dontRefresh = value;
            if (!value) RefreshDisplay();
            else canvasGroup.blocksRaycasts = false;
        }
    }

    private void Awake()
    {
        if (!onlyDisplay)
            Clear();

        foreach (var sprite in intentionSprites)
        {
            intentionSpriteLookup[sprite.name.Replace("Intention_", "")] = sprite;
        }

        foreach (var sprite in intentionSpritesReversedColor)
        {
            intentionSpriteReversedInColorLookup[sprite.name.Replace("Intention_r_", "")] = sprite;
        }
    }

    private void OnEnable()
    {
        if (onlyDisplay) return;

        //Clear();
        EventManager.Instance.AddListener(EventType.StartChangeTime, OnStartChangeTime);
        EventManager.Instance.AddListener(EventType.EndChangeTime, OnEndChangeTime);
        EventManager.Instance.AddListener<Card>(EventType.PickUpCard, OnCardPickedUp);
        EventManager.Instance.AddListener(EventType.PutDownCard, OnCardPutDown);
    }

    private void OnDisable()
    {
        if (onlyDisplay) return;

        Clear();
        Cards?.SetCardSlot(null);
        Cards = null;

        transform.DOKill();
        transform.localScale = Vector3.one;

        EventManager.Instance.RemoveListener(EventType.StartChangeTime, OnStartChangeTime);
        EventManager.Instance.RemoveListener(EventType.EndChangeTime, OnEndChangeTime);
        EventManager.Instance.RemoveListener<Card>(EventType.PickUpCard, OnCardPickedUp);
        EventManager.Instance.RemoveListener(EventType.PutDownCard, OnCardPutDown);
    }

    public void Init(SlotCards slotCards)
    {
        Cards = slotCards;
        slotCards.SetCardSlot(this);
        RefreshDisplay();
    }

    #region 显示
    private void OnCardPickedUp(Card card)
    {
        if (Cards == null) return;

        if (IsEmpty)
        {
            // 同背包或者卡牌可以放置到不同背包
            if (card.Bag == Cards.Bag || card.Moveable && Cards.Bag.CanAddCard(card, out _))
                mask.SetActive(false);
            else
                mask.SetActive(true);
            return;
        }

        if (PeekCard().CanQuickInteract(card, out var tip))
        {
            // 恢复 mask 的原始 raycastTarget 值
            RestoreMaskRaycastTarget();
            mask.SetActive(false);
            Interactable = true;
            tipController.enabled = true;
            tipController.SetTip(tip);
        }
        else
        {
            Interactable = false;
            // 即使不能交互，如果有提示信息也显示（比如攻击范围不够的提示）
            // 只针对有武器组件的情况，让 mask 不阻挡鼠标事件
            if (!string.IsNullOrEmpty(tip) && card.TryGetComponent<WeaponComponent>(out _))
            {
                // 保存并修改 mask 的 raycastTarget，使其不阻挡鼠标事件
                if (mask != null && mask.TryGetComponent<UnityEngine.UI.Image>(out var maskImage))
                {
                    if (!originalMaskRaycastTarget.HasValue)
                    {
                        originalMaskRaycastTarget = maskImage.raycastTarget;
                    }
                    maskImage.raycastTarget = false; // mask 不阻挡鼠标事件
                }
                mask.SetActive(true);
                tipController.enabled = true;
                tipController.SetTip(tip);
            }
            else
            {
                // 恢复 mask 的原始 raycastTarget 值
                RestoreMaskRaycastTarget();
                mask.SetActive(true);
                tipController.enabled = false;
            }
        }
    }

    private void OnCardPutDown()
    {
        // 恢复 mask 的原始 raycastTarget 值
        RestoreMaskRaycastTarget();
        mask.SetActive(false);
        Interactable = false;
        tipController.enabled = false;
    }

    /// <summary>
    /// 恢复 mask 的原始 raycastTarget 值
    /// </summary>
    private void RestoreMaskRaycastTarget()
    {
        if (originalMaskRaycastTarget.HasValue && mask != null && mask.TryGetComponent<UnityEngine.UI.Image>(out var maskImage))
        {
            maskImage.raycastTarget = originalMaskRaycastTarget.Value;
            originalMaskRaycastTarget = null;
        }
    }

    private void OnStartChangeTime()
    {
        // 记录组件的初始值
        lastComponentValues.Clear();
        foreach (var (type, slider) in componentSliders)
        {
            lastComponentValues.Add(type, slider.endValue);
        }
    }

    private void OnEndChangeTime()
    {
        // 计算组件的变化值
        Dictionary<Type, float> deltaValues = new();
        foreach (var (type, lastValue) in lastComponentValues)
        {
            if (componentSliders.TryGetValue(type, out var slider))
            {
                if (slider.endValue != lastValue)
                    deltaValues.Add(type, slider.endValue - lastValue);
            }
        }

        if (deltaValues.Count > 0)
            DisplayComponentValuesChange(deltaValues);

        lastComponentValues.Clear();
    }

    /// <summary>
    /// 刷新当前显示
    /// </summary>
    public void RefreshDisplay()
    {
        if (dontRefresh) return;

        mask.SetActive(false);
        Interactable = false;
        canvasGroup.blocksRaycasts = true;

        if (IsEmpty)
        {
            Clear();
            return;
        }

        DisplayCard(PeekCard(), StackNum);
    }

    private void DisplayCardImage(Sprite sprite, bool isBigIcon)
    {
        iconImage.sprite = sprite;

        if (isBigIcon)
        {
            iconImage.rectTransform.sizeDelta = new(BIG_ICON_IMAGE_SIZE, BIG_ICON_IMAGE_SIZE);
            iconImage.rectTransform.anchoredPosition = new Vector2(BIG_ICON_ANCHOR_POS, -BIG_ICON_ANCHOR_POS);
            middle.anchoredPosition = new Vector2(middle.anchoredPosition.x, BIG_ICON_MIDDLE_COMPONENT_LAYOUT_POSY);
        }
        else
        {
            iconImage.rectTransform.sizeDelta = new(SMALL_ICON_SIZE, SMALL_ICON_SIZE);
            iconImage.rectTransform.anchoredPosition = new Vector2(SMALL_ICON_ANCHOR_POS, -SMALL_ICON_ANCHOR_POS);
            middle.anchoredPosition = new Vector2(middle.anchoredPosition.x, SMALL_ICON_MIDDLE_COMPONENT_LAYOUT_POSY);
        }
    }

    private void DisplayStackNum(int stackNum, int maxStackNum, bool displayStack)
    {
        static int CountDigitFour(int number)
        {
            string numberStr = Math.Abs(number).ToString(); // 处理负数
            int count = 0;

            foreach (char c in numberStr)
            {
                if (c == '4')
                {
                    count++;
                }
            }

            return count;
        }

        if (maxStackNum <= 1 || !displayStack)
        {
            stackObject.SetActive(false);
            maxStackNumImage.gameObject.SetActive(false);
        }
        else
        {
            stackObject.SetActive(true);
            stackNumText.text = $"{stackNum}";
            stackNumText.GetComponent<TextSpacing>().spacing_x = -CountDigitFour(stackNum) * 2; // 处理字体中数字4导致的间距变大

            maxStackNumImage.gameObject.SetActive(stackNum == maxStackNum);
        }
    }

    /// <summary>
    /// 显示具有浮点值数据的组件
    /// </summary>
    /// <param name="component"></param>
    private void DisplayContinuousValueComponent(CardComponent component, RectTransform parent, bool vertical = false)
    {
        bool playAnim = true;
        if (!componentSliders.TryGetValue(component.GetType(), out UIStateSlider slider))
        {
            if (component is TemperatureComponent ||
                component is TimerComponent ||
                component is CoordinateComponent)
                slider = ObjectBufferPool.Instance.Get("Prefabs/UI/Controls/Components", component.GetType().Name, parent).GetComponent<UIStateSlider>();
            else
                slider = ObjectBufferPool.Instance.Get("Prefabs/UI/Controls/Components", $"{(vertical ? "Vertical" : "")}Component", parent).GetComponent<UIStateSlider>();

            slider.transform.SetAsLastSibling();

            slider.transform.position = parent.position;
            (slider.transform as RectTransform).anchoredPosition = Vector3.zero;
            slider.transform.localRotation = Quaternion.identity;
            componentSliders.Add(component.GetType(), slider);

            playAnim = false; // 对于新创建的组件不播放动画
        }

        if (ColorManager.CardComponentColors.TryGetValue(component.GetType(), out var fillColor))
            slider.fillColor = fillColor;

        switch (component)
        {
            case DurabilityComponent durabilityComponent:
                slider.SetValue(durabilityComponent.value, durabilityComponent.maxValue, playAnim);
                slider.tipController.SetTip($"耐久度:  {slider.endValue * 100:0.0}%", slider.fillColor);
                break;
            case FreshnessComponent freshnessComponent:
                slider.SetValue(freshnessComponent.value, freshnessComponent.maxValue, playAnim);
                slider.tipController.SetTip($"新鲜度:  {slider.endValue * 100:0.0}%", slider.fillColor);
                break;
            case GrowthComponent growthComponent:
                slider.SetValue(growthComponent.value, growthComponent.maxValue, playAnim);
                slider.tipController.SetTip($"生长度:  {slider.endValue * 100:0.0}%", slider.fillColor);
                break;
            case PlantGrowthComponent plantGrowthComponent:
                slider.SetValue(plantGrowthComponent.value, plantGrowthComponent.maxValue, playAnim);
                slider.tipController.SetTip($"生长度:  {slider.endValue * 100:0.0}%", slider.fillColor);
                break;
            case ProgressComponent progressComponent:
                slider.SetValue(progressComponent.value, progressComponent.maxValue, playAnim);
                slider.tipController.SetTip($"产物进度:  {slider.endValue * 100:0.0}%", slider.fillColor);
                break;
            case FuelStorageComponent fuelStorageComponent:
                slider.SetValue(fuelStorageComponent.value, fuelStorageComponent.maxValue, playAnim);
                string tip = $"剩余燃料:  {fuelStorageComponent.value}/{fuelStorageComponent.maxValue}";
                // 显示燃料消耗
                tip += $"\n自然消耗:  -{fuelStorageComponent.basicFuelConsumption:0.0}/15min";
                if (StateManager.Instance.WaterLevel.CurValue > 0)
                    tip += $"\n地面积水:  -{fuelStorageComponent.extraFuelConsumptionWhenWaterLevelHigh:0.0}/15min";

                // TODO: 冰层季额外消耗
                slider.tipController.SetTip(tip, slider.fillColor);
                break;
            case TemperatureComponent temperatureComponent:
                if (temperatureComponent.value <= 50)
                {
                    slider.fillColor = ColorManager.Green;
                }
                else if (temperatureComponent.value <= 100)
                {
                    slider.fillColor = ColorManager.Yellow;
                }
                else if (temperatureComponent.value <= 200)
                {
                    slider.fillColor = ColorManager.Orange;
                }
                else
                {
                    slider.fillColor = ColorManager.Red;
                }
                slider.SetValue(temperatureComponent.value, temperatureComponent.maxValue, playAnim);
                slider.tipController.SetTip($"当前温度:  {temperatureComponent.value}/{temperatureComponent.maxValue}", slider.fillColor);
                break;
            case OxygenStorageComponent oxygenStorageComponent:
                slider.SetValue(oxygenStorageComponent.value, oxygenStorageComponent.maxValue, playAnim);
                slider.tipController.SetTip($"剩余氧气:  {oxygenStorageComponent.value}/{oxygenStorageComponent.maxValue}", slider.fillColor);
                break;
            case FreshWaterStorageComponent freshWaterStorageComponent:
                slider.SetValue(freshWaterStorageComponent.value, freshWaterStorageComponent.maxValue, playAnim);
                slider.tipController.SetTip($"淡水储量:  {freshWaterStorageComponent.value}/{freshWaterStorageComponent.maxValue}", slider.fillColor);
                break;
            case SalineWaterStorageComponent salineWaterStorageComponent:
                slider.SetValue(salineWaterStorageComponent.value, salineWaterStorageComponent.maxValue, playAnim);
                slider.tipController.SetTip($"盐水储量:  {salineWaterStorageComponent.value}/{salineWaterStorageComponent.maxValue}", slider.fillColor);
                break;
            case TimerComponent timerComponent:
                slider.SetValue(timerComponent.value, timerComponent.maxValue, playAnim);
                var hour = Mathf.FloorToInt(timerComponent.value / 60);
                var minute = timerComponent.value % 60;
                string leftTime = "";
                if (hour == 0 && minute == 0)
                    leftTime = "0min";
                if (hour > 0)
                    leftTime += $"{hour}h";
                if (minute > 0)
                    leftTime += $"{minute}min";

                slider.tipController.SetTip($"{ColorManager.Colorize(timerComponent.tipText, ColorManager.Yellow)}剩余:  {leftTime}", slider.fillColor);
                break;
            case CoordinateComponent coordinateComponent:
                slider.SetValue(coordinateComponent.coordinate.Position, GameManager.Instance.CurEnvironmentBag.PlaceData.maxCoord, playAnim);
                var position = coordinateComponent.coordinate.Position;
                var dist = coordinateComponent.coordinate.DistanceTo(Player.Instance.Coordinate);
                slider.tipController.SetTip($"当前坐标:  {ColorManager.ColorizeNumber(position, ColorManager.Cyan)}\n距离麦麦:  {ColorManager.ColorizeNumber(dist, ColorManager.Cyan)}");
                break;
            case EntityComponent entityComponent:
                slider.SetValue(entityComponent.value, entityComponent.maxValue, playAnim);
                slider.tipController.SetTip($"生命值:  {entityComponent.value}/{entityComponent.maxValue}", slider.fillColor);
                break;
            default:
                Debug.LogWarning($"未知组件类型: {component.GetType()}");
                break;
        }
    }

    /// <summary>
    /// 显示内容物组件
    /// </summary>
    /// <param name="component"></param>
    private void DisplayInnerContentsComponent(InnerContentsComponent component)
    {
        innerContentsComponent.gameObject.SetActive(true);
        for (int i = 0; i < innerContentsComponent.childCount; i++)
        {
            innerContentsComponent.GetChild(i).gameObject.SetActive(i < component.bag.SlotCount);
            innerContentsComponent.GetChild(i).GetComponent<Image>().color = i < component.bag.SlotCount - component.bag.EmptySlotCount ? ColorManager.White : ColorManager.DarkGrey;
        }
    }

    /// <summary>
    /// 显示卡牌状态
    /// </summary>
    /// <param name="state"></param>
    private void DisplayCardState(Card card, CardState state)
    {
        var animName = card.CardId + state.stateName;
        bool IsAnim()
        {
            for (int i = 0; i < cardAnimator.runtimeAnimatorController.animationClips.Length; i++)
            {
                if (cardAnimator.runtimeAnimatorController.animationClips[i].name == animName)
                    return true;
            }
            return false;
        }

        // 有动画的播放动画
        if (IsAnim())
        {
            cardAnimator.enabled = true;
            cardAnimator.Play(animName);
        }
        else if (cardAnimator.enabled)
        {
            cardAnimator.Play("");
            cardAnimator.enabled = false;
        }

        // 组件摆放的位置同小图
        middle.anchoredPosition = new Vector2(middle.anchoredPosition.x, SMALL_ICON_MIDDLE_COMPONENT_LAYOUT_POSY);
    }

    /// <summary>
    /// 显示电力消耗图标
    /// </summary>
    private void DisplayElectricPowerIcon(bool connected, float consumptionRate)
    {
        iconLayout.SetActive(true);
        flashIcon.gameObject.SetActive(true);
        flashIcon.color = connected ? ColorManager.Yellow : ColorManager.DarkGrey;

        if (flashIcon.TryGetComponent<HoverTipController>(out var controller))
        {
            string tip = connected ? $"电力: -{consumptionRate:0.0}/15min" : "未接电";
            controller.SetTip(tip, ColorManager.Yellow);
        }
    }

    /// <summary>
    /// 显示燃烧图标
    /// </summary>
    private void DisplayBuriningIcon(bool isBurning, float oxygenConsumptionRate, float coProductionRate)
    {
        iconLayout.SetActive(true);
        fireIcon.gameObject.SetActive(true);
        fireIcon.color = isBurning ? ColorManager.BurntOrange : ColorManager.DarkGrey;

        if (fireIcon.TryGetComponent<HoverTipController>(out var controller))
        {
            string tip = isBurning ? $"氧气:  -{oxygenConsumptionRate:0.0}/15min\nCO浓度:  +{coProductionRate:0.0}/15min" : "未点燃";
            controller.SetTip(tip, ColorManager.BurntOrange);
        }
    }

    /// <summary>
    /// 切换意图
    /// </summary>
    /// <param name="previous">上一个意图</param>
    /// <param name="target">切换的目标意图</param>
    /// <param name="onSwitchOver">意图切换动画完成后执行</param>
    public void SwitchIntention(EntityIntention previous, EntityIntention target, UnityAction onSwitchOver)
    {
        switchIntentionCo = PublicMono.Instance.StartCoroutine(SwitchIntentionCo(previous, target, onSwitchOver));
    }

    private IEnumerator SwitchIntentionCo(EntityIntention previous, EntityIntention target, UnityAction onSwitchOver)
    {
        string animName;
        // 原意图的结束动画
        if (previous != null)
        {
            if (previous.ExeSucceed)
            {
                // 原意图执行成功
                // 播放成功结束动画
                intentionAnimator.enabled = true;
                animName = previous.GiveName() + "_End";
                intentionAnimator.ResetTrigger(animName);
                intentionAnimator.SetTrigger(animName);

                // 等待一帧确保动画切换完成
                yield return null;

                // 等待动画播放完成
                var duration = intentionAnimator.GetCurrentAnimatorStateInfo(0).length;
                yield return new WaitForSeconds(duration);
            }
            else
            {
                // 原意图执行失败
                // 播放执行失败动画
                var seq = AnimationManager.Instance.PlayIntentionFailed(intentionIcon);
                while (seq.active)
                {
                    yield return null;
                }
            }
        }

        // 新意图的开始动画
        if (target != null)
        {
            intentionAnimator.enabled = true;
            iconLayout.SetActive(true);
            intentionIcon.gameObject.SetActive(true);
            animName = target.GiveName() + "_Start";
            intentionAnimator.ResetTrigger(animName);
            intentionAnimator.SetTrigger(animName);

            // 等待一帧确保动画切换完成
            yield return null;

            // 等待动画播放完成
            var duration = intentionAnimator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(duration);

            DisplayEntityIntention(target);
        }
        else
        {
            iconLayout.SetActive(false);
            intentionIcon.gameObject.SetActive(false);
        }

        intentionAnimator.enabled = false;

        // 执行切换结束逻辑
        onSwitchOver?.Invoke();
    }

    public void DisplayEntityIntention(EntityIntention intention)
    {
        if (intention == null)
        {
            iconLayout.SetActive(false);
            intentionIcon.gameObject.SetActive(false);
            return;
        }

        // 设置新的意图图标和悬浮效果
        var tipController = intentionIcon.GetComponent<HoverTipController>();

        var intentionName = intention.GiveName();
        var normalSprite = intentionSpriteLookup[intentionName]; // 正常颜色图标
        var reversedColorSprite = intentionSpriteReversedInColorLookup[intentionName];  // 反色图标

        intentionIcon.sprite = normalSprite;

        tipController.onPointerEnter = () =>
        {
            intentionIcon.sprite = reversedColorSprite;
            var tip = new StringBuilder();
            tip.AppendLine("意图:  " + ColorManager.Colorize(intentionName, ColorManager.Yellow));
            tip.AppendLine("执行倒计时:  " + ColorManager.Colorize(intention.ExecutionCountdown.ToString() + "分钟", ColorManager.Cyan));
            tip.Append(intention.GetDescription());
            tipController.SetTip(tip.ToString().TrimEnd('\n'));
        };

        tipController.onPointerExit = () =>
        {
            intentionIcon.sprite = normalSprite;
        };

        intentionAnimator.enabled = false;

        iconLayout.SetActive(true);
        intentionIcon.gameObject.SetActive(true);
    }

    /// <summary>
    /// 显示指定数量的卡牌
    /// </summary>
    /// <param name="card"></param>
    /// <param name="stackNum"></param>
    public void DisplayCard(Card card, int stackNum, bool displayStack = true)
    {
        GetComponent<PassageIceOverlay>()?.Display(card);
        // 如果要显示的数量小于等于零，则什么也不显示
        if (stackNum <= 0)
        {
            Clear();
            return;
        }

        EnableDisplay();

        // 显示卡牌图
        DisplayCardImage(card.CardImage, card.IsBigIcon);

        // 显示卡牌名称
        nameText.text = card.CardName;

        // 显示额外信息
        moreInfoText.text = card.ExtraInfo;

        // 显示堆叠数量
        DisplayStackNum(stackNum, card.MaxStackNum, displayStack);

        // 显示耐久
        if (card.TryGetComponent<DurabilityComponent>(out var d))
            DisplayContinuousValueComponent(d, middle);
        // 显示新鲜度
        if (card.TryGetComponent<FreshnessComponent>(out var f))
            DisplayContinuousValueComponent(f, middle);
        // 显示生长度
        if (card.TryGetComponent<GrowthComponent>(out var g))
            DisplayContinuousValueComponent(g, middle);
        // 显示产物进度
        if (card.TryGetComponent<ProgressComponent>(out var p))
            DisplayContinuousValueComponent(p, middle);
        // 显示内容物数量
        if (card.TryGetComponent<InnerContentsComponent>(out var i))
            DisplayInnerContentsComponent(i);
        // 显示燃料存储
        if (card.TryGetComponent<FuelStorageComponent>(out var fc))
        {
            DisplayContinuousValueComponent(fc, middle);
            DisplayBuriningIcon(fc.isBurning, fc.oxygenConsumptionOnBurning, fc.coProductionOnBurning);
        }
        // 显示温度
        if (card.TryGetComponent<TemperatureComponent>(out var t))
            DisplayContinuousValueComponent(t, right);
        // 显示状态
        if (card.TryGetComponent<StateMachineComponent>(out var s))
            DisplayCardState(card, s.CurrentState);
        // 显示氧气存储
        if (card.TryGetComponent<OxygenStorageComponent>(out var o))
            DisplayContinuousValueComponent(o, middle);
        // 显示盐水存储
        if (card.TryGetComponent<SalineWaterStorageComponent>(out var sw))
            DisplayContinuousValueComponent(sw, left, true);
        // 显示淡水存储
        if (card.TryGetComponent<FreshWaterStorageComponent>(out var fw))
            DisplayContinuousValueComponent(fw, right, true);
        // 显示计时器
        if (card.TryGetComponent<TimerComponent>(out var tm))
            DisplayContinuousValueComponent(tm, left);
        else if (componentSliders.TryGetValue(typeof(TimerComponent), out var timer)) // 因为计时器可能被移除，所以这里要检查一下，如果有就移除
        {
            componentSliders.Remove(typeof(TimerComponent));
            ObjectBufferPool.Instance.Restore(timer.gameObject);
        }
        // 显示植物生长度
        if (card.TryGetComponent<PlantGrowthComponent>(out var pg))
            DisplayContinuousValueComponent(pg, middle);
        // 显示坐标
        if (card.TryGetComponent<CoordinateComponent>(out var cc))
        {
            if (card is EntityCard)
                // 实体卡牌的坐标显示在顶部
                DisplayContinuousValueComponent(cc, top);
            else
                DisplayContinuousValueComponent(cc, middle);
        }
        // 显示实体生命值
        if (card.TryGetComponent<EntityComponent>(out var ec))
            DisplayContinuousValueComponent(ec, middle);
        // 显示实体意图
        if (card is EntityCard en)
            DisplayEntityIntention(en.CurrentIntention);
        // 显示电力消耗
        if (card.TryGetComponent<PowerConsumptionComponent>(out var pc) && pc.consumptionRate > 0)
            DisplayElectricPowerIcon(pc.Connected, pc.consumptionRate);
    }

    /// <summary>
    /// 不显示卡牌
    /// </summary>
    public void Clear()
    {
        GetComponent<PassageIceOverlay>()?.Clear();
        cardAnimator.enabled = false;
        intentionAnimator.enabled = false;

        cardCanvasGroup.alpha = 0;
        cardCanvasGroup.blocksRaycasts = false;
        cardCanvasGroup.interactable = false;

        ObjectBufferPool.Instance.RestoreAllChildren(middle);
        ObjectBufferPool.Instance.RestoreAllChildren(left);
        ObjectBufferPool.Instance.RestoreAllChildren(right);
        ObjectBufferPool.Instance.RestoreAllChildren(top);

        if (mask != null) mask.SetActive(false);

        if (innerContentsComponent != null) innerContentsComponent.gameObject.SetActive(false);
        if (iconLayout != null) iconLayout.SetActive(false);
        if (fireIcon != null) fireIcon.gameObject.SetActive(false);
        if (flashIcon != null) flashIcon.gameObject.SetActive(false);
        if (intentionIcon != null) intentionIcon.gameObject.SetActive(false);

        componentSliders.Clear();
        lastComponentValues.Clear();

        tipController.enabled = false;

        if (switchIntentionCo != null)
        {
            PublicMono.Instance.StopCoroutine(switchIntentionCo);
            switchIntentionCo = null;
        }
    }

    /// <summary>
    /// 允许显示卡牌
    /// </summary>
    private void EnableDisplay()
    {
        mask.SetActive(false);
        cardCanvasGroup.alpha = 1;
        cardCanvasGroup.blocksRaycasts = true;
        cardCanvasGroup.interactable = true;
    }

    /// <summary>
    /// 显示卡牌组件值的变化（如耐久度变化、新鲜度变化）
    /// </summary>
    /// <param name="minute"></param>
    public void DisplayComponentValuesChange(Dictionary<Type, float> deltaValues)
    {
        List<(bool up, int level, Color color)> groups = new();

        foreach (var (type, deltaValue) in deltaValues)
        {
            // 更新最后记录的值，避免重复显示变化
            lastComponentValues.Remove(type);

            if (ColorManager.CardComponentColors.TryGetValue(type, out var color))
                groups.Add((deltaValue > 0, CalcLevel(deltaValue), color));
        }

        AnimationManager.Instance.ShowArrows(particleDisplayRect, groups);
    }

    public void DisplayComponentValueChange(Type componentType, float delta)
    {
        // 更新最后记录的值，避免重复显示变化
        lastComponentValues.Remove(componentType);

        if (ColorManager.CardComponentColors.TryGetValue(componentType, out var color))
            AnimationManager.Instance.ShowArrows(particleDisplayRect, delta > 0, CalcLevel(delta), color);
    }

    private int CalcLevel(float delta)
    {
        var absValue = Mathf.Abs(delta);
        if (absValue <= 0.1)
            return 1;
        else if (absValue <= 0.3)
            return 2;
        else
            return 3;
    }
    #endregion

    /// <summary>
    /// 判断该卡牌格是否放有同类卡牌（名称相同即同类）
    /// </summary>
    /// <param name="cardName"></param>
    /// <returns></returns>
    public bool ContainsByCardName(string cardName) => Cards.ContainsByCardName(cardName);

    /// <summary>
    /// 判断该卡牌格是否放有相同卡牌（ID相同）
    /// </summary>
    /// <param name="cardId"></param>
    /// <returns></returns>
    public bool ContainsByCardId(string cardId) => Cards.ContainsByCardId(cardId);

    /// <summary>
    /// 能否添加指定卡牌，只有id相同才能堆叠
    /// </summary>
    /// <returns></returns>
    public virtual bool CanAddCard(Card card) => Cards.CanAddCard(card);

    /// <summary>
    /// 添加一张卡牌
    /// </summary>
    /// <param name="card"></param>
    public virtual void AddCard(Card card) => Cards.AddCard(card);

    /// <summary>
    /// 移除指定的一张卡牌
    /// </summary>
    /// <param name="card"></param>
    public virtual void RemoveCard(Card card) => Cards.RemoveCard(card);

    /// <summary>
    /// 移除最优先显示的卡牌
    /// </summary>
    /// <returns></returns>
    public Card RemoveCard() => Cards.RemoveCard();

    /// <summary>
    /// 移除指定数量的卡牌
    /// </summary>
    /// <param name="amount"></param>
    public void RemoveCards(int amount) => Cards.RemoveCards(amount);

    /// <summary>
    /// 取得优先级最高的卡牌
    /// </summary>
    /// <returns></returns>
    public Card PeekCard() => Cards.PeekCard();
}
