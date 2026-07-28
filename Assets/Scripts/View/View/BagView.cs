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
        Debug.Log("[BagView] 注册背包刷新事件监听");
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister(CommunicateEvent.EVENT_REFRESH_BAG, OnBagRefresh);
    }

    private void OnBagRefresh()
    {
        Debug.Log("[BagView] 收到背包刷新事件，调用 RefreshItems");
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
        Debug.Log("[BagView] 切换到室内皮肤");
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
        Debug.Log("[BagView] 切换到室外皮肤");
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
        }
    }

    public void OpenBag()
    {
        Debug.Log("[BagView] OpenBag - 打开背包");

        gameObject.SetActive(true);

        RefreshItems();

        SendEvent();

        ClickFirstValidCategory();
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
        Debug.Log($"[BagView] UpdateBagItems - 物品数: {inventory?.Count ?? 0}");

        if (inventory == null || inventory.Count == 0)
        {
            Debug.LogWarning("[BagView] UpdateBagItems - 数据为空");
            return;
        }

        foreach (var item in inventory)
        {
            Debug.Log($"[BagView] UpdateBagItems - 物品ID: {item.Key}, 数量: {item.Value}");
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
        Debug.Log("[BagView] RefreshItems 被调用");

        if (PlayerDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            Debug.Log($"[BagView] PlayerDataManager 背包数据: {inventory?.Count ?? 0} 种物品");

            if (inventory != null && inventory.Count > 0)
            {
                foreach (var item in inventory)
                {
                    Debug.Log($"  物品ID: {item.Key}, 数量: {item.Value}");
                }
            }
        }
         
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
