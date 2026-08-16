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
/// - Resources 方法：同步，从主包加载（保持原有行为）
/// - Addressables 方法：异步，从 CDN 加载（支持热更）
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
        return Resources.Load<T>(path);
    }

    public static T[] LoadAllFromResources<T>(string path) where T : UnityEngine.Object
    {
        return Resources.LoadAll<T>(path);
    }

    public static void LoadFromResourcesAsync<T>(string path, Action<T> onLoaded) where T : UnityEngine.Object
    {
        T result = Resources.Load<T>(path);
        onLoaded?.Invoke(result);
    }

    // ============================================================
    //  ☁️ Addressables 方法（异步，从 CDN 加载，支持热更）
    // ============================================================

    /// <summary>
    /// 根据路径所在的文件夹推断文件后缀
    /// </summary>
    private static string InferFileExtension(string path)
    {
        if (string.IsNullOrEmpty(path))
            return "";

        // 如果已经有后缀，返回空
        string[] knownExtensions = { ".json", ".txt", ".ttf", ".png", ".jpg", ".jpeg",
                                     ".asset", ".prefab", ".mat", ".fbx", ".mp3", ".wav",
                                     ".mp4", ".avi", ".unity", ".anim" };
        foreach (string ext in knownExtensions)
        {
            if (path.EndsWith(ext))
                return "";
        }

        // 根据文件夹路径推断后缀
        foreach (var kvp in FolderExtensionMap)
        {
            if (path.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return "";
    }

    /// <summary>
    /// 获取文件扩展名（如果有）
    /// </summary>
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

    /// <summary>
    /// 规范化 Addressables Key：
    /// 1. 确保以 Assets/Addressables/ 开头
    /// 2. 确保有正确的扩展名
    /// </summary>
    private static string NormalizeAddressableKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        string result = key;

        // 1. 移除开头的 Assets/ 或 Assets/Addressables/（如果有），统一重新添加
        if (result.StartsWith("Assets/Addressables/"))
        {
            result = result.Substring("Assets/Addressables/".Length);
        }
        else if (result.StartsWith("Assets/"))
        {
            result = result.Substring("Assets/".Length);
        }

        // 移除开头的斜杠
        if (result.StartsWith("/"))
        {
            result = result.Substring(1);
        }

        // 2. 获取当前扩展名和推断扩展名
        string currentExt = GetFileExtension(result);
        string inferredExt = InferFileExtension(result);

        // 3. 如果没有扩展名，添加推断的扩展名
        if (string.IsNullOrEmpty(currentExt) && !string.IsNullOrEmpty(inferredExt))
        {
            result = result + inferredExt;
            Debug.Log($"[AssetManager] 自动添加扩展名: {key} -> {result}");
        }

        // 4. 添加 Assets/Addressables/ 前缀
        string finalKey = "Assets/Addressables/" + result;
        Debug.Log($"[AssetManager] 规范化 Key: {key} -> {finalKey}");

        return finalKey;
    }

    /// <summary>
    /// 从 Addressables 异步加载（回调方式）
    /// </summary>
    public static void LoadFromAddressables<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError("[AssetManager] Addressables key 为空");
            onLoaded?.Invoke(null, default);
            return;
        }

        string normalizedKey = NormalizeAddressableKey(key);

        Debug.Log($"[AssetManager] 加载: {normalizedKey}");

        var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"[AssetManager] ✅ 加载成功: {normalizedKey}");
                onLoaded?.Invoke(op.Result, op);
            }
            else
            {
                Debug.LogError($"[AssetManager] ❌ 加载失败: {normalizedKey}, 错误: {op.OperationException?.Message}");
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
            Debug.LogError("[AssetManager] Addressables key 为空");
            onLoaded?.Invoke(null, default);
            yield break;
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Debug.Log($"[AssetManager] 协程加载: {normalizedKey}");

        var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Debug.Log($"[AssetManager] 协程加载成功: {normalizedKey}");
            onLoaded?.Invoke(handle.Result, handle);
        }
        else
        {
            Debug.LogError($"[AssetManager] 协程加载失败: {normalizedKey}, 错误: {handle.OperationException?.Message}");
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
            Debug.LogError("[AssetManager] Addressables key 为空");
            return (null, default);
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Debug.Log($"[AssetManager] Async 加载: {normalizedKey}");

        try
        {
            var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"[AssetManager] Async 加载成功: {normalizedKey}");
                return (handle.Result, handle);
            }
            else
            {
                Debug.LogError($"[AssetManager] Async 加载失败: {normalizedKey}, 错误: {handle.OperationException?.Message}");
                return (null, default);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AssetManager] Async 加载异常: {normalizedKey}, {ex.Message}");
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
            Debug.LogError("[AssetManager] Addressables key 为空");
            onInstantiated?.Invoke(null, default);
            return;
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Debug.Log($"[AssetManager] 实例化: {normalizedKey}");

        var handle = Addressables.InstantiateAsync(normalizedKey);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"[AssetManager] 实例化成功: {normalizedKey}");
                onInstantiated?.Invoke(op.Result as T, op);
            }
            else
            {
                Debug.LogError($"[AssetManager] 实例化失败: {normalizedKey}, 错误: {op.OperationException?.Message}");
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
        }
    }

    public static void ReleaseAddressable(AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }
    }

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
            return exists;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AssetManager] 检查资源失败: {normalizedKey}, {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取所有 Addressables 资源 Key（用于调试）
    /// </summary>
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
                    Debug.Log($"Addressables Key: {keyStr}");
                }
            }
        }
        Debug.Log($"共找到 {allKeys.Count} 个 Addressables Key");
    }
#else
    public static bool AddressableExists(string key)
    {
        Debug.LogWarning("[AssetManager] AddressableExists 仅在编辑器下可用");
        return false;
    }
#endif
}
