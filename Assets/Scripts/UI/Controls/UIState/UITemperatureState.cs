using UnityEngine;
using UnityEngine.UI;

public class UITemperatureState : UIStateSlider
{
    public Sprite[] levels;
    private float minimum;
    private bool body;

    public override void SetValue(State state, bool playAnim)
    {
        minimum = state.MinValue;
        body = Mathf.Approximately(state.MaxValue, 42);
        base.SetValue(state, playAnim);
        tipController.enabled = true;
        var env = GameManager.Instance.CurEnvironmentBag;
        if (env == null) return;
        if (body)
        {
            float effective = ClimateManager.Instance.EffectiveTemperature(env);
            float environment = ClimateRules.BodyDelta(state.CurValue, effective, env.PlaceData.isInWater, StateManager.Instance.IsResting);
            var suit = GameManager.Instance.EquipmentBag?.FindCardOfId("保温服");
            float absorbed = environment < 0 && suit != null && suit.TryGetComponent<DurabilityComponent>(out var d) ? ClimateRules.AbsorbCold(-environment, d.value) : 0;
            tipController.SetTip($"体温：{state.CurValue:F1}℃（预计{environment + absorbed:+0.000;-0.000;0}℃/15分钟）\n正常范围：35.5～37.5℃\n" +
                $"环境影响：{environment:+0.000;-0.000;0}℃\n保温服吸收：+{absorbed:F3}℃（耐久-{absorbed * 10:0.##}）\n附近热源：+{ClimateManager.Instance.Heat(env).local:F1}℃\n" +
                $"有效环境温度：{effective:F1}℃\n水域倍率：{(env.PlaceData.isInWater ? 1.35f : 1)}\n休息降温倍率：{(StateManager.Instance.IsResting ? 1.25f : 1)}");
        }
        else
        {
            stateNameText.text = "温度";
            tipController.SetTip(ClimateManager.Instance.EnvironmentDescription(env));
        }
    }

    public override void SetValue(float curValue, float maxValue, bool playAnim)
    {
        UpdateSliderValue(curValue - minimum, maxValue - minimum, playAnim);

        // 根据不同的温度等级显示不同的图片
        int level = body ? curValue < 32 ? 0 : curValue < 35.5f ? 1 : curValue <= 37.5f ? 2 : curValue < 40 ? 3 : 4
            : curValue < -20 ? 0 : curValue < 0 ? 1 : curValue <= 30 ? 2 : curValue < 40 ? 3 : 4;

        if (level < levels.Length)
            icon.sprite = levels[level];

        var color = ColorManager.TemperatureColors[level];
        if (button != null)
        {
            button.hoveredColor = button.currentColor = color;
        }

        icon.color = arrow.color = stateNameText.color = valueText.color = slider.fillRect.GetComponent<Image>().color = color;
    }

    protected override void DisplayValueText(float curValue, float maxValue)
    {
        valueText.text = $"{curValue + minimum:0.0}℃";
    }

}
