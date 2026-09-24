using UnityEngine;
using UnityEngine.UI;

public class ClimateSeasonLabel : MonoBehaviour
{
    private Text label;
    private void Awake() => label = GetComponent<Text>();
    private void LateUpdate()
    {
        if (ClimateManager.Instance.Data != null && label != null)
            label.text = ClimateManager.Instance.GlacierDay > 0 ? $"冰层季 第{ClimateManager.Instance.GlacierDay}天" : "温和季";
    }
}
