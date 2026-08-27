#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

public class ScriptFinderByConditionTool : Editor
{
    private const string SERVER_PATH_KEY = "ZpfTool_ServerPath";

    // 历史记录相关
    private static string _lastClassName = "";
    private static List<string> _previewFileNames = new List<string>();

    // ============================================================
    // 获取编辑器工具脚本
    // ============================================================

    [MenuItem("Tools/获取脚本/获取所有编辑器工具脚本")]
    public static void GetAllEditorScripts()
    {
        string[] allCsFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        if (allCsFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何 C# 脚本文件！", "确定");
            return;
        }

        string[] editorKeywords = new string[]
        {
            "UnityEditor",
            "[MenuItem",
            "[InitializeOnLoad",
            "EditorWindow",
            "EditorGUILayout",
            "EditorGUI",
            "GUILayout",
            "EditorApplication",
            "AssetDatabase",
            "Selection.",
            "EditorUtility",
            "EditorGUIUtility",
            "EditorStyles",
            "HandleUtility",
            "SceneView",
            "EditorSceneManager",
            "PrefabUtility",
            "Undo",
            "SerializedProperty",
            "SerializedObject",
            "CustomEditor",
            "CanEditMultipleObjects",
            "InitializeOnLoadMethod",
            "ContextMenu",
            "ContextMenuItem",
            "Toolbar",
            "PopupWindow",
            "DropdownMenu",
            "GenericMenu",
            "EditorSettings",
            "BuildPipeline",
            "EditorUserBuildSettings",
            "PlayerSettings",
            "BuildPlayerOptions"
        };

        List<string> matchedFiles = new List<string>();
        Dictionary<string, List<string>> fileMatches = new Dictionary<string, List<string>>();

        foreach (string filePath in allCsFiles)
        {
            try
            {
                if (filePath.Contains("/Plugins/") || filePath.Contains("\\Plugins\\"))
                    continue;

                if (filePath.Contains("PackageCache") || filePath.Contains("BuiltInPackages"))
                    continue;

                string content = File.ReadAllText(filePath, Encoding.UTF8);
                bool hasMatch = false;
                List<string> foundKeywords = new List<string>();

                bool isInEditorFolder = filePath.Contains("/Editor/") || filePath.Contains("\\Editor\\");

                foreach (string keyword in editorKeywords)
                {
                    if (content.Contains(keyword))
                    {
                        hasMatch = true;
                        foundKeywords.Add(keyword);
                    }
                }

                if (content.Contains("using UnityEditor;"))
                {
                    hasMatch = true;
                    if (!foundKeywords.Contains("using UnityEditor;"))
                        foundKeywords.Add("using UnityEditor;");
                }

                if (hasMatch || isInEditorFolder)
                {
                    matchedFiles.Add(filePath);
                    fileMatches[filePath] = foundKeywords;
                }
            }
            catch (System.Exception ex)
            {
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        if (matchedFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何编辑器工具脚本！", "确定");
            return;
        }

        StringBuilder mergedContent = new StringBuilder();

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 编辑器工具脚本列表");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine($"// 找到文件数: {matchedFiles.Count}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        var groupedFiles = matchedFiles
            .Select(f => new { Path = f, RelativePath = f.Replace(Application.dataPath, "Assets") })
            .GroupBy(f => Path.GetDirectoryName(f.RelativePath))
            .OrderBy(g => g.Key);

        mergedContent.AppendLine("// 📁 按目录分组统计：");
        foreach (var group in groupedFiles)
        {
            string dirName = string.IsNullOrEmpty(group.Key) ? "(根目录)" : group.Key;
            mergedContent.AppendLine($"//   📁 {dirName}: {group.Count()} 个文件");
        }
        mergedContent.AppendLine();
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine("// 📄 完整文件内容");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        long totalSize = 0;
        List<string> sortedFiles = new List<string>(matchedFiles);
        sortedFiles.Sort();

        foreach (string filePath in sortedFiles)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string relativePath = filePath.Replace(Application.dataPath, "Assets");
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;
                totalSize += fileSize;

                string keywordsStr = fileMatches.ContainsKey(filePath) ? string.Join(", ", fileMatches[filePath]) : "(在Editor目录下)";

                bool isInEditorFolder = filePath.Contains("/Editor/") || filePath.Contains("\\Editor\\");
                string folderLabel = isInEditorFolder ? "📁 [Editor目录]" : "📁 [含编辑器代码]";

                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine($"// 📄 文件: {fileName}");
                mergedContent.AppendLine($"// 📂 路径: {relativePath}");
                mergedContent.AppendLine($"// 🔑 匹配关键词: {keywordsStr}");
                mergedContent.AppendLine($"// 📂 类型: {folderLabel}");
                mergedContent.AppendLine($"// 📊 大小: {FormatFileSize(fileSize)}");
                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine(content);
                mergedContent.AppendLine();
                mergedContent.AppendLine();
            }
            catch (System.Exception ex)
            {
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 📊 统计信息");
        mergedContent.AppendLine($"// ============================================");
        mergedContent.AppendLine($"// 总文件数: {sortedFiles.Count}");
        mergedContent.AppendLine($"// 总大小: {FormatFileSize(totalSize)}");
        mergedContent.AppendLine("// ============================================");

        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        string message = $"✅ 找到 {sortedFiles.Count} 个编辑器工具脚本！\n\n";
        message += $"📊 文件已按目录分组，内容已复制到粘贴板！\n\n";
        message += $"📁 目录分布:\n";
        foreach (var group in groupedFiles)
        {
            string dirName = string.IsNullOrEmpty(group.Key) ? "(根目录)" : group.Key;
            message += $"  - {dirName}: {group.Count()} 个\n";
        }

        EditorUtility.DisplayDialog("获取完成", message, "确定");

        Z_Logger.Log($"✅ 找到 {sortedFiles.Count} 个编辑器工具脚本，总大小 {FormatFileSize(totalSize)}，内容已复制到粘贴板。");
    }

    // ============================================================
    // 获取合并客户端网络脚本
    // ============================================================

    [MenuItem("Tools/获取脚本/获取合并客户端网络脚本")]
    public static void MergeNetServerManagerScripts()
    {
        string[] guids = AssetDatabase.FindAssets("NetServerManager t:Script");

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何NetServerManager脚本文件！", "确定");
            return;
        }

        Dictionary<string, string> scriptContents = new Dictionary<string, string>();
        StringBuilder mergedContent = new StringBuilder();
        int fileCount = 0;

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// NetServerManager 合并脚本 - 共找到 {guids.Length} 个partial文件");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (!assetPath.EndsWith(".cs"))
                continue;

            string fileName = Path.GetFileName(assetPath);

            string content = File.ReadAllText(assetPath, Encoding.UTF8);

            if (content.Contains("partial") && content.Contains("NetServerManager"))
            {
                fileCount++;
                scriptContents[fileName] = content;

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

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 总计合并 {fileCount} 个partial文件");
        mergedContent.AppendLine("// ============================================");

        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        string message = $"成功合并 {fileCount} 个NetServerManager partial文件！\n\n";
        message += "文件列表：\n";
        foreach (string fileName in scriptContents.Keys)
        {
            message += $"  - {fileName}\n";
        }
        message += "\n内容已复制到粘贴板，可以直接粘贴使用。";

        EditorUtility.DisplayDialog("合并完成", message, "确定");

        Z_Logger.Log($"已合并 {fileCount} 个NetServerManager partial文件，内容已复制到粘贴板。");
    }

    // ============================================================
    // 获取合并服务器代码
    // ============================================================

    [MenuItem("Tools/获取脚本/获取合并服务器代码")]
    public static void MergeServerCodes()
    {
        string serverProjectPath = EditorPrefs.GetString(SERVER_PATH_KEY, "");
        bool pathValid = !string.IsNullOrEmpty(serverProjectPath) && Directory.Exists(serverProjectPath);

        string dialogMessage = "选择操作：";
        string dialogTitle = "获取合并服务器代码";

        string useCurrentPathBtn = "使用当前路径";
        string selectNewPathBtn = "重新选择路径";
        string cancelBtn = "取消";

        if (pathValid)
        {
            dialogMessage = $"当前保存的路径：\n{serverProjectPath}\n\n选择操作：";
        }
        else
        {
            dialogMessage = "未找到有效的服务器代码路径！\n请选择服务器工程根目录。\n\n示例: E:\\TuanjieProject\\WxEndlessDriftServer";
            useCurrentPathBtn = "选择路径";
        }

        int result = EditorUtility.DisplayDialogComplex(
            dialogTitle,
            dialogMessage,
            useCurrentPathBtn,
            cancelBtn,
            selectNewPathBtn
        );

        if (result == 1) return;

        if (result == 2)
        {
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

            serverProjectPath = selectedPath;
            EditorPrefs.SetString(SERVER_PATH_KEY, serverProjectPath);

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
                    EditorPrefs.DeleteKey(SERVER_PATH_KEY);
                    MergeServerCodes();
                    return;
                }
            }
        }
        else
        {
            if (!pathValid)
            {
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
        }

        if (!Directory.Exists(serverProjectPath))
        {
            EditorUtility.DisplayDialog("错误", $"路径不存在！\n{serverProjectPath}\n\n请重新选择。", "确定");
            EditorPrefs.DeleteKey(SERVER_PATH_KEY);
            MergeServerCodes();
            return;
        }

        DoMergeServerCodes(serverProjectPath);
    }

    private static void DoMergeServerCodes(string serverProjectPath)
    {
        string[] excludeFolders = new string[]
        {
            "bin", "obj", ".vs", "Properties", "Migrations", "wwwroot",
            "node_modules", ".git", "packages", "TestResults"
        };

        string[] excludeExtensions = new string[]
        {
            ".meta", ".xml", ".config", ".csproj", ".sln", ".user", ".suo",
            ".pidb", ".db", ".sqlite", ".log", ".dll", ".exe", ".pdb",
            ".cache", ".editorconfig", ".gitignore", ".gitattributes"
        };

        List<string> allCsFiles = new List<string>();
        List<string> allJsonFiles = new List<string>();
        Dictionary<string, string> fileContents = new Dictionary<string, string>();

        GetAllFiles(serverProjectPath, allCsFiles, allJsonFiles, excludeFolders, excludeExtensions);

        int totalFiles = allCsFiles.Count + allJsonFiles.Count;

        if (totalFiles == 0)
        {
            EditorUtility.DisplayDialog("提示", $"在路径 {serverProjectPath} 下未找到任何C#或JSON文件！", "确定");
            return;
        }

        StringBuilder mergedContent = new StringBuilder();

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

        string directoryStructure = GetDirectoryStructure(serverProjectPath, allCsFiles, allJsonFiles);
        mergedContent.AppendLine(directoryStructure);
        mergedContent.AppendLine();
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine("// 📄 文件内容");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

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

                if (fileType == ".cs")
                    csFileCount++;
                else if (fileType == ".json")
                    jsonFileCount++;

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
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 📊 总计合并文件: {totalFiles}");
        mergedContent.AppendLine($"//   - C#文件: {csFileCount}");
        mergedContent.AppendLine($"//   - JSON文件: {jsonFileCount}");
        mergedContent.AppendLine("// ============================================");

        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        string message = $"✅ 成功合并 {totalFiles} 个文件！\n\n";
        message += $"📁 路径: {serverProjectPath}\n\n";
        message += $"📊 文件统计:\n";
        message += $"  - C#文件: {csFileCount} 个\n";
        message += $"  - JSON文件: {jsonFileCount} 个\n\n";

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

        EditorUtility.DisplayDialog("合并完成", message, "确定");

        Z_Logger.Log($"✅ 已合并 {totalFiles} 个服务器文件（{csFileCount}个C# + {jsonFileCount}个JSON），内容已复制到粘贴板。");
        Z_Logger.Log($"📁 服务器路径: {serverProjectPath}");
    }

    private static void GetAllFiles(string directory, List<string> csFiles, List<string> jsonFiles, string[] excludeFolders, string[] excludeExtensions)
    {
        try
        {
            string[] csFileList = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);
            foreach (string file in csFileList)
            {
                string ext = Path.GetExtension(file);
                if (excludeExtensions.Contains(ext))
                    continue;
                csFiles.Add(file);
            }

            string[] jsonFileList = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string file in jsonFileList)
            {
                string ext = Path.GetExtension(file);
                if (excludeExtensions.Contains(ext))
                    continue;
                jsonFiles.Add(file);
            }

            string[] subDirectories = Directory.GetDirectories(directory);

            foreach (string subDir in subDirectories)
            {
                string dirName = Path.GetFileName(subDir);

                if (excludeFolders.Contains(dirName))
                    continue;

                if (dirName.StartsWith("."))
                    continue;

                GetAllFiles(subDir, csFiles, jsonFiles, excludeFolders, excludeExtensions);
            }
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogWarning($"访问目录失败: {directory}, 错误: {ex.Message}");
        }
    }

    private static string GetDirectoryStructure(string basePath, List<string> csFiles, List<string> jsonFiles)
    {
        StringBuilder sb = new StringBuilder();

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

        var sortedDirs = allDirectories.OrderBy(d => d).ToList();

        foreach (var dir in sortedDirs)
        {
            string relativeDir = GetRelativePath(basePath, dir);
            if (string.IsNullOrEmpty(relativeDir))
                continue;

            int depth = relativeDir.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
            string indent = new string(' ', depth * 2);

            string dirName = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(dirName))
                dirName = "根目录";

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

    private static string GetRelativePath(string basePath, string fullPath)
    {
        if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
            return fullPath;

        try
        {
            string normalizedBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedFull = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedBase, normalizedFull, System.StringComparison.OrdinalIgnoreCase))
                return ".";

            if (!normalizedFull.StartsWith(normalizedBase, System.StringComparison.OrdinalIgnoreCase))
                return fullPath;

            if (normalizedFull.Length == normalizedBase.Length)
                return ".";

            int startIndex = normalizedBase.Length;
            if (normalizedFull[startIndex] == Path.DirectorySeparatorChar ||
                normalizedFull[startIndex] == Path.AltDirectorySeparatorChar)
            {
                startIndex++;
            }

            if (startIndex >= normalizedFull.Length)
                return ".";

            return normalizedFull.Substring(startIndex);
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogWarning($"获取相对路径失败: basePath={basePath}, fullPath={fullPath}, 错误: {ex.Message}");
            return fullPath;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    // ============================================================
    // 获取所有客户端C#脚本
    // ============================================================

    [MenuItem("Tools/获取脚本/获取所有客户端C#脚本")]
    public static void GetAllCSharpScripts()
    {
        string[] allCsFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        if (allCsFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何 C# 脚本文件！", "确定");
            return;
        }

        StringBuilder mergedContent = new StringBuilder();

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// Assets 目录下所有 C# 脚本合并");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine($"// 文件总数: {allCsFiles.Length}");
        mergedContent.AppendLine($"// Assets 路径: {Application.dataPath}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        List<string> sortedFiles = new List<string>(allCsFiles);
        sortedFiles.Sort();

        long totalSize = 0;
        Dictionary<string, int> folderFileCount = new Dictionary<string, int>();

        foreach (string filePath in sortedFiles)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string relativePath = filePath.Replace(Application.dataPath, "Assets");
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;
                totalSize += fileSize;

                string dirName = Path.GetDirectoryName(relativePath);
                if (!folderFileCount.ContainsKey(dirName))
                    folderFileCount[dirName] = 0;
                folderFileCount[dirName]++;

                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine($"// 📄 文件: {fileName}");
                mergedContent.AppendLine($"// 📂 路径: {relativePath}");
                mergedContent.AppendLine($"// 📊 大小: {FormatFileSize(fileSize)}");
                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine(content);
                mergedContent.AppendLine();
                mergedContent.AppendLine();
            }
            catch (System.Exception ex)
            {
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 📊 统计信息");
        mergedContent.AppendLine($"// ============================================");
        mergedContent.AppendLine($"// 总文件数: {sortedFiles.Count}");
        mergedContent.AppendLine($"// 总大小: {FormatFileSize(totalSize)}");
        mergedContent.AppendLine();
        mergedContent.AppendLine("// 📁 按目录统计：");
        foreach (var kvp in folderFileCount.OrderBy(x => x.Key))
        {
            mergedContent.AppendLine($"//   {kvp.Key}: {kvp.Value} 个文件");
        }
        mergedContent.AppendLine("// ============================================");

        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        string message = $"✅ 成功获取 {sortedFiles.Count} 个 C# 脚本！\n\n";
        message += $"📁 Assets 路径: {Application.dataPath}\n\n";
        message += $"📊 统计:\n";
        message += $"  - 文件总数: {sortedFiles.Count}\n";
        message += $"  - 总大小: {FormatFileSize(totalSize)}\n\n";
        message += $"📋 内容已复制到粘贴板！";

        EditorUtility.DisplayDialog("获取完成", message, "确定");

        Z_Logger.Log($"✅ 已获取 {sortedFiles.Count} 个 C# 脚本，总大小 {FormatFileSize(totalSize)}，内容已复制到粘贴板。");
    }

    // ============================================================
    // 获取指定类名的脚本（核心功能）
    // ============================================================

    [MenuItem("Tools/获取脚本/获取指定类名的脚本")]
    public static void GetScriptsByClassName()
    {
        ClassNameInputWindow window = EditorWindow.GetWindow<ClassNameInputWindow>(true, "🔍 搜索类名", true);
        window.ShowModal();
    }

    // ============================================================
    // 类名输入窗口（最终优化版）
    // ============================================================

    private class ClassNameInputWindow : EditorWindow
    {
        public string className = "";
        private string[] historyList = new string[0];
        private Vector2 historyScrollPosition = Vector2.zero;
        private bool showHistory = true;
        private Vector2 scrollPosition = Vector2.zero;
        private List<string> previewResults = new List<string>();
        private bool showPreview = false;

        private void OnEnable()
        {
            string historyJson = EditorPrefs.GetString("ScriptFinder_ClassSearchHistory", "");
            if (!string.IsNullOrEmpty(historyJson))
            {
                try
                {
                    historyList = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(historyJson) ?? new string[0];
                }
                catch
                {
                    historyList = new string[0];
                }
            }
            else
            {
                historyList = new string[0];
            }

            className = EditorPrefs.GetString("ScriptFinder_LastClassName", "");

            if (!string.IsNullOrEmpty(className))
            {
                PerformPreview(className);
            }

            position = new Rect(position.x, position.y, 650, 500);
            minSize = new Vector2(500, 400);
        }

        private void OnGUI()
        {
            GUILayout.Space(10);

            // ===== 标题区域 =====
            EditorGUILayout.LabelField("🔍 搜索指定类名的脚本", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("搜索所有 C# 脚本中的 public class 或 public partial class  |  💡 支持部分匹配，自动忽略大小写", EditorStyles.miniLabel);

            GUILayout.Space(8);

            // ===== 输入区域 =====
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("类名:", GUILayout.Width(40));

            // 输入框 - 字体16，高度36
            GUIStyle textFieldStyle = new GUIStyle(EditorStyles.textField);
            textFieldStyle.fontSize = 12;
            textFieldStyle.fixedHeight = 20;
            textFieldStyle.margin = new RectOffset(0, 0, 2, 2);

            className = EditorGUILayout.TextField(className, textFieldStyle);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ===== 操作按钮 =====
            EditorGUILayout.BeginHorizontal();

            // 搜索按钮
            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
            if (GUILayout.Button("🔍 搜索", GUILayout.Height(34), GUILayout.Width(90)))
            {
                if (string.IsNullOrEmpty(className))
                {
                    EditorUtility.DisplayDialog("提示", "请输入要搜索的类名！", "确定");
                    return;
                }
                PerformPreview(className);
            }
            GUI.backgroundColor = Color.white;

            // 清空搜索内容按钮（清空输入框和搜索结果）
            GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
            if (GUILayout.Button("清空内容", GUILayout.Height(34), GUILayout.Width(90)))
            {
                className = "";
                previewResults.Clear();
                showPreview = false;
                EditorPrefs.SetString("ScriptFinder_LastClassName", "");
                Repaint();
            }
            GUI.backgroundColor = Color.white;

            // 复制按钮
            GUI.backgroundColor = new Color(0.3f, 0.5f, 0.8f);
            if (GUILayout.Button("📋 复制完整内容", GUILayout.Height(34), GUILayout.Width(130)))
            {
                if (string.IsNullOrEmpty(className))
                {
                    EditorUtility.DisplayDialog("提示", "请先搜索类名！", "确定");
                    return;
                }
                if (previewResults.Count == 0)
                {
                    EditorUtility.DisplayDialog("提示", "没有匹配的文件可以复制！", "确定");
                    return;
                }
                SearchAndCopyClassScripts(className, false);
            }
            GUI.backgroundColor = Color.white;

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ===== 历史记录 =====
            if (historyList.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                showHistory = EditorGUILayout.Foldout(showHistory, $"📜 搜索历史 ({historyList.Length})", true);

                GUILayout.FlexibleSpace();

                // 清空历史按钮 - 更大
                GUI.backgroundColor = new Color(0.9f, 0.6f, 0.6f);
                if (GUILayout.Button("🗑 清空历史", GUILayout.Width(90), GUILayout.Height(24)))
                {
                    historyList = new string[0];
                    SaveHistory();
                    showHistory = true;
                    Repaint();
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (showHistory && historyList.Length > 0)
                {
                    historyScrollPosition = EditorGUILayout.BeginScrollView(historyScrollPosition, GUILayout.MaxHeight(80));
                    EditorGUILayout.BeginHorizontal();

                    int itemsPerRow = 4;
                    for (int i = 0; i < historyList.Length; i++)
                    {
                        if (i > 0 && i % itemsPerRow == 0)
                        {
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                        }

                        EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(200));

                        // 历史按钮
                        if (GUILayout.Button(historyList[i], GUILayout.Height(26), GUILayout.MinWidth(80)))
                        {
                            className = historyList[i];
                            PerformPreview(className);
                            Repaint();
                        }

                        // 单个删除按钮
                        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                        if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(26)))
                        {
                            var list = historyList.ToList();
                            list.RemoveAt(i);
                            historyList = list.ToArray();
                            SaveHistory();
                            Repaint();
                        }
                        GUI.backgroundColor = Color.white;

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndScrollView();
                }
            }

            GUILayout.Space(8);

            // ===== 分割线 =====
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            GUILayout.Space(6);

            // ===== 预览结果区域 =====
            if (showPreview)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"📄 匹配结果: {previewResults.Count} 个文件", EditorStyles.boldLabel);
                if (previewResults.Count > 0)
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.9f);
                    EditorGUILayout.LabelField($"💡 点击文件名可定位到脚本", EditorStyles.miniLabel);
                    GUI.color = Color.white;
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                if (previewResults.Count == 0)
                {
                    EditorGUILayout.HelpBox($"未找到包含类名 \"{className}\" 的脚本文件", MessageType.Info);
                }
                else
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

                    foreach (string fileName in previewResults)
                    {
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.LabelField("📄", GUILayout.Width(25));

                        GUIStyle linkStyle = new GUIStyle(EditorStyles.label);
                        linkStyle.normal.textColor = new Color(0.2f, 0.4f, 0.8f);
                        linkStyle.hover.textColor = new Color(0.1f, 0.2f, 0.6f);
                        linkStyle.fontSize = 13;
                        linkStyle.padding = new RectOffset(4, 4, 2, 2);

                        if (GUILayout.Button(fileName, linkStyle, GUILayout.Height(24)))
                        {
                            string filePath = FindScriptPath(fileName);
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
                                if (obj != null)
                                {
                                    EditorGUIUtility.PingObject(obj);
                                    Selection.activeObject = obj;
                                }
                                else
                                {
                                    EditorUtility.RevealInFinder(filePath);
                                }
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView();

                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("💡 点击文件名 → 在Project窗口中定位脚本", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("💡 点击「复制完整内容」→ 复制所有匹配文件的完整代码到粘贴板", EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("请输入类名并点击「搜索」按钮，或点击历史记录中的类名", MessageType.Info);
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);
        }

        private void OnLostFocus()
        {
            // 防止窗口失去焦点时自动关闭
        }

        // 执行预览搜索
        private void PerformPreview(string searchClass)
        {
            if (string.IsNullOrEmpty(searchClass))
                return;

            className = searchClass;
            EditorPrefs.SetString("ScriptFinder_LastClassName", searchClass);
            AddHistory(searchClass);
            previewResults = SearchForClassNames(searchClass);
            showPreview = true;
            Repaint();
        }

        // 仅搜索类名，返回文件列表（忽略大小写，支持部分匹配）
        private List<string> SearchForClassNames(string className)
        {
            if (string.IsNullOrEmpty(className))
                return new List<string>();

            string[] allCsFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            List<string> matchedFiles = new List<string>();

            // 支持部分匹配，忽略大小写
            string searchPattern = $@"public\s+(?:partial\s+)?class\s+\S*{Regex.Escape(className)}\S*";

            foreach (string filePath in allCsFiles)
            {
                try
                {
                    if (filePath.Contains("/Plugins/") || filePath.Contains("\\Plugins\\"))
                        continue;

                    if (filePath.Contains("PackageCache") || filePath.Contains("BuiltInPackages"))
                        continue;

                    string content = File.ReadAllText(filePath, Encoding.UTF8);
                    var matches = Regex.Matches(content, searchPattern, RegexOptions.IgnoreCase);

                    if (matches.Count > 0)
                    {
                        matchedFiles.Add(Path.GetFileName(filePath));
                    }
                }
                catch
                {
                    // 忽略读取错误
                }
            }

            matchedFiles.Sort();
            return matchedFiles;
        }

        // 根据文件名查找完整路径
        private string FindScriptPath(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "";

            string[] allCsFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

            foreach (string filePath in allCsFiles)
            {
                if (Path.GetFileName(filePath) == fileName)
                {
                    return filePath.Replace(Application.dataPath, "Assets");
                }
            }
            return "";
        }

        // 保存历史记录
        private void SaveHistory()
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(historyList);
            EditorPrefs.SetString("ScriptFinder_ClassSearchHistory", json);
        }

        // 添加历史记录
        private void AddHistory(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            var list = historyList.ToList();
            list.RemoveAll(x => x.Equals(value, System.StringComparison.OrdinalIgnoreCase));
            list.Insert(0, value);

            if (list.Count > 20)
            {
                list = list.Take(20).ToList();
            }

            historyList = list.ToArray();
            SaveHistory();
        }
    }

    // ============================================================
    // 核心搜索和复制功能
    // ============================================================

    private static void SearchAndCopyClassScripts(string className, bool isPreview = false)
    {
        if (string.IsNullOrEmpty(className))
            return;

        _lastClassName = className;

        string[] allCsFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        if (allCsFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何 C# 脚本文件！", "确定");
            return;
        }

        List<string> matchedFiles = new List<string>();
        Dictionary<string, string> classDefinitions = new Dictionary<string, string>();
        Dictionary<string, string> fileContents = new Dictionary<string, string>();

        // 支持部分匹配，忽略大小写
        string searchPattern = $@"public\s+(?:partial\s+)?class\s+\S*{Regex.Escape(className)}\S*";

        foreach (string filePath in allCsFiles)
        {
            try
            {
                if (filePath.Contains("/Plugins/") || filePath.Contains("\\Plugins\\"))
                    continue;

                if (filePath.Contains("PackageCache") || filePath.Contains("BuiltInPackages"))
                    continue;

                string content = File.ReadAllText(filePath, Encoding.UTF8);
                var matches = Regex.Matches(content, searchPattern, RegexOptions.IgnoreCase);

                if (matches.Count > 0)
                {
                    matchedFiles.Add(filePath);
                    fileContents[filePath] = content;

                    foreach (Match match in matches)
                    {
                        string line = match.Value;
                        int startIndex = content.LastIndexOf('\n', match.Index) + 1;
                        int endIndex = content.IndexOf('\n', match.Index);
                        if (endIndex == -1) endIndex = content.Length;

                        string fullLine = content.Substring(startIndex, endIndex - startIndex).Trim();
                        if (!classDefinitions.ContainsKey(filePath))
                            classDefinitions[filePath] = "";
                        classDefinitions[filePath] += fullLine + "\n";
                    }
                }
            }
            catch (System.Exception ex)
            {
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        if (isPreview)
        {
            return;
        }

        if (matchedFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", $"未找到包含类名 \"{className}\" 的脚本文件！", "确定");
            return;
        }

        StringBuilder outputContent = new StringBuilder();

        outputContent.AppendLine("// ============================================");
        outputContent.AppendLine($"// 包含类名 \"{className}\" 的脚本列表");
        outputContent.AppendLine($"// 搜索时间: {System.DateTime.Now}");
        outputContent.AppendLine($"// 找到文件数: {matchedFiles.Count}");
        outputContent.AppendLine("// ============================================");
        outputContent.AppendLine();

        outputContent.AppendLine("// 📋 类定义列表：");
        foreach (var kvp in classDefinitions.OrderBy(x => x.Key))
        {
            string relativePath = kvp.Key.Replace(Application.dataPath, "Assets");
            outputContent.AppendLine($"// 📁 {relativePath}");
            outputContent.AppendLine($"// {kvp.Value.TrimEnd()}");
            outputContent.AppendLine();
        }

        outputContent.AppendLine("// ============================================");
        outputContent.AppendLine("// 📄 完整文件内容");
        outputContent.AppendLine("// ============================================");
        outputContent.AppendLine();

        long totalSize = 0;
        List<string> sortedFiles = new List<string>(matchedFiles);
        sortedFiles.Sort();

        foreach (string filePath in sortedFiles)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string relativePath = filePath.Replace(Application.dataPath, "Assets");
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;
                totalSize += fileSize;

                string classDef = classDefinitions.ContainsKey(filePath) ? classDefinitions[filePath].TrimEnd() : "未找到";

                outputContent.AppendLine("// ============================================");
                outputContent.AppendLine($"// 📄 文件: {fileName}");
                outputContent.AppendLine($"// 📂 路径: {relativePath}");
                outputContent.AppendLine($"// 📋 类定义: {classDef}");
                outputContent.AppendLine($"// 📊 大小: {FormatFileSize(fileSize)}");
                outputContent.AppendLine("// ============================================");
                outputContent.AppendLine(content);
                outputContent.AppendLine();
                outputContent.AppendLine();
            }
            catch (System.Exception ex)
            {
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        outputContent.AppendLine("// ============================================");
        outputContent.AppendLine($"// 📊 统计信息");
        outputContent.AppendLine($"// ============================================");
        outputContent.AppendLine($"// 搜索类名: {className}");
        outputContent.AppendLine($"// 总文件数: {sortedFiles.Count}");
        outputContent.AppendLine($"// 总大小: {FormatFileSize(totalSize)}");
        outputContent.AppendLine("// ============================================");

        GUIUtility.systemCopyBuffer = outputContent.ToString();

        string message = $"✅ 找到 {sortedFiles.Count} 个包含类名 \"{className}\" 的脚本！\n\n";
        message += "📋 类定义列表:\n";
        foreach (var kvp in classDefinitions.OrderBy(x => x.Key))
        {
            string relativePath = kvp.Key.Replace(Application.dataPath, "Assets");
            string fileName = Path.GetFileName(kvp.Key);
            string classDef = kvp.Value.TrimEnd().Replace("\n", ", ");
            if (classDef.Length > 80)
                classDef = classDef.Substring(0, 80) + "...";
            message += $"  - {fileName}: {classDef}\n";
        }

        message += $"\n📊 统计:\n";
        message += $"  - 文件总数: {sortedFiles.Count}\n";
        message += $"  - 总大小: {FormatFileSize(totalSize)}\n\n";
        message += $"📋 完整内容已复制到粘贴板！";

        EditorUtility.DisplayDialog("搜索完成", message, "确定");

        Z_Logger.Log($"✅ 找到 {sortedFiles.Count} 个包含类名 \"{className}\" 的脚本，总大小 {FormatFileSize(totalSize)}，内容已复制到粘贴板。");
    }

    // ============================================================
    // 获取Resources引用的脚本
    // ============================================================

    [MenuItem("Tools/获取脚本/获取Resources引用的脚本")]
    public static void FindResourcesLoadScripts()
    {
        string[] allCsFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        if (allCsFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何 C# 脚本文件！", "确定");
            return;
        }

        string[] keywords = new string[]
        {
            "Resources.Load", "Resources.LoadAsync", "Resources.LoadAll",
            "Resources.FindObjectsOfTypeAll", "Resources.GetBuiltinResource",
            "Resources.UnloadAsset", "Resources.UnloadUnusedAssets"
        };

        List<string> matchedFiles = new List<string>();
        Dictionary<string, List<string>> fileMatches = new Dictionary<string, List<string>>();

        foreach (string filePath in allCsFiles)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                bool hasMatch = false;
                List<string> foundKeywords = new List<string>();

                foreach (string keyword in keywords)
                {
                    if (content.Contains(keyword))
                    {
                        hasMatch = true;
                        foundKeywords.Add(keyword);
                    }
                }

                if (hasMatch)
                {
                    matchedFiles.Add(filePath);
                    fileMatches[filePath] = foundKeywords;
                }
            }
            catch (System.Exception ex)
            {
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        if (matchedFiles.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何使用 Resources.Load 相关方法的脚本！", "确定");
            return;
        }

        StringBuilder mergedContent = new StringBuilder();

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 使用 Resources.Load 相关方法的脚本列表");
        mergedContent.AppendLine($"// 合并时间: {System.DateTime.Now}");
        mergedContent.AppendLine($"// 找到文件数: {matchedFiles.Count}");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        mergedContent.AppendLine("// 📁 文件列表及匹配的关键词：");
        foreach (var kvp in fileMatches.OrderBy(x => x.Key))
        {
            string relativePath = kvp.Key.Replace(Application.dataPath, "Assets");
            string keywordsStr = string.Join(", ", kvp.Value);
            mergedContent.AppendLine($"//   {relativePath} -> [{keywordsStr}]");
        }
        mergedContent.AppendLine();
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine("// 📄 完整文件内容");
        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine();

        long totalSize = 0;
        List<string> sortedFiles = new List<string>(matchedFiles);
        sortedFiles.Sort();

        foreach (string filePath in sortedFiles)
        {
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string relativePath = filePath.Replace(Application.dataPath, "Assets");
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;
                totalSize += fileSize;

                string keywordsStr = string.Join(", ", fileMatches[filePath]);

                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine($"// 📄 文件: {fileName}");
                mergedContent.AppendLine($"// 📂 路径: {relativePath}");
                mergedContent.AppendLine($"// 🔑 匹配关键词: {keywordsStr}");
                mergedContent.AppendLine($"// 📊 大小: {FormatFileSize(fileSize)}");
                mergedContent.AppendLine("// ============================================");
                mergedContent.AppendLine(content);
                mergedContent.AppendLine();
                mergedContent.AppendLine();
            }
            catch (System.Exception ex)
            {
                Z_Logger.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
            }
        }

        mergedContent.AppendLine("// ============================================");
        mergedContent.AppendLine($"// 📊 统计信息");
        mergedContent.AppendLine($"// ============================================");
        mergedContent.AppendLine($"// 总文件数: {sortedFiles.Count}");
        mergedContent.AppendLine($"// 总大小: {FormatFileSize(totalSize)}");
        mergedContent.AppendLine("// ============================================");

        GUIUtility.systemCopyBuffer = mergedContent.ToString();

        Dictionary<string, int> keywordStats = new Dictionary<string, int>();
        foreach (var matches in fileMatches.Values)
        {
            foreach (string keyword in matches)
            {
                if (!keywordStats.ContainsKey(keyword))
                    keywordStats[keyword] = 0;
                keywordStats[keyword]++;
            }
        }

        string message = $"✅ 找到 {sortedFiles.Count} 个使用 Resources.Load 相关方法的脚本！\n\n";
        message += $"📊 关键词统计:\n";
        foreach (var kvp in keywordStats.OrderByDescending(x => x.Value))
        {
            message += $"  - {kvp.Key}: {kvp.Value} 个文件\n";
        }
        message += $"\n📋 内容已复制到粘贴板！";

        EditorUtility.DisplayDialog("查找完成", message, "确定");

        Z_Logger.Log($"✅ 找到 {sortedFiles.Count} 个使用 Resources.Load 相关方法的脚本，总大小 {FormatFileSize(totalSize)}，内容已复制到粘贴板。");
    }
}
#endif
