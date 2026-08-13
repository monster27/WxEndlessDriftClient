// ==================== ExportTool.cs ====================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class ExportTool : EditorWindow
{
    // 导出模式枚举（中文显示）
    private enum ExportMode
    {
        [InspectorName("客户端导出")]
        Client,
        [InspectorName("服务器导出")]
        Server,
        [InspectorName("一键导出(带时间戳)")]
        ClientWithTimestamp
    }

    // 清除模式枚举
    private enum CleanMode
    {
        [InspectorName("仅列出文件")]
        ListOnly,
        [InspectorName("删除已有文件")]
        DeleteRedundant
    }

    private ExportMode currentMode = ExportMode.Client;
    private CleanMode cleanMode = CleanMode.ListOnly;
    private string exportPath = "";
    private string exportToPath = "";
    private Vector2 scrollPosition;
    private List<ExportFileInfo> exportFiles = new List<ExportFileInfo>();
    private List<ExportFileInfo> exportToFiles = new List<ExportFileInfo>();
    private bool showFileList = false;
    private bool showExportToFileList = false;

    // 扫描结果缓存
    private List<FileScanResult> scanResults = new List<FileScanResult>();
    private bool showScanResults = false;
    private bool hasScanned = false;

    private const string PREFS_KEY_EXPORT_PATH = "ExportTool_LastExportPath";
    private const string PREFS_KEY_EXPORT_TO_PATH = "ExportTool_LastExportToPath";
    private const string PREFS_KEY_EXPORT_MODE = "ExportTool_LastExportMode";
    private const string PREFS_KEY_CLEAN_MODE = "ExportTool_LastCleanMode";

    [MenuItem("Tools/一键导出工具")]
    public static void ShowWindow()
    {
        GetWindow<ExportTool>("一键导出工具");
    }

    private void OnEnable()
    {
        try
        {
            LoadLastExportPath();
            LoadLastExportToPath();
            LoadLastExportMode();
            LoadLastCleanMode();
            RefreshExportFileList();
            RefreshExportToFileList();
        }
        catch (System.NullReferenceException ex)
        {
            if (ex.Message.Contains("AssetStoreDownloadManager"))
            {
                Debug.LogWarning("Unity Package Manager 临时异常，已自动恢复");
            }
            else
            {
                throw;
            }
        }
    }

    private void OnDisable()
    {
        try
        {
            SaveLastExportPath();
            SaveLastExportToPath();
            SaveLastExportMode();
            SaveLastCleanMode();
        }
        catch (System.NullReferenceException ex)
        {
            if (ex.Message.Contains("AssetStoreDownloadManager"))
            {
                Debug.LogWarning("Unity Package Manager 临时异常，路径未保存");
            }
            else
            {
                throw;
            }
        }
    }

    private void LoadLastExportPath()
    {
        exportPath = EditorPrefs.GetString(PREFS_KEY_EXPORT_PATH, GetDesktopPath());
    }

    private void SaveLastExportPath()
    {
        EditorPrefs.SetString(PREFS_KEY_EXPORT_PATH, exportPath);
    }

    private void LoadLastExportToPath()
    {
        exportToPath = EditorPrefs.GetString(PREFS_KEY_EXPORT_TO_PATH, GetDesktopPath());
    }

    private void SaveLastExportToPath()
    {
        EditorPrefs.SetString(PREFS_KEY_EXPORT_TO_PATH, exportToPath);
    }

    private void LoadLastExportMode()
    {
        int mode = EditorPrefs.GetInt(PREFS_KEY_EXPORT_MODE, 0);
        currentMode = (ExportMode)mode;
    }

    private void SaveLastExportMode()
    {
        EditorPrefs.SetInt(PREFS_KEY_EXPORT_MODE, (int)currentMode);
    }

    private void LoadLastCleanMode()
    {
        int mode = EditorPrefs.GetInt(PREFS_KEY_CLEAN_MODE, 0);
        cleanMode = (CleanMode)mode;
    }

    private void SaveLastCleanMode()
    {
        EditorPrefs.SetInt(PREFS_KEY_CLEAN_MODE, (int)cleanMode);
    }

    private string GetDesktopPath()
    {
        return System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
    }

    /// <summary>
    /// 刷新导出到服务器的文件列表（服务器模式）
    /// </summary>
    private void RefreshExportFileList()
    {
        exportFiles.Clear();

        // 获取服务器Shared目录路径
        string serverSharedPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "..", "WxEndlessDriftServer", "Shared");

        Debug.Log($"[ExportTool] ========== 刷新服务器导出列表 ==========");
        Debug.Log($"[ExportTool] 服务器Shared路径: {serverSharedPath}");

        // 1. 获取客户端Resources目录下的JSON数据
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            Debug.Log($"[ExportTool] 扫描Resources目录: {resourcesPath}");
            foreach (string file in Directory.GetFiles(resourcesPath, "*.json", SearchOption.AllDirectories))
            {
                if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                    continue;

                string relativePath = file.Replace(resourcesPath, "").TrimStart('/', '\\');
                exportFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    fileName = Path.GetFileName(file),
                    destinationPath = Path.Combine(serverSharedPath, "Data", relativePath),
                    relativePath = Path.Combine("Data", relativePath),
                    fileType = "JSON数据",
                    color = new Color(0.2f, 0.6f, 1f)
                });
                Debug.Log($"[ExportTool]   📄 JSON: {file} → {Path.Combine("Data", relativePath)}");
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] Resources目录不存在: {resourcesPath}");
        }

        // 2. 获取客户端数据结构（导出到服务器Shared/Structures）
        string structSourcePath = Path.Combine(Application.dataPath, "Plugins", "Json");
        if (Directory.Exists(structSourcePath))
        {
            Debug.Log($"[ExportTool] 扫描数据结构目录: {structSourcePath}");
            foreach (string file in Directory.GetFiles(structSourcePath, "*.cs"))
            {
                string fileName = Path.GetFileName(file);
                exportFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    fileName = fileName,
                    destinationPath = Path.Combine(serverSharedPath, "Structures", fileName),
                    relativePath = Path.Combine("Structures", fileName),
                    fileType = "数据结构",
                    color = new Color(0.2f, 0.8f, 0.2f)
                });
                Debug.Log($"[ExportTool]   📄 数据结构: {file} → {Path.Combine("Structures", fileName)}");
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] 数据结构目录不存在: {structSourcePath}");
        }

        // 3. 获取客户端SharedModels（导出到服务器Shared/SharedModels）
        string clientSharedModelsPath = Path.Combine(Application.dataPath, "Plugins", "SharedModels");
        if (Directory.Exists(clientSharedModelsPath))
        {
            Debug.Log($"[ExportTool] 扫描SharedModels目录: {clientSharedModelsPath}");
            foreach (string file in Directory.GetFiles(clientSharedModelsPath, "*.cs"))
            {
                string fileName = Path.GetFileName(file);
                exportFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    fileName = fileName,
                    destinationPath = Path.Combine(serverSharedPath, "SharedModels", fileName),
                    relativePath = Path.Combine("SharedModels", fileName),
                    fileType = "共享模型",
                    color = new Color(1f, 0.6f, 0.2f)
                });
                Debug.Log($"[ExportTool]   📄 共享模型: {file} → {Path.Combine("SharedModels", fileName)}");
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] SharedModels目录不存在: {clientSharedModelsPath}");
        }

        // 4. 获取游戏事件常量文件（导出到服务器Shared/Events）
        string gameEventConstantsPath = Path.Combine(Application.dataPath, "Scripts", "BaseTool", "GameEventConstants.cs");
        if (File.Exists(gameEventConstantsPath))
        {
            string fileName = Path.GetFileName(gameEventConstantsPath);
            exportFiles.Add(new ExportFileInfo
            {
                sourcePath = gameEventConstantsPath,
                fileName = fileName,
                destinationPath = Path.Combine(serverSharedPath, "Events", fileName),
                relativePath = Path.Combine("Events", fileName),
                fileType = "事件常量",
                color = new Color(1f, 0.4f, 0.7f)
            });
            Debug.Log($"[ExportTool]   📄 事件常量: {gameEventConstantsPath} → {Path.Combine("Events", fileName)}");
        }
        else
        {
            Debug.LogWarning($"[ExportTool] 事件常量文件不存在: {gameEventConstantsPath}");
        }

        Debug.Log($"[ExportTool] 服务器导出列表刷新完成，共 {exportFiles.Count} 个文件");
        Debug.Log($"[ExportTool] =============================================");
    }

    /// <summary>
    /// 刷新导出到另一个目录的文件列表（客户端模式）
    /// </summary>
    private void RefreshExportToFileList()
    {
        exportToFiles.Clear();

        Debug.Log($"[ExportTool] ========== 刷新客户端导出列表 ==========");

        if (string.IsNullOrEmpty(exportToPath))
        {
            Debug.LogWarning($"[ExportTool] 目标路径为空，跳过刷新");
            Debug.Log($"[ExportTool] =============================================");
            return;
        }

        Debug.Log($"[ExportTool] 目标项目根目录: {exportToPath}");

        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            Debug.Log($"[ExportTool] 扫描Resources目录: {resourcesPath}");
            foreach (string file in Directory.GetFiles(resourcesPath, "*.json", SearchOption.AllDirectories))
            {
                if (file.Contains("ProjectSettings") || file.Contains("Packages") || file.Contains("Library"))
                    continue;

                string relativePath = file.Replace(Application.dataPath, "Assets");
                exportToFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    fileName = Path.GetFileName(file),
                    destinationPath = relativePath,
                    relativePath = relativePath,
                    fileType = "JSON文件",
                    color = new Color(0.2f, 0.8f, 0.6f)
                });
                Debug.Log($"[ExportTool]   📄 JSON: {file} → {relativePath}");
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] Resources目录不存在: {resourcesPath}");
        }

        Debug.Log($"[ExportTool] 客户端导出列表刷新完成，共 {exportToFiles.Count} 个文件");
        Debug.Log($"[ExportTool] =============================================");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Space(10);

        // ==================== 模式选择下拉框 ====================
        EditorGUILayout.LabelField("🔧 导出模式", EditorStyles.boldLabel);
        GUILayout.Space(5);

        string[] modeNames = { "客户端导出", "服务器导出", "📁 一键导出(带时间戳)" };
        int selectedIndex = (int)currentMode;
        int newIndex = EditorGUILayout.Popup("选择模式", selectedIndex, modeNames);

        if (newIndex != selectedIndex)
        {
            currentMode = (ExportMode)newIndex;
            SaveLastExportMode();
            if (currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp)
            {
                RefreshExportToFileList();
            }
            else
            {
                RefreshExportFileList();
            }
            scanResults.Clear();
            hasScanned = false;
        }

        GUILayout.Space(10);

        // ==================== 根据模式显示不同内容 ====================
        if (currentMode == ExportMode.Client)
        {
            DrawClientMode();
        }
        else if (currentMode == ExportMode.Server)
        {
            DrawServerMode();
        }
        else // ClientWithTimestamp
        {
            DrawClientWithTimestampMode();
        }

        GUILayout.Space(15);

        // ==================== 清除已有文件区域 ====================
        DrawCleanRedundantSection();

        // ==================== 数据一致性验证按钮（通用） ====================
        GUILayout.Space(10);
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(10);

        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);
        if (GUILayout.Button("🔍 验证数据一致性", GUILayout.Height(30)))
        {
            ValidateData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    // ==================== 新增：带时间戳的导出模式 ====================
    private void DrawClientWithTimestampMode()
    {
        EditorGUILayout.LabelField("📁 一键导出(带时间戳)", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "一键将 Resources 目录中的 JSON 文件导出到带时间戳的文件夹。\n" +
            "文件夹格式: json资源(2026-08-13_14-30-25)\n" +
            "包含完整的 Assets/Resources/ 目录结构。",
            MessageType.Info
        );

        GUILayout.Space(10);

        // 目标根目录（用户选择）
        exportToPath = EditorGUILayout.TextField("目标根目录", exportToPath);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("浏览", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择导出根目录", exportToPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                exportToPath = selectedPath;
                SaveLastExportToPath();
                RefreshExportToFileList();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 显示当前时间戳预览
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string folderName = $"json资源({timestamp})";
        string fullExportPath = string.IsNullOrEmpty(exportToPath) ? "" : Path.Combine(exportToPath, folderName);

        EditorGUILayout.LabelField($"📁 将创建文件夹: {folderName}", EditorStyles.miniLabel);

        if (!string.IsNullOrEmpty(exportToPath))
        {
            EditorGUILayout.LabelField($"📂 完整路径: {fullExportPath}", EditorStyles.miniLabel);
            if (Directory.Exists(fullExportPath))
            {
                EditorGUILayout.LabelField($"⚠️ 该文件夹已存在，将覆盖其中的文件", EditorStyles.miniLabel);
            }
        }

        GUILayout.Space(10);

        // 刷新文件列表
        RefreshExportToFileList();
        EditorGUILayout.LabelField($"📋 找到 {exportToFiles.Count} 个JSON文件", EditorStyles.miniLabel);

        showExportToFileList = EditorGUILayout.Foldout(showExportToFileList, "展开查看JSON文件列表", EditorStyles.foldoutHeader);
        if (showExportToFileList)
        {
            DrawJsonFileList(exportToFiles);
        }

        GUILayout.Space(10);

        // ===== 一键导出按钮 =====
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        GUI.enabled = !string.IsNullOrEmpty(exportToPath) && exportToFiles.Count > 0;
        if (GUILayout.Button($"📦 一键导出 (带时间戳)", GUILayout.Height(45)))
        {
            ExportJsonWithTimestamp();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        if (string.IsNullOrEmpty(exportToPath))
        {
            EditorGUILayout.HelpBox("⚠️ 请选择导出根目录！", MessageType.Warning);
        }
        else if (exportToFiles.Count == 0)
        {
            EditorGUILayout.HelpBox("⚠️ 未找到JSON文件！请检查 Resources 目录。", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"✅ 准备就绪！将导出 {exportToFiles.Count} 个文件到时间戳文件夹。",
                MessageType.Info
            );
        }
    }

    // ==================== 新增：带时间戳的导出方法 ====================
    private void ExportJsonWithTimestamp()
    {
        if (string.IsNullOrEmpty(exportToPath))
        {
            EditorUtility.DisplayDialog("提示", "请选择导出根目录！", "确定");
            return;
        }

        if (!Directory.Exists(exportToPath))
        {
            bool create = EditorUtility.DisplayDialog("目录不存在", $"根目录不存在，是否创建？\n{exportToPath}", "创建", "取消");
            if (create)
            {
                Directory.CreateDirectory(exportToPath);
            }
            else
            {
                return;
            }
        }

        // 生成时间戳文件夹名：json资源(2026-08-13_14-30-25)
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string folderName = $"json资源({timestamp})";
        string fullExportPath = Path.Combine(exportToPath, folderName);

        // 刷新文件列表
        RefreshExportToFileList();

        if (exportToFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "当前项目 Resources 目录中没有找到JSON文件！", "确定");
            return;
        }

        string confirmMsg = $"将导出 {exportToFiles.Count} 个JSON文件到：\n\n" +
                           $"📁 {fullExportPath}\n\n" +
                           $"⚠️ 如果文件夹已存在，其中的同名文件将被覆盖。\n\n" +
                           $"是否继续？";

        if (!EditorUtility.DisplayDialog("确认导出", confirmMsg, "导出", "取消"))
            return;

        Debug.Log($"[ExportTool] ========== 开始带时间戳导出 ==========");
        Debug.Log($"[ExportTool] 导出根目录: {exportToPath}");
        Debug.Log($"[ExportTool] 时间戳文件夹: {folderName}");
        Debug.Log($"[ExportTool] 完整路径: {fullExportPath}");
        Debug.Log($"[ExportTool] 待导出文件数: {exportToFiles.Count}");

        try
        {
            // 创建目标文件夹
            if (!Directory.Exists(fullExportPath))
            {
                Directory.CreateDirectory(fullExportPath);
                Debug.Log($"[ExportTool] 📁 创建导出目录: {fullExportPath}");
            }
            else
            {
                Debug.Log($"[ExportTool] 📁 使用已存在的目录: {fullExportPath}");
            }

            int successCount = 0;
            int failCount = 0;
            List<string> exportedFiles = new List<string>();

            foreach (var fileInfo in exportToFiles)
            {
                try
                {
                    // 目标路径：根目录/时间戳文件夹/Assets/Resources/...
                    string targetFilePath = Path.Combine(fullExportPath, fileInfo.destinationPath);
                    string targetDir = Path.GetDirectoryName(targetFilePath);

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    // 直接覆盖导出
                    File.Copy(fileInfo.sourcePath, targetFilePath, true);
                    successCount++;
                    exportedFiles.Add(fileInfo.destinationPath);
                    Debug.Log($"[ExportTool]   ✅ 导出成功: {fileInfo.destinationPath}");
                }
                catch (System.Exception ex)
                {
                    failCount++;
                    Debug.LogError($"[ExportTool]   ❌ 导出失败 {fileInfo.destinationPath}: {ex.Message}");
                }
            }

            SaveLastExportToPath();

            string resultMsg = $"✅ 导出完成！\n\n" +
                              $"📁 导出位置:\n{fullExportPath}\n\n" +
                              $"📊 统计:\n" +
                              $"✅ 成功: {successCount} 个文件\n" +
                              (failCount > 0 ? $"❌ 失败: {failCount} 个文件\n" : "") +
                              $"\n📂 文件夹名: {folderName}";

            Debug.Log($"[ExportTool] 导出完成: 成功{successCount}, 失败{failCount}");
            Debug.Log($"[ExportTool] 导出目录: {fullExportPath}");
            Debug.Log($"[ExportTool] =============================================");

            // 导出完成后自动打开文件夹
            bool openFolder = EditorUtility.DisplayDialog(
                "导出完成",
                resultMsg + "\n\n是否打开导出文件夹？",
                "打开文件夹",
                "关闭"
            );

            if (openFolder)
            {
                System.Diagnostics.Process.Start(fullExportPath);
            }

            scanResults.Clear();
            hasScanned = false;
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("导出失败", $"导出过程中发生错误：\n{ex.Message}", "确定");
            Debug.LogError($"[ExportTool] 导出错误: {ex}");
        }
    }

    /// <summary>
    /// 绘制清除已有文件区域
    /// </summary>
    private void DrawCleanRedundantSection()
    {
        GUILayout.Box("", GUILayout.Height(2), GUILayout.ExpandWidth(true));
        GUILayout.Space(8);

        EditorGUILayout.LabelField("🧹 清除已有文件", EditorStyles.boldLabel);
        GUILayout.Space(3);

        EditorGUILayout.HelpBox(
            "扫描目标目录，按文件名对比：\n" +
            "• 已有文件：与源目录文件名相同的文件（可删除）\n" +
            "• 其他文件：目标目录中多出的其他文件（仅显示）",
            MessageType.Info
        );

        GUILayout.Space(5);

        string[] cleanModeNames = { "仅列出文件", "删除已有文件" };
        int cleanSelected = (int)cleanMode;
        int cleanNew = EditorGUILayout.Popup("清除模式", cleanSelected, cleanModeNames);
        cleanMode = (CleanMode)cleanNew;

        GUILayout.Space(5);

        string basePath = currentMode == ExportMode.Client ? exportToPath :
                          (currentMode == ExportMode.ClientWithTimestamp ? exportToPath : exportPath);

        GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
        if (GUILayout.Button("🔍 扫描目标目录", GUILayout.Height(30)))
        {
            ScanTargetDirectory(basePath);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        if (hasScanned)
        {
            showScanResults = EditorGUILayout.Foldout(showScanResults, $"📋 扫描结果 ({scanResults.Count} 个文件)", EditorStyles.foldoutHeader);
            if (showScanResults)
            {
                DrawScanResults();
            }

            GUILayout.Space(5);

            int redundantCount = scanResults.Count(r => r.status == FileStatus.Redundant);
            int otherCount = scanResults.Count(r => r.status == FileStatus.Other);

            EditorGUILayout.LabelField($"📊 已有文件: {redundantCount} 个  其他文件: {otherCount} 个", EditorStyles.miniLabel);

            GUILayout.Space(5);

            if (cleanMode == CleanMode.DeleteRedundant)
            {
                if (redundantCount > 0)
                {
                    GUI.backgroundColor = new Color(1f, 0.5f, 0.3f);
                    if (GUILayout.Button($"🗑️ 删除 {redundantCount} 个已有文件", GUILayout.Height(30)))
                    {
                        bool confirm = EditorUtility.DisplayDialog(
                            "⚠️ 确认删除已有文件",
                            $"将删除 {redundantCount} 个已有文件。\n\n" +
                            $"这些文件与源目录文件名相同，可能是旧版本遗留。\n\n" +
                            $"⚠️ 此操作不可恢复！",
                            "确定删除",
                            "取消"
                        );
                        if (confirm)
                        {
                            DeleteRedundantFiles(basePath);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    EditorGUILayout.LabelField("✅ 没有已有文件需要删除", EditorStyles.miniLabel);
                }
            }
            else
            {
                GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);
                if (GUILayout.Button($"📋 打印完整报告到控制台", GUILayout.Height(25)))
                {
                    PrintFullReport(basePath);
                }
                GUI.backgroundColor = Color.white;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                EditorGUILayout.LabelField("💡 点击「扫描目标目录」查看文件状态", EditorStyles.miniLabel);
            }
        }

        if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
        {
            EditorGUILayout.HelpBox("⚠️ 请先设置有效路径！", MessageType.Warning);
        }
    }

    private void DrawScanResults()
    {
        GUILayout.BeginVertical("Box");

        if (scanResults.Count == 0)
        {
            GUILayout.Label("  没有找到文件", EditorStyles.miniLabel);
        }
        else
        {
            var grouped = scanResults.GroupBy(r => r.status);

            foreach (var group in grouped)
            {
                string statusName = group.Key == FileStatus.Redundant ? "🔄 已有文件 (可删除)" : "📄 其他文件 (仅显示)";
                Color statusColor = group.Key == FileStatus.Redundant ? new Color(1f, 0.4f, 0.3f) : new Color(0.3f, 0.6f, 1f);

                GUILayout.Space(3);
                GUI.color = statusColor;
                EditorGUILayout.LabelField($"━━━━ {statusName} ({group.Count()} 个) ━━━━", EditorStyles.boldLabel);
                GUI.color = Color.white;

                foreach (var result in group)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);

                    string icon = result.status == FileStatus.Redundant ? "🗑️" : "📄";
                    GUILayout.Label($"{icon} {result.fileName}", EditorStyles.miniLabel);
                    if (result.status == FileStatus.Other)
                    {
                        GUILayout.Label($"  (路径: {result.relativePath})", EditorStyles.miniLabel);
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.EndVertical();
    }

    /// <summary>
    /// 扫描目标目录 - 按文件名对比
    /// </summary>
    private void ScanTargetDirectory(string basePath)
    {
        scanResults.Clear();
        hasScanned = false;

        if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
        {
            EditorUtility.DisplayDialog("提示", "请先设置有效的目标路径！", "确定");
            return;
        }

        Debug.Log($"[ExportTool] ========== 开始扫描目标目录 ==========");
        Debug.Log($"[ExportTool] 目标目录根路径: {basePath}");
        Debug.Log($"[ExportTool] 当前模式: {(currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp ? "客户端导出" : "服务器导出")}");

        try
        {
            string actualBasePath = basePath;
            if (currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp)
            {
                string assetsPath = Path.Combine(basePath, "Assets");
                if (Directory.Exists(assetsPath))
                {
                    actualBasePath = assetsPath;
                    Debug.Log($"[ExportTool] 客户端模式: 使用 Assets 目录作为目标路径: {actualBasePath}");
                }
                else
                {
                    Debug.LogWarning($"[ExportTool] Assets 目录不存在，使用原始路径: {actualBasePath}");
                }
            }

            HashSet<string> sourceFileNames = new HashSet<string>();

            if (currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp)
            {
                string resourcesPath = Path.Combine(Application.dataPath, "Resources");
                Debug.Log($"[ExportTool] 源目录(客户端Resources): {resourcesPath}");

                if (exportToFiles != null && exportToFiles.Count > 0)
                {
                    Debug.Log($"[ExportTool] 从导出列表获取 {exportToFiles.Count} 个文件名");
                    foreach (var fileInfo in exportToFiles)
                    {
                        sourceFileNames.Add(fileInfo.fileName);
                    }
                }
                else if (Directory.Exists(resourcesPath))
                {
                    Debug.Log($"[ExportTool] 导出列表为空，直接从Resources目录读取");
                    foreach (string file in Directory.GetFiles(resourcesPath, "*", SearchOption.AllDirectories))
                    {
                        if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                            continue;
                        string fileName = Path.GetFileName(file);
                        sourceFileNames.Add(fileName);
                        Debug.Log($"[ExportTool]   添加源文件: {fileName}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[ExportTool] Resources目录不存在: {resourcesPath}");
                }
            }
            else
            {
                if (exportFiles != null && exportFiles.Count > 0)
                {
                    Debug.Log($"[ExportTool] 从导出列表获取 {exportFiles.Count} 个文件名");
                    foreach (var fileInfo in exportFiles)
                    {
                        sourceFileNames.Add(fileInfo.fileName);
                    }
                }
                else
                {
                    Debug.Log($"[ExportTool] 导出列表为空，尝试从源目录读取");

                    string[] sourceDirs = {
                        Path.Combine(Application.dataPath, "Resources"),
                        Path.Combine(Application.dataPath, "Plugins", "Json"),
                        Path.Combine(Application.dataPath, "Plugins", "SharedModels"),
                        Path.Combine(Application.dataPath, "Scripts", "BaseTool")
                    };

                    foreach (string dir in sourceDirs)
                    {
                        if (Directory.Exists(dir))
                        {
                            Debug.Log($"[ExportTool] 扫描源目录: {dir}");
                            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                            {
                                if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                                    continue;
                                string ext = Path.GetExtension(file).ToLower();
                                if (ext == ".cs" || ext == ".json")
                                {
                                    string fileName = Path.GetFileName(file);
                                    sourceFileNames.Add(fileName);
                                    Debug.Log($"[ExportTool]   添加源文件: {fileName}");
                                }
                            }
                        }
                    }
                }
            }

            Debug.Log($"[ExportTool] 源文件名称列表: 共 {sourceFileNames.Count} 个");

            string[] targetExtensions = { "*.json", "*.cs" };
            List<string> targetFiles = new List<string>();

            Debug.Log($"[ExportTool] 扫描目标目录: {actualBasePath}");
            Debug.Log($"[ExportTool] 扫描文件扩展名: {string.Join(", ", targetExtensions)}");

            string[] scanDirs;
            if (currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp)
            {
                string resourcesDir = Path.Combine(actualBasePath, "Resources");
                if (Directory.Exists(resourcesDir))
                {
                    scanDirs = new string[] { resourcesDir };
                    Debug.Log($"[ExportTool] 客户端模式: 只扫描 Resources 目录: {resourcesDir}");
                }
                else
                {
                    Debug.LogWarning($"[ExportTool] 客户端模式: Resources 目录不存在: {resourcesDir}");
                    scanDirs = new string[] { actualBasePath };
                }
            }
            else
            {
                string[] exportDirs = { "Data", "Structures", "SharedModels", "Events" };
                scanDirs = exportDirs
                    .Select(d => Path.Combine(actualBasePath, d))
                    .Where(Directory.Exists)
                    .ToArray();
                Debug.Log($"[ExportTool] 服务器模式: 扫描 {scanDirs.Length} 个子目录");
            }

            foreach (string scanDir in scanDirs)
            {
                if (!Directory.Exists(scanDir))
                    continue;

                Debug.Log($"[ExportTool] 扫描目录: {scanDir}");

                foreach (string ext in targetExtensions)
                {
                    foreach (string file in Directory.GetFiles(scanDir, ext, SearchOption.AllDirectories))
                    {
                        if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                            continue;

                        string relativePath = file.Replace(actualBasePath, "").TrimStart('/', '\\');
                        targetFiles.Add(relativePath);
                        Debug.Log($"[ExportTool]   找到目标文件: {relativePath}");
                    }
                }
            }

            Debug.Log($"[ExportTool] 目标目录相关文件总数: {targetFiles.Count}");

            foreach (string targetFile in targetFiles)
            {
                string fileName = Path.GetFileName(targetFile);

                if (sourceFileNames.Contains(fileName))
                {
                    scanResults.Add(new FileScanResult
                    {
                        fileName = fileName,
                        relativePath = targetFile,
                        status = FileStatus.Redundant,
                        fullPath = Path.Combine(actualBasePath, targetFile)
                    });
                    Debug.Log($"[ExportTool]   🔄 已有文件(冗余): {targetFile} (文件名: {fileName})");
                }
                else
                {
                    scanResults.Add(new FileScanResult
                    {
                        fileName = fileName,
                        relativePath = targetFile,
                        status = FileStatus.Other,
                        fullPath = Path.Combine(actualBasePath, targetFile)
                    });
                    Debug.Log($"[ExportTool]   📄 其他文件: {targetFile} (文件名: {fileName})");
                }
            }

            scanResults = scanResults.OrderByDescending(r => r.status).ToList();

            hasScanned = true;
            int redundantCount = scanResults.Count(r => r.status == FileStatus.Redundant);
            int otherCount = scanResults.Count(r => r.status == FileStatus.Other);

            Debug.Log($"[ExportTool] 扫描完成！找到 {redundantCount} 个已有文件，{otherCount} 个其他文件");
            Debug.Log($"[ExportTool] =============================================");

            EditorUtility.DisplayDialog("扫描完成", $"扫描完成！\n\n🔄 已有文件: {redundantCount} 个\n📄 其他文件: {otherCount} 个", "确定");
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("扫描失败", $"扫描过程中发生错误：\n{ex.Message}", "确定");
            Debug.LogError($"[ExportTool] 扫描文件错误: {ex}");
        }
    }

    private void DeleteRedundantFiles(string basePath)
    {
        var redundantFiles = scanResults.Where(r => r.status == FileStatus.Redundant).ToList();

        if (redundantFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有已有文件需要删除！", "确定");
            return;
        }

        int deletedCount = 0;
        int failCount = 0;
        List<string> deletedList = new List<string>();

        Debug.Log($"[ExportTool] ========== 开始删除已有文件 ==========");
        Debug.Log($"[ExportTool] 目标目录: {basePath}");
        Debug.Log($"[ExportTool] 待删除文件数: {redundantFiles.Count}");

        try
        {
            foreach (var result in redundantFiles)
            {
                string fullPath = result.fullPath;
                if (File.Exists(fullPath))
                {
                    try
                    {
                        string backupPath = fullPath + ".backup";
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        File.Delete(fullPath);
                        deletedCount++;
                        deletedList.Add(result.fileName);
                        Debug.Log($"[ExportTool]   🗑️ 已删除: {result.relativePath} (完整路径: {fullPath})");
                    }
                    catch (System.Exception ex)
                    {
                        failCount++;
                        Debug.LogError($"[ExportTool]   ❌ 删除失败 {result.relativePath}: {ex.Message}");
                    }
                }
            }

            int deletedDirs = DeleteEmptyExportDirectories(basePath);

            string resultMsg = $"🧹 清除已有文件完成！\n\n";
            resultMsg += $"✅ 已删除: {deletedCount} 个文件\n";
            resultMsg += $"📁 已删除空目录: {deletedDirs} 个\n";
            if (failCount > 0)
            {
                resultMsg += $"❌ 删除失败: {failCount} 个\n\n";
            }
            if (deletedList.Count > 0)
            {
                resultMsg += $"已删除文件:\n{string.Join("\n", deletedList.Take(20))}";
                if (deletedList.Count > 20)
                {
                    resultMsg += $"\n... 共 {deletedList.Count} 个";
                }
            }

            Debug.Log($"[ExportTool] 清除完成：删除 {deletedCount} 个文件，{deletedDirs} 个空目录");
            Debug.Log($"[ExportTool] =============================================");

            EditorUtility.DisplayDialog("清除完成", resultMsg, "确定");

            ScanTargetDirectory(basePath);
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("清除失败", $"清除过程中发生错误：\n{ex.Message}", "确定");
            Debug.LogError($"[ExportTool] 清除已有文件错误: {ex}");
        }
    }

    private int DeleteEmptyExportDirectories(string rootPath)
    {
        int deletedCount = 0;

        try
        {
            if (!Directory.Exists(rootPath))
                return 0;

            string[] allDirs = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories);
            var sortedDirs = allDirs.OrderByDescending(d => d.Length);

            foreach (string dir in sortedDirs)
            {
                try
                {
                    if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                    {
                        string dirName = Path.GetFileName(dir);
                        string[] exportDirs = { "Data", "Structures", "SharedModels", "Events" };
                        if (exportDirs.Contains(dirName))
                        {
                            Directory.Delete(dir);
                            deletedCount++;
                            Debug.Log($"[ExportTool]   📁 已删除空目录: {dir}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ExportTool]   删除目录失败 {dir}: {ex.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ExportTool] 删除空目录时发生错误: {ex.Message}");
        }

        return deletedCount;
    }

    private void PrintFullReport(string basePath)
    {
        if (scanResults.Count == 0)
        {
            Debug.Log("[ExportTool] 📋 没有文件");
            return;
        }

        var redundantFiles = scanResults.Where(r => r.status == FileStatus.Redundant).ToList();
        var otherFiles = scanResults.Where(r => r.status == FileStatus.Other).ToList();

        Debug.Log($"[ExportTool] ========== 📊 完整扫描报告 ==========");
        Debug.Log($"[ExportTool] 📁 目标目录根路径: {basePath}");
        Debug.Log($"[ExportTool] 📅 扫描时间: {System.DateTime.Now}");
        Debug.Log($"[ExportTool] 当前模式: {(currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp ? "客户端导出" : "服务器导出")}");
        Debug.Log($"[ExportTool] ");
        Debug.Log($"[ExportTool] 🔄 已有文件 ({redundantFiles.Count} 个) - 文件名与源目录相同，建议删除");
        Debug.Log($"[ExportTool] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        foreach (var file in redundantFiles)
        {
            Debug.Log($"[ExportTool]    🗑️ 相对路径: {file.relativePath}");
            Debug.Log($"[ExportTool]        完整路径: {file.fullPath}");
        }

        if (otherFiles.Count > 0)
        {
            Debug.Log($"[ExportTool] ");
            Debug.Log($"[ExportTool] 📄 其他文件 ({otherFiles.Count} 个) - 文件名不在源目录中，可能是手动创建");
            Debug.Log($"[ExportTool] ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            foreach (var file in otherFiles)
            {
                Debug.Log($"[ExportTool]    📄 相对路径: {file.relativePath}");
                Debug.Log($"[ExportTool]        完整路径: {file.fullPath}");
            }
        }

        Debug.Log($"[ExportTool] ");
        Debug.Log($"[ExportTool] ========== 📊 统计 ==========");
        Debug.Log($"[ExportTool] 总文件数: {scanResults.Count}");
        Debug.Log($"[ExportTool] 🔄 已有文件: {redundantFiles.Count}");
        Debug.Log($"[ExportTool] 📄 其他文件: {otherFiles.Count}");
        Debug.Log($"[ExportTool] =============================================");

        EditorUtility.DisplayDialog(
            "报告已输出",
            $"完整报告已输出到控制台！\n\n" +
            $"📊 统计:\n" +
            $"总文件数: {scanResults.Count}\n" +
            $"🔄 已有文件: {redundantFiles.Count}\n" +
            $"📄 其他文件: {otherFiles.Count}",
            "确定"
        );
    }

    private void DrawClientMode()
    {
        EditorGUILayout.LabelField("📦 客户端导出 - 导出Resources中的JSON", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "将当前Unity项目 Resources 目录中的所有JSON文件导出到另一个目录，\n" +
            "并保持相同的 Assets/Resources/ 目录结构。",
            MessageType.Info
        );

        GUILayout.Space(10);

        exportToPath = EditorGUILayout.TextField("目标项目根目录", exportToPath);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("浏览", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择目标项目根目录（包含Assets文件夹）", exportToPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                exportToPath = selectedPath;
                RefreshExportToFileList();
                SaveLastExportToPath();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        EditorGUILayout.LabelField($"📋 找到 {exportToFiles.Count} 个JSON文件", EditorStyles.miniLabel);

        if (!string.IsNullOrEmpty(exportToPath))
        {
            string targetAssetsPath = Path.Combine(exportToPath, "Assets");
            if (Directory.Exists(targetAssetsPath))
            {
                EditorGUILayout.LabelField($"✅ 目标Assets目录存在", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"⚠️ 目标Assets目录不存在，将自动创建", EditorStyles.miniLabel);
            }
        }

        showExportToFileList = EditorGUILayout.Foldout(showExportToFileList, "展开查看JSON文件列表", EditorStyles.foldoutHeader);
        if (showExportToFileList)
        {
            DrawJsonFileList(exportToFiles);
        }

        GUILayout.Space(10);

        GUI.backgroundColor = string.IsNullOrEmpty(exportToPath) ? Color.gray : new Color(0.2f, 0.7f, 0.4f);
        GUI.enabled = !string.IsNullOrEmpty(exportToPath);
        if (GUILayout.Button("📦 一键导出JSON到目标项目", GUILayout.Height(40)))
        {
            ExportJsonToAnotherProject();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        if (string.IsNullOrEmpty(exportToPath))
        {
            EditorGUILayout.HelpBox("⚠️ 请填写目标项目根目录！", MessageType.Warning);
        }
    }

    private void DrawServerMode()
    {
        EditorGUILayout.LabelField("📤 服务器导出 - 导出到服务器Shared目录", EditorStyles.boldLabel);
        GUILayout.Space(5);

        EditorGUILayout.HelpBox(
            "将客户端的数据结构、共享模型、Resources中的JSON配置等导出到服务器Shared目录。",
            MessageType.Info
        );

        GUILayout.Space(10);

        exportPath = EditorGUILayout.TextField("服务器Shared目录", exportPath);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("浏览", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择服务器Shared目录", exportPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                exportPath = selectedPath;
                SaveLastExportPath();
                RefreshExportFileList();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        EditorGUILayout.LabelField($"📋 待导出文件 ({exportFiles.Count} 个)", EditorStyles.boldLabel);

        GUILayout.BeginVertical("Box");
        EditorGUILayout.LabelField("🔍 检索文件夹路径", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"• JSON数据: Assets/Resources", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"• 数据结构: Assets/Plugins/Json", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"• 共享模型: Assets/Plugins/SharedModels", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"• 事件常量: Assets/Scripts/BaseTool", EditorStyles.miniLabel);
        GUILayout.EndVertical();

        showFileList = EditorGUILayout.Foldout(showFileList, "展开查看所有文件", EditorStyles.foldoutHeader);
        if (showFileList)
        {
            DrawFileList(exportFiles);
        }

        GUILayout.Space(10);

        GUI.backgroundColor = string.IsNullOrEmpty(exportPath) ? Color.gray : Color.green;
        GUI.enabled = !string.IsNullOrEmpty(exportPath);
        if (GUILayout.Button("🔄 一键导出到服务器", GUILayout.Height(35)))
        {
            ExportAll();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        if (string.IsNullOrEmpty(exportPath))
        {
            EditorGUILayout.HelpBox("⚠️ 请填写服务器Shared目录路径！", MessageType.Warning);
        }
    }

    private void DrawFileList(List<ExportFileInfo> files)
    {
        GUILayout.BeginVertical("Box");

        if (files.Count == 0)
        {
            GUILayout.Label("  暂无可导出的文件", EditorStyles.miniLabel);
        }
        else
        {
            var groupedFiles = files.GroupBy(f => f.fileType);

            foreach (var group in groupedFiles)
            {
                GUILayout.Space(5);

                var firstFile = group.First();
                GUI.color = firstFile.color;
                EditorGUILayout.LabelField($"━━━━ {group.Key} ({group.Count()} 个) ━━━━", EditorStyles.boldLabel);
                GUI.color = Color.white;

                GUILayout.Space(5);

                foreach (var fileInfo in group)
                {
                    GUILayout.BeginHorizontal();

                    GUILayout.Label("●", GUILayout.Width(20));
                    GUI.color = fileInfo.color;

                    string displayPath = fileInfo.sourcePath.Replace(Application.dataPath.Replace("/Assets", ""), "");
                    GUILayout.Label(displayPath, EditorStyles.miniLabel);

                    GUI.color = Color.white;

                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.EndVertical();
    }

    private void DrawJsonFileList(List<ExportFileInfo> jsonFiles)
    {
        GUILayout.BeginVertical("Box");

        if (jsonFiles.Count == 0)
        {
            GUILayout.Label("  未找到JSON文件", EditorStyles.miniLabel);
        }
        else
        {
            var grouped = jsonFiles.GroupBy(f => Path.GetDirectoryName(f.destinationPath));

            foreach (var group in grouped)
            {
                GUILayout.Space(3);
                EditorGUILayout.LabelField($"📁 {group.Key}", EditorStyles.miniBoldLabel);

                foreach (var file in group)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    GUILayout.Label($"📄 {Path.GetFileName(file.sourcePath)}", EditorStyles.miniLabel);
                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.EndVertical();
    }

    private void ExportJsonToAnotherProject()
    {
        if (string.IsNullOrEmpty(exportToPath))
        {
            EditorUtility.DisplayDialog("提示", "请填写目标项目根目录！", "确定");
            return;
        }

        string targetAssetsPath = Path.Combine(exportToPath, "Assets");
        if (!Directory.Exists(targetAssetsPath))
        {
            bool create = EditorUtility.DisplayDialog("目录不存在", $"目标Assets目录不存在，是否创建？\n{targetAssetsPath}", "创建", "取消");
            if (create)
            {
                Directory.CreateDirectory(targetAssetsPath);
            }
            else
            {
                return;
            }
        }

        RefreshExportToFileList();

        if (exportToFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "当前项目 Resources 目录中没有找到JSON文件！", "确定");
            return;
        }

        string confirmMsg = $"将导出 {exportToFiles.Count} 个JSON文件到目标项目：\n\n" +
                           $"源项目: {Application.dataPath}\n" +
                           $"目标项目: {exportToPath}\n\n" +
                           $"是否继续？";
        if (!EditorUtility.DisplayDialog("确认导出", confirmMsg, "导出", "取消"))
            return;

        Debug.Log($"[ExportTool] ========== 开始客户端导出 ==========");
        Debug.Log($"[ExportTool] 源项目: {Application.dataPath}");
        Debug.Log($"[ExportTool] 目标项目: {exportToPath}");
        Debug.Log($"[ExportTool] 待导出文件数: {exportToFiles.Count}");

        try
        {
            int successCount = 0;
            int skipCount = 0;
            int failCount = 0;

            foreach (var fileInfo in exportToFiles)
            {
                try
                {
                    string targetFilePath = Path.Combine(exportToPath, fileInfo.destinationPath);
                    string targetDir = Path.GetDirectoryName(targetFilePath);

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    if (File.Exists(targetFilePath))
                    {
                        string sourceContent = File.ReadAllText(fileInfo.sourcePath);
                        string destContent = File.ReadAllText(targetFilePath);

                        if (sourceContent == destContent)
                        {
                            skipCount++;
                            Debug.Log($"[ExportTool]   ⏭️ 跳过(内容相同): {fileInfo.destinationPath}");
                            continue;
                        }

                        string backupPath = targetFilePath + ".backup";
                        if (!File.Exists(backupPath))
                        {
                            File.Copy(targetFilePath, backupPath);
                            Debug.Log($"[ExportTool]   💾 备份: {backupPath}");
                        }
                    }

                    File.Copy(fileInfo.sourcePath, targetFilePath, true);
                    successCount++;
                    Debug.Log($"[ExportTool]   ✅ 导出成功: {fileInfo.destinationPath}");
                }
                catch (System.Exception ex)
                {
                    failCount++;
                    Debug.LogError($"[ExportTool]   ❌ 导出失败 {fileInfo.destinationPath}: {ex.Message}");
                }
            }

            SaveLastExportToPath();

            string resultMsg = $"导出完成！\n\n" +
                              $"✅ 成功: {successCount} 个文件\n" +
                              $"⏭️ 跳过（内容相同）: {skipCount} 个文件\n" +
                              $"❌ 失败: {failCount} 个文件\n\n" +
                              $"目标目录:\n{exportToPath}";

            Debug.Log($"[ExportTool] 导出完成: 成功{successCount}, 跳过{skipCount}, 失败{failCount}");
            Debug.Log($"[ExportTool] =============================================");

            EditorUtility.DisplayDialog("导出完成", resultMsg, "确定");

            scanResults.Clear();
            hasScanned = false;
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("导出失败", $"导出过程中发生错误:\n{ex.Message}", "确定");
            Debug.LogError($"[ExportTool] 导出JSON错误: {ex}");
        }
    }

    private void ExportAll()
    {
        if (string.IsNullOrEmpty(exportPath))
        {
            EditorUtility.DisplayDialog("提示", "请填写服务器Shared目录路径！", "确定");
            return;
        }

        if (!Directory.Exists(exportPath))
        {
            bool createDir = EditorUtility.DisplayDialog("路径不存在", $"目录不存在，是否创建？\n{exportPath}", "创建", "取消");
            if (createDir)
            {
                Directory.CreateDirectory(exportPath);
            }
            else
            {
                return;
            }
        }

        Debug.Log($"[ExportTool] ========== 开始服务器导出 ==========");
        Debug.Log($"[ExportTool] 服务器Shared目录: {exportPath}");
        Debug.Log($"[ExportTool] 待导出文件数: {exportFiles.Count}");

        try
        {
            int successCount = 0;
            int failCount = 0;

            foreach (var fileInfo in exportFiles)
            {
                try
                {
                    string destFullPath = Path.Combine(exportPath, fileInfo.destinationPath);
                    string destDir = Path.GetDirectoryName(destFullPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    if (File.Exists(destFullPath))
                    {
                        string sourceContent = File.ReadAllText(fileInfo.sourcePath);
                        string destContent = File.ReadAllText(destFullPath);

                        if (sourceContent == destContent)
                        {
                            continue;
                        }
                        else
                        {
                            string backupPath = destFullPath + ".backup";
                            if (!File.Exists(backupPath))
                            {
                                File.Copy(destFullPath, backupPath);
                                Debug.Log($"[ExportTool]   💾 备份: {backupPath}");
                            }
                        }
                    }

                    File.Copy(fileInfo.sourcePath, destFullPath, true);
                    successCount++;
                    Debug.Log($"[ExportTool]   ✅ 导出成功: {fileInfo.destinationPath}");
                }
                catch (System.Exception e)
                {
                    failCount++;
                    Debug.LogError($"[ExportTool]   ❌ 导出失败 {fileInfo.sourcePath}: {e.Message}");
                }
            }

            SaveLastExportPath();

            string message = $"导出完成！\n\n";
            message += $"成功: {successCount} 个文件\n";
            if (failCount > 0)
            {
                message += $"失败: {failCount} 个文件\n";
            }
            message += $"\n导出目录:\n{exportPath}";

            Debug.Log($"[ExportTool] 导出完成: 成功{successCount}, 失败{failCount}");
            Debug.Log($"[ExportTool] =============================================");

            EditorUtility.DisplayDialog("导出完成", message, "确定");

            scanResults.Clear();
            hasScanned = false;
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导出失败", $"导出过程中发生错误:\n{e.Message}", "确定");
            Debug.LogError($"[ExportTool] 导出错误: {e}");
        }
    }

    private void ValidateData()
    {
        Debug.Log($"[ExportTool] ========== 开始数据一致性验证 ==========");
        Debug.Log($"[ExportTool] 当前模式: {(currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp ? "客户端导出" : "服务器导出")}");

        string report;
        bool isConsistent = ValidateDataConsistency(out report);

        Debug.Log($"[ExportTool] 验证结果: {(isConsistent ? "✅ 一致" : "❌ 不一致")}");
        Debug.Log($"[ExportTool] 详细报告:\n{report}");
        Debug.Log($"[ExportTool] =============================================");

        if (isConsistent)
        {
            EditorUtility.DisplayDialog("数据一致性验证", report, "确定");
        }
        else
        {
            bool syncNow = EditorUtility.DisplayDialog("数据一致性验证", report + "\n\n是否立即同步数据？", "同步", "取消");
            if (syncNow)
            {
                if ((currentMode == ExportMode.Client || currentMode == ExportMode.ClientWithTimestamp) && !string.IsNullOrEmpty(exportToPath))
                {
                    ExportJsonToAnotherProject();
                }
                else if (currentMode == ExportMode.Server && !string.IsNullOrEmpty(exportPath))
                {
                    ExportAll();
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请先设置导出路径！", "确定");
                }
            }
        }
    }

    public static bool ValidateDataConsistency(out string report)
    {
        report = "";
        bool isConsistent = true;
        int totalFiles = 0;
        int consistentCount = 0;
        int inconsistentCount = 0;

        string serverSharedPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "..", "WxEndlessDriftServer", "Shared");

        Debug.Log($"[ExportTool] ========== 数据一致性验证详情 ==========");
        Debug.Log($"[ExportTool] 服务器Shared路径: {serverSharedPath}");

        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
            Debug.Log($"[ExportTool] 检查Resources目录: {resourcesPath}");
            foreach (string clientFile in Directory.GetFiles(resourcesPath, "*.json", SearchOption.AllDirectories))
            {
                if (clientFile.Contains("ProjectSettings") || clientFile.Contains("Packages"))
                    continue;

                string relativePath = clientFile.Replace(resourcesPath, "").TrimStart('/', '\\');
                string serverFile = Path.Combine(serverSharedPath, "Data", relativePath);

                totalFiles++;
                bool filesMatch = false;

                if (File.Exists(serverFile))
                {
                    string clientContent = File.ReadAllText(clientFile);
                    string serverContent = File.ReadAllText(serverFile);
                    filesMatch = clientContent == serverContent;
                }

                if (filesMatch)
                {
                    consistentCount++;
                    Debug.Log($"[ExportTool]   ✅ 一致: Data/{relativePath}");
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    Debug.Log($"[ExportTool]   ❌ 不一致: Data/{relativePath}{(File.Exists(serverFile) ? "" : " (服务器端不存在)")}");
                    report += $"\n❌ 不一致: {relativePath}";
                    if (!File.Exists(serverFile))
                    {
                        report += " (服务器端不存在)";
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] Resources目录不存在: {resourcesPath}");
        }

        string clientSharedModelsPath = Path.Combine(Application.dataPath, "Plugins", "SharedModels");
        string serverSharedModelsPath = Path.Combine(serverSharedPath, "SharedModels");

        if (Directory.Exists(clientSharedModelsPath))
        {
            Debug.Log($"[ExportTool] 检查SharedModels目录: {clientSharedModelsPath}");
            foreach (string clientFile in Directory.GetFiles(clientSharedModelsPath, "*.cs"))
            {
                string fileName = Path.GetFileName(clientFile);
                string serverFile = Path.Combine(serverSharedModelsPath, fileName);

                totalFiles++;
                bool filesMatch = false;

                if (File.Exists(serverFile))
                {
                    string clientContent = File.ReadAllText(clientFile);
                    string serverContent = File.ReadAllText(serverFile);
                    filesMatch = clientContent == serverContent;
                }

                if (filesMatch)
                {
                    consistentCount++;
                    Debug.Log($"[ExportTool]   ✅ 一致: SharedModels/{fileName}");
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    Debug.Log($"[ExportTool]   ❌ 不一致: SharedModels/{fileName}{(File.Exists(serverFile) ? "" : " (服务器端不存在)")}");
                    report += $"\n❌ 不一致: SharedModels/{fileName}";
                    if (!File.Exists(serverFile))
                    {
                        report += " (服务器端不存在)";
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] SharedModels目录不存在: {clientSharedModelsPath}");
        }

        string clientStructPath = Path.Combine(Application.dataPath, "Plugins", "Json");
        string serverStructPath = Path.Combine(serverSharedPath, "Structures");

        if (Directory.Exists(clientStructPath))
        {
            Debug.Log($"[ExportTool] 检查Structures目录: {clientStructPath}");
            foreach (string clientFile in Directory.GetFiles(clientStructPath, "*.cs"))
            {
                string fileName = Path.GetFileName(clientFile);
                string serverFile = Path.Combine(serverStructPath, fileName);

                totalFiles++;
                bool filesMatch = false;

                if (File.Exists(serverFile))
                {
                    string clientContent = File.ReadAllText(clientFile);
                    string serverContent = File.ReadAllText(serverFile);
                    filesMatch = clientContent == serverContent;
                }

                if (filesMatch)
                {
                    consistentCount++;
                    Debug.Log($"[ExportTool]   ✅ 一致: Structures/{fileName}");
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    Debug.Log($"[ExportTool]   ❌ 不一致: Structures/{fileName}{(File.Exists(serverFile) ? "" : " (服务器端不存在)")}");
                    report += $"\n❌ 不一致: Structures/{fileName}";
                    if (!File.Exists(serverFile))
                    {
                        report += " (服务器端不存在)";
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] Structures目录不存在: {clientStructPath}");
        }

        string clientEventPath = Path.Combine(Application.dataPath, "Scripts", "BaseTool", "GameEventConstants.cs");
        string serverEventPath = Path.Combine(serverSharedPath, "Events", "GameEventConstants.cs");

        if (File.Exists(clientEventPath))
        {
            Debug.Log($"[ExportTool] 检查事件常量: {clientEventPath}");
            totalFiles++;
            if (File.Exists(serverEventPath))
            {
                string clientContent = File.ReadAllText(clientEventPath);
                string serverContent = File.ReadAllText(serverEventPath);
                if (clientContent == serverContent)
                {
                    consistentCount++;
                    Debug.Log($"[ExportTool]   ✅ 一致: Events/GameEventConstants.cs");
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    Debug.Log($"[ExportTool]   ❌ 不一致: Events/GameEventConstants.cs");
                    report += "\n❌ 不一致: Events/GameEventConstants.cs";
                }
            }
            else
            {
                inconsistentCount++;
                isConsistent = false;
                Debug.Log($"[ExportTool]   ❌ 不一致: Events/GameEventConstants.cs (服务器端不存在)");
                report += "\n❌ 不一致: Events/GameEventConstants.cs (服务器端不存在)";
            }
        }
        else
        {
            Debug.LogWarning($"[ExportTool] 事件常量文件不存在: {clientEventPath}");
        }

        string summary = $"\n📊 数据一致性验证报告:\n";
        summary += $"总文件数: {totalFiles}\n";
        summary += $"✅ 一致: {consistentCount}\n";
        summary += $"❌ 不一致: {inconsistentCount}\n";

        if (isConsistent)
        {
            summary += "\n🎉 所有数据一致！";
        }
        else
        {
            summary += "\n⚠️ 发现不一致的文件，建议运行一键导出工具同步数据。";
        }

        Debug.Log($"[ExportTool] ========== 验证统计 ==========");
        Debug.Log($"[ExportTool] 总文件数: {totalFiles}");
        Debug.Log($"[ExportTool] ✅ 一致: {consistentCount}");
        Debug.Log($"[ExportTool] ❌ 不一致: {inconsistentCount}");
        Debug.Log($"[ExportTool] =============================================");

        report = summary + report;
        return isConsistent;
    }

    // ==================== 内部类 ====================

    private class ExportFileInfo
    {
        public string sourcePath;
        public string fileName;
        public string destinationPath;
        public string relativePath;
        public string fileType;
        public Color color;
    }

    private enum FileStatus
    {
        Redundant,  // 已有文件（文件名相同）
        Other       // 其他文件（文件名不同）
    }

    private class FileScanResult
    {
        public string fileName;
        public string relativePath;
        public string fullPath;
        public FileStatus status;
    }
}
#endif
