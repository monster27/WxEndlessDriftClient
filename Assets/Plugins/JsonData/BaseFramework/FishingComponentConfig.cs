using UnityEngine;
using System;
using System.Collections.Generic;
using SharedModels;

/// <summary>
/// FishingComponentConfig 的 Unity 扩展方法类
/// 提供 Unity 相关的加载和资源管理功能
/// </summary>
public static class FishingComponentConfigExtensions
{
    /// <summary>
    /// 获取指定等级的数据
    /// </summary>
    public static FishingComponentLevelData GetLevelData(this FishingComponentConfig config, int level)
    {
        if (config.levelDataList == null) return null;
        return config.levelDataList.Find(data => data.level == level);
    }
    
    /// <summary>
    /// 获取指定等级中指定参数ID的值
    /// </summary>
    public static float GetParamValue(this FishingComponentConfig config, int level, int paramId)
    {
        var levelData = config.GetLevelData(level);
        if (levelData == null || levelData.paramsList == null) return 0f;
        
        var param = levelData.paramsList.Find(p => p.paramId == paramId);
        return param != null ? param.value : 0f;
    }
    
    /// <summary>
    /// 获取指定等级的第index个参数（从0开始）
    /// </summary>
    public static FishingComponentParam GetParamByIndex(this FishingComponentConfig config, int level, int index)
    {
        var levelData = config.GetLevelData(level);
        if (levelData == null || levelData.paramsList == null || index >= levelData.paramsList.Count) return null;
        return levelData.paramsList[index];
    }
    
    /// <summary>
    /// 获取指定等级的参数数量
    /// </summary>
    public static int GetParamCount(this FishingComponentConfig config, int level)
    {
        var levelData = config.GetLevelData(level);
        return levelData != null && levelData.paramsList != null ? levelData.paramsList.Count : 0;
    }
}

/// <summary>
/// CompleteFishingSkillConfig 的 Unity 扩展方法类
/// 提供 Unity 相关的加载和资源管理功能
/// </summary>
public static class CompleteFishingSkillConfigExtensions
{
    /// <summary>
    /// 根据ID获取组件配置
    /// </summary>
    public static FishingComponentConfig GetComponentById(this CompleteFishingSkillConfig config, int id)
    {
        if (config.items == null) return null;
        return config.items.Find(c => c.id == id);
    }
    
    /// <summary>
    /// 根据类别获取组件配置列表
    /// </summary>
    public static List<FishingComponentConfig> GetComponentsByCategory(this CompleteFishingSkillConfig config, FishingComponentCategory category)
    {
        if (config.items == null) return new List<FishingComponentConfig>();
        return config.items.FindAll(c => c.category == category);
    }
    
    /// <summary>
    /// 根据名称获取组件配置
    /// </summary>
    public static FishingComponentConfig GetComponentByName(this CompleteFishingSkillConfig config, string name)
    {
        if (config.items == null) return null;
        return config.items.Find(c => c.name == name);
    }

    /// <summary>
    /// 获取所有组件的图标路径字典
    /// 如果JSON中iconPath为空，则根据ID规则生成路径
    /// 规则：3001-3099=Rod, 3101-3199=Line, 3201-3299=Hook, 3301-3399=Skill
    /// </summary>
    public static Dictionary<int, string> GetAllIconPaths(this CompleteFishingSkillConfig config)
    {
        var iconPaths = new Dictionary<int, string>();
        if (config.items == null) return iconPaths;

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

    /// <summary>
    /// 根据ID生成图标路径
    /// </summary>
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
    /// 从Resources路径加载配置
    /// </summary>
    /// <param name="path">Resources路径（不含拓展名）</param>
    /// <returns>配置实例，失败返回null</returns>
    public static CompleteFishingSkillConfig LoadFromResources(string path)
    {
        TextAsset textAsset = Resources.Load<TextAsset>(path);
        if (textAsset == null)
        {
            Debug.LogError($"[CompleteFishingSkillConfig] 加载失败: {path}");
            return null;
        }

        var config = JsonUtility.FromJson<CompleteFishingSkillConfig>(textAsset.text);
        if (config == null)
        {
            Debug.LogError($"[CompleteFishingSkillConfig] 解析失败: {path}");
            return null;
        }

        Debug.Log($"[CompleteFishingSkillConfig] 加载成功，路径: {path}");
        return config;
    }
}
