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

    // ============================================================
    // [Check] 诊断日志辅助（只读，不影响任何逻辑）
    // ============================================================
    private string DumpState()
    {
        return $"iid={GetInstanceID()}, " +
               $"activeSelf={gameObject.activeSelf}, " +
               $"activeInHierarchy={gameObject.activeInHierarchy}, " +
               $"scene='{gameObject.scene.name}', " +
               $"sceneValid={gameObject.scene.IsValid()}, " +
               $"t={Time.time:F3}";
    }

    private string GetHierarchyPath(Transform t)
    {
        if (t == null) return "null";
        var sb = new System.Text.StringBuilder(t.name);
        var cur = t.parent;
        while (cur != null)
        {
            sb.Insert(0, cur.name + "/");
            cur = cur.parent;
        }
        return sb.ToString();
    }

    // ============================================================
    // 初始化
    // ============================================================
    public override void BaseViewInit()
    {
        // ✅ 关键诊断：打印调用堆栈，用来定位"谁创建了这个 BagView 实例"
        Z_Logger.Log($"[BagView] [Check] BaseViewInit 被调用\n" +
                     $"  状态: {DumpState()}\n" +
                     $"  isInitialized={isInitialized}\n" +
                     $"  对象路径: {GetHierarchyPath(transform)}\n" +
                     $"  调用堆栈:\n{System.Environment.StackTrace}");

        if (isInitialized) return;
        base.BaseViewInit();
        InitCategoryMappings();
        InitToggleListeners();
        InitSkinToggleListeners();
        InitDefaultSkinState();
        RegisterEvents();
        isInitialized = true;

        Z_Logger.Log($"[BagView] [Check] BaseViewInit 完成, {DumpState()}");
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register(CommunicateEvent.EVENT_REFRESH_BAG, OnBagRefresh);
        Z_Logger.Log($"[BagView] [Check] 注册背包刷新事件监听, {DumpState()}");
    }

    private void OnDestroy()
    {
        Z_Logger.Log($"[BagView] [Check] OnDestroy 触发, {DumpState()}, 路径={GetHierarchyPath(transform)}");
        CommunicateEvent.Unregister(CommunicateEvent.EVENT_REFRESH_BAG, OnBagRefresh);
        Z_Logger.Log($"[BagView] [Check] OnDestroy 解除监听完成, {DumpState()}");
    }

    // ============================================================
    // 事件回调
    // ============================================================
    private void OnBagRefresh()
    {
        Z_Logger.Log($"[BagView] [Check] OnBagRefresh 收到刷新事件, {DumpState()}, 路径={GetHierarchyPath(transform)}");

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
        Z_Logger.Log($"[BagView] [Check] InitCategoryMappings 完成, 分类数={categoryIdToConfig.Count}, {DumpState()}");
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
        Z_Logger.Log($"[BagView] [Check] InitToggleListeners 完成, {DumpState()}");
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
        Z_Logger.Log($"[BagView] [Check] InitSkinToggleListeners 完成, {DumpState()}");
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
        Z_Logger.Log($"[BagView] [Check] 切换到室内皮肤, {DumpState()}");
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
        Z_Logger.Log($"[BagView] [Check] 切换到室外皮肤, {DumpState()}");
    }

    private void OnCategoryToggle(CategoryConfig config)
    {
        Z_Logger.Log($"[BagView] [Check] OnCategoryToggle 开始, categoryId={config?.categoryId}, {DumpState()}");

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
                    Z_Logger.Log($"[BagView] [Check] OnCategoryToggle 即将刷新分类详情, categoryId={config.categoryId}, {DumpState()}");
                    UpdateSingleCategoryDetails(config, inventory, itemDataMap);
                }
            }
        }

        Z_Logger.Log($"[BagView] [Check] OnCategoryToggle 完成, categoryId={config?.categoryId}, {DumpState()}");
    }

    // ============================================================
    // 对外接口
    // ============================================================
    public void OpenBag()
    {
        Z_Logger.Log($"[BagView] [Check] OpenBag 开始, {DumpState()}, 路径={GetHierarchyPath(transform)}");

        // ✅ 确保初始化已完成（首次打开时 Start() 可能还未执行，导致 RegisterEvents 未调用）
        if (!isInitialized)
        {
            Z_Logger.Log($"[BagView] [Check] OpenBag 检测到 isInitialized=False，调用 BaseViewInit, {DumpState()}");
            BaseViewInit();
        }

        // ✅ 主动触发 SkinManager 皮肤数据同步（解决 OpenBag 早于 OnAllLoadingComplete 的时序问题）
        // 确保刷新背包时 SkinManager.equippedSkins 已包含服务器数据
        if (SkinManager.Instance != null)
        {
            SkinManager.Instance.EnsureSkinsSynced();
        }

        Z_Logger.Log($"[BagView] [Check] OpenBag 即将 SetActive(true), {DumpState()}");
        gameObject.SetActive(true);
        Z_Logger.Log($"[BagView] [Check] OpenBag SetActive(true) 完成, {DumpState()}");

        // ✅ 优化：只更新当前可见分类，避免全量刷新所有分类导致卡顿
        RefreshVisibleCategory();

        SendEvent();
        Z_Logger.Log($"[BagView] [Check] OpenBag 完成, {DumpState()}");
    }

    /// <summary>
    /// 只刷新当前可见分类（打开背包时使用，避免全量刷新所有分类）
    /// </summary>
    private void RefreshVisibleCategory()
    {
        Z_Logger.Log($"[BagView] [Check] RefreshVisibleCategory 开始, {DumpState()}");

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
                    Z_Logger.Log($"[BagView] [Check] RefreshVisibleCategory 数据就绪, 物品定义数={itemDataMap.Count}, 背包物品数={inventory.Count}, {DumpState()}");
                }

                // 只更新当前选中的分类
                CategoryConfig currentCategory = GetCurrentCategory();
                if (currentCategory != null)
                {
                    Z_Logger.Log($"[BagView] [Check] RefreshVisibleCategory 当前分类 categoryId={currentCategory.categoryId}, {DumpState()}");
                    UpdateSingleCategoryDetails(currentCategory, inventory, itemDataMap);
                }
                else
                {
                    // 没有选中的分类，点击第一个有效分类
                    Z_Logger.Log($"[BagView] [Check] RefreshVisibleCategory 无选中分类，ClickFirstValidCategory, {DumpState()}");
                    ClickFirstValidCategory();
                    currentCategory = GetCurrentCategory();
                    if (currentCategory != null)
                    {
                        Z_Logger.Log($"[BagView] [Check] RefreshVisibleCategory 切换后 categoryId={currentCategory.categoryId}, {DumpState()}");
                        UpdateSingleCategoryDetails(currentCategory, inventory, itemDataMap);
                    }
                }

                // 通知其他模块（EquipPlayerView 等）
                CommunicateEvent.Modify("Bag_RefreshItems");
                Z_Logger.Log($"[BagView] [Check] RefreshVisibleCategory 完成（正常路径）, {DumpState()}");
                return;
            }
        }

        // 降级：数据管理器未就绪
        Z_Logger.LogWarning($"[BagView] [Check] RefreshVisibleCategory 数据管理器未就绪，降级为事件刷新, {DumpState()}");
        CommunicateEvent.Modify("Bag_RefreshItems");
    }

    /// <summary>
    /// 更新单个大分类下的所有小分类
    /// </summary>
    private void UpdateSingleCategoryDetails(CategoryConfig config, Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        if (config == null) return;

        Z_Logger.Log($"[BagView] [Check] UpdateSingleCategoryDetails 开始, categoryId={config.categoryId}, 小分类数={config.subCategoryConfigs?.Count ?? 0}, {DumpState()}");

        foreach (SubCategoryConfig subCfg in config.subCategoryConfigs)
        {
            if (subCfg != null && subCfg.bagDetail != null)
            {
                Z_Logger.Log($"[BagView] [Check] UpdateSingleCategoryDetails -> BagDetail.UpdateItemsBySingleCategory, " +
                             $"subCategoryId={subCfg.subCategoryId}, " +
                             $"bagDetailPath={GetHierarchyPath(subCfg.bagDetail.transform)}, " +
                             $"bagDetailActiveInHierarchy={subCfg.bagDetail.gameObject.activeInHierarchy}, " +
                             $"bagDetailScene='{subCfg.bagDetail.gameObject.scene.name}', " +
                             $"bagDetailSceneValid={subCfg.bagDetail.gameObject.scene.IsValid()}, " +
                             $"{DumpState()}");
                subCfg.bagDetail.UpdateItemsBySingleCategory(itemDataMap, inventory, subCfg.subCategoryId);
            }
        }

        Z_Logger.Log($"[BagView] [Check] UpdateSingleCategoryDetails 完成, categoryId={config.categoryId}, {DumpState()}");
    }

    private void SendEvent()
    {
        CommunicateEvent.Modify("Bag_Open");
        Z_Logger.Log($"[BagView] [Check] SendEvent: Bag_Open, {DumpState()}");
    }

    public void InitBag()
    {
        Z_Logger.Log($"[BagView] [Check] InitBag 被调用, {DumpState()}");
        CommunicateEvent.Modify("Bag_Init");
    }

    public void UpdateBagItems(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        Z_Logger.Log($"[BagView] [Check] UpdateBagItems 开始, 物品数={inventory?.Count ?? 0}, {DumpState()}, 路径={GetHierarchyPath(transform)} , {gameObject.name}");

        Z_Logger.Log($"[BagView] [Check] UpdateBagItems 被调用\n" +
     $"  iid={GetInstanceID()}\n" +
     $"  name='{name}'\n" +
     $"  equals null? {gameObject == null}\n" +
     $"  ReferenceEquals null? {ReferenceEquals(gameObject, null)}\n" +
     $"  parent={(transform.parent == null ? "null" : transform.parent.name)}\n" +
     $"  root={transform.root.name}\n" +
     $"  scene='{gameObject.scene.name}'\n" +
     $"  scene.IsValid={gameObject.scene.IsValid()}\n" +
     $"  scene.isLoaded={gameObject.scene.isLoaded}\n" +
     $"  activeSelf={gameObject.activeSelf}\n" +
     $"  activeInHierarchy={gameObject.activeInHierarchy}\n" +
     $"  childCount={transform.childCount}");

        if (inventory == null || inventory.Count == 0)
        {
            Z_Logger.LogWarning($"[BagView] [Check] UpdateBagItems 数据为空, {DumpState()}");
            return;
        }

        foreach (var item in inventory)
        {
            Z_Logger.Log($"[BagView] [Check] UpdateBagItems - 物品ID: {item.Key}, 数量: {item.Value}");
        }

        UpdateAllBagDetails(inventory, itemDataMap);

        CategoryConfig currentCategory = GetCurrentCategory();
        if (currentCategory != null)
        {
            Z_Logger.Log($"[BagView] [Check] UpdateBagItems -> OnCategoryToggle, categoryId={currentCategory.categoryId}, {DumpState()}");
            OnCategoryToggle(currentCategory);
        }
        else
        {
            Z_Logger.Log($"[BagView] [Check] UpdateBagItems -> ClickFirstValidCategory, {DumpState()}");
            ClickFirstValidCategory();
        }

        Z_Logger.Log($"[BagView] [Check] UpdateBagItems 完成, {DumpState()}");
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
                Z_Logger.Log($"[BagView] [Check] ClickFirstValidCategory 选择 categoryId={config.categoryId}, {DumpState()}");
                break;
            }
        }
    }

    private void UpdateAllBagDetails(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        Z_Logger.Log($"[BagView] [Check] UpdateAllBagDetails 开始, 分类数={categoryConfigs?.Count ?? 0}, {DumpState()}");

        foreach (CategoryConfig config in categoryConfigs)
        {
            if (config != null)
            {
                foreach (SubCategoryConfig subCfg in config.subCategoryConfigs)
                {
                    if (subCfg != null && subCfg.bagDetail != null)
                    {
                        Z_Logger.Log($"[BagView] [Check] UpdateAllBagDetails -> BagDetail.UpdateItemsBySingleCategory, " +
                                     $"categoryId={config.categoryId}, subCategoryId={subCfg.subCategoryId}, " +
                                     $"bagDetailPath={GetHierarchyPath(subCfg.bagDetail.transform)}, " +
                                     $"bagDetailActiveInHierarchy={subCfg.bagDetail.gameObject.activeInHierarchy}, " +
                                     $"bagDetailScene='{subCfg.bagDetail.gameObject.scene.name}', " +
                                     $"bagDetailSceneValid={subCfg.bagDetail.gameObject.scene.IsValid()}, " +
                                     $"{DumpState()}");
                        subCfg.bagDetail.UpdateItemsBySingleCategory(itemDataMap, inventory, subCfg.subCategoryId);
                    }
                }
            }
        }

        Z_Logger.Log($"[BagView] [Check] UpdateAllBagDetails 完成, {DumpState()}");
    }

    public void RefreshItems()
    {
        Z_Logger.Log($"[BagView] [Check] RefreshItems 开始, {DumpState()}, 路径={GetHierarchyPath(transform)}");

        // ✅ 优先直接获取数据并更新（不依赖事件链，修复首次打开时装备状态显示异常）
        if (PlayerDataManager.Instance != null && LoadDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            var itemDataMap = LoadDataManager.Instance.GetItemDataMap();

            if (inventory != null && itemDataMap != null)
            {
                Z_Logger.Log($"[BagView] [Check] RefreshItems 数据就绪，直接更新, 物品数={inventory.Count}, {DumpState()}");
                UpdateBagItems(inventory, itemDataMap);
                // 通知其他模块（EquipPlayerView 等）
                CommunicateEvent.Modify("Bag_RefreshItems");
                Z_Logger.Log($"[BagView] [Check] RefreshItems 完成（正常路径）, {DumpState()}");
                return;
            }
        }

        // 降级：数据管理器未就绪，通过事件让 LoadDataManager 间接处理
        Z_Logger.LogWarning($"[BagView] [Check] RefreshItems 数据管理器未就绪，降级为事件刷新, {DumpState()}");
        CommunicateEvent.Modify("Bag_RefreshItems");
    }

    public void UpdateBagWithInventory(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
    {
        Z_Logger.Log($"[BagView] [Check] UpdateBagWithInventory 开始, {DumpState()}");
        UpdateAllBagDetails(inventory, itemDataMap);
        Z_Logger.Log($"[BagView] [Check] UpdateBagWithInventory 完成, {DumpState()}");
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
