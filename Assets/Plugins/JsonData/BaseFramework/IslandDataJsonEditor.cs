#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IslandDataJsonEditor : EditorWindow
{
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.scene";

    private SceneDataWrapper currentData;
    private int selectedSceneIndex = -1;
    private bool isIndoorSelected = false;
    private bool isEditingScene = false;
    private bool isEditingElements = false;

    private enum EditMode { Default, Mirror, Indoor }
    private EditMode currentMode = EditMode.Default;

    private Vector2 mainScroll;
    private Vector2 sceneListScroll;
    private Vector2 elementListScroll;
    private string operationLog = "";
    private string searchFilter = "";
    private bool showHelp = false;

    [MenuItem("Tools/基础框架/101_岛屿")]
    public static void ShowWindow()
    {
        var window = GetWindow<IslandDataJsonEditor>("场景数据编辑器");
        window.minSize = new Vector2(720, 620);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private string GetFullPath()
    {
        return Path.Combine(Application.dataPath, "Addressables", "JsonData", "Game", "SceneTransData", "islandsTransData.json");
    }

    private void LoadData()
    {
        string fullPath = GetFullPath();
        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                currentData = JsonUtility.FromJson<SceneDataWrapper>(json);
                if (currentData == null) currentData = new SceneDataWrapper();
                if (currentData.scenes == null) currentData.scenes = new List<SceneData>();
                if (currentData.indoorElements == null) currentData.indoorElements = new List<SceneElementData>();

                foreach (var s in currentData.scenes)
                {
                    if (s.elements == null) s.elements = new List<SceneElementData>();
                    if (s.mirrorElements == null) s.mirrorElements = new List<SceneElementData>();
                }
                Z_Logger.Log($"[场景数据编辑器] 加载成功，{currentData.scenes.Count} 个场景，{currentData.indoorElements.Count} 个室内元素");
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[场景数据编辑器] 加载失败: {e.Message}");
                currentData = new SceneDataWrapper();
            }
        }
        else
        {
            currentData = new SceneDataWrapper();
        }

        selectedSceneIndex = currentData.scenes.Count > 0 ? 0 : -1;
        isIndoorSelected = false;
        isEditingScene = false;
        isEditingElements = false;
        Repaint();
    }

    private void SaveData()
    {
        if (currentData == null) return;
        string json = JsonUtility.ToJson(currentData, true);
        string fullPath = GetFullPath();
        string dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();
    }

    private void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
        DrawBanner();
        DrawHelpSection();
        DrawToolbar();
        DrawSceneList();
        DrawSceneDetail();
        DrawElementEditor();
        DrawOperationLog();
        EditorGUILayout.EndScrollView();
    }

    private void DrawBanner()
    {
        var style = new GUIStyle(EditorStyles.helpBox);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.45f, 0.8f);
        EditorGUILayout.BeginVertical(style);
        EditorGUILayout.LabelField("🛠️ 编辑器扩展工具 - 场景数据配置", style);
        EditorGUILayout.LabelField("此工具仅用于编辑 JSON 配置，不参与游戏运行逻辑", style);
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = old;
        GUILayout.Space(8);
    }

    private void DrawHelpSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        string icon = showHelp ? "▼" : "▶";
        EditorGUILayout.LabelField($"{icon} 📖 使用帮助", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(showHelp ? "收起" : "展开", GUILayout.Width(60)))
            showHelp = !showHelp;
        EditorGUILayout.EndHorizontal();

        if (showHelp)
        {
            EditorGUILayout.LabelField("• 场景列表：编辑岛屿 ID/名称，或跳转到 GameScene 编辑元素。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• 室内条目固定在最上方，不可删除。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• 修改 ID/名称会自动保存。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("• 三个保存按钮：室外默认 / 室外镜像 / 室内。", EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(8);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("🔄 重新加载 JSON", EditorStyles.toolbarButton, GUILayout.Width(120)))
        {
            bool ok = EditorUtility.DisplayDialog("重新加载 JSON",
                "未修改的数据不会保存，确定要重新加载吗？", "确定", "取消");
            if (ok) { LoadData(); AddLog("🔄 已重新加载 JSON"); }
        }

        if (GUILayout.Button("➕ 新增场景", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            AddNewScene();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("搜索:", GUILayout.Width(35));
        searchFilter = EditorGUILayout.TextField(searchFilter, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(6);
    }

    private void DrawSceneList()
    {
        EditorGUILayout.LabelField("📋 场景列表", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("ID", GUILayout.Width(80));
        EditorGUILayout.LabelField("名称", GUILayout.Width(180));
        EditorGUILayout.LabelField("元素数量", GUILayout.Width(80));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("操作", GUILayout.Width(260));
        EditorGUILayout.EndHorizontal();

        sceneListScroll = EditorGUILayout.BeginScrollView(sceneListScroll, GUILayout.Height(140));

        DrawIndoorRow();

        for (int i = 0; i < currentData.scenes.Count; i++)
        {
            var scene = currentData.scenes[i];
            if (!string.IsNullOrEmpty(searchFilter))
            {
                if (!scene.sceneId.Contains(searchFilter) &&
                    !scene.sceneName.Contains(searchFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            DrawSceneRow(scene, i);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(8);
    }

    private void DrawIndoorRow()
    {
        EditorGUILayout.BeginHorizontal();
        if (isIndoorSelected) GUI.backgroundColor = Color.cyan;

        var bold = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
        EditorGUILayout.LabelField("【室内】", bold, GUILayout.Width(80));
        EditorGUILayout.LabelField("室内", bold, GUILayout.Width(180));
        EditorGUILayout.LabelField((currentData.indoorElements?.Count ?? 0).ToString(), GUILayout.Width(80));
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("开始编辑元素数据", GUILayout.Width(160)))
        {
            OnClickEditElements(isIndoor: true, sceneId: null, sceneName: "室内");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawSceneRow(SceneData scene, int index)
    {
        EditorGUILayout.BeginHorizontal();
        if (!isIndoorSelected && selectedSceneIndex == index) GUI.backgroundColor = Color.cyan;

        EditorGUILayout.LabelField(scene.sceneId, GUILayout.Width(80));
        EditorGUILayout.LabelField(scene.sceneName, GUILayout.Width(180));
        EditorGUILayout.LabelField((scene.elements?.Count ?? 0).ToString(), GUILayout.Width(80));
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(60)))
        {
            selectedSceneIndex = index;
            isIndoorSelected = false;
            isEditingScene = true;
            isEditingElements = false;
            currentMode = EditMode.Default;
            AddLog($"📌 选择场景: {scene.sceneId}");
        }

        if (GUILayout.Button("开始编辑元素数据", GUILayout.Width(160)))
        {
            OnClickEditElements(isIndoor: false, sceneId: scene.sceneId, sceneName: scene.sceneName);
        }

        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("删除", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("确认删除",
                $"确定删除场景 [{scene.sceneId}] {scene.sceneName} 吗？", "删除", "取消"))
            {
                currentData.scenes.RemoveAt(index);
                if (selectedSceneIndex >= currentData.scenes.Count) selectedSceneIndex = -1;
                SaveData();
                AddLog($"🗑️ 删除场景: {scene.sceneId}");
                GUIUtility.ExitGUI();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawSceneDetail()
    {
        if (!isEditingScene) return;
        if (isIndoorSelected) return;
        if (selectedSceneIndex < 0 || selectedSceneIndex >= currentData.scenes.Count) return;

        var scene = currentData.scenes[selectedSceneIndex];

        EditorGUILayout.LabelField("✏️ 场景详情", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.HelpBox("修改后会自动保存到 JSON", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("场景ID:", GUILayout.Width(60));
        string newId = EditorGUILayout.TextField(scene.sceneId, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("场景名称:", GUILayout.Width(60));
        string newName = EditorGUILayout.TextField(scene.sceneName);
        EditorGUILayout.EndHorizontal();

        if (newId != scene.sceneId)
        {
            if (IsSceneIdDuplicate(newId, selectedSceneIndex))
                EditorGUILayout.HelpBox($"ID {newId} 已存在", MessageType.Error);
            else if (string.IsNullOrEmpty(newId))
                EditorGUILayout.HelpBox("ID 不能为空", MessageType.Error);
            else
            {
                scene.sceneId = newId;
                SaveData();
                AddLog($"📝 场景ID已更新 → {newId}（已自动保存）");
            }
        }

        if (newName != scene.sceneName)
        {
            scene.sceneName = newName;
            SaveData();
            AddLog($"📝 场景名称已更新 → {newName}（已自动保存）");
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(8);
    }

    private bool IsSceneIdDuplicate(string id, int excludeIndex)
    {
        for (int i = 0; i < currentData.scenes.Count; i++)
            if (i != excludeIndex && currentData.scenes[i].sceneId == id) return true;
        return false;
    }

    private void DrawElementEditor()
    {
        if (!isEditingElements) return;

        EditorGUILayout.LabelField("⚙️ 操作", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("JSON 路径:", GUILayout.Width(70));
        EditorGUILayout.SelectableLabel(GetFullPath(), EditorStyles.miniLabel, GUILayout.Height(18));
        EditorGUILayout.EndHorizontal();

        string curTarget = isIndoorSelected
            ? "室内"
            : (selectedSceneIndex >= 0 && selectedSceneIndex < currentData.scenes.Count
                ? $"{currentData.scenes[selectedSceneIndex].sceneName}（{currentData.scenes[selectedSceneIndex].sceneId}）"
                : "未选择");
        EditorGUILayout.LabelField($"当前编辑：{curTarget}", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("当前模式:", GUILayout.Width(70));
        if (isIndoorSelected)
        {
            currentMode = EditMode.Indoor;
            EditorGUILayout.LabelField("室内", GUILayout.Width(80));
        }
        else
        {
            string[] modeNames = { "默认", "镜像" };
            int idx = currentMode == EditMode.Mirror ? 1 : 0;
            int newIdx = EditorGUILayout.Popup(idx, modeNames, GUILayout.Width(80));
            currentMode = newIdx == 1 ? EditMode.Mirror : EditMode.Default;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (currentMode == EditMode.Default)
        {
            if (GUILayout.Button("保存室外默认数据", GUILayout.Height(30))) OnClickSave(EditMode.Default);
        }
        else if (currentMode == EditMode.Mirror)
        {
            if (GUILayout.Button("保存室外镜像数据", GUILayout.Height(30))) OnClickSave(EditMode.Mirror);
        }
        else
        {
            if (GUILayout.Button("保存室内数据", GUILayout.Height(30))) OnClickSave(EditMode.Indoor);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(8);

        DrawElementList();
    }

    private void DrawElementList()
    {
        string title = currentMode switch
        {
            EditMode.Default => "📦 元素列表（室外默认）",
            EditMode.Mirror => "📦 元素列表（室外镜像）",
            EditMode.Indoor => "📦 元素列表（室内）",
            _ => "📦 元素列表"
        };
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        var list = GetCurrentDataList();
        if (list == null || list.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无数据", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("元素ID", GUILayout.Width(120));
        EditorGUILayout.LabelField("名称", GUILayout.Width(120));
        EditorGUILayout.LabelField("位置", GUILayout.Width(220));
        EditorGUILayout.LabelField("大小", GUILayout.Width(220));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginVertical("box");
        elementListScroll = EditorGUILayout.BeginScrollView(elementListScroll, GUILayout.Height(180));

        foreach (var e in list)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(e.id, GUILayout.Width(120));
            EditorGUILayout.LabelField(e.name, GUILayout.Width(120));
            if (e.transform != null)
            {
                EditorGUILayout.LabelField(
                    $"({e.transform.position.x:F2}, {e.transform.position.y:F2}, {e.transform.position.z:F2})",
                    GUILayout.Width(220));
                EditorGUILayout.LabelField(
                    $"({e.transform.scale.x:F2}, {e.transform.scale.y:F2}, {e.transform.scale.z:F2})",
                    GUILayout.Width(220));
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private List<SceneElementData> GetCurrentDataList()
    {
        if (currentMode == EditMode.Indoor) return currentData.indoorElements;
        if (selectedSceneIndex < 0 || selectedSceneIndex >= currentData.scenes.Count) return null;
        var scene = currentData.scenes[selectedSceneIndex];
        return currentMode == EditMode.Mirror ? scene.mirrorElements : scene.elements;
    }

    private void DrawOperationLog()
    {
        if (string.IsNullOrEmpty(operationLog)) return;
        EditorGUILayout.LabelField("📋 操作日志", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(operationLog, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(60));
        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空日志", GUILayout.Width(80))) operationLog = "";
        EditorGUILayout.EndHorizontal();
    }

    private void AddLog(string msg)
    {
        string t = DateTime.Now.ToString("HH:mm:ss");
        operationLog = $"[{t}] {msg}\n" + operationLog;
        if (operationLog.Length > 3000) operationLog = operationLog.Substring(0, 3000);
    }

    // ==================== 开始编辑元素数据 ====================

    private void OnClickEditElements(bool isIndoor, string sceneId, string sceneName)
    {
        string msg = $"即将打开场景：{GAME_SCENE_PATH}\n\n" +
                     "操作说明：\n" +
                     "1. 打开后会自动选中 SceneMatManager 物体\n" +
                     "2. 在 Inspector 里手动设置 currentSceneId / currentIsFlipped\n" +
                     "3. 编辑完成后保存，返回本窗口重新加载 JSON\n\n" +
                     "注意：\n" +
                     "- 未保存的数据不会保留\n" +
                     "- 切换场景时，当前场景未保存的内容也不会保留\n\n" +
                     $"当前编辑：{(isIndoor ? "室内" : $"{sceneName}（{sceneId}）")}";

        if (!EditorUtility.DisplayDialog("打开场景", msg, "确定", "取消")) return;

        if (isIndoor)
        {
            isIndoorSelected = true;
        }
        else
        {
            isIndoorSelected = false;
            for (int i = 0; i < currentData.scenes.Count; i++)
            {
                if (currentData.scenes[i].sceneId == sceneId)
                {
                    selectedSceneIndex = i;
                    break;
                }
            }
        }

        isEditingScene = false;
        isEditingElements = true;
        currentMode = isIndoor ? EditMode.Indoor : EditMode.Default;

        OpenGameSceneAndSelectManager();

        AddLog($"📂 打开场景，编辑 {(isIndoor ? "室内" : sceneId)}");
    }

    /// <summary>
    /// 打开 GameScene，选中名为 "SceneMatManager" 的 GameObject。
    /// 不依赖任何运行时类型，不做赋值，不做显隐。
    /// </summary>
    private void OpenGameSceneAndSelectManager()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.isDirty)
        {
            bool save = EditorUtility.DisplayDialog(
                "当前场景有未保存内容",
                $"场景 {activeScene.name} 有未保存修改，是否保存？",
                "保存并继续", "不保存");
            if (save) EditorSceneManager.SaveScene(activeScene);
        }

        if (File.Exists(GAME_SCENE_PATH))
        {
            EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
        }
        else
        {
            EditorUtility.DisplayDialog("错误", $"找不到场景文件：{GAME_SCENE_PATH}", "确定");
            return;
        }

        GameObject managerObj = GameObject.Find("SceneMatManager");
        if (managerObj != null)
        {
            Selection.activeGameObject = managerObj;
            EditorGUIUtility.PingObject(managerObj);
            SceneView.FrameLastActiveSceneView();
        }
        else
        {
            EditorUtility.DisplayDialog("提示",
                "场景中未找到名为 'SceneMatManager' 的物体。\n" +
                "请确认物体名称是否正确。", "确定");
        }
    }

    // ==================== 三个保存按钮 ====================

    private void OnClickSave(EditMode mode)
    {
        if (mode == EditMode.Indoor) SaveIndoorData();
        else if (mode == EditMode.Default) SaveOutdoorDefault();
        else if (mode == EditMode.Mirror) SaveOutdoorMirror();
    }

    private void SaveOutdoorDefault()
    {
        if (selectedSceneIndex < 0 || selectedSceneIndex >= currentData.scenes.Count) return;
        var scene = currentData.scenes[selectedSceneIndex];
        var changes = CollectChanges(scene.elements);
        ShowPreviewWindow("保存室外默认数据", scene.sceneName, scene.sceneId, changes, () =>
        {
            ApplyChangesToElements(scene.elements, changes);
            SaveData();
            ShowSuccessDialog("室外默认数据", scene.sceneName, scene.sceneId, changes);
            AddLog($"💾 保存室外默认数据: {scene.sceneId}，有变化 {changes.Count(c => c.HasChange)} 个");
        });
    }

    private void SaveOutdoorMirror()
    {
        if (selectedSceneIndex < 0 || selectedSceneIndex >= currentData.scenes.Count) return;
        var scene = currentData.scenes[selectedSceneIndex];
        var changes = CollectChanges(scene.mirrorElements);
        ShowPreviewWindow("保存室外镜像数据", scene.sceneName, scene.sceneId, changes, () =>
        {
            ApplyChangesToElements(scene.mirrorElements, changes);
            SaveData();
            ShowSuccessDialog("室外镜像数据", scene.sceneName, scene.sceneId, changes);
            AddLog($"💾 保存室外镜像数据: {scene.sceneId}，有变化 {changes.Count(c => c.HasChange)} 个");
        });
    }

    private void SaveIndoorData()
    {
        var changes = CollectChanges(currentData.indoorElements);
        ShowPreviewWindow("保存室内数据", "室内", "-", changes, () =>
        {
            ApplyChangesToElements(currentData.indoorElements, changes);
            SaveData();
            ShowSuccessDialog("室内数据", "室内", "-", changes);
            AddLog($"💾 保存室内数据，有变化 {changes.Count(c => c.HasChange)} 个");
        });
    }

    // ==================== 变更收集 ====================

    public class ElementChange
    {
        public string id;
        public string name;
        public SceneElementTransformData oldT;
        public SceneElementTransformData newT;
        public bool HasChange;
        public bool PosChanged;
        public bool ScaleChanged;
    }

    /// <summary>
    /// 遍历当前 JSON 里已有的元素，按 GameObject 名字从场景读取当前 Transform。
    /// 场景里找不到的跳过。
    /// </summary>
    private List<ElementChange> CollectChanges(List<SceneElementData> oldList)
    {
        var result = new List<ElementChange>();
        if (oldList == null) return result;

        foreach (var old in oldList)
        {
            if (old == null || string.IsNullOrEmpty(old.id)) continue;

            GameObject go = GameObject.Find(old.id);
            if (go == null) continue;

            Vector3 pos = go.transform.position;
            Vector3 scl = go.transform.localScale;

            var newT = new SceneElementTransformData
            {
                position = SerializableVector3.FromUnityVector(pos.x, pos.y, pos.z),
                scale = SerializableVector3.FromUnityVector(scl.x, scl.y, scl.z)
            };

            var oldT = old.transform;

            var change = new ElementChange
            {
                id = old.id,
                name = old.name,
                oldT = oldT,
                newT = newT
            };

            change.PosChanged = oldT == null || !Approximately(oldT.position, newT.position);
            change.ScaleChanged = oldT == null || !Approximately(oldT.scale, newT.scale);
            change.HasChange = change.PosChanged || change.ScaleChanged;

            result.Add(change);
        }

        return result;
    }

    private bool Approximately(SerializableVector3 a, SerializableVector3 b)
    {
        if (a == null || b == null) return false;
        return Mathf.Approximately(a.x, b.x)
            && Mathf.Approximately(a.y, b.y)
            && Mathf.Approximately(a.z, b.z);
    }

    private void ApplyChangesToElements(List<SceneElementData> targetList, List<ElementChange> changes)
    {
        foreach (var c in changes)
        {
            var exist = targetList.FirstOrDefault(e => e.id == c.id);
            if (exist == null)
            {
                exist = new SceneElementData { id = c.id, name = c.name };
                targetList.Add(exist);
            }
            exist.name = c.name;
            exist.transform = new SceneElementTransformData
            {
                position = new SerializableVector3(c.newT.position.x, c.newT.position.y, c.newT.position.z),
                scale = new SerializableVector3(c.newT.scale.x, c.newT.scale.y, c.newT.scale.z)
            };
        }
    }

    private void ShowPreviewWindow(string title, string sceneName, string sceneId,
        List<ElementChange> changes, Action onConfirm)
    {
        SceneChangePreviewWindow.Open(title, sceneName, sceneId, changes, onConfirm);
    }

    private void ShowSuccessDialog(string typeName, string sceneName, string sceneId,
        List<ElementChange> changes)
    {
        int changed = changes.Count(c => c.HasChange);
        int unchanged = changes.Count - changed;

        string msg = $"✅ 保存成功\n\n" +
                     $"类型：{typeName}\n" +
                     $"场景：{sceneName}（{sceneId}）\n" +
                     $"元素数：{changes.Count} 个（有变化 {changed} 个 / 无变化 {unchanged} 个）\n\n" +
                     $"JSON 路径：\n{GetFullPath()}";

        EditorUtility.DisplayDialog("保存成功", msg, "确定");
    }

    // ==================== 新增场景 ====================

    private void AddNewScene()
    {
        if (currentData == null) currentData = new SceneDataWrapper();

        int maxId = 0;
        foreach (var s in currentData.scenes)
            if (int.TryParse(s.sceneId, out int id) && id > maxId) maxId = id;
        string newId = (maxId + 1).ToString();

        SceneData template = currentData.scenes.Count > 0
            ? currentData.scenes[0]
            : new SceneData
            {
                sceneId = "0",
                sceneName = "默认模板",
                elements = new List<SceneElementData>(),
                mirrorElements = new List<SceneElementData>()
            };

        var newScene = new SceneData
        {
            sceneId = newId,
            sceneName = $"新场景_{newId}",
            isFlipped = false,
            elements = DeepCopyElements(template.elements),
            mirrorElements = DeepCopyElements(template.mirrorElements)
        };

        currentData.scenes.Add(newScene);
        SaveData();

        selectedSceneIndex = currentData.scenes.Count - 1;
        isIndoorSelected = false;
        isEditingScene = true;
        isEditingElements = false;

        AddLog($"✨ 新增场景: {newScene.sceneId} - {newScene.sceneName}");
    }

    private List<SceneElementData> DeepCopyElements(List<SceneElementData> source)
    {
        var result = new List<SceneElementData>();
        if (source == null) return result;

        foreach (var e in source)
        {
            if (e == null) continue;
            result.Add(new SceneElementData
            {
                id = e.id,
                name = e.name,
                transform = e.transform == null ? null : new SceneElementTransformData
                {
                    position = e.transform.position == null
                        ? new SerializableVector3()
                        : new SerializableVector3(e.transform.position.x, e.transform.position.y, e.transform.position.z),
                    scale = e.transform.scale == null
                        ? new SerializableVector3(1, 1, 1)
                        : new SerializableVector3(e.transform.scale.x, e.transform.scale.y, e.transform.scale.z)
                }
            });
        }
        return result;
    }
}

// ============================================================
// 变更预览窗
// ============================================================

public class SceneChangePreviewWindow : EditorWindow
{
    private string titleText;
    private string sceneName;
    private string sceneId;
    private List<ElementChangeProxy> changedList;
    private List<ElementChangeProxy> unchangedList;
    private Action onConfirm;

    private Vector2 scroll1;
    private Vector2 scroll2;

    public static SceneChangePreviewWindow Open(string title, string sceneName, string sceneId,
        List<IslandDataJsonEditor.ElementChange> changes, Action onConfirm)
    {
        var win = GetWindow<SceneChangePreviewWindow>(true, title, true);
        win.minSize = new Vector2(700, 600);
        win.titleText = title;
        win.sceneName = sceneName;
        win.sceneId = sceneId;
        win.onConfirm = onConfirm;
        win.changedList = new List<ElementChangeProxy>();
        win.unchangedList = new List<ElementChangeProxy>();

        foreach (var c in changes)
        {
            var proxy = new ElementChangeProxy
            {
                id = c.id,
                name = c.name,
                oldPos = c.oldT?.position,
                newPos = c.newT?.position,
                oldScale = c.oldT?.scale,
                newScale = c.newT?.scale,
                PosChanged = c.PosChanged,
                ScaleChanged = c.ScaleChanged
            };
            if (c.HasChange) win.changedList.Add(proxy);
            else win.unchangedList.Add(proxy);
        }

        win.Show();
        return win;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField($"变更预览 - {titleText}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"场景：{sceneName}（{sceneId}）", EditorStyles.miniLabel);
        GUILayout.Space(6);

        EditorGUILayout.LabelField($"✅ 有变化的元素（{changedList.Count} 个）", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        scroll1 = EditorGUILayout.BeginScrollView(scroll1, GUILayout.Height(220));

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("元素ID", GUILayout.Width(100));
        EditorGUILayout.LabelField("旧位置", GUILayout.Width(160));
        EditorGUILayout.LabelField("新位置", GUILayout.Width(160));
        EditorGUILayout.LabelField("旧大小", GUILayout.Width(140));
        EditorGUILayout.LabelField("新大小", GUILayout.Width(140));
        EditorGUILayout.EndHorizontal();

        var redStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.9f, 0.3f, 0.3f) } };

        foreach (var c in changedList)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(c.id, GUILayout.Width(100));
            DrawVecPair(c.oldPos, c.newPos, c.PosChanged, redStyle, 160);
            DrawVecPair(c.oldScale, c.newScale, c.ScaleChanged, redStyle, 140);
            EditorGUILayout.EndHorizontal();
        }

        if (changedList.Count == 0)
            EditorGUILayout.LabelField("（无）", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);
        EditorGUILayout.LabelField($"➖ 无变化的元素（{unchangedList.Count} 个）", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        scroll2 = EditorGUILayout.BeginScrollView(scroll2, GUILayout.Height(150));

        var grayStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };

        foreach (var c in unchangedList)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(c.id, grayStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField(FormatVec(c.oldPos), grayStyle, GUILayout.Width(160));
            EditorGUILayout.LabelField(FormatVec(c.oldScale), grayStyle, GUILayout.Width(140));
            EditorGUILayout.EndHorizontal();
        }

        if (unchangedList.Count == 0)
            EditorGUILayout.LabelField("（无）", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("确认保存", GUILayout.Width(120), GUILayout.Height(30)))
        {
            onConfirm?.Invoke();
            Close();
        }
        if (GUILayout.Button("取消", GUILayout.Width(120), GUILayout.Height(30)))
        {
            Close();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawVecPair(SerializableVector3 oldV, SerializableVector3 newV,
        bool changed, GUIStyle redStyle, float width)
    {
        var style = changed ? redStyle : EditorStyles.label;
        EditorGUILayout.LabelField(FormatVec(oldV), style, GUILayout.Width(width * 0.5f));
        EditorGUILayout.LabelField(FormatVec(newV), style, GUILayout.Width(width * 0.5f));
    }

    private string FormatVec(SerializableVector3 v)
    {
        if (v == null) return "(null)";
        return $"({v.x:F2}, {v.y:F2}, {v.z:F2})";
    }
}

public class ElementChangeProxy
{
    public string id;
    public string name;
    public SerializableVector3 oldPos;
    public SerializableVector3 newPos;
    public SerializableVector3 oldScale;
    public SerializableVector3 newScale;
    public bool PosChanged;
    public bool ScaleChanged;
}
#endif
