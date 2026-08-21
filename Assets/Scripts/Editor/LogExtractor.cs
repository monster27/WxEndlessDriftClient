#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// 日志提取工具 - 严格筛选器：只保留包含标签的行
/// </summary>
public class LogExtractor : EditorWindow
{
    [System.Serializable]
    private class ExtractorSaveData
    {
        public List<string> tags = new List<string>();
        public string lastFilePath = "";
    }

    private ExtractorSaveData saveData = new ExtractorSaveData();
    private string filePath = "";
    private Vector2 scrollPos;
    private string previewContent = "";
    private int logCount = 0;
    private int totalLines = 0;
    private int removedLines = 0;
    private string newTagInput = "";
    private string fileContent = "";
    private const string DATA_PATH = "Library/LogExtractorData.json";

    [MenuItem("Tools/日志提取器")]
    public static void ShowWindow() => GetWindow<LogExtractor>("日志提取器");

    private void OnEnable()
    {
        LoadData();
        // 确保默认标签存在
        if (saveData.tags.Count == 0 || !saveData.tags.Contains(Z_Logger.UNITY_TAG))
        {
            saveData.tags.Insert(0, Z_Logger.UNITY_TAG);
            SaveData();
        }
    }

    private void OnDisable() => SaveData();

    private void LoadData()
    {
        string path = Path.Combine(Application.dataPath, "..", DATA_PATH);
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var loaded = JsonUtility.FromJson<ExtractorSaveData>(json);
                if (loaded != null)
                {
                    saveData = loaded;
                    // 确保 tags 不为空
                    if (saveData.tags == null) saveData.tags = new List<string>();
                    filePath = saveData.lastFilePath;
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath)) LoadFileContent();
                }
            }
            catch
            {
                saveData = new ExtractorSaveData();
                saveData.tags.Add(Z_Logger.UNITY_TAG);
            }
        }
        else
        {
            saveData.tags.Add(Z_Logger.UNITY_TAG);
        }
    }

    private void SaveData()
    {
        saveData.lastFilePath = filePath;
        string json = JsonUtility.ToJson(saveData, true);
        string path = Path.Combine(Application.dataPath, "..", DATA_PATH);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private void OnGUI()
    {
        // 文件选择
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("文件:", GUILayout.Width(35));
        filePath = EditorGUILayout.TextField(filePath);
        if (GUILayout.Button("浏览", GUILayout.Width(50)))
        {
            string p = EditorUtility.OpenFilePanel("选择日志文件", "", "txt,json,log,csv");
            if (!string.IsNullOrEmpty(p))
            {
                filePath = p;
                LoadFileContent();
                SaveData();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            EditorGUILayout.HelpBox(
                $"📄 文件: {Path.GetFileName(filePath)} | " +
                $"📊 总行数: {totalLines} | " +
                $"✅ 保留: {logCount} 行 | " +
                $"🗑️ 删除: {removedLines} 行",
                MessageType.Info
            );
        }

        GUILayout.Space(5);

        // 标签管理
        EditorGUILayout.LabelField("🏷️ 标签列表 (匹配任一标签即保留):", EditorStyles.boldLabel);

        // 添加新标签
        EditorGUILayout.BeginHorizontal();
        newTagInput = EditorGUILayout.TextField(newTagInput);
        if (GUILayout.Button("添加", GUILayout.Width(50)))
        {
            string tag = newTagInput.Trim();
            if (!string.IsNullOrEmpty(tag) && !saveData.tags.Contains(tag))
            {
                saveData.tags.Add(tag);
                newTagInput = "";
                SaveData();
                RefreshPreview();
            }
        }
        EditorGUILayout.EndHorizontal();

        // 标签列表
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(150));
        for (int i = 0; i < saveData.tags.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            // 默认标签显示锁图标，不可编辑
            bool isDefault = saveData.tags[i] == Z_Logger.UNITY_TAG;

            GUI.enabled = !isDefault;
            saveData.tags[i] = EditorGUILayout.TextField(saveData.tags[i]);
            GUI.enabled = true;

            if (isDefault)
            {
                GUI.color = Color.gray;
                GUILayout.Label("🔒 默认", GUILayout.Width(40));
                GUI.color = Color.white;
            }

            if (!isDefault && GUILayout.Button("✕", GUILayout.Width(25)))
            {
                saveData.tags.RemoveAt(i);
                SaveData();
                RefreshPreview();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(5);

        // 操作按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔍 预览", GUILayout.Height(25))) RefreshPreview();
        if (GUILayout.Button("💾 提取保存", GUILayout.Height(25))) ExtractAndSave();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // 预览
        if (!string.IsNullOrEmpty(previewContent))
        {
            GUILayout.Label($"📄 预览 (共 {logCount} 条):", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            EditorGUILayout.TextArea(previewContent, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    private void LoadFileContent()
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            totalLines = 0;
            logCount = 0;
            removedLines = 0;
            return;
        }
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            fileContent = DecodeText(bytes);
            RefreshPreview();
        }
        catch (System.Exception ex)
        {
            Z_Logger.LogWarning($"读取文件失败: {ex.Message}");
            totalLines = 0;
            logCount = 0;
            removedLines = 0;
        }
    }

    /// <summary>
    /// 智能解码文本，自动处理 BOM 和各种编码
    /// </summary>
    private string DecodeText(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return "";

        // 检测 UTF-8 BOM (EF BB BF)
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        // 检测 UTF-16 BE BOM (FE FF)
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // 检测 UTF-16 LE BOM (FF FE)
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        // 尝试 UTF-8（无 BOM）
        try
        {
            string utf8Result = Encoding.UTF8.GetString(bytes);
            if (!utf8Result.Contains("\uFFFD"))
            {
                return utf8Result;
            }
        }
        catch { }

        // 尝试 GB2312（中文编码）
        try
        {
            Encoding gb2312 = Encoding.GetEncoding("GB2312");
            return gb2312.GetString(bytes);
        }
        catch { }

        // 尝试 GBK
        try
        {
            Encoding gbk = Encoding.GetEncoding("GBK");
            return gbk.GetString(bytes);
        }
        catch { }

        // 尝试系统默认编码
        try
        {
            return Encoding.Default.GetString(bytes);
        }
        catch { }

        // 最后降级到 UTF-8
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// 检查行是否包含任一标签
    /// </summary>
    private bool HasAnyTag(string line, List<string> tags)
    {
        if (string.IsNullOrEmpty(line)) return false;
        if (tags == null || tags.Count == 0) return false;

        foreach (var tag in tags)
        {
            if (string.IsNullOrEmpty(tag)) continue;
            if (line.Contains(tag))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 筛选日志：只保留包含标签的行
    /// </summary>
    private List<string> FilterLogs(string content, List<string> tags)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(content) || tags == null || tags.Count == 0) return result;

        // 过滤掉空标签
        var validTags = tags.Where(t => !string.IsNullOrEmpty(t)).ToList();
        if (validTags.Count == 0) return result;

        var lines = content.Split(new[] { '\n' }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            string trimmedLine = line.TrimEnd('\r');
            // 严格检查：必须包含至少一个标签才保留
            if (HasAnyTag(trimmedLine, validTags))
            {
                result.Add(trimmedLine);
            }
        }

        return result;
    }

    private void RefreshPreview()
    {
        if (string.IsNullOrEmpty(fileContent))
        {
            previewContent = "请选择文件";
            logCount = 0;
            totalLines = 0;
            removedLines = 0;
            return;
        }

        var validTags = saveData.tags.Where(t => !string.IsNullOrEmpty(t)).ToList();
        if (validTags.Count == 0)
        {
            previewContent = "⚠️ 没有有效的标签，请添加标签";
            logCount = 0;
            totalLines = 0;
            removedLines = 0;
            return;
        }

        // 统计总行数
        var allLines = fileContent.Split(new[] { '\n' }, StringSplitOptions.None);
        totalLines = allLines.Length;

        // 筛选：只保留包含标签的行
        var matched = FilterLogs(fileContent, validTags);
        logCount = matched.Count;
        removedLines = totalLines - logCount;

        if (logCount == 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"❌ 未找到匹配的日志");
            sb.AppendLine($"标签: {string.Join(", ", validTags)}");
            sb.AppendLine($"总行数: {totalLines}");
            sb.AppendLine($"删除行数: {removedLines}");
            sb.AppendLine();
            sb.AppendLine("前5行不匹配的内容（用于调试）:");
            int debugCount = 0;
            foreach (var line in allLines)
            {
                string trimmed = line.TrimEnd('\r');
                if (!string.IsNullOrEmpty(trimmed) && !HasAnyTag(trimmed, validTags))
                {
                    if (debugCount < 5)
                    {
                        string display = trimmed.Length > 60 ? trimmed.Substring(0, 60) + "..." : trimmed;
                        sb.AppendLine($"  [{debugCount + 1}] {display}");
                        debugCount++;
                    }
                    else break;
                }
            }
            previewContent = sb.ToString();
            return;
        }

        // 显示前100条
        int show = Mathf.Min(100, matched.Count);
        var result = new StringBuilder();
        result.AppendLine($"=== 筛选结果 ===");
        result.AppendLine($"标签: {string.Join(", ", validTags)}");
        result.AppendLine($"总行数: {totalLines} | 保留: {logCount} 行 | 删除: {removedLines} 行");
        result.AppendLine(new string('=', 50));
        for (int i = 0; i < show; i++)
        {
            string display = matched[i];
            // 如果行太长，截断显示
            if (display.Length > 200)
            {
                display = display.Substring(0, 200) + "...";
            }
            result.AppendLine($"{i + 1,5}. {display}");
        }
        if (logCount > 100)
        {
            result.AppendLine($"\n... 还有 {logCount - 100} 条未显示");
        }
        previewContent = result.ToString();
    }

    private void ExtractAndSave()
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            EditorUtility.DisplayDialog("错误", "请选择有效文件", "确定");
            return;
        }

        // 重新读取文件
        byte[] bytes = File.ReadAllBytes(filePath);
        string content = DecodeText(bytes);

        var validTags = saveData.tags.Where(t => !string.IsNullOrEmpty(t)).ToList();
        if (validTags.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有有效的标签，请添加标签", "确定");
            return;
        }

        // 筛选：只保留包含标签的行
        var matched = FilterLogs(content, validTags);

        if (matched.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "未找到匹配的日志", "确定");
            return;
        }

        // 统计总行数
        var allLines = content.Split(new[] { '\n' }, StringSplitOptions.None);
        int total = allLines.Length;
        int removed = total - matched.Count;

        string dir = Path.GetDirectoryName(filePath);
        string name = Path.GetFileNameWithoutExtension(filePath);
        string tagSuffix = string.Join("_", validTags.Select(t => t.Replace("[", "").Replace("]", "").Replace(" ", "")));
        string output = Path.Combine(dir, $"{name}_{tagSuffix}_filtered.txt");

        // 写入筛选后的内容
        var sb = new StringBuilder();
        sb.AppendLine($"=== 从 {Path.GetFileName(filePath)} 筛选 ===");
        sb.AppendLine($"标签: {string.Join(", ", validTags)}");
        sb.AppendLine($"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"总行数: {total} | 保留: {matched.Count} 行 | 删除: {removed} 行");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        for (int i = 0; i < matched.Count; i++)
        {
            sb.AppendLine(matched[i]);
        }

        // 写入 UTF-8 with BOM
        byte[] outputBytes = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
        byte[] finalBytes = new byte[bom.Length + outputBytes.Length];
        System.Array.Copy(bom, 0, finalBytes, 0, bom.Length);
        System.Array.Copy(outputBytes, 0, finalBytes, bom.Length, outputBytes.Length);
        File.WriteAllBytes(output, finalBytes);

        EditorUtility.DisplayDialog(
            "完成",
            $"✅ 已提取 {matched.Count} 条日志\n" +
            $"🗑️ 删除 {removed} 条不匹配的行\n" +
            $"📁 {output}",
            "确定"
        );
        EditorUtility.RevealInFinder(output);
    }

    private void OnDragUpdated()
    {
        if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0 && File.Exists(DragAndDrop.paths[0]))
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
    }

    private void OnDragPerform()
    {
        if (DragAndDrop.paths != null && DragAndDrop.paths.Length > 0 && File.Exists(DragAndDrop.paths[0]))
        {
            filePath = DragAndDrop.paths[0];
            LoadFileContent();
            SaveData();
            DragAndDrop.AcceptDrag();
        }
    }
}
#endif
