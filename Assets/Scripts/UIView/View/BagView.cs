using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小分类配置
/// 一个小分类对应一个BagDetail
/// </summary>
[System.Serializable]
public class SubCategoryConfig
{
    public int subCategoryId;      // 小分类ID
    public View.Detail.BagDetail bagDetail;  // 对应的BagDetail
}

/// <summary>
/// 大分类配置
/// 一个大分类对应一个toggle和多个小分类detail
/// </summary>
[System.Serializable]
public class CategoryConfig
{
    public int categoryId;              // 大分类ID
    public string categoryName;         // 大分类名称（用于显示）
    public Toggle categoryToggle;       // 大分类的toggle
    public GameObject categoryRoot;     // 大分类对应的根物体，控制显隐
    public List<SubCategoryConfig> subCategoryConfigs = new List<SubCategoryConfig>();  // 小分类配置列表
}

public class BagView : BaseView
{
    public ToggleGroup toggleGroup;
    public List<CategoryConfig> categoryConfigs = new List<CategoryConfig>();

    public Toggle indoorSkinToggle;
    public Toggle outdoorSkinToggle;
    public GameObject indoorSkinObj;
    public GameObject outdoorSkinObj;

    private Dictionary<int, CategoryConfig> categoryIdToConfig = new Dictionary<int, CategoryConfig>();

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();
        InitCategoryMappings();
        InitToggleListeners();
        InitSkinToggleListeners();
        InitDefaultSkinState();
        RegisterEvents();
        isInitialized = true;
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register(CommunicateEvent.EVENT_REFRESH_BAG, OnBagRefresh);
        Z_Logger.Log("[BagView] 注册背包刷新事件监听");
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister(CommunicateEvent.EVENT_REFRESH_BAG, OnBagRefresh);
    }

    private void OnBagRefresh()
    {
        Z_Logger.Log("[BagView] 收到背包刷新事件，调用 RefreshItems");
        // ✅ 保险：确保刷新前 SkinManager 已同步最新皮肤数据
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.EnsureSkinsSynced();
        }
        RefreshItems();
    }

    private void InitDefaultSkinState()
    {
        if (outdoorSkinToggle != null)
        {
            outdoorSkinToggle.isOn = true;
        }
        else
        {
            ShowOutdoorSkin();
        }
    }

    private void InitCategoryMappings()
    {
        categoryIdToConfig.Clear();
        foreach (CategoryConfig config in categoryConfigs)
        {
            if (config != null && config.categoryId > 0)
            {
                categoryIdToConfig[config.categoryId] = config;
            }
        }
    }

    private void InitToggleListeners()
    {
        foreach (CategoryConfig config in categoryConfigs)
        {
            if (config != null && config.categoryToggle != null)
            {
                config.categoryToggle.onValueChanged.AddListener((isOn) =>
                {
                    if (isOn)
                    {
                        OnCategoryToggle(config);
                    }
                });
            }
        }
    }

    private void InitSkinToggleListeners()
    {
        if (indoorSkinToggle != null)
        {
            indoorSkinToggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                {
                    ShowIndoorSkin();
                }
            });
        }

        if (outdoorSkinToggle != null)
        {
            outdoorSkinToggle.onValueChanged.AddListener((isOn) =>
            {
                if (isOn)
                {
                    ShowOutdoorSkin();
                }
            });
        }
    }

    private void ShowIndoorSkin()
    {
        if (indoorSkinObj != null)
        {
            indoorSkinObj.SetActive(true);
        }
        if (outdoorSkinObj != null)
        {
            outdoorSkinObj.SetActive(false);
        }
        Z_Logger.Log("[BagView] 切换到室内皮肤");
    }

    private void ShowOutdoorSkin()
    {
        if (outdoorSkinObj != null)
        {
            outdoorSkinObj.SetActive(true);
        }
        if (indoorSkinObj != null)
        {
            indoorSkinObj.SetActive(false);
        }
        Z_Logger.Log("[BagView] 切换到室外皮肤");
    }

    private void OnCategoryToggle(CategoryConfig config)
    {
        foreach (CategoryConfig cfg in categoryConfigs)
        {
            if (cfg != null)
            {
                if (cfg.categoryRoot != null)
                {
                    cfg.categoryRoot.SetActive(false);
                }
                foreach (SubCategoryConfig subCfg in cfg.subCategoryConfigs)
                {
                    if (subCfg != null && subCfg.bagDetail != null)
                    {
                        subCfg.bagDetail.gameObject.SetActive(false);
                    }
                }
            }
        }

        if (config != null)
        {
            if (config.categoryRoot != null)
            {
                config.categoryRoot.SetActive(true);
            }
            foreach (SubCategoryConfig subCfg in config.subCategoryConfigs)
            {
                if (subCfg != null && subCfg.bagDetail != null)
                {
                    subCfg.bagDetail.gameObject.SetActive(true);
                }
            }

            // ✅ 切换分类时更新该分类的物品数据（配合 OpenBag 的按需刷新优化）
            if (PlayerDataManager.Instance != null && LoadDataManager.Instance != null)
            {
                var inventory = PlayerDataManager.Instance.GetInventory();
                var itemDataMap = LoadDataManager.Instance.GetItemDataMap();
                if (inventory != null && itemDataMap != null)
                {
                    UpdateSingleCategoryDetails(config, inventory, itemDataMap);
                }
            }
        }
    }

    public void OpenBag()
    {
        Z_Logger.Log("[BagView] OpenBag - 打开背包");

        // ✅ 确保初始化已完成（首次打开时 Start() 可能还未执行，导致 RegisterEvents 未调用）
        if (!isInitialized)
        {
            BaseViewInit();
        }

        // ✅ 主动触发 SkinManager 皮肤数据同步（解决 OpenBag 早于 OnAllLoadingComplete 的时序问题）
        // 确保刷新背包时 SkinManager.equippedSkins 已包含服务器数据
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.EnsureSkinsSynced();
        }

        gameObject.SetActive(true);

        // ✅ 优化：只更新当前可见分类，避免全量刷新所有分类导致卡顿
        RefreshVisibleCategory();

        SendEvent();
    }

    /// <summary>
    /// 只刷新当前可见分类（打开背包时使用，避免全量刷新所有分类）
    /// </summary>
    private void RefreshVisibleCategory()
    {
        Z_Logger.Log("[BagView] RefreshVisibleCategory - 只刷新可见分类");

        if (PlayerDataManager.Instance != null && LoadDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            // ✅ LoadDataManager.GetItemDataMap() 返回的是物品定义（名称、图标、分类），不是装备状态
            // 装备状态由 BagDetail.IsItemEquipped → NetServerManager.IsItemEquippedCached 查询
            var itemDataMap = LoadDataManager.Instance.GetItemDataMap();

            if (inventory != null && itemDataMap != null)
            {
                // ✅ 确认装备缓存已就绪
                if (NetServerManager.Instance != null)
                {
                    Z_Logger.Log($"[BagView] RefreshVisibleCategory - 物品定义数: {itemDataMap.Count}, 背包物品数: {inventory.Count}, 装备缓存就绪: true");
                }

                // 只更新当前选中的分类
                CategoryConfig currentCategory = GetCurrentCategory();
                if (currentCategory != null)
                {
                    UpdateSingleCategoryDetails(currentCategory, inventory, itemDataMap);
                }
                else
                {
                    // 没有选中的分类，点击第一个有效分类
                    ClickFirstValidCategory();
                    currentCategory = GetCurrentCategory();
                    if (currentCategory != null)
                    {
                        UpdateSingleCategoryDetails(currentCategory, inventory, itemDataMap);
                    }
                }

                // 通知其他模块（EquipPlayerView 等）
                CommunicateEvent.Modify("Bag_RefreshItems");
                return;
            }
        }

        // 降级：数据管理器未就绪
        Z_Logger.LogWarning("[BagView] RefreshVisibleCategory - 数据管理器未就绪，降级为事件刷新");
        CommunicateEvent.Modify("Bag_RefreshItems");
    }

    /// <summary>
    /// 更新单个大分类下的所有小分类
    /// </summary>
    private void UpdateSingleCategoryDetails(CategoryConfig config, Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        if (config == null) return;

        foreach (SubCategoryConfig subCfg in config.subCategoryConfigs)
        {
            if (subCfg != null && subCfg.bagDetail != null)
            {
                subCfg.bagDetail.UpdateItemsBySingleCategory(itemDataMap, inventory, subCfg.subCategoryId);
            }
        }
    }

    private void SendEvent()
    {
        CommunicateEvent.Modify("Bag_Open");
    }

    public void InitBag()
    {
        CommunicateEvent.Modify("Bag_Init");
    }

    public void UpdateBagItems(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        Z_Logger.Log($"[BagView] UpdateBagItems - 物品数: {inventory?.Count ?? 0}");

        if (inventory == null || inventory.Count == 0)
        {
            Z_Logger.LogWarning("[BagView] UpdateBagItems - 数据为空");
            return;
        }

        foreach (var item in inventory)
        {
            Z_Logger.Log($"[BagView] UpdateBagItems - 物品ID: {item.Key}, 数量: {item.Value}");
        }

        UpdateAllBagDetails(inventory, itemDataMap);

        CategoryConfig currentCategory = GetCurrentCategory();
        if (currentCategory != null)
        {
            OnCategoryToggle(currentCategory);
        }
        else
        {
            ClickFirstValidCategory();
        }
    }

    private CategoryConfig GetCurrentCategory()
    {
        foreach (CategoryConfig config in categoryConfigs)
        {
            if (config != null && config.categoryToggle != null && config.categoryToggle.isOn)
            {
                return config;
            }
        }
        return null;
    }

    private void ClickFirstValidCategory()
    {
        foreach (CategoryConfig config in categoryConfigs)
        {
            if (config != null && config.categoryToggle != null)
            {
                OnCategoryToggle(config);
                config.categoryToggle.isOn = true;
                break;
            }
        }
    }

    private void UpdateAllBagDetails(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        foreach (CategoryConfig config in categoryConfigs)
        {
            if (config != null)
            {
                foreach (SubCategoryConfig subCfg in config.subCategoryConfigs)
                {
                    if (subCfg != null && subCfg.bagDetail != null)
                    {
                        subCfg.bagDetail.UpdateItemsBySingleCategory(itemDataMap, inventory, subCfg.subCategoryId);
                    }
                }
            }
        }
    }

    public void RefreshItems()
    {
        Z_Logger.Log("[BagView] RefreshItems 被调用");

        // ✅ 优先直接获取数据并更新（不依赖事件链，修复首次打开时装备状态显示异常）
        if (PlayerDataManager.Instance != null && LoadDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            var itemDataMap = LoadDataManager.Instance.GetItemDataMap();

            if (inventory != null && itemDataMap != null)
            {
                Z_Logger.Log($"[BagView] RefreshItems - 直接更新，物品数: {inventory.Count}");
                UpdateBagItems(inventory, itemDataMap);
                // 通知其他模块（EquipPlayerView 等）
                CommunicateEvent.Modify("Bag_RefreshItems");
                return;
            }
        }

        // 降级：数据管理器未就绪，通过事件让 LoadDataManager 间接处理
        Z_Logger.LogWarning("[BagView] RefreshItems - 数据管理器未就绪，降级为事件刷新");
        CommunicateEvent.Modify("Bag_RefreshItems");
    }

    public void UpdateBagWithInventory(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        UpdateAllBagDetails(inventory, itemDataMap);
    }

    /// <summary>
    /// 根据大分类ID获取配置
    /// </summary>
    public CategoryConfig GetCategoryConfig(int categoryId)
    {
        if (categoryIdToConfig.TryGetValue(categoryId, out CategoryConfig config))
        {
            return config;
        }
        return null;
    }

    /// <summary>
    /// 获取所有大分类ID
    /// </summary>
    public List<int> GetAllCategoryIds()
    {
        return new List<int>(categoryIdToConfig.Keys);
    }
}
