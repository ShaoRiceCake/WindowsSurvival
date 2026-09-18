using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Resources资源管理器
/// </summary>
public class ResourcesManager
{
    private static ResourcesManager instance = new();

    public static ResourcesManager Instance => instance;

    private abstract class BaseResourceInfo { }

    private class ResourceInfo<T> : BaseResourceInfo where T : Object
    {
        /// <summary>
        /// 待加载的资源
        /// </summary>
        public T asset;
        /// <summary>
        /// 资源是否待卸载
        /// </summary>
        public bool toBeUnloaded;
        /// <summary>
        /// 资源加载完毕时的回调函数
        /// </summary>
        private UnityAction<T> onAssetLoaded;
        /// <summary>
        /// 开启异步加载资源的协同程序
        /// </summary>
        public Coroutine coroutine;

        public ResourceInfo(UnityAction<T> onAssetLoaded)
        {
            AddCallBack(onAssetLoaded);
        }

        public ResourceInfo(T asset)
        {
            this.asset = asset;
        }

        /// <summary>
        /// 停止异步加载过程
        /// </summary>
        public void StopCoroutine() => PublicMono.Instance.StopCoroutine(coroutine);

        /// <summary>
        /// 添加资源加载完毕后的执行的逻辑
        /// </summary>
        /// <param name="onAssetLoaded">资源加载完毕后的执行的逻辑</param>
        public void AddCallBack(UnityAction<T> onAssetLoaded) => this.onAssetLoaded += onAssetLoaded;

        /// <summary>
        /// 执行资源加载完毕后的逻辑
        /// </summary>
        public void InvokeCallBack() => onAssetLoaded?.Invoke(asset);

        /// <summary>
        /// 清除回调函数和协程 防止内存泄漏
        /// </summary>
        public void ClearMemory()
        {
            onAssetLoaded = null;
            coroutine = null;
        }
    }

    private Dictionary<string, BaseResourceInfo> resourceMap = new();

    private ResourcesManager() { }

    #region 资源加载
    /// <summary>
    /// 异步加载资源
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    /// <param name="path">资源路径</param>
    /// <param name="onAssetLoaded">异步加载结束后的回调函数</param>
    public void LoadAsync<T>(string path, UnityAction<T> onAssetLoaded) where T : Object
    {
        string assetName = GetAssetName<T>(path);
        ResourceInfo<T> resource;
        // 不存在该资源
        if (!resourceMap.ContainsKey(assetName))
        {
            resource = new ResourceInfo<T>(onAssetLoaded);
            // 将加载过的资源存入字典
            resourceMap.Add(assetName, resource);
            // 调用协程加载资源
            resource.coroutine = PublicMono.Instance.StartCoroutine(LoadAsyncCoroutine<T>(path));
        }
        // 存在该资源
        else
        {
            resource = resourceMap[assetName] as ResourceInfo<T>;
            // 资源没有加载完
            if (resource.asset == null)
            {
                // 记录对于当前资源加载完毕后执行的逻辑
                // 当以前的资源加载完后一并执行
                resource.AddCallBack(onAssetLoaded);
            }
            // 资源加载完成了
            else
            {
                onAssetLoaded?.Invoke(resource.asset);
            }
        }
    }

    private IEnumerator LoadAsyncCoroutine<T>(string path) where T : Object
    {
        ResourceRequest request = Resources.LoadAsync<T>(path);
        yield return request;
        // 异步加载结束后执行
        string assetName = GetAssetName<T>(path);
        // 记录异步加载的资源
        ResourceInfo<T> resource = resourceMap[assetName] as ResourceInfo<T>;
        resource.asset = request.asset as T;

        // 如果资源待卸载
        if (resource.toBeUnloaded)
        {
            // 直接卸载该资源
            UnloadAsset(assetName, resource.asset);
        }
        else
        {
            // 执行资源加载完毕后的回调函数
            resource.InvokeCallBack();
            resource.ClearMemory();
        }
    }

    /// <summary>
    /// 同步加载资源
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    /// <param name="path">资源路径</param>
    /// <returns></returns>
    public T Load<T>(string path) where T : Object
    {
        T asset;

        string assetName = GetAssetName<T>(path);
        ResourceInfo<T> resource;
        // 不存在该资源
        if (!resourceMap.ContainsKey(assetName))
        {
            // 同步加载
            asset = Resources.Load<T>(path);
            // 记录该资源
            resource = new ResourceInfo<T>(asset);
            resourceMap.Add(assetName, resource);
        }
        // 存在该资源
        else
        {
            resource = resourceMap[assetName] as ResourceInfo<T>;
            // 资源还没有加载完
            if (resource.asset == null)
            {
                // 停止异步加载
                resource.StopCoroutine();
                // 改用同步加载
                asset = Resources.Load<T>(path);
                resource.asset = asset;

                // 执行异步加载的回调
                resource.InvokeCallBack();
                resource.ClearMemory();
            }
            // 资源加载完毕
            else
            {
                asset = resource.asset;
            }
        }

        return asset;
    }
    #endregion

    #region 资源卸载
    public void UnloadAsset<T>(string path) where T : Object
    {
        string assetName = GetAssetName<T>(path);
        ResourceInfo<T> resource;
        // 存在该资源
        if (resourceMap.ContainsKey(assetName))
        {
            resource = resourceMap[assetName] as ResourceInfo<T>;
            // 资源加载完毕
            if (resource.asset != null)
            {
                // 卸载资源
                UnloadAsset(assetName, resource.asset);
            }
            // 资源还有没加载完
            else
            {
                // 标记该资源为待卸载
                resource.toBeUnloaded = true;
                resource.ClearMemory();
            }
        }
    }

    private void UnloadAsset(string assetName, Object assetToUnload)
    {
        resourceMap.Remove(assetName);
        Resources.UnloadAsset(assetToUnload);
    }

    // Scene loading owns resource reclamation; discard references to cancelled async requests.
    public void ClearSceneCache() => resourceMap.Clear();

    public void UnloadUnusedAssets(UnityAction onAssetUnloaded = null)
    {
        resourceMap.Clear();
        PublicMono.Instance.StartCoroutine(UnloadUnusedAssetsCoroutine(onAssetUnloaded));
    }

    private IEnumerator UnloadUnusedAssetsCoroutine(UnityAction onAssetUnloaded)
    {
        yield return Resources.UnloadUnusedAssets();
        // 执行垃圾回收
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        onAssetUnloaded?.Invoke();
    }
    #endregion

    private string GetAssetName<T>(string path) => path + "_" + typeof(T).Name;
}
