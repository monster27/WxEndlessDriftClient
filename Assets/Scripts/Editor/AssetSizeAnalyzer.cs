#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class AssetSizeAnalyzer
{
    [MenuItem("Tools/分析资源大小/按类型统计")]
    public static void AnalyzeAssetsByType()
    {
        // 获取所有资源
        string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });

        Dictionary<string, long> typeSizes = new Dictionary<string, long>();
        Dictionary<string, List<string>> largeAssets = new Dictionary<string, List<string>>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            long size = GetAssetSize(path);

            if (size > 1024 * 1024) // >1MB
            {
                string type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown";

                if (!largeAssets.ContainsKey(type))
                    largeAssets[type] = new List<string>();
                largeAssets[type].Add($"{path} ({size / 1024 / 1024} MB)");
            }

            string assetType = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown";
            if (!typeSizes.ContainsKey(assetType))
                typeSizes[assetType] = 0;
            typeSizes[assetType] += size;
        }

        // 输出统计
        Debug.Log("========== 📊 资源按类型统计 ==========");
        foreach (var kvp in typeSizes.OrderByDescending(x => x.Value))
        {
            Debug.Log($"{kvp.Key}: {kvp.Value / 1024 / 1024} MB");
        }

        Debug.Log("");
        Debug.Log("========== ⚠️ 大文件列表 (>1MB) ==========");
        foreach (var kvp in largeAssets)
        {
            Debug.Log($"\n【{kvp.Key}】共 {kvp.Value.Count} 个大文件:");
            foreach (string file in kvp.Value.Take(20))
            {
                Debug.Log($"  - {file}");
            }
        }
    }

    private static long GetAssetSize(string path)
    {
        string fullPath = Path.Combine(Application.dataPath, "..", path);
        if (File.Exists(fullPath))
        {
            return new FileInfo(fullPath).Length;
        }
        return 0;
    }

    [MenuItem("Tools/分析资源大小/查看最大文件")]
    public static void FindLargestFiles()
    {
        string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
        var files = new List<(string path, long size)>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            long size = GetAssetSize(path);
            if (size > 0)
            {
                files.Add((path, size));
            }
        }

        files = files.OrderByDescending(x => x.size).ToList();

        Debug.Log("========== 📁 最大文件 TOP 20 ==========");
        for (int i = 0; i < Math.Min(20, files.Count); i++)
        {
            var file = files[i];
            string sizeStr = file.size > 1024 * 1024
                ? $"{file.size / 1024 / 1024} MB"
                : $"{file.size / 1024} KB";
            Debug.Log($"{i + 1}. {file.path} ({sizeStr})");
        }
    }
}
#endif
