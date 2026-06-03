// ==================== ItemDataManager.cs ====================
using System.Collections.Generic;
using UnityEngine;
using System.Text;

public class ItemDataManager : SingletonMono<ItemDataManager>
{
    // 背包分类枚举 - 大分类
    public enum BagCategory
    {
        None = 0,
        Fishes = 1,      // 水产
        Baits = 2,       // 饵料
        Equipments = 3,  // 装备
        Decorations = 4, // 装饰
        Pets = 5,        // 宠物
        Special = 6,     // 特殊
        Trash = 9        // 垃圾
    }

    // 分类ID到分类名称的映射（包含大分类和小分类）
    private Dictionary<int, string> categoryIdToName = new Dictionary<int, string>();
    
    // 分类名称到分类ID的映射
    private Dictionary<string, int> categoryNameToId = new Dictionary<string, int>();
    
    // 分类ID到物品列表的映射
    private Dictionary<int, List<object>> categoryIdToItems = new Dictionary<int, List<object>>();
    
    // 物品ID到分类ID的映射
    private Dictionary<int, int> itemIdToCategoryId = new Dictionary<int, int>();
    
    // 小分类ID到大分类ID的映射
    private Dictionary<int, int> subCategoryToMainCategory = new Dictionary<int, int>();
    
    // 物品ID范围到分类ID的映射（用于快速查找）
    private List<(int startId, int endId, int categoryId)> idRangeMappings = new List<(int, int, int)>();

    protected override void Awake()
    {
        base.Awake();
    }

    public void Init()
    {
        // 确保LoadDataManager已加载数据
        if (LoadDataManager.Instance != null)
        {
            if (LoadDataManager.Instance.bagCategories.Count == 0)
            {
                Debug.Log("ItemDataManager: LoadDataManager数据为空，触发重新加载");
                LoadDataManager.Instance.LoadAllData();
                LoadDataManager.Instance.PrintAllData();
            }
        }
        else
        {
            Debug.LogError("ItemDataManager: LoadDataManager.Instance为NULL");
        }
        
        // 强制重新初始化分类映射
        Debug.Log("ItemDataManager: 强制重新初始化分类映射");
        InitCategoryMappings();
        
        // 初始化所有物品数据
        InitAllItems();
        
        // 收集并打印所有流程数据
        string processLog = CollectProcessData();
        Debug.Log("\n" + processLog);
    }

    private void InitCategoryMappings()
    {
        // 清空所有映射
        categoryIdToName.Clear();
        categoryNameToId.Clear();
        categoryIdToItems.Clear();
        itemIdToCategoryId.Clear();
        subCategoryToMainCategory.Clear();
        idRangeMappings.Clear();
        
        if (LoadDataManager.Instance != null)
        {
            if (LoadDataManager.Instance.bagCategories.Count == 0)
            {
                Debug.Log("ItemDataManager: 分类数据为空，触发LoadDataManager重新加载");
                LoadDataManager.Instance.LoadAllData();
            }
            
            // 从LoadDataManager获取分类数据
            List<BagCategoryData> categories = LoadDataManager.Instance.bagCategories;
            
            // 初始化分类映射
            foreach (BagCategoryData category in categories)
            {
                // 注册大分类
                categoryIdToName[category.id] = category.categoryName;
                categoryNameToId[category.categoryName] = category.id;
                categoryIdToItems[category.id] = new List<object>();
                
                // 记录ID范围映射
                idRangeMappings.Add((category.id, category.id, category.id)); // 大分类ID范围
                
                // 注册小分类
                subCategoryToMainCategory[category.id] = category.id; // 大分类的父分类是自己
            }
        }
        else
        {
            Debug.LogError("ItemDataManager: LoadDataManager.Instance为NULL");
        }
    }

    /// <summary>
    /// 初始化所有物品数据
    /// </summary>
    private void InitAllItems()
    {
        // 从items.json加载的物品数据
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.items != null)
        {
            foreach (var item in LoadDataManager.Instance.items)
            {
                // 使用物品的categoryId来分类
                int categoryId = item.categoryId;
                
                if (!categoryIdToItems.ContainsKey(categoryId))
                {
                    categoryIdToItems[categoryId] = new List<object>();
                }
                
                if (!categoryIdToName.ContainsKey(categoryId))
                {
                    // 如果分类ID不在映射中，使用默认名称
                    categoryIdToName[categoryId] = $"分类{categoryId}";
                }
                
                categoryIdToItems[categoryId].Add(item);
                itemIdToCategoryId[item.id] = categoryId;
            }
        }
        
        // 初始化鱼类数据（保持兼容性）
        List<object> fishObjects = new List<object>();
        foreach (var fish in LoadDataManager.Instance.fishes)
        {
            fishObjects.Add(fish);
        }
        InitCategoryItems(BagCategory.Fishes, fishObjects);
        
        // 初始化鱼饵数据（保持兼容性）
        List<object> baitObjects = new List<object>();
        foreach (var bait in LoadDataManager.Instance.baits)
        {
            baitObjects.Add(bait);
        }
        InitCategoryItems(BagCategory.Baits, baitObjects);
    }

    /// <summary>
    /// 通过物品ID范围获取分类ID
    /// </summary>
    public int GetCategoryIdByItemIdRange(int itemId)
    {
        foreach (var mapping in idRangeMappings)
        {
            if (itemId >= mapping.startId && itemId <= mapping.endId)
            {
                return mapping.categoryId;
            }
        }
        return 0;
    }

    /// <summary>
    /// 获取物品的大分类ID
    /// </summary>
    public int GetMainCategoryId(int categoryId)
    {
        if (subCategoryToMainCategory.TryGetValue(categoryId, out int mainId))
        {
            return mainId;
        }
        return categoryId;
    }

    /// <summary>
    /// 获取物品的大分类枚举
    /// </summary>
    public BagCategory GetMainCategoryEnum(int categoryId)
    {
        int mainId = GetMainCategoryId(categoryId);
        if (System.Enum.IsDefined(typeof(BagCategory), mainId))
        {
            return (BagCategory)mainId;
        }
        return BagCategory.None;
    }

    /// <summary>
    /// 收集所有流程数据到一个字符串
    /// </summary>
    private string CollectProcessData()
    {
        StringBuilder sb = new StringBuilder();
        
        sb.AppendLine("====================================");
        sb.AppendLine("         ItemDataManager 流程数据          ");
        sb.AppendLine("====================================");
        
        sb.AppendLine("1. LoadDataManager 状态:");
        sb.AppendLine($"   - LoadDataManager.Instance: {(LoadDataManager.Instance != null ? "OK" : "NULL")}");
        
        sb.AppendLine("2. 分类数据:");
        sb.AppendLine($"   - 分类数量: {categoryIdToName.Count}");
        foreach (var kvp in categoryIdToName)
        {
            sb.AppendLine($"   - ID: {kvp.Key}, 名称: {kvp.Value}");
        }
        
        sb.AppendLine("3. 物品数据:");
        foreach (var kvp in categoryIdToItems)
        {
            string categoryName = GetCategoryNameById(kvp.Key);
            int itemCount = kvp.Value.Count;
            sb.AppendLine($"   - 分类: {categoryName} (ID: {kvp.Key}), 物品数量: {itemCount}");
            
            int displayCount = Mathf.Min(itemCount, 5);
            for (int i = 0; i < displayCount; i++)
            {
                object item = kvp.Value[i];
                string itemInfo = GetItemInfo(item);
                sb.AppendLine($"     [{i + 1}] {itemInfo}");
            }
            
            if (itemCount > 5)
            {
                sb.AppendLine($"     ... 还有 {itemCount - 5} 个物品");
            }
        }
        
        if (LoadDataManager.Instance != null)
        {
            sb.AppendLine("4. LoadDataManager 原始数据:");
            sb.AppendLine($"   - 鱼类数量: {LoadDataManager.Instance.fishes.Count}");
            sb.AppendLine($"   - 鱼饵数量: {LoadDataManager.Instance.baits.Count}");
            sb.AppendLine($"   - 物品数量: {LoadDataManager.Instance.items.Count}");
            sb.AppendLine($"   - 分类数量: {LoadDataManager.Instance.bagCategories.Count}");
        }
        else
        {
            sb.AppendLine("4. LoadDataManager.Instance 为 NULL");
        }
        
        sb.AppendLine("5. 物品ID到分类ID映射:");
        sb.AppendLine($"   - 映射数量: {itemIdToCategoryId.Count}");
        int mappingCount = 0;
        foreach (var kvp in itemIdToCategoryId)
        {
            if (mappingCount < 10)
            {
                string categoryName = GetCategoryNameById(kvp.Value);
                sb.AppendLine($"   - 物品ID: {kvp.Key}, 分类ID: {kvp.Value}, 分类名称: {categoryName}");
            }
            mappingCount++;
        }
        if (mappingCount > 10)
        {
            sb.AppendLine($"   ... 还有 {mappingCount - 10} 个映射");
        }
        
        sb.AppendLine("====================================");
        sb.AppendLine("              流程结束               ");
        sb.AppendLine("====================================");
        
        return sb.ToString();
    }

    // ==================== 分类管理方法 ====================

    public void InitCategoryItems(int categoryId, List<object> items)
    {
        if (!categoryIdToItems.ContainsKey(categoryId))
        {
            categoryIdToItems[categoryId] = new List<object>();
        }
        categoryIdToItems[categoryId].AddRange(items);
        
        foreach (object item in items)
        {
            int itemId = GetItemId(item);
            if (itemId > 0)
            {
                itemIdToCategoryId[itemId] = categoryId;
            }
        }
    }

    public void InitCategoryItems(string categoryName, List<object> items)
    {
        int categoryId = GetCategoryIdByName(categoryName);
        if (categoryId > 0)
        {
            InitCategoryItems(categoryId, items);
        }
    }

    public void InitCategoryItems(BagCategory category, List<object> items)
    {
        int categoryId = (int)category;
        InitCategoryItems(categoryId, items);
    }

    // ==================== 分类查询方法 ====================

    public string GetCategoryNameById(int categoryId)
    {
        if (categoryIdToName.TryGetValue(categoryId, out string name))
        {
            return name;
        }
        return "未知分类";
    }

    public int GetCategoryIdByName(string categoryName)
    {
        if (categoryNameToId.TryGetValue(categoryName, out int id))
        {
            return id;
        }
        return 0;
    }

    public string GetCategoryNameByEnum(BagCategory category)
    {
        int categoryId = (int)category;
        return GetCategoryNameById(categoryId);
    }

    public int GetCategoryIdByEnum(BagCategory category)
    {
        return (int)category;
    }

    public List<int> GetAllCategoryIds()
    {
        return new List<int>(categoryIdToName.Keys);
    }

    public List<string> GetAllCategoryNames()
    {
        return new List<string>(categoryNameToId.Keys);
    }

    // ==================== 物品查询方法 ====================

    public int GetCategoryIdByItemId(int itemId)
    {
        if (itemIdToCategoryId.TryGetValue(itemId, out int categoryId))
        {
            return categoryId;
        }
        
        // 如果直接映射不存在，尝试通过ID范围查找
        return GetCategoryIdByItemIdRange(itemId);
    }

    public string GetCategoryNameByItemId(int itemId)
    {
        int categoryId = GetCategoryIdByItemId(itemId);
        return GetCategoryNameById(categoryId);
    }

    public BagCategory GetCategoryEnumByItemId(int itemId)
    {
        int categoryId = GetCategoryIdByItemId(itemId);
        int mainId = GetMainCategoryId(categoryId);
        if (System.Enum.IsDefined(typeof(BagCategory), mainId))
        {
            return (BagCategory)mainId;
        }
        return BagCategory.None;
    }

    public List<object> GetItemsByCategoryId(int categoryId)
    {
        if (categoryIdToItems.TryGetValue(categoryId, out List<object> items))
        {
            return items;
        }
        return new List<object>();
    }

    public List<object> GetItemsByCategoryName(string categoryName)
    {
        int categoryId = GetCategoryIdByName(categoryName);
        return GetItemsByCategoryId(categoryId);
    }

    public List<object> GetItemsByCategoryEnum(BagCategory category)
    {
        int categoryId = (int)category;
        return GetItemsByCategoryId(categoryId);
    }

    // ==================== 类型转换方法 ====================

    public List<FishData> GetFishesByCategoryId(int categoryId)
    {
        List<object> items = GetItemsByCategoryId(categoryId);
        List<FishData> fishes = new List<FishData>();
        
        foreach (object item in items)
        {
            if (item is FishData fish)
            {
                fishes.Add(fish);
            }
        }
        
        return fishes;
    }

    public List<FishData> GetFishesByCategoryName(string categoryName)
    {
        int categoryId = GetCategoryIdByName(categoryName);
        return GetFishesByCategoryId(categoryId);
    }

    public List<FishData> GetFishesByCategoryEnum(BagCategory category)
    {
        int categoryId = (int)category;
        return GetFishesByCategoryId(categoryId);
    }

    public List<BaitData> GetBaitsByCategoryId(int categoryId)
    {
        List<object> items = GetItemsByCategoryId(categoryId);
        List<BaitData> baits = new List<BaitData>();
        
        foreach (object item in items)
        {
            if (item is BaitData bait)
            {
                baits.Add(bait);
            }
        }
        
        return baits;
    }

    public List<BaitData> GetBaitsByCategoryName(string categoryName)
    {
        int categoryId = GetCategoryIdByName(categoryName);
        return GetBaitsByCategoryId(categoryId);
    }

    public List<BaitData> GetBaitsByCategoryEnum(BagCategory category)
    {
        int categoryId = (int)category;
        return GetBaitsByCategoryId(categoryId);
    }

    // ==================== 工具方法 ====================

    private int GetItemId(object item)
    {
        if (item is FishData fish)
        {
            return fish.id;
        }
        else if (item is BaitData bait)
        {
            return bait.id;
        }
        else if (item is ItemData itemData)
        {
            return itemData.id;
        }
        return 0;
    }

    public void Reinitialize()
    {
        InitCategoryMappings();
        InitAllItems();
        
        string processLog = CollectProcessData();
        Debug.Log("\nItemDataManager: 数据重新初始化完成\n" + processLog);
    }

    // ==================== 调试方法 ====================

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.Q))
        {
            string processLog = CollectProcessData();
            Debug.Log("\n" + processLog);
        }
    }

    public void PrintCategoryMappings()
    {
        string processLog = CollectProcessData();
        Debug.Log("\n" + processLog);
    }

    private string GetItemInfo(object item)
    {
        if (item is FishData fish)
        {
            return $"鱼类: {fish.name} (ID: {fish.id})";
        }
        else if (item is BaitData bait)
        {
            return $"鱼饵: {bait.name} (ID: {bait.id})";
        }
        else if (item is ItemData itemData)
        {
            return $"物品: {itemData.name} (ID: {itemData.id}, 分类ID: {itemData.categoryId})";
        }
        return $"未知物品: {item.GetType().Name}";
    }
}