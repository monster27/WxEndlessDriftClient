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

    // 装饰缓存
    private int _cachedDecorationHash = 0;
    private Dictionary<int, int> _cachedEquippedHashes = new Dictionary<int, int>();

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

        CommunicateEvent.Register(FishTankMessage.PlayerDataUpdated.ToString(), OnPlayerDataUpdated);

        CommunicateEvent.Register(FishTankMessage.OpenFishTank.ToString(), OnOpenFishTank);
        CommunicateEvent.Register(FishTankMessage.CloseFishTank.ToString(), OnCloseFishTank);
        CommunicateEvent.Register(FishTankMessage.RefreshFishTank.ToString(), OnRefreshFishTank);
        CommunicateEvent.Register(FishTankMessage.SwitchTank.ToString(), OnSwitchTank);
        CommunicateEvent.Register<int>(FishTankMessage.UnlockTank.ToString(), OnUnlockTank);
        CommunicateEvent.Register(FishTankMessage.ToggleManagerPanel.ToString(), OnToggleManagerPanel);

        CommunicateEvent.Register(FishTankMessage.DataLoaded.ToString(), OnDataLoaded);

        CommunicateEvent.Register<TransferData>(FishTankMessage.TransferFish.ToString(), OnTransferFish);

        // ===== 装饰相关消息注册 =====
        CommunicateEvent.Register<EquipDecorationData>(FishTankMessage.EquipDecoration.ToString(), OnEquipDecoration);
        CommunicateEvent.Register<RemoveDecorationData>(FishTankMessage.RemoveDecoration.ToString(), OnRemoveDecoration);
        CommunicateEvent.Register<MirrorDecorationData>(FishTankMessage.MirrorDecoration.ToString(), OnMirrorDecoration);
        CommunicateEvent.Register<MoveDecorationData>(FishTankMessage.MoveDecoration.ToString(), OnMoveDecoration);
        CommunicateEvent.Register<SelectDecorationData>(FishTankMessage.SelectDecoration.ToString(), OnSelectDecoration);
        CommunicateEvent.Register<ApplyDecorationData>(FishTankMessage.ApplyTextureDecoration.ToString(), OnApplyTextureDecoration);
        CommunicateEvent.Register(FishTankMessage.EnterDecorationMode.ToString(), OnEnterDecorationMode);
        CommunicateEvent.Register(FishTankMessage.ExitDecorationMode.ToString(), OnExitDecorationMode);

        LogDebug("事件注册完成（含装饰）");
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister(FishTankMessage.PlayerDataUpdated.ToString(), OnPlayerDataUpdated);
        CommunicateEvent.Unregister(FishTankMessage.OpenFishTank.ToString(), OnOpenFishTank);
        CommunicateEvent.Unregister(FishTankMessage.CloseFishTank.ToString(), OnCloseFishTank);
        CommunicateEvent.Unregister(FishTankMessage.RefreshFishTank.ToString(), OnRefreshFishTank);
        CommunicateEvent.Unregister(FishTankMessage.SwitchTank.ToString(), OnSwitchTank);
        CommunicateEvent.Unregister<TransferData>(FishTankMessage.TransferFish.ToString(), OnTransferFish);
        CommunicateEvent.Unregister<int>(FishTankMessage.UnlockTank.ToString(), OnUnlockTank);
        CommunicateEvent.Unregister(FishTankMessage.ToggleManagerPanel.ToString(), OnToggleManagerPanel);
        CommunicateEvent.Unregister(FishTankMessage.DataLoaded.ToString(), OnDataLoaded);

        CommunicateEvent.Unregister<EquipDecorationData>(FishTankMessage.EquipDecoration.ToString(), OnEquipDecoration);
        CommunicateEvent.Unregister<RemoveDecorationData>(FishTankMessage.RemoveDecoration.ToString(), OnRemoveDecoration);
        CommunicateEvent.Unregister<MirrorDecorationData>(FishTankMessage.MirrorDecoration.ToString(), OnMirrorDecoration);
        CommunicateEvent.Unregister<MoveDecorationData>(FishTankMessage.MoveDecoration.ToString(), OnMoveDecoration);
        CommunicateEvent.Unregister<SelectDecorationData>(FishTankMessage.SelectDecoration.ToString(), OnSelectDecoration);
        CommunicateEvent.Unregister<ApplyDecorationData>(FishTankMessage.ApplyTextureDecoration.ToString(), OnApplyTextureDecoration);
        CommunicateEvent.Unregister(FishTankMessage.EnterDecorationMode.ToString(), OnEnterDecorationMode);
        CommunicateEvent.Unregister(FishTankMessage.ExitDecorationMode.ToString(), OnExitDecorationMode);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 7. 消息处理器（原有）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnOpenFishTank()
    {
        LogDebug("收到 OpenFishTank 消息");
        if (IsDataReady()) NotifyView(FishTankMessage.DataUpdated);
        else LogDebug("数据尚未加载，等待 DataLoaded");
    }

    private void OnCloseFishTank()
    {
        LogDebug("收到 CloseFishTank 消息");
    }

    private void OnRefreshFishTank()
    {
        LogDebug("收到 RefreshFishTank 消息");
        if (IsDataReady()) NotifyView(FishTankMessage.DataUpdated);
    }

    private void OnSwitchTank()
    {
        LogDebug("收到 SwitchTank 消息");
        if (IsDataReady()) NotifyView(FishTankMessage.DataUpdated);
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

        if (transferData.IsFromBag && !transferData.IsToBag)
        {
            int tankId = transferData.ToIndex - 1;
            if (tankId < 0) { LogDebug("无效的目标鱼缸索引"); return; }
            NetServerManager.Instance?.MoveFishFromBagToTank(tankId, transferData.FishData.id, (success, message) =>
            {
                if (success) LogDebug("鱼篓→鱼缸转移成功");
                else LogDebug($"鱼篓→鱼缸转移失败: {message}");
            });
        }
        else if (!transferData.IsFromBag && transferData.IsToBag)
        {
            NetServerManager.Instance?.MoveFishFromTankToBag(transferData.FishData.id, (success, message) =>
            {
                if (success) LogDebug("鱼缸→鱼篓转移成功");
                else LogDebug($"鱼缸→鱼篓转移失败: {message}");
            });
        }
        else if (!transferData.IsFromBag && !transferData.IsToBag)
        {
            int fromTankId = transferData.FromIndex - 1;
            int toTankId = transferData.ToIndex - 1;
            if (fromTankId < 0 || toTankId < 0) { LogDebug("无效的鱼缸索引"); return; }
            NetServerManager.Instance?.MoveFishFromTankToTank(fromTankId, toTankId, transferData.FishData.id, (success, message) =>
            {
                if (success) LogDebug("鱼缸→鱼缸转移成功");
                else LogDebug($"鱼缸→鱼缸转移失败: {message}");
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
        NetServerManager.Instance?.OnUnlockFishTankRequest(tankId);
    }

    private void OnToggleManagerPanel()
    {
        LogDebug("收到 ToggleManagerPanel 消息");
    }

    private void OnDataLoaded()
    {
        LogDebug("收到 DataLoaded 消息，数据已就绪");
        NotifyView(FishTankMessage.DataUpdated);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 8. 装饰消息处理器
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnEquipDecoration(EquipDecorationData data)
    {
        if (NetServerManager.Instance == null) return;

        NetServerManager.Instance.EquipDecoration(
            data.TankId, data.Category, data.DecorationId,
            data.PosX, data.PosY, data.PosZ,
            data.ScaleX, data.ScaleY, data.ScaleZ,
            data.RotX, data.RotY, data.RotZ,
            (success, message, newRecordId) =>
            {
                if (!success)
                {
                    GameUIManager.ShowMessage($"装备失败: {message}");
                    return;
                }

                // 第 1 步：等背包刷新完（并且已同步到 PlayerDataManager）
                NetServerManager.Instance.RefreshPlayerInventoryAndSync(() =>
                {
                    // 第 2 步：等装备状态刷新完
                    NetServerManager.Instance.FetchEquippedStatus(data.TankId, (equipped) =>
                    {
                        if (equipped != null && PlayerDataManager.Instance != null)
                            PlayerDataManager.Instance.UpdateEquippedDecorations(data.TankId, equipped);

                        // 第 3 步：两个数据都 OK，只在这里触发一次 UI 刷新
                        CommunicateEvent.Modify(FishTankMessage.DecorationDataUpdated.ToString());
                    });
                });

                GameUIManager.ShowMessage("装备成功");
            });
    }

    private void OnRemoveDecoration(RemoveDecorationData data)
    {
        if (NetServerManager.Instance == null) return;

        NetServerManager.Instance.UnEquipDecoration(data.TankId, data.RecordId, (success, message) =>
        {
            if (!success)
            {
                GameUIManager.ShowMessage($"卸下失败: {message}");
                return;
            }

            NetServerManager.Instance.RefreshPlayerInventoryAndSync(() =>
            {
                if (PlayerDataManager.Instance != null)
                    PlayerDataManager.Instance.RemoveEquippedDecoration(data.TankId, data.RecordId);

                // 只触发一次 UI 刷新
                CommunicateEvent.Modify(FishTankMessage.DecorationDataUpdated.ToString());
            });

            GameUIManager.ShowMessage("已卸下装饰");
        });
    }

    private void OnMirrorDecoration(MirrorDecorationData data)
    {
        LogDebug($"收到 MirrorDecoration: TankId={data.TankId}, RecordId={data.RecordId}");

        var info = GetDecorationInfoByRecordId(data.RecordId);
        if (info == null) return;

        float newRotation = (info.RotationY == 0) ? 180 : 0;

        NetServerManager.Instance?.MirrorDecoration(data.TankId, data.RecordId, newRotation, (success, message) =>
        {
            if (success)
            {
                PlayerDataManager.Instance?.UpdateDecorationMirror(data.TankId, data.RecordId, newRotation);
                CommunicateEvent.Modify(FishTankMessage.DecorationDataUpdated.ToString());
            }
            else
            {
                LogDebug($"镜像失败: {message}");
                GameUIManager.ShowMessage($"镜像失败: {message}");
            }
        });
    }

    private void OnMoveDecoration(MoveDecorationData data)
    {
        LogDebug($"收到 MoveDecoration: TankId={data.TankId}, RecordId={data.RecordId}, Delta=({data.DeltaX:F2}, {data.DeltaY:F2})");

        var info = GetDecorationInfoByRecordId(data.RecordId);
        if (info == null) return;

        float newX = info.PositionX + data.DeltaX;
        float newY = info.PositionY + data.DeltaY;

        NetServerManager.Instance?.MoveDecoration(data.TankId, data.RecordId, newX, newY, (success, message) =>
        {
            if (success)
            {
                PlayerDataManager.Instance?.UpdateDecorationPosition(data.TankId, data.RecordId, newX, newY);
                CommunicateEvent.Modify(FishTankMessage.DecorationDataUpdated.ToString());
            }
            else
            {
                LogDebug($"移动失败: {message}");
            }
        });
    }

    private void OnSelectDecoration(SelectDecorationData data)
    {
        LogDebug($"收到 SelectDecoration: RecordId={data.RecordId}, Category={data.Category}");
        var showData = new ShowDecOperatorData
        {
            TankId = data.TankId,
            RecordId = data.RecordId,
            ScreenPosition = data.ScreenPosition
        };
        CommunicateEvent.Modify(FishTankMessage.ShowDecOperator.ToString(), showData);
    }

    private void OnApplyTextureDecoration(ApplyDecorationData data)
    {
        LogDebug($"收到 ApplyTextureDecoration: TankId={data.TankId}, Category={data.Category}, DecId={data.DecorationId}");
        NetServerManager.Instance?.ApplyTextureDecoration(data.TankId, data.Category, data.DecorationId, (success, message) =>
        {
            if (success)
            {
                LogDebug($"应用纹理成功: {message}");
                CommunicateEvent.Modify(FishTankMessage.DecorationDataUpdated.ToString());
                GameUIManager.ShowMessage("应用成功");
            }
            else
            {
                LogDebug($"应用纹理失败: {message}");
                GameUIManager.ShowMessage($"应用失败: {message}");
            }
        });
    }

    private void OnEnterDecorationMode()
    {
        LogDebug("进入装饰模式");
    }

    private void OnExitDecorationMode()
    {
        LogDebug("退出装饰模式");
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 9. DataManager数据变化处理（防抖）
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
    // 10. 数据变化检测
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
        bool decorationChanged = CheckDecorationChanges();

        if (bagChanged || tankChanged || decorationChanged)
        {
            LogDebug($"数据发生变化: bagChanged={bagChanged}, tankChanged={tankChanged}, decorationChanged={decorationChanged}");
            NotifyView(FishTankMessage.DataUpdated);

            if (decorationChanged)
                CommunicateEvent.Modify(FishTankMessage.DecorationDataUpdated.ToString());
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

    private bool CheckDecorationChanges()
    {
        if (PlayerDataManager.Instance == null) return false;

        // ★ 用装饰物品的背包数量做 hash（数量变化也触发刷新）
        var inventory = PlayerDataManager.Instance.GetInventory();
        int newHash = CalculateDecorationInventoryHash(inventory);
        bool changed = newHash != _cachedDecorationHash;
        if (changed) _cachedDecorationHash = newHash;

        // 装备状态 hash（保持不变）
        var tanks = PlayerDataManager.Instance.GetAllFishTankStatusOrdered();
        var currentEquippedHashes = new Dictionary<int, int>();
        foreach (var tank in tanks)
        {
            var equipped = PlayerDataManager.Instance.GetEquippedDecorations(tank.tankId);
            int hash = CalculateEquippedHash(equipped);
            currentEquippedHashes[tank.tankId] = hash;

            if (!_cachedEquippedHashes.TryGetValue(tank.tankId, out int oldHash) || oldHash != hash)
                changed = true;
        }

        if (changed) _cachedEquippedHashes = currentEquippedHashes;
        return changed;
    }

    /// <summary>
    /// 计算装饰物品（ID 8001-8999）的背包数量 hash
    /// </summary>
    private int CalculateDecorationInventoryHash(Dictionary<int, int> inventory)
    {
        if (inventory == null || inventory.Count == 0) return 0;
        int hash = 0;
        foreach (var kvp in inventory)
        {
            if (kvp.Key >= 8001 && kvp.Key <= 8999)
            {
                hash ^= (kvp.Key * 397) ^ kvp.Value;
            }
        }
        return hash;
    }

    private int CalculateEquippedHash(Dictionary<int, List<DecorationEquipInfo>> equipped)
    {
        if (equipped == null || equipped.Count == 0) return 0;
        int hash = 0;
        foreach (var kvp in equipped)
        {
            hash ^= kvp.Key.GetHashCode();
            foreach (var info in kvp.Value)
                hash ^= info.Id.GetHashCode();
        }
        return hash;
    }

    private int CalculateFishListHash(List<FishDetailData> list)
    {
        if (list == null || list.Count == 0) return 0;
        int hash = 0;
        foreach (var fish in list)
            if (fish != null) hash ^= fish.id.GetHashCode();
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
        if (PlayerDataManager.Instance == null) return null;
        return PlayerDataManager.Instance.GetFishTankStatus(tankId);
    }

    public List<FishDetailData> GetTankFishList(int tankId)
    {
        if (PlayerDataManager.Instance == null) return new List<FishDetailData>();
        return PlayerDataManager.Instance.GetFishTankItems(tankId);
    }

    public List<FishDetailData> GetBagFishList()
    {
        if (PlayerDataManager.Instance == null) return new List<FishDetailData>();
        return PlayerDataManager.Instance.GetFishBagList() ?? new List<FishDetailData>();
    }

    public int GetBagCapacity()
    {
        if (PlayerDataManager.Instance == null) return 10;
        return PlayerDataManager.Instance.fishBagCapacity;
    }

    public int GetBagRemaining()
    {
        if (PlayerDataManager.Instance == null) return 0;
        return PlayerDataManager.Instance.GetFishBagRemaining();
    }

    public bool IsBagFull() => GetBagRemaining() <= 0;

    public bool IsTankUnlocked(int tankId)
    {
        if (PlayerDataManager.Instance == null) return false;
        return PlayerDataManager.Instance.IsFishTankUnlocked(tankId);
    }

    public int GetTankCapacity(int tankId)
    {
        if (PlayerDataManager.Instance == null) return 10;
        return PlayerDataManager.Instance.GetFishTankCapacity(tankId);
    }

    public int GetTankRemaining(int tankId)
    {
        if (PlayerDataManager.Instance == null) return 0;
        return PlayerDataManager.Instance.GetFishTankRemaining(tankId);
    }

    public bool CanAddFishToTank(int tankId) => GetTankRemaining(tankId) > 0 && IsTankUnlocked(tankId);

    public bool IsDataReady()
    {
        if (PlayerDataManager.Instance == null) return false;
        return PlayerDataManager.Instance.IsFishDataLoaded;
    }

    public int GetTankCount() => GetTankList().Count;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 12. 装饰数据查询接口
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public List<int> GetOwnedDecorationIds()
    {
        if (PlayerDataManager.Instance == null) return new List<int>();
        return PlayerDataManager.Instance.GetOwnedDecorationIds();
    }

    public bool HasDecoration(int decorationId)
    {
        if (PlayerDataManager.Instance == null) return false;
        return PlayerDataManager.Instance.HasDecoration(decorationId);
    }

    /// <summary>
    /// ★ 新增：从背包数据获取装饰的拥有数量
    /// </summary>
    public int GetOwnedDecorationQuantity(int decorationId)
    {
        if (PlayerDataManager.Instance == null) return 0;
        var inventory = PlayerDataManager.Instance.GetInventory();
        if (inventory != null && inventory.TryGetValue(decorationId, out int q))
            return q;
        return 0;
    }

    public Dictionary<int, List<DecorationEquipInfo>> GetEquippedDecorations(int tankId)
    {
        if (PlayerDataManager.Instance == null) return new Dictionary<int, List<DecorationEquipInfo>>();
        return PlayerDataManager.Instance.GetEquippedDecorations(tankId);
    }

    public List<DecorationEquipInfo> GetEquippedDecorationsByCategory(int tankId, int category)
    {
        if (PlayerDataManager.Instance == null) return new List<DecorationEquipInfo>();
        return PlayerDataManager.Instance.GetEquippedDecorationsByCategory(tankId, category);
    }

    public List<DecorationEquipInfo> GetEquippedMovableDecorations(int tankId)
    {
        if (PlayerDataManager.Instance == null) return new List<DecorationEquipInfo>();
        return PlayerDataManager.Instance.GetEquippedMovableDecorations(tankId);
    }

    public DecorationEquipInfo GetDecorationInfoByRecordId(int recordId)
    {
        if (PlayerDataManager.Instance == null) return null;
        return PlayerDataManager.Instance.GetDecorationInfoByRecordId(recordId);
    }

    public List<FishTankDecData> GetDecorationConfigsByCategory(int category)
    {
        if (LoadDataManager.Instance == null) return new List<FishTankDecData>();
        return LoadDataManager.Instance.fishTankDecorations?
            .Where(d => d.categoryId == category).ToList() ?? new List<FishTankDecData>();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 13. UI展示数据接口
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public FishTankDisplayData GetTankDisplayData(int tankIndex)
    {
        var tanks = GetTankList();
        if (tankIndex < 0 || tankIndex >= tanks.Count) return null;

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

        if (PlayerDataManager.Instance == null)
        {
            return new FishTankStoreData
            {
                IsBag = (index == 0),
                Name = "加载中...",
                FishList = new List<FishDetailData>(),
                MaxCapacity = 10,
                IsUnlocked = false
            };
        }

        if (index == 0)
        {
            var bagList = PlayerDataManager.Instance.GetFishBagList();
            int capacity = PlayerDataManager.Instance.GetFishBagCapacity();
            return new FishTankStoreData
            {
                IsBag = true,
                Name = "鱼篓",
                FishList = new List<FishDetailData>(bagList),
                MaxCapacity = capacity,
                IsUnlocked = true
            };
        }

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
        return new FishTankStoreData
        {
            IsBag = false,
            TankId = tankId,
            Name = name,
            FishList = new List<FishDetailData>(tankList),
            MaxCapacity = status.capacity,
            IsUnlocked = status.isUnlocked
        };
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 14. 通知View
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void NotifyView(FishTankMessage message)
    {
        LogDebug($"通知View: {message}");
        CommunicateEvent.Modify(message.ToString());
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 15. 日志辅助
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
