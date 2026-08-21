using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// CompleteFishingSkillConfig 的 Unity 扩展方法类
/// </summary>
public static class CompleteFishingSkillConfigExtensions
{
    private static CompleteFishingSkillConfig _cachedConfig;
    private static bool _isLoading = false;
    private static readonly List<Action<CompleteFishingSkillConfig>> _pendingCallbacks = new List<Action<CompleteFishingSkillConfig>>();

    /// <summary>
    /// 根据ID获取组件配置（使用缓存）
    /// </summary>
    public static FishingComponentConfig GetComponentById(this CompleteFishingSkillConfig config, int id)
    {
        if (config?.items == null) return null;
        return config.items.Find(c => c.id == id);
    }

    /// <summary>
    /// 根据类别获取组件配置列表
    /// </summary>
    public static List<FishingComponentConfig> GetComponentsByCategory(this CompleteFishingSkillConfig config, FishingComponentCategory category)
    {
        if (config?.items == null) return new List<FishingComponentConfig>();
        return config.items.FindAll(c => c.category == category);
    }

    /// <summary>
    /// 根据名称获取组件配置
    /// </summary>
    public static FishingComponentConfig GetComponentByName(this CompleteFishingSkillConfig config, string name)
    {
        if (config?.items == null) return null;
        return config.items.Find(c => c.name == name);
    }

    /// <summary>
    /// 获取所有组件的图标路径字典
    /// </summary>
    public static Dictionary<int, string> GetAllIconPaths(this CompleteFishingSkillConfig config)
    {
        var iconPaths = new Dictionary<int, string>();
        if (config?.items == null) return iconPaths;

        foreach (var item in config.items)
        {
            string iconPath;
            if (!string.IsNullOrEmpty(item.iconPath))
            {
                iconPath = item.iconPath;
            }
            else
            {
                iconPath = GenerateIconPath(item.id);
            }
            iconPaths[item.id] = iconPath;
        }
        return iconPaths;
    }

    private static string GenerateIconPath(int id)
    {
        if (id >= 3001 && id <= 3099)
            return $"UI/Icon/Equipment/Rod/{id}";
        if (id >= 3101 && id <= 3199)
            return $"UI/Icon/Equipment/Line/{id}";
        if (id >= 3201 && id <= 3299)
            return $"UI/Icon/Equipment/Hook/{id}";
        if (id >= 3301 && id <= 3399)
            return $"UI/Icon/Equipment/Skill/{id}";
        return $"UI/Icon/Equipment/Unknown/{id}";
    }

    /// <summary>
    /// 从 Addressables 异步加载配置（带缓存）
    /// </summary>
    public static async Task<CompleteFishingSkillConfig> LoadFromAddressablesAsync(string path = "JsonData/Ability/fishing_components")
    {
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        if (_isLoading)
        {
            var tcs = new TaskCompletionSource<CompleteFishingSkillConfig>();
            _pendingCallbacks.Add(config => tcs.SetResult(config));
            return await tcs.Task;
        }

        _isLoading = true;
        try
        {
            var (textAsset, handle) = await AssetManager.LoadFromAddressablesAsync<TextAsset>(path);
            if (textAsset == null)
            {
                Z_Logger.LogError($"[CompleteFishingSkillConfig] 加载失败: {path}");
                return null;
            }

            _cachedConfig = JsonUtility.FromJson<CompleteFishingSkillConfig>(textAsset.text);
            AssetManager.ReleaseAddressable(handle);

            if (_cachedConfig == null)
            {
                Z_Logger.LogError($"[CompleteFishingSkillConfig] 解析失败: {path}");
                return null;
            }

            Z_Logger.Log($"[CompleteFishingSkillConfig] 加载成功，路径: {path}，共 {_cachedConfig.items?.Count ?? 0} 个组件");

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
    /// 清除缓存
    /// </summary>
    public static void ClearCache()
    {
        _cachedConfig = null;
        _isLoading = false;
        _pendingCallbacks.Clear();
    }
}
