#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Addressables 打包结果拷贝工具
/// 将 AA 打包输出（除 AddressablesLink 外）拷贝到 CustomCloudAssets 目录
/// </summary>
public class AACopyTool : EditorWindow
{
    private string status = "";
    private string sourcePath = "";
    private string targetPath = "";
    private bool autoCopyOnBuild = false;

    // 固定路径配置
    private const string PROJECT_ROOT = "E:/TuanjieProject/WxEndlessDriftClient";
    private const string SOURCE_RELATIVE_PATH = "Library/com.unity.addressables/aa/WeixinMiniGame";
    private const string TARGET_RELATIVE_PATH = "CustomCloudAssets";
    private const string EXCLUDED_FOLDER = "AddressablesLink";

    [MenuItem("Tools/资源工具/3.拷贝AA打包结果到云资源目录", false)]
    public static void ShowWindow()
    {
        var window = GetWindow<AACopyTool>("AA资源拷贝工具");
        window.minSize = new Vector2(600, 300);
        window.Show();
    }

    private void OnEnable()
    {
        // 初始化路径
        sourcePath = Path.Combine(PROJECT_ROOT, SOURCE_RELATIVE_PATH);
        targetPath = Path.Combine(PROJECT_ROOT, TARGET_RELATIVE_PATH);
        LoadSettings();
    }

    private void LoadSettings()
    {
        // 从 EditorPrefs 加载设置
        autoCopyOnBuild = EditorPrefs.GetBool("AACopyTool_AutoCopyOnBuild", false);
    }

    private void SaveSettings()
    {
        EditorPrefs.SetBool("AACopyTool_AutoCopyOnBuild", autoCopyOnBuild);
    }

    private void OnGUI()
    {
        DrawHeader();
        DrawSeparator();
        DrawPathInfo();
        DrawSeparator();
        DrawStatus();
        DrawSeparator();
        DrawExclusionInfo();
        DrawSeparator();
        DrawActionButtons();
        DrawSeparator();
        DrawSettings();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("📦 AA 打包结果拷贝工具", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("🔄 刷新路径", GUILayout.Width(80)))
        {
            RefreshPaths();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"项目根目录: {PROJECT_ROOT}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"排除文件夹: {EXCLUDED_FOLDER}", EditorStyles.miniLabel);
    }

    private void DrawSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        rect.height = 1;
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
        EditorGUILayout.Space(4);
    }

    private void DrawPathInfo()
    {
        EditorGUILayout.LabelField("📂 路径信息", EditorStyles.boldLabel);

        // 源路径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("源目录:", GUILayout.Width(70));
        GUI.color = new Color(0.8f, 0.9f, 1f);
        EditorGUILayout.TextField(sourcePath, GUILayout.ExpandWidth(true));
        GUI.color = Color.white;
        if (GUILayout.Button("📋", GUILayout.Width(30)))
        {
            GUIUtility.systemCopyBuffer = sourcePath;
            status = "✅ 已复制源路径到剪贴板";
        }
        EditorGUILayout.EndHorizontal();

        // 目标路径
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("目标目录:", GUILayout.Width(70));
        GUI.color = new Color(0.8f, 1f, 0.8f);
        EditorGUILayout.TextField(targetPath, GUILayout.ExpandWidth(true));
        GUI.color = Color.white;
        if (GUILayout.Button("📋", GUILayout.Width(30)))
        {
            GUIUtility.systemCopyBuffer = targetPath;
            status = "✅ 已复制目标路径到剪贴板";
        }
        EditorGUILayout.EndHorizontal();

        // 显示目录是否存在
        EditorGUILayout.BeginHorizontal();
        bool sourceExists = Directory.Exists(sourcePath);
        bool targetExists = Directory.Exists(targetPath);

        GUI.color = sourceExists ? Color.green : Color.red;
        EditorGUILayout.LabelField($"源目录: {(sourceExists ? "✅ 存在" : "❌ 不存在")}", GUILayout.Width(150));
        GUI.color = targetExists ? Color.green : Color.yellow;
        EditorGUILayout.LabelField($"目标目录: {(targetExists ? "✅ 存在" : "⚠️ 将自动创建")}", GUILayout.Width(180));
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(status))
        {
            GUI.color = status.StartsWith("✅") ? Color.green :
                        status.StartsWith("⚠️") ? Color.yellow :
                        status.StartsWith("❌") ? Color.red : Color.white;
            EditorGUILayout.LabelField(status, EditorStyles.boldLabel);
            GUI.color = Color.white;
        }
    }

    private void DrawExclusionInfo()
    {
        EditorGUILayout.LabelField("🚫 排除规则", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"将跳过以下文件夹: {EXCLUDED_FOLDER}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("其他所有文件/文件夹将被拷贝（重复则替换）", EditorStyles.miniLabel);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("🔧 操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🚀 执行拷贝", GUILayout.Height(35)))
        {
            ExecuteCopy();
        }
        if (GUILayout.Button("🧹 清空目标目录", GUILayout.Height(35)))
        {
            ClearTargetDirectory();
        }
        if (GUILayout.Button("📂 打开目标目录", GUILayout.Height(35)))
        {
            OpenTargetDirectory();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📂 打开源目录", GUILayout.Height(30)))
        {
            OpenSourceDirectory();
        }
        if (GUILayout.Button("📊 统计文件", GUILayout.Height(30)))
        {
            ShowStatistics();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSettings()
    {
        EditorGUILayout.LabelField("⚙️ 设置", EditorStyles.boldLabel);

        bool newAutoCopy = EditorGUILayout.Toggle("构建后自动拷贝", autoCopyOnBuild);
        if (newAutoCopy != autoCopyOnBuild)
        {
            autoCopyOnBuild = newAutoCopy;
            SaveSettings();
            if (autoCopyOnBuild)
            {
                status = "✅ 已启用自动拷贝（构建后自动执行）";
            }
            else
            {
                status = "⚠️ 已禁用自动拷贝";
            }
        }

        EditorGUILayout.LabelField("提示: 启用后，每次 Addressables 构建完成会自动执行拷贝", EditorStyles.miniLabel);
    }

    private void RefreshPaths()
    {
        sourcePath = Path.Combine(PROJECT_ROOT, SOURCE_RELATIVE_PATH);
        targetPath = Path.Combine(PROJECT_ROOT, TARGET_RELATIVE_PATH);
        status = "🔄 路径已刷新";
        Z_Logger.Log("🔄 [AACopyTool] 路径已刷新");
    }

    private void ExecuteCopy()
    {
        try
        {
            // 检查源目录
            if (!Directory.Exists(sourcePath))
            {
                status = $"❌ 源目录不存在: {sourcePath}";
                Z_Logger.LogError($"❌ [AACopyTool] 源目录不存在: {sourcePath}");
                return;
            }

            // 获取所有子目录（排除 AddressablesLink）
            var directories = Directory.GetDirectories(sourcePath, "*", SearchOption.TopDirectoryOnly)
                .Where(d => !Path.GetFileName(d).Equals(EXCLUDED_FOLDER, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (directories.Count == 0)
            {
                status = $"⚠️ 源目录下没有可拷贝的内容（已排除 {EXCLUDED_FOLDER}）";
                Z_Logger.LogWarning($"⚠️ [AACopyTool] 没有可拷贝的内容");
                return;
            }

            // 创建目标目录
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
                Z_Logger.Log($"📁 [AACopyTool] 创建目标目录: {targetPath}");
            }

            // 统计信息
            int totalFilesCopied = 0;
            int totalDirsCopied = 0;
            long totalBytes = 0;

            foreach (string dir in directories)
            {
                string dirName = Path.GetFileName(dir);
                string targetDir = Path.Combine(targetPath, dirName);

                Z_Logger.Log($"📁 [AACopyTool] 拷贝目录: {dirName}");

                // 拷贝目录
                int files = CopyDirectory(dir, targetDir);
                totalFilesCopied += files;
                totalDirsCopied++;

                // 计算大小
                totalBytes += GetDirectorySize(dir);
            }

            AssetDatabase.Refresh();

            status = $"✅ 拷贝完成!\n" +
                     $"   拷贝目录: {totalDirsCopied} 个\n" +
                     $"   拷贝文件: {totalFilesCopied} 个\n" +
                     $"   总大小: {FormatBytes(totalBytes)}\n" +
                     $"   目标: {targetPath}";

            Z_Logger.Log($"✅ [AACopyTool] 拷贝完成! 目录: {totalDirsCopied}, 文件: {totalFilesCopied}, 大小: {FormatBytes(totalBytes)}");
        }
        catch (Exception e)
        {
            status = $"❌ 拷贝失败: {e.Message}";
            Z_Logger.LogError($"❌ [AACopyTool] 拷贝失败: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// 拷贝目录（递归，重复则覆盖）
    /// </summary>
    private int CopyDirectory(string sourceDir, string targetDir)
    {
        int fileCount = 0;

        try
        {
            // 创建目标目录
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // 拷贝文件
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);

                // 拷贝文件（覆盖）
                File.Copy(file, targetFile, true);
                fileCount++;
            }

            // 递归拷贝子目录
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string targetSubDir = Path.Combine(targetDir, subDirName);
                fileCount += CopyDirectory(subDir, targetSubDir);
            }
        }
        catch (Exception e)
        {
            Z_Logger.LogWarning($"⚠️ [AACopyTool] 拷贝目录失败 {sourceDir}: {e.Message}");
        }

        return fileCount;
    }

    /// <summary>
    /// 获取目录大小
    /// </summary>
    private long GetDirectorySize(string dir)
    {
        long size = 0;
        try
        {
            foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch { }
            }
        }
        catch { }
        return size;
    }

    /// <summary>
    /// 格式化字节大小
    /// </summary>
    private string FormatBytes(long bytes)
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

    private void ClearTargetDirectory()
    {
        if (!Directory.Exists(targetPath))
        {
            status = "⚠️ 目标目录不存在，无需清空";
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "确认清空",
            $"确定要清空目标目录吗？\n\n{targetPath}\n\n此操作不可恢复！",
            "确定清空",
            "取消"))
        {
            return;
        }

        try
        {
            // 获取所有子目录
            foreach (string dir in Directory.GetDirectories(targetPath))
            {
                string dirName = Path.GetFileName(dir);
                if (dirName.Equals(EXCLUDED_FOLDER, StringComparison.OrdinalIgnoreCase))
                {
                    continue; // 跳过排除的目录
                }
                Directory.Delete(dir, true);
                Z_Logger.Log($"🗑️ [AACopyTool] 删除目录: {dirName}");
            }

            // 删除根目录下的文件
            foreach (string file in Directory.GetFiles(targetPath))
            {
                File.Delete(file);
                Z_Logger.Log($"🗑️ [AACopyTool] 删除文件: {Path.GetFileName(file)}");
            }

            status = "✅ 目标目录已清空（保留了排除的文件夹）";
            Z_Logger.Log("✅ [AACopyTool] 目标目录已清空");
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            status = $"❌ 清空失败: {e.Message}";
            Z_Logger.LogError($"❌ [AACopyTool] 清空失败: {e.Message}");
        }
    }

    private void OpenTargetDirectory()
    {
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }
        EditorUtility.RevealInFinder(targetPath);
        status = $"📂 已打开目标目录: {targetPath}";
    }

    private void OpenSourceDirectory()
    {
        if (!Directory.Exists(sourcePath))
        {
            status = $"❌ 源目录不存在: {sourcePath}";
            return;
        }
        EditorUtility.RevealInFinder(sourcePath);
        status = $"📂 已打开源目录: {sourcePath}";
    }

    private void ShowStatistics()
    {
        if (!Directory.Exists(sourcePath))
        {
            status = $"❌ 源目录不存在: {sourcePath}";
            return;
        }

        try
        {
            int totalFiles = 0;
            long totalSize = 0;
            int excludedFiles = 0;
            long excludedSize = 0;

            foreach (string dir in Directory.GetDirectories(sourcePath, "*", SearchOption.TopDirectoryOnly))
            {
                string dirName = Path.GetFileName(dir);
                bool isExcluded = dirName.Equals(EXCLUDED_FOLDER, StringComparison.OrdinalIgnoreCase);

                int files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
                long size = GetDirectorySize(dir);

                if (isExcluded)
                {
                    excludedFiles += files;
                    excludedSize += size;
                }
                else
                {
                    totalFiles += files;
                    totalSize += size;
                }
            }

            status = $"📊 统计信息:\n" +
                     $"   可拷贝文件: {totalFiles} 个\n" +
                     $"   可拷贝大小: {FormatBytes(totalSize)}\n" +
                     $"   排除文件: {excludedFiles} 个 ({EXCLUDED_FOLDER})\n" +
                     $"   排除大小: {FormatBytes(excludedSize)}";

            Z_Logger.Log($"📊 [AACopyTool] 统计: 可拷贝 {totalFiles} 个文件, {FormatBytes(totalSize)}");
        }
        catch (Exception e)
        {
            status = $"❌ 统计失败: {e.Message}";
            Z_Logger.LogError($"❌ [AACopyTool] 统计失败: {e.Message}");
        }
    }

    /// <summary>
    /// 供外部调用的静态方法（可用于构建后自动执行）
    /// </summary>
    public static void ExecuteCopyStatic()
    {
        var tool = GetWindow<AACopyTool>("AACopyTool", false);
        tool.sourcePath = Path.Combine(PROJECT_ROOT, SOURCE_RELATIVE_PATH);
        tool.targetPath = Path.Combine(PROJECT_ROOT, TARGET_RELATIVE_PATH);
        tool.ExecuteCopy();
        tool.Close();
    }

    /// <summary>
    /// 检查是否应该自动拷贝
    /// </summary>
    public static bool ShouldAutoCopy()
    {
        return EditorPrefs.GetBool("AACopyTool_AutoCopyOnBuild", false);
    }
}

/// <summary>
/// 构建后自动执行回调
/// </summary>
[InitializeOnLoad]
public static class AACopyBuildHandler
{
    static AACopyBuildHandler()
    {
        // 注册构建完成回调
        BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildPlayer);
    }

    private static void OnBuildPlayer(BuildPlayerOptions options)
    {
        // 执行构建
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);

        // 构建完成后，检查是否需要自动拷贝
        if (AACopyTool.ShouldAutoCopy())
        {
            Z_Logger.Log("🔄 [AACopyTool] 检测到构建完成，执行自动拷贝...");
            AACopyTool.ExecuteCopyStatic();
        }
    }
}
#endif
