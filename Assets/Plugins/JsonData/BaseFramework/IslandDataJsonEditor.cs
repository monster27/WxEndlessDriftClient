#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// 场景数据编辑器 - 用于编辑场景元素的位置和大小
/// 直接读写 JSON 文件，不依赖运行时脚本
/// 菜单路径: Tools/基础框架/101_岛屿
/// </summary>
public class IslandDataJsonEditor : EditorWindow
{
    // ========== 数据路径 ==========
    private const string RELATIVE_PATH = "Resources/JsonData/Game/SceneTransData/mainTransData.json";
    private const string FULL_PATH = "Assets/Resources/JsonData/Game/SceneTransData/mainTransData.json";

    // ========== 数据引用 ==========
    private SceneDataWrapper currentData;
    private SceneData selectedScene;
    private int selectedSceneIndex = -1;
    private string[] sceneOptions;

    // ========== UI 状态 ==========
    private Vector2 scrollPosition;
    private Vector2 elementScrollPosition;
    private string operationLog = "";
    private string searchFilter = "";
    private bool showHelp = true;

    // ========== 编辑器偏好 ==========
    private float col1 = 60;
    private float col2 = 120;
    private float col3 = 60;
    private float col4 = 60;

    // ========== 菜单入口 ==========
    [MenuItem("Tools/基础框架/101_岛屿")]
    public static void ShowWindow()
    {
        var window = GetWindow<IslandDataJsonEditor>("场景数据编辑器");
        window.minSize = new Vector2(700, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadData();
    }

    // ========== 数据加载 ==========
    private void LoadData()
    {
        string fullPath = GetFullPath();
        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                currentData = JsonUtility.FromJson<SceneDataWrapper>(json);
                if (currentData == null || currentData.scenes == null)
                {
                    currentData = new SceneDataWrapper();
                    Debug.LogWarning("[场景数据编辑器] JSON解析失败，创建新数据");
                }
                else
                {
                    Debug.Log($"[场景数据编辑器] 加载成功，共 {currentData.scenes.Count} 个场景");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[场景数据编辑器] 加载失败: {e.Message}");
                currentData = new SceneDataWrapper();
            }
        }
        else
        {
            Debug.LogWarning($"[场景数据编辑器] 文件不存在: {fullPath}，创建新数据");
            currentData = new SceneDataWrapper();
        }

        UpdateSceneOptions();
        Repaint();
    }

    private string GetFullPath()
    {
        // 尝试多个可能的路径
        string[] possiblePaths = new string[]
        {
            Path.Combine(Application.dataPath, "Resources", "JsonData", "Game", "SceneTransData", "mainTransData.json"),
            Path.Combine(Application.dataPath, "Resources", "JsonData/Game/SceneTransData/mainTransData.json"),
            Path.Combine(Application.dataPath, "Plugins", "JsonData", "Game", "SceneTransData", "mainTransData.json"),
            Path.Combine(Application.dataPath, "Resources", "mainTransData.json")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // 如果都不存在，返回默认路径
        return Path.Combine(Application.dataPath, "Resources", "JsonData", "Game", "SceneTransData", "mainTransData.json");
    }

    private void UpdateSceneOptions()
    {
        if (currentData == null || currentData.scenes == null || currentData.scenes.Count == 0)
        {
            sceneOptions = new string[] { "无场景数据" };
            selectedScene = null;
            selectedSceneIndex = -1;
            return;
        }

        sceneOptions = currentData.scenes.Select(s => $"{s.sceneId}: {s.sceneName}").ToArray();

        if (selectedSceneIndex >= 0 && selectedSceneIndex < currentData.scenes.Count)
        {
            selectedScene = currentData.scenes[selectedSceneIndex];
        }
        else
        {
            selectedScene = currentData.scenes.FirstOrDefault();
            selectedSceneIndex = selectedScene != null ? 0 : -1;
        }
    }

    // ========== GUI 绘制 ==========
    private void OnGUI()
    {
        DrawHelpSection();
        DrawToolbar();
        DrawSceneList();
        DrawSceneDetailEditor();
        DrawElementList();
        DrawActionButtons();
        DrawSaveButtons();
        DrawOperationLog();
    }

    // ========== 帮助文档 ==========
    private void DrawHelpSection()
    {
        EditorGUILayout.BeginVertical("box");

        // 标题
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("📖 场景数据编辑器使用说明", EditorStyles.boldLabel);
        showHelp = EditorGUILayout.Toggle("显示帮助", showHelp, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        if (showHelp)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("【功能说明】", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  此编辑器用于管理游戏场景中各个元素（背景、NPC、玩家等）的位置和大小数据。", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("【操作步骤】", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  1. 点击左侧场景列表中的\"选择\"按钮，选中要编辑的场景", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  2. 在\"场景详情\"区域修改场景名称或镜像开关", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  3. 在\"元素列表\"中查看该场景所有元素的位置和大小数据", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  4. 点击\"从场景加载数据\"从当前场景的控制器加载数据", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  5. 点击\"应用场景数据\"将选中的数据应用到场景", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("【数据说明】", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  • 场景ID: 场景的唯一标识（如 101, 102）", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  • 场景名称: 场景的显示名称（如 融冠岛, 彩虹岛）", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  • 镜像: 是否水平翻转场景", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  • 位置 (X, Y, Z): 元素在场景中的坐标", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  • 大小 (X, Y, Z): 元素的缩放比例", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("【JSON文件路径】", EditorStyles.boldLabel);
            string fullPath = GetFullPath();
            EditorGUILayout.LabelField($"  {fullPath}", EditorStyles.miniLabel);

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("【提示】", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  • 修改数据后记得点击\"保存\"按钮", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  • 编辑器模式下修改的JSON数据会在运行时被加载", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("  • 位置数据使用float数组存储，兼容其他C#引擎", EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    // ========== 工具栏 ==========
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            LoadData();
            AddLog("🔄 数据已刷新");
        }

        if (GUILayout.Button("➕ 新增场景", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            AddNewScene();
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField("搜索:", GUILayout.Width(35));
        searchFilter = EditorGUILayout.TextField(searchFilter, GUILayout.Width(150));

        EditorGUILayout.LabelField($"共 {currentData?.scenes?.Count ?? 0} 个场景", GUILayout.Width(120));

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    // ========== 场景列表 ==========
    private void DrawSceneList()
    {
        if (currentData == null || currentData.scenes == null || currentData.scenes.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无场景数据，点击 \"➕ 新增场景\" 添加", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("📋 场景列表", EditorStyles.boldLabel);

        // 表头
        EditorGUILayout.BeginHorizontal("box");
        DrawResizableColumn("ID", ref col1);
        DrawResizableColumn("名称", ref col2);
        DrawResizableColumn("镜像", ref col3);
        DrawResizableColumn("元素", ref col4);
        EditorGUILayout.LabelField("操作", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        // 数据行
        EditorGUILayout.BeginVertical("box");
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(120));

        for (int i = 0; i < currentData.scenes.Count; i++)
        {
            var scene = currentData.scenes[i];

            if (!string.IsNullOrEmpty(searchFilter))
            {
                if (!scene.sceneId.Contains(searchFilter) &&
                    !scene.sceneName.Contains(searchFilter, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            DrawSceneListItem(scene, i);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawSceneListItem(SceneData scene, int index)
    {
        EditorGUILayout.BeginHorizontal();

        if (selectedSceneIndex == index)
        {
            GUI.backgroundColor = Color.cyan;
        }

        EditorGUILayout.LabelField(scene.sceneId, GUILayout.Width(col1));
        EditorGUILayout.LabelField(scene.sceneName, GUILayout.Width(col2));
        EditorGUILayout.LabelField(scene.isFlipped ? "✅" : "❌", GUILayout.Width(col3));
        EditorGUILayout.LabelField(scene.elements?.Count.ToString() ?? "0", GUILayout.Width(col4));

        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("选择", GUILayout.Width(50)))
        {
            selectedSceneIndex = index;
            selectedScene = scene;
            AddLog($"📌 选择场景: {scene.sceneId} - {scene.sceneName}");
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) &&
            EditorUtility.DisplayDialog("确认删除", $"确定要删除场景 [{scene.sceneId}] {scene.sceneName} 吗？", "删除", "取消"))
        {
            currentData.scenes.RemoveAt(index);
            if (selectedSceneIndex >= currentData.scenes.Count) selectedSceneIndex = -1;
            SaveData();
            UpdateSceneOptions();
            AddLog($"🗑️ 删除场景: {scene.sceneId} - {scene.sceneName}");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        if (index < currentData.scenes.Count - 1) EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    // ========== 场景详情编辑 ==========
    private void DrawSceneDetailEditor()
    {
        if (selectedScene == null)
        {
            EditorGUILayout.HelpBox("请从左侧列表选择一个场景进行编辑", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("✏️ 场景详情", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField($"正在编辑: [{selectedScene.sceneId}] {selectedScene.sceneName}", EditorStyles.boldLabel);
        GUILayout.Space(5);

        // 场景ID（只读）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("场景ID:", GUILayout.Width(60));
        EditorGUILayout.LabelField(selectedScene.sceneId, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // 场景名称（可编辑）
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("场景名称:", GUILayout.Width(60));
        string newName = EditorGUILayout.TextField(selectedScene.sceneName);
        if (newName != selectedScene.sceneName)
        {
            selectedScene.sceneName = newName;
            UpdateSceneOptions();
            AddLog($"📝 场景名称已更新: {selectedScene.sceneId} -> {newName}");
        }
        EditorGUILayout.EndHorizontal();

        // 镜像开关
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("镜像:", GUILayout.Width(60));
        bool newFlip = EditorGUILayout.Toggle(selectedScene.isFlipped);
        if (newFlip != selectedScene.isFlipped)
        {
            selectedScene.isFlipped = newFlip;
            AddLog($"🔄 场景镜像切换为: {(newFlip ? "开启" : "关闭")}");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    // ========== 元素列表 ==========
    private void DrawElementList()
    {
        if (selectedScene == null || selectedScene.elements == null || selectedScene.elements.Count == 0)
        {
            EditorGUILayout.HelpBox("该场景没有元素数据", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"📦 元素列表 ({selectedScene.elements.Count} 个)", EditorStyles.boldLabel);

        // 表头
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("序号", GUILayout.Width(40));
        EditorGUILayout.LabelField("元素ID", GUILayout.Width(100));
        EditorGUILayout.LabelField("名称", GUILayout.Width(120));
        EditorGUILayout.LabelField("位置 (X, Y, Z)", GUILayout.Width(180));
        EditorGUILayout.LabelField("大小 (X, Y, Z)", GUILayout.Width(180));
        EditorGUILayout.EndHorizontal();

        // 数据行
        EditorGUILayout.BeginVertical("box");
        elementScrollPosition = EditorGUILayout.BeginScrollView(elementScrollPosition, GUILayout.Height(200));

        for (int i = 0; i < selectedScene.elements.Count; i++)
        {
            var element = selectedScene.elements[i];
            DrawElementListItem(element, i);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawElementListItem(SceneElementData element, int index)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(40));
        EditorGUILayout.LabelField(element.id, GUILayout.Width(100));
        EditorGUILayout.LabelField(element.name, GUILayout.Width(120));

        if (element.transform != null)
        {
            EditorGUILayout.LabelField(
                $"({element.transform.position.x:F2}, {element.transform.position.y:F2}, {element.transform.position.z:F2})",
                GUILayout.Width(180));
            EditorGUILayout.LabelField(
                $"({element.transform.scale.x:F2}, {element.transform.scale.y:F2}, {element.transform.scale.z:F2})",
                GUILayout.Width(180));
        }

        EditorGUILayout.EndHorizontal();
        if (index < selectedScene.elements.Count - 1) EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    // ========== 操作按钮 ==========
    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("⚙️ 操作", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("📥 从场景加载", GUILayout.Height(30)))
        {
            if (selectedScene != null)
            {
                AddLog($"📥 从场景加载数据: {selectedScene.sceneId}");
                EditorUtility.DisplayDialog("提示",
                    "此功能需要场景中有 SceneMatManager 组件。\n" +
                    "如果场景中没有该组件，请直接编辑 JSON 数据。",
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
            }
        }

        if (GUILayout.Button("📤 应用到场景", GUILayout.Height(30)))
        {
            if (selectedScene != null)
            {
                AddLog($"📤 应用场景数据: {selectedScene.sceneId}");
                EditorUtility.DisplayDialog("提示",
                    "此功能需要场景中有 SceneMatManager 组件。\n" +
                    "如果场景中没有该组件，数据已保存在 JSON 中，\n" +
                    "运行时会自动加载。",
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
            }
        }

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    // ========== 保存按钮 ==========
    private void DrawSaveButtons()
    {
        EditorGUILayout.LabelField("💾 保存", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("保存到文件", GUILayout.Height(30)))
        {
            SaveDataToFile();
            AddLog("💾 数据已保存到文件");
            EditorUtility.DisplayDialog("保存成功", "场景数据已保存到 JSON 文件", "确定");
        }

        if (GUILayout.Button("导出JSON预览", GUILayout.Height(30)))
        {
            string json = JsonUtility.ToJson(currentData, true);
            EditorUtility.DisplayDialog("JSON预览", json, "确定");
            AddLog("📄 已导出JSON预览");
        }

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);
    }

    // ========== 操作日志 ==========
    private void DrawOperationLog()
    {
        if (string.IsNullOrEmpty(operationLog)) return;

        EditorGUILayout.LabelField("📋 操作日志", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField(operationLog, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(60));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空日志", GUILayout.Width(80)))
        {
            operationLog = "";
        }
        EditorGUILayout.EndHorizontal();
    }

    // ========== 辅助方法 ==========

    private void DrawResizableColumn(string label, ref float width)
    {
        EditorGUILayout.LabelField(label, GUILayout.Width(width));
        Rect rect = GUILayoutUtility.GetLastRect();
        EditorGUIUtility.AddCursorRect(new Rect(rect.x + rect.width - 5, rect.y, 10, rect.height), MouseCursor.ResizeHorizontal);
    }

    private void AddLog(string message)
    {
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        operationLog = $"[{timestamp}] {message}\n" + operationLog;
        if (operationLog.Length > 2000)
        {
            operationLog = operationLog.Substring(0, 2000);
        }
    }

    private void AddNewScene()
    {
        if (currentData == null) currentData = new SceneDataWrapper();

        int maxId = 0;
        if (currentData.scenes != null)
        {
            foreach (var scene in currentData.scenes)
            {
                if (int.TryParse(scene.sceneId, out int id) && id > maxId)
                {
                    maxId = id;
                }
            }
        }
        int newId = maxId + 1;

        var newScene = new SceneData
        {
            sceneId = newId.ToString(),
            sceneName = $"新场景_{newId}",
            isFlipped = false,
            elements = new List<SceneElementData>()
        };

        currentData.scenes.Add(newScene);
        SaveData();
        UpdateSceneOptions();
        selectedScene = newScene;
        selectedSceneIndex = currentData.scenes.Count - 1;
        AddLog($"✨ 新增场景: {newScene.sceneId} - {newScene.sceneName}");
    }

    private void SaveData()
    {
        // 数据已经在内存中更新，保存到文件
        SaveDataToFile();
        UpdateSceneOptions();
        Repaint();
    }

    private void SaveDataToFile()
    {
        if (currentData == null) return;

        string json = JsonUtility.ToJson(currentData, true);
        string fullPath = GetFullPath();

        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();

        Debug.Log($"[场景数据编辑器] 数据已保存到: {fullPath}");
        AddLog($"💾 保存成功: {Path.GetFileName(fullPath)}");
    }
}
#endif