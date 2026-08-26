using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 鱼缸管理器 - 负责3D鱼缸中鱼的显示和动画（纯渲染层）
/// 职责：只负责渲染，不管理数据版本，不做数据比较
/// </summary>
public class FishTankManager : MonoBehaviour
{
    [SerializeField] private GameObject fishTankGo;
    [Header("===== 鱼缸区域(绑定Quad) =====")]
    [SerializeField] private Transform totalArea;
    [SerializeField] private Transform bottomArea;

    [Header("===== 鱼容器 =====")]
    [SerializeField] private GameObject fishContainer;

    [Header("===== 四个行为类型列表 =====")]
    [SerializeField] private List<FishTankFishCtrl> fullScreenSwimList = new List<FishTankFishCtrl>();
    [SerializeField] private List<FishTankFishCtrl> fullScreenStaticList = new List<FishTankFishCtrl>();
    [SerializeField] private List<FishTankFishCtrl> bottomSwimList = new List<FishTankFishCtrl>();
    [SerializeField] private List<FishTankFishCtrl> bottomStaticList = new List<FishTankFishCtrl>();

    [Header("===== 鱼预制体 =====")]
    [SerializeField] private GameObject fishPrefab;

    [Header("===== Shader =====")]
    [SerializeField] private Shader fishShader;

    [Header("===== 大小参数 =====")]
    [SerializeField] private float baseHeight = 0.5f;
    [SerializeField] private float uniformScale = 1f;

    [Header("===== 方向变化间隔(秒) =====")]
    [SerializeField] private float directionChangeIntervalMin = 4f;
    [SerializeField] private float directionChangeIntervalMax = 20f;

    [Header("===== 全屏移动鱼类生成范围 =====")]
    [Range(0.1f, 1f)]
    [SerializeField] private float fullScreenSwimSpawnRange = 0.7f;

    [Header("===== 渲染队列 =====")]
    [SerializeField] private int renderQueue = 3110;

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    // ============================================================
    // 鱼游动物理参数
    // ============================================================

    [Header("===== 水平移动(左右) =====")]
    [SerializeField] private float moveSpeedMin = 0.35f;
    [SerializeField] private float moveSpeedMax = 1.2f;

    [Header("===== 垂直移动(上下) =====")]
    [SerializeField] private float verticalSpeedRatio = 0.4f;
    [Range(0f, 1f)]
    [SerializeField] private float verticalMoveProbability = 0.2f;

    [Header("===== 物理参数 =====")]
    [SerializeField] private float accelerationMin = 2.5f;
    [SerializeField] private float accelerationMax = 5.0f;
    [SerializeField] private float dragForce = 0.8f;

    [Header("===== 蓄力参数 =====")]
    [SerializeField] private float chargeDurationMin = 0.4f;
    [SerializeField] private float chargeDurationMax = 1f;
    [SerializeField] private float chargeScaleX = 0.6f;
    [SerializeField] private float chargeScaleY = 1.35f;
    [SerializeField] private float chargeSpeedRatio = 0.15f;

    [Header("===== 冲刺参数 =====")]
    [SerializeField] private float sprintDurationMin = 1.5f;
    [SerializeField] private float sprintDurationMax = 3.5f;

    // ============================================================
    // 鱼饵系统（对象池）
    // ============================================================

    [Header("===== 鱼饵系统 =====")]
    [SerializeField] private GameObject fishTankBaitPrefab;
    [SerializeField] private Transform fishTankBaitContainer;
    [SerializeField] private float fishTankBaitTriggerRadius = 1.5f;
    [SerializeField] private float fishTankBaitFallSpeed = 0.5f;
    [SerializeField] private float fishTankBaitChaseDurationMin = 0.5f;
    [SerializeField] private float fishTankBaitChaseDurationMax = 0.8f;
    [SerializeField] private float fishTankBaitChaseSpeedMultiplier = 5f;
    [SerializeField] private int fishTankBaitMaxQueueSize = 50;
    [SerializeField] private float fishTankBaitScale = 0.5f;
    [SerializeField] private int fishTankBaitPoolInitSize = 5;

    // 鱼饵对象池
    private Queue<GameObject> _baitPool = new Queue<GameObject>();
    private Queue<GameObject> _activeBaits = new Queue<GameObject>();

    // ============================================================
    // 核心状态管理（纯渲染状态）
    // ============================================================

    private bool _isInitialized;
    private Coroutine _updateCoroutine;
    private Rect _totalRect;
    private Rect _bottomRect;

    // 当前正在显示的鱼列表（用于判断是否需要重建）
    private List<FishDetailData> _currentDisplayingFish = new List<FishDetailData>();

    // ============================================================
    // 属性
    // ============================================================

    public bool IsInitialized => _isInitialized;
    public int TotalFishCount => fullScreenSwimList.Count + fullScreenStaticList.Count +
                                 bottomSwimList.Count + bottomStaticList.Count;
    public bool EnableDebugLog => enableDebugLog;
    public int BaitCount => _activeBaits.Count;
    public int PoolCount => _baitPool.Count;
    public Rect TotalRect => _totalRect;
    public Rect BottomRect => _bottomRect;

    // 公共参数访问
    public float DirectionChangeIntervalMin => directionChangeIntervalMin;
    public float DirectionChangeIntervalMax => directionChangeIntervalMax;
    public float FullScreenSwimSpawnRange => fullScreenSwimSpawnRange;
    public float MoveSpeedMin => moveSpeedMin;
    public float MoveSpeedMax => moveSpeedMax;
    public float VerticalSpeedRatio => verticalSpeedRatio;
    public float VerticalMoveProbability => verticalMoveProbability;
    public float AccelerationMin => accelerationMin;
    public float AccelerationMax => accelerationMax;
    public float DragForce => dragForce;
    public float ChargeDurationMin => chargeDurationMin;
    public float ChargeDurationMax => chargeDurationMax;
    public float ChargeScaleX => chargeScaleX;
    public float ChargeScaleY => chargeScaleY;
    public float ChargeSpeedRatio => chargeSpeedRatio;
    public float SprintDurationMin => sprintDurationMin;
    public float SprintDurationMax => sprintDurationMax;

    public float BaitTriggerRadius => fishTankBaitTriggerRadius;
    public float BaitFallSpeed => fishTankBaitFallSpeed;
    public float BaitChaseDurationMin => fishTankBaitChaseDurationMin;
    public float BaitChaseDurationMax => fishTankBaitChaseDurationMax;
    public float BaitChaseSpeedMultiplier => fishTankBaitChaseSpeedMultiplier;
    public float BaitScale => fishTankBaitScale;

    // ============================================================
    // 日志辅助方法
    // ============================================================

    private void LogDebug(string message)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankManager] {message}");
    }

    private void LogInfo(string message)
    {
        Z_Logger.Log($"[FishTankManager] {message}");
    }

    // ============================================================
    // 初始化
    // ============================================================

    public void Init()
    {
        if (_isInitialized) return;

        InitContainer();
        InitAreas();
        InitBaitSystem();
        UpdateRects();
        PreCreateBaits();

        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
        }

        _isInitialized = true;
        LogInfo("鱼缸管理器初始化完成");
    }

    private void InitContainer()
    {
        if (fishContainer == null)
        {
            fishContainer = new GameObject("FishContainer");
            fishContainer.transform.SetParent(transform);
            fishContainer.transform.localPosition = Vector3.zero;
            fishContainer.transform.localScale = Vector3.one;
        }
    }

    private void InitAreas()
    {
        if (totalArea == null)
        {
            GameObject go = new GameObject("TotalArea");
            go.transform.SetParent(transform);
            totalArea = go.transform;
            totalArea.localPosition = Vector3.zero;
            totalArea.localScale = Vector3.one;
            Z_Logger.LogWarning("[FishTankManager] totalArea 未绑定，已自动创建");
        }

        if (bottomArea == null)
        {
            GameObject go = new GameObject("BottomArea");
            go.transform.SetParent(transform);
            bottomArea = go.transform;
            bottomArea.localPosition = Vector3.zero;
            bottomArea.localScale = Vector3.one;
            Z_Logger.LogWarning("[FishTankManager] bottomArea 未绑定，已自动创建");
        }
    }

    private void InitBaitSystem()
    {
        if (fishTankBaitContainer == null)
        {
            GameObject go = new GameObject("FishTankBaitContainer");
            go.transform.SetParent(transform);
            fishTankBaitContainer = go.transform;
            fishTankBaitContainer.localPosition = Vector3.zero;
            fishTankBaitContainer.localScale = Vector3.one;
        }
    }

    // ============================================================
    // 鱼饵对象池
    // ============================================================

    private void PreCreateBaits()
    {
        for (int i = 0; i < fishTankBaitPoolInitSize; i++)
        {
            GameObject bait = CreateBaitInstance();
            bait.SetActive(false);
            _baitPool.Enqueue(bait);
        }
        LogDebug($"预创建 {fishTankBaitPoolInitSize} 个鱼饵到对象池");
    }

    private GameObject CreateBaitInstance()
    {
        if (fishTankBaitPrefab == null)
        {
            Z_Logger.LogError("[FishTankManager] fishTankBaitPrefab 为空!");
            return null;
        }

        GameObject bait = Instantiate(fishTankBaitPrefab, fishTankBaitContainer);
        bait.transform.localScale = Vector3.one * fishTankBaitScale;

        FishTankBaitCtrl baitComp = bait.GetComponent<FishTankBaitCtrl>();
        if (baitComp == null)
        {
            baitComp = bait.AddComponent<FishTankBaitCtrl>();
        }
        baitComp.Init(this, _totalRect, fishTankBaitFallSpeed, fishTankBaitScale);

        return bait;
    }

    private GameObject GetBaitFromPool(Vector3 position)
    {
        GameObject bait = null;

        if (_baitPool.Count > 0)
        {
            bait = _baitPool.Dequeue();
        }
        else
        {
            LogDebug($"对象池为空，动态创建新鱼饵");
            bait = CreateBaitInstance();
        }

        if (bait != null)
        {
            bait.SetActive(true);
            bait.transform.position = position;

            FishTankBaitCtrl baitComp = bait.GetComponent<FishTankBaitCtrl>();
            if (baitComp != null)
            {
                baitComp.ResetBait(position);
            }
        }

        return bait;
    }

    private void ReturnBaitToPool(GameObject bait)
    {
        if (bait == null) return;

        FishTankBaitCtrl baitComp = bait.GetComponent<FishTankBaitCtrl>();
        if (baitComp != null)
        {
            baitComp.Deactivate();
        }

        bait.SetActive(false);

        if (_baitPool.Count < fishTankBaitMaxQueueSize)
        {
            _baitPool.Enqueue(bait);
        }
        else
        {
            Destroy(bait);
        }
    }

    // ============================================================
    // 区域更新
    // ============================================================

    private void UpdateRects()
    {
        if (totalArea != null)
        {
            Vector3 pos = totalArea.position;
            Vector3 scale = totalArea.localScale;
            float width = scale.x;
            float height = scale.y;
            _totalRect = new Rect(pos.x - width / 2f, pos.y - height / 2f, width, height);
        }

        if (bottomArea != null)
        {
            Vector3 pos = bottomArea.position;
            Vector3 scale = bottomArea.localScale;
            float width = scale.x;
            float height = scale.y;
            _bottomRect = new Rect(pos.x - width / 2f, pos.y - height / 2f, width, height);
        }

        foreach (var fish in fullScreenSwimList) SetFishParams(fish);
        foreach (var fish in fullScreenStaticList) SetFishParams(fish);
        foreach (var fish in bottomSwimList) SetFishParams(fish);
        foreach (var fish in bottomStaticList) SetFishParams(fish);
    }

    private void SetFishParams(FishTankFishCtrl fish)
    {
        if (fish == null) return;

        float acceleration = UnityEngine.Random.Range(accelerationMin, accelerationMax);

        fish.totalAreaRect = _totalRect;
        fish.bottomAreaRect = _bottomRect;
        fish.SetBaseHeight(baseHeight);
        fish.UniformScale = uniformScale;
        fish.SetRenderQueue(renderQueue);
        fish.EnableDebugLog = enableDebugLog;
        fish.SetPhysicsParams(
            moveSpeedMin, moveSpeedMax,
            verticalSpeedRatio, verticalMoveProbability,
            acceleration, dragForce,
            chargeDurationMin, chargeDurationMax,
            chargeScaleX, chargeScaleY,
            chargeSpeedRatio,
            sprintDurationMin, sprintDurationMax
        );
    }

    // ============================================================
    // ✅ 核心渲染方法（纯渲染，不做数据判断）
    // ============================================================

    /// <summary>
    /// 显示鱼 - 纯渲染入口，由View调用
    /// </summary>
    public void ShowFish(List<FishDetailData> fishList)
    {
        LogInfo($"ShowFish: 接收 {fishList?.Count ?? 0} 条鱼");

        // 如果数据为空，清空显示
        if (fishList == null || fishList.Count == 0)
        {
            ClearFish();
            return;
        }

        // 比较是否和当前显示的数据相同（只比较ID，不做版本管理）
        if (IsSameFishList(fishList, _currentDisplayingFish))
        {
            LogDebug("鱼列表与当前显示相同，跳过重建");
            return;
        }

        // 更新当前显示列表
        _currentDisplayingFish = new List<FishDetailData>(fishList);

        // 清空旧鱼
        ClearAllFish();

        // 创建新鱼
        StartCoroutine(CreateFishCoroutine(fishList));
    }

    /// <summary>
    /// 比较两个鱼列表是否相同
    /// </summary>
    private bool IsSameFishList(List<FishDetailData> list1, List<FishDetailData> list2)
    {
        if (list1 == null && list2 == null) return true;
        if (list1 == null || list2 == null) return false;
        if (list1.Count != list2.Count) return false;

        // 比较ID集合
        var ids1 = new HashSet<int>();
        foreach (var f in list1) if (f != null) ids1.Add(f.id);

        var ids2 = new HashSet<int>();
        foreach (var f in list2) if (f != null) ids2.Add(f.id);

        return ids1.SetEquals(ids2);
    }

    /// <summary>
    /// 清空所有鱼
    /// </summary>
    public void ClearFish()
    {
        LogInfo("清空鱼缸");
        ClearAllFish();
        _currentDisplayingFish.Clear();
    }

    /// <summary>
    /// 隐藏鱼（不销毁，只隐藏）
    /// </summary>
    public void HideFish()
    {
        if (fishTankGo != null)
            fishTankGo.SetActive(false);
    }

    // ============================================================
    // 协程创建鱼
    // ============================================================

    private IEnumerator CreateFishCoroutine(List<FishDetailData> fishList)
    {
        List<FishTankFishCtrl> createdFish = new List<FishTankFishCtrl>();

        foreach (var fishDetail in fishList)
        {
            if (fishDetail == null) continue;

            var fishData = LoadDataManager.Instance?.GetFishById(fishDetail.fishId);
            if (fishData == null)
            {
                LogDebug($"跳过鱼: 找不到鱼类数据 ID={fishDetail.fishId}");
                continue;
            }

            int speciesId = fishData.fishSpeciesId;
            FishSpeciesType speciesType = GetFishSpeciesTypeById(speciesId);

            yield return LoadFishTextureCoroutine(fishDetail, speciesType, createdFish);
        }

        // 更新所有鱼的位置参数
        UpdateRects();
        LogInfo($"鱼创建完成，共 {TotalFishCount} 条鱼");
    }

    private IEnumerator LoadFishTextureCoroutine(
        FishDetailData fishDetail,
        FishSpeciesType speciesType,
        List<FishTankFishCtrl> createdFish)
    {
        var itemData = LoadDataManager.Instance?.GetItemById(fishDetail.fishId);
        if (itemData == null)
        {
            LogDebug($"跳过鱼: 找不到物品数据 ID={fishDetail.fishId}");
            yield break;
        }

        string iconPath = itemData.iconPath;
        if (string.IsNullOrEmpty(iconPath))
        {
            LogDebug($"跳过鱼: 图标路径为空 ID={fishDetail.fishId}");
            yield break;
        }

        string loadPath = fishDetail.isShiny ? iconPath + "_s" : iconPath;
        LogDebug($"加载鱼贴图: {loadPath}, 类型: {speciesType}");

        bool isLoaded = false;
        Texture2D loadedTexture = null;

        AssetManager.LoadFromAddressables<Texture2D>(loadPath, (texture, handle) =>
        {
            if (texture != null)
            {
                loadedTexture = texture;
                LogDebug($"鱼贴图加载成功: {loadPath}");
            }
            else
            {
                // 降级加载普通纹理
                LogDebug($"鱼贴图加载失败: {loadPath}, 尝试回退");
                AssetManager.LoadFromAddressables<Texture2D>(iconPath, (fallbackTexture, fallbackHandle) =>
                {
                    if (fallbackTexture != null)
                    {
                        loadedTexture = fallbackTexture;
                        LogDebug($"回退鱼贴图加载成功: {iconPath}");
                    }
                    else
                    {
                        LogDebug($"回退鱼贴图也加载失败: {iconPath}");
                    }
                    isLoaded = true;
                });
                return;
            }
            isLoaded = true;
        });

        while (!isLoaded)
        {
            yield return null;
        }

        if (loadedTexture != null)
        {
            FishTankFishCtrl fish = CreateFishFromTexture(loadedTexture, speciesType);
            if (fish != null)
            {
                createdFish.Add(fish);
            }
        }
    }

    // ============================================================
    // 鱼创建和类型判断
    // ============================================================

    private FishSpeciesType GetFishSpeciesTypeById(int speciesId)
    {
        if (LoadDataManager.Instance != null)
        {
            var speciesData = LoadDataManager.Instance.GetFishSpeciesById(speciesId);
            if (speciesData != null)
            {
                return LoadDataManager.Instance.GetFishSpeciesType(speciesData.type);
            }
        }
        return FishSpeciesType.FullScreenSwim;
    }

    private FishTankFishCtrl CreateFishFromTexture(Texture2D texture, FishSpeciesType speciesType)
    {
        if (fishPrefab == null)
        {
            Z_Logger.LogError("[FishTankManager] fishPrefab 为空");
            return null;
        }

        GameObject go = Instantiate(fishPrefab, fishContainer.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        FishTankFishCtrl fish = go.GetComponent<FishTankFishCtrl>();
        if (fish == null) fish = go.AddComponent<FishTankFishCtrl>();

        FishSpeciesData data = new FishSpeciesData
        {
            id = (int)speciesType,
            name = speciesType.ToString(),
            type = speciesType.ToString()
        };

        float acceleration = UnityEngine.Random.Range(accelerationMin, accelerationMax);

        fish.Init(data, fishShader);
        fish.SetBaseHeight(baseHeight);
        fish.UniformScale = uniformScale;
        fish.SetRenderQueue(renderQueue);
        fish.EnableDebugLog = enableDebugLog;
        fish.SetPhysicsParams(
            moveSpeedMin, moveSpeedMax,
            verticalSpeedRatio, verticalMoveProbability,
            acceleration, dragForce,
            chargeDurationMin, chargeDurationMax,
            chargeScaleX, chargeScaleY,
            chargeSpeedRatio,
            sprintDurationMin, sprintDurationMax
        );
        fish.SetTexture(texture);
        SetFishParams(fish);
        SetBehavior(fish);
        AddToList(fish, speciesType);

        return fish;
    }

    private void SetBehavior(FishTankFishCtrl fish)
    {
        if (fish == null) return;

        switch (fish.SpeciesType)
        {
            case FishSpeciesType.FullScreenSwim:
                Vector2 swimPos = GetRandomPosInRectWithRange(_totalRect, fullScreenSwimSpawnRange);
                fish.SetFullScreenSwim(
                    moveSpeedMin, moveSpeedMax,
                    directionChangeIntervalMin, directionChangeIntervalMax,
                    swimPos
                );
                break;
            case FishSpeciesType.FullScreenStatic:
                fish.SetFullScreenStatic();
                break;
            case FishSpeciesType.BottomSwim:
                fish.SetBottomSwim(
                    moveSpeedMin * 0.5f, moveSpeedMax * 0.6f,
                    directionChangeIntervalMin, directionChangeIntervalMax
                );
                break;
            case FishSpeciesType.BottomStatic:
                fish.SetBottomStatic();
                break;
        }
    }

    private Vector2 GetRandomPosInRectWithRange(Rect rect, float range)
    {
        float margin = 0.3f;
        float x = UnityEngine.Random.Range(rect.xMin + margin, rect.xMax - margin);
        float yMax = rect.yMax - margin;
        float yMin = rect.yMax - (rect.yMax - rect.yMin) * range + margin;
        if (yMin > yMax) yMin = yMax;
        float y = UnityEngine.Random.Range(yMin, yMax);
        return new Vector2(x, y);
    }

    private void AddToList(FishTankFishCtrl fish, FishSpeciesType type)
    {
        switch (type)
        {
            case FishSpeciesType.FullScreenSwim: fullScreenSwimList.Add(fish); break;
            case FishSpeciesType.FullScreenStatic: fullScreenStaticList.Add(fish); break;
            case FishSpeciesType.BottomSwim: bottomSwimList.Add(fish); break;
            case FishSpeciesType.BottomStatic: bottomStaticList.Add(fish); break;
        }
    }

    private void ClearAllFish()
    {
        foreach (var f in fullScreenSwimList) if (f) Destroy(f.gameObject);
        foreach (var f in fullScreenStaticList) if (f) Destroy(f.gameObject);
        foreach (var f in bottomSwimList) if (f) Destroy(f.gameObject);
        foreach (var f in bottomStaticList) if (f) Destroy(f.gameObject);

        fullScreenSwimList.Clear();
        fullScreenStaticList.Clear();
        bottomSwimList.Clear();
        bottomStaticList.Clear();
    }

    private void LogFishCount()
    {
        int total = TotalFishCount;
        if (enableDebugLog) Z_Logger.Log($"[FishTankManager] 鱼数量: {total} | " +
                  $"全屏游动: {fullScreenSwimList.Count}, " +
                  $"全屏静止: {fullScreenStaticList.Count}, " +
                  $"底部游动: {bottomSwimList.Count}, " +
                  $"底部静止: {bottomStaticList.Count}");
    }

    // ============================================================
    // 更新循环
    // ============================================================

    private IEnumerator UpdateLoop()
    {
        int frameCount = 0;
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!gameObject.activeSelf) continue;

            frameCount++;

            UpdateBaits();

            foreach (var f in fullScreenSwimList) f?.UpdateFullScreenSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var f in fullScreenStaticList) f?.UpdateFullScreenStatic();
            foreach (var f in bottomSwimList) f?.UpdateBottomSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var f in bottomStaticList) f?.UpdateBottomStatic();

            if (enableDebugLog && frameCount % 60 == 0 && fullScreenSwimList.Count > 0)
            {
                var fish = fullScreenSwimList[0];
                if (fish != null && fish.gameObject.activeSelf)
                {
                    Z_Logger.Log($"[FishTankManager] 鱼位置: ({fish.transform.position.x:F1}, {fish.transform.position.y:F1})");
                }
            }
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));

            if (worldPos.x >= _totalRect.xMin && worldPos.x <= _totalRect.xMax &&
                worldPos.y >= _totalRect.yMin && worldPos.y <= _totalRect.yMax)
            {
                SpawnBaitAtPosition(worldPos);
            }
        }
    }

    // ============================================================
    // 鱼饵系统
    // ============================================================

    public void SpawnBaitAtPosition(Vector3 position)
    {
        if (fishTankBaitPrefab == null) return;

        if (_activeBaits.Count >= fishTankBaitMaxQueueSize)
        {
            GameObject oldestBait = _activeBaits.Dequeue();
            if (oldestBait != null)
            {
                ResetFishChasingBait(oldestBait);
                ReturnBaitToPool(oldestBait);
            }
        }

        float margin = 0.5f;
        Vector3 spawnPos = position;
        spawnPos.x = Mathf.Clamp(spawnPos.x, _totalRect.xMin + margin, _totalRect.xMax - margin);
        spawnPos.y = Mathf.Clamp(spawnPos.y, _totalRect.yMin + margin, _totalRect.yMax - margin);
        spawnPos.z = 0;

        GameObject bait = GetBaitFromPool(spawnPos);
        if (bait == null) return;

        _activeBaits.Enqueue(bait);
        CheckNearbyFish(bait);
    }

    private void ResetFishChasingBait(GameObject bait)
    {
        if (bait == null) return;

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;
            if (fish.IsChasingBait)
            {
                float distance = Vector3.Distance(fish.transform.position, bait.transform.position);
                if (distance < 0.5f) continue;
            }
        }
    }

    private void CheckNearbyFish(GameObject bait)
    {
        if (bait == null) return;

        Vector3 baitPos = bait.transform.position;
        float radius = fishTankBaitTriggerRadius;

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;
            if (fish.IsChasingBait) continue;

            float distance = Vector3.Distance(fish.transform.position, baitPos);
            if (distance <= radius)
            {
                float chaseDuration = UnityEngine.Random.Range(fishTankBaitChaseDurationMin, fishTankBaitChaseDurationMax);
                fish.StartChasingBait(baitPos, chaseDuration, fishTankBaitChaseSpeedMultiplier);
                LogInfo($"鱼 {fish.UniqueId} 开始追逐鱼饵! 距离: {distance:F2}");
            }
        }
    }

    private void UpdateBaits()
    {
        List<GameObject> baitsList = new List<GameObject>(_activeBaits);

        foreach (var bait in baitsList)
        {
            if (bait == null) continue;

            FishTankBaitCtrl baitComp = bait.GetComponent<FishTankBaitCtrl>();
            if (baitComp != null)
            {
                baitComp.UpdateBait();
                CheckNearbyFish(bait);
                CheckBaitConsumption(bait);
            }
        }
    }

    private void CheckBaitConsumption(GameObject bait)
    {
        if (bait == null) return;
        if (!_activeBaits.Contains(bait)) return;

        Vector3 baitPos = bait.transform.position;

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;
            if (!fish.IsChasingBait) continue;

            float distance = Vector3.Distance(fish.transform.position, baitPos);
            if (distance < 0.3f)
            {
                LogInfo($"鱼饵已被鱼 {fish.UniqueId} 吃掉!");
                RemoveBait(bait);
                fish.ResetFishState();
                break;
            }
        }
    }

    public void RemoveBait(GameObject bait)
    {
        if (bait == null) return;

        Queue<GameObject> newQueue = new Queue<GameObject>();
        bool found = false;

        while (_activeBaits.Count > 0)
        {
            GameObject current = _activeBaits.Dequeue();
            if (current == bait && !found)
            {
                found = true;
                ResetFishChasingBait(bait);
                ReturnBaitToPool(bait);
            }
            else
            {
                newQueue.Enqueue(current);
            }
        }

        _activeBaits = newQueue;
    }

    public void ClearAllBaits()
    {
        while (_activeBaits.Count > 0)
        {
            GameObject bait = _activeBaits.Dequeue();
            if (bait != null)
            {
                ResetFishChasingBait(bait);
                ReturnBaitToPool(bait);
            }
        }
    }

    // ============================================================
    // 公共方法
    // ============================================================

    /// <summary>
    /// 打开鱼缸显示
    /// </summary>
    public void OpenFishTank()
    {
        LogInfo("OpenFishTank 开始");

        fishTankGo.SetActive(true);
        ClearAllBaits();

        if (!_isInitialized)
        {
            Init();
        }
        else
        {
            UpdateRects();
            // 如果有当前数据，重新显示
            if (_currentDisplayingFish.Count > 0)
            {
                ShowFish(_currentDisplayingFish);
            }
        }

        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
            LogInfo("更新协程已重新启动");
        }

        LogInfo("OpenFishTank 完成");
    }

    /// <summary>
    /// 关闭鱼缸显示
    /// </summary>
    public void CloseFishTank()
    {
        LogInfo("CloseFishTank 开始");

        if (fishTankGo != null)
            fishTankGo.SetActive(false);

        ClearAllBaits();

        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
            LogInfo("更新协程已停止");
        }

        _isInitialized = false;
        LogInfo("CloseFishTank 完成");
    }

    /// <summary>
    /// 重置鱼缸 - 销毁所有鱼
    /// </summary>
    public void ResetFishTank()
    {
        LogInfo("ResetFishTank 开始");
        CloseFishTank();
        ClearAllFish();
        _currentDisplayingFish.Clear();
        LogInfo("ResetFishTank 完成");
    }

    /// <summary>
    /// 兼容旧接口 - 设置鱼数据（内部调用ShowFish）
    /// </summary>
    public void SetFishData(List<FishDetailData> fishList)
    {
        if (fishList == null || fishList.Count == 0)
        {
            ClearFish();
            return;
        }
        ShowFish(fishList);
    }

    public void SetUniformScale(float scale)
    {
        uniformScale = scale;
        foreach (var f in fullScreenSwimList) if (f) f.UniformScale = scale;
        foreach (var f in fullScreenStaticList) if (f) f.UniformScale = scale;
        foreach (var f in bottomSwimList) if (f) f.UniformScale = scale;
        foreach (var f in bottomStaticList) if (f) f.UniformScale = scale;
    }

    public void SetRenderQueue(int queue)
    {
        renderQueue = queue;
        foreach (var f in fullScreenSwimList) if (f) f.SetRenderQueue(queue);
        foreach (var f in fullScreenStaticList) if (f) f.SetRenderQueue(queue);
        foreach (var f in bottomSwimList) if (f) f.SetRenderQueue(queue);
        foreach (var f in bottomStaticList) if (f) f.SetRenderQueue(queue);
    }

    // ============================================================
    // 生命周期
    // ============================================================

    private void Start() { }

    private void OnEnable()
    {
        if (_isInitialized) UpdateRects();
    }

    private void OnDestroy()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }

        while (_baitPool.Count > 0)
        {
            GameObject bait = _baitPool.Dequeue();
            if (bait != null) Destroy(bait);
        }

        ClearAllBaits();
        ClearAllFish();
    }
}
