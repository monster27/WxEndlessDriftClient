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

    /// <summary>
    /// 直接添加鱼到鱼篓（不经过网络请求，用于轮询时临时更新）
    /// </summary>
    /// <param name="fishId">鱼ID</param>
    /// <param name="quantity">数量</param>
    public void AddFishToInventoryDirectly(int fishId, int quantity)
    {
        if (fishInventory == null)
        {
            fishInventory = new Dictionary<int, int>();
        }

        if (fishInventory.ContainsKey(fishId))
        {
            fishInventory[fishId] += quantity;
        }
        else
        {
            fishInventory[fishId] = quantity;
        }

        Debug.Log($"[PlayerDataManager] 直接添加鱼到鱼篓: ID={fishId}, 数量={quantity}, 当前总数={GetTotalFishCount()}");

        // 发送数据更新事件，通知UI刷新
        CommunicateEvent.Modify("FishBagDataUpdated");

        // 如果鱼篓界面是打开的，直接刷新
        if (UIManager.Instance?.fishBagView != null && UIManager.Instance.fishBagView.gameObject.activeSelf)
        {
            UIManager.Instance.fishBagView.RefreshItems();
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
        Debug.Log("[PlayerDataManager] ===== 开始同步背包数据 =====");

        // 1. 同步背包
        playerInventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, 0);
        Debug.Log($"[PlayerDataManager] 背包数据同步完成，物品数: {playerInventory?.Count ?? 0}");

        // 2. 获取服务器最新的鱼篓数据
        var serverFishInventory = CommunicateEvent.Request<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_FISH_INVENTORY, 0);
        Debug.Log($"[PlayerDataManager] 服务器鱼篓数据: {(serverFishInventory != null ? serverFishInventory.Count : 0)} 种物品");

        if (serverFishInventory != null)
        {
            foreach (var kvp in serverFishInventory)
            {
                Debug.Log($"   服务器数据 - ID: {kvp.Key}, 数量: {kvp.Value}");
            }
        }

        // 3. 合并本地临时添加的数据
        if (fishInventory == null)
        {
            fishInventory = new Dictionary<int, int>();
        }

        // 关键修复：直接使用服务器数据覆盖，因为服务器数据是最准确的
        // 之前的合并逻辑可能导致数据不一致
        fishInventory.Clear();
        foreach (var kvp in serverFishInventory)
        {
            fishInventory[kvp.Key] = kvp.Value;
        }

        // 4. 同步鱼篓容量
        fishBagCapacity = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_FISH_BAG_CAPACITY, 0);
        Debug.Log($"[PlayerDataManager] 鱼篓容量: {fishBagCapacity}");

        // 5. 打印最终数据
        Debug.Log($"[PlayerDataManager] 最终鱼篓数据: {fishInventory.Count} 种物品");
        if (fishInventory.Count > 0)
        {
            int totalCount = 0;
            foreach (var kvp in fishInventory)
            {
                totalCount += kvp.Value;
                Debug.Log($"   物品ID: {kvp.Key}, 数量: {kvp.Value}");
            }
            Debug.Log($"   鱼篓总数量: {totalCount}/{fishBagCapacity}");
        }
        else
        {
            Debug.Log("   鱼篓为空");
        }

        PrintAllData();
        CheckAndUpdateAnimationState();

        // 6. 通知UI更新
        CommunicateEvent.Modify("FishBagDataUpdated");

        // 7. 新增：通知普通背包数据更新（钓鱼消耗鱼饵后需要刷新背包UI）
        CommunicateEvent.Modify("Bag_RefreshItems");

        // 8. 如果鱼篓界面是打开的，直接刷新
        if (UIManager.Instance?.fishBagView != null && UIManager.Instance.fishBagView.gameObject.activeSelf)
        {
            UIManager.Instance.fishBagView.RefreshItems();
            Debug.Log("[PlayerDataManager] 已刷新鱼篓UI");
        }

        Debug.Log("[PlayerDataManager] ===== 背包数据同步完成 =====");
    }

    private void CheckAndUpdateAnimationState()
    {
        if (NetServerManager.Instance == null) return;

        bool isFull = IsFishBagFull();

        if (isFull)
        {
            // 鱼篓已满 -> Lazy动画（无法钓鱼）
            Debug.Log("[PlayerDataManager] 鱼篓已满，切换到懒动画");
            NetServerManager.Instance.NotifyPlayLazyAnimation();
        }
        else
        {
            // 鱼篓未满时，检查是否正在播放Reel动画
            if (NetServerManager.Instance.IsPlayingReelAnimation)
            {
                // 正在播放拉杆动画，不切换状态
                Debug.Log("[PlayerDataManager] 正在播放拉杆动画，保持当前动画");
                return;
            }

            // 鱼篓未满且未在播放动画，切换到空闲动画（等待下次钓鱼）
            // 注意：服务器的停滞状态（isPaused）是钓鱼后的正常等待，
            // 此时应该显示Idle动画而不是Lazy动画
            Debug.Log("[PlayerDataManager] 鱼篓未满，切换到空闲动画");
            NetServerManager.Instance.NotifyPlayIdleAnimation();
        }
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