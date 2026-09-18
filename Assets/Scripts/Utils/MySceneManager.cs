using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MySceneManager
{
    private static AsyncOperation operation;
    public static bool IsLoading => operation != null && !operation.isDone;
    public static float Progress => operation == null ? 0 : operation.isDone ? 1 : Mathf.Clamp01(operation.progress / .9f);
    public static void LoadScene(int sceneBuildIndex)
    {
        if (IsLoading) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneBuildIndex)) throw new System.InvalidOperationException("缺少目标场景。");
        SaveSystem.Ready = false;
        Time.timeScale = 1;
        PublicMono.Instance.StopAllCoroutines();
        // 停止所有DOTween动画
        DOTween.KillAll();
        // 清空对象池
        ObjectBufferPool.Instance.Clear();
        // 移除事件监听
        EventManager.Instance.ClearEvents();
        // Drop cancelled requests/cache references. Single-mode scene loading already unloads unused assets.
        // Avoid two additional sweeps and synchronous GC/finalizer waits around every scene change.
        ResourcesManager.Instance.ClearSceneCache();
        operation = SceneManager.LoadSceneAsync(sceneBuildIndex, LoadSceneMode.Single);
    }
}
