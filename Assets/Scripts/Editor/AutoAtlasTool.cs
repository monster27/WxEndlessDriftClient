using UnityEngine;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.U2D;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public class AutoAtlasTool
{
    // ✅ 图集统一存放在 Addressables/Atlases 目录下
    private const string AtlasRootFolder = "Assets/Addressables/Atlases";

    // 支持的图片格式（不区分大小写）
    private static readonly string[] SupportedImageExtensions = new string[]
    {
        ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".psd", ".gif", ".tif", ".tiff"
    };

    [MenuItem("Tools/资源工具/1.自动打包图集/扫描所有图片并重建图集")]
    static void RebuildAllAtlases()
    {
        // ========== 第一步：确保 Addressables 文件夹存在 ==========
        if (!AssetDatabase.IsValidFolder("Assets/Addressables"))
        {
            AssetDatabase.CreateFolder("Assets", "Addressables");
        }

        // ========== 第二步：完全清空 Addressables/Atlases 文件夹 ==========
        if (AssetDatabase.IsValidFolder(AtlasRootFolder))
        {
            if (!EditorUtility.DisplayDialog("确认清空",
                $"即将删除 '{AtlasRootFolder}' 文件夹下的所有内容，然后重新生成图集。\n\n确定要继续吗？",
                "确定", "取消"))
            {
                Z_Logger.Log("AutoAtlasTool: 操作已取消。");
                return;
            }

            Z_Logger.Log($"AutoAtlasTool: 正在清空 '{AtlasRootFolder}' ...");
            FileUtil.DeleteFileOrDirectory(AtlasRootFolder);
            FileUtil.DeleteFileOrDirectory(AtlasRootFolder + ".meta");
            AssetDatabase.Refresh();
            Z_Logger.Log($"AutoAtlasTool: '{AtlasRootFolder}' 已清空。");
        }

        // 重新创建 Addressables/Atlases 根目录
        if (!AssetDatabase.IsValidFolder(AtlasRootFolder))
        {
            string parentFolder = Path.GetDirectoryName(AtlasRootFolder).Replace('\\', '/');
            string newFolderName = Path.GetFileName(AtlasRootFolder);
            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }

        // ========== 第三步：扫描所有图片文件 ==========
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        List<string> imagePaths = new List<string>();

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (SupportedImageExtensions.Contains(ext))
            {
                // ✅ 排除 Addressables/Atlases 目录下的图片（避免循环）
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

        Z_Logger.Log($"AutoAtlasTool: 共找到 {imagePaths.Count} 个图片文件。");

        // ========== 第四步：按文件夹分组 ==========
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

        Z_Logger.Log($"AutoAtlasTool: 图片分布在 {folders.Count} 个文件夹中。");

        // ========== 第五步：设置 Sprite Packer 模式 ==========
        var originalPackerMode = EditorSettings.spritePackerMode;
        EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;
        Z_Logger.Log($"AutoAtlasTool: Sprite Packer Mode 设置为 'Sprite Atlas V2 - Enabled for Builds'。");

        // ✅ 获取 Addressable Settings
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Z_Logger.LogError("AutoAtlasTool: Addressable Settings 不存在！请先创建 Addressables 配置。");
            EditorUtility.DisplayDialog("错误", "Addressable Settings 不存在！\n请先通过 Window → Asset Management → Addressables → Settings 创建。", "确定");
            return;
        }

        // ✅ 确保有默认的 Addressables Group
        AddressableAssetGroup atlasGroup = settings.FindGroup("AtlasGroup");
        if (atlasGroup == null)
        {
            atlasGroup = settings.CreateGroup("AtlasGroup", false, false, true, null);
            Z_Logger.Log("AutoAtlasTool: 创建 AtlasGroup 用于存放图集");
        }

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
                    Z_Logger.Log($"AutoAtlasTool: 文件夹 '{sourceFolder}' 中有 {invalidTexturePaths.Count} 张图片不是 Sprite 类型，已跳过。");
                    foreach (string path in invalidTexturePaths)
                    {
                        Z_Logger.Log($"  - 跳过: {path}");
                    }
                }

                if (validTextures.Count == 0)
                {
                    Z_Logger.Log($"AutoAtlasTool: 跳过 '{sourceFolder}' - 没有有效的 Sprite 图片。");
                    skippedCount++;
                    continue;
                }

                // 构建目标路径（保持原有目录结构）
                string relativePath = sourceFolder;
                if (relativePath.StartsWith("Assets/"))
                {
                    relativePath = relativePath.Substring(7);
                }
                relativePath = relativePath.Replace('\\', '/').TrimStart('/');

                // ✅ 目标路径：Addressables/Atlases/{原路径}
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

                // 设置图集参数
                SpriteAtlasPackingSettings packingSettings = new SpriteAtlasPackingSettings()
                {
                    enableTightPacking = false,
                    enableRotation = false,
                    padding = 4
                };
                atlas.SetPackingSettings(packingSettings);

                SpriteAtlasTextureSettings textureSettings = new SpriteAtlasTextureSettings()
                {
                    readable = false,
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 1
                };
                atlas.SetTextureSettings(textureSettings);

                // 保存图集
                AssetDatabase.CreateAsset(atlas, atlasPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // ✅✅✅ 关键步骤：标记为 Addressable，放入 AtlasGroup
                MarkAssetAsAddressable(atlasPath, settings, atlasGroup);

                Z_Logger.Log($"AutoAtlasTool: 创建图集 '{atlasPath}'，包含 {validTextures.Count} 张 Sprite，并标记为 Addressable。");
                createdCount++;
                totalSpriteCount += validTextures.Count;
            }

            // ========== 第六步：统一打包所有图集 ==========
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
            Z_Logger.Log("AutoAtlasTool: 所有图集打包完成。");

            // ✅ 保存 Addressables 配置
            if (settings != null)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
                Z_Logger.Log($"AutoAtlasTool: Addressables 配置已保存，共标记 {createdCount} 个图集。");
            }

            EditorUtility.DisplayDialog("完成",
                $"图集重建完成！\n\n" +
                $"创建图集: {createdCount} 个\n" +
                $"跳过（无有效Sprite）: {skippedCount} 个\n" +
                $"总图片数: {imagePaths.Count} 张\n" +
                $"成功打包的Sprite数: {totalSpriteCount} 张\n" +
                $"\n✅ 图集存放在: {AtlasRootFolder}\n" +
                $"✅ 所有图集已自动标记为 Addressable！",
                "确定");
        }
        catch (System.Exception e)
        {
            Z_Logger.LogError($"AutoAtlasTool: 处理过程中发生错误: {e.Message}");
            Z_Logger.LogError($"AutoAtlasTool: 堆栈跟踪:\n{e.StackTrace}");
            EditorUtility.DisplayDialog("错误", $"处理过程中发生错误:\n{e.Message}\n\n请查看控制台日志。", "确定");
        }
        finally
        {
            EditorSettings.spritePackerMode = originalPackerMode;
            Z_Logger.Log($"AutoAtlasTool: Sprite Packer Mode 已恢复为 '{originalPackerMode}'。");
        }
    }

    /// <summary>
    /// ✅ 标记资源为 Addressable
    /// </summary>
    static void MarkAssetAsAddressable(string assetPath, AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        if (settings == null)
        {
            Z_Logger.LogError($"AutoAtlasTool: Addressable Settings 为 null，无法标记 {assetPath}");
            return;
        }

        if (group == null)
        {
            group = settings.DefaultGroup;
            if (group == null)
            {
                group = settings.CreateGroup("Default Local Group", false, false, true, null);
                Z_Logger.Log($"AutoAtlasTool: 创建默认 Addressables 组 'Default Local Group'");
            }
        }

        // 获取资源的 GUID
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrEmpty(guid))
        {
            Z_Logger.LogError($"AutoAtlasTool: 无法获取 {assetPath} 的 GUID");
            return;
        }

        // 检查是否已经标记
        AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
        if (existingEntry != null)
        {
            Z_Logger.Log($"AutoAtlasTool: {assetPath} 已经标记为 Addressable，跳过。");
            return;
        }

        // 创建新的 Addressable 条目
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
        if (entry != null)
        {
            // ✅ 设置 Addressable Key 为图集的路径（方便通过路径加载）
            entry.SetAddress(assetPath);
            Z_Logger.Log($"AutoAtlasTool: ✅ 标记图集为 Addressable: {assetPath} -> Key: {assetPath}");
        }
        else
        {
            Z_Logger.LogError($"AutoAtlasTool: ❌ 标记失败: {assetPath}");
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

    // ========== 额外功能：清空菜单 ==========
    [MenuItem("Tools/资源工具/1.自动打包图集/清空Addressables/Atlases文件夹")]
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
        Z_Logger.Log($"AutoAtlasTool: '{AtlasRootFolder}' 已清空。");
        EditorUtility.DisplayDialog("完成", $"'{AtlasRootFolder}' 已清空。", "确定");
    }

    // ✅ 额外功能：手动标记已有图集为 Addressable
    [MenuItem("Tools/资源工具/1.自动打包图集/标记所有图集为Addressable")]
    static void MarkAllAtlasesAsAddressable()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Z_Logger.LogError("AutoAtlasTool: Addressable Settings 不存在！");
            EditorUtility.DisplayDialog("错误", "Addressable Settings 不存在！\n请先通过 Window → Asset Management → Addressables → Settings 创建。", "确定");
            return;
        }

        // ✅ 获取或创建 AtlasGroup
        AddressableAssetGroup atlasGroup = settings.FindGroup("AtlasGroup");
        if (atlasGroup == null)
        {
            atlasGroup = settings.CreateGroup("AtlasGroup", false, false, true, null);
            Z_Logger.Log("AutoAtlasTool: 创建 AtlasGroup 用于存放图集");
        }

        string[] atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasRootFolder });
        int markedCount = 0;
        int skippedCount = 0;

        foreach (string guid in atlasGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
            if (existingEntry != null)
            {
                Z_Logger.Log($"AutoAtlasTool: {path} 已经是 Addressable，跳过。");
                skippedCount++;
                continue;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, atlasGroup);
            if (entry != null)
            {
                entry.SetAddress(path);
                Z_Logger.Log($"AutoAtlasTool: ✅ 标记图集为 Addressable: {path}");
                markedCount++;
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("完成",
            $"标记完成！\n\n" +
            $"新标记: {markedCount} 个\n" +
            $"已存在: {skippedCount} 个",
            "确定");
    }
}
