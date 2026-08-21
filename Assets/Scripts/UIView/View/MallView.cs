using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//using SharedModels;

public class MallView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;
    public Text goldText;
    public Transform contentTransform;
    public GameObject mallItemPrefab;
    public MallItemDetailView mallItemDetailView;

    private Dictionary<int, UI_MallPrefab> mallItemPrefabs = new Dictionary<int, UI_MallPrefab>();
    private List<int> currentMallItemIds = new List<int>();
    private Dictionary<int, MallItemData> mallData;
    private Dictionary<int, ItemData> itemDataMap;
    private bool isMallOpen = false;

    void Start()
    {
        if (maskBtn != null)
        {
            maskBtn.onClick.AddListener(OnMaskClick);
        }

        if (closeBtn != null)
        {
            closeBtn.onClick.AddListener(CloseMall);
        }

        CommunicateEvent.Register<int>("Mall_ItemClicked", OnMallItemClicked);

        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Register<Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_MALL_DATA_CHANGED, OnMallDataChanged);

        if (LoadDataManager.Instance != null)
        {
            itemDataMap = LoadDataManager.Instance.GetItemDataMap();
            Z_Logger.Log($"[MallView] 初始化 itemDataMap，共 {itemDataMap.Count} 个物品");
        }
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<int>("Mall_ItemClicked", OnMallItemClicked);
        CommunicateEvent.Unregister<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Unregister<Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_MALL_DATA_CHANGED, OnMallDataChanged);
    }

    private void OnMaskClick()
    {
        Z_Logger.Log("[MallView] OnMaskClick - 点击遮罩关闭");
        CloseMall();
    }

    private void OnMallItemClicked(int itemId)
    {
        Z_Logger.Log($"[MallView] OnMallItemClicked - itemId={itemId}");
        if (mallItemDetailView != null)
        {
            ItemData itemData = null;
            MallItemData mallItemData = null;

            if (itemDataMap != null && itemDataMap.TryGetValue(itemId, out itemData))
            {
                if (mallData != null && mallData.TryGetValue(itemId, out mallItemData))
                {
                    mallItemDetailView.ShowItem(itemId, itemData, mallItemData);
                    return;
                }
            }

            mallItemData = CommunicateEvent.Request<int, MallItemData>(CommunicateEvent.EVENT_GET_MALL_ITEM, itemId);
            if (mallItemData != null && LoadDataManager.Instance != null)
            {
                itemData = LoadDataManager.Instance.GetItemById(itemId);
                if (itemData != null)
                {
                    mallItemDetailView.ShowItem(itemId, itemData, mallItemData);
                }
            }
        }
    }

    public void OpenMall()
    {
        isMallOpen = true;
        gameObject.SetActive(true);
        RefreshMallData();
        CommunicateEvent.Modify("Mall_Open");
    }

    public void CloseMall()
    {
        Z_Logger.Log("[MallView] CloseMall - 关闭商城");
        isMallOpen = false;
        gameObject.SetActive(false);
        CommunicateEvent.Modify("Mall_Close");
    }

    private void OnGoldChanged(Dictionary<string, object> data)
    {
        if (goldText != null && data.ContainsKey("gold"))
        {
            goldText.text = data["gold"].ToString();
        }
    }

    private void OnMallDataChanged(Dictionary<int, MallItemData> newMallData)
    {
        Z_Logger.Log($"[MallView] OnMallDataChanged - 收到商城数据更新，共 {newMallData?.Count ?? 0} 个商品");

        if (newMallData == null || newMallData.Count == 0)
        {
            Z_Logger.LogWarning("[MallView] OnMallDataChanged - 收到的数据为空");
            mallData = newMallData;
            if (isMallOpen)
            {
                ClearAllItems();
            }
            return;
        }

        // ✅ 检测上架状态变化
        if (mallData != null)
        {
            foreach (var kvp in newMallData)
            {
                int itemId = kvp.Key;
                MallItemData newItem = kvp.Value;

                if (mallData.TryGetValue(itemId, out var oldItem))
                {
                    // ✅ 如果商品从下架变为上架，需要显示
                    if (!oldItem.isOnSale && newItem.isOnSale)
                    {
                        Z_Logger.Log($"[MallView] 商品 {itemId} 已上架，将显示");
                    }
                    // ✅ 如果商品从上架变为下架，需要隐藏
                    else if (oldItem.isOnSale && !newItem.isOnSale)
                    {
                        Z_Logger.Log($"[MallView] 商品 {itemId} 已下架，将隐藏");
                        if (mallItemPrefabs.TryGetValue(itemId, out var prefab))
                        {
                            prefab.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }

        mallData = newMallData;

        if (isMallOpen)
        {
            Z_Logger.Log("[MallView] 商城已打开，立即刷新UI");
            UpdateMallItems();
        }
        else
        {
            Z_Logger.Log("[MallView] 商城未打开，数据已缓存，下次打开时生效");
        }
    }

    public void RefreshMallData()
    {
        Z_Logger.Log("[MallView] RefreshMallData - 从服务器请求最新商城数据");
        mallData = CommunicateEvent.Request<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, 0);

        if (mallData != null && mallData.Count > 0)
        {
            Z_Logger.Log($"[MallView] 从服务器获取到 {mallData.Count} 个商品");
            UpdateMallItems();
        }
        else
        {
            Z_Logger.LogWarning("[MallView] 从服务器获取商城数据失败或为空");
            ClearAllItems();
        }
    }

    private void UpdateGoldDisplay()
    {
        int gold = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_GOLD, 0);
        if (goldText != null)
        {
            goldText.text = gold.ToString();
        }
    }

    /// <summary>
    /// ✅ 清空所有商品（用于数据为空时）
    /// </summary>
    private void ClearAllItems()
    {
        foreach (var kvp in mallItemPrefabs)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        mallItemPrefabs.Clear();
        currentMallItemIds.Clear();
    }

    private void UpdateMallItems()
    {
        if (mallData == null || mallData.Count == 0)
        {
            Z_Logger.LogWarning("[MallView] UpdateMallItems - mallData 为空");
            ClearAllItems();
            return;
        }

        if (itemDataMap == null || itemDataMap.Count == 0)
        {
            if (LoadDataManager.Instance != null)
            {
                itemDataMap = LoadDataManager.Instance.GetItemDataMap();
                Z_Logger.Log($"[MallView] 延迟初始化 itemDataMap，共 {itemDataMap.Count} 个物品");
            }
            if (itemDataMap == null || itemDataMap.Count == 0)
            {
                Z_Logger.LogWarning("[MallView] itemDataMap 为空，无法更新商城物品");
                return;
            }
        }

        currentMallItemIds.Clear();

        int updatedCount = 0;
        int hiddenCount = 0;
        int removedCount = 0;

        // ✅ 先检查需要移除的商品（已下架且不在新数据中）
        List<int> toRemove = new List<int>();
        foreach (var kvp in mallItemPrefabs)
        {
            int itemId = kvp.Key;
            if (!mallData.ContainsKey(itemId))
            {
                toRemove.Add(itemId);
                removedCount++;
            }
        }
        foreach (var id in toRemove)
        {
            if (mallItemPrefabs.TryGetValue(id, out var prefab))
            {
                Destroy(prefab.gameObject);
                mallItemPrefabs.Remove(id);
            }
        }

        foreach (var kvp in mallData)
        {
            int itemId = kvp.Key;
            MallItemData mallItem = kvp.Value;

            if (mallItem == null)
                continue;

            // ✅ 检查是否上架，未上架的商品跳过显示
            if (!mallItem.isOnSale)
            {
                hiddenCount++;
                // 如果已存在则隐藏
                if (mallItemPrefabs.ContainsKey(itemId))
                {
                    mallItemPrefabs[itemId].gameObject.SetActive(false);
                }
                continue;
            }

            if (!itemDataMap.TryGetValue(itemId, out ItemData itemData))
            {
                Z_Logger.LogWarning($"[MallView] 未找到物品数据: itemId={itemId}");
                continue;
            }

            currentMallItemIds.Add(itemId);

            if (mallItemPrefabs.ContainsKey(itemId))
            {
                var prefab = mallItemPrefabs[itemId];
                prefab.UpdateDisplay(itemData, mallItem);
                prefab.gameObject.SetActive(true);
                updatedCount++;
                Z_Logger.Log($"[MallView] 更新商品: itemId={itemId}, stock={mallItem.stock}, isOnSale={mallItem.isOnSale}");
            }
            else
            {
                CreateMallItemPrefab(itemId, itemData, mallItem);
                updatedCount++;
            }
        }

        // ✅ 清理不在当前列表中的预制体（已隐藏的）
        ReturnUnusedToPool();

        Z_Logger.Log($"[MallView] UpdateMallItems 完成，更新了 {updatedCount} 个商品，隐藏了 {hiddenCount} 个未上架商品，移除了 {removedCount} 个已下架商品");
    }

    private void CreateMallItemPrefab(int itemId, ItemData itemData, MallItemData mallItem)
    {
        if (mallItemPrefab == null)
        {
            Z_Logger.LogError("[MallView] mallItemPrefab is not assigned");
            return;
        }

        GameObject itemObj = Instantiate(mallItemPrefab, contentTransform);
        UI_MallPrefab mallPrefab = itemObj.GetComponent<UI_MallPrefab>();

        if (mallPrefab == null)
        {
            Destroy(itemObj);
            Z_Logger.LogError("[MallView] UI_MallPrefab component not found");
            return;
        }

        mallPrefab.Init(itemId, itemData, mallItem);
        mallPrefab.gameObject.SetActive(true);
        mallItemPrefabs[itemId] = mallPrefab;

        Z_Logger.Log($"[MallView] 创建商品预制体: itemId={itemId}, stock={mallItem.stock}, isOnSale={mallItem.isOnSale}");
    }

    private void ReturnUnusedToPool()
    {
        List<int> toRemove = new List<int>();
        foreach (var kvp in mallItemPrefabs)
        {
            if (!currentMallItemIds.Contains(kvp.Key))
            {
                // ✅ 如果在当前数据中不存在，则删除（而不是隐藏）
                if (mallData != null && !mallData.ContainsKey(kvp.Key))
                {
                    Destroy(kvp.Value.gameObject);
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    kvp.Value.gameObject.SetActive(false);
                }
            }
        }
        foreach (var id in toRemove)
        {
            mallItemPrefabs.Remove(id);
        }
    }

    public void UpdateMallItemStock(int itemId)
    {
        if (mallItemPrefabs.ContainsKey(itemId) && mallData != null)
        {
            if (mallData.TryGetValue(itemId, out MallItemData mallItem))
            {
                if (itemDataMap.TryGetValue(itemId, out ItemData itemData))
                {
                    mallItemPrefabs[itemId].UpdateDisplay(itemData, mallItem);
                    Z_Logger.Log($"[MallView] 更新商品库存: itemId={itemId}, stock={mallItem.stock}");
                }
            }
        }
    }

    public void OnItemPurchased(int itemId)
    {
        UpdateGoldDisplay();
        UpdateMallItemStock(itemId);
    }

    /// <summary>
    /// ✅ 强制刷新商城（外部调用）
    /// </summary>
    public void ForceRefresh()
    {
        Z_Logger.Log("[MallView] ForceRefresh - 强制刷新商城");
        if (isMallOpen)
        {
            RefreshMallData();
        }
    }
}
