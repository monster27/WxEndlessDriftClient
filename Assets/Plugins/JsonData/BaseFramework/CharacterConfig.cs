using UnityEngine;
using System;
using System.Collections.Generic;

// 引入SharedModels命名空间以使用统一的数据类型
using SharedModels;

/// <summary>
/// 人物配置列表扩展类（Unity专用）
/// 提供Unity相关的资源加载功能
/// 数据类型定义请参见 SharedModels/CharacterConfig.cs
/// </summary>
public static class CharacterConfigListExtensions
{
    /// <summary>
    /// 从Unity Resources加载人物配置
    /// </summary>
    public static CharacterConfigList LoadFromResources(string path = "JsonData/BaseFramework/characters")
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

    /// <summary>
    /// 加载人物配置（带错误处理）
    /// </summary>
    public static bool TryLoadFromResources(out CharacterConfigList config, string path = "JsonData/BaseFramework/characters")
    {
        try
        {
            config = LoadFromResources(path);
            return config != null;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[CharacterConfigList] 加载异常: {ex.Message}");
            config = null;
            return false;
        }
    }
}

/// <summary>
/// 人物配置扩展类（Unity专用）
/// 提供Unity相关的功能扩展
/// </summary>
public static class CharacterConfigExtensions
{
    /// <summary>
    /// 加载人物图标Sprite
    /// </summary>
    public static Sprite LoadIconSprite(this CharacterConfig config)
    {
        if (string.IsNullOrEmpty(config.iconPath))
        {
            Debug.LogWarning($"[CharacterConfig] 人物ID={config.id} 图标路径为空");
            return null;
        }
        
        Sprite sprite = Resources.Load<Sprite>(config.iconPath);
        if (sprite == null)
        {
            Debug.LogWarning($"[CharacterConfig] 加载图标失败: {config.iconPath}");
        }
        return sprite;
    }

    /// <summary>
    /// 加载人物动画纹理
    /// </summary>
    public static Texture2D LoadIdleTexture(this CharacterConfig config)
    {
        return LoadTexture(config.idleTexturePath);
    }

    /// <summary>
    /// 加载收杆动画纹理
    /// </summary>
    public static Texture2D LoadReelTexture(this CharacterConfig config)
    {
        return LoadTexture(config.reelTexturePath);
    }

    /// <summary>
    /// 加载懒怠动画纹理
    /// </summary>
    public static Texture2D LoadLazyTexture(this CharacterConfig config)
    {
        return LoadTexture(config.lazyTexturePath);
    }

    private static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning($"[CharacterConfig] 加载纹理失败: {path}");
        }
        return texture;
    }
}
