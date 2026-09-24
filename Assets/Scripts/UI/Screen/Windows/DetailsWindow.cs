using UnityEngine;
using UnityEngine.UI;

public enum DisplayType
{
    All,
    OnlyDetails,
    DetailsAndCraftButton
}

public class DetailsWindow : BagWindow
{
    [SerializeField] private Text detailsText;
    [SerializeField] private GameObject detailsScrollView;
    [SerializeField] private Transform buttonLayout;
    [SerializeField] private CardSlot slot;
    [SerializeField] private RectTransform contentsView;

    [SerializeField] private Transform menuLayout; // 菜单布局
    [SerializeField] private HoverableButton detailsButton; // 显示详细信息按钮
    [SerializeField] private HoverableButton innerContentsButton; // 显示内部内容按钮
    [SerializeField] private HoverableButton modificationsButton;
    [SerializeField] private ModificationDetailsView modificationsView;
    [SerializeField] private GameObject placeDisplay;
    [SerializeField] private Image placeImage;
    [SerializeField] private Text placeTitle;
    private EnvironmentBag displayedEnvironment;

    [SerializeField] private GameObject eventButtonPrefab;

    [SerializeField] private RectTransform selectRect; // 选择框

    private string currentDisplayedPart;

    private Card currentDisplayedCard;
    // 公开当前正在显示的卡牌，供其他系统（如音效管理）查询
    public Card CurrentDisplayedCard => currentDisplayedCard;
    private Bag innerBag;
    private DisplayType displayType = DisplayType.All;
    public string CurrentDisplayPart => currentDisplayedPart;
    private bool wasAdvancing;
    private float fittedEventWidth = -1;
    private void OnRectTransformDimensionsChange() => FitEventButtons();
    private void FitEventButtons()
    {
        if (buttonLayout == null || eventButtonPrefab == null) return;
        var content = (RectTransform)buttonLayout;
        float width = ((RectTransform)buttonLayout.parent).rect.width;
        if (width <= 0 || Mathf.Approximately(width, fittedEventWidth)) return;
        fittedEventWidth = width;
        var controls = buttonLayout.GetComponentsInChildren<HoverableButton>();
        if (controls.Length == 0) return;
        int fontSize = eventButtonPrefab.GetComponent<HoverableButton>().text.fontSize;
        var layout = buttonLayout.GetComponent<HorizontalLayoutGroup>();
        float gaps = layout == null ? 0 : layout.padding.horizontal + layout.spacing * (controls.Length - 1);
        float required;
        do
        {
            required = gaps;
            foreach (var control in controls)
            {
                control.text.fontSize = fontSize;
                control.minWidth = 32;
                control.AdaptWidth();
                required += control.rectTransform.rect.width;
            }
            if (required <= width || fontSize <= 16) break;
            fontSize--;
        } while (true);
        // Preserve horizontal scrolling for unusually large action sets at the readable minimum.
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(width, required));
        content.anchoredPosition = new(Mathf.Max(0, required - width) / 2, content.anchoredPosition.y);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }
    private void LateUpdate()
    {
        bool advancing = TimeManager.Instance.IsAdvancing;
        if (wasAdvancing != advancing)
        {
            wasAdvancing = advancing;
            DisplayEventButtons();
            if (currentDisplayedPart == "改装") modificationsView.Refresh();
        }
        FitEventButtons();
    }

    protected override void Awake()
    {
        base.Awake();
        EventManager.Instance.AddListener<Card>(EventType.ChangeCardProperty, RefreshCard);
        EventManager.Instance.AddListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, OnChangeEnv);
        EventManager.Instance.AddListener<AddRemoveCardArgs>(EventType.AddRemoveCard, OnPlayerCardsChanged);
        EventManager.Instance.AddListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, OnEnvironmentStateChange);
    }

    private void OnDestroy()
    {
        Clear();
        EventManager.Instance.RemoveListener<Card>(EventType.ChangeCardProperty, RefreshCard);
        EventManager.Instance.RemoveListener<EnvironmentBag>(EventType.ChangeCurrentEnvironment, OnChangeEnv);
        EventManager.Instance.RemoveListener<AddRemoveCardArgs>(EventType.AddRemoveCard, OnPlayerCardsChanged);
        EventManager.Instance.RemoveListener<RefreshEnvironmentStateArgs>(EventType.RefreshEnvironmentState, OnEnvironmentStateChange);
    }

    protected override void Init()
    {
        if (currentDisplayedCard == null && displayedEnvironment == null)
        {
            ResetDisplay();
        }

        detailsButton.onClick.AddListener(() =>
        {
            if ((currentDisplayedCard != null || displayedEnvironment != null) && currentDisplayedPart != "详情")
            {
                DisplayDetails();
            }
        });

        innerContentsButton.onClick.AddListener(() =>
        {
            if (currentDisplayedCard != null && currentDisplayedPart != "内容物")
            {
                DisplayInnerContents();
            }
        });
        if (modificationsButton != null) modificationsButton.onClick.AddListener(DisplayModifications);
    }

    /// <summary>
    /// 当玩家背包物品变化时触发，这是为了刷新卡牌事件的触发条件
    /// </summary>
    /// <param name="args"></param>
    private void OnPlayerCardsChanged(AddRemoveCardArgs args)
    {
        if (currentDisplayedCard == null) return;

        if (args.AffectedBag is not PlayerBag) return;

        DisplayEventButtons();
    }

    /// <summary>
    /// 电力变化时触发，这是为了刷新卡牌事件的触发条件
    /// </summary>
    /// <param name="args"></param>
    private void OnEnvironmentStateChange(RefreshEnvironmentStateArgs args)
    {
        if (currentDisplayedCard == null) return;

        switch (args.stateEnum)
        {
            case EnvironmentStateEnum.Electricity:
            case EnvironmentStateEnum.WaterLevel:
                DisplayEventButtons();
                break;
        }
    }

    private void RefreshCard(Card card)
    {
        if (currentDisplayedCard != card) return;

        // 如果卡牌要被销毁
        if (currentDisplayedCard.Destroyed)
        {
            // 尝试从这个卡牌的slotCount里取出同类卡牌并刷新
            if (currentDisplayedCard.SlotCards.ContainsByCardId(currentDisplayedCard.CardId))
                Display(currentDisplayedCard.SlotCards);
            // 否则清空显示
            else
                Clear();
        }
        // 正常刷新显示
        else
        {
            slot.DisplayCard(currentDisplayedCard, 1, false);
            if (currentDisplayedPart == "详情") detailsText.text = currentDisplayedCard.CardDesc;
            DisplayEventButtons();
            //if (currentDisplay == "内容物" && innerBag != null)
            //    DisplayBag(innerBag);
        }
    }

    bool envChanged = false;
    private void OnChangeEnv(EnvironmentBag curEnvironmentBag)
    {
        // 切地点时清除显示
        envChanged = true;
    }

    public void Display(SlotCards slotCards, DisplayType displayType = DisplayType.All)
    {
        ResetDisplay();

        if (slotCards.IsEmpty) return;

        // 记录当前显示的卡牌
        currentDisplayedCard = slotCards.PeekCard();

        currentDisplayedCard.Transform = slot.transform;

        Display(displayType);
    }

    public void Display(Card card, DisplayType displayType = DisplayType.All)
    {
        ResetDisplay();

        if (card == null) return;

        // 记录当前显示的卡牌
        currentDisplayedCard = card;

        currentDisplayedCard.Transform = slot.transform;

        Display(displayType);
    }

    private void Display(DisplayType displayType = DisplayType.All)
    {
        slot.gameObject.SetActive(true);
        if (modificationsButton != null) modificationsButton.gameObject.SetActive(displayType == DisplayType.All && currentDisplayedCard is PassageCard { Connection: { IsWater: true } });
        this.displayType = displayType;

        // 显示卡牌
        slot.DisplayCard(currentDisplayedCard, 1, false);

        // 显示可交互按钮
        DisplayEventButtons();

        switch (displayType)
        {
            case DisplayType.All:
                // 有内容物优先显示内容物
                if (currentDisplayedCard.TryGetComponent<InnerContentsComponent>(out var component) && component.display)
                {
                    innerContentsButton.gameObject.SetActive(true);
                    innerContentsButton.Interactable = true;
                    innerBag = component.bag;

                    // 显示内容物
                    DisplayInnerContents();
                }
                // 优先显示详情
                else
                {
                    innerContentsButton.gameObject.SetActive(false);
                    innerContentsButton.Interactable = false;
                    // 显示详情
                    DisplayDetails();
                }
                break;
            case DisplayType.OnlyDetails:
            case DisplayType.DetailsAndCraftButton:
            default:
                DisplayDetails();
                break;
        }

        // 打开详情如果卡牌有循环音
        if (currentDisplayedCard.HasLoopSound)
            currentDisplayedCard.OnDetailOpen();

        FitEventButtons();

        EventManager.Instance.TriggerEvent(EventType.ChangeDisplayedCard);
        EventManager.Instance.TriggerEvent(EventType.DialogueCondition, new SubscribeActionArgs("Detail", currentDisplayedCard.CardName));
    }

    private void DisplayDetails()
    {
        if (modificationsView != null) modificationsView.gameObject.SetActive(false);
        currentDisplayedPart = "详情";

        detailsScrollView.SetActive(true);
        contentsView.gameObject.SetActive(false);

        // 显示卡牌详细信息
        detailsText.text = currentDisplayedCard != null ? currentDisplayedCard.CardDesc : displayedEnvironment == null ? "" :
            displayedEnvironment.PlaceData.placeDesc + "\n\n" + ClimateManager.Instance.EnvironmentDescription(displayedEnvironment);

        SelectWithTween(detailsButton.GetComponent<RectTransform>());
    }

    private void DisplayInnerContents()
    {
        if (modificationsView != null) modificationsView.gameObject.SetActive(false);
        currentDisplayedPart = "内容物";

        detailsScrollView.SetActive(false);
        contentsView.gameObject.SetActive(true);

        DisplayBag(innerBag);

        SelectWithTween(innerContentsButton.GetComponent<RectTransform>());
    }

    private void DisplayEventButtons()
    {
        if (currentDisplayedCard == null || displayType == DisplayType.OnlyDetails) return;
        fittedEventWidth = -1;

        ObjectBufferPool.Instance.RestoreAllChildren(buttonLayout);

        // 显示详情和前往制作按钮
        if (displayType == DisplayType.DetailsAndCraftButton)
        {
            var button = ObjectBufferPool.Instance.Get(eventButtonPrefab, buttonLayout).GetComponent<HoverableButton>();
            button.text.text = "前往制作";
            button.Interactable = WindowsManager.Instance.GetUnlockedShortcuts().Contains("Craft");
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                var window = WindowsManager.Instance.OpenWindow("Craft") as CraftWindow;
                window.DisplayRecipe(currentDisplayedCard.CardId);
            });

            if (button.Interactable)
            {
                button.text.color = ColorManager.White;
                button.GetComponent<HoverTipController>().SetTip("");
            }
            else
            {
                button.text.color = ColorManager.DarkGrey;
                button.GetComponent<HoverTipController>().SetTip("制作窗口尚未解锁");
            }

            button.transform.localScale = Vector3.one; // 确保按钮缩放为1
            button.transform.SetAsLastSibling();
            return;
        }

        foreach (var e in currentDisplayedCard.Events)
        {
            if (e.ShouldHideThis()) continue;

            var card = currentDisplayedCard;
            var button = ObjectBufferPool.Instance.Get(eventButtonPrefab, buttonLayout).GetComponent<HoverableButton>();
            button.text.text = e.Name;

            var interactable = e.Judge();
            button.Interactable = interactable;

            // 判断cardEvent是否满足条件
            if (interactable)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    // 先执行事件
                    e.Inovke();

                    // 显示状态变化
                    var playerStateChanges = e.GetPlayerStateChanges();
                    if (!playerStateChanges.IsNullOrEmpty())
                    {
                        foreach (var (state, delta) in playerStateChanges)
                        {
                            ShowStateChange(state, delta, button.transform.position);
                        }
                    }

                    var envStateChanges = e.GetEnvStateChanges();
                    if (!envStateChanges.IsNullOrEmpty())
                    {
                        foreach (var (state, delta) in envStateChanges)
                        {
                            ShowStateChange(state, delta, button.transform.position);
                        }
                    }

                    // 改变场景了就清空详情
                    if (envChanged) Clear();
                    // 否则刷新卡牌和详情
                    else RefreshCard(card);
                    //else card?.RefreshSlot();

                    // 否则尝试刷新
                    //else if (currentDisplayedCard != null && !currentDisplayedCard.Destroyed)
                    //    DisplayCardDetails(currentDisplayedCard);
                });

                // 设置提示
                button.GetComponent<HoverTipController>().SetTip(e.Description, e.GetTimeChange(), e.GetPlayerStateChanges(), e.GetEnvStateChanges());
            }
            else
            {
                button.text.color = ColorManager.DarkGrey;
                button.GetComponent<HoverTipController>().SetTip(e.Hint);
            }

            button.transform.localScale = Vector3.one; // 确保按钮缩放为1
            button.transform.SetAsLastSibling();
            button.AdaptWidth(); // 自适应宽度
        }

        MonoUtility.UpdateLayoutSize(buttonLayout.GetComponent<ILayoutGroup>());
    }

    public void ResetDisplay()
    {
        displayedEnvironment = null;
        if (placeDisplay != null) placeDisplay.SetActive(false);
        if (modificationsView != null) modificationsView.gameObject.SetActive(false);
        if (modificationsButton != null) modificationsButton.gameObject.SetActive(false);
        ClearBag();

        currentDisplayedPart = null;
        envChanged = false;
        slot.Clear();

        // 关闭时如果卡牌有循环音将循环音减小
        if (currentDisplayedCard != null && currentDisplayedCard.HasLoopSound)
            currentDisplayedCard.OnDetailClose();

        if (currentDisplayedCard != null && !currentDisplayedCard.Destroyed)
            currentDisplayedCard.Transform = null;

        displayType = DisplayType.All;
        currentDisplayedCard = null;
        innerBag = null;
        detailsText.text = "";
        contentsView.gameObject.SetActive(false);
        innerContentsButton.gameObject.SetActive(false);
        ObjectBufferPool.Instance.RestoreAllChildren(buttonLayout);
    }

    public void DisplayEnvironment(EnvironmentBag environment)
    {
        ResetDisplay();
        displayedEnvironment = environment;
        slot.gameObject.SetActive(false);
        placeDisplay.SetActive(true); placeImage.sprite = environment.PlaceData.placeImage; placeTitle.text = environment.PlaceName;
        modificationsButton.gameObject.SetActive(true);
        DisplayModifications();
    }
    public void DisplayModifications()
    {
        var passage = currentDisplayedCard as PassageCard;
        var env = displayedEnvironment ?? passage?.Bag as EnvironmentBag;
        if (env == null || modificationsView == null) return;
        currentDisplayedPart = "改装";
        detailsScrollView.SetActive(false); contentsView.gameObject.SetActive(false);
        modificationsView.gameObject.SetActive(true);
        modificationsView.Bind(env, passage);
        SelectWithTween(modificationsButton.GetComponent<RectTransform>());
    }

    public void Clear()
    {
        ResetDisplay();
        EventManager.Instance.TriggerEvent(EventType.ChangeDisplayedCard);
    }

    private void SelectWithTween(RectTransform target)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(menuLayout as RectTransform);

        Vector2 targetPos = new(target.anchoredPosition.x, selectRect.anchoredPosition.y);

        AnimationManager.Instance.PlayAnchorMove(selectRect, targetPos);
    }

    public override void Hide(ShowMode showMode = ShowMode.Fade, UnityEngine.Events.UnityAction onFinished = null)
    {
        if (currentDisplayedCard != null && currentDisplayedCard.HasLoopSound)
            currentDisplayedCard.OnDetailClose();
        base.Hide(showMode, onFinished);
    }

    public override void Minimize(Transform shortcut)
    {
        if (currentDisplayedCard != null && currentDisplayedCard.HasLoopSound)
            currentDisplayedCard.OnDetailClose();
        base.Minimize(shortcut);
    }

    /// <summary>
    /// 显示玩家状态的变化值
    /// </summary>
    /// <param name="state"></param>
    /// <param name="delta"></param>
    public void ShowStateChange(PlayerStateEnum state, float delta, Vector3 center)
    {
        var stateWindow = WindowsManager.Instance.OpenWindow("State") as StateWindow;

        ShowStateChange(stateWindow.stateSliders[state].icon, StateManager.Instance.PlayerStateDict[state].MaxValue, delta, center);
    }

    /// <summary>
    /// 显示玩家状态的变化值
    /// </summary>
    /// <param name="state"></param>
    /// <param name="delta"></param>
    public void ShowStateChange(EnvironmentStateEnum state, float delta, Vector3 center)
    {
        var envWindow = WindowsManager.Instance.OpenWindow("EnvironmentBag") as EnvironmentBagWindow;

        float maxValue;
        if (state == EnvironmentStateEnum.Electricity)
            maxValue = ElectricPowerManager.Instance.Power.MaxValue;
        else if (state == EnvironmentStateEnum.WaterLevel)
            maxValue = StateManager.Instance.WaterLevel.MaxValue;
        else
            maxValue = GameManager.Instance.CurEnvironmentBag.StateDict[state].MaxValue;

        ShowStateChange(envWindow.continuousValueStates[state].icon, maxValue, delta, center);
    }

    /// <summary>
    /// 显示玩家状态的变化值
    /// </summary>
    /// <param name="state"></param>
    /// <param name="delta"></param>
    public void ShowStateChange(Image icon, float stateMaxValue, float delta, Vector3 center)
    {
        AnimationManager.Instance.PlayStateIconFly(icon, icon.sprite, center, stateMaxValue, delta);
    }
}
