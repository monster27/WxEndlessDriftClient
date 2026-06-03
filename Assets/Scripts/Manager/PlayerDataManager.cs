using System.Collections.Generic;
using UnityEngine;
using System.Text;

public class PlayerDataManager : SingletonMono<PlayerDataManager>
{
    private Dictionary<int, int> playerInventory = new Dictionary<int, int>();
    private Dictionary<int, int> fishInventory = new Dictionary<int, int>();

    private int fishBagCapacity = 20;
    private int gold = 0;

    public void Init()
    {
        RegisterEvents();

        if (LoadDataManager.Instance != null)
        {
            if (LoadDataManager.Instance.isDataLoaded)
            {
                SyncInventoryFromServer();
                SyncGoldFromServer();
            }
            else
            {
                LoadDataManager.Instance.onDataLoaded += () => {
                    SyncInventoryFromServer();
                    SyncGoldFromServer();
                };
            }
        }
        else
        {
            Debug.LogError("[PlayerDataManager] LoadDataManager未找到");
        }
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_GOLD_CHANGED, OnGoldChanged);
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_INVENTORY, SyncInventoryFromServer);
    }

    private void OnGoldChanged(Dictionary<string, object> data)
    {
        if (data.TryGetValue("gold", out object goldObj))
        {
            gold = System.Convert.ToInt32(goldObj);
            Debug.LogFormat("[PlayerDataManager] 金币变化: {0}", gold);
            UpdateGoldUI();
        }
    }

    private void UpdateGoldUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateGoldDisplay(gold);
        }
    }

    public void SyncGoldFromServer()
    {
        gold = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_GOLD, 0);
        Debug.LogFormat("[PlayerDataManager] 同步金币: {0}", gold);
        UpdateGoldUI();
    }

    public void SyncInventoryFromServer()
    {
        playerInventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
        fishInventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_FISH_INVENTORY, 0);
        fishBagCapacity = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_FISH_BAG_CAPACITY, 0);
        PrintAllData();
    }

    private void PrintAllData()
    {
        StringBuilder logBuilder = new StringBuilder();
        logBuilder.AppendLine("[PlayerDataManager] 数据同步完成:");

        logBuilder.AppendLine("  背包数据:");
        if (playerInventory == null || playerInventory.Count == 0)
        {
            logBuilder.AppendLine("    背包为空");
        }
        else
        {
            foreach (var item in playerInventory)
            {
                logBuilder.AppendLine($"    物品ID: {item.Key}, 数量: {item.Value}");
            }
        }

        logBuilder.AppendLine($"  鱼篓容量: {fishBagCapacity}");
        logBuilder.AppendLine("  鱼篓数据:");
        if (fishInventory == null || fishInventory.Count == 0)
        {
            logBuilder.AppendLine("    鱼篓为空");
        }
        else
        {
            foreach (var item in fishInventory)
            {
                logBuilder.AppendLine($"    物品ID: {item.Key}, 数量: {item.Value}");
            }
        }

        Debug.Log(logBuilder.ToString());
    }

    public void AddItem(int itemId, int quantity)
    {
        CommunicateEvent.Modify(CommunicateEvent.EVENT_ADD_ITEM, (itemId, quantity));
        SyncInventoryFromServer();
    }

    public void RemoveItem(int itemId, int quantity)
    {
        CommunicateEvent.Modify(CommunicateEvent.EVENT_REMOVE_ITEM, (itemId, quantity));
        SyncInventoryFromServer();
    }

    public void AddFishToInventory(int fishId, int quantity)
    {
        CommunicateEvent.Modify(CommunicateEvent.EVENT_ADD_FISH, (fishId, quantity));
        SyncInventoryFromServer();
    }

    public int GetItemQuantity(int itemId)
    {
        return playerInventory != null && playerInventory.ContainsKey(itemId) ? playerInventory[itemId] : 0;
    }

    public Dictionary<int, int> GetInventory()
    {
        return playerInventory != null ? new Dictionary<int, int>(playerInventory) : new Dictionary<int, int>();
    }

    public Dictionary<int, int> GetFishInventory()
    {
        return fishInventory != null ? new Dictionary<int, int>(fishInventory) : new Dictionary<int, int>();
    }

    public void RefreshUI()
    {
        if (UIManager.Instance != null)
        {
            if (UIManager.Instance.bagView != null)
            {
                UIManager.Instance.bagView.RefreshItems();
            }
            if (UIManager.Instance.fishBagView != null)
            {
                UIManager.Instance.fishBagView.RefreshItems();
            }
        }
    }

    public int FishBagCapacity => fishBagCapacity;

    public int GetTotalFishCount()
    {
        int total = 0;
        if (fishInventory != null)
        {
            foreach (var kvp in fishInventory)
            {
                total += kvp.Value;
            }
        }
        return total;
    }

    public bool IsFishBagFull()
    {
        return GetTotalFishCount() >= fishBagCapacity;
    }

    public int GetFishBagRemainingSpace()
    {
        return fishBagCapacity - GetTotalFishCount();
    }

    public bool ShouldEnterLazyState()
    {
        return IsFishBagFull();
    }

    public bool ShouldEnterAutoFishingState()
    {
        return !IsFishBagFull();
    }
}