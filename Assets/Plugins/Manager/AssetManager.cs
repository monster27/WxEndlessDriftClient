// ==================== AssetManager.cs ====================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 统一资源管理器
/// - Resources 方法：同步，从主包加载（保持原有行为）
/// - Addressables 方法：异步，从 CDN 加载（支持热更）
/// 两者独立，互不影响，可以混用
/// </summary>
public class AssetManager : MonoBehaviour
{
    private static AssetManager _instance;
    public static AssetManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AssetManager");
                _instance = go.AddComponent<AssetManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // ============================================================
    //  📦 Resources 方法（同步，从主包加载）
    //  替换规则：Resources.Load<T>("path") → AssetManager.LoadFromResources<T>("path")
    // ============================================================

    /// <summary>
    /// 从 Resources 同步加载单个资源
    /// </summary>
    public static T LoadFromResources<T>(string path) where T : UnityEngine.Object
    {
        return Resources.Load<T>(path);
    }

    /// <summary>
    /// 从 Resources 同步加载所有资源
    /// </summary>
    public static T[] LoadAllFromResources<T>(string path) where T : UnityEngine.Object
    {
        return Resources.LoadAll<T>(path);
    }

    /// <summary>
    /// 从 Resources 异步加载（实际还是同步，只是包装成异步接口便于统一）
    /// </summary>
    public static void LoadFromResourcesAsync<T>(string path, Action<T> onLoaded) where T : UnityEngine.Object
    {
        T result = Resources.Load<T>(path);
        onLoaded?.Invoke(result);
    }

    // ============================================================
    //  ☁️ Addressables 方法（异步，从 CDN 加载，支持热更）
    //  用法：AssetManager.LoadFromAddressables<Sprite>("key", (sprite, handle) => { ... });
    // ============================================================

    /// <summary>
    /// 从 Addressables 异步加载（回调方式，返回 handle 用于释放）
    /// </summary>
    public static void LoadFromAddressables<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[AssetManager] Addressables key 为空");
            onLoaded?.Invoke(null, default);
            return;
        }

        var handle = Addressables.LoadAssetAsync<T>(key);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                onLoaded?.Invoke(op.Result, op);
            }
            else
            {
                Debug.LogError($"[AssetManager] Addressables 加载失败: {key}, 错误: {op.OperationException?.Message}");
                onLoaded?.Invoke(null, op);
            }
        };
    }

    /// <summary>
    /// 从 Addressables 异步加载（协程方式，返回 handle 用于释放）
    /// </summary>
    public static IEnumerator LoadFromAddressablesCoroutine<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[AssetManager] Addressables key 为空");
            onLoaded?.Invoke(null, default);
            yield break;
        }

        var handle = Addressables.LoadAssetAsync<T>(key);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            onLoaded?.Invoke(handle.Result, handle);
        }
        else
        {
            Debug.LogError($"[AssetManager] Addressables 加载失败: {key}");
            onLoaded?.Invoke(null, handle);
        }
    }

    /// <summary>
    /// 从 Addressables 异步加载（async/await 方式，返回 handle 用于释放）
    /// </summary>
    public static async System.Threading.Tasks.Task<(T Result, AsyncOperationHandle<T> Handle)> LoadFromAddressablesAsync<T>(string key) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[AssetManager] Addressables key 为空");
            return (null, default);
        }

        var handle = Addressables.LoadAssetAsync<T>(key);
        await handle.Task;
        return (handle.Result, handle);
    }

    /// <summary>
    /// 从 Addressables 加载并实例化预制体（回调方式，返回 handle 用于释放）
    /// </summary>
    public static void InstantiateFromAddressables<T>(string key, Action<T, AsyncOperationHandle> onInstantiated) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[AssetManager] Addressables key 为空");
            onInstantiated?.Invoke(null, default);
            return;
        }

        var handle = Addressables.InstantiateAsync(key);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                onInstantiated?.Invoke(op.Result as T, op);
            }
            else
            {
                Debug.LogError($"[AssetManager] Addressables 实例化失败: {key}");
                onInstantiated?.Invoke(null, op);
            }
        };
    }

    // ============================================================
    //  🧹 释放资源
    // ============================================================

    /// <summary>
    /// 释放 Addressables 资源（带类型）
    /// </summary>
    public static void ReleaseAddressable<T>(AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    /// <summary>
    /// 释放 Addressables 资源（通用版本）
    /// </summary>
    public static void ReleaseAddressable(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

    /// <summary>
    /// 释放 Addressables 实例化出来的 GameObject
    /// </summary>
    public static void ReleaseInstance(GameObject instance)
    {
        if (instance != null)
        {
            Addressables.ReleaseInstance(instance);
        }
    }

    // ============================================================
    //  🛠️ 工具方法
    // ============================================================

    /// <summary>
    /// 清空 Addressables 缓存
    /// </summary>
    /// <summary>
    /// 清空 Addressables 缓存（仅 Editor 和 Standalone 平台可用）
    /// </summary>
    public static void ClearAddressablesCache()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (UnityEngine.Caching.ClearCache())
        {
            Debug.Log("[AssetManager] Addressables 缓存已清空");
        }
        else
        {
            Debug.LogWarning("[AssetManager] 清空缓存失败，可能有正在使用的资源");
        }
#else
    Debug.LogWarning("[AssetManager] WebGL 平台不支持清空缓存");
#endif
    }

    /// <summary>
    /// 检查 Addressables 资源是否存在（仅编辑器）
    /// </summary>
#if UNITY_EDITOR
    public static bool AddressableExists(string key)
    {
        var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(key);
        handle.WaitForCompletion();
        bool exists = handle.Status == AsyncOperationStatus.Succeeded;
        if (exists)
        {
            Addressables.Release(handle);
        }
        return exists;
    }
#else
    public static bool AddressableExists(string key)
    {
        Debug.LogWarning("[AssetManager] AddressableExists 仅在编辑器下可用");
        return false;
    }
#endif
}
