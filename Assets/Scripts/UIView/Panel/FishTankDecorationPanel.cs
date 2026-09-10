// 路径：Assets/Scripts/UIView/Panel/FishTankDecorationPanel.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Linq;
using static PlayerDataManager;

public class FishTankDecorationPanel : MonoBehaviour
{
    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    [Header("===== 关闭按钮 =====")]
    [SerializeField] private Button closeBtn;

    [Header("===== 品类 Toggle 组 =====")]
    [SerializeField] private ToggleGroup categoryToggleGroup;
    [SerializeField] private Toggle toggle80;
    [SerializeField] private Toggle toggle81;
    [SerializeField] private Toggle toggle82;
    [SerializeField] private Toggle toggle83;
    [SerializeField] private Toggle toggle84;

    [Header("===== 容器和预制体 =====")]
    [SerializeField] private Transform decorationContainer;
    [SerializeField] private GameObject decPrefab;

    [Header("===== 操作面板引用 =====")]
    [SerializeField] private UI_FishTankDecOperator decOperator;

    [Header("===== 对象池 =====")]
    [SerializeField] private int poolInitSize = 5;

    // 数据
    private int _currentCategory = 80;
    private int _currentTankId = 1;
    private Dictionary<int, List<DecorationEquipInfo>> _equippedStatus = new Dictionary<int, List<DecorationEquipInfo>>();
    private List<FishTankDecData> _allDecConfigs = new List<FishTankDecData>();

    private DecorationObjectPool _pool;
    private List<UI_FishTankDecPrefab> _activeItems = new List<UI_FishTankDecPrefab>();
    private bool _isInitialized = false;

    // 回调
    private Action _onCloseCallback;
    private Action<FishTankDecData> _onItemClickCallback;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 初始化
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void Init(bool isEnableDebug = false)
    {
        if (_isInitialized) return;
        enableDebugLog = isEnableDebug;

        _allDecConfigs = LoadDataManager.Instance?.fishTankDecorations ?? new List<FishTankDecData>();
        _pool = new DecorationObjectPool(decPrefab, decorationContainer, poolInitSize);

        SetupToggles();
        SetupCloseButton();

        if (decOperator != null) decOperator.EnsureInit();

        _isInitialized = true;
        LogDebug("初始化完成");
    }

    private void SetupCloseButton()
    {
        if (closeBtn == null) return;
        closeBtn.onClick.RemoveAllListeners();
        closeBtn.onClick.AddListener(() =>
        {
            HideDecOperator();
            gameObject.SetActive(false);
            _onCloseCallback?.Invoke();
        });
    }

    private void SetupToggles()
    {
        if (categoryToggleGroup == null) return;
        categoryToggleGroup.SetAllTogglesOff();

        if (toggle80 != null) toggle80.onValueChanged.AddListener(isOn => { if (isOn) OnCategoryChanged(80); });
        if (toggle81 != null) toggle81.onValueChanged.AddListener(isOn => { if (isOn) OnCategoryChanged(81); });
        if (toggle82 != null) toggle82.onValueChanged.AddListener(isOn => { if (isOn) OnCategoryChanged(82); });
        if (toggle83 != null) toggle83.onValueChanged.AddListener(isOn => { if (isOn) OnCategoryChanged(83); });
        if (toggle84 != null) toggle84.onValueChanged.AddListener(isOn => { if (isOn) OnCategoryChanged(84); });

        if (toggle80 != null) toggle80.isOn = true;
        else _currentCategory = 80;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 对外接口
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void SetCloseCallback(Action callback) { _onCloseCallback = callback; }
    public void SetItemClickCallback(Action<FishTankDecData> callback) { _onItemClickCallback = callback; }

    public void SetOperatorCallbacks(
        Action<int, float, float> onDragMove,
        Action<int, float, float> onDragEnd,
        Action<int> onMirror,
        Action<int> onRemove)
    {
        if (decOperator != null)
            decOperator.SetCallbacks(onDragMove, onDragEnd, onMirror, onRemove);
    }

    public void Open(int tankId)
    {
        _currentTankId = tankId;
        RefreshData();
    }

    public void Close()
    {
        HideDecOperator();
        gameObject.SetActive(false);
    }

    public void RefreshData()
    {
        if (!_isInitialized) return;

        // ★ 不再拉取 _ownedDecorationIds，改为直接查背包数量
        _equippedStatus = PlayerDataService.Instance?.GetEquippedDecorations(_currentTankId)
                          ?? new Dictionary<int, List<DecorationEquipInfo>>();

        RenderCurrentCategory();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 操作面板控制
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public bool IsDecOperatorShowing => decOperator != null && decOperator.IsShowing;
    public int CurrentDecOperatorRecordId => decOperator != null ? decOperator.CurrentRecordId : 0;

    public void ShowDecOperator(int tankId, int recordId, int category, int decorationId, RectTransform decorationRect)
    {
        if (decOperator == null) return;
        decOperator.ShowForDecoration(tankId, recordId, category, decorationId, decorationRect);
    }

    public void HideDecOperator()
    {
        if (decOperator != null) decOperator.Hide();
    }

    public void RefreshDecOperatorPosition()
    {
        if (decOperator != null) decOperator.UpdatePosition();
    }

    public void RebindDecOperatorTarget(int recordId, RectTransform newRect)
    {
        if (decOperator == null) return;
        if (!decOperator.IsShowing) return;
        if (decOperator.CurrentRecordId != recordId) return;

        if (newRect != null) decOperator.RebindTarget(newRect);
        else decOperator.Hide();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnCategoryChanged(int category)
    {
        if (_currentCategory == category) return;
        _currentCategory = category;
        RenderCurrentCategory();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 渲染
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void RenderCurrentCategory()
    {
        ClearActiveItems();

        var configs = _allDecConfigs
            .Where(c => c.categoryId == _currentCategory)
            .OrderBy(c => c.id)
            .ToList();

        if (configs.Count == 0) return;

        for (int i = 0; i < configs.Count; i++)
        {
            var config = configs[i];
            UI_FishTankDecPrefab item = _pool.Get();
            item.transform.SetParent(decorationContainer, false);
            item.transform.SetSiblingIndex(i);   // ★ 强制按 ID 顺序
            item.gameObject.SetActive(true);

            // ★ 从背包查真实数量（不再是 owned ? 1 : 0）
            int ownedQuantity = PlayerDataService.Instance?.GetOwnedDecorationQuantity(config.id) ?? 0;
            bool owned = ownedQuantity > 0;

            // 当前品类已装备数量
            int equippedCount = 0;
            if (_equippedStatus.TryGetValue(_currentCategory, out var list))
            {
                foreach (var info in list)
                {
                    if (info.DecorationId == config.id) equippedCount++;
                }
            }

            // 80/81 跨品类统计；82-84 只统计当前品类
            int totalEquippedCount = 0;
            if (_currentCategory == 80 || _currentCategory == 81)
            {
                foreach (var kvp in _equippedStatus)
                {
                    foreach (var info in kvp.Value)
                    {
                        if (info.DecorationId == config.id)
                            totalEquippedCount++;
                    }
                }
            }
            else
            {
                totalEquippedCount = equippedCount;
            }

            bool equipped = totalEquippedCount > 0;
            int unEquippedCount = Mathf.Max(0, ownedQuantity - totalEquippedCount);

            item.Init(config, owned, equipped, totalEquippedCount, unEquippedCount, _currentTankId, OnDecorationItemClickedInternal);
            _activeItems.Add(item);
        }
    }

    private void ClearActiveItems()
    {
        foreach (var item in _activeItems)
        {
            if (item != null)
            {
                item.gameObject.SetActive(false);
                _pool.Return(item);
            }
        }
        _activeItems.Clear();
    }

    private void OnDecorationItemClickedInternal(UI_FishTankDecPrefab item)
    {
        if (item == null || item.Config == null) return;
        _onItemClickCallback?.Invoke(item.Config);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 对象池
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private class DecorationObjectPool
    {
        private GameObject _prefab;
        private Transform _parent;
        private Queue<UI_FishTankDecPrefab> _pool = new Queue<UI_FishTankDecPrefab>();
        private List<UI_FishTankDecPrefab> _allObjects = new List<UI_FishTankDecPrefab>();

        public DecorationObjectPool(GameObject prefab, Transform parent, int initialSize)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < initialSize; i++) CreateNewObject();
        }

        private UI_FishTankDecPrefab CreateNewObject()
        {
            GameObject go = GameObject.Instantiate(_prefab, _parent);
            go.SetActive(false);
            var item = go.GetComponent<UI_FishTankDecPrefab>();
            if (item == null)
            {
                Z_Logger.LogError("DecorationObjectPool: 预制体缺少 UI_FishTankDecPrefab 组件");
                return null;
            }
            _allObjects.Add(item);
            _pool.Enqueue(item);
            return item;
        }

        public UI_FishTankDecPrefab Get()
        {
            if (_pool.Count == 0)
            {
                for (int i = 0; i < 5; i++) CreateNewObject();
            }
            return _pool.Dequeue();
        }

        public void Return(UI_FishTankDecPrefab item)
        {
            if (item == null) return;
            if (!_pool.Contains(item) && _allObjects.Contains(item))
                _pool.Enqueue(item);
        }

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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnDestroy()
    {
        if (_pool != null) _pool.Clear();
        if (closeBtn != null) closeBtn.onClick.RemoveAllListeners();
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankDecorationPanel] {msg}");
    }
}
