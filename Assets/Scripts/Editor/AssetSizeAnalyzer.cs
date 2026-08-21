#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 资源分析工具 - 实时版
/// </summary>
public class AssetSizeAnalyzerWindow : EditorWindow
{
    private Vector2 scrollPos;
    private Vector2 cloudScrollPos;
    private int tab = 0;
    private string[] tabs = new string[] { "📁 源文件", "📦 AB包", "☁️ Cloud统计" };
    private string jsonPath = "";
    private bool isParsing = false;
    private string status = "";
    private string searchFilter = "";
    private string folderFilter = "";
    private string typeFilter = "";
    private int sortMode = 0;
    private string[] sortOptions = new string[] { "按大小↓", "按大小↑", "按名称A-Z", "按名称Z-A" };
    private bool showOnlyWarnings = false;
    private int sizeFilterIndex = 0;
    private string[] sizeFilterOptions = new string[] { "全部", ">1MB", ">5MB", ">10MB", ">50MB" };
    private int viewMode = 0;
    private string[] viewModes = new string[] { "📦 按Bundle", "📂 按文件夹", "📄 按类型" };

    // 折叠状态
    private Dictionary<string, bool> bundleFoldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> folderFoldouts = new Dictionary<string, bool>();
    private bool expandAll = false;

    // 下拉框选择 - Cloud分类
    private int selectedCategoryIndex = 0;
    private string[] categoryOptions = new string[] { "全部" };
    private Dictionary<string, List<AssetInfo>> categoryAssets = new Dictionary<string, List<AssetInfo>>();

    // 下拉框选择 - 按类型视图 (AB包Tab)
    private int selectedTypeIndex = 0;
    private string[] typeOptions = new string[] { "全部" };

    // 下拉框选择 - Cloud按类型
    private int selectedCloudTypeIndex = 0;
    private string[] cloudTypeOptions = new string[] { "全部" };

    // 源文件
    private List<SourceFileInfo> sourceFiles = new List<SourceFileInfo>();
    private Dictionary<string, long> sourceTypeStats = new Dictionary<string, long>();
    private bool sourceAnalyzed = false;
    private string sourceFilter = "";
    private DateTime lastSourceScanTime = DateTime.MinValue;
    private bool isScanningSource = false;

    // AB包
    private List<BundleInfo> bundles = new List<BundleInfo>();
    private List<AssetInfo> allAssets = new List<AssetInfo>();
    private Dictionary<string, long> typeStats = new Dictionary<string, long>();

    // Cloud统计
    private CloudStats cloudStats = new CloudStats();

    // 缓存 - 避免重复计算
    private Dictionary<string, long> assetSizeCache = new Dictionary<string, long>();
    private Dictionary<string, string> assetTypeCache = new Dictionary<string, string>();

    private class BundleInfo
    {
        public string Name;
        public long Size;
        public int AssetCount;
        public List<AssetInfo> Assets = new List<AssetInfo>();
        public string Warning;
        public Dictionary<string, long> TypeStats = new Dictionary<string, long>();
        public string Category;
        public bool IsCloud = false;
        public string CloudUrl;
        public string BundleType;
    }

    private class AssetInfo
    {
        public string Path, Type, Folder, BundleName, Guid;
        public long Size;
        public bool IsCloud = false;
        public string CloudUrl;
        public string BundleType;
        public long FileSize;
        public long ImportedSize;
    }

    private class SourceFileInfo
    {
        public string Path, Type, Folder;
        public long Size;
        public long FileSize;
        public bool IsImported;
    }

    private class CloudStats
    {
        public long Total;
        public int TotalAssets;
        public Dictionary<string, long> Categories = new Dictionary<string, long>();
        public Dictionary<string, long> CustomDetail = new Dictionary<string, long>();
        public List<BundleInfo> Bundles = new List<BundleInfo>();
        public Dictionary<string, List<AssetInfo>> CategoryAssets = new Dictionary<string, List<AssetInfo>>();
        public Dictionary<string, List<AssetInfo>> TypeAssets = new Dictionary<string, List<AssetInfo>>();
    }

    [MenuItem("Tools/资源工具/0.资源分析工具", false, 100)]
    public static void ShowWindow() => GetWindow<AssetSizeAnalyzerWindow>("资源分析工具");

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        tab = GUILayout.Toolbar(tab, tabs, GUILayout.Height(28));
        EditorGUILayout.Space(8);

        DrawSeparator();

        if (isScanningSource || isParsing)
        {
            EditorGUILayout.HelpBox("⏳ 正在扫描/解析中...", MessageType.Info);
        }

        switch (tab)
        {
            case 0: DrawSourceTab(); break;
            case 1: DrawBundleTab(); break;
            case 2: DrawCloudTab(); break;
        }
    }

    private void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
        EditorGUILayout.Space(4);
    }

    // ======================== Tab1: 源文件 (实时) ========================

    private void DrawSourceTab()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍 分析所有资源 (实时)", GUILayout.Height(28))) AnalyzeSourceFilesRealTime();
        if (GUILayout.Button("📂 分析选中文件夹", GUILayout.Height(28))) AnalyzeSelectedFolderRealTime();
        if (GUILayout.Button("🔄 强制刷新全部", GUILayout.Height(28)))
        {
            assetSizeCache.Clear();
            assetTypeCache.Clear();
            ForceReimportAndAnalyze();
        }
        if (GUILayout.Button("🗑️ 清空缓存", GUILayout.Height(28)))
        {
            assetSizeCache.Clear();
            assetTypeCache.Clear();
            sourceFiles.Clear();
            sourceTypeStats.Clear();
            sourceAnalyzed = false;
            lastSourceScanTime = DateTime.MinValue;
            status = "缓存已清空";
            EditorUtility.DisplayDialog("提示", "缓存已清空，点击「分析所有资源」重新获取实时数据", "确定");
        }
        EditorGUILayout.EndHorizontal();

        if (sourceAnalyzed && lastSourceScanTime > DateTime.MinValue)
        {
            EditorGUILayout.LabelField($"⏱️ 最后扫描: {lastSourceScanTime:HH:mm:ss}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"💡 提示: 修改资源设置后点击「强制刷新全部」重新导入并分析", EditorStyles.miniLabel);
        }

        DrawSeparator();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🔎 过滤:", GUILayout.Width(50));
        sourceFilter = EditorGUILayout.TextField(sourceFilter);
        if (GUILayout.Button("✕", GUILayout.Width(25))) sourceFilter = "";
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        if (isScanningSource)
        {
            EditorGUILayout.HelpBox("⏳ 扫描中，请稍候...", MessageType.Info);
            return;
        }

        if (!sourceAnalyzed)
        {
            EditorGUILayout.HelpBox("点击「🔍 分析所有资源 (实时)」获取 Unity 导入后的真实资源大小", MessageType.Info);
            return;
        }

        long total = sourceFiles.Sum(f => f.Size);
        long totalFileSize = sourceFiles.Sum(f => f.FileSize);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"📦 {sourceFiles.Count} 个文件", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"💾 导入后总大小: {Fmt(total)}  (硬盘大小: {Fmt(totalFileSize)})", EditorStyles.boldLabel);

        if (total != totalFileSize && total > 0 && totalFileSize > 0)
        {
            float ratio = (float)total / totalFileSize;
            EditorGUILayout.LabelField($"📊 压缩率: {ratio:P1}  (导入后 / 硬盘)", EditorStyles.miniLabel);
        }

        EditorGUILayout.LabelField("📊 类型分布:", EditorStyles.miniLabel);
        foreach (var kv in sourceTypeStats.OrderByDescending(x => x.Value).Take(8))
        {
            float pct = total > 0 ? (float)kv.Value / total * 100 : 0;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"  {kv.Key,-15}", GUILayout.Width(130));
            GUILayout.Label(Fmt(kv.Value), GUILayout.Width(90));
            Rect r = GUILayoutUtility.GetRect(100, 16);
            EditorGUI.ProgressBar(r, pct / 100f, $"{pct:F1}%");
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        DrawSeparator();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        var list = sourceFiles;
        if (!string.IsNullOrEmpty(sourceFilter))
            list = list.Where(f => f.Path.ToLower().Contains(sourceFilter.ToLower())).ToList();

        if (list.Count > 0)
        {
            long filteredTotal = list.Sum(f => f.Size);
            EditorGUILayout.LabelField($"显示 {list.Count} 个文件, 总大小 {Fmt(filteredTotal)}", EditorStyles.miniLabel);
        }

        foreach (var f in list.OrderByDescending(x => x.Size).Take(300))
        {
            EditorGUILayout.BeginHorizontal();

            GUI.color = f.Size > 5 * 1024 * 1024 ? Color.red :
                       f.Size > 1024 * 1024 ? new Color(1f, 0.8f, 0.2f) : Color.white;

            string sizeDisplay = f.IsImported ? Fmt(f.Size) : Fmt(f.FileSize) + " (未导入)";
            if (f.IsImported && f.Size != f.FileSize && f.FileSize > 0)
            {
                float ratio = (float)f.Size / f.FileSize;
                sizeDisplay += $" [{ratio:P0}]";
            }

            GUILayout.Label($"  {f.Path,-55} [{f.Type,-12}] {sizeDisplay}", EditorStyles.miniLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        if (list.Count > 300) GUILayout.Label($"... 还有 {list.Count - 300} 个", EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
    }

    // ======================== Tab2: AB包 ========================

    private void DrawBundleTab()
    {
        DrawPathSelector();
        DrawSeparator();

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, status.StartsWith("✅") ? MessageType.Info : MessageType.Warning);

        if (isParsing) { EditorGUILayout.HelpBox("⏳ 解析中...", MessageType.Info); return; }
        if (bundles.Count == 0)
        {
            EditorGUILayout.HelpBox("点击「🔍 解析」加载 buildlayout.json", MessageType.Info);
            return;
        }

        DrawFilters();
        DrawSeparator();
        DrawStats();
        DrawSeparator();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(expandAll ? "📂 全部折叠" : "📂 全部展开", GUILayout.Width(100)))
        {
            expandAll = !expandAll;
            foreach (var key in bundleFoldouts.Keys.ToList())
                bundleFoldouts[key] = expandAll;
        }
        GUILayout.Label($"共 {bundles.Count} 个AB包", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        DrawBundleList();
    }

    private void DrawPathSelector()
    {
        EditorGUILayout.BeginHorizontal();
        jsonPath = EditorGUILayout.TextField("JSON路径:", jsonPath);

        if (GUILayout.Button("📂", GUILayout.Width(30)))
        {
            string p = EditorUtility.OpenFilePanel("选择 buildlayout.json", Application.dataPath, "json");
            if (!string.IsNullOrEmpty(p)) jsonPath = p;
        }

        string def = Path.Combine(Application.dataPath, "../Library/com.unity.addressables/buildlayout.json");
        if (GUILayout.Button("📍 定位", GUILayout.Width(50)))
        {
            if (File.Exists(def)) { jsonPath = def; ParseBundles(def); }
            else ShowMsg($"未找到 buildlayout.json\n路径: {def}");
        }

        if (GUILayout.Button("🔍 解析", GUILayout.Width(60)) && !isParsing)
        {
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
            {
                if (File.Exists(def)) { jsonPath = def; ParseBundles(def); }
                else ShowMsg("请选择有效的 buildlayout.json");
            }
            else ParseBundles(jsonPath);
        }

        if (GUILayout.Button("🔄 刷新", GUILayout.Width(60)) && !isParsing && File.Exists(jsonPath))
        {
            ParseBundles(jsonPath);
        }

        if (GUILayout.Button("📊 导出CSV", GUILayout.Width(70)) && bundles.Count > 0)
            ExportToCSV();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFilters()
    {
        EditorGUILayout.BeginHorizontal();
        viewMode = GUILayout.Toolbar(viewMode, viewModes, GUILayout.Height(22));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("🔎", GUILayout.Width(20));
        searchFilter = EditorGUILayout.TextField(searchFilter, GUILayout.Width(120));

        GUILayout.Label("📁", GUILayout.Width(20));
        folderFilter = EditorGUILayout.TextField(folderFilter, GUILayout.Width(100));

        GUILayout.Label("📄", GUILayout.Width(20));
        typeFilter = EditorGUILayout.TextField(typeFilter, GUILayout.Width(80));

        GUILayout.Label("大小:", GUILayout.Width(30));
        sizeFilterIndex = EditorGUILayout.Popup(sizeFilterIndex, sizeFilterOptions, GUILayout.Width(70));

        sortMode = EditorGUILayout.Popup(sortMode, sortOptions, GUILayout.Width(100));

        showOnlyWarnings = GUILayout.Toggle(showOnlyWarnings, "⚠️", GUILayout.Width(30));

        if (GUILayout.Button("✕ 重置", GUILayout.Width(50)))
        {
            searchFilter = ""; folderFilter = ""; typeFilter = "";
            sizeFilterIndex = 0; showOnlyWarnings = false;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStats()
    {
        if (bundles.Count == 0) return;

        long total = bundles.Sum(b => b.Size);
        int totalAssets = bundles.Sum(b => b.AssetCount);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"📦 {bundles.Count} 个AB包", EditorStyles.boldLabel, GUILayout.Width(120));
        GUILayout.Label($"📄 {totalAssets} 个资源", EditorStyles.boldLabel, GUILayout.Width(120));
        GUILayout.Label($"💾 {Fmt(total)}", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        if (typeStats.Count > 0)
        {
            EditorGUILayout.LabelField("类型分布:", EditorStyles.miniLabel);
            foreach (var kv in typeStats.OrderByDescending(x => x.Value).Take(6))
            {
                float pct = total > 0 ? (float)kv.Value / total * 100 : 0;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"  {kv.Key,-12}", GUILayout.Width(100));
                GUILayout.Label(Fmt(kv.Value), GUILayout.Width(80));
                Rect r = GUILayoutUtility.GetRect(100, 16);
                EditorGUI.ProgressBar(r, pct / 100f, $"{pct:F1}%");
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawBundleList()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        long sizeLimit = 0;
        switch (sizeFilterIndex)
        {
            case 1: sizeLimit = 1024 * 1024; break;
            case 2: sizeLimit = 5 * 1024 * 1024; break;
            case 3: sizeLimit = 10 * 1024 * 1024; break;
            case 4: sizeLimit = 50 * 1024 * 1024; break;
        }

        var displayBundles = bundles.AsEnumerable();

        if (showOnlyWarnings)
            displayBundles = displayBundles.Where(b => !string.IsNullOrEmpty(b.Warning));

        switch (sortMode)
        {
            case 0: displayBundles = displayBundles.OrderByDescending(b => b.Size); break;
            case 1: displayBundles = displayBundles.OrderBy(b => b.Size); break;
            case 2: displayBundles = displayBundles.OrderBy(b => b.Name); break;
            case 3: displayBundles = displayBundles.OrderByDescending(b => b.Name); break;
        }

        foreach (var b in displayBundles)
        {
            var assets = b.Assets.AsEnumerable();

            if (!string.IsNullOrEmpty(searchFilter))
                assets = assets.Where(a => a.Path.ToLower().Contains(searchFilter.ToLower()) ||
                                           a.Folder.ToLower().Contains(searchFilter.ToLower()));
            if (!string.IsNullOrEmpty(folderFilter))
                assets = assets.Where(a => a.Folder.ToLower().Contains(folderFilter.ToLower()));
            if (!string.IsNullOrEmpty(typeFilter))
                assets = assets.Where(a => a.Type.ToLower().Contains(typeFilter.ToLower()));
            if (sizeLimit > 0)
                assets = assets.Where(a => a.Size >= sizeLimit);

            var assetList = assets.ToList();
            if (assetList.Count == 0 && !string.IsNullOrEmpty(searchFilter)) continue;

            if (!bundleFoldouts.ContainsKey(b.Name))
                bundleFoldouts[b.Name] = false;

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            bool isExpanded = bundleFoldouts[b.Name];
            string foldIcon = isExpanded ? "▼" : "▶";
            if (GUILayout.Button($"{foldIcon} {b.Name}", EditorStyles.boldLabel, GUILayout.Width(300)))
            {
                bundleFoldouts[b.Name] = !bundleFoldouts[b.Name];
            }

            GUI.color = b.Size > 10 * 1024 * 1024 ? new Color(1f, 0.3f, 0.3f) :
                       b.Size > 5 * 1024 * 1024 ? new Color(1f, 0.8f, 0.2f) : Color.white;
            GUILayout.Label(Fmt(b.Size), GUILayout.Width(100));
            GUI.color = Color.white;
            GUILayout.Label($"资源: {b.AssetCount} / 显示: {assetList.Count}", GUILayout.Width(130));
            if (!string.IsNullOrEmpty(b.Category))
                GUILayout.Label($"🏷️ {b.Category}", GUILayout.Width(100));
            if (b.IsCloud)
                GUILayout.Label("☁️", GUILayout.Width(25));
            if (!string.IsNullOrEmpty(b.Warning))
                GUILayout.Label("⚠️", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(b.Warning))
                EditorGUILayout.HelpBox(b.Warning, MessageType.Warning);

            if (isExpanded && assetList.Count > 0)
            {
                DrawAssets(assetList);
            }
            else if (isExpanded && assetList.Count == 0)
            {
                GUILayout.Label("  (无匹配资源)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawAssets(List<AssetInfo> assets)
    {
        if (viewMode == 0) // 按Bundle
        {
            foreach (var a in assets.OrderByDescending(x => x.Size).Take(100))
                DrawAssetLine(a);
            if (assets.Count > 100)
                GUILayout.Label($"  ... 还有 {assets.Count - 100} 个", EditorStyles.miniLabel);
        }
        else if (viewMode == 1) // 按文件夹
        {
            var groups = assets.GroupBy(a => a.Folder ?? "根目录")
                .OrderByDescending(g => g.Sum(x => x.Size));

            foreach (var group in groups)
            {
                string folderKey = group.Key;
                if (!folderFoldouts.ContainsKey(folderKey))
                    folderFoldouts[folderKey] = false;

                long groupSize = group.Sum(x => x.Size);
                EditorGUILayout.BeginHorizontal();
                bool isExpanded = folderFoldouts[folderKey];
                string foldIcon = isExpanded ? "▼" : "▶";
                if (GUILayout.Button($"{foldIcon} 📁 {folderKey} ({group.Count()} 个) - {Fmt(groupSize)}",
                    EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true)))
                {
                    folderFoldouts[folderKey] = !folderFoldouts[folderKey];
                }
                EditorGUILayout.EndHorizontal();

                if (isExpanded)
                {
                    foreach (var a in group.OrderByDescending(x => x.Size).Take(50))
                        DrawAssetLine(a, "    ");
                    if (group.Count() > 50)
                        GUILayout.Label($"      ... 还有 {group.Count() - 50} 个", EditorStyles.miniLabel);
                }
            }
        }
        else // 按类型 - 带下拉框
        {
            var typeGroups = assets.GroupBy(a => a.Type)
                .OrderByDescending(g => g.Sum(x => x.Size))
                .ToDictionary(g => g.Key, g => g.ToList());

            var typeNames = typeGroups.Keys.ToList();
            var options = new List<string> { "📂 全部" };
            options.AddRange(typeNames);
            typeOptions = options.ToArray();

            if (selectedTypeIndex >= typeOptions.Length)
                selectedTypeIndex = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("选择类型:", GUILayout.Width(60));
            selectedTypeIndex = EditorGUILayout.Popup(selectedTypeIndex, typeOptions, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            string selectedType = selectedTypeIndex == 0 ? "全部" : typeOptions[selectedTypeIndex];

            if (selectedType == "全部")
            {
                foreach (var kv in typeGroups)
                {
                    long groupSize = kv.Value.Sum(a => a.Size);
                    string typeKey = "type_" + kv.Key;
                    if (!folderFoldouts.ContainsKey(typeKey))
                        folderFoldouts[typeKey] = false;

                    EditorGUILayout.BeginHorizontal();
                    bool isExpanded = folderFoldouts[typeKey];
                    string foldIcon = isExpanded ? "▼" : "▶";
                    if (GUILayout.Button($"{foldIcon} 📄 {kv.Key} ({kv.Value.Count} 个) - {Fmt(groupSize)}",
                        EditorStyles.miniBoldLabel, GUILayout.ExpandWidth(true)))
                    {
                        folderFoldouts[typeKey] = !folderFoldouts[typeKey];
                    }
                    EditorGUILayout.EndHorizontal();

                    if (isExpanded)
                    {
                        foreach (var a in kv.Value.OrderByDescending(x => x.Size).Take(50))
                            DrawAssetLine(a, "    ");
                        if (kv.Value.Count > 50)
                            GUILayout.Label($"      ... 还有 {kv.Value.Count - 50} 个", EditorStyles.miniLabel);
                    }
                }
            }
            else
            {
                if (typeGroups.ContainsKey(selectedType))
                {
                    var items = typeGroups[selectedType];
                    long totalSize = items.Sum(a => a.Size);
                    EditorGUILayout.LabelField($"📄 {selectedType}: {items.Count} 个资源, 总大小 {Fmt(totalSize)}", EditorStyles.boldLabel);

                    foreach (var a in items.OrderByDescending(x => x.Size).Take(100))
                        DrawAssetLine(a, "  ");
                    if (items.Count > 100)
                        GUILayout.Label($"  ... 还有 {items.Count - 100} 个", EditorStyles.miniLabel);
                }
            }
        }
    }

    private void DrawAssetLine(AssetInfo a, string indent = "  ")
    {
        EditorGUILayout.BeginHorizontal();
        GUI.color = a.Size > 1024 * 1024 ? new Color(1f, 0.9f, 0.4f) : Color.white;
        string cloudIcon = a.IsCloud ? "☁️" : "📄";
        GUILayout.Label($"{indent}{cloudIcon} {a.Path,-50} [{a.Type,-14}] {Fmt(a.Size)}", EditorStyles.miniLabel);
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // ======================== Tab3: Cloud统计 ========================

    private void DrawCloudTab()
    {
        EditorGUILayout.LabelField("☁️ Cloud Assets 分类统计", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📊 从JSON生成", GUILayout.Height(28)))
        {
            string def = Path.Combine(Application.dataPath, "../Library/com.unity.addressables/buildlayout.json");
            if (File.Exists(def)) { jsonPath = def; ParseCloudStats(def); }
            else ShowMsg("未找到 buildlayout.json");
        }
        if (GUILayout.Button("📋 复制统计", GUILayout.Height(28)) && cloudStats.Total > 0)
        {
            GUIUtility.systemCopyBuffer = GetCloudStatsString();
            ShowMsg("已复制到剪贴板");
        }
        if (GUILayout.Button("📊 导出报告", GUILayout.Height(28)) && cloudStats.Total > 0)
            ExportCloudReport();
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        if (cloudStats.Total == 0)
        {
            EditorGUILayout.HelpBox("点击「📊 从JSON生成」生成统计", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField($"📊 总大小: {Fmt(cloudStats.Total)}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"📦 总资源数: {cloudStats.TotalAssets}");
        EditorGUILayout.LabelField($"📦 AB包数: {cloudStats.Bundles.Count}");

        string detailLine = GetCloudDetailLine();
        EditorGUILayout.LabelField(detailLine, EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        DrawSeparator();

        // 分类统计 - 带下拉框
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📊 分类统计:", EditorStyles.boldLabel);

        var categoryNames = cloudStats.Categories.Keys.ToList();
        if (categoryNames.Count > 0)
        {
            var options = new List<string> { "📂 全部" };
            options.AddRange(categoryNames);
            categoryOptions = options.ToArray();

            if (selectedCategoryIndex >= categoryOptions.Length)
                selectedCategoryIndex = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("选择分类:", GUILayout.Width(60));
            selectedCategoryIndex = EditorGUILayout.Popup(selectedCategoryIndex, categoryOptions, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            DrawSeparator();

            string selectedCategory = selectedCategoryIndex == 0 ? "全部" : categoryOptions[selectedCategoryIndex];

            if (selectedCategory == "全部")
            {
                foreach (var kv in cloudStats.Categories.OrderByDescending(x => x.Value))
                {
                    float pct = cloudStats.Total > 0 ? (float)kv.Value / cloudStats.Total * 100 : 0;
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"  {kv.Key,-12}", GUILayout.Width(120));
                    GUILayout.Label(Fmt(kv.Value), GUILayout.Width(100));
                    Rect r = GUILayoutUtility.GetRect(100, 18);
                    EditorGUI.ProgressBar(r, pct / 100f, $"{pct:F1}%");
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                if (cloudStats.CategoryAssets.ContainsKey(selectedCategory))
                {
                    var assets = cloudStats.CategoryAssets[selectedCategory];
                    long totalSize = assets.Sum(a => a.Size);
                    EditorGUILayout.LabelField($"📦 {selectedCategory}: {assets.Count} 个资源, 总大小 {Fmt(totalSize)}", EditorStyles.boldLabel);

                    var subCategories = assets.GroupBy(a => a.Type).OrderByDescending(g => g.Sum(x => x.Size));
                    foreach (var sub in subCategories)
                    {
                        long subSize = sub.Sum(a => a.Size);
                        string subKey = $"{selectedCategory}_{sub.Key}";
                        if (!folderFoldouts.ContainsKey(subKey))
                            folderFoldouts[subKey] = false;

                        bool isExpanded = folderFoldouts[subKey];

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label($"  📄 {sub.Key}: {sub.Count()} 个 - {Fmt(subSize)}", EditorStyles.miniBoldLabel, GUILayout.Width(300));
                        if (GUILayout.Button(isExpanded ? "▼ 隐藏" : "▶ 显示", EditorStyles.miniLabel, GUILayout.Width(60)))
                        {
                            folderFoldouts[subKey] = !folderFoldouts[subKey];
                        }
                        EditorGUILayout.EndHorizontal();

                        if (isExpanded)
                        {
                            foreach (var a in sub.OrderByDescending(x => x.Size).Take(50))
                            {
                                string icon = a.IsCloud ? "☁️" : "📄";
                                GUILayout.Label($"      {icon} {a.Path,-50} [{Fmt(a.Size)}]", EditorStyles.miniLabel);
                            }
                            if (sub.Count() > 50)
                                GUILayout.Label($"      ... 还有 {sub.Count() - 50} 个", EditorStyles.miniLabel);
                        }
                    }
                }
            }
        }
        EditorGUILayout.EndVertical();

        if (cloudStats.CustomDetail.Count > 0)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("📂 Custom 细分:", EditorStyles.boldLabel);
            foreach (var kv in cloudStats.CustomDetail.OrderByDescending(x => x.Value))
            {
                float pct = cloudStats.Total > 0 ? (float)kv.Value / cloudStats.Total * 100 : 0;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"    {kv.Key,-12}", GUILayout.Width(120));
                GUILayout.Label(Fmt(kv.Value), GUILayout.Width(100));
                Rect r = GUILayoutUtility.GetRect(100, 16);
                EditorGUI.ProgressBar(r, pct / 100f, $"{pct:F1}%");
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        DrawSeparator();

        // 按类型分组
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📄 按类型分组:", EditorStyles.boldLabel);

        var allCloudAssets = new List<AssetInfo>();
        foreach (var list in cloudStats.CategoryAssets.Values)
            allCloudAssets.AddRange(list);

        var typeGroups = allCloudAssets.GroupBy(a => a.Type)
            .OrderByDescending(g => g.Sum(x => x.Size))
            .ToDictionary(g => g.Key, g => g.ToList());

        if (typeGroups.Count > 0)
        {
            var typeNames = typeGroups.Keys.ToList();
            var options = new List<string> { "📂 全部" };
            options.AddRange(typeNames);
            cloudTypeOptions = options.ToArray();

            if (selectedCloudTypeIndex >= cloudTypeOptions.Length)
                selectedCloudTypeIndex = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("选择类型:", GUILayout.Width(60));
            selectedCloudTypeIndex = EditorGUILayout.Popup(selectedCloudTypeIndex, cloudTypeOptions, GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            DrawSeparator();

            string selectedTypeName = selectedCloudTypeIndex == 0 ? "全部" : cloudTypeOptions[selectedCloudTypeIndex];

            if (selectedTypeName == "全部")
            {
                foreach (var kv in typeGroups)
                {
                    long groupSize = kv.Value.Sum(a => a.Size);
                    string typeKey = "type_cloud_" + kv.Key;
                    if (!folderFoldouts.ContainsKey(typeKey))
                        folderFoldouts[typeKey] = false;

                    bool isExpanded = folderFoldouts[typeKey];
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(isExpanded ? "▼" : "▶", EditorStyles.miniLabel, GUILayout.Width(25)))
                    {
                        folderFoldouts[typeKey] = !folderFoldouts[typeKey];
                    }
                    GUILayout.Label($"📄 {kv.Key} ({kv.Value.Count} 个) - {Fmt(groupSize)}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.EndHorizontal();

                    if (isExpanded)
                    {
                        foreach (var a in kv.Value.OrderByDescending(x => x.Size).Take(50))
                        {
                            string icon = a.IsCloud ? "☁️" : "📄";
                            GUILayout.Label($"    {icon} {a.Path,-50} [{Fmt(a.Size)}]", EditorStyles.miniLabel);
                        }
                        if (kv.Value.Count > 50)
                            GUILayout.Label($"    ... 还有 {kv.Value.Count - 50} 个", EditorStyles.miniLabel);
                    }
                }
            }
            else
            {
                if (typeGroups.ContainsKey(selectedTypeName))
                {
                    var items = typeGroups[selectedTypeName];
                    long totalSize = items.Sum(a => a.Size);
                    EditorGUILayout.LabelField($"📄 {selectedTypeName}: {items.Count} 个资源, 总大小 {Fmt(totalSize)}", EditorStyles.boldLabel);
                    foreach (var a in items.OrderByDescending(x => x.Size).Take(100))
                    {
                        string icon = a.IsCloud ? "☁️" : "📄";
                        GUILayout.Label($"  {icon} {a.Path,-50} [{Fmt(a.Size)}]", EditorStyles.miniLabel);
                    }
                    if (items.Count > 100)
                        GUILayout.Label($"  ... 还有 {items.Count - 100} 个", EditorStyles.miniLabel);
                }
            }
        }
        EditorGUILayout.EndVertical();

        DrawSeparator();

        string cloudStr = GetCloudStatsString();
        cloudScrollPos = EditorGUILayout.BeginScrollView(cloudScrollPos, GUILayout.Height(80));
        EditorGUILayout.TextArea(cloudStr, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        DrawSeparator();

        EditorGUILayout.LabelField("📦 AB包明细:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📂 展开全部", GUILayout.Width(100)))
        {
            foreach (var key in bundleFoldouts.Keys.ToList())
                bundleFoldouts[key] = true;
        }
        if (GUILayout.Button("📂 折叠全部", GUILayout.Width(100)))
        {
            foreach (var key in bundleFoldouts.Keys.ToList())
                bundleFoldouts[key] = false;
        }
        GUILayout.Label($"共 {cloudStats.Bundles.Count} 个", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        cloudScrollPos = EditorGUILayout.BeginScrollView(cloudScrollPos, GUILayout.Height(250));

        foreach (var b in cloudStats.Bundles.OrderByDescending(x => x.Size).Take(100))
        {
            string cloudKey = b.Name + "_cloud";
            if (!bundleFoldouts.ContainsKey(cloudKey))
                bundleFoldouts[cloudKey] = false;

            EditorGUILayout.BeginHorizontal();
            bool isExpanded = bundleFoldouts[cloudKey];
            string foldIcon = isExpanded ? "▼" : "▶";
            if (GUILayout.Button($"{foldIcon} {b.Name,-50}", EditorStyles.miniLabel, GUILayout.Width(400)))
            {
                bundleFoldouts[cloudKey] = !bundleFoldouts[cloudKey];
            }
            GUI.color = b.Size > 10 * 1024 * 1024 ? Color.red : b.Size > 5 * 1024 * 1024 ? Color.yellow : Color.white;
            GUILayout.Label(Fmt(b.Size), GUILayout.Width(100));
            GUI.color = Color.white;
            GUILayout.Label($"资源: {b.AssetCount}", GUILayout.Width(80));
            if (b.IsCloud)
                GUILayout.Label("☁️", GUILayout.Width(25));
            EditorGUILayout.EndHorizontal();

            if (isExpanded && b.Assets.Count > 0)
            {
                var typeGroupsLocal = b.Assets.GroupBy(a => a.Type).OrderByDescending(g => g.Sum(x => x.Size));
                foreach (var tg in typeGroupsLocal)
                {
                    long tgSize = tg.Sum(a => a.Size);
                    GUILayout.Label($"    📄 {tg.Key}: {tg.Count()} 个 - {Fmt(tgSize)}", EditorStyles.miniBoldLabel);
                    foreach (var a in tg.OrderByDescending(x => x.Size).Take(20))
                    {
                        string icon = a.IsCloud ? "☁️" : "📄";
                        GUILayout.Label($"      {icon} {a.Path,-45} [{Fmt(a.Size)}]", EditorStyles.miniLabel);
                    }
                    if (tg.Count() > 20)
                        GUILayout.Label($"        ... 还有 {tg.Count() - 20} 个", EditorStyles.miniLabel);
                }
                if (b.Assets.Count > 100)
                    GUILayout.Label($"      ... 还有 {b.Assets.Count - 100} 个", EditorStyles.miniLabel);
            }
        }
        if (cloudStats.Bundles.Count > 100)
            GUILayout.Label($"... 还有 {cloudStats.Bundles.Count - 100} 个", EditorStyles.miniLabel);

        EditorGUILayout.EndScrollView();
    }

    // ======================== 实时分析核心方法 ========================

    /// <summary>
    /// 获取 Unity 导入后的真实资源大小
    /// </summary>
    private long GetImportedAssetSize(string assetPath)
    {
        // 检查缓存
        if (assetSizeCache.ContainsKey(assetPath))
            return assetSizeCache[assetPath];

        long size = 0;
        try
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                if (importer is TextureImporter textureImporter)
                {
                    // 获取纹理在内存中的大小
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (tex != null)
                    {
                        // 获取平台纹理格式
                        TextureImporterPlatformSettings platformSettings = textureImporter.GetPlatformTextureSettings("Standalone");
                        TextureImporterFormat format = platformSettings.format;

                        // 如果格式未设置或者是 Automatic，使用默认格式
                        if (format == TextureImporterFormat.Automatic)
                        {
                            // 根据纹理类型选择默认格式
                            if (textureImporter.textureType == TextureImporterType.Sprite ||
                                textureImporter.textureType == TextureImporterType.Default)
                            {
                                // 检查是否有 Alpha 通道
                                bool hasAlpha = textureImporter.DoesSourceTextureHaveAlpha();
                                if (hasAlpha)
                                    format = TextureImporterFormat.RGBA32;
                                else
                                    format = TextureImporterFormat.RGB24;
                            }
                            else if (textureImporter.textureType == TextureImporterType.NormalMap)
                            {
                                format = TextureImporterFormat.RGBA32;
                            }
                            else
                            {
                                format = TextureImporterFormat.RGBA32;
                            }
                        }

                        // 计算每像素字节数
                        float bytesPerPixel = GetBytesPerPixel(format);

                        // 计算纹理大小: 宽度 * 高度 * 每像素字节数
                        size = (long)(tex.width * tex.height * bytesPerPixel);

                        // 如果有 mipmap，增加约 33%
                        if (textureImporter.mipmapEnabled)
                            size = (long)(size * 1.33f);

                        // 如果是 Crunch 压缩，进一步压缩 (约50%)
                        if (format == TextureImporterFormat.DXT1Crunched ||
                            format == TextureImporterFormat.DXT5Crunched)
                        {
                            size = (long)(size * 0.5f);
                        }
                    }
                    else
                    {
                        // 如果无法加载纹理，使用文件大小
                        string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                        if (File.Exists(fullPath))
                            size = new FileInfo(fullPath).Length;
                    }
                }
                else if (importer is AudioImporter)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                    if (clip != null)
                    {
                        // 估算音频大小: 时长 * 采样率 * 声道 * 2字节
                        size = (long)(clip.length * 44100 * 2 * 2);
                    }
                    else
                    {
                        string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                        if (File.Exists(fullPath))
                            size = new FileInfo(fullPath).Length;
                    }
                }
                else if (importer is ModelImporter)
                {
                    string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                    if (File.Exists(fullPath))
                        size = new FileInfo(fullPath).Length;
                }
                else
                {
                    string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                    if (File.Exists(fullPath))
                        size = new FileInfo(fullPath).Length;
                }
            }
            else
            {
                string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                if (File.Exists(fullPath))
                    size = new FileInfo(fullPath).Length;
            }
        }
        catch
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                if (File.Exists(fullPath))
                    size = new FileInfo(fullPath).Length;
            }
            catch { size = 0; }
        }

        if (size == 0)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                if (File.Exists(fullPath))
                    size = new FileInfo(fullPath).Length;
            }
            catch { size = 0; }
        }

        assetSizeCache[assetPath] = size;
        return size;
    }

    /// <summary>
    /// 获取纹理格式的每像素字节数
    /// </summary>
    /// <summary>
    /// 获取纹理格式的每像素字节数 - Unity 2022.3 兼容版
    /// </summary>
    private float GetBytesPerPixel(TextureImporterFormat format)
    {
        switch (format)
        {
            // RGBA 格式
            case TextureImporterFormat.RGBA32:
                return 4f;
            case TextureImporterFormat.RGB24:
                return 3f;
            case TextureImporterFormat.Alpha8:
                return 1f;
            case TextureImporterFormat.R16:
                return 2f;
            case TextureImporterFormat.RG16:
                return 4f;
            case TextureImporterFormat.RGBA16:
                return 8f;
            case TextureImporterFormat.RGBAFloat:
                return 16f;
            case TextureImporterFormat.RGBAHalf:
                return 8f;
            case TextureImporterFormat.RGFloat:
                return 8f;
            case TextureImporterFormat.RFloat:
                return 4f;

            // DXT 压缩格式
            case TextureImporterFormat.DXT1:
            case TextureImporterFormat.DXT1Crunched:
                return 0.5f;
            case TextureImporterFormat.DXT5:
            case TextureImporterFormat.DXT5Crunched:
                return 1f;

            // ASTC 压缩格式
            case TextureImporterFormat.ASTC_4x4:
                return 1f;
            case TextureImporterFormat.ASTC_5x5:
                return 0.64f;
            case TextureImporterFormat.ASTC_6x6:
                return 0.44f;
            case TextureImporterFormat.ASTC_8x8:
                return 0.25f;
            case TextureImporterFormat.ASTC_10x10:
                return 0.16f;
            case TextureImporterFormat.ASTC_12x12:
                return 0.11f;

            // ETC 压缩格式 (合并重复)
            case TextureImporterFormat.ETC_RGB4:
            case TextureImporterFormat.ETC2_RGB4:
                return 0.5f;
            case TextureImporterFormat.ETC2_RGBA8:
                return 1f;

            // PVRTC 压缩格式
            case TextureImporterFormat.PVRTC_RGB2:
            case TextureImporterFormat.PVRTC_RGBA2:
                return 0.25f;
            case TextureImporterFormat.PVRTC_RGB4:
            case TextureImporterFormat.PVRTC_RGBA4:
                return 0.5f;

            //// ATC 压缩格式 (Android)
            //case TextureImporterFormat.ETC_RGB4:
            //    return 0.5f;
            //case TextureImporterFormat.ETC2_RGBA8:
            //    return 1f;

            // EAC 格式
            case TextureImporterFormat.EAC_R:
            case TextureImporterFormat.EAC_R_SIGNED:
                return 0.5f;
            case TextureImporterFormat.EAC_RG:
            case TextureImporterFormat.EAC_RG_SIGNED:
                return 1f;

            // 默认
            default:
                return 4f;
        }
    }

    /// <summary>
    /// 获取资源类型
    /// </summary>
    private string GetAssetTypeWithCache(string path)
    {
        if (assetTypeCache.ContainsKey(path))
            return assetTypeCache[path];

        string type = GetAssetType(path);
        assetTypeCache[path] = type;
        return type;
    }

    /// <summary>
    /// 实时分析所有资源
    /// </summary>
    private void AnalyzeSourceFilesRealTime()
    {
        if (isScanningSource) return;
        isScanningSource = true;

        try
        {
            sourceFiles.Clear();
            sourceTypeStats.Clear();

            string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            int total = guids.Length;
            int processed = 0;

            AssetDatabase.Refresh();

            foreach (string guid in guids)
            {
                processed++;
                if (processed % 50 == 0)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("扫描资源 (实时)",
                        $"处理中 {processed}/{total}\n获取 Unity 导入后大小", (float)processed / total))
                    {
                        break;
                    }
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                if (path.StartsWith("Assets/Editor") || path.StartsWith("Assets/Gizmos") || path.StartsWith("Assets/Plugins"))
                    continue;

                long importedSize = GetImportedAssetSize(path);

                long fileSize = 0;
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                if (File.Exists(fullPath))
                    fileSize = new FileInfo(fullPath).Length;

                string type = GetAssetTypeWithCache(path);

                long size = importedSize > 0 ? importedSize : fileSize;

                sourceFiles.Add(new SourceFileInfo
                {
                    Path = path,
                    Type = type,
                    Folder = Path.GetDirectoryName(path),
                    Size = size,
                    FileSize = fileSize,
                    IsImported = importedSize > 0
                });

                if (!sourceTypeStats.ContainsKey(type)) sourceTypeStats[type] = 0;
                sourceTypeStats[type] += size;
            }

            EditorUtility.ClearProgressBar();

            sourceAnalyzed = true;
            lastSourceScanTime = DateTime.Now;
            status = $"✅ 扫描完成: {sourceFiles.Count} 个文件 (使用 Unity 导入后大小)";
            Z_Logger.Log($"✅ 实时分析完成: {sourceFiles.Count} 个文件");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            status = $"❌ 扫描失败: {e.Message}";
            Z_Logger.LogError(e);
        }
        finally
        {
            isScanningSource = false;
        }
    }

    /// <summary>
    /// 实时分析选中文件夹
    /// </summary>
    private void AnalyzeSelectedFolderRealTime()
    {
        if (isScanningSource) return;

        var sel = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
        if (sel == null || sel.Length == 0 || !Directory.Exists(AssetDatabase.GetAssetPath(sel[0])))
        {
            EditorUtility.DisplayDialog("提示", "请在 Project 中选中一个文件夹", "确定");
            return;
        }

        isScanningSource = true;
        try
        {
            string target = AssetDatabase.GetAssetPath(sel[0]);
            sourceFiles.Clear();
            sourceTypeStats.Clear();

            string[] guids = AssetDatabase.FindAssets("", new[] { target });
            int total = guids.Length;
            int processed = 0;

            AssetDatabase.Refresh();

            foreach (string guid in guids)
            {
                processed++;
                if (processed % 50 == 0)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("扫描文件夹 (实时)",
                        $"处理中 {processed}/{total}", (float)processed / total))
                    {
                        break;
                    }
                }

                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;

                long importedSize = GetImportedAssetSize(path);
                long fileSize = 0;
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                if (File.Exists(fullPath))
                    fileSize = new FileInfo(fullPath).Length;

                string type = GetAssetTypeWithCache(path);
                long size = importedSize > 0 ? importedSize : fileSize;

                sourceFiles.Add(new SourceFileInfo
                {
                    Path = path,
                    Type = type,
                    Folder = Path.GetDirectoryName(path),
                    Size = size,
                    FileSize = fileSize,
                    IsImported = importedSize > 0
                });

                if (!sourceTypeStats.ContainsKey(type)) sourceTypeStats[type] = 0;
                sourceTypeStats[type] += size;
            }

            EditorUtility.ClearProgressBar();

            sourceAnalyzed = true;
            lastSourceScanTime = DateTime.Now;
            status = $"✅ 扫描完成: {sourceFiles.Count} 个文件";
            Z_Logger.Log($"✅ 分析完成: {sourceFiles.Count} 个文件");
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            status = $"❌ 扫描失败: {e.Message}";
            Z_Logger.LogError(e);
        }
        finally
        {
            isScanningSource = false;
        }
    }

    /// <summary>
    /// 强制重新导入并分析
    /// </summary>
    private void ForceReimportAndAnalyze()
    {
        if (EditorUtility.DisplayDialog("强制刷新",
            "将重新导入所有资源并重新分析，这可能需要一些时间。\n确定要继续吗？",
            "确定", "取消"))
        {
            try
            {
                EditorUtility.DisplayProgressBar("强制刷新", "重新导入资源...", 0.5f);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();

                assetSizeCache.Clear();
                assetTypeCache.Clear();
                AnalyzeSourceFilesRealTime();

                EditorUtility.DisplayDialog("完成", "资源已重新导入并分析完成", "确定");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("错误", $"刷新失败: {e.Message}", "确定");
            }
        }
    }

    // ======================== 核心解析 (AB包) ========================

    private void ParseBundles(string path)
    {
        isParsing = true;
        bundles.Clear();
        allAssets.Clear();
        typeStats.Clear();
        bundleFoldouts.Clear();
        status = "⏳ 解析中...";

        try
        {
            string json = File.ReadAllText(path);

            ExtractExplicitAssets(json);
            ExtractDataFromOtherAsset(json);
            ExtractAssetsDirectly(json);

            if (bundles.Count == 0)
            {
                var b = new BundleInfo { Name = "defaultlocalgroup_assets_all", Size = 0, AssetCount = 0 };
                bundles.Add(b);
            }

            ExtractCloudAssets(json);

            foreach (var b in bundles)
            {
                b.AssetCount = b.Assets.Count;
                b.Size = b.Assets.Sum(a => a.Size);
                b.Category = GetCategory(b.Name);
                b.BundleType = GetBundleType(b.Name);

                foreach (var a in b.Assets)
                {
                    a.BundleType = b.BundleType;
                    if (!typeStats.ContainsKey(a.Type)) typeStats[a.Type] = 0;
                    typeStats[a.Type] += a.Size;
                    allAssets.Add(a);
                }
            }

            bundles = bundles.OrderByDescending(b => b.Size).ToList();

            long totalSize = bundles.Sum(b => b.Size);
            status = $"✅ 解析完成: {bundles.Count} 个AB包, {allAssets.Count} 个资源, {Fmt(totalSize)}";

            Z_Logger.Log($"✅ AB包解析完成: {bundles.Count} 个，总大小 {Fmt(totalSize)}");
        }
        catch (Exception e)
        {
            status = $"❌ 解析失败: {e.Message}";
            Z_Logger.LogError(e);
        }
        finally { isParsing = false; }
    }

    private void ExtractExplicitAssets(string json)
    {
        int pos = 0;
        while (pos < json.Length)
        {
            int start = json.IndexOf("\"ExplicitAsset\"", pos);
            if (start < 0) break;

            int objStart = json.IndexOf("{", start);
            if (objStart < 0) break;
            int objEnd = FindBrace(json, objStart);
            if (objEnd < 0) break;

            string obj = json.Substring(objStart, objEnd - objStart + 1);
            int dataStart = obj.IndexOf("\"data\"");
            if (dataStart > 0)
            {
                int dStart = obj.IndexOf("{", dataStart);
                if (dStart > 0)
                {
                    int dEnd = FindBrace(obj, dStart);
                    if (dEnd > 0)
                    {
                        string data = obj.Substring(dStart, dEnd - dStart + 1);
                        string path = GetStr(data, "AssetPath");
                        if (!string.IsNullOrEmpty(path))
                        {
                            long size = GetLong(data, "SerializedSize");
                            string type = GetTypeName((int)GetLong(data, "MainAssetType"));
                            if (string.IsNullOrEmpty(type) || type == "Type_0") type = GetAssetType(path);

                            string bundleName = GetBundleName(obj, json);
                            var bundle = GetOrCreateBundle(bundleName);

                            bundle.Assets.Add(new AssetInfo
                            {
                                Path = System.IO.Path.GetFileName(path),
                                Folder = System.IO.Path.GetDirectoryName(path),
                                Type = type,
                                Size = size,
                                BundleName = bundleName,
                                Guid = GetStr(data, "Guid")
                            });
                        }
                    }
                }
            }
            pos = objEnd + 1;
        }
    }

    private void ExtractDataFromOtherAsset(string json)
    {
        int pos = 0;
        while (pos < json.Length)
        {
            int start = json.IndexOf("\"DataFromOtherAsset\"", pos);
            if (start < 0) break;

            int objStart = json.IndexOf("{", start);
            if (objStart < 0) break;
            int objEnd = FindBrace(json, objStart);
            if (objEnd < 0) break;

            string obj = json.Substring(objStart, objEnd - objStart + 1);
            string path = GetStr(obj, "AssetPath");
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/"))
            {
                long size = GetLong(obj, "SerializedSize");
                string type = GetAssetType(path);
                var bundle = GetOrCreateBundle("defaultlocalgroup_assets_all");

                bundle.Assets.Add(new AssetInfo
                {
                    Path = System.IO.Path.GetFileName(path),
                    Folder = System.IO.Path.GetDirectoryName(path),
                    Type = type,
                    Size = size,
                    BundleName = "defaultlocalgroup_assets_all"
                });
            }
            pos = objEnd + 1;
        }
    }

    private void ExtractAssetsDirectly(string json)
    {
        int pos = 0;
        while (pos < json.Length)
        {
            int start = json.IndexOf("\"AssetPath\"", pos);
            if (start < 0) break;

            int objStart = json.LastIndexOf("{", start);
            if (objStart < 0) break;
            int objEnd = FindBrace(json, objStart);
            if (objEnd < 0) break;

            string obj = json.Substring(objStart, objEnd - objStart + 1);
            string path = GetStr(obj, "AssetPath");
            if (!string.IsNullOrEmpty(path))
            {
                long size = GetLong(obj, "SerializedSize");
                if (size == 0) size = GetLong(obj, "Size");
                string type = GetTypeName((int)GetLong(obj, "MainAssetType"));
                if (string.IsNullOrEmpty(type) || type == "Type_0") type = GetAssetType(path);

                string bundleName = GetBundleName(obj, json);
                var bundle = GetOrCreateBundle(bundleName);

                bundle.Assets.Add(new AssetInfo
                {
                    Path = System.IO.Path.GetFileName(path),
                    Folder = System.IO.Path.GetDirectoryName(path),
                    Type = type,
                    Size = size,
                    BundleName = bundleName
                });
            }
            pos = objEnd + 1;
        }
    }

    private void ExtractCloudAssets(string json)
    {
        int pos = 0;
        while (pos < json.Length)
        {
            int start = json.IndexOf("\"CatalogLoadPaths\"", pos);
            if (start < 0) start = json.IndexOf("\"RemoteCatalogBuildPath\"", pos);
            if (start < 0) break;

            int arrStart = json.IndexOf("[", start);
            if (arrStart > 0)
            {
                int arrEnd = FindBracket(json, arrStart);
                if (arrEnd > 0)
                {
                    string urls = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                    int urlPos = 0;
                    while (urlPos < urls.Length)
                    {
                        int urlStart = urls.IndexOf("\"", urlPos);
                        if (urlStart < 0) break;
                        int urlEnd = urls.IndexOf("\"", urlStart + 1);
                        if (urlEnd < 0) break;
                        string url = urls.Substring(urlStart + 1, urlEnd - urlStart - 1);
                        if (!string.IsNullOrEmpty(url) && (url.StartsWith("http") || url.StartsWith("https")))
                        {
                            foreach (var b in bundles)
                            {
                                b.IsCloud = true;
                                b.CloudUrl = url;
                                foreach (var a in b.Assets)
                                {
                                    a.IsCloud = true;
                                    a.CloudUrl = url;
                                }
                            }
                        }
                        urlPos = urlEnd + 1;
                    }
                }
            }
            pos = start + 1;
        }
    }

    private int FindBracket(string text, int start)
    {
        int depth = 0;
        bool inString = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                inString = !inString;
            if (inString) continue;
            if (c == '[') depth++;
            else if (c == ']')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private BundleInfo GetOrCreateBundle(string name)
    {
        if (string.IsNullOrEmpty(name)) name = "defaultlocalgroup_assets_all";
        var b = bundles.FirstOrDefault(x => x.Name == name);
        if (b == null)
        {
            b = new BundleInfo { Name = name, Size = 0, AssetCount = 0 };
            bundles.Add(b);
        }
        return b;
    }

    private string GetBundleName(string data, string json)
    {
        int start = data.IndexOf("\"Bundle\"");
        if (start > 0)
        {
            int ridStart = data.IndexOf("{", start);
            if (ridStart > 0)
            {
                int ridEnd = FindBrace(data, ridStart);
                if (ridEnd > 0)
                {
                    string ridObj = data.Substring(ridStart, ridEnd - ridStart + 1);
                    string rid = GetStr(ridObj, "rid");
                    if (!string.IsNullOrEmpty(rid))
                    {
                        string bData = FindDataByRid(json, rid);
                        if (!string.IsNullOrEmpty(bData))
                        {
                            string name = GetStr(bData, "Name");
                            if (!string.IsNullOrEmpty(name)) return name;
                        }
                    }
                }
            }
        }
        return "defaultlocalgroup_assets_all";
    }

    private string FindDataByRid(string json, string rid)
    {
        string key = $"\"rid\":{rid}";
        int pos = json.IndexOf(key);
        if (pos < 0) return "";

        int objStart = json.LastIndexOf("{", pos);
        if (objStart < 0) return "";
        int objEnd = FindBrace(json, objStart);
        if (objEnd < 0) return "";

        return json.Substring(objStart, objEnd - objStart + 1);
    }

    private string GetCategory(string name)
    {
        string n = name.ToLower();
        if (n.Contains("scene")) return "场景";
        if (n.Contains("texture") || n.Contains("sprite")) return "纹理";
        if (n.Contains("audio")) return "音频";
        if (n.Contains("mesh")) return "模型";
        if (n.Contains("anim")) return "动画";
        if (n.Contains("font")) return "字体";
        if (n.Contains("shader")) return "Shader";
        if (n.Contains("prefab")) return "预制体";
        if (n.Contains("atlas")) return "图集";
        if (n.Contains("json")) return "数据";
        if (n.Contains("ui") || n.Contains("icon")) return "UI";
        return "其他";
    }

    private string GetBundleType(string name)
    {
        string n = name.ToLower();
        if (n.Contains("scene")) return "scene";
        if (n.Contains("texture") || n.Contains("sprite")) return "texture";
        if (n.Contains("audio")) return "audio";
        if (n.Contains("mesh")) return "mesh";
        if (n.Contains("anim")) return "animation";
        if (n.Contains("font")) return "font";
        if (n.Contains("shader")) return "shader";
        if (n.Contains("prefab")) return "prefab";
        if (n.Contains("atlas")) return "atlas";
        if (n.Contains("json")) return "data";
        return "custom";
    }

    // ======================== Cloud统计 ========================

    private void ParseCloudStats(string path)
    {
        ParseBundles(path);
        if (bundles.Count == 0) return;

        cloudStats = new CloudStats();
        cloudStats.Categories = new Dictionary<string, long>();
        cloudStats.CustomDetail = new Dictionary<string, long>();
        cloudStats.CategoryAssets = new Dictionary<string, List<AssetInfo>>();
        cloudStats.TypeAssets = new Dictionary<string, List<AssetInfo>>();
        cloudStats.Bundles = bundles.ToList();
        cloudStats.TotalAssets = bundles.Sum(b => b.AssetCount);

        foreach (var b in bundles)
        {
            long size = b.Size;
            if (size == 0) size = b.Assets.Sum(a => a.Size);

            string category = GetCategory(b.Name);

            if (!cloudStats.Categories.ContainsKey(category))
            {
                cloudStats.Categories[category] = 0;
                cloudStats.CategoryAssets[category] = new List<AssetInfo>();
            }
            cloudStats.Categories[category] += size;
            cloudStats.CategoryAssets[category].AddRange(b.Assets);

            foreach (var a in b.Assets)
            {
                if (!cloudStats.TypeAssets.ContainsKey(a.Type))
                    cloudStats.TypeAssets[a.Type] = new List<AssetInfo>();
                cloudStats.TypeAssets[a.Type].Add(a);
            }

            if (category == "其他" || category == "数据")
            {
                string sub = "其他";
                if (b.Name.ToLower().Contains("shader")) sub = "Shader";
                else if (b.Name.ToLower().Contains("material")) sub = "材质";
                else if (b.Name.ToLower().Contains("scriptable")) sub = "ScriptableObject";
                else if (b.Name.ToLower().Contains("prefab")) sub = "预制体";
                else if (b.Name.ToLower().Contains("atlas")) sub = "图集";
                else if (b.Name.ToLower().Contains("json")) sub = "JSON";
                else if (b.Name.ToLower().Contains("ui") || b.Name.ToLower().Contains("icon")) sub = "UI图标";

                if (!cloudStats.CustomDetail.ContainsKey(sub))
                    cloudStats.CustomDetail[sub] = 0;
                cloudStats.CustomDetail[sub] += size;
            }

            cloudStats.Total += size;
        }
    }

    private string GetCloudDetailLine()
    {
        string detail = $"Cloud Assets: {Fmt(cloudStats.Total)}  (";

        int count = 0;
        foreach (var kv in cloudStats.Categories.OrderByDescending(x => x.Value))
        {
            if (count > 0) detail += "    ";
            detail += $"{kv.Key}: {Fmt(kv.Value)}";
            count++;
        }
        detail += ")";
        return detail;
    }

    private string GetCloudStatsString()
    {
        var lines = new List<string>();
        lines.Add(GetCloudDetailLine());
        return string.Join("\n", lines);
    }

    private void ExportCloudReport()
    {
        string path = EditorUtility.SaveFilePanel("导出报告", "", "Cloud报告.txt", "txt");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var lines = new List<string>();
            lines.Add("=== Cloud Assets 统计报告 ===");
            lines.Add($"生成时间: {DateTime.Now}");
            lines.Add($"总大小: {Fmt(cloudStats.Total)}");
            lines.Add($"总资源数: {cloudStats.TotalAssets}");
            lines.Add($"AB包数: {cloudStats.Bundles.Count}");
            lines.Add("");
            lines.Add("--- 分类统计 ---");
            foreach (var kv in cloudStats.Categories.OrderByDescending(x => x.Value))
            {
                float pct = cloudStats.Total > 0 ? (float)kv.Value / cloudStats.Total * 100 : 0;
                lines.Add($"{kv.Key,-12}: {Fmt(kv.Value),10} ({pct:F1}%)");
            }
            if (cloudStats.CustomDetail.Count > 0)
            {
                lines.Add("");
                lines.Add("--- Custom 细分 ---");
                foreach (var kv in cloudStats.CustomDetail.OrderByDescending(x => x.Value))
                {
                    float pct = cloudStats.Total > 0 ? (float)kv.Value / cloudStats.Total * 100 : 0;
                    lines.Add($"{kv.Key,-12}: {Fmt(kv.Value),10} ({pct:F1}%)");
                }
            }
            lines.Add("");
            lines.Add("--- AB包明细 (Top 50) ---");
            foreach (var b in cloudStats.Bundles.OrderByDescending(x => x.Size).Take(50))
            {
                string typeIcon = b.BundleType == "scene" ? "🎬" :
                                  b.BundleType == "texture" ? "🖼️" :
                                  b.BundleType == "font" ? "🔤" :
                                  b.BundleType == "audio" ? "🔊" :
                                  b.BundleType == "animation" ? "🎞️" : "📦";
                lines.Add($"{typeIcon} {b.Name,-50} {Fmt(b.Size),10} [{b.AssetCount}个资源]");
            }
            File.WriteAllText(path, string.Join("\n", lines));
            ShowMsg($"✅ 已导出: {path}");
        }
        catch (Exception e) { ShowMsg($"❌ 导出失败: {e.Message}"); }
    }

    // ======================== 导出 ========================

    private void ExportToCSV()
    {
        string path = EditorUtility.SaveFilePanel("导出CSV", "", "AB包分析报告.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("Bundle名称,资源数,大小(MB),类型分布");
                foreach (var b in bundles.OrderByDescending(x => x.Size))
                {
                    string types = string.Join("; ", b.TypeStats.OrderByDescending(x => x.Value)
                        .Select(x => $"{x.Key}:{Fmt(x.Value)}"));
                    sw.WriteLine($"\"{b.Name}\",{b.AssetCount},{b.Size / (1024f * 1024f):F2},\"{types}\"");
                }
                sw.WriteLine();
                sw.WriteLine($"总AB包数,{bundles.Count},总大小,{Fmt(bundles.Sum(b => b.Size))}");
                sw.WriteLine($"总资源数,{bundles.Sum(b => b.AssetCount)}");
            }
            ShowMsg($"✅ 已导出: {path}");
        }
        catch (Exception e) { ShowMsg($"❌ 导出失败: {e.Message}"); }
    }

    // ======================== 工具函数 ========================

    private int FindBrace(string text, int start)
    {
        int depth = 0;
        bool inString = false;
        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                inString = !inString;
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private string GetStr(string json, string key)
    {
        string k = $"\"{key}\"";
        int s = json.IndexOf(k);
        if (s < 0) return "";
        int c = json.IndexOf(":", s);
        if (c < 0) return "";
        int v = c + 1;
        while (v < json.Length && (json[v] == ' ' || json[v] == '\t' || json[v] == '\n' || json[v] == '\r')) v++;
        if (v >= json.Length) return "";
        if (json[v] != '"') return "";
        int e = json.IndexOf('"', v + 1);
        if (e < 0) return "";
        return json.Substring(v + 1, e - v - 1);
    }

    private long GetLong(string json, string key)
    {
        string k = $"\"{key}\"";
        int s = json.IndexOf(k);
        if (s < 0) return 0;
        int c = json.IndexOf(":", s);
        if (c < 0) return 0;
        int v = c + 1;
        while (v < json.Length && (json[v] == ' ' || json[v] == '\t' || json[v] == '\n' || json[v] == '\r')) v++;
        if (v >= json.Length) return 0;
        int e = v;
        while (e < json.Length && json[e] != ',' && json[e] != '}' && json[e] != ']' && json[e] != ' ' && json[e] != '\n' && json[e] != '\r')
            e++;
        string num = json.Substring(v, e - v).Trim().Trim('"');
        if (string.IsNullOrEmpty(num)) return 0;
        long.TryParse(num, out long result);
        return result;
    }

    private string GetAssetType(string path)
    {
        if (assetTypeCache.ContainsKey(path))
            return assetTypeCache[path];

        string result = "Other";
        try
        {
            Type t = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (t != null)
            {
                string n = t.Name;
                if (n == "TextAsset")
                {
                    if (path.EndsWith(".json")) result = "JSON";
                    else if (path.EndsWith(".bytes")) result = "Bytes";
                    else if (path.EndsWith(".csv")) result = "CSV";
                    else if (path.EndsWith(".xml")) result = "XML";
                    else result = "TextAsset";
                }
                else if (n == "MonoScript") result = "Script";
                else if (n == "Shader") result = "Shader";
                else if (n == "Material") result = "Material";
                else if (n == "Font") result = "Font";
                else if (n == "GameObject") result = "Prefab";
                else if (n == "SceneAsset") result = "Scene";
                else if (n == "AudioClip") result = "Audio";
                else if (n == "Texture2D") result = "Texture";
                else if (n == "Sprite") result = "Sprite";
                else if (n == "AnimationClip") result = "Animation";
                else if (n == "AnimatorController") result = "AnimatorController";
                else if (n == "ScriptableObject") result = "ScriptableObject";
                else result = n;
            }
            else
            {
                string ext = Path.GetExtension(path).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".psd" || ext == ".tga") result = "Texture";
                else if (ext == ".fbx" || ext == ".obj" || ext == ".blend") result = "Model";
                else if (ext == ".wav" || ext == ".mp3" || ext == ".ogg" || ext == ".aiff") result = "Audio";
                else if (ext == ".ttf" || ext == ".otf") result = "TTF";
                else if (ext == ".json") result = "JSON";
                else if (ext == ".asset") result = "ScriptableObject";
                else if (ext == ".prefab") result = "Prefab";
                else if (ext == ".unity") result = "Scene";
                else if (ext == ".shader") result = "Shader";
                else if (ext == ".cginc" || ext == ".hlsl") result = "ShaderInclude";
                else result = "Other";
            }
        }
        catch
        {
            result = "Other";
        }

        assetTypeCache[path] = result;
        return result;
    }

    private string GetTypeName(int id)
    {
        switch (id)
        {
            case 1: return "Font";
            case 3: case 13: return "Texture";
            case 6: return "Shader";
            case 7: case 19: return "Material";
            case 8: return "Mesh";
            case 9: return "Animation";
            case 10: return "Audio";
            case 11: return "Scene";
            case 15: return "Sprite";
            case 18: return "AnimatorController";
            case 22: return "JSON";
            case 43: return "TTF";
            default: return id > 0 ? $"Type_{id}" : "Unknown";
        }
    }

    private string Fmt(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F2} MB";
        if (bytes >= 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes} B";
    }

    private void ShowMsg(string msg) => EditorUtility.DisplayDialog("提示", msg, "确定");
}
#endif
