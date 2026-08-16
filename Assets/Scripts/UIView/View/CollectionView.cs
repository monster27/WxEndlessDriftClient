using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class CollectionView : BaseView
{
    [Header("UI组件")]
    public Text pageNameText;
    public Text completionText;
    public Button rewardButton;
    public Toggle[] categoryToggles = new Toggle[6];
    public Button prevPageButton;
    public Button nextPageButton;
    public Text pageInfoText;
    public Transform collectionPrefabParent;
    public CollInfoPanel collInfoPanel;

    [Header("预制体")]
    public GameObject collectionPrefab;

    private int currentCategoryIndex = 0;
    private int currentPageIndex = 0;
    private List<CollectionCategory> categories = new List<CollectionCategory>();
    private List<UI_CollectionPrefab> activePrefabs = new List<UI_CollectionPrefab>();
    private AsyncOperationHandle<TextAsset> _jsonHandle;
    private AsyncOperationHandle<GameObject> _prefabHandle;

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        LoadCollectionData();
        RegisterEvents();

        isInitialized = true;
    }

    void OnDestroy()
    {
        AssetManager.ReleaseAddressable(_jsonHandle);
        AssetManager.ReleaseAddressable(_prefabHandle);
    }

    private void RegisterEvents()
    {
        if (rewardButton != null)
        {
            rewardButton.onClick.AddListener(OnRewardButtonClick);
        }

        if (prevPageButton != null)
        {
            prevPageButton.onClick.AddListener(OnPrevPage);
        }
        if (nextPageButton != null)
        {
            nextPageButton.onClick.AddListener(OnNextPage);
        }

        for (int i = 0; i < categoryToggles.Length; i++)
        {
            int index = i;
            if (categoryToggles[i] != null)
            {
                categoryToggles[i].onValueChanged.AddListener((bool isOn) => OnCategoryToggleChanged(index, isOn));
            }
        }
    }

    private void LoadCollectionData()
    {
        string path = "JsonData/BaseFramework/collection";
        AssetManager.LoadFromAddressables<TextAsset>(path, (jsonAsset, handle) =>
        {
            _jsonHandle = handle;
            if (jsonAsset != null)
            {
                var wrapper = JsonUtility.FromJson<CollectionWrapper>(jsonAsset.text);
                if (wrapper?.collection?.categories != null)
                {
                    categories = wrapper.collection.categories;
                }
            }
        });
    }

    public void OpenCollection()
    {
        ShowView();

        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.FetchPlayerCollection(() =>
            {
                NetServerManager.Instance.FetchPlayerCollectionProgress(() =>
                {
                    NetServerManager.Instance.FetchPurchasedCollectionInfo(() =>
                    {
                        UpdateCategoryToggle(0);
                        RefreshCurrentPage();
                    });
                });
            });
        }
        else
        {
            UpdateCategoryToggle(0);
            RefreshCurrentPage();
        }
    }

    public void CloseCollection()
    {
        HideView();
    }

    private void OnCategoryToggleChanged(int index, bool isOn)
    {
        if (isOn)
        {
            currentCategoryIndex = index;
            currentPageIndex = 0;
            RefreshCurrentPage();
        }
    }

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

    private void OnPrevPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            RefreshCurrentPage();
        }
    }

    private void OnNextPage()
    {
        var category = GetCurrentCategory();
        if (category != null && currentPageIndex < category.pages.Count - 1)
        {
            currentPageIndex++;
            RefreshCurrentPage();
        }
    }

    private CollectionCategory GetCurrentCategory()
    {
        if (currentCategoryIndex >= 0 && currentCategoryIndex < categories.Count)
        {
            return categories[currentCategoryIndex];
        }
        return null;
    }

    private CollectionPage GetCurrentPage()
    {
        var category = GetCurrentCategory();
        if (category != null && currentPageIndex >= 0 && currentPageIndex < category.pages.Count)
        {
            return category.pages[currentPageIndex];
        }
        return null;
    }

    private void RefreshCurrentPage()
    {
        ClearPrefabs();

        var page = GetCurrentPage();
        if (page != null)
        {
            UpdatePageInfo(page);
            UpdateCompletion(page);
            UpdateRewardButton(page);
            CreateCollectionPrefabs(page);
        }
        else
        {
            if (pageNameText != null)
            {
                pageNameText.text = "";
            }
            completionText.text = "0";
            pageInfoText.text = "0/0";
            rewardButton.interactable = false;
        }
    }

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
                int level = GetFishCollectionLevel(entryId);
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

    public int GetFishCollectionLevel(int fishId)
    {
        return PlayerDataManager.Instance?.GetCollectionLevel(fishId) ?? 0;
    }

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

    private void OnRewardButtonClick()
    {
        var page = GetCurrentPage();
        if (page != null)
        {
            float completion = CalculatePageCompletion(page);

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

    private void ShowRewardDialog(Reward reward)
    {
        string message = $"获得奖励：物品ID={reward.rewardId}，数量={reward.rewardAmount}";
        GameUIManager.ShowInfoMessage(message);
    }

    private void CreateCollectionPrefabs(CollectionPage page)
    {
        foreach (int entryId in page.entries)
        {
            CreatePrefab(entryId, page.pageName);
        }
    }

    private void CreatePrefab(int entryId, string pageName = "")
    {
        if (collectionPrefab != null)
        {
            CreatePrefabFromPrefab(entryId, pageName);
        }
        else
        {
            AssetManager.LoadFromAddressables<GameObject>("Prefabs/UI_CollectionBtn", (prefab, handle) =>
            {
                _prefabHandle = handle;
                if (prefab != null && collectionPrefabParent != null)
                {
                    InstantiateAndInitPrefab(prefab, entryId, pageName);
                }
            });
        }
    }

    private void CreatePrefabFromPrefab(int entryId, string pageName)
    {
        if (collectionPrefab != null && collectionPrefabParent != null)
        {
            GameObject obj = Instantiate(collectionPrefab);
            InitPrefab(obj, entryId, pageName);
        }
    }

    private void InstantiateAndInitPrefab(GameObject prefab, int entryId, string pageName)
    {
        if (prefab != null && collectionPrefabParent != null)
        {
            GameObject obj = Instantiate(prefab);
            InitPrefab(obj, entryId, pageName);
        }
    }

    private void InitPrefab(GameObject obj, int entryId, string pageName)
    {
        obj.transform.SetParent(collectionPrefabParent, false);
        UI_CollectionPrefab collectionPrefab = obj.GetComponent<UI_CollectionPrefab>();

        if (collectionPrefab != null)
        {
            CollectionInfoState infoState = GetEntryInfoState(entryId);
            collectionPrefab.Init(entryId, currentCategoryIndex == 0, infoState, pageName);

            if (currentCategoryIndex == 0 && infoState == CollectionInfoState.Obtained)
            {
                int level = GetFishCollectionLevel(entryId);
                collectionPrefab.SetCollectionLevel(level);

                bool hasShiny = PlayerDataManager.Instance?.HasCaughtShinyFish(entryId) ?? false;
                collectionPrefab.SetHasShiny(hasShiny);
            }

            collectionPrefab.OnClick += OnCollectionPrefabClick;
            activePrefabs.Add(collectionPrefab);
        }
    }

    private CollectionInfoState GetEntryInfoState(int entryId)
    {
        bool hasObtained = false;
        if (currentCategoryIndex == 0)
        {
            int catchCount = PlayerDataManager.Instance?.GetFishCatchCount(entryId) ?? 0;
            hasObtained = catchCount > 0;
        }
        else
        {
            int itemCount = PlayerDataManager.Instance?.GetItemQuantity(entryId) ?? 0;
            hasObtained = itemCount > 0;
        }

        if (hasObtained)
        {
            return CollectionInfoState.Obtained;
        }

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

    private bool HasPageInfo(int pageId)
    {
        if (NetServerManager.Instance != null)
        {
            return NetServerManager.Instance.HasPurchasedCollectionInfo(pageId);
        }
        return false;
    }

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

    private void OnCollectionPrefabClick(UI_CollectionPrefab prefab)
    {
        if (collInfoPanel != null)
        {
            collInfoPanel.ShowInfo(prefab.EntryId, currentCategoryIndex == 0);
        }
    }

    #region 数据类定义

    [System.Serializable]
    public class Reward
    {
        public int percent;
        public int rewardId;
        public int rewardAmount;
    }

    [System.Serializable]
    public class CollectionPage
    {
        public int id;
        public string pageName;
        public List<Reward> rewards;
        public List<int> entries;
    }

    [System.Serializable]
    public class CollectionCategory
    {
        public int id;
        public string name;
        public string icon;
        public List<CollectionPage> pages;
    }

    [System.Serializable]
    public class CollectionRoot
    {
        public List<CollectionCategory> categories;
    }

    [System.Serializable]
    public class CollectionWrapper
    {
        public CollectionRoot collection;
    }

    #endregion
}
