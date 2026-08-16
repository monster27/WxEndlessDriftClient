using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// 图鉴视图
/// 负责显示游戏中的图鉴系统，包括鱼类和物品的收集进度、等级和奖励
/// </summary>
public class CollectionView : BaseView
{
    [Header("UI组件")]
    public Text pageNameText;               // 页面名称文本
    public Text completionText;              // 完成度百分比文本
    public Button rewardButton;              // 奖励领取按钮
    public Toggle[] categoryToggles = new Toggle[6];  // 分类切换按钮数组（最多6个分类）
    public Button prevPageButton;            // 上一页按钮
    public Button nextPageButton;            // 下一页按钮
    public Text pageInfoText;                // 页码信息文本
    public Transform collectionPrefabParent; // 图鉴条目父容器
    public CollInfoPanel collInfoPanel;  // 图鉴详情面板

    [Header("预制体")]
    public GameObject collectionPrefab;      // 图鉴条目预制体（编辑器填入）

    // 私有变量
    private int currentCategoryIndex = 0;     // 当前选中的分类索引
    private int currentPageIndex = 0;         // 当前页码
    private List<CollectionCategory> categories = new List<CollectionCategory>();  // 所有分类数据
    private List<UI_CollectionPrefab> activePrefabs = new List<UI_CollectionPrefab>();  // 当前显示的图鉴条目列表

    /// <summary>
    /// 初始化图鉴视图
    /// </summary>
    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        // 加载图鉴数据
        LoadCollectionData();

        // 注册UI事件
        RegisterEvents();

        isInitialized = true;
    }

    /// <summary>
    /// 注册所有UI组件的事件监听
    /// </summary>
    private void RegisterEvents()
    {
        // 奖励按钮点击事件
        if (rewardButton != null)
        {
            rewardButton.onClick.AddListener(OnRewardButtonClick);
        }

        // 翻页按钮事件
        if (prevPageButton != null)
        {
            prevPageButton.onClick.AddListener(OnPrevPage);
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.AddListener(OnNextPage);
        }

        // 分类切换事件
        for (int i = 0; i < categoryToggles.Length; i++)
        {
            int index = i;  // 捕获索引变量
            if (categoryToggles[i] != null)
            {
                categoryToggles[i].onValueChanged.AddListener((bool isOn) => OnCategoryToggleChanged(index, isOn));
            }
        }
    }

    /// <summary>
    /// 从Resources加载图鉴数据
    /// </summary>
    private void LoadCollectionData()
    {
        string path = "JsonData/BaseFramework/collection";
        TextAsset jsonAsset = AssetManager.LoadFromResources<TextAsset>(path);

        if (jsonAsset != null)
        {
            var wrapper = JsonUtility.FromJson<CollectionWrapper>(jsonAsset.text);
            if (wrapper?.collection?.categories != null)
            {
                categories = wrapper.collection.categories;
            }
        }
    }

    /// <summary>
    /// 打开图鉴
    /// </summary>
    public void OpenCollection()
    {
        ShowView();  // 显示视图
        
        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.FetchPlayerCollection(() =>
            {
                NetServerManager.Instance.FetchPlayerCollectionProgress(() =>
                {
                    NetServerManager.Instance.FetchPurchasedCollectionInfo(() =>
                    {
                        UpdateCategoryToggle(0);  // 默认选中第一个分类
                        RefreshCurrentPage();  // 刷新当前页面
                    });
                });
            });
        }
        else
        {
            UpdateCategoryToggle(0);  // 默认选中第一个分类
            RefreshCurrentPage();  // 刷新当前页面
        }
    }

    /// <summary>
    /// 关闭图鉴
    /// </summary>
    public void CloseCollection()
    {
        HideView();  // 隐藏视图
    }

    /// <summary>
    /// 分类切换事件处理
    /// </summary>
    /// <param name="index">分类索引</param>
    /// <param name="isOn">是否选中</param>
    private void OnCategoryToggleChanged(int index, bool isOn)
    {
        if (isOn)
        {
            currentCategoryIndex = index;
            currentPageIndex = 0;  // 重置页码
            RefreshCurrentPage();  // 刷新当前页面
        }
    }

    /// <summary>
    /// 更新分类切换按钮状态
    /// </summary>
    /// <param name="index">要选中的分类索引</param>
    private void UpdateCategoryToggle(int index)
    {
        for (int i = 0; i < categoryToggles.Length; i++)
        {
            if (categoryToggles[i] != null)
            {
                categoryToggles[i].isOn = (i == index);
            }
        }
    }

    /// <summary>
    /// 上一页按钮点击处理
    /// </summary>
    private void OnPrevPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            RefreshCurrentPage();
        }
    }

    /// <summary>
    /// 下一页按钮点击处理
    /// </summary>
    private void OnNextPage()
    {
        var category = GetCurrentCategory();
        if (category != null && currentPageIndex < category.pages.Count - 1)
        {
            currentPageIndex++;
            RefreshCurrentPage();
        }
    }

    /// <summary>
    /// 获取当前选中的分类
    /// </summary>
    /// <returns>当前分类数据</returns>
    private CollectionCategory GetCurrentCategory()
    {
        if (currentCategoryIndex >= 0 && currentCategoryIndex < categories.Count)
        {
            return categories[currentCategoryIndex];
        }
        return null;
    }

    /// <summary>
    /// 获取当前页面数据
    /// </summary>
    /// <returns>当前页面数据</returns>
    private CollectionPage GetCurrentPage()
    {
        var category = GetCurrentCategory();
        if (category != null && currentPageIndex >= 0 && currentPageIndex < category.pages.Count)
        {
            return category.pages[currentPageIndex];
        }
        return null;
    }

    /// <summary>
    /// 刷新当前页面显示
    /// </summary>
    private void RefreshCurrentPage()
    {
        // 清除旧的图鉴条目
        ClearPrefabs();

        var page = GetCurrentPage();
        if (page != null)
        {
            // 更新页面信息
            UpdatePageInfo(page);
            // 更新完成度
            UpdateCompletion(page);
            // 更新奖励按钮状态
            UpdateRewardButton(page);
            // 创建图鉴条目
            CreateCollectionPrefabs(page);
        }
        else
        {
            // 如果没有数据，重置显示
            if (pageNameText != null)
            {
                pageNameText.text = "";
            }
            completionText.text = "0";
            pageInfoText.text = "0/0";
            rewardButton.interactable = false;
        }
    }

    /// <summary>
    /// 更新页码信息显示
    /// </summary>
    /// <param name="page">当前页面数据</param>
    private void UpdatePageInfo(CollectionPage page)
    {
        if (pageNameText != null)
        {
            pageNameText.text = page.pageName ?? "";
        }

        var category = GetCurrentCategory();
        if (category != null)
        {
            pageInfoText.text = $"{currentPageIndex + 1}/{category.pages.Count}";
        }
    }

    /// <summary>
    /// 更新完成度显示（优先使用服务器计算的进度）
    /// </summary>
    /// <param name="page">当前页面数据</param>
    private void UpdateCompletion(CollectionPage page)
    {
        float completion = 0;
        
        var progress = GetServerProgress(page.id);
        if (progress != null)
        {
            completion = progress.completionPercent;
        }
        else
        {
            completion = CalculatePageCompletion(page);
        }
        
        completionText.text = Mathf.FloorToInt(completion).ToString();
    }
    
    /// <summary>
    /// 计算页面完成度（客户端回退，与服务器逻辑一致）
    /// 鱼类：每条鱼3个条件，每个完成的条件贡献1/3
    /// 非鱼类：每个条目1个条件，拥有即满足
    /// </summary>
    private float CalculatePageCompletion(CollectionPage page)
    {
        if (page.entries == null || page.entries.Count == 0)
            return 0;
        
        bool isFishCategory = currentCategoryIndex == 0;
        float completedConditions = 0;
        float totalConditions = 0;
        
        if (isFishCategory)
        {
            totalConditions = page.entries.Count * 3f;
            foreach (int entryId in page.entries)
            {
                int level = GetFishCollectionLevel(entryId); // 0-3
                completedConditions += level;
            }
        }
        else
        {
            totalConditions = page.entries.Count;
            foreach (int entryId in page.entries)
            {
                int quantity = PlayerDataManager.Instance?.GetItemQuantity(entryId) ?? 0;
                if (quantity > 0)
                {
                    completedConditions++;
                }
            }
        }
        
        return totalConditions > 0 ? completedConditions / totalConditions * 100f : 0;
    }
    
    /// <summary>
    /// 获取服务器返回的页面进度
    /// </summary>
    private NetServerManager.PlayerCollectionProgress GetServerProgress(int pageId)
    {
        if (NetServerManager.Instance != null)
        {
            var progressList = NetServerManager.Instance.GetPlayerCollectionProgress();
            if (progressList != null)
            {
                return progressList.FirstOrDefault(p => p.pageId == pageId);
            }
        }
        return null;
    }

    /// <summary>
    /// 获取鱼类收集等级（0-3级）- 由服务器计算
    /// </summary>
    /// <param name="fishId">鱼类ID</param>
    /// <returns>收集等级</returns>
    public int GetFishCollectionLevel(int fishId)
    {
        return PlayerDataManager.Instance?.GetCollectionLevel(fishId) ?? 0;
    }

    /// <summary>
    /// 更新奖励按钮的可用状态（优先使用服务器返回的可用奖励）
    /// </summary>
    /// <param name="page">当前页面数据</param>
    private void UpdateRewardButton(CollectionPage page)
    {
        bool hasAvailableReward = false;
        
        var progress = GetServerProgress(page.id);
        if (progress != null && progress.availableRewards != null)
        {
            hasAvailableReward = progress.availableRewards.Count > 0;
        }
        else
        {
            float completion = CalculatePageCompletion(page);

            foreach (var reward in page.rewards)
            {
                if (completion >= reward.percent && reward.rewardId > 0)
                {
                    hasAvailableReward = true;
                    break;
                }
            }
        }

        rewardButton.interactable = hasAvailableReward;
    }

    /// <summary>
    /// 奖励按钮点击处理
    /// </summary>
    private void OnRewardButtonClick()
    {
        var page = GetCurrentPage();
        if (page != null)
        {
            float completion = CalculatePageCompletion(page);

            // 查找可领取的奖励
            foreach (var reward in page.rewards)
            {
                if (completion >= reward.percent && reward.rewardId > 0)
                {
                    ShowRewardDialog(reward);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 显示奖励对话框
    /// </summary>
    /// <param name="reward">奖励数据</param>
    private void ShowRewardDialog(Reward reward)
    {
        string message = $"获得奖励：物品ID={reward.rewardId}，数量={reward.rewardAmount}";
        GameUIManager.ShowInfoMessage(message);
    }

    /// <summary>
    /// 创建图鉴条目
    /// </summary>
    /// <param name="page">当前页面数据</param>
    private void CreateCollectionPrefabs(CollectionPage page)
    {
        foreach (int entryId in page.entries)
        {
            CreatePrefab(entryId, page.pageName);
        }
    }

    /// <summary>
    /// 创建单个图鉴条目
    /// </summary>
    /// <param name="entryId">条目ID</param>
    /// <param name="pageName">所属页面名称</param>
    private void CreatePrefab(int entryId, string pageName = "")
    {
        GameObject prefab = null;
        
        if (collectionPrefab != null)
        {
            prefab = collectionPrefab;
        }
        else
        {
            prefab = AssetManager.LoadFromResources<GameObject>("Prefabs/UI_CollectionBtn");
        }
        
        if (prefab != null && collectionPrefabParent != null)
        {
            GameObject obj = Instantiate(prefab);
            obj.transform.SetParent(collectionPrefabParent, false);
            UI_CollectionPrefab collectionPrefab = obj.GetComponent<UI_CollectionPrefab>();

            if (collectionPrefab != null)
            {
                // 获取情报状态
                CollectionInfoState infoState = GetEntryInfoState(entryId);

                // 初始化图鉴条目（鱼类分类传入true，传入情报状态和页面名称）
                collectionPrefab.Init(entryId, currentCategoryIndex == 0, infoState, pageName);

                // 如果是鱼类分类且已获取物品，设置收集等级和闪光状态
                if (currentCategoryIndex == 0 && infoState == CollectionInfoState.Obtained)
                {
                    int level = GetFishCollectionLevel(entryId);
                    collectionPrefab.SetCollectionLevel(level);

                    bool hasShiny = PlayerDataManager.Instance?.HasCaughtShinyFish(entryId) ?? false;
                    collectionPrefab.SetHasShiny(hasShiny);
                }

                // 注册点击事件
                collectionPrefab.OnClick += OnCollectionPrefabClick;

                // 添加到活动列表
                activePrefabs.Add(collectionPrefab);
            }
        }
    }
    
    /// <summary>
    /// 获取条目情报状态
    /// </summary>
    private CollectionInfoState GetEntryInfoState(int entryId)
    {
        // 判断是否已经获取过物品
        bool hasObtained = false;
        if (currentCategoryIndex == 0)
        {
            // 鱼类：捕获过就算获取
            int catchCount = PlayerDataManager.Instance?.GetFishCatchCount(entryId) ?? 0;
            hasObtained = catchCount > 0;
        }
        else
        {
            // 非鱼类：物品数量大于0就算获取
            int itemCount = PlayerDataManager.Instance?.GetItemQuantity(entryId) ?? 0;
            hasObtained = itemCount > 0;
        }
        
        if (hasObtained)
        {
            return CollectionInfoState.Obtained;
        }
        
        // 检查是否已获取情报（购买了该页面的情报）
        var page = GetCurrentPage();
        if (page != null)
        {
            bool hasInfo = HasPageInfo(page.id);
            if (hasInfo)
            {
                return CollectionInfoState.InfoObtained;
            }
        }
        
        return CollectionInfoState.Unknown;
    }
    
    /// <summary>
    /// 检查是否已购买该页面的情报
    /// </summary>
    private bool HasPageInfo(int pageId)
    {
        // 从NetServerManager获取已购买的情报页面列表
        if (NetServerManager.Instance != null)
        {
            return NetServerManager.Instance.HasPurchasedCollectionInfo(pageId);
        }
        return false;
    }

    /// <summary>
    /// 清除所有图鉴条目
    /// </summary>
    private void ClearPrefabs()
    {
        foreach (var prefab in activePrefabs)
        {
            if (prefab != null && prefab.gameObject != null)
            {
                Destroy(prefab.gameObject);
            }
        }
        activePrefabs.Clear();
    }

    /// <summary>
    /// 图鉴条目点击事件处理
    /// </summary>
    /// <param name="prefab">被点击的图鉴条目</param>
    private void OnCollectionPrefabClick(UI_CollectionPrefab prefab)
    {
        if (collInfoPanel != null)
        {
            // 显示详情面板（鱼类分类传入true）
            collInfoPanel.ShowInfo(prefab.EntryId, currentCategoryIndex == 0);
        }
    }

    #region 数据类定义

    /// <summary>
    /// 奖励数据
    /// </summary>
    [System.Serializable]
    public class Reward
    {
        public int percent;        // 需要达到的完成百分比
        public int rewardId;       // 奖励物品ID
        public int rewardAmount;   // 奖励数量
    }

    /// <summary>
    /// 图鉴页面数据
    /// </summary>
    [System.Serializable]
    public class CollectionPage
    {
        public int id;                    // 页面ID
        public string pageName;           // 页面名称
        public List<Reward> rewards;      // 奖励列表
        public List<int> entries;         // 条目ID列表
    }

    /// <summary>
    /// 图鉴分类数据
    /// </summary>
    [System.Serializable]
    public class CollectionCategory
    {
        public int id;                    // 分类ID
        public string name;               // 分类名称
        public string icon;               // 图标路径
        public List<CollectionPage> pages; // 页面列表
    }

    /// <summary>
    /// 图鉴根数据
    /// </summary>
    [System.Serializable]
    public class CollectionRoot
    {
        public List<CollectionCategory> categories;
    }

    /// <summary>
    /// 图鉴数据包装类（用于JSON解析）
    /// </summary>
    [System.Serializable]
    public class CollectionWrapper
    {
        public CollectionRoot collection;
    }

    #endregion
}
