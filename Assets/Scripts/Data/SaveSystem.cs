using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SaveSystem
{
    public static string StorageRootOverride;
    private static SaveRepository repository;
    public static SaveRepository Repository => repository ??= new SaveRepository(StorageRootOverride ?? Path.Combine(Application.persistentDataPath, "SavesV2"));
    public static RunData Current { get; private set; }
    public static string LoadedPointId { get; private set; }
    public static double PointPlaySeconds;
    public static bool IsDead { get; private set; }
    public static bool Ready;
    public static string Notice;
    public static event Action Changed;
    public static Func<DateTime, string> SeasonNameProvider;
    // A future character-trait step can claim the draft and later call StartNewRun; no trait UI yet.
    public static Func<NewRunOptions, bool> NextCreationStep;
    public static void CompleteCreation(NewRunOptions options, Action<Exception> failed = null)
    {
        if (SaveRuntime.Instance.TransitionActive) return;
        if (NextCreationStep?.Invoke(options) == true) return;
        StartNewRun(options, failed);
    }
    public static void MigrateLegacy()
    {
        if (StorageRootOverride != null) return;
        string path = Path.Combine(Application.persistentDataPath, "LoadData", "LoadData.json");
        if (!File.Exists(path)) return;
        try
        {
            var legacy = (LoadData)JsonManager.Deserialize(File.ReadAllText(path), typeof(LoadData));
            for (int i = 0; i < legacy.loads.Length; i++)
            {
                if (legacy.loads[i] == null || legacy.loads[i].gameTime == DateTime.MinValue || Repository.LegacyImported(i)) continue;
                try
                {
                    string oldFolder = Path.Combine(Application.persistentDataPath, "GameData" + i);
                    bool empty = !Directory.Exists(oldFolder) || Directory.GetFiles(oldFolder, "*.json").Length == 0;
                    var payload = empty && legacy.loads[i].gameTime == new DateTime(2020, 1, 1) ? SaveDataContract.NewGame() : SaveDataContract.Legacy(i);
                    var run = new RunData { name = "旧版世界线 " + (i + 1), skipGuide = legacy.loads[i].skipGuide, legacySlot = i };
                    var point = Describe(payload, SaveKind.Manual);
                    Repository.Commit(run, point, payload, 10);
                    Repository.MarkLegacy(run, i);
                }
                catch (Exception e) { Notice = "部分旧档未迁移，原文件已保留：" + e.Message; Debug.LogWarning(Notice); }
            }
        }
        catch (Exception e) { Notice = "旧档目录读取失败，原文件已保留：" + e.Message; Debug.LogWarning(Notice); }
    }
    public static void StartNewRun(NewRunOptions options, Action<Exception> failed = null)
    {
        var name = CleanName(options.name); bool hardcore = options.hardcore, skipGuide = options.skipGuide;
        SaveRuntime.Instance.BeginEntry("正在创建世界线", name, () =>
        {
            var run = new RunData { name = name, hardcore = hardcore, skipGuide = skipGuide };
            var payload = SaveDataContract.NewGame();
            var point = Describe(payload, SaveKind.Manual); point.initial = true;
            Repository.Commit(run, point, payload, GameSettings.Current.autoKeep);
            // The just-created payload is already in memory; do not write then immediately re-read it.
            PrepareEntry(run, point, payload);
        }, failed);
    }
    public static string CleanName(string name)
    {
        name = (name ?? "").Trim().Replace("\n", "").Replace("\r", "");
        if (name.Length == 0) return RunNameGenerator.Generate(Repository.List().Select(r => r.name));
        return name.Substring(0, Math.Min(30, name.Length));
    }
    public static void Enter(RunData run, SavePoint point = null, Action<Exception> failed = null)
    {
        point ??= run.snapshots.FirstOrDefault(p => p.id == run.latestSnapshotId) ?? throw new InvalidDataException("没有可继续的保存点");
        SaveRuntime.Instance.BeginEntry("正在载入世界线", run.name, () => PrepareEntry(run, point, Repository.Read(run, point)), failed);
    }
    private static void PrepareEntry(RunData run, SavePoint point, SavePayload payload)
    {
        // Validate and stage data before the new scene's managers initialize.
        GameDataManager.Instance.LoadSnapshot(payload);
        if (run.legacySlot >= 0) GameSettings.MigrateAudio((AudioData)JsonManager.Deserialize(payload.files["Audio"], typeof(AudioData)));
        SaveRuntime.Instance.CancelPending();
        Current = run; LoadedPointId = point.id; PointPlaySeconds = point.playSeconds; IsDead = false; Ready = false;
        Time.timeScale = 1;
        MySceneManager.LoadScene(1);
    }
    public static void LoadPendingBeforeManagers()
    {
        if (Current == null)
        {
            var payload = SaveDataContract.NewGame();
            Current = new RunData { name = RunNameGenerator.Generate(), skipGuide = true };
            var point = Describe(payload, SaveKind.Manual); point.initial = true;
            Repository.Commit(Current, point, payload, GameSettings.Current.autoKeep);
            LoadedPointId = point.id;
            GameDataManager.Instance.LoadSnapshot(payload);
        }
    }
    public static bool Safe => Ready && !IsDead && !TimeManager.Instance.IsAdvancing && (UnityEngine.Object.FindObjectOfType<ChatManager>() == null || !ChatManager.Instance.AwaitingDelivery) && (MouseManager.Instance == null || !MouseManager.Instance.IsBusy);
    public static void RequestSave(SaveKind kind, Action<bool> complete = null) => SaveRuntime.Instance.Enqueue(kind, complete);
    public static bool SaveNow(SaveKind kind)
    {
        if (!Safe || Current == null) return false;
        try
        {
            var payload = GameDataManager.Instance.CaptureSnapshot();
            var point = Describe(payload, kind); point.playSeconds = PointPlaySeconds;
            double oldElapsed = Current.unsavedSeconds;
            Current.unsavedSeconds = 0;
            try { Repository.Commit(Current, point, payload, GameSettings.Current.autoKeep); }
            catch { Current.unsavedSeconds = oldElapsed; throw; }
            LoadedPointId = point.id;
            Notice = point.KindLabel + "成功"; Changed?.Invoke();
            if (point.kind == SaveKind.Auto && !SaveRuntime.Instance.TransitionActive) SaveHubUI.Instance?.ShowAutoSaveResult(true);
            return true;
        }
        catch (Exception e) { Notice = "保存失败，原有进度已保留：" + e.Message; Debug.LogError(Notice); Changed?.Invoke(); if (kind == SaveKind.Auto && !SaveRuntime.Instance.TransitionActive) SaveHubUI.Instance?.ShowAutoSaveResult(false); return false; }
    }
    public static void MaterializeInitial(SavePoint initial)
    {
        try
        {
            var payload = GameDataManager.Instance.CaptureSnapshot();
            var point = Describe(payload, SaveKind.Manual); point.initial = true;
            point.playSeconds = PointPlaySeconds;
            Repository.Commit(Current, point, payload, GameSettings.Current.autoKeep);
            LoadedPointId = point.id;
            if (!Current.hardcore && Current.snapshots.Any(p => p.id == initial.id)) Repository.DeletePoint(Current, initial);
        }
        catch (Exception e) { Notice = "初始进度保存未完成：" + e.Message; Debug.LogWarning(Notice); }
    }
    public static SavePoint Describe(SavePayload payload, SaveKind kind)
    {
        var time = (TimeData)JsonManager.Deserialize(payload.files["TimeData"], typeof(TimeData));
        var point = new SavePoint { kind = kind == SaveKind.Exit ? SaveKind.Auto : kind, gameTime = time.init ? time.curTime : new DateTime(2020, 1, 1), gameVersion = Application.version };
        int place = (int)JsonManager.Deserialize(payload.files["LastPlace"], typeof(int));
        var config = Resources.LoadAll<PlaceData>("ScriptableObject/Place").FirstOrDefault(p => (int)p.placeType == place);
        point.place = config != null ? config.name : ((PlaceEnum)place).ToString();
        var state = (StateData)JsonManager.Deserialize(payload.files["State"], typeof(StateData));
        if (state.playerState != null)
            foreach (var pair in state.playerState) point.states[StateManager.ParsePlayerState(pair.Key)] = pair.Value.CurValue;
        // Preserve the actual HUD label until a world-calendar provider is available.
        point.season = SeasonNameProvider?.Invoke(point.gameTime);
        if (string.IsNullOrEmpty(point.season) && Ready)
            point.season = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>().FirstOrDefault(t => t.name == "SeasonText")?.text;
        return point;
    }
    public static void Die()
    {
        if (IsDead) return;
        IsDead = true;
        TimeManager.Instance.ShutTimePass();
        SaveRuntime.Instance.CancelPending();
        if (Current?.hardcore == true)
        {
            try { Repository.DeleteRun(Current); }
            catch (Exception e) { Notice = "硬核世界线删除失败：" + e.Message; Debug.LogError(Notice); }
        }
        SaveHubUI.Instance.ShowDeath();
    }
    public static void LeaveToMenu()
    {
        if (Current?.hardcore == true && IsDead && !Current.dead) Repository.DeleteRun(Current);
        if (Current != null && !Current.dead) Repository.Update(Current);
        Ready = false; Time.timeScale = 1; SaveRuntime.Instance.CancelPending();
        MySceneManager.LoadScene(0);
    }
    public static void NotifyPlayed()
    {
        if (Current == null) return;
        Current.lastPlayedUtc = DateTime.UtcNow;
        Repository.Update(Current);
    }
}

public sealed class SaveRuntime : MonoBehaviour
{
    public static SaveRuntime Instance { get; private set; }
    private SaveKind? pending;
    private Action<bool> callbacks;
    private bool allowQuit;
    private float retryAt;
    private float nextHeartbeat;
    public bool PausedByUI;
    public bool TransitionActive { get; private set; }
    private Coroutine readyRoutine;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("SaveRuntime"); DontDestroyOnLoad(go); go.AddComponent<SaveRuntime>();
    }
    private void Awake()
    {
        Instance = this;
        Application.wantsToQuit += WantsQuit;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameSettings.ApplyAudio();
        if (!Application.isEditor) GameSettings.ApplyDisplay();
        SaveSystem.MigrateLegacy();
    }
    private void OnDestroy() { Application.wantsToQuit -= WantsQuit; SceneManager.sceneLoaded -= OnSceneLoaded; if (Instance == this) { Instance = null; MouseManager.SetTransitionCursor(false); } }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (readyRoutine != null) StopCoroutine(readyRoutine);
        PausedByUI = TransitionActive; Time.timeScale = 1;
        SaveHubUI.Ensure();
        SaveTransitionUI.Prepare();
        if (scene.buildIndex == 1) readyRoutine = StartCoroutine(ReadyNextFrame());
        else SaveSystem.Ready = false;
    }
    private SaveTransitionUI BeginTransition(string title, string name)
    {
        var view = SaveTransitionUI.Open(title, name);
        TransitionActive = true; PausedByUI = true;
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        SaveHubUI.Instance?.SetLoading(true);
        MouseManager.SetTransitionCursor(true);
        return view;
    }
    private void EndTransition()
    {
        TransitionActive = false;
        SaveHubUI.Instance?.SetLoading(false);
        PausedByUI = SaveHubUI.Instance != null && SaveHubUI.Instance.IsOpen;
        MouseManager.SetTransitionCursor(false);
    }
    private void FailTransition(Exception error, Action<Exception> failed)
    {
        StartCoroutine(FinishFailedTransition(error, failed));
    }
    private IEnumerator FinishFailedTransition(Exception error, Action<Exception> failed)
    {
        if (SaveTransitionUI.Instance != null) yield return SaveTransitionUI.Instance.WaitForMinimumDisplay();
        SaveTransitionUI.Instance?.Hide(); EndTransition();
        SaveSystem.Notice = "操作未完成：" + error.Message;
        Debug.LogWarning(SaveSystem.Notice);
        failed?.Invoke(error);
    }
    public void BeginEntry(string title, string name, Action prepare, Action<Exception> failed)
    {
        if (TransitionActive) return;
        var view = BeginTransition(title, name);
        StartCoroutine(EnterWorld(view, prepare, failed));
    }
    private IEnumerator EnterWorld(SaveTransitionUI view, Action prepare, Action<Exception> failed)
    {
        // Render the input blocker and feedback before serialization or scene work starts.
        yield return null;
        try { prepare(); }
        catch(Exception error) { FailTransition(error, failed); yield break; }
        while (!SaveSystem.Ready)
        {
            float progress = MySceneManager.Progress;
            view.SetProgress(progress < .9f ? "正在加载场景…" : "正在准备开局…", .1f + .8f * progress);
            yield return null;
        }
        view.SetProgress("准备完成", 1);
        yield return view.Finish(); EndTransition();
    }
    public void SaveAndExit(bool application, Action<Exception> failed)
    {
        if (TransitionActive) return;
        var view = BeginTransition("正在保存进度", SaveSystem.Current?.name ?? "");
        StartCoroutine(SaveThenExit(view, application, failed));
    }
    public void SaveProgress(Action<Exception> failed)
    {
        if (TransitionActive) return;
        var view = BeginTransition("正在保存进度", SaveSystem.Current?.name ?? "");
        StartCoroutine(SaveThenExit(view, null, failed));
    }
    public void ExitWithoutSaving(bool application, Action<Exception> failed)
    {
        if (TransitionActive) return;
        var view = BeginTransition(application ? "正在退出游戏" : "正在返回主菜单", SaveSystem.Current?.name ?? "");
        StartCoroutine(ExitWithoutSave(view, application, failed));
    }
    private IEnumerator ExitWithoutSave(SaveTransitionUI view, bool application, Action<Exception> failed)
    {
        yield return null;
        try
        {
            if (application) { if (SaveSystem.Current != null && !SaveSystem.Current.dead) SaveSystem.Repository.Update(SaveSystem.Current); }
            else SaveSystem.LeaveToMenu();
        }
        catch(Exception error) { FailTransition(error,failed); yield break; }
        if (application) { yield return view.WaitForMinimumDisplay(); QuitApplication(); yield break; }
        while(SceneManager.GetActiveScene().buildIndex!=0 || MySceneManager.IsLoading)
        {view.SetProgress("正在返回主菜单…",MySceneManager.Progress);yield return null;}
        yield return view.Finish();EndTransition();
    }
    private IEnumerator SaveThenExit(SaveTransitionUI view, bool? application, Action<Exception> failed)
    {
        view.SetProgress(application.HasValue ? "正在写入自动保存…" : "正在写入手动保存…", .1f); yield return null;
        // Exit is offered at a safe point. Re-check before touching disk, and leave the player in place on failure.
        if (!SaveSystem.Safe || !SaveSystem.SaveNow(application.HasValue ? SaveKind.Auto : SaveKind.Manual))
        {
            FailTransition(new IOException(SaveSystem.Notice ?? "当前进度暂时无法保存，请稍后重试。"), failed); yield break;
        }
        if (!application.HasValue)
        {
            view.SetProgress("保存完成", 1); yield return view.Finish(); EndTransition();
            SaveHubUI.Instance?.ShowGameMenu(); yield break;
        }
        view.SetProgress(application.Value ? "保存完成，正在退出…" : "保存完成，正在返回主菜单…", 1);
        yield return null;
        if (application.Value) { yield return view.WaitForMinimumDisplay(); QuitApplication(); yield break; }
        try { SaveSystem.LeaveToMenu(); }
        catch(Exception error) { FailTransition(error,failed); yield break; }
        while(SceneManager.GetActiveScene().buildIndex != 0 || MySceneManager.IsLoading) yield return null;
        yield return view.Finish(); EndTransition();
    }
    private IEnumerator ReadyNextFrame()
    {
        yield return null;
        SaveSystem.Ready = true;
        SaveSystem.NotifyPlayed();
        var initial = SaveSystem.Current?.snapshots.FirstOrDefault(p => p.id == SaveSystem.LoadedPointId && p.initial && p.states.Count == 0);
        if (initial != null)
        {
            while (!SaveSystem.Safe && !SaveSystem.IsDead) yield return null;
            if (!SaveSystem.IsDead) SaveSystem.MaterializeInitial(initial);
        }
    }
    public void Enqueue(SaveKind kind, Action<bool> complete)
    {
        if (SaveSystem.IsDead || SaveSystem.Current == null) { complete?.Invoke(false); return; }
        if (kind == SaveKind.Exit) kind = SaveKind.Auto;
        if (!pending.HasValue || kind > pending.Value) pending = kind;
        callbacks += complete;
    }
    public void CancelPending() { var c = callbacks; callbacks = null; pending = null; c?.Invoke(false); }
    private void Update()
    {
        var run = SaveSystem.Current;
        if (!SaveSystem.Ready || run == null || SaveSystem.IsDead) return;
        bool active = !PausedByUI && Application.isFocused;
        if (SavePlayClock.Advance(run, Time.unscaledDeltaTime, active, GameSettings.Current.autoSave, GameSettings.Current.autoMinutes) && Time.unscaledTime >= retryAt) Enqueue(SaveKind.Auto, null);
        if (active) SaveSystem.PointPlaySeconds += Time.unscaledDeltaTime;
        if (pending.HasValue && SaveSystem.Safe && !TransitionActive)
        {
            var kind = pending.Value; var callback = callbacks; pending = null; callbacks = null;
            bool ok = SaveSystem.SaveNow(kind); retryAt = Time.unscaledTime + (ok ? 0 : 60); callback?.Invoke(ok);
        }
        if (Time.unscaledTime > nextHeartbeat)
        {
            nextHeartbeat = Time.unscaledTime + 30;
            try { SaveSystem.Repository.Update(run); } catch (Exception e) { Debug.LogWarning(e.Message); }
        }
        if (!TransitionActive && Input.GetKeyDown(KeyCode.Escape)) SaveHubUI.Instance.ToggleGameMenu();
    }
    private bool WantsQuit()
    {
        if (allowQuit) return true;
        if (TransitionActive) return false;
        if (!SaveSystem.Ready || SaveSystem.IsDead || SaveSystem.Current == null) return true;
        SaveHubUI.Instance.ShowExit(true); return false;
    }
    public void QuitApplication()
    {
        allowQuit = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
