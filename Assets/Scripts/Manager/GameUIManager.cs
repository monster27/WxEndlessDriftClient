using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class GameUIManager : SingletonMonoFromScene<GameUIManager>
{
    // ============================================================
    //  #region 1. 场景固定的常驻 View（不走 AA）
    // ============================================================

    [Header("===== 场景固定的常驻 View =====")]
    public MainGameView mainGameView;
    public DialogView dialogView;
    public TipView tipView;

    [Header("===== 动态 View 的父容器 =====")]
    [Tooltip("所有走 AA 动态加载的 View 都实例化到这个节点下")]
    public RectTransform viewContainer;

    // ============================================================
    //  #region 1.5 兼容字段（走 AA 的 View，加载后自动赋值）
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
    //  #region 2. View 元数据表（ViewRegistry）
    // ============================================================

    public enum ViewType
    {
        Resident,   // 常驻型：单例，隐藏不销毁
        Popup,      // 弹窗型：关闭回池
        Temporary,  // 临时型：关闭即销毁
    }

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
        // ---- 弹窗型 ----
        RegisterView("BagView", "Assets/Addressables/UI/View/BagView.prefab", ViewType.Popup, typeof(BagView));
        RegisterView("FishBagView", "Assets/Addressables/UI/View/FishBagView.prefab", ViewType.Popup, typeof(FishBagView));
        RegisterView("MallView", "Assets/Addressables/UI/View/MallView.prefab", ViewType.Popup, typeof(MallView));
        RegisterView("MapView", "Assets/Addressables/UI/View/MapView.prefab", ViewType.Popup, typeof(MapView));
        RegisterView("CollectionView", "Assets/Addressables/UI/View/CollectionView.prefab", ViewType.Popup, typeof(CollectionView));
        RegisterView("FishTankView", "Assets/Addressables/UI/View/FishTankView.prefab", ViewType.Popup, typeof(FishTankView));
        RegisterView("EggView", "Assets/Addressables/UI/View/EggView.prefab", ViewType.Popup, typeof(EggView));
        RegisterView("PetView", "Assets/Addressables/UI/View/PetView.prefab", ViewType.Popup, typeof(PetView));

        // ---- 常驻型 ----
        RegisterView("EquipmentView", "Assets/Addressables/UI/View/EquipmentView.prefab", ViewType.Resident, typeof(EquipmentView));
        RegisterView("GameMatch3View", "Assets/Addressables/UI/View/GameMatch3View.prefab", ViewType.Resident, typeof(GameMatch3View));

        // ---- 广告 ----
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
    //  #region 3. View 状态机 + 引用计数 + 实例管理
    // ============================================================

    private enum ViewState
    {
        NotLoaded,
        Loading,
        Loaded,
        Showing,
        Hiding,
        Releasing,
        Released,
    }

    private class ViewRuntime
    {
        public ViewState State = ViewState.NotLoaded;
        public int RefCount = 0;
        public float ReleaseStartTime = 0f;
        public GameObject Instance;
        public Queue<GameObject> PopupPool = new Queue<GameObject>();
        public bool CancelledWhileLoading = false;
        public List<Action<GameObject>> WaitingCallbacks = new List<Action<GameObject>>();
    }

    private readonly Dictionary<string, ViewRuntime> _viewRuntime = new Dictionary<string, ViewRuntime>();

    private const float ReleaseDelay = 10f;
    private const int PopupPoolMaxSize = 3;

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
    //  #region 4. 对外接口
    // ============================================================

    /// <summary>
    /// 打开 View
    /// </summary>
    public void OpenView(string key, Action<GameObject> onOpened = null)
    {
        if (string.IsNullOrEmpty(key))
        {
            Z_Logger.LogError("[GameUIManager] OpenView: key 为空");
            onOpened?.Invoke(null);
            return;
        }

        if (!_viewRegistry.TryGetValue(key, out var meta))
        {
            Z_Logger.LogError($"[GameUIManager] OpenView: 未注册的 View key = {key}");
            onOpened?.Invoke(null);
            return;
        }

        if (viewContainer == null)
        {
            Z_Logger.LogError("[GameUIManager] OpenView: viewContainer 未绑定！");
            onOpened?.Invoke(null);
            return;
        }

        var rt = GetOrCreateRuntime(key);

        Z_Logger.Log($"[GameUIManager] OpenView: {key}, 当前状态: {rt.State}, RefCount: {rt.RefCount}");

        switch (rt.State)
        {
            case ViewState.Showing:
                onOpened?.Invoke(rt.Instance);
                return;

            case ViewState.Loading:
                rt.WaitingCallbacks.Add(onOpened);
                return;

            case ViewState.Hiding:
                rt.State = ViewState.Showing;
                rt.RefCount++;
                ShowExistingInstance(key, meta, rt, onOpened);
                return;

            case ViewState.Loaded:
            case ViewState.Released:
            case ViewState.NotLoaded:
                rt.RefCount++;
                StartCoroutine(LoadAndShowCoroutine(key, meta, rt, onOpened));
                return;

            case ViewState.Releasing:
                rt.RefCount++;
                rt.ReleaseStartTime = 0f;
                ShowExistingInstance(key, meta, rt, onOpened);
                return;
        }

        onOpened?.Invoke(null);
    }

    /// <summary>
    /// 关闭 View
    /// </summary>
    public void CloseView(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!_viewRegistry.TryGetValue(key, out var meta)) return;

        var rt = GetOrCreateRuntime(key);

        Z_Logger.Log($"[GameUIManager] CloseView: {key}, 当前状态: {rt.State}, RefCount: {rt.RefCount}");

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
    //  #region 5. 内部实现
    // ============================================================

    private IEnumerator LoadAndShowCoroutine(string key, ViewMeta meta, ViewRuntime rt, Action<GameObject> onOpened)
    {
        // 缓存已有
        if (rt.Instance != null || rt.PopupPool.Count > 0)
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
            yield break;
        }

        rt.State = ViewState.Loaded;

        if (rt.CancelledWhileLoading)
        {
            Z_Logger.Log($"[GameUIManager] 加载完成但已被取消，直接释放: {key}");
            rt.CancelledWhileLoading = false;

            foreach (var cb in rt.WaitingCallbacks) cb?.Invoke(null);
            rt.WaitingCallbacks.Clear();

            AssetManager.ReleaseCachedReference(meta.PrefabPath);
            rt.State = ViewState.NotLoaded;
            yield break;
        }

        ShowExistingInstance(key, meta, rt, null);

        foreach (var cb in rt.WaitingCallbacks) cb?.Invoke(rt.Instance);
        rt.WaitingCallbacks.Clear();
    }

    /// <summary>
    /// 从缓存/池取实例并显示
    /// </summary>
    private void ShowExistingInstance(string key, ViewMeta meta, ViewRuntime rt, Action<GameObject> onOpened)
    {
        GameObject instance = null;

        switch (meta.Type)
        {
            case ViewType.Resident:
                if (rt.Instance == null)
                {
                    var prefab = GetCachedPrefab(meta.PrefabPath);
                    if (prefab != null)
                    {
                        instance = Instantiate(prefab, viewContainer);  // ✅ 用 viewContainer
                        rt.Instance = instance;
                    }
                }
                else
                {
                    instance = rt.Instance;
                }
                break;

            case ViewType.Popup:
                if (rt.PopupPool.Count > 0)
                {
                    instance = rt.PopupPool.Dequeue();
                    instance.SetActive(true);
                }
                else
                {
                    var prefab = GetCachedPrefab(meta.PrefabPath);
                    if (prefab != null)
                    {
                        instance = Instantiate(prefab, viewContainer);  // ✅ 用 viewContainer
                    }
                }
                break;

            case ViewType.Temporary:
                var prefab2 = GetCachedPrefab(meta.PrefabPath);
                if (prefab2 != null)
                {
                    instance = Instantiate(prefab2, viewContainer);  // ✅ 用 viewContainer
                }
                break;
        }

        if (instance == null)
        {
            Z_Logger.LogError($"[GameUIManager] 实例化失败: {key}");
            onOpened?.Invoke(null);
            return;
        }

        rt.Instance = instance;
        rt.State = ViewState.Showing;

        // 自动调 BaseViewInit + ShowView
        var baseView = instance.GetComponent<BaseView>();
        if (baseView != null)
        {
            baseView.BaseViewInit();
            baseView.ShowView();
        }

        // 把实例赋给兼容字段
        AssignToCompatField(key, instance);

        Z_Logger.Log($"[GameUIManager] 显示 View: {key}");
        onOpened?.Invoke(instance);
    }

    private void HideInstance(string key, ViewMeta meta, ViewRuntime rt)
    {
        if (rt.Instance == null) return;

        var baseView = rt.Instance.GetComponent<BaseView>();
        if (baseView != null)
        {
            baseView.HideView();
        }

        switch (meta.Type)
        {
            case ViewType.Resident:
                rt.Instance.SetActive(false);
                break;

            case ViewType.Popup:
                rt.Instance.SetActive(false);
                if (rt.PopupPool.Count < PopupPoolMaxSize)
                {
                    rt.PopupPool.Enqueue(rt.Instance);
                }
                else
                {
                    Destroy(rt.Instance);
                }
                rt.Instance = null;
                break;

            case ViewType.Temporary:
                Destroy(rt.Instance);
                rt.Instance = null;
                break;
        }

        Z_Logger.Log($"[GameUIManager] 隐藏 View: {key}");
    }

    private void ReleaseView(string key, ViewMeta meta, ViewRuntime rt)
    {
        Z_Logger.Log($"[GameUIManager] 释放 View: {key}");

        rt.State = ViewState.Releasing;

        if (rt.Instance != null)
        {
            Destroy(rt.Instance);
            rt.Instance = null;
        }
        while (rt.PopupPool.Count > 0)
        {
            var obj = rt.PopupPool.Dequeue();
            if (obj != null) Destroy(obj);
        }

        // ✅ 每个 key 引用计数 -1
        AssetManager.ReleaseCachedReference(meta.PrefabPath);

        ClearCompatField(key);

        rt.State = ViewState.Released;
    }

    /// <summary>
    /// 从 AssetManager 缓存里取 prefab
    /// </summary>
    private GameObject GetCachedPrefab(string prefabPath)
    {
        GameObject prefab = null;

        AssetManager.LoadFromAddressables<GameObject>(prefabPath, AssetCategory.UI, (result, handle) =>
        {
            prefab = result;
        });

        return prefab;
    }

    private float _memCheckTimer = 0f;

    private void Update()
    {
        float now = Time.time;

        // 延迟释放
        List<string> toRelease = null;
        foreach (var kvp in _viewRuntime)
        {
            var rt = kvp.Value;
            if (rt.State == ViewState.Loaded && rt.RefCount <= 0 &&
                rt.ReleaseStartTime > 0 && now - rt.ReleaseStartTime >= ReleaseDelay)
            {
                if (toRelease == null) toRelease = new List<string>();
                toRelease.Add(kvp.Key);
            }
        }

        if (toRelease != null)
        {
            foreach (string key in toRelease)
            {
                if (_viewRegistry.TryGetValue(key, out var meta))
                {
                    ReleaseView(key, meta, _viewRuntime[key]);
                }
            }
        }

        // 内存压力检查
        _memCheckTimer += Time.deltaTime;
        if (_memCheckTimer >= 5f)
        {
            _memCheckTimer = 0f;
            long currentMem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            if (currentMem > 200L * 1024 * 1024)
            {
                AssetManager.CleanupByMemoryPressure(200L * 1024 * 1024);
            }
        }
    }

    private void AssignToCompatField(string key, GameObject instance)
    {
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

    // ============================================================
    //  #region 6. 生命周期 + 兼容外壳
    // ============================================================

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

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

    // ---- 兼容外壳 ----

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

    // ---- 常驻 View 直接接口 ----

    public void UpdateMainViewTimee(TimeStatus status, string timeName)
    {
        if (mainGameView != null) mainGameView.UpdateTime(status, timeName);
    }

    public void UpdateMainViewWeather(int weatherId, string weatherName)
    {
        if (mainGameView != null) mainGameView.UpdateWeather(weatherId, weatherName);
    }

    public void ShowCatchResult(string itemName, float weight, Sprite icon, int starRatingId = 0, int itemId = 0, bool isFish = true, bool isFirstCatch = false)
    {
        if (mainGameView != null) mainGameView.ShowCatchResult(itemName, weight, icon, starRatingId, itemId, isFish, isFirstCatch);
    }

    public void UpdateGoldDisplay(int goldAmount)
    {
        if (mainGameView != null) mainGameView.UpdateGold(goldAmount);
    }

    public void UpdateFishCountDisplay(int currentCount, int maxCapacity)
    {
        if (mainGameView != null) mainGameView.UpdateFishCount(currentCount, maxCapacity);
    }

    public void UpdateBaitCountDisplay(int baitCount)
    {
        if (mainGameView != null) mainGameView.UpdateBaitCount(baitCount);
    }

    public void UpdateContinuousModeRemainingTime(float remainingTime)
    {
        if (mainGameView != null) mainGameView.UpdateContinuousModeTime(remainingTime);
    }

    // ---- Tip ----

    public void ShowTip(string message)
    {
        if (tipView != null) tipView.ShowTip(message);
    }

    // ---- Dialog（常驻，不走 AA） ----

    public void ShowDialog(string message, DialogType type = DialogType.Warning, System.Action onConfirm = null)
    {
        if (dialogView != null) dialogView.Show(message, type, onConfirm);
    }

    public void HideDialog()
    {
        if (dialogView != null) dialogView.Hide();
    }

    public static void ShowWarningMessage(string message)
    {
        if (Instance != null) Instance.ShowDialog(message, DialogType.Warning);
    }

    public static void ShowInfoMessage(string message, System.Action onConfirm = null)
    {
        if (Instance != null) Instance.ShowDialog(message, DialogType.Info, onConfirm);
    }

    public static void ShowMessage(string message)
    {
        if (Instance != null) Instance.ShowTip(message);
    }

    // ---- 广告 ----

    public void ShowAdvertising(string info, int targetId, string btnText, System.Action onConfirm)
    {
        OpenView("AdvertisingView", obj =>
        {
            if (obj != null)
            {
                var ad = obj.GetComponent<AdvertisingView>();
                if (ad != null) ad.ShowAd(info, onConfirm, null, btnText);
            }
        });
    }

    public void ShowAdvertising(string info, int targetId, string btnText, System.Action<bool> onConfirmWithResult)
    {
        OpenView("AdvertisingView", obj =>
        {
            if (obj != null)
            {
                var ad = obj.GetComponent<AdvertisingView>();
                if (ad != null) ad.ShowAd(info, onConfirmWithResult, null, btnText);
            }
        });
    }

    // ---- 场景切换 ----

    private void OnSceneSwitchRequest(Dictionary<string, object> data)
    {
        if (data == null || !data.ContainsKey("sceneId"))
        {
            Z_Logger.LogWarning("[GameUIManager] 场景切换请求数据无效");
            return;
        }

        int sceneId = (int)data["sceneId"];
        Z_Logger.Log($"[GameUIManager] 收到场景切换请求: {sceneId}");
        CommunicateEvent.Modify<int>("Server_SceneSwitch", sceneId);
    }

    private void OnFishTankDataUpdated()
    {
        Z_Logger.Log("[GameUIManager] 收到鱼缸数据更新事件");
        if (fishTankView != null) fishTankView.RefreshAll();
    }

    public void RefreshFishTankData()
    {
        Z_Logger.Log("[GameUIManager] RefreshFishTankData 被调用");
        if (fishTankView != null) fishTankView.RefreshAll();
    }

    public void InitTimeNameDic() { }
}
