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

    [Header("===== 当前鱼缸ID =====")]
    [SerializeField] private int currentTankId = 1;

    [Header("===== 对象池 =====")]
    [SerializeField] private int poolInitSize = 5;

    // ---------- 数据 ----------
    private int _currentCategory = 80;
    private List<int> _ownedDecorationIds = new List<int>();
    private Dictionary<int, List<DecorationEquipInfo>> _equippedStatus = new Dictionary<int, List<DecorationEquipInfo>>();

    private DecorationObjectPool _pool;
    private List<UI_FishTankDecPrefab> _activeItems = new List<UI_FishTankDecPrefab>();
    private bool _isInitialized = false;
    private List<FishTankDecData> _allDecConfigs = new List<FishTankDecData>();

    // ---------- 初始化 ----------
    public void Init(int tankId = 1)
    {
        currentTankId = tankId;
        _allDecConfigs = LoadDataManager.Instance.fishTankDecorations ?? new List<FishTankDecData>();
        _pool = new DecorationObjectPool(decPrefab, decorationContainer, poolInitSize);
        SetupToggles();
        RegisterEvents();
        _isInitialized = true;
        LogDebug($"初始化完成，当前鱼缸ID={currentTankId}");
        RefreshData();
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

    private void RegisterEvents()
    {
        UnregisterEvents();
        CommunicateEvent.Register(FishTankMessage.DecorationDataUpdated.ToString(), OnDecorationDataUpdated);
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister(FishTankMessage.DecorationDataUpdated.ToString(), OnDecorationDataUpdated);
    }

    // ---------- 数据刷新（通过 PlayerDataService） ----------
    public void RefreshData()
    {
        if (!_isInitialized) return;

        // 通过 Service 获取数据
        _ownedDecorationIds = PlayerDataService.Instance?.GetOwnedDecorationIds() ?? new List<int>();
        _equippedStatus = PlayerDataService.Instance?.GetEquippedDecorations(currentTankId)
                          ?? new Dictionary<int, List<DecorationEquipInfo>>();

        // 补全：若已装备但未在拥有列表中（数据一致性问题）
        foreach (var kvp in _equippedStatus)
        {
            foreach (var info in kvp.Value)
            {
                if (!_ownedDecorationIds.Contains(info.DecorationId))
                    _ownedDecorationIds.Add(info.DecorationId);
            }
        }

        RenderCurrentCategory();

        // 通知主面板刷新装饰显示（如果是装饰模式）
        CommunicateEvent.Modify(FishTankMessage.DecorationDataUpdated.ToString());
    }

    private void OnDecorationDataUpdated()
    {
        LogDebug("收到装饰数据更新通知，刷新数据");
        RefreshData();
    }

    private void OnCategoryChanged(int category)
    {
        if (_currentCategory == category) return;
        _currentCategory = category;
        LogDebug($"切换到品类 {category}");
        RenderCurrentCategory();
    }

    // ---------- 渲染 ----------
    private void RenderCurrentCategory()
    {
        ClearActiveItems();

        var configs = _allDecConfigs
            .Where(c => c.categoryId == _currentCategory)
            .OrderBy(c => c.id)
            .ToList();

        if (configs.Count == 0)
        {
            LogDebug($"品类 {_currentCategory} 无装饰配置");
            return;
        }

        foreach (var config in configs)
        {
            UI_FishTankDecPrefab item = _pool.Get();
            item.transform.SetParent(decorationContainer, false);
            item.gameObject.SetActive(true);

            bool owned = _ownedDecorationIds.Contains(config.id);

            bool equipped = false;
            int equippedCount = 0;
            if (_equippedStatus.TryGetValue(_currentCategory, out var list))
            {
                foreach (var info in list)
                {
                    if (info.DecorationId == config.id)
                    {
                        equipped = true;
                        equippedCount++;
                    }
                }
            }

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

            int ownedQuantity = 0;
            if (owned) ownedQuantity = 1; // 简化

            int unEquippedCount = ownedQuantity - totalEquippedCount;
            if (unEquippedCount < 0) unEquippedCount = 0;

            item.Init(config, owned, equipped, totalEquippedCount, unEquippedCount, currentTankId, OnDecorationClick);
            _activeItems.Add(item);
        }

        LogDebug($"渲染完成，品类{_currentCategory} 共 {configs.Count} 个装饰");
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

    // ============================================================
    // 点击事件处理（全部发送事件）
    // ============================================================
    private void OnDecorationClick(UI_FishTankDecPrefab item)
    {
        if (item == null || item.Config == null)
        {
            LogDebug("OnDecorationClick: item 或 config 为空");
            return;
        }

        int decId = item.Config.id;
        int category = item.Config.categoryId;
        LogDebug($"OnDecorationClick: decId={decId}, category={category}, tankId={currentTankId}");

        bool owned = _ownedDecorationIds.Contains(decId);
        if (!owned)
        {
            LogDebug("未拥有此装饰，显示提示");
            GameUIManager.ShowMessage("尚未拥有该装饰");
            return;
        }

        // 80/81：已装备则选中显示操作面板，否则装备
        if (category == 80 || category == 81)
        {
            bool alreadyEquipped = false;
            int recordId = 0;
            if (_equippedStatus.TryGetValue(category, out var list))
            {
                var info = list.FirstOrDefault(i => i.DecorationId == decId);
                if (info != null)
                {
                    alreadyEquipped = true;
                    recordId = info.Id; // 使用 Id
                }
            }

            if (alreadyEquipped)
            {
                // 发送选中事件，携带品类
                var selectData = new SelectDecorationData
                {
                    TankId = currentTankId,
                    RecordId = recordId,
                    Category = category,
                    ScreenPosition = Input.mousePosition
                };
                CommunicateEvent.Modify(FishTankMessage.SelectDecoration.ToString(), selectData);
                LogDebug($"选中已装备的80/81装饰，显示操作面板");
                return;
            }
            else
            {
                // 未装备，发送装备请求
                var equipData = new EquipDecorationData
                {
                    TankId = currentTankId,
                    Category = category,
                    DecorationId = decId,
                    PosX = 0,
                    PosY = 0,
                    PosZ = 0,
                    ScaleX = 1,
                    ScaleY = 1,
                    ScaleZ = 1,
                    RotX = 0,
                    RotY = 0,
                    RotZ = 0
                };
                CommunicateEvent.Modify(FishTankMessage.EquipDecoration.ToString(), equipData);
            }
            return;
        }

        // 82~84：纹理替换类
        if (category >= 82 && category <= 84)
        {
            bool alreadyEquipped = false;
            if (_equippedStatus.TryGetValue(category, out var list))
            {
                alreadyEquipped = list.Any(info => info.DecorationId == decId);
            }

            if (alreadyEquipped)
            {
                // 已装备，应用纹理（发送应用事件）
                var applyData = new ApplyDecorationData
                {
                    TankId = currentTankId,
                    Category = category,
                    DecorationId = decId
                };
                CommunicateEvent.Modify(FishTankMessage.ApplyTextureDecoration.ToString(), applyData);
                GameUIManager.ShowMessage("已应用装饰");
                return;
            }
            else
            {
                LogDebug($"82~84 未装备，发送装备请求");
                var equipData = new EquipDecorationData
                {
                    TankId = currentTankId,
                    Category = category,
                    DecorationId = decId,
                    PosX = 0,
                    PosY = 0,
                    PosZ = 0,
                    ScaleX = 1,
                    ScaleY = 1,
                    ScaleZ = 1,
                    RotX = 0,
                    RotY = 0,
                    RotZ = 0
                };
                CommunicateEvent.Modify(FishTankMessage.EquipDecoration.ToString(), equipData);
            }
        }
    }

    // ---------- 对象池 ----------
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
            for (int i = 0; i < initialSize; i++)
            {
                CreateNewObject();
            }
        }

        private UI_FishTankDecPrefab CreateNewObject()
        {
            GameObject go = GameObject.Instantiate(_prefab, _parent);
            go.SetActive(false);
            var item = go.GetComponent<UI_FishTankDecPrefab>();
            if (item == null)
            {
                Debug.LogError("DecorationObjectPool: 预制体缺少 UI_FishTankDecPrefab 组件");
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
                for (int i = 0; i < 5; i++)
                {
                    CreateNewObject();
                }
            }
            return _pool.Dequeue();
        }

        public void Return(UI_FishTankDecPrefab item)
        {
            if (item == null) return;
            if (!_pool.Contains(item) && _allObjects.Contains(item))
            {
                _pool.Enqueue(item);
            }
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

    // ---------- 生命周期 ----------
    private void OnDestroy()
    {
        UnregisterEvents();
        if (_pool != null) _pool.Clear();
    }

    private void LogDebug(string msg)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankDecorationPanel] {msg}");
    }
}
