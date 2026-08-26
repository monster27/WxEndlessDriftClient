// ============================================================
// 文件: FishTankView.cs
// 说明: 鱼缸主视图 - 只负责UI展示，通过 PlayerDataService 读取数据
// 路径: Assets/Scripts/UIView/View/
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;

// ✅ 使用别名引用 NetServerManager 中的类型
using FishTankStatusData = NetServerManager.FishTankStatusData;
using System.Collections;

public class FishTankView : BaseView
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    public GameObject defaultMaskImg;

    [Header("===== 顶部信息 =====")]
    [SerializeField] private Button leftBtn;
    [SerializeField] private Button rightBtn;
    [SerializeField] private Text tankNameText;
    [SerializeField] private Text capacityText;
    [SerializeField] private Text harvestText;
    [SerializeField] private Button lockBtn;
    [SerializeField] private GameObject lockIcon;

    [Header("===== 底部按钮 =====")]
    [SerializeField] private Button manageBtn;
    [SerializeField] private Button decorationBtn;

    [Header("===== 鱼缸3D显示 =====")]
    [SerializeField] private FishTankManager fishTankManager;

    [Header("===== 管理面板 =====")]
    [SerializeField] private FishTankManagerPanel managerPanel;
    [SerializeField] private Button managerCloseBtn;

    [Header("===== StorePanel 预制体 =====")]
    [SerializeField] private GameObject fishTankStorePrefab;

    // ============================================================
    // 数据
    // ============================================================

    private int _currentTankIndex = 0;
    private bool _isManagerOpen = false;
    private bool _isRefreshing = false;

    // ============================================================
    // 生命周期
    // ============================================================

    protected override void Awake()
    {
        base.Awake();
        RegisterEvents();
    }

    private void RegisterEvents()
    {
        UnregisterEvents();

        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);
        if (manageBtn != null) manageBtn.onClick.AddListener(ToggleManagerPanel);
        if (managerCloseBtn != null) managerCloseBtn.onClick.AddListener(CloseManagerPanel);
        if (decorationBtn != null) decorationBtn.onClick.AddListener(OnDecorationClick);
        if (lockBtn != null) lockBtn.onClick.AddListener(OnLockClick);

        // ✅ 只监听数据就绪事件
        CommunicateEvent.Register("FishTankDataReady", OnDataReady);
    }

    private void UnregisterEvents()
    {
        if (leftBtn != null) leftBtn.onClick.RemoveAllListeners();
        if (rightBtn != null) rightBtn.onClick.RemoveAllListeners();
        if (manageBtn != null) manageBtn.onClick.RemoveAllListeners();
        if (managerCloseBtn != null) managerCloseBtn.onClick.RemoveAllListeners();
        if (decorationBtn != null) decorationBtn.onClick.RemoveAllListeners();
        if (lockBtn != null) lockBtn.onClick.RemoveAllListeners();

        CommunicateEvent.Unregister("FishTankDataReady", OnDataReady);
    }

    // ============================================================
    // 事件处理
    // ============================================================
    private void OnDataReady()
    {
        Z_Logger.Log("[FishTankView] OnDataReady 被调用");
        // 添加日志查看数据
        var statuses = PlayerDataManager.Instance?.GetAllFishTankStatusOrdered();
        Z_Logger.Log($"[FishTankView] OnDataReady - 鱼缸数量: {statuses?.Count ?? 0}");
        if (statuses != null)
        {
            foreach (var s in statuses)
            {
                Z_Logger.Log($"[FishTankView] 鱼缸 {s.tankId}: isUnlocked={s.isUnlocked}, items={s.items?.Count ?? 0}");
            }
        }
        RefreshAll();
    }

    private IEnumerator DelayedRefresh()
    {
        yield return null; // 等待一帧
        yield return null; // 再等一帧，确保数据已同步

        if (enableDebugLog)
            Z_Logger.Log("[FishTankView] 延迟刷新");

        RefreshAll();
    }

    // ============================================================
    // 打开/关闭
    // ============================================================

    public override void BaseViewInit()
    {
        base.BaseViewInit();

        if (managerPanel != null)
        {
            managerPanel.Init(fishTankStorePrefab, enableDebugLog);
            managerPanel.SetTransferCallback(OnFishTransferRequest);
            managerPanel.SetUnlockCallback(OnPanelUnlockRequest);
            managerPanel.ClosePanel();
        }
    }

    public void OpenFishTank()
    {
        defaultMaskImg.SetActive(true);
        ShowView();

        _currentTankIndex = 0;

        // ✅ 先尝试刷新，如果有数据就直接显示
        RefreshAll();

        // ✅ 如果没有数据，请求网络，等待数据后刷新
        var tanks = GetTankList();
        if (tanks == null || tanks.Count == 0)
        {
            if (enableDebugLog)
                Z_Logger.Log("[FishTankView] OpenFishTank: 无数据，请求网络");

            ShowLoadingState();
            CommunicateEvent.Modify("FISH_TANK_OPEN");
        }
    }

    protected override void OnCloseButtonClick()
    {
        base.OnCloseButtonClick();
        CloseFishTank();
    }

    public void CloseFishTank()
    {
        // ✅ 取消事件注册
        CommunicateEvent.Unregister("FishTankDataReady", OnDataReady);

        if (defaultMaskImg != null) defaultMaskImg.SetActive(false);
        if (fishTankManager != null) fishTankManager.CloseFishTank();
        HideView();
        CloseManagerPanel();

        _isManagerOpen = false;
        _currentTankIndex = 0;
    }

    // ============================================================
    // 加载状态
    // ============================================================

    private void ShowLoadingState()
    {
        if (tankNameText != null) tankNameText.text = "加载中...";
        if (capacityText != null) capacityText.text = "";
        if (harvestText != null) harvestText.gameObject.SetActive(false);
        if (lockIcon != null) lockIcon.SetActive(false);
        if (lockBtn != null) lockBtn.gameObject.SetActive(false);
        if (leftBtn != null) leftBtn.interactable = false;
        if (rightBtn != null) rightBtn.interactable = false;

        if (fishTankManager != null)
            fishTankManager.CloseFishTank();
    }

    // ============================================================
    // 数据访问
    // ============================================================

    private List<FishTankStatusData> GetTankList()
    {
        if (PlayerDataService.Instance == null)
            return new List<FishTankStatusData>();
        return PlayerDataService.Instance.GetTankList();
    }

    private FishTankStatusData GetCurrentTank()
    {
        var tanks = GetTankList();
        if (tanks.Count == 0) return null;

        if (_currentTankIndex >= tanks.Count)
            _currentTankIndex = tanks.Count - 1;
        if (_currentTankIndex < 0)
            _currentTankIndex = 0;

        return tanks[_currentTankIndex];
    }

    private List<FishDetailData> GetCurrentTankFish()
    {
        var tank = GetCurrentTank();
        if (tank == null) return new List<FishDetailData>();

        if (PlayerDataService.Instance != null)
            return PlayerDataService.Instance.GetTankFishList(tank.tankId);
        return new List<FishDetailData>();
    }

    private FishTankConfig GetTankConfig(int tankId)
    {
        return LoadDataManager.Instance?.GetFishTankConfig(tankId);
    }

    // ============================================================
    // 核心刷新
    // ============================================================

    public void RefreshAll()
    {
        if (_isRefreshing) return;
        if (!gameObject.activeSelf) return;

        _isRefreshing = true;

        try
        {
            RefreshTopUI();
            RefreshFishTankManager();

            if (_isManagerOpen && managerPanel != null)
            {
                managerPanel.RefreshData();
            }
        }
        catch (Exception ex)
        {
            if (enableDebugLog)
                Z_Logger.LogError($"[FishTankView] RefreshAll 异常: {ex.Message}");
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    // ============================================================
    // UI刷新
    // ============================================================

    private void RefreshTopUI()
    {
        var tanks = GetTankList();

        if (tanks == null || tanks.Count == 0)
        {
            if (enableDebugLog)
                Z_Logger.Log("[FishTankView] RefreshTopUI: 没有鱼缸数据");

            if (tankNameText != null) tankNameText.text = "暂无鱼缸";
            if (capacityText != null) capacityText.text = "0/0";
            if (harvestText != null) harvestText.gameObject.SetActive(false);
            if (lockIcon != null) lockIcon.SetActive(false);
            if (lockBtn != null) lockBtn.gameObject.SetActive(false);
            if (leftBtn != null) leftBtn.interactable = false;
            if (rightBtn != null) rightBtn.interactable = false;
            return;
        }

        // 确保索引有效
        if (_currentTankIndex >= tanks.Count)
            _currentTankIndex = tanks.Count - 1;
        if (_currentTankIndex < 0)
            _currentTankIndex = 0;

        var tank = tanks[_currentTankIndex];
        if (tank == null)
        {
            if (enableDebugLog)
                Z_Logger.LogWarning("[FishTankView] RefreshTopUI: 当前鱼缸为空");
            return;
        }

        var fishList = GetCurrentTankFish();
        var config = GetTankConfig(tank.tankId);

        if (tankNameText != null)
            tankNameText.text = config?.name ?? $"鱼缸{tank.tankId}";

        if (capacityText != null)
            capacityText.text = $"{fishList.Count}/{tank.capacity}";

        if (harvestText != null)
        {
            if (config?.type == "special" && tank.isUnlocked && fishList.Count > 0)
            {
                harvestText.text = $"每小时: {fishList.Count * 10} 金币";
                harvestText.gameObject.SetActive(true);
            }
            else
            {
                harvestText.gameObject.SetActive(false);
            }
        }

        if (lockIcon != null) lockIcon.SetActive(!tank.isUnlocked);
        if (lockBtn != null) lockBtn.gameObject.SetActive(!tank.isUnlocked);

        if (leftBtn != null) leftBtn.interactable = tanks.Count > 1;
        if (rightBtn != null) rightBtn.interactable = tanks.Count > 1;
    }

    private void RefreshFishTankManager()
    {
        if (fishTankManager == null) return;

        var tank = GetCurrentTank();
        if (tank == null || !tank.isUnlocked)
        {
            fishTankManager.SetFishData(null);
            fishTankManager.CloseFishTank();
            return;
        }

        var fishList = GetCurrentTankFish();
        if (fishList == null)
            fishList = new List<FishDetailData>();

        fishTankManager.SetFishData(fishList);
        fishTankManager.OpenFishTank();
    }

    // ============================================================
    // 管理面板
    // ============================================================

    private void ToggleManagerPanel()
    {
        _isManagerOpen = !_isManagerOpen;

        if (managerPanel != null)
        {
            if (_isManagerOpen)
            {
                managerPanel.OpenPanel();
            }
            else
            {
                managerPanel.ClosePanel();
            }
        }
    }

    private void CloseManagerPanel()
    {
        _isManagerOpen = false;
        if (managerPanel != null) managerPanel.ClosePanel();
    }

    // ============================================================
    // 按钮事件
    // ============================================================

    private void OnLeftClick()
    {
        var tanks = GetTankList();
        if (tanks.Count <= 1) return;

        _currentTankIndex = (_currentTankIndex - 1 + tanks.Count) % tanks.Count;

        RefreshTopUI();
        RefreshFishTankManager();

        if (_isManagerOpen && managerPanel != null)
        {
            managerPanel.OnTankSwitched(_currentTankIndex);
        }
    }

    private void OnRightClick()
    {
        var tanks = GetTankList();
        if (tanks.Count <= 1) return;

        _currentTankIndex = (_currentTankIndex + 1) % tanks.Count;

        RefreshTopUI();
        RefreshFishTankManager();

        if (_isManagerOpen && managerPanel != null)
        {
            managerPanel.OnTankSwitched(_currentTankIndex);
        }
    }

    private void OnDecorationClick()
    {
        GameUIManager.ShowMessage("装饰功能开发中");
    }

    private void OnLockClick()
    {
        var tank = GetCurrentTank();
        if (tank == null || tank.isUnlocked) return;

        var config = GetTankConfig(tank.tankId);
        if (config == null) return;

        GameUIManager.Instance?.ShowDialog(
            $"花费 {config.purchaseCost} 金币解锁 {config.name}？",
            DialogType.Info,
            () => RequestUnlockFishTank(tank.tankId)
        );
    }

    private void RequestUnlockFishTank(int tankId)
    {
        if (NetServerManager.Instance == null) return;

        NetServerManager.Instance.UnlockFishTank(tankId, (success, message) =>
        {
            if (success)
                GameUIManager.ShowMessage("鱼缸解锁成功！");
            else
                GameUIManager.ShowMessage(message);
        });
    }

    private void OnPanelUnlockRequest(int tankId)
    {
        RequestUnlockFishTank(tankId);
    }

    // ============================================================
    // 鱼转移
    // ============================================================

    private void OnFishTransferRequest(FishDetailData fishData, FishTankStoreData fromContainer, FishTankStoreData toContainer)
    {
        if (fishData == null || toContainer == null) return;

        if (toContainer.IsBag)
        {
            if (fromContainer != null && !fromContainer.IsBag && fromContainer.IsUnlocked)
            {
                MoveFishFromTankToBag(fishData);
            }
            return;
        }

        if (!toContainer.IsBag)
        {
            if (!toContainer.IsUnlocked)
            {
                GameUIManager.ShowMessage($"{toContainer.Name} 未解锁");
                return;
            }

            if (toContainer.FishList != null && toContainer.FishList.Count >= toContainer.MaxCapacity)
            {
                GameUIManager.ShowMessage($"{toContainer.Name} 已满");
                return;
            }

            if (fromContainer != null && fromContainer.IsBag)
            {
                MoveFishFromBagToTank(fishData, toContainer);
                return;
            }

            if (fromContainer != null && !fromContainer.IsBag && fromContainer.IsUnlocked)
            {
                MoveFishFromTankToTank(fishData, fromContainer, toContainer);
            }
        }
    }

    private void MoveFishFromBagToTank(FishDetailData fishData, FishTankStoreData targetTank)
    {
        if (NetServerManager.Instance == null || targetTank == null) return;

        NetServerManager.Instance.MoveFishFromBagToTank(
            targetTank.TankId,
            fishData.id,
            (success, message) =>
            {
                if (success)
                    GameUIManager.ShowMessage($"已放入 {targetTank.Name}");
                else
                    GameUIManager.ShowMessage(message);
            }
        );
    }

    private void MoveFishFromTankToBag(FishDetailData fishData)
    {
        if (NetServerManager.Instance == null) return;

        NetServerManager.Instance.MoveFishFromTankToBag(
            fishData.id,
            (success, message) =>
            {
                if (success)
                    GameUIManager.ShowMessage("已取出到鱼篓");
                else
                    GameUIManager.ShowMessage(message);
            }
        );
    }

    private void MoveFishFromTankToTank(FishDetailData fishData, FishTankStoreData fromTank, FishTankStoreData toTank)
    {
        if (NetServerManager.Instance == null || fromTank == null || toTank == null) return;

        NetServerManager.Instance.MoveFishFromTankToTank(
            fromTank.TankId,
            toTank.TankId,
            fishData.id,
            (success, message) =>
            {
                if (success)
                    GameUIManager.ShowMessage($"已从 {fromTank.Name} 转移到 {toTank.Name}");
                else
                    GameUIManager.ShowMessage(message);
            }
        );
    }

    // ============================================================
    // 生命周期
    // ============================================================

    void OnDestroy()
    {
        UnregisterEvents();
    }
}
