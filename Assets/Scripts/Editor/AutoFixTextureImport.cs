using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 自动将导入的图片设置为Sprite
/// </summary>
public class AutoSetTextureToSprite : AssetPostprocessor
{
    // 在纹理导入前触发
    void OnPreprocessTexture()
    {
        // 只处理图片文件
        string ext = Path.GetExtension(assetPath).ToLower();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".psd")
            return;

        // 跳过系统文件夹
        if (assetPath.Contains("unity_builtin") || assetPath.Contains("Library/") ||
            assetPath.Contains("Gizmos/") || assetPath.Contains("Backup_OriginalTextures"))
            return;

        // 获取纹理导入器
        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null) return;

        // 检查是否已经是Sprite类型
        if (importer.textureType == TextureImporterType.Sprite)
            return;

        // 设置为Sprite
        importer.textureType = TextureImporterType.Sprite;

        // 可选：设置一些常用的Sprite导入选项
        importer.spriteImportMode = SpriteImportMode.Single; // 单张Sprite
        importer.mipmapEnabled = false; // 禁用Mipmap（UI图片通常不需要）
        importer.filterMode = FilterMode.Bilinear; // 双线性过滤

        // 根据图片尺寸自动调整压缩格式
        int width, height;
        if (GetImageSizeFromFile(assetPath, out width, out height))
        {
            // 如果图片尺寸大于1024，使用压缩格式
            if (width > 1024 || height > 1024)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
            }
            else
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }

        // 重新导入
        importer.SaveAndReimport();

        Z_Logger.Log($"✅ [自动设置Sprite] {Path.GetFileName(assetPath)} 已设置为Sprite");
    }

    /// <summary>
    /// 从文件读取图片尺寸
    /// </summary>
    bool GetImageSizeFromFile(string assetPath, out int width, out int height)
    {
        width = 0;
        height = 0;

        string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!File.Exists(fullPath)) return false;

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);

            // PNG
            if (bytes.Length > 24 && bytes[0] == 0x89 && bytes[1] == 0x50 &&
                bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                for (int i = 0; i < bytes.Length - 8; i++)
                {
                    if (bytes[i] == 0x49 && bytes[i + 1] == 0x48 &&
                        bytes[i + 2] == 0x44 && bytes[i + 3] == 0x52)
                    {
                        width = (bytes[i + 4] << 24) | (bytes[i + 5] << 16) |
                                (bytes[i + 6] << 8) | bytes[i + 7];
                        height = (bytes[i + 8] << 24) | (bytes[i + 9] << 16) |
                                 (bytes[i + 10] << 8) | bytes[i + 11];
                        return true;
                    }
                }
                return false;
            }

            // JPEG
            if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                int index = 2;
                while (index < bytes.Length - 1)
                {
                    if (bytes[index] == 0xFF)
                    {
                        int marker = bytes[index + 1];
                        if (marker >= 0xC0 && marker <= 0xC3)
                        {
                            height = (bytes[index + 5] << 8) | bytes[index + 6];
                            width = (bytes[index + 7] << 8) | bytes[index + 8];
                            return true;
                        }
                        index += 2;
                        if (index < bytes.Length)
                        {
                            int blockLength = (bytes[index] << 8) | bytes[index + 1];
                            index += blockLength;
                        }
                    }
                    else
                    {
                        index++;
                    }
                }
                return false;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
