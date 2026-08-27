// ============================================================
// 文件: FishTankManagerPanel.cs
// 说明: 鱼缸管理面板 - 管理上下两个StorePanel
// 路径: Assets/Scripts/UIView/Panel/
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System;
using static PlayerDataManager;

public class FishTankManagerPanel : MonoBehaviour
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 面板引用 =====")]
    [SerializeField] private FishTankStorePanel upperStorePanel;
    [SerializeField] private FishTankStorePanel lowerStorePanel;

    [Header("===== 按钮 =====")]
    [SerializeField] private Button closeBtn;

    [Header("===== 收益显示 =====")]
    [SerializeField] private GameObject harvestInfoObj;
    [SerializeField] private Text harvestTitleText;
    [SerializeField] private Text harvestValueText;

    // ============================================================
    // 数据
    // ============================================================

    private GameObject _fishTankStorePrefab;
    private Action<FishDetailData, FishTankStoreData, FishTankStoreData> _onFishTransfer;
    private Action<int> _onUnlockRequest;
    private bool _isInitialized = false;

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(GameObject fishTankStorePrefab, bool isEnableDebug = false)
    {
        enableDebugLog = isEnableDebug;
        _fishTankStorePrefab = fishTankStorePrefab;

        InitPanels();
        SetupUI();
        RegisterEvents();

        _isInitialized = true;
        LogDebug("Init 完成");
    }

    private void InitPanels()
    {
        if (upperStorePanel != null)
        {
            upperStorePanel.Init(0, enableDebugLog);
            upperStorePanel.SetTransferCallback(OnFishTransferRequest);
            upperStorePanel.SetUnlockCallback(OnUnlockRequest);
        }

        if (lowerStorePanel != null)
        {
            lowerStorePanel.Init(1, enableDebugLog);
            lowerStorePanel.SetTransferCallback(OnFishTransferRequest);
            lowerStorePanel.SetUnlockCallback(OnUnlockRequest);
        }

        if (upperStorePanel != null && lowerStorePanel != null)
        {
            upperStorePanel.SetLockedIndex(lowerStorePanel.CurrentIndex);
            lowerStorePanel.SetLockedIndex(upperStorePanel.CurrentIndex);
        }
    }

    private void SetupUI()
    {
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(ClosePanel);
        }
    }

    // ============================================================
    // 事件注册
    // ============================================================

    private void RegisterEvents()
    {
        UnregisterEvents();
        CommunicateEvent.Register(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
        LogDebug("事件注册完成");
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
    }

    // ============================================================
    // 事件处理
    // ============================================================

    private void OnDataUpdated()
    {
        if (!_isInitialized) return;
        if (!gameObject.activeSelf) return;

        LogDebug("收到 DataUpdated 消息，刷新面板");
        RefreshData();
    }

    // ============================================================
    // 回调设置
    // ============================================================

    public void SetTransferCallback(Action<FishDetailData, FishTankStoreData, FishTankStoreData> callback)
    {
        _onFishTransfer = callback;
        if (upperStorePanel != null) upperStorePanel.SetTransferCallback(callback);
        if (lowerStorePanel != null) lowerStorePanel.SetTransferCallback(callback);
    }

    public void SetUnlockCallback(Action<int> callback)
    {
        _onUnlockRequest = callback;
        if (upperStorePanel != null) upperStorePanel.SetUnlockCallback(callback);
        if (lowerStorePanel != null) lowerStorePanel.SetUnlockCallback(callback);
    }

    // ============================================================
    // 数据更新
    // ============================================================

    public void RefreshData()
    {
        if (!_isInitialized) return;

        if (upperStorePanel != null) upperStorePanel.RefreshData();
        if (lowerStorePanel != null) lowerStorePanel.RefreshData();

        UpdateHarvestInfo();
    }

    public void OnTankSwitched(int tankIndex)
    {
        if (!_isInitialized) return;

        if (upperStorePanel != null)
            upperStorePanel.SetCurrentIndex(0);

        if (lowerStorePanel != null)
            lowerStorePanel.SetCurrentIndex(tankIndex + 1);
    }

    // ============================================================
    // 收益显示
    // ============================================================

    private void UpdateHarvestInfo()
    {
        int hourlyEarning = 0;
        string tankName = "";

        if (PlayerDataService.Instance != null)
        {
            var tanks = PlayerDataService.Instance.GetTankList();
            foreach (var tank in tanks)
            {
                var config = LoadDataManager.Instance?.GetFishTankConfig(tank.tankId);
                if (config?.type == "special" && tank.isUnlocked)
                {
                    var fishList = PlayerDataService.Instance.GetTankFishList(tank.tankId);
                    hourlyEarning = fishList.Count * 10;
                    tankName = config.name;
                    break;
                }
            }
        }

        if (hourlyEarning > 0)
        {
            if (harvestInfoObj != null) harvestInfoObj.SetActive(true);
            if (harvestTitleText != null) harvestTitleText.text = tankName;
            if (harvestValueText != null) harvestValueText.text = $"{hourlyEarning}";
        }
        else
        {
            if (harvestInfoObj != null) harvestInfoObj.SetActive(false);
        }
    }

    // ============================================================
    // 面板开关
    // ============================================================

    public void OpenPanel()
    {
        gameObject.SetActive(true);
        RefreshData();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    // ============================================================
    // 回调转发
    // ============================================================

    private void OnFishTransferRequest(FishDetailData fishData, FishTankStoreData fromContainer, FishTankStoreData toContainer)
    {
        _onFishTransfer?.Invoke(fishData, fromContainer, toContainer);
    }

    private void OnUnlockRequest(int tankId)
    {
        _onUnlockRequest?.Invoke(tankId);
    }

    // ============================================================
    // 生命周期
    // ============================================================

    private void OnDestroy()
    {
        UnregisterEvents();
        if (closeBtn != null) closeBtn.onClick.RemoveAllListeners();
    }

    // ============================================================
    // 日志
    // ============================================================

    private void LogDebug(string message)
    {
        if (enableDebugLog)
            Z_Logger.Log($"[FishTankManagerPanel] {message}");
    }
}
