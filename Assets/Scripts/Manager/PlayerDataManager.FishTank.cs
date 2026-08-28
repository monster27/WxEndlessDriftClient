// ============================================================
// 文件: PlayerDataManager.FishTank.cs
// 说明: 鱼缸系统数据管理 - 鱼缸专用数据（鱼篓数据统一使用主文件的 fishDetailData）
// 路径: Assets/Scripts/Manager/
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static NetServerManager;

public partial class PlayerDataManager
{
    // ============================================================
    // 消息枚举定义
    // ============================================================

    public enum FishTankMessage
    {
        // View → Service
        OpenFishTank,           // 打开鱼缸界面
        CloseFishTank,          // 关闭鱼缸界面
        RefreshFishTank,        // 刷新鱼缸数据
        SwitchTank,             // 切换鱼缸 (参数: int newIndex)
        TransferFish,           // 转移鱼 (参数: TransferData)
        UnlockTank,             // 解锁鱼缸 (参数: int tankId)
        ToggleManagerPanel,     // 切换管理面板
        PlayerDataUpdated,

        // Network → Service
        DataLoaded,             // 网络数据加载完成

        // Service → View
        DataUpdated,            // 数据已更新，请刷新UI
    }

    // ============================================================
    // 数据类型定义
    // ============================================================

    /// <summary>
    /// 鱼缸展示数据（UI展示用）
    /// </summary>
    [Serializable]
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

    /// <summary>
    /// 鱼篓展示数据（UI展示用）
    /// </summary>
    [Serializable]
    public class FishBagDisplayData
    {
        public List<FishDetailData> FishList = new List<FishDetailData>();
        public int Capacity;
        public int CurrentCount;
        public int Remaining;
        public bool IsFull;
    }

    /// <summary>
    /// 存储面板数据（UI展示用）
    /// </summary>
    [Serializable]
    public class FishTankStoreData
    {
        public int TankId;
        public string Name;
        public bool IsBag;
        public bool IsSpecial;
        public int PurchaseCost;
        public int MaxCapacity;
        public List<FishDetailData> FishList = new List<FishDetailData>();
        public bool IsUnlocked = true;
    }

    /// <summary>
    /// 鱼转移数据（传递参数）
    /// </summary>
    [Serializable]
    public class TransferData
    {
        public FishDetailData FishData;
        public int FromIndex;
        public int ToIndex;
        public bool IsFromBag;
        public bool IsToBag;
    }

    // ============================================================
    // 私有数据（鱼缸专用，鱼篓数据使用主文件的 fishDetailData）
    // ============================================================

    /// <summary>
    /// 鱼缸数据 - Key: TankId, Value: List of FishDetailData
    /// </summary>
    private Dictionary<int, List<FishDetailData>> _fishTankData = new Dictionary<int, List<FishDetailData>>();

    /// <summary>
    /// 鱼缸状态信息 - Key: TankId, Value: FishTankStatusData
    /// </summary>
    private Dictionary<int, FishTankStatusData> _fishTankStatus = new Dictionary<int, FishTankStatusData>();

    /// <summary>
    /// 是否已加载数据
    /// </summary>
    private bool _isFishDataLoaded = false;

    // ============================================================
    // 公开属性
    // ============================================================

    public bool IsFishDataLoaded => _isFishDataLoaded;

    // ============================================================
    // 鱼篓数据查询方法（统一使用主文件的 fishDetailData）
    // ============================================================

    /// <summary>
    /// 获取鱼篓中的所有鱼（从主文件的 fishDetailData 中筛选 location == 0）
    /// </summary>
    public List<FishDetailData> GetFishBagList()
    {
        var result = new List<FishDetailData>();

        if (fishDetailData != null)
        {
            foreach (var kvp in fishDetailData)
            {
                foreach (var fish in kvp.Value)
                {
                    if (fish != null && fish.location == 0)
                    {
                        result.Add(fish);
                    }
                }
            }
        }
        return result;
    }

    // 在 PlayerDataManager.FishTank.cs 中添加
    public int GetFishBagCapacity()
    {
        return fishBagCapacity;
    }
    /// <summary>
    /// 获取鱼篓总数量
    /// </summary>
    public int GetFishBagCount()
    {
        return GetFishBagList().Count;
    }

    /// <summary>
    /// 获取鱼篓剩余空间
    /// </summary>
    public int GetFishBagRemaining()
    {
        return fishBagCapacity - GetFishBagCount();
    }

    /// <summary>
    /// 添加鱼到鱼篓（添加到主文件的 fishDetailData）
    /// </summary>
    public void AddFishToBag(FishDetailData fish)
    {
        if (fish == null) return;

        if (fishDetailData == null)
        {
            fishDetailData = new Dictionary<int, List<FishDetailData>>();
        }

        fish.location = 0;
        fish.tankId = 0;

        if (!fishDetailData.ContainsKey(fish.fishId))
        {
            fishDetailData[fish.fishId] = new List<FishDetailData>();
        }

        fishDetailData[fish.fishId].Add(fish);
        NotifyDataChanged();
    }

    /// <summary>
    /// 从鱼篓移除指定鱼（从主文件的 fishDetailData 中移除）
    /// </summary>
    public bool RemoveFishFromBag(int fishItemId)
    {
        if (fishDetailData == null) return false;

        foreach (var kvp in fishDetailData)
        {
            int fishId = kvp.Key;
            var list = kvp.Value;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null && list[i].id == fishItemId)
                {
                    list.RemoveAt(i);

                    if (list.Count == 0)
                    {
                        fishDetailData.Remove(fishId);
                    }

                    NotifyDataChanged();
                    return true;
                }
            }
        }

        return false;
    }

    // ============================================================
    // 鱼缸数据查询方法
    // ============================================================

    /// <summary>
    /// 获取指定鱼缸中的鱼列表
    /// </summary>
    public List<FishDetailData> GetFishTankItems(int tankId)
    {
        if (_fishTankData.TryGetValue(tankId, out var list))
        {
            var copy = list.ToList();
            return copy;
        }
        Z_Logger.Log($"[PlayerDataManager] GetFishTankItems: tankId={tankId} 未找到数据，返回空列表");
        return new List<FishDetailData>();
    }

    /// <summary>
    /// 获取指定鱼缸的鱼数量
    /// </summary>
    public int GetFishTankCount(int tankId)
    {
        if (_fishTankData.TryGetValue(tankId, out var list))
            return list.Count;
        return 0;
    }

    /// <summary>
    /// 获取指定鱼缸状态
    /// </summary>
    public FishTankStatusData GetFishTankStatus(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status;
        return null;
    }

    /// <summary>
    /// 获取所有鱼缸状态列表
    /// </summary>
    public List<FishTankStatusData> GetAllFishTankStatus()
    {
        return _fishTankStatus.Values.ToList();
    }

    /// <summary>
    /// 获取所有鱼缸状态（按TankId排序）
    /// </summary>
    public List<FishTankStatusData> GetAllFishTankStatusOrdered()
    {
        return _fishTankStatus.Values.OrderBy(s => s.tankId).ToList();
    }

    /// <summary>
    /// 检查鱼缸是否已解锁
    /// </summary>
    public bool IsFishTankUnlocked(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status.isUnlocked;
        return false;
    }

    /// <summary>
    /// 获取鱼缸容量
    /// </summary>
    public int GetFishTankCapacity(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status.capacity;
        return 10;
    }

    /// <summary>
    /// 获取鱼缸剩余空间
    /// </summary>
    public int GetFishTankRemaining(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status.remainingSpace;
        return 0;
    }

    /// <summary>
    /// 根据 FishItemId 查找所在的鱼缸ID
    /// </summary>
    public int FindTankIdByFishItemId(int fishItemId)
    {
        foreach (var kvp in _fishTankData)
        {
            if (kvp.Value.Any(f => f.id == fishItemId))
                return kvp.Key;
        }
        return -1;
    }

    /// <summary>
    /// 根据 FishItemId 获取鱼数据（先查鱼篓，再查鱼缸）
    /// </summary>
    public FishDetailData FindFishByItemId(int fishItemId)
    {
        // 先从鱼篓中查找（主文件的 fishDetailData）
        if (fishDetailData != null)
        {
            foreach (var kvp in fishDetailData)
            {
                foreach (var fish in kvp.Value)
                {
                    if (fish != null && fish.id == fishItemId)
                        return fish;
                }
            }
        }

        // 再从鱼缸中查找
        foreach (var kvp in _fishTankData)
        {
            var found = kvp.Value.FirstOrDefault(f => f.id == fishItemId);
            if (found != null)
                return found;
        }

        return null;
    }

    // ============================================================
    // 数据更新方法（由 NetServerManager 调用）
    // ============================================================

    /// <summary>
    /// 从服务器响应更新鱼篓数据（存入主文件的 fishDetailData）
    /// </summary>
    public void UpdateFishBagFromResponse(List<FishDetailData> fishList, int capacity)
    {
        if (fishDetailData == null)
        {
            fishDetailData = new Dictionary<int, List<FishDetailData>>();
        }

        // 清空旧的鱼篓数据（只删除 location == 0 的鱼）
        var keysToRemove = new List<int>();
        foreach (var kvp in fishDetailData)
        {
            if (kvp.Value.All(f => f.location == 0))
            {
                keysToRemove.Add(kvp.Key);
            }
        }
        foreach (var key in keysToRemove)
        {
            fishDetailData.Remove(key);
        }

        // 添加新数据
        if (fishList != null)
        {
            foreach (var fish in fishList)
            {
                if (fish == null) continue;

                fish.location = 0;
                fish.tankId = 0;

                if (!fishDetailData.ContainsKey(fish.fishId))
                {
                    fishDetailData[fish.fishId] = new List<FishDetailData>();
                }
                fishDetailData[fish.fishId].Add(fish);
            }
        }

        if (capacity > 0)
            fishBagCapacity = capacity;

        Z_Logger.Log($"[PlayerDataManager] 鱼篓数据更新: {GetFishBagCount()} 条鱼, 容量 {fishBagCapacity}");
        NotifyDataChanged();
    }

    /// <summary>
    /// 从服务器响应更新鱼缸数据（全量替换）
    /// </summary>
    public void UpdateFishTankFromResponse(List<FishTankStatusResponse> tankStatusList)
    {
        _fishTankData.Clear();
        _fishTankStatus.Clear();

        if (tankStatusList != null)
        {
            foreach (var tank in tankStatusList)
            {
                if (tank == null) continue;

                _fishTankStatus[tank.tankId] = new FishTankStatusData
                {
                    tankId = tank.tankId,
                    isUnlocked = tank.isUnlocked,
                    level = tank.level,
                    capacity = tank.capacity,
                    currentCount = tank.currentCount,
                    remainingSpace = tank.remainingSpace,
                    items = tank.items ?? new List<FishDetailData>()
                };

                _fishTankData[tank.tankId] = tank.items ?? new List<FishDetailData>();
            }
        }

        _isFishDataLoaded = true;
        Z_Logger.Log($"[PlayerDataManager] 鱼缸数据更新: {_fishTankStatus.Count} 个鱼缸");
        NotifyDataChanged();
    }

    /// <summary>
    /// 更新单个鱼缸状态
    /// </summary>
    public void UpdateSingleFishTankFromResponse(FishTankStatusResponse tank)
    {
        if (tank == null) return;

        Z_Logger.Log($"[PlayerDataManager] UpdateSingleFishTankFromResponse 开始: tankId={tank.tankId}, items.Count={tank.items?.Count ?? 0}");

        if (!_fishTankStatus.ContainsKey(tank.tankId))
            _fishTankStatus[tank.tankId] = new FishTankStatusData();

        _fishTankStatus[tank.tankId].tankId = tank.tankId;
        _fishTankStatus[tank.tankId].isUnlocked = tank.isUnlocked;
        _fishTankStatus[tank.tankId].level = tank.level;
        _fishTankStatus[tank.tankId].capacity = tank.capacity;
        _fishTankStatus[tank.tankId].currentCount = tank.currentCount;
        _fishTankStatus[tank.tankId].remainingSpace = tank.remainingSpace;

        // ✅ 复制一份，避免外部修改影响内部数据
        _fishTankData[tank.tankId] = tank.items != null ? new List<FishDetailData>(tank.items) : new List<FishDetailData>();

        _isFishDataLoaded = true;

        // 打印更新后的鱼ID列表
        var ids = string.Join(",", _fishTankData[tank.tankId].Select(f => f.id));
        Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tank.tankId} 更新后鱼ID列表: {ids}");

        Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tank.tankId} 状态已更新");
        NotifyDataChanged();
    }

    // ============================================================
    // 数据操作辅助方法
    // ============================================================

    /// <summary>
    /// 从鱼缸移除指定鱼
    /// </summary>
    public bool RemoveFishFromTank(int tankId, int fishItemId)
    {
        if (_fishTankData.TryGetValue(tankId, out var list))
        {
            int removed = list.RemoveAll(f => f.id == fishItemId);
            if (removed > 0)
            {
                UpdateTankStatusCount(tankId);
                NotifyDataChanged();
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 添加鱼到鱼缸
    /// </summary>
    public void AddFishToTank(int tankId, FishDetailData fish)
    {
        if (fish == null) return;

        if (!_fishTankData.ContainsKey(tankId))
            _fishTankData[tankId] = new List<FishDetailData>();

        fish.location = 1;
        fish.tankId = tankId;

        _fishTankData[tankId].Add(fish);
        UpdateTankStatusCount(tankId);
        NotifyDataChanged();
    }

    /// <summary>
    /// 从鱼缸转移到鱼篓
    /// </summary>
    public bool TransferFishFromTankToBag(int tankId, int fishItemId)
    {
        var fish = FindFishByItemId(fishItemId);
        if (fish == null) return false;

        bool removed = RemoveFishFromTank(tankId, fishItemId);
        if (removed)
        {
            AddFishToBag(fish);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 从鱼篓转移到鱼缸
    /// </summary>
    public bool TransferFishFromBagToTank(int tankId, int fishItemId)
    {
        var fish = FindFishByItemId(fishItemId);
        if (fish == null) return false;

        bool removed = RemoveFishFromBag(fishItemId);
        if (removed)
        {
            AddFishToTank(tankId, fish);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 从鱼缸转移到鱼缸
    /// </summary>
    public bool TransferFishFromTankToTank(int fromTankId, int toTankId, int fishItemId)
    {
        var fish = FindFishByItemId(fishItemId);
        if (fish == null) return false;

        bool removed = RemoveFishFromTank(fromTankId, fishItemId);
        if (removed)
        {
            AddFishToTank(toTankId, fish);
            return true;
        }
        return false;
    }

    // ============================================================
    // 私有辅助方法
    // ============================================================

    private void UpdateTankStatusCount(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
        {
            var list = _fishTankData.TryGetValue(tankId, out var fishList) ? fishList : new List<FishDetailData>();
            status.currentCount = list.Count;
            status.remainingSpace = status.capacity - list.Count;
        }
    }

    // ============================================================
    // 数据变更通知
    // ============================================================

    private void NotifyDataChanged()
    {
        CommunicateEvent.Modify(FishTankMessage.PlayerDataUpdated.ToString());
    }

    // ============================================================
    // 初始化和清理
    // ============================================================

    public void InitFishTankData()
    {
        _fishTankData.Clear();
        _fishTankStatus.Clear();
        _isFishDataLoaded = false;
        Z_Logger.Log("[PlayerDataManager] 鱼缸数据已初始化");
    }

    public void ClearFishTankData()
    {
        _fishTankData.Clear();
        _fishTankStatus.Clear();
        _isFishDataLoaded = false;
        Z_Logger.Log("[PlayerDataManager] 鱼缸数据已清空");
    }

    // ============================================================
    // 调试
    // ============================================================

    public void DebugPrintFishData()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 鱼缸数据 Debug =====");
        sb.AppendLine($"数据已加载: {_isFishDataLoaded}");
        sb.AppendLine($"鱼篓: {GetFishBagCount()}/{fishBagCapacity}");

        foreach (var kvp in _fishTankStatus.OrderBy(k => k.Key))
        {
            var status = kvp.Value;
            var fishList = GetFishTankItems(kvp.Key);
            sb.AppendLine($"鱼缸 {kvp.Key}: {fishList.Count}/{status.capacity} 解锁={status.isUnlocked}");
        }
        Z_Logger.Log(sb.ToString());
    }
}

// ============================================================
// 数据类定义（从NetServerManager迁移）
// ============================================================

/// <summary>
/// 鱼缸状态响应（从服务器接收）
/// </summary>
[Serializable]
public class FishTankStatusResponse
{
    public bool success;
    public int tankId;
    public string Name;
    public string Type;
    public int PurchaseCost;
    public bool isUnlocked;
    public int level;
    public int capacity;
    public int currentCount;
    public int remainingSpace;
    public List<FishDetailData> items = new List<FishDetailData>();
}

/// <summary>
/// 鱼缸状态数据（存储用）
/// </summary>
[Serializable]
public class FishTankStatusData
{
    public int tankId;
    public bool isUnlocked;
    public int level;
    public int capacity;
    public int currentCount;
    public int remainingSpace;
    public List<FishDetailData> items;
}

/// <summary>
/// 鱼缸配置（从 LoadDataManager 获取）
/// </summary>
[Serializable]
public class FishTankConfig
{
    public int id;
    public string name;
    public string type;
    public int purchaseCost;
    public int defaultCapacity;
}
