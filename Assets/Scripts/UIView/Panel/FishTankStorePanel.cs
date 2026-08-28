// ============================================================
// 文件: FishTankStorePanel.cs
// 说明: 鱼缸存储面板 - 显示单个容器的鱼列表（使用对象池）
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

    [Header("===== 对象池 =====")]
    [SerializeField] private int poolInitialCapacity = 10;  // 初始容量

    // ============================================================
    // 数据
    // ============================================================

    private int _currentIndex = 0;
    private FishTankPanelType _panelType = FishTankPanelType.Upper;
    private FishTankStoreData _currentData;

    private Dictionary<int, UI_FishTankStorePrefab> _activeFishItems = new Dictionary<int, UI_FishTankStorePrefab>();

    private Action<FishDetailData, FishTankStoreData, FishTankStoreData> _onFishTransfer;
    private Action<int> _onUnlockRequest;

    private bool _isInitialized = false;
    public int _lockedIndex = -1;

    public bool IsInitialized => _isInitialized;
    public int CurrentIndex => _currentIndex;

    // 对象池
    private UI_FishItemPool _fishItemPool;

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(int startIndex = 0, bool isEnableDebug = false)
    {
        enableDebugLog = isEnableDebug;
        _currentIndex = startIndex;

        // 创建对象池
        _fishItemPool = new UI_FishItemPool(fishPrefab, fishContainer, poolInitialCapacity);

        _isInitialized = true;
        SetupUI();
        RegisterEvents();

        LogDebug($"初始化完成, 起始索引={startIndex}, 池初始容量={poolInitialCapacity}");
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

        LogDebug($"RenderCurrentContainer: 准备渲染鱼列表, FishList.Count={current.FishList?.Count ?? 0}");

        if (current.FishList == null || current.FishList.Count == 0)
        {
            LogDebug("RenderCurrentContainer: FishList is empty or null, clearing items");
            ClearAllFishItems();
            UpdateSwitchButtons();
            return;
        }

        UpdateFishItems(current);
        UpdateSwitchButtons();
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
    // 鱼项管理（使用对象池）
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

        // 移除不在当前列表中的鱼（回收至池）
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
                _fishItemPool.Return(item);          // 回收
                _activeFishItems.Remove(fishId);
            }
        }

        // 添加新鱼或更新现有鱼
        foreach (var fishData in current.FishList)
        {
            if (fishData == null || fishData.id <= 0) continue;

            if (_activeFishItems.TryGetValue(fishData.id, out var existingItem))
            {
                // 已有：只更新数据
                existingItem.UpdateData(fishData);
            }
            else
            {
                // 新增：从池中取出
                var newItem = _fishItemPool.Get();
                newItem.Init(fishData);
                newItem.SetClickCallback(OnFishItemClick);
                newItem.gameObject.SetActive(true);

                _activeFishItems[fishData.id] = newItem;
            }
        }
    }

    private void ClearAllFishItems()
    {
        // 将所有活动项回收至池
        foreach (var kvp in _activeFishItems)
        {
            if (kvp.Value != null)
                _fishItemPool.Return(kvp.Value);
        }
        _activeFishItems.Clear();
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

        // 清空对象池（销毁所有对象）
        if (_fishItemPool != null)
            _fishItemPool.Clear();
    }

    // ============================================================
    // 日志
    // ============================================================

    private void LogDebug(string message)
    {
        if (enableDebugLog)
            Z_Logger.Log($"[FishTankStorePanel] {message}");
    }

    // ============================================================
    // 内部对象池类
    // ============================================================

    private class UI_FishItemPool
    {
        private GameObject _prefab;
        private Transform _parent;
        private Queue<UI_FishTankStorePrefab> _pool = new Queue<UI_FishTankStorePrefab>();
        private List<UI_FishTankStorePrefab> _allObjects = new List<UI_FishTankStorePrefab>(); // 跟踪所有已创建的对象

        public UI_FishItemPool(GameObject prefab, Transform parent, int initialCapacity)
        {
            _prefab = prefab;
            _parent = parent;
            // 预创建 initialCapacity 个对象
            for (int i = 0; i < initialCapacity; i++)
            {
                CreateNewObject();
            }
        }

        /// <summary>
        /// 创建一个新对象（不激活），加入池
        /// </summary>
        private UI_FishTankStorePrefab CreateNewObject()
        {
            GameObject go = GameObject.Instantiate(_prefab, _parent);
            go.SetActive(false);
            var item = go.GetComponent<UI_FishTankStorePrefab>();
            if (item == null)
            {
                Debug.LogError("UI_FishItemPool: 预制体缺少 UI_FishTankStorePrefab 组件");
                return null;
            }
            _allObjects.Add(item);
            _pool.Enqueue(item);  // 直接放入池中备用
            return item;
        }

        /// <summary>
        /// 从池中取出一个对象（激活状态）
        /// </summary>
        public UI_FishTankStorePrefab Get()
        {
            UI_FishTankStorePrefab item;
            if (_pool.Count > 0)
            {
                item = _pool.Dequeue();
            }
            else
            {
                // 池中无空闲对象，动态扩容（创建新对象）
                item = CreateNewObject();
                // 新创建的对象已入池，需要取出（从池中取出，但刚创建的已在队列末尾，需要 Dequeue）
                // 但上面 CreateNewObject 直接将对象入队，所以需要从队列中取出
                // 而因为我们是先创建再加入，现在队列非空，Dequeue 会取出刚加入的那个
                item = _pool.Dequeue();  // 取出刚入队的对象
            }
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>
        /// 回收对象（禁用并放回池）
        /// </summary>
        public void Return(UI_FishTankStorePrefab item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            // 如果对象是从池中创建的（应该在 _allObjects 中），可以放回
            // 但是为了安全，检查是否已经在池中（避免重复入队）
            if (!_pool.Contains(item) && _allObjects.Contains(item))
            {
                _pool.Enqueue(item);
            }
            else
            {
                // 如果对象不在 _allObjects 中（可能被外部销毁），忽略
                Debug.LogWarning("UI_FishItemPool: 尝试回收一个不属于该池的对象");
            }
        }

        /// <summary>
        /// 清空池，销毁所有对象
        /// </summary>
        public void Clear()
        {
            foreach (var item in _allObjects)
            {
                if (item != null && item.gameObject != null)
                    GameObject.Destroy(item.gameObject);
            }
            _pool.Clear();
            _allObjects.Clear();
        }
    }
}
