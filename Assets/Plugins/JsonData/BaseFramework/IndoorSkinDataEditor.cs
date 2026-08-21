#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 室内装饰皮肤编辑器（ID范围：5000-5599）
/// </summary>
public class IndoorSkinDataEditor : EditorWindow
{
    private const string RELATIVE_PATH = "Addressables/JsonData/Game/BagItem/indoorSkin.json";

    private List<IndoorSkinData> decorationList = new List<IndoorSkinData>();
    private int selectedDecorationId = -1;
    private int editingDecorationId = -1;

    private enum EditMode { List, Edit }
    private EditMode currentMode = EditMode.List;

    private string searchText = "";
    private int selectedCategoryFilter = 0;

    private Vector2 listScrollPosition = Vector2.zero;
    private Vector2 editScrollPosition = Vector2.zero;

    private int newId = 5001;
    private string newName = "新装饰";
    private int newCategoryIndex = 0;

    private readonly string[] categoryNames = { "全部", "墙壁(51)", "地板(52)", "楼梯(53)", "灯带(54)", "挂饰(55)", "望远镜(56)", "昆虫房(57)", "宠物屋(58)", "鱼缸(59)", "熊猫(60)", "鹦鹉(61)", "桌子(62)" };
    private readonly int[] categoryValues = { 0, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62 };
    private readonly int[] categoryStartIds = { 0, 5000, 5050, 5100, 5150, 5200, 5250, 5300, 5350, 5400, 5450, 5500, 5550 };
    private readonly int[] categoryEndIds = { 0, 5049, 5099, 5149, 5199, 5249, 5299, 5349, 5399, 5449, 5499, 5549, 5599 };

    private readonly Dictionary<int, string> categoryNameMap = new Dictionary<int, string>()
    {
        { 51, "墙壁" },
        { 52, "地板" },
        { 53, "楼梯" },
        { 54, "灯带" },
        { 55, "挂饰" },
        { 56, "望远镜" },
        { 57, "昆虫房" },
        { 58, "宠物屋" },
        { 59, "鱼缸" },
        { 60, "熊猫" },
        { 61, "鹦鹉" },
        { 62, "桌子" }
    };

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/5001_室内装饰皮肤")]
    public static void ShowWindow()
    {
        var window = GetWindow<IndoorSkinDataEditor>("室内装饰皮肤编辑器");
        window.minSize = new Vector2(950, 650);
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
        LoadData();
    }

    #region List Mode

    private void DrawListMode()
    {
        DrawTopToolbar();
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(520));
        DrawSearchFilter();
        DrawQuickCreate();
        DrawDecorationList();
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        DrawDetailPreview();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("🏠 室内装饰皮肤编辑器 (ID: 5000-5599)", EditorStyles.boldLabel, GUILayout.Width(220));
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton, GUILayout.Width(70)))
            LoadData();
        if (GUILayout.Button("➕ 新增", EditorStyles.toolbarButton, GUILayout.Width(70)))
            QuickCreateDecoration();

        EditorGUILayout.LabelField($"共{decorationList.Count}条", EditorStyles.toolbarButton, GUILayout.Width(60));
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

        EditorGUILayout.LabelField("名称:", GUILayout.Width(35));
        newName = EditorGUILayout.TextField(newName, GUILayout.Width(140));

        EditorGUILayout.LabelField("类别:", GUILayout.Width(35));
        newCategoryIndex = EditorGUILayout.Popup(newCategoryIndex, new string[] { "墙壁", "地板", "楼梯", "灯带", "挂饰", "望远镜", "昆虫房", "宠物屋", "鱼缸", "熊猫", "鹦鹉", "桌子" });

        int categoryId = categoryValues[newCategoryIndex + 1];
        EditorGUILayout.LabelField($"范围: {categoryStartIds[newCategoryIndex + 1]}-{categoryEndIds[newCategoryIndex + 1]}", GUILayout.Width(130));

        GUI.backgroundColor = new Color(0.2f, 0.7f, 0.2f);
        if (GUILayout.Button("创建", GUILayout.Width(50)))
            QuickCreateDecoration();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private List<IndoorSkinData> GetFilteredList()
    {
        var filtered = decorationList.AsEnumerable();

        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(c => c.name.Contains(searchText) || c.id.ToString().Contains(searchText));
        }

        if (selectedCategoryFilter > 0)
        {
            filtered = filtered.Where(c => c.categoryId == categoryValues[selectedCategoryFilter]);
        }

        return filtered.ToList();
    }

    private void DrawDecorationList()
    {
        EditorGUILayout.LabelField("装饰列表", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("ID", EditorStyles.toolbarButton, GUILayout.Width(50));
        EditorGUILayout.LabelField("名称", EditorStyles.toolbarButton, GUILayout.Width(150));
        EditorGUILayout.LabelField("类别", EditorStyles.toolbarButton, GUILayout.Width(80));
        EditorGUILayout.LabelField("操作", EditorStyles.toolbarButton, GUILayout.Width(95));
        EditorGUILayout.EndHorizontal();

        listScrollPosition = EditorGUILayout.BeginScrollView(listScrollPosition, GUILayout.ExpandHeight(true));

        var filteredList = GetFilteredList();

        int lastCategory = -1;
        for (int i = 0; i < filteredList.Count; i++)
        {
            var item = filteredList[i];

            if (lastCategory != -1 && item.categoryId != lastCategory)
            {
                DrawCategorySeparator(item.categoryId);
            }

            DrawDecorationItem(item);
            lastCategory = item.categoryId;
        }

        if (filteredList.Count == 0)
            EditorGUILayout.LabelField("暂无数据", EditorStyles.centeredGreyMiniLabel, GUILayout.Height(50));

        EditorGUILayout.EndScrollView();
    }

    private void DrawCategorySeparator(int categoryId)
    {
        string categoryName = categoryNameMap.TryGetValue(categoryId, out string name) ? name : $"未知({categoryId})";
        int startId = 0, endId = 0;
        for (int i = 0; i < categoryValues.Length; i++)
        {
            if (categoryValues[i] == categoryId)
            {
                startId = categoryStartIds[i];
                endId = categoryEndIds[i];
                break;
            }
        }

        EditorGUILayout.Space(3);
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField("", GUILayout.Height(1));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(10);
        EditorGUILayout.LabelField($"🏷️ {categoryName} ({startId}-{endId})", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
        EditorGUILayout.LabelField("", GUILayout.Height(1));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    private void DrawDecorationItem(IndoorSkinData item)
    {
        bool isSelected = selectedDecorationId == item.id;

        EditorGUILayout.BeginHorizontal(isSelected ? "SelectionRect" : "box", GUILayout.Height(26));

        EditorGUILayout.LabelField($"{item.id}", GUILayout.Width(50));

        string displayName = item.name;
        if (displayName.Length > 20) displayName = displayName.Substring(0, 18) + "..";
        EditorGUILayout.LabelField(displayName, EditorStyles.boldLabel, GUILayout.Width(150));

        string categoryName = categoryNameMap.TryGetValue(item.categoryId, out string name) ? name : $"未知({item.categoryId})";
        var categoryStyle = new GUIStyle(EditorStyles.miniLabel);
        categoryStyle.fontSize = 10;
        categoryStyle.padding = new RectOffset(3, 3, 2, 2);
        categoryStyle.normal.textColor = Color.white;
        categoryStyle.alignment = TextAnchor.MiddleCenter;
        GUI.backgroundColor = GetCategoryColor(item.categoryId);
        EditorGUILayout.LabelField(categoryName, categoryStyle, GUILayout.Width(80));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.BeginHorizontal(GUILayout.Width(95));

        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("编辑", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            editingDecorationId = item.id;
            currentMode = EditMode.Edit;
        }
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("删除", EditorStyles.miniButton, GUILayout.Width(45)))
        {
            if (EditorUtility.DisplayDialog("确认删除", $"确定删除 [{item.id}] {item.name} 吗？", "删除", "取消"))
            {
                decorationList.Remove(item);
                if (selectedDecorationId == item.id) selectedDecorationId = -1;
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
            selectedDecorationId = item.id;
            Event.current.Use();
            Repaint();
        }
    }

    private IndoorSkinData GetDecorationById(int id)
    {
        return decorationList.Find(c => c.id == id);
    }

    private void DrawDetailPreview()
    {
        var item = GetDecorationById(selectedDecorationId);

        if (item == null)
        {
            EditorGUILayout.LabelField("请从左侧列表选择一个装饰", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        EditorGUILayout.LabelField("装饰详情", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"ID: {item.id}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"名称: {item.name}", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        string categoryName = categoryNameMap.TryGetValue(item.categoryId, out string name) ? name : $"未知({item.categoryId})";
        EditorGUILayout.LabelField($"类别: {categoryName}");

        EditorGUILayout.LabelField("描述:", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(item.description, GUILayout.Height(60));

        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button("✏️ 编辑", GUILayout.Width(80)))
        {
            editingDecorationId = item.id;
            currentMode = EditMode.Edit;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Edit Mode

    private void DrawEditMode()
    {
        var item = GetDecorationById(editingDecorationId);

        if (item == null)
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

        EditorGUILayout.LabelField($"编辑: [{item.id}] {item.name}", EditorStyles.boldLabel);

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
        int newId = EditorGUILayout.IntField(item.id, GUILayout.Width(80));
        if (newId != item.id && !IsIdDuplicate(newId, editingDecorationId))
            item.id = newId;
        else if (newId != item.id)
            EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("名称:", GUILayout.Width(60));
        item.name = EditorGUILayout.TextField(item.name);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("类别:", GUILayout.Width(60));
        int currentCategoryIndex = GetCategoryIndexByValue(item.categoryId);
        int newCategoryIndex = EditorGUILayout.Popup(currentCategoryIndex, new string[] { "墙壁", "地板", "楼梯", "灯带", "挂饰", "望远镜", "昆虫房", "宠物屋", "鱼缸", "熊猫", "鹦鹉", "桌子" });
        item.categoryId = categoryValues[newCategoryIndex + 1];
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("描述:", GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();
        item.description = EditorGUILayout.TextArea(item.description, GUILayout.Height(80));

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    private int GetCategoryIndexByValue(int categoryValue)
    {
        for (int i = 1; i < categoryValues.Length; i++)
        {
            if (categoryValues[i] == categoryValue)
                return i - 1;
        }
        return 0;
    }

    #endregion

    #region Helper Methods

    private Color GetCategoryColor(int categoryId)
    {
        switch (categoryId)
        {
            case 51: return new Color(0.8f, 0.7f, 0.5f); // 墙壁
            case 52: return new Color(0.6f, 0.4f, 0.2f); // 地板
            case 53: return new Color(0.7f, 0.5f, 0.3f); // 楼梯
            case 54: return new Color(1f, 0.9f, 0.3f);   // 灯带
            case 55: return new Color(0.9f, 0.3f, 0.5f); // 挂饰
            case 56: return new Color(0.3f, 0.6f, 0.8f); // 望远镜
            case 57: return new Color(0.3f, 0.8f, 0.3f); // 昆虫房
            case 58: return new Color(0.8f, 0.5f, 0.2f); // 宠物屋
            case 59: return new Color(0.2f, 0.6f, 0.9f); // 鱼缸
            case 60: return new Color(0.2f, 0.2f, 0.2f); // 熊猫
            case 61: return new Color(0.9f, 0.4f, 0.1f); // 鹦鹉
            case 62: return new Color(0.7f, 0.5f, 0.3f); // 桌子
            default: return Color.gray;
        }
    }

    private bool IsIdDuplicate(int id, int excludeId)
    {
        return decorationList.Any(d => d.id == id && d.id != excludeId);
    }

    private bool IsIdInRange(int id, int categoryId)
    {
        int startId = 0, endId = 0;
        for (int i = 0; i < categoryValues.Length; i++)
        {
            if (categoryValues[i] == categoryId)
            {
                startId = categoryStartIds[i];
                endId = categoryEndIds[i];
                break;
            }
        }
        return id >= startId && id <= endId;
    }

    #endregion

    #region Data Operations

    private void LoadData()
    {
        string fullPath = Path.Combine(Application.dataPath, RELATIVE_PATH);

        decorationList = new List<IndoorSkinData>();

        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var wrapper = JsonUtility.FromJson<IndoorSkinListWrapper>(json);
                if (wrapper != null && wrapper.decorations != null)
                {
                    decorationList = wrapper.decorations.ToList();
                    Z_Logger.Log($"成功加载 {decorationList.Count} 条数据");
                }
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"加载数据失败: {e.Message}");
            }
        }

        if (decorationList.Count == 0)
        {
            AddDefaultData();
        }
    }

    private void SaveData()
    {
        string fullPath = Path.Combine(Application.dataPath, RELATIVE_PATH);
        string directory = Path.GetDirectoryName(fullPath);

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var wrapper = new IndoorSkinListWrapper { decorations = decorationList };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(fullPath, json);

        AssetDatabase.Refresh();
        Z_Logger.Log($"保存成功: {fullPath}");
    }

    private void QuickCreateDecoration()
    {
        int categoryId = categoryValues[newCategoryIndex + 1];

        if (!IsIdInRange(newId, categoryId))
        {
            EditorUtility.DisplayDialog("错误", $"ID {newId} 不在 {categoryNameMap[categoryId]} 的范围内！\n范围: {categoryStartIds[newCategoryIndex + 1]}-{categoryEndIds[newCategoryIndex + 1]}", "确定");
            return;
        }

        if (IsIdDuplicate(newId, -1))
        {
            EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在！", "确定");
            return;
        }

        var newItem = new IndoorSkinData
        {
            id = newId,
            name = newName,
            description = "",
            categoryId = categoryId
        };

        decorationList.Add(newItem);
        decorationList = decorationList.OrderBy(d => d.id).ToList();
        SaveData();

        newId = FindNextAvailableId(categoryId);
        if (newId == -1) newId = categoryStartIds[newCategoryIndex + 1];

        EditorUtility.DisplayDialog("成功", $"已创建 [{newItem.id}] {newItem.name}", "确定");
        Repaint();
    }

    private int FindNextAvailableId(int categoryId)
    {
        int startId = 0, endId = 0;
        for (int i = 0; i < categoryValues.Length; i++)
        {
            if (categoryValues[i] == categoryId)
            {
                startId = categoryStartIds[i];
                endId = categoryEndIds[i];
                break;
            }
        }

        var existingIds = decorationList.Where(d => d.categoryId == categoryId).Select(d => d.id).ToHashSet();
        for (int id = startId; id <= endId; id++)
        {
            if (!existingIds.Contains(id))
                return id;
        }
        return -1;
    }

    private void AddDefaultData()
    {
        // 默认数据已在JSON中，这里不重复添加
    }

    #endregion
}
#endif
