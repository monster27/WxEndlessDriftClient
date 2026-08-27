// ============================================================
// 文件: FishTankView.cs
// 说明: 鱼缸主视图 - 只负责UI展示
// 路径: Assets/Scripts/UIView/View/
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using static PlayerDataManager;

public class FishTankView : BaseView
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== UI组件 =====")]
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

        // UI按钮事件
        if (leftBtn != null) leftBtn.onClick.AddListener(OnLeftClick);
        if (rightBtn != null) rightBtn.onClick.AddListener(OnRightClick);
        if (manageBtn != null) manageBtn.onClick.AddListener(ToggleManagerPanel);
        if (managerCloseBtn != null) managerCloseBtn.onClick.AddListener(CloseManagerPanel);
        if (decorationBtn != null) decorationBtn.onClick.AddListener(OnDecorationClick);
        if (lockBtn != null) lockBtn.onClick.AddListener(OnLockClick);

        // 监听Service的数据更新通知
        CommunicateEvent.Register(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
    }

    private void UnregisterEvents()
    {
        if (leftBtn != null) leftBtn.onClick.RemoveAllListeners();
        if (rightBtn != null) rightBtn.onClick.RemoveAllListeners();
        if (manageBtn != null) manageBtn.onClick.RemoveAllListeners();
        if (managerCloseBtn != null) managerCloseBtn.onClick.RemoveAllListeners();
        if (decorationBtn != null) decorationBtn.onClick.RemoveAllListeners();
        if (lockBtn != null) lockBtn.onClick.RemoveAllListeners();

        CommunicateEvent.Unregister(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
    }

    // ============================================================
    // 消息处理
    // ============================================================

    private void SendMessage(FishTankMessage message)
    {
        CommunicateEvent.Modify(message.ToString());
    }

    private void SendMessage(FishTankMessage message, object parameter)
    {
        CommunicateEvent.Modify(message.ToString(), parameter);
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
            managerPanel.SetUnlockCallback(OnUnlockRequest);
            managerPanel.ClosePanel();
        }
    }

    public void OpenFishTank()
    {
        LogDebug("OpenFishTank");

        // 显示界面
        defaultMaskImg.SetActive(true);
        ShowView();
        _currentTankIndex = 0;

        // 显示加载状态
        ShowLoadingState();

        // 发送消息给Service
        SendMessage(FishTankMessage.OpenFishTank);
    }

    private void OnDataUpdated()
    {
        if (!gameObject.activeSelf) return;
        LogDebug("数据已更新，刷新UI");
        RefreshAll();
    }

    public void CloseFishTank()
    {
        LogDebug("CloseFishTank");

        defaultMaskImg.SetActive(false);
        if (fishTankManager != null) fishTankManager.CloseFishTank();
        HideView();
        CloseManagerPanel();

        _isManagerOpen = false;
        _currentTankIndex = 0;

        SendMessage(FishTankMessage.CloseFishTank);
    }

    protected override void OnCloseButtonClick()
    {
        base.OnCloseButtonClick();
        CloseFishTank();
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

    private void HideLoadingState()
    {
        if (tankNameText != null && tankNameText.text == "加载中...")
        {
            // 加载状态由RefreshAll覆盖
        }
    }

    // ============================================================
    // 数据访问（通过Service）
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
        if (!gameObject.activeSelf) return;

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
    }

    // ============================================================
    // UI刷新
    // ============================================================

    private void RefreshTopUI()
    {
        var tanks = GetTankList();

        if (tanks == null || tanks.Count == 0)
        {
            if (tankNameText != null) tankNameText.text = "暂无鱼缸";
            if (capacityText != null) capacityText.text = "0/0";
            if (harvestText != null) harvestText.gameObject.SetActive(false);
            if (lockIcon != null) lockIcon.SetActive(false);
            if (lockBtn != null) lockBtn.gameObject.SetActive(false);
            if (leftBtn != null) leftBtn.interactable = false;
            if (rightBtn != null) rightBtn.interactable = false;
            return;
        }

        if (_currentTankIndex >= tanks.Count)
            _currentTankIndex = tanks.Count - 1;
        if (_currentTankIndex < 0)
            _currentTankIndex = 0;

        var tank = tanks[_currentTankIndex];
        if (tank == null) return;

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
                managerPanel.OpenPanel();
            else
                managerPanel.ClosePanel();
        }

        SendMessage(FishTankMessage.ToggleManagerPanel);
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
        SendMessage(FishTankMessage.SwitchTank, _currentTankIndex);
        RefreshAll();

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
        SendMessage(FishTankMessage.SwitchTank, _currentTankIndex);
        RefreshAll();

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
            () =>
            {
                SendMessage(FishTankMessage.UnlockTank, tank.tankId);
                GameUIManager.ShowMessage("解锁请求已发送");
            }
        );
    }

    private void OnUnlockRequest(int tankId)
    {
        SendMessage(FishTankMessage.UnlockTank, tankId);
    }

    // ============================================================
    // 鱼转移
    // ============================================================

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

        SendMessage(FishTankMessage.TransferFish, transferData);
    }

    // ============================================================
    // 生命周期
    // ============================================================

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    // ============================================================
    // 日志
    // ============================================================

    private void LogDebug(string message)
    {
        if (enableDebugLog)
            Z_Logger.Log($"[FishTankView] {message}");
    }
}
