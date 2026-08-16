using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 人物配置列表扩展类（Unity专用）
/// 提供Unity相关的资源加载功能
/// 数据类型定义请参见 SharedModels/CharacterConfig.cs
/// </summary>
public static class CharacterConfigListExtensions
{
    private static CharacterConfigList _cachedConfig;
    private static bool _isLoading = false;
    private static readonly List<Action<CharacterConfigList>> _pendingCallbacks = new List<Action<CharacterConfigList>>();

    /// <summary>
    /// 从 Addressables 异步加载人物配置（带缓存）
    /// </summary>
    public static async Task<CharacterConfigList> LoadFromAddressablesAsync(string path = "JsonData/BaseFramework/characters")
    {
        // 如果有缓存，直接返回
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        // 如果正在加载，等待
        if (_isLoading)
        {
            var tcs = new TaskCompletionSource<CharacterConfigList>();
            _pendingCallbacks.Add(config => tcs.SetResult(config));
            return await tcs.Task;
        }

        _isLoading = true;
        try
        {
            var (textAsset, handle) = await AssetManager.LoadFromAddressablesAsync<TextAsset>(path);
            if (textAsset == null)
            {
                Debug.LogError($"[CharacterConfigList] 加载失败: {path}");
                return null;
            }

            _cachedConfig = JsonUtility.FromJson<CharacterConfigList>(textAsset.text);
            AssetManager.ReleaseAddressable(handle);

            if (_cachedConfig == null)
            {
                Debug.LogError($"[CharacterConfigList] 解析失败: {path}");
                return null;
            }

            Debug.Log($"[CharacterConfigList] 加载成功，路径: {path}，共 {_cachedConfig.characters?.Count ?? 0} 个人物");

            // 触发所有等待的回调
            foreach (var cb in _pendingCallbacks)
            {
                cb?.Invoke(_cachedConfig);
            }
            _pendingCallbacks.Clear();

            return _cachedConfig;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// 加载人物配置（带错误处理）
    /// </summary>
    public static async Task<bool> TryLoadFromAddressablesAsync(string path = "JsonData/BaseFramework/characters")
    {
        try
        {
            var config = await LoadFromAddressablesAsync(path);
            return config != null;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CharacterConfigList] 加载异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清除缓存
    /// </summary>
    public static void ClearCache()
    {
        _cachedConfig = null;
        _isLoading = false;
        _pendingCallbacks.Clear();
    }

    // ===== 以下方法已废弃（Resources 已删除，仅用于兼容旧代码） =====

    /// <summary>
    /// 同步加载人物配置（仅编辑器工具使用）
    /// </summary>
    [Obsolete("Resources 已删除，请使用 LoadFromAddressablesAsync")]
    public static CharacterConfigList LoadFromResourcesSync(string path = "JsonData/BaseFramework/characters")
    {
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogError($"[CharacterConfigList] 加载失败: {path}");
            return null;
        }
        var config = JsonUtility.FromJson<CharacterConfigList>(textAsset.text);
        if (config == null)
        {
            Debug.LogError($"[CharacterConfigList] 解析失败: {path}");
            return null;
        }
        Debug.Log($"[CharacterConfigList] 加载成功，路径: {path}");
        return config;
    }
}
