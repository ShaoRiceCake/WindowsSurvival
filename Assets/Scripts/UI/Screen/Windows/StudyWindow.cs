using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class StudyWindow : WindowBase
{
    [SerializeField] private UITechNode detailsUINode;
    [SerializeField] private Text techName;
    [SerializeField] private Text techDescription;

    [SerializeField] private StudyButton studyButton;
    [SerializeField] private UIStateSlider progressSlider;
    [SerializeField] private Text studyInfo;
    [SerializeField] private Text studyTime;

    [SerializeField] private Transform detailLayout;
    [SerializeField] private Transform menuLayout;
    [SerializeField] private Transform content;
    [SerializeField] private Transform[] intermediateTechLocks;
    private Color lockedColor;

    [SerializeField] private GameObject prerequisite;
    [SerializeField] private GameObject unlockRecipe;

    [SerializeField] private RectTransform selectRect;

    [SerializeField] private GameObject recipeItem;
    [SerializeField] private GameObject prerequisitePrefab;

    [SerializeField] private Text currentStudiedText;
    [SerializeField] private Text leftTimeText;
    [SerializeField] private UITechNode[] studyQueueNodePlaceHolders;

    private Color defaultTechNameColor;
    private Color defaultTechDescriptionColor;
    private Color defaultStudyInfoColor;
    private Color defaultStudyTimeColor;

    private int studyState;
    [SerializeField] private HoverableButton studyStateButton; // 显示研究状态的按钮
    [SerializeField] private Animator studyStateButtonAnimator;

    private ScriptableTechnologyNode curSelectedTechNode; // 记录当前选中的科技节点

    private Dictionary<TechType, RectTransform> menuItemTransforms = new();
    private Dictionary<TechType, (UITechNode[] uiNodes, Transform root)> uiNodesRoot = new();

    #region Init
    protected override void Awake()
    {
        base.Awake();

        lockedColor = intermediateTechLocks[0].GetComponentInChildren<Image>().color;
        defaultTechNameColor = techName.color;
        defaultTechDescriptionColor = techDescription.color;
        defaultStudyInfoColor = studyInfo.color;
        defaultStudyTimeColor = studyTime.color;

        LayoutRebuilder.ForceRebuildLayoutImmediate(menuLayout as RectTransform);

        // 初始化所有科技节点的 UI
        for (int i = 0; i < content.childCount; i++)
        {
            var root = content.GetChild(i);
            var techType = Enum.Parse<TechType>(root.name);
            var uiNodes = root.GetComponentsInChildren<UITechNode>();

            uiNodesRoot.Add(techType, (uiNodes, root));

            foreach (var uiNode in uiNodes)
            {
                var node = TechnologyManager.Instance.GetTechNodeByName(uiNode.name);
                uiNode.Init(node);
                uiNode.onClick.RemoveAllListeners();
                uiNode.onClick.AddListener(() =>
                {
                    if (curSelectedTechNode == node) return;

                    curSelectedTechNode = node;
                    DisplayTechNodeDetails(node, false);
                });
            }
        }

        // 初始化菜单按钮
        menuItemTransforms.Clear();
        for (int i = 0; i < menuLayout.childCount; i++)
        {
            var child = menuLayout.GetChild(i);
            var button = child.GetComponent<HoverableButton>();
            var type = Enum.Parse<TechType>(child.name);
            button.onClick.AddListener(() =>
            {
                curSelectedTechNode = null;
                DisplayTechTree(type, false);
            });
            menuItemTransforms.Add(type, child as RectTransform);
        }

        EventManager.Instance.AddListener(EventType.RefreshStudyWindow, RefreshDisplay);
        EventManager.Instance.AddListener(EventType.StopStudy, OnStopStudy);
        EventManager.Instance.AddListener<string>(EventType.InterruptStudy, OnStudyInterrupted);
        EventManager.Instance.AddListener<ScriptableTechnologyNode>(EventType.StartStudy, OnStartStudy);
        EventManager.Instance.AddListener<ScriptableTechnologyNode>(EventType.ComplishStudy, OnComplishStudy);
    }

    private void OnDestroy()
    {
        curSelectedTechNode = null;
        menuItemTransforms.Clear();
        uiNodesRoot.Clear();
        EventManager.Instance.RemoveListener(EventType.RefreshStudyWindow, RefreshDisplay);
        EventManager.Instance.RemoveListener(EventType.StopStudy, OnStopStudy);
        EventManager.Instance.RemoveListener<string>(EventType.InterruptStudy, OnStudyInterrupted);
        EventManager.Instance.RemoveListener<ScriptableTechnologyNode>(EventType.StartStudy, OnStartStudy);
        EventManager.Instance.RemoveListener<ScriptableTechnologyNode>(EventType.ComplishStudy, OnComplishStudy);
    }

    protected override void Init()
    {
        // 初始化研究状态按钮
        if (GameDataManager.Instance.CurLoad.skipGuide || GameDataManager.Instance.WindowsData.unlockedShortcuts.Contains(AppName))
            DisplayStudyState(TechnologyManager.Instance.IsStudying ? 1 : 2, null);
        else
            studyStateButton.gameObject.SetActive(false);

        DisplayStudyQueue();
        DisplayIntermediateTechLock();

        ShowSelected();
    }

    public override void Show(ShowMode showMode = ShowMode.Fade, UnityAction onFinished = null)
    {
        base.Show(showMode, onFinished);

        ShowSelected();
    }

    private void ShowSelected()
    {
        // 如果没有当前选择的节点，则尝试选择正在研究的节点
        if (curSelectedTechNode == null)
            curSelectedTechNode = TechnologyManager.Instance.CurStudiedTechNode;

        DisplayTechTree(curSelectedTechNode == null ? 0 : curSelectedTechNode.techType, false);
    }

    public override void Hide(ShowMode showMode = ShowMode.Fade, UnityAction onFinished = null)
    {
        base.Hide(showMode, onFinished);
        curSelectedTechNode = null;
    }
    #endregion

    #region 事件监听
    private void OnStartStudy(ScriptableTechnologyNode techNode)
    {
        RefreshDisplay();
        DisplayStudyState(1, techNode);
    }

    private void OnComplishStudy(ScriptableTechnologyNode techNode)
    {
        curSelectedTechNode = techNode;
        RefreshDisplay();
        DisplayStudyState(0, techNode);
        AnimationManager.Instance.ShowFloatingTipAbove(studyStateButton.transform, $"\"{techNode.techName}\"研究完成！", 1.4f);
    }

    private void OnStopStudy()
    {
        RefreshDisplay();
        DisplayStudyState(2, null);
    }

    private void OnStudyInterrupted(string reason)
    {
        if (string.IsNullOrEmpty(reason)) return;

        AnimationManager.Instance.ShowFloatingTipAbove(studyStateButton.transform, $"{reason}，研究中止！", 1.4f);
        SoundManager.Instance.PlaySound("错误提示");
    }

    public void RefreshDisplay()
    {
        if (curSelectedTechNode != null)
            DisplayTechTree(curSelectedTechNode.techType, true);

        // 刷新队列ui
        DisplayStudyQueue();

        // 刷新中级科技ui
        DisplayIntermediateTechLock();
    }
    #endregion

    private void DisplayIntermediateTechLock()
    {
        var locked = GlobalDataManager.Instance.GetCardNum("数据传输台") <= 0;
        foreach (var mask in intermediateTechLocks)
        {
            foreach (var img in mask.GetComponentsInChildren<Image>())
            {
                img.color = locked ? lockedColor : ColorManager.White;
            }
        }
    }

    private void DisplayStudyQueue()
    {
        var studyQueue = TechnologyManager.Instance.StudyQueue;

        for (int i = 0; i < studyQueueNodePlaceHolders.Length; i++)
        {
            var uiNode = studyQueueNodePlaceHolders[i];

            if (i >= studyQueue.Count)
            {
                uiNode.gameObject.SetActive(false);
                continue;
            }

            uiNode.gameObject.SetActive(true);
            var techNode = TechnologyManager.Instance.GetTechNodeByName(studyQueue[i]);
            uiNode.Display(techNode);
            uiNode.onClick.RemoveAllListeners();
            uiNode.onClick.AddListener(() =>
            {
                if (curSelectedTechNode == techNode) return;

                curSelectedTechNode = techNode;
                DisplayTechTree(techNode.techType, false);
            });
        }

        // 显示剩余研究时间
        if (studyQueue.Count > 0)
        {
            leftTimeText.transform.parent.gameObject.SetActive(true);
            leftTimeText.text = GetLeftStudyTimeString(TechnologyManager.Instance.GetTechNodeByName(studyQueue[0]));
        }
        else
        {
            leftTimeText.transform.parent.gameObject.SetActive(false);
        }

        if (TechnologyManager.Instance.IsStudying)
        {
            currentStudiedText.text = "正在研究";
            currentStudiedText.color = ColorManager.White;
        }
        else
        {
            currentStudiedText.text = "未在研究";
            currentStudiedText.color = ColorManager.Red;
        }
    }

    private void DisplayTechTree(TechType type, bool playAnim)
    {
        // 只显示对应类型的科技节点
        foreach (var (techType, uiNodesRoot) in uiNodesRoot)
        {
            if (techType != type)
            {
                // 失活不需要显示的根节点
                uiNodesRoot.root.gameObject.SetActive(false);
                continue;
            }

            // 刷新需要显示的节点
            uiNodesRoot.root.gameObject.SetActive(true);
            foreach (var uiNode in uiNodesRoot.uiNodes)
            {
                uiNode.RefreshDisplay();
            }

            // 默认选择第一个科技节点
            if (curSelectedTechNode == null)
                curSelectedTechNode = TechnologyManager.Instance.GetTechNodeByName(uiNodesRoot.uiNodes[0].name);
        }

        DisplayTechNodeDetails(curSelectedTechNode, playAnim);

        SelectTechTreeWithTween(type);
    }

    private void SelectTechTreeWithTween(TechType type)
    {
        Vector2 targetPos = new(menuItemTransforms[type].anchoredPosition.x, selectRect.anchoredPosition.y);

        AnimationManager.Instance.PlayAnchorMove(selectRect, targetPos);
    }

    private List<GameObject> temp = new();

    private void DisplayTechNodeDetails(ScriptableTechnologyNode techNode, bool playAnim)
    {
        // 回收前置研究和解锁配方对应的预制体
        foreach (var obj in temp)
        {
            ObjectBufferPool.Instance.Restore(obj);
        }
        temp.Clear();

        // 显示科技的名称和描述
        detailsUINode.Display(techNode);
        techName.text = techNode.techName;
        techDescription.text = techNode.techDescription;

        // 显示科技的前置研究项目
        prerequisite.SetActive(techNode.prerequisites.Count != 0);
        UIStateToggle toggle;
        foreach (var prerequisite in techNode.prerequisites)
        {
            toggle = ObjectBufferPool.Instance.Get(prerequisitePrefab, detailLayout).GetComponentInChildren<UIStateToggle>();
            unlockRecipe.transform.SetAsLastSibling();
            toggle.SetStateName(prerequisite.techName);
            toggle.SetValue(TechnologyManager.Instance.IsTechNodeComplished(prerequisite));
            temp.Add(toggle.gameObject);
        }

        // 显示可以解锁的配方
        HoverableButton button;
        foreach (var recipe in techNode.recipes)
        {
            button = ObjectBufferPool.Instance.Get(recipeItem, detailLayout).GetComponent<HoverableButton>();
            button.image.sprite = recipe.CardImage;
            button.text.text = recipe.cardId;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                (WindowsManager.Instance.OpenWindow("Details") as DetailsWindow).Display(recipe.CardInstance, DisplayType.DetailsAndCraftButton);
            });

            temp.Add(button.gameObject);
        }

        // 研究状态
        var techNodeState = TechnologyManager.Instance.GetTechNodeState(techNode, out string lockedReason, out int order);
        ApplyLockedDetailsVisual(techNodeState == TechNodeState.Locked);

        // 显示研究按钮
        studyButton.Display(techNode, techNodeState);

        // 显示研究进度
        progressSlider.gameObject.SetActive(true);
        var progress = TechnologyManager.Instance.GetStudyProgress(techNode);
        progressSlider.SetValue(progress, techNode.cost, playAnim);
        progressSlider.SetLockedVisual(techNodeState == TechNodeState.Locked);

        // 显示剩余研究时间
        studyTime.transform.parent.gameObject.SetActive(true);
        studyTime.text = GetLeftStudyTimeString(techNode);

        // 显示研究进度与其他信息
        studyInfo.gameObject.SetActive(true);
        switch (techNodeState)
        {
            case TechNodeState.Locked:
                studyInfo.text = lockedReason;
                break;
            case TechNodeState.BeingStudied:
                studyInfo.text = $"+{TechnologyManager.BASIC_STUDY_RATE:0.0}科技点/15min";
                break;
            case TechNodeState.Complished:
                studyInfo.gameObject.SetActive(false);
                //progressSlider.gameObject.SetActive(false);
                studyTime.transform.parent.gameObject.SetActive(false);
                break;
            case TechNodeState.Queued:
                studyInfo.text = $"研究顺位:  第 {order + 1} 位";
                break;
            case TechNodeState.ToStudy:
                studyInfo.gameObject.SetActive(false);
                break;
        }
    }

    private void ApplyLockedDetailsVisual(bool locked)
    {
        var color = locked ? ColorManager.DarkGrey : ColorManager.White;
        techName.color = locked ? ColorManager.DarkGrey : defaultTechNameColor;
        techDescription.color = locked ? ColorManager.DarkGrey : defaultTechDescriptionColor;
        studyInfo.color = locked ? ColorManager.DarkGrey : defaultStudyInfoColor;
        studyTime.color = locked ? ColorManager.DarkGrey : defaultStudyTimeColor;

        foreach (var obj in temp)
        {
            var button = obj.GetComponent<HoverableButton>();
            if (button != null)
            {
                button.currentColor = button.hoveredColor = color;
                if (button.image != null) button.image.color = color;
                if (button.text != null) button.text.color = color;
            }

            var toggle = obj.GetComponentInChildren<UIStateToggle>();
            if (locked && toggle != null)
                toggle.SetLockedVisual();
        }
    }

    private string GetLeftStudyTimeString(ScriptableTechnologyNode node)
    {
        var leftProgress = node.cost - TechnologyManager.Instance.GetStudyProgress(node);
        var time = Mathf.CeilToInt(leftProgress * 15f / TechnologyManager.BASIC_STUDY_RATE);
        int hour = time / 60;
        int minute = time % 60;
        StringBuilder sb = new();
        if (hour > 0)
            sb.Append(hour + "h");
        if (minute > 0)
            sb.Append(minute + "min");
        return sb.ToString();
    }

    private void DisplayStudyState(int state, ScriptableTechnologyNode techNode)
    {
        if (TechnologyManager.Instance.AllTechComplished)
        {
            studyStateButton.gameObject.SetActive(false);
            return;
        }

        if (studyState == state) return;

        studyStateButton.gameObject.SetActive(true);

        studyStateButton.onClick.RemoveAllListeners();
        studyStateButton.onClick.AddListener(() =>
        {
            WindowsManager.Instance.OpenWindow("Study");
        });

        studyStateButton.transform.GetChild(2).gameObject.SetActive(state == 2);
        studyStateButton.transform.GetChild(1).gameObject.SetActive(state != 2);

        string text = "";
        Color color = ColorManager.White;
        switch (state)
        {
            // 研究完成
            case 0:
                text = "研究完成";
                color = ColorManager.Cyan;
                studyStateButtonAnimator.Play("Default");
                break;
            // 开始研究
            case 1:
                text = "正在研究";
                color = ColorManager.White;
                studyStateButtonAnimator.Play("StudyingGif");
                studyStateButton.onClick.AddListener(() =>
                {
                    curSelectedTechNode = techNode;
                    RefreshDisplay();
                });
                break;
            // 未在研究
            case 2:
                text = "未在研究";
                color = ColorManager.Red;
                break;
        }

        studyStateButton.GetComponentInChildren<Text>().text = text;
        studyStateButton.hoveredColor = studyStateButton.currentColor = color;
        studyStateButton.ChangeColor(color);

        studyState = state;
    }

    protected override void OnFocused()
    {
        if (studyState == 0)
            // 显示未在研究
            DisplayStudyState(2, null);
    }
}
