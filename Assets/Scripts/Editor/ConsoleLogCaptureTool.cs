using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using System.Linq;

public class ConsoleLogCaptureTool : EditorWindow
{
    // 关键字组数据
    [Serializable]
    public class KeywordGroup
    {
        public string groupName;
        public List<string> keywords;
        public bool isSelected;

        public KeywordGroup(string name)
        {
            groupName = name;
            keywords = new List<string>();
            isSelected = false;
        }

        public KeywordGroup(string name, List<string> initialKeywords)
        {
            groupName = name;
            keywords = new List<string>(initialKeywords);
            isSelected = false;
        }
    }

    private List<KeywordGroup> keywordGroups = new List<KeywordGroup>();
    private int selectedGroupIndex = -1;
    private string newGroupName = "";
    private string newKeyword = "";
    private Vector2 scrollPosition;
    private List<LogEntry> filteredLogs = new List<LogEntry>();
    private bool isCapturing = false;
    private bool autoCaptureOnPlay = true;
    private int maxLogCount = 99999;

    // 重命名相关
    private bool isRenaming = false;
    private string renameTempName = "";

    // 当前激活的关键字列表（从选中的组获取）
    private List<string> ActiveKeywords
    {
        get
        {
            if (selectedGroupIndex >= 0 && selectedGroupIndex < keywordGroups.Count)
            {
                return keywordGroups[selectedGroupIndex].keywords;
            }
            return new List<string>();
        }
    }

    [MenuItem("Tools/日志捕获工具")]
    public static void ShowWindow()
    {
        GetWindow<ConsoleLogCaptureTool>("日志捕获工具");
    }

    private void OnEnable()
    {
        LoadAllData();

        // 如果没有分组，创建默认分组
        if (keywordGroups.Count == 0)
        {
            var defaultGroup = new KeywordGroup("默认", new List<string>
            {
                "[BagDetail]",
                "[NetServerManager]",
                "[BagView]",
                "[SkinManager]"
            });
            defaultGroup.isSelected = true;
            keywordGroups.Add(defaultGroup);
            selectedGroupIndex = 0;
            SaveAllData();
        }
        else
        {
            // 恢复选中的分组
            for (int i = 0; i < keywordGroups.Count; i++)
            {
                if (keywordGroups[i].isSelected)
                {
                    selectedGroupIndex = i;
                    break;
                }
            }
            if (selectedGroupIndex == -1 && keywordGroups.Count > 0)
            {
                keywordGroups[0].isSelected = true;
                selectedGroupIndex = 0;
            }
        }

        LoadSettingsFromPrefs();

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        if (EditorApplication.isPlaying && autoCaptureOnPlay && !isCapturing)
        {
            StartCapture();
            Debug.Log("[日志捕获工具] 编辑器启动时自动开始捕获（运行模式）");
        }
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            if (autoCaptureOnPlay && !isCapturing)
            {
                StartCapture();
                Debug.Log("[日志捕获工具] 自动开始捕获（进入运行模式）");
            }
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (isCapturing)
            {
                StopCapture();
                Debug.Log("[日志捕获工具] 自动停止捕获（退出运行模式）");
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("日志捕获与过滤工具", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // ========== 关键字组管理 ==========
        EditorGUILayout.LabelField("关键字组管理", EditorStyles.boldLabel);

        // 新建组
        EditorGUILayout.BeginHorizontal();
        newGroupName = EditorGUILayout.TextField("新建分组:", newGroupName);
        if (GUILayout.Button("创建分组", GUILayout.Width(80)))
        {
            if (!string.IsNullOrEmpty(newGroupName) && !keywordGroups.Any(g => g.groupName == newGroupName))
            {
                keywordGroups.Add(new KeywordGroup(newGroupName));
                SaveAllData();
                newGroupName = "";
            }
            else if (keywordGroups.Any(g => g.groupName == newGroupName))
            {
                EditorUtility.DisplayDialog("提示", "分组名称已存在", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 分组切换标签
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("切换分组:", GUILayout.Width(70));

        // 显示所有分组为按钮
        for (int i = 0; i < keywordGroups.Count; i++)
        {
            Color originalColor = GUI.backgroundColor;
            if (selectedGroupIndex == i)
            {
                GUI.backgroundColor = Color.green;
            }

            if (GUILayout.Button(keywordGroups[i].groupName, GUILayout.Width(80)))
            {
                // 取消所有分组的选中状态
                foreach (var group in keywordGroups)
                {
                    group.isSelected = false;
                }
                keywordGroups[i].isSelected = true;
                selectedGroupIndex = i;
                SaveAllData();

                // 退出重命名模式
                isRenaming = false;

                if (isCapturing)
                {
                    RefreshFilteredLogs();
                }
            }
            GUI.backgroundColor = originalColor;
        }

        // 删除分组按钮（不能删除最后一个分组）
        if (keywordGroups.Count > 1 && selectedGroupIndex >= 0)
        {
            if (GUILayout.Button("✕", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("确认删除",
                    $"确定要删除分组 \"{keywordGroups[selectedGroupIndex].groupName}\" 吗？",
                    "确定", "取消"))
                {
                    string groupName = keywordGroups[selectedGroupIndex].groupName;
                    keywordGroups.RemoveAt(selectedGroupIndex);
                    selectedGroupIndex = Mathf.Min(selectedGroupIndex, keywordGroups.Count - 1);
                    if (selectedGroupIndex >= 0)
                    {
                        keywordGroups[selectedGroupIndex].isSelected = true;
                    }
                    isRenaming = false;
                    SaveAllData();
                    Debug.Log($"[日志捕获工具] 已删除分组: {groupName}");

                    if (isCapturing)
                    {
                        RefreshFilteredLogs();
                    }
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        // ========== 当前分组操作 ==========
        if (selectedGroupIndex >= 0 && selectedGroupIndex < keywordGroups.Count)
        {
            var currentGroup = keywordGroups[selectedGroupIndex];

            EditorGUILayout.Space();

            // 分组名称显示 + 重命名
            EditorGUILayout.BeginHorizontal();

            if (!isRenaming)
            {
                EditorGUILayout.LabelField($"当前分组: {currentGroup.groupName} (共 {currentGroup.keywords.Count} 个关键字)", EditorStyles.boldLabel);
                if (GUILayout.Button("重命名", GUILayout.Width(60)))
                {
                    isRenaming = true;
                    renameTempName = currentGroup.groupName;
                }
            }
            else
            {
                EditorGUILayout.LabelField("重命名:", GUILayout.Width(50));
                renameTempName = EditorGUILayout.TextField(renameTempName, GUILayout.Width(150));
                if (GUILayout.Button("确认", GUILayout.Width(50)))
                {
                    if (!string.IsNullOrEmpty(renameTempName) && !keywordGroups.Any(g => g.groupName == renameTempName && g != currentGroup))
                    {
                        currentGroup.groupName = renameTempName;
                        isRenaming = false;
                        SaveAllData();
                        Debug.Log($"[日志捕获工具] 分组已重命名为: {renameTempName}");
                    }
                    else if (keywordGroups.Any(g => g.groupName == renameTempName && g != currentGroup))
                    {
                        EditorUtility.DisplayDialog("提示", "分组名称已存在", "确定");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("提示", "分组名称不能为空", "确定");
                    }
                }
                if (GUILayout.Button("取消", GUILayout.Width(50)))
                {
                    isRenaming = false;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // ========== 关键字管理 ==========
            EditorGUILayout.LabelField("关键字管理", EditorStyles.boldLabel);

            // 添加关键字到当前分组
            EditorGUILayout.BeginHorizontal();
            newKeyword = EditorGUILayout.TextField("添加关键字:", newKeyword);
            if (GUILayout.Button("添加", GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(newKeyword) && !currentGroup.keywords.Contains(newKeyword))
                {
                    currentGroup.keywords.Add(newKeyword);
                    SaveAllData();
                    newKeyword = "";

                    if (isCapturing)
                    {
                        RefreshFilteredLogs();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // 显示当前分组的关键字列表
            EditorGUILayout.BeginVertical("box");
            for (int i = 0; i < currentGroup.keywords.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"● {currentGroup.keywords[i]}", GUILayout.Width(200));
                if (GUILayout.Button("移除", GUILayout.Width(50)))
                {
                    currentGroup.keywords.RemoveAt(i);
                    SaveAllData();
                    i--;

                    if (isCapturing)
                    {
                        RefreshFilteredLogs();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            if (currentGroup.keywords.Count == 0)
            {
                EditorGUILayout.LabelField("(此分组暂无关键字)", EditorStyles.centeredGreyMiniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        // ========== 设置区域 ==========
        EditorGUILayout.LabelField("设置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        bool newAutoCapture = EditorGUILayout.Toggle("运行时自动捕获:", autoCaptureOnPlay);
        if (newAutoCapture != autoCaptureOnPlay)
        {
            autoCaptureOnPlay = newAutoCapture;
            SaveSettingsToPrefs();
        }
        EditorGUILayout.EndHorizontal();

        if (autoCaptureOnPlay)
        {
            EditorGUILayout.HelpBox(
                "当游戏进入运行模式时自动开始捕获，退出运行模式时自动停止。\n" +
                "你也可以手动点击下方的按钮来控制捕获。",
                MessageType.Info
            );
        }

        EditorGUILayout.Space();

        // ========== 控制按钮 ==========
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !autoCaptureOnPlay || !EditorApplication.isPlaying;
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
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ========== 最大显示条数 ==========
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("最大显示条数:", GUILayout.Width(100));
        int newMaxLogCount = EditorGUILayout.IntField(maxLogCount, GUILayout.Width(80));
        if (newMaxLogCount != maxLogCount && newMaxLogCount > 0)
        {
            maxLogCount = newMaxLogCount;
            SaveSettingsToPrefs();
            if (filteredLogs.Count > maxLogCount)
            {
                filteredLogs.RemoveRange(maxLogCount, filteredLogs.Count - maxLogCount);
                Repaint();
            }
        }
        EditorGUILayout.LabelField("(输入0表示无限制)", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ========== 复制功能 ==========
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

        if (GUILayout.Button("复制详细日志（含时间戳）", GUILayout.Height(25)))
        {
            CopyLogsWithTimestamp();
        }

        EditorGUILayout.Space();

        // ========== 清空日志 ==========
        if (GUILayout.Button("清空日志", GUILayout.Height(25)))
        {
            ClearLogs();
        }

        EditorGUILayout.Space();

        // ========== 状态信息 ==========
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("状态:", GUILayout.Width(50));
        if (isCapturing)
        {
            GUI.color = Color.green;
            EditorGUILayout.LabelField("● 捕获中");
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = Color.gray;
            EditorGUILayout.LabelField("○ 已停止");
            GUI.color = Color.white;
        }

        if (autoCaptureOnPlay)
        {
            GUI.color = Color.cyan;
            EditorGUILayout.LabelField("| 自动模式已启用");
            GUI.color = Color.white;
        }

        if (selectedGroupIndex >= 0 && selectedGroupIndex < keywordGroups.Count)
        {
            EditorGUILayout.LabelField($"| 分组: {keywordGroups[selectedGroupIndex].groupName}", GUILayout.Width(150));
        }

        EditorGUILayout.LabelField($"| 当前: {filteredLogs.Count}/{maxLogCount}", GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ========== 日志列表 ==========
        EditorGUILayout.LabelField($"过滤后的日志 (共 {filteredLogs.Count} 条)", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));

        foreach (var log in filteredLogs)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField($"日志: {log.SimpleMessage}", EditorStyles.wordWrappedLabel);

            if (!string.IsNullOrEmpty(log.FilePath))
            {
                EditorGUILayout.LabelField($"位置: {log.FilePath}", EditorStyles.miniLabel);
            }

            if (!string.IsNullOrEmpty(log.PreviousFilePath))
            {
                EditorGUILayout.LabelField($"上一级: {log.PreviousFilePath}", EditorStyles.miniLabel);
            }

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
        if (ActiveKeywords.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "当前分组没有关键字，请先添加关键字", "确定");
            return;
        }

        isCapturing = true;
        filteredLogs.Clear();
        Application.logMessageReceived += OnLogMessageReceived;
        Debug.Log($"日志捕获已开始... (分组: {keywordGroups[selectedGroupIndex].groupName})");
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

    private void RefreshFilteredLogs()
    {
        Debug.Log($"[日志捕获工具] 切换分组到: {keywordGroups[selectedGroupIndex].groupName}");
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (!isCapturing) return;

        var activeKeywords = ActiveKeywords;
        if (activeKeywords.Count == 0) return;

        bool containsKeyword = false;
        foreach (var keyword in activeKeywords)
        {
            if (condition.Contains(keyword) || stackTrace.Contains(keyword))
            {
                containsKeyword = true;
                break;
            }
        }

        if (!containsKeyword) return;

        LogEntry entry = new LogEntry();
        entry.RawMessage = condition;
        entry.StackTrace = stackTrace;
        entry.LogType = type;
        entry.TimeStamp = DateTime.Now;
        entry.HasStackTrace = !string.IsNullOrEmpty(stackTrace);

        entry.SimpleMessage = ExtractSimpleMessage(condition);
        ExtractFilePaths(stackTrace, out string currentPath, out string previousPath);
        entry.FilePath = currentPath;
        entry.PreviousFilePath = previousPath;

        filteredLogs.Insert(0, entry);
        if (maxLogCount > 0 && filteredLogs.Count > maxLogCount)
        {
            filteredLogs.RemoveAt(filteredLogs.Count - 1);
        }

        Repaint();
    }

    private string ExtractSimpleMessage(string condition)
    {
        int colonIndex = condition.IndexOf(':');
        if (colonIndex > 0 && colonIndex < condition.Length - 1)
        {
            return condition.Substring(colonIndex + 1).Trim();
        }
        return condition;
    }

    private void ExtractFilePaths(string stackTrace, out string currentPath, out string previousPath)
    {
        currentPath = "";
        previousPath = "";

        if (string.IsNullOrEmpty(stackTrace)) return;

        Regex regex = new Regex(@"\(at\s+([^:]+):(\d+)\)");
        MatchCollection matches = regex.Matches(stackTrace);

        if (matches.Count > 0)
        {
            string filePath = matches[0].Groups[1].Value;
            string lineNumber = matches[0].Groups[2].Value;
            currentPath = $"{filePath}:{lineNumber}";

            if (matches.Count > 1)
            {
                filePath = matches[1].Groups[1].Value;
                lineNumber = matches[1].Groups[2].Value;
                previousPath = $"{filePath}:{lineNumber}";
            }
        }
        else
        {
            regex = new Regex(@"Assets/[^\s]+\.cs:\d+");
            matches = regex.Matches(stackTrace);

            if (matches.Count > 0)
            {
                currentPath = matches[0].Value;
                if (matches.Count > 1)
                {
                    previousPath = matches[1].Value;
                }
            }
        }
    }

    private void CopyLogsToClipboard(bool includePath)
    {
        if (filteredLogs.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有日志可复制", "确定");
            return;
        }

        string result = "";
        string groupName = selectedGroupIndex >= 0 ? keywordGroups[selectedGroupIndex].groupName : "未知";
        string keywordsStr = string.Join(", ", ActiveKeywords);

        if (includePath)
        {
            result = $"=== 过滤日志 (包含路径) ===\n";
            result += $"分组: {groupName}\n";
            result += $"关键字: {keywordsStr}\n";
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
                if (!string.IsNullOrEmpty(log.PreviousFilePath))
                {
                    result += $" <- [{log.PreviousFilePath}]";
                }
                result += "\n";
            }
        }
        else
        {
            result = $"=== 简化日志 ===\n";
            result += $"分组: {groupName}\n";
            result += $"关键字: {keywordsStr}\n";
            result += $"总计: {filteredLogs.Count} 条\n";
            result += new string('=', 50) + "\n\n";

            for (int i = 0; i < filteredLogs.Count; i++)
            {
                result += $"{i + 1}. {filteredLogs[i].SimpleMessage}\n";
            }
        }

        EditorGUIUtility.systemCopyBuffer = result;
        EditorUtility.DisplayDialog("成功", $"已复制 {filteredLogs.Count} 条日志到剪贴板", "确定");
    }

    private void CopyLogsWithTimestamp()
    {
        if (filteredLogs.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "没有日志可复制", "确定");
            return;
        }

        string groupName = selectedGroupIndex >= 0 ? keywordGroups[selectedGroupIndex].groupName : "未知";
        string keywordsStr = string.Join(", ", ActiveKeywords);

        string result = $"=== 详细日志 ({DateTime.Now:yyyy-MM-dd HH:mm:ss}) ===\n";
        result += $"分组: {groupName}\n";
        result += $"关键字: {keywordsStr}\n";
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
            if (!string.IsNullOrEmpty(log.PreviousFilePath))
            {
                result += $"上一级: {log.PreviousFilePath}\n";
            }
            result += "\n";
        }

        EditorGUIUtility.systemCopyBuffer = result;
        EditorUtility.DisplayDialog("成功", $"已复制 {filteredLogs.Count} 条详细日志到剪贴板", "确定");
    }

    // ========== 数据持久化 ==========

    private void SaveAllData()
    {
        var groupData = new List<GroupSaveData>();
        foreach (var group in keywordGroups)
        {
            groupData.Add(new GroupSaveData
            {
                groupName = group.groupName,
                keywords = group.keywords,
                isSelected = group.isSelected
            });
        }
        string json = JsonUtility.ToJson(new GroupListWrapper { groups = groupData });
        EditorPrefs.SetString("ConsoleLogTool_Groups", json);
    }

    private void LoadAllData()
    {
        string json = EditorPrefs.GetString("ConsoleLogTool_Groups", "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<GroupListWrapper>(json);
                if (wrapper?.groups != null)
                {
                    keywordGroups.Clear();
                    foreach (var data in wrapper.groups)
                    {
                        var group = new KeywordGroup(data.groupName, data.keywords ?? new List<string>());
                        group.isSelected = data.isSelected;
                        keywordGroups.Add(group);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[日志捕获工具] 加载分组数据失败: {e.Message}");
            }
        }
    }

    private void SaveSettingsToPrefs()
    {
        EditorPrefs.SetBool("ConsoleLogTool_AutoCapture", autoCaptureOnPlay);
        EditorPrefs.SetInt("ConsoleLogTool_MaxLogCount", maxLogCount);
    }

    private void LoadSettingsFromPrefs()
    {
        autoCaptureOnPlay = EditorPrefs.GetBool("ConsoleLogTool_AutoCapture", true);
        maxLogCount = EditorPrefs.GetInt("ConsoleLogTool_MaxLogCount", 99999);
    }

    // ========== 数据类 ==========

    [Serializable]
    public class GroupSaveData
    {
        public string groupName;
        public List<string> keywords;
        public bool isSelected;
    }

    [Serializable]
    public class GroupListWrapper
    {
        public List<GroupSaveData> groups;
    }

    private class LogEntry
    {
        public string RawMessage { get; set; }
        public string SimpleMessage { get; set; }
        public string FilePath { get; set; }
        public string PreviousFilePath { get; set; }
        public string StackTrace { get; set; }
        public LogType LogType { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool HasStackTrace { get; set; }
        public bool ShowStackTrace { get; set; }
    }
}
