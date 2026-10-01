// ============================================================
// 文件: PlayerDataManager.Pet.cs
// 说明: 宠物 + 宠物栏 + 自动喂食 本地数据管理
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

    public enum PetMessage
    {
        /// <summary>宠物列表更新</summary>
        PetsUpdated,
        /// <summary>出战宠物变更</summary>
        PetActiveChanged,
        /// <summary>宠物改名</summary>
        PetRenamed,
        /// <summary>宠物饥饿度变化</summary>
        PetHungerChanged,
        /// <summary>宠物锁定状态变化</summary>
        PetLockChanged,
        /// <summary>宠物栏状态更新</summary>
        PetStorageUpdated,
        /// <summary>宠物图鉴更新</summary>
        PetCollectionUpdated,
        /// <summary>自动喂食状态更新</summary>
        AutoFeedStatusUpdated,
        /// <summary>自动喂食过滤配置更新</summary>
        AutoFeedFilterUpdated,
    }

    // ============================================================
    // 本地缓存
    // ============================================================

    /// <summary>玩家宠物列表</summary>
    private List<PlayerPetData> _playerPets = new List<PlayerPetData>();

    /// <summary>当前出战宠物 ID（-1=无）</summary>
    private int _activePetInstanceId = -1;

    /// <summary>宠物栏状态</summary>
    private PetStorageStatusData _petStorageStatus;

    /// <summary>已解锁的宠物图鉴 ID 集合</summary>
    private HashSet<int> _petCollectionUnlocked = new HashSet<int>();

    /// <summary>宠物图鉴总种类数</summary>
    private int _petCollectionTotal = 0;

    /// <summary>自动喂食状态</summary>
    private AutoFeedStatusData _autoFeedStatus;

    /// <summary>自动喂食过滤配置</summary>
    private AutoFeedFilterData _autoFeedFilter;

    private bool _isPetDataLoaded = false;
    private bool _isPetStorageLoaded = false;

    public bool IsPetDataLoaded => _isPetDataLoaded;
    public bool IsPetStorageLoaded => _isPetStorageLoaded;

    // ============================================================
    // 公开只读属性
    // ============================================================

    /// <summary>宠物列表（只读视图，避免外部修改 + 减少 GC）</summary>
    public IReadOnlyList<PlayerPetData> PlayerPets => _playerPets;

    public PetStorageStatusData PetStorageStatus => _petStorageStatus;
    public int ActivePetInstanceId => _activePetInstanceId;
    public AutoFeedStatusData AutoFeedStatus => _autoFeedStatus;
    public AutoFeedFilterData AutoFeedFilter => _autoFeedFilter;

    // ============================================================
    // 宠物 - 查询接口
    // ============================================================

    /// <summary>根据 instanceId 获取宠物</summary>
    public PlayerPetData GetPetByInstanceId(int instanceId)
    {
        return _playerPets.FirstOrDefault(p => p.petInstanceId == instanceId);
    }

    /// <summary>获取当前出战宠物</summary>
    public PlayerPetData GetActivePet()
    {
        return _playerPets.FirstOrDefault(p => p.isActive);
    }

    /// <summary>按稀有度获取宠物</summary>
    public List<PlayerPetData> GetPetsByRarity(int rarityId)
    {
        return _playerPets.Where(p => p.rarityId == rarityId).ToList();
    }

    /// <summary>按种类 ID 获取宠物</summary>
    public List<PlayerPetData> GetPetsByPetId(int petId)
    {
        return _playerPets.Where(p => p.petId == petId).ToList();
    }

    /// <summary>宠物总数</summary>
    public int GetPetCount() => _playerPets.Count;

    /// <summary>是否拥有出战宠物</summary>
    public bool HasActivePet() => _playerPets.Any(p => p.isActive);

    /// <summary>根据宠物种类ID获取种类名称</summary>
    public string GetPetName(int petId)
    {
        if (LoadDataManager.Instance == null) return $"宠物{petId}";
        var pet = LoadDataManager.Instance.GetPetById(petId);
        return pet?.name ?? $"宠物{petId}";
    }

    /// <summary>宠物显示名（优先昵称）</summary>
    public string GetPetDisplayName(PlayerPetData pet)
    {
        if (pet == null) return "";
        return string.IsNullOrEmpty(pet.nickname) ? pet.name : pet.nickname;
    }

    // ============================================================
    // 宠物栏 - 查询接口
    // ============================================================

    /// <summary>宠物栏是否已满</summary>
    public bool IsPetStorageFull()
    {
        if (_petStorageStatus == null) return false;
        return _petStorageStatus.isFull;
    }

    /// <summary>宠物栏剩余空格</summary>
    public int GetPetStorageRemaining() => _petStorageStatus?.remaining ?? 0;

    /// <summary>宠物栏容量</summary>
    public int GetPetStorageCapacity() => _petStorageStatus?.capacity ?? 20;

    /// <summary>宠物栏等级</summary>
    public int GetPetStorageLevel() => _petStorageStatus?.level ?? 1;

    /// <summary>宠物栏已用格子</summary>
    public int GetPetStorageUsed() => _petStorageStatus?.used ?? _playerPets.Count;

    /// <summary>宠物栏是否满级</summary>
    public bool IsPetStorageMaxLevel()
    {
        if (_petStorageStatus == null) return false;
        return _petStorageStatus.level >= _petStorageStatus.maxLevel;
    }

    // ============================================================
    // 图鉴 - 查询接口
    // ============================================================

    /// <summary>宠物图鉴是否已解锁</summary>
    public bool IsPetUnlockedInCollection(int petId)
    {
        return _petCollectionUnlocked.Contains(petId);
    }

    /// <summary>宠物图鉴已解锁数量</summary>
    public int GetPetCollectionUnlockedCount() => _petCollectionUnlocked.Count;

    /// <summary>宠物图鉴总种类数</summary>
    public int GetPetCollectionTotal() => _petCollectionTotal;

    /// <summary>宠物图鉴进度百分比</summary>
    public float GetPetCollectionProgress()
    {
        if (_petCollectionTotal <= 0) return 0f;
        return (float)_petCollectionUnlocked.Count / _petCollectionTotal * 100f;
    }

    /// <summary>获取所有已解锁的宠物种类ID</summary>
    public List<int> GetUnlockedPetIds() => _petCollectionUnlocked.ToList();

    // ============================================================
    // 数据更新入口（由 NetServerManager 调用）
    // ============================================================

    /// <summary>
    /// 更新宠物列表（逐个更新，保留对象引用）
    /// ✅ 不整个替换 List，避免 UI 持有快照时引用脱钩
    /// ✅ 按 petInstanceId 匹配，更新字段；不在 incoming 里的移除；不在 _playerPets 里的添加
    /// ✅ 方案 A：心跳看到 petHungerDirty=true 时，客户端调 FetchPlayerPets() 走这里更新
    /// </summary>
    public void UpdatePlayerPets(List<PlayerPetData> pets)
    {
        if (pets == null)
        {
            _playerPets.Clear();
            _activePetInstanceId = -1;
            _isPetDataLoaded = true;
            NotifyPetDataChanged(PetMessage.PetsUpdated);
            return;
        }

        // 1. 建立 incoming 索引
        var incoming = new Dictionary<int, PlayerPetData>();
        foreach (var p in pets)
        {
            if (p != null) incoming[p.petInstanceId] = p;
        }

        // 2. 移除不在 incoming 里的旧对象
        for (int i = _playerPets.Count - 1; i >= 0; i--)
        {
            if (!incoming.ContainsKey(_playerPets[i].petInstanceId))
            {
                _playerPets.RemoveAt(i);
            }
        }

        // 3. 逐个更新 / 添加
        foreach (var newPet in pets)
        {
            if (newPet == null) continue;

            var existing = _playerPets.FirstOrDefault(p => p.petInstanceId == newPet.petInstanceId);
            if (existing != null)
            {
                // ✅ 逐字段更新，保留对象引用
                existing.petId = newPet.petId;
                existing.rarityId = newPet.rarityId;
                existing.name = newPet.name;
                existing.nickname = newPet.nickname;
                existing.level = newPet.level;
                existing.exp = newPet.exp;
                existing.isActive = newPet.isActive;
                existing.isDefault = newPet.isDefault;
                existing.isLocked = newPet.isLocked;
                existing.obtainedAt = newPet.obtainedAt;
                existing.lastInsectCatchTime = newPet.lastInsectCatchTime;
                existing.nextInsectCatchTime = newPet.nextInsectCatchTime;
                existing.hunger = newPet.hunger;
                existing.maxHunger = newPet.maxHunger;
                existing.hungerRemainingSeconds = newPet.hungerRemainingSeconds;
            }
            else
            {
                _playerPets.Add(newPet);
            }
        }

        _isPetDataLoaded = true;

        // 4. 更新出战 ID
        var active = _playerPets.FirstOrDefault(p => p.isActive);
        _activePetInstanceId = active?.petInstanceId ?? -1;

        // 5. 补图鉴
        foreach (var p in _playerPets)
        {
            _petCollectionUnlocked.Add(p.petId);
        }

        Z_Logger.Log($"[PlayerDataManager] 宠物列表更新（逐个）: {_playerPets.Count} 只，出战: {_activePetInstanceId}");
        NotifyPetDataChanged(PetMessage.PetsUpdated);
    }

    // ❌ 已删除 UpdateLocalPetsHunger(List<PetHungerKV>)
    //    原因：方案 A 不再用心跳带全量 petHunger，客户端看到 petHungerDirty 就拉 /pets，
    //          走 UpdatePlayerPets 更新

    /// <summary>添加一只宠物（孵化后，去重）</summary>
    public void AddPet(PlayerPetData pet)
    {
        if (pet == null) return;

        // 去重：如果已存在同 instanceId，则替换
        int idx = _playerPets.FindIndex(p => p.petInstanceId == pet.petInstanceId);
        if (idx >= 0)
        {
            _playerPets[idx] = pet;
            Z_Logger.Log($"[PlayerDataManager] 宠物已存在，替换: instanceId={pet.petInstanceId}");
        }
        else
        {
            _playerPets.Add(pet);
        }

        if (pet.isActive) _activePetInstanceId = pet.petInstanceId;

        if (!_petCollectionUnlocked.Contains(pet.petId))
        {
            _petCollectionUnlocked.Add(pet.petId);
            NotifyPetDataChanged(PetMessage.PetCollectionUpdated);
        }

        Z_Logger.Log($"[PlayerDataManager] 添加宠物: instanceId={pet.petInstanceId}, petId={pet.petId}");
        NotifyPetDataChanged(PetMessage.PetsUpdated);
    }

    /// <summary>移除一只宠物（卖宠物后）</summary>
    public bool RemovePet(int instanceId)
    {
        int removed = _playerPets.RemoveAll(p => p.petInstanceId == instanceId);
        if (removed > 0)
        {
            if (_activePetInstanceId == instanceId) _activePetInstanceId = -1;

            // 同步宠物栏已用 -1
            if (_petStorageStatus != null)
            {
                _petStorageStatus.used = Math.Max(0, _petStorageStatus.used - 1);
                _petStorageStatus.remaining = Math.Max(0, _petStorageStatus.capacity - _petStorageStatus.used);
                _petStorageStatus.isFull = _petStorageStatus.used >= _petStorageStatus.capacity;
                NotifyPetDataChanged(PetMessage.PetStorageUpdated);
            }

            Z_Logger.Log($"[PlayerDataManager] 移除宠物: instanceId={instanceId}");
            NotifyPetDataChanged(PetMessage.PetsUpdated);
            return true;
        }
        return false;
    }

    /// <summary>更新宠物栏状态</summary>
    public void UpdatePetStorageStatus(PetStorageStatusData status)
    {
        _petStorageStatus = status;
        _isPetStorageLoaded = true;
        Z_Logger.Log($"[PlayerDataManager] 宠物栏状态更新: Lv.{status?.level}, {status?.used}/{status?.capacity}");
        NotifyPetDataChanged(PetMessage.PetStorageUpdated);
    }

    /// <summary>更新宠物图鉴（服务器返回的完整列表）</summary>
    public void UpdatePetCollection(List<int> unlockedPetIds, int totalCount = 0)
    {
        _petCollectionUnlocked = new HashSet<int>(unlockedPetIds ?? new List<int>());
        if (totalCount > 0) _petCollectionTotal = totalCount;

        Z_Logger.Log($"[PlayerDataManager] 宠物图鉴更新: {_petCollectionUnlocked.Count}/{_petCollectionTotal}");
        NotifyPetDataChanged(PetMessage.PetCollectionUpdated);
    }

    // ============================================================
    // 本地状态更新（网络请求成功后由上层调用）
    // ============================================================

    /// <summary>本地标记宠物为出战</summary>
    public void SetLocalActivePet(int instanceId)
    {
        foreach (var p in _playerPets)
            p.isActive = (p.petInstanceId == instanceId);

        _activePetInstanceId = instanceId;
        Z_Logger.Log($"[PlayerDataManager] 本地设置出战宠物: instanceId={instanceId}");
        NotifyPetDataChanged(PetMessage.PetActiveChanged);
        NotifyPetDataChanged(PetMessage.PetsUpdated);
    }

    /// <summary>本地更新宠物昵称</summary>
    public void UpdateLocalPetNickname(int instanceId, string nickname)
    {
        var pet = GetPetByInstanceId(instanceId);
        if (pet != null)
        {
            pet.nickname = nickname;
            Z_Logger.Log($"[PlayerDataManager] 本地更新宠物昵称: instanceId={instanceId}, nickname={nickname}");
            NotifyPetDataChanged(PetMessage.PetRenamed);
            NotifyPetDataChanged(PetMessage.PetsUpdated);
        }
    }

    /// <summary>本地更新宠物饥饿度（喂食后调用，方案 A 保留）</summary>
    public void UpdateLocalPetHunger(int instanceId, int newHunger)
    {
        var pet = GetPetByInstanceId(instanceId);
        if (pet != null)
        {
            pet.hunger = newHunger;
            NotifyPetDataChanged(PetMessage.PetHungerChanged);
            NotifyPetDataChanged(PetMessage.PetsUpdated);
        }
    }

    /// <summary>本地更新宠物锁定状态</summary>
    public void UpdateLocalPetLocked(int instanceId, bool isLocked)
    {
        var pet = GetPetByInstanceId(instanceId);
        if (pet != null)
        {
            pet.isLocked = isLocked;
            Z_Logger.Log($"[PlayerDataManager] 本地更新宠物锁定状态: instanceId={instanceId}, isLocked={isLocked}");
            NotifyPetDataChanged(PetMessage.PetLockChanged);
            NotifyPetDataChanged(PetMessage.PetsUpdated);
        }
    }

    /// <summary>本地更新宠物栏等级和容量（升级成功后）</summary>
    public void UpdateLocalPetStorageLevel(int newLevel, int newCapacity)
    {
        if (_petStorageStatus != null)
        {
            _petStorageStatus.level = newLevel;
            _petStorageStatus.capacity = newCapacity;
            _petStorageStatus.remaining = Math.Max(0, newCapacity - _petStorageStatus.used);
            _petStorageStatus.isFull = _petStorageStatus.used >= newCapacity;
        }
        NotifyPetDataChanged(PetMessage.PetStorageUpdated);
    }

    /// <summary>本地增加宠物栏已用格子（孵化领取后）</summary>
    public void LocalIncreasePetStorageUsed(int count = 1)
    {
        if (_petStorageStatus != null && count > 0)
        {
            _petStorageStatus.used += count;
            _petStorageStatus.remaining = Math.Max(0, _petStorageStatus.capacity - _petStorageStatus.used);
            _petStorageStatus.isFull = _petStorageStatus.used >= _petStorageStatus.capacity;
            Z_Logger.Log($"[PlayerDataManager] 宠物栏使用: {_petStorageStatus.used}/{_petStorageStatus.capacity}");
            NotifyPetDataChanged(PetMessage.PetStorageUpdated);
        }
    }

    /// <summary>本地减少宠物栏已用格子（卖宠物后，RemovePet 里已处理，这个留给批量/放生）</summary>
    public void LocalDecreasePetStorage(int count = 1)
    {
        if (_petStorageStatus != null && count > 0)
        {
            _petStorageStatus.used = Math.Max(0, _petStorageStatus.used - count);
            _petStorageStatus.remaining = Math.Max(0, _petStorageStatus.capacity - _petStorageStatus.used);
            _petStorageStatus.isFull = _petStorageStatus.used >= _petStorageStatus.capacity;
            Z_Logger.Log($"[PlayerDataManager] 宠物栏释放: {_petStorageStatus.used}/{_petStorageStatus.capacity}");
            NotifyPetDataChanged(PetMessage.PetStorageUpdated);
        }
    }

    // ============================================================
    // 自动喂食 - 本地更新
    // ============================================================

    public void UpdateAutoFeedStatus(AutoFeedStatusData status)
    {
        _autoFeedStatus = status;
        Z_Logger.Log($"[PlayerDataManager] 自动喂食状态更新: Lv.{status?.level}, enabled={status?.enabled}, remaining={status?.remainingSeconds}s");
        NotifyPetDataChanged(PetMessage.AutoFeedStatusUpdated);
    }

    public void UpdateLocalAutoFeedEnabled(bool enabled)
    {
        if (_autoFeedStatus != null)
        {
            _autoFeedStatus.enabled = enabled;
        }
        NotifyPetDataChanged(PetMessage.AutoFeedStatusUpdated);
    }

    public void UpdateAutoFeedFilter(AutoFeedFilterData filter)
    {
        _autoFeedFilter = filter;
        Z_Logger.Log($"[PlayerDataManager] 自动喂食过滤配置更新");
        NotifyPetDataChanged(PetMessage.AutoFeedFilterUpdated);
    }

    // ============================================================
    // 数据清理
    // ============================================================

    /// <summary>初始化宠物数据（登出/切换账号时调用）</summary>
    public void InitPetData()
    {
        _playerPets.Clear();
        _petCollectionUnlocked.Clear();
        _petStorageStatus = null;
        _autoFeedStatus = null;
        _autoFeedFilter = null;
        _activePetInstanceId = -1;
        _isPetDataLoaded = false;
        _isPetStorageLoaded = false;
        _petCollectionTotal = 0;
        Z_Logger.Log("[PlayerDataManager] 宠物数据已初始化");
    }

    /// <summary>清空宠物数据</summary>
    public void ClearPetData()
    {
        InitPetData();
        Z_Logger.Log("[PlayerDataManager] 宠物数据已清空");
    }

    // ============================================================
    // 内部通知
    // ============================================================

    private void NotifyPetDataChanged(PetMessage message)
    {
        CommunicateEvent.Modify(message.ToString());
    }

    // ============================================================
    // 调试
    // ============================================================

    public void DebugPrintPetData()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 宠物数据 Debug =====");
        sb.AppendLine($"已加载: {_isPetDataLoaded}");
        sb.AppendLine($"宠物总数: {_playerPets.Count}");
        sb.AppendLine($"出战宠物 ID: {_activePetInstanceId}");
        foreach (var pet in _playerPets)
        {
            sb.AppendLine($"  [{pet.petInstanceId}] {GetPetDisplayName(pet)} (petId={pet.petId}, 稀有度={pet.rarityId}, 饥饿度={pet.hunger}/{pet.maxHunger}, 出战={pet.isActive}, 默认={pet.isDefault}, 锁定={pet.isLocked})");
        }
        sb.AppendLine($"宠物栏: Lv.{_petStorageStatus?.level}, {_petStorageStatus?.used}/{_petStorageStatus?.capacity}");
        sb.AppendLine($"宠物图鉴: {_petCollectionUnlocked.Count}/{_petCollectionTotal}");
        sb.AppendLine($"自动喂食: Lv.{_autoFeedStatus?.level}, enabled={_autoFeedStatus?.enabled}, remaining={_autoFeedStatus?.remainingSeconds}s");
        Z_Logger.Log(sb.ToString());
    }
}
