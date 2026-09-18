using UnityEngine;
using UnityEngine.UI;

public sealed class SaveJournalRow : MonoBehaviour
{
    public SaveHubButton button, retain;
    public Text day, time, kind, information;
    public Image icon, marker;
    public CanvasGroup details;
    public LayoutElement height;
    private bool expanded;
    private float target = 84;
    public void Select(bool value, bool immediate = false)
    {
        expanded = value; target = value ? (retain.gameObject.activeSelf ? 202 : 164) : 84;
        button.selected = value;
        details.interactable = details.blocksRaycasts = value;
        time.color = marker.color = value ? SaveHubButton.Accent : new Color32(158, 158, 158, 255);
        if (immediate) { height.preferredHeight = target; details.alpha = value ? 1 : 0; }
    }
    private void Update()
    {
        float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime / .055f);
        height.preferredHeight = Mathf.Abs(height.preferredHeight - target) < .15f ? target : Mathf.Lerp(height.preferredHeight, target, blend);
        details.alpha = Mathf.MoveTowards(details.alpha, expanded ? 1 : 0, Time.unscaledDeltaTime / .15f);
        // Collapsed children must not receive keyboard navigation either.
        retain.Interactable = expanded;
    }
}
