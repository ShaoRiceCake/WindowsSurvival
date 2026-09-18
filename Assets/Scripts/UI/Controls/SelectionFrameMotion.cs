using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// One authored sprite frame moves between options, matching the existing window tabs.
[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class SelectionFrameMotion : MonoBehaviour
{
    public float duration = .2f;
    public Ease ease = Ease.OutQuad;
    private RectTransform rect;
    private Image frame;
    private SaveHubButton selectedButton;
    private Sequence motion;
    private bool placed;
    private Vector2 destination, targetSize;
    private readonly Vector3[] corners = new Vector3[4];

    public void Select(RectTransform target, bool animate = true)
    {
        if (target == null) { Clear(); return; }
        if (rect == null) { rect = (RectTransform)transform; frame = GetComponent<Image>(); }
        var button = target.GetComponentInParent<SaveHubButton>();
        var parent = (RectTransform)rect.parent;
        target.GetWorldCorners(corners);
        var bottomLeft = parent.InverseTransformPoint(corners[0]);
        var topRight = parent.InverseTransformPoint(corners[2]);
        var position = new Vector2(bottomLeft.x - parent.rect.xMin, topRight.y - parent.rect.yMax);
        var size = new Vector2(topRight.x - bottomLeft.x, topRight.y - bottomLeft.y);
        bool sameTarget = placed && (position - destination).sqrMagnitude < .01f && (size - targetSize).sqrMagnitude < .01f;
        if (selectedButton != button)
        {
            if (selectedButton != null) selectedButton.SetSelectionFramePending(false);
            selectedButton = button;
        }
        gameObject.SetActive(true); transform.SetAsLastSibling();
        if (!placed) frame.color = selectedButton != null ? selectedButton.SelectionFrameColor : SaveHubButton.Accent;
        if (sameTarget && animate)
        {
            if (selectedButton != null) selectedButton.SetSelectionFramePending(motion != null && motion.IsActive() && motion.IsPlaying());
            return;
        }
        destination = position; targetSize = size;
        motion?.Kill(); motion = null;
        bool moving = placed && animate && duration > 0;
        if (selectedButton != null) selectedButton.SetSelectionFramePending(moving);
        if (!moving)
        {
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        else
        {
            // Continue from the currently visible position when a second click interrupts a tween.
            motion = DOTween.Sequence()
                .Join(rect.DOAnchorPos(position, duration).SetEase(ease))
                .Join(rect.DOSizeDelta(size, duration).SetEase(ease))
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    motion = null;
                    if (selectedButton != null) selectedButton.SetSelectionFramePending(false);
                });
        }
        placed = true;
    }

    private void LateUpdate()
    {
        if (selectedButton != null)
            frame.color = Color.Lerp(frame.color, selectedButton.SelectionFrameColor, 1 - Mathf.Exp(-Time.unscaledDeltaTime / .055f));
    }

    public void Clear() { gameObject.SetActive(false); }
    private void OnDisable()
    {
        motion?.Kill(); motion = null;
        if (selectedButton != null) selectedButton.SetSelectionFramePending(false);
        placed = false; selectedButton = null;
    }
}
