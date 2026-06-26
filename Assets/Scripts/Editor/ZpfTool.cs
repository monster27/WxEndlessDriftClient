#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;


public class ZpfTool : Editor
{
    // 服务器路径存储Key
    private const string SERVER_PATH_KEY = "ZpfTool_ServerPath";

    /// <summary>
    /// 切换物体显隐状态
    /// </summary>
    [MenuItem("Tools/Zpf/显隐 &1")]
    public static void SetObjActive()
    {
        GameObject[] selectObjs = Selection.gameObjects;
        int objCtn = selectObjs.Length;
        for (int i = 0; i < objCtn; i++)
        {
            bool isAcitve = selectObjs[i].activeSelf;
            selectObjs[i].SetActive(!isAcitve);
        }
    }


    /// <summary>
    /// 设置名称
    /// </summary>
    [MenuItem("Tools/Zpf/名称 &2")]
    public static void SetObjName()
    {
        GameObject[] selectObjs = Selection.gameObjects;

        int objCtn = selectObjs.Length;

        for (int i = 0; i < objCtn; i++)
        {
            selectObjs[i].name = selectObjs[i].name + "_" + i;
        }
    }

    /// <summary>
    ///
    /// </summary>
    [MenuItem("Tools/Zpf/排序 &3")]
    public static void SetObjWH()
    {
        GameObject[] selectObjs = Selection.gameObjects;
        int objCtn = selectObjs.Length;

        Vector3 firstPos = selectObjs[0].transform.position;
        for (int i = 0; i < objCtn; i++)
        {
            selectObjs[i].GetComponent<Transform>().position = new Vector3(firstPos.x + i, firstPos.y, firstPos.z);
        }
    }
    /// <summary>
    /// 设置比例宽高
    /// </summary>
    [MenuItem("Tools/Zpf/宽高 &4")]
    public static void SetObjWH2()
    {
        GameObject[] selectObjs = Selection.gameObjects;

        int objCtn = selectObjs.Length;

        float proportion = 1.5f;

        for (int i = 0; i < objCtn; i++)
        {
            float width = selectObjs[i].GetComponent<RectTransform>().sizeDelta.x;

            float height = selectObjs[i].GetComponent<RectTransform>().sizeDelta.y;

            width *= proportion;

            height *= proportion;

            selectObjs[i].GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
        }
    }

    /// <summary>
    /// 检索所有NetServerManager partial脚本并合并内容到粘贴板
    /// </summary>
    [MenuItem("Tools/获取合并客户端网络脚本")]
    public static void MergeNetServerManagerScripts()
    {
        // 查找所有NetServerManager相关的脚本文件
        string[] guids = AssetDatabase.FindAssets("NetServerManager t:Script");

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何NetServerManager脚本文件！", "确定");
            return;
        }

        Dictionary<string, string> scriptContents = new Dictionary<string, string>();
        StringBuilder mergedContent = new StringBuilder();
        int fileCount = 0;

        // 添加文件头信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// NetServerManager 合并脚本 - 共找到 {guids.Length} 个partial文件");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            // 只处理.cs文件
            if (!assetPath.EndsWith(".cs"))
                continue;

            string fileName = Path.GetFileName(assetPath);

            // 检查文件内容是否包含partial关键字（进一步过滤）
            string content = File.ReadAllText(assetPath, Encoding.UTF8);

            // 检查是否包含NetServerManager和partial
            if (content.Contains("partial") && content.Contains("NetServerManager"))
            {
                fileCount++;
                scriptContents[fileName] = content;

                // 添加文件分隔标记和内容
                mergedContent.AppendLine($"// ============================================");
                mergedContent.AppendLine($"// 文件: {fileName}");
                mergedContent.AppendLine($"// 路径: {assetPath}");
                mergedContent.AppendLine($"// ============================================");
                mergedContent.AppendLine(content);
                mergedContent.AppendLine();
                mergedContent.AppendLine();
            }
        }

        if (fileCount == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到包含partial关键字的NetServerManager脚本文件！", "确定");
            return;
        }

        // 添加文件统计信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 总计合并 {fileCount} 个partial文件");
        mergedContent.AppendLine("// ============================================");

        // 复制到粘贴板
        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        // 显示成功信息
        string message = $"成功合并 {fileCount} 个NetServerManager partial文件！\n\n";
        message += "文件列表：\n";
        foreach (string fileName in scriptContents.Keys)
        {
            message += $"  - {fileName}\n";
        }
        message += "\n内容已复制到粘贴板，可以直接粘贴使用。";

        EditorUtility.DisplayDialog("合并完成", message, "确定");

        // 输出到控制台
        Debug.Log($"已合并 {fileCount} 个NetServerManager partial文件，内容已复制到粘贴板。");
    }

    /// <summary>
    /// 获取服务器工程所有C#代码并合并到粘贴板
    /// </summary>
    [MenuItem("Tools/获取合并服务器代码")]
    public static void MergeServerCodes()
    {
        // 从EditorPrefs获取保存的路径
        string serverProjectPath = EditorPrefs.GetString(SERVER_PATH_KEY, "");

        // 检查路径是否有效
        bool pathValid = !string.IsNullOrEmpty(serverProjectPath) && Directory.Exists(serverProjectPath);

        // 如果路径无效或为空，弹窗让用户选择
        if (!pathValid)
        {
            bool chooseNewPath = EditorUtility.DisplayDialog(
                "选择服务器路径",
                "未找到有效的服务器代码路径！\n\n" +
                "请选择服务器工程根目录（包含 .csproj 或 .sln 文件的文件夹）。\n\n" +
                "示例: E:\\TuanjieProject\\WxEndlessDriftServer",
                "选择路径",
                "取消"
            );

            if (!chooseNewPath)
            {
                return;
            }

            // 打开文件夹选择对话框
            string selectedPath = EditorUtility.OpenFolderPanel(
                "选择服务器工程目录",
                serverProjectPath,
                ""
            );

            if (string.IsNullOrEmpty(selectedPath))
            {
                EditorUtility.DisplayDialog("提示", "未选择任何路径，操作已取消。", "确定");
                return;
            }

            // 保存路径
            serverProjectPath = selectedPath;
            EditorPrefs.SetString(SERVER_PATH_KEY, serverProjectPath);

            Debug.Log($"服务器路径已保存: {serverProjectPath}");
        }

        // 再次确认路径存在
        if (!Directory.Exists(serverProjectPath))
        {
            EditorUtility.DisplayDialog("错误", $"路径不存在！\n{serverProjectPath}\n\n请重新选择。", "确定");

            // 清除无效路径
            EditorPrefs.DeleteKey(SERVER_PATH_KEY);

            // 递归调用重新选择
            MergeServerCodes();
            return;
        }

        // 检查目录下是否有.cs文件
        int fileCount = Directory.GetFiles(serverProjectPath, "*.cs", SearchOption.AllDirectories).Length;

        if (fileCount == 0)
        {
            bool chooseNewPath = EditorUtility.DisplayDialog(
                "提示",
                $"在路径 \"{serverProjectPath}\" 下未找到任何C#文件！\n\n" +
                "这可能不是正确的服务器工程目录。\n" +
                "是否重新选择路径？",
                "重新选择",
                "取消"
            );

            if (chooseNewPath)
            {
                // 清除保存的路径
                EditorPrefs.DeleteKey(SERVER_PATH_KEY);
                MergeServerCodes();
            }
            return;
        }

        // 执行合并操作
        DoMergeServerCodes(serverProjectPath);
    }

    /// <summary>
    /// 执行合并服务器代码的实际操作
    /// </summary>
    private static void DoMergeServerCodes(string serverProjectPath)
    {
        // 需要排除的文件夹（不需要合并的）
        string[] excludeFolders = new string[]
        {
            "bin",
            "obj",
            ".vs",
            "Properties",
            "Migrations",
            "wwwroot",
            "node_modules",
            ".git"
        };

        // 需要排除的文件扩展名
        string[] excludeExtensions = new string[]
        {
            ".meta",
            ".json",
            ".xml",
            ".config",
            ".csproj",
            ".sln",
            ".user",
            ".suo",
            ".pidb",
            ".db",
            ".sqlite",
            ".log"
        };

        // 收集所有.cs文件
        List<string> allCsFiles = new List<string>();
        Dictionary<string, string> fileContents = new Dictionary<string, string>();

        // 递归遍历目录
        GetAllCsFiles(serverProjectPath, allCsFiles, excludeFolders, excludeExtensions);

        if (allCsFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在路径 {serverProjectPath} 下未找到任何C#文件！", "确定");
            return;
        }

        StringBuilder mergedContent = new StringBuilder();
        int fileCount = 0;

        // 添加文件头信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 服务器代码合并 - 共找到 {allCsFiles.Count} 个C#文件");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine($"// 路径: {serverProjectPath}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        // 按文件夹路径排序
        allCsFiles.Sort();

        foreach (string filePath in allCsFiles)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string relativePath = GetRelativePath(serverProjectPath, filePath);
                string fileName = Path.GetFileName(filePath);

                // 添加文件分隔标记和内容
                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine($"// 文件: {fileName}");
                mergedContent.AppendLine($"// 相对路径: {relativePath}");
                mergedContent.AppendLine($"// 完整路径: {filePath}");
                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine(content);
                mergedContent.AppendLine();
                mergedContent.AppendLine();

                fileContents[relativePath] = content;
                fileCount++;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        // 添加文件统计信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 总计合并 {fileCount} 个C#文件");
        mergedContent.AppendLine("// ============================================");

        // 复制到粘贴板
        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        // 显示成功信息
        string message = $"✅ 成功合并 {fileCount} 个C#文件！\n\n";

        // 显示保存的路径
        message += $"📁 路径: {serverProjectPath}\n\n";

        // 按文件夹分组显示文件列表
        var groupedFiles = fileContents.Keys
            .Select(path => new { FullPath = path, Directory = Path.GetDirectoryName(path) })
            .GroupBy(x => x.Directory)
            .OrderBy(g => g.Key);

        int fileListCount = 0;
        foreach (var group in groupedFiles)
        {
            string displayName = string.IsNullOrEmpty(group.Key) ? "根目录" : group.Key;
            message += $"  📁 {displayName}\n";
            foreach (var item in group)
            {
                if (fileListCount < 30) // 限制显示数量
                {
                    message += $"    📄 {Path.GetFileName(item.FullPath)}\n";
                }
                fileListCount++;
            }
        }

        if (fileListCount > 30)
        {
            message += $"    ... 还有 {fileListCount - 30} 个文件\n";
        }

        message += $"\n📋 内容已复制到粘贴板，可以直接粘贴使用。";

        // 显示对话框
        EditorUtility.DisplayDialog("合并完成", message, "确定");

        // 输出到控制台
        Debug.Log($"✅ 已合并 {fileCount} 个服务器C#文件，内容已复制到粘贴板。");
        Debug.Log($"📁 服务器路径: {serverProjectPath}");

        // 输出完整文件列表到控制台
        Debug.Log("完整文件列表:\n" + string.Join("\n", allCsFiles));
    }

    /// <summary>
    /// 递归获取所有.cs文件
    /// </summary>
    private static void GetAllCsFiles(string directory, List<string> csFiles, string[] excludeFolders, string[] excludeExtensions)
    {
        try
        {
            // 获取当前目录下所有.cs文件
            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);

            foreach (string file in files)
            {
                // 检查文件扩展名是否在排除列表中
                string ext = Path.GetExtension(file);
                if (excludeExtensions.Contains(ext))
                    continue;

                csFiles.Add(file);
            }

            // 递归遍历子目录
            string[] subDirectories = Directory.GetDirectories(directory);

            foreach (string subDir in subDirectories)
            {
                string dirName = Path.GetFileName(subDir);

                // 检查是否在排除列表中
                if (excludeFolders.Contains(dirName))
                    continue;

                // 跳过隐藏目录
                if (dirName.StartsWith("."))
                    continue;

                GetAllCsFiles(subDir, csFiles, excludeFolders, excludeExtensions);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"访问目录失败: {directory}, 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取相对路径
    /// </summary>
    private static string GetRelativePath(string basePath, string fullPath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
            return fullPath;

        // 确保路径格式一致
        basePath = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        fullPath = Path.GetFullPath(fullPath);

        if (!fullPath.StartsWith(basePath))
            return fullPath;

        string relativePath = fullPath.Substring(basePath.Length + 1);
        return relativePath;
    }

    /// <summary>
    /// 清除保存的服务器路径
    /// </summary>
    [MenuItem("Tools/Zpf/清除服务器路径")]
    public static void ClearServerPath()
    {
        if (EditorPrefs.HasKey(SERVER_PATH_KEY))
        {
            EditorPrefs.DeleteKey(SERVER_PATH_KEY);
            Debug.Log("✅ 已清除保存的服务器路径");
            EditorUtility.DisplayDialog("提示", "已清除保存的服务器路径！", "确定");
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "没有保存的服务器路径。", "确定");
        }
    }

}
#endif