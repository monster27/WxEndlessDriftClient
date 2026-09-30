// ============================================================
// 文件: PlayerDataManager.Insect.cs
// 说明: 昆虫 本地数据管理
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

    public enum InsectMessage
    {
        /// <summary>昆虫列表更新</summary>
        InsectsUpdated,
        /// <summary>昆虫图鉴更新</summary>
        InsectCollectionUpdated,
    }

    // ============================================================
    // 本地缓存
    // ============================================================

    /// <summary>昆虫列表</summary>
    private List<PlayerInsectData> _playerInsects = new List<PlayerInsectData>();

    /// <summary>已解锁的昆虫图鉴 ID 集合</summary>
    private HashSet<int> _insectCollectionUnlocked = new HashSet<int>();

    /// <summary>昆虫图鉴总种类数</summary>
    private int _insectCollectionTotal = 0;

    private bool _isInsectDataLoaded = false;
    public bool IsInsectDataLoaded => _isInsectDataLoaded;

    // ============================================================
    // 公开只读属性
    // ============================================================

    /// <summary>昆虫列表（只读视图）</summary>
    public IReadOnlyList<PlayerInsectData> PlayerInsects => _playerInsects;

    // ============================================================
    // 查询接口
    // ============================================================

    public PlayerInsectData GetInsectByInstanceId(int instanceId)
    {
        return _playerInsects.FirstOrDefault(i => i.insectInstanceId == instanceId);
    }

    public List<PlayerInsectData> GetInsectsByInsectId(int insectId)
    {
        return _playerInsects.Where(i => i.insectId == insectId).ToList();
    }

    public List<PlayerInsectData> GetInsectsByRarity(int rarityId)
    {
        return _playerInsects.Where(i => i.rarityId == rarityId).ToList();
    }

    public int GetInsectCount() => _playerInsects.Count;

    public string GetInsectName(int insectId)
    {
        if (LoadDataManager.Instance == null) return $"昆虫{insectId}";
        var insect = LoadDataManager.Instance.GetInsectById(insectId);
        return insect?.name ?? $"昆虫{insectId}";
    }

    // ============================================================
    // 图鉴 - 查询接口
    // ============================================================

    public bool IsInsectUnlockedInCollection(int insectId)
    {
        return _insectCollectionUnlocked.Contains(insectId);
    }

    public int GetInsectCollectionUnlockedCount() => _insectCollectionUnlocked.Count;

    public int GetInsectCollectionTotal() => _insectCollectionTotal;

    public float GetInsectCollectionProgress()
    {
        if (_insectCollectionTotal <= 0) return 0f;
        return (float)_insectCollectionUnlocked.Count / _insectCollectionTotal * 100f;
    }

    public List<int> GetUnlockedInsectIds() => _insectCollectionUnlocked.ToList();

    // ============================================================
    // 数据更新入口（由 NetServerManager 调用）
    // ============================================================

    public void UpdatePlayerInsects(List<PlayerInsectData> insects)
    {
        _playerInsects = insects ?? new List<PlayerInsectData>();
        _isInsectDataLoaded = true;

        foreach (var insect in _playerInsects)
        {
            _insectCollectionUnlocked.Add(insect.insectId);
        }

        Z_Logger.Log($"[PlayerDataManager] 昆虫列表更新: {_playerInsects.Count} 只");
        NotifyInsectDataChanged(InsectMessage.InsectsUpdated);
        NotifyInsectDataChanged(InsectMessage.InsectCollectionUpdated);
    }

    public void AddInsect(PlayerInsectData insect)
    {
        if (insect == null) return;

        // 去重
        int idx = _playerInsects.FindIndex(i => i.insectInstanceId == insect.insectInstanceId);
        if (idx >= 0)
        {
            _playerInsects[idx] = insect;
        }
        else
        {
            _playerInsects.Add(insect);
        }

        if (!_insectCollectionUnlocked.Contains(insect.insectId))
        {
            _insectCollectionUnlocked.Add(insect.insectId);
            NotifyInsectDataChanged(InsectMessage.InsectCollectionUpdated);
        }

        Z_Logger.Log($"[PlayerDataManager] 添加昆虫: instanceId={insect.insectInstanceId}, insectId={insect.insectId}");
        NotifyInsectDataChanged(InsectMessage.InsectsUpdated);
    }

    public bool RemoveInsect(int insectInstanceId)
    {
        int removed = _playerInsects.RemoveAll(i => i.insectInstanceId == insectInstanceId);
        if (removed > 0)
        {
            Z_Logger.Log($"[PlayerDataManager] 移除昆虫: instanceId={insectInstanceId}");
            NotifyInsectDataChanged(InsectMessage.InsectsUpdated);
            return true;
        }
        return false;
    }

    public void UpdateInsectCollection(List<int> unlockedInsectIds, int totalCount = 0)
    {
        _insectCollectionUnlocked = new HashSet<int>(unlockedInsectIds ?? new List<int>());
        if (totalCount > 0) _insectCollectionTotal = totalCount;

        Z_Logger.Log($"[PlayerDataManager] 昆虫图鉴更新: {_insectCollectionUnlocked.Count}/{_insectCollectionTotal}");
        NotifyInsectDataChanged(InsectMessage.InsectCollectionUpdated);
    }

    // ============================================================
    // 数据清理
    // ============================================================

    public void InitInsectData()
    {
        _playerInsects.Clear();
        _insectCollectionUnlocked.Clear();
        _isInsectDataLoaded = false;
        _insectCollectionTotal = 0;
        Z_Logger.Log("[PlayerDataManager] 昆虫数据已初始化");
    }

    public void ClearInsectData()
    {
        InitInsectData();
        Z_Logger.Log("[PlayerDataManager] 昆虫数据已清空");
    }

    // ============================================================
    // 内部通知
    // ============================================================

    private void NotifyInsectDataChanged(InsectMessage message)
    {
        CommunicateEvent.Modify(message.ToString());
    }

    // ============================================================
    // 调试
    // ============================================================

    public void DebugPrintInsectData()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 昆虫数据 Debug =====");
        sb.AppendLine($"已加载: {_isInsectDataLoaded}");
        sb.AppendLine($"昆虫总数: {_playerInsects.Count}");
        sb.AppendLine($"昆虫图鉴: {_insectCollectionUnlocked.Count}/{_insectCollectionTotal}");
        Z_Logger.Log(sb.ToString());
    }
}
