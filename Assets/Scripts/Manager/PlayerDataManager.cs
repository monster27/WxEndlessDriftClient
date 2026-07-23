using System.Collections.Generic;
using UnityEngine;
using System.Text;
using SharedModels;
using System.Collections;
using System.Linq;

public class PlayerDataManager : SingletonMono<PlayerDataManager>
{
    private Dictionary<int, int> playerInventory = new Dictionary<int, int>();
    private Dictionary<int, int> fishInventory = new Dictionary<int, int>();
    private Dictionary<int, List<FishDetailData>> fishDetailData = new Dictionary<int, List<FishDetailData>>();

    private int fishBagCapacity = 20;
    private int gold = 0;

    private bool _isInitialized = false;
    private bool _isSyncing = false;
    private bool _hasSubscribedToNetServer = false;
    private bool _isWaitingForNetServer = false;
    private bool _isReady = false;

    /// <summary>
    /// 管理器是否已就绪（可以安全调用同步方法）
    /// </summary>
    public bool IsReady => _isReady;

    public void Init()
    {
        if (_isInitialized)
        {
            Debug.Log("[PlayerDataManager] 已经初始化完成，跳过");
            return;
        }

        RegisterEvents();

        // 订阅 NetServerManager 初始化完成事件
        SubscribeToNetServerInitialization();

        _isInitialized = true;
        _isReady = true;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[PlayerDataManager] 初始化完成");
    }

    private void SubscribeToNetServerInitialization()
    {
        if (_hasSubscribedToNetServer || _isWaitingForNetServer) return;

        if (NetServerManager.Instance != null)
        {
            // 如果 NetServerManager 已经初始化完成且事件已注册，直接同步
            if (NetServerManager.Instance.IsInitialized)
            {
                Debug.Log("[PlayerDataManager] NetServerManager 已初始化，直接同步数据");
                SyncInventoryFromServer();
                SyncGoldFromServer();
            }
            else
            {
                // 订阅初始化完成事件
                NetServerManager.Instance.OnInitializationComplete += OnNetServerInitialized;
                _hasSubscribedToNetServer = true;
                Debug.Log("[PlayerDataManager] 已订阅 NetServerManager 初始化完成事件，等待初始化...");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerDataManager] NetServerManager 实例不存在，延迟订阅");
            _isWaitingForNetServer = true;
            StartCoroutine(DelayedSubscribe());
        }
    }

    private IEnumerator DelayedSubscribe()
    {
        int retryCount = 0;
        while (retryCount < 10)
        {
            yield return new WaitForSeconds(0.1f);

            if (NetServerManager.Instance != null)
            {
                _isWaitingForNetServer = false;

                if (NetServerManager.Instance.IsInitialized)
                {
                    Debug.Log("[PlayerDataManager] NetServerManager 已初始化，同步数据");
                    SyncInventoryFromServer();
                    SyncGoldFromServer();
                }
                else if (!_hasSubscribedToNetServer)
                {
                    NetServerManager.Instance.OnInitializationComplete += OnNetServerInitialized;
                    _hasSubscribedToNetServer = true;
                    Debug.Log("[PlayerDataManager] 延迟订阅 NetServerManager 初始化完成事件成功");
                }
                yield break;
            }
            retryCount++;
        }

        _isWaitingForNetServer = false;
        Debug.LogWarning("[PlayerDataManager] 无法找到 NetServerManager 实例，数据同步可能失败");
    }

    private void RegisterEvents()
    {
        // 金币变化
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_GOLD_CHANGED, OnGoldChanged);

        // 同步背包
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_INVENTORY, SyncInventoryFromServer);

        // 人物解锁
        CommunicateEvent.Register<List<int>>("SyncUnlockedCharacters", OnSyncUnlockedCharacters);

        // 装备解锁
        CommunicateEvent.Register<List<int>>("SyncUnlockedEquipment", OnSyncUnlockedEquipment);

        // 商城数据
        CommunicateEvent.Register<Dictionary<int, MallItemData>>("S2C_EVENT_MALL_DATA_CHANGED", OnMallDataChanged);

        // 人物数据变化
        CommunicateEvent.Register<(int, int, int)>(CommunicateEvent.EVENT_CHARACTER_DATA_CHANGED, OnCharacterDataChanged);

        // 背包数据更新
        CommunicateEvent.Register<Dictionary<int, int>>("BagDataUpdated", OnBagDataUpdated);

        // 鱼篓数据更新
        CommunicateEvent.Register("FishBagDataUpdated", OnFishBagDataUpdated);

        // ✅ 注册UI数据更新请求事件
        CommunicateEvent.Register("UI_RequestUpdateAllData", OnRequestUpdateAllData);

        Debug.Log("[PlayerDataManager] 事件注册完成");
    }

    private void OnNetServerInitialized()
    {
        Debug.Log("[PlayerDataManager] NetServerManager 初始化完成，同步数据");
        SyncInventoryFromServer();
        SyncGoldFromServer();
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
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateGoldDisplay(gold);
        }
    }


    public void SyncGoldFromServer()
    {
        if (_isSyncing) return;

        try
        {
            if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[PlayerDataManager] NetServerManager 未初始化，跳过金币同步");
                return;
            }

            gold = CommunicateEvent.Request<int, int>("VIEW_EVENT_GET_GOLD", 0);
            Debug.LogFormat("[PlayerDataManager] 同步金币: {0}", gold);
            UpdateGoldUI();

            // ✅ 通知UI更新金币
            CommunicateEvent.Modify(CommunicateEvent.EVENT_CLIENT_GOLD_CHANGED, new Dictionary<string, object> { { "gold", gold } });
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerDataManager] 同步金币失败: {ex.Message}");
        }
    }

    public void SyncInventoryFromServer()
    {
        if (_isSyncing)
        {
            Debug.Log("[PlayerDataManager] 正在同步中，跳过重复调用");
            return;
        }

        _isSyncing = true;

        try
        {
            Debug.Log("[PlayerDataManager] ===== 开始同步背包数据 =====");

            if (NetServerManager.Instance == null || !NetServerManager.Instance.IsInitialized)
            {
                Debug.LogWarning("[PlayerDataManager] NetServerManager 未初始化，跳过同步");
                _isSyncing = false;
                return;
            }

            // 1. 同步背包
            var inventory = CommunicateEvent.Request<int, Dictionary<int, int>>("VIEW_EVENT_GET_INVENTORY", 0);
            if (inventory != null)
            {
                playerInventory = inventory;
                Debug.Log($"[PlayerDataManager] 背包数据同步完成，物品数: {playerInventory?.Count ?? 0}");
            }

            // 2. 获取服务器最新的鱼篓数据（数量汇总）
            var serverFishInventory = CommunicateEvent.Request<int, Dictionary<int, int>>("VIEW_EVENT_GET_FISH_INVENTORY", 0);
            Debug.Log($"[PlayerDataManager] 服务器鱼篓数据: {(serverFishInventory != null ? serverFishInventory.Count : 0)} 种物品");

            // ✅ 关键修复：同步鱼详情数据
            // 通过 VIEW_EVENT_GET_FISH_DETAIL_DATA 从 NetServerManager 获取详情数据
            var serverFishDetailData = CommunicateEvent.Request<int, Dictionary<int, List<FishDetailData>>>("VIEW_EVENT_GET_FISH_DETAIL_DATA", 0);
            if (serverFishDetailData != null)
            {
                fishDetailData = serverFishDetailData;
                Debug.Log($"[PlayerDataManager] 鱼详情数据同步完成: {fishDetailData.Count} 种鱼");
            }
            else
            {
                // 降级方案：如果获取不到，从 NetServerManager 直接获取
                if (NetServerManager.Instance != null)
                {
                    // 需要 NetServerManager 暴露 GetFishDetailData 方法
                    // 或者直接调用 NetServerManager.Instance.FetchPlayerFishBag()
                }
            }

            // 3. 更新鱼篓数据
            if (fishInventory == null)
            {
                fishInventory = new Dictionary<int, int>();
            }
            fishInventory.Clear();
            if (serverFishInventory != null)
            {
                foreach (var kvp in serverFishInventory)
                {
                    fishInventory[kvp.Key] = kvp.Value;
                }
            }

            // 4. 同步鱼篓容量
            fishBagCapacity = CommunicateEvent.Request<int, int>("VIEW_EVENT_GET_FISH_BAG_CAPACITY", 0);
            Debug.Log($"[PlayerDataManager] 鱼篓容量: {fishBagCapacity}");

            // 5. 打印最终数据
            Debug.Log($"[PlayerDataManager] 最终鱼篓数据: {fishInventory.Count} 种物品");
            int totalCount = 0;
            foreach (var kvp in fishInventory)
            {
                totalCount += kvp.Value;
                Debug.Log($"   物品ID: {kvp.Key}, 数量: {kvp.Value}");
            }
            Debug.Log($"   鱼篓总数量: {totalCount}/{fishBagCapacity}");

            PrintAllData();
            CheckAndUpdateAnimationState();

            // 6. 通知UI更新
            CommunicateEvent.Modify("FishBagDataUpdated");
            CommunicateEvent.Modify("Bag_RefreshItems");

            if (GameUIManager.Instance?.fishBagView != null && GameUIManager.Instance.fishBagView.gameObject.activeSelf)
            {
                GameUIManager.Instance.fishBagView.RefreshItems();
                Debug.Log("[PlayerDataManager] 已刷新鱼篓UI");
            }

            if (GameUIManager.Instance?.bagView != null && GameUIManager.Instance.bagView.gameObject.activeSelf)
            {
                var bagInventory = GetInventory();
                var itemDataMap = LoadDataManager.Instance?.GetItemDataMap();
                if (bagInventory != null && itemDataMap != null)
                {
                    GameUIManager.Instance.bagView.UpdateBagItems(bagInventory, itemDataMap);
                    Debug.Log("[PlayerDataManager] 已刷新背包UI");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerDataManager] 同步背包数据异常: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            _isSyncing = false;
        }

        Debug.Log("[PlayerDataManager] ===== 背包数据同步完成 =====");
    }

    /// <summary>
    /// 用服务器返回的背包数据直接覆盖本地数据（事件驱动更新，避免额外网络请求）
    /// </summary>
    // PlayerDataManager.cs - 修改 UpdateInventoryFromServer 方法

    /// <summary>
    /// 用服务器返回的背包数据直接覆盖本地数据（事件驱动更新，避免额外网络请求）
    /// </summary>
    public void UpdateInventoryFromServer(Dictionary<int, int> newInventory)
    {
        if (newInventory == null)
        {
            Debug.LogWarning("[PlayerDataManager] UpdateInventoryFromServer: 新背包数据为空，跳过");
            return;
        }

        // ✅ 先更新数据
        playerInventory = new Dictionary<int, int>(newInventory);
        Debug.Log($"[PlayerDataManager] 从服务器响应更新背包数据，物品数: {playerInventory.Count}");

        CheckAndUpdateAnimationState();

        // ✅ 先触发背包刷新事件（EquipPlayerView 监听了这个事件）
        CommunicateEvent.Modify("Bag_RefreshItems");
        CommunicateEvent.Modify("BaitCountChanged");

        // ✅ 再触发装备刷新事件（确保所有装备UI都能刷新）
        CommunicateEvent.Modify("Equipment_Refresh");

        // 更新背包UI
        if (GameUIManager.Instance?.bagView != null && GameUIManager.Instance.bagView.gameObject.activeSelf)
        {
            var itemDataMap = LoadDataManager.Instance?.GetItemDataMap();
            if (itemDataMap != null)
            {
                GameUIManager.Instance.bagView.UpdateBagItems(playerInventory, itemDataMap);
                Debug.Log("[PlayerDataManager] 已刷新背包UI");
            }
        }
    }

    private void CheckAndUpdateAnimationState()
    {
        if (NetServerManager.Instance == null)
        {
            Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - NetServerManager 为空，跳过");
            return;
        }

        if (PlayerAniManager.Instance == null)
        {
            Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - PlayerAniManager 不存在，跳过动画更新（当前场景可能不是 GameScene）");
            return;
        }

        try
        {
            if (PlayerAniManager.Instance.CurrentPlayerState == PlayerAniManager.PlayerAnimState.Reel)
            {
                Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - 收杆动画已结束，准备切换到目标动画");
            }
        }
        catch
        {
            Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - PlayerAniManager 状态访问失败，跳过动画更新");
            return;
        }

        if (NetServerManager.Instance.IsPlayingReelAnimation)
        {
            Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - 正在播放收杆动画，保持当前动画");
            return;
        }

        if (PlayerAniManager.Instance.CurrentPlayerState == PlayerAniManager.PlayerAnimState.Reel)
        {
            Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - 收杆动画已结束，准备切换到目标动画");
        }

        bool isFull = IsFishBagFull();

        if (isFull)
        {
            Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - 鱼篓已满，切换到懒动画");
            NetServerManager.Instance.NotifyPlayLazyAnimation();
        }
        else
        {
            Debug.Log("[PlayerDataManager] CheckAndUpdateAnimationState - 鱼篓未满，切换到空闲动画");
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
        if (fishDetailData != null && fishDetailData.Count > 0)
        {
            var result = new Dictionary<int, int>();
            foreach (var kvp in fishDetailData)
            {
                result[kvp.Key] = kvp.Value.Count;
            }
            return result;
        }
        return fishInventory != null ? new Dictionary<int, int>(fishInventory) : new Dictionary<int, int>();
    }

    public Dictionary<int, List<FishDetailData>> GetFishDetailData()
    {
        return fishDetailData != null ? new Dictionary<int, List<FishDetailData>>(fishDetailData) : new Dictionary<int, List<FishDetailData>>();
    }

    public void UpdateFishDetailData(Dictionary<int, List<FishDetailData>> newDetailData)
    {
        fishDetailData = newDetailData ?? new Dictionary<int, List<FishDetailData>>();
    }

    public List<FishDetailData> GetFishDetailDataById(int fishId)
    {
        if (fishDetailData != null && fishDetailData.ContainsKey(fishId))
        {
            return fishDetailData[fishId];
        }
        return new List<FishDetailData>();
    }

    public void RefreshUI()
    {
        if (GameUIManager.Instance != null)
        {
            if (GameUIManager.Instance.bagView != null)
            {
                GameUIManager.Instance.bagView.RefreshItems();
            }
            if (GameUIManager.Instance.fishBagView != null)
            {
                GameUIManager.Instance.fishBagView.RefreshItems();
            }
        }
    }

    public int FishBagCapacity => fishBagCapacity;

    // ✅ 正确：与服务器保持一致
    private int GetTotalFishCount()
    {
        int total = 0;
        if (fishInventory != null)
        {
            // fishInventory 已经按鱼ID聚合了数量
            foreach (var kvp in fishInventory)
            {
                total += kvp.Value;  // ✅ 累加 Quantity
            }
        }
        return total;
    }
    //public int GetTotalFishCount()
    //{
    //    int total = 0;
    //    if (fishDetailData != null && fishDetailData.Count > 0)
    //    {
    //        foreach (var kvp in fishDetailData)
    //        {
    //            total += kvp.Value.Count;
    //        }
    //    }
    //    else if (fishInventory != null)
    //    {
    //        foreach (var kvp in fishInventory)
    //        {
    //            total += kvp.Value;
    //        }
    //    }
    //    return total;
    //}

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

    private void OnSyncUnlockedCharacters(List<int> characterIds)
    {
        Debug.Log($"[PlayerDataManager] 收到人物解锁数据同步，共 {characterIds?.Count ?? 0} 个");
    }

    private void OnSyncUnlockedEquipment(List<int> equipmentIds)
    {
        Debug.Log($"[PlayerDataManager] 收到装备解锁数据同步，共 {equipmentIds?.Count ?? 0} 个");
    }

    private void OnMallDataChanged(Dictionary<int, MallItemData> mallItems)
    {
        Debug.Log($"[PlayerDataManager] 收到商城数据同步，共 {mallItems?.Count ?? 0} 个物品");
    }

    private void OnCharacterDataChanged((int, int, int) data)
    {
        Debug.Log($"[PlayerDataManager] 收到人物数据变化: 角色ID={data.Item1}, 等级={data.Item2}, 经验={data.Item3}");
    }

    private void OnBagDataUpdated(Dictionary<int, int> inventory)
    {
        Debug.Log($"[PlayerDataManager] 收到背包数据更新，共 {inventory?.Count ?? 0} 个物品");
        SyncInventoryFromServer();
    }

    private void OnFishBagDataUpdated()
    {
        Debug.Log("[PlayerDataManager] 收到鱼篓数据更新事件");
        SyncInventoryFromServer();
    }
    /// <summary>
    /// 处理UI数据更新请求
    /// </summary>
    private void OnRequestUpdateAllData()
    {
        Debug.Log("[PlayerDataManager] 收到UI数据更新请求");

        // 同步最新数据
        SyncInventoryFromServer();
        SyncGoldFromServer();

        // 数据同步完成后，通过GameUIManager更新UI
        UpdateAllUI();
    }

    /// <summary>
    /// 更新所有UI（通过GameUIManager）
    /// </summary>
    private void UpdateAllUI()
    {
        if (GameUIManager.Instance == null)
        {
            Debug.LogWarning("[PlayerDataManager] GameUIManager 不可用");
            return;
        }

        // 更新金币
        GameUIManager.Instance.UpdateGoldDisplay(gold);

        // 更新鱼篓数量
        int totalCount = GetTotalFishCount();
        GameUIManager.Instance.UpdateFishCountDisplay(totalCount, fishBagCapacity);

        // 更新窝料数量
        int baitCount = 0;
        if (playerInventory != null)
        {
            int currentScene = EnvManager.Instance?.currentSceneId ?? 1;
            var nestBaits = LoadDataManager.Instance.nestBaitDict.Values;
            var applicableBait = nestBaits.FirstOrDefault(n => n.applicableScene == currentScene);
            int baitId = applicableBait?.id ?? 2501;
            playerInventory.TryGetValue(baitId, out baitCount);
        }
        GameUIManager.Instance.UpdateBaitCountDisplay(baitCount);

        Debug.Log($"[PlayerDataManager] UI数据更新完成 - 金币:{gold}, 鱼篓:{totalCount}/{fishBagCapacity}, 窝料:{baitCount}");
    }

    private void OnDestroy()
    {
        if (NetServerManager.Instance != null && _hasSubscribedToNetServer)
        {
            NetServerManager.Instance.OnInitializationComplete -= OnNetServerInitialized;
            _hasSubscribedToNetServer = false;
            Debug.Log("[PlayerDataManager] 已取消订阅 NetServerManager 初始化完成事件");
        }
    }
}
