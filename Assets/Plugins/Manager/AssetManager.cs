// ==================== AssetManager.cs ====================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 资源分类（用于缓存管理）
/// </summary>
public enum AssetCategory
{
    Other = 0,
    UI = 1,
    Audio = 2,
    Config = 3,
}

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

    private static readonly Dictionary<string, CacheEntry> _assetCache = new Dictionary<string, CacheEntry>();

    // ✅ 加载去重：key -> 等待回调列表
    private static readonly Dictionary<string, List<Action<UnityEngine.Object, AsyncOperationHandle>>> _loadingCallbacks
        = new Dictionary<string, List<Action<UnityEngine.Object, AsyncOperationHandle>>>();

    // ✅ 正在加载的 key
    private static readonly HashSet<string> _loadingKeys = new HashSet<string>();

    private class CacheEntry
    {
        public UnityEngine.Object Asset;
        public AsyncOperationHandle Handle;
        public int RefCount;
        public Type AssetType;
        public AssetCategory Category;
        public DateTime LastAccessTime;

        public CacheEntry(UnityEngine.Object asset, AsyncOperationHandle handle, Type type, AssetCategory category)
        {
            Asset = asset;
            Handle = handle;
            RefCount = 1;
            AssetType = type;
            Category = category;
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

    private static string GetCacheKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;
        return NormalizeAddressableKeyInternal(key);
    }

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

    private static bool TryGetFromCache<T>(string key, out T asset) where T : UnityEngine.Object
    {
        string cacheKey = GetCacheKey(key);

        if (_assetCache.TryGetValue(cacheKey, out CacheEntry entry))
        {
            if (entry.Asset != null)
            {
                entry.AddRef();
                asset = entry.Asset as T;
                return true;
            }
            else
            {
                _assetCache.Remove(cacheKey);
                Z_Logger.LogWarning($"[AssetManager] ⚠️ 缓存中的资源已销毁，移除: {cacheKey}");
            }
        }

        asset = null;
        return false;
    }

    private static void AddToCache<T>(string key, T asset, AsyncOperationHandle handle, AssetCategory category) where T : UnityEngine.Object
    {
        if (asset == null)
            return;

        string cacheKey = GetCacheKey(key);

        if (_assetCache.TryGetValue(cacheKey, out CacheEntry existingEntry))
        {
            existingEntry.AddRef();
            Z_Logger.Log($"[AssetManager] 💾 缓存已存在，增加引用: {cacheKey} (引用计数: {existingEntry.RefCount})");
        }
        else
        {
            var entry = new CacheEntry(asset, handle, typeof(T), category);
            _assetCache[cacheKey] = entry;
            Z_Logger.Log($"[AssetManager] 💾 添加到缓存: {cacheKey} (类型: {typeof(T).Name}, 分类: {category})");
        }
    }

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
    /// ✅ 内存压力响应：按分类清理缓存
    /// </summary>
    public static void CleanupByMemoryPressure(long memoryThresholdBytes)
    {
        long currentMem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        if (currentMem < memoryThresholdBytes) return;

        Z_Logger.LogWarning($"[AssetManager] ⚠️ 内存超阈值 ({currentMem / 1024 / 1024}MB)，清理引用为 0 的缓存");

        List<string> toRemove = new List<string>();
        foreach (var kvp in _assetCache)
        {
            if (kvp.Value.RefCount <= 0)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (string key in toRemove)
        {
            ReleaseCachedAsset(key);
        }

        Z_Logger.Log($"[AssetManager] 🧹 内存压力清理：释放 {toRemove.Count} 个资源");
    }

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

    public static void ReleaseCachedReference(string key)
    {
        string cacheKey = GetCacheKey(key);

        if (_assetCache.TryGetValue(cacheKey, out CacheEntry entry))
        {
            if (entry.Release())
            {
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
    /// ✅ 批量释放：每个 key 引用计数 -1
    /// </summary>
    public static void ReleaseKeys(List<string> keys)
    {
        if (keys == null) return;
        foreach (string key in keys)
        {
            ReleaseCachedReference(key);
        }
    }

    public static void ClearAllCache()
    {
        List<string> keys = new List<string>(_assetCache.Keys);
        foreach (string key in keys)
        {
            ReleaseCachedAsset(key);
        }
        _assetCache.Clear();
        _loadingCallbacks.Clear();
        _loadingKeys.Clear();
        Z_Logger.Log($"[AssetManager] 🧹 已清空所有缓存");
    }

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
    //  📦 Resources 方法
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
    //  ☁️ Addressables 方法
    // ============================================================

    private static string InferFileExtension(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        string[] knownExtensions = { ".json", ".txt", ".ttf", ".png", ".jpg", ".jpeg",
                                     ".asset", ".prefab", ".mat", ".fbx", ".mp3", ".wav",
                                     ".mp4", ".avi", ".unity", ".anim" };
        foreach (string ext in knownExtensions)
        {
            if (path.EndsWith(ext)) return "";
        }

        foreach (var kvp in FolderExtensionMap)
        {
            if (path.Contains(kvp.Key)) return kvp.Value;
        }

        return "";
    }

    private static string GetFileExtension(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";

        string[] knownExtensions = { ".json", ".txt", ".ttf", ".png", ".jpg", ".jpeg",
                                     ".asset", ".prefab", ".mat", ".fbx", ".mp3", ".wav",
                                     ".mp4", ".avi", ".unity", ".anim" };
        foreach (string ext in knownExtensions)
        {
            if (path.EndsWith(ext)) return ext;
        }
        return "";
    }

    private static string NormalizeAddressableKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        string result = key;

        if (result.StartsWith("Assets/Addressables/"))
            result = result.Substring("Assets/Addressables/".Length);
        else if (result.StartsWith("Assets/"))
            result = result.Substring("Assets/".Length);

        if (result.StartsWith("/"))
            result = result.Substring(1);

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

    // ============================================================
    //  ✅ 统一的内部加载入口（带去重）
    // ============================================================

    private static void LoadInternal<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded, AssetCategory category) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError($"[AssetManager] ❌ Addressables key 为空");
            onLoaded?.Invoke(null, default);
            return;
        }

        // 1. 缓存命中
        if (TryGetFromCache(key, out T cachedAsset))
        {
            onLoaded?.Invoke(cachedAsset, default);
            return;
        }

        string cacheKey = GetCacheKey(key);

        // 2. 正在加载中，追加等待
        if (_loadingKeys.Contains(cacheKey))
        {
            if (!_loadingCallbacks.TryGetValue(cacheKey, out var list))
            {
                list = new List<Action<UnityEngine.Object, AsyncOperationHandle>>();
                _loadingCallbacks[cacheKey] = list;
            }

            list.Add((obj, h) =>
            {
                onLoaded?.Invoke(obj as T, h is AsyncOperationHandle<T> typed ? typed : default);
            });

            Z_Logger.Log($"[AssetManager] ⏳ 已有加载请求进行中，追加等待: {cacheKey}");
            return;
        }

        // 3. 发起新加载
        string normalizedKey = NormalizeAddressableKey(key);
        _loadingKeys.Add(cacheKey);

        Z_Logger.Log($"[AssetManager] 📥 开始加载: {normalizedKey} (类型: {typeof(T).Name}, 分类: {category})");

        var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
        handle.Completed += (op) =>
        {
            _loadingKeys.Remove(cacheKey);

            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Z_Logger.Log($"[AssetManager] ✅✅✅ 加载成功: {normalizedKey}");
                AddToCache(key, op.Result, op, category);

                onLoaded?.Invoke(op.Result, op);

                if (_loadingCallbacks.TryGetValue(cacheKey, out var list))
                {
                    foreach (var cb in list) cb?.Invoke(op.Result, op);
                    _loadingCallbacks.Remove(cacheKey);
                }
            }
            else
            {
                Z_Logger.LogError($"[AssetManager] ❌❌❌ 加载失败: {normalizedKey}, 错误: {op.OperationException?.Message}");
                onLoaded?.Invoke(null, default);

                if (_loadingCallbacks.TryGetValue(cacheKey, out var list))
                {
                    foreach (var cb in list) cb?.Invoke(null, default);
                    _loadingCallbacks.Remove(cacheKey);
                }
            }
        };
    }

    // ============================================================
    //  ✅ 三个对外加载接口
    // ============================================================

    public static void LoadFromAddressables<T>(string key, AssetCategory category, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        LoadInternal(key, onLoaded, category);
    }

    public static void LoadFromAddressables<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        LoadInternal(key, onLoaded, AssetCategory.Other);
    }

    public static IEnumerator LoadFromAddressablesCoroutine<T>(string key, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        bool done = false;
        T result = null;
        AsyncOperationHandle<T> resultHandle = default;

        LoadInternal<T>(key, (asset, handle) =>
        {
            result = asset;
            resultHandle = handle;
            done = true;
        }, AssetCategory.Other);

        while (!done) yield return null;

        onLoaded?.Invoke(result, resultHandle);
    }

    public static IEnumerator LoadFromAddressablesCoroutine<T>(string key, AssetCategory category, Action<T, AsyncOperationHandle<T>> onLoaded) where T : UnityEngine.Object
    {
        bool done = false;
        T result = null;
        AsyncOperationHandle<T> resultHandle = default;

        LoadInternal<T>(key, (asset, handle) =>
        {
            result = asset;
            resultHandle = handle;
            done = true;
        }, category);

        while (!done) yield return null;

        onLoaded?.Invoke(result, resultHandle);
    }

    public static async System.Threading.Tasks.Task<(T Result, AsyncOperationHandle<T> Handle)> LoadFromAddressablesAsync<T>(string key, AssetCategory category) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[AssetManager] Addressables key 为空");
            return (null, default);
        }

        if (TryGetFromCache(key, out T cachedAsset))
        {
            return (cachedAsset, default);
        }

        string cacheKey = GetCacheKey(key);

        if (_loadingKeys.Contains(cacheKey))
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<(T, AsyncOperationHandle<T>)>();

            if (!_loadingCallbacks.TryGetValue(cacheKey, out var list))
            {
                list = new List<Action<UnityEngine.Object, AsyncOperationHandle>>();
                _loadingCallbacks[cacheKey] = list;
            }

            list.Add((obj, h) =>
            {
                tcs.TrySetResult((obj as T, h is AsyncOperationHandle<T> typed ? typed : default));
            });

            return await tcs.Task;
        }

        string normalizedKey = NormalizeAddressableKey(key);
        _loadingKeys.Add(cacheKey);

        try
        {
            var handle = Addressables.LoadAssetAsync<T>(normalizedKey);
            await handle.Task;

            _loadingKeys.Remove(cacheKey);

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Z_Logger.Log($"[AssetManager] ✅✅✅ Async 加载成功: {normalizedKey}");
                AddToCache(key, handle.Result, handle, category);

                if (_loadingCallbacks.TryGetValue(cacheKey, out var list))
                {
                    foreach (var cb in list) cb?.Invoke(handle.Result, handle);
                    _loadingCallbacks.Remove(cacheKey);
                }

                return (handle.Result, handle);
            }
            else
            {
                Z_Logger.LogError($"[AssetManager] ❌❌❌ Async 加载失败: {normalizedKey}, {handle.OperationException?.Message}");

                if (_loadingCallbacks.TryGetValue(cacheKey, out var list))
                {
                    foreach (var cb in list) cb?.Invoke(null, default);
                    _loadingCallbacks.Remove(cacheKey);
                }

                return (null, default);
            }
        }
        catch (Exception ex)
        {
            _loadingKeys.Remove(cacheKey);
            Z_Logger.LogError($"[AssetManager] ❌❌❌ Async 加载异常: {normalizedKey}, {ex.Message}");
            return (null, default);
        }
    }

    public static System.Threading.Tasks.Task<(T Result, AsyncOperationHandle<T> Handle)> LoadFromAddressablesAsync<T>(string key) where T : UnityEngine.Object
    {
        return LoadFromAddressablesAsync<T>(key, AssetCategory.Other);
    }

    // ============================================================
    //  🧹 释放
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
        if (string.IsNullOrEmpty(key)) return false;

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
