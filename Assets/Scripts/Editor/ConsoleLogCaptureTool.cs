using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

public class ConsoleLogCaptureTool : EditorWindow
{
    private List<string> keywords = new List<string>();
    private string newKeyword = "";
    private Vector2 scrollPosition;
    private List<LogEntry> filteredLogs = new List<LogEntry>();
    private bool isCapturing = false;
    private int maxLogCount = 1000;

    [MenuItem("Tools/Console Log Capture Tool")]
    public static void ShowWindow()
    {
        GetWindow<ConsoleLogCaptureTool>("日志捕获工具");
    }

    private void OnEnable()
    {
        // 添加默认关键字示例
        if (keywords.Count == 0)
        {
            keywords.Add("[BagDetail]");
            keywords.Add("Error");
        }
        LoadKeywordsFromPrefs();
    }

    private void OnGUI()
    {
        GUILayout.Label("日志捕获与过滤工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 关键字管理区域
        EditorGUILayout.LabelField("关键字管理", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        newKeyword = EditorGUILayout.TextField("添加关键字:", newKeyword);
        if (GUILayout.Button("添加", GUILayout.Width(60)))
        {
            if (!string.IsNullOrEmpty(newKeyword) && !keywords.Contains(newKeyword))
            {
                keywords.Add(newKeyword);
                SaveKeywordsToPrefs();
                newKeyword = "";
            }
        }
        EditorGUILayout.EndHorizontal();

        // 显示关键字列表
        EditorGUILayout.BeginVertical("box");
        for (int i = 0; i < keywords.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"● {keywords[i]}", GUILayout.Width(200));
            if (GUILayout.Button("移除", GUILayout.Width(50)))
            {
                keywords.RemoveAt(i);
                SaveKeywordsToPrefs();
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();

        // 控制按钮区域
        EditorGUILayout.BeginHorizontal();
        if (!isCapturing)
        {
            if (GUILayout.Button("开始捕获", GUILayout.Height(30)))
            {
                StartCapture();
            }
        }
        else
        {
            if (GUILayout.Button("停止捕获", GUILayout.Height(30)))
            {
                StopCapture();
            }
        }

        if (GUILayout.Button("清空日志", GUILayout.Height(30)))
        {
            ClearLogs();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 设置区域
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("最大显示条数:", GUILayout.Width(100));
        maxLogCount = EditorGUILayout.IntField(maxLogCount, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 复制功能 - 多种格式
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("复制简化日志", GUILayout.Height(25)))
        {
            CopyLogsToClipboard(false);
        }
        if (GUILayout.Button("复制完整日志（含路径）", GUILayout.Height(25)))
        {
            CopyLogsToClipboard(true);
        }
        EditorGUILayout.EndHorizontal();

        // 一键复制所有日志（带时间戳）
        if (GUILayout.Button("复制详细日志（含时间戳）", GUILayout.Height(25)))
        {
            CopyLogsWithTimestamp();
        }

        EditorGUILayout.Space();

        // 显示过滤后的日志
        EditorGUILayout.LabelField($"过滤后的日志 (共 {filteredLogs.Count} 条)", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));

        foreach (var log in filteredLogs)
        {
            EditorGUILayout.BeginVertical("box");

            // 显示原始日志内容（简化版）
            EditorGUILayout.LabelField($"日志: {log.SimpleMessage}", EditorStyles.wordWrappedLabel);

            // 显示来源位置（提取的路径）
            if (!string.IsNullOrEmpty(log.FilePath))
            {
                EditorGUILayout.LabelField($"位置: {log.FilePath}", EditorStyles.miniLabel);
            }

            // 显示完整调用栈（可折叠）
            if (log.HasStackTrace && GUILayout.Button("显示调用栈", GUILayout.Width(100)))
            {
                log.ShowStackTrace = !log.ShowStackTrace;
            }

            if (log.ShowStackTrace)
            {
                EditorGUILayout.TextArea(log.StackTrace, GUILayout.Height(100));
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndScrollView();
    }

    private void StartCapture()
    {
        if (keywords.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请至少添加一个关键字", "确定");
            return;
        }

        isCapturing = true;
        filteredLogs.Clear();
        Application.logMessageReceived += OnLogMessageReceived;
        Debug.Log("日志捕获已开始...");
    }

    private void StopCapture()
    {
        isCapturing = false;
        Application.logMessageReceived -= OnLogMessageReceived;
        Debug.Log("日志捕获已停止");
    }

    private void ClearLogs()
    {
        filteredLogs.Clear();
        Repaint();
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (!isCapturing) return;

        // 检查是否包含关键字
        bool containsKeyword = false;
        foreach (var keyword in keywords)
        {
            if (condition.Contains(keyword) || stackTrace.Contains(keyword))
            {
                containsKeyword = true;
                break;
            }
        }

        if (!containsKeyword) return;

        // 创建日志条目
        LogEntry entry = new LogEntry();
        entry.RawMessage = condition;
        entry.StackTrace = stackTrace;
        entry.LogType = type;
        entry.TimeStamp = DateTime.Now;
        entry.HasStackTrace = !string.IsNullOrEmpty(stackTrace);

        // 提取简化的日志信息
        entry.SimpleMessage = ExtractSimpleMessage(condition);

        // 提取文件路径和行号
        entry.FilePath = ExtractFilePath(stackTrace);

        // 添加到列表，控制最大数量
        filteredLogs.Insert(0, entry);
        if (filteredLogs.Count > maxLogCount)
        {
            filteredLogs.RemoveAt(filteredLogs.Count - 1);
        }

        // 刷新UI
        Repaint();
    }

    private string ExtractSimpleMessage(string condition)
    {
        // 尝试提取第一个冒号后的内容
        int colonIndex = condition.IndexOf(':');
        if (colonIndex > 0 && colonIndex < condition.Length - 1)
        {
            return condition.Substring(colonIndex + 1).Trim();
        }
        return condition;
    }

    private string ExtractFilePath(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace)) return "";

        // 使用正则匹配 (at Assets/...:行号)
        Regex regex = new Regex(@"\(at\s+([^:]+):(\d+)\)");
        Match match = regex.Match(stackTrace);

        if (match.Success)
        {
            string filePath = match.Groups[1].Value;
            string lineNumber = match.Groups[2].Value;

            // 获取第一个匹配的路径（最近的调用）
            return $"{filePath}:{lineNumber}";
        }

        // 如果没找到，尝试查找任何包含 "Assets/" 的路径
        regex = new Regex(@"Assets/[^\s]+\.cs:\d+");
        match = regex.Match(stackTrace);
        if (match.Success)
        {
            return match.Value;
        }

        return "";
    }

    // 复制简化日志到剪贴板（只复制日志消息）
    private void CopyLogsToClipboard(bool includePath)
    {
        if (filteredLogs.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有日志可复制", "确定");
            return;
        }

        string result = "";
        if (includePath)
        {
            // 包含路径的格式
            result = $"=== 过滤日志 (包含路径) ===\n";
            result += $"关键字: {string.Join(", ", keywords)}\n";
            result += $"总计: {filteredLogs.Count} 条\n";
            result += new string('=', 50) + "\n\n";

            for (int i = 0; i < filteredLogs.Count; i++)
            {
                var log = filteredLogs[i];
                result += $"{i + 1}. {log.SimpleMessage}";
                if (!string.IsNullOrEmpty(log.FilePath))
                {
                    result += $" [{log.FilePath}]";
                }
                result += "\n";
            }
        }
        else
        {
            // 只复制简化日志
            result = $"=== 简化日志 ===\n";
            result += $"关键字: {string.Join(", ", keywords)}\n";
            result += $"总计: {filteredLogs.Count} 条\n";
            result += new string('=', 50) + "\n\n";

            for (int i = 0; i < filteredLogs.Count; i++)
            {
                result += $"{i + 1}. {filteredLogs[i].SimpleMessage}\n";
            }
        }

        // 复制到剪贴板
        EditorGUIUtility.systemCopyBuffer = result;
        EditorUtility.DisplayDialog("成功", $"已复制 {filteredLogs.Count} 条日志到剪贴板", "确定");
    }

    // 复制详细日志（含时间戳）
    private void CopyLogsWithTimestamp()
    {
        if (filteredLogs.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有日志可复制", "确定");
            return;
        }

        string result = $"=== 详细日志 ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===\n";
        result += $"关键字: {string.Join(", ", keywords)}\n";
        result += $"总计: {filteredLogs.Count} 条\n";
        result += new string('=', 50) + "\n\n";

        for (int i = 0; i < filteredLogs.Count; i++)
        {
            var log = filteredLogs[i];
            result += $"--- #{i + 1} ---\n";
            result += $"时间: {log.TimeStamp:HH:mm:ss}\n";
            result += $"类型: {log.LogType}\n";
            result += $"消息: {log.SimpleMessage}\n";
            if (!string.IsNullOrEmpty(log.FilePath))
            {
                result += $"位置: {log.FilePath}\n";
            }
            result += "\n";
        }

        // 复制到剪贴板
        EditorGUIUtility.systemCopyBuffer = result;
        EditorUtility.DisplayDialog("成功", $"已复制 {filteredLogs.Count} 条详细日志到剪贴板", "确定");
    }

    private void SaveKeywordsToPrefs()
    {
        string keywordsStr = string.Join("|", keywords);
        EditorPrefs.SetString("ConsoleLogTool_Keywords", keywordsStr);
    }

    private void LoadKeywordsFromPrefs()
    {
        string keywordsStr = EditorPrefs.GetString("ConsoleLogTool_Keywords", "");
        if (!string.IsNullOrEmpty(keywordsStr))
        {
            keywords = new List<string>(keywordsStr.Split('|'));
        }
    }

    private class LogEntry
    {
        public string RawMessage { get; set; }
        public string SimpleMessage { get; set; }
        public string FilePath { get; set; }
        public string StackTrace { get; set; }
        public LogType LogType { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool HasStackTrace { get; set; }
        public bool ShowStackTrace { get; set; }
    }
}
