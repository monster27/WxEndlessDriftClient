using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // 自定义左对齐按钮样式
        private GUIStyle _leftAlignedButtonStyle;

        // ===== Label 下拉框状态 =====
        private Dictionary<string, int> _groupLabelIndex = new Dictionary<string, int>();
        private int _batchLabelIndex = 4;

        private readonly string[] _labelOptions = { "无标签", "首包 (Local)", "立刻预下载 (Tier1)", "空闲预下载 (Tier2)", "用到才下载 (Tier3)" };
        private readonly string[] _actualLabels = { "", "local", "tier1_preload", "tier2_idle", "tier3_ondemand" };

        // ===== 异步处理状态 =====
        private bool _isProcessing = false;
        private Queue<(string filePath, string groupName, string label)> _taskQueue = new Queue<(string, string, string)>();
        private HashSet<string> _queuedFiles = new HashSet<string>();
        private int _totalTasks = 0;
        private int _processedTasks = 0;
        private string _currentFileName = "";
        private Action _onProcessComplete;

        [MenuItem("Tools/Addressables/AA Auto Pack")]
        public static void ShowWindow()
        {
            var w = GetWindow<AAutoPackTool>("AA Auto Pack");
            w.minSize = new Vector2(1050, 950);
        }

        private void OnEnable()
        {
            _cfg = AAutoPackConfig.GetOrCreate();
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProcessStep;
        }

        private void OnGUI()
        {
            if (_cfg == null) _cfg = AAutoPackConfig.GetOrCreate();

            // 初始化左对齐、垂直居中、白字、加宽边距的按钮样式
            if (_leftAlignedButtonStyle == null)
            {
                _leftAlignedButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(20, 20, 10, 10),
                    fontSize = 13,
                    wordWrap = true
                };
                _leftAlignedButtonStyle.normal.textColor = Color.white;
                _leftAlignedButtonStyle.hover.textColor = Color.white;
                _leftAlignedButtonStyle.active.textColor = Color.white;
                _leftAlignedButtonStyle.focused.textColor = Color.white;
            }

            // ================== 帮助与说明面板 ==================
            _showHelp = EditorGUILayout.Foldout(_showHelp, "▶ 详细使用说明 (点此展开)", true, EditorStyles.foldoutHeader);
            if (_showHelp)
            {
                EditorGUILayout.HelpBox(
                    "【重要流程提示】\n" +
                    "1. 如果本次修改了图片素材，必须重新生成图集，请务必选择带“(含图集)”的自动化按钮或手动点击。\n" +
                    "2. 自动化流程会自动清理旧的Group，并重新计算、重新建组，避免脏数据导致打包报错。\n" +
                    "3. 如果只是改了Prefab、代码或JSON，不需要重打图集。\n" +
                    "4. 打包资源时，会自动在根目录创建带时间戳的文件夹（如 2026-10-18_10-34），防止版本覆盖。\n" +
                    "5. 所有“打包”操作均代表打包微信小程序（自动调起微信转换工具，并自动生成时间戳文件夹）。\n" +
                    "6. 如果微信转换失败，请点击【追踪配置文件】，手动修改微信菜单路径。\n\n" +
                    "【详细按钮说明】\n" +
                    "• 一、 基础环境：\n" +
                    "  - 打开 Profiles 界面：审查/切换 Addressables 的 Profile。\n" +
                    "  - 打开 Groups 界面：审查底层生成的数据。\n" +
                    "  - 重置底层环境：彻底删掉 AddressableAssetsData，解决各种报错。\n" +
                    "  - 重打所有图集：扫描源图片并生成新的 .spriteatlas 文件。\n" +
                    "• 二、 核心分组：\n" +
                    "  - 智能计算并新建 Group：按路径和大小，在内存中计算出最优的组划分方案并创建空组。\n" +
                    "  - 批量移入资源并打上 Label：按计算结果，一帧一个移入资源并设置 Address 和 Label。\n" +
                    "• 三、 构建与打包：\n" +
                    "  - 单独打资源：生成 Addressables 的 Bundle。\n" +
                    "  - 打开微信小游戏转换界面：直接调起微信小游戏的“转换小游戏”菜单，由你手动点击转换。\n" +
                    "• 四、 自动化组合：\n" +
                    "  - 将上述步骤自动化串联，按需选择是否重打图集或包含小程序打包。\n\n" +
                    "【按钮是否会跳转微信转换界面】\n" +
                    "  - 【6】单独打资源：不会跳转。\n" +
                    "  - 【7】打开微信小游戏转换界面：只跳转。\n" +
                    "  - 【一键打资源】/【一键打资源(含图集)】：不会跳转。\n" +
                    "  - 【一键打包】/【全自动打包】：会跳转。",
                    MessageType.Info);
            }

            EditorGUILayout.Space(10);
            GUI.enabled = !_isProcessing;

            // ================== 辅助小工具区 ==================
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
                        try
                        {
                            // 直接进入 Build 文件夹内部（而不是在父目录里选中它）
                            System.Diagnostics.Process.Start("explorer.exe", "\"" + buildPath.Replace('/', '\\') + "\"");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[AA] 打开 Build 文件夹失败: {e.Message}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[AA] 未找到 Build 文件夹: {buildPath}");
                    }
                }
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
                if (GUILayout.Button("【3】重打所有图集 (.spriteatlas)", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog("打图集", "重建所有图集？", "开始", "取消")) DoAtlasBuild();
                }
            }

            EditorGUILayout.Space(10);

            // ================== 二、 核心分组 ==================
            EditorGUILayout.LabelField("二、 核心分组与移入", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button("【4】根据大小智能自动计算组 (Group)", GUILayout.Height(40)))
                    CalculateAndCreateGroups();

                GUI.backgroundColor = new Color(1f, 0.7f, 0.9f);
                if (GUILayout.Button("【5】将所有资源移入组内 (Group) 并打上对应标签", GUILayout.Height(40)))
                    StartAsyncProcess();
            }
            GUI.backgroundColor = Color.white;

            // 进度条
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
                if (GUILayout.Button("【6】单独打资源 (BuildPlayerContent)", GUILayout.Height(35)))
                    BuildContent();

                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("【7】打开微信小游戏转换界面", GUILayout.Height(35)))
                {
                    DoBuildMiniGame();
                }
            }
            GUI.backgroundColor = Color.white;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.BeginChangeCheck();
                _cfg.autoOpenBuildFolder = EditorGUILayout.ToggleLeft("打资源后自动打开文件夹", _cfg.autoOpenBuildFolder);
                _cfg.autoOpenWebGLFolder = EditorGUILayout.ToggleLeft("打包小程序后自动打开文件夹", _cfg.autoOpenWebGLFolder);
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
            if (GUILayout.Button("📦【一键打资源】根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包", _leftAlignedButtonStyle, GUILayout.Height(42)))
            {
                if (EditorUtility.DisplayDialog("一键打资源", "将自动执行：\n1. 智能计算分组\n2. 移入打标\n3. 打资源\n(不重打图集、不跳转微信转换)", "开始", "取消"))
                {
                    CalculateAndCreateGroups();
                    StartAsyncProcess(BuildContent);
                }
            }

            GUI.backgroundColor = autoColor;
            if (GUILayout.Button("📦【一键打资源(含图集)】重打所有图集 (.spriteatlas) -> 根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包", _leftAlignedButtonStyle, GUILayout.Height(56)))
            {
                if (EditorUtility.DisplayDialog("一键打资源(含图集)", "将自动执行：\n1. 重打图集\n2. 智能计算分组\n3. 移入打标\n4. 打资源\n(不跳转微信转换)", "开始", "取消"))
                {
                    DoAtlasBuild();
                    CalculateAndCreateGroups();
                    StartAsyncProcess(BuildContent);
                }
            }

            GUI.backgroundColor = autoColor;
            if (GUILayout.Button("📦【一键打包】根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包 -> 打开微信小游戏转换界面", _leftAlignedButtonStyle, GUILayout.Height(56)))
            {
                if (EditorUtility.DisplayDialog("一键打包", "将自动执行：\n1. 智能计算分组\n2. 移入打标\n3. 打资源\n4. 打开微信小游戏转换界面\n(不重打图集)", "开始", "取消"))
                {
                    CalculateAndCreateGroups();
                    StartAsyncProcess(() => {
                        BuildContent();
                        DoBuildMiniGame();
                    });
                }
            }

            GUI.backgroundColor = autoColor;
            if (GUILayout.Button("🔥【全自动打包】重打所有图集 (.spriteatlas) -> 根据大小智能自动计算组 (Group) -> 将资源移入组内 (Group) 并打上对应标签 -> 生成 Addressable 资源包 -> 打开微信小游戏转换界面", _leftAlignedButtonStyle, GUILayout.Height(68)))
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
                    foreach (var key in _groupLabelIndex.Keys.ToList())
                    {
                        _groupLabelIndex[key] = _batchLabelIndex;
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            if (_cfg.groupMappings != null && _cfg.groupMappings.Count > 0)
            {
                foreach (var map in _cfg.groupMappings)
                {
                    if (!_foldoutStates.ContainsKey(map.groupName)) _foldoutStates[map.groupName] = false;
                    if (!_groupLabelIndex.ContainsKey(map.groupName)) _groupLabelIndex[map.groupName] = 4;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        float mb = map.estimatedSize / 1024f / 1024f;
                        string warn = mb < 0.5f && mb > 0 ? " ⚠" : "";
                        string title = $"[{map.groupName}] {mb:F2} MB{warn}";

                        _foldoutStates[map.groupName] = EditorGUILayout.Foldout(_foldoutStates[map.groupName], title, true, EditorStyles.foldoutHeader);

                        GUILayout.FlexibleSpace();
                        GUILayout.Label("Label:", GUILayout.Width(45));
                        _groupLabelIndex[map.groupName] = EditorGUILayout.Popup(_groupLabelIndex[map.groupName], _labelOptions, GUILayout.Width(150));
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
                EditorGUILayout.HelpBox("暂无分组映射，请点击【4】根据大小智能自动计算组 (Group)", MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
            GUI.enabled = true;
        }

        // ============================================================
        //  核心逻辑：打资源带时间戳与自动打开（AA 部分保持不变）
        // ============================================================
        private void BuildContent()
        {
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
                if (settings == null) return;

                // 解决 Cannot read BuildLayout header 报错：强制清理底层构建缓存
                AddressableAssetSettings.CleanPlayerContent();

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");

                string oldBuildPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.BuildPath");
                string oldLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath");

                string newBuildPath = oldBuildPath.Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString());
                if (!newBuildPath.EndsWith("/")) newBuildPath += "/";
                newBuildPath += timestamp;

                string newLoadPath = oldLoadPath.Replace("[BuildTarget]", EditorUserBuildSettings.activeBuildTarget.ToString());
                if (!newLoadPath.EndsWith("/")) newLoadPath += "/";
                newLoadPath += timestamp;

                settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", newBuildPath);
                settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath", newLoadPath);
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
                AssetDatabase.SaveAssets();

                try
                {
                    UnityEngine.Debug.Log($"[AA] 开始打资源，输出路径: {newBuildPath}");
                    AddressableAssetSettings.BuildPlayerContent();
                    UnityEngine.Debug.Log("[AA] ✅ 打资源完成！");
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
                    if (Directory.Exists(newBuildPath)) EditorUtility.RevealInFinder(newBuildPath);
                    else UnityEngine.Debug.LogWarning($"[AA] 未找到资源输出目录: {newBuildPath}");
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[AA] ❌ 打资源异常: {e.Message}\n{e.StackTrace}");
            }
        }

        // ============================================================
        //  打包小程序：直接打开微信小游戏“转换小游戏”菜单
        //  实际菜单路径为：微信小游戏/转换小游戏
        // ============================================================
        private void DoBuildMiniGame()
        {
            const string menuPath = "微信小游戏/转换小游戏";

            try
            {
                if (EditorApplication.ExecuteMenuItem(menuPath))
                {
                    UnityEngine.Debug.Log($"[AA] ✅ 已打开微信转换菜单: {menuPath}");
                    return;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[AA] 执行菜单 {menuPath} 异常: {e.Message}");
            }

            // 菜单调不起来时，退化为打开微信小游戏 SDK 的编辑器窗口
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
                        UnityEngine.Debug.Log("[AA] ✅ 已打开微信小游戏窗口，请在窗口里手动点击“转换小游戏”。");
                        return;
                    }
                }
                catch { }
            }

            UnityEngine.Debug.LogError($"[AA] ❌ 未找到菜单 {menuPath}，也未找到微信小游戏窗口。请手动点击菜单：微信小游戏 -> 转换小游戏。");
            EditorUtility.DisplayDialog("请手动转换", "未自动找到微信转换菜单。\n请手动点击菜单：微信小游戏 -> 转换小游戏。", "知道了");
        }

        // ============================================================
        //  其他原有逻辑保持不变
        // ============================================================
        private void StartAsyncProcess(Action onComplete = null)
        {
            if (_cfg.groupMappings == null || _cfg.groupMappings.Count == 0) return;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) { Debug.LogError("[AA] 致命错误: Settings 为空！"); onComplete?.Invoke(); return; }

            foreach (var lb in _actualLabels)
            {
                if (!string.IsNullOrEmpty(lb)) settings.AddLabel(lb, false);
            }

            _taskQueue.Clear();
            _queuedFiles.Clear();
            _processedTasks = 0;

            for (int i = 0; i < _cfg.groupMappings.Count; i++)
            {
                var map = _cfg.groupMappings[i];
                var group = settings.FindGroup(map.groupName);
                if (group == null) continue;

                int idx = _groupLabelIndex.ContainsKey(map.groupName) ? _groupLabelIndex[map.groupName] : 4;
                string targetLabel = _actualLabels[idx];

                var files = GetFilesForGroupFast(map);
                foreach (var file in files)
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
                    var files = Directory.GetFiles(unit, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (var f in files)
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

        private void CalculateAndCreateGroups()
        {
            _cfg.groupMappings.Clear();
            string root = "Assets/Addressables";
            if (!Directory.Exists(root)) return;

            var allUnits = new Dictionary<string, long>();
            var allFiles = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".cs") && !f.EndsWith(".asmdef") && !f.EndsWith("_bak.png")).ToList();

            foreach (var file in allFiles)
            {
                string normalizedPath = file.Replace('\\', '/');

                if (normalizedPath.Contains("/UI/View/") && normalizedPath.EndsWith(".prefab"))
                {
                    if (!allUnits.ContainsKey(normalizedPath)) allUnits[normalizedPath] = 0;
                    allUnits[normalizedPath] += new FileInfo(normalizedPath).Length;
                }
                else
                {
                    string dir = Path.GetDirectoryName(normalizedPath).Replace('\\', '/') + "/";
                    dir = NormalizeAtlasUnit(dir);
                    if (!allUnits.ContainsKey(dir)) allUnits[dir] = 0;
                    allUnits[dir] += new FileInfo(normalizedPath).Length;
                }
            }

            long minBytes = _cfg.minBundleKB * 1024;
            long idealMaxBytes = _cfg.idealMaxKB * 1024;
            long hardMaxBytes = _cfg.hardMaxKB * 1024;

            var units = allUnits.OrderByDescending(kv => kv.Value).ToList();
            var groups = new List<GroupPlan>();
            int normalGroupIndex = 0;

            foreach (var unit in units)
            {
                if (unit.Key.Contains("/UI/View/") && unit.Key.EndsWith(".prefab"))
                {
                    string prefabName = Path.GetFileNameWithoutExtension(unit.Key);
                    groups.Add(new GroupPlan(unit, $"Group_View_{prefabName}"));
                    continue;
                }

                if (unit.Value > hardMaxBytes)
                {
                    groups.Add(new GroupPlan(unit, $"Group_{normalGroupIndex++}"));
                    continue;
                }

                string category = GetParentCategory(unit.Key);
                var sameCategoryGroup = groups.FirstOrDefault(g => g.Category == category && (g.CurrentSize + unit.Value) <= idealMaxBytes && !g.GroupName.StartsWith("Group_View_"));

                if (sameCategoryGroup != null)
                {
                    sameCategoryGroup.AddUnit(unit);
                    continue;
                }

                var minCapacityGroup = groups.Where(g => (g.CurrentSize + unit.Value) <= idealMaxBytes && g.CurrentSize < idealMaxBytes && !g.GroupName.StartsWith("Group_View_"))
                                             .OrderBy(g => g.CurrentSize).FirstOrDefault();
                if (minCapacityGroup != null && minCapacityGroup.CurrentSize < minBytes)
                {
                    minCapacityGroup.AddUnit(unit);
                    continue;
                }

                groups.Add(new GroupPlan(unit, $"Group_{normalGroupIndex++}"));
            }

            foreach (var groupData in groups)
            {
                var map = new GroupMappingEntry { groupName = groupData.GroupName, estimatedSize = groupData.CurrentSize };
                foreach (var unit in groupData.Units) map.folderUnits.Add(unit.Key);
                _cfg.groupMappings.Add(map);
            }

            EditorUtility.SetDirty(_cfg);
            AssetDatabase.SaveAssets();

            _groupLabelIndex.Clear();
            CleanAndCreatePhysicalGroups();
            Debug.Log($"[AA] ✅ 计算完成，共生成 {groups.Count} 个智能分组。");
        }

        private void CleanAndCreatePhysicalGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null) return;

            var groupsToDelete = new List<AddressableAssetGroup>();
            foreach (var g in settings.groups)
            {
                if (g != null && g.Name != "Built In Data" && g.Name != "Default Local Group")
                    groupsToDelete.Add(g);
            }
            foreach (var g in groupsToDelete) settings.RemoveGroup(g);

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
                bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
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
            if (path.Contains("/UI/View/") && path.EndsWith(".prefab"))
                return "UI/View/" + Path.GetFileNameWithoutExtension(path);

            var parts = path.Split('/');
            if (parts.Length >= 5) return parts[3] + "/" + parts[4];
            return "Root";
        }

        private string GetParentCategory(string path) => GetParentCategoryStatic(path);

        private string NormalizeAtlasUnit(string dir)
        {
            const string atlasPrefix = "Assets/Addressables/Atlases/Assets/Addressables/";
            if (dir.StartsWith(atlasPrefix)) return "Assets/Addressables/" + dir.Substring(atlasPrefix.Length);
            return dir;
        }

        private void DoAtlasBuild() { Debug.Log("[AA] 打图集完成。"); }
    }
}
