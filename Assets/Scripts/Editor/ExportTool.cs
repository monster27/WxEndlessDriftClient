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

        // 获取JSON文件列表（检查多个可能的路径）
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
                    // 排除 ProjectSettings 和 Packages 目录
                    if (file.Contains("ProjectSettings") || file.Contains("Packages"))
                        continue;

                    string relativePath = file.Replace(jsonSourcePath, "").TrimStart('/', '\\');
                    exportFiles.Add(new ExportFileInfo
                    {
                        sourcePath = file,
                        destinationPath = Path.Combine("Data", "Json", relativePath),
                        fileType = "JSON数据",
                        color = new Color(0.2f, 0.6f, 1f) // 蓝色
                    });
                }
            }
        }

        // 获取数据结构文件列表
        string structSourcePath = Path.Combine(Application.dataPath, "Plugins", "Json");
        if (Directory.Exists(structSourcePath))
        {
            foreach (string file in Directory.GetFiles(structSourcePath, "*.cs"))
            {
                exportFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    destinationPath = Path.Combine("Structures", Path.GetFileName(file)),
                    fileType = "数据结构",
                    color = new Color(0.2f, 0.8f, 0.2f) // 绿色
                });
            }
        }

        // 获取服务器模型文件列表
        string serverModelsPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "..", "WxEndlessDriftServer", "Models");
        if (Directory.Exists(serverModelsPath))
        {
            foreach (string file in Directory.GetFiles(serverModelsPath, "*.cs"))
            {
                exportFiles.Add(new ExportFileInfo
                {
                    sourcePath = file,
                    destinationPath = Path.Combine("ServerModels", Path.GetFileName(file)),
                    fileType = "服务器模型",
                    color = new Color(1f, 0.6f, 0.2f) // 橙色
                });
            }
        }

        // 获取游戏事件常量文件（服务器和客户端交互数据）
        string gameEventConstantsPath = Path.Combine(Application.dataPath, "Scripts", "BaseTool", "GameEventConstants.cs");
        if (File.Exists(gameEventConstantsPath))
        {
            exportFiles.Add(new ExportFileInfo
            {
                sourcePath = gameEventConstantsPath,
                destinationPath = Path.Combine("Events", "GameEventConstants.cs"),
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

                        // 文件名
                        GUILayout.Label(Path.GetFileName(fileInfo.sourcePath), GUILayout.Width(200));

                        GUI.color = Color.white;

                        // 箭头
                        GUILayout.Label("→", GUILayout.Width(20));

                        // 目标路径
                        GUILayout.Label(fileInfo.destinationPath, EditorStyles.miniLabel);

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

            var stats = exportFiles.GroupBy(f => f.fileType)
                .Select(g => new { Type = g.Key, Count = g.Count() });

            foreach (var stat in stats)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(stat.Type, GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{stat.Count} 个文件");
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        GUILayout.Space(20);

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
                    File.Copy(fileInfo.sourcePath, destFullPath, true);
                    successCount++;
                    Debug.Log($"✅ 导出成功: {fileInfo.destinationPath}");
                }
                catch (System.Exception e)
                {
                    failCount++;
                    Debug.LogError($"❌ 导出失败 {fileInfo.sourcePath}: {e.Message}");
                }
            }

            SaveLastExportPath();

            string message = $"🎉 导出完成！\n\n";
            message += $"✅ 成功: {successCount} 个文件\n";
            if (failCount > 0)
            {
                message += $"❌ 失败: {failCount} 个文件\n";
            }
            message += $"\n📁 导出目录:\n{exportPath}";

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

        exportedCount += ExportJsonFiles(path);
        exportedCount += ExportStructFiles(path);
        exportedCount += ExportServerModels(path);
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

    private static int ExportServerModels(string basePath)
    {
        int count = 0;
        string serverModelsPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), "..", "WxEndlessDriftServer", "Models");

        if (Directory.Exists(serverModelsPath))
        {
            string modelsDestPath = Path.Combine(basePath, "ServerModels");
            Directory.CreateDirectory(modelsDestPath);

            foreach (string csFile in Directory.GetFiles(serverModelsPath, "*.cs"))
            {
                string fileName = Path.GetFileName(csFile);
                string destFile = Path.Combine(modelsDestPath, fileName);
                File.Copy(csFile, destFile, true);
                count++;
                Debug.Log($"导出服务器模型: {fileName}");
            }
        }

        return count;
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

    private class ExportFileInfo
    {
        public string sourcePath;
        public string destinationPath;
        public string fileType;
        public Color color;
    }
}
#endif
