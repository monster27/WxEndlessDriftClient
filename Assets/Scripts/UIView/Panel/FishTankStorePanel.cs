// ============================================================
// 文件: FishTankStorePanel.cs
// 说明: 鱼缸存储面板 - 显示单个容器的鱼列表
// 路径: Assets/Scripts/UIView/Panel/
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Linq;
using static NetServerManager;

public enum FishTankPanelType
{
    Upper,
    Lower
}

public class FishTankStorePanel : MonoBehaviour
{
    [Header("===== 调试 =====")]
    private bool enableDebugLog = false;

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
    // 数据（只保留UI状态）
    // ============================================================

    private int _currentIndex = 0;
    private FishTankPanelType _panelType = FishTankPanelType.Upper;


    public FishTankStorePanel _otherPanel = null;
    public int _lockedIndex = -1;

    private List<UI_FishTankStorePrefab> _fishItems = new List<UI_FishTankStorePrefab>();
    private Stack<UI_FishTankStorePrefab> _fishItemPool = new Stack<UI_FishTankStorePrefab>();
    private Dictionary<int, UI_FishTankStorePrefab> _activeFishItems = new Dictionary<int, UI_FishTankStorePrefab>();

    private Action<FishDetailData, FishTankStoreData, FishTankStoreData> _onFishTransfer;
    private Action<int> _onUnlockRequest;

    private bool _isInitialized = false;
    private int _lastFishListHash = 0;
    private FishTankStoreData _cachedData = null;

    public bool IsInitialized => _isInitialized;
    public int CurrentIndex => _currentIndex;

    private void LogDebug(string message)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankStorePanel] {message}");
    }

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(GameObject prefab, int startIndex = 0, FishTankPanelType panelType = FishTankPanelType.Upper, bool isEnableDebug = false)
    {
        enableDebugLog = isEnableDebug;
        fishPrefab = prefab;
        _panelType = panelType;
        _currentIndex = startIndex;

        _isInitialized = true;
        SetupUI();

        LogDebug($"初始化完成, 类型={panelType}, 起始索引={startIndex}");

        // ✅ 注册鱼篓变化事件
        CommunicateEvent.Register("FishBagChanged", OnBagDataChanged);
        CommunicateEvent.Register("FishTankChanged", OnTankDataChanged);
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

        RefreshData();
    }

    public void SetTransferCallback(Action<FishDetailData, FishTankStoreData, FishTankStoreData> callback)
    {
        _onFishTransfer = callback;
    }

    public void SetUnlockCallback(Action<int> callback)
    {
        _onUnlockRequest = callback;
    }

    // ============================================================
    // 事件处理
    // ============================================================

    private void OnBagDataChanged()
    {
        if (_currentIndex == 0) // 当前显示的是鱼篓
        {
            RefreshData();
        }
        else
        {
            // 鱼篓变化但当前显示的是鱼缸，只更新Hash缓存
            UpdateBagHashCache();
        }
    }

    private void OnTankDataChanged()
    {
        // 鱼缸变化，检查是否影响当前显示
        RefreshData();
    }

    private void UpdateBagHashCache()
    {
        if (PlayerDataService.Instance == null) return;
        var bagList = PlayerDataService.Instance.GetBagFishList();
        _lastFishListHash = CalculateFishListHash(bagList);
    }

    private int CalculateFishListHash(List<FishDetailData> list)
    {
        if (list == null || list.Count == 0) return 0;
        int hash = 0;
        foreach (var fish in list)
        {
            if (fish != null)
                hash ^= fish.id.GetHashCode();
        }
        return hash;
    }

    // ============================================================
    // 数据读取（通过 PlayerDataService）
    // ============================================================

    private List<FishTankStatusData> GetTankList()
    {
        if (PlayerDataService.Instance == null)
            return new List<FishTankStatusData>();
        return PlayerDataService.Instance.GetTankList();
    }

    private FishTankStoreData GetBagData()
    {
        if (PlayerDataService.Instance == null) return null;

        // ✅ 使用 PlayerDataService 的 GetBagFishList()
        var fishList = PlayerDataService.Instance.GetBagFishList();

        return new FishTankStoreData
        {
            TankId = 0,
            Name = "鱼篓",
            IsBag = true,
            IsSpecial = false,
            PurchaseCost = 0,
            MaxCapacity = PlayerDataService.Instance.GetBagCapacity(),
            FishList = fishList,
            IsUnlocked = true
        };
    }

    public FishTankStoreData GetCurrentData()
    {
        if (PlayerDataService.Instance == null) return null;

        if (_currentIndex == 0)
            return GetBagData();  // 使用上面修复后的方法

        var tanks = PlayerDataService.Instance.GetTankList();
        int tankIndex = _currentIndex - 1;
        if (tankIndex < 0 || tankIndex >= tanks.Count) return null;

        var tank = tanks[tankIndex];
        var config = LoadDataManager.Instance?.GetFishTankConfig(tank.tankId);
        var fishList = PlayerDataService.Instance.GetTankFishList(tank.tankId);

        return new FishTankStoreData
        {
            TankId = tank.tankId,
            Name = config?.name ?? $"鱼缸{tank.tankId}",
            IsBag = false,
            IsSpecial = config?.type == "special",
            PurchaseCost = config?.purchaseCost ?? 0,
            MaxCapacity = tank.capacity,
            FishList = fishList,
            IsUnlocked = tank.isUnlocked
        };
    }

    private int GetTotalContainerCount()
    {
        return 1 + GetTankList().Count;
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
        var current = GetCurrentData();
        _cachedData = current;

        if (current == null)
        {
            ClearAllFishItems();
            UpdateTitleAndCapacity(null);
            UpdateLockUI(false, false);
            return;
        }

        bool isUnlocked = current.IsUnlocked;
        bool isBag = current.IsBag;

        UpdateLockUI(isUnlocked, isBag);

        if (!isUnlocked && !isBag)
        {
            ClearAllFishItems();
            if (titleText != null) titleText.text = current.Name;
            if (capacityText != null) capacityText.text = "🔒 未解锁";
            return;
        }

        UpdateTitleAndCapacity(current);
        UpdateFishItems(current);

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

        int newHash = CalculateFishListHash(current.FishList);

        if (newHash == _lastFishListHash && _activeFishItems.Count == current.FishList.Count)
        {
            return;
        }
        _lastFishListHash = newHash;

        HashSet<int> newFishIds = new HashSet<int>();
        foreach (var fishData in current.FishList)
        {
            if (fishData == null || fishData.fishId <= 0) continue;
            newFishIds.Add(fishData.id);
        }

        List<int> toRemove = new List<int>();
        foreach (var kvp in _activeFishItems)
        {
            if (!newFishIds.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }

        foreach (var fishId in toRemove)
        {
            if (_activeFishItems.TryGetValue(fishId, out var item))
            {
                ReturnFishItemToPool(item);
                _activeFishItems.Remove(fishId);
                _fishItems.Remove(item);
            }
        }

        foreach (var fishData in current.FishList)
        {
            if (fishData == null || fishData.fishId <= 0) continue;

            if (_activeFishItems.TryGetValue(fishData.id, out var existingItem))
            {
                existingItem.UpdateData(fishData);
            }
            else
            {
                var newItem = GetFishItemFromPool();
                if (newItem == null)
                {
                    GameObject itemObj = Instantiate(fishPrefab, fishContainer);
                    newItem = itemObj.GetComponent<UI_FishTankStorePrefab>();
                    if (newItem == null) continue;
                }

                newItem.Init(fishData);
                newItem.SetClickCallback(OnFishItemClick);
                newItem.gameObject.SetActive(true);

                _activeFishItems[fishData.id] = newItem;
                _fishItems.Add(newItem);
            }
        }
    }

    // ============================================================
    // 对象池
    // ============================================================

    private UI_FishTankStorePrefab GetFishItemFromPool()
    {
        while (_fishItemPool.Count > 0)
        {
            var item = _fishItemPool.Pop();
            if (item != null && item.gameObject != null)
                return item;
        }
        return null;
    }

    private void ReturnFishItemToPool(UI_FishTankStorePrefab item)
    {
        if (item == null || item.gameObject == null) return;
        item.gameObject.SetActive(false);
        //item.transform.SetParent(null);
        _fishItemPool.Push(item);
    }

    private void ClearAllFishItems()
    {
        foreach (var kvp in _activeFishItems)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
            {
                ReturnFishItemToPool(kvp.Value);
            }
        }
        _activeFishItems.Clear();
        _fishItems.Clear();
        _lastFishListHash = 0;
    }

    // ============================================================
    // 按钮事件
    // ============================================================

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

            // ✅ 只需要检查是否与锁定索引冲突
            if (_lockedIndex >= 0 && newIndex == _lockedIndex)
            {
                continue; // 被锁定了，继续往左找
            }

            break; // 找到了可用索引
        }

        if (newIndex != _currentIndex)
        {
            _currentIndex = newIndex;
            RefreshData();

            // ✅ 通知另一个面板更新锁定索引
            if (_otherPanel != null)
            {
                _otherPanel.SetLockedIndex(_currentIndex);
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

            // ✅ 只需要检查是否与锁定索引冲突
            if (_lockedIndex >= 0 && newIndex == _lockedIndex)
            {
                continue; // 被锁定了，继续往右找
            }

            break; // 找到了可用索引
        }

        if (newIndex != _currentIndex)
        {
            _currentIndex = newIndex;
            RefreshData();

            if (_otherPanel != null)
            {
                _otherPanel.SetLockedIndex(_currentIndex);
            }
        }
    }

    private void OnLockClick()
    {
        var current = GetCurrentData();
        if (current == null || current.IsUnlocked || current.IsBag) return;
        _onUnlockRequest?.Invoke(current.TankId);
    }

    private void OnFishItemClick(UI_FishTankStorePrefab fishItem)
    {
        if (fishItem == null || fishItem.FishDetail == null) return;
        if (_onFishTransfer == null) return;

        var fromData = GetCurrentData();
        if (fromData == null) return;

        if (!fromData.IsBag && !fromData.IsUnlocked)
        {
            GameUIManager.ShowMessage($"{fromData.Name} 未解锁，请先解锁");
            return;
        }

        FishTankStorePanel targetPanel = GetTargetPanel();
        if (targetPanel == null) return;

        var toData = targetPanel.GetCurrentData();
        if (toData == null) return;

        if (!toData.IsBag && !toData.IsUnlocked)
        {
            GameUIManager.ShowMessage($"{toData.Name} 未解锁，请先解锁");
            return;
        }

        _onFishTransfer?.Invoke(fishItem.FishDetail, fromData, toData);
    }

    /// <summary>
    /// 设置锁定索引（由另一个面板调用）
    /// </summary>
    public void SetLockedIndex(int index)
    {
        _lockedIndex = index;
    }

    private FishTankStorePanel GetTargetPanel()
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

    /// <summary>
    /// 强制刷新（重置Hash缓存，强制重新渲染）
    /// </summary>
    public void ForceRefresh()
    {
        if (!_isInitialized) return;

        // ✅ 重置Hash，强制重新渲染
        _lastFishListHash = 0;
        _cachedData = null;

        // 清空所有鱼项，重新创建
        ClearAllFishItems();

        RefreshData();

        LogDebug("强制刷新完成");
    }

    // ============================================================
    // 生命周期
    // ============================================================

    private void OnDestroy()
    {
        if (leftSwitchBtn != null) leftSwitchBtn.onClick.RemoveAllListeners();
        if (rightSwitchBtn != null) rightSwitchBtn.onClick.RemoveAllListeners();
        if (lockBtn != null) lockBtn.onClick.RemoveAllListeners();

        CommunicateEvent.Unregister("FishBagChanged", OnBagDataChanged);
        CommunicateEvent.Unregister("FishTankChanged", OnTankDataChanged);

        ClearAllFishItems();

        while (_fishItemPool.Count > 0)
        {
            var item = _fishItemPool.Pop();
            if (item != null && item.gameObject != null)
                Destroy(item.gameObject);
        }
    }
}

/// <summary>
/// 鱼缸存储数据（UI展示用）
/// </summary>
[Serializable]
public class FishTankStoreData
{
    public int TankId;
    public string Name;
    public bool IsBag;
    public bool IsSpecial;
    public int PurchaseCost;
    public int MaxCapacity;
    public List<FishDetailData> FishList = new List<FishDetailData>();
    public bool IsUnlocked = true;
}
