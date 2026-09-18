using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Static composition lives in SaveSettingsPanel.prefab. Runtime only binds data and
// instantiates the authored category / setting-row templates for registered settings.
public sealed class SaveSettingsView : MonoBehaviour
{
    public Vector2 windowSize = new Vector2(1290, 720);
    public ScrollRect navigationScroll, optionsScroll;
    public SaveHubButton categoryTemplate;
    public SelectionFrameMotion categorySelection;
    public SaveSettingRow rowTemplate;
    public GameObject gamePage, optionsPage, gameActions;
    public Text worldName, place, gameTime, recent, categoryHeading;
    public Image placeIcon;
    public SaveHubButton save, load, menu, quit, resume, reset, apply;
    public CanvasGroup pageFade;
    public Sprite settingsIcon;
    public string Category { get; private set; }
    public IReadOnlyList<SaveSettingRow> Rows => rows;
    private readonly List<SaveHubButton> categories = new();
    private readonly List<SaveSettingRow> rows = new();
    private PlaceData[] places;
    private Coroutine motion;

    public void BindNavigation(IEnumerable<string> names, string selected, Action<string> choose)
    {
        Category = selected;
        var source = names.ToArray();
        for (int i = 0; i < source.Length; i++)
        {
            if (i == categories.Count)
            {
                var tab = Instantiate(categoryTemplate, navigationScroll.content);
                tab.gameObject.SetActive(true); categories.Add(tab);
            }
            var button = categories[i]; string category = source[i];
            button.gameObject.SetActive(true); button.name = "Category-" + category;
            button.text.text = category; button.selected = category == selected;
            button.useSharedSelectionFrame = true;
            button.transform.Find("SelectionMark").gameObject.SetActive(false);
            Wire(button, () => choose(category));
        }
        for (int i = source.Length; i < categories.Count; i++) categories[i].gameObject.SetActive(false);
        LayoutRebuilder.ForceRebuildLayoutImmediate(navigationScroll.content);
        var chosen = categories.FirstOrDefault(b => b.gameObject.activeSelf && b.selected);
        categorySelection.Select(chosen != null ? chosen.frame.rectTransform : null);
    }

    public void ShowGame(RunData run, bool safe, Action saveAction, Action loadAction,
        Action menuAction, Action quitAction, Action resumeAction)
    {
        BeginPage(true, "继续游戏", resumeAction);
        worldName.text = run?.name ?? "";
        var latest = run == null ? null : SaveWorldlineView.Latest(run);
        place.text = latest?.place ?? "尚未保存";
        gameTime.text = latest?.TimeLabel ?? "还没有保存记录";
        recent.text = latest == null ? "最近保存：尚未保存" : "最近保存  " + latest.savedUtc.ToLocalTime().ToString("HH:mm") + "  ·  " + latest.KindLabel;
        if (places == null) places = Resources.LoadAll<PlaceData>("ScriptableObject/Place");
        var location = places.FirstOrDefault(p => p.name == latest?.place || latest?.place == "驾驶舱" && p.name == "驾驶室");
        placeIcon.sprite = location != null ? location.placeImage : null;
        placeIcon.enabled = placeIcon.sprite != null;
        Wire(save, saveAction); Wire(load, loadAction); Wire(menu, menuAction); Wire(quit, quitAction);
        save.Interactable = menu.Interactable = quit.Interactable = safe;
        load.Interactable = safe && latest != null;
    }

    public void ShowOptions(string heading, string backLabel, Action back, Action restore, Action applyDisplay = null)
    {
        BeginPage(false, backLabel, back);
        categoryHeading.text = heading;
        reset.gameObject.SetActive(restore != null); Wire(reset, restore);
        apply.gameObject.SetActive(applyDisplay != null); Wire(apply, applyDisplay);
    }

    public void AddSetting(SaveSettingDefinition definition, Action<Action> guard)
    {
        var row = Instantiate(rowTemplate, optionsScroll.content);
        row.gameObject.SetActive(true);
        rows.Add(row);
        row.Bind(definition, guard, RefreshRows);
    }

    public void RefreshRows() { foreach (var row in rows) row.Refresh(); }

    private void BeginPage(bool game, string backLabel, Action back)
    {
        gamePage.SetActive(game); gameActions.SetActive(game); optionsPage.SetActive(!game);
        reset.gameObject.SetActive(false); apply.gameObject.SetActive(false);
        resume.text.text = backLabel; Wire(resume, back);
        foreach (var row in rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
        rows.Clear(); optionsScroll.verticalNormalizedPosition = 1;
        if (motion != null) StopCoroutine(motion);
        motion = StartCoroutine(FadePage());
    }

    private IEnumerator FadePage()
    {
        for (float t = 0; t < .14f; t += Time.unscaledDeltaTime)
        { pageFade.alpha = Mathf.Lerp(.45f, 1, t / .14f); yield return null; }
        pageFade.alpha = 1; motion = null;
    }

    private static void Wire(SaveHubButton button, Action action)
    {
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(() => action());
    }

    private void OnDisable()
    {
        StopAllCoroutines(); motion = null; pageFade.alpha = 1;
    }
}
