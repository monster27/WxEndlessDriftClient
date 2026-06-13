// ==================== ExportTool.cs ====================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class ExportTool : EditorWindow
{
    private string exportPath = "";
    private Vector2 scrollPosition;
    private List<ExportFileInfo> exportFiles = new List<ExportFileInfo>();
    private bool showFileList = false;

    private const string PREFS_KEY_EXPORT_PATH = "ExportTool_LastExportPath";

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
            RefreshExportFileList();
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
        exportPath = EditorPrefs.GetString(PREFS_KEY_EXPORT_PATH, "");
    }

    private void SaveLastExportPath()
    {
        EditorPrefs.SetString(PREFS_KEY_EXPORT_PATH, exportPath);
    }

    private void RefreshExportFileList()
    {
        exportFiles.Clear();

        // 获取服务器Shared目录路径
        string serverSharedPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "..", "WxEndlessDriftServer", "Shared");

        // 1. 获取客户端JSON数据（导出到服务器Shared/Data）
        List<string> jsonPaths = new List<string>
        {
            Path.Combine(Application.dataPath, "Resources", "JsonData"),
            Path.Combine(Application.dataPath, "Resources", "Json"),
            Path.Combine(Application.dataPath, "Resources"),
            Path.Combine(Application.dataPath, "Plugins", "JsonData"),
            Path.Combine(Application.dataPath, "Json")
        };

        foreach (string jsonSourcePath in jsonPaths)
        {
            if (Directory.Exists(jsonSourcePath))
            {
                foreach (string file in Directory.GetFiles(jsonSourcePath, "*.json", SearchOption.AllDirectories))
                {
                    if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                        continue;

                    string relativePath = file.Replace(jsonSourcePath, "").TrimStart('/', '\\');
                    exportFiles.Add(new ExportFileInfo
                    {
                        sourcePath = file,
                        destinationPath = Path.Combine(serverSharedPath, "Data", relativePath),
                        fileType = "JSON数据",
                        color = new Color(0.2f, 0.6f, 1f) // 蓝色
                    });
                }
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
                    color = new Color(0.2f, 0.8f, 0.2f) // 绿色
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
                    color = new Color(1f, 0.6f, 0.2f) // 橙色
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
                color = new Color(1f, 0.4f, 0.7f) // 粉色
            });
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Space(10);

        // 导出路径输入框
        EditorGUILayout.LabelField("📁 导出地址", EditorStyles.boldLabel);
        exportPath = EditorGUILayout.TextField("", exportPath);

        // 浏览按钮
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("浏览", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("选择导出目录", exportPath, "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                exportPath = selectedPath;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(20);

        // 导出内容预览
        EditorGUILayout.LabelField($"📋 待导出文件 ({exportFiles.Count} 个)", EditorStyles.boldLabel);

        // 检索文件夹路径显示
        GUILayout.BeginVertical("Box");
        EditorGUILayout.LabelField("🔍 检索文件夹路径", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"• JSON数据: Assets/Resources/JsonData", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"• 数据结构: Assets/Plugins/Json", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"• 共享模型: Assets/Plugins/SharedModels", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"• 事件常量: Assets/Scripts/BaseTool", EditorStyles.miniLabel);
        GUILayout.EndVertical();

        showFileList = EditorGUILayout.Foldout(showFileList, $"展开查看所有文件", EditorStyles.foldoutHeader);
        if (showFileList)
        {
            GUILayout.BeginVertical("Box");

            if (exportFiles.Count == 0)
            {
                GUILayout.Label("  暂无可导出的文件", EditorStyles.miniLabel);
            }
            else
            {
                // 按类型分组显示
                var groupedFiles = exportFiles.GroupBy(f => f.fileType);

                foreach (var group in groupedFiles)
                {
                    GUILayout.Space(5);

                    // 类型标题
                    var firstFile = group.First();
                    GUI.color = firstFile.color;
                    EditorGUILayout.LabelField($"━━━━ {group.Key} ({group.Count()} 个) ━━━━", EditorStyles.boldLabel);
                    GUI.color = Color.white;

                    GUILayout.Space(5);

                    foreach (var fileInfo in group)
                    {
                        GUILayout.BeginHorizontal();

                        // 颜色标记
                        GUILayout.Label("●", GUILayout.Width(20));
                        GUI.color = fileInfo.color;

                        // 只显示Unity路径
                        string unityPath = fileInfo.sourcePath.Replace(Application.dataPath.Replace("/Assets", ""), "");
                        GUILayout.Label(unityPath, EditorStyles.miniLabel);

                        GUI.color = Color.white;

                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndVertical();
        }

        GUILayout.Space(20);

        // 统计信息
        if (exportFiles.Count > 0)
        {
            GUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("📊 统计信息", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("总文件数", GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{exportFiles.Count} 个文件");
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        GUILayout.Space(20);

        // 数据一致性验证按钮
        GUI.backgroundColor = new Color(0.6f, 0.8f, 1f); // 浅蓝色
        if (GUILayout.Button("🔍 验证数据一致性", GUILayout.Height(30)))
        {
            ValidateData();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);

        // 导出按钮
        GUI.backgroundColor = string.IsNullOrEmpty(exportPath) ? Color.gray : Color.green;
        GUI.enabled = !string.IsNullOrEmpty(exportPath);
        if (GUILayout.Button("🔄 一键导出", GUILayout.Height(40)))
        {
            ExportAll();
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        // 路径为空时的提示
        if (string.IsNullOrEmpty(exportPath))
        {
            EditorGUILayout.HelpBox("⚠️ 请填写导出地址！", MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
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
            if (syncNow && !string.IsNullOrEmpty(exportPath))
            {
                ExportAll();
            }
            else if (syncNow && string.IsNullOrEmpty(exportPath))
            {
                EditorUtility.DisplayDialog("提示", "请先设置导出地址！", "确定");
            }
        }
    }

    private void ExportAll()
    {
        if (string.IsNullOrEmpty(exportPath))
        {
            EditorUtility.DisplayDialog("提示", "请填写导出地址！", "确定");
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

                        // 检查文件是否存在且内容不同
                        if (File.Exists(destFullPath))
                        {
                            string sourceContent = File.ReadAllText(fileInfo.sourcePath);
                            string destContent = File.ReadAllText(destFullPath);
                            
                            if (sourceContent == destContent)
                            {
                                // 内容相同，跳过
                                Debug.Log($"跳过（内容相同）: {fileInfo.destinationPath}");
                                continue;
                            }
                            else
                            {
                                // 内容不同，先备份旧文件
                                string backupPath = destFullPath + ".backup";
                                if (!File.Exists(backupPath))
                                {
                                    File.Copy(destFullPath, backupPath);
                                }
                                Debug.Log($"备份旧文件: {backupPath}");
                            }
                        }

                        // 执行复制（覆盖）
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

    /// <summary>
    /// 静态导出方法，可通过代码调用
    /// </summary>
    /// <param name="path">导出路径，为空则提示需要填写地址</param>
    public static void Export(string path = "")
    {
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("提示", "请填写导出地址！", "确定");
            return;
        }

        if (!Directory.Exists(path))
        {
            bool createDir = EditorUtility.DisplayDialog("路径不存在", $"目录不存在，是否创建？\n{path}", "创建", "取消");
            if (createDir)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                return;
            }
        }

        int exportedCount = 0;

        exportedCount += ExportStructFiles(path);
        exportedCount += ExportSharedModels(path);
        exportedCount += ExportSharedStructures(path);
        exportedCount += ExportSharedData(path);
        exportedCount += ExportGameEventConstants(path);

        EditorPrefs.SetString(PREFS_KEY_EXPORT_PATH, path);

        EditorUtility.DisplayDialog("导出完成", $"成功导出 {exportedCount} 个文件！\n\n导出目录:\n{path}", "确定");
    }

    private static int ExportJsonFiles(string basePath)
    {
        int count = 0;
        List<string> jsonPaths = new List<string>
        {
            Path.Combine(Application.dataPath, "Resources", "JsonData"),
            Path.Combine(Application.dataPath, "Resources", "Json"),
            Path.Combine(Application.dataPath, "Resources"),
            Path.Combine(Application.dataPath, "Plugins", "JsonData"),
            Path.Combine(Application.dataPath, "Json")
        };

        foreach (string jsonSourcePath in jsonPaths)
        {
            if (Directory.Exists(jsonSourcePath))
            {
                string jsonDestPath = Path.Combine(basePath, "Data", "Json");
                Directory.CreateDirectory(jsonDestPath);

                foreach (string jsonFile in Directory.GetFiles(jsonSourcePath, "*.json", SearchOption.AllDirectories))
                {
                    // 排除 ProjectSettings 和 Packages 目录
                    if (jsonFile.Contains("ProjectSettings") || jsonFile.Contains("Packages"))
                        continue;

                    string relativePath = jsonFile.Replace(jsonSourcePath, "").TrimStart('/', '\\');
                    string fileName = Path.GetFileName(jsonFile);
                    string destFile = Path.Combine(jsonDestPath, relativePath);
                    
                    string destDir = Path.GetDirectoryName(destFile);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    
                    File.Copy(jsonFile, destFile, true);
                    count++;
                    Debug.Log($"导出JSON: {destFile}");
                }
            }
        }

        return count;
    }

    private static int ExportStructFiles(string basePath)
    {
        int count = 0;
        string structSourcePath = Path.Combine(Application.dataPath, "Plugins", "Json");

        if (Directory.Exists(structSourcePath))
        {
            string structDestPath = Path.Combine(basePath, "Structures");
            Directory.CreateDirectory(structDestPath);

            foreach (string csFile in Directory.GetFiles(structSourcePath, "*.cs"))
            {
                string fileName = Path.GetFileName(csFile);
                string destFile = Path.Combine(structDestPath, fileName);
                File.Copy(csFile, destFile, true);
                count++;
                Debug.Log($"导出结构文件: {fileName}");
            }
        }

        return count;
    }

    private static int ExportSharedModels(string basePath)
    {
        int count = 0;
        // 从客户端读取SharedModels（保持与RefreshExportFileList一致）
        string clientSharedModelsPath = Path.Combine(Application.dataPath, "Plugins", "SharedModels");

        if (Directory.Exists(clientSharedModelsPath))
        {
            string modelsDestPath = Path.Combine(basePath, "Shared", "SharedModels");
            Directory.CreateDirectory(modelsDestPath);

            foreach (string csFile in Directory.GetFiles(clientSharedModelsPath, "*.cs"))
            {
                string fileName = Path.GetFileName(csFile);
                string destFile = Path.Combine(modelsDestPath, fileName);
                File.Copy(csFile, destFile, true);
                count++;
                Debug.Log($"导出共享模型: {fileName}");
            }
        }

        return count;
    }

    private static int ExportSharedStructures(string basePath)
    {
        int count = 0;
        // 从客户端读取数据结构（保持与RefreshExportFileList一致）
        string clientStructuresPath = Path.Combine(Application.dataPath, "Plugins", "Json");

        if (Directory.Exists(clientStructuresPath))
        {
            string structuresDestPath = Path.Combine(basePath, "Shared", "Structures");
            Directory.CreateDirectory(structuresDestPath);

            foreach (string csFile in Directory.GetFiles(clientStructuresPath, "*.cs"))
            {
                string fileName = Path.GetFileName(csFile);
                string destFile = Path.Combine(structuresDestPath, fileName);
                File.Copy(csFile, destFile, true);
                count++;
                Debug.Log($"导出Shared结构: {fileName}");
            }
        }

        return count;
    }

    private static int ExportSharedData(string basePath)
    {
        int count = 0;
        // 从客户端读取JSON数据（保持与RefreshExportFileList一致）
        List<string> jsonPaths = new List<string>
        {
            Path.Combine(Application.dataPath, "Resources", "JsonData"),
            Path.Combine(Application.dataPath, "Resources", "Json"),
            Path.Combine(Application.dataPath, "Resources"),
            Path.Combine(Application.dataPath, "Plugins", "JsonData"),
            Path.Combine(Application.dataPath, "Json")
        };

        foreach (string jsonSourcePath in jsonPaths)
        {
            if (Directory.Exists(jsonSourcePath))
            {
                string dataDestPath = Path.Combine(basePath, "Shared", "Data");
                Directory.CreateDirectory(dataDestPath);

                foreach (string jsonFile in Directory.GetFiles(jsonSourcePath, "*.json", SearchOption.AllDirectories))
                {
                    // 排除 ProjectSettings 和 Packages 目录
                    if (jsonFile.Contains("ProjectSettings") || jsonFile.Contains("Packages"))
                        continue;

                    string relativePath = jsonFile.Replace(jsonSourcePath, "").TrimStart('/', '\\');
                    string destFile = Path.Combine(dataDestPath, relativePath);
                    string destDir = Path.GetDirectoryName(destFile);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    File.Copy(jsonFile, destFile, true);
                    count++;
                    Debug.Log($"导出Shared数据: {relativePath}");
                }
            }
        }

        return count;
    }

    /// <summary>
    /// 验证客户端与服务器数据一致性
    /// </summary>
    public static bool ValidateDataConsistency(out string report)
    {
        report = "";
        bool isConsistent = true;
        int totalFiles = 0;
        int consistentCount = 0;
        int inconsistentCount = 0;

        string serverSharedPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "..", "WxEndlessDriftServer", "Shared");

        // 1. 验证JSON数据一致性
        List<string> jsonPaths = new List<string>
        {
            Path.Combine(Application.dataPath, "Resources", "JsonData"),
            Path.Combine(Application.dataPath, "Resources", "Json"),
            Path.Combine(Application.dataPath, "Resources"),
            Path.Combine(Application.dataPath, "Plugins", "JsonData"),
            Path.Combine(Application.dataPath, "Json")
        };

        foreach (string jsonSourcePath in jsonPaths)
        {
            if (Directory.Exists(jsonSourcePath))
            {
                foreach (string clientFile in Directory.GetFiles(jsonSourcePath, "*.json", SearchOption.AllDirectories))
                {
                    if (clientFile.Contains("ProjectSettings") || clientFile.Contains("Packages"))
                        continue;

                    string relativePath = clientFile.Replace(jsonSourcePath, "").TrimStart('/', '\\');
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
        }

        // 2. 验证SharedModels一致性
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

        // 3. 验证数据结构一致性
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

        // 4. 验证事件常量一致性
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

        // 生成报告
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

    private static int ExportGameEventConstants(string basePath)
    {
        int count = 0;
        string gameEventConstantsPath = Path.Combine(Application.dataPath, "Scripts", "BaseTool", "GameEventConstants.cs");

        if (File.Exists(gameEventConstantsPath))
        {
            string eventsDestPath = Path.Combine(basePath, "Events");
            Directory.CreateDirectory(eventsDestPath);

            string fileName = Path.GetFileName(gameEventConstantsPath);
            string destFile = Path.Combine(eventsDestPath, fileName);
            File.Copy(gameEventConstantsPath, destFile, true);
            count++;
            Debug.Log($"导出事件常量: {fileName}");
        }

        return count;
    }

    private static void ExportSharedJsonFiles(string sourcePath, string relativePath, List<ExportFileInfo> exportFiles)
    {
        foreach (string file in Directory.GetFiles(sourcePath, "*.json", SearchOption.AllDirectories))
        {
            string fileRelativePath = file.Replace(sourcePath, "").TrimStart('/', '\\');
            exportFiles.Add(new ExportFileInfo
            {
                sourcePath = file,
                destinationPath = Path.Combine("Shared", "Data", fileRelativePath),
                fileType = "Shared数据",
                color = new Color(0.4f, 0.6f, 1f) // 浅蓝色
            });
        }
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
