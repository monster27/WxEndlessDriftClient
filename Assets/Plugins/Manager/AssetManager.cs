// ==================== AssetManager.cs ====================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 统一资源管理器
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

    // 文件夹类型与扩展名的映射
    private static readonly Dictionary<string, string> FolderExtensionMap = new Dictionary<string, string>
    {
        { "UI/", ".png" },
        { "GameScene/", ".png" },
        { "Icon/", ".png" },
        { "Texture/", ".png" },
        { "Sprite/", ".png" },
        { "JsonData/", ".json" },
        { "Audio/", ".mp3" },
        { "Sound/", ".mp3" },
        { "Music/", ".mp3" },
        { "Material/", ".mat" },
        { "Materials/", ".mat" },
        { "Prefabs/", ".prefab" },
        { "Prefab/", ".prefab" },
        { "TTF/", ".ttf" },
        { "Font/", ".ttf" },
        { "Fonts/", ".ttf" },
        { "Model/", ".fbx" },
        { "Models/", ".fbx" },
        { "Mesh/", ".fbx" },
        { "Animation/", ".anim" },
        { "Animations/", ".anim" },
        { "Anim/", ".anim" },
        { "Scene/", ".unity" },
        { "Scenes/", ".unity" },
    };

    // ============================================================
    //  📦 Resources 方法（同步，从主包加载）
    // ============================================================

    public static T LoadFromResources<T>(string path) where T : UnityEngine.Object
    {
        T result = Resources.Load<T>(path);
        if (result != null)
        {
            Z_Logger.Log($"[AssetManager] ✅ Resources 加载成功: {path} (类型: {typeof(T).Name})");
        }
        else
        {
            Z_Logger.LogWarning($"[AssetManager] ⚠️ Resources 加载失败: {path} (类型: {typeof(T).Name})");
        }
        return result;
    }

    public static T[] LoadAllFromResources<T>(string path) where T : UnityEngine.Object
    {
        T[] result = Resources.LoadAll<T>(path);
        if (result != null && result.Length > 0)
        {
            Z_Logger.Log($"[AssetManager] ✅ Resources 加载成功 {result.Length} 个资源: {path}");
        }
        else
        {
            Z_Logger.LogWarning($"[AssetManager] ⚠️ Resources 加载失败或为空: {path}");
        }
        return result;
    }

    public static void LoadFromResourcesAsync<T>(string path, Action<T> onLoaded) where T : UnityEngine.Object
    {
        T result = Resources.Load<T>(path);
        if (result != null)
        {
            Z_Logger.Log($"[AssetManager] ✅ Resources 异步加载成功: {path}");
        }
        else
        {
            Z_Logger.LogWarning($"[AssetManager] ⚠️ Resources 异步加载失败: {path}");
        }
        onLoaded?.Invoke(result);
    }

    // ============================================================
    //  ☁️ Addressables 方法（异步，从 CDN 加载，支持热更）
    // ============================================================

    private static string InferFileExtension(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        string[] knownExtensions = { ".json", ".txt", ".ttf", ".png", ".jpg", ".jpeg",
                                     ".asset", ".prefab", ".mat", ".fbx", ".mp3", ".wav",
                                     ".mp4", ".avi", ".unity", ".anim" };
        foreach (string ext in knownExtensions)
        {
            if (path.EndsWith(ext))
                return "";
        }

        foreach (var kvp in FolderExtensionMap)
        {
            if (path.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return "";
    }

    private static string GetFileExtension(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        string[] knownExtensions = { ".json", ".txt", ".ttf", ".png", ".jpg", ".jpeg",
                                     ".asset", ".prefab", ".mat", ".fbx", ".mp3", ".wav",
                                     ".mp4", ".avi", ".unity", ".anim" };
        foreach (string ext in knownExtensions)
        {
            if (path.EndsWith(ext))
                return ext;
        }
        return "";
    }

    private static string NormalizeAddressableKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        string result = key;

        if (result.StartsWith("Assets/Addressables/"))
        {
            result = result.Substring("Assets/Addressables/".Length);
        }
        else if (result.StartsWith("Assets/"))
        {
            result = result.Substring("Assets/".Length);
        }

        if (result.StartsWith("/"))
        {
            result = result.Substring(1);
        }

        string currentExt = GetFileExtension(result);
        string inferredExt = InferFileExtension(result);

        if (string.IsNullOrEmpty(currentExt) && !string.IsNullOrEmpty(inferredExt))
        {
            result = result + inferredExt;
            Z_Logger.Log($"[AssetManager] 🔄 自动添加扩展名: {key} -> {result}");
        }

        string finalKey = "Assets/Addressables/" + result;
        Z_Logger.Log($"[AssetManager] 🔄 规范化 Key: {key} -> {finalKey}");

        return finalKey;
    }

    /// <summary>
    /// 从 Addressables 异步加载（回调方式）- 带详细日志
    /// </summary>
    public static void LoadFromAddressables<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError($"[AssetManager] ❌ Addressables key 为空");
            onLoaded?.Invoke(null, default);
            return;
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Z_Logger.Log($"[AssetManager] 📥 开始加载 Addressables: {normalizedKey} (类型: {typeof(T).Name})");

        var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Z_Logger.Log($"[AssetManager] ✅✅✅ 加载成功: {normalizedKey} (类型: {typeof(T).Name})");
                onLoaded?.Invoke(op.Result, op);
            }
            else
            {
                Z_Logger.LogError($"[AssetManager] ❌❌❌ 加载失败: {normalizedKey}, 错误: {op.OperationException?.Message}");
                onLoaded?.Invoke(null, default);
            }
        };
    }

    /// <summary>
    /// 从 Addressables 异步加载（协程方式）
    /// </summary>
    public static IEnumerator LoadFromAddressablesCoroutine<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[AssetManager] Addressables key 为空");
            onLoaded?.Invoke(null, default);
            yield break;
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Z_Logger.Log($"[AssetManager] 📥 协程开始加载: {normalizedKey}");

        var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Z_Logger.Log($"[AssetManager] ✅✅✅ 协程加载成功: {normalizedKey}");
            onLoaded?.Invoke(handle.Result, handle);
        }
        else
        {
            Z_Logger.LogError($"[AssetManager] ❌❌❌ 协程加载失败: {normalizedKey}, 错误: {handle.OperationException?.Message}");
            onLoaded?.Invoke(null, default);
        }
    }

    /// <summary>
    /// 从 Addressables 异步加载（async/await 方式）
    /// </summary>
    public static async System.Threading.Tasks.Task<(T Result, AsyncOperationHandle<T> Handle)> LoadFromAddressablesAsync<T>(string key) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[AssetManager] Addressables key 为空");
            return (null, default);
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Z_Logger.Log($"[AssetManager] 📥 Async 开始加载: {normalizedKey} (类型: {typeof(T).Name})");

        try
        {
            var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Z_Logger.Log($"[AssetManager] ✅✅✅ Async 加载成功: {normalizedKey} (类型: {typeof(T).Name})");
                return (handle.Result, handle);
            }
            else
            {
                Z_Logger.LogError($"[AssetManager] ❌❌❌ Async 加载失败: {normalizedKey}, 错误: {handle.OperationException?.Message}");
                return (null, default);
            }
        }
        catch (Exception ex)
        {
            Z_Logger.LogError($"[AssetManager] ❌❌❌ Async 加载异常: {normalizedKey}, {ex.Message}");
            return (null, default);
        }
    }

    /// <summary>
    /// 从 Addressables 加载并实例化预制体
    /// </summary>
    public static void InstantiateFromAddressables<T>(string key, Action<T, AsyncOperationHandle> onInstantiated) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[AssetManager] Addressables key 为空");
            onInstantiated?.Invoke(null, default);
            return;
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Z_Logger.Log($"[AssetManager] 📥 开始实例化: {normalizedKey}");

        var handle = Addressables.InstantiateAsync(normalizedKey);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Z_Logger.Log($"[AssetManager] ✅✅✅ 实例化成功: {normalizedKey}");
                onInstantiated?.Invoke(op.Result as T, op);
            }
            else
            {
                Z_Logger.LogError($"[AssetManager] ❌❌❌ 实例化失败: {normalizedKey}, 错误: {op.OperationException?.Message}");
                onInstantiated?.Invoke(null, default);
            }
        };
    }

    // ============================================================
    //  🧹 释放资源
    // ============================================================

    public static void ReleaseAddressable<T>(AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
            Z_Logger.Log($"[AssetManager] 🔓 释放资源: {handle}");
        }
    }

    public static void ReleaseAddressable(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
            Z_Logger.Log($"[AssetManager] 🔓 释放资源: {handle}");
        }
    }

    public static void ReleaseInstance(GameObject instance)
    {
        if (instance != null)
        {
            Addressables.ReleaseInstance(instance);
            Z_Logger.Log($"[AssetManager] 🔓 释放实例: {instance.name}");
        }
    }

    // ============================================================
    //  🛠️ 工具方法
    // ============================================================

    public static void ClearAddressablesCache()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (UnityEngine.Caching.ClearCache())
        {
            Z_Logger.Log("[AssetManager] 🧹 Addressables 缓存已清空");
        }
        else
        {
            Z_Logger.LogWarning("[AssetManager] ⚠️ 清空缓存失败，可能有正在使用的资源");
        }
#else
        Z_Logger.LogWarning("[AssetManager] ⚠️ WebGL 平台不支持清空缓存");
#endif
    }

#if UNITY_EDITOR
    public static bool AddressableExists(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        string normalizedKey = NormalizeAddressableKey(key);
        try
        {
            var handle = Addressables.LoadAssetAsync<UnityEngine.Object>(normalizedKey);
            handle.WaitForCompletion();
            bool exists = handle.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(handle);
            Z_Logger.Log($"[AssetManager] 🔍 检查资源存在: {normalizedKey} -> {exists}");
            return exists;
        }
        catch (Exception ex)
        {
            Z_Logger.LogWarning($"[AssetManager] ⚠️ 检查资源失败: {normalizedKey}, {ex.Message}");
            return false;
        }
    }

    public static void LogAllAddressableKeys()
    {
        var allKeys = new List<string>();
        var locators = Addressables.ResourceLocators;
        foreach (var locator in locators)
        {
            var keys = new List<object>();
            locator.Keys.ToList().ForEach(k => keys.Add(k));
            foreach (var key in keys)
            {
                string keyStr = key.ToString();
                if (!allKeys.Contains(keyStr))
                {
                    allKeys.Add(keyStr);
                    Z_Logger.Log($"Addressables Key: {keyStr}");
                }
            }
        }
        Z_Logger.Log($"共找到 {allKeys.Count} 个 Addressables Key");
    }
#else
    public static bool AddressableExists(string key)
    {
        Z_Logger.LogWarning("[AssetManager] AddressableExists 仅在编辑器下可用");
        return false;
    }
#endif
}
