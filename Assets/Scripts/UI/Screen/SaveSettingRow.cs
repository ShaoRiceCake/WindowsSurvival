using System;
using UnityEngine;
using UnityEngine.UI;

// Controls and their layout are authored in SaveSettingRow.prefab.
public sealed class SaveSettingRow : MonoBehaviour
{
    public Text label, help, value, percentage;
    public GameObject choiceGroup, stepperGroup, sliderGroup;
    public SaveHubButton choice, minus, plus;
    public Slider slider;
    public Image enabledMark;
    private Action refresh;

    public void Bind(SaveSettingDefinition setting, Action<Action> guard, Action refreshAll)
    {
        name = "Setting-" + setting.id;
        label.text = setting.label;
        help.text = setting.description ?? "";
        help.gameObject.SetActive(!string.IsNullOrEmpty(help.text));
        choiceGroup.SetActive(setting.number == null);
        stepperGroup.SetActive(setting.number != null && setting.whole);
        sliderGroup.SetActive(setting.number != null && !setting.whole);
        enabledMark.gameObject.SetActive(setting.toggle != null);
        if (setting.number == null)
        {
            refresh = () =>
            {
                choice.text.text = setting.value();
                choice.Interactable = setting.enabled?.Invoke() != false;
                enabledMark.color = setting.toggle?.Invoke() == true ? SaveHubButton.Accent : new Color32(105,105,105,255);
            };
            choice.onClick.AddListener(() => guard(() => { setting.change(); refreshAll(); }));
        }
        else if (setting.whole)
        {
            refresh = () =>
            {
                float number = setting.number();
                value.text = number.ToString("0") + " " + (setting.unit ?? "");
                bool active = setting.enabled?.Invoke() != false;
                minus.Interactable = active && number > setting.min;
                plus.Interactable = active && number < setting.max;
                value.color = active ? Color.white : new Color32(105,105,105,255);
            };
            minus.onClick.AddListener(() => guard(() => { setting.setNumber(Mathf.Max(setting.min, setting.number() - 1)); Refresh(); }));
            plus.onClick.AddListener(() => guard(() => { setting.setNumber(Mathf.Min(setting.max, setting.number() + 1)); Refresh(); }));
        }
        else
        {
            slider.minValue = setting.min; slider.maxValue = setting.max;
            refresh = () =>
            {
                slider.SetValueWithoutNotify(setting.number());
                percentage.text = setting.number().ToString("P0");
                slider.interactable = setting.enabled?.Invoke() != false;
            };
            slider.onValueChanged.AddListener(number => guard(() => { setting.setNumber(number); Refresh(); }));
        }
        Refresh();
    }

    public void Refresh() => refresh?.Invoke();
}
