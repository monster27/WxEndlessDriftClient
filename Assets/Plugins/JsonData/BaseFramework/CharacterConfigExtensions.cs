using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
/// <summary>
/// 人物配置扩展类（Unity专用）
/// 提供Unity相关的功能扩展
/// </summary>
public static class CharacterConfigExtensions
{
    /// <summary>
    /// 异步加载人物图标Sprite
    /// </summary>
    public static async Task<Sprite> LoadIconSpriteAsync(this CharacterConfig config)
    {
        if (string.IsNullOrEmpty(config.iconPath))
        {
            Z_Logger.LogWarning($"[CharacterConfig] 人物ID={config.id} 图标路径为空");
            return null;
        }

        var (sprite, handle) = await AssetManager.LoadFromAddressablesAsync<Sprite>(config.iconPath);
        if (sprite == null)
        {
            Z_Logger.LogWarning($"[CharacterConfig] 加载图标失败: {config.iconPath}");
        }
        return sprite;
    }

    /// <summary>
    /// 异步加载人物动画纹理
    /// </summary>
    public static async Task<Texture2D> LoadIdleTextureAsync(this CharacterConfig config)
    {
        return await LoadTextureAsync(config.idleTexturePath);
    }

    /// <summary>
    /// 异步加载收杆动画纹理
    /// </summary>
    public static async Task<Texture2D> LoadReelTextureAsync(this CharacterConfig config)
    {
        return await LoadTextureAsync(config.reelTexturePath);
    }

    /// <summary>
    /// 异步加载懒怠动画纹理
    /// </summary>
    public static async Task<Texture2D> LoadLazyTextureAsync(this CharacterConfig config)
    {
        return await LoadTextureAsync(config.lazyTexturePath);
    }

    private static async Task<Texture2D> LoadTextureAsync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var (texture, handle) = await AssetManager.LoadFromAddressablesAsync<Texture2D>(path);
        if (texture == null)
        {
            Z_Logger.LogWarning($"[CharacterConfig] 加载纹理失败: {path}");
        }
        return texture;
    }

    // ===== 同步版本（仅编辑器工具使用） =====
    public static Sprite LoadIconSpriteSync(this CharacterConfig config)
    {
        if (string.IsNullOrEmpty(config.iconPath))
        {
            Z_Logger.LogWarning($"[CharacterConfig] 人物ID={config.id} 图标路径为空");
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(config.iconPath);
        if (sprite == null)
        {
            Z_Logger.LogWarning($"[CharacterConfig] 加载图标失败: {config.iconPath}");
        }
        return sprite;
    }

    public static Texture2D LoadIdleTextureSync(this CharacterConfig config)
    {
        return LoadTextureSync(config.idleTexturePath);
    }

    public static Texture2D LoadReelTextureSync(this CharacterConfig config)
    {
        return LoadTextureSync(config.reelTexturePath);
    }

    public static Texture2D LoadLazyTextureSync(this CharacterConfig config)
    {
        return LoadTextureSync(config.lazyTexturePath);
    }

    private static Texture2D LoadTextureSync(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Z_Logger.LogWarning($"[CharacterConfig] 加载纹理失败: {path}");
        }
        return texture;
    }
}
