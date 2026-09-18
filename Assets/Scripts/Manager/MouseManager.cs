using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum MouseState
{
    Default,
    Click,
    ClickDown,
    Drag,
    ResizeMainDiagonal,
    ResizeCounterDiagonal,
    ResizeX,
    ResizeY,
}

public class MouseManager : MonoBehaviour
{
    private static MouseManager instance;
    public static MouseManager Instance => instance;
    private static bool systemCursorForUI;
    private static bool transitionCursor;
    public static void SetTransitionCursor(bool visible)
    {
        transitionCursor = visible; RefreshCursorPresentation();
    }

    // One owner for both pointers. Modal windows request a handoff only on open/close.
    public static void SetSystemCursorForUI(bool visible)
    {
        systemCursorForUI = visible;
        RefreshCursorPresentation();
    }

    private static void RefreshCursorPresentation()
    {
        bool showSystem = systemCursorForUI || transitionCursor || instance == null;
        if (Cursor.visible != showSystem) Cursor.visible = showSystem;
        if (instance == null) return;
        instance.GetComponent<Image>().enabled = !showSystem;
        instance.animator.gameObject.SetActive(instance._isWaiting && !showSystem);
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) RefreshCursorPresentation();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        RefreshCursorPresentation();
    }

    public const float DEFAULT_WAIT_TIME = 19f / 24;

    public Stack<ChangeMouseType> curChangeMouseType = new Stack<ChangeMouseType>();

    public Sprite DefaultSprite; // 默认
    public Sprite ClickSprite; // 点击
    public Sprite ClickDownSprite;//点下
    public Sprite DragSprite; // 拖拽
    public Sprite ResizeCornerSprite; // 右上角
    public Sprite ResizeSideSprite; //Y轴
    public Sprite InputSprite; // 输入框

    [SerializeField] private bool isDragging;

    public Animator animator;
    public CanvasGroup mouseCanvasGroup;


    public void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            instance = this;
        }
        animator.gameObject.SetActive(false);
        ChangeMouseState(MouseState.Default);
        RefreshCursorPresentation();
    }

    public void Update()
    {
        SetCursor();
    }

    private void SetCursor()
    {
        //设置鼠标位置
        Vector3 curTransform = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        curTransform.z = 0;
        transform.position = curTransform;
    }

    public void StartDragging()
    {
        ChangeMouseState(MouseState.Drag);
        isDragging = true;
    }

    public void EndDragging()
    {
        isDragging = false;
        ChangeMouseState(MouseState.Default);
    }

    public void ChangeMouseState(MouseState mouseState)
    {
        if (isDragging) return;

        ResetRotation(mouseState);
        switch (mouseState)
        {
            case MouseState.Default:
                ResetPivot(DefaultSprite);
                GetComponent<Image>().sprite = DefaultSprite;
                break;
            case MouseState.Click:
                ResetPivot(ClickSprite);
                GetComponent<Image>().sprite = ClickSprite;
                break;
            case MouseState.ClickDown:
                ResetPivot(ClickDownSprite);
                GetComponent<Image>().sprite = ClickDownSprite;
                break;
            case MouseState.Drag:
                ResetPivot(DragSprite);
                GetComponent<Image>().sprite = DragSprite;
                break;
            case MouseState.ResizeMainDiagonal:
                ResetPivot(ResizeCornerSprite);
                GetComponent<Image>().sprite = ResizeCornerSprite;
                break;
            case MouseState.ResizeCounterDiagonal:
                ResetPivot(ResizeCornerSprite);
                GetComponent<Image>().sprite = ResizeCornerSprite;
                break;
            case MouseState.ResizeX:
                ResetPivot(ResizeSideSprite);
                GetComponent<Image>().sprite = ResizeSideSprite;
                break;
            case MouseState.ResizeY:
                ResetPivot(ResizeSideSprite);
                GetComponent<Image>().sprite = ResizeSideSprite;
                break;
            default:
                break;
        }
    }


    //根据图片设置组件中心点位置
    public void ResetPivot(Sprite sprite)
    {
        // 1. 获取pivot（像素单位）
        Vector2 pivotPixel = sprite.pivot;

        // 2. 获取pivot（归一化到0~1）
        Vector2 pivotNormalized = new Vector2(
            sprite.pivot.x / sprite.rect.width,
            sprite.pivot.y / sprite.rect.height
        );

        // 3. 设置RectTransform的pivot
        GetComponent<RectTransform>().pivot = pivotNormalized;

    }

    public void ResetRotation(MouseState mouseState)
    {
        GetComponent<RectTransform>().rotation = mouseState switch
        {
            MouseState.ResizeCounterDiagonal => Quaternion.Euler(0, 0, 90),
            MouseState.ResizeX => Quaternion.Euler(0, 0, 90),
            _ => Quaternion.Euler(0, 0, 0),
        };
    }

    #region 等待
    public bool IsBusy => isDragging || _isWaiting;
    private float _startTime;   // 等待开始时间
    private float _endTime;     // 等待结束时间
    private bool _isWaiting;    // 当前是否在等待

    private void StartWaiting()
    {
        _isWaiting = true;

        // 禁用鼠标
        SetMouseEnabled(false);

        // 隐藏鼠标
        SetMouseVisible(false);

        // 播放动画
        RefreshCursorPresentation();
        animator.Play("Waiting");

        PublicMono.Instance.StartCoroutine(WaitingCo());
    }

    private IEnumerator WaitingCo()
    {
        while (Time.time < _endTime)
        {
            yield return null;
        }

        EndWaiting();
    }

    private void EndWaiting()
    {
        _isWaiting = false;

        // 停止动画
        animator.Play("");
        animator.gameObject.SetActive(false);

        // 显示鼠标
        SetMouseVisible(true);

        // 启用鼠标
        SetMouseEnabled(true);
    }

    /// <summary>
    /// 设置等待时间（如果已经在等待，则延长等待时间）
    /// </summary>
    /// <param name="waitTime">等待时间（秒）</param>
    public void Wait(float waitTime = DEFAULT_WAIT_TIME)
    {
        var currentTime = Time.time;
        if (!_isWaiting)
            _startTime = currentTime;

        // 保证等待时间是动画时长的 周期 或 半周期
        var deltaTime = currentTime + waitTime - _startTime;
        deltaTime = Mathf.CeilToInt(deltaTime * 2 / DEFAULT_WAIT_TIME) * DEFAULT_WAIT_TIME / 2;
        // 更新等待结束时间
        _endTime = Mathf.Max(_endTime, _startTime + deltaTime);

        // 开始等待
        if (!_isWaiting)
            StartWaiting();
    }

    private void SetMouseVisible(bool visible)
    {
        mouseCanvasGroup.alpha = visible ? 1 : 0;
    }

    private void SetMouseEnabled(bool enabled)
    {
        mouseCanvasGroup.interactable = !enabled;
    }
    #endregion
}

