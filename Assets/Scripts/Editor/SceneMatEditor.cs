using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 场景材质编辑器 - 用于编辑场景数据
/// </summary>
[CustomEditor(typeof(SceneMatManager))]
public class SceneMatEditor : Editor
{
    private SceneMatManager manager;
    private SceneMatManager.SceneData selectedScene;
    private int selectedSceneIndex = -1;
    private string[] sceneOptions;
    private Vector2 scrollPosition;

    // 临时编辑数据
    private SceneMatManager.SceneElementData editingElement;
    private int editingElementIndex = -1;
    private bool isEditingElement = false;

    // 渲染层级选项
    private string[] levelOptions = { "Background", "Environment", "Character", "Foreground", "UI" };

    // 编辑用的临时位置和缩放值（因为SceneElementData使用的是Vector3）
    private Vector3 tempPosition;
    private Vector3 tempScale;

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

        // 场景选择
        DrawSceneSelector();

        if (selectedScene != null)
        {
            DrawSceneDataEditor();
        }

        // 操作按钮
        DrawActionButtons();

        // 数据验证
        DrawDataValidation();

        // 保存按钮
        DrawSaveButton();
    }

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

        if (selectedSceneIndex >= 0 && selectedSceneIndex < scenes.Count)
        {
            selectedScene = scenes[selectedSceneIndex];
        }
        else if (scenes.Count > 0)
        {
            selectedScene = scenes[0];
            selectedSceneIndex = 0;
        }
    }

    private void DrawSceneSelector()
    {
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
            }
        }
    }

    private void DrawSceneDataEditor()
    {
        EditorGUILayout.LabelField($"场景: {selectedScene.sceneName} (ID: {selectedScene.sceneId})", EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"元素数量: {selectedScene.elements?.Count ?? 0}", EditorStyles.miniLabel);

        if (selectedScene.elements == null || selectedScene.elements.Count == 0)
        {
            EditorGUILayout.HelpBox("该场景没有元素数据", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));

        for (int i = 0; i < selectedScene.elements.Count; i++)
        {
            var element = selectedScene.elements[i];
            DrawElementItem(element, i);
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("添加新元素"))
        {
            AddNewElement();
        }

        if (isEditingElement && editingElement != null)
        {
            DrawElementEditor();
        }
    }

    private void DrawElementItem(SceneMatManager.SceneElementData element, int index)
    {
        EditorGUILayout.BeginHorizontal();

        string displayText = $"{element.id}: {element.name}";
        EditorGUILayout.LabelField(displayText, GUILayout.Width(150));

        // 使用 position 和 scale
        EditorGUILayout.LabelField($"位置: ({element.position.x:F2}, {element.position.y:F2}, {element.position.z:F2})", GUILayout.Width(150));

        EditorGUILayout.LabelField($"镜像: {(element.isFlipped ? "是" : "否")}", GUILayout.Width(60));

        if (GUILayout.Button("编辑", GUILayout.Width(50)))
        {
            StartEditingElement(index);
        }

        if (GUILayout.Button("删除", GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("删除元素", $"确定要删除元素 {element.id}: {element.name} 吗？", "确定", "取消"))
            {
                selectedScene.elements.RemoveAt(index);
                EditorUtility.SetDirty(manager);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void StartEditingElement(int index)
    {
        if (selectedScene.elements == null || index >= selectedScene.elements.Count) return;

        editingElementIndex = index;
        editingElement = new SceneMatManager.SceneElementData();
        CopyElementData(selectedScene.elements[index], editingElement);

        // 初始化临时位置和缩放
        tempPosition = editingElement.position;
        tempScale = editingElement.scale;

        isEditingElement = true;
    }

    private void CopyElementData(SceneMatManager.SceneElementData source, SceneMatManager.SceneElementData target)
    {
        target.id = source.id;
        target.name = source.name;
        target.imagePath = source.imagePath;
        target.position = source.position;
        target.scale = source.scale;
        target.renderLevel = source.renderLevel;
        target.isFlipped = source.isFlipped;
        target.isLockFlip = source.isLockFlip;
        target.sceneId = source.sceneId;
    }

    private void DrawElementEditor()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 编辑元素 ===", EditorStyles.boldLabel);

        if (editingElement == null) return;

        // 元素类型下拉选择
        string[] elementTypeNames = Enum.GetNames(typeof(SceneMatManager.ElementType));
        int currentTypeIndex = Array.IndexOf(elementTypeNames, editingElement.id);
        if (currentTypeIndex < 0) currentTypeIndex = 0;
        int newTypeIndex = EditorGUILayout.Popup("元素类型", currentTypeIndex, elementTypeNames);
        editingElement.id = elementTypeNames[newTypeIndex];

        editingElement.name = EditorGUILayout.TextField("名称", editingElement.name);
        editingElement.imagePath = EditorGUILayout.TextField("图片路径", editingElement.imagePath);

        EditorGUILayout.LabelField("位置", EditorStyles.boldLabel);
        tempPosition.x = EditorGUILayout.FloatField("X", tempPosition.x);
        tempPosition.y = EditorGUILayout.FloatField("Y", tempPosition.y);
        tempPosition.z = EditorGUILayout.FloatField("Z", tempPosition.z);

        EditorGUILayout.LabelField("大小", EditorStyles.boldLabel);
        tempScale.x = EditorGUILayout.FloatField("X", tempScale.x);
        tempScale.y = EditorGUILayout.FloatField("Y", tempScale.y);
        tempScale.z = EditorGUILayout.FloatField("Z", tempScale.z);

        // 更新编辑元素的position和scale
        editingElement.position = tempPosition;
        editingElement.scale = tempScale;

        int currentLevelIndex = Array.IndexOf(levelOptions, editingElement.renderLevel);
        if (currentLevelIndex < 0) currentLevelIndex = 1;
        int newLevelIndex = EditorGUILayout.Popup("渲染层级", currentLevelIndex, levelOptions);
        editingElement.renderLevel = levelOptions[newLevelIndex];

        editingElement.isFlipped = EditorGUILayout.Toggle("是否镜像", editingElement.isFlipped);
        editingElement.isLockFlip = EditorGUILayout.Toggle("锁定镜像", editingElement.isLockFlip);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存修改"))
        {
            SaveElementEdit();
        }

        if (GUILayout.Button("取消"))
        {
            isEditingElement = false;
            editingElement = null;
            editingElementIndex = -1;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void SaveElementEdit()
    {
        if (selectedScene.elements == null)
        {
            selectedScene.elements = new List<SceneMatManager.SceneElementData>();
        }

        if (editingElementIndex < 0 || editingElementIndex >= selectedScene.elements.Count)
        {
            selectedScene.elements.Add(editingElement);
        }
        else
        {
            CopyElementData(editingElement, selectedScene.elements[editingElementIndex]);
        }

        isEditingElement = false;
        editingElement = null;
        editingElementIndex = -1;
        EditorUtility.SetDirty(manager);
    }

    private void AddNewElement()
    {
        string[] elementTypeNames = Enum.GetNames(typeof(SceneMatManager.ElementType));

        editingElement = new SceneMatManager.SceneElementData
        {
            id = elementTypeNames.Length > 0 ? elementTypeNames[0] : "NewElement",
            name = "新元素",
            imagePath = "",
            position = Vector3.zero,
            scale = Vector3.one,
            renderLevel = "Environment",
            isFlipped = false,
            isLockFlip = false,
            sceneId = ""
        };

        tempPosition = editingElement.position;
        tempScale = editingElement.scale;

        editingElementIndex = -1;
        isEditingElement = true;
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 操作 ===", EditorStyles.boldLabel);

        if (GUILayout.Button("查找并注册所有控制器"))
        {
            manager.FindAndRegisterAllControllers();
            EditorUtility.SetDirty(manager);
        }

        if (GUILayout.Button("从控制器同步数据"))
        {
            SyncFromControllers();
        }

        if (GUILayout.Button("验证场景数据"))
        {
            ValidateSceneData();
        }

        EditorGUILayout.BeginHorizontal();
        string testSceneId = EditorGUILayout.TextField("校验场景ID", manager.CurrentSceneId);
        if (GUILayout.Button("校验", GUILayout.Width(60)))
        {
            CheckSceneId(testSceneId);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDataValidation()
    {
        EditorGUILayout.Space(10);
        if (GUILayout.Button("显示数据校验结果"))
        {
            ValidateAndShowResults();
        }
    }

    private void DrawSaveButton()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("=== 保存 ===", EditorStyles.boldLabel);

        if (GUILayout.Button("保存场景数据到JSON"))
        {
            SaveSceneDataToJson();
        }
    }

    // ========== 功能方法 ==========

    private void SyncFromControllers()
    {
        var controllers = manager.GetAllControllers();
        if (controllers == null || controllers.Count == 0)
        {
            manager.FindAndRegisterAllControllers();
            controllers = manager.GetAllControllers();

            if (controllers == null || controllers.Count == 0)
            {
                EditorUtility.DisplayDialog("同步数据", "没有找到控制器", "确定");
                return;
            }
        }

        if (selectedScene == null)
        {
            EditorUtility.DisplayDialog("同步数据", "请先选择一个场景", "确定");
            return;
        }

        selectedScene.elements.Clear();

        foreach (var controller in controllers)
        {
            if (controller == null) continue;

            var element = new SceneMatManager.SceneElementData
            {
                id = controller.ElementId.ToString(),
                name = controller.gameObject.name,
                imagePath = controller.ElementPath,
                position = controller.transform.position,
                scale = controller.transform.localScale,
                renderLevel = controller.RenderQueue.ToString(),
                isFlipped = controller.IsFlipped,
                isLockFlip = controller.IsLockFlip,
                sceneId = manager.CurrentSceneId
            };

            selectedScene.elements.Add(element);
        }

        EditorUtility.SetDirty(manager);
        EditorUtility.DisplayDialog("同步数据", $"成功同步 {controllers.Count} 个控制器的数据", "确定");
    }

    private void ValidateSceneData()
    {
        if (selectedScene == null || selectedScene.elements == null)
        {
            EditorUtility.DisplayDialog("数据验证", "没有可验证的数据", "确定");
            return;
        }

        List<string> errors = new List<string>();
        List<string> warnings = new List<string>();

        var validTypes = Enum.GetNames(typeof(SceneMatManager.ElementType));

        foreach (var element in selectedScene.elements)
        {
            if (!Array.Exists(validTypes, t => t == element.id))
            {
                warnings.Add($"元素 \"{element.id}\" 不是有效的 ElementType");
            }
        }

        var idGroups = selectedScene.elements.GroupBy(e => e.id);
        foreach (var group in idGroups)
        {
            if (group.Count() > 1)
            {
                errors.Add($"重复的ID: {group.Key} (出现 {group.Count()} 次)");
            }
        }

        foreach (var element in selectedScene.elements)
        {
            if (string.IsNullOrEmpty(element.id))
            {
                errors.Add($"元素 \"{element.name}\" 的ID为空");
            }

            if (string.IsNullOrEmpty(element.imagePath))
            {
                warnings.Add($"元素 {element.id} 的图片路径为空");
            }

            if (element.scale.x == 0 || element.scale.y == 0 || element.scale.z == 0)
            {
                warnings.Add($"元素 {element.id} 的scale值为0，可能无法显示");
            }

            if (!Array.Exists(levelOptions, t => t == element.renderLevel))
            {
                warnings.Add($"元素 {element.id} 的渲染层级 \"{element.renderLevel}\" 无效");
            }
        }

        string message = $"验证完成！\n\n";
        if (errors.Count > 0)
        {
            message += $"错误 ({errors.Count}):\n{string.Join("\n", errors)}\n\n";
        }
        if (warnings.Count > 0)
        {
            message += $"警告 ({warnings.Count}):\n{string.Join("\n", warnings)}\n\n";
        }
        if (errors.Count == 0 && warnings.Count == 0)
        {
            message += "所有数据验证通过！";
        }

        EditorUtility.DisplayDialog("数据验证结果", message, "确定");
    }

    private void ValidateAndShowResults()
    {
        ValidateSceneData();
    }

    private void CheckSceneId(string sceneId)
    {
        if (string.IsNullOrEmpty(sceneId))
        {
            EditorUtility.DisplayDialog("校验结果", "请输入场景ID", "确定");
            return;
        }

        var sceneData = manager.GetSceneData(sceneId);
        if (sceneData == null)
        {
            EditorUtility.DisplayDialog("校验结果", $"未找到场景ID: {sceneId}", "确定");
            return;
        }

        var currentScene = selectedScene;
        if (currentScene != null && currentScene.sceneId == sceneId)
        {
            EditorUtility.DisplayDialog("校验结果", $"场景ID {sceneId} 一致！\n场景名称: {sceneData.sceneName}\n元素数量: {sceneData.elements?.Count ?? 0}", "确定");
            return;
        }

        string message = $"=== 场景信息 ===\n";
        message += $"场景ID: {sceneId}\n";
        message += $"场景名称: {sceneData.sceneName}\n";
        message += $"元素数量: {sceneData.elements?.Count ?? 0}\n\n";

        message += "=== 元素列表 ===\n";
        if (sceneData.elements != null)
        {
            foreach (var element in sceneData.elements)
            {
                message += $"- {element.id}: {element.name}\n";
                message += $"  位置: ({element.position.x:F2}, {element.position.y:F2}, {element.position.z:F2})\n";
                message += $"  大小: ({element.scale.x:F2}, {element.scale.y:F2}, {element.scale.z:F2})\n";
                message += $"  渲染层级: {element.renderLevel}\n";
                message += $"  镜像: {(element.isFlipped ? "是" : "否")}\n\n";
            }
        }

        EditorUtility.DisplayDialog("场景信息", message, "确定");
    }

    private void SaveSceneDataToJson()
    {
        string path = EditorUtility.SaveFilePanel("保存场景数据", Application.dataPath, "mainTransData", "json");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            manager.SaveSceneDataToFile(path);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("保存成功", $"数据已保存到:\n{path}", "确定");
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("保存失败", $"保存数据时出错:\n{e.Message}", "确定");
        }
    }
}