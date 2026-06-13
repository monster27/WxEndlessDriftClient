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

        // 订阅金币变更事件
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Register<Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_MALL_DATA_CHANGED, OnMallDataChanged);

        // 初始化物品数据映射
        if (LoadDataManager.Instance != null)
        {
            itemDataMap = LoadDataManager.Instance.GetItemDataMap();
            Debug.Log($"[MallView] 初始化 itemDataMap，共 {itemDataMap.Count} 个物品");
        }
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<int>("Mall_ItemClicked", OnMallItemClicked);

        // 取消订阅金币变更事件
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
                // 尝试从当前 mallData 获取
                if (mallData != null && mallData.TryGetValue(itemId, out mallItemData))
                {
                    mallItemDetailView.ShowItem(itemId, itemData, mallItemData);
                    return;
                }
            }

            // 如果本地没有，从服务器获取最新数据
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
        mallData = newMallData;
        RefreshMallData();
    }

    public void RefreshMallData()
    {
        mallData = CommunicateEvent.Request<int, Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_GET_MALL_ITEMS, 0);
        UpdateMallItems();
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
        if (mallData == null)
            return;
        
        if (itemDataMap == null)
        {
            if (LoadDataManager.Instance != null)
            {
                itemDataMap = LoadDataManager.Instance.GetItemDataMap();
                Debug.Log($"[MallView] 延迟初始化 itemDataMap，共 {itemDataMap.Count} 个物品");
            }
            if (itemDataMap == null)
            {
                Debug.LogWarning("[MallView] itemDataMap 仍然为 null，无法更新商城物品");
                return;
            }
        }

        currentMallItemIds.Clear();
        ReturnUnusedToPool();

        foreach (var kvp in mallData)
        {
            int itemId = kvp.Key;
            MallItemData mallItem = kvp.Value;

            if (mallItem == null)
                continue;

            if (!itemDataMap.TryGetValue(itemId, out ItemData itemData))
                continue;

            currentMallItemIds.Add(itemId);

            if (mallItemPrefabs.ContainsKey(itemId))
            {
                mallItemPrefabs[itemId].UpdateDisplay(itemData, mallItem);
                mallItemPrefabs[itemId].gameObject.SetActive(true);
            }
            else
            {
                CreateMallItemPrefab(itemId, itemData, mallItem);
            }
        }
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