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

public class PlayerDataService : MonoBehaviour
{
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 1. 单例
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private static PlayerDataService _instance;
    public static PlayerDataService Instance => _instance;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 2. 配置
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 防抖设置 =====")]
    [SerializeField] private float debounceDelay = 0.1f;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 3. 缓存数据（用于比较）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    // 鱼篓缓存
    private int _cachedBagHash = 0;
    private int _cachedBagCapacity = 10;

    // 鱼缸缓存 - Key: TankId, Value: Hash
    private Dictionary<int, int> _cachedTankHashes = new Dictionary<int, int>();

    // 记录上次处理的版本号，防止重复处理
    private int _lastProcessedVersion = -1;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 4. 防抖
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private Coroutine _debounceCoroutine;
    private bool _isProcessing = false;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 5. 生命周期
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        RegisterEvents();
        LogDebug("PlayerDataService 启动完成");
    }

    private void OnDestroy()
    {
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

        CommunicateEvent.Register("PlayerDataUpdated", OnPlayerDataUpdated);
        CommunicateEvent.Register("FISH_TANK_OPEN", OnFishTankOpen);
        CommunicateEvent.Register("FISH_TANK_FORCE_REFRESH", OnForceRefresh);

        // ✅ 监听数据就绪事件，立即刷新缓存
        CommunicateEvent.Register("FishTankDataReady", OnFishTankDataReady);

        LogDebug("事件注册完成");
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister("PlayerDataUpdated", OnPlayerDataUpdated);
        CommunicateEvent.Unregister("FISH_TANK_OPEN", OnFishTankOpen);
        CommunicateEvent.Unregister("FISH_TANK_FORCE_REFRESH", OnForceRefresh);
        CommunicateEvent.Unregister("FishTankDataReady", OnFishTankDataReady);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 7. 事件处理器
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 数据层数据更新 - 防抖入口
    /// </summary>
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

    /// <summary>
    /// 鱼缸打开事件
    /// </summary>
    private void OnFishTankOpen()
    {
        LogDebug("鱼缸打开，触发数据刷新");
    }

    /// <summary>
    /// 强制刷新事件
    /// </summary>
    private void OnForceRefresh()
    {
        LogDebug("强制刷新");
        _lastProcessedVersion = -1;
        _cachedBagHash = 0;
        _cachedTankHashes.Clear();

        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.ForceNotifyDataChanged();
        }
    }

    /// <summary>
    /// ✅ 鱼缸数据就绪 - 立即刷新缓存
    /// </summary>
    private void OnFishTankDataReady()
    {
        LogDebug("鱼缸数据就绪，刷新缓存");

        // 重置缓存，强制重新读取
        _cachedTankHashes.Clear();
        _lastProcessedVersion = -1;

        // 立即处理数据变化
        ProcessDataChanges();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 8. 防抖处理
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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
    // 9. 核心：数据变化检测与分发
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void ProcessDataChanges()
    {
        if (PlayerDataManager.Instance == null)
        {
            LogDebug("PlayerDataManager 未初始化");
            return;
        }

        bool bagChanged = false;
        bool tankChanged = false;

        if (CheckBagChanges())
        {
            bagChanged = true;
            LogDebug("鱼篓数据发生变化");
        }

        var tankChanges = CheckTankChanges();
        if (tankChanges.Count > 0)
        {
            tankChanged = true;
            LogDebug($"鱼缸数据发生变化: {string.Join(", ", tankChanges)}");
        }

        _lastProcessedVersion = PlayerDataManager.Instance.FishDataVersion;

        if (bagChanged || tankChanged)
        {
            CommunicateEvent.Modify("FishTankDataChanged");
            LogDebug("分发: FishTankDataChanged");
        }
        else
        {
            LogDebug("数据无变化，不分发事件");
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 10. 数据比较方法
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

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

    private List<int> CheckTankChanges()
    {
        var changedTanks = new List<int>();
        var tanks = PlayerDataManager.Instance.GetAllFishTankStatusOrdered();

        var currentTankIds = new HashSet<int>(tanks.Select(t => t.tankId));
        var cachedTankIds = new HashSet<int>(_cachedTankHashes.Keys);

        foreach (int tankId in cachedTankIds)
        {
            if (!currentTankIds.Contains(tankId))
            {
                _cachedTankHashes.Remove(tankId);
                changedTanks.Add(tankId);
            }
        }

        foreach (var tank in tanks)
        {
            var fishList = GetTankFishList(tank.tankId);
            int newHash = CalculateFishListHash(fishList);

            if (!_cachedTankHashes.TryGetValue(tank.tankId, out int oldHash) || oldHash != newHash)
            {
                _cachedTankHashes[tank.tankId] = newHash;
                changedTanks.Add(tank.tankId);
            }
        }

        return changedTanks;
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
    // 11. 对外查询接口
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

        var allFish = PlayerDataManager.Instance.GetAllFishDetailData();

        if (enableDebugLog)
        {
            Z_Logger.Log($"[GetTankFishList] tankId={tankId}, allFish.Count={allFish.Count}");
            foreach (var fish in allFish)
            {
                Z_Logger.Log($"[GetTankFishList] fish.id={fish.id}, fishId={fish.fishId}, location={fish.location}, tankId={fish.tankId}");
            }
        }

        return allFish.Where(f => f.location == 1 && f.tankId == tankId).ToList();
    }

    public List<FishDetailData> GetBagFishList()
    {
        if (PlayerDataManager.Instance == null)
            return new List<FishDetailData>();

        var allFish = PlayerDataManager.Instance.GetAllFishDetailData();
        var result = new List<FishDetailData>();

        foreach (var fish in allFish)
        {
            if (fish.location == 0)
            {
                result.Add(fish);
            }
        }

        return result;
    }

    public int GetBagCapacity()
    {
        if (PlayerDataManager.Instance == null)
            return 10;
        return PlayerDataManager.Instance.fishBagCapacity;
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

    public bool CanAddFishToTank(int tankId)
    {
        return GetTankRemaining(tankId) > 0 && IsTankUnlocked(tankId);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 12. 业务协调接口
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
        return new FishBagDisplayData
        {
            FishList = fishList,
            Capacity = GetBagCapacity(),
            CurrentCount = fishList.Count,
            Remaining = GetBagCapacity() - fishList.Count,
            IsFull = fishList.Count >= GetBagCapacity()
        };
    }

    public int GetTankCount()
    {
        return GetTankList().Count;
    }

    public int GetSpecialTankHourlyEarning()
    {
        var tanks = GetTankList();
        foreach (var tank in tanks)
        {
            var config = LoadDataManager.Instance?.GetFishTankConfig(tank.tankId);
            if (config?.type == "special" && tank.isUnlocked)
            {
                var fishList = GetTankFishList(tank.tankId);
                return fishList.Count * 10;
            }
        }
        return 0;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 13. 调试
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void DebugPrintCache()
    {
        LogInfo("===== PlayerDataService 缓存状态 =====");
        LogInfo($"鱼篓 Hash: {_cachedBagHash}, 容量: {_cachedBagCapacity}");
        LogInfo($"已处理版本: {_lastProcessedVersion}");

        foreach (var kvp in _cachedTankHashes)
        {
            LogInfo($"鱼缸 {kvp.Key}: Hash={kvp.Value}");
        }

        if (PlayerDataManager.Instance != null)
        {
            PlayerDataManager.Instance.DebugPrintFishData();
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 14. 日志辅助
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

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
// 15. 数据类
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

public class FishTankDisplayData
{
    public int TankId;
    public string Name;
    public bool IsUnlocked;
    public int Capacity;
    public int CurrentCount;
    public List<FishDetailData> FishList = new List<FishDetailData>();
    public bool IsSpecial;
    public int PurchaseCost;
    public int HourlyEarning;
}

public class FishBagDisplayData
{
    public List<FishDetailData> FishList = new List<FishDetailData>();
    public int Capacity;
    public int CurrentCount;
    public int Remaining;
    public bool IsFull;
}
