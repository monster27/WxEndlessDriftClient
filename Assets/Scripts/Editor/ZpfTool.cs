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

    // ============================================
    // 🆕 场景相关工具 - 使用文件存储场景路径
    // ============================================

    // 存储场景路径的文件名
    private const string SCENE_CACHE_FILE = "LastScenePath.cache";

    /// <summary>
    /// 获取缓存文件路径
    /// </summary>
    private static string GetCacheFilePath()
    {
        // 存储在项目的Temp文件夹中，这样不会污染版本控制
        string tempPath = Path.Combine(Application.dataPath, "..", "Temp");
        if (!Directory.Exists(tempPath))
        {
            Directory.CreateDirectory(tempPath);
        }
        return Path.Combine(tempPath, SCENE_CACHE_FILE);
    }

    /// <summary>
    /// 保存场景路径到文件
    /// </summary>
    private static void SaveScenePathToCache(string scenePath)
    {
        try
        {
            string filePath = GetCacheFilePath();
            File.WriteAllText(filePath, scenePath);
            Debug.Log($"🔵 [SaveScenePath] 已保存场景路径: {scenePath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"🔴 [SaveScenePath] 保存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从文件读取场景路径
    /// </summary>
    private static string LoadScenePathFromCache()
    {
        try
        {
            string filePath = GetCacheFilePath();
            if (File.Exists(filePath))
            {
                string scenePath = File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
                {
                    Debug.Log($"🔵 [LoadScenePath] 读取成功: {scenePath}");
                    return scenePath;
                }
                else
                {
                    Debug.Log("🔵 [LoadScenePath] 缓存文件内容无效，删除缓存");
                    File.Delete(filePath);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"🔴 [LoadScenePath] 读取失败: {ex.Message}");
        }
        return "";
    }

    /// <summary>
    /// 清除缓存文件
    /// </summary>
    private static void ClearScenePathCache()
    {
        try
        {
            string filePath = GetCacheFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log("🔵 [ClearScenePath] 缓存已清除");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"🔴 [ClearScenePath] 清除失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 场景相关场景（Build Index 0）- 运行结束后切换到GameScene
    /// </summary>
    [MenuItem("Tools/场景相关/切换到第一个场景", priority = 1)]
    public static void RunScene0()
    {
        Debug.Log("🔵 [RunScene0] 开始执行...");

        // 获取Build Settings中的场景列表
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "Build Settings中没有场景！请先添加场景到Build Settings。", "确定");
            return;
        }

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes[0].path);
    }

    /// <summary>
    /// 🆕 获取GameScene的路径
    /// </summary>
    private static string GetGameScenePath()
    {
        // 方法1：从Build Settings中查找
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (var scene in scenes)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scene.path);
            if (sceneName.Equals("GameScene", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"🔵 [GetGameScenePath] 在Build Settings中找到GameScene: {scene.path}");
                return scene.path;
            }
        }

        // 方法2：如果Build Settings中没有，尝试在Assets中查找
        string[] guids = AssetDatabase.FindAssets("GameScene t:Scene");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Debug.Log($"🔵 [GetGameScenePath] 在Assets中找到GameScene: {path}");
            return path;
        }

        Debug.LogWarning("🔴 [GetGameScenePath] 未找到GameScene！");
        return "";
    }


    /// <summary>
    /// 检测Play模式是否结束，结束后切换到GameScene
    /// </summary>
    private static void CheckPlayModeEnd()
    {
        // 检测Play模式是否刚结束（从true变为false）
        if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode == false)
        {
            // 取消注册回调，避免重复执行
            EditorApplication.update -= CheckPlayModeEnd;

            // 从文件读取GameScene路径
            string gameScenePath = LoadScenePathFromCache();

            Debug.Log($"🔵 [CheckPlayModeEnd] 从缓存读取到场景路径: '{gameScenePath}'");

            // 切换到GameScene
            if (!string.IsNullOrEmpty(gameScenePath) && File.Exists(gameScenePath))
            {
                Debug.Log($"📌 正在切换到GameScene: {Path.GetFileNameWithoutExtension(gameScenePath)}");
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(gameScenePath);
                Debug.Log("✅ 已切换到GameScene！");
                // 清除缓存
                ClearScenePathCache();
            }
            else
            {
                Debug.Log("ℹ️ 没有可切换的GameScene，当前停留在目标场景。");
            }
        }
    }

    /// <summary>
    /// 🆕 手动切换到GameScene按钮
    /// </summary>
    [MenuItem("Tools/场景相关/切换到GameScene")]
    public static void SwitchToGameScene()
    {
        string gameScenePath = GetGameScenePath();

        if (!string.IsNullOrEmpty(gameScenePath) && File.Exists(gameScenePath))
        {
            Debug.Log($"📌 手动切换到GameScene: {Path.GetFileNameWithoutExtension(gameScenePath)}");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(gameScenePath);
            Debug.Log($"✅ 已切换到GameScene: {Path.GetFileNameWithoutExtension(gameScenePath)}");
            //EditorUtility.DisplayDialog("切换成功", $"已切换到GameScene: {Path.GetFileNameWithoutExtension(gameScenePath)}", "确定");
        }
        else
        {
            Debug.Log("ℹ️ 未找到GameScene。");
            EditorUtility.DisplayDialog("提示",
                "未找到GameScene！\n\n" +
                "请确保场景文件名为 'GameScene' 或已添加到Build Settings中。",
                "确定");
        }
    }

    // ============================================
    // 原有的其他工具方法
    // ============================================

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
    [MenuItem("Tools/获取脚本/获取合并客户端网络脚本")]
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
    [MenuItem("Tools/获取脚本/获取合并服务器代码")]
    public static void MergeServerCodes()
    {
        // 从EditorPrefs获取保存的路径
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

    /// <summary>
    /// 执行合并服务器代码的实际操作
    /// </summary>
    private static void DoMergeServerCodes(string serverProjectPath)
    {
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
                Debug.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
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
            Debug.LogWarning($"访问目录失败: {directory}, 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取目录结构树
    /// </summary>
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

    /// <summary>
    /// 获取相对路径（修复了边界情况）
    /// </summary>
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

    // ============================================
    // 🆕 新增：获取 Asset 下所有 C# 脚本
    // ============================================

    /// <summary>
    /// 获取 Assets 目录下所有 C# 脚本文件并合并到粘贴板
    /// </summary>
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
                Debug.LogWarning($"读取文件失败: {filePath}, 错误: {ex.Message}");
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

        Debug.Log($"✅ 已获取 {sortedFiles.Count} 个 C# 脚本，总大小 {FormatFileSize(totalSize)}，内容已复制到粘贴板。");
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
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

}
#endif
