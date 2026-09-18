using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SaveCreationValidation
{
    private const string Active = "SaveCreationValidation";
    private static int stage, trial;
    private static double clicked, deadline;
    private static double uiReady;
    private static FileStream lockedManifest;
    private static string previousPoint, lastRun;
    private static int transitionFrames;
    private static bool Fixed => SessionState.GetBool(Active+"Fixed",false);
    private static readonly System.Collections.Generic.List<string> report = new();
    static SaveCreationValidation()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message,stack,type) =>
        {
            if(!SessionState.GetBool(Active,false) || type!=LogType.Exception && type!=LogType.Error && type!=LogType.Assert)return;
            if(lockedManifest!=null && message.StartsWith("保存失败，原有进度已保留："))return;
            Directory.CreateDirectory("Logs/SaveValidation"); File.AppendAllText("Logs/SaveValidation/creation-unexpected-error.txt",message+"\n"+stack+"\n");
        };
    }
    public static void Baseline()
    {
        SessionState.SetBool(Active+"Fixed",false); Begin();
    }
    public static void Run()
    {
        SessionState.SetBool(Active+"Fixed",true); Begin();
    }
    private static void Begin()
    {
        SessionState.SetBool(Active, true); SessionState.SetBool(Active+"Finished", false);
        SessionState.SetBool(Active+"ExpectQuit",false);
        EditorSceneManager.OpenScene("Assets/Scenes/StartScene.unity"); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Isolate()
    {
        if(!SessionState.GetBool(Active,false))return;
        SaveSystem.StorageRootOverride=Path.Combine(Application.temporaryCachePath,"SaveCreationValidation-"+Guid.NewGuid().ToString("N"));
        GameSettings.StorageRootOverride=SaveSystem.StorageRootOverride;
        SessionState.SetString(Active+"Root",SaveSystem.StorageRootOverride);
    }
    private static void Tick()
    {
        if(SessionState.GetBool(Active+"ExpectQuit",false) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetBool(Active+"ExpectQuit",false); SessionState.SetBool(Active,false);
            try
            {
                var repo=new SaveRepository(SessionState.GetString(Active+"Root",""));
                var run=repo.List().First(r=>r.id==SessionState.GetString(Active+"QuitRun",""));
                var point=run.snapshots.First(p=>p.id==run.latestSnapshotId);
                if(point.kind!=SaveKind.Auto || point.id==SessionState.GetString(Active+"PreviousPoint",""))throw new Exception("Quit did not finish a new automatic save");
                SaveDataContract.Validate(repo.Read(run,point));
                report.Add("PASS save-and-quit wrote a valid new auto snapshot and stopped Editor Play Mode");
                report.Add("PASS blocked input and cursor ownership observed through "+transitionFrames+" transition frames");
                File.WriteAllLines("Logs/SaveValidation/creation-fixed.txt",report);EditorApplication.Exit(0);
            }
            catch(Exception e) { File.WriteAllText("Logs/SaveValidation/creation-error.txt",e.ToString());EditorApplication.Exit(1); }
            return;
        }
        if(SessionState.GetBool(Active+"Finished",false) && !EditorApplication.isPlayingOrWillChangePlaymode) { SessionState.SetBool(Active+"Finished",false); EditorApplication.Exit(0); return; }
        if(!SessionState.GetBool(Active,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(stage!=0 && EditorApplication.timeSinceStartup>deadline)throw new Exception("Creation timed out at stage "+stage);
            var hub=SaveHubUI.Instance;
            if(Fixed && SaveRuntime.Instance!=null && SaveRuntime.Instance.TransitionActive)
            {
                transitionFrames++;
                if(SaveTransitionUI.Instance==null || !SaveTransitionUI.Instance.IsVisible || !Cursor.visible)throw new Exception("Transition overlay or cursor disappeared before completion");
            }
            if(stage==0)
            {
                if(hub==null)return;
                deadline=EditorApplication.timeSinceStartup+60;
                hub.ShowCreation();
                hub.detailContent.GetComponentInChildren<UnityEngine.UI.InputField>().text="创建速度测试 "+trial;
                hub.panel.GetComponentsInChildren<HoverableButton>().First(b=>b.text!=null&&b.text.text=="开启").onClick.Invoke();
                stage=1;return;
            }
            if(stage==1)
            {
                clicked=EditorApplication.timeSinceStartup;
                var button=hub.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="开始游戏");
                button.onClick.Invoke();
                report.Add("trial="+trial+" handler_ms="+((EditorApplication.timeSinceStartup-clicked)*1000).ToString("F1")+" button_interactable_after_click="+button.Interactable);
                if(Fixed)
                {
                    if(button.Interactable || !hub.IsLoading || !SaveRuntime.Instance.TransitionActive)throw new Exception("Start click was not locked synchronously");
                    button.onClick.Invoke();button.onClick.Invoke();
                    hub.Close();
                    if(!hub.IsOpen)throw new Exception("Loading was closed");
                    var back=hub.footer.GetComponentsInChildren<HoverableButton>().First(b=>b.text.text=="返回");
                    Canvas.ForceUpdateCanvases();
                    var rect=(RectTransform)back.transform;
                    var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
                    var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                    if(hits.Count==0 || hits[0].gameObject.GetComponentInParent<SaveTransitionUI>()==null)
                    {
                        var graphics=SaveTransitionUI.Instance.GetComponentsInChildren<UnityEngine.UI.Graphic>();
                        var raycaster=SaveTransitionUI.Instance.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                        var direct=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();raycaster.Raycast(pointer,direct);
                        string detail="Loading input blocker: pointer="+pointer.position+" screen="+Screen.width+"x"+Screen.height+" hits="+string.Join(",",hits.Select(h=>h.gameObject.name))+" direct="+string.Join(",",direct.Select(h=>h.gameObject.name))+" raycaster="+raycaster.isActiveAndEnabled+" scale="+raycaster.transform.localScale+" graphics="+string.Join(";",graphics.Select(g=>g.name+" depth="+g.depth+" cull="+g.canvasRenderer.cull+" contains="+RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform,pointer.position,null)+" valid="+g.Raycast(pointer.position,null)+" scale="+g.transform.lossyScale));
                        CaptureTransition("creation-blocker-debug");throw new Exception(detail);
                    }
                    if(trial==0)CaptureTransition("creation-loading");
                }
                stage=2;return;
            }
            if(stage==2)
            {
                if(SceneManager.GetActiveScene().buildIndex!=1||!SaveSystem.Safe || Fixed && SaveRuntime.Instance.TransitionActive)return;
                report.Add("trial="+trial+" ready_ms="+((EditorApplication.timeSinceStartup-clicked)*1000).ToString("F1"));
                if(Fixed && SaveSystem.Repository.List().Count!=trial+1)throw new Exception("Repeated start created extra worlds");
                if(++trial<3)
                {
                    lastRun=SaveSystem.Current.id;
                    if(Fixed) {hub.ShowExit(false);HubButton("保存并退出").onClick.Invoke();}else SaveSystem.LeaveToMenu();
                    stage=3;return;
                }
                if(Fixed) {report.Add("PASS three repeated-click creation trials create exactly three worlds");stage=40;return;}
                Directory.CreateDirectory("Logs/SaveValidation");File.WriteAllLines("Logs/SaveValidation/creation-baseline.txt",report);
                SessionState.SetBool(Active,false);SessionState.SetBool(Active+"Finished",true);EditorApplication.isPlaying=false;
            }
            if(stage==3 && SceneManager.GetActiveScene().buildIndex==0 && (!Fixed || !SaveRuntime.Instance.TransitionActive))
            {
                if(Fixed)
                {
                    var run=SaveSystem.Repository.List().First(r=>r.id==lastRun);
                    if(run.snapshots.First(p=>p.id==run.latestSnapshotId).kind!=SaveKind.Auto)throw new Exception("Return-to-menu skipped auto save");
                    report.Add("PASS save-and-exit to main menu completed with automatic save");
                }
                stage=0;
            }
            if(stage==4)
            {
                previousPoint=SaveSystem.LoadedPointId;
                lockedManifest=new FileStream(Path.Combine(SaveSystem.Repository.Root,SaveSystem.Current.id,"run.json"),FileMode.Open,FileAccess.Read,FileShare.Read);
                hub.ShowExit(true);HubButton("保存并退出").onClick.Invoke();CaptureTransition("saving-exit");stage=5;return;
            }
            if(stage==40)
            {
                previousPoint=SaveSystem.LoadedPointId;hub.ShowGameMenu();uiReady=EditorApplication.timeSinceStartup+.25;stage=41;return;
            }
            if(stage==41)
            {
                if(EditorApplication.timeSinceStartup<uiReady)return;
                CaptureTransition("settings-game");HubButton("手动保存").onClick.Invoke();stage=42;return;
            }
            if(stage==42)
            {
                if(SaveRuntime.Instance.TransitionActive)return;
                var run=SaveSystem.Repository.List().First(r=>r.id==SaveSystem.Current.id);
                if(run.latestSnapshotId==previousPoint || run.snapshots.First(p=>p.id==run.latestSnapshotId).kind!=SaveKind.Manual)throw new Exception("Manual-save action failed");
                report.Add("PASS manual save displays transition, saves once and restores pause menu");
                hub.ShowExit(true);uiReady=EditorApplication.timeSinceStartup+.25;stage=43;return;
            }
            if(stage==43) {if(EditorApplication.timeSinceStartup<uiReady)return;CaptureTransition("small-save-exit");stage=4;return;}
            if(stage==5)
            {
                if(SaveRuntime.Instance.TransitionActive)return;
                lockedManifest.Dispose();lockedManifest=null;
                if(!EditorApplication.isPlaying || SaveSystem.LoadedPointId!=previousPoint || !HubButton("保存并退出").Interactable || !SaveSystem.Notice.Contains("未完成"))throw new Exception("Save failure did not preserve progress and restore controls");
                report.Add("PASS injected disk write failure stays in game, preserves last save, restores controls and shows failure");
                stage=6;return;
            }
            if(stage==6)
            {
                SessionState.SetString(Active+"QuitRun",SaveSystem.Current.id);
                SessionState.SetString(Active+"PreviousPoint",SaveSystem.LoadedPointId);
                SessionState.SetBool(Active+"ExpectQuit",true);
                HubButton("保存并退出").onClick.Invoke();stage=7;
            }
        }
        catch(Exception e) {lockedManifest?.Dispose();Directory.CreateDirectory("Logs/SaveValidation");File.WriteAllText("Logs/SaveValidation/creation-error.txt",e.ToString());SessionState.SetBool(Active,false);EditorApplication.Exit(1);}
    }
    private static HoverableButton HubButton(string text)=>SaveHubUI.Instance.panel.GetComponentsInChildren<HoverableButton>().First(b=>b.text!=null&&b.text.text==text);
    private static void CaptureTransition(string name)
    {
        Canvas.ForceUpdateCanvases();
        var roots=new[]{SaveHubUI.Instance.gameObject,SaveTransitionUI.Instance.gameObject};
        var cameraObject=new GameObject("Transition validation camera");var camera=cameraObject.AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.cullingMask=1<<31;
        var target=new RenderTexture(1920,1080,24);camera.targetTexture=target;
        var transforms=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        foreach(var t in transforms)t.gameObject.layer=31;
        var canvases=roots.Select(r=>r.GetComponent<Canvas>()).ToArray();
        var scalers=roots.Select(r=>r.GetComponent<UnityEngine.UI.CanvasScaler>()).ToArray();var scales=scalers.Select(s=>s.scaleFactor).ToArray();
        foreach(var scaler in scalers){scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;}
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
        Directory.CreateDirectory("Logs/SaveValidation");File.WriteAllBytes("Logs/SaveValidation/"+name+".png",image.EncodeToPNG());
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
        for(int i=0;i<scalers.Length;i++){scalers[i].uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scalers[i].scaleFactor=scales[i];}
        for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];
        RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(cameraObject);
    }
}
