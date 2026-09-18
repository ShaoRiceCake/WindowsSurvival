using UnityEngine;
using UnityEngine.EventSystems;

// Same title-bar drag affordance, usable before the gameplay desktop has been created.
public sealed class SaveWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    public RectTransform window;
    private Vector2 offset;
    public void OnBeginDrag(PointerEventData data)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)window.parent, data.position, data.pressEventCamera, out var point);
        offset = window.anchoredPosition - point;
    }
    public void OnDrag(PointerEventData data)
    {
        var parent = (RectTransform)window.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out var point);
        var position = point + offset;
        var bounds = parent.rect;
        position.x = Mathf.Clamp(position.x, bounds.xMin + window.rect.width / 2, bounds.xMax - window.rect.width / 2);
        position.y = Mathf.Clamp(position.y, bounds.yMin + window.rect.height / 2, bounds.yMax - window.rect.height / 2);
        window.anchoredPosition = position;
    }
}
