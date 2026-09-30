// ============================================================
// 文件: PlayerDataManager.Egg.cs
// 说明: 蛋 / 孵化 本地数据管理
// 路径: Assets/Scripts/Manager/
// ============================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class PlayerDataManager
{
    // ============================================================
    // 消息枚举
    // ============================================================

    public enum EggMessage
    {
        /// <summary>孵化槽位数据更新</summary>
        EggSlotsUpdated,
        /// <summary>蛋数据已加载</summary>
        EggDataLoaded,
    }

    // ============================================================
    // 本地缓存
    // ============================================================

    /// <summary>孵化槽位列表（固定 6 个）</summary>
    private List<EggHatchSlotData> _eggHatchSlots = new List<EggHatchSlotData>();

    private bool _isEggDataLoaded = false;
    public bool IsEggDataLoaded => _isEggDataLoaded;

    // ============================================================
    // 公开只读属性
    // ============================================================

    /// <summary>孵化槽位列表（只读视图）</summary>
    public IReadOnlyList<EggHatchSlotData> EggHatchSlots => _eggHatchSlots;

    // ============================================================
    // 查询接口
    // ============================================================

    /// <summary>获取指定槽位数据</summary>
    public EggHatchSlotData GetHatchSlot(int slotIndex)
    {
        return _eggHatchSlots.FirstOrDefault(s => s.slotIndex == slotIndex);
    }

    /// <summary>获取所有正在孵化的槽位</summary>
    public List<EggHatchSlotData> GetHatchingSlots()
    {
        return _eggHatchSlots.Where(s => s.isOccupied && !s.isClaimed).ToList();
    }

    /// <summary>获取所有可领取的槽位</summary>
    public List<EggHatchSlotData> GetCompletedSlots()
    {
        return _eggHatchSlots.Where(s => s.isCompleted && !s.isClaimed).ToList();
    }

    /// <summary>判断某槽位是否可以开始孵化</summary>
    public bool CanStartHatch(int slotIndex)
    {
        var slot = GetHatchSlot(slotIndex);
        if (slot == null) return true;
        return !slot.isOccupied || slot.isClaimed;
    }

    /// <summary>检查某稀有度的蛋是否正在孵化（同稀有度互斥）</summary>
    public bool IsRarityHatching(int rarityId)
    {
        return _eggHatchSlots.Any(s => s.isOccupied && !s.isClaimed && s.rarityId == rarityId);
    }

    /// <summary>根据蛋ID查它的稀有度</summary>
    public int GetEggRarityId(int eggId)
    {
        if (LoadDataManager.Instance == null) return 0;
        var egg = LoadDataManager.Instance.GetEggById(eggId);
        return egg?.rarityId ?? 0;
    }

    // ============================================================
    // 数据更新入口（由 NetServerManager 调用）
    // ============================================================

    /// <summary>更新孵化槽位数据（全量覆盖）</summary>
    public void UpdateEggHatchSlots(List<EggHatchSlotData> slots)
    {
        _eggHatchSlots = slots ?? new List<EggHatchSlotData>();
        _isEggDataLoaded = true;
        Z_Logger.Log($"[PlayerDataManager] 孵化槽位更新: {_eggHatchSlots.Count} 个");
        NotifyEggDataChanged(EggMessage.EggSlotsUpdated);
        NotifyEggDataChanged(EggMessage.EggDataLoaded);
    }

    /// <summary>更新单个孵化槽位</summary>
    public void UpdateSingleHatchSlot(EggHatchSlotData slot)
    {
        if (slot == null) return;
        int idx = _eggHatchSlots.FindIndex(s => s.slotIndex == slot.slotIndex);
        if (idx >= 0) _eggHatchSlots[idx] = slot;
        else _eggHatchSlots.Add(slot);
        NotifyEggDataChanged(EggMessage.EggSlotsUpdated);
    }

    /// <summary>本地更新槽位倒计时（UI 每帧调用，不触发网络）</summary>
    public void UpdateSlotRemainingSeconds(int slotIndex, int remainingSeconds)
    {
        var slot = _eggHatchSlots.FirstOrDefault(s => s.slotIndex == slotIndex);
        if (slot == null) return;

        slot.remainingSeconds = Math.Max(0, remainingSeconds);
        slot.isCompleted = (slot.remainingSeconds <= 0);

        NotifyEggDataChanged(EggMessage.EggSlotsUpdated);
    }

    // ============================================================
    // 数据清理
    // ============================================================

    public void InitEggData()
    {
        _eggHatchSlots.Clear();
        _isEggDataLoaded = false;
        Z_Logger.Log("[PlayerDataManager] 蛋数据已初始化");
    }

    public void ClearEggData()
    {
        InitEggData();
        Z_Logger.Log("[PlayerDataManager] 蛋数据已清空");
    }

    // ============================================================
    // 内部通知
    // ============================================================

    private void NotifyEggDataChanged(EggMessage message)
    {
        CommunicateEvent.Modify(message.ToString());
    }

    // ============================================================
    // 调试
    // ============================================================

    public void DebugPrintEggData()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 蛋数据 Debug =====");
        sb.AppendLine($"已加载: {_isEggDataLoaded}");
        sb.AppendLine($"槽位总数: {_eggHatchSlots.Count}");
        foreach (var slot in _eggHatchSlots)
        {
            if (slot.isOccupied && !slot.isClaimed)
            {
                sb.AppendLine($"  槽位{slot.slotIndex}: eggId={slot.eggId}, 剩余{slot.remainingSeconds}秒, 完成={slot.isCompleted}");
            }
        }
        Z_Logger.Log(sb.ToString());
    }
}
