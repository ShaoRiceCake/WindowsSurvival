using UnityEngine;
using UnityEngine.UI;

public class UIStateToggle : MonoBehaviour
{
    public Text stateNameText;
    public Image offImage;
    public Image onImage;

    public Color onColor;
    public Color offColor;

    public HoverableButton button;

    public void SetStateName(string name)
    {
        stateNameText.text = name;
    }

    public void SetValue(bool value)
    {
        onImage.gameObject.SetActive(value);
        var color = value ? onColor : offColor;
        onImage.color = offImage.color = stateNameText.color = color;
        if (button != null)
        {
            button.currentColor = button.hoveredColor = color;
            if (button.image != null)
            {
                button.image.color = color;
            }
        }
    }

    public void SetLockedVisual()
    {
        onImage.color = offImage.color = stateNameText.color = ColorManager.DarkGrey;
        if (button != null)
        {
            button.currentColor = button.hoveredColor = ColorManager.DarkGrey;
            if (button.image != null)
                button.image.color = ColorManager.DarkGrey;
        }
    }
}
