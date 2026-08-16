#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 完整钓鱼技能编辑器（ID范围：3001-3399）
/// </summary>
public class FishingComponentJsonEditor : EditorWindow
{
    private const string RELATIVE_PATH = "Addressables/JsonData/Ability/fishing_components.json";
    private const string ABILITIES_PATH = "Addressables/JsonData/Ability/abilities.json";

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

    // 快速创建参数
    private int newId = 3001;
    private string newName = "新技能";
    private int newCategoryIndex = 4; // 默认技能
    private int newDefaultLevel = 30;

    // 新增标记（用于显示新增条目高亮）- 仅在当前编辑会话有效
    private HashSet<int> newItemIds = new HashSet<int>();

    // ===== 编辑状态追踪 =====
    private FishingComponentConfig editingComponentBackup;  // 进入编辑时的备份
    private bool hasUnsavedChanges = false;

    // ===== 最大等级编辑临时变量 =====
    private int tempMaxLevel = 0;

    private readonly string[] categoryNames = { "全部", "钓竿", "钓线", "钓钩", "技能" };
    private readonly string[] categoryDefaultNames = { "", "钓竿", "钓线", "钓钩", "技能" };
    private readonly int[] categoryStartIds = { 0, 3001, 3101, 3201, 3301 };
    private readonly int[] categoryEndIds = { 0, 3099, 3199, 3299, 3399 };

    // 能力下拉选项缓存
    private string[] abilityDisplayOptions;
    private int[] abilityIdOptions;
    private bool abilityOptionsLoaded = false;

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
        BuildAbilityOptions();
        newItemIds.Clear();
        hasUnsavedChanges = false;
        editingComponentBackup = null;

        // 初始化批量设置的范围为1到当前选中技能的最大等级，如果没有选中则默认为1-10
        if (selectedComponentId != -1)
        {
            var comp = GetComponentById(selectedComponentId);
            if (comp != null)
            {
                batchEndLevel = comp.maxLevel;
                tempMaxLevel = comp.maxLevel;
            }
        }
        else
        {
            batchEndLevel = 10;
            tempMaxLevel = 10;
        }
        batchStartLevel = 1;
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

        BuildAbilityOptions();
    }

    private void BuildAbilityOptions()
    {
        var displayList = new List<string>();
        var idList = new List<int>();

        displayList.Add("(无参数)");
        idList.Add(0);

        foreach (var ability in abilityList.OrderBy(a => a.id))
        {
            displayList.Add($"[{ability.id}] {ability.name}");
            idList.Add(ability.id);
        }

        abilityDisplayOptions = displayList.ToArray();
        abilityIdOptions = idList.ToArray();
        abilityOptionsLoaded = true;
    }

    private int GetAbilityIndex(int abilityId)
    {
        if (!abilityOptionsLoaded) BuildAbilityOptions();
        for (int i = 0; i < abilityIdOptions.Length; i++)
        {
            if (abilityIdOptions[i] == abilityId)
                return i;
        }
        return 0;
    }

    private int DrawAbilityPopup(string label, int currentAbilityId, ref float value, GUILayoutOption width = null)
    {
        if (!abilityOptionsLoaded) BuildAbilityOptions();

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(label, GUILayout.Width(60));
        int currentIndex = GetAbilityIndex(currentAbilityId);
        int newIndex = EditorGUILayout.Popup(currentIndex, abilityDisplayOptions, width ?? GUILayout.Width(160));

        int newAbilityId = abilityIdOptions[newIndex];

        if (newAbilityId != 0)
        {
            EditorGUILayout.LabelField("数值:", GUILayout.Width(30));
            value = EditorGUILayout.FloatField(value, GUILayout.Width(60));
        }
        else
        {
            value = 0f;
        }

        EditorGUILayout.EndHorizontal();

        return newAbilityId;
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
            BuildAbilityOptions();
            newItemIds.Clear();
            hasUnsavedChanges = false;
            editingComponentBackup = null;
        }
        if (GUILayout.Button("➕ 新增", EditorStyles.toolbarButton, GUILayout.Width(70)))
            QuickCreateComponent();

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
        EditorGUILayout.LabelField("名称:", GUILayout.Width(35));
        newName = EditorGUILayout.TextField(newName, GUILayout.Width(140));

        EditorGUILayout.LabelField("类别:", GUILayout.Width(35));
        int newCatIndex = EditorGUILayout.Popup(newCategoryIndex - 1, new string[] { "钓竿", "钓线", "钓钩", "技能" }) + 1;

        if (newCatIndex != newCategoryIndex)
        {
            newCategoryIndex = newCatIndex;
            newName = categoryDefaultNames[newCategoryIndex];
        }

        EditorGUILayout.LabelField("等级:", GUILayout.Width(35));
        newDefaultLevel = EditorGUILayout.IntField(newDefaultLevel, GUILayout.Width(40));

        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
        if (GUILayout.Button("创建", GUILayout.Width(50)))
            QuickCreateComponent();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"ID范围: {categoryStartIds[newCategoryIndex]}-{categoryEndIds[newCategoryIndex]}", GUILayout.Width(250));
        int nextId = GetNextAvailableIdInCategory((FishingComponentCategory)newCategoryIndex);
        if (nextId > 0)
            EditorGUILayout.LabelField($"下一个可用ID: {nextId}", GUILayout.Width(150));
        else
            EditorGUILayout.LabelField("⚠️ 该类别ID已满!", GUILayout.Width(150));
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
        bool isNew = newItemIds.Contains(item.id);

        if (isNew)
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        else if (isSelected)
            GUI.backgroundColor = Color.cyan;
        else
            GUI.backgroundColor = Color.white;

        EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box", GUILayout.Height(26));

        EditorGUILayout.LabelField(GetCategoryIcon(item.category), GUILayout.Width(35));

        string displayName = isNew ? $"🆕 [{item.id}] {item.name}" : $"[{item.id}] {item.name}";
        if (displayName.Length > 22) displayName = displayName.Substring(0, 20) + "..";
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
            EnterEditMode(item.id);
        }
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定删除 [{item.id}] {item.name} 吗？", "删除", "取消"))
            {
                componentList.Remove(item);
                newItemIds.Remove(item.id);
                if (selectedComponentId == item.id) selectedComponentId = -1;
                SaveData();
                Repaint();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;

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

        bool isNew = newItemIds.Contains(component.id);

        EditorGUILayout.LabelField("技能详情", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(GetCategoryIcon(component.category), GUILayout.Width(30));
        if (isNew)
            EditorGUILayout.LabelField($"🆕 ID: {component.id} (新增)", EditorStyles.boldLabel);
        else
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
        EditorGUILayout.LabelField("参数1", EditorStyles.toolbarButton, GUILayout.Width(180));
        EditorGUILayout.LabelField("参数2", EditorStyles.toolbarButton, GUILayout.Width(180));
        EditorGUILayout.LabelField("参数3", EditorStyles.toolbarButton, GUILayout.Width(180));
        EditorGUILayout.EndHorizontal();

        previewScrollPosition = EditorGUILayout.BeginScrollView(previewScrollPosition, GUILayout.Height(350));

        if (component.levelDataList != null)
        {
            for (int i = 0; i < component.levelDataList.Count; i++)
            {
                var levelData = component.levelDataList[i];
                EditorGUILayout.BeginHorizontal(i % 2 == 0 ? "box" : GUIStyle.none);
                EditorGUILayout.LabelField($"Lv.{levelData.level}", GUILayout.Width(50));

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
                        EditorGUILayout.LabelField(displayText, GUILayout.Width(180));
                    }
                    else
                    {
                        EditorGUILayout.LabelField("-", GUILayout.Width(180));
                    }
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
            EnterEditMode(component.id);
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

    private void EnterEditMode(int componentId)
    {
        var component = GetComponentById(componentId);
        if (component == null) return;

        editingComponentBackup = DeepCopy(component);
        hasUnsavedChanges = false;

        // 初始化最大等级临时变量
        tempMaxLevel = component.maxLevel;

        // 更新批量设置的范围为当前技能的最大等级
        if (component != null)
        {
            batchEndLevel = component.maxLevel;
            if (batchStartLevel > batchEndLevel)
                batchStartLevel = 1;
        }

        editingComponentId = componentId;
        currentMode = EditMode.Edit;
        Repaint();
    }

    private FishingComponentConfig DeepCopy(FishingComponentConfig source)
    {
        var copy = new FishingComponentConfig
        {
            id = source.id,
            name = source.name,
            category = source.category,
            maxLevel = source.maxLevel,
            levelDataList = new List<FishingComponentLevelData>()
        };

        if (source.levelDataList != null)
        {
            foreach (var levelData in source.levelDataList)
            {
                var levelCopy = new FishingComponentLevelData
                {
                    level = levelData.level,
                    levelDescription = levelData.levelDescription,
                    upgradeDescription = levelData.upgradeDescription,
                    upgradeCost = levelData.upgradeCost,
                    paramsList = new List<FishingComponentParam>()
                };

                if (levelData.paramsList != null)
                {
                    foreach (var param in levelData.paramsList)
                    {
                        levelCopy.paramsList.Add(new FishingComponentParam
                        {
                            paramId = param.paramId,
                            value = param.value
                        });
                    }
                }

                copy.levelDataList.Add(levelCopy);
            }
        }

        return copy;
    }

    private void DrawEditMode()
    {
        var component = GetComponentById(editingComponentId);

        if (component == null)
        {
            currentMode = EditMode.List;
            return;
        }

        bool isNew = newItemIds.Contains(component.id);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
        if (GUILayout.Button("← 返回列表", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            if (hasUnsavedChanges)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "未保存的更改",
                    "当前技能有未保存的修改，是否保存？",
                    "保存",     // 0
                    "不保存",   // 1
                    "取消"      // 2
                );

                if (choice == 0) // 保存
                {
                    SaveData();
                    newItemIds.Remove(component.id);
                    hasUnsavedChanges = false;
                    editingComponentBackup = null;
                    currentMode = EditMode.List;
                    Repaint();
                    return;
                }
                else if (choice == 1) // 不保存 - 恢复备份
                {
                    if (editingComponentBackup != null)
                    {
                        component.name = editingComponentBackup.name;
                        component.category = editingComponentBackup.category;
                        component.maxLevel = editingComponentBackup.maxLevel;
                        component.levelDataList = editingComponentBackup.levelDataList;
                        tempMaxLevel = editingComponentBackup.maxLevel;
                    }
                    hasUnsavedChanges = false;
                    editingComponentBackup = null;
                    currentMode = EditMode.List;
                    Repaint();
                    return;
                }
                else // 取消
                {
                    return;
                }
            }
            else
            {
                editingComponentBackup = null;
                currentMode = EditMode.List;
                Repaint();
                return;
            }
        }
        GUI.backgroundColor = Color.white;

        if (isNew)
            EditorGUILayout.LabelField($"🆕 编辑: [{component.id}] {component.name} (新增)", EditorStyles.boldLabel);
        else
            EditorGUILayout.LabelField($"编辑: [{component.id}] {component.name}", EditorStyles.boldLabel);

        if (hasUnsavedChanges)
        {
            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            EditorGUILayout.LabelField("⚠️ 有未保存的修改", EditorStyles.boldLabel, GUILayout.Width(120));
            GUI.backgroundColor = Color.white;
        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("💾 保存", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            SaveData();
            EditorUtility.DisplayDialog("保存成功", "数据已保存！", "确定");
            newItemIds.Remove(component.id);
            hasUnsavedChanges = false;
            editingComponentBackup = null;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);

        editScrollPosition = EditorGUILayout.BeginScrollView(editScrollPosition);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("基本信息", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(60));
        if (isNew)
            EditorGUILayout.LabelField($"{component.id} 🆕", EditorStyles.boldLabel);
        else
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
        string newName = EditorGUILayout.TextField(component.name);
        if (newName != component.name)
        {
            component.name = newName;
            hasUnsavedChanges = true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("类别:", GUILayout.Width(60));
        int currentCatIndex = (int)component.category;
        int newCatIndex = EditorGUILayout.Popup(currentCatIndex - 1, new string[] { "钓竿", "钓线", "钓钩", "技能" }) + 1;
        if (newCatIndex != currentCatIndex)
        {
            component.category = (FishingComponentCategory)newCatIndex;
            hasUnsavedChanges = true;
        }
        EditorGUILayout.EndHorizontal();

        // ===== 修复：使用临时变量存储最大等级输入值 =====
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("最大等级:", GUILayout.Width(60));
        // 显示当前最大等级，但用临时变量存储输入
        tempMaxLevel = EditorGUILayout.IntField(tempMaxLevel, GUILayout.Width(50));
        EditorGUILayout.LabelField($"当前: {component.maxLevel}", GUILayout.Width(80));

        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("确认", GUILayout.Width(50)))
        {
            if (tempMaxLevel >= 1 && tempMaxLevel <= 100)
            {
                // 更新最大等级
                component.maxLevel = tempMaxLevel;
                // 调整等级列表
                AdjustLevelDataCount(component, tempMaxLevel);
                hasUnsavedChanges = true;
                // 更新批量设置的范围
                batchEndLevel = tempMaxLevel;
                if (batchStartLevel > batchEndLevel)
                    batchStartLevel = 1;
                // 强制刷新界面
                Repaint();
                EditorUtility.DisplayDialog("成功", $"最大等级已更新为 {tempMaxLevel}", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("错误", "最大等级必须在 1-100 之间！", "确定");
                // 恢复显示当前值
                tempMaxLevel = component.maxLevel;
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("等级参数配置", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("从 abilities.json 动态加载能力列表", MessageType.Info);

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

        // 批量设置范围 - 动态调整为1到当前技能的最大等级
        batchStartLevel = EditorGUILayout.IntField(batchStartLevel, GUILayout.Width(50));
        EditorGUILayout.LabelField("到", GUILayout.Width(20));

        // 限制范围在1到最大等级之间
        int maxLevel = component != null ? component.maxLevel : 10;
        if (batchEndLevel > maxLevel) batchEndLevel = maxLevel;
        if (batchEndLevel < 1) batchEndLevel = 1;

        batchEndLevel = EditorGUILayout.IntField(batchEndLevel, GUILayout.Width(50));
        EditorGUILayout.LabelField($"(1-{maxLevel})", GUILayout.Width(60));

        int oldParamId = batchParam1Id;
        float oldValue = batchParam1Value;
        batchParam1Id = DrawAbilityPopup("能力:", batchParam1Id, ref batchParam1Value, GUILayout.Width(180));

        if (oldParamId != batchParam1Id || Math.Abs(oldValue - batchParam1Value) > 0.0001f)
        {
            hasUnsavedChanges = true;
        }

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

        int oldParam1 = levelData.paramsList[0].paramId;
        float oldVal1 = levelData.paramsList[0].value;
        int oldParam2 = levelData.paramsList[1].paramId;
        float oldVal2 = levelData.paramsList[1].value;
        int oldParam3 = levelData.paramsList[2].paramId;
        float oldVal3 = levelData.paramsList[2].value;
        string oldLevelDesc = levelData.levelDescription;
        string oldUpgradeDesc = levelData.upgradeDescription;
        int oldUpgradeCost = levelData.upgradeCost;

        levelData.paramsList[0].paramId = DrawAbilityPopup("参数1:", levelData.paramsList[0].paramId, ref levelData.paramsList[0].value, GUILayout.Width(180));
        levelData.paramsList[1].paramId = DrawAbilityPopup("参数2:", levelData.paramsList[1].paramId, ref levelData.paramsList[1].value, GUILayout.Width(180));
        levelData.paramsList[2].paramId = DrawAbilityPopup("参数3:", levelData.paramsList[2].paramId, ref levelData.paramsList[2].value, GUILayout.Width(180));

        if (oldParam1 != levelData.paramsList[0].paramId || Math.Abs(oldVal1 - levelData.paramsList[0].value) > 0.0001f ||
            oldParam2 != levelData.paramsList[1].paramId || Math.Abs(oldVal2 - levelData.paramsList[1].value) > 0.0001f ||
            oldParam3 != levelData.paramsList[2].paramId || Math.Abs(oldVal3 - levelData.paramsList[2].value) > 0.0001f)
        {
            hasUnsavedChanges = true;
        }

        GUILayout.Space(5);
        EditorGUILayout.LabelField("等级描述:", GUILayout.Width(60));
        string newLevelDesc = EditorGUILayout.TextArea(levelData.levelDescription, GUILayout.Height(60));
        if (newLevelDesc != oldLevelDesc) { levelData.levelDescription = newLevelDesc; hasUnsavedChanges = true; }

        GUILayout.Space(5);
        EditorGUILayout.LabelField("升级效果:", GUILayout.Width(60));
        string newUpgradeDesc = EditorGUILayout.TextField(levelData.upgradeDescription);
        if (newUpgradeDesc != oldUpgradeDesc) { levelData.upgradeDescription = newUpgradeDesc; hasUnsavedChanges = true; }

        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("升级费用:", GUILayout.Width(60));
        int newUpgradeCost = EditorGUILayout.IntField(levelData.upgradeCost);
        if (newUpgradeCost != oldUpgradeCost) { levelData.upgradeCost = newUpgradeCost; hasUnsavedChanges = true; }
        EditorGUILayout.LabelField("金币", GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
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

        // 如果新数量大于当前数量，添加新等级
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
            hasUnsavedChanges = true;
        }

        // 如果新数量小于当前数量，移除多余的等级
        while (component.levelDataList.Count > newCount)
        {
            component.levelDataList.RemoveAt(component.levelDataList.Count - 1);
            hasUnsavedChanges = true;
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

        componentList = componentList.OrderBy(c => c.id).ToList();

        var array = new FishingComponentConfigArray { items = componentList.ToArray() };
        string json = JsonUtility.ToJson(array, true);
        File.WriteAllText(fullPath, json);

        AssetDatabase.Refresh();
        Debug.Log($"保存成功: {fullPath}");

        hasUnsavedChanges = false;
    }

    private void QuickCreateComponent()
    {
        int autoId = GetNextAvailableIdInCategory((FishingComponentCategory)newCategoryIndex);
        if (autoId == -1)
        {
            EditorUtility.DisplayDialog("错误", $"类别 {categoryNames[newCategoryIndex]} 的ID范围已满！\n范围: {categoryStartIds[newCategoryIndex]}-{categoryEndIds[newCategoryIndex]}", "确定");
            return;
        }

        var newComponent = new FishingComponentConfig
        {
            id = autoId,
            name = newName,
            category = (FishingComponentCategory)newCategoryIndex,
            maxLevel = newDefaultLevel,
            levelDataList = new List<FishingComponentLevelData>()
        };

        for (int i = 1; i <= newDefaultLevel; i++)
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

        int insertIndex = componentList.FindIndex(c => c.id > autoId);
        if (insertIndex == -1)
            componentList.Add(newComponent);
        else
            componentList.Insert(insertIndex, newComponent);

        newItemIds.Add(autoId);

        SaveData();

        selectedComponentId = autoId;
        EnterEditMode(autoId);

        Debug.Log($"已创建新技能: [{autoId}] {newName} (新增标记已添加)");
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

        int insertIndex = componentList.FindIndex(c => c.id > newId);
        if (insertIndex == -1)
            componentList.Add(newComponent);
        else
            componentList.Insert(insertIndex, newComponent);

        newItemIds.Add(newId);
        SaveData();

        EditorUtility.DisplayDialog("成功", $"已复制为 [{newComponent.id}] {newComponent.name}", "确定");
        Repaint();
    }

    private int GetNextAvailableIdInCategory(FishingComponentCategory category)
    {
        int catIdx = (int)category;
        int startId = categoryStartIds[catIdx];
        int endId = categoryEndIds[catIdx];

        var usedIds = componentList
            .Where(c => (int)c.category == catIdx)
            .Select(c => c.id)
            .ToHashSet();

        for (int id = startId; id <= endId; id++)
        {
            if (!usedIds.Contains(id))
                return id;
        }
        return -1;
    }

    #endregion

    #region Batch Settings

    private int batchStartLevel = 1;
    private int batchEndLevel = 10;
    private int batchParam1Id = 0;
    private float batchParam1Value = 0f;

    private void ApplyBatchSettings(FishingComponentConfig component)
    {
        // 确保范围在有效范围内
        batchStartLevel = Mathf.Clamp(batchStartLevel, 1, component.maxLevel);
        batchEndLevel = Mathf.Clamp(batchEndLevel, batchStartLevel, component.maxLevel);

        int appliedCount = 0;
        for (int i = batchStartLevel - 1; i < batchEndLevel && i < component.levelDataList.Count; i++)
        {
            var levelData = component.levelDataList[i];
            EnsureParamsList(levelData);

            if (batchParam1Id != 0)
            {
                if (levelData.paramsList[0].paramId != batchParam1Id || Math.Abs(levelData.paramsList[0].value - batchParam1Value) > 0.0001f)
                {
                    levelData.paramsList[0].paramId = batchParam1Id;
                    levelData.paramsList[0].value = batchParam1Value;
                    appliedCount++;
                    hasUnsavedChanges = true;
                }
            }
        }

        EditorUtility.DisplayDialog("成功", $"已批量设置等级 {batchStartLevel} 到 {batchEndLevel}\n共更新 {appliedCount} 个参数", "确定");
    }

    #endregion
}

// ==================== 编辑器内部使用的数据结构 ====================

[System.Serializable]
public class AbilityItem
{
    public int id;
    public string name;
    public string description;
    public string abilityType;
    public int targetRarityId;
}

[System.Serializable]
public class AbilityItemListWrapper
{
    public List<AbilityItem> abilities;
}
#endif
