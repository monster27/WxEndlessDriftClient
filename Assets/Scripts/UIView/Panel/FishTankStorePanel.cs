// ============================================================
// 文件: FishTankStorePanel.cs
// 说明: 鱼缸存储面板 - 显示单个容器的鱼列表
// 路径: Assets/Scripts/UIView/Panel/
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using static PlayerDataManager;

public enum FishTankPanelType
{
    Upper,
    Lower
}

public class FishTankStorePanel : MonoBehaviour
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 标题 =====")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text capacityText;

    [Header("===== 切换按钮 =====")]
    [SerializeField] private Button leftSwitchBtn;
    [SerializeField] private Button rightSwitchBtn;

    [Header("===== 鱼容器 =====")]
    [SerializeField] private Transform fishContainer;
    [SerializeField] private GameObject fishPrefab;

    [Header("===== 锁定 =====")]
    [SerializeField] private Button lockBtn;
    [SerializeField] private GameObject lockIcon;

    // ============================================================
    // 数据
    // ============================================================

    private int _currentIndex = 0;
    private FishTankPanelType _panelType = FishTankPanelType.Upper;
    private FishTankStoreData _currentData;

    private List<UI_FishTankStorePrefab> _fishItems = new List<UI_FishTankStorePrefab>();
    private Dictionary<int, UI_FishTankStorePrefab> _activeFishItems = new Dictionary<int, UI_FishTankStorePrefab>();

    private Action<FishDetailData, FishTankStoreData, FishTankStoreData> _onFishTransfer;
    private Action<int> _onUnlockRequest;

    private bool _isInitialized = false;
    public int _lockedIndex = -1;

    public bool IsInitialized => _isInitialized;
    public int CurrentIndex => _currentIndex;

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(int startIndex = 0, bool isEnableDebug = false)
    {
        enableDebugLog = isEnableDebug;
        _currentIndex = startIndex;

        _isInitialized = true;
        SetupUI();
        RegisterEvents();

        LogDebug($"初始化完成, 起始索引={startIndex}");
        RefreshData();
    }

    private void SetupUI()
    {
        if (leftSwitchBtn != null)
        {
            leftSwitchBtn.onClick.RemoveAllListeners();
            leftSwitchBtn.onClick.AddListener(OnLeftSwitch);
        }
        if (rightSwitchBtn != null)
        {
            rightSwitchBtn.onClick.RemoveAllListeners();
            rightSwitchBtn.onClick.AddListener(OnRightSwitch);
        }
        if (lockBtn != null)
        {
            lockBtn.onClick.RemoveAllListeners();
            lockBtn.onClick.AddListener(OnLockClick);
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
        LogDebug("收到 DataUpdated 消息");
        RefreshData();
    }

    // ============================================================
    // 回调设置
    // ============================================================

    public void SetTransferCallback(Action<FishDetailData, FishTankStoreData, FishTankStoreData> callback)
    {
        _onFishTransfer = callback;
    }

    public void SetUnlockCallback(Action<int> callback)
    {
        _onUnlockRequest = callback;
    }

    public void SetLockedIndex(int index)
    {
        _lockedIndex = index;
    }

    // ============================================================
    // 数据读取（通过Service）
    // ============================================================

    private FishTankStoreData GetCurrentData()
    {
        if (PlayerDataService.Instance == null)
            return null;

        return PlayerDataService.Instance.GetStoreData(_currentIndex);
    }

    private int GetTotalContainerCount()
    {
        if (PlayerDataService.Instance == null)
            return 1;

        return 1 + PlayerDataService.Instance.GetTankCount();
    }

    // ============================================================
    // 刷新
    // ============================================================

    public void RefreshData()
    {
        if (!_isInitialized) return;

        int total = GetTotalContainerCount();
        if (_currentIndex >= total)
            _currentIndex = total - 1;
        if (_currentIndex < 0)
            _currentIndex = 0;

        _currentData = GetCurrentData();
        RenderCurrentContainer();
    }

    public void SetCurrentIndex(int index)
    {
        int total = GetTotalContainerCount();
        if (index < 0 || index >= total) return;

        _currentIndex = index;
        RefreshData();
    }

    // ============================================================
    // 渲染
    // ============================================================

    private void RenderCurrentContainer()
    {
        LogDebug($"RenderCurrentContainer: _currentData==null? {_currentData == null}");

        var current = _currentData;

        if (current == null)
        {
            LogDebug("RenderCurrentContainer: current is null, clearing items");
            ClearAllFishItems();
            UpdateTitleAndCapacity(null);
            UpdateLockUI(false, false);
            UpdateSwitchButtons();
            return;
        }

        LogDebug($"RenderCurrentContainer: IsBag={current.IsBag}, IsUnlocked={current.IsUnlocked}, FishList.Count={current.FishList?.Count ?? 0}, Name={current.Name}");

        bool isUnlocked = current.IsUnlocked;
        bool isBag = current.IsBag;

        UpdateLockUI(isUnlocked, isBag);

        if (!isUnlocked && !isBag)
        {
            LogDebug($"RenderCurrentContainer: {current.Name} is locked");
            ClearAllFishItems();
            if (titleText != null) titleText.text = current.Name;
            if (capacityText != null) capacityText.text = "🔒 未解锁";
            UpdateSwitchButtons();
            return;
        }

        UpdateTitleAndCapacity(current);

        // ✅ 添加日志：渲染前
        LogDebug($"RenderCurrentContainer: 准备渲染鱼列表, FishList.Count={current.FishList?.Count ?? 0}");

        if (current.FishList == null || current.FishList.Count == 0)
        {
            LogDebug("RenderCurrentContainer: FishList is empty or null, clearing items");
            ClearAllFishItems();
            UpdateSwitchButtons();
            return;
        }

        // 打印每条鱼的信息
        foreach (var fish in current.FishList)
        {
            LogDebug($"RenderCurrentContainer: fish.id={fish.id}, fishId={fish.fishId}, location={fish.location}, tankId={fish.tankId}");
        }

        UpdateFishItems(current);
        UpdateSwitchButtons();

        LogDebug($"RenderCurrentContainer: 渲染完成, activeFishItems={_activeFishItems.Count}");
    }

    private void UpdateSwitchButtons()
    {
        int total = GetTotalContainerCount();
        if (leftSwitchBtn != null) leftSwitchBtn.interactable = total > 1;
        if (rightSwitchBtn != null) rightSwitchBtn.interactable = total > 1;
    }

    private void UpdateTitleAndCapacity(FishTankStoreData current)
    {
        if (current == null)
        {
            if (titleText != null) titleText.text = "空";
            if (capacityText != null) capacityText.text = "0/0";
            return;
        }

        if (titleText != null) titleText.text = current.Name;

        if (capacityText != null)
        {
            int fishCount = current.FishList?.Count ?? 0;
            int maxCapacity = current.MaxCapacity;

            if (current.IsBag && fishCount > maxCapacity)
            {
                capacityText.text = $"{fishCount}/{maxCapacity} ⚠️";
                capacityText.color = Color.red;
            }
            else
            {
                capacityText.text = $"{fishCount}/{maxCapacity}";
                capacityText.color = Color.black;
            }
        }
    }

    private void UpdateLockUI(bool isUnlocked, bool isBag)
    {
        if (isBag)
        {
            if (lockIcon != null) lockIcon.SetActive(false);
            if (lockBtn != null) lockBtn.gameObject.SetActive(false);
            return;
        }

        if (lockIcon != null) lockIcon.SetActive(!isUnlocked);
        if (lockBtn != null) lockBtn.gameObject.SetActive(!isUnlocked);
    }

    // ============================================================
    // 鱼项管理
    // ============================================================

    private void UpdateFishItems(FishTankStoreData current)
    {
        if (current.FishList == null || current.FishList.Count == 0)
        {
            ClearAllFishItems();
            return;
        }

        // 构建当前鱼ID集合
        HashSet<int> currentFishIds = new HashSet<int>();
        foreach (var fishData in current.FishList)
        {
            if (fishData != null && fishData.id > 0)
                currentFishIds.Add(fishData.id);
        }

        // 移除不在当前列表中的鱼
        List<int> toRemove = new List<int>();
        foreach (var kvp in _activeFishItems)
        {
            if (!currentFishIds.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }

        foreach (var fishId in toRemove)
        {
            if (_activeFishItems.TryGetValue(fishId, out var item))
            {
                Destroy(item.gameObject);
                _activeFishItems.Remove(fishId);
                _fishItems.Remove(item);
            }
        }

        // 添加新鱼或更新现有鱼
        foreach (var fishData in current.FishList)
        {
            if (fishData == null || fishData.id <= 0) continue;

            if (_activeFishItems.TryGetValue(fishData.id, out var existingItem))
            {
                existingItem.UpdateData(fishData);
            }
            else
            {
                GameObject itemObj = Instantiate(fishPrefab, fishContainer);
                var newItem = itemObj.GetComponent<UI_FishTankStorePrefab>();
                if (newItem == null) continue;

                newItem.Init(fishData);
                newItem.SetClickCallback(OnFishItemClick);
                newItem.gameObject.SetActive(true);

                _activeFishItems[fishData.id] = newItem;
                _fishItems.Add(newItem);
            }
        }
    }

    private void ClearAllFishItems()
    {
        foreach (var kvp in _activeFishItems)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
                Destroy(kvp.Value.gameObject);
        }
        _activeFishItems.Clear();
        _fishItems.Clear();
    }

    // ============================================================
    // 按钮事件
    // ============================================================

    private void OnLeftSwitch()
    {
        int total = GetTotalContainerCount();
        if (total <= 1) return;

        int newIndex = _currentIndex;
        int maxAttempts = total;

        for (int i = 0; i < maxAttempts; i++)
        {
            newIndex = (_currentIndex - 1 - i + total) % total;

            if (_lockedIndex >= 0 && newIndex == _lockedIndex)
                continue;

            break;
        }

        if (newIndex != _currentIndex)
        {
            _currentIndex = newIndex;
            RefreshData();

            if (transform.parent != null)
            {
                var otherPanel = GetOtherPanel();
                if (otherPanel != null)
                    otherPanel.SetLockedIndex(_currentIndex);
            }
        }
    }

    private void OnRightSwitch()
    {
        int total = GetTotalContainerCount();
        if (total <= 1) return;

        int newIndex = _currentIndex;
        int maxAttempts = total;

        for (int i = 0; i < maxAttempts; i++)
        {
            newIndex = (_currentIndex + 1 + i) % total;

            if (_lockedIndex >= 0 && newIndex == _lockedIndex)
                continue;

            break;
        }

        if (newIndex != _currentIndex)
        {
            _currentIndex = newIndex;
            RefreshData();

            var otherPanel = GetOtherPanel();
            if (otherPanel != null)
                otherPanel.SetLockedIndex(_currentIndex);
        }
    }

    private void OnLockClick()
    {
        if (_currentData == null || _currentData.IsUnlocked || _currentData.IsBag) return;
        _onUnlockRequest?.Invoke(_currentData.TankId);
    }

    private void OnFishItemClick(UI_FishTankStorePrefab fishItem)
    {
        if (fishItem == null || fishItem.FishDetail == null) return;
        if (_onFishTransfer == null) return;

        if (_currentData == null) return;

        if (!_currentData.IsBag && !_currentData.IsUnlocked)
        {
            GameUIManager.ShowMessage($"{_currentData.Name} 未解锁，请先解锁");
            return;
        }

        var targetPanel = GetOtherPanel();
        if (targetPanel == null) return;

        var toData = targetPanel.GetCurrentData();
        if (toData == null) return;

        if (!toData.IsBag && !toData.IsUnlocked)
        {
            GameUIManager.ShowMessage($"{toData.Name} 未解锁，请先解锁");
            return;
        }

        _onFishTransfer?.Invoke(fishItem.FishDetail, _currentData, toData);
    }

    // ============================================================
    // 辅助方法
    // ============================================================

    private FishTankStorePanel GetOtherPanel()
    {
        Transform parent = transform.parent;
        if (parent == null) return null;

        foreach (Transform child in parent)
        {
            var panel = child.GetComponent<FishTankStorePanel>();
            if (panel != null && panel != this && panel.IsInitialized)
                return panel;
        }
        return null;
    }

    // ============================================================
    // 生命周期
    // ============================================================

    private void OnDestroy()
    {
        UnregisterEvents();

        if (leftSwitchBtn != null) leftSwitchBtn.onClick.RemoveAllListeners();
        if (rightSwitchBtn != null) rightSwitchBtn.onClick.RemoveAllListeners();
        if (lockBtn != null) lockBtn.onClick.RemoveAllListeners();

        ClearAllFishItems();
    }

    // ============================================================
    // 日志
    // ============================================================

    private void LogDebug(string message)
    {
        if (enableDebugLog)
            Z_Logger.Log($"[FishTankStorePanel] {message}");
    }
}
