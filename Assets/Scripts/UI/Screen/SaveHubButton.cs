using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// This variant is only used by the authored SaveHub prefab. Other windows keep their own feedback.
public sealed class SaveHubButton : HoverableButton, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public Image frame;
    public RectTransform feedbackGlyph;
    public bool selected, primary, dangerous;
    public bool secondary;
    private bool hover, pressed, focused;
    private Vector2 rest;
    private Vector2 glyphRest;
    private Selectable navigation;
    public static readonly Color Accent = new Color32(100, 222, 174, 255);
    protected override void Awake()
    {
        base.Awake();
        rest = text != null ? text.rectTransform.anchoredPosition : Vector2.zero;
        if (feedbackGlyph != null) glyphRest = feedbackGlyph.anchoredPosition;
        navigation = GetComponent<Selectable>();
    }
    protected override void OnEnable() { base.OnEnable(); hover = pressed = focused = false; Refresh(true); }
    protected override void OnDisable() { hover = pressed = focused = false; if (text != null) text.rectTransform.anchoredPosition = rest; base.OnDisable(); }
    private void Update() => Refresh(false);
    private void Refresh(bool instant)
    {
        bool active = Interactable;
        Color color = secondary ? new Color32(82, 82, 82, 255) : new Color32(118, 118, 118, 255);
        if (active)
        {
            if (selected || primary) color = Accent;
            if (hover) color = primary || selected ? new Color32(156, 233, 201, 255) : new Color32(180, 199, 189, 255);
            if (pressed) color = Accent;
            if (dangerous && (hover || pressed)) color = ColorManager.Red;
            if (focused && !hover) color = new Color32(217, 248, 235, 255);
        }
        float blend = instant ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime / (pressed ? .025f : .055f));
        if (frame != null) frame.color = Color.Lerp(frame.color, color, blend);
        if (feedbackGlyph != null) feedbackGlyph.anchoredPosition = Vector2.Lerp(feedbackGlyph.anchoredPosition,
            glyphRest + (hover && active ? Vector2.right * 3 : Vector2.zero) + (pressed && active ? Vector2.down : Vector2.zero), blend);
        if (text != null)
        {
            text.color = secondary && !hover && !focused && !pressed ? new Color32(180,180,180,255) : Color.white;
            text.rectTransform.anchoredPosition = Vector2.Lerp(text.rectTransform.anchoredPosition, rest + (pressed && active ? Vector2.down : Vector2.zero), blend);
        }
        if (canvasGroup != null) canvasGroup.alpha = active ? 1 : .34f;
        if (navigation != null) navigation.interactable = active;
    }
    public override void OnPointerEnter(PointerEventData e) { hover = true; if (Interactable && playHoverSound && SoundManager.Instance != null) SoundManager.Instance.PlaySound(hoveredAudio, true, .12f); }
    public override void OnPointerExit(PointerEventData e) { hover = pressed = false; }
    public override void OnPointerClick(PointerEventData e) { if (Interactable && (e == null || e.button == PointerEventData.InputButton.Left)) onClick?.Invoke(); }
    public void OnPointerDown(PointerEventData e) { if (!Interactable || e.button != PointerEventData.InputButton.Left) return; pressed = true; EventSystem.current?.SetSelectedGameObject(gameObject); }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    public void OnSelect(BaseEventData e) { focused = true; }
    public void OnDeselect(BaseEventData e) { focused = pressed = false; }
    public void OnSubmit(BaseEventData e) { if (Interactable) onClick?.Invoke(); }
}
