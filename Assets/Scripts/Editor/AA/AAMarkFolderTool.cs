#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

/// <summary>
/// 单独抽离的标记工具：右键将文件夹内所有资源标记为 Addressable
/// </summary>
public class AAMarkFolderTool
{
    [MenuItem("Assets/标记文件夹为 Addressable", false, 31)]
    public static void MarkFolderAsAddressable()
    {
        string folderPath = GetSelectedFolderPath();

        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog("提示", "请先选中一个文件夹！", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "确认操作",
            $"将把文件夹中的所有资源标记为 Addressable：\n\n📁 {folderPath}\n\n" +
            "⚠️ 包括子文件夹中的所有资源都会被标记！",
            "确认",
            "取消"))
        {
            return;
        }

        ProcessMarkFolder(folderPath);
    }

    private static void ProcessMarkFolder(string folderPath)
    {
        folderPath = NormalizeFolderPath(folderPath);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("错误", "未找到 Addressable 设置！\n请先初始化 Addressables", "确定");
            return;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();

        string fullPath = Path.Combine(Application.dataPath, folderPath.Replace("Assets/", ""));

        if (!Directory.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("错误", $"目录不存在：\n{fullPath}", "确定");
            return;
        }

        string[] allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);

        List<string> assetPaths = new List<string>();
        int skippedCount = 0;

        foreach (string file in allFiles)
        {
            string relativePath = file.Replace(Application.dataPath, "Assets").Replace('\\', '/');

            // 跳过 .meta 文件
            if (file.EndsWith(".meta"))
            {
                skippedCount++;
                continue;
            }

            // ⚠️ 只跳过脚本和程序集定义文件，不跳过 .json
            if (file.EndsWith(".cs"))
            {
                skippedCount++;
                continue;
            }
            if (file.EndsWith(".asmdef"))
            {
                skippedCount++;
                continue;
            }
            if (file.EndsWith(".asmref"))
            {
                skippedCount++;
                continue;
            }
            if (file.EndsWith(".DS_Store"))
            {
                skippedCount++;
                continue;
            }

            // ✅ 其他所有文件（包括 .json）都尝试标记
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(relativePath);
            if (obj != null)
            {
                assetPaths.Add(relativePath);
            }
            else
            {
                skippedCount++;
                Debug.LogWarning($"[Addressable标记] ⚠️ 无法加载: {relativePath}");
            }
        }

        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示",
                $"未找到可标记的资源文件！\n\n" +
                $"总文件数: {allFiles.Length}\n" +
                $"跳过的文件: {skippedCount} (包括 .meta、脚本等)",
                "确定");
            return;
        }

        int addedCount = 0;
        int existingCount = 0;

        AddressableAssetGroup group = settings.DefaultGroup;
        if (group == null)
        {
            group = settings.CreateGroup("Default Local Group", false, false, true, null);
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

        for (int i = 0; i < assetPaths.Count; i++)
        {
            string assetPath = assetPaths[i];
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            EditorUtility.DisplayProgressBar(
                "标记 Addressable",
                $"处理: {Path.GetFileName(assetPath)} ({i + 1}/{assetPaths.Count})",
                (float)i / assetPaths.Count
            );

            AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
            if (existingEntry != null)
            {
                existingCount++;
                Debug.Log($"[Addressable标记] 已存在: {assetPath}");
                continue;
            }

            Debug.Log($"[Addressable标记] ✅ 新增: {assetPath}");
            settings.CreateOrMoveEntry(guid, group, false, false);
            addedCount++;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.ClearProgressBar();

        string msg = $"✅ 标记完成！\n\n" +
                     $"📁 {folderPath}\n" +
                     $"📄 新增标记: {addedCount} 个资源\n" +
                     $"📄 已存在: {existingCount} 个资源\n" +
                     $"⏭️ 跳过: {skippedCount} 个文件\n";

        Debug.Log($"[Addressable标记] ===== 完成 =====\n" +
                  $"  文件夹: {folderPath}\n" +
                  $"  新增: {addedCount} 个\n" +
                  $"  已存在: {existingCount} 个\n" +
                  $"  跳过: {skippedCount} 个");

        EditorUtility.DisplayDialog("标记完成", msg, "确定");
    }

    [MenuItem("Assets/标记文件夹为 Addressable", true)]
    private static bool ValidateMarkFolder()
    {
        return !string.IsNullOrEmpty(GetSelectedFolderPath());
    }

    private static string NormalizeFolderPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        path = path.TrimStart('/', '\\');

        while (path.StartsWith("Assets/Assets/"))
        {
            path = path.Substring("Assets/".Length);
        }

        if (path.StartsWith("Assets"))
        {
            return "Assets/" + path.Substring("Assets".Length).TrimStart('/', '\\');
        }

        return "Assets/" + path.TrimStart('/', '\\');
    }

    private static string GetSelectedFolderPath()
    {
        var objs = Selection.GetFiltered<Object>(SelectionMode.Assets);
        if (objs == null || objs.Length == 0) return "";

        string path = AssetDatabase.GetAssetPath(objs[0]);
        if (!AssetDatabase.IsValidFolder(path))
        {
            path = Path.GetDirectoryName(path);
        }
        return NormalizeFolderPath(path);
    }
}
#endif
