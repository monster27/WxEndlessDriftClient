using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AAEditor
{
    public class AAutoPackTool : EditorWindow
    {
        private AAutoPackConfig _cfg;
        private Vector2 _scroll;
        private bool _showHelp = false;
        private Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

        private GUIStyle _leftAlignedButtonStyle;

        private Dictionary<string, int> _groupLabelIndex = new Dictionary<string, int>();
        private int _batchLabelIndex = 4;

        private readonly string[] _labelOptions = { "无标签", "首包 (Local)", "立刻预下载 (Tier1)", "空闲预下载 (Tier2)", "用到才下载 (Tier3)" };
        private readonly string[] _actualLabels = { "", "local", "tier1_preload", "tier2_idle", "tier3_ondemand" };

        private bool _isProcessing = false;
        private Queue<(string filePath, string groupName, string label)> _taskQueue = new Queue<(string, string, string)>();
        private HashSet<string> _queuedFiles = new HashSet<string>();
        private int _totalTasks = 0;
        private int _processedTasks = 0;
        private string _currentFileName = "";
        private Action _onProcessComplete;

        private const float AtlasCompressionRatio = 3.0f;
        private const float NormalCompressionRatio = 1.3f;
        private const float HealthTolerance = 1.15f;

        [MenuItem("Tools/Addressables/AA Auto Pack")]
        public static void ShowWindow()
        {
            var w = GetWindow<AAutoPackTool>("AA Auto Pack");
            w.minSize = new Vector2(1050, 950);
        }

        private void OnEnable()
        {
            _cfg = AAutoPackConfig.GetOrCreate();
            EnsureSweetSpotDefaults();
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProcessStep;
        }

        private void EnsureSweetSpotDefaults()
        {
            if (_cfg == null) return;
            bool dirty = false;

            bool isOldNormal1 = (_cfg.minBundleKB == 500 && _cfg.idealMaxKB == 4096 && _cfg.hardMaxKB == 8192);
            bool isOldNormal2 = (_cfg.minBundleKB == 512 && _cfg.idealMaxKB == 4096 && _cfg.hardMaxKB == 8192);
            if (isOldNormal1 || isOldNormal2)
            {
                _cfg.minBundleKB = 512;
                _cfg.idealMaxKB = 2048;
                _cfg.hardMaxKB = 4096;
                dirty = true;
            }

            bool isOldAtlas1 = (_cfg.atlasIdealMaxKB == 4096 && _cfg.atlasHardMaxKB == 16384);
            bool isOldAtlas2 = (_cfg.atlasIdealMaxKB == 12288 && _cfg.atlasHardMaxKB == 16384);
            if (isOldAtlas1 || isOldAtlas2 || _cfg.atlasIdealMaxKB == 0)
            {
                _cfg.atlasIdealMaxKB = 6144;
                _cfg.atlasHardMaxKB = 12288;
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(_cfg);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AA] ✅ 已应用甜区配置：普通 min={_cfg.minBundleKB}/ideal={_cfg.idealMaxKB}/hard={_cfg.hardMaxKB} KB，图集 atlasIdeal={_cfg.atlasIdealMaxKB}/atlasHard={_cfg.atlasHardMaxKB} KB");
            }
        }

        private float EstimatePackedMB(long rawBytes, bool isAtlas)
        {
            float rawMB = rawBytes / 1024f / 1024f;
            return isAtlas ? rawMB / AtlasCompressionRatio : rawMB / NormalCompressionRatio;
        }

        private void OnGUI()
        {
            if (_cfg == null) _cfg = AAutoPackConfig.GetOrCreate();

            if (_leftAlignedButtonStyle == null)
            {
                _leftAlignedButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(20, 20, 10, 10),
                    fontSize = 13,
                    wordWrap = false
                };
                _leftAlignedButtonStyle.normal.textColor = Color.white;
                _leftAlignedButtonStyle.hover.textColor = Color.white;
                _leftAlignedButtonStyle.active.textColor = Color.white;
                _leftAlignedButtonStyle.focused.textColor = Color.white;
            }

            _showHelp = EditorGUILayout.Foldout(_showHelp, "▶ 详细使用说明 (点此展开)", true, EditorStyles.foldoutHeader);
            if (_showHelp)
            {
                EditorGUILayout.HelpBox(
                    "【推荐流程】\n" +
                    "  第一次使用：点【2】重置环境 → 点【一键打资源(含图集)】\n" +
                    "  日常更新：直接点【一键打资源】\n" +
                    "  含小游戏出包：点【一键打包】或【全自动打包】\n\n" +
                    "【关于标记】\n" +
                    "  工具在【6】计算分组前，会自动将所有旧组全删掉，并强制将 Assets/Addressables 下的所有资源重新标记为 AA。\n" +
                    "  也可以手动点【4】全清和【5】全标记。\n\n" +
                    "【甜区（微信小游戏）】\n" +
                    "  普通资源：装箱 0.5~2 MB，打包后约 0.38~1.54 MB\n" +
                    "  图集：装箱 6~12 MB（未压缩），打包后约 2~4 MB\n" +
                    "  允许超出 15% 不警告\n" +
                    "  理由：单包 ≤ 2MB 弱网下载快，请求数少握手开销低\n\n" +
                    "【图集与图同组策略】\n" +
                    "  扫描所有 .spriteatlas，跳过它们引用的纹理，不作为独立 entry。\n" +
                    "  图集 bundle 天然包含 .spriteatlas + 它引用的纹理，不会重复。\n\n" +
                    "【装箱策略】\n" +
                    "  best-fit：优先塞进最满且装得下的同 category 组，其次塞进最满且装得下的任意同类型组。\n" +
                    "  兜底合并：装箱结束后，把所有 < min 的组两两合并进同类型最大可容纳组。\n\n" +
                    "【内置组提示】\n" +
                    "  group_0_assets_all = Built In Data\n" +
                    "  group_*_scenes_all = Default Local Group 的场景\n" +
                    "  如需手动拆小，在 Groups 窗口手动把 Default Local Group 的 Bundle Mode 改成 Pack Separately。",
                    MessageType.Info);
            }

            EditorGUILayout.Space(10);
            GUI.enabled = !_isProcessing;

            // ================== 辅助工具 ==================
            EditorGUILayout.LabelField("辅助工具", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);

                if (GUILayout.Button("🔍 追踪配置文件", GUILayout.Height(28)))
                {
                    if (_cfg != null)
                    {
                        Selection.activeObject = _cfg;
                        EditorGUIUtility.PingObject(_cfg);
                    }
                }

                if (GUILayout.Button("📁 打开资源根目录", GUILayout.Height(28)))
                {
                    string path = Path.Combine(Application.dataPath, "../" + _cfg.remoteBuildPath.Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString()));
                    if (Directory.Exists(path)) EditorUtility.RevealInFinder(path);
                    else Debug.LogWarning($"[AA] 未找到资源根目录: {path}，可能还未进行过打资源。");
                }

                if (GUILayout.Button("📁 打开 Build 文件夹", GUILayout.Height(28)))
                {
                    string buildPath = string.IsNullOrEmpty(_cfg.buildFolderPath) ? "Build" : _cfg.buildFolderPath;
                    if (!Path.IsPathRooted(buildPath))
                        buildPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), buildPath);
                    buildPath = buildPath.Replace('\\', '/');

                    if (Directory.Exists(buildPath))
                    {
                        try { System.Diagnostics.Process.Start("explorer.exe", "\"" + buildPath.Replace('/', '\\') + "\""); }
                        catch (Exception e) { Debug.LogError($"[AA] 打开 Build 文件夹失败: {e.Message}"); }
                    }
                    else Debug.LogWarning($"[AA] 未找到 Build 文件夹: {buildPath}");
                }
            }
            GUI.backgroundColor = Color.white;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
                if (GUILayout.Button("📊 分析打出来的资源", GUILayout.Height(28)))
                    AnalyzeBuildOutput();
                if (GUILayout.Button("⚙ 设置包大小范围", GUILayout.Height(28)))
                    BundleSizeRangeWindow.ShowWindow(_cfg);
                if (GUILayout.Button("📦 单独打包列表", GUILayout.Height(28)))
                    StandaloneListWindow.ShowWindow(_cfg);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // ================== 一、 基础环境 ==================
            EditorGUILayout.LabelField("一、 基础环境", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.9f, 0.9f, 1f);
                if (GUILayout.Button("【0】打开 Profiles 界面", GUILayout.Height(35)))
                    EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Profiles");

                GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
                if (GUILayout.Button("【1】打开 Groups 界面", GUILayout.Height(35)))
                    EditorApplication.ExecuteMenuItem("Window/Asset Management/Addressables/Groups");

                GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
                if (GUILayout.Button("【2】重置底层环境", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("重置环境", "彻底清空旧的 AA 配置？", "确定", "取消")) ResetEnvironment();
                }

                GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
                if (GUILayout.Button("【3】打图集", GUILayout.Height(35)))
                {
                    EditorUtility.DisplayDialog("打图集未实现", "此工具只做分组与打包。\n图集请在项目里手动生成，或使用专门的图集工具。", "知道了");
                    DoAtlasBuild();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            // ================== 二、 核心分组 ==================
            EditorGUILayout.LabelField("二、 核心分组与移入", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
                if (GUILayout.Button("【4】强制全清", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("强制全清", "删除所有非内置组？", "确定", "取消"))
                    {
                        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                        var groupsToDelete = new List<AddressableAssetGroup>();
                        foreach (var g in settings.groups)
                        {
                            if (g != null && g.Name != "Built In Data" && g.Name != "Default Local Group")
                                groupsToDelete.Add(g);
                        }
                        foreach (var g in groupsToDelete) settings.RemoveGroup(g);
                        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                        AssetDatabase.SaveAssets();
                    }
                }

                GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
                if (GUILayout.Button("【5】强制全标记", GUILayout.Height(35)))
                {
                    MarkAllAssetsAsAddressable();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("【6】根据大小智能自动计算组", GUILayout.Height(40)))
                    CalculateAndCreateGroups();

                GUI.backgroundColor = new Color(1f, 0.7f, 0.9f);
                if (GUILayout.Button("【7】将所有资源移入组内并打上对应标签", GUILayout.Height(40)))
                    StartAsyncProcess();
            }
            GUI.backgroundColor = Color.white;

            if (_isProcessing)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("正在处理中，请勿关闭窗口...", MessageType.Warning);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), (float)_processedTasks / _totalTasks, $"处理中: {_processedTasks}/{_totalTasks} ({_currentFileName})");
            }

            EditorGUILayout.Space(10);

            // ================== 三、 构建与打包 ==================
            EditorGUILayout.LabelField("三、 构建与打包", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(1f, 0.8f, 0.5f);
                if (GUILayout.Button("【8】单独打资源", GUILayout.Height(35)))
                    BuildContent();

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("【9】打开微信小游戏转换界面", GUILayout.Height(35)))
                    DoBuildMiniGame();
            }
            GUI.backgroundColor = Color.white;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _cfg.autoOpenBuildFolder = EditorGUILayout.ToggleLeft("打资源后自动打开文件夹", _cfg.autoOpenBuildFolder);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_cfg);
                    AssetDatabase.SaveAssets();
                }
            }

            EditorGUILayout.Space(15);

            // ================== 四、 自动化组合 ==================
            EditorGUILayout.LabelField("四、 自动化组合 (一键流程)", EditorStyles.boldLabel);

            Color autoColor = new Color(0.5f, 0.9f, 0.9f);

            GUI.backgroundColor = autoColor;
            if (GUILayout.Button("📦【一键打资源】全标记 -> 根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包", _leftAlignedButtonStyle, GUILayout.Height(56)))
            {
                if (EditorUtility.DisplayDialog("一键打资源", "将自动执行：\n1. 全标记\n2. 智能计算分组\n3. 移入打标\n4. 打资源\n(不重打图集、不跳转微信转换)", "开始", "取消"))
                {
                    CalculateAndCreateGroups();
                    StartAsyncProcess(BuildContent);
                }
            }

            GUI.backgroundColor = autoColor;
            if (GUILayout.Button("📦【一键打资源(含图集)】重打所有图集 (.spriteatlas) -> 全标记 -> 根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包", _leftAlignedButtonStyle, GUILayout.Height(56)))
            {
                if (EditorUtility.DisplayDialog("一键打资源(含图集)", "将自动执行：\n1. 重打图集\n2. 全标记\n3. 智能计算分组\n4. 移入打标\n5. 打资源\n(不跳转微信转换)", "开始", "取消"))
                {
                    DoAtlasBuild();
                    CalculateAndCreateGroups();
                    StartAsyncProcess(BuildContent);
                }
            }

            GUI.backgroundColor = autoColor;
            if (GUILayout.Button("📦【一键打包】全标记 -> 根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包 -> 打开微信小游戏转换界面", _leftAlignedButtonStyle, GUILayout.Height(56)))
            {
                if (EditorUtility.DisplayDialog("一键打包", "将自动执行：\n1. 全标记\n2. 智能计算分组\n3. 移入打标\n4. 打资源\n5. 打开微信小游戏转换界面\n(不重打图集)", "开始", "取消"))
                {
                    CalculateAndCreateGroups();
                    StartAsyncProcess(() => {
                        BuildContent();
                        DoBuildMiniGame();
                    });
                }
            }

            GUI.backgroundColor = autoColor;
            if (GUILayout.Button("🔥【全自动打包】重打所有图集 (.spriteatlas) -> 全标记 -> 根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包 -> 打开微信小游戏转换界面", _leftAlignedButtonStyle, GUILayout.Height(68)))
            {
                if (EditorUtility.DisplayDialog("全自动打包", "将自动执行所有步骤，最后打开微信小游戏转换界面。", "开始", "取消"))
                {
                    DoAtlasBuild();
                    CalculateAndCreateGroups();
                    StartAsyncProcess(() => {
                        BuildContent();
                        DoBuildMiniGame();
                    });
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(15);

            // ================== 当前 Group 映射区 ==================
            EditorGUILayout.LabelField("当前 Group 映射与 Label 配置", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("一键全部设置:", GUILayout.Width(85));
                _batchLabelIndex = EditorGUILayout.Popup(_batchLabelIndex, _labelOptions, GUILayout.Width(150));

                GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
                if (GUILayout.Button("应用到所有组", GUILayout.Width(100)))
                {
                    // ★ 批量应用：同时写回 _groupLabelIndex 和 cfg.groupMappings[].labelIndex
                    foreach (var map in _cfg.groupMappings)
                    {
                        _groupLabelIndex[map.groupName] = _batchLabelIndex;
                        map.labelIndex = _batchLabelIndex;
                    }
                    EditorUtility.SetDirty(_cfg);
                    AssetDatabase.SaveAssets();
                }
                GUI.backgroundColor = Color.white;
            }

            float normalHealthMin = _cfg.minBundleKB / 1024f / NormalCompressionRatio;
            float normalHealthMax = _cfg.idealMaxKB / 1024f / NormalCompressionRatio;
            float atlasHealthMin = _cfg.minBundleKB / 1024f / AtlasCompressionRatio;
            float atlasHealthMax = _cfg.atlasIdealMaxKB / 1024f / AtlasCompressionRatio;

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (_cfg.groupMappings != null && _cfg.groupMappings.Count > 0)
            {
                foreach (var map in _cfg.groupMappings)
                {
                    if (!_foldoutStates.ContainsKey(map.groupName)) _foldoutStates[map.groupName] = false;
                    // ★ 初始化时从配置读取 labelIndex，而不是写死 4
                    if (!_groupLabelIndex.ContainsKey(map.groupName)) _groupLabelIndex[map.groupName] = map.labelIndex;

                    bool isAtlas = map.groupName.StartsWith("Group_Atlas_");
                    float rawMB = map.estimatedSize / 1024f / 1024f;
                    float packedMB = EstimatePackedMB(map.estimatedSize, isAtlas);

                    float hMin = isAtlas ? atlasHealthMin : normalHealthMin;
                    float hMax = isAtlas ? atlasHealthMax : normalHealthMax;
                    bool overMax = packedMB > hMax * HealthTolerance;
                    bool underMin = packedMB > 0 && packedMB < hMin / HealthTolerance;
                    bool outOfRange = overMax || underMin;

                    string warn = outOfRange ? " ⚠" : "";
                    string title = $"[{map.groupName}] {rawMB:F2} MB → 打包后约 {packedMB:F2} MB{warn}";

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _foldoutStates[map.groupName] = EditorGUILayout.Foldout(_foldoutStates[map.groupName], title, true, EditorStyles.foldoutHeader);

                        GUILayout.FlexibleSpace();
                        GUILayout.Label("Label:", GUILayout.Width(45));

                        // ★ 下拉框变化时写回 cfg 并保存
                        EditorGUI.BeginChangeCheck();
                        int newIdx = EditorGUILayout.Popup(_groupLabelIndex[map.groupName], _labelOptions, GUILayout.Width(150));
                        if (EditorGUI.EndChangeCheck())
                        {
                            _groupLabelIndex[map.groupName] = newIdx;
                            map.labelIndex = newIdx;
                            EditorUtility.SetDirty(_cfg);
                            AssetDatabase.SaveAssets();
                        }
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var mini = new GUIStyle(EditorStyles.miniLabel);
                        if (outOfRange) mini.normal.textColor = new Color(0.8f, 0.4f, 0.1f);

                        string reason = "";
                        if (overMax) reason = $"（超出上限 {hMax:F2}MB 的 {((packedMB / hMax - 1) * 100):F0}%，允许 15% 内）";
                        else if (underMin) reason = $"（低于下限 {hMin:F2}MB）";

                        GUILayout.Label(
                            $"  未压缩 {rawMB:F2} MB，按 {(isAtlas ? $"图集 {AtlasCompressionRatio:F1}x" : $"普通 {NormalCompressionRatio:F1}x")} 压缩估算 → 打包后约 {packedMB:F2} MB（健康 {hMin:F2}~{hMax:F2} MB）{reason}",
                            mini);
                    }

                    if (_foldoutStates[map.groupName])
                    {
                        EditorGUI.indentLevel++;
                        foreach (var unit in map.folderUnits)
                            EditorGUILayout.LabelField("📄 " + unit, EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("暂无分组映射，请点击【6】根据大小智能自动计算组", MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
            GUI.enabled = true;
        }

        private void AnalyzeBuildOutput()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) { Debug.LogError("[AA] 分析失败：Settings 为空"); return; }

            string rawRemote = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.BuildPath");
            string remoteBuildPath = rawRemote.Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString());
            if (!Path.IsPathRooted(remoteBuildPath))
                remoteBuildPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), remoteBuildPath);
            remoteBuildPath = remoteBuildPath.Replace('\\', '/');

            if (!Directory.Exists(remoteBuildPath))
            {
                Debug.LogError($"[AA] 分析失败：远程构建目录不存在 {remoteBuildPath}");
                return;
            }

            string targetDir = remoteBuildPath;
            var subDirs = Directory.GetDirectories(remoteBuildPath)
                .OrderByDescending(d => Directory.GetLastWriteTime(d)).ToList();
            if (subDirs.Count > 0 && Directory.GetFiles(subDirs[0], "*.bundle").Length > 0)
                targetDir = subDirs[0];
            else if (Directory.GetFiles(remoteBuildPath, "*.bundle").Length == 0)
            {
                Debug.LogError($"[AA] 分析失败：{remoteBuildPath} 下没有 .bundle 文件");
                return;
            }

            var bundles = Directory.GetFiles(targetDir, "*.bundle");
            long normalIdealBytes = _cfg.idealMaxKB * 1024;
            long atlasPostCompressMax = _cfg.atlasHardMaxKB * 1024 / 3;

            var infos = bundles.Select(f => new { Name = Path.GetFileName(f), Size = new FileInfo(f).Length })
                .OrderByDescending(x => x.Size).ToList();
            long totalSize = infos.Sum(x => x.Size);

            var sb = new StringBuilder();
            sb.AppendLine($"[AA] ========== 构建产物分析 ==========");
            sb.AppendLine($"[AA] 目录: {targetDir}");
            sb.AppendLine($"[AA] 总包数: {infos.Count}, 总体积: {totalSize / 1024f / 1024f:F2} MB");
            sb.AppendLine($"[AA] 普通阈值(装箱≈未压缩): min={_cfg.minBundleKB}KB, ideal={_cfg.idealMaxKB}KB, hard={_cfg.hardMaxKB}KB");
            sb.AppendLine($"[AA] 图集阈值(未压缩): atlasIdeal={_cfg.atlasIdealMaxKB}KB, atlasHard={_cfg.atlasHardMaxKB}KB（打包后约 {atlasPostCompressMax / 1024f / 1024f:F1}MB）");
            sb.AppendLine($"[AA] 健康容差: 超出 {((HealthTolerance - 1) * 100):F0}% 以内不警告");
            sb.AppendLine($"[AA] -----------------------------------");

            int tooSmall = 0, tooLarge = 0, normal = 0;
            int smallAtlasCount = 0, smallNormalCount = 0;
            int builtInCount = 0, atlasTotalCount = 0, normalTotalCount = 0;

            foreach (var info in infos)
            {
                bool isBuiltIn = info.Name.StartsWith("group_0_") || info.Name.Contains("_scenes_");
                bool isAtlas = info.Name.Contains("_atlas_");
                bool isSmall = info.Size < _cfg.minBundleKB * 1024 / NormalCompressionRatio;

                string category;
                if (isBuiltIn) { category = " [内置/场景组]"; builtInCount++; }
                else if (isAtlas) { category = " [Atlas]"; atlasTotalCount++; }
                else { category = ""; normalTotalCount++; }

                bool isLarge = isAtlas
                    ? (info.Size > atlasPostCompressMax * HealthTolerance)
                    : (info.Size > normalIdealBytes * HealthTolerance);

                string flag = "";
                if (isSmall) { flag = " ⚠ 过小"; tooSmall++; if (isAtlas) smallAtlasCount++; else if (!isBuiltIn) smallNormalCount++; }
                else if (isLarge) { flag = " ⚠ 过大"; tooLarge++; }
                else normal++;

                sb.AppendLine($"[AA] {info.Name} : {info.Size / 1024f:F1} KB ({info.Size / 1024f / 1024f:F2} MB){category}{flag}");
            }

            sb.AppendLine($"[AA] -----------------------------------");
            sb.AppendLine($"[AA] 过小: {tooSmall} 个（Atlas {smallAtlasCount} 个，普通 {smallNormalCount} 个）");
            sb.AppendLine($"[AA] 正常: {normal} 个");
            sb.AppendLine($"[AA] 过大: {tooLarge} 个（已应用 {((HealthTolerance - 1) * 100):F0}% 容差）");
            sb.AppendLine($"[AA] 分类统计：图集 {atlasTotalCount} 个，普通 {normalTotalCount} 个，内置/场景 {builtInCount} 个");
            if (builtInCount > 0)
                sb.AppendLine($"[AA] 提示：内置/场景组（group_0 / *_scenes_*）是 Addressables 内置组的产物，工具管不到。如需拆小，在 Groups 窗口手动把 Default Local Group 的 Bundle Mode 改成 Pack Separately。");
            sb.AppendLine($"[AA] ========== 分析完成 ==========");

            string resultText = sb.ToString();
            Debug.Log(resultText);
            GUIUtility.systemCopyBuffer = resultText;
            Debug.Log("[AA] 📋 分析结果已自动复制到剪贴板！");
        }

        private void MarkAllAssetsAsAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) { Debug.LogError("[AA] 标记失败：Settings 为空"); return; }

            string root = "Assets/Addressables";
            if (!Directory.Exists(root)) { Debug.LogWarning($"[AA] 标记失败：目录 {root} 不存在"); return; }

            var allFiles = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".cs") && !f.EndsWith(".asmdef") && !f.EndsWith("_bak.png")).ToList();

            int count = 0;
            foreach (var file in allFiles)
            {
                string guid = AssetDatabase.AssetPathToGUID(file);
                if (string.IsNullOrEmpty(guid)) continue;

                if (settings.FindAssetEntry(guid) == null)
                {
                    settings.CreateOrMoveEntry(guid, settings.DefaultGroup, false, false);
                    count++;
                }
            }

            if (count > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AA] ✅ 已强制将 {count} 个未标记资源加入 Addressables");
            }
            else
            {
                Debug.Log($"[AA] 所有资源都已经在 Addressables 系统中，无需重新标记。");
            }
        }

        // ============================================================
        // ★ 核心修改：BuildPath 带时间戳（本地版本存档），LoadPath 不带时间戳
        // ============================================================
        private void BuildContent()
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null) return;

                // 打包前检查系统内置组是否被错误挂载了 Remote Schema
                CheckSystemGroupsHealth(settings);

                AddressableAssetSettings.CleanPlayerContent();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

                string oldBuildPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.BuildPath");
                string oldLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath");

                // BuildPath 带时间戳：本地输出到 ServerData/WebGL/2026-xx-xx_xx-xx/，用于版本存档
                string newBuildPath = oldBuildPath.Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString());
                if (!newBuildPath.EndsWith("/")) newBuildPath += "/";
                newBuildPath += timestamp;

                // LoadPath 坚决不带时间戳：catalog 里记录的 bundle URL 永远是服务器根路径
                // 这样你把时间戳文件夹里的文件上传到服务器根目录后，运行时能直接匹配
                string newLoadPath = oldLoadPath.Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString());
                if (!newLoadPath.EndsWith("/")) newLoadPath += "/";
                // ★★★ 注意：这里绝对不要 += timestamp ★★★

                settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", newBuildPath);
                settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", newLoadPath);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();

                try
                {
                    Debug.Log($"[AA] 开始打资源，本地输出路径: {newBuildPath}");
                    Debug.Log($"[AA] 运行时 LoadPath（写入 catalog）: {newLoadPath}");
                    AddressableAssetSettings.BuildPlayerContent();
                    Debug.Log("[AA] ✅ 打资源完成！");

                    // 自动把 Library 下的 catalog_0.1.json / catalog_0.1.hash 复制到时间戳目录
                    MoveCatalogToBuildFolder(newBuildPath);
                }
                finally
                {
                    settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", oldBuildPath);
                    settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", oldLoadPath);
                    settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                    AssetDatabase.SaveAssets();
                }

                if (_cfg.autoOpenBuildFolder)
                {
                    string abs = newBuildPath;
                    if (!Path.IsPathRooted(abs))
                        abs = Path.Combine(Path.GetDirectoryName(Application.dataPath), abs);
                    if (Directory.Exists(abs)) EditorUtility.RevealInFinder(abs);
                    else Debug.LogWarning($"[AA] 未找到资源输出目录: {abs}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AA] ❌ 打资源异常: {e.Message}\n{e.StackTrace}");
            }
        }

        // ============================================================
        // 检查系统内置组（Built In Data / Default Local Group）是否被错误挂了 Remote Schema
        // ============================================================
        private void CheckSystemGroupsHealth(AddressableAssetSettings settings)
        {
            try
            {
                var defaultGroup = settings.FindGroup("Default Local Group");
                if (defaultGroup != null)
                {
                    var schema = defaultGroup.GetSchema<BundledAssetGroupSchema>();
                    if (schema != null)
                    {
                        string buildVar = "";
                        string loadVar = "";
                        try { buildVar = schema.BuildPath.GetName(settings); } catch { }
                        try { loadVar = schema.LoadPath.GetName(settings); } catch { }

                        if (buildVar == "Remote.BuildPath" || loadVar == "Remote.LoadPath")
                        {
                            Debug.LogWarning(
                                $"[AA] ⚠ Default Local Group 被错误挂载了 Remote Schema（BuildPath={buildVar}, LoadPath={loadVar}）。\n" +
                                "[AA] 系统内置组必须走 Local，否则它的 bundle 会去请求远程服务器导致 404。\n" +
                                "[AA] 修复方法：Groups 窗口选中 Default Local Group，右键 Remove Group Schema，或把 Build & Load Paths 改为 Local。");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AA] 检查系统组状态时出现异常（不影响打包）: {e.Message}");
            }
        }

        // ============================================================
        // 自动从 Library 下查找 catalog 文件，并复制到时间戳输出目录
        // ============================================================
        private void MoveCatalogToBuildFolder(string buildPath)
        {
            try
            {
                string absBuildPath = buildPath;
                if (!Path.IsPathRooted(absBuildPath))
                    absBuildPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), absBuildPath);
                absBuildPath = absBuildPath.Replace('\\', '/');
                if (!Directory.Exists(absBuildPath)) Directory.CreateDirectory(absBuildPath);

                // 情况 1：Addressables 直接把 Remote Catalog 生成到了 BuildPath 下，无需搬运
                var existingCatalogs = Directory.GetFiles(absBuildPath, "catalog*.json")
                    .Concat(Directory.GetFiles(absBuildPath, "catalog*.hash"))
                    .ToList();
                if (existingCatalogs.Count > 0)
                {
                    Debug.Log($"[AA] ✅ Catalog 文件已存在于输出目录：{absBuildPath}，无需搬运。");
                    return;
                }

                // 情况 2：从 Library 下的默认 Local.BuildPath 位置查找
                string libraryPath = Path.Combine(
                    Path.GetDirectoryName(Application.dataPath),
                    "Library", "com.unity.addressables", "aa",
                    EditorUserBuildSettings.activeBuildTarget.ToString()
                ).Replace('\\', '/');

                var catalogFiles = new List<string>();
                if (Directory.Exists(libraryPath))
                {
                    catalogFiles.AddRange(Directory.GetFiles(libraryPath, "catalog*.json"));
                    catalogFiles.AddRange(Directory.GetFiles(libraryPath, "catalog*.hash"));
                }

                int copiedCount = 0;
                foreach (var file in catalogFiles)
                {
                    string destFile = Path.Combine(absBuildPath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                    copiedCount++;
                    Debug.Log($"[AA] ✅ 自动将 {Path.GetFileName(file)} 复制到时间戳目录: {absBuildPath}");
                }

                if (copiedCount > 0)
                {
                    Debug.Log($"[AA] ✅ 成功搬运 {copiedCount} 个 Catalog 文件。现在可以把该文件夹里的文件上传服务器根目录了！");
                }
                else
                {
                    Debug.LogWarning($"[AA] ⚠ 在 {libraryPath} 未找到 catalog 文件。请确认已勾选 Build Remote Catalog（Addressables Settings → Catalog → Build Remote Catalog）。");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AA] ❌ 移动 Catalog 文件失败: {ex.Message}");
            }
        }

        private void DoBuildMiniGame()
        {
            const string menuPath = "微信小游戏/转换小游戏";

            try
            {
                if (EditorApplication.ExecuteMenuItem(menuPath))
                {
                    Debug.Log($"[AA] ✅ 已打开微信转换菜单: {menuPath}");
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AA] 执行菜单 {menuPath} 异常: {e.Message}");
            }

            var wxEditorWindowType = Type.GetType("WeChatWASM.WXEditorWindow, WX-WASM-SDK-V2.Editor") ?? Type.GetType("WeChatWASM.WXEditorWindow");
            if (wxEditorWindowType != null)
            {
                try
                {
                    var win = EditorWindow.GetWindow(wxEditorWindowType, false, "微信小游戏", true);
                    if (win != null)
                    {
                        win.Show();
                        win.Focus();
                        Debug.Log("[AA] ✅ 已打开微信小游戏窗口，请在窗口里手动点击“转换小游戏”。");
                        return;
                    }
                }
                catch { }
            }

            Debug.LogError($"[AA] ❌ 未找到菜单 {menuPath}，也未找到微信小游戏窗口。请手动点击菜单：微信小游戏 -> 转换小游戏。");
            EditorUtility.DisplayDialog("请手动转换", "未自动找到微信转换菜单。\n请手动点击菜单：微信小游戏 -> 转换小游戏。", "知道了");
        }

        private void StartAsyncProcess(Action onComplete = null)
        {
            if (_cfg.groupMappings == null || _cfg.groupMappings.Count == 0) return;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) { Debug.LogError("[AA] 致命错误: Settings 为空！"); onComplete?.Invoke(); return; }

            foreach (var lb in _actualLabels)
                if (!string.IsNullOrEmpty(lb)) settings.AddLabel(lb, false);

            _taskQueue.Clear();
            _queuedFiles.Clear();
            _processedTasks = 0;

            for (int i = 0; i < _cfg.groupMappings.Count; i++)
            {
                var map = _cfg.groupMappings[i];
                var group = settings.FindGroup(map.groupName);
                if (group == null) continue;

                // ★ 用持久化的 labelIndex，而不是内存字典
                int idx = map.labelIndex;
                if (idx < 0 || idx >= _actualLabels.Length) idx = 4;
                string targetLabel = _actualLabels[idx];

                foreach (var file in GetFilesForGroupFast(map))
                {
                    if (!_queuedFiles.Contains(file))
                    {
                        _queuedFiles.Add(file);
                        _taskQueue.Enqueue((file, map.groupName, targetLabel));
                    }
                }
            }

            _totalTasks = _taskQueue.Count;
            if (_totalTasks == 0) { Debug.LogError("[AA] 没有需要移入的文件！"); onComplete?.Invoke(); return; }

            _isProcessing = true;
            _onProcessComplete = onComplete;

            EditorApplication.update -= ProcessStep;
            EditorApplication.update += ProcessStep;
            Debug.Log($"[AA] 开始异步处理 {_totalTasks} 个文件...");
        }

        private void ProcessStep()
        {
            if (!_isProcessing) return;

            if (_taskQueue.Count == 0)
            {
                _isProcessing = false;
                EditorApplication.update -= ProcessStep;

                var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[AA] ✅ 所有 {_totalTasks} 个文件均已成功移入并打标！");

                _onProcessComplete?.Invoke();
                _onProcessComplete = null;
                return;
            }

            var task = _taskQueue.Dequeue();
            _currentFileName = Path.GetFileName(task.filePath);

            try
            {
                var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                var currentGroup = settings.FindGroup(task.groupName);

                if (currentGroup != null)
                {
                    string guid = AssetDatabase.AssetPathToGUID(task.filePath);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        var entry = settings.CreateOrMoveEntry(guid, currentGroup, false, false);
                        if (entry != null)
                        {
                            if (string.IsNullOrEmpty(entry.address) || entry.address == guid)
                                entry.SetAddress(task.filePath, false);
                            if (!string.IsNullOrEmpty(task.label) && !entry.labels.Contains(task.label))
                                entry.SetLabel(task.label, true, true, false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AA] 异常 {task.filePath}: {e.Message}");
            }

            _processedTasks++;
            Repaint();
        }

        private List<string> GetFilesForGroupFast(GroupMappingEntry map)
        {
            var result = new List<string>();
            foreach (var unit in map.folderUnits)
            {
                if (File.Exists(unit))
                {
                    result.Add(unit.Replace('\\', '/'));
                }
                else if (Directory.Exists(unit))
                {
                    foreach (var f in Directory.GetFiles(unit, "*.*", SearchOption.TopDirectoryOnly))
                    {
                        if (!f.EndsWith(".meta") && !f.EndsWith(".cs") && !f.EndsWith(".asmdef") && !f.EndsWith("_bak.png"))
                            result.Add(f.Replace('\\', '/'));
                    }
                }
            }
            return result;
        }

        private void ResetEnvironment()
        {
            string path = "Assets/AddressableAssetsData";
            if (Directory.Exists(path)) { FileUtil.DeleteFileOrDirectory(path); AssetDatabase.Refresh(); }

            var settings = AddressableAssetSettings.Create(
                AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);

            if (settings != null)
            {
                AddressableAssetSettingsDefaultObject.Settings = settings;
                var ps = settings.profileSettings;
                var names = ps.GetVariableNames();
                string activeProfileId = settings.activeProfileId;

                void EnsureAndSet(string varName, string varValue)
                {
                    if (!names.Contains(varName)) ps.CreateValue(varName, varValue);
                    ps.SetValue(activeProfileId, varName, varValue);
                }

                EnsureAndSet("Local.BuildPath", "[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]");
                EnsureAndSet("Local.LoadPath", "{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]");
                EnsureAndSet("Remote.BuildPath", _cfg.remoteBuildPath);
                EnsureAndSet("Remote.LoadPath", _cfg.remoteLoadPath);

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[AA] ✅ 环境重置成功！");
            }
        }

        private long GetAtlasRealSize(string atlasAssetPath)
        {
            long total = 0;
            string normalized = atlasAssetPath.Replace('\\', '/');
            var visited = new HashSet<string>();

            if (File.Exists(normalized))
            {
                total += new FileInfo(normalized).Length;
                visited.Add(normalized);
            }

            foreach (var dep in AssetDatabase.GetDependencies(normalized, true))
            {
                string d = dep.Replace('\\', '/');
                if (visited.Contains(d)) continue;
                if (!d.StartsWith("Assets/")) continue;
                if (d.EndsWith(".cs") || d.EndsWith(".meta") || d.EndsWith(".asmdef")) continue;
                if (!File.Exists(d)) continue;
                total += new FileInfo(d).Length;
                visited.Add(d);
            }
            return total;
        }

        private bool IsAtlasPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.Replace('\\', '/').Contains("/Atlases/");
        }

        private static bool IsTextureAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string p = path.ToLower();
            return p.EndsWith(".png") || p.EndsWith(".jpg") || p.EndsWith(".jpeg")
                || p.EndsWith(".tga") || p.EndsWith(".psd") || p.EndsWith(".tif")
                || p.EndsWith(".tiff") || p.EndsWith(".exr") || p.EndsWith(".bmp");
        }

        private void CalculateAndCreateGroups()
        {
            EnsureSweetSpotDefaults();

            // 核心：在计算分组之前，强制把所有资源全部标记为 AA
            MarkAllAssetsAsAddressable();

            // ★ 备份旧 Label（按组名），重建时按组名迁移
            var oldLabelMap = new Dictionary<string, int>();
            foreach (var m in _cfg.groupMappings)
            {
                if (m != null && !string.IsNullOrEmpty(m.groupName))
                    oldLabelMap[m.groupName] = m.labelIndex;
            }

            _cfg.groupMappings.Clear();
            string root = "Assets/Addressables";
            if (!Directory.Exists(root)) return;

            var allFiles = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".cs") && !f.EndsWith(".asmdef") && !f.EndsWith("_bak.png")).ToList();

            var standalonePaths = new HashSet<string>(_cfg.standaloneAssets.Select(p => p.Replace('\\', '/')));
            var standaloneFiles = allFiles.Where(f => standalonePaths.Contains(f.Replace('\\', '/'))).ToList();

            allFiles = allFiles.Except(standaloneFiles).ToList();

            var standaloneGroups = new List<GroupPlan>();
            foreach (var f in standaloneFiles)
            {
                string normalized = f.Replace('\\', '/');
                long size = new FileInfo(normalized).Length;
                string fileName = Path.GetFileNameWithoutExtension(normalized);

                string prefix = normalized.EndsWith(".unity") ? "Group_Scene_" : "Group_Standalone_";
                string groupName = prefix + fileName;

                standaloneGroups.Add(new GroupPlan(new KeyValuePair<string, long>(normalized, size), groupName));
                Debug.Log($"[AA] 发现独立打包资源，将强制单独成组: {normalized} -> {groupName}");
            }

            var atlasTextures = new HashSet<string>();
            var atlasFiles = new List<string>();
            foreach (var file in allFiles)
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.EndsWith(".spriteatlas") && IsAtlasPath(normalized))
                {
                    atlasFiles.Add(normalized);
                    foreach (var dep in AssetDatabase.GetDependencies(normalized, true))
                    {
                        string d = dep.Replace('\\', '/');
                        if (d == normalized) continue;
                        if (IsTextureAsset(d) && File.Exists(d)) atlasTextures.Add(d);
                    }
                }
            }
            Debug.Log($"[AA] 识别到 {atlasFiles.Count} 个图集，{atlasTextures.Count} 张被图集引用的纹理（不单独标记）");

            var allUnits = new Dictionary<string, long>();
            foreach (var file in allFiles)
            {
                string normalizedPath = file.Replace('\\', '/');
                long fileSize = new FileInfo(normalizedPath).Length;

                if (atlasTextures.Contains(normalizedPath)) continue;

                if (normalizedPath.EndsWith(".spriteatlas") && IsAtlasPath(normalizedPath))
                {
                    long realSize = GetAtlasRealSize(normalizedPath);
                    allUnits[normalizedPath] = realSize;
                }
                else
                {
                    string dir = Path.GetDirectoryName(normalizedPath).Replace('\\', '/') + "/";
                    if (!allUnits.ContainsKey(dir)) allUnits[dir] = 0;
                    allUnits[dir] += fileSize;
                }
            }

            long minBytes = _cfg.minBundleKB * 1024;
            long idealMaxBytes = _cfg.idealMaxKB * 1024;
            long hardMaxBytes = _cfg.hardMaxKB * 1024;
            long atlasIdealMaxBytes = _cfg.atlasIdealMaxKB * 1024;
            long atlasHardMaxBytes = _cfg.atlasHardMaxKB * 1024;

            var units = allUnits.OrderByDescending(kv => kv.Value).ToList();
            var groups = new List<GroupPlan>(standaloneGroups);

            int normalGroupIndex = 0;
            int atlasGroupIndex = 0;

            foreach (var unit in units)
            {
                bool isAtlasUnit = IsAtlasPath(unit.Key) && unit.Key.EndsWith(".spriteatlas");
                long currentIdealMax = isAtlasUnit ? atlasIdealMaxBytes : idealMaxBytes;
                long currentHardMax = isAtlasUnit ? atlasHardMaxBytes : hardMaxBytes;

                if (unit.Value > currentHardMax)
                {
                    string name = isAtlasUnit ? $"Group_Atlas_{atlasGroupIndex++}" : $"Group_{normalGroupIndex++}";
                    groups.Add(new GroupPlan(unit, name));
                    continue;
                }

                string category = GetParentCategory(unit.Key);

                var sameCategoryGroup = groups
                    .Where(g => g.Category == category && !g.GroupName.StartsWith("Group_Scene_") && !g.GroupName.StartsWith("Group_Standalone_")
                        && (g.CurrentSize + unit.Value) <= currentIdealMax
                        && (isAtlasUnit ? g.GroupName.StartsWith("Group_Atlas_") : !g.GroupName.StartsWith("Group_Atlas_")))
                    .OrderByDescending(g => g.CurrentSize).FirstOrDefault();

                if (sameCategoryGroup != null) { sameCategoryGroup.AddUnit(unit); continue; }

                var bestFitGroup = groups
                    .Where(g => !g.GroupName.StartsWith("Group_Scene_") && !g.GroupName.StartsWith("Group_Standalone_")
                        && (g.CurrentSize + unit.Value) <= currentIdealMax
                        && (isAtlasUnit ? g.GroupName.StartsWith("Group_Atlas_") : !g.GroupName.StartsWith("Group_Atlas_")))
                    .OrderByDescending(g => g.CurrentSize).FirstOrDefault();

                if (bestFitGroup != null) { bestFitGroup.AddUnit(unit); continue; }

                string newName = isAtlasUnit ? $"Group_Atlas_{atlasGroupIndex++}" : $"Group_{normalGroupIndex++}";
                groups.Add(new GroupPlan(unit, newName));
            }

            int mergedCount = MergeSmallGroups(groups, minBytes, idealMaxBytes, atlasIdealMaxBytes);
            if (mergedCount > 0)
                Debug.Log($"[AA] 兜底合并：消除了 {mergedCount} 个过小组");

            foreach (var groupData in groups)
            {
                var map = new GroupMappingEntry
                {
                    groupName = groupData.GroupName,
                    estimatedSize = groupData.CurrentSize,
                    // ★ 按组名从旧配置迁移 Label，找不到就用默认 4
                    labelIndex = oldLabelMap.ContainsKey(groupData.GroupName) ? oldLabelMap[groupData.GroupName] : 4
                };
                foreach (var unit in groupData.Units) map.folderUnits.Add(unit.Key);
                _cfg.groupMappings.Add(map);
            }

            EditorUtility.SetDirty(_cfg);
            AssetDatabase.SaveAssets();

            _groupLabelIndex.Clear();

            // 清理重建物理组
            CleanAndCreatePhysicalGroups();

            int atlasCount = groups.Count(g => g.GroupName.StartsWith("Group_Atlas_"));
            int normalCount = groups.Count(g => !g.GroupName.StartsWith("Group_Atlas_") && !g.GroupName.StartsWith("Group_Scene_") && !g.GroupName.StartsWith("Group_Standalone_"));
            int standaloneCount = groups.Count(g => g.GroupName.StartsWith("Group_Scene_") || g.GroupName.StartsWith("Group_Standalone_"));

            long totalBytes = groups.Sum(g => g.CurrentSize);
            Debug.Log($"[AA] ✅ 计算完成：普通组 {normalCount} 个，图集组 {atlasCount} 个，独立资源组 {standaloneCount} 个。总计 {groups.Count} 个。");
            Debug.Log($"[AA] 总体积约 {totalBytes / 1024f / 1024f:F2} MB（未压缩）");
        }

        private int MergeSmallGroups(List<GroupPlan> groups, long minBytes, long idealMaxBytes, long atlasIdealMaxBytes)
        {
            int mergedCount = 0;
            bool merged = true;
            int guard = 0;

            while (merged && guard++ < 1000)
            {
                merged = false;
                var smallGroups = groups.Where(g => g.CurrentSize < minBytes && !g.GroupName.StartsWith("Group_Scene_") && !g.GroupName.StartsWith("Group_Standalone_")).ToList();
                if (smallGroups.Count == 0) break;

                foreach (var small in smallGroups)
                {
                    bool smallIsAtlas = small.GroupName.StartsWith("Group_Atlas_");
                    long limit = smallIsAtlas ? atlasIdealMaxBytes : idealMaxBytes;

                    var target = groups
                        .Where(g => g != small && !g.GroupName.StartsWith("Group_Scene_") && !g.GroupName.StartsWith("Group_Standalone_")
                            && g.GroupName.StartsWith("Group_Atlas_") == smallIsAtlas
                            && (g.CurrentSize + small.CurrentSize) <= limit)
                        .OrderByDescending(g => g.CurrentSize).FirstOrDefault();

                    if (target != null)
                    {
                        foreach (var unit in small.Units) target.AddUnit(unit);
                        groups.Remove(small);
                        merged = true;
                        mergedCount++;
                        Debug.Log($"[AA] 兜底合并：{small.GroupName} ({small.CurrentSize / 1024f:F1}KB) -> {target.GroupName} (新大小 {target.CurrentSize / 1024f:F1}KB)");
                        break;
                    }
                }
            }
            return mergedCount;
        }

        private void CleanAndCreatePhysicalGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;

            // 暴力全清（除了系统内置组）
            var groupsToDelete = new List<AddressableAssetGroup>();
            foreach (var g in settings.groups)
            {
                if (g != null && g.Name != "Built In Data" && g.Name != "Default Local Group")
                    groupsToDelete.Add(g);
            }
            foreach (var g in groupsToDelete) settings.RemoveGroup(g);

            // 创建新物理组
            foreach (var map in _cfg.groupMappings)
            {
                if (settings.FindGroup(map.groupName) == null)
                    CreateGroupPhysically(settings, map.groupName);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        private AddressableAssetGroup CreateGroupPhysically(AddressableAssetSettings settings, string groupName, bool isLocal = false)
        {
            var group = settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            if (group == null) return null;

            var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
            if (bundleSchema != null)
            {
                bundleSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
                bundleSchema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
                try
                {
                    bundleSchema.BuildPath.SetVariableByName(settings, isLocal ? "Local.BuildPath" : "Remote.BuildPath");
                    bundleSchema.LoadPath.SetVariableByName(settings, isLocal ? "Local.LoadPath" : "Remote.LoadPath");
                }
                catch { }
            }
            return group;
        }

        private class GroupPlan
        {
            public string GroupName;
            public string Category;
            public long CurrentSize = 0;
            public List<KeyValuePair<string, long>> Units = new List<KeyValuePair<string, long>>();

            public GroupPlan(KeyValuePair<string, long> firstUnit, string groupName)
            {
                GroupName = groupName;
                Category = GetParentCategoryStatic(firstUnit.Key);
                AddUnit(firstUnit);
            }

            public void AddUnit(KeyValuePair<string, long> unit)
            {
                Units.Add(unit);
                CurrentSize += unit.Value;
            }
        }

        private static string GetParentCategoryStatic(string path)
        {
            string normalized = path.Replace('\\', '/');

            if (normalized.EndsWith(".spriteatlas") && normalized.Contains("/Atlases/"))
            {
                var dir = Path.GetDirectoryName(normalized).Replace('\\', '/');
                if (dir.StartsWith("Assets/Addressables/"))
                    dir = dir.Substring("Assets/Addressables/".Length);
                return "ATLAS/" + dir;
            }

            var normalDir = Path.GetDirectoryName(normalized).Replace('\\', '/');
            if (normalDir.StartsWith("Assets/Addressables/"))
                normalDir = normalDir.Substring("Assets/Addressables/".Length);
            return normalDir;
        }

        private string GetParentCategory(string path) => GetParentCategoryStatic(path);

        private void DoAtlasBuild()
        {
            Debug.LogWarning("[AA] ⚠ 打图集功能此工具未实现。图集请在项目里手动生成，或使用专门的图集工具。");
        }
    }

    // ============================================================
    //  设置包大小范围弹窗（包含阈值介绍）
    // ============================================================
    public class BundleSizeRangeWindow : EditorWindow
    {
        private AAutoPackConfig _cfg;
        private float _minMB;
        private float _maxMB;

        public static void ShowWindow(AAutoPackConfig cfg)
        {
            var w = GetWindow<BundleSizeRangeWindow>("设置包大小范围", true);
            w._cfg = cfg;
            w._minMB = cfg.minBundleKB / 1024f;
            w._maxMB = cfg.idealMaxKB / 1024f;
            w.minSize = new Vector2(450, 520);
            w.maxSize = new Vector2(450, 520);
            w.ShowUtility();
            w.Focus();
        }

        private void OnGUI()
        {
            if (_cfg == null)
            {
                EditorGUILayout.HelpBox("配置丢失，请重新打开主窗口", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("设置普通资源装箱阈值（MB，≈未压缩体积）", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "这里设置的是装箱阈值（≈未压缩体积，不是打包后）。\n" +
                "映射区显示的“打包后”是按压缩比估算的（图集 3x、普通 1.3x）。\n" +
                "打包后估算超出 15% 以内不警告。",
                MessageType.Info);

            EditorGUILayout.Space(5);

            _minMB = EditorGUILayout.FloatField("最小值（MB）", _minMB);
            _maxMB = EditorGUILayout.FloatField("最大值（MB）", _maxMB);

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                float normalHealthMinMB = _cfg.minBundleKB / 1024f / 1.3f;
                float normalHealthMaxMB = _cfg.idealMaxKB / 1024f / 1.3f;
                float atlasHealthMinMB = _cfg.minBundleKB / 1024f / 3.0f;
                float atlasHealthMaxMB = _cfg.atlasIdealMaxKB / 1024f / 3.0f;

                EditorGUILayout.LabelField(
                    $"普通装箱阈值（KB，≈未压缩）：min={_cfg.minBundleKB} / ideal={_cfg.idealMaxKB} / hard={_cfg.hardMaxKB}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"图集装箱阈值（KB，未压缩）：atlasIdeal={_cfg.atlasIdealMaxKB} / atlasHard={_cfg.atlasHardMaxKB}",
                    EditorStyles.miniLabel);

                var style = new GUIStyle(EditorStyles.boldLabel);
                style.normal.textColor = new Color(0.2f, 0.6f, 0.2f);
                EditorGUILayout.LabelField(
                    $"🎯 普通资源健康范围（打包后估算）：{normalHealthMinMB:F2} MB ~ {normalHealthMaxMB:F2} MB，允许超出 15% 不警告",
                    style);

                EditorGUILayout.LabelField(
                    $"🎯 图集健康范围（打包后估算）：{atlasHealthMinMB:F2} MB ~ {atlasHealthMaxMB:F2} MB，允许超出 15% 不警告",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUILayout.Button("确定", GUILayout.Height(30)))
                {
                    if (_minMB <= 0 || _maxMB <= 0 || _minMB >= _maxMB)
                    {
                        EditorUtility.DisplayDialog("参数错误", "最小值必须 > 0 且小于最大值", "确定");
                        return;
                    }

                    _cfg.minBundleKB = (long)Math.Round(_minMB * 1024);
                    _cfg.idealMaxKB = (long)Math.Round(_maxMB * 1024);
                    _cfg.hardMaxKB = (long)Math.Round(_maxMB * 2 * 1024);

                    _cfg.atlasIdealMaxKB = (long)Math.Round(_maxMB * 3 * 1024);
                    _cfg.atlasHardMaxKB = (long)Math.Round(_maxMB * 6 * 1024);

                    EditorUtility.SetDirty(_cfg);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[AA] ✅ 已设置装箱阈值：{_minMB:F2} MB ~ {_maxMB:F2} MB（普通 min={_cfg.minBundleKB}KB, ideal={_cfg.idealMaxKB}KB, hard={_cfg.hardMaxKB}KB；图集 atlasIdeal={_cfg.atlasIdealMaxKB}KB, atlasHard={_cfg.atlasHardMaxKB}KB）");
                    Close();
                }

                GUI.backgroundColor = new Color(1f, 0.8f, 0.8f);
                if (GUILayout.Button("取消", GUILayout.Height(30)))
                    Close();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    // ============================================================
    //  单独打包列表弹窗
    // ============================================================
    public class StandaloneListWindow : EditorWindow
    {
        private AAutoPackConfig _cfg;
        private Vector2 _scroll;

        public static void ShowWindow(AAutoPackConfig cfg)
        {
            var w = GetWindow<StandaloneListWindow>("单独打包列表", true);
            w._cfg = cfg;
            w.minSize = new Vector2(550, 400);
            w.Show();
        }

        private void OnGUI()
        {
            if (_cfg == null)
            {
                EditorGUILayout.HelpBox("配置丢失，请重新打开主窗口", MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox("在此列表中的资源（如 Assets/Addressables/Scenes/GameScene.unity）将被强制单独成组（Group_Scene_XXX 或 Group_Standalone_XXX），绝对不参与任何智能装箱合并。", MessageType.Info);
            EditorGUILayout.Space(5);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_cfg.standaloneAssets != null)
            {
                for (int i = 0; i < _cfg.standaloneAssets.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        string newPath = EditorGUILayout.TextField(_cfg.standaloneAssets[i]);
                        if (EditorGUI.EndChangeCheck())
                        {
                            _cfg.standaloneAssets[i] = newPath;
                            EditorUtility.SetDirty(_cfg);
                        }

                        UnityEngine.Object selectedObj = EditorGUILayout.ObjectField(null, typeof(UnityEngine.Object), false, GUILayout.Width(80));
                        if (selectedObj != null)
                        {
                            _cfg.standaloneAssets[i] = AssetDatabase.GetAssetPath(selectedObj);
                            EditorUtility.SetDirty(_cfg);
                            GUI.FocusControl(null);
                        }

                        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                        if (GUILayout.Button("删除", GUILayout.Width(45)))
                        {
                            _cfg.standaloneAssets.RemoveAt(i);
                            EditorUtility.SetDirty(_cfg);
                            AssetDatabase.SaveAssets();
                            break;
                        }
                        GUI.backgroundColor = Color.white;
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                if (GUILayout.Button("+ 添加新路径", GUILayout.Height(30)))
                {
                    _cfg.standaloneAssets.Add("");
                    EditorUtility.SetDirty(_cfg);
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("保存并关闭", GUILayout.Height(30)))
            {
                EditorUtility.SetDirty(_cfg);
                AssetDatabase.SaveAssets();
                Close();
            }
        }
    }
}
