using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SaveFeatureValidation
{
    private static int stage;
    private static double next;
    private static string chosenWorldline;
    static SaveFeatureValidation()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (!SessionState.GetBool("SaveValidationActive", false) || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            Directory.CreateDirectory("Logs/SaveValidation");
            File.AppendAllText("Logs/SaveValidation/play-error.txt", message + "\n" + stack + "\n");
            SessionState.SetBool("SaveValidationFailed", true);
        };
    }
    public static void BatchAll()
    {
        Directory.CreateDirectory("Logs/SaveValidation");
        EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity");
        Run();
        BeginPlay();
    }
    public static void BuildStandalone()
    {
        Directory.CreateDirectory("Logs/SaveValidation");
        EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/StartScene.unity", "Assets/Scenes/GameScene.unity" },
            locationPathName = "Builds/SaveValidation/WindowsSurvival.exe",
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        File.WriteAllText("Logs/SaveValidation/build.txt", result.summary.result + " errors=" + result.summary.totalErrors);
        EditorApplication.Exit(result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
    public static void Run()
    {
        var log = new List<string>();
        void Check(bool value, string name) { if (!value) throw new Exception(name); log.Add("PASS " + name); }
        string root = Path.Combine(Path.GetTempPath(), "WindowsSurvival-SaveTests-" + Guid.NewGuid().ToString("N"));
        var repo = new SaveRepository(root);
        var payload = SaveDataContract.NewGame(); SaveDataContract.Validate(payload);
        var run = new RunData { name = "麦麦想吃白爆矿" };
        var first = new SavePoint { kind = SaveKind.Manual }; repo.Commit(run, first, payload, 2);
        for (int i = 0; i < 4; i++) repo.Commit(run, new SavePoint { kind = SaveKind.Auto, savedUtc = DateTime.UtcNow.AddSeconds(i) }, payload, 2);
        Check(run.snapshots.Count == 3 && run.snapshots.Any(p => p.id == first.id), "auto retention preserves manual");
        repo.Commit(run, new SavePoint { kind = SaveKind.Exit }, payload, 2);
        Check(run.snapshots.Count == 3 && run.snapshots.All(p=>p.kind != SaveKind.Exit) && run.snapshots.Any(p=>p.id==run.latestSnapshotId) && repo.Read(run, first).files.Count == payload.files.Count, "exit uses auto retention, preserves manual and newest head");
        var legacy = new RunData { name = "legacy exit kind" };
        repo.Commit(legacy,new SavePoint {kind=SaveKind.Manual},payload,2);
        legacy.snapshots[0].kind=SaveKind.Exit;
        SaveRepository.AtomicWrite(Path.Combine(root,legacy.id,"run.json"),Newtonsoft.Json.JsonConvert.SerializeObject(legacy));
        var migrated=repo.List().First(r=>r.id==legacy.id);
        Check(migrated.snapshots[0].kind==SaveKind.Auto && migrated.snapshots[0].KindLabel=="自动保存", "legacy exit reads as auto without losing snapshot");
        string legacyShare=Path.Combine(root,"legacy.wssave");repo.Export(legacy,legacyShare);
        Check(repo.InspectImport(legacyShare,SaveDataContract.Validate,out _).snapshots[0].kind==SaveKind.Auto,"legacy shared exit imports as auto");
        repo.Commit(migrated,new SavePoint {kind=SaveKind.Auto},payload,1);
        Check(migrated.snapshots.Count==1 && migrated.snapshots[0].id==migrated.latestSnapshotId,"legacy exit participates in auto rotation");
        var copy = repo.Copy(run); Check(copy.id != run.id && !copy.snapshots.Any(p => run.snapshots.Any(s => s.id == p.id)), "copy has independent IDs");
        string archive = Path.Combine(root, "share.wssave"); repo.Export(run, archive);
        var imported = repo.Import(archive, SaveDataContract.Validate); Check(imported.snapshots.Count == run.snapshots.Count && imported.id != run.id, "export import roundtrip");
        var hard = new RunData { name = "硬核测试", hardcore = true };
        repo.Commit(hard, new SavePoint(), payload, 10); repo.Commit(hard, new SavePoint(), payload, 10);
        Check(hard.snapshots.Count == 1, "hardcore single head");
        Check(!repo.Copy(hard).hardcore, "hardcore copy downgraded");
        repo.DeleteRun(hard); Check(!repo.List().Any(r => r.id == hard.id), "death tombstone hides run");
        bool rejected = false; try { repo.Update(hard); } catch { rejected = true; } Check(rejected, "dead run cannot republish");
        string file = Path.Combine(root, run.id, first.id + ".json"); File.AppendAllText(file, "x");
        rejected = false; try { repo.Read(run, first); } catch { rejected = true; } Check(rejected, "corruption is rejected");
        var invalid = SaveDataContract.NewGame(); invalid.files.Remove("PlayerBag");
        rejected = false; try { SaveDataContract.Validate(invalid); } catch { rejected = true; } Check(rejected, "partial snapshot rejected");
        rejected = false; try { JsonManager.Deserialize("{\"$type\":\"System.IO.FileInfo, mscorlib\",\"OriginalPath\":\"x\"}", typeof(object)); } catch { rejected = true; } Check(rejected, "unsafe polymorphic type rejected");
        var clock = new RunData();
        Check(!SavePlayClock.Advance(clock, 599, true, true, 10), "no autosave before ten minutes");
        Check(SavePlayClock.Advance(clock, 1, true, true, 10), "autosave due at ten minutes");
        Check(!SavePlayClock.Advance(clock, 600, false, true, 10) && clock.unsavedSeconds == 600, "paused or unfocused clock excluded");
        Check(!SavePlayClock.Advance(clock, 1, true, false, 1), "autosave disabled");
        var stable = new RunData { name = "atomic test" }; repo.Commit(stable, new SavePoint(), payload, 10);
        string stableId = stable.latestSnapshotId;
        using (var locked = new FileStream(Path.Combine(root, stable.id, "run.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            rejected = false; try { repo.Commit(stable, new SavePoint(), payload, 10); } catch (IOException) { rejected = true; }
            Check(rejected && stable.latestSnapshotId == stableId && stable.snapshots.Count == 1, "failed manifest commit preserves old head");
        }
        for (int i = 0; i < 20; i++) repo.Update(new RunData { name = "run " + i });
        Check(repo.List().Count > 20, "no four-slot limit");
        var names = new HashSet<string>(); for (int i = 0; i < 500; i++) { string name = RunNameGenerator.Generate(names); Check(name.StartsWith("麦麦") && name.Length <= 30 && names.Add(name), "name " + i + " " + name); }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UI/SaveHub.prefab");
        Check(prefab != null && prefab.GetComponent<SaveHubUI>().sliderTemplate != null, "authored prefab wired");
        Check(prefab.GetComponent<SaveHubUI>().window.GetComponent<CanvasGroup>() != null, "window CanvasGroup serialized in authored prefab");
        Check(prefab.GetComponent<SaveHubUI>().detailContent.GetComponent<CanvasGroup>() != null, "page CanvasGroup serialized in authored prefab");
        var settingsView=prefab.GetComponent<SaveHubUI>().settingsView;
        Check(settingsView!=null && settingsView.rowTemplate!=null && settingsView.categoryTemplate!=null && settingsView.settingsIcon!=null,"B settings panel, row template and original settings icon serialized");
        Check(PrefabUtility.IsPartOfPrefabInstance(settingsView) && settingsView.rowTemplate.gameObject.name=="SaveSettingRow","settings composition uses nested prefab and authored row asset");
        var worldlines = prefab.GetComponent<SaveHubUI>().worldlines;
        Check(worldlines != null && worldlines.rowTemplate != null && worldlines.dismissMenu != null && worldlines.placeIcon != null, "worldline page and menu serialized in authored prefab");
        Check(worldlines.load.primary && !worldlines.manage.primary && worldlines.delete.dangerous, "load is primary and management is secondary");
        File.WriteAllLines("Logs/SaveValidation/tests.txt", log);
    }
    private static void Capture(string name, int width = 1920, int height = 1080)
    {
        var cameraObject = new GameObject("Save UI verification camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .035f, .04f); camera.cullingMask = 1 << 31;
        var texture = new RenderTexture(width, height, 24); camera.targetTexture = texture;
        var root = SaveHubUI.Instance;
        var transforms = root.GetComponentsInChildren<Transform>(true); var layers = transforms.Select(t => t.gameObject.layer).ToArray();
        foreach (var t in transforms) t.gameObject.layer = 31;
        var canvas = root.GetComponent<Canvas>(); var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        var mode = scaler.uiScaleMode; var factor = scaler.scaleFactor;
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = Mathf.Min(width / 1920f, height / 1080f);
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases(); camera.Render(); var active = RenderTexture.active; RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/SaveValidation/" + name + ".png", image.EncodeToPNG());
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; scaler.uiScaleMode = mode; scaler.scaleFactor = factor;
        for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
        RenderTexture.active = active;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(cameraObject);
    }
    public static void BeginPlay()
    {
        Directory.CreateDirectory("Logs/SaveValidation");
        SessionState.SetBool("SaveValidationFailed", false);
        SessionState.SetBool("SaveValidationCompleted", false);
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("当前场景未保存或正在运行，未接管场景。");
        SessionState.SetString("SaveValidationOriginalScene", EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool("SaveValidationActive", true);
        EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity");
        EditorApplication.isPlaying = true;
    }
    public static void WorldlineVisuals()
    {
        SessionState.SetBool("WorldlineVisualValidation", true);
        EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity"); BeginPlay();
    }
    public static void SettingsDisplayRegression()
    {
        SessionState.SetBool("SettingsDisplayValidation", true);
        EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity"); BeginPlay();
    }
    private static IEnumerator CheckSettingsDisplay()
    {
        var hub=SaveHubUI.Instance;hub.ShowSettings();yield return new WaitForSecondsRealtime(.3f);
        var view=hub.settingsView;
        Click(view.navigationScroll.content.GetComponentsInChildren<SaveHubButton>().First(b=>b.text.text=="显示").gameObject);
        yield return new WaitForSecondsRealtime(.25f);
        bool fullscreen=GameSettings.Current.fullscreen;
        Click(view.Rows.First(r=>r.name=="Setting-mode").choice.gameObject);
        if(GameSettings.Current.fullscreen!=fullscreen)throw new Exception("Display draft applied before confirmation");
        Click(view.apply.gameObject);yield return new WaitForSecondsRealtime(.25f);
        if(GameSettings.Current.fullscreen==fullscreen || hub.title.text!="确认显示设置")throw new Exception("Display apply did not open timed confirmation");
        ClickHub("恢复之前设置");yield return new WaitForSecondsRealtime(.25f);
        if(GameSettings.Current.fullscreen!=fullscreen || !view.gameObject.activeInHierarchy || view.Category!="显示")throw new Exception("Display rollback did not restore B settings");
        Click(view.Rows.First(r=>r.name=="Setting-mode").choice.gameObject);
        Click(view.apply.gameObject);yield return new WaitForSecondsRealtime(15.5f);
        if(GameSettings.Current.fullscreen!=fullscreen || !view.gameObject.activeInHierarchy)throw new Exception("Display timeout did not restore old settings");
        File.WriteAllText("Logs/SaveValidation/settings-display.txt","PASS authored B display rows keep edits as draft.\nPASS Apply opens the timed confirmation.\nPASS Cancel restores display preferences and the same B category.\nPASS 15-second timeout restores old display preferences.\nPASS no Unity Error/Exception/Assert.\n");
        SessionState.SetBool("SettingsDisplayValidation",false);SessionState.SetBool("SaveValidationActive",false);SessionState.SetBool("SaveValidationCompleted",true);EditorApplication.isPlaying=false;
    }
    private static IEnumerator CheckWorldlineVisuals()
    {
        var report = new List<string>();
        void Check(bool value, string label) { if (!value) throw new Exception(label); report.Add("PASS " + label); }
        var payload = SaveDataContract.NewGame();
        var names = new[] {"麦麦听说废铁刀和白爆矿在谈恋爱", "麦麦把白爆矿塞进了冰箱", "麦麦正在研究矿石释氧机"};
        for (int i=0; i<names.Length; i++)
        {
            var run = new RunData { name=names[i], totalPlaySeconds=13320-i*3000, lastPlayedUtc=DateTime.UtcNow.AddDays(-i) };
            SaveSystem.Repository.Commit(run, new SavePoint {place="动力舱",gameTime=new DateTime(2020,1,13,22,10,0),season="温和季",kind=SaveKind.Manual},payload,10);
            SaveSystem.Repository.Commit(run, new SavePoint {place="驾驶室",gameTime=new DateTime(2020,1,14,14,30,0),season="温和季",kind=SaveKind.Auto},payload,10);
        }
        var hub=SaveHubUI.Instance; hub.ShowRuns();
        yield return new WaitForSecondsRealtime(.5f);
        var view=hub.worldlines; var button=view.load;
        Check(view.place.text=="驾驶室" && view.time.text.Contains("第14天 14:30") && view.records.text=="2 个", "summary uses latest point while list has one entry per worldline");
        Check(view.worldName.preferredHeight<=view.worldName.rectTransform.rect.height, "worldline name fits authored title area");
        Capture("worldline-final"); Capture("worldline-1280x720",1280,720); Capture("worldline-1600x1000",1600,1000);
        var background=button.GetComponent<UnityEngine.UI.Image>();
        var fill=background.color; var ink=button.text.color; var border=button.frame.color;
        var bounds=button.rectTransform.anchoredPosition; var size=button.rectTransform.sizeDelta;
        var textPosition=button.text.rectTransform.anchoredPosition; var glyphPosition=button.feedbackGlyph.anchoredPosition;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        button.OnPointerEnter(pointer); yield return new WaitForSecondsRealtime(.25f);
        Check(background.color==fill && button.text.color==ink && button.frame.color!=border, "hover changes original border only, preserving fill and text");
        Check(button.feedbackGlyph.anchoredPosition.x>glyphPosition.x+2.5f, "load arrow moves without moving hit area");
        Capture("worldline-hover");
        button.OnPointerDown(pointer); yield return new WaitForSecondsRealtime(.12f);
        Check(Mathf.Abs(button.text.rectTransform.anchoredPosition.y-textPosition.y+1)<.1f, "press sinks label by one pixel");
        Check(button.rectTransform.anchoredPosition==bounds && button.rectTransform.sizeDelta==size, "hover and press preserve clickable bounds");
        button.OnPointerUp(pointer); button.OnPointerExit(pointer); yield return new WaitForSecondsRealtime(.25f);
        Check(Vector2.Distance(button.text.rectTransform.anchoredPosition,textPosition)<.1f && Vector2.Distance(button.feedbackGlyph.anchoredPosition,glyphPosition)<.1f, "release restores label and arrow");
        Click(view.manage.gameObject); yield return new WaitForSecondsRealtime(.2f); Capture("worldline-final-menu");
        for(int i=0;i<120;i++)
        {
            if(!Cursor.visible || MouseManager.Instance != null && MouseManager.Instance.GetComponent<UnityEngine.UI.Image>().enabled) throw new Exception("Cursor ownership changed while menu is open");
            yield return null;
        }
        report.Add("PASS system cursor ownership stable for 120 frames with management menu open");
        hub.Close(); Check(!view.menu.activeSelf && hub.IsOpen, "Escape/close dismisses management before closing the window");
        File.WriteAllLines("Logs/SaveValidation/worldline-visuals.txt",report);
        SessionState.SetBool("WorldlineVisualValidation",false); SessionState.SetBool("SaveValidationActive",false);
        SessionState.SetBool("SaveValidationCompleted",true); EditorApplication.isPlaying=false;
    }
    private static void Click(GameObject target)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = target.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var rect = target.GetComponent<RectTransform>();
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        { position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
        if (hits.Count == 0 || UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject) != target)
            throw new Exception("UI click blocked or missing: " + target.name);
        UnityEngine.EventSystems.ExecuteEvents.Execute(target, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }
    private static void ClickMain(string name) => Click(UnityEngine.Object.FindObjectOfType<StartSceneManager>().StartButton.transform.Find(name).gameObject);
    private static void ClickHub(string text) => Click(SaveHubUI.Instance.panel.GetComponentsInChildren<HoverableButton>().First(b => b.text != null && b.text.text == text).gameObject);
    private static void CheckLegacyRemoved()
    {
        if (SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "LoadButton" || t.name == "ChooseSkipGuide"))
            throw new Exception("Legacy save UI still exists in scene");
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Isolate()
    {
        if (!SessionState.GetBool("SaveValidationActive", false)) return;
        SaveSystem.StorageRootOverride = Path.Combine(Application.temporaryCachePath, "SaveValidation-" + Guid.NewGuid().ToString("N"));
        GameSettings.StorageRootOverride = SaveSystem.StorageRootOverride;
    }
    private static void Tick()
    {
        if (SessionState.GetBool("SaveValidationCompleted", false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool("SaveValidationCompleted", false);
            string original = SessionState.GetString("SaveValidationOriginalScene", "");
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool("SaveValidationFailed", false) ? 1 : 0);
            return;
        }
        if (!SessionState.GetBool("SaveValidationActive", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .8;
        try
        {
            if (SessionState.GetBool("SaveValidationFailed", false)) throw new Exception("Unity logged an error during Play Mode; see play-error.txt");
            if (SessionState.GetBool("SettingsDisplayValidation", false))
            {
                if (stage == 0 && SaveHubUI.Instance != null) { stage++; SaveHubUI.Instance.StartCoroutine(CheckSettingsDisplay()); }
                return;
            }
            if (SessionState.GetBool("WorldlineVisualValidation", false))
            {
                if (stage == 0 && SaveHubUI.Instance != null) { stage++; SaveHubUI.Instance.StartCoroutine(CheckWorldlineVisuals()); }
                return;
            }
            switch (stage)
            {
                case 0:
                    if (SaveHubUI.Instance == null) return;
                    CheckLegacyRemoved(); ClickMain("EnterGame"); stage++; break;
                case 1:
                    if (!SaveHubUI.Instance.IsOpen || SaveHubUI.Instance.window.GetComponent<CanvasGroup>().alpha < .99f) throw new Exception("EnterGame did not open the authored window");
                    Capture("runs");
                    Click(SaveHubUI.Instance.closeButton.gameObject); stage++; break;
                case 2:
                    if (SaveHubUI.Instance.IsOpen) throw new Exception("Close did not finish");
                    ClickMain("Setting"); stage++; break;
                case 3:
                    Capture("settings");
                    Click(SaveHubUI.Instance.closeButton.gameObject); stage++; break;
                case 4:
                    ClickMain("EnterGame"); stage++; break;
                case 5:
                    Click(SaveHubUI.Instance.worldlines.create.gameObject); stage++; break;
                case 6:
                    SaveHubUI.Instance.detailContent.GetComponentInChildren<UnityEngine.UI.InputField>().text = "麦麦想吃白爆矿";
                    ClickHub("开启"); Capture("creation"); ClickHub("开始游戏"); stage++; break;
                case 7:
                    if (!SaveSystem.Safe) return;
                    if (SaveSystem.Current.name != "麦麦想吃白爆矿") throw new Exception("UI creation did not enter the selected run");
                    if (!SaveSystem.SaveNow(SaveKind.Manual)) throw new Exception(SaveSystem.Notice);
                    SaveHubUI.Instance.ShowHistory(SaveSystem.Current); stage++; break;
                case 8:
                    Capture("history");
                    ClickHub("读取此记录"); stage++; break;
                case 9:
                    ClickHub("确认"); stage++; break;
                case 10:
                    if (!SaveSystem.Safe) return;
                    if (!SaveSystem.SaveNow(SaveKind.Exit)) throw new Exception(SaveSystem.Notice);
                    SaveSystem.Die(); stage++; break;
                case 11:
                    Capture("death");
                    ClickHub("返回主菜单"); stage++; break;
                case 12:
                    if (SceneManager.GetActiveScene().name != "StartScene") return;
                    CheckLegacyRemoved(); ClickMain("EnterGame"); stage++; break;
                case 13:
                    Capture("return-runs");
                    var view = SaveHubUI.Instance.worldlines;
                    chosenWorldline = view.Selected.id;
                    if (view.count.text != "1" || view.records.text != view.Selected.snapshots.Count + " 个") throw new Exception("Worldlines are not grouped by run or summary count is wrong");
                    if (SaveHubUI.Instance.panel.GetComponentsInChildren<HoverableButton>().Any(b => b.text != null && (b.text.text == "继续游戏" || b.text.text == "查看保存记录"))) throw new Exception("Old load actions remain");
                    Click(view.load.gameObject); stage++; break;
                case 14:
                    if (SceneManager.GetActiveScene().name != "StartScene" || SaveHubUI.Instance.worldlines.gameObject.activeSelf) throw new Exception("Load game must open history without entering gameplay");
                    if (SaveHubUI.Instance.detailContent.GetComponentsInChildren<SaveJournalRow>().Length != SaveSystem.Repository.List().First(r=>r.id==chosenWorldline).snapshots.Count) throw new Exception("History does not show selected world's saves");
                    Capture("worldline-history"); ClickHub("自动"); stage++; break;
                case 15:
                    ClickHub("返回"); stage++; break;
                case 16:
                    if (SaveHubUI.Instance.worldlines.Selected.id != chosenWorldline) throw new Exception("History return lost worldline selection");
                    ClickHub("管理世界线"); stage++; break;
                case 17:
                    Capture("worldline-management"); ClickHub("修改名称"); stage++; break;
                case 18:
                    var nameInput = SaveHubUI.Instance.detailContent.GetComponentInChildren<UnityEngine.UI.InputField>();
                    nameInput.text = " ";
                    if (SaveHubUI.Instance.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="保存名称").Interactable) throw new Exception("Blank rename is enabled");
                    nameInput.text = "麦麦听说矿石释氧机和睡眠脉冲仪在白爆矿旁边悄悄地谈恋爱了呀";
                    ClickHub("保存名称"); stage++; break;
                case 19:
                    Capture("worldline-long-name"); ClickHub("管理世界线"); stage++; break;
                case 20:
                    ClickHub("复制世界线"); stage++; break;
                case 21:
                    if (SaveHubUI.Instance.worldlines.Selected.id == chosenWorldline || SaveHubUI.Instance.worldlines.count.text != "2") throw new Exception("Copy does not select independent worldline");
                    ClickHub("管理世界线"); stage++; break;
                case 22:
                    ClickHub("删除世界线"); stage++; break;
                case 23:
                    Capture("worldline-delete"); ClickHub("取消"); stage++; break;
                case 24:
                    if (SaveHubUI.Instance.worldlines.count.text != "2") throw new Exception("Cancel deleted a worldline");
                    ClickHub("管理世界线"); stage++; break;
                case 25:
                    ClickHub("删除世界线"); stage++; break;
                case 26:
                    ClickHub("确认"); stage++; break;
                case 27:
                    if (SaveHubUI.Instance.worldlines.count.text != "1") throw new Exception("Confirmed deletion failed");
                    var payload = SaveDataContract.NewGame();
                    for(int i=0;i<20;i++)
                    {
                        var run = new RunData {name="麦麦把白爆矿塞进了冰箱",skipGuide=true,lastPlayedUtc=DateTime.UtcNow.AddMinutes(-i-1)};
                        SaveSystem.Repository.Commit(run,new SavePoint {gameTime=new DateTime(2020,1,14,14,30,0),place="驾驶室",season="温和季"},payload,10);
                    }
                    var hard = new RunData {name="麦麦带着燃素寻找氧气罐",hardcore=true,skipGuide=true,lastPlayedUtc=DateTime.UtcNow.AddMinutes(1)};
                    SaveSystem.Repository.Commit(hard,new SavePoint {gameTime=new DateTime(2020,1,21,22,45,0),place="动力舱",season="温和季"},payload,10);
                    SaveHubUI.Instance.ShowRuns();
                    // The existing selection is retained; click the new first row to check sorted ordering.
                    SaveHubUI.Instance.worldlines.scroll.verticalNormalizedPosition=1; stage++; break;
                case 28:
                    var list = SaveHubUI.Instance.worldlines.scroll.content;
                    Click(list.GetComponentsInChildren<SaveHubButton>().First(b=>b.gameObject.activeInHierarchy).gameObject); stage++; break;
                case 29:
                    if (!SaveHubUI.Instance.worldlines.Selected.hardcore || SaveHubUI.Instance.worldlines.count.text != "22") throw new Exception("Recent sorting or worldline count failed");
                    Capture("worldline-hardcore"); ClickHub("载入游戏"); stage++; break;
                case 30:
                    var hardRows = SaveHubUI.Instance.detailContent.GetComponentsInChildren<SaveJournalRow>();
                    if (hardRows.Length != 1 || hardRows[0].retain.gameObject.activeInHierarchy || SaveHubUI.Instance.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="删除记录").Interactable) throw new Exception("Hardcore history rules failed");
                    ClickHub("返回"); stage++; break;
                case 31:
                    SaveHubUI.Instance.worldlines.scroll.verticalNormalizedPosition=0; stage++; break;
                case 32:
                    Capture("worldline-scrolled");
                    var last = SaveHubUI.Instance.worldlines.scroll.content.GetComponentsInChildren<SaveHubButton>().Last(b=>b.gameObject.activeInHierarchy);
                    Click(last.gameObject); stage++; break;
                case 33:
                    chosenWorldline = SaveHubUI.Instance.worldlines.Selected.id;
                    if (SaveHubUI.Instance.worldlines.Selected.hardcore) throw new Exception("Pooled row selected wrong worldline");
                    ClickHub("载入游戏"); stage++; break;
                case 34:
                    if (SceneManager.GetActiveScene().name != "StartScene") throw new Exception("First load click entered game");
                    ClickHub("读取此记录"); stage++; break;
                case 35:
                    if (!SaveSystem.Safe) return;
                    if(SaveSystem.Current.id != chosenWorldline) throw new Exception("Second load click entered wrong worldline");
                    File.WriteAllText("Logs/SaveValidation/play.txt", "PASS Unity Editor Play Mode: real main-menu Button pointer events and raycast targets, open/close, settings, create via UI, manual save, UI rewind, exit save, death, return to menu and reopen.\nPASS worldline-only list, summary record count, two-step load into selected worldline, filtered history return retains selection.\nPASS rename with 30-character name and blank rejection, independent copy, cancel/confirm deletion, 22 worlds and pooled scroll selection.\nPASS hardcore worldline opens single record with retain/delete disabled.\nPASS no legacy save UI objects (including inactive).\nPASS no Unity Error/Exception/Assert.\nPASS authored scene and prefab used without test-time rebuild.\n");
                    SessionState.SetBool("SaveValidationActive", false); SessionState.SetBool("SaveValidationCompleted", true); EditorApplication.isPlaying = false;
                    break;
            }
        }
        catch (Exception e) { File.WriteAllText("Logs/SaveValidation/play-error.txt", e.ToString()); SessionState.SetBool("SaveValidationActive", false); EditorApplication.isPlaying = false; if (Application.isBatchMode) EditorApplication.Exit(1); }
    }
}
