#if UNITY_EDITOR
// ==================== ItemDataEditor.cs ====================
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ItemDataEditor : EditorWindow
{
    private string inputPath = "JsonData/Game/Items/items";
    private List<ItemData> items = new List<ItemData>();
    private int selectedIndex = -1;
    private Vector2 scrollPosition;

    private float col1 = 60;
    private float col2 = 120;
    private float col3 = 60;
    private float col4 = 200;
    private float col5 = 60;
    private float col6 = 60;
    private float col7 = 60;
    private float col8 = 50;

    private bool showFishList = true;
    private bool showBaitList = true;
    private bool showTrashList = true;
    private bool showNestBaitList = true;
    private bool showOtherList = true;
    private bool showCollectionInfoList = true;  // ✅ 新增：图鉴情报列表

    // ===== 筛选相关 =====
    private int selectedTypeFilter = -1; // -1=全部
    private string[] typeFilterOptions;

    // ===== 新增：水产岛屿筛选 =====
    private int selectedFishIslandFilter = -1; // -1=全部
    private string[] fishIslandFilterOptions;

    [MenuItem("Tools/游戏内容/3.物品通用数据/2.编辑价格数据")]
    public static void ShowWindow()
    {
        ItemDataEditor window = GetWindow<ItemDataEditor>("物品价格数据编辑器");
        window.minSize = new Vector2(750, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadItemsFromJson();
        BuildTypeFilterOptions();
        BuildFishIslandFilterOptions();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawDataTable();
        DrawEditPanel();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LoadItemsFromJson();
            BuildTypeFilterOptions();
            BuildFishIslandFilterOptions();
        }

        GUILayout.Space(20);

        // ===== 类型筛选下拉框 =====
        EditorGUILayout.LabelField("筛选类型:", GUILayout.Width(60));

        if (selectedTypeFilter >= typeFilterOptions.Length)
            selectedTypeFilter = 0;

        int newFilterIndex = EditorGUILayout.Popup(selectedTypeFilter + 1, typeFilterOptions, GUILayout.Width(120));
        selectedTypeFilter = newFilterIndex - 1;

        GUILayout.Space(10);

        // ===== 新增：水产岛屿筛选（仅当筛选类型为"水产"或"全部"时可用）=====
        bool enableIslandFilter = (selectedTypeFilter == -1) ||
                                  (selectedTypeFilter < typeFilterOptions.Length &&
                                   typeFilterOptions[selectedTypeFilter + 1].Contains("水产"));

        GUI.enabled = enableIslandFilter;

        EditorGUILayout.LabelField("鱼类岛屿:", GUILayout.Width(60));

        if (selectedFishIslandFilter >= fishIslandFilterOptions.Length)
            selectedFishIslandFilter = 0;

        int newIslandIndex = EditorGUILayout.Popup(selectedFishIslandFilter + 1, fishIslandFilterOptions, GUILayout.Width(120));
        selectedFishIslandFilter = newIslandIndex - 1;

        GUI.enabled = true;

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {GetFilteredItems().Count} / {items.Count} 条数据", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    /// <summary>
    /// 构建类型筛选选项列表
    /// </summary>
    private void BuildTypeFilterOptions()
    {
        var typeSet = new HashSet<int>();
        foreach (var item in items)
        {
            typeSet.Add(item.itemType);
        }

        var options = new List<string>();
        options.Add("全部");

        var sortedTypes = typeSet.OrderBy(t => t).ToList();
        foreach (var type in sortedTypes)
        {
            string typeName = GetItemTypeName(type);
            options.Add($"{typeName} ({type})");
        }

        typeFilterOptions = options.ToArray();
    }

    /// <summary>
    /// 构建水产岛屿筛选选项列表
    /// </summary>
    private void BuildFishIslandFilterOptions()
    {
        var islandNames = new Dictionary<int, string>
        {
            { 101, "融冠岛" },
            { 102, "彩虹岛" },
            { 103, "场景三" },
            { 104, "场景四" },
            { 105, "场景五" }
        };

        var options = new List<string>();
        options.Add("全部");

        // 从 fishes.json 读取岛屿数据
        string fishPath = Path.Combine(Application.dataPath, "Resources", "JsonData/Game/BagItem/fishes.json");
        if (File.Exists(fishPath))
        {
            try
            {
                string json = File.ReadAllText(fishPath);
                var wrapper = JsonUtility.FromJson<FishListWrapper>(json);
                if (wrapper?.fishes != null)
                {
                    var islandSet = new HashSet<int>();
                    foreach (var fish in wrapper.fishes)
                    {
                        islandSet.Add(fish.islandId);
                    }

                    // 排序，0和-1放在最后
                    var sortedIslands = islandSet
                        .Where(id => id > 0)
                        .OrderBy(id => id)
                        .ToList();

                    // 添加0和-1（如果有）
                    if (islandSet.Contains(0))
                        sortedIslands.Add(0);
                    if (islandSet.Contains(-1))
                        sortedIslands.Add(-1);

                    foreach (var id in sortedIslands)
                    {
                        string name = id switch
                        {
                            0 => "所有岛屿",
                            -1 => "无岛屿",
                            _ => islandNames.ContainsKey(id) ? islandNames[id] : $"岛屿{id}"
                        };
                        options.Add($"{name} ({id})");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[物品编辑器] 读取鱼类岛屿失败: {e.Message}");
            }
        }

        // 如果没有读取到数据，使用默认岛屿列表
        if (options.Count <= 1)
        {
            foreach (var kvp in islandNames.OrderBy(k => k.Key))
            {
                options.Add($"{kvp.Value} ({kvp.Key})");
            }
        }

        fishIslandFilterOptions = options.ToArray();
    }

    /// <summary>
    /// 获取经过筛选后的数据列表
    /// </summary>
    private List<ItemData> GetFilteredItems()
    {
        var result = items;

        // 类型筛选
        if (selectedTypeFilter != -1 && typeFilterOptions.Length > 1)
        {
            string selectedOption = typeFilterOptions[selectedTypeFilter + 1];
            int startIndex = selectedOption.LastIndexOf('(');
            int endIndex = selectedOption.LastIndexOf(')');
            if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
            {
                string idStr = selectedOption.Substring(startIndex + 1, endIndex - startIndex - 1);
                if (int.TryParse(idStr, out int typeId))
                {
                    result = result.Where(item => item.itemType == typeId).ToList();
                }
            }
        }

        // 水产岛屿筛选（仅当筛选类型为"全部"或"水产"时生效）
        if (selectedFishIslandFilter != -1 && fishIslandFilterOptions.Length > 1)
        {
            // 检查当前筛选类型是否是水产或全部
            bool isFishType = (selectedTypeFilter == -1) ||
                              (selectedTypeFilter < typeFilterOptions.Length &&
                               typeFilterOptions[selectedTypeFilter + 1].Contains("水产"));

            if (isFishType)
            {
                string selectedOption = fishIslandFilterOptions[selectedFishIslandFilter + 1];
                int startIndex = selectedOption.LastIndexOf('(');
                int endIndex = selectedOption.LastIndexOf(')');
                if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                {
                    string idStr = selectedOption.Substring(startIndex + 1, endIndex - startIndex - 1);
                    if (int.TryParse(idStr, out int islandId))
                    {
                        // 获取该岛屿的鱼类ID列表
                        var fishIds = GetFishIdsByIsland(islandId);
                        result = result.Where(item => fishIds.Contains(item.id)).ToList();
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 根据岛屿获取鱼类ID列表
    /// </summary>
    private HashSet<int> GetFishIdsByIsland(int islandId)
    {
        var fishIds = new HashSet<int>();
        string fishPath = Path.Combine(Application.dataPath, "Resources", "JsonData/Game/BagItem/fishes.json");

        if (File.Exists(fishPath))
        {
            try
            {
                string json = File.ReadAllText(fishPath);
                var wrapper = JsonUtility.FromJson<FishListWrapper>(json);
                if (wrapper?.fishes != null)
                {
                    foreach (var fish in wrapper.fishes)
                    {
                        if (fish.islandId == islandId)
                        {
                            fishIds.Add(fish.id);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[物品编辑器] 读取鱼类数据失败: {e.Message}");
            }
        }

        return fishIds;
    }

    private void DrawDataTable()
    {
        EditorGUILayout.LabelField("物品列表", EditorStyles.boldLabel);

        var filteredItems = GetFilteredItems();

        if (filteredItems.Count == 0)
        {
            string message = items.Count == 0 ? "暂无数据，点击\"刷新\"加载" : "当前筛选条件下没有数据";
            EditorGUILayout.LabelField(message, EditorStyles.centeredGreyMiniLabel);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        List<ItemData> fishItems = filteredItems.FindAll(item => item.itemType == 1);
        List<ItemData> baitItems = filteredItems.FindAll(item => item.itemType == 2);
        List<ItemData> trashItems = filteredItems.FindAll(item => item.itemType == 3);
        List<ItemData> nestBaitItems = filteredItems.FindAll(item => item.itemType == 6);
        List<ItemData> collectionInfoItems = filteredItems.FindAll(item => item.itemType == 7);  // ✅ 新增：图鉴情报
        List<ItemData> otherItems = filteredItems.FindAll(item => item.itemType == 4 || item.itemType == 5);

        DrawItemGroup("🐟 水产数据", fishItems, ref showFishList);
        DrawItemGroup("🎣 饵料数据", baitItems, ref showBaitList);
        DrawItemGroup("🗑️ 垃圾数据", trashItems, ref showTrashList);
        DrawItemGroup("🪣 窝料数据", nestBaitItems, ref showNestBaitList);
        DrawItemGroup("📖 图鉴情报数据", collectionInfoItems, ref showCollectionInfoList);  // ✅ 新增
        DrawItemGroup("📦 其他物品", otherItems, ref showOtherList);

        EditorGUILayout.EndScrollView();
        GUILayout.Space(5);
    }

    private void DrawItemGroup(string title, List<ItemData> groupItems, ref bool isExpanded)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, EditorStyles.foldoutHeader);

        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.6f);
        EditorGUILayout.LabelField($"共 {groupItems.Count} 条", GUILayout.Width(60));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (isExpanded && groupItems.Count > 0)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal("box");
            DrawResizableColumn("ID", ref col1);
            DrawResizableColumn("名称", ref col2);
            DrawResizableColumn("类型", ref col3);
            DrawResizableColumn("描述", ref col4);
            DrawResizableColumn("出售价", ref col5);
            DrawResizableColumn("购买价", ref col6);
            DrawResizableColumn("所属ID", ref col7);
            DrawResizableColumn("唯一", ref col8);
            EditorGUILayout.LabelField("操作", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < groupItems.Count; i++)
            {
                ItemData item = groupItems[i];
                int originalIndex = items.IndexOf(item);

                if (selectedIndex == originalIndex)
                    GUI.backgroundColor = Color.cyan;
                else if (i % 2 == 0)
                    GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                else
                    GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

                EditorGUILayout.BeginHorizontal("box");

                EditorGUILayout.LabelField(item.id.ToString(), GUILayout.Width(col1));
                EditorGUILayout.LabelField(item.name, GUILayout.Width(col2));
                EditorGUILayout.LabelField(GetItemTypeName(item.itemType), GUILayout.Width(col3));
                EditorGUILayout.LabelField(item.description.Length > 25 ? item.description.Substring(0, 25) + "..." : item.description, GUILayout.Width(col4));
                EditorGUILayout.LabelField(item.sellPrice.ToString(), GUILayout.Width(col5));
                EditorGUILayout.LabelField(item.buyPrice.ToString(), GUILayout.Width(col6));
                EditorGUILayout.LabelField(item.categoryId.ToString(), GUILayout.Width(col7));
                EditorGUILayout.LabelField(item.isUnique ? "✓" : "✗", GUILayout.Width(col8));

                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = originalIndex;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }
        else if (isExpanded && groupItems.Count == 0)
        {
            EditorGUILayout.LabelField("无数据", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawResizableColumn(string title, ref float width)
    {
        EditorGUILayout.LabelField(title, GUILayout.Width(width));
    }

    private string GetItemTypeName(int itemType)
    {
        switch (itemType)
        {
            case 1: return "水产";
            case 2: return "饵料";
            case 3: return "垃圾";
            case 4: return "室外皮肤";
            case 5: return "室内皮肤";
            case 6: return "窝料";
            case 7: return "图鉴情报";  // ✅ 新增
            default: return "未知";
        }
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < items.Count)
        {
            ItemData item = items[selectedIndex];

            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("基础信息 (不可修改)", EditorStyles.boldLabel);
            GUILayout.Space(5);

            GUI.enabled = false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(100));
            EditorGUILayout.IntField(item.id, GUILayout.Width(100));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("名称:", GUILayout.Width(100));
            EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(100), GUILayout.Height(40));
            EditorGUILayout.TextArea(item.description, GUILayout.Height(40));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("物品类型:", GUILayout.Width(100));
            EditorGUILayout.IntField(item.itemType, GUILayout.Width(100));
            EditorGUILayout.LabelField($"({GetItemTypeName(item.itemType)})", GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("可编辑信息", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("出售价格:", GUILayout.Width(100));
            item.sellPrice = EditorGUILayout.IntField(item.sellPrice, GUILayout.Width(100));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("购买价格:", GUILayout.Width(100));
            item.buyPrice = EditorGUILayout.IntField(item.buyPrice, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("所属分类ID:", GUILayout.Width(100));
            item.categoryId = EditorGUILayout.IntField(item.categoryId, GUILayout.Width(100));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("图标路径:", GUILayout.Width(100));
            item.iconPath = EditorGUILayout.TextField(item.iconPath);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("是否唯一:", GUILayout.Width(100));
            item.isUnique = EditorGUILayout.Toggle(item.isUnique, GUILayout.Width(30));
            EditorGUILayout.LabelField(item.isUnique ? "唯一（可堆叠数量上限1）" : "不唯一（可堆叠）", GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("图鉴情报页面ID列表", EditorStyles.boldLabel);

            if (item.collectionInfoPages == null)
            {
                item.collectionInfoPages = new List<int>();
            }

            for (int i = 0; i < item.collectionInfoPages.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"页面 {i + 1}:", GUILayout.Width(60));
                item.collectionInfoPages[i] = EditorGUILayout.IntField(item.collectionInfoPages[i]);
                if (GUILayout.Button("移除", GUILayout.Width(50)))
                {
                    item.collectionInfoPages.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("添加页面:", GUILayout.Width(60));
            int newPageId = EditorGUILayout.IntField(0);
            if (GUILayout.Button("添加", GUILayout.Width(50)))
            {
                if (newPageId > 0 && !item.collectionInfoPages.Contains(newPageId))
                {
                    item.collectionInfoPages.Add(newPageId);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            GUILayout.Space(15);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("保存修改", GUILayout.Width(120), GUILayout.Height(30)))
            {
                SaveItemsToJson();
                EditorUtility.DisplayDialog("成功", "数据已保存", "确定");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("请从上方列表选择要编辑的项", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void LoadItemsFromJson()
    {
        string fullPath = Path.Combine(Application.dataPath, "Resources", $"{inputPath}.json");

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[物品编辑器] 文件不存在: {fullPath}");
            items = new List<ItemData>();
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            ItemListWrapper wrapper = JsonUtility.FromJson<ItemListWrapper>(json);

            if (wrapper == null || wrapper.items == null)
            {
                Debug.LogError($"[物品编辑器] JSON文件解析失败！");
                items = new List<ItemData>();
                return;
            }

            items = wrapper.items;
            selectedIndex = -1;

            Debug.Log($"[物品编辑器] 加载了 {items.Count} 条物品数据");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品编辑器] 错误: {e.Message}");
            items = new List<ItemData>();
        }
        Repaint();
    }

    private void SaveItemsToJson()
    {
        ItemListWrapper wrapper = new ItemListWrapper
        {
            items = items
        };

        string json = JsonUtility.ToJson(wrapper, true);
        string fullPath = Path.Combine(Application.dataPath, "Resources", $"{inputPath}.json");

        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"[物品编辑器] 已保存 {items.Count} 条物品数据");
    }

    [System.Serializable]
    private class ItemListWrapper
    {
        public List<ItemData> items;
    }

    [System.Serializable]
    private class FishListWrapper
    {
        public List<FishData> fishes;
    }

    [System.Serializable]
    private class FishData
    {
        public int id;
        public string name;
        public int rarityId;
        public string description;
        public int islandId;
        public List<int> preferredIslandIds;
        public List<int> preferredTimeIds;
        public List<int> preferredBaitIds;
        public List<int> preferredWeatherIds;
        public int fishSpeciesId;
        public int struggleTime;
        public float flashProbability;
        public float baseWeight;
        public float baseExp;
        public float scale;
    }
}
#endif
