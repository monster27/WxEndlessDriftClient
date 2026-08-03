using UnityEngine;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.U2D;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class AutoAtlasTool
{
    // 图集统一存放的根目录
    private const string AtlasRootFolder = "Assets/Atlases";

    // 支持的图片格式（不区分大小写）
    private static readonly string[] SupportedImageExtensions = new string[]
    {
        ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".gif", ".tif", ".tiff"
    };

    [MenuItem("Tools/自动打包图集/扫描所有图片并重建图集")]
    static void RebuildAllAtlases()
    {
        // ========== 第一步：完全清空 Atlases 文件夹 ==========
        if (AssetDatabase.IsValidFolder(AtlasRootFolder))
        {
            if (!EditorUtility.DisplayDialog("确认清空",
                $"即将删除 '{AtlasRootFolder}' 文件夹下的所有内容，然后重新生成图集。\n\n确定要继续吗？",
                "确定", "取消"))
            {
                Debug.Log("AutoAtlasTool: 操作已取消。");
                return;
            }

            Debug.Log($"AutoAtlasTool: 正在清空 '{AtlasRootFolder}' ...");
            FileUtil.DeleteFileOrDirectory(AtlasRootFolder);
            FileUtil.DeleteFileOrDirectory(AtlasRootFolder + ".meta");
            AssetDatabase.Refresh();
            Debug.Log($"AutoAtlasTool: '{AtlasRootFolder}' 已清空。");
        }

        // 重新创建 Atlases 根目录
        if (!AssetDatabase.IsValidFolder(AtlasRootFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Atlases");
        }

        // ========== 第二步：扫描所有图片文件 ==========
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        List<string> imagePaths = new List<string>();

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (SupportedImageExtensions.Contains(ext))
            {
                if (!path.StartsWith(AtlasRootFolder + "/") && path != AtlasRootFolder)
                {
                    imagePaths.Add(path);
                }
            }
        }

        if (imagePaths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "项目中没有找到支持的图片文件。\n\n支持的格式: " + string.Join(", ", SupportedImageExtensions), "确定");
            return;
        }

        Debug.Log($"AutoAtlasTool: 共找到 {imagePaths.Count} 个图片文件。");

        // ========== 第三步：按文件夹分组 ==========
        var folders = new Dictionary<string, List<string>>();
        foreach (string imagePath in imagePaths)
        {
            string folder = Path.GetDirectoryName(imagePath);
            if (!folders.ContainsKey(folder))
            {
                folders[folder] = new List<string>();
            }
            folders[folder].Add(imagePath);
        }

        Debug.Log($"AutoAtlasTool: 图片分布在 {folders.Count} 个文件夹中。");

        // ========== 第四步：设置 Sprite Packer 模式 ==========
        var originalPackerMode = EditorSettings.spritePackerMode;
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;
        Debug.Log($"AutoAtlasTool: Sprite Packer Mode 设置为 'Sprite Atlas V2 - Enabled for Builds'。");

        try
        {
            int createdCount = 0;
            int skippedCount = 0;
            int totalSpriteCount = 0;

            foreach (var kvp in folders)
            {
                string sourceFolder = kvp.Key;
                List<string> imageFiles = kvp.Value;

                // 检查这些图片是否是有效的 Sprite
                List<Texture2D> validTextures = new List<Texture2D>();
                List<string> invalidTexturePaths = new List<string>();

                foreach (string imagePath in imageFiles)
                {
                    TextureImporter importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
                    if (importer != null && importer.textureType == TextureImporterType.Sprite)
                    {
                        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                        if (tex != null)
                        {
                            validTextures.Add(tex);
                        }
                    }
                    else
                    {
                        invalidTexturePaths.Add(imagePath);
                    }
                }

                if (invalidTexturePaths.Count > 0)
                {
                    Debug.Log($"AutoAtlasTool: 文件夹 '{sourceFolder}' 中有 {invalidTexturePaths.Count} 张图片不是 Sprite 类型，已跳过。");
                    foreach (string path in invalidTexturePaths)
                    {
                        Debug.Log($"  - 跳过: {path}");
                    }
                }

                if (validTextures.Count == 0)
                {
                    Debug.Log($"AutoAtlasTool: 跳过 '{sourceFolder}' - 没有有效的 Sprite 图片。");
                    skippedCount++;
                    continue;
                }

                // ===== 修复点：使用更健壮的路径处理方式 =====
                // 使用 Path.GetRelativePath 或手动处理
                // 目标：将 "Assets/Resources/UI/Icon" 转换为 "Resources/UI/Icon"
                string relativePath = sourceFolder;

                // 去掉开头的 "Assets/" 前缀
                if (relativePath.StartsWith("Assets/"))
                {
                    // 从索引 7 开始截取，去掉 "Assets/"（7个字符）
                    relativePath = relativePath.Substring(7);
                }
                // 如果还以 "/" 开头，去掉
                if (relativePath.StartsWith("/"))
                {
                    relativePath = relativePath.Substring(1);
                }
                // 如果还以 "\" 开头，去掉
                if (relativePath.StartsWith("\\"))
                {
                    relativePath = relativePath.Substring(1);
                }

                // 构建目标路径：Assets/Atlases/ + relativePath + /文件夹名.spriteatlas
                string atlasFolder = Path.Combine(AtlasRootFolder, relativePath).Replace('\\', '/');
                string atlasFileName = Path.GetFileName(sourceFolder) + ".spriteatlas";
                string atlasPath = Path.Combine(atlasFolder, atlasFileName).Replace('\\', '/');

                // 确保目标文件夹存在
                if (!AssetDatabase.IsValidFolder(atlasFolder))
                {
                    CreateNestedFolder(atlasFolder);
                }

                // 创建图集
                SpriteAtlas atlas = new SpriteAtlas();
                atlas.Add(validTextures.ToArray());

                // 保存图集
                AssetDatabase.CreateAsset(atlas, atlasPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"AutoAtlasTool: 创建图集 '{atlasPath}'，包含 {validTextures.Count} 张 Sprite。");
                createdCount++;
                totalSpriteCount += validTextures.Count;
            }

            // ========== 第五步：统一打包所有图集 ==========
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
            Debug.Log("AutoAtlasTool: 所有图集打包完成。");

            EditorUtility.DisplayDialog("完成",
                $"图集重建完成！\n\n创建图集: {createdCount} 个\n跳过（无有效Sprite）: {skippedCount} 个\n总图片数: {imagePaths.Count} 张\n成功打包的Sprite数: {totalSpriteCount} 张",
                "确定");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"AutoAtlasTool: 处理过程中发生错误: {e.Message}");
            Debug.LogError($"AutoAtlasTool: 堆栈跟踪:\n{e.StackTrace}");
            EditorUtility.DisplayDialog("错误", $"处理过程中发生错误:\n{e.Message}\n\n请查看控制台日志。", "确定");
        }
        finally
        {
            EditorSettings.spritePackerMode = originalPackerMode;
            Debug.Log($"AutoAtlasTool: Sprite Packer Mode 已恢复为 '{originalPackerMode}'。");
        }
    }

    /// <summary>
    /// 递归创建多层文件夹
    /// </summary>
    static void CreateNestedFolder(string folderPath)
    {
        string normalizedPath = folderPath.Replace('\\', '/');
        string[] subFolders = normalizedPath.Split('/');
        string currentPath = "";
        for (int i = 0; i < subFolders.Length; i++)
        {
            string folderName = subFolders[i];
            if (string.IsNullOrEmpty(folderName)) continue;

            if (i == 0)
            {
                currentPath = folderName;
                continue;
            }
            string newPath = Path.Combine(currentPath, folderName).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(newPath))
            {
                AssetDatabase.CreateFolder(currentPath, folderName);
            }
            currentPath = newPath;
        }
    }

    // ========== 额外功能：单独的清空菜单 ==========
    [MenuItem("Tools/自动打包图集/清空Atlases文件夹")]
    static void ClearAtlasesFolder()
    {
        if (!AssetDatabase.IsValidFolder(AtlasRootFolder))
        {
            EditorUtility.DisplayDialog("提示", $"'{AtlasRootFolder}' 文件夹不存在。", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog("确认清空",
            $"确定要删除 '{AtlasRootFolder}' 文件夹下的所有内容吗？\n\n此操作不可恢复！",
            "确定删除", "取消"))
        {
            return;
        }

        FileUtil.DeleteFileOrDirectory(AtlasRootFolder);
        FileUtil.DeleteFileOrDirectory(AtlasRootFolder + ".meta");
        AssetDatabase.Refresh();
        Debug.Log($"AutoAtlasTool: '{AtlasRootFolder}' 已清空。");
        EditorUtility.DisplayDialog("完成", $"'{AtlasRootFolder}' 已清空。", "确定");
    }
}
