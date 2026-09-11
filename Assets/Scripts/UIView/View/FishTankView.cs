// 路径：Assets/Scripts/UIView/View/FishTankView.cs
using UnityEngine;
using System.Collections.Generic;
using static PlayerDataManager;

public class FishTankView : BaseView
{
    [Header("===== 调试 =====")]
    public bool enableDebugLog = false;

    [Header("===== 子面板 =====")]
    [SerializeField] private FishTankMainPanel mainPanel;
    [SerializeField] private FishTankMainOperatePanel mainOperatePanel;
    [SerializeField] private FishTankManagerPanel managerPanel;
    [SerializeField] private FishTankDecorationPanel decorationPanel;

    private int _currentTankIndex = 0;
    private bool _isManagerOpen = false;
    private bool _isDecorationMode = false;

    public bool EnableDebugLog => enableDebugLog;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 初始化
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public override void BaseViewInit()
    {
        if (isInitialized) return;
        base.BaseViewInit();

        if (mainPanel != null)
        {
            mainPanel.Init(enableDebugLog);
            mainPanel.OnDecorationClickedCallback = OnDecorationInstanceClicked;
        }
        else Z_Logger.LogError("[FishTankView] mainPanel 未绑定");

        if (mainOperatePanel != null)
        {
            mainOperatePanel.Init();
            mainOperatePanel.SetLeftCallback(OnLeftTankClicked);
            mainOperatePanel.SetRightCallback(OnRightTankClicked);
            mainOperatePanel.SetLockCallback(OnLockClicked);
            mainOperatePanel.SetManageCallback(OnManageClicked);
            mainOperatePanel.SetDecorationCallback(OnDecorationClicked);
        }
        else Z_Logger.LogError("[FishTankView] mainOperatePanel 未绑定");

        if (managerPanel != null)
        {
            managerPanel.Init(enableDebugLog);
            managerPanel.SetTransferCallback(OnFishTransferRequest);
            managerPanel.SetUnlockCallback(OnUnlockRequest);
            managerPanel.SetCloseCallback(OnManagerPanelClosed);
            managerPanel.ClosePanel();
        }
        else Z_Logger.LogError("[FishTankView] managerPanel 未绑定");

        if (decorationPanel != null)
        {
            decorationPanel.Init(enableDebugLog);
            decorationPanel.SetMainPanel(mainPanel);
            decorationPanel.SetCloseCallback(OnDecorationPanelClosed);
            decorationPanel.SetItemClickCallback(OnDecorationItemClicked);
            decorationPanel.SetOperatorCallbacks(
                OnDecDragMove,
                OnDecDragEnd,
                OnDecMirrorClicked,
                OnDecRemoveClicked
            );
            decorationPanel.Close();
        }
        else Z_Logger.LogError("[FishTankView] decorationPanel 未绑定");

        RegisterEvents();
        isInitialized = true;
        LogDebug("BaseViewInit 完成");
    }

    private void RegisterEvents()
    {
        UnregisterEvents();
        CommunicateEvent.Register(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
        CommunicateEvent.Register(FishTankMessage.DecorationDataUpdated.ToString(), OnDecorationDataUpdated);
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
        CommunicateEvent.Unregister(FishTankMessage.DecorationDataUpdated.ToString(), OnDecorationDataUpdated);
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 打开/关闭
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void OpenFishTank()
    {
        if (!isInitialized) BaseViewInit();

        ShowView();
        _currentTankIndex = 0;
        _isManagerOpen = false;
        _isDecorationMode = false;

        if (mainPanel != null)
        {
            mainPanel.OpenPanel();
            mainPanel.SetFishVisible(true);
            mainPanel.SetBaitVisible(true);

            mainPanel.SetDecorationMode(false);
        }

        if (mainOperatePanel != null) mainOperatePanel.OpenPanel();
        if (managerPanel != null) managerPanel.ClosePanel();
        if (decorationPanel != null) decorationPanel.Close();

        RefreshOperatePanel();
        if (mainPanel != null) mainPanel.RefreshFishTank(_currentTankIndex);

        RenderDecorationsForCurrentTank();
        SyncCurrentTankDecorations();

        CommunicateEvent.Modify(FishTankMessage.OpenFishTank.ToString());
    }

    public void CloseFishTank()
    {
        if (mainPanel != null)
        {
            mainPanel.SetFishVisible(false);
            mainPanel.SetBaitVisible(false);
            mainPanel.ClosePanel();
        }
        if (mainOperatePanel != null) mainOperatePanel.ClosePanel();
        if (managerPanel != null) managerPanel.ClosePanel();
        if (decorationPanel != null) decorationPanel.Close();

        _isManagerOpen = false;
        _isDecorationMode = false;

        HideView();
        CommunicateEvent.Modify(FishTankMessage.CloseFishTank.ToString());
    }

    protected override void OnCloseButtonClick()
    {
        base.OnCloseButtonClick();
        CloseFishTank();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 对外：刷新
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void RefreshAll()
    {
        if (!gameObject.activeInHierarchy) return;

        RefreshOperatePanel();
        if (mainPanel != null) mainPanel.RefreshFishTank(_currentTankIndex);
        if (managerPanel != null && managerPanel.gameObject.activeSelf) managerPanel.RefreshData();
        if (decorationPanel != null && decorationPanel.gameObject.activeSelf) decorationPanel.RefreshData();
        RenderDecorationsForCurrentTank();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 事件
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnDataUpdated()
    {
        if (!gameObject.activeInHierarchy) return;
        RefreshOperatePanel();
        if (mainPanel != null) mainPanel.RefreshFishTank(_currentTankIndex);
    }

    private void OnDecorationDataUpdated()
    {
        if (!gameObject.activeInHierarchy) return;
        RenderDecorationsForCurrentTank();
        if (decorationPanel != null && decorationPanel.gameObject.activeSelf)
            decorationPanel.RefreshData();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 顶部 UI
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void RefreshOperatePanel()
    {
        if (mainOperatePanel == null) return;

        var tanks = PlayerDataService.Instance?.GetTankList() ?? new List<FishTankStatusData>();
        if (tanks.Count == 0) { mainOperatePanel.SetEmpty(); return; }

        if (_currentTankIndex >= tanks.Count) _currentTankIndex = tanks.Count - 1;
        if (_currentTankIndex < 0) _currentTankIndex = 0;

        mainOperatePanel.RefreshTopUI(tanks[_currentTankIndex], tanks.Count > 1);
    }

    private FishTankStatusData GetCurrentTank()
    {
        var tanks = PlayerDataService.Instance?.GetTankList() ?? new List<FishTankStatusData>();
        if (tanks.Count == 0) return null;
        if (_currentTankIndex >= tanks.Count) _currentTankIndex = tanks.Count - 1;
        if (_currentTankIndex < 0) _currentTankIndex = 0;
        return tanks[_currentTankIndex];
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 顶部按钮
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnLeftTankClicked()
    {
        var tanks = PlayerDataService.Instance?.GetTankList() ?? new List<FishTankStatusData>();
        if (tanks.Count <= 1) return;

        if (mainPanel != null) mainPanel.ClearAllBaits();
        _currentTankIndex = (_currentTankIndex - 1 + tanks.Count) % tanks.Count;

        CommunicateEvent.Modify(FishTankMessage.SwitchTank.ToString(), _currentTankIndex);

        RefreshOperatePanel();
        if (mainPanel != null) mainPanel.RefreshFishTank(_currentTankIndex);

        RenderDecorationsForCurrentTank();
        SyncCurrentTankDecorations();
    }

    private void OnRightTankClicked()
    {
        var tanks = PlayerDataService.Instance?.GetTankList() ?? new List<FishTankStatusData>();
        if (tanks.Count <= 1) return;

        if (mainPanel != null) mainPanel.ClearAllBaits();
        _currentTankIndex = (_currentTankIndex + 1) % tanks.Count;

        CommunicateEvent.Modify(FishTankMessage.SwitchTank.ToString(), _currentTankIndex);

        RefreshOperatePanel();
        if (mainPanel != null) mainPanel.RefreshFishTank(_currentTankIndex);

        RenderDecorationsForCurrentTank();
        SyncCurrentTankDecorations();
    }

    private void OnLockClicked()
    {
        var tank = GetCurrentTank();
        if (tank == null || tank.isUnlocked) return;
        var config = LoadDataManager.Instance?.GetFishTankConfig(tank.tankId);
        if (config == null) return;

        GameUIManager.Instance?.ShowDialog(
            $"花费 {config.purchaseCost} 金币解锁 {config.name}？",
            DialogType.Info,
            () =>
            {
                CommunicateEvent.Modify(FishTankMessage.UnlockTank.ToString(), tank.tankId);
                GameUIManager.ShowMessage("解锁请求已发送");
            }
        );
    }

    private void OnManageClicked()
    {
        if (_isManagerOpen) return;
        if (_isDecorationMode) ExitDecorationMode();

        _isManagerOpen = true;
        if (mainOperatePanel != null) mainOperatePanel.ClosePanel();
        if (mainPanel != null)
        {
            mainPanel.SetFishVisible(false);
            mainPanel.SetBaitVisible(false);
            mainPanel.ClearAllBaits();
        }
        if (managerPanel != null) managerPanel.OpenPanel();

        CommunicateEvent.Modify(FishTankMessage.ToggleManagerPanel.ToString());
    }

    private void OnDecorationClicked()
    {
        if (_isDecorationMode) return;
        if (_isManagerOpen) CloseManagerPanel();
        EnterDecorationMode();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 管理面板
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void CloseManagerPanel()
    {
        if (!_isManagerOpen) return;
        _isManagerOpen = false;
        if (managerPanel != null) managerPanel.ClosePanel();
        RestoreMainPanels();
    }

    private void OnManagerPanelClosed()
    {
        _isManagerOpen = false;
        RestoreMainPanels();
        RefreshOperatePanel();
    }

    private void OnFishTransferRequest(FishDetailData fishData, FishTankStoreData fromContainer, FishTankStoreData toContainer)
    {
        if (fishData == null || toContainer == null) return;

        var transferData = new TransferData
        {
            FishData = fishData,
            FromIndex = fromContainer?.IsBag == true ? 0 : (fromContainer?.TankId ?? 0) + 1,
            ToIndex = toContainer.IsBag ? 0 : toContainer.TankId + 1,
            IsFromBag = fromContainer?.IsBag ?? false,
            IsToBag = toContainer.IsBag
        };
        CommunicateEvent.Modify(FishTankMessage.TransferFish.ToString(), transferData);
    }

    private void OnUnlockRequest(int tankId)
    {
        var config = LoadDataManager.Instance?.GetFishTankConfig(tankId);
        if (config == null) return;

        GameUIManager.Instance?.ShowDialog(
            $"花费 {config.purchaseCost} 金币解锁 {config.name}？",
            DialogType.Info,
            () =>
            {
                CommunicateEvent.Modify(FishTankMessage.UnlockTank.ToString(), tankId);
                GameUIManager.ShowMessage("解锁请求已发送");
            }
        );
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装饰模式
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void EnterDecorationMode()
    {
        _isDecorationMode = true;

        if (mainOperatePanel != null) mainOperatePanel.ClosePanel();
        if (mainPanel != null)
        {
            mainPanel.SetFishVisible(false);
            mainPanel.SetBaitVisible(false);
            mainPanel.ClearAllBaits();

            mainPanel.SetDecorationMode(true);
        }

        RenderDecorationsForCurrentTank();

        if (decorationPanel != null)
        {
            decorationPanel.gameObject.SetActive(true);
            decorationPanel.Open(GetCurrentTank()?.tankId ?? 1);
        }

        CommunicateEvent.Modify(FishTankMessage.EnterDecorationMode.ToString());
    }

    private void ExitDecorationMode()
    {
        if (!_isDecorationMode) return;
        _isDecorationMode = false;

        if (decorationPanel != null) decorationPanel.Close();
        if (mainPanel != null)
        {
            mainPanel.SetDecorationMode(false);
        }

        RestoreMainPanels();

        CommunicateEvent.Modify(FishTankMessage.ExitDecorationMode.ToString());
    }

    private void OnDecorationPanelClosed()
    {
        ExitDecorationMode();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装饰选择面板里的项被点击
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnDecorationItemClicked(FishTankDecData config)
    {
        if (config == null) return;

        int tankId = GetCurrentTank()?.tankId ?? 1;
        int category = config.categoryId;
        int decId = config.id;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 80/81：可多次装备
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        if (category == 80 || category == 81)
        {
            int bagQty = PlayerDataService.Instance?.GetOwnedDecorationQuantity(decId) ?? 0;

            if (bagQty > 0)
            {
                // ★ 用所属区域中心作为初始位置
                Vector2 spawnPos = (mainPanel != null)
                    ? mainPanel.GetDefaultSpawnPosition(category)
                    : Vector2.zero;

                var equipData = new EquipDecorationData
                {
                    TankId = tankId,
                    Category = category,
                    DecorationId = decId,
                    PosX = spawnPos.x,
                    PosY = spawnPos.y,
                    PosZ = 0,
                    ScaleX = 1,
                    ScaleY = 1,
                    ScaleZ = 1,
                    RotX = 0,
                    RotY = 0,
                    RotZ = 0
                };
                CommunicateEvent.Modify(FishTankMessage.EquipDecoration.ToString(), equipData);
            }
            else
            {
                GameUIManager.ShowMessage("背包中没有多余的该装饰");
            }
            return;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 82-84：唯一
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        if (category >= 82 && category <= 84)
        {
            var equipped = PlayerDataService.Instance?.GetEquippedDecorations(tankId);
            bool alreadyEquipped = false;
            if (equipped != null && equipped.TryGetValue(category, out var list))
            {
                alreadyEquipped = list.Exists(i => i.DecorationId == decId);
            }

            if (alreadyEquipped)
            {
                var applyData = new ApplyDecorationData
                {
                    TankId = tankId,
                    Category = category,
                    DecorationId = decId
                };
                CommunicateEvent.Modify(FishTankMessage.ApplyTextureDecoration.ToString(), applyData);
                GameUIManager.ShowMessage("已应用装饰");
            }
            else
            {
                var equipData = new EquipDecorationData
                {
                    TankId = tankId,
                    Category = category,
                    DecorationId = decId,
                    PosX = 0,
                    PosY = 0,
                    PosZ = 0,
                    ScaleX = 1,
                    ScaleY = 1,
                    ScaleZ = 1,
                    RotX = 0,
                    RotY = 0,
                    RotZ = 0
                };
                CommunicateEvent.Modify(FishTankMessage.EquipDecoration.ToString(), equipData);
            }
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装饰实例被点击（显示 Operator）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnDecorationInstanceClicked(UI_FishTankDec dec)
    {
        if (dec == null || decorationPanel == null || mainPanel == null) return;

        int recordId = dec.RecordId;

        if (decorationPanel.IsDecOperatorShowing && decorationPanel.CurrentDecOperatorRecordId == recordId)
        {
            decorationPanel.HideDecOperator();
            return;
        }

        var rect = mainPanel.GetDecorationRect(recordId);
        if (rect == null) return;

        decorationPanel.ShowDecOperator(dec.TankId, recordId, dec.Category, dec.DecorationId, rect);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // Operator 回调
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnDecDragMove(int recordId, float dx, float dy)
    {
        if (decorationPanel != null)
            decorationPanel.HandleDecDragMove(recordId, dx, dy);
    }

    private void OnDecDragEnd(int recordId, float totalDx, float totalDy)
    {
        Vector2 finalDelta = decorationPanel != null
            ? decorationPanel.GetFinalDragDelta(recordId)
            : new Vector2(totalDx, totalDy);

        var data = new MoveDecorationData
        {
            TankId = GetCurrentTank()?.tankId ?? 1,
            RecordId = recordId,
            DeltaX = finalDelta.x,
            DeltaY = finalDelta.y
        };
        CommunicateEvent.Modify(FishTankMessage.MoveDecoration.ToString(), data);
    }

    private void OnDecMirrorClicked(int recordId)
    {
        var data = new MirrorDecorationData
        {
            TankId = GetCurrentTank()?.tankId ?? 1,
            RecordId = recordId
        };
        CommunicateEvent.Modify(FishTankMessage.MirrorDecoration.ToString(), data);
    }

    private void OnDecRemoveClicked(int recordId)
    {
        var data = new RemoveDecorationData
        {
            TankId = GetCurrentTank()?.tankId ?? 1,
            RecordId = recordId
        };
        CommunicateEvent.Modify(FishTankMessage.RemoveDecoration.ToString(), data);
        if (decorationPanel != null) decorationPanel.HideDecOperator();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 装饰同步
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void SyncCurrentTankDecorations()
    {
        int tankId = GetCurrentTank()?.tankId ?? 1;

        if (NetServerManager.Instance == null) return;

        NetServerManager.Instance.FetchEquippedStatus(tankId, (result) =>
        {
            if (result != null && PlayerDataManager.Instance != null)
                PlayerDataManager.Instance.UpdateEquippedDecorations(tankId, result);
            RenderDecorationsForCurrentTank();
        });
    }

    private void RenderDecorationsForCurrentTank()
    {
        if (mainPanel == null) return;

        int showingRecordId = (decorationPanel != null && decorationPanel.IsDecOperatorShowing)
            ? decorationPanel.CurrentDecOperatorRecordId : 0;

        int tankId = GetCurrentTank()?.tankId ?? 1;
        mainPanel.RenderDecorations(tankId);

        if (showingRecordId != 0 && decorationPanel != null)
        {
            var newRect = mainPanel.GetDecorationRect(showingRecordId);
            decorationPanel.RebindDecOperatorTarget(showingRecordId, newRect);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 辅助
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void RestoreMainPanels()
    {
        if (mainPanel != null)
        {
            mainPanel.SetFishVisible(true);
            mainPanel.SetBaitVisible(true);
            mainPanel.RefreshFishTank(_currentTankIndex);
        }
        if (mainOperatePanel != null) mainOperatePanel.OpenPanel();
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankView] {msg}");
    }
}
