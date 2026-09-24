using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// The layout and artwork live in SaveHub.prefab; only list entries are pooled at runtime.
public sealed class SaveWorldlineView : MonoBehaviour
{
    public ScrollRect scroll;
    public SelectionFrameMotion selectionFrame;
    public SaveHubButton rowTemplate, create, import, load, manage, rename, copy, share, delete, emptyCreate;
    public Text count, mode, worldName, place, time, played, records, recent, hardRule;
    public Image placeIcon;
    public GameObject details, empty, menu;
    public Button dismissMenu;
    public CanvasGroup detailFade, menuFade;
    public const float RowHeight = 128;
    public RunData Selected { get; private set; }
    private List<RunData> runs = new();
    private readonly List<SaveHubButton> rows = new();
    private Action<RunData> selected;
    private Coroutine fading, menuMotion;
    private PlaceData[] places;

    private void Awake()
    {
        scroll.onValueChanged.AddListener(_ => RenderList());
        dismissMenu.onClick.AddListener(() => CloseMenu());
        places = Resources.LoadAll<PlaceData>("ScriptableObject/Place");
    }

    public static SavePoint Latest(RunData run) => run.snapshots.FirstOrDefault(p => p.id == run.latestSnapshotId)
        ?? run.snapshots.OrderByDescending(p => p.savedUtc).FirstOrDefault();

    public void Bind(List<RunData> source, string preferred, Action newWorld, Action importWorld,
        Action<RunData> choose, Action<RunData> loadWorld, Action<RunData> renameWorld,
        Action<RunData> copyWorld, Action<RunData> shareWorld, Action<RunData> deleteWorld)
    {
        bool sameSelection = Selected?.id == preferred;
        runs = source; selected = choose;
        Wire(create, newWorld); Wire(emptyCreate, newWorld); Wire(import, importWorld);
        Wire(load, () => loadWorld(Selected));
        Wire(manage, () => { if (!CloseMenu()) OpenMenu(); });
        Wire(rename, () => { CloseMenu(); renameWorld(Selected); });
        Wire(copy, () => { CloseMenu(); copyWorld(Selected); });
        Wire(share, () => { CloseMenu(); shareWorld(Selected); });
        Wire(delete, () => { CloseMenu(); deleteWorld(Selected); });
        count.text = runs.Count.ToString();
        scroll.content.sizeDelta = new Vector2(0, runs.Count * RowHeight);
        if (!sameSelection) scroll.verticalNormalizedPosition = 1;
        Select(runs.FirstOrDefault(r => r.id == preferred) ?? runs.FirstOrDefault(), false);
        Canvas.ForceUpdateCanvases();
        if (!sameSelection && Selected != null)
        {
            float offset = runs.IndexOf(Selected) * RowHeight;
            float maximum = Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height);
            scroll.content.anchoredPosition = new Vector2(0, Mathf.Min(offset, maximum));
        }
        RenderList();
    }

    private static void Wire(SaveHubButton button, Action action)
    {
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => action());
    }

    private void RenderList()
    {
        if (!isActiveAndEnabled) return;
        int visible = Mathf.CeilToInt(scroll.viewport.rect.height / RowHeight) + 2;
        while (rows.Count < visible)
        {
            var row = Instantiate(rowTemplate, scroll.content); row.gameObject.SetActive(true); rows.Add(row);
        }
        int first = Mathf.Clamp(Mathf.FloorToInt(scroll.content.anchoredPosition.y / RowHeight), 0, Mathf.Max(0, runs.Count - 1));
        for (int i = 0; i < rows.Count; i++)
        {
            int index = first + i; var row = rows[i];
            row.gameObject.SetActive(index < runs.Count);
            if (index >= runs.Count) continue;
            var run = runs[index]; var point = Latest(run);
            row.gameObject.name = "Worldline-" + run.id;
            var rect = (RectTransform)row.transform;
            rect.anchoredPosition = new Vector2(0, -index * RowHeight); rect.sizeDelta = new Vector2(0, RowHeight - 12);
            row.text.text = run.name; row.selected = Selected?.id == run.id;
            row.useSharedSelectionFrame = true;
            row.transform.Find("Summary").GetComponent<Text>().text =
                !run.IsCompatible ? SaveDataContract.IncompatibleMessage : (run.hardcore ? "硬核" : "普通") + " · " + (point?.TimeLabel ?? "尚未开始");
            row.onClick.RemoveAllListeners(); row.onClick.AddListener(() => Select(run, true));
            if (row.selected) selectionFrame.Select(row.frame.rectTransform);
        }
        if (Selected == null) selectionFrame.Clear();
    }

    private void Select(RunData run, bool animate)
    {
        if (animate && Selected?.id == run?.id) return;
        if (!animate) selectionFrame.Clear();
        CloseMenu(); Selected = run; selected?.Invoke(run);
        details.SetActive(run != null); empty.SetActive(run == null);
        manage.gameObject.SetActive(run != null); load.gameObject.SetActive(run != null);
        if (run == null) { RenderList(); return; }
        var point = Latest(run);
        mode.text = run.hardcore ? "硬核模式" : run.fromHardcore ? "普通模式 · 来自硬核副本" : "普通模式";
        worldName.text = "时间线名称：" + run.name;
        place.text = point?.place ?? "尚未开始"; time.text = point?.TimeLabel ?? "还没有保存记录";
        var location = places.FirstOrDefault(p => p.name == point?.place || point?.place == "驾驶舱" && p.name == "驾驶室");
        placeIcon.sprite = location != null ? location.placeImage : null; placeIcon.enabled = placeIcon.sprite != null;
        played.text = $"{(int)(run.totalPlaySeconds / 3600)}小时{(int)(run.totalPlaySeconds / 60) % 60}分";
        records.text = run.snapshots.Count + " 个";
        recent.text = run.lastPlayedUtc == DateTime.MinValue ? "尚未开始" : run.lastPlayedUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        if (!run.IsCompatible) time.text = SaveDataContract.IncompatibleMessage;
        copy.Interactable = share.Interactable = run.IsCompatible;
        hardRule.gameObject.SetActive(run.hardcore); load.Interactable = point != null && run.IsCompatible;
        if (fading != null) StopCoroutine(fading);
        if (animate) fading = StartCoroutine(Fade(detailFade, .16f)); else detailFade.alpha = 1;
        RenderList();
    }

    private void OpenMenu()
    {
        menu.SetActive(true); manage.selected = true;
        if (menuMotion != null) StopCoroutine(menuMotion);
        menuMotion = StartCoroutine(Fade(menuFade, .12f));
    }

    public bool CloseMenu()
    {
        if (!menu.activeSelf) return false;
        if (menuMotion != null) { StopCoroutine(menuMotion); menuMotion = null; }
        menu.SetActive(false); manage.selected = false; return true;
    }

    private static IEnumerator Fade(CanvasGroup group, float duration)
    {
        for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
        { group.alpha = Mathf.Lerp(.25f, 1, t / duration); yield return null; }
        group.alpha = 1;
    }

    private void OnDisable()
    {
        StopAllCoroutines(); fading = menuMotion = null; CloseMenu(); detailFade.alpha = 1;
    }
}
