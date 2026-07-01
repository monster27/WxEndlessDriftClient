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
    private SceneMatManager.SceneData selectedScene;
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

        // 场景选择下拉框
        int newIndex = EditorGUILayout.Popup("选择场景", selectedSceneIndex, sceneOptions);
        if (newIndex != selectedSceneIndex)
        {
            selectedSceneIndex = newIndex;
            var scenes = manager.GetAllSceneData();
            if (scenes != null && selectedSceneIndex >= 0 && selectedSceneIndex < scenes.Count)
            {
                selectedScene = scenes[selectedSceneIndex];
                // ✅ 同步更新 currentSceneName
                if (selectedScene != null)
                {
                    manager.currentSceneName = selectedScene.sceneName;
                }
            }
        }

        // ===== 切换场景按钮 =====
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("切换场景", GUILayout.Height(30)))
        {
            if (selectedScene != null)
            {
                string sceneId = selectedScene.sceneId;

                Debug.Log($"[SceneMatEditor] ===== 切换场景: {sceneId} =====");
                Debug.Log($"[SceneMatEditor] 场景名称: {selectedScene.sceneName}");
                Debug.Log($"[SceneMatEditor] 场景镜像: {selectedScene.isFlipped}");
                Debug.Log($"[SceneMatEditor] 元素数量: {selectedScene.elements?.Count ?? 0}");

                // 打印前3个元素的位置信息
                if (selectedScene.elements != null)
                {
                    for (int i = 0; i < Math.Min(selectedScene.elements.Count, 5); i++)
                    {
                        var elem = selectedScene.elements[i];
                        if (elem.transform != null)
                        {
                            Debug.Log($"[SceneMatEditor] 元素 {i + 1}: {elem.id}, 位置=({elem.transform.position.x:F2}, {elem.transform.position.y:F2}, {elem.transform.position.z:F2}), 大小=({elem.transform.scale.x:F2}, {elem.transform.scale.y:F2}, {elem.transform.scale.z:F2})");
                        }
                    }
                }

                // ===== 应用场景数据（位置和大小） =====
                manager.ApplySceneData(sceneId);

                string logMsg = $"✅ 切换到场景: {sceneId} - {selectedScene.sceneName}，加载了 {selectedScene.elements?.Count ?? 0} 个元素，镜像: {(selectedScene.isFlipped ? "开启" : "关闭")}";
                AddLog(logMsg);
                Debug.Log($"[SceneMatEditor] {logMsg}");
                Debug.Log($"[SceneMatEditor] ===== 切换完成 =====");
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

        // ===== 显示完整的场景信息 =====
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("=== 场景详细信息 ===", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField($"场景ID: {selectedScene.sceneId}", EditorStyles.boldLabel);

        // ✅ 添加场景名称编辑框
        string newSceneName = EditorGUILayout.TextField("场景名称:", selectedScene.sceneName);
        if (newSceneName != selectedScene.sceneName)
        {
            selectedScene.sceneName = newSceneName;
            // ✅ 同步更新 currentSceneName
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

        // ===== 显示元素列表 =====
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

    private void DrawElementItem(SceneMatManager.SceneElementData element, int index)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);

        // 第一行：ID 和 名称
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"#{index + 1}", GUILayout.Width(30));
        EditorGUILayout.LabelField($"ID: {element.id}", EditorStyles.boldLabel, GUILayout.Width(120));
        EditorGUILayout.LabelField($"名称: {element.name}", GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        // 第二行：位置和大小
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

        // ===== 切换场景镜像 =====
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("切换场景镜像", GUILayout.Height(25)))
        {
            if (selectedScene != null)
            {
                bool currentFlip = selectedScene.isFlipped;
                bool newFlip = !currentFlip;

                // 更新场景数据
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

        // ✅ 添加从当前场景加载按钮
        if (GUILayout.Button("从当前场景加载", GUILayout.Height(25)))
        {
            if (selectedScene != null)
            {
                // 从当前场景的控制器加载数据
                manager.CollectDataFromControllers();
                // 重新加载场景数据
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
            // ✅ 关键修复：从 Inspector 中读取 currentSceneName 的值
            SerializedProperty sceneNameProp = serializedObject.FindProperty("currentSceneName");
            if (sceneNameProp != null)
            {
                string inspectorName = sceneNameProp.stringValue;
                if (!string.IsNullOrEmpty(inspectorName))
                {
                    manager.currentSceneName = inspectorName;
                    Debug.Log($"[SceneMatEditor] 从 Inspector 读取场景名称: {inspectorName}");
                }
            }

            // ✅ 同步更新选中的场景数据
            if (selectedScene != null)
            {
                selectedScene.sceneName = manager.currentSceneName;
            }

            // 先收集控制器数据
            manager.CollectDataFromControllers();

            // 保存到文件
            manager.SaveToDefaultPath();
            AssetDatabase.Refresh();

            // 重新加载数据
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
            // ✅ 先确保场景名称已同步
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

            // ✅ 重新加载数据以刷新列表
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