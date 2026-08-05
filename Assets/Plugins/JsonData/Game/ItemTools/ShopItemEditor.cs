#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 商场物品编辑器
/// 从物品数据中提取可购买物品（购买价格 > 0），按分类显示，支持编辑是否售卖
/// </summary>
public class ShopItemEditor : EditorWindow
{
    #region 数据路径配置
    private const string ITEM_DATA_PATH = "Resources/JsonData/Game/Items/items.json";
    private const string CATEGORY_DATA_PATH = "Resources/JsonData/Game/GameFramework/itemCategories.json";
    private const string SHOP_DATA_PATH = "Resources/JsonData/Game/Shop/shopItems.json";
    #endregion

    #region UI状态变量
    private Vector2 scrollPosition;
    private Dictionary<int, bool> categoryFoldoutStates = new Dictionary<int, bool>();
    private Dictionary<int, int> categoryPageIndex = new Dictionary<int, int>();
    private const int ITEMS_PER_PAGE = 8;

    private List<ShopItemData> shopItems = new List<ShopItemData>();
    private List<ItemData> allItems = new List<ItemData>();
    private CategoryListWrapper categoryWrapper;
    private Dictionary<int, string> categoryNameMap = new Dictionary<int, string>();

    private bool isDataLoaded = false;
    private string searchFilter = "";
    #endregion

    #region 菜单入口
    [MenuItem("Tools/游戏内容/3.物品通用数据/3.商场物品编辑器(基于价格)", false)]
    public static void ShowWindow()
    {
        ShopItemEditor window = GetWindow<ShopItemEditor>("商场物品编辑器");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }
    #endregion

    #region Unity生命周期
    private void OnEnable()
    {
        LoadAllData();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawStatistics();

        if (!isDataLoaded || shopItems.Count == 0)
        {
            EditorGUILayout.HelpBox("暂无商场物品数据，请点击\"刷新\"按钮加载数据", MessageType.Info);
            return;
        }

        DrawCategoryList();
    }
    #endregion

    #region 数据加载
    private void LoadAllData()
    {
        LoadCategoryData();
        LoadAllItems();
        LoadShopItems();
        isDataLoaded = true;
        Debug.Log($"[商场物品编辑器] 加载完成: 总物品={allItems.Count}, 商场物品={shopItems.Count}");
    }

    private void LoadCategoryData()
    {
        string fullPath = Path.Combine(Application.dataPath, CATEGORY_DATA_PATH);
        categoryNameMap.Clear();

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[商场物品编辑器] 分类文件不存在: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            categoryWrapper = JsonUtility.FromJson<CategoryListWrapper>(json);

            if (categoryWrapper?.categories != null)
            {
                foreach (var cat in categoryWrapper.categories)
                {
                    categoryNameMap[cat.id] = cat.name;
                    if (cat.subCategories != null)
                    {
                        foreach (var sub in cat.subCategories)
                        {
                            categoryNameMap[sub.id] = sub.name;
                        }
                    }
                }
                Debug.Log($"[商场物品编辑器] 加载分类: {categoryNameMap.Count} 个");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[商场物品编辑器] 加载分类失败: {e.Message}");
        }
    }

    private void LoadAllItems()
    {
        string fullPath = Path.Combine(Application.dataPath, ITEM_DATA_PATH);
        allItems.Clear();

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[商场物品编辑器] 物品文件不存在: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<ItemListWrapper>(json);
            if (wrapper?.items != null)
            {
                allItems = wrapper.items;
                Debug.Log($"[商场物品编辑器] 加载物品: {allItems.Count} 条");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[商场物品编辑器] 加载物品失败: {e.Message}");
        }
    }

    private void LoadShopItems()
    {
        string fullPath = Path.Combine(Application.dataPath, SHOP_DATA_PATH);
        shopItems.Clear();

        // 如果文件存在，加载已有数据
        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var wrapper = JsonUtility.FromJson<ShopItemListWrapper>(json);
                if (wrapper?.shopItems != null)
                {
                    shopItems = wrapper.shopItems;
                    Debug.Log($"[商场物品编辑器] 加载商场数据: {shopItems.Count} 条");
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[商场物品编辑器] 加载商场数据失败: {e.Message}");
            }
        }

        // 如果文件不存在或加载失败，从物品数据提取
        ExtractShopItemsFromAllItems();
    }

    private void ExtractShopItemsFromAllItems()
    {
        shopItems.Clear();
        int extractedCount = 0;

        foreach (var item in allItems)
        {
            // 只提取购买价格 > 0 的物品
            if (item.buyPrice > 0)
            {
                var shopItem = new ShopItemData
                {
                    itemId = item.id,
                    price = item.buyPrice,
                    stock = 99, // 默认库存
                    isOnSale = true, // 默认上架
                    categoryId = item.categoryId
                };
                shopItems.Add(shopItem);
                extractedCount++;
            }
        }

        Debug.Log($"[商场物品编辑器] 从物品数据提取商场物品: {extractedCount} 条");
        SaveShopItems();
    }
    #endregion

    #region UI绘制
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // 刷新按钮
        if (GUILayout.Button("🔄 刷新", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            LoadAllData();
            Repaint();
        }

        GUILayout.Space(10);

        // 搜索框
        GUILayout.Label("搜索:", GUILayout.Width(40));
        string newSearch = EditorGUILayout.TextField(searchFilter, GUILayout.Width(150));
        if (newSearch != searchFilter)
        {
            searchFilter = newSearch.ToLower();
            Repaint();
        }

        GUILayout.FlexibleSpace();

        // 统计信息
        int onSaleCount = shopItems.Count(s => s.isOnSale);
        EditorGUILayout.LabelField($"共 {shopItems.Count} 件商品 | 上架: {onSaleCount} 件", GUILayout.Width(200));

        // 保存按钮
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("💾 保存", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            SaveShopItems();
            EditorUtility.DisplayDialog("成功", $"商场数据已保存！\n共 {shopItems.Count} 件商品", "确定");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawStatistics()
    {
        if (shopItems.Count == 0) return;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("📊 数据统计", EditorStyles.boldLabel);

        // 按类型统计
        var fishItems = shopItems.Where(s => GetItemType(s.itemId) == 1).ToList();
        var baitItems = shopItems.Where(s => GetItemType(s.itemId) == 2).ToList();
        var skinItems = shopItems.Where(s => GetItemType(s.itemId) == 4 || GetItemType(s.itemId) == 5).ToList();
        var otherItems = shopItems.Where(s => GetItemType(s.itemId) >= 3).ToList();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"🐟 水产: {fishItems.Count} 件", GUILayout.Width(120));
        EditorGUILayout.LabelField($"🎣 饵料: {baitItems.Count} 件", GUILayout.Width(120));
        EditorGUILayout.LabelField($"🎨 皮肤: {skinItems.Count} 件", GUILayout.Width(120));
        EditorGUILayout.LabelField($"📦 其他: {otherItems.Count} 件", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawCategoryList()
    {
        // 按分类分组
        var groupedItems = shopItems
            .Where(s => string.IsNullOrEmpty(searchFilter) ||
                        GetItemName(s.itemId).ToLower().Contains(searchFilter) ||
                        s.itemId.ToString().Contains(searchFilter))
            .GroupBy(s => s.categoryId)
            .OrderBy(g => g.Key)
            .ToList();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var group in groupedItems)
        {
            string categoryName = GetCategoryName(group.Key);
            DrawCategoryGroup(group.Key, categoryName, group.ToList());
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCategoryGroup(int categoryId, string categoryName, List<ShopItemData> items)
    {
        // 初始化折叠状态
        if (!categoryFoldoutStates.ContainsKey(categoryId))
        {
            categoryFoldoutStates[categoryId] = true;
        }

        // 初始化分页
        if (!categoryPageIndex.ContainsKey(categoryId))
        {
            categoryPageIndex[categoryId] = 0;
        }

        EditorGUILayout.BeginVertical("box");

        // 分类标题（可折叠）
        EditorGUILayout.BeginHorizontal();
        categoryFoldoutStates[categoryId] = EditorGUILayout.Foldout(
            categoryFoldoutStates[categoryId],
            $"📂 {categoryName} (ID: {categoryId})",
            true,
            EditorStyles.foldoutHeader
        );

        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.6f);
        int onSaleCount = items.Count(s => s.isOnSale);
        EditorGUILayout.LabelField($"共 {items.Count} 件 | 上架: {onSaleCount}", GUILayout.Width(150));
        GUI.backgroundColor = Color.white;

        // 全选/取消全选按钮
        if (GUILayout.Button("全部上架", GUILayout.Width(70)))
        {
            foreach (var item in items) item.isOnSale = true;
        }
        if (GUILayout.Button("全部下架", GUILayout.Width(70)))
        {
            foreach (var item in items) item.isOnSale = false;
        }

        EditorGUILayout.EndHorizontal();

        if (categoryFoldoutStates[categoryId])
        {
            EditorGUI.indentLevel++;

            // 表头
            DrawTableHeader();

            // 分页计算
            int totalPages = Mathf.CeilToInt((float)items.Count / ITEMS_PER_PAGE);
            int currentPage = categoryPageIndex[categoryId];
            int startIndex = currentPage * ITEMS_PER_PAGE;
            int endIndex = Mathf.Min(startIndex + ITEMS_PER_PAGE, items.Count);

            // 显示当前页数据
            for (int i = startIndex; i < endIndex; i++)
            {
                var shopItem = items[i];
                DrawShopItemRow(shopItem);
            }

            // 分页控制
            if (totalPages > 1)
            {
                DrawPagination(categoryId, currentPage, totalPages);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawTableHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("ID", GUILayout.Width(60));
        EditorGUILayout.LabelField("物品名称", GUILayout.Width(130));
        EditorGUILayout.LabelField("价格", GUILayout.Width(60));
        EditorGUILayout.LabelField("库存", GUILayout.Width(60));
        EditorGUILayout.LabelField("上架", GUILayout.Width(50));
        EditorGUILayout.LabelField("操作", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawShopItemRow(ShopItemData shopItem)
    {
        string itemName = GetItemName(shopItem.itemId);
        int itemType = GetItemType(shopItem.itemId);

        EditorGUILayout.BeginHorizontal("box");

        // 交替行背景色
        GUI.backgroundColor = (shopItem.itemId % 2 == 0) ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.88f, 0.88f, 0.88f);

        // ID
        EditorGUILayout.LabelField(shopItem.itemId.ToString(), GUILayout.Width(60));

        // 名称 + 类型图标
        string typeIcon = GetItemTypeIcon(itemType);
        EditorGUILayout.LabelField($"{typeIcon} {itemName}", GUILayout.Width(130));

        // 价格（可编辑）
        shopItem.price = EditorGUILayout.IntField(shopItem.price, GUILayout.Width(60));

        // 库存（可编辑）
        shopItem.stock = EditorGUILayout.IntField(shopItem.stock, GUILayout.Width(60));

        // 上架开关（可编辑）
        shopItem.isOnSale = EditorGUILayout.Toggle(shopItem.isOnSale, GUILayout.Width(50));

        // 操作按钮
        if (GUILayout.Button("定位物品", GUILayout.Width(70)))
        {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>($"Assets/Resources/UI/Icon/{GetItemIconPath(shopItem.itemId)}"));
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPagination(int categoryId, int currentPage, int totalPages)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // 上一页
        GUI.enabled = currentPage > 0;
        if (GUILayout.Button("◀", GUILayout.Width(30)))
        {
            categoryPageIndex[categoryId]--;
        }
        GUI.enabled = true;

        // 页码
        EditorGUILayout.LabelField($"{currentPage + 1} / {totalPages}", GUILayout.Width(60));

        // 下一页
        GUI.enabled = currentPage < totalPages - 1;
        if (GUILayout.Button("▶", GUILayout.Width(30)))
        {
            categoryPageIndex[categoryId]++;
        }
        GUI.enabled = true;

        // 跳转
        int jumpPage = EditorGUILayout.IntField(currentPage + 1, GUILayout.Width(40));
        if (jumpPage >= 1 && jumpPage <= totalPages && jumpPage != currentPage + 1)
        {
            categoryPageIndex[categoryId] = jumpPage - 1;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
    #endregion

    #region 辅助方法
    private string GetCategoryName(int categoryId)
    {
        if (categoryNameMap.TryGetValue(categoryId, out string name))
        {
            return name;
        }
        return $"分类 {categoryId}";
    }

    private string GetItemName(int itemId)
    {
        var item = allItems.FirstOrDefault(i => i.id == itemId);
        return item?.name ?? $"物品 {itemId}";
    }

    private int GetItemType(int itemId)
    {
        var item = allItems.FirstOrDefault(i => i.id == itemId);
        return item?.itemType ?? 0;
    }

    private string GetItemTypeIcon(int itemType)
    {
        switch (itemType)
        {
            case 1: return "🐟";
            case 2: return "🎣";
            case 3: return "🪣";
            case 4: return "🏕️";
            case 5: return "🏠";
            default: return "📦";
        }
    }

    private string GetItemIconPath(int itemId)
    {
        var item = allItems.FirstOrDefault(i => i.id == itemId);
        return item?.iconPath ?? $"";
    }

    private void SaveShopItems()
    {
        var wrapper = new ShopItemListWrapper
        {
            shopItems = shopItems
        };

        string json = JsonUtility.ToJson(wrapper, true);
        string fullPath = Path.Combine(Application.dataPath, SHOP_DATA_PATH);

        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"[商场物品编辑器] 保存商场数据: {shopItems.Count} 条");
    }
    #endregion

    #region 数据类定义
    [System.Serializable]
    public class ShopItemData
    {
        public int itemId;
        public int price;
        public int stock;
        public bool isOnSale;
        public int categoryId;
    }

    [System.Serializable]
    public class ShopItemListWrapper
    {
        public List<ShopItemData> shopItems;
    }

    [System.Serializable]
    public class ItemListWrapper
    {
        public List<ItemData> items;
    }

    [System.Serializable]
    public class CategoryListWrapper
    {
        public List<CategoryData> categories;
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
    #endregion
}
#endif
