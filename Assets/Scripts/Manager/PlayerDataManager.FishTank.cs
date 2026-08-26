// ============================================================
// 文件: PlayerDataManager.FishTank.cs
// 说明: 鱼缸系统数据管理 - 统一数据源（纯数据层）
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
    // 私有数据（唯一数据源）
    // ============================================================

    /// <summary>
    /// 鱼篓数据 - Key: FishItemId (唯一ID), Value: FishDetailData
    /// </summary>
    private Dictionary<int, FishDetailData> _fishBagData = new Dictionary<int, FishDetailData>();

    /// <summary>
    /// 鱼缸数据 - Key: TankId, Value: List of FishDetailData
    /// </summary>
    private Dictionary<int, List<FishDetailData>> _fishTankData = new Dictionary<int, List<FishDetailData>>();

    /// <summary>
    /// 鱼缸状态信息 - Key: TankId, Value: FishTankStatusData
    /// </summary>
    private Dictionary<int, FishTankStatusData> _fishTankStatus = new Dictionary<int, FishTankStatusData>();

    /// <summary>
    /// 数据版本号，每次数据变更时递增
    /// </summary>
    private int _fishDataVersion = 0;

    /// <summary>
    /// 上次通知的版本号（用于去重）
    /// </summary>
    private int _lastNotifiedVersion = -1;

    // ============================================================
    // 公开属性（只读）
    // ============================================================

    public int FishDataVersion => _fishDataVersion;

    /// <summary>
    /// 获取鱼篓总数量
    /// </summary>
    public int GetFishBagTotalCount()
    {
        return _fishBagData.Count;
    }

    /// <summary>
    /// 鱼篓剩余空间
    /// </summary>
    public int GetFishBagRemaining()
    {
        return fishBagCapacity - _fishBagData.Count;
    }

    // ============================================================
    // 公开查询方法（只读）
    // ============================================================

    /// <summary>
    /// 获取鱼篓中的所有鱼（平铺列表）
    /// </summary>
    public List<FishDetailData> GetFishBagList()
    {
        return _fishBagData.Values.ToList();
    }

    /// <summary>
    /// 获取鱼篓数据（按鱼ID分组）
    /// </summary>
    public Dictionary<int, List<FishDetailData>> GetFishBagGrouped()
    {
        var result = new Dictionary<int, List<FishDetailData>>();
        foreach (var fish in _fishBagData.Values)
        {
            if (!result.ContainsKey(fish.fishId))
                result[fish.fishId] = new List<FishDetailData>();
            result[fish.fishId].Add(fish);
        }
        return result;
    }

    /// <summary>
    /// 获取鱼篓数据（汇总数量）
    /// </summary>
    public Dictionary<int, int> GetFishBagSummary()
    {
        var result = new Dictionary<int, int>();
        foreach (var fish in _fishBagData.Values)
        {
            if (!result.ContainsKey(fish.fishId))
                result[fish.fishId] = 0;
            result[fish.fishId]++;
        }
        return result;
    }

    /// <summary>
    /// 获取指定鱼缸中的鱼列表
    /// </summary>
    public List<FishDetailData> GetFishTankItems(int tankId)
    {
        if (_fishTankData.TryGetValue(tankId, out var list))
            return list.ToList();
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
    /// 根据 FishItemId 获取鱼数据
    /// </summary>
    public FishDetailData FindFishByItemId(int fishItemId)
    {
        if (_fishBagData.TryGetValue(fishItemId, out var fish))
            return fish;

        foreach (var kvp in _fishTankData)
        {
            var found = kvp.Value.FirstOrDefault(f => f.id == fishItemId);
            if (found != null)
                return found;
        }
        return null;
    }

    // ============================================================
    // 数据变更检测（内部方法）
    // ============================================================

    /// <summary>
    /// 计算鱼列表的Hash值（用于快速比较）
    /// </summary>
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

    /// <summary>
    /// 计算鱼缸状态列表的Hash值
    /// </summary>
    private int CalculateTankStatusHash(List<FishTankStatusResponse> statusList)
    {
        if (statusList == null || statusList.Count == 0) return 0;
        int hash = 0;
        foreach (var tank in statusList)
        {
            if (tank != null)
            {
                hash ^= tank.tankId.GetHashCode();
                hash ^= tank.isUnlocked.GetHashCode();
                hash ^= tank.capacity.GetHashCode();
                hash ^= tank.currentCount.GetHashCode();
                // 包含鱼列表
                if (tank.items != null)
                {
                    foreach (var fish in tank.items)
                    {
                        if (fish != null)
                            hash ^= fish.id.GetHashCode();
                    }
                }
            }
        }
        return hash;
    }

    /// <summary>
    /// 计算单个鱼缸状态的Hash值
    /// </summary>
    private int CalculateSingleTankHash(FishTankStatusResponse tank)
    {
        if (tank == null) return 0;
        int hash = tank.tankId.GetHashCode();
        hash ^= tank.isUnlocked.GetHashCode();
        hash ^= tank.capacity.GetHashCode();
        hash ^= tank.currentCount.GetHashCode();
        if (tank.items != null)
        {
            foreach (var fish in tank.items)
            {
                if (fish != null)
                    hash ^= fish.id.GetHashCode();
            }
        }
        return hash;
    }

    /// <summary>
    /// 检查鱼篓数据是否变化
    /// </summary>
    private bool HasBagDataChanged(List<FishDetailData> newList, int newCapacity)
    {
        int oldHash = CalculateFishListHash(_fishBagData.Values.ToList());
        int newHash = CalculateFishListHash(newList);
        return oldHash != newHash || fishBagCapacity != newCapacity;
    }

    // ============================================================
    // 数据更新方法（由 NetServerManager 调用）
    // ============================================================

    /// <summary>
    /// 从服务器响应更新鱼篓数据（全量替换）- 带变化检测
    /// </summary>
    public void UpdateFishBagFromResponse(List<FishDetailData> fishList, int capacity)
    {
        Z_Logger.Log($"[PlayerDataManager] UpdateFishBagFromResponse: 收到 {fishList?.Count ?? 0} 条鱼, 容量 {capacity}");

        if (fishList == null)
        {
            Z_Logger.LogWarning("[PlayerDataManager] 鱼篓数据为空，跳过更新");
            return;
        }

        // ✅ 先清空旧数据，再填充新数据（强制刷新）
        _fishBagData.Clear();

        foreach (var fish in fishList)
        {
            if (fish != null)
                _fishBagData[fish.id] = fish;
        }

        if (capacity > 0)
            fishBagCapacity = capacity;

        Z_Logger.Log($"[PlayerDataManager] 鱼篓数据更新完成: {_fishBagData.Count} 条鱼, 容量 {fishBagCapacity}");

        _fishDataVersion++;
        _lastNotifiedVersion = -1;  // ✅ 强制触发事件通知
        NotifyDataChanged();
    }

    /// <summary>
    /// 从服务器响应更新鱼缸数据（全量替换）- 带变化检测
    /// </summary>
    public void UpdateFishTankFromResponse(List<FishTankStatusResponse> tankStatusList)
    {
        Z_Logger.Log($"[PlayerDataManager] UpdateFishTankFromResponse: 收到 {tankStatusList?.Count ?? 0} 个鱼缸数据");

        if (tankStatusList == null || tankStatusList.Count == 0)
        {
            Z_Logger.LogWarning("[PlayerDataManager] 鱼缸数据为空，跳过更新");
            return;
        }

        // ✅ 先清空旧数据，再填充新数据（强制刷新）
        _fishTankData.Clear();
        _fishTankStatus.Clear();

        foreach (var tank in tankStatusList)
        {
            if (tank == null) continue;

            Z_Logger.Log($"[PlayerDataManager] 存储鱼缸 {tank.tankId}: isUnlocked={tank.isUnlocked}, items={tank.items?.Count ?? 0}");

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

        Z_Logger.Log($"[PlayerDataManager] 鱼缸数据更新完成: {_fishTankStatus.Count} 个鱼缸");

        _fishDataVersion++;
        _lastNotifiedVersion = -1;  // ✅ 强制触发事件通知
        NotifyDataChanged();
    }

    /// <summary>
    /// 检测鱼缸状态列表是否有变化
    /// </summary>
    private bool HasTankStatusChanged(List<FishTankStatusResponse> newList)
    {
        if (newList == null || newList.Count == 0)
        {
            return _fishTankStatus.Count > 0;
        }

        int oldHash = CalculateTankStatusHash(GetCurrentTankStatusList());
        int newHash = CalculateTankStatusHash(newList);
        return oldHash != newHash;
    }

    /// <summary>
    /// 获取当前鱼缸状态列表（用于Hash比较）
    /// </summary>
    private List<FishTankStatusResponse> GetCurrentTankStatusList()
    {
        var result = new List<FishTankStatusResponse>();
        foreach (var kvp in _fishTankStatus)
        {
            var status = kvp.Value;
            var fishList = GetFishTankItems(kvp.Key);
            result.Add(new FishTankStatusResponse
            {
                tankId = status.tankId,
                isUnlocked = status.isUnlocked,
                level = status.level,
                capacity = status.capacity,
                currentCount = status.currentCount,
                remainingSpace = status.remainingSpace,
                items = fishList.ToList()
            });
        }
        return result;
    }

    /// <summary>
    /// 更新单个鱼缸状态 - 带变化检测
    /// </summary>
    public void UpdateSingleFishTankFromResponse(FishTankStatusResponse tank)
    {
        if (tank == null) return;

        // 检测是否真的有变化
        int oldHash = CalculateSingleTankHash(GetCurrentSingleTankStatus(tank.tankId));
        int newHash = CalculateSingleTankHash(tank);
        if (oldHash == newHash && _fishTankStatus.ContainsKey(tank.tankId))
        {
            Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tank.tankId} 数据未变化，跳过更新");
            return;
        }

        if (!_fishTankStatus.ContainsKey(tank.tankId))
            _fishTankStatus[tank.tankId] = new FishTankStatusData();

        _fishTankStatus[tank.tankId].tankId = tank.tankId;
        _fishTankStatus[tank.tankId].isUnlocked = tank.isUnlocked;
        _fishTankStatus[tank.tankId].level = tank.level;
        _fishTankStatus[tank.tankId].capacity = tank.capacity;
        _fishTankStatus[tank.tankId].currentCount = tank.currentCount;
        _fishTankStatus[tank.tankId].remainingSpace = tank.remainingSpace;

        _fishTankData[tank.tankId] = tank.items ?? new List<FishDetailData>();

        _fishDataVersion++;
        Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tank.tankId} 状态已更新, 版本 {_fishDataVersion}");

        NotifyDataChanged();
    }

    /// <summary>
    /// 获取单个鱼缸当前状态（用于Hash比较）
    /// </summary>
    private FishTankStatusResponse GetCurrentSingleTankStatus(int tankId)
    {
        if (!_fishTankStatus.TryGetValue(tankId, out var status))
            return null;

        var fishList = GetFishTankItems(tankId);
        return new FishTankStatusResponse
        {
            tankId = status.tankId,
            isUnlocked = status.isUnlocked,
            level = status.level,
            capacity = status.capacity,
            currentCount = status.currentCount,
            remainingSpace = status.remainingSpace,
            items = fishList.ToList()
        };
    }

    // ============================================================
    // 数据操作辅助方法（带变化检测）
    // ============================================================

    /// <summary>
    /// 从鱼篓移除指定鱼
    /// </summary>
    public bool RemoveFishFromBag(int fishItemId)
    {
        if (_fishBagData.Remove(fishItemId))
        {
            _fishDataVersion++;
            NotifyDataChanged();
            return true;
        }
        return false;
    }

    /// <summary>
    /// 添加鱼到鱼篓
    /// </summary>
    public void AddFishToBag(FishDetailData fish)
    {
        if (fish == null) return;
        _fishBagData[fish.id] = fish;
        _fishDataVersion++;
        NotifyDataChanged();
    }

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
                if (_fishTankStatus.TryGetValue(tankId, out var status))
                {
                    status.currentCount = list.Count;
                    status.remainingSpace = status.capacity - list.Count;
                }
                _fishDataVersion++;
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

        _fishTankData[tankId].Add(fish);

        if (_fishTankStatus.TryGetValue(tankId, out var status))
        {
            status.currentCount = _fishTankData[tankId].Count;
            status.remainingSpace = status.capacity - status.currentCount;
        }

        _fishDataVersion++;
        NotifyDataChanged();
    }

    /// <summary>
    /// 从鱼缸转移到鱼篓（完整操作）
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
    /// 从鱼篓转移到鱼缸（完整操作）
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

    // ============================================================
    // 数据变更通知 - 统一事件（只触发一个）
    // ============================================================

    /// <summary>
    /// 通知数据已变更 - 只触发统一事件
    /// </summary>
    private void NotifyDataChanged()
    {
        // 去重：如果版本号没变，不触发事件
        if (_fishDataVersion == _lastNotifiedVersion)
        {
            Z_Logger.Log("[PlayerDataManager] 版本号未变化，跳过事件通知");
            return;
        }
        _lastNotifiedVersion = _fishDataVersion;

        Z_Logger.Log($"[PlayerDataManager] 触发数据更新通知: 版本 {_fishDataVersion}");

        // ✅ 触发数据更新事件，由 PlayerDataService 处理
        CommunicateEvent.Modify("PlayerDataUpdated");
    }

    /// <summary>
    /// 强制触发数据更新通知（用于强制刷新场景）
    /// </summary>
    public void ForceNotifyDataChanged()
    {
        _lastNotifiedVersion = -1;
        NotifyDataChanged();
    }

    // ============================================================
    // 数据快照
    // ============================================================

    /// <summary>
    /// 获取数据快照（用于UI一次性读取）
    /// </summary>
    public FishDataSnapshot GetFishDataSnapshot()
    {
        return new FishDataSnapshot
        {
            Version = _fishDataVersion,
            FishBag = GetFishBagList(),
            FishBagCapacity = fishBagCapacity,
            FishTankStatus = new Dictionary<int, FishTankStatusData>(_fishTankStatus),
            FishTankData = _fishTankData.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToList()
            )
        };
    }

    // ============================================================
    // 初始化和清理
    // ============================================================

    public void InitFishTankData()
    {
        _fishBagData.Clear();
        _fishTankData.Clear();
        _fishTankStatus.Clear();
        fishBagCapacity = 10;
        _fishDataVersion = 0;
        _lastNotifiedVersion = -1;
        Z_Logger.Log("[PlayerDataManager] 鱼缸数据已初始化");
    }

    public void ClearFishTankData()
    {
        _fishBagData.Clear();
        _fishTankData.Clear();
        _fishTankStatus.Clear();
        fishBagCapacity = 10;
        _fishDataVersion = 0;
        _lastNotifiedVersion = -1;
        Z_Logger.Log("[PlayerDataManager] 鱼缸数据已清空");
    }

    // ============================================================
    // 调试
    // ============================================================

    public void DebugPrintFishData()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 鱼缸数据 Debug =====");
        sb.AppendLine($"版本: {_fishDataVersion}");
        sb.AppendLine($"上次通知版本: {_lastNotifiedVersion}");
        sb.AppendLine($"鱼篓: {_fishBagData.Count}/{fishBagCapacity}");

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
// 数据类定义
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
/// 鱼缸数据快照
/// </summary>
[Serializable]
public class FishDataSnapshot
{
    public int Version;
    public List<FishDetailData> FishBag = new List<FishDetailData>();
    public int FishBagCapacity;
    public Dictionary<int, FishTankStatusData> FishTankStatus = new Dictionary<int, FishTankStatusData>();
    public Dictionary<int, List<FishDetailData>> FishTankData = new Dictionary<int, List<FishDetailData>>();
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
