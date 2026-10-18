using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class GameUIManager : SingletonMonoFromScene<GameUIManager>
{
    // ============================================================
    //  1. 场景固定的常驻 View（不走 AA）
    // ============================================================
    [Header("===== 场景固定的常驻 View =====")]
    public MainGameView mainGameView;
    public DialogView dialogView;
    public TipView tipView;

    [Header("===== 动态 View 的父容器 =====")]
    [Tooltip("所有走 AA 动态加载的 View 都实例化到这个节点下")]
    public RectTransform viewContainer;

    // ============================================================
    //  1.5 兼容字段
    // ============================================================
    public BagView bagView;
    public FishBagView fishBagView;
    public MallView mallView;
    public EquipmentView equipmentView;
    public AdvertisingView advertisingView;
    public MapView mapView;
    public CollectionView collectionView;
    public FishTankView fishTankView;
    public EggView eggView;
    public PetView petView;
    public GameMatch3View gameMatch3View;

    // ============================================================
    //  2. View 元数据
    // ============================================================
    public enum ViewType { Resident, Popup, Temporary }

    private class ViewMeta
    {
        public string Key;
        public string PrefabPath;
        public ViewType Type;
        public Type ComponentType;
        public AssetCategory Category;
    }

    private readonly Dictionary<string, ViewMeta> _viewRegistry = new Dictionary<string, ViewMeta>();

    private void InitViewRegistry()
    {
        RegisterView("BagView", "Assets/Addressables/UI/View/BagView.prefab", ViewType.Popup, typeof(BagView));
        RegisterView("FishBagView", "Assets/Addressables/UI/View/FishBagView.prefab", ViewType.Popup, typeof(FishBagView));
        RegisterView("MallView", "Assets/Addressables/UI/View/MallView.prefab", ViewType.Popup, typeof(MallView));
        RegisterView("MapView", "Assets/Addressables/UI/View/MapView.prefab", ViewType.Popup, typeof(MapView));
        RegisterView("CollectionView", "Assets/Addressables/UI/View/CollectionView.prefab", ViewType.Popup, typeof(CollectionView));
        RegisterView("FishTankView", "Assets/Addressables/UI/View/FishTankView.prefab", ViewType.Popup, typeof(FishTankView));
        RegisterView("EggView", "Assets/Addressables/UI/View/EggView.prefab", ViewType.Popup, typeof(EggView));
        RegisterView("PetView", "Assets/Addressables/UI/View/PetView.prefab", ViewType.Popup, typeof(PetView));
        RegisterView("EquipmentView", "Assets/Addressables/UI/View/EquipmentView.prefab", ViewType.Resident, typeof(EquipmentView));
        RegisterView("GameMatch3View", "Assets/Addressables/UI/View/GameMatch3View.prefab", ViewType.Resident, typeof(GameMatch3View));
        RegisterView("AdvertisingView", "Assets/Addressables/UI/View/AdvertisingView.prefab", ViewType.Popup, typeof(AdvertisingView));
    }

    private void RegisterView(string key, string prefabPath, ViewType type, Type componentType)
    {
        _viewRegistry[key] = new ViewMeta
        {
            Key = key,
            PrefabPath = prefabPath,
            Type = type,
            ComponentType = componentType,
            Category = AssetCategory.UI,
        };
    }

    // ============================================================
    //  3. 状态机 + 带时间戳的池
    // ============================================================
    private enum ViewState { NotLoaded, Loading, Loaded, Showing, Hiding, Releasing, Released }

    private class PooledInstance
    {
        public GameObject Instance;
        public float HiddenTime;      // 进入池的时间戳，用于空闲超时
        public string PrefabPath;     // 保留字段，池内单实例销毁不再 Release prefab
    }

    private class ViewRuntime
    {
        public ViewState State = ViewState.NotLoaded;
        public int RefCount = 0;
        public float ReleaseStartTime = 0f;
        public GameObject Instance;
        public List<PooledInstance> PopupPool = new List<PooledInstance>();
        public bool CancelledWhileLoading = false;
        public List<Action<GameObject>> WaitingCallbacks = new List<Action<GameObject>>();
    }

    private readonly Dictionary<string, ViewRuntime> _viewRuntime = new Dictionary<string, ViewRuntime>();

    private const float ReleaseDelay = 10f;
    private const int PopupPoolMaxSize = 2;

    // 空闲超时：关闭后多久销毁
    private const float IdleTimeout = 120f;
    // 扫描间隔
    private const float ScanInterval = 1f;
    private float _lastScanTime = 0f;

    private ViewRuntime GetOrCreateRuntime(string key)
    {
        if (!_viewRuntime.TryGetValue(key, out var rt))
        {
            rt = new ViewRuntime();
            _viewRuntime[key] = rt;
        }
        return rt;
    }

    // ============================================================
    //  4. 实例有效性检查（职责分离）
    // ============================================================
    /// <summary>
    /// 僵尸检测：存活 + 场景有效。不判激活。
    /// </summary>
    private static bool IsValidInstance(GameObject go)
    {
        if (go == null) return false;
        if (!go.scene.IsValid()) return false;
        if (!go.scene.isLoaded) return false;
        return true;
    }

    /// <summary>
    /// 可用性判据：存活 + 场景有效 + 激活链上。
    /// 用于兼容字段语义（字段非空 ⇔ 实例可响应）与业务调用判据。
    /// </summary>
    private static bool IsUsableInstance(GameObject go)
    {
        if (go == null) return false;
        if (!go.scene.IsValid() || !go.scene.isLoaded) return false;
        if (!go.activeInHierarchy) return false;
        return true;
    }

    /// <summary>
    /// 可用性判据（BaseView 版本），供外部/内部字段自检使用。
    /// </summary>
    private static bool IsUsableInstance(BaseView view)
    {
        if (view == null) return false;
        return IsUsableInstance(view.gameObject);
    }

    // ============================================================
    //  5. OpenView
    // ============================================================
    public void OpenView(string key, Action<GameObject> onOpened = null)
    {
        if (string.IsNullOrEmpty(key)) { onOpened?.Invoke(null); return; }
        if (!_viewRegistry.TryGetValue(key, out var meta)) { Z_Logger.LogError($"[GameUIManager] 未注册: {key}"); onOpened?.Invoke(null); return; }
        if (viewContainer == null) { Z_Logger.LogError("[GameUIManager] viewContainer 未绑定"); onOpened?.Invoke(null); return; }

        var rt = GetOrCreateRuntime(key);

        // 每次 OpenView 前先清池
        CleanupZombiesInPool(rt, key);

        // 当前 Instance 是僵尸，直接丢弃
        if (rt.Instance != null && !IsValidInstance(rt.Instance))
        {
            Z_Logger.LogWarning($"[GameUIManager] 🧟 OpenView 时发现 Instance 是僵尸: {key}");
            SafeDestroyInstance(rt.Instance, key);
            rt.Instance = null;
            rt.State = ViewState.NotLoaded;
            rt.RefCount = 0;
            ClearCompatField(key);
        }

        Z_Logger.Log($"[GameUIManager] OpenView: {key}, 状态: {rt.State}, RefCount: {rt.RefCount}");

        switch (rt.State)
        {
            case ViewState.Showing:
                onOpened?.Invoke(rt.Instance);
                return;

            case ViewState.Loading:
                rt.WaitingCallbacks.Add(onOpened);
                return;

            case ViewState.Hiding:
            case ViewState.Loaded:
            case ViewState.Released:
            case ViewState.NotLoaded:
                rt.RefCount++;
                StartCoroutine(LoadAndShowCoroutine(key, meta, rt, onOpened));
                return;

            case ViewState.Releasing:
                // 释放中：实例已销毁、prefab 已 Release，不能直接 ShowExistingInstance，
                // 当作 NotLoaded 重新加载是唯一安全路径。
                rt.RefCount++;
                rt.ReleaseStartTime = 0f;
                rt.State = ViewState.NotLoaded;
                ClearCompatField(key);
                StartCoroutine(LoadAndShowCoroutine(key, meta, rt, onOpened));
                return;
        }

        onOpened?.Invoke(null);
    }

    public void CloseView(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!_viewRegistry.TryGetValue(key, out var meta)) return;

        var rt = GetOrCreateRuntime(key);
        Z_Logger.Log($"[GameUIManager] CloseView: {key}, 状态: {rt.State}, RefCount: {rt.RefCount}");

        switch (rt.State)
        {
            case ViewState.Showing:
                rt.RefCount--;
                rt.State = ViewState.Hiding;
                HideInstance(key, meta, rt);
                rt.State = ViewState.Loaded;
                break;

            case ViewState.Loading:
                rt.CancelledWhileLoading = true;
                rt.RefCount--;
                break;

            case ViewState.Loaded:
                rt.RefCount--;
                break;

            default:
                return;
        }

        if (rt.RefCount <= 0)
        {
            rt.RefCount = 0;
            rt.ReleaseStartTime = Time.time;
        }
    }

    public GameMatch3View GetGameMatch3View() => gameMatch3View;
    public FishTankView GetFishTankView() => fishTankView;

    // ============================================================
    //  6. 加载
    // ============================================================
    private IEnumerator LoadAndShowCoroutine(string key, ViewMeta meta, ViewRuntime rt, Action<GameObject> onOpened)
    {
        // 池里有就复用
        if (meta.Type == ViewType.Popup)
        {
            CleanupZombiesInPool(rt, key);
            GameObject pooled = TryRentFromPool(rt, key);
            if (pooled != null)
            {
                rt.Instance = pooled;
                rt.State = ViewState.Loaded;
                ShowExistingInstance(key, meta, rt, onOpened);
                yield break;
            }
        }
        else if (rt.Instance != null && IsValidInstance(rt.Instance))
        {
            rt.State = ViewState.Loaded;
            ShowExistingInstance(key, meta, rt, onOpened);
            yield break;
        }

        rt.State = ViewState.Loading;
        rt.WaitingCallbacks.Add(onOpened);
        Z_Logger.Log($"[GameUIManager] 开始加载 View: {key} ({meta.PrefabPath})");

        bool done = false;
        GameObject prefab = null;
        AssetManager.LoadFromAddressables<GameObject>(meta.PrefabPath, meta.Category, (result, h) =>
        {
            prefab = result;
            done = true;
        });
        while (!done) yield return null;

        if (prefab == null)
        {
            Z_Logger.LogError($"[GameUIManager] View 加载失败: {key}");
            foreach (var cb in rt.WaitingCallbacks) cb?.Invoke(null);
            rt.WaitingCallbacks.Clear();
            rt.State = ViewState.NotLoaded;
            ClearCompatField(key);
            yield break;
        }

        rt.State = ViewState.Loaded;

        if (rt.CancelledWhileLoading)
        {
            rt.CancelledWhileLoading = false;
            foreach (var cb in rt.WaitingCallbacks) cb?.Invoke(null);
            rt.WaitingCallbacks.Clear();
            AssetManager.ReleaseCachedReference(meta.PrefabPath);
            rt.State = ViewState.NotLoaded;
            ClearCompatField(key);
            yield break;
        }

        ShowExistingInstance(key, meta, rt, null);

        if (rt.State != ViewState.Showing)
        {
            // 显示失败，回调 null 并清等待队列（字段已由 ShowExistingInstance 清）
            foreach (var cb in rt.WaitingCallbacks) cb?.Invoke(null);
            rt.WaitingCallbacks.Clear();
            yield break;
        }

        foreach (var cb in rt.WaitingCallbacks) cb?.Invoke(rt.Instance);
        rt.WaitingCallbacks.Clear();
    }

    // ============================================================
    //  7. 显示（含实例化）
    // ============================================================
    private void ShowExistingInstance(string key, ViewMeta meta, ViewRuntime rt, Action<GameObject> onOpened)
    {
        GameObject instance = null;

        switch (meta.Type)
        {
            case ViewType.Resident:
                if (rt.Instance == null || !IsValidInstance(rt.Instance))
                {
                    if (rt.Instance != null) SafeDestroyInstance(rt.Instance, key);
                    var prefab = GetCachedPrefab(meta.PrefabPath);
                    if (prefab != null)
                    {
                        instance = Instantiate(prefab, viewContainer);
                        rt.Instance = instance;
                    }
                }
                else instance = rt.Instance;
                break;

            case ViewType.Popup:
                CleanupZombiesInPool(rt, key);
                if (rt.PopupPool.Count > 0)
                {
                    instance = rt.PopupPool[0].Instance;
                    rt.PopupPool.RemoveAt(0);
                    if (!IsValidInstance(instance))
                    {
                        Z_Logger.LogWarning($"[GameUIManager] 🧟 从池取出后仍是僵尸: {key}");
                        SafeDestroyInstance(instance, key);
                        instance = null;
                    }
                    else
                    {
                        instance.SetActive(true);
                    }
                }
                if (instance == null)
                {
                    var prefab = GetCachedPrefab(meta.PrefabPath);
                    if (prefab != null)
                    {
                        instance = Instantiate(prefab, viewContainer);
                    }
                }
                break;

            case ViewType.Temporary:
                var prefab2 = GetCachedPrefab(meta.PrefabPath);
                if (prefab2 != null)
                    instance = Instantiate(prefab2, viewContainer);
                break;
        }

        if (instance == null)
        {
            Z_Logger.LogError($"[GameUIManager] 实例化失败: {key}");
            rt.State = ViewState.NotLoaded;
            rt.Instance = null;
            ClearCompatField(key);
            onOpened?.Invoke(null);
            return;
        }

        rt.Instance = instance;
        rt.State = ViewState.Showing;
        instance.transform.SetAsLastSibling();

        var baseView = instance.GetComponent<BaseView>();
        if (baseView != null)
        {
            baseView.BaseViewInit();
            baseView.ShowView();
        }

        // 字段语义：仅在实例可用时赋值
        if (IsUsableInstance(instance))
        {
            AssignToCompatField(key, instance);
        }
        else
        {
            Z_Logger.LogWarning($"[GameUIManager] ⚠️ Show 后实例不可用，清字段: {key}");
            ClearCompatField(key);
        }

        Z_Logger.Log($"[GameUIManager] 显示 View: {key}");
        onOpened?.Invoke(instance);
    }

    // ============================================================
    //  8. 从池取（带时间戳清零）
    // ============================================================
    private GameObject TryRentFromPool(ViewRuntime rt, string key)
    {
        for (int i = rt.PopupPool.Count - 1; i >= 0; i--)
        {
            var entry = rt.PopupPool[i];
            rt.PopupPool.RemoveAt(i);
            if (entry.Instance == null) continue;

            if (!IsValidInstance(entry.Instance))
            {
                Z_Logger.LogWarning($"[GameUIManager] 🧟 池中僵尸: {key}, iid={entry.Instance.GetInstanceID()}, scene='{entry.Instance.scene.name}', name='{entry.Instance.name}'");
                SafeDestroyInstance(entry.Instance, key);
                continue;
            }

            entry.HiddenTime = 0;   // 清除隐藏标记
            return entry.Instance;
        }
        return null;
    }

    // ============================================================
    //  9. 隐藏 + 入池（带时间戳）
    // ============================================================
    private void HideInstance(string key, ViewMeta meta, ViewRuntime rt)
    {
        if (rt.Instance == null) return;

        var baseView = rt.Instance.GetComponent<BaseView>();
        if (baseView != null) baseView.HideView();

        switch (meta.Type)
        {
            case ViewType.Resident:
                rt.Instance.SetActive(false);
                ClearCompatField(key);   // 字段语义：Hide 即不可响应
                break;

            case ViewType.Popup:
                if (IsValidInstance(rt.Instance))
                {
                    rt.Instance.SetActive(false);
                    if (rt.PopupPool.Count < PopupPoolMaxSize)
                    {
                        rt.PopupPool.Add(new PooledInstance
                        {
                            Instance = rt.Instance,
                            HiddenTime = Time.time,
                            PrefabPath = meta.PrefabPath,
                        });
                    }
                    else
                    {
                        SafeDestroyInstance(rt.Instance, key);
                    }
                }
                else
                {
                    Z_Logger.LogWarning($"[GameUIManager] 🧟 隐藏时已僵尸，直接销毁: {key}, scene='{rt.Instance.scene.name}'");
                    SafeDestroyInstance(rt.Instance, key);
                }
                rt.Instance = null;
                ClearCompatField(key);   // 字段语义：入池即不可响应
                break;

            case ViewType.Temporary:
                SafeDestroyInstance(rt.Instance, key);
                rt.Instance = null;
                ClearCompatField(key);
                break;
        }

        Z_Logger.Log($"[GameUIManager] 隐藏 View: {key}, 池大小={rt.PopupPool.Count}");
    }

    // ============================================================
    //  10. 安全销毁（统一入口）
    // ============================================================
    private void SafeDestroyInstance(GameObject go, string key)
    {
        if (go == null) return;
        Destroy(go);
        Z_Logger.Log($"[GameUIManager] 销毁 View 实例: {key}, name='{go.name}'");
    }

    // ============================================================
    //  11. 清池僵尸
    // ============================================================
    private void CleanupZombiesInPool(ViewRuntime rt, string key)
    {
        int cleaned = 0;
        for (int i = rt.PopupPool.Count - 1; i >= 0; i--)
        {
            var entry = rt.PopupPool[i];
            if (entry.Instance == null)
            {
                rt.PopupPool.RemoveAt(i);
                cleaned++;
                continue;
            }
            if (!IsValidInstance(entry.Instance))
            {
                Z_Logger.LogWarning($"[GameUIManager] 🧟 清理池中僵尸: {key}, iid={entry.Instance.GetInstanceID()}, name='{entry.Instance.name}', scene='{entry.Instance.scene.name}'");
                SafeDestroyInstance(entry.Instance, key);
                rt.PopupPool.RemoveAt(i);
                // 注意：prefab 是按路径共享的，单实例销毁不减少 prefab 引用计数。
                // 统一由 ReleaseView 释放一次，账平。
                cleaned++;
            }
        }
        if (cleaned > 0)
            Z_Logger.LogWarning($"[GameUIManager] 🧟 清理了 {cleaned} 个僵尸实例: {key}");
    }

    // ============================================================
    //  12. Update：空闲超时 + 僵尸自检 + 字段一致性兜底（每 1 秒扫一次）
    // ============================================================
    private void Update()
    {
        float now = Time.time;

        // 12.1 每 1 秒扫一次僵尸 + 空闲超时 + 字段一致性
        if (now - _lastScanTime >= ScanInterval)
        {
            _lastScanTime = now;

            // 快照遍历，防止内部回调改 _viewRuntime 导致迭代器失效
            var snapshot = new List<KeyValuePair<string, ViewRuntime>>(_viewRuntime);
            foreach (var kvp in snapshot)
            {
                var rt = kvp.Value;
                string key = kvp.Key;
                if (rt == null) continue;
                if (!_viewRuntime.TryGetValue(key, out var current) || current != rt) continue;

                // 活跃 Instance 是僵尸？
                if (rt.Instance != null && !IsValidInstance(rt.Instance))
                {
                    Z_Logger.LogWarning($"[GameUIManager] 🧟 Update 检测到活跃僵尸: {key}, name='{rt.Instance.name}', scene='{rt.Instance.scene.name}'");
                    SafeDestroyInstance(rt.Instance, key);
                    rt.Instance = null;
                    rt.State = ViewState.NotLoaded;
                    ClearCompatField(key);
                }

                // ✅ 字段一致性兜底：字段语义 = 可用实例
                var compat = GetCompatField(key);
                if (compat != null && !IsUsableInstance(compat))
                {
                    Z_Logger.LogWarning($"[GameUIManager] 🧹 字段与实例不一致，清字段: {key}, " +
                                        $"active={compat.gameObject.activeInHierarchy}, scene='{compat.gameObject.scene.name}'");
                    ClearCompatField(key);
                }
                else if (compat == null && rt.State == ViewState.Showing && IsUsableInstance(rt.Instance))
                {
                    Z_Logger.LogWarning($"[GameUIManager] 🧹 字段为空但实例可用，补赋: {key}");
                    AssignToCompatField(key, rt.Instance);
                }

                // 池里僵尸 + 空闲超时
                for (int i = rt.PopupPool.Count - 1; i >= 0; i--)
                {
                    var entry = rt.PopupPool[i];

                    if (entry.Instance == null)
                    {
                        rt.PopupPool.RemoveAt(i);
                        continue;
                    }

                    if (!IsValidInstance(entry.Instance))
                    {
                        Z_Logger.LogWarning($"[GameUIManager] 🧟 Update 检测到池中僵尸: {key}, name='{entry.Instance.name}', scene='{entry.Instance.scene.name}'");
                        SafeDestroyInstance(entry.Instance, key);
                        rt.PopupPool.RemoveAt(i);
                        // prefab 引用由 ReleaseView 统一释放，此处不 Release
                        continue;
                    }

                    if (now - entry.HiddenTime >= IdleTimeout)
                    {
                        Z_Logger.Log($"[GameUIManager] ⏰ 空闲超时销毁: {key}, 隐藏 {now - entry.HiddenTime:F1}s");
                        SafeDestroyInstance(entry.Instance, key);
                        rt.PopupPool.RemoveAt(i);
                        // prefab 引用由 ReleaseView 统一释放，此处不 Release
                    }
                }
            }
        }

        // 12.2 延迟释放
        List<string> toRelease = null;
        {
            var snapshot = new List<KeyValuePair<string, ViewRuntime>>(_viewRuntime);
            foreach (var kvp in snapshot)
            {
                var rt = kvp.Value;
                if (rt == null) continue;
                if (rt.State == ViewState.Loaded && rt.RefCount <= 0 &&
                    rt.ReleaseStartTime > 0 && now - rt.ReleaseStartTime >= ReleaseDelay)
                {
                    if (toRelease == null) toRelease = new List<string>();
                    toRelease.Add(kvp.Key);
                }
            }
        }
        if (toRelease != null)
        {
            foreach (string key in toRelease)
                if (_viewRuntime.TryGetValue(key, out var rt) && _viewRegistry.TryGetValue(key, out var meta))
                    ReleaseView(key, meta, rt);
        }

        // 12.3 内存压力检查
        _memCheckTimer += Time.deltaTime;
        if (_memCheckTimer >= 5f)
        {
            _memCheckTimer = 0f;
            long currentMem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            if (currentMem > 200L * 1024 * 1024)
                AssetManager.CleanupByMemoryPressure(200L * 1024 * 1024);
        }
    }

    private float _memCheckTimer = 0f;

    // ============================================================
    //  13. ReleaseView
    // ============================================================
    private void ReleaseView(string key, ViewMeta meta, ViewRuntime rt)
    {
        Z_Logger.Log($"[GameUIManager] 释放 View: {key}");
        rt.State = ViewState.Releasing;

        if (rt.Instance != null) { SafeDestroyInstance(rt.Instance, key); rt.Instance = null; }
        while (rt.PopupPool.Count > 0)
        {
            var entry = rt.PopupPool[0];
            rt.PopupPool.RemoveAt(0);
            if (entry.Instance != null) SafeDestroyInstance(entry.Instance, key);
        }

        AssetManager.ReleaseCachedReference(meta.PrefabPath);
        Z_Logger.Log($"[GameUIManager] Release 清字段: {key}");
        ClearCompatField(key);
        rt.State = ViewState.Released;
    }

    private GameObject GetCachedPrefab(string prefabPath)
    {
        GameObject prefab = null;
        AssetManager.LoadFromAddressables<GameObject>(prefabPath, AssetCategory.UI, (result, handle) => { prefab = result; });
        return prefab;
    }

    // ============================================================
    //  14. 兼容字段
    // ============================================================
    private void AssignToCompatField(string key, GameObject instance)
    {
        if (instance == null) return;
        switch (key)
        {
            case "BagView": bagView = instance.GetComponent<BagView>(); break;
            case "FishBagView": fishBagView = instance.GetComponent<FishBagView>(); break;
            case "MallView": mallView = instance.GetComponent<MallView>(); break;
            case "EquipmentView": equipmentView = instance.GetComponent<EquipmentView>(); break;
            case "AdvertisingView": advertisingView = instance.GetComponent<AdvertisingView>(); break;
            case "MapView": mapView = instance.GetComponent<MapView>(); break;
            case "CollectionView": collectionView = instance.GetComponent<CollectionView>(); break;
            case "FishTankView": fishTankView = instance.GetComponent<FishTankView>(); break;
            case "EggView": eggView = instance.GetComponent<EggView>(); break;
            case "PetView": petView = instance.GetComponent<PetView>(); break;
            case "GameMatch3View": gameMatch3View = instance.GetComponent<GameMatch3View>(); break;
        }
    }

    private void ClearCompatField(string key)
    {
        switch (key)
        {
            case "BagView": bagView = null; break;
            case "FishBagView": fishBagView = null; break;
            case "MallView": mallView = null; break;
            case "EquipmentView": equipmentView = null; break;
            case "AdvertisingView": advertisingView = null; break;
            case "MapView": mapView = null; break;
            case "CollectionView": collectionView = null; break;
            case "FishTankView": fishTankView = null; break;
            case "EggView": eggView = null; break;
            case "PetView": petView = null; break;
            case "GameMatch3View": gameMatch3View = null; break;
        }
    }

    private BaseView GetCompatField(string key)
    {
        switch (key)
        {
            case "BagView": return bagView;
            case "FishBagView": return fishBagView;
            case "MallView": return mallView;
            case "EquipmentView": return equipmentView;
            case "AdvertisingView": return advertisingView;
            case "MapView": return mapView;
            case "CollectionView": return collectionView;
            case "FishTankView": return fishTankView;
            case "EggView": return eggView;
            case "PetView": return petView;
            case "GameMatch3View": return gameMatch3View;
            default: return null;
        }
    }

    // ============================================================
    //  15. 生命周期 & 事件
    // ============================================================
    public void Init()
    {
        InitViewRegistry();
        RegisterEvents();
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register("UI_OpenBag", OpenBag);
        CommunicateEvent.Register("UI_OpenFishBag", OpenFishBag);
        CommunicateEvent.Register("UI_OpenMall", OpenMall);
        CommunicateEvent.Register("UI_OpenEquipment", OpenEquipment);
        CommunicateEvent.Register("UI_OpenMap", OpenMap);
        CommunicateEvent.Register("UI_OpenCollection", OpenCollection);
        CommunicateEvent.Register("UI_OpenFishTank", OpenFishTankView);
        CommunicateEvent.Register("UI_CloseFishTank", CloseFishTankView);
        CommunicateEvent.Register("UI_OpenEgg", OpenEggView);
        CommunicateEvent.Register("UI_CloseEgg", CloseEggView);
        CommunicateEvent.Register("UI_OpenPet", OpenPetView);
        CommunicateEvent.Register("UI_ClosePet", ClosePetView);
        CommunicateEvent.Register("UI_OpenGameMatch3", OpenGameMatch3);
        CommunicateEvent.Register("UI_CloseGameMatch3", CloseGameMatch3);

        CommunicateEvent.Register<string>(CommunicateEvent.EVENT_UI_SHOW_TIP, ShowTip);
        CommunicateEvent.Register<CommunicateEvent.AdvertisingRequest>(CommunicateEvent.EVENT_UI_SHOW_ADVERTISING, OnShowAdvertisingRequest);
        CommunicateEvent.Register<Dictionary<string, object>>("SceneSwitchRequest", OnSceneSwitchRequest);
        CommunicateEvent.Register("FishTankDataUpdated", OnFishTankDataUpdated);
    }

    private void OnShowAdvertisingRequest(CommunicateEvent.AdvertisingRequest request)
    {
        ShowAdvertising(request.info, request.targetId, request.btnText, (bool success) =>
        {
            CommunicateEvent.OnCallback(request.callbackId, success);
        });
    }

    // ============================================================
    //  16. 对外判据（供 LoadDataManager / PlayerDataManager 等调用）
    // ============================================================

    /// <summary>
    /// 实例是否可响应：存活 + 场景有效 + 激活链上。
    /// </summary>
    public static bool IsViewReady(BaseView view)
    {
        if (view == null) return false;
        var go = view.gameObject;
        if (go == null) return false;
        if (!go.scene.IsValid() || !go.scene.isLoaded) return false;
        if (!go.activeInHierarchy) return false;
        return true;
    }

    /// <summary>
    /// 按 key 取"当前可响应"的 View，不可用返回 null。
    /// 调用方用它判"能不能调"，不要直接碰兼容字段。
    /// </summary>
    public T GetReadyView<T>(string key) where T : BaseView
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (!_viewRuntime.TryGetValue(key, out var rt)) return null;
        if (rt.State != ViewState.Showing) return null;
        if (!IsUsableInstance(rt.Instance)) return null;
        return rt.Instance.GetComponent<T>();
    }

    // ---- 对外接口 ----
    public void OpenBag() => OpenView("BagView");
    public void CloseBag() => CloseView("BagView");
    public void OpenFishBag() => OpenView("FishBagView");
    public void CloseFishBag() => CloseView("FishBagView");
    public void OpenMall() => OpenView("MallView");
    public void CloseMall() => CloseView("MallView");
    public void OpenEquipment() => OpenView("EquipmentView");
    public void CloseEquipment() => CloseView("EquipmentView");
    public void OpenMap() => OpenView("MapView");
    public void CloseMap() => CloseView("MapView");
    public void OpenCollection() => OpenView("CollectionView");
    public void CloseCollection() => CloseView("CollectionView");
    public void OpenFishTankView() => OpenView("FishTankView");
    public void CloseFishTankView() => CloseView("FishTankView");
    public void OpenEggView() => OpenView("EggView");
    public void CloseEggView() => CloseView("EggView");
    public void OpenPetView() => OpenView("PetView");
    public void ClosePetView() => CloseView("PetView");
    public void OpenGameMatch3() => OpenView("GameMatch3View");
    public void CloseGameMatch3() => CloseView("GameMatch3View");

    public void UpdateMainViewTimee(TimeStatus status, string timeName) { if (mainGameView != null) mainGameView.UpdateTime(status, timeName); }
    public void UpdateMainViewWeather(int weatherId, string weatherName) { if (mainGameView != null) mainGameView.UpdateWeather(weatherId, weatherName); }
    public void ShowCatchResult(string itemName, float weight, Sprite icon, int starRatingId = 0, int itemId = 0, bool isFish = true, bool isFirstCatch = false)
    { if (mainGameView != null) mainGameView.ShowCatchResult(itemName, weight, icon, starRatingId, itemId, isFish, isFirstCatch); }
    public void UpdateGoldDisplay(int goldAmount) { if (mainGameView != null) mainGameView.UpdateGold(goldAmount); }
    public void UpdateFishCountDisplay(int currentCount, int maxCapacity) { if (mainGameView != null) mainGameView.UpdateFishCount(currentCount, maxCapacity); }
    public void UpdateBaitCountDisplay(int baitCount) { if (mainGameView != null) mainGameView.UpdateBaitCount(baitCount); }
    public void UpdateContinuousModeRemainingTime(float remainingTime) { if (mainGameView != null) mainGameView.UpdateContinuousModeTime(remainingTime); }

    public void ShowTip(string message) { if (tipView != null) tipView.ShowTip(message); }
    public void ShowDialog(string message, DialogType type = DialogType.Warning, System.Action onConfirm = null) { if (dialogView != null) dialogView.Show(message, type, onConfirm); }
    public void HideDialog() { if (dialogView != null) dialogView.Hide(); }
    public static void ShowWarningMessage(string message) { if (Instance != null) Instance.ShowDialog(message, DialogType.Warning); }
    public static void ShowInfoMessage(string message, System.Action onConfirm = null) { if (Instance != null) Instance.ShowDialog(message, DialogType.Info, onConfirm); }
    public static void ShowMessage(string message) { if (Instance != null) Instance.ShowTip(message); }

    public void ShowAdvertising(string info, int targetId, string btnText, System.Action onConfirm)
    {
        OpenView("AdvertisingView", obj => { if (obj != null) obj.GetComponent<AdvertisingView>()?.ShowAd(info, onConfirm, null, btnText); });
    }
    public void ShowAdvertising(string info, int targetId, string btnText, System.Action<bool> onConfirmWithResult)
    {
        OpenView("AdvertisingView", obj => { if (obj != null) obj.GetComponent<AdvertisingView>()?.ShowAd(info, onConfirmWithResult, null, btnText); });
    }

    private void OnSceneSwitchRequest(Dictionary<string, object> data)
    {
        if (data == null || !data.ContainsKey("sceneId")) return;
        int sceneId = (int)data["sceneId"];
        CommunicateEvent.Modify<int>("Server_SceneSwitch", sceneId);
    }

    private void OnFishTankDataUpdated()
    {
        // 字段语义已保证：仅 Showing 且可用时非空
        if (IsUsableInstance(fishTankView)) fishTankView.RefreshAll();
    }
    public void RefreshFishTankData()
    {
        if (IsUsableInstance(fishTankView)) fishTankView.RefreshAll();
    }
    public void InitTimeNameDic() { }
}

/* ============================================================
 *  ⚠️ 跳场景隐患说明（当前项目 GameScene 一路到底，不会触发）
 * ============================================================
 *
 *  当前架构的前提：
 *    - GameScene 从头到尾不卸载
 *    - viewContainer 属于 GameScene 的 Canvas
 *    - GameUIManager 是 DontDestroyOnLoad 常驻
 *
 *  如果将来引入「加载场景 → 游戏场景」这种真正的场景切换（例如
 *  从 StartScene 登录后跳 LoadingScene 再跳 GameScene），会触发
 *  以下问题：
 *
 *  1. GameScene 卸载时，viewContainer 和它下面的所有 BagView /
 *     FishBagView 会被 Unity 强制销毁。
 *  2. GameUIManager 的 PopupPool 里持有的实例引用会变成「伪空」
 *     （== null 为 true，但引用地址还在），形成僵尸引用。
 *  3. 场景卸载后，这些实例的 scene.IsValid() 会返回 false，
 *     scene.name 为空字符串。
 *  4. 如果此时 MainGameView 或其他常驻 View 触发了
 *     CommunicateEvent.Modify("Bag_RefreshItems") 等事件，
 *     挂在事件表里的僵尸实例的 OnBagRefresh 会被调用，
 *     在 scene='' 的 contentTransform 上 Instantiate →
 *     「Cannot instantiate objects with a parent which is persistent」。
 *
 *  解决方案（二选一，本次未实施）：
 *
 *    【方案 A】viewContainer 常驻化：
 *      把 viewContainer 从 UICanvas 下移出，作为 GameUIManager
 *      的子物体（或独立根节点），在 GameUIManager.Awake 里对它
 *      调用 DontDestroyOnLoad(viewContainer.root.gameObject)。
 *      这样 BagView 也常驻，任何场景卸载都不影响它，
 *      GameUIManager 可以永远持有、永远不丢引用。
 *      代价：viewContainer 需要自带 Canvas（ScreenSpaceOverlay），
 *            并调整 sortingOrder 与 UICanvas 协调。
 *
 *    【方案 B】场景卸载时清空池：
 *      在 GameUIManager.Init 里注册
 *          SceneManager.sceneUnloaded += OnSceneUnloaded;
 *      在 OnSceneUnloaded 里清空所有 _viewRuntime 的 Instance /
 *      PopupPool / 兼容字段。因为实例本身已被 Unity 随场景销毁，
 *      这里只需要放弃引用（不要手动 Destroy）。新场景加载后，
 *      OpenBag 会从零开始 Instantiate 新的 BagView，不会产生僵尸。
 *
 *  当前项目采用「GameScene 一路到底」架构，不存在真正的场景切换，
 *  因此上面的隐患不会触发。如果将来项目扩展为多场景，请务必按
 *  方案 A 或方案 B 改造。
 * ============================================================ */
