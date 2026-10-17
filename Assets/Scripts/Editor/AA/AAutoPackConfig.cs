using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAEditor
{
    public enum LabelOption
    {
        [InspectorName("【默认】跟随上级目录")] Inherit = 0,
        [InspectorName("不参与预下载")] None = 1,
        [InspectorName("打进主包（首包）")] Local = 2,
        [InspectorName("立刻预下载")] Tier1_Preload = 3,
        [InspectorName("空闲时预下载")] Tier2_Idle = 4,
        [InspectorName("用到才下载")] Tier3_OnDemand = 5,
    }

    [Serializable]
    public class FolderLabelEntry
    {
        public string folderPath;
        public LabelOption label = LabelOption.Inherit;
    }

    // ★ 新增：用于存储 Group 与 文件夹单元 的映射关系
    [Serializable]
    public class GroupMappingEntry
    {
        public string groupName;
        public List<string> folderUnits = new List<string>(); // 记录的单元路径（如 Assets/Addressables/UI/Icon/BaitIcons/）
        public long estimatedSize; // 预估大小，用于验证
    }

    [CreateAssetMenu(fileName = "AAutoPackConfig", menuName = "AA/AutoPackConfig")]
    public class AAutoPackConfig : ScriptableObject
    {
        [Header("阈值（KB）")]
        public long minBundleKB = 500;
        public long idealMaxKB = 4096;
        public long hardMaxKB = 8192;

        [Header("扫描根")]
        public List<string> scanRoots = new List<string> { "Assets/Addressables" };

        [Header("打图集扫描")]
        public List<string> atlasScanRoots = new List<string> { "Assets/Textures", "Assets/Addressables" };
        public List<string> atlasExcludes = new List<string> { "Assets/Addressables/Atlases" };

        [Header("View")]
        public bool viewPerPrefab = true;
        public string viewFolderPath = "Assets/Addressables/UI/View/";

        [Header("远程路径")]
        public string remoteBuildPath = "ServerData/[BuildTarget]";
        public string remoteLoadPath = "https://endlessdriftfish.top/aa/WebGL/[BuildTarget]";

        [Header("WebGL 输出")]
        public string webglOutput = "Build/WebGL";

        [Header("文件夹 Label 配置")]
        public List<FolderLabelEntry> folderLabels = new List<FolderLabelEntry>();

        // ★ 新增：存储智能分组的结果
        [Header("Group 映射配置 (按钮3生成)")]
        public List<GroupMappingEntry> groupMappings = new List<GroupMappingEntry>();

        public LabelOption GetLabelForPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return LabelOption.Tier3_OnDemand;

            foreach (var e in folderLabels)
            {
                if (e.label == LabelOption.Inherit) continue;
                if (!e.folderPath.EndsWith("/") && e.folderPath == assetPath) return e.label;
            }

            string cur = assetPath;
            while (true)
            {
                int slash = cur.LastIndexOf('/');
                if (slash <= 0) break;
                cur = cur.Substring(0, slash + 1);

                foreach (var e in folderLabels)
                {
                    if (e.label == LabelOption.Inherit) continue;
                    if (e.folderPath == cur) return e.label;
                }
            }

            return LabelOption.Tier3_OnDemand;
        }

        public string[] GetLabelsForPath(string assetPath)
        {
            var opt = GetLabelForPath(assetPath);
            switch (opt)
            {
                case LabelOption.Tier1_Preload: return new[] { "tier1_preload" };
                case LabelOption.Tier2_Idle: return new[] { "tier2_idle" };
                case LabelOption.Tier3_OnDemand: return new[] { "tier3_ondemand" };
                default: return Array.Empty<string>();
            }
        }

        public bool IsLocal(string assetPath)
        {
            return GetLabelForPath(assetPath) == LabelOption.Local;
        }

        public static AAutoPackConfig GetOrCreate()
        {
            const string dir = "Assets/Editor";
            const string path = dir + "/AAutoPackConfig.asset";

            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<AAutoPackConfig>(path);
            if (cfg != null) return cfg;

            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Editor");

            cfg = ScriptableObject.CreateInstance<AAutoPackConfig>();
            UnityEditor.AssetDatabase.CreateAsset(cfg, path);
            UnityEditor.AssetDatabase.SaveAssets();
            return cfg;
        }
    }
}
