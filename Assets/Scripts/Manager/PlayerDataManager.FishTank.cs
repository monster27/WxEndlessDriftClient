// ============================================================
// 文件: PlayerDataManager.FishTank.cs
// 说明: 鱼缸系统数据管理 - 包含鱼缸、鱼篓、装饰数据
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
        // ===== 基础操作 =====
        OpenFishTank,           // 打开鱼缸
        CloseFishTank,          // 关闭鱼缸
        RefreshFishTank,        // 刷新数据
        SwitchTank,             // 切换鱼缸 (参数: int newIndex)
        TransferFish,           // 转移鱼 (参数: TransferData)
        UnlockTank,             // 解锁鱼缸 (参数: int tankId)
        ToggleManagerPanel,     // 切换管理面板

        // ===== 数据通知 =====
        DataUpdated,            // 数据已更新，请刷新UI
        DataLoaded,             // 网络数据加载完成
        PlayerDataUpdated,      // 玩家数据变化（由 DataManager 触发）

        // ===== 装饰相关 =====
        EnterDecorationMode,    // 进入装饰模式 (参数: int tankId)
        ExitDecorationMode,     // 退出装饰模式
        EquipDecoration,        // 装备装饰 (参数: EquipDecorationData)
        RemoveDecoration,       // 卸下装饰 (参数: RemoveDecorationData)
        MirrorDecoration,       // 镜像装饰 (参数: MirrorDecorationData)
        MoveDecoration,         // 移动装饰 (参数: MoveDecorationData)
        SelectDecoration,       // 选中已装备装饰 (参数: SelectDecorationData)
        ShowDecOperator,        // 显示操作面板 (参数: ShowDecOperatorData)
        HideDecOperator,        // 隐藏操作面板
        DecorationDataUpdated,  // 装饰数据已更新
        ApplyTextureDecoration, // 应用纹理装饰（82~84）
    }

    // ============================================================
    // 装饰操作参数类
    // ============================================================

    /// <summary>
    /// 装备装饰参数
    /// </summary>
    [Serializable]
    public class EquipDecorationData
    {
        public int TankId;
        public int Category;
        public int DecorationId;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float ScaleX;
        public float ScaleY;
        public float ScaleZ;
        public float RotX;
        public float RotY;
        public float RotZ;
    }

    /// <summary>
    /// 卸下装饰参数
    /// </summary>
    [Serializable]
    public class RemoveDecorationData
    {
        public int TankId;
        public int RecordId;
    }

    /// <summary>
    /// 镜像装饰参数
    /// </summary>
    [Serializable]
    public class MirrorDecorationData
    {
        public int TankId;
        public int RecordId;
    }

    /// <summary>
    /// 移动装饰参数
    /// </summary>
    [Serializable]
    public class MoveDecorationData
    {
        public int TankId;
        public int RecordId;
        public float DeltaX;
        public float DeltaY;
    }

    /// <summary>
    /// 选中装饰参数（显示操作面板）
    /// </summary>
    [Serializable]
    public class SelectDecorationData
    {
        public int TankId;
        public int RecordId;
        public int Category;               // 新增品类字段
        public Vector2 ScreenPosition;
    }

    /// <summary>
    /// 显示操作面板参数
    /// </summary>
    [Serializable]
    public class ShowDecOperatorData
    {
        public int TankId;
        public int RecordId;
        public Vector2 ScreenPosition;
    }

    /// <summary>
    /// 应用纹理装饰参数（82~84）
    /// </summary>
    [Serializable]
    public class ApplyDecorationData
    {
        public int TankId;
        public int Category;
        public int DecorationId;
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
    // 装饰数据存储
    // ============================================================

    /// <summary>
    /// 玩家拥有的装饰ID列表
    /// </summary>
    private List<int> _ownedDecorationIds = new List<int>();

    /// <summary>
    /// 鱼缸装备状态 - Key: TankId, Value: Dictionary(品类, List<装备信息>)
    /// </summary>
    private Dictionary<int, Dictionary<int, List<DecorationEquipInfo>>> _tankEquippedDecorations =
        new Dictionary<int, Dictionary<int, List<DecorationEquipInfo>>>();

    /// <summary>
    /// 按RecordId索引的装备信息（快速查询）
    /// </summary>
    private Dictionary<int, DecorationEquipInfo> _decorationInfoByRecordId = new Dictionary<int, DecorationEquipInfo>();

    // ============================================================
    // 公开属性
    // ============================================================

    public bool IsFishDataLoaded => _isFishDataLoaded;

    // ============================================================
    // 鱼篓数据查询方法（统一使用主文件的 fishDetailData）
    // ============================================================

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
                        result.Add(fish);
                }
            }
        }
        return result;
    }

    public int GetFishBagCapacity() => fishBagCapacity;

    public int GetFishBagCount() => GetFishBagList().Count;

    public int GetFishBagRemaining() => fishBagCapacity - GetFishBagCount();

    public void AddFishToBag(FishDetailData fish)
    {
        if (fish == null) return;
        if (fishDetailData == null)
            fishDetailData = new Dictionary<int, List<FishDetailData>>();
        fish.location = 0;
        fish.tankId = 0;
        if (!fishDetailData.ContainsKey(fish.fishId))
            fishDetailData[fish.fishId] = new List<FishDetailData>();
        fishDetailData[fish.fishId].Add(fish);
        NotifyDataChanged();
    }

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
                        fishDetailData.Remove(fishId);
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

    public List<FishDetailData> GetFishTankItems(int tankId)
    {
        if (_fishTankData.TryGetValue(tankId, out var list))
            return list.ToList();
        Z_Logger.Log($"[PlayerDataManager] GetFishTankItems: tankId={tankId} 未找到数据，返回空列表");
        return new List<FishDetailData>();
    }

    public int GetFishTankCount(int tankId)
    {
        if (_fishTankData.TryGetValue(tankId, out var list))
            return list.Count;
        return 0;
    }

    public FishTankStatusData GetFishTankStatus(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status;
        return null;
    }

    public List<FishTankStatusData> GetAllFishTankStatus() => _fishTankStatus.Values.ToList();

    public List<FishTankStatusData> GetAllFishTankStatusOrdered() =>
        _fishTankStatus.Values.OrderBy(s => s.tankId).ToList();

    public bool IsFishTankUnlocked(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status.isUnlocked;
        return false;
    }

    public int GetFishTankCapacity(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status.capacity;
        return 10;
    }

    public int GetFishTankRemaining(int tankId)
    {
        if (_fishTankStatus.TryGetValue(tankId, out var status))
            return status.remainingSpace;
        return 0;
    }

    public int FindTankIdByFishItemId(int fishItemId)
    {
        foreach (var kvp in _fishTankData)
            if (kvp.Value.Any(f => f.id == fishItemId))
                return kvp.Key;
        return -1;
    }

    public FishDetailData FindFishByItemId(int fishItemId)
    {
        if (fishDetailData != null)
        {
            foreach (var kvp in fishDetailData)
                foreach (var fish in kvp.Value)
                    if (fish != null && fish.id == fishItemId)
                        return fish;
        }
        foreach (var kvp in _fishTankData)
        {
            var found = kvp.Value.FirstOrDefault(f => f.id == fishItemId);
            if (found != null) return found;
        }
        return null;
    }

    // ============================================================
    // 装饰数据查询方法
    // ============================================================

    public List<int> GetOwnedDecorationIds() => new List<int>(_ownedDecorationIds);

    public bool HasDecoration(int decorationId) => _ownedDecorationIds.Contains(decorationId);

    public Dictionary<int, List<DecorationEquipInfo>> GetEquippedDecorations(int tankId)
    {
        if (_tankEquippedDecorations.TryGetValue(tankId, out var result))
        {
            var copy = new Dictionary<int, List<DecorationEquipInfo>>();
            foreach (var kvp in result)
                copy[kvp.Key] = new List<DecorationEquipInfo>(kvp.Value);
            return copy;
        }
        return new Dictionary<int, List<DecorationEquipInfo>>();
    }

    public List<DecorationEquipInfo> GetEquippedDecorationsByCategory(int tankId, int category)
    {
        if (_tankEquippedDecorations.TryGetValue(tankId, out var tankDecorations))
        {
            if (tankDecorations.TryGetValue(category, out var list))
                return new List<DecorationEquipInfo>(list);
        }
        return new List<DecorationEquipInfo>();
    }

    public List<DecorationEquipInfo> GetEquippedMovableDecorations(int tankId)
    {
        var result = new List<DecorationEquipInfo>();
        if (_tankEquippedDecorations.TryGetValue(tankId, out var tankDecorations))
        {
            if (tankDecorations.TryGetValue(80, out var list80))
                result.AddRange(list80);
            if (tankDecorations.TryGetValue(81, out var list81))
                result.AddRange(list81);
        }
        return result;
    }

    public DecorationEquipInfo GetDecorationInfoByRecordId(int recordId)
    {
        if (_decorationInfoByRecordId.TryGetValue(recordId, out var info))
            return info;
        return null;
    }

    public bool IsDecorationEquipped(int tankId, int recordId) =>
        _decorationInfoByRecordId.ContainsKey(recordId);

    // ============================================================
    // 装饰数据更新方法（由 NetServerManager 调用）
    // ============================================================

    public void UpdateOwnedDecorations(List<int> decorationIds)
    {
        _ownedDecorationIds = decorationIds ?? new List<int>();
        Z_Logger.Log($"[PlayerDataManager] 拥有装饰更新: {_ownedDecorationIds.Count} 个");
    }

    public void UpdateEquippedDecorations(int tankId, Dictionary<int, List<DecorationEquipInfo>> equipped)
    {
        // 清理旧的RecordId索引
        if (_tankEquippedDecorations.TryGetValue(tankId, out var oldDecorations))
        {
            foreach (var kvp in oldDecorations)
                foreach (var info in kvp.Value)
                    _decorationInfoByRecordId.Remove(info.Id);   // 使用 Id 作为 RecordId
        }

        _tankEquippedDecorations[tankId] = equipped ?? new Dictionary<int, List<DecorationEquipInfo>>();

        // 更新索引
        foreach (var kvp in _tankEquippedDecorations[tankId])
            foreach (var info in kvp.Value)
                _decorationInfoByRecordId[info.Id] = info;

        Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tankId} 装备装饰更新完成");
        NotifyDataChanged();
    }

    public void AddEquippedDecoration(int tankId, int category, DecorationEquipInfo info)
    {
        if (info == null) return;
        if (!_tankEquippedDecorations.ContainsKey(tankId))
            _tankEquippedDecorations[tankId] = new Dictionary<int, List<DecorationEquipInfo>>();
        if (!_tankEquippedDecorations[tankId].ContainsKey(category))
            _tankEquippedDecorations[tankId][category] = new List<DecorationEquipInfo>();

        _tankEquippedDecorations[tankId][category].Add(info);
        _decorationInfoByRecordId[info.Id] = info;
        Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tankId} 装备装饰: RecordId={info.Id}, DecId={info.DecorationId}");
        NotifyDataChanged();
    }

    public bool RemoveEquippedDecoration(int tankId, int recordId)
    {
        if (!_tankEquippedDecorations.TryGetValue(tankId, out var tankDecorations))
            return false;

        foreach (var kvp in tankDecorations)
        {
            var list = kvp.Value;
            int removed = list.RemoveAll(info => info.Id == recordId);
            if (removed > 0)
            {
                _decorationInfoByRecordId.Remove(recordId);
                Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tankId} 卸下装饰: RecordId={recordId}");
                NotifyDataChanged();
                return true;
            }
        }
        return false;
    }

    public void UpdateDecorationPosition(int tankId, int recordId, float posX, float posY)
    {
        if (_decorationInfoByRecordId.TryGetValue(recordId, out var info))
        {
            info.PositionX = posX;
            info.PositionY = posY;
            Z_Logger.Log($"[PlayerDataManager] 装饰位置更新: RecordId={recordId}, ({posX:F2}, {posY:F2})");
            NotifyDataChanged();
        }
    }

    public void UpdateDecorationMirror(int tankId, int recordId, float rotationY)
    {
        if (_decorationInfoByRecordId.TryGetValue(recordId, out var info))
        {
            info.RotationY = rotationY;
            Z_Logger.Log($"[PlayerDataManager] 装饰镜像更新: RecordId={recordId}, RotationY={rotationY}");
            NotifyDataChanged();
        }
    }

    // ============================================================
    // 数据更新方法（由 NetServerManager 调用）
    // ============================================================

    public void UpdateFishBagFromResponse(List<FishDetailData> fishList, int capacity)
    {
        if (fishDetailData == null)
            fishDetailData = new Dictionary<int, List<FishDetailData>>();

        // 清空旧的鱼篓数据（只删除 location == 0 的鱼）
        var keysToRemove = new List<int>();
        foreach (var kvp in fishDetailData)
        {
            if (kvp.Value.All(f => f.location == 0))
                keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
            fishDetailData.Remove(key);

        if (fishList != null)
        {
            foreach (var fish in fishList)
            {
                if (fish == null) continue;
                fish.location = 0;
                fish.tankId = 0;
                if (!fishDetailData.ContainsKey(fish.fishId))
                    fishDetailData[fish.fishId] = new List<FishDetailData>();
                fishDetailData[fish.fishId].Add(fish);
            }
        }

        if (capacity > 0)
            fishBagCapacity = capacity;

        Z_Logger.Log($"[PlayerDataManager] 鱼篓数据更新: {GetFishBagCount()} 条鱼, 容量 {fishBagCapacity}");
        NotifyDataChanged();
    }

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

    public void UpdateSingleFishTankFromResponse(FishTankStatusResponse tank)
    {
        if (tank == null) return;

        if (!_fishTankStatus.ContainsKey(tank.tankId))
            _fishTankStatus[tank.tankId] = new FishTankStatusData();

        _fishTankStatus[tank.tankId].tankId = tank.tankId;
        _fishTankStatus[tank.tankId].isUnlocked = tank.isUnlocked;
        _fishTankStatus[tank.tankId].level = tank.level;
        _fishTankStatus[tank.tankId].capacity = tank.capacity;
        _fishTankStatus[tank.tankId].currentCount = tank.currentCount;
        _fishTankStatus[tank.tankId].remainingSpace = tank.remainingSpace;

        _fishTankData[tank.tankId] = tank.items != null ? new List<FishDetailData>(tank.items) : new List<FishDetailData>();

        _isFishDataLoaded = true;
        Z_Logger.Log($"[PlayerDataManager] 鱼缸 {tank.tankId} 状态已更新");
        NotifyDataChanged();
    }

    // ============================================================
    // 数据操作辅助方法
    // ============================================================

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

    public bool TransferFishFromTankToBag(int tankId, int fishItemId)
    {
        var fish = FindFishByItemId(fishItemId);
        if (fish == null) return false;
        if (RemoveFishFromTank(tankId, fishItemId))
        {
            AddFishToBag(fish);
            return true;
        }
        return false;
    }

    public bool TransferFishFromBagToTank(int tankId, int fishItemId)
    {
        var fish = FindFishByItemId(fishItemId);
        if (fish == null) return false;
        if (RemoveFishFromBag(fishItemId))
        {
            AddFishToTank(tankId, fish);
            return true;
        }
        return false;
    }

    public bool TransferFishFromTankToTank(int fromTankId, int toTankId, int fishItemId)
    {
        var fish = FindFishByItemId(fishItemId);
        if (fish == null) return false;
        if (RemoveFishFromTank(fromTankId, fishItemId))
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
        _ownedDecorationIds.Clear();
        _tankEquippedDecorations.Clear();
        _decorationInfoByRecordId.Clear();
        _isFishDataLoaded = false;
        Z_Logger.Log("[PlayerDataManager] 鱼缸数据已初始化");
    }

    public void ClearFishTankData()
    {
        _fishTankData.Clear();
        _fishTankStatus.Clear();
        _ownedDecorationIds.Clear();
        _tankEquippedDecorations.Clear();
        _decorationInfoByRecordId.Clear();
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
        sb.AppendLine($"拥有装饰: {_ownedDecorationIds.Count} 个");

        foreach (var kvp in _fishTankStatus.OrderBy(k => k.Key))
        {
            var status = kvp.Value;
            var fishList = GetFishTankItems(kvp.Key);
            sb.AppendLine($"鱼缸 {kvp.Key}: {fishList.Count}/{status.capacity} 解锁={status.isUnlocked}");
        }

        foreach (var kvp in _tankEquippedDecorations)
        {
            sb.AppendLine($"鱼缸 {kvp.Key} 装备装饰:");
            foreach (var catKvp in kvp.Value)
            {
                sb.AppendLine($"  品类 {catKvp.Key}: {catKvp.Value.Count} 个");
            }
        }
        Z_Logger.Log(sb.ToString());
    }
}

// ============================================================
// 数据类定义（从NetServerManager迁移）
// ============================================================

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

[Serializable]
public class FishTankConfig
{
    public int id;
    public string name;
    public string type;
    public int purchaseCost;
    public int defaultCapacity;
}
