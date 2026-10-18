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

    [Serializable]
    public class GroupMappingEntry
    {
        public string groupName;
        public List<string> folderUnits = new List<string>();
        public long estimatedSize;

        // ★ 新增：持久化 Label 选择（0=无标签,1=Local,2=Tier1,3=Tier2,4=Tier3）
        public int labelIndex = 4;
    }

    [CreateAssetMenu(fileName = "AAutoPackConfig", menuName = "AA/AutoPackConfig")]
    public class AAutoPackConfig : ScriptableObject
    {
        [Header("普通资源装箱阈值（KB，≈未压缩体积）")]
        public long minBundleKB = 512;
        public long idealMaxKB = 2048;
        public long hardMaxKB = 4096;

        [Header("图集装箱阈值（KB，未压缩体积）")]
        public long atlasIdealMaxKB = 6144;
        public long atlasHardMaxKB = 12288;

        [Header("远程路径")]
        public string remoteBuildPath = "ServerData/[BuildTarget]";
        public string remoteLoadPath = "https://endlessdriftfish.top/aa/WebGL/[BuildTarget]";

        [Header("Build 文件夹路径")]
        public string buildFolderPath = "Build";

        [Header("打包设置")]
        public bool autoOpenBuildFolder = false;

        [Header("独立打包列表 (强制单独成组，不参与合并)")]
        [Tooltip("填入资源的 Asset 路径，例如 Assets/Addressables/Scenes/GameScene.unity")]
        public List<string> standaloneAssets = new List<string>()
        {
            "Assets/Addressables/Scenes/GameScene.unity"
        };

        [Header("文件夹 Label 配置")]
        public List<FolderLabelEntry> folderLabels = new List<FolderLabelEntry>();

        [Header("Group 映射配置")]
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

        public bool IsLocal(string assetPath) => GetLabelForPath(assetPath) == LabelOption.Local;

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
