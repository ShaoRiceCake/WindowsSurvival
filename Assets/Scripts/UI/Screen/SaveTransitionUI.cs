using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Authored overlay survives scene replacement and consumes input until initialization finishes.
public sealed class SaveTransitionUI : MonoBehaviour
{
    public static SaveTransitionUI Instance { get; private set; }
    public CanvasGroup group;
    public Text heading, worldName, phase;
    public Image progress;
    public RectTransform activity;
    private Vector2 activityRest;
    public const float MinimumVisibleSeconds = .8f;
    private float openedAt;
    public bool IsVisible { get; private set; }
    private void Awake()
    {
        Instance = this; DontDestroyOnLoad(gameObject);
        activityRest = activity.anchoredPosition;
        Hide();
    }
    public static void Prepare()
    {
        if (Instance == null) Instantiate(Resources.Load<GameObject>("Prefabs/UI/SaveTransition"));
    }
    public static SaveTransitionUI Open(string title, string name)
    {
        Prepare();
        var view = Instance; view.IsVisible = true;
        view.openedAt = Time.realtimeSinceStartup;
        view.group.alpha = 1; view.group.blocksRaycasts = true;
        view.heading.text = title; view.worldName.text = "时间线名称：" + name;
        view.SetProgress("正在准备…", 0);
        // Apply scaling now, before the next input event can reach the old screen.
        var scaler = view.GetComponent<CanvasScaler>();
        scaler.enabled = false; scaler.enabled = true;
        Canvas.ForceUpdateCanvases();
        return view;
    }
    public void SetProgress(string label, float value)
    {
        phase.text = label;
        // Resize the solid, left-anchored fill; the default UI sprite fades at both ends.
        progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
    }
    private void Update()
    {
        if (!IsVisible) return;
        // A small moving marker signals activity without inventing a percentage.
        activity.anchoredPosition = activityRest + Vector2.right * (Mathf.PingPong(Time.unscaledTime * 22, 16) - 8);
    }
    public IEnumerator Finish()
    {
        SetProgress(phase.text, 1);
        yield return WaitForMinimumDisplay();
        for(float t=0;t<.16f;t+=Time.unscaledDeltaTime) {group.alpha=1-t/.16f;yield return null;}
        Hide();
    }
    public IEnumerator WaitForMinimumDisplay()
    {
        while (Time.realtimeSinceStartup - openedAt < MinimumVisibleSeconds) yield return null;
    }
    // Keep the Canvas registered after its first render so the next click can be
    // intercepted immediately, without waiting for newly enabled Graphic depths.
    public void Hide() { IsVisible = false; group.alpha = 0; group.blocksRaycasts = false; }
    private void OnDestroy() { if(Instance==this) Instance=null; }
}
