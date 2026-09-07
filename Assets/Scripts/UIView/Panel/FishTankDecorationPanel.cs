// ============================================================
// 文件: FishTankDecorationPanel.cs
// 说明: 鱼缸装饰面板 - 管理品类切换和装饰列表显示
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Linq;

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
    // 新的装备状态：每个槽位一个列表
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
        CommunicateEvent.Register("DecorationDataUpdated", OnDecorationDataUpdated);
    }

    private void UnregisterEvents()
    {
        CommunicateEvent.Unregister("DecorationDataUpdated", OnDecorationDataUpdated);
    }

    // ---------- 数据刷新 ----------
    public void RefreshData()
    {
        if (!_isInitialized) return;

        if (NetServerManager.Instance != null)
        {
            NetServerManager.Instance.FetchOwnedDecorations(ids =>
            {
                _ownedDecorationIds = ids ?? new List<int>();
                LogDebug($"FetchOwnedDecorations 返回 {_ownedDecorationIds.Count} 个");
                NetServerManager.Instance.FetchEquippedStatus(currentTankId, status =>
                {
                    _equippedStatus = status ?? new Dictionary<int, List<DecorationEquipInfo>>();

                    // 补全拥有列表（已装备的装饰必定拥有）
                    foreach (var kvp in _equippedStatus)
                    {
                        foreach (var info in kvp.Value)
                        {
                            int decId = info.DecorationId;
                            if (decId != 0 && !_ownedDecorationIds.Contains(decId))
                            {
                                _ownedDecorationIds.Add(decId);
                                LogDebug($"补全拥有列表：{decId}（已装备）");
                            }
                        }
                    }

                    RenderCurrentCategory();
                });
            });
        }
        else
        {
            RenderCurrentCategory();
        }
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

            // 计算是否装备（当前槽位是否有该装饰实例）
            bool equipped = false;
            int equippedCount = 0;

            // 获取该槽位的列表
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

            // 对于80/81，totalEquippedCount 是该装饰在所有槽位的总数（跨鱼缸，但这里只统计当前鱼缸）
            // 客户端需要显示“已装备数量”，这里沿用之前的逻辑：统计当前鱼缸中该装饰的实例总数
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
                // 其他类别：每个槽位只能1个，但现已改为列表，统计时也是每个实例
                // 这里使用 equippedCount 即可，因为只有该槽位有且仅有一个实例（但为了兼容，仍用列表）
                totalEquippedCount = equippedCount;
            }

            int ownedQuantity = 0;
            if (NetServerManager.Instance != null)
            {
                var inventory = NetServerManager.Instance.GetPlayerInventory();
                inventory.TryGetValue(config.id, out ownedQuantity);
            }

            // 未装备数量 = 背包剩余
            int unEquippedCount = ownedQuantity;

            // 对于非80/81，即使有多个实例，但设计上仍限制为1，但逻辑不变
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
    // 点击事件处理
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
        LogDebug($"owned = {owned} (包含于拥有列表)");

        if (!owned)
        {
            LogDebug("未拥有此装饰，显示提示");
            GameUIManager.ShowMessage("尚未拥有该装饰");
            return;
        }

        // ============================================================
        // 80/81 类别：直接发送装备请求（允许重复放置），不检查槽位占用
        // ============================================================
        if (category == 80 || category == 81)
        {
            LogDebug("80/81 分支：发送装备请求给服务器");
            EquipDecoration(decId, category);
            return;
        }

        // ============================================================
        // 其他类别（82/83/84）：每个槽位仍限制1个（但服务器也允许重复，这里客户端做限制）
        // 如果已装备，则提示，否则装备
        // ============================================================
        bool alreadyEquipped = false;
        if (_equippedStatus.TryGetValue(category, out var list))
        {
            alreadyEquipped = list.Any(info => info.DecorationId == decId);
        }

        if (alreadyEquipped)
        {
            LogDebug($"装饰 {decId} 已装备在当前槽位，提示用户");
            GameUIManager.ShowMessage("该装饰已装备（每个槽位限装1个）");
            return;
        }

        // 如果当前槽位有其他装饰，允许替换（但允许重复放置，所以这里无需替换，直接装备）
        // 为了保持逻辑，直接装备
        EquipDecoration(decId, category);
    }

    private void EquipDecoration(int decId, int category)
    {
        LogDebug($"EquipDecoration: decId={decId}, category={category}, tankId={currentTankId}");
        NetServerManager.Instance.EquipDecoration(
            currentTankId, category, decId,
            posX: 0, posY: 0, posZ: 0,
            scaleX: 1, scaleY: 1, scaleZ: 1,
            rotX: 0, rotY: 0, rotZ: 0,
            (success, msg, newRecordId) =>
            {
                if (success)
                {
                    LogDebug($"装备成功，消息: {msg}, 新ID={newRecordId}");

                    // 只有真正消耗背包时才减1（服务器返回"已装备该装饰"时不消耗）
                    if (!msg.Contains("已装备该装饰"))
                    {
                        var inv = NetServerManager.Instance.GetPlayerInventory();
                        if (inv.ContainsKey(decId))
                        {
                            int old = inv[decId];
                            inv[decId] = Math.Max(0, old - 1);
                            LogDebug($"背包缓存更新: {decId} 从 {old} 减至 {inv[decId]}");
                        }
                    }
                    else
                    {
                        LogDebug($"服务器返回幂等成功，不减少背包数量");
                    }

                    GameUIManager.ShowMessage("装备成功");
                    RefreshData();
                }
                else
                {
                    LogDebug($"装备失败: {msg}");
                    GameUIManager.ShowMessage($"装备失败: {msg}");
                }
            }
        );
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
