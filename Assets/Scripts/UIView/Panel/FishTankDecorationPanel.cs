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

    // ★ 拖动/边界处理相关
    private FishTankMainPanel _mainPanel;
    private int _currentOperatorCategory;

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

    /// <summary>
    /// ★ 注入 mainPanel，供拖动边界判断使用
    /// </summary>
    public void SetMainPanel(FishTankMainPanel panel)
    {
        _mainPanel = panel;
    }

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
        _currentOperatorCategory = category;   // ★ 记录当前品类
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
    // ★ 拖动边界处理
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 拖动装饰（带区域限制），由 View 的 OnDecDragMove 调用
    /// </summary>
    public void HandleDecDragMove(int recordId, float dx, float dy)
    {
        if (_mainPanel == null) return;

        var rect = _mainPanel.GetDecorationRect(recordId);
        if (rect == null) return;

        Rect area = GetAreaForCategory(_currentOperatorCategory);
        if (area.width <= 0f || area.height <= 0f)
        {
            // 没有区域信息，直接位移
            rect.anchoredPosition += new Vector2(dx, dy);
        }
        else
        {
            Vector2 newPos = rect.anchoredPosition + new Vector2(dx, dy);
            newPos = ClampToArea(newPos, rect, area);
            rect.anchoredPosition = newPos;
        }

        RefreshDecOperatorPosition();
    }

    /// <summary>
    /// 拖动结束后，返回相对服务器记录原点的最终 delta（clamp 后的真实位移）
    /// </summary>
    public Vector2 GetFinalDragDelta(int recordId)
    {
        if (_mainPanel == null) return Vector2.zero;

        var rect = _mainPanel.GetDecorationRect(recordId);
        if (rect == null) return Vector2.zero;

        var info = PlayerDataManager.Instance?.GetDecorationInfoByRecordId(recordId);
        float originX = info?.PositionX ?? 0f;
        float originY = info?.PositionY ?? 0f;

        return new Vector2(rect.anchoredPosition.x - originX,
                           rect.anchoredPosition.y - originY);
    }

    /// <summary>
    /// 80（摆饰）→ 底部区域 bottomAreaRect
    /// 81（挂饰）→ 上部区域 upAreaRect
    /// </summary>
    private Rect GetAreaForCategory(int category)
    {
        if (_mainPanel == null) return new Rect();

        if (category == 80) return _mainPanel.BottomRect;
        if (category == 81)
        {
            Rect up = _mainPanel.UpRect;
            // upAreaRect 未配置时退化为整个区域
            return (up.width > 0f && up.height > 0f) ? up : _mainPanel.TotalRect;
        }

        // 其他品类不限制
        return _mainPanel.TotalRect;
    }

    /// <summary>
    /// 把位置钳制到区域内部（考虑装饰自身尺寸与缩放，含镜像）
    /// </summary>
    private Vector2 ClampToArea(Vector2 pos, RectTransform rect, Rect area)
    {
        Vector2 size = rect.rect.size;
        float halfW = size.x * 0.5f * Mathf.Abs(rect.localScale.x);
        float halfH = size.y * 0.5f * Mathf.Abs(rect.localScale.y);

        float minX = area.xMin + halfW;
        float maxX = area.xMax - halfW;
        float minY = area.yMin + halfH;
        float maxY = area.yMax - halfH;

        if (minX > maxX) pos.x = (area.xMin + area.xMax) * 0.5f;
        else pos.x = Mathf.Clamp(pos.x, minX, maxX);

        if (minY > maxY) pos.y = (area.yMin + area.yMax) * 0.5f;
        else pos.y = Mathf.Clamp(pos.y, minY, maxY);

        return pos;
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
            item.transform.SetSiblingIndex(i);
            item.gameObject.SetActive(true);

            // 背包剩余数量
            int bagQuantity = PlayerDataService.Instance?.GetOwnedDecorationQuantity(config.id) ?? 0;

            // 本槽位已装备数量
            int equippedCount = 0;
            if (_equippedStatus.TryGetValue(_currentCategory, out var list))
            {
                foreach (var info in list)
                {
                    if (info.DecorationId == config.id) equippedCount++;
                }
            }

            // 80/81 跨所有槽位统计；82-84 只统计当前槽位（唯一）
            int totalEquippedCount;
            if (_currentCategory == 80 || _currentCategory == 81)
            {
                totalEquippedCount = 0;
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

            // 总拥有数量 = 背包剩余 + 已装备
            int ownedQuantity = bagQuantity + totalEquippedCount;
            bool owned = ownedQuantity > 0;
            bool equipped = totalEquippedCount > 0;

            // 80/81 显示背包剩余；82-84 显示总拥有数量
            int displayCount = (_currentCategory == 80 || _currentCategory == 81)
                ? bagQuantity
                : ownedQuantity;

            item.Init(config, owned, equipped, totalEquippedCount, displayCount, _currentTankId, OnDecorationItemClickedInternal);
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
