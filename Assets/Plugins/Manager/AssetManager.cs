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

    // ============================================================
    //  📦 缓存系统
    // ============================================================

    /// <summary>
    /// 资源缓存字典：key -> 缓存条目
    /// </summary>
    private static readonly Dictionary<string, CacheEntry> _assetCache = new Dictionary<string, CacheEntry>();

    /// <summary>
    /// 缓存条目
    /// </summary>
    private class CacheEntry
    {
        public UnityEngine.Object Asset;
        public AsyncOperationHandle Handle;
        public int RefCount;
        public Type AssetType;
        public DateTime LastAccessTime;

        public CacheEntry(UnityEngine.Object asset, AsyncOperationHandle handle, Type type)
        {
            Asset = asset;
            Handle = handle;
            RefCount = 1;
            AssetType = type;
            LastAccessTime = DateTime.Now;
        }

        public void AddRef()
        {
            RefCount++;
            LastAccessTime = DateTime.Now;
        }

        public bool Release()
        {
            RefCount--;
            LastAccessTime = DateTime.Now;
            return RefCount <= 0;
        }
    }

    /// <summary>
    /// 获取缓存 Key（使用规范化后的路径）
    /// </summary>
    private static string GetCacheKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        // 使用规范化后的 key 作为缓存 key
        string normalized = NormalizeAddressableKeyInternal(key);
        return normalized;
    }

    /// <summary>
    /// 内部规范化方法（不输出日志，用于缓存 key 生成）
    /// </summary>
    private static string NormalizeAddressableKeyInternal(string key)
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
        }

        return "Assets/Addressables/" + result;
    }

    /// <summary>
    /// 从缓存中获取资源（如果存在）
    /// </summary>
    private static bool TryGetFromCache<T>(string key, out T asset) where T : UnityEngine.Object
    {
        string cacheKey = GetCacheKey(key);

        if (_assetCache.TryGetValue(cacheKey, out CacheEntry entry))
        {
            if (entry.Asset != null)
            {
                entry.AddRef();
                asset = entry.Asset as T;
                //Z_Logger.Log($"[AssetManager] 💾 缓存命中: {cacheKey} (类型: {typeof(T).Name}, 引用计数: {entry.RefCount})");
                return true;
            }
            else
            {
                // 资源已被销毁，移除缓存
                _assetCache.Remove(cacheKey);
                Z_Logger.LogWarning($"[AssetManager] ⚠️ 缓存中的资源已销毁，移除: {cacheKey}");
            }
        }

        asset = null;
        return false;
    }

    /// <summary>
    /// 添加到缓存
    /// </summary>
    private static void AddToCache<T>(string key, T asset, AsyncOperationHandle handle) where T : UnityEngine.Object
    {
        if (asset == null)
            return;

        string cacheKey = GetCacheKey(key);

        if (_assetCache.TryGetValue(cacheKey, out CacheEntry existingEntry))
        {
            // 如果已经存在，增加引用计数
            existingEntry.AddRef();
            Z_Logger.Log($"[AssetManager] 💾 缓存已存在，增加引用: {cacheKey} (引用计数: {existingEntry.RefCount})");
        }
        else
        {
            // 新条目
            var entry = new CacheEntry(asset, handle, typeof(T));
            _assetCache[cacheKey] = entry;
            Z_Logger.Log($"[AssetManager] 💾 添加到缓存: {cacheKey} (类型: {typeof(T).Name})");
        }
    }

    /// <summary>
    /// 清理未使用的缓存（引用计数为 0 且超过指定时间未访问）
    /// </summary>
    public static void CleanupCache(int maxAgeSeconds = 300)
    {
        DateTime now = DateTime.Now;
        List<string> keysToRemove = new List<string>();

        foreach (var kvp in _assetCache)
        {
            if (kvp.Value.RefCount <= 0)
            {
                TimeSpan age = now - kvp.Value.LastAccessTime;
                if (age.TotalSeconds > maxAgeSeconds)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (string key in keysToRemove)
        {
            ReleaseCachedAsset(key);
        }

        if (keysToRemove.Count > 0)
        {
            Z_Logger.Log($"[AssetManager] 🧹 清理了 {keysToRemove.Count} 个未使用的缓存资源");
        }
    }

    /// <summary>
    /// 释放缓存中的资源
    /// </summary>
    private static void ReleaseCachedAsset(string cacheKey)
    {
        if (_assetCache.TryGetValue(cacheKey, out CacheEntry entry))
        {
            _assetCache.Remove(cacheKey);

            if (entry.Handle.IsValid())
            {
                Addressables.Release(entry.Handle);
                Z_Logger.Log($"[AssetManager] 🔓 释放缓存资源: {cacheKey}");
            }

            entry.Asset = null;
        }
    }

    /// <summary>
    /// 手动释放某个缓存的引用（减少引用计数）
    /// </summary>
    public static void ReleaseCachedReference(string key)
    {
        string cacheKey = GetCacheKey(key);

        if (_assetCache.TryGetValue(cacheKey, out CacheEntry entry))
        {
            if (entry.Release())
            {
                // 引用计数为 0，立即释放
                ReleaseCachedAsset(cacheKey);
                Z_Logger.Log($"[AssetManager] 🔓 引用归零，释放资源: {cacheKey}");
            }
            else
            {
                Z_Logger.Log($"[AssetManager] 🔄 减少引用: {cacheKey} (剩余引用: {entry.RefCount})");
            }
        }
        else
        {
            Z_Logger.LogWarning($"[AssetManager] ⚠️ 尝试释放不存在的缓存引用: {cacheKey}");
        }
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public static void ClearAllCache()
    {
        List<string> keys = new List<string>(_assetCache.Keys);
        foreach (string key in keys)
        {
            ReleaseCachedAsset(key);
        }
        _assetCache.Clear();
        Z_Logger.Log($"[AssetManager] 🧹 已清空所有缓存");
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public static string GetCacheStats()
    {
        int totalCount = _assetCache.Count;
        int totalRefs = 0;
        foreach (var kvp in _assetCache)
        {
            totalRefs += kvp.Value.RefCount;
        }
        return $"[AssetManager] 📊 缓存统计: {totalCount} 个资源, {totalRefs} 个总引用";
    }

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
    /// 从 Addressables 异步加载（回调方式）- 带缓存
    /// </summary>
    public static void LoadFromAddressables<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError($"[AssetManager] ❌ Addressables key 为空");
            onLoaded?.Invoke(null, default);
            return;
        }

        // 1. 先检查缓存
        if (TryGetFromCache(key, out T cachedAsset))
        {
            onLoaded?.Invoke(cachedAsset, default);
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

                // 2. 添加到缓存
                AddToCache(key, op.Result, op);

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
    /// 从 Addressables 异步加载（协程方式）- 带缓存
    /// </summary>
    public static IEnumerator LoadFromAddressablesCoroutine<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[AssetManager] Addressables key 为空");
            onLoaded?.Invoke(null, default);
            yield break;
        }

        // 1. 先检查缓存
        if (TryGetFromCache(key, out T cachedAsset))
        {
            onLoaded?.Invoke(cachedAsset, default);
            yield break;
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Z_Logger.Log($"[AssetManager] 📥 协程开始加载: {normalizedKey}");

        var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            Z_Logger.Log($"[AssetManager] ✅✅✅ 协程加载成功: {normalizedKey}");

            // 2. 添加到缓存
            AddToCache(key, handle.Result, handle);

            onLoaded?.Invoke(handle.Result, handle);
        }
        else
        {
            Z_Logger.LogError($"[AssetManager] ❌❌❌ 协程加载失败: {normalizedKey}, 错误: {handle.OperationException?.Message}");
            onLoaded?.Invoke(null, default);
        }
    }

    /// <summary>
    /// 从 Addressables 异步加载（async/await 方式）- 带缓存
    /// </summary>
    public static async System.Threading.Tasks.Task<(T Result, AsyncOperationHandle<T> Handle)> LoadFromAddressablesAsync<T>(string key) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[AssetManager] Addressables key 为空");
            return (null, default);
        }

        // 1. 先检查缓存
        if (TryGetFromCache(key, out T cachedAsset))
        {
            return (cachedAsset, default);
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

                // 2. 添加到缓存
                AddToCache(key, handle.Result, handle);

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
    /// 从 Addressables 加载并实例化预制体（带缓存检测）
    /// </summary>
    public static void InstantiateFromAddressables<T>(string key, Action<T, AsyncOperationHandle> onInstantiated) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[AssetManager] Addressables key 为空");
            onInstantiated?.Invoke(null, default);
            return;
        }

        // 对于实例化，我们检查缓存中是否有资源，如果有则直接实例化
        if (TryGetFromCache(key, out T cachedAsset))
        {
            // 直接实例化缓存的资源
            if (cachedAsset is GameObject prefab)
            {
                GameObject instance = GameObject.Instantiate(prefab);
                Z_Logger.Log($"[AssetManager] 🎯 从缓存实例化成功: {key}");
                onInstantiated?.Invoke(instance as T, default);
                return;
            }
            else
            {
                Z_Logger.LogWarning($"[AssetManager] ⚠️ 缓存资源不是预制体: {key}");
                // 继续走正常加载流程
            }
        }

        string normalizedKey = NormalizeAddressableKey(key);
        Z_Logger.Log($"[AssetManager] 📥 开始实例化: {normalizedKey}");

        var handle = Addressables.InstantiateAsync(normalizedKey);
        handle.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Z_Logger.Log($"[AssetManager] ✅✅✅ 实例化成功: {normalizedKey}");

                // 注意：实例化本身不缓存实例，但我们可以缓存原始资源
                // 检查原始资源是否已经在缓存中
                if (!_assetCache.ContainsKey(GetCacheKey(key)))
                {
                    // 尝试获取原始资源并缓存（使用 LoadAssetAsync 来缓存原始资源）
                    Addressables.LoadAssetAsync<T>(normalizedKey).Completed += (loadOp) =>
                    {
                        if (loadOp.Status == AsyncOperationStatus.Succeeded)
                        {
                            AddToCache(key, loadOp.Result, loadOp);
                        }
                    };
                }

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

    // ============================================================
    //  📋 文件夹类型与扩展名的映射
    // ============================================================

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
}
