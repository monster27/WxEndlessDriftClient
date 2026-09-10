//using System;
//using System.Collections.Generic;
//using UnityEngine;
//using UnityEngine.AddressableAssets;
//using static PlayerDataManager;

///// <summary>
///// 3D 装饰管理器 - 负责鱼缸内所有装饰的渲染、交互和同步
///// </summary>
//public class FishTankDecManager : MonoBehaviour
//{
//    [Header("依赖引用")]
//    [SerializeField] private FishTankManager fishTankManager;
//    [SerializeField] private Transform decorationContainer;

//    [Header("装饰预制体")]
//    [SerializeField] private GameObject decPrefab_80;
//    [SerializeField] private GameObject decPrefab_81;

//    [Header("纹理替换目标")]
//    [SerializeField] private Renderer bottomRenderer;
//    [SerializeField] private Renderer backgroundRenderer;

//    [Header("操作面板预制体")]
//    [SerializeField] private GameObject operatorPrefab;

//    // 数据
//    private int _currentTankId = 1;
//    private Rect _totalRect;
//    private Rect _bottomRect;

//    // 装饰实例缓存
//    private class DecInstance
//    {
//        public GameObject go;
//        public int category;
//        public int decorationId;
//        public int recordId;
//        public Vector3 position;
//        public float rotationY;
//    }
//    private Dictionary<string, DecInstance> _decInstances = new Dictionary<string, DecInstance>();
//    private string _selectedInstanceId = null;

//    // UI操作面板引用
//    private UI_FishTankDecOperator _operatorUIInstance;
//    private Canvas _uiCanvas;

//    private void Awake()
//    {
//        // 查找UI Canvas
//        _uiCanvas = FindObjectOfType<Canvas>();
//        if (_uiCanvas == null)
//        {
//            Debug.LogError("[FishTankDecManager] 场景中没有找到 Canvas！");
//            return;
//        }

//        // 实例化操作面板到 Canvas 下
//        if (operatorPrefab != null && _uiCanvas != null)
//        {
//            var go = Instantiate(operatorPrefab, _uiCanvas.transform);
//            _operatorUIInstance = go.GetComponent<UI_FishTankDecOperator>();
//            if (_operatorUIInstance != null)
//            {
//                _operatorUIInstance.gameObject.SetActive(false);
//                _operatorUIInstance.Init(this);
//            }
//        }
//    }

//    private void UpdateRects()
//    {
//        if (fishTankManager != null)
//        {
//            _totalRect = fishTankManager.TotalRect;
//            _bottomRect = fishTankManager.BottomRect;
//        }
//    }

//    /// <summary>
//    /// 加载当前鱼缸的装饰数据
//    /// </summary>
//    public void LoadDecorationsForTank(int tankId, List<DecorationEquipInfo> equippedList)
//    {
//        _currentTankId = tankId;
//        UpdateRects();
//        ClearAllDecorations();

//        if (equippedList == null) return;

//        foreach (var info in equippedList)
//        {
//            int category = GetCategoryByDecorationId(info.DecorationId);

//            if (category == 80 || category == 81)
//            {
//                CreateMovableDecoration(
//                    category,
//                    info.DecorationId,
//                    info.Id,
//                    new Vector3(info.PositionX, info.PositionY, 0),
//                    info.RotationY
//                );
//            }
//            else if (category >= 82 && category <= 84)
//            {
//                ApplyTextureDecoration(category, info.DecorationId);
//            }
//        }
//    }

//    private int GetCategoryByDecorationId(int decorationId)
//    {
//        var decData = LoadDataManager.Instance?.fishTankDecorations?.Find(d => d.id == decorationId);
//        return decData?.categoryId ?? 0;
//    }

//    /// <summary>
//    /// 创建80/81装饰（可移动、镜像）
//    /// </summary>
//    private void CreateMovableDecoration(int category, int decId, int recordId, Vector3 pos, float rotY)
//    {
//        GameObject prefab = (category == 80) ? decPrefab_80 : decPrefab_81;
//        if (prefab == null) return;

//        GameObject go = Instantiate(prefab, decorationContainer);
//        go.transform.position = pos;
//        go.transform.rotation = Quaternion.Euler(0, rotY, 0);
//        go.SetActive(true);

//        string uid = Guid.NewGuid().ToString();
//        var inst = new DecInstance
//        {
//            go = go,
//            category = category,
//            decorationId = decId,
//            recordId = recordId,
//            position = pos,
//            rotationY = rotY
//        };
//        _decInstances[uid] = inst;

//        // 添加点击选中组件（不处理拖拽，只负责选中）
//        var clickHandler = go.GetComponent<DecClickHandler>();
//        if (clickHandler == null) clickHandler = go.AddComponent<DecClickHandler>();
//        clickHandler.Init(this, uid);
//    }

//    // ============================================================
//    // 公开接口
//    // ============================================================

//    /// <summary>
//    /// 应用82~84纹理替换
//    /// </summary>
//    public void ApplyTextureDecoration(int category, int decId)
//    {
//        Renderer targetRenderer = (category == 82 || category == 83) ? bottomRenderer : backgroundRenderer;
//        if (targetRenderer == null) return;

//        var itemData = LoadDataManager.Instance?.GetItemById(decId);
//        if (itemData == null || string.IsNullOrEmpty(itemData.iconPath)) return;

//        string path = itemData.iconPath;
//        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
//        {
//            if (sprite != null && targetRenderer != null)
//            {
//                targetRenderer.material.mainTexture = sprite.texture;
//            }
//        });
//    }

//    /// <summary>
//    /// 根据装饰ID和类别获取实例ID
//    /// </summary>
//    public string GetInstanceIdByDecoration(int decorationId, int category)
//    {
//        foreach (var kvp in _decInstances)
//        {
//            if (kvp.Value.decorationId == decorationId && kvp.Value.category == category)
//                return kvp.Key;
//        }
//        return null;
//    }

//    /// <summary>
//    /// 获取装饰实例的世界位置
//    /// </summary>
//    public Vector3 GetDecorationPosition(string instanceId)
//    {
//        if (_decInstances.TryGetValue(instanceId, out var inst))
//            return inst.position;
//        return Vector3.zero;
//    }

//    /// <summary>
//    /// 获取装饰实例的类别
//    /// </summary>
//    public int GetDecorationCategory(string instanceId)
//    {
//        if (_decInstances.TryGetValue(instanceId, out var inst))
//            return inst.category;
//        return 0;
//    }

//    /// <summary>
//    /// 获取装饰实例的记录ID
//    /// </summary>
//    public int GetDecorationRecordId(string instanceId)
//    {
//        if (_decInstances.TryGetValue(instanceId, out var inst))
//            return inst.recordId;
//        return 0;
//    }

//    /// <summary>
//    /// 选中某个80/81装饰（点击3D物体时调用）
//    /// </summary>
//    public void SelectDecoration(string instanceId)
//    {
//        if (!_decInstances.ContainsKey(instanceId)) return;
//        _selectedInstanceId = instanceId;

//        if (_operatorUIInstance != null)
//        {
//            var inst = _decInstances[instanceId];
//            Vector3 screenPos = Camera.main.WorldToScreenPoint(inst.position);
//            // 偏移到右上角
//            screenPos += new Vector3(80, -80, 0);
//            _operatorUIInstance.ShowAt(screenPos, instanceId);
//        }
//    }

//    /// <summary>
//    /// 取消选中
//    /// </summary>
//    public void DeselectDecoration()
//    {
//        _selectedInstanceId = null;
//        if (_operatorUIInstance != null)
//        {
//            _operatorUIInstance.Hide();
//        }
//    }

//    /// <summary>
//    /// 移动装饰（由操作面板的拖拽调用）
//    /// </summary>
//    public void MoveDecoration(string instanceId, Vector3 worldDelta)
//    {
//        if (!_decInstances.TryGetValue(instanceId, out var inst)) return;

//        Vector3 newPos = inst.position + worldDelta;
//        Rect limitRect = (inst.category == 80) ? _bottomRect : _totalRect;
//        newPos.x = Mathf.Clamp(newPos.x, limitRect.xMin + 0.5f, limitRect.xMax - 0.5f);
//        newPos.y = Mathf.Clamp(newPos.y, limitRect.yMin + 0.5f, limitRect.yMax - 0.5f);
//        newPos.z = 0;

//        inst.position = newPos;
//        inst.go.transform.position = newPos;

//        // 实时更新操作面板位置（跟随装饰）
//        if (_operatorUIInstance != null && _operatorUIInstance.gameObject.activeSelf)
//        {
//            Vector3 screenPos = Camera.main.WorldToScreenPoint(newPos);
//            screenPos += new Vector3(80, -80, 0);
//            _operatorUIInstance.UpdatePosition(screenPos);
//        }
//    }

//    /// <summary>
//    /// 放下装饰（拖拽结束）- 同步服务器位置
//    /// </summary>
//    public void DropDecoration(string instanceId)
//    {
//        if (!_decInstances.TryGetValue(instanceId, out var inst)) return;
//        // TODO: 调用网络接口更新位置
//        Debug.Log($"[FishTankDecManager] Synced position for {instanceId}: {inst.position}");
//    }

//    /// <summary>
//    /// 镜像装饰（点击镜像按钮）
//    /// </summary>
//    public void MirrorDecoration(string instanceId)
//    {
//        if (!_decInstances.TryGetValue(instanceId, out var inst)) return;
//        inst.rotationY = (inst.rotationY == 0) ? 180 : 0;
//        inst.go.transform.rotation = Quaternion.Euler(0, inst.rotationY, 0);
//        // TODO: 同步服务器
//        Debug.Log($"[FishTankDecManager] Mirrored {instanceId} to {inst.rotationY}");
//    }

//    /// <summary>
//    /// 卸下装饰（点击卸下按钮）
//    /// </summary>
//    public void RemoveDecoration(string instanceId)
//    {
//        if (!_decInstances.TryGetValue(instanceId, out var inst)) return;
//        Destroy(inst.go);
//        _decInstances.Remove(instanceId);
//        // TODO: 同步服务器删除
//        Debug.Log($"[FishTankDecManager] Removed {instanceId}");

//        if (_operatorUIInstance != null)
//            _operatorUIInstance.Hide();
//        _selectedInstanceId = null;
//    }

//    /// <summary>
//    /// 清空所有装饰
//    /// </summary>
//    public void ClearAllDecorations()
//    {
//        foreach (var kvp in _decInstances)
//        {
//            if (kvp.Value.go != null) Destroy(kvp.Value.go);
//        }
//        _decInstances.Clear();
//        _selectedInstanceId = null;
//        if (_operatorUIInstance != null) _operatorUIInstance.Hide();
//    }

//    /// <summary>
//    /// 设置鱼的显隐
//    /// </summary>
//    public void SetFishVisible(bool visible)
//    {
//        fishTankManager?.SetFishVisible(visible);
//    }
//}

///// <summary>
///// 点击处理组件（只负责点击选中，不处理拖拽）
///// </summary>
//public class DecClickHandler : MonoBehaviour
//{
//    private FishTankDecManager _manager;
//    private string _instanceId;

//    public void Init(FishTankDecManager manager, string instanceId)
//    {
//        _manager = manager;
//        _instanceId = instanceId;
//    }

//    private void OnMouseDown()
//    {
//        if (_manager != null)
//        {
//            // 只负责选中，不拖拽
//            _manager.SelectDecoration(_instanceId);
//        }
//    }
//}
