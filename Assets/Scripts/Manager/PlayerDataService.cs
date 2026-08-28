// ============================================================
// 文件: PlayerDataService.cs
// 说明: 玩家数据业务协调层 - 防抖、缓存、分发
// 路径: Assets/Scripts/Manager/
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static NetServerManager;
using static PlayerDataManager;

public class PlayerDataService : SingletonMono<PlayerDataService>
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 1. _isReady
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private bool _isReady = false;

    /// <summary>
    /// 管理器是否已就绪（可以安全调用同步方法）
    /// </summary>
    public bool IsReady => _isReady;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 2. 配置
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 防抖设置 =====")]
    [SerializeField] private float debounceDelay = 0.1f;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 3. 缓存数据
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private int _cachedBagHash = 0;
    private int _cachedBagCapacity = 10;
    private Dictionary<int, int> _cachedTankHashes = new Dictionary<int, int>();

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 4. 防抖
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private Coroutine _debounceCoroutine;
    private bool _isProcessing = false;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 5. 生命周期
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void Init()
    {
        RegisterEvents();
        LogDebug("PlayerDataService 启动完成");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        UnregisterEvents();
        if (_debounceCoroutine != null)
        {
            StopCoroutine(_debounceCoroutine);
            _debounceCoroutine = null;
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 6. 事件注册
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void RegisterEvents()
    {
        UnregisterEvents();

        // 监听DataManager的数据变化
        CommunicateEvent.Register(FishTankMessage.PlayerDataUpdated.ToString(), OnPlayerDataUpdated);

        // 监听View的消息（无参数）
        CommunicateEvent.Register(FishTankMessage.OpenFishTank.ToString(), OnOpenFishTank);
        CommunicateEvent.Register(FishTankMessage.CloseFishTank.ToString(), OnCloseFishTank);
        CommunicateEvent.Register(FishTankMessage.RefreshFishTank.ToString(), OnRefreshFishTank);
        CommunicateEvent.Register(FishTankMessage.SwitchTank.ToString(), OnSwitchTank);
        CommunicateEvent.Register<int>(FishTankMessage.UnlockTank.ToString(), OnUnlockTank);
        CommunicateEvent.Register(FishTankMessage.ToggleManagerPanel.ToString(), OnToggleManagerPanel);

        // 监听Network层数据加载完成
        CommunicateEvent.Register(FishTankMessage.DataLoaded.ToString(), OnDataLoaded);

        // ✅ 带参数的 TransferFish
        CommunicateEvent.Register<TransferData>(FishTankMessage.TransferFish.ToString(), OnTransferFish);

        LogDebug("事件注册完成");
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister(FishTankMessage.PlayerDataUpdated.ToString(), OnPlayerDataUpdated);
        CommunicateEvent.Unregister(FishTankMessage.OpenFishTank.ToString(), OnOpenFishTank);
        CommunicateEvent.Unregister(FishTankMessage.CloseFishTank.ToString(), OnCloseFishTank);
        CommunicateEvent.Unregister(FishTankMessage.RefreshFishTank.ToString(), OnRefreshFishTank);
        CommunicateEvent.Unregister(FishTankMessage.SwitchTank.ToString(), OnSwitchTank);
        // 注意：TransferFish 使用泛型注册，取消注册也要用泛型
        CommunicateEvent.Unregister<TransferData>(FishTankMessage.TransferFish.ToString(), OnTransferFish);
        CommunicateEvent.Unregister<int>(FishTankMessage.UnlockTank.ToString(), OnUnlockTank);
        CommunicateEvent.Unregister(FishTankMessage.ToggleManagerPanel.ToString(), OnToggleManagerPanel);
        CommunicateEvent.Unregister(FishTankMessage.DataLoaded.ToString(), OnDataLoaded);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 7. 消息处理器
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnOpenFishTank()
    {
        LogDebug("收到 OpenFishTank 消息");
        if (IsDataReady())
        {
            NotifyView(FishTankMessage.DataUpdated);
        }
        else
        {
            LogDebug("数据尚未加载，等待 DataLoaded");
        }
    }

    private void OnCloseFishTank()
    {
        LogDebug("收到 CloseFishTank 消息");
        // 不需要额外处理
    }

    private void OnRefreshFishTank()
    {
        LogDebug("收到 RefreshFishTank 消息");
        if (IsDataReady())
        {
            NotifyView(FishTankMessage.DataUpdated);
        }
    }

    private void OnSwitchTank()
    {
        LogDebug("收到 SwitchTank 消息");
        // View已经更新了索引，只需要通知刷新
        if (IsDataReady())
        {
            NotifyView(FishTankMessage.DataUpdated);
        }
    }

    private void OnTransferFish(TransferData transferData)
    {
        LogDebug("收到 TransferFish 消息");

        if (transferData == null || transferData.FishData == null)
        {
            LogDebug("TransferFish: 参数无效");
            return;
        }

        LogDebug($"TransferFish: FromIndex={transferData.FromIndex}, ToIndex={transferData.ToIndex}, FishId={transferData.FishData.id}");

        // 根据转移类型调用网络请求
        if (transferData.IsFromBag && !transferData.IsToBag)
        {
            int tankId = transferData.ToIndex - 1;
            if (tankId < 0) { LogDebug("无效的目标鱼缸索引"); return; }
            NetServerManager.Instance?.MoveFishFromBagToTank(tankId, transferData.FishData.id, (success, message) =>
            {
                if (success)
                    LogDebug("鱼篓→鱼缸转移成功");
                else
                    LogDebug($"鱼篓→鱼缸转移失败: {message}");
            });
        }
        else if (!transferData.IsFromBag && transferData.IsToBag)
        {
            NetServerManager.Instance?.MoveFishFromTankToBag(transferData.FishData.id, (success, message) =>
            {
                if (success)
                    LogDebug("鱼缸→鱼篓转移成功");
                else
                    LogDebug($"鱼缸→鱼篓转移失败: {message}");
            });
        }
        else if (!transferData.IsFromBag && !transferData.IsToBag)
        {
            int fromTankId = transferData.FromIndex - 1;
            int toTankId = transferData.ToIndex - 1;
            if (fromTankId < 0 || toTankId < 0) { LogDebug("无效的鱼缸索引"); return; }
            NetServerManager.Instance?.MoveFishFromTankToTank(fromTankId, toTankId, transferData.FishData.id, (success, message) =>
            {
                if (success)
                    LogDebug("鱼缸→鱼缸转移成功");
                else
                    LogDebug($"鱼缸→鱼缸转移失败: {message}");
            });
        }
        else
        {
            LogDebug("未知的转移类型");
        }
    }

    private void OnUnlockTank(int tankId)
    {
        LogDebug($"收到 UnlockTank 消息，tankId={tankId}");

        // 调用 NetServerManager 的解锁请求（会触发网络请求并自动刷新数据）
        NetServerManager.Instance?.OnUnlockFishTankRequest(tankId);
    }

    private void OnToggleManagerPanel()
    {
        LogDebug("收到 ToggleManagerPanel 消息");
        // View自己处理面板切换
    }

    private void OnDataLoaded()
    {
        LogDebug("收到 DataLoaded 消息，数据已就绪");
        NotifyView(FishTankMessage.DataUpdated);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 8. DataManager数据变化处理（防抖）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnPlayerDataUpdated()
    {
        if (_isProcessing) return;

        if (_debounceCoroutine != null)
        {
            StopCoroutine(_debounceCoroutine);
            _debounceCoroutine = null;
        }
        _debounceCoroutine = StartCoroutine(DebounceProcess());
        LogDebug("数据更新事件已接收，启动防抖");
    }

    private IEnumerator DebounceProcess()
    {
        yield return new WaitForSecondsRealtime(debounceDelay);
        _debounceCoroutine = null;

        _isProcessing = true;
        try
        {
            ProcessDataChanges();
        }
        finally
        {
            _isProcessing = false;
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 9. 数据变化检测
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void ProcessDataChanges()
    {
        if (PlayerDataManager.Instance == null)
        {
            LogDebug("PlayerDataManager 未初始化");
            return;
        }

        bool bagChanged = CheckBagChanges();
        bool tankChanged = CheckTankChanges();

        if (bagChanged || tankChanged)
        {
            LogDebug($"数据发生变化: bagChanged={bagChanged}, tankChanged={tankChanged}");
            NotifyView(FishTankMessage.DataUpdated);
        }
        else
        {
            LogDebug("数据无变化");
        }
    }

    private bool CheckBagChanges()
    {
        var bagList = GetBagFishList();
        int newCapacity = PlayerDataManager.Instance.fishBagCapacity;
        int newHash = CalculateFishListHash(bagList);

        bool changed = (newHash != _cachedBagHash) || (newCapacity != _cachedBagCapacity);

        if (changed)
        {
            _cachedBagHash = newHash;
            _cachedBagCapacity = newCapacity;
        }

        return changed;
    }

    private bool CheckTankChanges()
    {
        var tanks = PlayerDataManager.Instance.GetAllFishTankStatusOrdered();
        bool changed = false;

        var currentTankIds = new HashSet<int>(tanks.Select(t => t.tankId));
        var cachedTankIds = new HashSet<int>(_cachedTankHashes.Keys);

        foreach (int tankId in cachedTankIds)
        {
            if (!currentTankIds.Contains(tankId))
            {
                _cachedTankHashes.Remove(tankId);
                changed = true;
            }
        }

        foreach (var tank in tanks)
        {
            var fishList = GetTankFishList(tank.tankId);
            int newHash = CalculateFishListHash(fishList);

            if (!_cachedTankHashes.TryGetValue(tank.tankId, out int oldHash) || oldHash != newHash)
            {
                _cachedTankHashes[tank.tankId] = newHash;
                changed = true;
            }
        }

        return changed;
    }

    private int CalculateFishListHash(List<FishDetailData> list)
    {
        if (list == null || list.Count == 0) return 0;
        int hash = 0;
        foreach (var fish in list)
        {
            if (fish != null)
                hash ^= fish.id.GetHashCode();
        }
        return hash;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 10. 对外查询接口（供View调用）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public List<FishTankStatusData> GetTankList()
    {
        if (PlayerDataManager.Instance == null)
            return new List<FishTankStatusData>();
        return PlayerDataManager.Instance.GetAllFishTankStatusOrdered();
    }

    public FishTankStatusData GetTankStatus(int tankId)
    {
        if (PlayerDataManager.Instance == null)
            return null;
        return PlayerDataManager.Instance.GetFishTankStatus(tankId);
    }

    public List<FishDetailData> GetTankFishList(int tankId)
    {
        if (PlayerDataManager.Instance == null)
            return new List<FishDetailData>();
        var list = PlayerDataManager.Instance.GetFishTankItems(tankId);
        return list;
    }

    public List<FishDetailData> GetBagFishList()
    {
        if (PlayerDataManager.Instance == null)
        {
            LogDebug("GetBagFishList: PlayerDataManager.Instance == null");
            return new List<FishDetailData>();
        }

        var result = PlayerDataManager.Instance.GetFishBagList();
        LogDebug($"GetBagFishList: result.Count={result?.Count ?? 0}");
        return result ?? new List<FishDetailData>();
    }

    public int GetBagCapacity()
    {
        if (PlayerDataManager.Instance == null)
            return 10;
        return PlayerDataManager.Instance.fishBagCapacity;
    }

    public int GetBagRemaining()
    {
        if (PlayerDataManager.Instance == null)
            return 0;
        return PlayerDataManager.Instance.GetFishBagRemaining();
    }

    public bool IsBagFull()
    {
        return GetBagRemaining() <= 0;
    }

    public bool IsTankUnlocked(int tankId)
    {
        if (PlayerDataManager.Instance == null)
            return false;
        return PlayerDataManager.Instance.IsFishTankUnlocked(tankId);
    }

    public int GetTankCapacity(int tankId)
    {
        if (PlayerDataManager.Instance == null)
            return 10;
        return PlayerDataManager.Instance.GetFishTankCapacity(tankId);
    }

    public int GetTankRemaining(int tankId)
    {
        if (PlayerDataManager.Instance == null)
            return 0;
        return PlayerDataManager.Instance.GetFishTankRemaining(tankId);
    }

    public bool CanAddFishToTank(int tankId)
    {
        return GetTankRemaining(tankId) > 0 && IsTankUnlocked(tankId);
    }

    public bool IsDataReady()
    {
        if (PlayerDataManager.Instance == null)
            return false;
        return PlayerDataManager.Instance.IsFishDataLoaded;
    }

    public int GetTankCount()
    {
        return GetTankList().Count;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 11. UI展示数据接口
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public FishTankDisplayData GetTankDisplayData(int tankIndex)
    {
        var tanks = GetTankList();
        if (tankIndex < 0 || tankIndex >= tanks.Count)
            return null;

        var tank = tanks[tankIndex];
        var fishList = GetTankFishList(tank.tankId);
        var config = LoadDataManager.Instance?.GetFishTankConfig(tank.tankId);

        return new FishTankDisplayData
        {
            TankId = tank.tankId,
            Name = config?.name ?? $"鱼缸{tank.tankId}",
            IsUnlocked = tank.isUnlocked,
            Capacity = tank.capacity,
            CurrentCount = fishList.Count,
            FishList = fishList,
            IsSpecial = config?.type == "special",
            PurchaseCost = config?.purchaseCost ?? 0,
            HourlyEarning = (config?.type == "special" && tank.isUnlocked) ? fishList.Count * 10 : 0
        };
    }

    public FishBagDisplayData GetBagDisplayData()
    {
        var fishList = GetBagFishList();
        int capacity = GetBagCapacity();

        LogDebug($"GetBagDisplayData: fishList.Count={fishList?.Count ?? 0}, capacity={capacity}");

        return new FishBagDisplayData
        {
            FishList = fishList ?? new List<FishDetailData>(),
            Capacity = capacity,
            CurrentCount = fishList?.Count ?? 0,
            Remaining = capacity - (fishList?.Count ?? 0),
            IsFull = (fishList?.Count ?? 0) >= capacity
        };
    }

    public FishTankStoreData GetStoreData(int index)
    {
        Z_Logger.Log($"[PlayerDataService] GetStoreData 被调用: index={index}");

        // 防御：检查 PlayerDataManager 是否就绪
        if (PlayerDataManager.Instance == null)
        {
            Z_Logger.LogError("[PlayerDataService] GetStoreData 失败: PlayerDataManager.Instance 为 null");
            return new FishTankStoreData
            {
                IsBag = (index == 0),
                Name = "加载中...",
                FishList = new List<FishDetailData>(),
                MaxCapacity = 10,
                IsUnlocked = false
            };
        }

        // 鱼篓 (index == 0)
        if (index == 0)
        {
            var bagList = PlayerDataManager.Instance.GetFishBagList();
            int capacity = PlayerDataManager.Instance.GetFishBagCapacity();
            var data = new FishTankStoreData
            {
                IsBag = true,
                Name = "鱼篓",
                FishList = new List<FishDetailData>(bagList),
                MaxCapacity = capacity,
                IsUnlocked = true
            };
            return data;
        }

        // 鱼缸 (index >= 1)
        int tankId = index;
        var status = PlayerDataManager.Instance.GetFishTankStatus(tankId);
        if (status == null)
        {
            Z_Logger.LogError($"[PlayerDataService] GetStoreData 失败: 鱼缸 {tankId} 的状态为 null");
            return new FishTankStoreData
            {
                IsBag = false,
                TankId = tankId,
                Name = $"鱼缸{tankId}",
                FishList = new List<FishDetailData>(),
                MaxCapacity = 10,
                IsUnlocked = false
            };
        }

        string name = $"鱼缸{tankId}";
        if (LoadDataManager.Instance != null)
        {
            var config = LoadDataManager.Instance.GetFishTankConfig(tankId);
            if (config != null) name = config.name;
        }

        var tankList = PlayerDataManager.Instance.GetFishTankItems(tankId);
        var dataTank = new FishTankStoreData
        {
            IsBag = false,
            TankId = tankId,
            Name = name,
            FishList = new List<FishDetailData>(tankList),
            MaxCapacity = status.capacity,
            IsUnlocked = status.isUnlocked
        };
        return dataTank;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 12. 通知View
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void NotifyView(FishTankMessage message)
    {
        LogDebug($"通知View: {message}");
        CommunicateEvent.Modify(message.ToString());
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 13. 日志辅助
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void LogDebug(string message)
    {
        if (enableDebugLog)
            Z_Logger.Log($"[PlayerDataService] {message}");
    }

    private void LogInfo(string message)
    {
        Z_Logger.Log($"[PlayerDataService] {message}");
    }
}
