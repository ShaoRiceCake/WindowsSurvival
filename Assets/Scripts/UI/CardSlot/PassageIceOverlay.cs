using UnityEngine;
using UnityEngine.UI;

public class PassageIceOverlay : MonoBehaviour
{
    public Image frame;
    public Slider progress;
    public Text label;
    public Text extraInfo;
    public void Display(Card card)
    {
        var connection = (card as PassageCard)?.Connection;
        bool visible = connection != null && connection.ice > 0;
        frame.gameObject.SetActive(visible && connection.IsFrozen);
        progress.gameObject.SetActive(visible);
        if (extraInfo != null) extraInfo.gameObject.SetActive(!visible);
        if (!visible) return;
        progress.value = connection.ice;
        label.text = $"冰封 {connection.ice:0}/200";
    }
    public void Clear() { frame.gameObject.SetActive(false); progress.gameObject.SetActive(false); if (extraInfo != null) extraInfo.gameObject.SetActive(true); }
}
