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
    /// 获取服务器工程所有C#代码和JSON文件并合并到粘贴板
    /// </summary>
    [MenuItem("Tools/获取合并服务器代码")]
    public static void MergeServerCodes()
    {
        // 从EditorPrefs获取保存的路径
        string serverProjectPath = EditorPrefs.GetString(SERVER_PATH_KEY, "");
        bool pathValid = !string.IsNullOrEmpty(serverProjectPath) && Directory.Exists(serverProjectPath);

        // ✅ 总是显示选择对话框，让用户决定
        string dialogMessage = "选择操作：";
        string dialogTitle = "获取合并服务器代码";

        // 构建按钮文本
        string useCurrentPathBtn = "使用当前路径";
        string selectNewPathBtn = "重新选择路径";
        string cancelBtn = "取消";

        // 如果有保存的路径，在消息中显示
        if (pathValid)
        {
            dialogMessage = $"当前保存的路径：\n{serverProjectPath}\n\n选择操作：";
        }
        else
        {
            dialogMessage = "未找到有效的服务器代码路径！\n请选择服务器工程根目录。\n\n示例: E:\\TuanjieProject\\WxEndlessDriftServer";
            useCurrentPathBtn = "选择路径"; // 没有有效路径时，这个按钮变成"选择路径"
        }

        // ✅ 显示三个按钮的对话框
        int result = EditorUtility.DisplayDialogComplex(
            dialogTitle,
            dialogMessage,
            useCurrentPathBtn,      // 第一个按钮（绿色/蓝色）
            cancelBtn,              // 第二个按钮（红色/取消）
            selectNewPathBtn        // 第三个按钮（灰色/备用）
        );

        // result 返回值：
        // 0 = 第一个按钮 (使用当前路径 / 选择路径)
        // 1 = 第二个按钮 (取消)
        // 2 = 第三个按钮 (重新选择路径)

        if (result == 1) // 取消
        {
            return;
        }

        if (result == 2) // 重新选择路径
        {
            // 打开文件夹选择对话框
            string selectedPath = EditorUtility.OpenFolderPanel(
                "选择服务器工程目录",
                pathValid ? serverProjectPath : "",
                ""
            );

            if (string.IsNullOrEmpty(selectedPath))
            {
                EditorUtility.DisplayDialog("提示", "未选择任何路径，操作已取消。", "确定");
                return;
            }

            // 保存新路径
            serverProjectPath = selectedPath;
            EditorPrefs.SetString(SERVER_PATH_KEY, serverProjectPath);

            // 验证路径下是否有.cs或.json文件
            int csFileCount = Directory.GetFiles(serverProjectPath, "*.cs", SearchOption.AllDirectories).Length;
            int jsonFileCount = Directory.GetFiles(serverProjectPath, "*.json", SearchOption.AllDirectories).Length;

            if (csFileCount == 0 && jsonFileCount == 0)
            {
                bool retry = EditorUtility.DisplayDialog(
                    "警告",
                    $"在路径 \"{serverProjectPath}\" 下未找到任何C#或JSON文件！\n\n这可能不是正确的服务器工程目录。\n是否重新选择？",
                    "重新选择",
                    "继续"
                );

                if (retry)
                {
                    // 重新选择
                    EditorPrefs.DeleteKey(SERVER_PATH_KEY);
                    MergeServerCodes();
                    return;
                }
                // 用户选择继续，尽管没有文件
            }
        }
        else // result == 0 (使用当前路径 或 选择路径)
        {
            if (!pathValid)
            {
                // 没有有效路径时，第一个按钮是"选择路径"，需要打开文件夹选择
                string selectedPath = EditorUtility.OpenFolderPanel(
                    "选择服务器工程目录",
                    "",
                    ""
                );

                if (string.IsNullOrEmpty(selectedPath))
                {
                    EditorUtility.DisplayDialog("提示", "未选择任何路径，操作已取消。", "确定");
                    return;
                }

                serverProjectPath = selectedPath;
                EditorPrefs.SetString(SERVER_PATH_KEY, serverProjectPath);
            }
            // 否则使用当前路径
        }

        // 最后检查路径是否有效
        if (!Directory.Exists(serverProjectPath))
        {
            EditorUtility.DisplayDialog("错误", $"路径不存在！\n{serverProjectPath}\n\n请重新选择。", "确定");
            EditorPrefs.DeleteKey(SERVER_PATH_KEY);
            MergeServerCodes();
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
            ".git",
            "packages",
            "TestResults"
        };

        // 需要排除的文件扩展名
        string[] excludeExtensions = new string[]
        {
            ".meta",
            ".xml",
            ".config",
            ".csproj",
            ".sln",
            ".user",
            ".suo",
            ".pidb",
            ".db",
            ".sqlite",
            ".log",
            ".dll",
            ".exe",
            ".pdb",
            ".cache",
            ".editorconfig",
            ".gitignore",
            ".gitattributes"
        };

        // 收集所有.cs和.json文件
        List<string> allCsFiles = new List<string>();
        List<string> allJsonFiles = new List<string>();
        Dictionary<string, string> fileContents = new Dictionary<string, string>();

        // 递归遍历目录
        GetAllFiles(serverProjectPath, allCsFiles, allJsonFiles, excludeFolders, excludeExtensions);

        int totalFiles = allCsFiles.Count + allJsonFiles.Count;

        if (totalFiles == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在路径 {serverProjectPath} 下未找到任何C#或JSON文件！", "确定");
            return;
        }

        StringBuilder mergedContent = new StringBuilder();

        // 添加文件头信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 服务器代码合并 - 文件统计信息");
        mergedContent.AppendLine($"// ============================================");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine($"// 根路径: {serverProjectPath}");
        mergedContent.AppendLine($"// 总文件数: {totalFiles}");
        mergedContent.AppendLine($"//   - C#文件: {allCsFiles.Count}");
        mergedContent.AppendLine($"//   - JSON文件: {allJsonFiles.Count}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine("// 📁 文件目录结构");
        mergedContent.AppendLine("// ============================================");

        // 打印目录结构
        string directoryStructure = GetDirectoryStructure(serverProjectPath, allCsFiles, allJsonFiles);
        mergedContent.AppendLine(directoryStructure);
        mergedContent.AppendLine();
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine("// 📄 文件内容");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        // 按文件类型分组排序（先显示.cs，再显示.json）
        var allFiles = new List<string>();
        allFiles.AddRange(allCsFiles);
        allFiles.AddRange(allJsonFiles);
        allFiles.Sort();

        int csFileCount = 0;
        int jsonFileCount = 0;

        foreach (string filePath in allFiles)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string relativePath = GetRelativePath(serverProjectPath, filePath);
                string fileName = Path.GetFileName(filePath);
                string fileType = Path.GetExtension(filePath).ToLower();
                string fileTypeLabel = fileType == ".cs" ? "C#" : "JSON";

                // 记录文件类型统计
                if (fileType == ".cs")
                    csFileCount++;
                else if (fileType == ".json")
                    jsonFileCount++;

                // 添加文件分隔标记和内容
                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine($"// 📄 文件: {fileName}");
                mergedContent.AppendLine($"// 📁 类型: {fileTypeLabel}");
                mergedContent.AppendLine($"// 📂 相对路径: {relativePath}");
                mergedContent.AppendLine($"// 📍 完整路径: {filePath}");
                mergedContent.AppendLine($"// 📊 文件大小: {new FileInfo(filePath).Length} 字节");
                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine(content);
                mergedContent.AppendLine();
                mergedContent.AppendLine();

                fileContents[relativePath] = content;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        // 添加文件统计信息
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 📊 总计合并文件: {totalFiles}");
        mergedContent.AppendLine($"//   - C#文件: {csFileCount}");
        mergedContent.AppendLine($"//   - JSON文件: {jsonFileCount}");
        mergedContent.AppendLine("// ============================================");

        // 复制到粘贴板
        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        // 显示成功信息
        string message = $"✅ 成功合并 {totalFiles} 个文件！\n\n";
        message += $"📁 路径: {serverProjectPath}\n\n";
        message += $"📊 文件统计:\n";
        message += $"  - C#文件: {csFileCount} 个\n";
        message += $"  - JSON文件: {jsonFileCount} 个\n\n";

        // 显示文件列表（限制显示数量）
        int displayLimit = 50;
        int displayedCount = 0;

        message += "📄 文件列表（按目录分组）:\n";

        var groupedFiles = fileContents.Keys
            .Select(path => new { FullPath = path, Directory = Path.GetDirectoryName(path), FileName = Path.GetFileName(path) })
            .OrderBy(x => x.Directory)
            .ThenBy(x => x.FileName);

        string currentDir = null;
        foreach (var item in groupedFiles)
        {
            if (currentDir != item.Directory)
            {
                currentDir = item.Directory;
                string displayDir = string.IsNullOrEmpty(currentDir) ? "(根目录)" : currentDir;
                message += $"\n  📁 {displayDir}\n";
            }

            if (displayedCount < displayLimit)
            {
                string fileIcon = Path.GetExtension(item.FileName).ToLower() == ".cs" ? "📄" : "📋";
                message += $"    {fileIcon} {item.FileName}\n";
            }
            displayedCount++;
        }

        if (displayedCount > displayLimit)
        {
            message += $"\n  ... 还有 {displayedCount - displayLimit} 个文件未显示\n";
        }

        message += $"\n📋 完整内容已复制到粘贴板，可以直接粘贴使用。";
        message += $"\n💡 提示：粘贴板包含完整的目录结构和文件内容。";

        // 显示对话框
        EditorUtility.DisplayDialog("合并完成", message, "确定");

        // 输出到控制台
        Debug.Log($"✅ 已合并 {totalFiles} 个服务器文件（{csFileCount}个C# + {jsonFileCount}个JSON），内容已复制到粘贴板。");
        Debug.Log($"📁 服务器路径: {serverProjectPath}");
    }

    /// <summary>
    /// 递归获取所有.cs和.json文件
    /// </summary>
    private static void GetAllFiles(string directory, List<string> csFiles, List<string> jsonFiles, string[] excludeFolders, string[] excludeExtensions)
    {
        try
        {
            // 获取当前目录下所有.cs文件
            string[] csFileList = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);
            foreach (string file in csFileList)
            {
                string ext = Path.GetExtension(file);
                if (excludeExtensions.Contains(ext))
                    continue;
                csFiles.Add(file);
            }

            // 获取当前目录下所有.json文件
            string[] jsonFileList = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string file in jsonFileList)
            {
                string ext = Path.GetExtension(file);
                if (excludeExtensions.Contains(ext))
                    continue;
                jsonFiles.Add(file);
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

                GetAllFiles(subDir, csFiles, jsonFiles, excludeFolders, excludeExtensions);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"访问目录失败: {directory}, 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取目录结构树
    /// </summary>
    private static string GetDirectoryStructure(string basePath, List<string> csFiles, List<string> jsonFiles)
    {
        StringBuilder sb = new StringBuilder();

        // 获取所有目录
        var allDirectories = new HashSet<string>();
        foreach (var file in csFiles)
        {
            string dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir))
                allDirectories.Add(dir);
        }
        foreach (var file in jsonFiles)
        {
            string dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir))
                allDirectories.Add(dir);
        }

        // 按层级排序
        var sortedDirs = allDirectories.OrderBy(d => d).ToList();

        // 构建树形结构
        foreach (var dir in sortedDirs)
        {
            string relativeDir = GetRelativePath(basePath, dir);
            if (string.IsNullOrEmpty(relativeDir))
                continue;

            // 计算缩进层级
            int depth = relativeDir.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
            string indent = new string(' ', depth * 2);

            string dirName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(dirName))
                dirName = "根目录";

            // 统计该目录下的文件数
            int csCount = csFiles.Where(f => Path.GetDirectoryName(f) == dir).Count();
            int jsonCount = jsonFiles.Where(f => Path.GetDirectoryName(f) == dir).Count();
            string fileInfo = "";
            if (csCount > 0 || jsonCount > 0)
            {
                fileInfo = $" ({csCount}个C#";
                if (jsonCount > 0)
                    fileInfo += $", {jsonCount}个JSON";
                fileInfo += ")";
            }

            sb.AppendLine($"// {indent}📁 {dirName}{fileInfo}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 获取相对路径（修复了边界情况）
    /// </summary>
    private static string GetRelativePath(string basePath, string fullPath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
            return fullPath;

        try
        {
            // 确保路径格式一致
            string normalizedBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedFull = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // 如果路径相同，返回空字符串或当前目录
            if (string.Equals(normalizedBase, normalizedFull, System.StringComparison.OrdinalIgnoreCase))
                return ".";

            // 检查fullPath是否以basePath开头
            if (!normalizedFull.StartsWith(normalizedBase, System.StringComparison.OrdinalIgnoreCase))
                return fullPath;

            // 如果fullPath等于basePath，返回"."
            if (normalizedFull.Length == normalizedBase.Length)
                return ".";

            // 确保basePath后面有路径分隔符
            int startIndex = normalizedBase.Length;
            if (normalizedFull[startIndex] == Path.DirectorySeparatorChar ||
                normalizedFull[startIndex] == Path.AltDirectorySeparatorChar)
            {
                startIndex++;
            }

            // 如果startIndex超出长度，返回"."
            if (startIndex >= normalizedFull.Length)
                return ".";

            return normalizedFull.Substring(startIndex);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"获取相对路径失败: basePath={basePath}, fullPath={fullPath}, 错误: {ex.Message}");
            return fullPath;
        }
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