#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// Explicit opt-in development-player smoke test. Normal launches never instantiate this component.
[DefaultExecutionOrder(32000)]
public sealed class SaveRuntimeValidation : MonoBehaviour
{
    private static bool enabledForRun;
    private string output;
    private double started;
    private bool probeCursor, expectedSystemCursor;
    private int cursorSamples, cursorFailures;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Isolate()
    {
        enabledForRun = Environment.GetCommandLineArgs().Contains("--save-validation");
        if (!enabledForRun) return;
        SaveSystem.StorageRootOverride = Path.Combine(Application.temporaryCachePath, "SavePlayerValidation-" + Guid.NewGuid().ToString("N"));
        GameSettings.StorageRootOverride = SaveSystem.StorageRootOverride;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartValidation()
    {
        if (!enabledForRun) return;
        var obj = new GameObject("SaveValidation"); DontDestroyOnLoad(obj); obj.AddComponent<SaveRuntimeValidation>();
    }
    private void Start()
    {
        output = Path.Combine(Application.dataPath, "..", "SaveValidation"); Directory.CreateDirectory(output);
        started = Time.realtimeSinceStartupAsDouble;
        Application.logMessageReceived += OnLog;
        StartCoroutine(Run());
    }
    private void Update()
    {
        SampleCursor("Update");
        if (Time.realtimeSinceStartupAsDouble - started > 150) { File.WriteAllText(Path.Combine(output, "timeout.txt"), SaveSystem.Notice ?? "Timeout"); SaveRuntime.Instance.QuitApplication(); }
    }
    private void LateUpdate() => SampleCursor("LateUpdate");
    private void SampleCursor(string phase)
    {
        if (!probeCursor) return;
        cursorSamples++;
        var mouse = MouseManager.Instance;
        bool correct = Cursor.visible == expectedSystemCursor;
        if (mouse != null)
            correct &= mouse.GetComponent<UnityEngine.UI.Image>().enabled != expectedSystemCursor
                && (!expectedSystemCursor || !mouse.animator.gameObject.activeSelf);
        if (!correct && cursorFailures++ == 0)
            File.AppendAllText(Path.Combine(output, "cursor-details.txt"), phase + " frame=" + Time.frameCount + " expected system=" + expectedSystemCursor + " actual=" + Cursor.visible + "\n");
    }
    private IEnumerator CheckCursor(bool systemCursor, string label)
    {
        cursorSamples = cursorFailures = 0; expectedSystemCursor = systemCursor; probeCursor = true;
        for (int i = 0; i < 30; i++) yield return null;
        probeCursor = false;
        File.AppendAllText(Path.Combine(output, "cursor-checks.txt"), label + ": samples=" + cursorSamples + ", mismatches=" + cursorFailures + "\n");
        Check(cursorSamples >= 58 && cursorFailures == 0, label + " (30 frames, Update + LateUpdate)");
    }
    private void OnLog(string condition, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) File.AppendAllText(Path.Combine(output, "errors.txt"), condition + "\n" + stack + "\n");
    }
    private IEnumerator Ready() { while (!SaveSystem.Safe || SaveRuntime.Instance.TransitionActive) yield return null; yield return null; }
    private void Check(bool valid, string label)
    {
        File.AppendAllText(Path.Combine(output, "checks.txt"), (valid ? "PASS " : "FAIL ") + label + "\n");
        if (!valid) throw new Exception(label);
    }
    private void CaptureUI(string path)
    {
        // Batch players do not present the backbuffer. Render the live Canvas through a camera.
        var hub=SaveHubUI.Instance; if(hub==null || !hub.IsOpen)return;
        int width=path.Contains("720p")?1280:1920,height=path.Contains("720p")?720:1080;
        var go=new GameObject("UI validation camera");var camera=go.AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.04f);camera.cullingMask=1<<31;
        var texture=new RenderTexture(width,height,24);camera.targetTexture=texture;
        var transforms=hub.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        foreach(var t in transforms)t.gameObject.layer=31;
        var canvas=hub.GetComponent<Canvas>();var scaler=hub.GetComponent<UnityEngine.UI.CanvasScaler>();
        var mode=scaler.uiScaleMode;var factor=scaler.scaleFactor;
        scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=width/1920f;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=texture;
        var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
        File.WriteAllBytes(path,image.EncodeToPNG());
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;scaler.uiScaleMode=mode;scaler.scaleFactor=factor;
        for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
        RenderTexture.active=old;camera.targetTexture=null;Destroy(image);Destroy(texture);Destroy(go);
    }
    private void ClickVisible(HoverableButton button)
    {
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;
        var buttonCanvas=button.GetComponentInParent<Canvas>().rootCanvas;
        var eventCamera=buttonCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:buttonCanvas.worldCamera;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {
            position=RectTransformUtility.WorldToScreenPoint(eventCamera,rect.TransformPoint(rect.rect.center)),
            button=UnityEngine.EventSystems.PointerEventData.InputButton.Left };
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform)), "visible pointer target: "+button.name);
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }
    private IEnumerator CheckSettingsFlow()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var transforms=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
        Check(!transforms.Any(t=>t.name=="SaveButton"||t.name=="QuitButton"),"temporary top save/quit objects removed from authored scene");
        WindowsManager.Instance.OpenWindow("EnvironmentBag");WindowsManager.Instance.OpenWindow("State");
        yield return new WaitForSecondsRealtime(.3f);
        var settings=transforms.First(t=>t.name=="Settings").GetComponent<HoverableButton>();
        ClickVisible(settings);yield return new WaitForSecondsRealtime(.3f);
        var hub=SaveHubUI.Instance;
        var view=hub.settingsView;
        Check(hub.IsOpen && hub.title.text=="设置" && view.Category=="游戏" && view.gameObject.activeInHierarchy,"bottom settings opens authored B sidebar Game page");
        Check(view.worldName.text==SaveSystem.Current.name && view.gamePage.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text=="时间线名称："),"game page labels timeline name");
        Check(!hub.filterBar.gameObject.activeSelf && !hub.detailScroll.gameObject.activeSelf && !hub.footer.gameObject.activeSelf,"old settings tabs and generic content are hidden");
        Check(view.menu.secondary && view.quit.secondary && view.resume.primary && view.save.frame.sprite!=null && view.load.frame.sprite!=null,"authored button artwork and action hierarchy retained");
        var desktopModal=transforms.First(t=>t.name=="Modal").GetComponent<UnityEngine.UI.Image>();
        Check(hub.panel.GetComponent<UnityEngine.UI.Image>().color==desktopModal.color && hub.panel.GetComponent<UnityEngine.UI.Image>().raycastTarget,"settings reuses rest backdrop appearance and blocks rays");
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {position=new Vector2(8,8)};
        var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0 && hits[0].gameObject==hub.panel,"background ray hits modal before desktop windows");
        yield return CaptureDesktop("settings-game-desktop.png");
        yield return CheckSettingsPresentation();
        string current=SaveSystem.LoadedPointId;
        ClickVisible(view.load);
        yield return new WaitForSecondsRealtime(.3f);
        Check(SaveSystem.LoadedPointId==current && scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene() && hub.detailContent.GetComponentsInChildren<SaveJournalRow>().Length>0,"settings load opens history without loading a save");
        yield return CaptureDesktop("settings-history-desktop.png");
        ClickVisible(hub.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="返回"));yield return new WaitForSecondsRealtime(.3f);
        float startedSave=Time.realtimeSinceStartup;
        ClickVisible(view.save);
        yield return new WaitForSecondsRealtime(.2f);
        Check(SaveRuntime.Instance.TransitionActive && SaveTransitionUI.Instance.IsVisible,"fast save progress is still visible after 0.2 seconds");
        while(SaveRuntime.Instance.TransitionActive)yield return null;
        Check(Time.realtimeSinceStartup-startedSave>=SaveTransitionUI.MinimumVisibleSeconds,"manual save respects minimum progress duration");
        Check(view.gameObject.activeInHierarchy && view.recent.text.Contains("手动保存"),"completed manual save refreshes the B summary");
        hub.Close();yield return new WaitForSecondsRealtime(.2f);
        float scale=Time.timeScale;string before=SaveSystem.LoadedPointId;SaveSystem.RequestSave(SaveKind.Auto);
        while(SaveSystem.LoadedPointId==before)yield return null;
        yield return null;
        Check(!hub.IsOpen && hub.autoSaveToast.alpha>.9f && hub.autoSaveToastText.text=="自动保存完成" && !hub.autoSaveToast.blocksRaycasts && Time.timeScale==scale,"background automatic save shows nonblocking toast without pausing");
        yield return CaptureDesktop("auto-save-toast-desktop.png");
        yield return new WaitForSecondsRealtime(1);
        Check(hub.autoSaveToast.alpha>.9f,"automatic save notice remains readable for at least one second");
        yield return new WaitForSecondsRealtime(2);
        Check(hub.autoSaveToast.alpha==0,"automatic save notice fades away");
    }
    private IEnumerator CheckSettingsPresentation()
    {
        var hub=SaveHubUI.Instance;var view=hub.settingsView;
        var button=view.load;var fill=button.GetComponent<UnityEngine.UI.Image>().color;var frame=button.frame.color;
        var label=button.text.rectTransform.anchoredPosition;var bounds=button.rectTransform.rect;
        var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
        button.OnPointerEnter(pointer);yield return new WaitForSecondsRealtime(.25f);
        Check(button.GetComponent<UnityEngine.UI.Image>().color==fill && button.frame.color!=frame,"B hover changes existing border without recoloring button fill");
        button.OnPointerDown(pointer);yield return new WaitForSecondsRealtime(.12f);
        Check(Mathf.Abs(button.text.rectTransform.anchoredPosition.y-label.y+1)<.1f && button.rectTransform.rect==bounds,"B press feedback moves label one pixel and preserves hit area");
        button.OnPointerUp(pointer);button.OnPointerExit(pointer);yield return new WaitForSecondsRealtime(.2f);
        string originalName=SaveSystem.Current.name;
        SaveSystem.Current.name="麦麦听说矿石释氧机和睡眠脉冲仪在白爆矿旁边悄悄地谈恋爱了呀";
        hub.ShowGameMenu();yield return new WaitForSecondsRealtime(.2f);
        Check(view.worldName.preferredHeight<=view.worldName.rectTransform.rect.height,"long worldline name fits authored multiline area");
        yield return CaptureDesktop("settings-long-name-desktop.png");
        CaptureUI(Path.Combine(output,"settings-game-720p.png"));
        SaveSystem.Current.name=originalName;
        var windowSize=hub.window.sizeDelta;
        ClickVisible(view.navigationScroll.content.GetComponentsInChildren<SaveHubButton>().First(b=>b.text.text=="声音"));
        yield return new WaitForSecondsRealtime(.25f);
        var volume=view.Rows.First(r=>r.name=="Setting-master");volume.slider.value=.37f;
        Check(Math.Abs(AudioListener.volume-.37f)<.01f && hub.window.sizeDelta==windowSize,"B volume slider changes actual volume without resizing window");
        yield return CaptureDesktop("settings-sound-desktop.png");
        ClickVisible(view.navigationScroll.content.GetComponentsInChildren<SaveHubButton>().First(b=>b.text.text=="存档"));
        yield return new WaitForSecondsRealtime(.2f);
        yield return CaptureDesktop("settings-storage-desktop.png");
        for(int i=0;i<12;i++)SaveSettingRegistry.Register(new SaveSettingDefinition {id="validation-"+i,category="扩展测试",label="扩展设置 "+i,value=()=>"默认",change=()=>{}});
        hub.ShowSettings();yield return new WaitForSecondsRealtime(.2f);
        ClickVisible(view.navigationScroll.content.GetComponentsInChildren<SaveHubButton>().First(b=>b.text.text=="扩展测试"));
        yield return new WaitForSecondsRealtime(.2f);Canvas.ForceUpdateCanvases();
        Check(view.Rows.Count==12 && view.optionsScroll.content.rect.height>view.optionsScroll.viewport.rect.height && view.optionsScroll.verticalScrollbar.gameObject.activeInHierarchy,"registered category and long setting list use authored templates with scrolling");
        view.optionsScroll.verticalNormalizedPosition=0;yield return new WaitForSecondsRealtime(.2f);
        Check(view.resume.gameObject.activeInHierarchy && hub.window.sizeDelta==windowSize,"scrolling settings preserves fixed resume footer and window size");
        SaveSettingRegistry.Items.RemoveAll(s=>s.id.StartsWith("validation-"));
        hub.ShowGameMenu();yield return new WaitForSecondsRealtime(.25f);
        ClickVisible(view.menu);yield return new WaitForSecondsRealtime(.2f);
        Check(!view.gameObject.activeSelf && hub.title.text=="返回主菜单","B secondary menu action opens save-and-return confirmation");
        ClickVisible(hub.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="取消"));yield return new WaitForSecondsRealtime(.2f);
        Check(view.gameObject.activeSelf && view.Category=="游戏","cancel return confirmation restores B game page");
    }
    private IEnumerator CaptureDesktop(string filename)
    {
        var canvases=FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas&&c.renderMode!=RenderMode.WorldSpace).ToArray();
        var renderModes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();var distances=canvases.Select(c=>c.planeDistance).ToArray();
        var scalers=canvases.Select(c=>c.GetComponent<UnityEngine.UI.CanvasScaler>()).ToArray();
        var modes=scalers.Select(s=>s!=null?s.uiScaleMode:default).ToArray();var factors=scalers.Select(s=>s!=null?s.scaleFactor:1).ToArray();
        var go=new GameObject("Desktop verification camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        var target=new RenderTexture(1920,1080,24);camera.targetTexture=target;
        for(int i=0;i<canvases.Length;i++) {
            if(scalers[i]!=null){scalers[i].uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;scalers[i].scaleFactor=1;}
            canvases[i].renderMode=RenderMode.ScreenSpaceCamera;canvases[i].worldCamera=camera;canvases[i].planeDistance=1;
            // Switching a live overlay to a camera invalidates cached UI batches as well
            // as masks. Rebuild every graphic for the verification camera.
            foreach(var graphic in canvases[i].GetComponentsInChildren<UnityEngine.UI.Graphic>())graphic.SetAllDirty();
        }
        // Let RectMask2D recalculate its camera-dependent clip rect before capturing.
        yield return null;Canvas.ForceUpdateCanvases();yield return null;
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(1920,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(output,filename),texture.EncodeToPNG());
        if(SaveHubUI.Instance!=null && SaveHubUI.Instance.settingsView.gameObject.activeInHierarchy)
        {
            foreach(var button in SaveHubUI.Instance.settingsView.GetComponentsInChildren<SaveHubButton>().Where(b=>b.primary||b.selected))
            {
                var rect=button.rectTransform;
                var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.center.y,0)));
                bool green=false;
                for(int x=0;x<10;x++) { var color=texture.GetPixel(Mathf.Clamp((int)point.x+x,0,1919),Mathf.Clamp((int)point.y,0,1079));green|=color.g>.55f && color.g-color.r>.12f; }
                File.AppendAllText(Path.Combine(output,"render-checks.txt"),filename+" "+button.name+" frame-visible="+green+" cull="+button.frame.canvasRenderer.cull+" color="+button.frame.color+"\n");
            }
        }
        for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=renderModes[i];canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];if(scalers[i]!=null){scalers[i].uiScaleMode=modes[i];scalers[i].scaleFactor=factors[i];}}
        RenderTexture.active=old;camera.targetTexture=null;Destroy(texture);Destroy(target);Destroy(go);Canvas.ForceUpdateCanvases();
        yield return null;
    }
    private IEnumerator Run()
    {
        yield return null;
        yield return CheckCursor(false, "main menu custom cursor stable");
        SaveHubUI.Instance.ShowRuns();
        yield return new WaitForSecondsRealtime(1);
        yield return CheckCursor(true, "main menu modal cursor stable");
        SaveHubUI.Instance.worldlines.create.onClick.Invoke();
        yield return new WaitForSecondsRealtime(1);
        var input = SaveHubUI.Instance.detailContent.GetComponentInChildren<UnityEngine.UI.InputField>();
        var random = SaveHubUI.Instance.detailContent.GetComponentsInChildren<HoverableButton>().First(b => b.text.text == "随机");
        var previousName = input.text; random.onClick.Invoke();
        Check(input.text.StartsWith("麦麦") && input.text != previousName, "random name button updates input");
        random.OnPointerEnter(null);
        yield return new WaitForSecondsRealtime(.25f);
        Check(((SaveHubButton)random).frame.color.g > .7f && random.text.color == Color.white, "prefab button hover works while paused");
        random.OnPointerExit(null);
        yield return new WaitForSecondsRealtime(.25f);
        Check(Math.Abs(((SaveHubButton)random).frame.color.r - 118f/255) < .04f, "prefab button hover exits while paused");
        var hub = SaveHubUI.Instance;
        var drag = hub.window.GetComponentInChildren<SaveWindowDrag>();
        var beforeDrag = hub.window.anchoredPosition;
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = new Vector2(Screen.width / 2, Screen.height / 2) };
        drag.OnBeginDrag(pointer); pointer.position += new Vector2(40, 20); drag.OnDrag(pointer);
        Check((hub.window.anchoredPosition - beforeDrag).sqrMagnitude > 100, "title bar drag moves window");
        hub.window.anchoredPosition = beforeDrag;
        input.text = "麦麦听说矿石释氧机和精密元件在谈恋爱";
        Check(!hub.panel.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("偏好设置对")||t.text.Contains("更改即时生效")), "no redundant captions");
        CaptureUI(Path.Combine(output, "creation.png"));
        yield return new WaitForSecondsRealtime(1);
        string newName=input.text;
        var preview=new RunData {name="麦麦听说废铁刀和白爆矿在谈恋爱"};
        var sample=SaveDataContract.NewGame();
        for(int i=0;i<6;i++) SaveSystem.Repository.Commit(preview,new SavePoint {kind=i%2==0?SaveKind.Manual:SaveKind.Auto,gameTime=new DateTime(2020,1,14,14,30,0).AddHours(-i*7),place=new[]{"驾驶室","动力舱","维生舱"}[i%3],season="温和季",playSeconds=13320-i*600,savedUtc=DateTime.UtcNow.AddMinutes(-i*10)},sample,10);
        string previewShare=Path.Combine(SaveSystem.Repository.Root,"preview.wssave");SaveSystem.Repository.Export(preview,previewShare);
        hub.ShowRuns();hub.worldlines.import.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
        var inspect=hub.footer.GetComponentsInChildren<SaveHubButton>().First(b=>b.text.text=="查看文件信息");
        Check(!inspect.Interactable && inspect.primary,"import preview is primary and disabled until a path is provided");
        hub.detailContent.GetComponentInChildren<UnityEngine.UI.InputField>().text=previewShare;
        yield return new WaitForSecondsRealtime(.25f);
        var browseImport=hub.detailContent.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="选择文件");
        Check(browseImport.text.preferredHeight<=browseImport.text.rectTransform.rect.height,"long import path keeps browse label readable");
        CaptureUI(Path.Combine(output,"small-import.png"));
        inspect.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
        var confirmImport=hub.footer.GetComponentsInChildren<SaveHubButton>().First(b=>b.text.text=="确认");
        Check(confirmImport.primary && !confirmImport.dangerous,"import confirmation is a normal primary action");
        CaptureUI(Path.Combine(output,"small-import-confirm.png"));confirmImport.onClick.Invoke();
        Check(SaveSystem.Repository.List().Count==2,"compact import flow creates an independent worldline");
        hub.ShowHistory(preview); yield return new WaitForSecondsRealtime(.5f);
        var journal=hub.detailContent.GetComponentsInChildren<SaveJournalRow>();
        Check(journal.Length==6 && hub.filterBar.GetComponentsInChildren<HoverableButton>().Length==3,"journal layout and only all/auto/manual tabs");
        Check(journal[0].button.selected && journal[0].height.preferredHeight>150,"first journal record expanded");
        CaptureUI(Path.Combine(output,"journal.png"));yield return new WaitForSecondsRealtime(.4f);
        journal[1].button.onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);
        Check(journal[1].button.selected && !journal[0].button.selected && journal[0].height.preferredHeight<85 && journal[1].height.preferredHeight>200,"journal selection collapses previous and expands auto retain action");
        Check(journal[1].button.text.color==Color.white && journal[1].button.GetComponent<UnityEngine.UI.Image>().color==new Color32(17,17,17,255),"selected record preserves text and surface colors");
        CaptureUI(Path.Combine(output,"journal-selected.png"));yield return new WaitForSecondsRealtime(.4f);
        hub.ShowSettings();yield return new WaitForSecondsRealtime(.3f);
        hub.settingsView.navigationScroll.content.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="存档").onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);
        var settingsHub=SaveHubUI.Instance;
        Check(settingsHub.settingsView.optionsScroll.content.GetComponentsInChildren<UnityEngine.UI.Text>().Count(t=>t.text=="+"||t.text=="-")==4,"numeric setting controls have visible ASCII labels");
        var autoButton=settingsHub.settingsView.optionsScroll.content.GetComponentsInChildren<SaveHubButton>().First(b=>b.text.text=="开启");autoButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.2f);
        Check(settingsHub.settingsView.optionsScroll.content.GetComponentsInChildren<SaveHubButton>().Count(b=>(b.text.text=="+"||b.text.text=="-")&&!b.Interactable)==2,"auto interval disabled while timer off; exit retention still editable");
        autoButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.2f);
        CaptureUI(Path.Combine(output,"save-settings.png"));yield return new WaitForSecondsRealtime(.4f);
        SaveSystem.StartNewRun(new NewRunOptions { name = newName, skipGuide = true });
        yield return Ready();
        Check(MouseManager.Instance != null, "game scene has custom cursor owner");
        yield return CheckSettingsFlow();
        yield return CheckCursor(false, "game custom cursor stable after scene entry");
        for (int cycle = 0; cycle < 3; cycle++)
        {
            SaveHubUI.Instance.ShowGameMenu();
            yield return new WaitForSecondsRealtime(.25f);
            yield return CheckCursor(true, "game modal cursor stable cycle " + cycle);
            SaveHubUI.Instance.Close();
            yield return new WaitForSecondsRealtime(.25f);
            yield return CheckCursor(false, "custom cursor restored cycle " + cycle);
        }
        MouseManager.Instance.Wait(3);
        SaveHubUI.Instance.ShowSettings();
        yield return CheckCursor(true, "modal suppresses waiting cursor without duplicate");
        SaveHubUI.Instance.Close();
        yield return new WaitForSecondsRealtime(.25f);
        Check(MouseManager.Instance.IsBusy && MouseManager.Instance.animator.gameObject.activeSelf && MouseManager.Instance.mouseCanvasGroup.alpha == 0, "closing modal preserves active wait cursor");
        yield return Ready();
        yield return CheckCursor(false, "custom cursor restored after waiting");
        CaptureUI(Path.Combine(output, "existing-windows.png"));
        yield return new WaitForSecondsRealtime(.5f);
        Check(SaveSystem.SaveNow(SaveKind.Manual), "manual save");
        GameSettings.Current.audio.masterVolume = .5f; GameSettings.Current.audio.sfxVolume = .25f; GameSettings.Save();
        Check(Math.Abs(AudioListener.volume - .5f) < .01 && Math.Abs(SoundManager.Instance.transform.Find("SFX_AudioSource").GetComponent<AudioSource>().volume - .25f) < .01, "global volume and SFX channel gain");
        var run = SaveSystem.Current;
        var manual = run.snapshots.Last();
        double state = StateManager.Instance.PlayerStateDict[PlayerStateEnum.Health].CurValue;
        StateManager.Instance.ChangePlayerState(PlayerStateEnum.Health, -1);
        SaveHubUI.Instance.ShowHistory(run);yield return new WaitForSecondsRealtime(.3f);
        SaveHubUI.Instance.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="读取此记录").onClick.Invoke();
        SaveHubUI.Instance.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="确认").onClick.Invoke();
        Check(SaveTransitionUI.Instance != null && SaveTransitionUI.Instance.IsVisible,"read action shows loading feedback");
        yield return new WaitForSecondsRealtime(.4f);yield return Ready();
        yield return CheckCursor(false, "custom cursor stable after rewind scene reload");
        Check(Math.Abs(StateManager.Instance.PlayerStateDict[PlayerStateEnum.Health].CurValue - state) < .01, "health restored");
        SaveHubUI.Instance.ShowHistory(run);
        yield return new WaitForSecondsRealtime(1);
        CaptureUI(Path.Combine(output, "history.png"));
        yield return new WaitForSecondsRealtime(1);
        SaveHubUI.Instance.Close();
        var reference = WindowsManager.Instance.OpenWindow("Custom", true) as CustomWindow;
        reference.SetContent("游戏原有窗口\n确认后继续游戏？"); reference.AddButton("确认", () => { }, false); reference.AddButton("取消", () => { }, false);
        yield return new WaitForSecondsRealtime(.8f);
        reference.RectTransform.anchoredPosition = new Vector2(-430, 0);
        SaveHubUI.Instance.ShowGameMenu();
        SaveHubUI.Instance.window.anchoredPosition = new Vector2(430, 0);
        var blocker = SaveHubUI.Instance.panel.GetComponent<UnityEngine.UI.Image>(); var shade = blocker.color; blocker.color = Color.clear;
        yield return new WaitForSecondsRealtime(.5f);
        CaptureUI(Path.Combine(output, "window-comparison.png"));
        yield return new WaitForSecondsRealtime(.5f);
        blocker.color = shade; SaveHubUI.Instance.Close(); WindowsManager.Instance.CloseWindow("Custom");
        Check(SaveSystem.SaveNow(SaveKind.Exit) && run.snapshots.First(p=>p.id==run.latestSnapshotId).kind==SaveKind.Auto, "legacy exit request normalized to auto");
        SaveHubUI.Instance.ShowExit(false);yield return new WaitForSecondsRealtime(.3f);
        SaveHubUI.Instance.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="保存并退出").onClick.Invoke();
        while(SaveSystem.Ready)yield return null;yield return new WaitForSecondsRealtime(.5f);
        var afterExit=SaveSystem.Repository.List().First(r=>r.id==run.id);
        Check(afterExit.snapshots.First(p=>p.id==afterExit.latestSnapshotId).kind==SaveKind.Auto,"save and exit button writes auto and returns to menu");
        while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "StartScene" || SaveRuntime.Instance.TransitionActive) yield return null;
        yield return null;
        yield return CheckCursor(false, "custom cursor restored after exit to main menu");
        SaveSystem.Enter(afterExit);yield return Ready();run=SaveSystem.Current;
        string shared = Path.Combine(output, "roundtrip.wssave"); SaveSystem.Repository.Export(run, shared);
        var imported = SaveSystem.Repository.Import(shared, SaveDataContract.Validate);
        Check(imported.id != run.id && imported.snapshots.Count == run.snapshots.Count, "live snapshot share roundtrip");
        SaveSystem.Die();
        Check(SaveSystem.Repository.List().Any(r => r.id == run.id), "ordinary death preserves run");
        SaveSystem.StartNewRun(new NewRunOptions { name = "麦麦残忍地杀害了白爆矿", hardcore = true, skipGuide = true });
        yield return Ready();
        Check(SaveSystem.SaveNow(SaveKind.Manual), "hardcore manual save");
        Check(SaveSystem.SaveNow(SaveKind.Auto) && SaveSystem.Current.snapshots.Count == 1, "hardcore only latest");
        Check(!SaveSystem.Repository.Copy(SaveSystem.Current).hardcore, "hardcore copy ordinary");
        var hardId = SaveSystem.Current.id; SaveSystem.Die();
        Check(!SaveSystem.Repository.List().Any(r => r.id == hardId), "hardcore death deletion");
        SaveSystem.StartNewRun(new NewRunOptions { name = "教程回档测试", skipGuide = false });
        yield return Ready();
        yield return new WaitForSecondsRealtime(.5f);
        yield return Ready();
        Check(SaveSystem.SaveNow(SaveKind.Manual), "tutorial dialogue save");
        SaveSystem.Enter(SaveSystem.Current);
        yield return Ready();
        Check(!SaveSystem.Current.skipGuide, "tutorial dialogue load");
        SaveHubUI.Instance.ShowSettings();
        yield return new WaitForSecondsRealtime(.5f);
        Screen.SetResolution(1280, 720, false);
        yield return new WaitForSecondsRealtime(.5f);
        CaptureUI(Path.Combine(output, "settings-720p.png"));
        yield return new WaitForSecondsRealtime(.5f);
        string previousExitPoint = SaveSystem.LoadedPointId;
        string exitRun = SaveSystem.Current.id;
        Application.quitting += () =>
        {
            var saved = SaveSystem.Repository.List().First(r => r.id == exitRun);
            var point = saved.snapshots.First(p => p.id == saved.latestSnapshotId);
            Check(point.kind == SaveKind.Auto && point.id != previousExitPoint, "save-and-quit finishes a new automatic save before application quitting");
            SaveDataContract.Validate(SaveSystem.Repository.Read(saved, point));
            File.WriteAllText(Path.Combine(output, "complete.txt"), "Development player save/load/death tests completed; save-and-quit wrote a valid automatic snapshot and reached application quitting.");
        };
        SaveHubUI.Instance.ShowExit(true);
        yield return new WaitForSecondsRealtime(.25f);
        SaveHubUI.Instance.footer.GetComponentsInChildren<HoverableButton>().First(b => b.text.text == "保存并退出").onClick.Invoke();
    }
}
#endif
