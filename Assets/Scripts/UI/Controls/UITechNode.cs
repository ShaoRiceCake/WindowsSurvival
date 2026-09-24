using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UITechNode : HoverableButton
{
    public Text techName;
    public Text progressText;
    public GameObject checkIcon;
    public GameObject lockIcon;
    public RectTransform recipeLayout;
    public GameObject recipeIconPrefab;
    public GameObject baseLayer;
    public RectTransform fillMask;
    public RectTransform queueInfo;
    public Animator gifAnimator;
    public Text orderText;
    public HoverableButton dequeueButton;
    public UITechNodeConnectionLine[] lines;

    private GameObject fillLayer;
    private ScriptableTechnologyNode techNode;
    private TechNodeState currentState;
    private int studyOrder;

    private float originalFillMaskWidth = -1;
    private float originalQueueInfoAnchorPosX;
    private float animTransition = 0.4f;

    protected override void Awake()
    {
        base.Awake();

        lines = GetComponentsInChildren<UITechNodeConnectionLine>(true);
        foreach (var line in lines)
        {
            line.Init();
        }
    }

    public void Init(ScriptableTechnologyNode techNode)
    {
        this.techNode = techNode;

        ObjectBufferPool.Instance.RestoreAllChildren(recipeLayout);
        if (fillLayer != null)
        {
            Destroy(fillLayer);
            fillLayer = null;
        }

        if (queueInfo != null)
        {
            queueInfo.gameObject.SetActive(false);
            if (originalQueueInfoAnchorPosX == 0)
                originalQueueInfoAnchorPosX =  queueInfo.anchoredPosition.x;
            queueInfo.anchoredPosition = Vector2.zero;

            // 点击取消排队按钮，从研究队列中移除该科技
            dequeueButton.onClick.RemoveAllListeners();
            dequeueButton.onClick.AddListener(() =>
            {
                TechnologyManager.Instance.RemoveFromStudyQueue(techNode);
            });
        }

        foreach (var recipe in techNode.recipes)
        {
            var image = ObjectBufferPool.Instance.Get(recipeIconPrefab, recipeLayout).GetComponent<Image>();
            image.sprite = recipe.CardImage;
            var tip = image.GetComponent<HoverTipController>() ?? image.gameObject.AddComponent<HoverTipController>();
            tip.SetTip(recipe.cardId + (recipe.outputCount > 1 ? $"（每次产出{recipe.outputCount}个）" : "") + "\n" + recipe.CardInstance.CardDesc);
        }

        techName.text = techNode.techName;

        // 克隆 baseLayer 得到 fillLayer
        fillLayer = Instantiate(baseLayer, fillMask);
        (fillLayer.transform as RectTransform).anchoredPosition = new(1, 0);
        SetColor(fillLayer.transform, ColorManager.Cyan);

        if (originalFillMaskWidth < 0)
            originalFillMaskWidth = fillMask.sizeDelta.x;
        fillMask.DOKill();
        fillMask.sizeDelta = new(0, fillMask.sizeDelta.y);

        currentState = TechnologyManager.Instance.GetTechNodeState(techNode, out studyOrder);
        Display(false);
    }

    private void SetColor(Transform layer, Color color)
    {
        foreach (var img in layer.GetComponentsInChildren<Image>(true))
        {
            img.color = color;
        }
    }

    public void RefreshDisplay()
    {
        if (techNode == null) return;

        // 刷新连接线显示
        foreach (var line in lines)
        {
            line.RefreshDisplay();
        }

        var newState = TechnologyManager.Instance.GetTechNodeState(techNode, out studyOrder);

        // 前后状态相同时，不刷新显示
        // 排除排队和正在学习状态
        if (currentState == newState && currentState != TechNodeState.BeingStudied && currentState != TechNodeState.Queued) return;

        currentState = newState;
        Display(true);
    }

    private void Display(bool playAnim)
    {
        SetColor(baseLayer.transform, ColorManager.White);
        SetRecipeVisualColor(ColorManager.White);
        techName.color = ColorManager.White;

        // 显示研究进度
        var progress = TechnologyManager.Instance.GetStudyProgress(techNode);
        //progressText.gameObject.SetActive(true);
        //progressText.text = $"{progress}/{techNode.cost}";
        fillMask.gameObject.SetActive(true);
        fillMask.DOKill();
        var targetWidth = originalFillMaskWidth * progress / techNode.cost;
        if (playAnim)
            fillMask.DOSizeDelta(new(targetWidth, fillMask.sizeDelta.y), animTransition);
        else
            fillMask.sizeDelta = new(targetWidth, fillMask.sizeDelta.y);

        lockIcon.SetActive(false);
        checkIcon.SetActive(false);

        switch (currentState)
        {
            case TechNodeState.Locked:
                Locked();
                break;
            case TechNodeState.ToStudy:
                ToStudy();
                break;
            case TechNodeState.BeingStudied:
                BeingStudied();
                break;
            case TechNodeState.Complished:
                Complished();
                break;
            case TechNodeState.Queued:
                Queued();
                break;
        }
    }

    public void Display(ScriptableTechnologyNode techNode)
    {
        if (this.techNode == techNode)
        {
            RefreshDisplay();
            return;
        }

        Init(techNode);
    }

    private void Locked()
    {
        lockIcon.SetActive(true);
        SetColor(baseLayer.transform, ColorManager.DarkGrey);
        SetRecipeVisualColor(ColorManager.DarkGrey);
        techName.color = ColorManager.DarkGrey;
        fillMask.gameObject.SetActive(false);
        progressText.gameObject.SetActive(false);
        Dequeue();
    }

    private void SetRecipeVisualColor(Color color)
    {
        foreach (var image in recipeLayout.GetComponentsInChildren<Image>(true))
        {
            image.color = color;
        }
    }

    private void Complished()
    {
        checkIcon.SetActive(true);
        progressText.gameObject.SetActive(false);
        Dequeue();
    }

    private void BeingStudied()
    {
        Enqueue(true);
    }

    private void ToStudy()
    {
        Dequeue();
    }

    private void Queued()
    {
        Enqueue(false);
    }

    private void Enqueue(bool playGif)
    {
        if (queueInfo == null) return;

        queueInfo.DOKill();
        queueInfo.gameObject.SetActive(true);
        queueInfo.DOAnchorPosX(originalQueueInfoAnchorPosX, animTransition);

        if (playGif)
            gifAnimator.Play("StudyingGif");
        else
            gifAnimator.Play("Default");

        orderText.text = (studyOrder + 1).ToString();
    }

    private void Dequeue()
    {
        if (queueInfo == null) return;

        queueInfo.DOKill();
        queueInfo.DOAnchorPosX(0, animTransition).OnComplete(() =>
        {
            queueInfo.gameObject.SetActive(false);
        });
    }
}
