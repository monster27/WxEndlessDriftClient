using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using View.Detail;

public class FishBagView : BagViewBase
{
    public View.Detail.FishDetail fishDetail;
    public Text fishCountText;
    public Button selectAllButton;
    public Button sellButton;
    public Text totalSellPriceText;
    public GameObject fishBagItemPrefab;

    private bool isAllSelected = false;
    private const string EVENT_FISHBAG_SELL = "FishBag_Sell";

    public override void Init()
    {
        if (isInitialized) return;
        base.Init();
        InitializeFishDetail();
        ShowFishDetail();
        RegisterEvents();
        BindButtonListeners();
        isInitialized = true;
    }

    private void InitializeFishDetail()
    {
        if (fishDetail != null && fishBagItemPrefab != null)
        {
            fishDetail.SetFishBagItemPrefab(fishBagItemPrefab);
        }

        if (fishDetail != null)
        {
            fishDetail.SetOnFishSelectionChanged(OnFishSelectionChanged);
        }
    }

    private void BindButtonListeners()
    {
        if (selectAllButton != null)
        {
            selectAllButton.onClick.AddListener(OnSelectAllButtonClick);
        }

        if (sellButton != null)
        {
            sellButton.onClick.AddListener(OnSellButtonClick);
        }
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register<Dictionary<string, object>>(EVENT_FISHBAG_SELL, OnFishBagSell);
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<Dictionary<string, object>>(EVENT_FISHBAG_SELL, OnFishBagSell);
    }

    private void OnFishSelectionChanged(UI_FishBagPrefab fishPrefab)
    {
        Debug.Log($"[FishBagView] 鱼类选择改变: ID={fishPrefab.ItemId}, IsSelected={fishPrefab.IsSelected}");
        UpdateTotalSellPrice();
    }

    private void ShowFishDetail()
    {
        if (fishDetail != null)
        {
            fishDetail.gameObject.SetActive(true);
        }
    }

    public void OpenBag()
    {
        RefreshItems();

        gameObject.SetActive(true);
        SendEvent();
        UpdateTotalSellPrice();

        CommunicateEvent.Modify(CommunicateEvent.EVENT_SYNC_GOLD);
    }

    public void OpenFishBag()
    {
        ClearAllSelections();
        OpenBag();
    }

    public void CloseFishBag()
    {
        gameObject.SetActive(false);
        ClearAllSelections();
    }

    public void InitFishBag()
    {
        CommunicateEvent.Modify("FishBag_Init");
    }

    private void SendEvent()
    {
        CommunicateEvent.Modify("FishBag_Open");
    }

    public void UpdateFishItems(Dictionary<int, int> fishInventory, Dictionary<int, ItemData> itemDataMap)
    {
        UpdateFishDetail(fishInventory, itemDataMap);
    }

    private void UpdateFishDetail(Dictionary<int, int> fishInventory, Dictionary<int, ItemData> itemDataMap)
    {
        if (fishDetail != null)
        {
            // ========== 添加调试日志 ==========
            Debug.Log($"[FishBagView] UpdateFishDetail - 传入数据: fishInventory数量={fishInventory?.Count ?? 0}, itemDataMap数量={itemDataMap?.Count ?? 0}");
            // ========== 调试日志结束 ==========

            fishDetail.UpdateFishItems(itemDataMap, fishInventory);
        }
    }

    public void RefreshItems()
    {
        // 直接从 PlayerDataManager 获取最新数据并更新UI
        if (PlayerDataManager.Instance != null)
        {
            var fishInventory = PlayerDataManager.Instance.GetFishInventory();
            var itemDataMap = LoadDataManager.Instance?.GetItemDataMap();

            // ========== 添加调试日志 ==========
            Debug.Log($"[FishBagView] RefreshItems - 鱼篓数据:");
            if (fishInventory != null)
            {
                int totalCount = 0;
                foreach (var kvp in fishInventory)
                {
                    totalCount += kvp.Value;
                    Debug.Log($"   物品ID: {kvp.Key}, 数量: {kvp.Value}");
                }
                Debug.Log($"   总数量: {totalCount}");
            }
            else
            {
                Debug.Log("   鱼篓数据为 null");
            }

            if (itemDataMap != null)
            {
                Debug.Log($"   物品数据映射数量: {itemDataMap.Count}");
            }
            // ========== 调试日志结束 ==========

            if (itemDataMap != null)
            {
                UpdateFishDetail(fishInventory, itemDataMap);
                UpdateFishCountDisplay(fishInventory);
                UpdateTotalSellPrice();
            }
        }
    }



    public void UpdateFishBagWithInventory(Dictionary<int, int> fishInventory, Dictionary<int, ItemData> itemDataMap)
    {
        UpdateFishDetail(fishInventory, itemDataMap);
        UpdateFishCountDisplay(fishInventory);
        UpdateTotalSellPrice();
    }

    private void UpdateFishCountDisplay(Dictionary<int, int> fishInventory)
    {
        if (fishCountText != null && fishInventory != null)
        {
            int totalCount = 0;
            foreach (var kvp in fishInventory)
            {
                totalCount += kvp.Value;
            }

            int maxCapacity = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_FISH_BAG_CAPACITY, 0);
            fishCountText.text = $"鱼篓: {totalCount}/{maxCapacity}";
        }
    }

    public void OnSelectAllButtonClick()
    {
        if (fishDetail != null)
        {
            List<UI_FishBagPrefab> allFishPrefabs = fishDetail.GetAllFishPrefabs();
            foreach (var fishPrefab in allFishPrefabs)
            {
                if (fishPrefab != null)
                {
                    fishPrefab.SetSelection(true);
                }
            }
        }

        UpdateTotalSellPrice();
    }

    public void OnSellButtonClick()
    {
        Debug.Log("[FishBagView] OnSellButtonClick 被调用");

        if (fishDetail != null)
        {
            List<UI_FishBagPrefab> allFishPrefabs = fishDetail.GetAllFishPrefabs();
            List<int> selectedItemIds = new List<int>();
            List<UI_FishBagPrefab> selectedPrefabs = new List<UI_FishBagPrefab>();  // 记录选中的预制体
            int totalPrice = 0;

            foreach (var fishPrefab in allFishPrefabs)
            {
                if (fishPrefab != null && fishPrefab.IsSelected)
                {
                    selectedItemIds.Add(fishPrefab.ItemId);
                    selectedPrefabs.Add(fishPrefab);  // 记录预制体引用
                    totalPrice += fishPrefab.GetTotalSellPrice();
                    Debug.Log($"[FishBagView] 选中物品: ID={fishPrefab.ItemId}, 价格={fishPrefab.GetTotalSellPrice()}");
                }
            }

            if (selectedItemIds.Count == 0)
            {
                Debug.Log("[FishBagView] 没有选中任何物品，无法售卖");
                return;
            }

            Debug.LogFormat("[FishBagView] 准备售卖 {0} 个物品，总价: {1}金币", selectedItemIds.Count, totalPrice);

            // 先立即隐藏选中的预制体（客户端即时反馈）
            foreach (var prefab in selectedPrefabs)
            {
                prefab.MarkAsSold();
            }

            Dictionary<string, object> sellData = new Dictionary<string, object>
        {
            { "itemIds", selectedItemIds },
            { "totalPrice", totalPrice }
        };
            CommunicateEvent.Modify(EVENT_FISHBAG_SELL, sellData);

            // 更新总价显示
            UpdateTotalSellPrice();
            UpdateFishCountDisplay(GetCurrentFishInventory());
        }
    }

    // 辅助方法：获取当前鱼篓数据（需要实现）
    private Dictionary<int, int> GetCurrentFishInventory()
    {
        if (PlayerDataManager.Instance != null)
        {
            return PlayerDataManager.Instance.GetFishInventory();
        }
        return new Dictionary<int, int>();
    }

    private void OnFishBagSell(Dictionary<string, object> data)
    {
        if (data.TryGetValue("itemIds", out object itemIdsObj) &&
            data.TryGetValue("totalPrice", out object totalPriceObj))
        {
            List<int> itemIds = itemIdsObj as List<int>;
            int totalPrice = System.Convert.ToInt32(totalPriceObj);

            if (itemIds != null && itemIds.Count > 0)
            {
                Debug.LogFormat("[FishBagView] 收到售卖请求: 物品数量={0}, 总价={1}", itemIds.Count, totalPrice);

                CommunicateEvent.Modify<(List<int>, int)>(CommunicateEvent.EVENT_SELL_FISH_ITEMS, (itemIds, totalPrice));
            }
        }
    }

    public void UpdateTotalSellPrice()
    {
        if (totalSellPriceText != null)
        {
            int totalPrice = 0;

            if (fishDetail != null)
            {
                List<UI_FishBagPrefab> allFishPrefabs = fishDetail.GetAllFishPrefabs();
                foreach (var fishPrefab in allFishPrefabs)
                {
                    if (fishPrefab != null && fishPrefab.IsSelected)
                    {
                        totalPrice += fishPrefab.GetTotalSellPrice();
                    }
                }
            }

            totalSellPriceText.text = $"总价: {totalPrice} 金币";
        }
    }

    public void ClearAllSelections()
    {
        isAllSelected = false;
        if (fishDetail != null)
        {
            List<UI_FishBagPrefab> allFishPrefabs = fishDetail.GetAllFishPrefabs();
            foreach (var fishPrefab in allFishPrefabs)
            {
                if (fishPrefab != null)
                {
                    fishPrefab.SetSelection(false);
                }
            }
        }
    }

    private void OnEnable()
    {
        // 注册数据更新事件
        CommunicateEvent.Register("FishBagDataUpdated", OnDataUpdated);
    }

    private void OnDisable()
    {
        CommunicateEvent.Unregister("FishBagDataUpdated", OnDataUpdated);
    }

    private void OnDataUpdated()
    {
        Debug.Log("[FishBagView] 收到数据更新事件，刷新鱼篓");
        RefreshItems();
    }
}