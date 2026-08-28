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

    // 物理参数
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

    // 鱼饵系统
    [Header("===== 鱼饵系统 =====")]
    [SerializeField] private GameObject fishTankBaitPrefab;
    [SerializeField] private Transform fishTankBaitContainer;
    [SerializeField] private float fishTankBaitTriggerRadius = 1.5f;
    [SerializeField] private float fishTankBaitFallSpeed = 0.5f;
    [SerializeField] private float fishTankBaitChaseDurationMin = 0.5f;
    [SerializeField] private float fishTankBaitChaseDurationMax = 0.8f;
    [SerializeField] private float fishTankBaitChaseSpeedMultiplier = 5f;
    [SerializeField] private float fishTankBaitScale = 0.5f;
    [SerializeField] private int fishTankBaitPoolInitSize = 5;

    // 对象池配置
    [Header("===== 对象池 =====")]
    [SerializeField] private int fishPoolInitialCapacity = 10;

    // 核心状态
    private bool _isInitialized;
    private Coroutine _updateCoroutine;
    private Rect _totalRect;
    private Rect _bottomRect;

    // 当前显示的鱼列表（用于比较）
    private List<FishDetailData> _currentDisplayingFish = new List<FishDetailData>();

    // ----- 防抖队列（Update 驱动）-----
    private List<FishDetailData> _pendingData = null;          // 待处理的最新数据
    private Coroutine _createCoroutine = null;                // 当前正在执行的创建协程

    // ----- 对象池 -----
    private FishObjectPool _fishPool;
    private BaitObjectPool _baitPool;

    // ============================================================
    // 属性
    // ============================================================

    public int TotalFishCount => fullScreenSwimList.Count + fullScreenStaticList.Count +
                                 bottomSwimList.Count + bottomStaticList.Count;
    public bool EnableDebugLog => enableDebugLog;

    // ============================================================
    // 日志辅助
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

        // 初始化对象池
        _fishPool = new FishObjectPool(fishPrefab, fishContainer.transform, fishPoolInitialCapacity, this);
        _baitPool = new BaitObjectPool(fishTankBaitPrefab, fishTankBaitContainer, fishTankBaitPoolInitSize, this, _totalRect, fishTankBaitFallSpeed, fishTankBaitScale);

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
    // 核心显示接口（外部调用）
    // ============================================================

    /// <summary>
    /// 显示鱼 - 由View调用，将数据放入防抖队列
    /// </summary>
    public void ShowFish(List<FishDetailData> fishList)
    {
        LogInfo($"ShowFish: 接收 {fishList?.Count ?? 0} 条鱼");

        if (fishList == null || fishList.Count == 0)
        {
            ClearFish();
            return;
        }

        // 保存最新数据（只保留最新的，实现防抖）
        _pendingData = new List<FishDetailData>(fishList);
        LogDebug($"待处理数据已更新，数量: {_pendingData.Count}");
    }

    /// <summary>
    /// 清空所有鱼（回收至池）
    /// </summary>
    public void ClearFish()
    {
        LogInfo("清空鱼缸");
        ClearAllFish();
        _currentDisplayingFish.Clear();
        _pendingData = null;
        if (_createCoroutine != null)
        {
            StopCoroutine(_createCoroutine);
            _createCoroutine = null;
        }
    }

    /// <summary>
    /// 隐藏鱼缸
    /// </summary>
    public void HideFish()
    {
        if (fishTankGo != null)
            fishTankGo.SetActive(false);
    }

    // ============================================================
    // Update 驱动防抖处理
    // ============================================================

    private void Update()
    {
        // 检查是否有待处理数据，且当前没有正在执行的创建协程
        if (_pendingData != null && _createCoroutine == null)
        {
            List<FishDetailData> fishList = _pendingData;
            _pendingData = null; // 清空，防止重复处理

            // 比较是否与当前显示相同
            if (IsSameFishList(fishList, _currentDisplayingFish))
            {
                LogDebug("数据与当前显示相同，跳过重建");
                return;
            }

            LogDebug($"数据变化，启动重建协程，数量: {fishList.Count}");
            _createCoroutine = StartCoroutine(RebuildFish(fishList));
        }
    }

    // ============================================================
    // 重建逻辑（协程）
    // ============================================================

    private IEnumerator RebuildFish(List<FishDetailData> fishList)
    {
        // 更新当前显示列表
        _currentDisplayingFish = new List<FishDetailData>(fishList);

        // 清空旧鱼
        ClearAllFish();

        // 创建新鱼
        yield return StartCoroutine(CreateFishCoroutine(fishList));

        // 协程结束，清空标记
        _createCoroutine = null;
        LogInfo($"重建完成，当前鱼总数: {TotalFishCount}");
    }

    // ============================================================
    // 鱼列表比较
    // ============================================================

    private bool IsSameFishList(List<FishDetailData> list1, List<FishDetailData> list2)
    {
        if (list1 == null && list2 == null) return true;
        if (list1 == null || list2 == null) return false;
        if (list1.Count != list2.Count) return false;

        var ids1 = new HashSet<int>();
        foreach (var f in list1) if (f != null) ids1.Add(f.id);

        var ids2 = new HashSet<int>();
        foreach (var f in list2) if (f != null) ids2.Add(f.id);

        return ids1.SetEquals(ids2);
    }

    // ============================================================
    // 鱼创建（协程）
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
        LogDebug($"加载鱼贴图(Sprite): {loadPath}, 类型: {speciesType}");

        bool isLoaded = false;
        Sprite loadedSprite = null;

        AssetManager.LoadFromAddressables<Sprite>(loadPath, (sprite, handle) =>
        {
            if (sprite != null)
            {
                loadedSprite = sprite;
                LogDebug($"鱼 Sprite 加载成功: {loadPath}");
            }
            else
            {
                LogDebug($"鱼 Sprite 加载失败: {loadPath}, 尝试回退");
                AssetManager.LoadFromAddressables<Sprite>(iconPath, (fallbackSprite, fallbackHandle) =>
                {
                    if (fallbackSprite != null)
                    {
                        loadedSprite = fallbackSprite;
                        LogDebug($"回退鱼 Sprite 加载成功: {iconPath}");
                    }
                    else
                    {
                        LogDebug($"回退鱼 Sprite 也加载失败: {iconPath}");
                    }
                    isLoaded = true;
                });
                return;
            }
            isLoaded = true;
        });

        float timeout = 3f;
        float timer = 0f;
        while (!isLoaded && timer < timeout)
        {
            yield return null;
            timer += Time.deltaTime;
        }

        if (!isLoaded)
        {
            LogDebug($"加载鱼 Sprite 超时: {loadPath}，跳过此鱼");
            yield break;
        }

        if (loadedSprite != null)
        {
            Texture2D texture = loadedSprite.texture;
            if (texture != null)
            {
                FishTankFishCtrl fish = CreateFishFromTexture(texture, speciesType);
                if (fish != null)
                {
                    createdFish.Add(fish);
                }
            }
            else
            {
                LogDebug($"无法从 Sprite 获取纹理，跳过鱼: {fishDetail.fishId}");
            }
        }
        else
        {
            LogDebug($"鱼 Sprite 最终为空，跳过鱼: {fishDetail.fishId}");
        }
    }

    // ============================================================
    // 鱼创建辅助
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
        FishTankFishCtrl fish = _fishPool.Get();
        if (fish == null)
        {
            Z_Logger.LogError("[FishTankManager] 无法从对象池获取鱼");
            return null;
        }

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
        foreach (var fish in fullScreenSwimList) if (fish) _fishPool.Return(fish);
        foreach (var fish in fullScreenStaticList) if (fish) _fishPool.Return(fish);
        foreach (var fish in bottomSwimList) if (fish) _fishPool.Return(fish);
        foreach (var fish in bottomStaticList) if (fish) _fishPool.Return(fish);

        fullScreenSwimList.Clear();
        fullScreenStaticList.Clear();
        bottomSwimList.Clear();
        bottomStaticList.Clear();
    }

    // ============================================================
    // 更新循环（负责鱼动画和鱼饵更新）
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

    // ============================================================
    // 鱼饵系统
    // ============================================================

    public void SpawnBaitAtPosition(Vector3 position)
    {
        if (fishTankBaitPrefab == null) return;

        float margin = 0.5f;
        Vector3 spawnPos = position;
        spawnPos.x = Mathf.Clamp(spawnPos.x, _totalRect.xMin + margin, _totalRect.xMax - margin);
        spawnPos.y = Mathf.Clamp(spawnPos.y, _totalRect.yMin + margin, _totalRect.yMax - margin);
        spawnPos.z = 0;

        GameObject bait = _baitPool.Get(spawnPos);
        if (bait == null) return;

        CheckNearbyFish(bait);
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
        _baitPool.UpdateAllBaits(_totalRect, CheckNearbyFish, CheckBaitConsumption);
    }

    private void CheckBaitConsumption(GameObject bait)
    {
        if (bait == null) return;

        Vector3 baitPos = bait.transform.position;

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;
            if (!fish.IsChasingBait) continue;

            float distance = Vector3.Distance(fish.transform.position, baitPos);
            if (distance < 0.3f)
            {
                LogInfo($"鱼饵已被鱼 {fish.UniqueId} 吃掉!");
                _baitPool.RemoveBait(bait);
                fish.ResetFishState();
                break;
            }
        }
    }

    public void ClearAllBaits()
    {
        _baitPool?.ClearAll();
    }

    // ============================================================
    // 公共方法（打开/关闭/重置）
    // ============================================================

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
        }

        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
            LogInfo("更新协程已重新启动");
        }

        LogInfo("OpenFishTank 完成");
    }

    public void CloseFishTank()
    {
        LogInfo("CloseFishTank 开始");

        if (fishTankGo != null)
            fishTankGo.SetActive(false);

        ClearAllBaits();
        _pendingData = null;
        if (_createCoroutine != null)
        {
            StopCoroutine(_createCoroutine);
            _createCoroutine = null;
        }

        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
            LogInfo("更新协程已停止");
        }

        _isInitialized = false;
        LogInfo("CloseFishTank 完成");
    }

    public void ResetFishTank()
    {
        LogInfo("ResetFishTank 开始");
        CloseFishTank();
        ClearAllFish();
        _currentDisplayingFish.Clear();
        _pendingData = null;
        _fishPool?.Clear();
        _baitPool?.Clear();
        LogInfo("ResetFishTank 完成");
    }

    /// <summary>
    /// 兼容旧接口
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

        _fishPool?.Clear();
        _baitPool?.Clear();
        ClearAllFish();
    }

    // ============================================================
    // 内部对象池类 - 鱼对象池
    // ============================================================

    private class FishObjectPool
    {
        private GameObject _prefab;
        private Transform _parent;
        private FishTankManager _manager;
        private Queue<FishTankFishCtrl> _pool = new Queue<FishTankFishCtrl>();
        private List<FishTankFishCtrl> _allObjects = new List<FishTankFishCtrl>();

        public FishObjectPool(GameObject prefab, Transform parent, int initialCapacity, FishTankManager manager)
        {
            _prefab = prefab;
            _parent = parent;
            _manager = manager;
            for (int i = 0; i < initialCapacity; i++)
            {
                CreateNewObject();
            }
        }

        private FishTankFishCtrl CreateNewObject()
        {
            GameObject go = GameObject.Instantiate(_prefab, _parent);
            go.SetActive(false);
            var fish = go.GetComponent<FishTankFishCtrl>();
            if (fish == null)
            {
                Debug.LogError("FishObjectPool: 预制体缺少 FishTankFishCtrl 组件");
                return null;
            }
            _allObjects.Add(fish);
            _pool.Enqueue(fish);
            return fish;
        }

        public FishTankFishCtrl Get()
        {
            FishTankFishCtrl fish;
            if (_pool.Count > 0)
            {
                fish = _pool.Dequeue();
            }
            else
            {
                fish = CreateNewObject();
                fish = _pool.Dequeue();
            }
            fish.gameObject.SetActive(true);
            return fish;
        }

        public void Return(FishTankFishCtrl fish)
        {
            if (fish == null) return;
            fish.gameObject.SetActive(false);
            if (!_pool.Contains(fish) && _allObjects.Contains(fish))
            {
                _pool.Enqueue(fish);
            }
        }

        public void Clear()
        {
            foreach (var fish in _allObjects)
            {
                if (fish != null && fish.gameObject != null)
                    GameObject.Destroy(fish.gameObject);
            }
            _pool.Clear();
            _allObjects.Clear();
        }
    }

    // ============================================================
    // 内部对象池类 - 鱼饵对象池
    // ============================================================

    private class BaitObjectPool
    {
        private GameObject _prefab;
        private Transform _parent;
        private FishTankManager _manager;
        private Rect _totalRect;
        private float _fallSpeed;
        private float _scale;
        private Queue<GameObject> _pool = new Queue<GameObject>();
        private List<GameObject> _allObjects = new List<GameObject>();
        private Queue<GameObject> _activeBaits = new Queue<GameObject>();

        public BaitObjectPool(GameObject prefab, Transform parent, int initialCapacity, FishTankManager manager, Rect totalRect, float fallSpeed, float scale)
        {
            _prefab = prefab;
            _parent = parent;
            _manager = manager;
            _totalRect = totalRect;
            _fallSpeed = fallSpeed;
            _scale = scale;
            for (int i = 0; i < initialCapacity; i++)
            {
                CreateNewBait();
            }
        }

        private GameObject CreateNewBait()
        {
            if (_prefab == null) return null;
            GameObject go = GameObject.Instantiate(_prefab, _parent);
            go.SetActive(false);
            go.transform.localScale = Vector3.one * _scale;

            FishTankBaitCtrl baitComp = go.GetComponent<FishTankBaitCtrl>();
            if (baitComp == null) baitComp = go.AddComponent<FishTankBaitCtrl>();
            baitComp.Init(_manager, _totalRect, _fallSpeed, _scale);

            _allObjects.Add(go);
            _pool.Enqueue(go);
            return go;
        }

        public GameObject Get(Vector3 position)
        {
            GameObject bait;
            if (_pool.Count > 0)
            {
                bait = _pool.Dequeue();
            }
            else
            {
                bait = CreateNewBait();
                bait = _pool.Dequeue();
            }
            bait.SetActive(true);
            bait.transform.position = position;
            FishTankBaitCtrl comp = bait.GetComponent<FishTankBaitCtrl>();
            if (comp != null) comp.ResetBait(position);
            _activeBaits.Enqueue(bait);
            return bait;
        }

        public void Return(GameObject bait)
        {
            if (bait == null) return;
            FishTankBaitCtrl comp = bait.GetComponent<FishTankBaitCtrl>();
            if (comp != null) comp.Deactivate();
            bait.SetActive(false);
            if (!_pool.Contains(bait) && _allObjects.Contains(bait))
            {
                _pool.Enqueue(bait);
            }
        }

        public void RemoveBait(GameObject bait)
        {
            Queue<GameObject> newQueue = new Queue<GameObject>();
            while (_activeBaits.Count > 0)
            {
                GameObject current = _activeBaits.Dequeue();
                if (current == bait)
                {
                    Return(bait);
                }
                else
                {
                    newQueue.Enqueue(current);
                }
            }
            _activeBaits = newQueue;
        }

        public void ClearAll()
        {
            while (_activeBaits.Count > 0)
            {
                GameObject bait = _activeBaits.Dequeue();
                if (bait != null) Return(bait);
            }
        }

        public void Clear()
        {
            ClearAll();
            foreach (var bait in _allObjects)
            {
                if (bait != null) GameObject.Destroy(bait);
            }
            _pool.Clear();
            _allObjects.Clear();
        }

        public void UpdateAllBaits(Rect totalRect, Action<GameObject> checkNearby, Action<GameObject> checkConsumption)
        {
            List<GameObject> baitsList = new List<GameObject>(_activeBaits);
            foreach (var bait in baitsList)
            { 
                if (bait == null) continue;
                FishTankBaitCtrl comp = bait.GetComponent<FishTankBaitCtrl>();
                if (comp != null)
                {
                    comp.UpdateBait();
                    checkNearby?.Invoke(bait);
                    checkConsumption?.Invoke(bait);
                }
            }
        }
    }
}
