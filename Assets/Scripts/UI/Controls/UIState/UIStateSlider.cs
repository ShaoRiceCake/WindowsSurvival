using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIStateSlider : MonoBehaviour
{
    public Image icon;
    public Text stateNameText;
    public Text valueText;
    public Slider slider;

    public Image arrow;
    public RectTransform ceil;
    public RectTransform floor;
    public Sprite[] arrowSprites;

    public HoverableButton button;
    public HoverTipController tipController;

    public bool displayPercentage;          // 是否以百分比形式显示数值
    public int displayDigits;               // 显示几位小数

    private DangerLevelEnum curDangerLevel; // 当前危险等级
    private bool init;                      // 是否已初始化

    private int curChangeLavel;             // 当前变化率等级

    public Color fillColor = ColorManager.White;

    public float endValue { get; private set; }

    // 动效参数
    private float arrowMoveTransition = 0.35f;
    private float highDangerScale = 1.25f;
    private float lowDangerScale = 1.1f;
    private float highDangerTransition = 0.35f;
    private float lowDangerTransition = 0.35f;
    protected float valueTransition = 0.3f;

    private float GetArrowTweenPhase()
    {
        var cycle = arrowMoveTransition * 2f;
        if (cycle <= 0f) return 0f;
        return Mathf.Repeat(Time.time, cycle);
    }

    protected virtual void OnDisable()
    {
        init = false;
        curChangeLavel = 0;
        if (button != null) button.transform.DOKill();
        if (arrow != null) arrow.transform.DOKill();
        if (icon != null)
        {
            icon.transform.DOKill();
            icon.color = ColorManager.White;
        }
        if (slider != null) slider.DOKill();
        fillColor = ColorManager.White;
        if (button != null)
            button.currentColor = button.hoveredColor = ColorManager.White;
    }

    protected void UpdateSliderValue(float curValue, float maxValue, bool playAnim)
    {
        var endValue = curValue / maxValue;
        if (playAnim)
        {
            slider.DOKill();
            slider.DOValue(endValue, valueTransition).OnUpdate(() =>
            {
                if (valueText != null)
                    DisplayValueText(slider.value * maxValue, maxValue);
            });
        }
        else
        {
            slider.value = endValue;
            if (valueText != null)
                DisplayValueText(curValue, maxValue);
        }
    }

    public virtual void SetValue(float curValue, float maxValue, bool playAnim)
    {
        if (slider.fillRect.TryGetComponent<Image>(out var fill))
        {
            fill.color = fillColor;
        }

        if (slider.handleRect != null)
        {
            var handle = slider.handleRect.GetComponentInChildren<Image>();
            if (handle != null)
            {
                handle.color = fillColor;
            }
        }

        endValue = curValue / maxValue;

        UpdateSliderValue(curValue, maxValue, playAnim);
    }

    public void SetLockedVisual(bool locked)
    {
        var color = locked ? ColorManager.DarkGrey : ColorManager.White;
        if (icon != null) icon.color = color;
        if (stateNameText != null) stateNameText.color = color;
        if (valueText != null) valueText.color = color;
        if (button != null)
        {
            button.currentColor = button.hoveredColor = color;
            if (button.image != null) button.image.color = color;
        }
        if (slider != null)
        {
            if (slider.fillRect != null && slider.fillRect.TryGetComponent<Image>(out var fill)) fill.color = color;
            if (slider.handleRect != null && slider.handleRect.TryGetComponent<Image>(out var handle)) handle.color = color;
        }
    }

    protected virtual void DisplayValueText(float curValue, float maxValue)
    {
        if (displayPercentage)
            valueText.text = $"{curValue * 100 / maxValue: 0.0}%";
        else
            valueText.text = $"{curValue.ToString($"F{displayDigits}")}/{maxValue}";
    }

    public virtual void SetValue(State state, bool playAnim)
    {
        SetValue(state.CurValue, state.MaxValue, playAnim);

        // 显示变化率
        DisplayChangeRate(state.ChangeRate, state.CurValue, state.MaxValue, state.HigherIsBetter, state.LowerIsBetter, state.IsDecreaseNatural, state.IsIncreaseNatural);

        // 根据状态的危险程度，给予提示
        PlayerStateDangerAlert(state.DangerLevel);
    }

    private void PlayerStateDangerAlert(DangerLevelEnum dangerLevel)
    {
        if (init && curDangerLevel == dangerLevel) return;

        init = true;
        curDangerLevel = dangerLevel;

        button.transform.DOKill();
        button.transform.localScale = Vector3.one;

        var iconColor = ColorManager.White;

        switch (dangerLevel)
        {
            case DangerLevelEnum.High:
                IconSizeYoloTween(highDangerScale, highDangerTransition);
                iconColor = ColorManager.Red;       // 高危 -> 红色
                break;
            case DangerLevelEnum.Low:
                IconSizeYoloTween(lowDangerScale, lowDangerTransition);
                iconColor = ColorManager.Yellow;    // 低危 -> 黄色
                break;
            case DangerLevelEnum.None:
                iconColor = ColorManager.White;     // 无危险 -> 白色
                break;
        }

        // 根据危险状态，图标显示不同颜色
        if (icon != null && button != null && GetType() == typeof(UIStateSlider))
            icon.color = button.currentColor = button.hoveredColor = iconColor;
    }

    private void IconSizeYoloTween(float scaleSize, float duration)
    {
        var amplitude = Mathf.Max(0f, scaleSize - 1f);
        var minScale = Mathf.Max(0.01f, 1f - amplitude * 0.5f);
        var maxScale = Mathf.Max(minScale, scaleSize);

        button.transform.localScale = Vector3.one * minScale;
        var tween = button.transform.DOScale(maxScale, duration)
            .SetLoops(-1, LoopType.Yoyo) // 无限循环，来回播放
            .SetEase(Ease.InOutSine);    // 设置缓动效果

        tween.Goto(GetIconTweenPhase(duration), true);
    }

    private float GetIconTweenPhase(float duration)
    {
        var cycle = duration * 2f;
        if (cycle <= 0f) return 0f;
        return Mathf.Repeat(Time.time, cycle);
    }

    private void DisplayChangeRate(
        float changeRate,
        float curValue,
        float maxValue,
        bool higherIsBetter,
        bool lowerIsBetter,
        bool isDecreaseNatural,
        bool isIncreaseNatural)
    {
        if (changeRate == 0)
        {
            DisableArrow();
            tipController.enabled = false;
            curChangeLavel = 0;
            return;
        }

        // 悬浮显示
        tipController.enabled = true;

        string tip;
        // 百分比显示
        if (displayPercentage)
            tip = $"{changeRate / maxValue * 100:0.0}%";
        // 数值显示
        else
            tip = changeRate.ToString("0.0");

        tip = (changeRate > 0 ? "+" : "") + tip + "/15min";

        tipController.SetTip(tip);

        // 显示变化箭头
        DisplayChangeRateArrow(changeRate, curValue, maxValue, higherIsBetter, lowerIsBetter, isDecreaseNatural, isIncreaseNatural);
    }

    private void DisplayChangeRateArrow(
        float changeRate,
        float curValue,
        float maxValue,
        bool higherIsBetter,
        bool lowerIsBetter,
        bool isDecreaseNatural,
        bool isIncreaseNatural)
    {
        // 箭头显示
        var lastLevel = curChangeLavel;
        curChangeLavel = CalcLevel(changeRate / maxValue);

        // 变化率等级不变，不做处理
        if (curChangeLavel == lastLevel) return;

        // 处理状态值为极值的情况
        if (curValue <= 0 && changeRate < 0 && lowerIsBetter ||         // 如果当前值已经为0 且 变化率为负 且 低值更好
            curValue >= maxValue && changeRate > 0 && higherIsBetter)   // 当前值已经为最大值 且 变化率为正 且 高值更好
        {
            // 不显示箭头
            DisableArrow();
            return;
        }

        // 处理变化率为 +-1 的情况
        if (curChangeLavel == -1 && isDecreaseNatural ||    // 如果当前变化率为-1，且该状态是自然下降的
            curChangeLavel == 1 && isIncreaseNatural)       // 如果当前变化率为+1，且该状态是自然上升的
        {
            // 不显示箭头
            DisableArrow();
            return;
        }

        arrow.gameObject.SetActive(true);
        arrow.rectTransform.DOKill();

        // 设置贴图
        arrow.sprite = arrowSprites[Mathf.Abs(curChangeLavel) - 1];
        // 设置箭头方向
        arrow.transform.localEulerAngles = new Vector3(changeRate > 0 ? 0 : 180, 0, 0);

        Tween tween;
        if (changeRate > 0)
            tween = arrow.rectTransform.DOAnchorPos(ceil.anchoredPosition, arrowMoveTransition).From(floor.anchoredPosition).SetLoops(-1, LoopType.Yoyo);
        else
            tween = arrow.rectTransform.DOAnchorPos(floor.anchoredPosition, arrowMoveTransition).From(ceil.anchoredPosition).SetLoops(-1, LoopType.Yoyo);

        tween.Goto(GetArrowTweenPhase(), true);
    }

    private void DisableArrow()
    {
        arrow.rectTransform.DOKill();
        arrow.gameObject.SetActive(false);
    }

    /// <summary>
    /// 计算变化率等级
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    private int CalcLevel(float value)
    {
        int signal = value > 0 ? 1 : -1;
        var absValue = Mathf.Abs(value);
        if (absValue <= 0.05)
            return 1 * signal;
        else if (absValue <= 0.1)
            return 2 * signal;
        else
            return 3 * signal;
    }
}
