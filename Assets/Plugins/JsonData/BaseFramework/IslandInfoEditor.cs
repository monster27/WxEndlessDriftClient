#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 岛屿情报编辑器
/// 根据 islands.json 自动生成岛屿情报条目（7001-7099）
/// 只读取岛屿信息和分类信息，不包含价格/上架等商城字段
/// 数据保存在独立的岛屿情报配置文件中
/// </summary>
public class IslandInfoEditor : EditorWindow
{
    private const string ISLAND_DATA_PATH = "Resources/JsonData/BaseFramework/islands.json";
    private const string CATEGORY_DATA_PATH = "Resources/JsonData/Game/GameFramework/itemCategories.json";
    private const string ISLAND_INFO_DATA_PATH = "Resources/JsonData/Game/GameFramework/islandInfo.json";

    private List<IslandData> islandDataList = new List<IslandData>();
    private List<IslandInfoEntry> islandInfoList = new List<IslandInfoEntry>();
    private List<IslandInfoEntry> savedIslandInfoList = new List<IslandInfoEntry>();
    private CategoryListWrapper categoryWrapper;

    private Vector2 scrollPosition;
    private bool isDataLoaded = false;
    private string statusMessage = "";
    private bool isDirty = false;

    // 筛选
    private string searchFilter = "";
    private bool showOnlyWithInfo = true;

    // 列宽
    private float col1 = 70;
    private float col2 = 60;
    private float col3 = 120;
    private float col4 = 150;
    private float col5 = 120;

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/7001_岛屿情报")]
    public static void ShowWindow()
    {
        IslandInfoEditor window = GetWindow<IslandInfoEditor>("岛屿情报编辑器");
        window.minSize = new Vector2(700, 400);
        window.Show();
    }

    private void OnEnable()
    {
        LoadAllData();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (!isDataLoaded || islandDataList.Count == 0)
        {
            EditorGUILayout.HelpBox("请确保 islands.json 存在且包含岛屿数据", MessageType.Info);
            return;
        }

        DrawStatistics();
        DrawDataTable();
        DrawStatusBar();
        DrawSaveButton();
    }

    #region 数据加载

    private void LoadAllData()
    {
        LoadIslandData();
        LoadCategoryData();
        LoadSavedIslandInfo();
        GenerateIslandInfoList();

        if (islandInfoList.Count > 0 && savedIslandInfoList.Count == 0)
        {
            Debug.Log("[岛屿情报编辑器] 未找到已保存的数据，自动创建默认数据");
            SaveIslandInfoData();
            LoadSavedIslandInfo();
            GenerateIslandInfoList();
        }

        isDataLoaded = true;
        isDirty = false;

        Debug.Log($"[岛屿情报编辑器] 加载完成: 岛屿={islandDataList.Count}, 情报={islandInfoList.Count}, 已保存={savedIslandInfoList.Count}");
    }

    private void LoadIslandData()
    {
        string fullPath = Path.Combine(Application.dataPath, ISLAND_DATA_PATH);
        islandDataList.Clear();

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[岛屿情报编辑器] 岛屿数据文件不存在: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<IslandListWrapper>(json);
            if (wrapper?.islands != null)
            {
                islandDataList = wrapper.islands;
                Debug.Log($"[岛屿情报编辑器] 加载岛屿: {islandDataList.Count} 个");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[岛屿情报编辑器] 加载岛屿数据失败: {e.Message}");
        }
    }

    private void LoadCategoryData()
    {
        string fullPath = Path.Combine(Application.dataPath, CATEGORY_DATA_PATH);
        categoryWrapper = null;

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[岛屿情报编辑器] 分类数据文件不存在: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            categoryWrapper = JsonUtility.FromJson<CategoryListWrapper>(json);
            Debug.Log($"[岛屿情报编辑器] 加载分类数据");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[岛屿情报编辑器] 加载分类数据失败: {e.Message}");
        }
    }

    private void LoadSavedIslandInfo()
    {
        string fullPath = Path.Combine(Application.dataPath, ISLAND_INFO_DATA_PATH);
        savedIslandInfoList.Clear();

        if (!File.Exists(fullPath))
        {
            Debug.Log($"[岛屿情报编辑器] 岛屿情报数据文件不存在: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<IslandInfoListWrapper>(json);
            if (wrapper?.islandInfoList != null)
            {
                foreach (var saved in wrapper.islandInfoList)
                {
                    savedIslandInfoList.Add(new IslandInfoEntry
                    {
                        infoId = saved.infoId,
                        islandId = saved.islandId,
                        islandName = saved.islandName,
                        infoName = saved.infoName,
                        categoryId = saved.categoryId,
                        iconPath = saved.iconPath
                    });
                }
                Debug.Log($"[岛屿情报编辑器] 加载已保存岛屿情报: {savedIslandInfoList.Count} 个");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[岛屿情报编辑器] 加载岛屿情报数据失败: {e.Message}");
        }
    }

    private void GenerateIslandInfoList()
    {
        islandInfoList.Clear();

        if (islandDataList == null || islandDataList.Count == 0)
            return;

        var sortedIslands = islandDataList.OrderBy(i => i.id).ToList();

        int baseId = 7001;

        for (int i = 0; i < sortedIslands.Count; i++)
        {
            var island = sortedIslands[i];
            int infoId = baseId + i;

            bool hasCategory = false;
            string categoryName = "";
            int categoryId = 0;
            if (categoryWrapper?.categories != null)
            {
                foreach (var cat in categoryWrapper.categories)
                {
                    if (infoId >= cat.startId && infoId <= cat.endId)
                    {
                        hasCategory = true;
                        categoryName = cat.name;
                        categoryId = cat.id;
                        break;
                    }
                    if (cat.subCategories != null)
                    {
                        foreach (var sub in cat.subCategories)
                        {
                            if (infoId >= sub.startId && infoId <= sub.endId)
                            {
                                hasCategory = true;
                                categoryName = $"{cat.name} > {sub.name}";
                                categoryId = sub.id;
                                break;
                            }
                        }
                    }
                    if (hasCategory) break;
                }
            }

            var savedEntry = savedIslandInfoList.FirstOrDefault(s => s.infoId == infoId);

            var entry = new IslandInfoEntry
            {
                infoId = infoId,
                islandId = island.id,
                islandName = island.name,
                infoName = $"{island.name}岛屿情报",
                categoryExists = hasCategory,
                categoryName = categoryName,
                categoryId = categoryId,
                iconPath = savedEntry?.iconPath ?? $"UI/Icon/IslandInfoIcons/{infoId}"
            };

            islandInfoList.Add(entry);
        }

        Debug.Log($"[岛屿情报编辑器] 生成 {islandInfoList.Count} 个岛屿情报");
    }

    #endregion

    #region UI 绘制

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            LoadAllData();
            Repaint();
        }

        GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
        if (GUILayout.Button("📥 重新生成", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            ReGenerateWithPrompt();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);

        GUILayout.Label("搜索:", GUILayout.Width(35));
        string newSearch = EditorGUILayout.TextField(searchFilter, GUILayout.Width(150));
        if (newSearch != searchFilter)
        {
            searchFilter = newSearch.ToLower();
            Repaint();
        }

        showOnlyWithInfo = EditorGUILayout.ToggleLeft("仅显示有分类的", showOnlyWithInfo, GUILayout.Width(110));

        GUILayout.FlexibleSpace();

        if (isDirty)
        {
            GUI.backgroundColor = new Color(1f, 0.8f, 0.2f);
            EditorGUILayout.LabelField("⚠️ 有未保存的修改", GUILayout.Width(100));
            GUI.backgroundColor = Color.white;
        }

        int total = islandInfoList.Count;
        int withCategory = islandInfoList.Count(i => i.categoryExists);
        EditorGUILayout.LabelField($"共 {total} 个 | 已分类: {withCategory}", GUILayout.Width(180));

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    /// <summary>
    /// 重新生成并询问是否自动保存
    /// </summary>
    private void ReGenerateWithPrompt()
    {
        // 先检查是否有未保存的修改
        if (isDirty)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "有未保存的修改",
                "当前有未保存的修改，重新生成将覆盖这些修改。\n是否先保存当前修改？",
                "保存并重新生成",
                "不保存，直接重新生成",
                "取消"
            );

            if (choice == 2) // 取消
            {
                return;
            }

            if (choice == 0) // 保存并重新生成
            {
                SaveIslandInfoData();
            }
            // choice == 1 不保存，直接重新生成
        }

        // 执行重新生成
        LoadIslandData();
        LoadCategoryData();
        LoadSavedIslandInfo();
        GenerateIslandInfoList();

        // ✅ 重新生成后询问是否自动保存
        int saveChoice = EditorUtility.DisplayDialogComplex(
            "重新生成完成",
            $"已从 islands.json 重新生成 {islandInfoList.Count} 个岛屿情报。\n\n是否自动保存到文件？",
            "自动保存",
            "不保存",
            "取消"
        );

        if (saveChoice == 0) // 自动保存
        {
            SaveIslandInfoData();
            statusMessage = $"✅ 数据已重新生成并保存！共 {islandInfoList.Count} 个岛屿情报";
        }
        else if (saveChoice == 1) // 不保存
        {
            isDirty = true;
            statusMessage = $"⚠️ 数据已重新生成，请点击\"保存\"按钮保存（共 {islandInfoList.Count} 个）";
        }
        else // 取消
        {
            // 恢复之前的数据
            LoadSavedIslandInfo();
            GenerateIslandInfoList();
            statusMessage = "已取消重新生成";
        }

        Repaint();
    }

    private void DrawStatistics()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("📊 数据概览", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"🏝️ 岛屿总数: {islandDataList.Count} 个", GUILayout.Width(180));
        EditorGUILayout.LabelField($"📖 情报总数: {islandInfoList.Count} 个", GUILayout.Width(180));
        EditorGUILayout.LabelField($"✅ 已配置分类: {islandInfoList.Count(i => i.categoryExists)} 个", GUILayout.Width(200));
        EditorGUILayout.LabelField($"❌ 未配置分类: {islandInfoList.Count(i => !i.categoryExists)} 个", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        if (islandInfoList.Count > 0)
        {
            int firstId = islandInfoList.First().infoId;
            int lastId = islandInfoList.Last().infoId;
            EditorGUILayout.LabelField($"📌 情报ID范围: {firstId} - {lastId}  (7001-7099)", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawDataTable()
    {
        var filteredList = GetFilteredList();

        if (filteredList.Count == 0)
        {
            EditorGUILayout.HelpBox("当前筛选条件下没有数据", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("岛屿情报列表", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        DrawTableHeader();

        for (int i = 0; i < filteredList.Count; i++)
        {
            DrawDataRow(filteredList[i], i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTableHeader()
    {
        EditorGUILayout.BeginHorizontal("box");

        GUI.backgroundColor = new Color(0.2f, 0.5f, 0.8f);
        GUI.color = Color.white;

        EditorGUILayout.LabelField("情报ID", GUILayout.Width(col1));
        EditorGUILayout.LabelField("岛屿ID", GUILayout.Width(col2));
        EditorGUILayout.LabelField("岛屿名称", GUILayout.Width(col3));
        EditorGUILayout.LabelField("情报名称", GUILayout.Width(col4));
        EditorGUILayout.LabelField("分类状态", GUILayout.Width(col5));

        GUI.backgroundColor = Color.white;
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDataRow(IslandInfoEntry entry, int index)
    {
        GUI.backgroundColor = index % 2 == 0 ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.88f, 0.88f, 0.88f);

        if (entry.categoryExists)
        {
            GUI.backgroundColor = new Color(0.85f, 1f, 0.85f);
        }

        EditorGUILayout.BeginHorizontal("box");

        EditorGUILayout.LabelField(entry.infoId.ToString(), GUILayout.Width(col1));
        EditorGUILayout.LabelField(entry.islandId.ToString(), GUILayout.Width(col2));
        EditorGUILayout.LabelField(entry.islandName, GUILayout.Width(col3));
        EditorGUILayout.LabelField(entry.infoName, GUILayout.Width(col4));

        string statusText = entry.categoryExists ? "✅ 已配置" : "❌ 未配置";
        GUI.color = entry.categoryExists ? new Color(0.2f, 0.7f, 0.2f) : new Color(0.8f, 0.2f, 0.2f);
        EditorGUILayout.LabelField(statusText, GUILayout.Width(col5));
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = Color.white;
    }

    private void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal();

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.LabelField(statusMessage, EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("💡 岛屿情报由 islands.json 自动生成，仅用于数据提取", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSaveButton()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        GUI.backgroundColor = isDirty ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.6f, 0.6f, 0.6f);
        GUI.enabled = isDirty;

        if (GUILayout.Button("💾 保存岛屿情报数据", GUILayout.Width(180), GUILayout.Height(30)))
        {
            SaveIslandInfoData();
        }

        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
    }

    #endregion

    #region 数据保存

    private void SaveIslandInfoData()
    {
        var saveList = new List<IslandInfoSaveData>();
        foreach (var entry in islandInfoList)
        {
            saveList.Add(new IslandInfoSaveData
            {
                infoId = entry.infoId,
                islandId = entry.islandId,
                islandName = entry.islandName,
                infoName = entry.infoName,
                categoryId = entry.categoryId,
                iconPath = entry.iconPath
            });
        }

        var wrapper = new IslandInfoListWrapper
        {
            islandInfoList = saveList,
            version = "1.0",
            lastUpdateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string json = JsonUtility.ToJson(wrapper, true);
        string fullPath = Path.Combine(Application.dataPath, ISLAND_INFO_DATA_PATH);

        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();

        savedIslandInfoList = islandInfoList.Select(e => new IslandInfoEntry
        {
            infoId = e.infoId,
            islandId = e.islandId,
            islandName = e.islandName,
            infoName = e.infoName,
            categoryExists = e.categoryExists,
            categoryName = e.categoryName,
            categoryId = e.categoryId,
            iconPath = e.iconPath
        }).ToList();

        isDirty = false;
        statusMessage = $"✅ 数据已保存！共 {saveList.Count} 个岛屿情报";
        Debug.Log($"[岛屿情报编辑器] 保存成功: {fullPath}, 共 {saveList.Count} 条");

        EditorUtility.DisplayDialog("保存成功", $"岛屿情报数据已保存！\n共 {saveList.Count} 个岛屿情报\n\n文件路径:\n{fullPath}", "确定");

        Repaint();
    }

    #endregion

    #region 辅助方法

    private List<IslandInfoEntry> GetFilteredList()
    {
        var result = islandInfoList;

        if (!string.IsNullOrEmpty(searchFilter))
        {
            result = result.Where(i =>
                i.islandName.ToLower().Contains(searchFilter) ||
                i.infoName.ToLower().Contains(searchFilter) ||
                i.infoId.ToString().Contains(searchFilter) ||
                i.islandId.ToString().Contains(searchFilter)
            ).ToList();
        }

        if (showOnlyWithInfo)
        {
            result = result.Where(i => i.categoryExists).ToList();
        }

        return result;
    }

    #endregion

    #region 数据类

    [System.Serializable]
    public class IslandData
    {
        public int id;
        public string name;
    }

    [System.Serializable]
    public class IslandListWrapper
    {
        public List<IslandData> islands;
    }

    [System.Serializable]
    public class CategoryListWrapper
    {
        public List<CategoryData> categories;
        public List<string> notes;
    }

    [System.Serializable]
    public class CategoryData
    {
        public int id;
        public string name;
        public string code;
        public string description;
        public int startId;
        public int endId;
        public List<SubCategoryData> subCategories;
    }

    [System.Serializable]
    public class SubCategoryData
    {
        public int id;
        public string name;
        public string description;
        public int startId;
        public int endId;
    }

    [System.Serializable]
    public class IslandInfoSaveData
    {
        public int infoId;
        public int islandId;
        public string islandName;
        public string infoName;
        public int categoryId;
        public string iconPath;
    }

    [System.Serializable]
    public class IslandInfoListWrapper
    {
        public List<IslandInfoSaveData> islandInfoList;
        public string version;
        public string lastUpdateTime;
    }

    public class IslandInfoEntry
    {
        public int infoId;
        public int islandId;
        public string islandName;
        public string infoName;
        public bool categoryExists;
        public string categoryName;
        public int categoryId;
        public string iconPath;
    }

    #endregion
}
#endif
