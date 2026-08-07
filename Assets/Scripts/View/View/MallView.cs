using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SharedModels;

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
            Debug.Log($"[MallView] 初始化 itemDataMap，共 {itemDataMap.Count} 个物品");
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
        Debug.Log("[MallView] OnMaskClick - 点击遮罩关闭");
        CloseMall();
    }

    private void OnMallItemClicked(int itemId)
    {
        Debug.Log($"[MallView] OnMallItemClicked - itemId={itemId}");
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
        gameObject.SetActive(true);
        RefreshMallData();
        CommunicateEvent.Modify("Mall_Open");
    }

    public void CloseMall()
    {
        Debug.Log("[MallView] CloseMall - 关闭商城");
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
        Debug.Log($"[MallView] OnMallDataChanged - 收到商城数据更新，共 {newMallData?.Count ?? 0} 个商品");

        if (newMallData == null || newMallData.Count == 0)
        {
            Debug.LogWarning("[MallView] OnMallDataChanged - 收到的数据为空");
            return;
        }

        // ✅ 打印每个商品的库存，便于调试
        foreach (var kvp in newMallData)
        {
            Debug.Log($"[MallView] 收到商品数据: itemId={kvp.Key}, stock={kvp.Value.stock}");
        }

        // ✅ 直接使用传入的新数据
        mallData = newMallData;

        // ✅ 如果商城打开，立即刷新UI
        if (gameObject.activeSelf)
        {
            Debug.Log("[MallView] 商城已打开，立即刷新UI");
            UpdateMallItems();
        }
        else
        {
            Debug.Log("[MallView] 商城未打开，数据已缓存，下次打开时生效");
        }
    }

    public void RefreshMallData()
    {
        Debug.Log("[MallView] RefreshMallData - 从服务器请求最新商城数据");
        mallData = CommunicateEvent.Request<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, 0);

        if (mallData != null && mallData.Count > 0)
        {
            Debug.Log($"[MallView] 从服务器获取到 {mallData.Count} 个商品");
            UpdateMallItems();
        }
        else
        {
            Debug.LogWarning("[MallView] 从服务器获取商城数据失败或为空");
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

    private void UpdateMallItems()
    {
        if (mallData == null || mallData.Count == 0)
        {
            Debug.LogWarning("[MallView] UpdateMallItems - mallData 为空");
            return;
        }

        if (itemDataMap == null || itemDataMap.Count == 0)
        {
            if (LoadDataManager.Instance != null)
            {
                itemDataMap = LoadDataManager.Instance.GetItemDataMap();
                Debug.Log($"[MallView] 延迟初始化 itemDataMap，共 {itemDataMap.Count} 个物品");
            }
            if (itemDataMap == null || itemDataMap.Count == 0)
            {
                Debug.LogWarning("[MallView] itemDataMap 为空，无法更新商城物品");
                return;
            }
        }

        currentMallItemIds.Clear();
        ReturnUnusedToPool();

        int updatedCount = 0;
        foreach (var kvp in mallData)
        {
            int itemId = kvp.Key;
            MallItemData mallItem = kvp.Value;

            if (mallItem == null)
                continue;

            if (!itemDataMap.TryGetValue(itemId, out ItemData itemData))
            {
                Debug.LogWarning($"[MallView] 未找到物品数据: itemId={itemId}");
                continue;
            }

            currentMallItemIds.Add(itemId);

            if (mallItemPrefabs.ContainsKey(itemId))
            {
                // ✅ 强制更新显示
                var prefab = mallItemPrefabs[itemId];
                prefab.UpdateDisplay(itemData, mallItem);
                prefab.gameObject.SetActive(true);
                updatedCount++;

                Debug.Log($"[MallView] 更新商品: itemId={itemId}, stock={mallItem.stock}");
            }
            else
            {
                CreateMallItemPrefab(itemId, itemData, mallItem);
                updatedCount++;
            }
        }

        Debug.Log($"[MallView] UpdateMallItems 完成，更新了 {updatedCount} 个商品");
    }

    private void CreateMallItemPrefab(int itemId, ItemData itemData, MallItemData mallItem)
    {
        if (mallItemPrefab == null)
        {
            Debug.LogError("[MallView] mallItemPrefab is not assigned");
            return;
        }

        GameObject itemObj = Instantiate(mallItemPrefab, contentTransform);
        UI_MallPrefab mallPrefab = itemObj.GetComponent<UI_MallPrefab>();

        if (mallPrefab == null)
        {
            Destroy(itemObj);
            Debug.LogError("[MallView] UI_MallPrefab component not found");
            return;
        }

        mallPrefab.Init(itemId, itemData, mallItem);
        mallPrefab.gameObject.SetActive(true);
        mallItemPrefabs[itemId] = mallPrefab;

        Debug.Log($"[MallView] 创建商品预制体: itemId={itemId}, stock={mallItem.stock}");
    }

    private void ReturnUnusedToPool()
    {
        List<int> toRemove = new List<int>();
        foreach (var kvp in mallItemPrefabs)
        {
            if (!currentMallItemIds.Contains(kvp.Key))
            {
                kvp.Value.gameObject.SetActive(false);
                toRemove.Add(kvp.Key);
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
                    Debug.Log($"[MallView] 更新商品库存: itemId={itemId}, stock={mallItem.stock}");
                }
            }
        }
    }

    public void OnItemPurchased(int itemId)
    {
        UpdateGoldDisplay();
        UpdateMallItemStock(itemId);
    }
}
