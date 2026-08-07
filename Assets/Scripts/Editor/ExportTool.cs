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

    private ExportMode currentMode = ExportMode.Client;
    private string exportPath = "";
    private string exportToPath = "";
    private Vector2 scrollPosition;
    private List<ExportFileInfo> exportFiles = new List<ExportFileInfo>();
    private List<ExportFileInfo> exportToFiles = new List<ExportFileInfo>();
    private bool showFileList = false;
    private bool showExportToFileList = false;

    private const string PREFS_KEY_EXPORT_PATH = "ExportTool_LastExportPath";
    private const string PREFS_KEY_EXPORT_TO_PATH = "ExportTool_LastExportToPath";
    private const string PREFS_KEY_EXPORT_MODE = "ExportTool_LastExportMode";

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
                    destinationPath = Path.Combine(serverSharedPath, "Data", relativePath),
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
                exportFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    destinationPath = Path.Combine(serverSharedPath, "Structures", Path.GetFileName(file)),
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
                exportFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    destinationPath = Path.Combine(serverSharedPath, "SharedModels", Path.GetFileName(file)),
                    fileType = "共享模型",
                    color = new Color(1f, 0.6f, 0.2f)
                });
            }
        }

        // 4. 获取游戏事件常量文件（导出到服务器Shared/Events）
        string gameEventConstantsPath = Path.Combine(Application.dataPath, "Scripts", "BaseTool", "GameEventConstants.cs");
        if (File.Exists(gameEventConstantsPath))
        {
            exportFiles.Add(new ExportFileInfo
            {
                sourcePath = gameEventConstantsPath,
                destinationPath = Path.Combine(serverSharedPath, "Events", "GameEventConstants.cs"),
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

        // ✅ 只获取 Resources 目录下的 JSON 文件
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
                    destinationPath = relativePath,
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

        // 自定义中文显示
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

        // ==================== 数据一致性验证按钮（通用） ====================
        GUILayout.Space(20);
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

        // 目标路径
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

        // 显示统计信息
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

        // 导出路径
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
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // 显示导出文件统计
        EditorGUILayout.LabelField($"📋 待导出文件 ({exportFiles.Count} 个)", EditorStyles.boldLabel);

        // 检索文件夹路径显示
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
            // 按目录分组显示
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

        // 刷新文件列表
        RefreshExportToFileList();

        if (exportToFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "当前项目 Resources 目录中没有找到JSON文件！", "确定");
            return;
        }

        // 确认导出
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
                    // 构建目标路径（保持相同的相对路径）
                    string targetFilePath = Path.Combine(exportToPath, fileInfo.destinationPath);
                    string targetDir = Path.GetDirectoryName(targetFilePath);

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    // 检查目标文件是否已存在且内容相同
                    if (File.Exists(targetFilePath))
                    {
                        string sourceContent = File.ReadAllText(fileInfo.sourcePath);
                        string destContent = File.ReadAllText(targetFilePath);

                        if (sourceContent == destContent)
                        {
                            skipCount++;
                            continue;
                        }

                        // 备份旧文件
                        string backupPath = targetFilePath + ".backup";
                        if (!File.Exists(backupPath))
                        {
                            File.Copy(targetFilePath, backupPath);
                        }
                    }

                    // 复制文件
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

        // ✅ 只检查 Resources 目录下的 JSON 数据
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

    private class ExportFileInfo
    {
        public string sourcePath;
        public string destinationPath;
        public string fileType;
        public Color color;
    }
}
#endif
