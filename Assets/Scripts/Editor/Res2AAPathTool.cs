#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// 批量将 Resources/ 路径替换为 Addressables/
/// </summary>
public class Res2AAPathTool : EditorWindow
{
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
        string fullPath = Path.Combine(Application.dataPath, folderPath.Replace("Assets/", ""));
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

                // 替换 "Resources/" 为 "Addressables/"
                content = content.Replace("\"Resources/", "\"Addressables/");
                content = content.Replace("'Resources/", "'Addressables/");
                content = content.Replace("\"Resources\\", "\"Addressables\\");
                content = content.Replace("'Resources\\", "'Addressables\\");

                // 统计替换数量
                int count = 0;
                int index = 0;
                while ((index = original.IndexOf("Resources/", index)) != -1)
                {
                    count++;
                    index += "Resources/".Length;
                }

                if (content != original)
                {
                    File.WriteAllText(filePath, content, Encoding.UTF8);
                    modifiedCount++;
                    totalReplacements += count;
                    modifiedFiles.Add($"{Path.GetFileName(filePath)} ({count} 处)");

                    Debug.Log($"[路径替换] ✅ {filePath.Replace(Application.dataPath, "Assets")} -> 替换了 {count} 处");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[路径替换] ❌ 处理失败: {filePath}\n{ex.Message}");
            }
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        // 输出汇总日志
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

    private static string GetSelectedFolderPath()
    {
        var objs = Selection.GetFiltered<Object>(SelectionMode.Assets);
        if (objs == null || objs.Length == 0) return "";

        string path = AssetDatabase.GetAssetPath(objs[0]);
        if (!AssetDatabase.IsValidFolder(path))
        {
            path = Path.GetDirectoryName(path);
        }
        return path;
    }

    [MenuItem("Assets/路径替换 Resources→Addressables", true)]
    private static bool ValidateReplacePath()
    {
        return !string.IsNullOrEmpty(GetSelectedFolderPath());
    }

    // ===== 窗口版 =====
    [MenuItem("Tools/路径替换 Resources→Addressables")]
    public static void ShowWindow()
    {
        GetWindow<Res2AAPathTool>("路径替换");
    }

    private string targetFolder = "";
    private Vector2 scrollPos;

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
                targetFolder = path.Replace(Application.dataPath, "Assets");
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(targetFolder))
        {
            string fullPath = Path.Combine(Application.dataPath, targetFolder.Replace("Assets/", ""));
            if (Directory.Exists(fullPath))
            {
                int count = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories).Length;
                EditorGUILayout.LabelField($"📄 找到 {count} 个 .cs 文件", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("预览", GUILayout.Height(25)))
        {
            ShowPreview();
        }

        EditorGUILayout.Space();

        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);
        if (GUILayout.Button("执行替换", GUILayout.Height(35)))
        {
            if (string.IsNullOrEmpty(targetFolder))
            {
                EditorUtility.DisplayDialog("提示", "请选择文件夹！", "确定");
                return;
            }
            ProcessFolder(targetFolder);
        }
        GUI.backgroundColor = Color.white;
    }

    private void ShowPreview()
    {
        if (string.IsNullOrEmpty(targetFolder))
        {
            EditorUtility.DisplayDialog("提示", "请选择文件夹！", "确定");
            return;
        }

        string fullPath = Path.Combine(Application.dataPath, targetFolder.Replace("Assets/", ""));
        string[] files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== 预览: {targetFolder} ===\n");

        foreach (string file in files)
        {
            string content = File.ReadAllText(file, Encoding.UTF8);
            string relPath = file.Replace(Application.dataPath, "Assets");

            if (content.Contains("Resources/"))
            {
                sb.AppendLine($"📄 {relPath}");
                string[] lines = content.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("Resources/"))
                    {
                        string line = lines[i].Trim();
                        if (line.Length > 80) line = line.Substring(0, 80) + "...";
                        sb.AppendLine($"  第 {i + 1} 行: {line}");
                        sb.AppendLine($"      → {line.Replace("Resources/", "Addressables/")}");
                    }
                }
                sb.AppendLine();
            }
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
        EditorGUILayout.TextArea(sb.ToString(), GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("复制预览"))
        {
            GUIUtility.systemCopyBuffer = sb.ToString();
            EditorUtility.DisplayDialog("提示", "已复制到剪贴板", "确定");
        }
    }
}
#endif
