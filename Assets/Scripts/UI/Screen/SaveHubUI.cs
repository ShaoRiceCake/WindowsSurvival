using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class SaveHubUI : MonoBehaviour
{
    public static SaveHubUI Instance { get; private set; }
    public GameObject panel;
    public Text title, subtitle, status;
    public HoverableButton closeButton;
    public RectTransform listContent, detailContent, filterBar;
    public ScrollRect listScroll, detailScroll;
    public RectTransform window;
    public HoverableButton rowTemplate, actionTemplate;
    public Text textTemplate;
    public InputField inputTemplate;
    public Slider sliderTemplate;
    public RectTransform horizontalTemplate, footer, settingRowTemplate;
    public SaveJournalRow journalTemplate;
    public SaveWorldlineView worldlines;
    public SaveSettingsView settingsView;
    public CanvasGroup autoSaveToast;
    public Text autoSaveToastText;
    private Coroutine toastMotion;
    private string selectedWorldline;
    private readonly List<SaveJournalRow> journalRows = new();
    private Coroutine pageMotion, windowMotion;
    private string lastNotice;
    private float noticeUntil;
    private bool loading;
    private readonly Dictionary<HoverableButton, bool> loadingControls = new();
    private bool ownsSystemCursor;
    private Action escapeAction;
    private readonly List<HoverableButton> pool = new();
    private List<string> labels = new();
    private Action<int> select;
    private bool death;
    private bool displayPending;
    private Action revertDisplay;
    private Action returnFromSettings;
    private string category = "声音";
    private float RowHeight = 104;
    private int selectedIndex;
    private float previousScale = 1;
    private Image[] titleIcons;
    private Sprite[] originalTitleIcons;
    public bool IsOpen => panel.activeSelf;
    public bool IsLoading => loading;
    public void SetLoading(bool busy)
    {
        loading = busy; GetCanvasGroup(window).interactable = !busy;
        if (busy)
        {
            loadingControls.Clear();
            foreach(var button in panel.GetComponentsInChildren<HoverableButton>())
            { loadingControls[button] = button.Interactable; button.Interactable = false; }
        }
        else
        {
            foreach(var pair in loadingControls) if(pair.Key != null) pair.Key.Interactable = pair.Value;
            loadingControls.Clear();
        }
    }

    public static void Ensure()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("Prefabs/UI/SaveHub");
        if (prefab == null) { Debug.LogError("缺少 SaveHub UI Prefab，请运行 Tools/存档/生成界面"); return; }
        Instantiate(prefab);
    }
    private void Awake()
    {
        Instance = this; panel.SetActive(false);
        titleIcons = window.Find("TopBar").GetComponentsInChildren<Image>(true).Where(i => i.name == "Icon").ToArray();
        originalTitleIcons = titleIcons.Select(i => i.sprite).ToArray();
        closeButton.onClick.AddListener(Close);
        listScroll.onValueChanged.AddListener(_ => RenderList());
        SaveSettingRegistry.Initialize();
        SaveSystem.Changed += OnSaveChanged;
    }
    private void OnSaveChanged() { lastNotice = SaveSystem.Notice; noticeUntil = Time.unscaledTime + 5; }
    private void OnDestroy() { SaveSystem.Changed -= OnSaveChanged; revertDisplay?.Invoke(); if (Instance == this) Instance = null; }
    private void OnDisable() => ReleaseCursor();
    private void ReleaseCursor()
    {
        if (!ownsSystemCursor) return;
        ownsSystemCursor = false;
        MouseManager.SetSystemCursorForUI(false);
    }
    private void LateUpdate()
    {
        if (!IsOpen) return;
        if (lastNotice != SaveSystem.Notice) { lastNotice = SaveSystem.Notice; noticeUntil = Time.unscaledTime + 5; }
        status.text = Time.unscaledTime < noticeUntil ? lastNotice ?? "" : "";
        if (listScroll.gameObject.activeSelf) RenderList();
    }
    private void Open(string heading, string sub)
    {
        worldlines.gameObject.SetActive(false);
        settingsView.gameObject.SetActive(false);
        for (int i = 0; i < titleIcons.Length; i++) titleIcons[i].sprite = originalTitleIcons[i];
        status.gameObject.SetActive(true);
        detailScroll.gameObject.SetActive(true); footer.gameObject.SetActive(true);
        bool opening = !IsOpen;
        if (windowMotion != null) { StopCoroutine(windowMotion); windowMotion = null; }
        escapeAction = null;
        if (!IsOpen) { previousScale = Time.timeScale; Time.timeScale = 0; }
        panel.SetActive(true); SaveRuntime.Instance.PausedByUI = true;
        if (!ownsSystemCursor) { ownsSystemCursor = true; MouseManager.SetSystemCursorForUI(true); }
        foreach (var caption in window.Find("TopBar").GetComponentsInChildren<Text>(true))
            if (caption.name == "Name") caption.text = heading;
        subtitle.text = sub; closeButton.gameObject.SetActive(!death);
        filterBar.gameObject.SetActive(false);
        Layout(1240, 860, 380);
        var group = GetCanvasGroup(window); group.alpha = 1;
        if (opening) windowMotion = StartCoroutine(WindowFade(true));
    }
    private void Layout(float width, float height, float sidebar)
    {
        window.sizeDelta = new Vector2(width, height);
        window.anchoredPosition = Vector2.zero;
        listScroll.gameObject.SetActive(sidebar > 0);
        RowHeight = sidebar < 300 ? 64 : 104;
        var left = (RectTransform)listScroll.transform;
        float contentTop = string.IsNullOrEmpty(subtitle.text) ? 100 : 138;
        left.anchoredPosition = new Vector2(30, -contentTop); left.sizeDelta = new Vector2(sidebar, height - contentTop - 106);
        float x = sidebar > 0 ? sidebar + 60 : 30;
        var right = (RectTransform)detailScroll.transform;
        right.anchoredPosition = new Vector2(x, -contentTop); right.sizeDelta = new Vector2(width - x - 30, height - contentTop - 106);
        subtitle.rectTransform.sizeDelta = new Vector2(width - 60, 42);
        status.rectTransform.anchoredPosition = new Vector2(30, -(height - 36)); status.rectTransform.sizeDelta = new Vector2(width - 60, 28);
        footer.anchoredPosition = new Vector2(30, -(height - 94)); footer.sizeDelta = new Vector2(width - 60, 48);
    }
    public void Close()
    {
        if (!IsOpen || death || displayPending || loading) return;
        if (worldlines.gameObject.activeSelf && worldlines.CloseMenu()) return;
        if (escapeAction != null) { var back = escapeAction; escapeAction = null; back(); return; }
        if (windowMotion != null) StopCoroutine(windowMotion);
        windowMotion = StartCoroutine(WindowFade(false));
    }
    private IEnumerator WindowFade(bool opening)
    {
        var group = GetCanvasGroup(window);
        float start = group.alpha, elapsed = 0, duration = opening ? .18f : .12f;
        if (opening) start = 0;
        while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; group.alpha = Mathf.Lerp(start, opening ? 1 : 0, elapsed / duration); yield return null; }
        if (!opening) { panel.SetActive(false); ReleaseCursor(); Time.timeScale = previousScale; SaveRuntime.Instance.PausedByUI = false; }
        windowMotion = null;
    }
    private IEnumerator PageFade()
    {
        var group = GetCanvasGroup(detailContent);
        group.alpha = 0;
        for (float t = 0; t < .18f; t += Time.unscaledDeltaTime) { group.alpha = Mathf.Clamp01(t / .18f); yield return null; }
        group.alpha = 1; pageMotion = null;
    }
    private static CanvasGroup GetCanvasGroup(Component target)
    {
        // Unity's missing-component wrapper is not a CLR null in the Editor.
        if (!target.TryGetComponent<CanvasGroup>(out var group))
            group = target.gameObject.AddComponent<CanvasGroup>();
        return group;
    }
    private void ClearDetails()
    {
        escapeAction = null; journalRows.Clear();
        foreach (Transform child in footer) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        if (pageMotion != null) StopCoroutine(pageMotion);
        pageMotion = StartCoroutine(PageFade());
        detailScroll.verticalNormalizedPosition = 1;
        foreach (Transform child in detailContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    }
    private void SetList(IEnumerable<string> rows, Action<int> onSelect)
    {
        labels = rows.ToList(); select = onSelect;
        selectedIndex = -1;
        listContent.sizeDelta = new Vector2(0, labels.Count * RowHeight);
        listScroll.verticalNormalizedPosition = 1;
        RenderList();
    }
    private void RenderList()
    {
        int visible = Mathf.CeilToInt(listScroll.viewport.rect.height / RowHeight) + 2;
        while (pool.Count < visible)
        {
            var button = Instantiate(rowTemplate, listContent); button.gameObject.SetActive(true); pool.Add(button);
        }
        int first = Mathf.Max(0, Mathf.FloorToInt(listContent.anchoredPosition.y / RowHeight));
        for (int i = 0; i < pool.Count; i++)
        {
            int index = first + i; var button = pool[i]; button.gameObject.SetActive(index < labels.Count);
            if (index >= labels.Count) continue;
            var rect = (RectTransform)button.transform; rect.sizeDelta = new Vector2(-16, RowHeight - 8); rect.anchoredPosition = new Vector2(0, -index * RowHeight);
            button.GetComponentInChildren<Text>().text = labels[index];
            if (button is SaveHubButton styled) styled.selected = index == selectedIndex;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => { if (!displayPending) { selectedIndex = index; Guard(() => select?.Invoke(index)); } });
        }
    }
    private Text Text(string value, int height = 70)
    {
        var text = Instantiate(textTemplate, detailContent); text.gameObject.SetActive(true); text.text = value;
        text.GetComponent<LayoutElement>().preferredHeight = height; return text;
    }
    private HoverableButton Action(string label, Action action)
    {
        var button = Instantiate(actionTemplate, detailContent); button.gameObject.SetActive(true);
        button.GetComponentInChildren<Text>().text = label;
        button.onClick.AddListener(() => Guard(action)); return button;
    }
    private SaveHubButton Footer(string label, Action action, bool primary = false, bool dangerous = false)
    {
        var b = (SaveHubButton)Action(label, action); b.transform.SetParent(footer, false);
        b.GetComponent<LayoutElement>().preferredWidth = Mathf.Max(110, b.text.preferredWidth + 40);
        b.primary = primary; b.dangerous = dangerous; return b;
    }
    private RectTransform Line(float height, bool framed = false)
    {
        var row = Instantiate(framed ? settingRowTemplate : horizontalTemplate, detailContent); row.gameObject.SetActive(true);
        row.GetComponent<LayoutElement>().preferredHeight = height; return row;
    }
    private void MoveTo(Transform child, Transform parent, float width, bool flexible = false)
    {
        child.SetParent(parent, false); var layout = child.GetComponent<LayoutElement>(); layout.preferredWidth = width; layout.flexibleWidth = flexible ? 1 : 0;
    }
    private void Tabs(IEnumerable<string> names, string current, Action<string> choose)
    {
        filterBar.gameObject.SetActive(true); filterBar.sizeDelta = new Vector2(window.sizeDelta.x - 60, 44);
        foreach (Transform child in filterBar) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        foreach (var name in names)
        {
            var b = Instantiate(actionTemplate, filterBar); b.gameObject.SetActive(true); b.text.text = name;
            b.GetComponent<LayoutElement>().preferredWidth = 130;
            ((SaveHubButton)b).selected = name == current;
            b.onClick.AddListener(() => Guard(() => { if (!displayPending && !loading) choose(name); }));
        }
        float tabsTop = string.IsNullOrEmpty(subtitle.text) ? 100 : 137;
        filterBar.anchoredPosition = new Vector2(30,-tabsTop);
        var right = (RectTransform)detailScroll.transform; right.anchoredPosition = new Vector2(30,-tabsTop-60); right.sizeDelta = new Vector2(window.sizeDelta.x-60,window.sizeDelta.y-tabsTop-166);
    }
    private InputField Input(string value, int limit = 30)
    {
        var input = Instantiate(inputTemplate, detailContent); input.gameObject.SetActive(true); input.characterLimit = limit; input.text = value; return input;
    }
    private void Guard(Action action)
    {
        if (loading) return;
        try { action?.Invoke(); }
        catch (Exception e)
        {
            SaveSystem.Notice = e.Message; Debug.LogWarning(e);
            if (settingsView.gameObject.activeInHierarchy) ShowToast("操作未完成，请重试", 4);
        }
    }
    private string Duration(double seconds) => $"{(int)(seconds / 3600)}小时{(int)(seconds / 60) % 60}分";
    private void Confirm(string message, Action yes, Action back, bool dangerous = true, string heading = "确认操作")
    {
        Open(heading, ""); Layout(820, 430, 0); SetList(Array.Empty<string>(), null);
        ClearDetails(); Text(message, 150); escapeAction = back;
        Footer("取消", back); Footer("确认", () => { escapeAction = null; yes(); }, !dangerous, dangerous);
    }
    public void ShowAutoSaveResult(bool success)
    {
        ShowToast(success ? "自动保存完成" : "自动保存失败，原有进度已保留", success ? 2.5f : 4f);
    }
    private void ShowToast(string message, float hold)
    {
        if (toastMotion != null) StopCoroutine(toastMotion);
        autoSaveToastText.text = message;
        toastMotion = StartCoroutine(AutoSaveToast(hold));
    }
    private IEnumerator AutoSaveToast(float hold)
    {
        autoSaveToast.alpha = 1;
        yield return new WaitForSecondsRealtime(hold);
        for (float t=0;t<.2f;t+=Time.unscaledDeltaTime) { autoSaveToast.alpha=1-t/.2f; yield return null; }
        autoSaveToast.alpha=0;toastMotion=null;
    }
    public void ShowRuns()
    {
        death = false; Open("世界线", "");
        var runs = SaveSystem.Repository.List();
        if (SaveSystem.Repository.Warnings.Count > 0) SaveSystem.Notice = "部分世界线无法读取：" + string.Join("；", SaveSystem.Repository.Warnings);
        Layout(1240, 700, 0); SetList(Array.Empty<string>(), null); ClearDetails();
        detailScroll.gameObject.SetActive(false); footer.gameObject.SetActive(false);
        worldlines.gameObject.SetActive(true);
        worldlines.Bind(runs, selectedWorldline, ShowCreation, ShowImport,
            run => selectedWorldline = run?.id,
            run => ShowHistory(run, null, ShowRuns),
            RenameWorldline,
            run => Guard(() => { selectedWorldline = SaveSystem.Repository.Copy(run).id; ShowRuns(); }),
            run => Guard(() => Export(run)),
            run => { WorldlineForm("删除世界线"); Confirm("删除「" + run.name + "」及其全部 " + run.snapshots.Count + " 个保存点？\n删除后无法恢复。",
                () => { SaveSystem.Repository.DeleteRun(run); selectedWorldline = null; ShowRuns(); }, ShowRuns, true, "删除世界线"); });
    }
    private void WorldlineForm(string heading)
    {
        Open(heading, ""); Layout(820, 430, 0); SetList(Array.Empty<string>(), null); ClearDetails();
        escapeAction = ShowRuns;
    }
    private void RenameWorldline(RunData run)
    {
        WorldlineForm("修改世界线名称"); Text("时间线名称：", 36); var input = Input(run.name);
        Footer("取消", ShowRuns);
        var save = Footer("保存名称", () => { run.name = SaveSystem.CleanName(input.text); SaveSystem.Repository.Update(run); ShowRuns(); }, true);
        input.onValueChanged.AddListener(value => save.Interactable = !string.IsNullOrWhiteSpace(value));
    }
    private void Enter(RunData run, SavePoint point, Action back)
    {
        if (SaveSystem.Ready && !SaveSystem.IsDead)
        {
            Confirm("载入所选记录？\n当前未保存的进度将被替换。", () => SaveSystem.Enter(run, point, _ => back()), back, false, "载入保存记录");
            return;
        }
        SaveSystem.Enter(run, point, _ => back());
    }
    public void ShowHistory(RunData run, SaveKind? filter = null, Action back = null)
    {
        Open("航行日志", "时间线名称：" + run.name); Layout(1240, 900, 0); SetList(Array.Empty<string>(), null); ClearDetails();
        Action returnTo = back ?? (() => { if (death) ShowDeath(); else if (SaveSystem.Ready) ShowGameMenu(); else ShowRuns(); });
        escapeAction = returnTo;
        Tabs(new[] { "全部", "自动", "手动" }, !filter.HasValue ? "全部" : filter == SaveKind.Manual ? "手动" : "自动", name => ShowHistory(run, name == "全部" ? null : name == "手动" ? SaveKind.Manual : SaveKind.Auto, returnTo));
        var points = run.snapshots.Where(p => !filter.HasValue || (p.kind == SaveKind.Manual ? SaveKind.Manual : SaveKind.Auto) == filter).OrderByDescending(p => p.savedUtc).ToList();
        SavePoint chosen = points.FirstOrDefault();
        SaveHubButton read = null, remove = null;
        var places = Resources.LoadAll<PlaceData>("ScriptableObject/Place"); DateTime lastDay = DateTime.MinValue;
        foreach (var point in points)
        {
            var row = Instantiate(journalTemplate, detailContent); row.gameObject.SetActive(true); journalRows.Add(row);
            row.day.text = lastDay != point.gameTime.Date ? "第" + Math.Max(1,(point.gameTime.Date-new DateTime(2020,1,1)).Days+1) + "天" : ""; lastDay = point.gameTime.Date;
            row.time.text = point.gameTime.ToString("HH:mm"); row.button.text.text = point.place; row.kind.text = point.KindLabel;
            row.information.text = point.TimeLabel + "\n" + point.savedUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm") + " 保存 · 游玩 " + Duration(point.playSeconds);
            var place = places.FirstOrDefault(p => p.name == point.place || point.place == "驾驶舱" && p.name == "驾驶室"); row.icon.sprite = place != null ? place.placeImage : null; row.icon.enabled = row.icon.sprite != null;
            row.retain.gameObject.SetActive(point.kind != SaveKind.Manual && !run.hardcore);
            row.retain.onClick.AddListener(() => Guard(() => { point.kind = SaveKind.Manual; SaveSystem.Repository.Update(run); ShowHistory(run,filter,returnTo); }));
            row.button.onClick.AddListener(() => { chosen = point; foreach (var item in journalRows) item.Select(item == row); });
            row.Select(point == chosen, true);
        }
        if (points.Count == 0) Text("此分类暂无保存记录。");
        remove = Footer("删除记录", () => Confirm("删除这个保存点？\n其他记录不受影响。", () => { SaveSystem.Repository.DeletePoint(run,chosen); ShowHistory(run,filter,returnTo); }, () => ShowHistory(run,filter,returnTo), true, "删除保存记录"), false, true);
        remove.Interactable = chosen != null && run.snapshots.Count > 1 && !run.hardcore;
        Footer("返回", returnTo);
        read = Footer("读取此记录", () => Enter(run,chosen,() => ShowHistory(run,filter,returnTo)), true); read.Interactable = chosen != null;
    }
    public void ShowCreation() => ShowCreation(new NewRunOptions { name = RunNameGenerator.Generate(SaveSystem.Repository.List().Select(r=>r.name)) });
    private void ShowCreation(NewRunOptions draft)
    {
        Open("创建世界线", ""); Layout(1080, 820, 0); SetList(Array.Empty<string>(),null); ClearDetails();
        Text("麦麦的新一段旅途",48); Text("时间线名称：",36);
        var line=Line(56); var input=Input(draft.name);
        MoveTo(input.transform,line,650,true);
        var random=Action("随机",()=>input.text=RunNameGenerator.Generate(SaveSystem.Repository.List().Select(r=>r.name)));
        MoveTo(random.transform,line,110);
        var choices=Line(148);
        var ordinary=(SaveHubButton)Action("普通模式\n\n保留多个时间点，死亡后可以回档",null);
        var hardcore=(SaveHubButton)Action("硬核模式\n\n仅保留最新进度，死亡后删除世界线",null);
        MoveTo(ordinary.transform,choices,450,true); MoveTo(hardcore.transform,choices,450,true);
        ordinary.text.fontSize=22; hardcore.text.fontSize=22; ordinary.text.alignment=hardcore.text.alignment=TextAnchor.MiddleLeft; ordinary.selected=true;
        var rules=Text("",64);
        void SetMode(bool hard) { draft.hardcore=hard; ordinary.selected=!hard; hardcore.selected=hard; rules.text=hard ? "硬核模式退出前必须保存。复制与分享的副本转为普通模式。" : ""; }
        ordinary.onClick.AddListener(()=>SetMode(false)); hardcore.onClick.AddListener(()=>SetMode(true));
        SetMode(draft.hardcore);
        var tutorialRow=Line(84,true); var tutorialLabel=Text("新手教程",44); MoveTo(tutorialLabel.transform,tutorialRow,600,true);
        var tutorial=(SaveHubButton)Action(draft.skipGuide?"跳过":"开启",null); MoveTo(tutorial.transform,tutorialRow,140); tutorial.selected=!draft.skipGuide;
        tutorial.onClick.AddListener(()=>{draft.skipGuide=!draft.skipGuide;tutorial.text.text=draft.skipGuide?"跳过":"开启";tutorial.selected=!draft.skipGuide;});
        Footer("返回",ShowRuns); var start=Footer("开始游戏",()=>{draft.name=input.text;SaveSystem.CompleteCreation(draft, _ => { if(this!=null) ShowCreation(draft); });},true);
        input.onValueChanged.AddListener(value=>start.Interactable=!string.IsNullOrWhiteSpace(value));
    }
    private void Export(RunData run)
    {
        string folder = Path.Combine(Application.persistentDataPath, "Shares"); Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, run.id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".wssave");
        SaveSystem.Repository.Export(run, file); SaveSystem.Notice = "分享文件已导出：" + file;
        Application.OpenURL(new Uri(folder).AbsoluteUri);
    }
    private void ShowImport()
    {
        WorldlineForm("导入世界线"); Layout(860, 470, 0);
        Text("选择 .wssave 分享文件", 44);
        var hint=Text("将创建一条独立世界线。",36);hint.fontSize=19;hint.color=new Color32(170,170,170,255);
        var line=Line(56);var path = Input("", 2048);MoveTo(path.transform,line,580,true);
        // InputField also supplies layout metrics at priority 1; long paths must
        // scroll inside the field instead of taking width from the browse button.
        path.GetComponent<LayoutElement>().layoutPriority=2;
        var browse=Action("选择文件", () => { var selected = SaveFileDialog.Open(); if (!string.IsNullOrEmpty(selected)) path.text = selected; });
        browse.text.fontSize=19;MoveTo(browse.transform,line,130);
        browse.GetComponent<LayoutElement>().minWidth=130;
        Footer("返回", ShowRuns);
        var inspect=Footer("查看文件信息", () =>
        {
            var run = SaveSystem.Repository.InspectImport(path.text, SaveDataContract.Validate, out _);
            string file = path.text;
            Confirm($"导入「{run.name}」？\n{run.snapshots.Count} 个保存点 · 普通模式\n版本：{run.snapshots.Last().gameVersion}", () => { SaveSystem.Repository.Import(file, SaveDataContract.Validate); ShowRuns(); }, ShowImport, false, "导入世界线");
        },true);
        inspect.Interactable=false;path.onValueChanged.AddListener(value=>inspect.Interactable=!string.IsNullOrWhiteSpace(value));
    }
    public void ToggleGameMenu()
    {
        if (IsOpen) Close(); else ShowGameMenu();
    }
    public void ShowGameMenu()
    {
        category="游戏"; ShowSettings();
    }
    private void ShowGamePage()
    {
        settingsView.ShowGame(SaveSystem.Current, SaveSystem.Safe,
            () => Guard(() => SaveRuntime.Instance.SaveProgress(_ => { if (this != null) { ShowGameMenu(); ShowToast("保存失败，原有进度已保留", 4); } })),
            () => Guard(() => ShowHistory(SaveSystem.Current, null, ShowGameMenu)),
            () => Guard(() => ShowExit(false)), () => Guard(() => ShowExit(true)), Close);
    }
    public void ShowExit(bool application)
    {
        if (!SaveSystem.Safe && !SaveSystem.IsDead) { StartCoroutine(WaitForSafeExit(application)); return; }
        Open(application ? "退出游戏" : "返回主菜单", "");
        Layout(820, 430, 0); SetList(Array.Empty<string>(), null); ClearDetails();
        Text(SaveSystem.Current?.hardcore == true ? "硬核模式需保存最新进度后退出。" : "退出前保存当前进度？", 64);
        var hint = Text("时间线名称：" + (SaveSystem.Current?.name ?? ""), 68); hint.fontSize=21; hint.color=new Color32(170,170,170,255);
        escapeAction = ShowGameMenu;
        Footer("取消", ShowGameMenu);
        if (SaveSystem.Current?.hardcore != true)
        {
            var discard = Footer("不保存退出", () => SaveRuntime.Instance.ExitWithoutSaving(application,_=>{if(this!=null)ShowExit(application);}), false, true);
            discard.text.fontSize = 19;
            discard.GetComponent<LayoutElement>().preferredWidth = 150;
        }
        Footer("保存并退出", () => SaveRuntime.Instance.SaveAndExit(application, _ => { if(this!=null) ShowExit(application); }), true);
    }
    private IEnumerator WaitForSafeExit(bool application) { while (!SaveSystem.Safe && !SaveSystem.IsDead) yield return null; if (!SaveSystem.IsDead) ShowExit(application); }
    public void ShowDeath()
    {
        death = true; Open("麦麦的旅途暂告一段落", "");
        Layout(820, 450, 0); SetList(Array.Empty<string>(), null); ClearDetails();
        Text(SaveSystem.Current?.hardcore == true ? "硬核世界线已结束。" : "回到之前的时间点，再试一次。", 74);
        var name=Text("时间线名称：" + (SaveSystem.Current?.name ?? ""),70); name.fontSize=21; name.color=new Color32(170,170,170,255);
        Footer("返回主菜单", SaveSystem.LeaveToMenu, SaveSystem.Current?.hardcore == true);
        if (SaveSystem.Current?.hardcore == false)
        {
            Footer("查看历史记录", () => ShowHistory(SaveSystem.Current));
            Footer("读取最近有效保存点", () =>
            {
                foreach (var point in SaveSystem.Current.snapshots.OrderByDescending(p => p.savedUtc))
                {
                    try { SaveDataContract.Validate(SaveSystem.Repository.Read(SaveSystem.Current, point)); }
                    catch { continue; }
                    SaveSystem.Enter(SaveSystem.Current, point); return;
                }
                throw new InvalidDataException("没有可读取的完整保存点");
            }, true);
        }
    }
    public void ShowSettings(Action back = null)
    {
        returnFromSettings=back; death=false; Open("设置", ""); SetList(Array.Empty<string>(),null);
        if (pageMotion != null) { StopCoroutine(pageMotion); pageMotion = null; }
        detailScroll.gameObject.SetActive(false); footer.gameObject.SetActive(false);
        subtitle.text=""; status.gameObject.SetActive(false); listScroll.gameObject.SetActive(false);
        window.sizeDelta=settingsView.windowSize; window.anchoredPosition=Vector2.zero;
        settingsView.gameObject.SetActive(true);
        foreach(var icon in titleIcons)icon.sprite=settingsView.settingsIcon;
        var categories=SaveSettingRegistry.Items.Select(s=>s.category).Concat(new[]{"显示"}).Distinct().ToList();
        if(SaveSystem.Ready && !SaveSystem.IsDead && SaveSystem.Current!=null)categories.Insert(0,"游戏");
        if(!categories.Contains(category))category=categories[0];
        void Choose(string chosen)
        {
            Guard(() =>
            {
                if(displayPending || category==chosen)return;
                category=chosen;
                settingsView.BindNavigation(categories,category,Choose);
                ShowCategory();
            });
        }
        settingsView.BindNavigation(categories,category,Choose); ShowCategory();
    }
    private void ShowCategory()
    {
        if(displayPending)return;
        if(category=="游戏") { ShowGamePage(); return; }
        if(category=="显示") { ShowDisplay(); return; }
        settingsView.ShowOptions(category, SettingsBackLabel, SettingsBack,
            () => Guard(() => Confirm("恢复「"+category+"」的默认设置？",()=>{SaveSettingRegistry.Reset(category);ShowSettings(returnFromSettings);},()=>ShowSettings(returnFromSettings),false,"恢复默认设置")));
        foreach(var setting in SaveSettingRegistry.Items.Where(s=>s.category==category))
            settingsView.AddSetting(setting, Guard);
    }
    private string SettingsBackLabel => SaveSystem.Ready && !SaveSystem.IsDead ? "继续游戏" : "完成";
    private void SettingsBack() { if(returnFromSettings!=null)returnFromSettings();else Close(); }
    private void ShowDisplay()
    {
        var settings = GameSettings.Current;
        bool full = settings.fullscreen; int width = settings.width, height = settings.height, fps = settings.fps;
        var resolutions = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct().ToList();
        if (resolutions.Count == 0) resolutions.Add(new Vector2Int(1920, 1080));
        settingsView.ShowOptions("显示", SettingsBackLabel, SettingsBack,
            () => Guard(() => StartCoroutine(ConfirmDisplay(true,1920,1080,60))),
            () => Guard(() => StartCoroutine(ConfirmDisplay(full,width,height,fps))));
        settingsView.AddSetting(new SaveSettingDefinition { id="mode",label="显示模式",value=()=>full?"全屏":"窗口",change=()=>full=!full }, Guard);
        settingsView.AddSetting(new SaveSettingDefinition { id="resolution",label="分辨率",value=()=>width+" × "+height,
            change=()=>{var r=resolutions[(resolutions.FindIndex(r=>r.x==width&&r.y==height)+1)%resolutions.Count];width=r.x;height=r.y;} }, Guard);
        settingsView.AddSetting(new SaveSettingDefinition { id="fps",label="帧率上限",value=()=>fps<0?"不限":fps.ToString(),
            change=()=>{var rates=new[]{30,60,120,-1};fps=rates[(Array.IndexOf(rates,fps)+1)%rates.Length];} }, Guard);
    }
    private IEnumerator ConfirmDisplay(bool full, int width, int height, int fps)
    {
        var s = GameSettings.Current; bool oldFull = s.fullscreen; int oldW = s.width, oldH = s.height, oldFps = s.fps;
        revertDisplay = () => { s.fullscreen = oldFull; s.width = oldW; s.height = oldH; s.fps = oldFps; GameSettings.ApplyDisplay(); };
        displayPending = true; s.fullscreen = full; s.width = width; s.height = height; s.fps = fps; GameSettings.ApplyDisplay();
        Open("确认显示设置", ""); Layout(820,430,0); SetList(Array.Empty<string>(),null);
        ClearDetails(); var countdown = Text("", 130); bool accept = false, cancel = false;
        Footer("恢复之前设置", () => cancel = true); Footer("保留设置", () => accept = true,true);
        float end = Time.realtimeSinceStartup + 15;
        while (!accept && !cancel && Time.realtimeSinceStartup < end) { countdown.text = "保留当前显示设置？\n" + Mathf.CeilToInt(end - Time.realtimeSinceStartup) + " 秒后自动恢复"; yield return null; }
        if (accept) { GameSettings.Save(); revertDisplay = null; } else { revertDisplay(); revertDisplay = null; }
        displayPending = false; ShowSettings(returnFromSettings);
    }
}
