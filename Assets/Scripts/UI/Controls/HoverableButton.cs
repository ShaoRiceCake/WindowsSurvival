using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverableButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Image image;                     // 正常状态的图像
    public Text text;                       // 正常状态的图像
    public float minWidth;                  // 最小宽度
    public float reservedWidth;             // 预留长度
    public List<Graphic> hoveredGraphics;   // 鼠标悬停时显示的图像
    public float fadeTransition = 0.1f;     // 淡入淡出持续时间

    public Text[] textsNeedToReverseColor;
    public Image[] imagseNeedToReverseColor;

    public CanvasGroup canvasGroup {  get; private set; }

    public string hoveredAudio = "临时悬浮";
    public bool playHoverSound = true;
    public bool useUnscaledTime;

    public Color currentColor { get; set; }
    public Color hoveredColor { get; set; } = ColorManager.White; // 鼠标悬停时的颜色，默认为白色

    public UnityEvent onClick { get; set; } = new UnityEvent();
    public UnityEvent onPointerEnter { get; set; } = new UnityEvent();
    public UnityEvent onPointerExit { get; set; } = new UnityEvent();

    [HideInInspector] public RectTransform rectTransform;

    public bool Interactable
    {
        get => interactable;
        set
        {
            interactable = value;
            if (!value)
            {
                foreach (var graphic in hoveredGraphics)
                {
                    graphic.DOKill();
                    graphic.gameObject.SetActive(false); // 确保初始状态下图像不可见
                    graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, 0f); // 设置透明度为0
                }
            }
            else
            {
                ChangeColor(currentColor);
            }

            //var changeMouse = GetComponentInChildren<ChangeMouse>();
            //if (changeMouse != null) changeMouse.enabled = value;
            if (canvasGroup != null)
                canvasGroup.interactable = value;
        }
    }

    [SerializeField] private bool interactable = true;

    protected virtual void Awake()
    {
        rectTransform = transform as RectTransform;

        if (image != null)
            currentColor = image.color;
        else
            currentColor = ColorManager.White;

        var hoveredGraphic = transform.Find("Hovered");
        if (hoveredGraphic != null)
            hoveredGraphics.AddRange(hoveredGraphic.GetComponentsInChildren<Graphic>());

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Interactable = interactable;
    }

    protected virtual void OnEnable()
    {
        if (minWidth == 0)
            minWidth = rectTransform.sizeDelta.x;

        // 初始化时确保hoveredImage是透明的
        foreach (var graphic in hoveredGraphics)
        {
            graphic.gameObject.SetActive(false); // 确保初始状态下图像不可见
            graphic.color = new Color(graphic.color.r, graphic.color.g, graphic.color.b, 0f); // 设置透明度为0
        }

        ChangeColor(currentColor);
    }

    protected virtual void OnDisable()
    {
        // 清理DOTween动画
        foreach (var graphic in hoveredGraphics)
        {
            graphic.DOKill(); // 停止所有正在进行的动画
        }

        rectTransform.sizeDelta = new(minWidth, rectTransform.sizeDelta.y);
    }

    public void AdaptWidth()
    {
        var newWidth = Mathf.Max(minWidth, text.preferredWidth + reservedWidth);
        rectTransform.sizeDelta = new(newWidth, rectTransform.sizeDelta.y);
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable) return; // 如果不可交互，则不处理点击事件
        StopBlinking();
        onClick?.Invoke();
        OnPointerEnter(eventData); // 点击时触发鼠标悬停事件
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (!interactable) return;

        if (playHoverSound && SoundManager.Instance != null)
        SoundManager.Instance.PlaySound(hoveredAudio, true,0.2f);

        onPointerEnter?.Invoke();

        // 激活图像并开始淡入动画
        foreach (var graphic in hoveredGraphics)
        {
            graphic.gameObject.SetActive(true); // 确保图像可见
            graphic.DOKill(); // 停止所有正在进行的动画
            graphic.DOFade(1f, fadeTransition)
                .SetUpdate(useUnscaledTime).SetEase(Ease.OutQuad)
                .OnStart(() =>
                {
                    ChangeColor(ColorManager.Black); // 反色
                    graphic.color = hoveredColor; // 改变悬浮框的颜色
                }); // 在动画开始时改变颜色
        }
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (!interactable) return;

        onPointerExit?.Invoke();

        // 开始淡出动画，完成后禁用图像
        foreach (var graphic in hoveredGraphics)
        {
            graphic.DOKill(); // 停止所有正在进行的动画
            graphic.DOFade(0f, fadeTransition)
                .SetUpdate(useUnscaledTime).SetEase(Ease.InQuad)
                .OnComplete(() => graphic.gameObject.SetActive(false)) // 动画完成后禁用图像
                .OnStart(() => ChangeColor(currentColor));
        }
    }

    public void ChangeColor(Color color)
    {
        foreach (var text in textsNeedToReverseColor)
        {
            text.color = color;
        }
        foreach (var image in imagseNeedToReverseColor)
        {
            image.color = color;
        }
    }

    private bool isBlinking = false;
    public void StartBlinking(float interval = .5f)
    {
        if (isBlinking) return;

        isBlinking = true;
        currentColor = Color.white;
        ChangeColor(ColorManager.White);

        canvasGroup.DOFade(0f, interval)
            .SetLoops(-1, LoopType.Yoyo) // Yoyo 模式让动画往返播放
            .SetEase(Ease.Linear);
    }

    public void StopBlinking()
    {
        if (!isBlinking) return;

        isBlinking = false;
        canvasGroup.DOKill();
        canvasGroup.alpha = 1f;
    }
}