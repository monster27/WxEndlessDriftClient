#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using SharedModels;

/// <summary>
/// 完整钓鱼技能编辑器（ID范围：3001-3399）
/// </summary>
public class FishingComponentJsonEditor : EditorWindow
{
    private const string RELATIVE_PATH = "Resources/JsonData/Ability/fishing_components.json";
    private const string ABILITIES_PATH = "Resources/JsonData/Ability/abilities.json";

    private List<FishingComponentConfig> componentList = new List<FishingComponentConfig>();
    private List<AbilityItem> abilityList = new List<AbilityItem>();

    private int selectedComponentId = -1;
    private int editingComponentId = -1;

    private enum EditMode { List, Edit }
    private EditMode currentMode = EditMode.List;

    private string searchText = "";
    private int selectedCategoryFilter = 0;

    private Vector2 listScrollPosition = Vector2.zero;
    private Vector2 editScrollPosition = Vector2.zero;
    private Vector2 previewScrollPosition = Vector2.zero;

    private int newId = 3001;
    private string newName = "新钓鱼技能";
    private int newCategoryIndex = 4;
    private int newMaxLevel = 10;

    private readonly string[] categoryNames = { "全部", "钓竿", "钓线", "钓钩", "技能" };
    private readonly int[] categoryStartIds = { 0, 3001, 3101, 3201, 3301 };
    private readonly int[] categoryEndIds = { 0, 3099, 3199, 3299, 3399 };

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/3001_钓具与技能")]
    public static void ShowWindow()
    {
        var window = GetWindow<FishingComponentJsonEditor>("钓具与技能编辑器");
        window.minSize = new Vector2(1000, 700);
        window.Show();
    }
    private void OnGUI()
    {
        if (currentMode == EditMode.List)
            DrawListMode();
        else
            DrawEditMode();
    }
    private void OnEnable()
    {
        LoadAbilities();
        LoadData();
    }

    private void LoadAbilities()
    {
        string fullPath = Path.Combine(Application.dataPath, ABILITIES_PATH);
        abilityList = new List<AbilityItem>();

        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var wrapper = JsonUtility.FromJson<AbilityItemListWrapper>(json);
                if (wrapper != null && wrapper.abilities != null)
                {
                    abilityList = wrapper.abilities.ToList();
                    Debug.Log($"成功加载 {abilityList.Count} 个单一能力");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"加载能力配置失败: {e.Message}");
            }
        }
    }

    private void DrawListMode()
    {
        DrawTopToolbar();
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(520));
        DrawSearchFilter();
        DrawQuickCreate();
        DrawComponentList();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        DrawDetailPreview();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("🐟 钓鱼技能编辑器 (ID: 3001-3399)", EditorStyles.boldLabel, GUILayout.Width(200));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            LoadAbilities();
            LoadData();
        }
        if (GUILayout.Button("➕ 新增", EditorStyles.toolbarButton, GUILayout.Width(70)))
            QuickCreateComponent();
        if (GUILayout.Button("📥 预设", EditorStyles.toolbarButton, GUILayout.Width(70)))
            ShowPresetMenu();

        EditorGUILayout.LabelField($"共{componentList.Count}条", EditorStyles.toolbarButton, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawSearchFilter()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("搜索筛选", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        searchText = EditorGUILayout.TextField("", searchText, "SearchTextField", GUILayout.Height(20));
        if (GUILayout.Button("×", GUILayout.Width(25))) searchText = "";
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("类别:", GUILayout.Width(35));
        selectedCategoryFilter = EditorGUILayout.Popup(selectedCategoryFilter, categoryNames, GUILayout.Height(20));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawQuickCreate()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("快速创建", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(25));
        newId = EditorGUILayout.IntField(newId, GUILayout.Width(60));

        if (newId < 3001 || newId > 3399)
        {
            GUI.backgroundColor = Color.red;
            EditorGUILayout.LabelField("❌ ID应在3001-3399范围内", GUILayout.Width(150));
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = Color.green;
            EditorGUILayout.LabelField("✓", GUILayout.Width(20));
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.LabelField("名称:", GUILayout.Width(35));
        newName = EditorGUILayout.TextField(newName, GUILayout.Width(140));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("类别:", GUILayout.Width(35));
        newCategoryIndex = EditorGUILayout.Popup(newCategoryIndex - 1, new string[] { "钓竿", "钓线", "钓钩", "技能" }) + 1;

        EditorGUILayout.LabelField($"范围: {categoryStartIds[newCategoryIndex]}-{categoryEndIds[newCategoryIndex]}", GUILayout.Width(130));

        EditorGUILayout.LabelField("等级:", GUILayout.Width(35));
        newMaxLevel = EditorGUILayout.IntSlider(newMaxLevel, 1, 20, GUILayout.Width(120));

        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
        if (GUILayout.Button("创建", GUILayout.Width(50)))
            QuickCreateComponent();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private List<FishingComponentConfig> GetFilteredList()
    {
        var filtered = componentList.AsEnumerable();

        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(c => c.name.Contains(searchText) || c.id.ToString().Contains(searchText));
        }

        if (selectedCategoryFilter > 0)
        {
            filtered = filtered.Where(c => (int)c.category == selectedCategoryFilter);
        }

        return filtered.ToList();
    }

    private void DrawComponentList()
    {
        EditorGUILayout.LabelField("技能列表", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("图标", EditorStyles.toolbarButton, GUILayout.Width(35));
        EditorGUILayout.LabelField("ID/名称", EditorStyles.toolbarButton, GUILayout.Width(180));
        EditorGUILayout.LabelField("类别", EditorStyles.toolbarButton, GUILayout.Width(60));
        EditorGUILayout.LabelField("等级", EditorStyles.toolbarButton, GUILayout.Width(50));
        EditorGUILayout.LabelField("操作", EditorStyles.toolbarButton, GUILayout.Width(95));
        EditorGUILayout.EndHorizontal();

        listScrollPosition = EditorGUILayout.BeginScrollView(listScrollPosition, GUILayout.ExpandHeight(true));

        var filteredList = GetFilteredList();

        int lastCategory = -1;
        for (int i = 0; i < filteredList.Count; i++)
        {
            var item = filteredList[i];
            int currentCategory = (int)item.category;

            if (lastCategory != -1 && currentCategory != lastCategory)
            {
                DrawCategorySeparator(currentCategory);
            }

            DrawComponentItem(item);
            lastCategory = currentCategory;
        }

        if (filteredList.Count == 0)
            EditorGUILayout.LabelField("暂无数据", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(50));

        EditorGUILayout.EndScrollView();
    }

    private void DrawCategorySeparator(int categoryIndex)
    {
        string categoryName = categoryIndex switch
        {
            1 => "🎣 钓竿 (3001-3099)",
            2 => "🧵 钓线 (3101-3199)",
            3 => "🪝 钓钩 (3201-3299)",
            4 => "✨ 技能 (3301-3399)",
            _ => "其他"
        };

        EditorGUILayout.Space(3);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField("", GUILayout.Height(1));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        EditorGUILayout.LabelField(categoryName, EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField("", GUILayout.Height(1));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    private void DrawComponentItem(FishingComponentConfig item)
    {
        bool isSelected = selectedComponentId == item.id;

        EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box", GUILayout.Height(26));

        EditorGUILayout.LabelField(GetCategoryIcon(item.category), GUILayout.Width(35));

        string displayName = $"[{item.id}] {item.name}";
        if (displayName.Length > 20) displayName = displayName.Substring(0, 18) + "..";
        EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel, GUILayout.Width(180));

        var categoryStyle = new GUIStyle(EditorStyles.miniLabel);
        categoryStyle.fontSize = 10;
        categoryStyle.padding = new RectOffset(3, 3, 2, 2);
        categoryStyle.normal.textColor = Color.white;
        categoryStyle.alignment = TextAnchor.MiddleCenter;
        GUI.backgroundColor = GetCategoryColor(item.category);
        EditorGUILayout.LabelField(GetCategoryName(item.category), categoryStyle, GUILayout.Width(60));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField($"Lv.{item.maxLevel}", GUILayout.Width(50));

        EditorGUILayout.BeginHorizontal(GUILayout.Width(95));

        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("编辑", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            editingComponentId = item.id;
            currentMode = EditMode.Edit;
        }
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定删除 [{item.id}] {item.name} 吗？", "删除", "取消"))
            {
                componentList.Remove(item);
                if (selectedComponentId == item.id) selectedComponentId = -1;
                SaveData();
                Repaint();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndHorizontal();

        Rect lastRect = GUILayoutUtility.GetLastRect();
        if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
        {
            selectedComponentId = item.id;
            Event.current.Use();
            Repaint();
        }
    }

    private FishingComponentConfig GetComponentById(int id)
    {
        return componentList.Find(c => c.id == id);
    }

    private void DrawDetailPreview()
    {
        var component = GetComponentById(selectedComponentId);

        if (component == null)
        {
            EditorGUILayout.LabelField("请从左侧列表选择一个技能", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField("技能详情", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(GetCategoryIcon(component.category), GUILayout.Width(30));
        EditorGUILayout.LabelField($"ID: {component.id}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"名称: {component.name}", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"类别: {GetCategoryName(component.category)}", GUILayout.Width(150));
        EditorGUILayout.LabelField($"最大等级: {component.maxLevel}", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        EditorGUILayout.LabelField($"等级参数列表 (共{component.levelDataList?.Count ?? 0}级)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("等级", EditorStyles.toolbarButton, GUILayout.Width(50));
        EditorGUILayout.LabelField("参数1 (能力ID:值)", EditorStyles.toolbarButton, GUILayout.Width(150));
        EditorGUILayout.LabelField("参数2 (能力ID:值)", EditorStyles.toolbarButton, GUILayout.Width(150));
        EditorGUILayout.LabelField("参数3 (能力ID:值)", EditorStyles.toolbarButton, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(350));

        if (component.levelDataList != null)
        {
            for (int i = 0; i < component.levelDataList.Count; i++)
            {
                var levelData = component.levelDataList[i];
                EditorGUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                EditorGUILayout.LabelField($"Lv.{levelData.level}", GUILayout.Width(50));

                if (levelData.paramsList != null)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (j < levelData.paramsList.Count && levelData.paramsList[j].paramId != 0)
                        {
                            var param = levelData.paramsList[j];
                            string abilityName = GetAbilityName(param.paramId);
                            string displayText = $"{param.paramId}:{param.value:F2}";
                            if (!string.IsNullOrEmpty(abilityName))
                            {
                                displayText = $"{abilityName}({param.value:F2})";
                            }
                            EditorGUILayout.LabelField(displayText, GUILayout.Width(150));
                        }
                        else
                        {
                            EditorGUILayout.LabelField("-", GUILayout.Width(150));
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("无参数", GUILayout.Width(450));
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("✏️ 编辑技能", GUILayout.Width(100)))
        {
            editingComponentId = component.id;
            currentMode = EditMode.Edit;
        }
        GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
        if (GUILayout.Button("📋 复制技能", GUILayout.Width(100)))
        {
            DuplicateComponent(component);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    #region Edit Mode

    private void DrawEditMode()
    {
        var component = GetComponentById(editingComponentId);

        if (component == null)
        {
            currentMode = EditMode.List;
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("← 返回列表", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            currentMode = EditMode.List;
            SaveData();
            Repaint();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.LabelField($"编辑: [{component.id}] {component.name}", EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("💾 保存", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            SaveData();
            EditorUtility.DisplayDialog("保存成功", "数据已保存！", "确定");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        editScrollPosition = EditorGUILayout.BeginScrollView(editScrollPosition);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("基本信息", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(60));
        EditorGUILayout.LabelField(component.id.ToString());

        int categoryIdx = (int)component.category;
        if (component.id < categoryStartIds[categoryIdx] || component.id > categoryEndIds[categoryIdx])
        {
            GUI.backgroundColor = Color.red;
            EditorGUILayout.LabelField($"⚠️ 建议ID范围: {categoryStartIds[categoryIdx]}-{categoryEndIds[categoryIdx]}", GUILayout.Width(200));
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("名称:", GUILayout.Width(60));
        component.name = EditorGUILayout.TextField(component.name);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("类别:", GUILayout.Width(60));
        int currentCatIndex = (int)component.category;
        int newCatIndex = EditorGUILayout.Popup(currentCatIndex - 1, new string[] { "钓竿", "钓线", "钓钩", "技能" }) + 1;
        if (newCatIndex != currentCatIndex)
        {
            component.category = (FishingComponentCategory)newCatIndex;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("最大等级:", GUILayout.Width(60));
        int newMaxLevel = EditorGUILayout.IntSlider(component.maxLevel, 1, 20);
        if (newMaxLevel != component.maxLevel)
        {
            AdjustLevelDataCount(component, newMaxLevel);
            component.maxLevel = newMaxLevel;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("等级参数配置 (能力ID参考: 701-713)", EditorStyles.boldLabel);

        DrawBatchSettings(component);
        GUILayout.Space(10);

        for (int i = 0; i < component.levelDataList.Count; i++)
        {
            DrawLevelEditor(component.levelDataList[i], i);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private void DrawBatchSettings(FishingComponentConfig component)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField("批量设置:", GUILayout.Width(60));
        batchStartLevel = EditorGUILayout.IntField(batchStartLevel, GUILayout.Width(50));
        EditorGUILayout.LabelField("到", GUILayout.Width(20));
        batchEndLevel = EditorGUILayout.IntField(batchEndLevel, GUILayout.Width(50));

        EditorGUILayout.LabelField("能力ID:", GUILayout.Width(55));
        batchParam1Id = EditorGUILayout.IntField(batchParam1Id, GUILayout.Width(60));
        string abilityHint = GetAbilityName(batchParam1Id);
        if (!string.IsNullOrEmpty(abilityHint))
        {
            EditorGUILayout.LabelField($"({abilityHint})", GUILayout.Width(100));
        }

        EditorGUILayout.LabelField("值:", GUILayout.Width(25));
        batchParam1Value = EditorGUILayout.FloatField(batchParam1Value, GUILayout.Width(60));

        if (GUILayout.Button("应用", GUILayout.Width(60)))
        {
            ApplyBatchSettings(component);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLevelEditor(FishingComponentLevelData levelData, int index)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField($"等级 {levelData.level}", EditorStyles.boldLabel);

        EnsureParamsList(levelData);

        DrawParamField("参数1", levelData.paramsList[0]);
        DrawParamField("参数2", levelData.paramsList[1]);
        DrawParamField("参数3", levelData.paramsList[2]);

        GUILayout.Space(5);
        EditorGUILayout.LabelField("等级描述:", GUILayout.Width(60));
        levelData.levelDescription = EditorGUILayout.TextArea(levelData.levelDescription, GUILayout.Height(60));

        GUILayout.Space(5);
        EditorGUILayout.LabelField("升级效果:", GUILayout.Width(60));
        levelData.upgradeDescription = EditorGUILayout.TextField(levelData.upgradeDescription);

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("升级费用:", GUILayout.Width(60));
        levelData.upgradeCost = EditorGUILayout.IntField(levelData.upgradeCost);
        EditorGUILayout.LabelField("金币", GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawParamField(string label, FishingComponentParam param)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{label} 能力ID:", GUILayout.Width(70));
        param.paramId = EditorGUILayout.IntField(param.paramId, GUILayout.Width(60));

        string abilityName = GetAbilityName(param.paramId);
        if (param.paramId != 0)
        {
            if (!string.IsNullOrEmpty(abilityName))
            {
                EditorGUILayout.LabelField($"({abilityName})", GUILayout.Width(120));
            }
            else
            {
                EditorGUILayout.LabelField("(未知能力)", GUILayout.Width(80));
            }

            EditorGUILayout.LabelField("数值:", GUILayout.Width(30));
            param.value = EditorGUILayout.FloatField(param.value, GUILayout.Width(70));
        }
        else
        {
            EditorGUILayout.LabelField("(无参数)", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(100));
        }

        EditorGUILayout.EndHorizontal();
    }

    private string GetAbilityName(int abilityId)
    {
        var ability = abilityList.Find(a => a.id == abilityId);
        return ability != null ? ability.name : "";
    }

    #endregion

    #region Helper Methods

    private void EnsureParamsList(FishingComponentLevelData levelData)
    {
        if (levelData.paramsList == null)
            levelData.paramsList = new List<FishingComponentParam>();

        while (levelData.paramsList.Count < 3)
        {
            levelData.paramsList.Add(new FishingComponentParam { paramId = 0, value = 0f });
        }
    }

    private void AdjustLevelDataCount(FishingComponentConfig component, int newCount)
    {
        if (component.levelDataList == null)
            component.levelDataList = new List<FishingComponentLevelData>();

        while (component.levelDataList.Count < newCount)
        {
            int newLevel = component.levelDataList.Count + 1;
            component.levelDataList.Add(new FishingComponentLevelData
            {
                level = newLevel,
                paramsList = new List<FishingComponentParam>
                {
                    new FishingComponentParam { paramId = 0, value = 0f },
                    new FishingComponentParam { paramId = 0, value = 0f },
                    new FishingComponentParam { paramId = 0, value = 0f }
                }
            });
        }

        while (component.levelDataList.Count > newCount)
        {
            component.levelDataList.RemoveAt(component.levelDataList.Count - 1);
        }
    }

    private string GetCategoryIcon(FishingComponentCategory category)
    {
        switch (category)
        {
            case FishingComponentCategory.Rod: return "🎣";
            case FishingComponentCategory.Line: return "🧵";
            case FishingComponentCategory.Hook: return "🪝";
            default: return "✨";
        }
    }

    private string GetCategoryName(FishingComponentCategory category)
    {
        switch (category)
        {
            case FishingComponentCategory.Rod: return "钓竿";
            case FishingComponentCategory.Line: return "钓线";
            case FishingComponentCategory.Hook: return "钓钩";
            default: return "技能";
        }
    }

    private Color GetCategoryColor(FishingComponentCategory category)
    {
        switch (category)
        {
            case FishingComponentCategory.Rod: return new Color(1f, 0.6f, 0.2f);
            case FishingComponentCategory.Line: return new Color(0.2f, 0.8f, 0.4f);
            case FishingComponentCategory.Hook: return new Color(0.9f, 0.8f, 0.2f);
            default: return new Color(0.3f, 0.6f, 1f);
        }
    }

    private bool IsIdExists(int id)
    {
        return componentList.Exists(c => c.id == id);
    }

    private bool IsIdInRange(int id, FishingComponentCategory category)
    {
        int catIdx = (int)category;
        return id >= categoryStartIds[catIdx] && id <= categoryEndIds[catIdx];
    }

    #endregion

    #region Data Operations

    private void LoadData()
    {
        string fullPath = Path.Combine(Application.dataPath, RELATIVE_PATH);

        componentList = new List<FishingComponentConfig>();

        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var array = JsonUtility.FromJson<FishingComponentConfigArray>(json);
                if (array != null && array.items != null)
                {
                    componentList = array.items.ToList();
                    foreach (var component in componentList)
                    {
                        EnsureLevelData(component);
                    }
                    Debug.Log($"成功加载 {componentList.Count} 条数据");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"加载数据失败: {e.Message}");
            }
        }

        if (componentList.Count == 0)
        {
            AddHardcodedDefaults();
        }
    }

    private void EnsureLevelData(FishingComponentConfig component)
    {
        if (component.levelDataList == null)
            component.levelDataList = new List<FishingComponentLevelData>();

        for (int i = 1; i <= component.maxLevel; i++)
        {
            var existing = component.levelDataList.Find(l => l.level == i);
            if (existing == null)
            {
                component.levelDataList.Add(new FishingComponentLevelData
                {
                    level = i,
                    paramsList = new List<FishingComponentParam>
                    {
                        new FishingComponentParam { paramId = 0, value = 0f },
                        new FishingComponentParam { paramId = 0, value = 0f },
                        new FishingComponentParam { paramId = 0, value = 0f }
                    }
                });
            }
        }

        component.levelDataList = component.levelDataList.OrderBy(l => l.level).ToList();
    }

    private void SaveData()
    {
        string fullPath = Path.Combine(Application.dataPath, RELATIVE_PATH);
        string directory = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var array = new FishingComponentConfigArray { items = componentList.ToArray() };
        string json = JsonUtility.ToJson(array, true);
        File.WriteAllText(fullPath, json);

        AssetDatabase.Refresh();
        Debug.Log($"保存成功: {fullPath}");
    }

    private void QuickCreateComponent()
    {
        if (!IsIdInRange(newId, (FishingComponentCategory)newCategoryIndex))
        {
            EditorUtility.DisplayDialog("错误", $"ID {newId} 不在 {categoryNames[newCategoryIndex]} 的范围内！\n范围: {categoryStartIds[newCategoryIndex]}-{categoryEndIds[newCategoryIndex]}", "确定");
            return;
        }

        if (IsIdExists(newId))
        {
            EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在！", "确定");
            return;
        }

        var newComponent = new FishingComponentConfig
        {
            id = newId,
            name = newName,
            category = (FishingComponentCategory)newCategoryIndex,
            maxLevel = newMaxLevel,
            levelDataList = new List<FishingComponentLevelData>()
        };

        for (int i = 1; i <= newMaxLevel; i++)
        {
            newComponent.levelDataList.Add(new FishingComponentLevelData
            {
                level = i,
                paramsList = new List<FishingComponentParam>
                {
                    new FishingComponentParam { paramId = 0, value = 0f },
                    new FishingComponentParam { paramId = 0, value = 0f },
                    new FishingComponentParam { paramId = 0, value = 0f }
                }
            });
        }

        componentList.Add(newComponent);
        SaveData();

        newId = newId + 1;
        while (IsIdExists(newId) && newId <= categoryEndIds[newCategoryIndex]) newId++;
        if (newId > categoryEndIds[newCategoryIndex])
        {
            newId = categoryStartIds[newCategoryIndex];
        }

        EditorUtility.DisplayDialog("成功", $"已创建 [{newComponent.id}] {newComponent.name}", "确定");
        Repaint();
    }

    private void DuplicateComponent(FishingComponentConfig source)
    {
        int newId = source.id + 1;
        while (IsIdExists(newId) && newId <= categoryEndIds[(int)source.category]) newId++;

        if (newId > categoryEndIds[(int)source.category])
        {
            EditorUtility.DisplayDialog("错误", $"ID范围 {categoryStartIds[(int)source.category]}-{categoryEndIds[(int)source.category]} 已满！", "确定");
            return;
        }

        var newComponent = new FishingComponentConfig
        {
            id = newId,
            name = source.name + "_复制",
            category = source.category,
            maxLevel = source.maxLevel,
            levelDataList = new List<FishingComponentLevelData>()
        };

        foreach (var levelData in source.levelDataList)
        {
            var newLevelData = new FishingComponentLevelData
            {
                level = levelData.level,
                paramsList = new List<FishingComponentParam>()
            };
            foreach (var param in levelData.paramsList)
            {
                newLevelData.paramsList.Add(new FishingComponentParam { paramId = param.paramId, value = param.value });
            }
            newComponent.levelDataList.Add(newLevelData);
        }

        componentList.Add(newComponent);
        SaveData();

        EditorUtility.DisplayDialog("成功", $"已复制为 [{newComponent.id}] {newComponent.name}", "确定");
        Repaint();
    }

    private void ShowPresetMenu()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("🎣 钓竿 - 基础钓竿"), false, () => AddPreset("rod_basic"));
        menu.AddItem(new GUIContent("🎣 钓竿 - 高级钓竿"), false, () => AddPreset("rod_advanced"));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("🧵 钓线 - 基础钓线"), false, () => AddPreset("line_basic"));
        menu.AddItem(new GUIContent("🧵 钓线 - 高级钓线"), false, () => AddPreset("line_advanced"));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("🪝 钓钩 - 基础钓钩"), false, () => AddPreset("hook_basic"));
        menu.AddItem(new GUIContent("🪝 钓钩 - 高级钓钩"), false, () => AddPreset("hook_advanced"));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("✨ 技能 - 咬钩概率"), false, () => AddPreset("skill_bite"));
        menu.AddItem(new GUIContent("✨ 技能 - 稀有鱼加成"), false, () => AddPreset("skill_rare"));
        menu.ShowAsContext();
    }

    private void AddPreset(string presetType)
    {
        int newId = 3001;
        FishingComponentCategory category = FishingComponentCategory.Skill;
        int maxLevel = 10;
        int paramId = 709;
        string name = "";

        switch (presetType)
        {
            case "rod_basic":
                category = FishingComponentCategory.Rod;
                newId = GetNextAvailableIdInCategory(category, 3001);
                name = "基础钓竿";
                paramId = 709;
                break;
            case "rod_advanced":
                category = FishingComponentCategory.Rod;
                newId = GetNextAvailableIdInCategory(category, 3001);
                name = "高级钓竿";
                paramId = 709;
                break;
            case "line_basic":
                category = FishingComponentCategory.Line;
                newId = GetNextAvailableIdInCategory(category, 3101);
                name = "基础钓线";
                paramId = 710;
                break;
            case "line_advanced":
                category = FishingComponentCategory.Line;
                newId = GetNextAvailableIdInCategory(category, 3101);
                name = "高级钓线";
                paramId = 710;
                break;
            case "hook_basic":
                category = FishingComponentCategory.Hook;
                newId = GetNextAvailableIdInCategory(category, 3201);
                name = "基础钓钩";
                paramId = 711;
                break;
            case "hook_advanced":
                category = FishingComponentCategory.Hook;
                newId = GetNextAvailableIdInCategory(category, 3201);
                name = "高级钓钩";
                paramId = 711;
                break;
            case "skill_bite":
                category = FishingComponentCategory.Skill;
                newId = GetNextAvailableIdInCategory(category, 3301);
                name = "咬钩技巧";
                paramId = 709;
                break;
            case "skill_rare":
                category = FishingComponentCategory.Skill;
                newId = GetNextAvailableIdInCategory(category, 3301);
                name = "稀有鱼专精";
                paramId = 703;
                break;
            default:
                name = "示例技能";
                paramId = 709;
                break;
        }

        if (newId == -1)
        {
            EditorUtility.DisplayDialog("错误", "该类别的ID范围已满！", "确定");
            return;
        }

        var preset = new FishingComponentConfig
        {
            id = newId,
            name = name,
            category = category,
            maxLevel = maxLevel,
            levelDataList = new List<FishingComponentLevelData>()
        };

        for (int i = 1; i <= maxLevel; i++)
        {
            preset.levelDataList.Add(new FishingComponentLevelData
            {
                level = i,
                paramsList = new List<FishingComponentParam>
                {
                    new FishingComponentParam { paramId = paramId, value = 0.05f * i },
                    new FishingComponentParam { paramId = 0, value = 0f },
                    new FishingComponentParam { paramId = 0, value = 0f }
                }
            });
        }

        componentList.Add(preset);
        SaveData();

        EditorUtility.DisplayDialog("成功", $"已添加预设: [{preset.id}] {preset.name}", "确定");
        Repaint();
    }

    private int GetNextAvailableIdInCategory(FishingComponentCategory category, int startId)
    {
        int catIdx = (int)category;
        for (int id = startId; id <= categoryEndIds[catIdx]; id++)
        {
            if (!IsIdExists(id))
                return id;
        }
        return -1;
    }

    private void AddHardcodedDefaults()
    {
        var rod = new FishingComponentConfig
        {
            id = 3001,
            name = "基础钓竿",
            category = FishingComponentCategory.Rod,
            maxLevel = 10,
            levelDataList = new List<FishingComponentLevelData>()
        };
        for (int i = 1; i <= 10; i++)
        {
            rod.levelDataList.Add(new FishingComponentLevelData
            {
                level = i,
                paramsList = new List<FishingComponentParam>
                {
                    new FishingComponentParam { paramId = 709, value = 0.03f * i },
                    new FishingComponentParam { paramId = 708, value = 0.02f * i },
                    new FishingComponentParam { paramId = 0, value = 0f }
                }
            });
        }
        componentList.Add(rod);

        var line = new FishingComponentConfig
        {
            id = 3101,
            name = "基础钓线",
            category = FishingComponentCategory.Line,
            maxLevel = 10,
            levelDataList = new List<FishingComponentLevelData>()
        };
        for (int i = 1; i <= 10; i++)
        {
            line.levelDataList.Add(new FishingComponentLevelData
            {
                level = i,
                paramsList = new List<FishingComponentParam>
                {
                    new FishingComponentParam { paramId = 710, value = 0.03f * i },
                    new FishingComponentParam { paramId = 703, value = 0.02f * i },
                    new FishingComponentParam { paramId = 0, value = 0f }
                }
            });
        }
        componentList.Add(line);

        var hook = new FishingComponentConfig
        {
            id = 3201,
            name = "基础钓钩",
            category = FishingComponentCategory.Hook,
            maxLevel = 10,
            levelDataList = new List<FishingComponentLevelData>()
        };
        for (int i = 1; i <= 10; i++)
        {
            hook.levelDataList.Add(new FishingComponentLevelData
            {
                level = i,
                paramsList = new List<FishingComponentParam>
                {
                    new FishingComponentParam { paramId = 711, value = 0.02f * i },
                    new FishingComponentParam { paramId = 707, value = 0.01f * i },
                    new FishingComponentParam { paramId = 0, value = 0f }
                }
            });
        }
        componentList.Add(hook);

        SaveData();
    }

    #endregion

    #region Batch Settings

    private int batchStartLevel = 1;
    private int batchEndLevel = 10;
    private int batchParam1Id = 709;
    private float batchParam1Value = 0.05f;
    private int batchParam2Id = 0;
    private float batchParam2Value = 0f;

    private void ApplyBatchSettings(FishingComponentConfig component)
    {
        batchStartLevel = Mathf.Clamp(batchStartLevel, 1, component.maxLevel);
        batchEndLevel = Mathf.Clamp(batchEndLevel, batchStartLevel, component.maxLevel);

        int appliedCount = 0;
        for (int i = batchStartLevel - 1; i < batchEndLevel && i < component.levelDataList.Count; i++)
        {
            var levelData = component.levelDataList[i];
            EnsureParamsList(levelData);

            if (batchParam1Id != 0)
            {
                levelData.paramsList[0].paramId = batchParam1Id;
                levelData.paramsList[0].value = batchParam1Value;
                appliedCount++;
            }

            if (batchParam2Id != 0)
            {
                levelData.paramsList[1].paramId = batchParam2Id;
                levelData.paramsList[1].value = batchParam2Value;
                appliedCount++;
            }
        }

        EditorUtility.DisplayDialog("成功", $"已批量设置等级 {batchStartLevel} 到 {batchEndLevel}\n共更新 {appliedCount} 个参数", "确定");
    }

    #endregion
}

// ==================== 编辑器内部使用的数据结构（避免与外部冲突）====================

/// <summary>
/// 能力配置项（编辑器内部使用）
/// </summary>
[System.Serializable]
public class AbilityItem
{
    public int id;
    public string name;
    public string description;
    public string abilityType;
    public int targetRarityId;
}

/// <summary>
/// 能力列表包装器（编辑器内部使用）
/// </summary>
[System.Serializable]
public class AbilityItemListWrapper
{
    public List<AbilityItem> abilities;
}
#endif