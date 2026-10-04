using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ============================================================
// 1. 编辑器：SceneMatEditor
// ============================================================

[CustomEditor(typeof(SceneMatManager))]
public class SceneMatEditor : Editor
{
    private SceneMatManager manager;
    private SceneData selectedScene;
    private int selectedSceneIndex = -1;
    private string[] sceneOptions;

    private bool foldOutdoor = true;
    private bool foldMirror = false;
    private bool foldIndoor = false;

    private Vector2 outdoorScroll;
    private Vector2 mirrorScroll;
    private Vector2 indoorScroll;

    private string operationLog = "";

    private enum SaveTarget { OutdoorDefault, OutdoorMirror, Indoor }

    private async void OnEnable()
    {
        manager = (SceneMatManager)target;
        await manager.LoadSceneData();
        LoadSceneOptions();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DrawBanner();

        EditorGUILayout.Space(8);
        DrawSceneSelector();

        if (selectedScene != null)
        {
            DrawSceneInfo();
            DrawOutdoorElements();
            DrawMirrorElements();
        }

        DrawIndoorElements();
        DrawOperationArea();
        DrawOperationLog();
    }

    // ---------- 顶部横幅 ----------

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
        EditorGUILayout.LabelField("🛠️ 编辑器扩展工具 - 场景材质 Inspector", style);
        EditorGUILayout.LabelField("此工具仅用于查看/切换/保存场景配置，不参与游戏运行逻辑", style);
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = old;
        GUILayout.Space(8);
    }

    // ---------- 场景选择 ----------

    private void LoadSceneOptions()
    {
        if (manager == null) return;

        var scenes = manager.GetAllSceneData();
        if (scenes == null || scenes.Count == 0)
        {
            sceneOptions = new string[] { "无场景数据" };
            return;
        }

        sceneOptions = scenes.Select(s => $"{s.sceneId}: {s.sceneName}").ToArray();

        string currentId = manager.CurrentSceneId;
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].sceneId == currentId)
            {
                selectedSceneIndex = i;
                selectedScene = scenes[i];
                break;
            }
        }

        if (selectedSceneIndex == -1 && scenes.Count > 0)
        {
            selectedScene = scenes[0];
            selectedSceneIndex = 0;
        }
    }

    private void DrawSceneSelector()
    {
        EditorGUILayout.LabelField($"当前场景ID: {manager.CurrentSceneId}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"当前场景名称: {manager.CurrentSceneName}", EditorStyles.boldLabel);

        if (sceneOptions == null || sceneOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("请先加载场景数据", MessageType.Warning);
            return;
        }

        int newIndex = EditorGUILayout.Popup("选择场景", selectedSceneIndex, sceneOptions);
        if (newIndex != selectedSceneIndex)
        {
            selectedSceneIndex = newIndex;
            var scenes = manager.GetAllSceneData();
            if (scenes != null && selectedSceneIndex >= 0 && selectedSceneIndex < scenes.Count)
            {
                selectedScene = scenes[selectedSceneIndex];
                if (selectedScene != null)
                    manager.currentSceneName = selectedScene.sceneName;
            }
        }

        if (GUILayout.Button("加载室外默认数据（无镜像）", GUILayout.Height(28)))
        {
            if (selectedScene == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
                return;
            }
            manager.currentIsFlipped = false;
            manager.ApplySceneData(selectedScene.sceneId);
            AddLog($"✅ 加载室外默认数据: {selectedScene.sceneId} - {selectedScene.sceneName}");
        }

        if (GUILayout.Button("加载室外镜像数据", GUILayout.Height(28)))
        {
            if (selectedScene == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
                return;
            }
            manager.currentIsFlipped = true;
            manager.ApplySceneData(selectedScene.sceneId);
            AddLog($"✅ 加载室外镜像数据: {selectedScene.sceneId} - {selectedScene.sceneName}");
        }

        if (GUILayout.Button("加载室内数据", GUILayout.Height(28)))
        {
            manager.currentIsFlipped = false;
            manager.ApplySceneData(manager.CurrentSceneId);
            AddLog("✅ 加载室内数据");
        }

        if (GUILayout.Button("镜像元素默认显示", GUILayout.Height(28)))
        {
            if (selectedScene == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
                return;
            }

            var mirrorList = selectedScene.mirrorElements;
            if (mirrorList == null || mirrorList.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "当前场景没有镜像元素数据。", "确定");
                return;
            }

            int count = 0;
            foreach (var e in mirrorList)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                manager.SetControllerFlip(e.id, false);
                count++;
            }

            AddLog($"↩️ 镜像元素默认显示: {count} 个");
        }

        if (GUILayout.Button("镜像元素镜像显示", GUILayout.Height(28)))
        {
            if (selectedScene == null)
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
                return;
            }

            var mirrorList = selectedScene.mirrorElements;
            if (mirrorList == null || mirrorList.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "当前场景没有镜像元素数据。", "确定");
                return;
            }

            int count = 0;
            foreach (var e in mirrorList)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                manager.SetControllerFlip(e.id, true);
                count++;
            }

            AddLog($"🔁 镜像元素镜像显示: {count} 个");
        }
    }

    // ---------- 场景信息 ----------

    private void DrawSceneInfo()
    {
        if (selectedScene == null) return;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("=== 场景信息 ===", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("场景ID:", GUILayout.Width(60));
        EditorGUILayout.LabelField(selectedScene.sceneId, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("场景名称:", GUILayout.Width(60));
        string newName = EditorGUILayout.TextField(selectedScene.sceneName);
        if (newName != selectedScene.sceneName)
        {
            selectedScene.sceneName = newName;
            if (selectedScene.sceneId == manager.CurrentSceneId)
                manager.currentSceneName = newName;
            EditorUtility.SetDirty(manager);
            AddLog($"📝 场景名称已更新: {selectedScene.sceneId} -> {newName}");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(6);
    }

    // ---------- 室外元素 ----------

    private void DrawOutdoorElements()
    {
        if (selectedScene == null) return;
        int count = selectedScene.elements?.Count ?? 0;

        EditorGUILayout.BeginVertical("box");
        foldOutdoor = EditorGUILayout.Foldout(foldOutdoor,
            $"📦 室外元素（{count} 个）", true, EditorStyles.foldoutHeader);

        if (foldOutdoor)
        {
            if (selectedScene.elements == null || selectedScene.elements.Count == 0)
            {
                EditorGUILayout.HelpBox("无室外元素数据", MessageType.Info);
            }
            else
            {
                outdoorScroll = EditorGUILayout.BeginScrollView(outdoorScroll, GUILayout.Height(200));
                for (int i = 0; i < selectedScene.elements.Count; i++)
                    DrawElementItem(selectedScene.elements[i], i);
                EditorGUILayout.EndScrollView();
            }
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(4);
    }

    // ---------- 镜像元素 ----------

    private void DrawMirrorElements()
    {
        if (selectedScene == null) return;
        int count = selectedScene.mirrorElements?.Count ?? 0;

        EditorGUILayout.BeginVertical("box");
        foldMirror = EditorGUILayout.Foldout(foldMirror,
            $"🔁 镜像元素（{count} 个）", true, EditorStyles.foldoutHeader);

        if (foldMirror)
        {
            if (selectedScene.mirrorElements == null || selectedScene.mirrorElements.Count == 0)
            {
                EditorGUILayout.HelpBox("无镜像元素数据", MessageType.Info);
            }
            else
            {
                mirrorScroll = EditorGUILayout.BeginScrollView(mirrorScroll, GUILayout.Height(160));
                for (int i = 0; i < selectedScene.mirrorElements.Count; i++)
                    DrawElementItem(selectedScene.mirrorElements[i], i);
                EditorGUILayout.EndScrollView();
            }
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(4);
    }

    // ---------- 室内元素 ----------

    private void DrawIndoorElements()
    {
        var indoor = manager.GetIndoorElements();
        int count = indoor?.Count ?? 0;

        EditorGUILayout.BeginVertical("box");
        foldIndoor = EditorGUILayout.Foldout(foldIndoor,
            $"🏠 室内元素（{count} 个，全局唯一）", true, EditorStyles.foldoutHeader);

        if (foldIndoor)
        {
            if (indoor == null || indoor.Count == 0)
            {
                EditorGUILayout.HelpBox("无室内元素数据", MessageType.Info);
            }
            else
            {
                indoorScroll = EditorGUILayout.BeginScrollView(indoorScroll, GUILayout.Height(180));
                for (int i = 0; i < indoor.Count; i++)
                    DrawElementItem(indoor[i], i);
                EditorGUILayout.EndScrollView();
            }
        }
        EditorGUILayout.EndVertical();
        GUILayout.Space(6);
    }

    private void DrawElementItem(SceneElementData element, int index)
    {
        if (element == null) return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(30));
        EditorGUILayout.LabelField($"ID: {element.id}", EditorStyles.boldLabel, GUILayout.Width(160));
        EditorGUILayout.LabelField($"名称: {element.name}", GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();

        if (element.transform != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"位置: ({element.transform.position.x:F2}, {element.transform.position.y:F2}, {element.transform.position.z:F2})",
                GUILayout.Width(240));
            EditorGUILayout.LabelField(
                $"大小: ({element.transform.scale.x:F2}, {element.transform.scale.y:F2}, {element.transform.scale.z:F2})",
                GUILayout.Width(240));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(1);
    }

    // ---------- 操作区 ----------

    private void DrawOperationArea()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("=== 操作 ===", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        string path = Path.Combine("Assets/Addressables/", manager.sceneDataPath + ".json");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("JSON 路径:", GUILayout.Width(70));
        EditorGUILayout.SelectableLabel(path, EditorStyles.miniLabel, GUILayout.Height(18));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("保存室外默认数据", GUILayout.Height(28)))
            OnClickSave(SaveTarget.OutdoorDefault, "室外默认数据");

        if (GUILayout.Button("保存室外镜像数据", GUILayout.Height(28)))
            OnClickSave(SaveTarget.OutdoorMirror, "室外镜像数据");

        if (GUILayout.Button("保存室内数据", GUILayout.Height(28)))
            OnClickSave(SaveTarget.Indoor, "室内数据");

        EditorGUILayout.EndHorizontal();

        // ★ 新增：重置该场景
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
        if (GUILayout.Button("重置该场景数据", GUILayout.Height(28)))
            OnClickResetScene();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ---------- 保存 ----------

    private void OnClickSave(SaveTarget target, string typeName)
    {
        List<SceneElementData> rawList = GetTargetList(target);
        if (rawList == null)
        {
            EditorUtility.DisplayDialog("提示", "未找到目标数据列表。", "确定");
            return;
        }

        List<SceneElementData> oldList = DeepCopyList(rawList);
        if (oldList.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "当前模式下没有元素数据可保存。", "确定");
            return;
        }

        var changes = CollectChanges(oldList);
        if (changes.Count == 0)
        {
            EditorUtility.DisplayDialog("提示",
                "没有可对比的元素数据。\n" +
                "请先确认场景里有已注册的 SceneMatCtrl，且 JSON 中有对应元素。", "确定");
            return;
        }

        SceneChangePreviewWindow.Open(typeName, selectedScene?.sceneName ?? "室内",
            selectedScene?.sceneId ?? "-", changes, () =>
            {
                ApplyChanges(target, changes);
                manager.SaveToDefaultPath();
                AssetDatabase.Refresh();

                int changedCount = changes.Count(c => c.HasChange);
                int unchangedCount = changes.Count - changedCount;
                string path = Path.Combine("Assets/Addressables/", manager.sceneDataPath + ".json");
                string msg = $"✅ 保存成功\n\n" +
                             $"类型：{typeName}\n" +
                             $"元素数：{changes.Count} 个（有变化 {changedCount} 个 / 无变化 {unchangedCount} 个）\n\n" +
                             $"JSON 路径：\n{path}";
                EditorUtility.DisplayDialog("保存成功", msg, "确定");

                LoadSceneDataAndRefresh();
                AddLog($"💾 保存 {typeName}，有变化 {changedCount} 个");
            });
    }

    // ---------- 重置该场景 ----------

    private void OnClickResetScene()
    {
        if (selectedScene == null)
        {
            EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            "重置该场景数据",
            $"确定要重置场景 [{selectedScene.sceneId}] {selectedScene.sceneName} 吗？\n\n" +
            "操作内容：\n" +
            "1. 把 elements 恢复成场景里 SceneMatCtrl 的当前值\n" +
            "2. 清空 mirrorElements\n" +
            "3. 保存到 JSON\n\n" +
            "此操作不可撤销！",
            "确定重置", "取消");
        if (!ok) return;

        manager.CollectDataFromControllers();

        var scenes = manager.GetAllSceneData();
        if (scenes != null && selectedSceneIndex >= 0 && selectedSceneIndex < scenes.Count)
            selectedScene = scenes[selectedSceneIndex];

        if (selectedScene != null)
            selectedScene.mirrorElements = new List<SceneElementData>();

        manager.SaveToDefaultPath();
        AssetDatabase.Refresh();
        LoadSceneDataAndRefresh();

        AddLog($"🧹 重置场景数据: {selectedScene?.sceneId}");
        EditorUtility.DisplayDialog("重置完成",
            $"场景 [{selectedScene?.sceneId}] 已重置。\n" +
            "elements 已从场景收集，mirrorElements 已清空。", "确定");
    }

    private List<SceneElementData> GetTargetList(SaveTarget target)
    {
        if (target == SaveTarget.OutdoorDefault) return selectedScene?.elements;
        if (target == SaveTarget.OutdoorMirror) return selectedScene?.mirrorElements;
        if (target == SaveTarget.Indoor) return manager.GetIndoorElements();
        return null;
    }

    private List<SceneElementData> DeepCopyList(List<SceneElementData> source)
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

    private List<SceneElementChange> CollectChanges(List<SceneElementData> oldList)
    {
        var result = new List<SceneElementChange>();
        if (oldList == null) return result;

        foreach (var old in oldList)
        {
            if (old == null || string.IsNullOrEmpty(old.id)) continue;

            Transform t = manager.GetControllerTransform(old.id);
            if (t == null) continue;

            Vector3 pos = t.position;
            Vector3 scl = t.localScale;

            var newT = new SceneElementTransformData
            {
                position = SerializableVector3.FromUnityVector(pos.x, pos.y, pos.z),
                scale = SerializableVector3.FromUnityVector(scl.x, scl.y, scl.z)
            };

            var oldT = old.transform;

            var change = new SceneElementChange
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
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.z, b.z);
    }

    private void ApplyChanges(SaveTarget target, List<SceneElementChange> changes)
    {
        List<SceneElementData> targetList = GetTargetList(target);
        if (targetList == null) return;

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

    // ---------- 其他 ----------

    private async void LoadSceneDataAndRefresh()
    {
        await manager.LoadSceneData();
        LoadSceneOptions();
    }

    private void DrawOperationLog()
    {
        if (string.IsNullOrEmpty(operationLog)) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("=== 操作日志 ===", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(operationLog, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(60));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空日志", GUILayout.Width(80)))
            operationLog = "";
        EditorGUILayout.EndHorizontal();
    }

    private void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        operationLog = $"[{timestamp}] {message}\n" + operationLog;
        if (operationLog.Length > 2000) operationLog = operationLog.Substring(0, 2000);
    }
}

// ============================================================
// 2. 变更记录（公共）
// ============================================================

public class SceneElementChange
{
    public string id;
    public string name;
    public SceneElementTransformData oldT;
    public SceneElementTransformData newT;
    public bool HasChange;
    public bool PosChanged;
    public bool ScaleChanged;
}

public class SceneElementChangeProxy
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

// ============================================================
// 3. 变更预览窗
// ============================================================

public class SceneChangePreviewWindow : EditorWindow
{
    private string typeName;
    private string sceneName;
    private string sceneId;
    private List<SceneElementChangeProxy> changedList;
    private List<SceneElementChangeProxy> unchangedList;
    private Action onConfirm;

    private Vector2 scroll1;
    private Vector2 scroll2;

    public static SceneChangePreviewWindow Open(string typeName, string sceneName, string sceneId,
        List<SceneElementChange> changes, Action onConfirm)
    {
        var win = GetWindow<SceneChangePreviewWindow>(true, "变更预览", true);
        win.minSize = new Vector2(700, 600);
        win.typeName = typeName;
        win.sceneName = sceneName;
        win.sceneId = sceneId;
        win.onConfirm = onConfirm;

        win.changedList = new List<SceneElementChangeProxy>();
        win.unchangedList = new List<SceneElementChangeProxy>();

        foreach (var c in changes)
        {
            var proxy = new SceneElementChangeProxy
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
        EditorGUILayout.LabelField($"变更预览 - {typeName}", EditorStyles.boldLabel);
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

        var redStyle = new GUIStyle(EditorStyles.label);
        redStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);

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

        var grayStyle = new GUIStyle(EditorStyles.label);
        grayStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

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
