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
        Server
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
        // 默认值为 0 (ListOnly)，因为枚举中 ListOnly 是第一个
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

        // 1. 获取客户端Resources目录下的JSON数据
        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
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
            }
        }

        // 2. 获取客户端数据结构（导出到服务器Shared/Structures）
        string structSourcePath = Path.Combine(Application.dataPath, "Plugins", "Json");
        if (Directory.Exists(structSourcePath))
        {
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
            }
        }

        // 3. 获取客户端SharedModels（导出到服务器Shared/SharedModels）
        string clientSharedModelsPath = Path.Combine(Application.dataPath, "Plugins", "SharedModels");
        if (Directory.Exists(clientSharedModelsPath))
        {
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
            }
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
        }
    }

    /// <summary>
    /// 刷新导出到另一个目录的文件列表（客户端模式）
    /// </summary>
    private void RefreshExportToFileList()
    {
        exportToFiles.Clear();

        if (string.IsNullOrEmpty(exportToPath))
            return;

        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
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
            }
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Space(10);

        // ==================== 模式选择下拉框（中文） ====================
        EditorGUILayout.LabelField("🔧 导出模式", EditorStyles.boldLabel);
        GUILayout.Space(5);

        string[] modeNames = { "客户端导出", "服务器导出" };
        int selectedIndex = (int)currentMode;
        int newIndex = EditorGUILayout.Popup("选择模式", selectedIndex, modeNames);

        if (newIndex != selectedIndex)
        {
            currentMode = (ExportMode)newIndex;
            SaveLastExportMode();
            if (currentMode == ExportMode.Client)
            {
                RefreshExportToFileList();
            }
            else
            {
                RefreshExportFileList();
            }
            // 清空扫描结果
            scanResults.Clear();
            hasScanned = false;
        }

        GUILayout.Space(10);

        // ==================== 根据模式显示不同内容 ====================
        if (currentMode == ExportMode.Client)
        {
            DrawClientMode();
        }
        else
        {
            DrawServerMode();
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

        // 清除模式选择
        string[] cleanModeNames = { "仅列出文件", "删除已有文件" };
        int cleanSelected = (int)cleanMode;
        int cleanNew = EditorGUILayout.Popup("清除模式", cleanSelected, cleanModeNames);
        cleanMode = (CleanMode)cleanNew;

        GUILayout.Space(5);

        string basePath = currentMode == ExportMode.Client ? exportToPath : exportPath;

        // 扫描按钮
        GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
        if (GUILayout.Button("🔍 扫描目标目录", GUILayout.Height(30)))
        {
            ScanTargetDirectory(basePath);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(5);

        // 显示扫描结果
        if (hasScanned)
        {
            showScanResults = EditorGUILayout.Foldout(showScanResults, $"📋 扫描结果 ({scanResults.Count} 个文件)", EditorStyles.foldoutHeader);
            if (showScanResults)
            {
                DrawScanResults();
            }

            GUILayout.Space(5);

            // 统计
            int redundantCount = scanResults.Count(r => r.status == FileStatus.Redundant);
            int otherCount = scanResults.Count(r => r.status == FileStatus.Other);

            EditorGUILayout.LabelField($"📊 已有文件: {redundantCount} 个  其他文件: {otherCount} 个", EditorStyles.miniLabel);

            GUILayout.Space(5);

            // 执行清除按钮
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
                // 仅列出模式 - 打印所有信息
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

    /// <summary>
    /// 绘制扫描结果
    /// </summary>
    private void DrawScanResults()
    {
        GUILayout.BeginVertical("Box");

        if (scanResults.Count == 0)
        {
            GUILayout.Label("  没有找到文件", EditorStyles.miniLabel);
        }
        else
        {
            // 按状态分组显示
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
                    string displayPath = result.relativePath.Replace(Path.GetFileName(result.relativePath), "");

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

        try
        {
            // 获取源文件名称列表（用于对比）
            HashSet<string> sourceFileNames = new HashSet<string>();

            if (currentMode == ExportMode.Client)
            {
                // 客户端模式：源是 Resources 目录
                string resourcesPath = Path.Combine(Application.dataPath, "Resources");
                if (Directory.Exists(resourcesPath))
                {
                    foreach (string file in Directory.GetFiles(resourcesPath, "*", SearchOption.AllDirectories))
                    {
                        if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                            continue;
                        string fileName = Path.GetFileName(file);
                        sourceFileNames.Add(fileName);
                    }
                }
                // 添加导出列表中的文件名
                foreach (var fileInfo in exportToFiles)
                {
                    sourceFileNames.Add(fileInfo.fileName);
                }
            }
            else
            {
                // 服务器模式：源是导出列表
                foreach (var fileInfo in exportFiles)
                {
                    sourceFileNames.Add(fileInfo.fileName);
                }
            }

            // 获取目标目录所有文件
            string[] targetExtensions = { "*.json", "*.cs" };
            List<string> targetFiles = new List<string>();

            foreach (string ext in targetExtensions)
            {
                foreach (string file in Directory.GetFiles(basePath, ext, SearchOption.AllDirectories))
                {
                    if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                        continue;

                    string relativePath = file.Replace(basePath, "").TrimStart('/', '\\');

                    // 只检查导出相关的目录
                    bool isExportDir = false;
                    string[] exportDirs = { "Data", "Structures", "SharedModels", "Events" };
                    foreach (string dir in exportDirs)
                    {
                        if (relativePath.StartsWith(dir))
                        {
                            isExportDir = true;
                            break;
                        }
                    }

                    if (isExportDir)
                    {
                        targetFiles.Add(relativePath);
                    }
                }
            }

            // 对比文件名
            foreach (string targetFile in targetFiles)
            {
                string fileName = Path.GetFileName(targetFile);

                if (sourceFileNames.Contains(fileName))
                {
                    // 文件名相同 -> 已有文件
                    scanResults.Add(new FileScanResult
                    {
                        fileName = fileName,
                        relativePath = targetFile,
                        status = FileStatus.Redundant,
                        fullPath = Path.Combine(basePath, targetFile)
                    });
                }
                else
                {
                    // 文件名不同 -> 其他文件
                    scanResults.Add(new FileScanResult
                    {
                        fileName = fileName,
                        relativePath = targetFile,
                        status = FileStatus.Other,
                        fullPath = Path.Combine(basePath, targetFile)
                    });
                }
            }

            // 按状态排序：先显示已有文件
            scanResults = scanResults.OrderByDescending(r => r.status).ToList();

            hasScanned = true;
            int redundantCount = scanResults.Count(r => r.status == FileStatus.Redundant);
            int otherCount = scanResults.Count(r => r.status == FileStatus.Other);

            Debug.Log($"🔍 扫描完成！找到 {redundantCount} 个已有文件，{otherCount} 个其他文件");
            EditorUtility.DisplayDialog("扫描完成", $"扫描完成！\n\n🔄 已有文件: {redundantCount} 个\n📄 其他文件: {otherCount} 个", "确定");
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("扫描失败", $"扫描过程中发生错误：\n{ex.Message}", "确定");
            Debug.LogError($"扫描文件错误: {ex}");
        }
    }

    /// <summary>
    /// 删除已有文件（只删除文件名相同的）
    /// </summary>
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

        try
        {
            foreach (var result in redundantFiles)
            {
                string fullPath = result.fullPath;
                if (File.Exists(fullPath))
                {
                    try
                    {
                        // 删除备份文件
                        string backupPath = fullPath + ".backup";
                        if (File.Exists(backupPath))
                        {
                            File.Delete(backupPath);
                        }

                        File.Delete(fullPath);
                        deletedCount++;
                        deletedList.Add(result.fileName);
                        Debug.Log($"🗑️ 已删除已有文件: {result.relativePath}");
                    }
                    catch (System.Exception ex)
                    {
                        failCount++;
                        Debug.LogError($"删除失败 {result.relativePath}: {ex.Message}");
                    }
                }
            }

            // 删除空目录
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

            EditorUtility.DisplayDialog("清除完成", resultMsg, "确定");
            Debug.Log($"清除已有完成：删除 {deletedCount} 个文件，{deletedDirs} 个空目录");

            // 重新扫描
            ScanTargetDirectory(basePath);
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("清除失败", $"清除过程中发生错误：\n{ex.Message}", "确定");
            Debug.LogError($"清除已有文件错误: {ex}");
        }
    }

    /// <summary>
    /// 删除空的导出目录
    /// </summary>
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
                            Debug.Log($"🗑️ 已删除空导出目录: {dir}");
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"删除目录失败 {dir}: {ex.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"删除空目录时发生错误: {ex.Message}");
        }

        return deletedCount;
    }

    /// <summary>
    /// 打印完整报告到控制台
    /// </summary>
    private void PrintFullReport(string basePath)
    {
        if (scanResults.Count == 0)
        {
            Debug.Log("📋 没有文件");
            return;
        }

        var redundantFiles = scanResults.Where(r => r.status == FileStatus.Redundant).ToList();
        var otherFiles = scanResults.Where(r => r.status == FileStatus.Other).ToList();

        Debug.Log($"========== 📊 完整扫描报告 ==========");
        Debug.Log($"📁 目标目录: {basePath}");
        Debug.Log($"📅 扫描时间: {System.DateTime.Now}");
        Debug.Log("");
        Debug.Log($"🔄 已有文件 ({redundantFiles.Count} 个) - 文件名与源目录相同，建议删除");
        Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        foreach (var file in redundantFiles)
        {
            Debug.Log($"   🗑️ {file.relativePath}");
        }

        if (otherFiles.Count > 0)
        {
            Debug.Log("");
            Debug.Log($"📄 其他文件 ({otherFiles.Count} 个) - 文件名不在源目录中，可能是手动创建");
            Debug.Log("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            foreach (var file in otherFiles)
            {
                Debug.Log($"   📄 {file.relativePath}");
            }
        }

        Debug.Log("");
        Debug.Log($"========== 📊 统计 ==========");
        Debug.Log($"总文件数: {scanResults.Count}");
        Debug.Log($"🔄 已有文件: {redundantFiles.Count}");
        Debug.Log($"📄 其他文件: {otherFiles.Count}");
        Debug.Log($"==============================");

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

    /// <summary>
    /// 绘制客户端模式界面
    /// </summary>
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

    /// <summary>
    /// 绘制服务器模式界面
    /// </summary>
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

    /// <summary>
    /// 导出JSON到另一个项目（客户端模式）
    /// </summary>
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
                            continue;
                        }

                        string backupPath = targetFilePath + ".backup";
                        if (!File.Exists(backupPath))
                        {
                            File.Copy(targetFilePath, backupPath);
                        }
                    }

                    File.Copy(fileInfo.sourcePath, targetFilePath, true);
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    failCount++;
                    Debug.LogError($"导出失败 {fileInfo.destinationPath}: {ex.Message}");
                }
            }

            SaveLastExportToPath();

            string resultMsg = $"导出完成！\n\n" +
                              $"✅ 成功: {successCount} 个文件\n" +
                              $"⏭️ 跳过（内容相同）: {skipCount} 个文件\n" +
                              $"❌ 失败: {failCount} 个文件\n\n" +
                              $"目标目录:\n{exportToPath}";

            EditorUtility.DisplayDialog("导出完成", resultMsg, "确定");
            Debug.Log($"导出JSON完成: 成功{successCount}, 跳过{skipCount}, 失败{failCount}");

            // 清除扫描缓存
            scanResults.Clear();
            hasScanned = false;
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("导出失败", $"导出过程中发生错误:\n{ex.Message}", "确定");
            Debug.LogError($"导出JSON错误: {ex}");
        }
    }

    /// <summary>
    /// 导出到服务器（服务器模式）
    /// </summary>
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
                            }
                            Debug.Log($"备份旧文件: {backupPath}");
                        }
                    }

                    File.Copy(fileInfo.sourcePath, destFullPath, true);
                    successCount++;
                    Debug.Log($"导出成功: {fileInfo.destinationPath}");
                }
                catch (System.Exception e)
                {
                    failCount++;
                    Debug.LogError($"导出失败 {fileInfo.sourcePath}: {e.Message}");
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

            EditorUtility.DisplayDialog("导出完成", message, "确定");

            // 清除扫描缓存
            scanResults.Clear();
            hasScanned = false;
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("导出失败", $"导出过程中发生错误:\n{e.Message}", "确定");
            Debug.LogError($"导出错误: {e}");
        }
    }

    private void ValidateData()
    {
        string report;
        bool isConsistent = ValidateDataConsistency(out report);

        Debug.Log(report);

        if (isConsistent)
        {
            EditorUtility.DisplayDialog("数据一致性验证", report, "确定");
        }
        else
        {
            bool syncNow = EditorUtility.DisplayDialog("数据一致性验证", report + "\n\n是否立即同步数据？", "同步", "取消");
            if (syncNow)
            {
                if (currentMode == ExportMode.Client && !string.IsNullOrEmpty(exportToPath))
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

        string resourcesPath = Path.Combine(Application.dataPath, "Resources");
        if (Directory.Exists(resourcesPath))
        {
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
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    report += $"\n❌ 不一致: {relativePath}";
                    if (!File.Exists(serverFile))
                    {
                        report += " (服务器端不存在)";
                    }
                }
            }
        }

        string clientSharedModelsPath = Path.Combine(Application.dataPath, "Plugins", "SharedModels");
        string serverSharedModelsPath = Path.Combine(serverSharedPath, "SharedModels");

        if (Directory.Exists(clientSharedModelsPath))
        {
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
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    report += $"\n❌ 不一致: SharedModels/{fileName}";
                    if (!File.Exists(serverFile))
                    {
                        report += " (服务器端不存在)";
                    }
                }
            }
        }

        string clientStructPath = Path.Combine(Application.dataPath, "Plugins", "Json");
        string serverStructPath = Path.Combine(serverSharedPath, "Structures");

        if (Directory.Exists(clientStructPath))
        {
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
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    report += $"\n❌ 不一致: Structures/{fileName}";
                    if (!File.Exists(serverFile))
                    {
                        report += " (服务器端不存在)";
                    }
                }
            }
        }

        string clientEventPath = Path.Combine(Application.dataPath, "Scripts", "BaseTool", "GameEventConstants.cs");
        string serverEventPath = Path.Combine(serverSharedPath, "Events", "GameEventConstants.cs");

        if (File.Exists(clientEventPath))
        {
            totalFiles++;
            if (File.Exists(serverEventPath))
            {
                string clientContent = File.ReadAllText(clientEventPath);
                string serverContent = File.ReadAllText(serverEventPath);
                if (clientContent == serverContent)
                {
                    consistentCount++;
                }
                else
                {
                    inconsistentCount++;
                    isConsistent = false;
                    report += "\n❌ 不一致: Events/GameEventConstants.cs";
                }
            }
            else
            {
                inconsistentCount++;
                isConsistent = false;
                report += "\n❌ 不一致: Events/GameEventConstants.cs (服务器端不存在)";
            }
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
