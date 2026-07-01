using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 场景材质编辑器
/// </summary>
[CustomEditor(typeof(SceneMatManager))]
public class SceneMatEditor : Editor
{
    private SceneMatManager manager;
    private SceneData selectedScene;
    private int selectedSceneIndex = -1;
    private string[] sceneOptions;
    private Vector2 scrollPosition;
    private Vector2 elementScrollPosition;

    private string operationLog = "";

    private void OnEnable()
    {
        manager = (SceneMatManager)target;
        LoadSceneOptions();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 场景数据编辑器 ===", EditorStyles.boldLabel);

        DrawSceneSelector();

        if (selectedScene != null)
        {
            DrawSceneDataEditor();
        }

        DrawActionButtons();
        DrawSaveButton();
        DrawOperationLog();
    }

    private void LoadSceneOptions()
    {
        if (manager == null) return;

        manager.LoadSceneData();

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
            LoadSceneOptions();
            if (sceneOptions == null || sceneOptions.Length == 0)
            {
                EditorGUILayout.HelpBox("请先加载场景数据", MessageType.Warning);
                return;
            }
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
                {
                    manager.currentSceneName = selectedScene.sceneName;
                }
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("切换场景", GUILayout.Height(30)))
        {
            if (selectedScene != null)
            {
                string sceneId = selectedScene.sceneId;
                manager.ApplySceneData(sceneId);
                string logMsg = $"✅ 切换到场景: {sceneId} - {selectedScene.sceneName}";
                AddLog(logMsg);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSceneDataEditor()
    {
        if (selectedScene == null) return;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("=== 场景详细信息 ===", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField($"场景ID: {selectedScene.sceneId}", EditorStyles.boldLabel);

        string newSceneName = EditorGUILayout.TextField("场景名称:", selectedScene.sceneName);
        if (newSceneName != selectedScene.sceneName)
        {
            selectedScene.sceneName = newSceneName;
            if (selectedScene.sceneId == manager.CurrentSceneId)
            {
                manager.currentSceneName = newSceneName;
            }
            EditorUtility.SetDirty(manager);
            AddLog($"📝 场景名称已更新: {selectedScene.sceneId} -> {newSceneName}");
        }

        EditorGUILayout.LabelField($"是否镜像: {(selectedScene.isFlipped ? "✅ 开启" : "❌ 关闭")}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"元素数量: {selectedScene.elements?.Count ?? 0}");
        EditorGUILayout.EndVertical();

        if (selectedScene.elements == null || selectedScene.elements.Count == 0)
        {
            EditorGUILayout.HelpBox("该场景没有元素数据", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"=== 元素列表 ({selectedScene.elements.Count} 个) ===", EditorStyles.boldLabel);

        elementScrollPosition = EditorGUILayout.BeginScrollView(elementScrollPosition, GUILayout.Height(300));

        for (int i = 0; i < selectedScene.elements.Count; i++)
        {
            var element = selectedScene.elements[i];
            DrawElementItem(element, i);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新场景列表（重新读取JSON）"))
        {
            LoadSceneOptions();
            AddLog("🔄 已重新读取JSON数据，刷新场景列表");
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawElementItem(SceneElementData element, int index)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(30));
        EditorGUILayout.LabelField($"ID: {element.id}", EditorStyles.boldLabel, GUILayout.Width(120));
        EditorGUILayout.LabelField($"名称: {element.name}", GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        if (element.transform != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"位置: ({element.transform.position.x:F2}, {element.transform.position.y:F2}, {element.transform.position.z:F2})", GUILayout.Width(200));
            EditorGUILayout.LabelField($"大小: ({element.transform.scale.x:F2}, {element.transform.scale.y:F2}, {element.transform.scale.z:F2})", GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 操作 ===", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("切换场景镜像", GUILayout.Height(25)))
        {
            if (selectedScene != null)
            {
                bool currentFlip = selectedScene.isFlipped;
                bool newFlip = !currentFlip;
                selectedScene.isFlipped = newFlip;
                manager.SetSceneFlip(newFlip);
                AddLog($"🔄 场景镜像切换为: {(newFlip ? "开启" : "关闭")}");
                EditorUtility.SetDirty(manager);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
            }
        }

        if (GUILayout.Button("从当前场景加载", GUILayout.Height(25)))
        {
            if (selectedScene != null)
            {
                manager.CollectDataFromControllers();
                manager.LoadSceneData();
                LoadSceneOptions();
                AddLog($"📥 已从当前场景加载数据: {selectedScene.sceneId}");
                EditorUtility.DisplayDialog("加载完成", $"已从场景 {selectedScene.sceneId} 加载数据", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个场景", "确定");
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSaveButton()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 保存 ===", EditorStyles.boldLabel);

        string defaultPath = Path.Combine("Assets/Resources", manager.sceneDataPath + ".json");
        EditorGUILayout.LabelField($"默认保存路径: {defaultPath}", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存到默认路径"))
        {
            SerializedProperty sceneNameProp = serializedObject.FindProperty("currentSceneName");
            if (sceneNameProp != null)
            {
                string inspectorName = sceneNameProp.stringValue;
                if (!string.IsNullOrEmpty(inspectorName))
                {
                    manager.currentSceneName = inspectorName;
                }
            }

            if (selectedScene != null)
            {
                selectedScene.sceneName = manager.currentSceneName;
            }

            manager.CollectDataFromControllers();
            manager.SaveToDefaultPath();
            AssetDatabase.Refresh();

            manager.LoadSceneData();
            LoadSceneOptions();

            string sceneInfo = "";
            if (selectedScene != null)
            {
                sceneInfo = $"场景: {selectedScene.sceneId} - {selectedScene.sceneName}, 元素: {selectedScene.elements?.Count ?? 0}, 镜像: {(selectedScene.isFlipped ? "开启" : "关闭")}";
            }

            AddLog($"💾 保存完成: {defaultPath} ({sceneInfo})");
            EditorUtility.DisplayDialog("保存成功", $"数据已保存到:\n{defaultPath}\n\n{sceneInfo}", "确定");
        }

        if (GUILayout.Button("另存为..."))
        {
            SaveSceneDataToJson();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawOperationLog()
    {
        if (string.IsNullOrEmpty(operationLog)) return;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 操作日志 ===", EditorStyles.boldLabel);

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

    private void AddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        operationLog = $"[{timestamp}] {message}\n" + operationLog;
        if (operationLog.Length > 2000)
        {
            operationLog = operationLog.Substring(0, 2000);
        }
    }

    private void SaveSceneDataToJson()
    {
        string path = EditorUtility.SaveFilePanel("保存场景数据", Application.dataPath, "mainTransData", "json");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (selectedScene != null && !string.IsNullOrEmpty(selectedScene.sceneName))
            {
                manager.currentSceneName = selectedScene.sceneName;
            }

            string sceneInfo = "";
            if (selectedScene != null)
            {
                sceneInfo = $"场景: {selectedScene.sceneId} - {selectedScene.sceneName}, 元素: {selectedScene.elements?.Count ?? 0}, 镜像: {(selectedScene.isFlipped ? "开启" : "关闭")}";
            }

            manager.CollectDataFromControllers();
            manager.SaveSceneDataToFile(path);
            AssetDatabase.Refresh();

            manager.LoadSceneData();
            LoadSceneOptions();

            AddLog($"💾 另存为: {path} ({sceneInfo})");
            EditorUtility.DisplayDialog("保存成功", $"数据已保存到:\n{path}\n\n{sceneInfo}", "确定");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("保存失败", $"保存数据时出错:\n{e.Message}", "确定");
        }
    }
}