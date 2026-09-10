// 路径：Assets/Scripts/UIView/Panel/FishTankManagerPanel.cs
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
    [SerializeField] private Button sortByRarityBtn;
    [SerializeField] private Button sortByPriceBtn;

    [Header("===== 收益显示 =====")]
    [SerializeField] private GameObject harvestInfoObj;
    [SerializeField] private Text harvestTitleText;
    [SerializeField] private Text harvestValueText;

    private Action<FishDetailData, FishTankStoreData, FishTankStoreData> _onFishTransfer;
    private Action<int> _onUnlockRequest;
    private Action _onCloseCallback;
    private bool _isInitialized = false;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void Init(bool isEnableDebug = false)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        enableDebugLog = isEnableDebug;

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

        SetupUI();
        RegisterEvents();
    }

    private void SetupUI()
    {
        if (closeBtn != null)
        {
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(() =>
            {
                ClosePanel();
                _onCloseCallback?.Invoke();
            });
        }

        if (sortByRarityBtn != null)
        {
            sortByRarityBtn.onClick.RemoveAllListeners();
            sortByRarityBtn.onClick.AddListener(OnSortByRarity);
        }
        if (sortByPriceBtn != null)
        {
            sortByPriceBtn.onClick.RemoveAllListeners();
            sortByPriceBtn.onClick.AddListener(OnSortByPrice);
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void RegisterEvents()
    {
        UnregisterEvents();
        CommunicateEvent.Register(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister(FishTankMessage.DataUpdated.ToString(), OnDataUpdated);
    }

    private void OnDataUpdated()
    {
        if (!_isInitialized) return;
        if (!gameObject.activeSelf) return;
        RefreshData();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
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

    public void SetCloseCallback(Action callback)
    {
        _onCloseCallback = callback;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
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
        if (upperStorePanel != null) upperStorePanel.SetCurrentIndex(0);
        if (lowerStorePanel != null) lowerStorePanel.SetCurrentIndex(tankIndex + 1);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
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
                    foreach (var fish in fishList)
                    {
                        hourlyEarning += Mathf.RoundToInt(fish.calculatedPrice * LoadDataManager.Instance.baseEarningRate);
                    }
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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void OpenPanel()
    {
        gameObject.SetActive(true);
        RefreshData();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    private void OnSortByRarity()
    {
        if (upperStorePanel != null) upperStorePanel.SortByRarity();
        if (lowerStorePanel != null) lowerStorePanel.SortByRarity();
    }

    private void OnSortByPrice()
    {
        if (upperStorePanel != null) upperStorePanel.SortByPrice();
        if (lowerStorePanel != null) lowerStorePanel.SortByPrice();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnFishTransferRequest(FishDetailData fishData, FishTankStoreData fromContainer, FishTankStoreData toContainer)
    {
        _onFishTransfer?.Invoke(fishData, fromContainer, toContainer);
    }

    private void OnUnlockRequest(int tankId)
    {
        _onUnlockRequest?.Invoke(tankId);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnDestroy()
    {
        UnregisterEvents();
        if (closeBtn != null) closeBtn.onClick.RemoveAllListeners();
        if (sortByRarityBtn != null) sortByRarityBtn.onClick.RemoveAllListeners();
        if (sortByPriceBtn != null) sortByPriceBtn.onClick.RemoveAllListeners();
    }

    private void LogDebug(string message)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankManagerPanel] {message}");
    }
}
