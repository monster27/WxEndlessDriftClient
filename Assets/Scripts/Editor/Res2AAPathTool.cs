#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

/// <summary>
/// 批量将 Resources/ 路径替换为 Addressables/，并标记文件夹内所有资源为 Addressable
/// </summary>
public class Res2AAPathTool : EditorWindow
{
    // ==================== 路径替换 ====================

    [MenuItem("Assets/路径替换 Resources→Addressables", false, 30)]
    public static void ReplacePath()
    {
        string folderPath = GetSelectedFolderPath();

        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog("提示", "请先选中一个文件夹！", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "确认替换",
            $"将扫描并替换文件夹中的所有 .cs 脚本：\n\n📁 {folderPath}\n\n" +
            "替换规则：\n" +
            "• \"Resources/\" → \"Addressables/\"\n" +
            "• \"Resources\\\" → \"Addressables\\\"\n\n" +
            "⚠️ 操作不可逆，建议先备份！",
            "确认",
            "取消"))
        {
            return;
        }

        ProcessFolder(folderPath);
    }

    private static void ProcessFolder(string folderPath)
    {
        folderPath = NormalizeFolderPath(folderPath);

        string fullPath = Path.Combine(Application.dataPath, folderPath.Replace("Assets/", ""));

        if (!Directory.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("错误", $"目录不存在：\n{fullPath}", "确定");
            return;
        }

        string[] csFiles = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);

        if (csFiles.Length == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到任何 .cs 文件！", "确定");
            return;
        }

        int modifiedCount = 0;
        int totalReplacements = 0;
        List<string> modifiedFiles = new List<string>();

        for (int i = 0; i < csFiles.Length; i++)
        {
            string filePath = csFiles[i];

            EditorUtility.DisplayProgressBar(
                "替换 Resources → Addressables",
                $"处理: {Path.GetFileName(filePath)} ({i + 1}/{csFiles.Length})",
                (float)i / csFiles.Length
            );

            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string original = content;

                if (!content.Contains("Resources/") && !content.Contains("Resources\\"))
                {
                    continue;
                }

                content = content.Replace("\"Resources/", "\"Addressables/");
                content = content.Replace("'Resources/", "'Addressables/");
                content = content.Replace("\"Resources\\", "\"Addressables\\");
                content = content.Replace("'Resources\\", "'Addressables\\");

                content = content.Replace(" Resources/", " Addressables/");
                content = content.Replace("(Resources/", "(Addressables/");
                content = content.Replace("[Resources/", "[Addressables/");
                content = content.Replace("{Resources/", "{Addressables/");
                content = content.Replace("=Resources/", "=Addressables/");
                content = content.Replace("+Resources/", "+Addressables/");

                int count = CountOccurrences(original, "Resources/") + CountOccurrences(original, "Resources\\");

                if (content != original)
                {
                    File.WriteAllText(filePath, content, Encoding.UTF8);
                    modifiedCount++;
                    totalReplacements += count;
                    modifiedFiles.Add($"{Path.GetFileName(filePath)} ({count} 处)");

                    Debug.Log($"[路径替换] ✅ {filePath.Replace(Application.dataPath, "Assets")} -> 替换了 {count} 处");
                }
                else
                {
                    string contentLower = content.ToLower();
                    if (contentLower.Contains("resources/"))
                    {
                        content = ReplaceCaseInsensitive(content, "Resources/", "Addressables/");
                        content = ReplaceCaseInsensitive(content, "Resources\\", "Addressables\\");

                        int count2 = CountOccurrences(original, "Resources/") + CountOccurrences(original, "Resources\\");
                        if (content != original)
                        {
                            File.WriteAllText(filePath, content, Encoding.UTF8);
                            modifiedCount++;
                            totalReplacements += count2;
                            modifiedFiles.Add($"{Path.GetFileName(filePath)} ({count2} 处, 忽略大小写)");
                            Debug.Log($"[路径替换] ✅ {filePath.Replace(Application.dataPath, "Assets")} -> 替换了 {count2} 处 (忽略大小写)");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[路径替换] ❌ 处理失败: {filePath}\n{ex.Message}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        Debug.Log($"[路径替换] ===== 完成 =====\n" +
                  $"  文件夹: {folderPath}\n" +
                  $"  扫描: {csFiles.Length} 个文件\n" +
                  $"  修改: {modifiedCount} 个文件\n" +
                  $"  替换: {totalReplacements} 处");

        string msg = $"✅ 替换完成！\n\n" +
                     $"📁 {folderPath}\n" +
                     $"📄 扫描: {csFiles.Length} 个文件\n" +
                     $"✏️ 修改: {modifiedCount} 个文件\n" +
                     $"🔄 替换: {totalReplacements} 处\n\n";

        if (modifiedFiles.Count > 0)
        {
            msg += "📋 修改的文件：\n";
            foreach (string f in modifiedFiles)
                msg += $"  • {f}\n";
        }

        EditorUtility.DisplayDialog("替换完成", msg, "确定");
    }

    private static string ReplaceCaseInsensitive(string text, string oldValue, string newValue)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldValue))
            return text;

        int startIndex = 0;
        while (true)
        {
            int index = text.IndexOf(oldValue, startIndex, System.StringComparison.OrdinalIgnoreCase);
            if (index == -1)
                break;

            text = text.Remove(index, oldValue.Length).Insert(index, newValue);
            startIndex = index + newValue.Length;
        }
        return text;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, System.StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    private static string NormalizeFolderPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        path = path.TrimStart('/', '\\');

        while (path.StartsWith("Assets/Assets/"))
        {
            path = path.Substring("Assets/".Length);
        }

        if (path.StartsWith("Assets"))
        {
            return "Assets/" + path.Substring("Assets".Length).TrimStart('/', '\\');
        }

        return "Assets/" + path.TrimStart('/', '\\');
    }

    private static string GetSelectedFolderPath()
    {
        var objs = Selection.GetFiltered<Object>(SelectionMode.Assets);
        if (objs == null || objs.Length == 0) return "";

        string path = AssetDatabase.GetAssetPath(objs[0]);
        if (!AssetDatabase.IsValidFolder(path))
        {
            path = Path.GetDirectoryName(path);
        }
        return NormalizeFolderPath(path);
    }

    [MenuItem("Assets/文本替换 Res→AA", true)]
    private static bool ValidateReplacePath()
    {
        return !string.IsNullOrEmpty(GetSelectedFolderPath());
    }

    // ==================== 标记为 Addressable ====================

    [MenuItem("Assets/标记文件夹为 Addressable", false, 31)]
    public static void MarkFolderAsAddressable()
    {
        string folderPath = GetSelectedFolderPath();

        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog("提示", "请先选中一个文件夹！", "确定");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "确认操作",
            $"将把文件夹中的所有资源标记为 Addressable：\n\n📁 {folderPath}\n\n" +
            "⚠️ 包括子文件夹中的所有资源都会被标记！",
            "确认",
            "取消"))
        {
            return;
        }

        ProcessMarkFolder(folderPath);
    }

    private static void ProcessMarkFolder(string folderPath)
    {
        folderPath = NormalizeFolderPath(folderPath);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("错误", "未找到 Addressable 设置！\n请先初始化 Addressables", "确定");
            return;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();

        string fullPath = Path.Combine(Application.dataPath, folderPath.Replace("Assets/", ""));

        if (!Directory.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("错误", $"目录不存在：\n{fullPath}", "确定");
            return;
        }

        string[] allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories);

        List<string> assetPaths = new List<string>();
        int skippedCount = 0;

        foreach (string file in allFiles)
        {
            string relativePath = file.Replace(Application.dataPath, "Assets").Replace('\\', '/');

            // 跳过 .meta 文件
            if (file.EndsWith(".meta"))
            {
                skippedCount++;
                continue;
            }

            // ⚠️ 只跳过脚本和程序集定义文件，不跳过 .json
            if (file.EndsWith(".cs"))
            {
                skippedCount++;
                continue;
            }
            if (file.EndsWith(".asmdef"))
            {
                skippedCount++;
                continue;
            }
            if (file.EndsWith(".asmref"))
            {
                skippedCount++;
                continue;
            }
            if (file.EndsWith(".DS_Store"))
            {
                skippedCount++;
                continue;
            }

            // ✅ 其他所有文件（包括 .json）都尝试标记
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(relativePath);
            if (obj != null)
            {
                assetPaths.Add(relativePath);
            }
            else
            {
                skippedCount++;
                Debug.LogWarning($"[Addressable标记] ⚠️ 无法加载: {relativePath}");
            }
        }

        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("提示",
                $"未找到可标记的资源文件！\n\n" +
                $"总文件数: {allFiles.Length}\n" +
                $"跳过的文件: {skippedCount} (包括 .meta、脚本等)",
                "确定");
            return;
        }

        int addedCount = 0;
        int existingCount = 0;

        AddressableAssetGroup group = settings.DefaultGroup;
        if (group == null)
        {
            group = settings.CreateGroup("Default Local Group", false, false, true, null);
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

        for (int i = 0; i < assetPaths.Count; i++)
        {
            string assetPath = assetPaths[i];
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            EditorUtility.DisplayProgressBar(
                "标记 Addressable",
                $"处理: {Path.GetFileName(assetPath)} ({i + 1}/{assetPaths.Count})",
                (float)i / assetPaths.Count
            );

            AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
            if (existingEntry != null)
            {
                existingCount++;
                Debug.Log($"[Addressable标记] 已存在: {assetPath}");
                continue;
            }

            Debug.Log($"[Addressable标记] ✅ 新增: {assetPath}");
            settings.CreateOrMoveEntry(guid, group, false, false);
            addedCount++;
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.ClearProgressBar();

        string msg = $"✅ 标记完成！\n\n" +
                     $"📁 {folderPath}\n" +
                     $"📄 新增标记: {addedCount} 个资源\n" +
                     $"📄 已存在: {existingCount} 个资源\n" +
                     $"⏭️ 跳过: {skippedCount} 个文件\n";

        Debug.Log($"[Addressable标记] ===== 完成 =====\n" +
                  $"  文件夹: {folderPath}\n" +
                  $"  新增: {addedCount} 个\n" +
                  $"  已存在: {existingCount} 个\n" +
                  $"  跳过: {skippedCount} 个");

        EditorUtility.DisplayDialog("标记完成", msg, "确定");
    }

    [MenuItem("Assets/标记文件夹为 Addressable", true)]
    private static bool ValidateMarkFolder()
    {
        return !string.IsNullOrEmpty(GetSelectedFolderPath());
    }

    // ==================== 窗口版 ====================

    [MenuItem("Tools/通用/文本替换 Res→AA")]
    public static void ShowWindow()
    {
        GetWindow<Res2AAPathTool>("路径替换");
    }

    private string targetFolder = "";
    private Vector2 scrollPos;
    private string previewContent = "";
    private bool showPreview = false;

    private void OnGUI()
    {
        GUILayout.Label("Resources → Addressables 路径替换", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "将脚本中 \"Resources/\" 路径替换为 \"Addressables/\"",
            MessageType.Info
        );

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        targetFolder = EditorGUILayout.TextField("文件夹:", targetFolder);
        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("选择文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                targetFolder = NormalizeFolderPath(path.Replace(Application.dataPath, "Assets"));
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(targetFolder))
        {
            string normalized = NormalizeFolderPath(targetFolder);
            string fullPath = Path.Combine(Application.dataPath, normalized.Replace("Assets/", ""));
            if (Directory.Exists(fullPath))
            {
                int count = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories).Length;
                EditorGUILayout.LabelField($"📄 找到 {count} 个 .cs 文件", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox($"目录不存在：{normalized}", MessageType.Warning);
            }
        }

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("预览", GUILayout.Height(25)))
        {
            ShowPreview();
        }

        if (GUILayout.Button("清空预览", GUILayout.Height(25)))
        {
            previewContent = "";
            showPreview = false;
        }
        EditorGUILayout.EndHorizontal();

        if (showPreview && !string.IsNullOrEmpty(previewContent))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("=== 预览结果 ===", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(250));
            EditorGUILayout.TextArea(previewContent, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("复制预览", GUILayout.Height(25)))
            {
                GUIUtility.systemCopyBuffer = previewContent;
                EditorUtility.DisplayDialog("提示", "已复制到剪贴板", "确定");
            }
        }

        EditorGUILayout.Space();

        // 两个操作按钮
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("替换路径", GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(targetFolder))
            {
                EditorUtility.DisplayDialog("提示", "请选择文件夹！", "确定");
                return;
            }
            string normalized = NormalizeFolderPath(targetFolder);
            string fullPath = Path.Combine(Application.dataPath, normalized.Replace("Assets/", ""));
            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("错误", $"目录不存在：\n{normalized}", "确定");
                return;
            }
            ProcessFolder(normalized);
        }
        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("标记 Addressable", GUILayout.Height(30)))
        {
            if (string.IsNullOrEmpty(targetFolder))
            {
                EditorUtility.DisplayDialog("提示", "请选择文件夹！", "确定");
                return;
            }
            string normalized = NormalizeFolderPath(targetFolder);
            string fullPath = Path.Combine(Application.dataPath, normalized.Replace("Assets/", ""));
            if (!Directory.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("错误", $"目录不存在：\n{normalized}", "确定");
                return;
            }
            ProcessMarkFolder(normalized);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "📌 右键菜单说明：\n" +
            "• 路径替换 Resources→Addressables：替换脚本中的路径字符串\n" +
            "• 标记文件夹为 Addressable：将文件夹内所有资源标记为 Addressable（包含 .json）",
            MessageType.Info
        );
    }

    private void ShowPreview()
    {
        if (string.IsNullOrEmpty(targetFolder))
        {
            EditorUtility.DisplayDialog("提示", "请选择文件夹！", "确定");
            return;
        }

        string normalized = NormalizeFolderPath(targetFolder);
        string fullPath = Path.Combine(Application.dataPath, normalized.Replace("Assets/", ""));

        if (!Directory.Exists(fullPath))
        {
            EditorUtility.DisplayDialog("错误", $"目录不存在：\n{normalized}", "确定");
            return;
        }

        string[] files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== 预览: {normalized} ===\n");
        sb.AppendLine($"扫描到 {files.Length} 个文件\n");

        int totalReplacements = 0;
        int filesWithChanges = 0;

        foreach (string file in files)
        {
            string content = File.ReadAllText(file, Encoding.UTF8);
            string relPath = file.Replace(Application.dataPath, "Assets");

            if (content.Contains("Resources/") || content.Contains("Resources\\") ||
                content.ToLower().Contains("resources/") || content.ToLower().Contains("resources\\"))
            {
                filesWithChanges++;
                sb.AppendLine($"📄 {relPath}");
                string[] lines = content.Split('\n');
                int lineCount = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("Resources/") || lines[i].Contains("Resources\\") ||
                        lines[i].ToLower().Contains("resources/") || lines[i].ToLower().Contains("resources\\"))
                    {
                        string line = lines[i].Trim();
                        if (line.Length > 100) line = line.Substring(0, 100) + "...";
                        string replaced = line.Replace("Resources/", "Addressables/")
                                               .Replace("Resources\\", "Addressables\\");
                        sb.AppendLine($"  第 {i + 1} 行:");
                        sb.AppendLine($"    替换前: {line}");
                        sb.AppendLine($"    替换后: {replaced}");
                        lineCount++;
                        totalReplacements++;
                    }
                }
                sb.AppendLine($"  (共 {lineCount} 处替换)");
                sb.AppendLine();
            }
        }

        if (filesWithChanges == 0)
        {
            sb.AppendLine("✅ 未找到任何需要替换的内容");
        }

        previewContent = sb.ToString();
        showPreview = true;

        EditorUtility.DisplayDialog("预览完成", $"找到 {filesWithChanges} 个文件需要修改，共 {totalReplacements} 处替换", "确定");
    }
}
#endif
