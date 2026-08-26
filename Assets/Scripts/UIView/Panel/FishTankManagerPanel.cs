// ============================================================
// 文件: FishTankManagerPanel.cs
// 说明: 鱼缸管理面板 - 管理上下两个StorePanel
// 路径: Assets/Scripts/UIView/Panel/
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Linq;
using static NetServerManager;

public class FishTankManagerPanel : MonoBehaviour
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 面板引用 =====")]
    [SerializeField] private FishTankStorePanel upperStorePanel;
    [SerializeField] private FishTankStorePanel lowerStorePanel;

    [Header("===== 按钮 =====")]
    [SerializeField] private Button closeBtn;

    [Header("===== 排序 =====")]
    [SerializeField] private Button sortByRarityBtn;
    [SerializeField] private Button sortByHarvestBtn;

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

    public enum SortType { Rarity, Harvest }
    private SortType _currentSortType = SortType.Rarity;
    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    private void LogDebug(string message)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankManagerPanel] {message}");
    }

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
            upperStorePanel.Init(_fishTankStorePrefab, 0, FishTankPanelType.Upper, enableDebugLog);
            upperStorePanel.SetTransferCallback(OnFishTransferRequest);
            upperStorePanel.SetUnlockCallback(OnUnlockRequest);
        }

        if (lowerStorePanel != null)
        {
            lowerStorePanel.Init(_fishTankStorePrefab, 1, FishTankPanelType.Lower, enableDebugLog);
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
            closeBtn.onClick.AddListener(OnCloseClick);
        }

        if (sortByRarityBtn != null)
        {
            sortByRarityBtn.onClick.RemoveAllListeners();
            sortByRarityBtn.onClick.AddListener(() => OnSortButtonClick(SortType.Rarity));
        }

        if (sortByHarvestBtn != null)
        {
            sortByHarvestBtn.onClick.RemoveAllListeners();
            sortByHarvestBtn.onClick.AddListener(() => OnSortButtonClick(SortType.Harvest));
        }
    }

    // ============================================================
    // 事件注册
    // ============================================================

    private void RegisterEvents()
    {
        UnregisterEvents();

        // ✅ 只监听最关键的事件
        CommunicateEvent.Register("FishTankChanged", OnDataChanged);
        CommunicateEvent.Register("PlayerDataChanged", OnDataChanged);

        LogDebug("事件注册完成");
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister("FishTankChanged", OnDataChanged);
        CommunicateEvent.Unregister("PlayerDataChanged", OnDataChanged);
    }

    // ============================================================
    // 事件处理
    // ============================================================

    private void OnDataChanged()
    {
        if (!_isInitialized) return;
        if (!gameObject.activeSelf) return;

        LogDebug("收到数据变化事件，刷新面板");
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
    // 排序
    // ============================================================

    private void OnSortButtonClick(SortType sortType)
    {
        if (_currentSortType == sortType)
        {
            ReverseFishLists();
        }
        else
        {
            _currentSortType = sortType;
            SortFishLists();
        }

        RefreshData();
    }

    private void SortFishLists()
    {
        RefreshData();
    }

    private void ReverseFishLists()
    {
        RefreshData();
    }

    private int GetFishRarity(int fishId)
    {
        if (LoadDataManager.Instance != null)
        {
            var fishData = LoadDataManager.Instance.GetFishById(fishId);
            if (fishData != null) return fishData.rarityId;
        }
        if (fishId >= 1010 && fishId <= 1015) return 204;
        if (fishId >= 1006 && fishId <= 1009) return 202;
        return 201;
    }

    // ============================================================
    // 收益显示
    // ============================================================

    private void UpdateHarvestInfo()
    {
        if (PlayerDataManager.Instance == null)
        {
            if (harvestInfoObj != null) harvestInfoObj.SetActive(false);
            return;
        }

        var tanks = PlayerDataManager.Instance.GetAllFishTankStatusOrdered();

        FishTankStatusData specialTank = null;
        foreach (var tank in tanks)
        {
            var config = LoadDataManager.Instance?.GetFishTankConfig(tank.tankId);
            if (config?.type == "special" && tank.isUnlocked)
            {
                specialTank = tank;
                break;
            }
        }

        if (specialTank != null)
        {
            var fishList = PlayerDataManager.Instance.GetFishTankItems(specialTank.tankId);
            if (fishList != null && fishList.Count > 0)
            {
                int fishCount = fishList.Count;
                int hourlyEarning = fishCount * 10;

                if (harvestInfoObj != null) harvestInfoObj.SetActive(true);
                if (harvestTitleText != null)
                {
                    var config = LoadDataManager.Instance?.GetFishTankConfig(specialTank.tankId);
                    harvestTitleText.text = config?.name ?? "特殊鱼缸";
                }
                if (harvestValueText != null) harvestValueText.text = $"{hourlyEarning}";
                return;
            }
        }

        if (harvestInfoObj != null) harvestInfoObj.SetActive(false);
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

    private void OnCloseClick()
    {
        ClosePanel();
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
        if (sortByRarityBtn != null) sortByRarityBtn.onClick.RemoveAllListeners();
        if (sortByHarvestBtn != null) sortByHarvestBtn.onClick.RemoveAllListeners();
    }
}
