using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FishTankFishSpeciesType
{
    FullScreenSwim,      // 全屏游动类型
    FullScreenStatic,    // 全屏静止类型
    BottomSwim,          // 底部游动类型
    BottomStatic         // 底部静止类型
}

[System.Serializable]
public class FishTankFishSpeciesData
{
    public int id;          // 鱼种ID
    public string name;     // 鱼种名称
    public string type;     // 鱼种类型字符串
}

public class FishTankManager : MonoBehaviour
{
    [Header("鱼缸区域(绑定Quad)")]
    [SerializeField] private Transform totalArea;   // 全屏区域（鱼的活动范围）
    [SerializeField] private Transform bottomArea;  // 底部区域

    [Header("鱼容器")]
    [SerializeField] private GameObject fishContainer;  // 鱼的父容器

    [Header("四个行为类型列表")]
    [SerializeField] private List<FishTankFishCtrl> fullScreenSwimList = new List<FishTankFishCtrl>();    // 全屏游动鱼列表
    [SerializeField] private List<FishTankFishCtrl> fullScreenStaticList = new List<FishTankFishCtrl>();  // 全屏静止鱼列表
    [SerializeField] private List<FishTankFishCtrl> bottomSwimList = new List<FishTankFishCtrl>();        // 底部游动鱼列表
    [SerializeField] private List<FishTankFishCtrl> bottomStaticList = new List<FishTankFishCtrl>();      // 底部静止鱼列表

    [Header("临时图片列表")]
    [SerializeField] private List<Texture2D> tempFullScreenSwimTextures = new List<Texture2D>();    // 全屏游动鱼贴图
    [SerializeField] private List<Texture2D> tempFullScreenStaticTextures = new List<Texture2D>();  // 全屏静止鱼贴图
    [SerializeField] private List<Texture2D> tempBottomSwimTextures = new List<Texture2D>();        // 底部游动鱼贴图
    [SerializeField] private List<Texture2D> tempBottomStaticTextures = new List<Texture2D>();      // 底部静止鱼贴图

    [Header("鱼预制体")]
    [SerializeField] private GameObject fishPrefab;  // 鱼预制体

    [Header("Shader(用于创建独立材质)")]
    [SerializeField] private Shader fishShader;      // 鱼使用的Shader

    [Header("===== 大小参数 =====")]
    [SerializeField] private float baseHeight = 0.5f;          // 鱼的基础高度
    [SerializeField] private float uniformScale = 1f;          // 鱼的统一缩放比例

    [Header("===== 方向变化间隔(秒) =====")]
    [SerializeField] private float directionChangeIntervalMin = 4f;   // 方向变化最短间隔
    [SerializeField] private float directionChangeIntervalMax = 20f;  // 方向变化最长间隔

    [Header("===== 全屏移动鱼类生成范围 =====")]
    [Range(0.1f, 1f)]
    [SerializeField] private float fullScreenSwimSpawnRange = 0.7f;   // 全屏游动鱼生成范围（0-1，0.7表示在顶部70%区域）

    [Header("===== 渲染队列 =====")]
    [SerializeField] private int renderQueue = 3110;      // 渲染队列值，控制渲染顺序

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false; // 是否启用调试日志

    // ============================================================
    // 鱼游动物理参数
    // ============================================================

    [Header("===== 水平移动(左右) =====")]
    [SerializeField] private float moveSpeedMin = 0.35f;   // 水平移动最小速度（单位/秒）
    [SerializeField] private float moveSpeedMax = 1.2f;    // 水平移动最大速度（单位/秒）

    [Header("===== 垂直移动(上下) =====")]
    [SerializeField] private float verticalSpeedRatio = 0.4f;           // 垂直速度占水平速度的比例（0.3~0.6）
    [Range(0f, 1f)]
    [SerializeField] private float verticalMoveProbability = 0.2f;      // 转向时触发垂直移动的概率

    [Header("===== 物理参数 =====")]
    [SerializeField] private float accelerationMin = 2.5f;   // 加速度最小值，值越大加速越快
    [SerializeField] private float accelerationMax = 5.0f;   // 加速度最大值，值越大加速越快
    [SerializeField] private float dragForce = 0.8f;         // 阻力强度，值越大减速越快（建议0.3~3）

    [Header("===== 蓄力参数 =====")]
    [SerializeField] private float chargeDurationMin = 0.4f;   // 蓄力最短时间（秒）
    [SerializeField] private float chargeDurationMax = 1f;     // 蓄力最长时间（秒）
    [SerializeField] private float chargeScaleX = 0.6f;        // 蓄力时X轴缩放比例（0.3~0.6），值越小压得越扁
    [SerializeField] private float chargeScaleY = 1.35f;       // 蓄力时Y轴缩放比例（1.5~2.5），值越大拉得越长
    [SerializeField] private float chargeSpeedRatio = 0.15f;   // 蓄力速度为最大速度的百分比（0.1~0.3）

    [Header("===== 冲刺参数 =====")]
    [SerializeField] private float sprintDurationMin = 1.5f;   // 冲刺最短时间（秒）
    [SerializeField] private float sprintDurationMax = 3.5f;   // 冲刺最长时间（秒）

    // ============================================================
    // 鱼饵系统（对象池）
    // ============================================================

    [Header("===== 鱼饵系统 =====")]
    [SerializeField] private GameObject fishTankBaitPrefab;                    // 鱼饵预制体
    [SerializeField] private Transform fishTankBaitContainer;                  // 鱼饵容器
    [SerializeField] private float fishTankBaitTriggerRadius = 1.5f;           // 鱼饵触发范围半径（圆形检测）
    [SerializeField] private float fishTankBaitFallSpeed = 0.5f;               // 鱼饵下落速度（单位/秒）
    [SerializeField] private float fishTankBaitChaseDurationMin = 0.5f;        // 鱼追逐鱼饵的最短时间（秒）
    [SerializeField] private float fishTankBaitChaseDurationMax = 0.8f;        // 鱼追逐鱼饵的最长时间（秒）
    [SerializeField] private float fishTankBaitChaseSpeedMultiplier = 5f;      // 鱼追逐鱼饵的速度倍数（基础速度 × 该值）
    [SerializeField] private int fishTankBaitMaxQueueSize = 50;                // 鱼饵队列最大容量
    [SerializeField] private float fishTankBaitScale = 0.5f;                   // 鱼饵大小缩放
    [SerializeField] private int fishTankBaitPoolInitSize = 5;                 // 对象池初始大小（预创建数量）

    // 对象池
    private Queue<GameObject> _baitPool = new Queue<GameObject>();      // 空闲鱼饵池
    private Queue<GameObject> _activeBaits = new Queue<GameObject>();   // 活跃鱼饵队列

    private bool _isInitialized;
    private Coroutine _updateCoroutine;

    private Rect _totalRect;
    private Rect _bottomRect;

    public bool IsInitialized => _isInitialized;
    public int TotalFishCount => fullScreenSwimList.Count + fullScreenStaticList.Count +
                                 bottomSwimList.Count + bottomStaticList.Count;
    public bool EnableDebugLog => enableDebugLog;
    public int BaitCount => _activeBaits.Count;
    public int PoolCount => _baitPool.Count;

    // ============================================================
    // 日志辅助方法
    // ============================================================

    /// <summary>调试日志（受 enableDebugLog 控制）</summary>
    private void LogDebug(string message)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankManager] {message}");
    }

    /// <summary>关键日志（直接打印）</summary>
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
        CreateAllFish();

        // 预创建鱼饵对象池（初始数量 = fishTankBaitPoolInitSize）
        PreCreateBaits();

        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
        }

        LogFishCount();
        _isInitialized = true;
        LogInfo("初始化完成");
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
    // 对象池核心方法
    // ============================================================

    /// <summary>预创建鱼饵到对象池（初始创建 fishTankBaitPoolInitSize 个）</summary>
    private void PreCreateBaits()
    {
        for (int i = 0; i < fishTankBaitPoolInitSize; i++)
        {
            GameObject bait = CreateBaitInstance();
            bait.SetActive(false);
            _baitPool.Enqueue(bait);
        }
        LogDebug($"预创建 {fishTankBaitPoolInitSize} 个鱼饵到对象池（初始大小）");
    }

    /// <summary>创建一个鱼饵实例（不激活）</summary>
    private GameObject CreateBaitInstance()
    {
        if (fishTankBaitPrefab == null)
        {
            Z_Logger.LogError("[FishTankManager] fishTankBaitPrefab 为空!");
            return null;
        }

        GameObject bait = Instantiate(fishTankBaitPrefab, fishTankBaitContainer);
        bait.transform.localScale = Vector3.one * fishTankBaitScale;

        FishTankBaitComponent baitComp = bait.GetComponent<FishTankBaitComponent>();
        if (baitComp == null)
        {
            baitComp = bait.AddComponent<FishTankBaitComponent>();
        }
        baitComp.Init(this, _totalRect, fishTankBaitFallSpeed, fishTankBaitScale);

        return bait;
    }

    /// <summary>从对象池获取鱼饵（池空则动态创建）</summary>
    private GameObject GetBaitFromPool(Vector3 position)
    {
        GameObject bait = null;

        // 从池中取出
        if (_baitPool.Count > 0)
        {
            bait = _baitPool.Dequeue();
            LogDebug($"从对象池取出鱼饵，剩余空闲: {_baitPool.Count}");
        }
        else
        {
            // 池为空，动态创建新的鱼饵（按需扩展）
            LogDebug($"对象池为空，动态创建新鱼饵（当前活跃: {_activeBaits.Count}/{fishTankBaitMaxQueueSize}）");
            bait = CreateBaitInstance();
        }

        if (bait != null)
        {
            bait.SetActive(true);
            bait.transform.position = position;

            FishTankBaitComponent baitComp = bait.GetComponent<FishTankBaitComponent>();
            if (baitComp != null)
            {
                baitComp.ResetBait(position);
            }
        }

        return bait;
    }

    /// <summary>将鱼饵归还到对象池（池满则销毁）</summary>
    private void ReturnBaitToPool(GameObject bait)
    {
        if (bait == null) return;

        // 重置鱼饵状态
        FishTankBaitComponent baitComp = bait.GetComponent<FishTankBaitComponent>();
        if (baitComp != null)
        {
            baitComp.Deactivate();
        }

        bait.SetActive(false);

        // 检查池是否已满（池满则销毁，避免内存无限增长）
        if (_baitPool.Count < fishTankBaitMaxQueueSize)
        {
            _baitPool.Enqueue(bait);
            LogDebug($"鱼饵归还到池，当前池大小: {_baitPool.Count}/{fishTankBaitMaxQueueSize}");
        }
        else
        {
            // 池已满，销毁多余的鱼饵
            Destroy(bait);
            LogDebug($"对象池已满（{fishTankBaitMaxQueueSize}），销毁多余鱼饵");
        }
    }

    private void UpdateRects()
    {
        if (totalArea != null)
        {
            Vector3 pos = totalArea.position;
            Vector3 scale = totalArea.localScale;
            float width = scale.x;
            float height = scale.y;
            _totalRect = new Rect(pos.x - width / 2f, pos.y - height / 2f, width, height);

            if (enableDebugLog)
                Z_Logger.Log($"[FishTankManager] TotalArea: X:[{_totalRect.xMin:F2}, {_totalRect.xMax:F2}], Y:[{_totalRect.yMin:F2}, {_totalRect.yMax:F2}]");
        }

        if (bottomArea != null)
        {
            Vector3 pos = bottomArea.position;
            Vector3 scale = bottomArea.localScale;
            float width = scale.x;
            float height = scale.y;
            _bottomRect = new Rect(pos.x - width / 2f, pos.y - height / 2f, width, height);

            if (enableDebugLog)
                Z_Logger.Log($"[FishTankManager] BottomArea: X:[{_bottomRect.xMin:F2}, {_bottomRect.xMax:F2}], Y:[{_bottomRect.yMin:F2}, {_bottomRect.yMax:F2}]");
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

    private void CreateAllFish()
    {
        ClearAllFish();

        foreach (var tex in tempFullScreenSwimTextures)
            if (tex != null) CreateFish(tex, FishTankFishSpeciesType.FullScreenSwim);

        foreach (var tex in tempFullScreenStaticTextures)
            if (tex != null) CreateFish(tex, FishTankFishSpeciesType.FullScreenStatic);

        foreach (var tex in tempBottomSwimTextures)
            if (tex != null) CreateFish(tex, FishTankFishSpeciesType.BottomSwim);

        foreach (var tex in tempBottomStaticTextures)
            if (tex != null) CreateFish(tex, FishTankFishSpeciesType.BottomStatic);

        LogFishCount();
    }

    private void CreateFish(Texture2D texture, FishTankFishSpeciesType type)
    {
        if (fishPrefab == null)
        {
            Z_Logger.LogError("[FishTankManager] fishPrefab 为空");
            return;
        }

        GameObject go = Instantiate(fishPrefab, fishContainer.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        FishTankFishCtrl fish = go.GetComponent<FishTankFishCtrl>();
        if (fish == null) fish = go.AddComponent<FishTankFishCtrl>();

        FishTankFishSpeciesData data = new FishTankFishSpeciesData
        {
            id = (int)type,
            name = type.ToString(),
            type = type.ToString()
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

        AddToList(fish, type);
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

    private void SetBehavior(FishTankFishCtrl fish)
    {
        switch (fish.SpeciesType)
        {
            case FishTankFishSpeciesType.FullScreenSwim:
                Vector2 swimPos = GetRandomPosInRectWithRange(_totalRect, fullScreenSwimSpawnRange);
                fish.SetFullScreenSwim(
                    moveSpeedMin, moveSpeedMax,
                    directionChangeIntervalMin, directionChangeIntervalMax,
                    swimPos
                );
                break;
            case FishTankFishSpeciesType.FullScreenStatic:
                fish.SetFullScreenStatic();
                break;
            case FishTankFishSpeciesType.BottomSwim:
                fish.SetBottomSwim(
                    moveSpeedMin * 0.5f, moveSpeedMax * 0.6f,
                    directionChangeIntervalMin, directionChangeIntervalMax
                );
                break;
            case FishTankFishSpeciesType.BottomStatic:
                fish.SetBottomStatic();
                break;
        }
    }

    private void AddToList(FishTankFishCtrl fish, FishTankFishSpeciesType type)
    {
        switch (type)
        {
            case FishTankFishSpeciesType.FullScreenSwim: fullScreenSwimList.Add(fish); break;
            case FishTankFishSpeciesType.FullScreenStatic: fullScreenStaticList.Add(fish); break;
            case FishTankFishSpeciesType.BottomSwim: bottomSwimList.Add(fish); break;
            case FishTankFishSpeciesType.BottomStatic: bottomStaticList.Add(fish); break;
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
                    Z_Logger.Log($"[FishTankManager] 鱼位置: ({fish.transform.position.x:F1}, {fish.transform.position.y:F1}), " +
                              $"速度: {fish.GetCurrentMoveSpeed():F1}, 方向: {(fish.GetCurrentDirection() > 0 ? "→" : "←")}, " +
                              $"追逐状态: {fish.IsChasingBait}, 鱼饵数量: {_activeBaits.Count}");
                }
            }
        }
    }

    private void Update()
    {
        // F5刷新
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Refresh();
        }

        // 鼠标点击生成鱼饵
        // 适配微信小游戏的触摸事件
        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            Vector3 clickPosition = Vector3.zero;
            bool isValidClick = false;

            // 检测鼠标点击
            if (Input.GetMouseButtonDown(0))
            {
                clickPosition = Input.mousePosition;
                isValidClick = true;
            }

            // 检测触摸（微信小游戏适配）
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    clickPosition = touch.position;
                    isValidClick = true;
                }
            }

            if (isValidClick)
            {
                // 将屏幕坐标转换为世界坐标
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(clickPosition.x, clickPosition.y, 10f));

                // 检查点击位置是否在 totalArea 范围内
                if (worldPos.x >= _totalRect.xMin && worldPos.x <= _totalRect.xMax &&
                    worldPos.y >= _totalRect.yMin && worldPos.y <= _totalRect.yMax)
                {
                    SpawnBaitAtPosition(worldPos);
                }
                else
                {
                    if (enableDebugLog)
                        Z_Logger.Log($"[FishTankManager] 点击位置 ({worldPos.x:F2}, {worldPos.y:F2}) 不在鱼缸范围内");
                }
            }
        }
    }

    // ============================================================
    // 鱼饵生成方法（对象池）
    // ============================================================

    /// <summary>在指定位置生成鱼饵</summary>
    public void SpawnBaitAtPosition(Vector3 position)
    {
        LogDebug("=== SpawnBaitAtPosition 开始 ===");

        if (fishTankBaitPrefab == null)
        {
            Z_Logger.LogError("[FishTankManager] fishTankBaitPrefab 为空!");
            return;
        }

        LogDebug($"当前活跃: {_activeBaits.Count}/{fishTankBaitMaxQueueSize}, 池中空闲: {_baitPool.Count}");

        // 检查队列是否已满，如果满了则移除最旧的鱼饵（FIFO）
        if (_activeBaits.Count >= fishTankBaitMaxQueueSize)
        {
            GameObject oldestBait = _activeBaits.Dequeue();
            if (oldestBait != null)
            {
                LogDebug("队列已满，移除最旧鱼饵");
                ResetFishChasingBait(oldestBait);
                ReturnBaitToPool(oldestBait);
            }
        }

        // 确保鱼饵在 totalArea 范围内
        float margin = 0.5f;
        Vector3 spawnPos = position;
        spawnPos.x = Mathf.Clamp(spawnPos.x, _totalRect.xMin + margin, _totalRect.xMax - margin);
        spawnPos.y = Mathf.Clamp(spawnPos.y, _totalRect.yMin + margin, _totalRect.yMax - margin);
        spawnPos.z = 0;

        LogDebug($"鱼饵生成位置: ({spawnPos.x:F2}, {spawnPos.y:F2})");

        // 从对象池获取鱼饵（池空则动态创建）
        GameObject bait = GetBaitFromPool(spawnPos);
        if (bait == null)
        {
            Z_Logger.LogError("[FishTankManager] 从对象池获取鱼饵失败!");
            return;
        }

        _activeBaits.Enqueue(bait);

        LogDebug($"鱼饵已入队，当前活跃: {_activeBaits.Count}, 池中空闲: {_baitPool.Count}");

        // 生成时立即检测一次附近的鱼
        CheckNearbyFish(bait);

        LogDebug("=== SpawnBaitAtPosition 结束 ===");
    }

    /// <summary>在随机位置生成鱼饵（用于调试）</summary>
    public void SpawnBait()
    {
        LogDebug("=== SpawnBait 开始 ===");

        if (fishTankBaitPrefab == null)
        {
            Z_Logger.LogError("[FishTankManager] fishTankBaitPrefab 为空!");
            return;
        }

        LogDebug($"当前活跃: {_activeBaits.Count}/{fishTankBaitMaxQueueSize}, 池中空闲: {_baitPool.Count}");

        if (_activeBaits.Count >= fishTankBaitMaxQueueSize)
        {
            GameObject oldestBait = _activeBaits.Dequeue();
            if (oldestBait != null)
            {
                LogDebug("队列已满，移除最旧鱼饵");
                ResetFishChasingBait(oldestBait);
                ReturnBaitToPool(oldestBait);
            }
        }

        float margin = 0.5f;
        float x = UnityEngine.Random.Range(_totalRect.xMin + margin, _totalRect.xMax - margin);
        float y = UnityEngine.Random.Range(_totalRect.yMin + margin, _totalRect.yMax - margin);
        Vector3 spawnPos = new Vector3(x, y, 0);

        LogDebug($"鱼饵生成位置: ({x:F2}, {y:F2})");

        GameObject bait = GetBaitFromPool(spawnPos);
        if (bait == null)
        {
            Z_Logger.LogError("[FishTankManager] 从对象池获取鱼饵失败!");
            return;
        }

        _activeBaits.Enqueue(bait);

        LogDebug($"鱼饵已入队，当前活跃: {_activeBaits.Count}, 池中空闲: {_baitPool.Count}");

        CheckNearbyFish(bait);

        LogDebug("=== SpawnBait 结束 ===");
    }

    // ============================================================
    // 鱼饵系统方法
    // ============================================================

    /// <summary>重置所有追逐该鱼饵的鱼的状态</summary>
    private void ResetFishChasingBait(GameObject bait)
    {
        if (bait == null) return;

        LogDebug($"ResetFishChasingBait 被调用");

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;
            if (fish.IsChasingBait)
            {
                float distance = Vector3.Distance(fish.transform.position, bait.transform.position);
                LogDebug($"鱼 {fish.UniqueId} 正在追逐，距离鱼饵: {distance:F2}");

                if (distance < 0.5f)
                {
                    LogDebug($"鱼 {fish.UniqueId} 距离鱼饵太近({distance:F2})，跳过重置");
                    continue;
                }

                if (distance < fishTankBaitTriggerRadius)
                {
                    if (Vector3.Distance(fish.GetBaitTargetPosition(), bait.transform.position) < 0.1f)
                    {
                        LogDebug($"鱼 {fish.UniqueId} 追逐的鱼饵被移除，继续向最后位置移动");
                    }
                }
            }
        }
    }

    /// <summary>检测鱼饵附近的鱼并触发追逐</summary>
    private void CheckNearbyFish(GameObject bait)
    {
        if (enableDebugLog)
            Z_Logger.Log("[FishTankManager] === CheckNearbyFish 开始 ===");

        if (bait == null)
        {
            if (enableDebugLog)
                Z_Logger.Log("[FishTankManager] CheckNearbyFish: bait 为空");
            return;
        }

        Vector3 baitPos = bait.transform.position;
        float radius = fishTankBaitTriggerRadius;

        if (enableDebugLog)
        {
            Z_Logger.Log($"[FishTankManager] 鱼饵位置: ({baitPos.x:F2}, {baitPos.y:F2}), 触发半径: {radius}");
            Z_Logger.Log($"[FishTankManager] 全屏游动鱼数量: {fullScreenSwimList.Count}");
        }

        bool anyFishTriggered = false;
        int fishChecked = 0;
        int fishSkippedChasing = 0;

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf)
            {
                continue;
            }

            fishChecked++;

            // 已经追逐中的不触发
            if (fish.IsChasingBait)
            {
                fishSkippedChasing++;
                if (enableDebugLog)
                    Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 已在追逐中，跳过");
                continue;
            }

            float distance = Vector3.Distance(fish.transform.position, baitPos);
            if (enableDebugLog)
                Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 距离鱼饵: {distance:F2}, 状态: {fish.GetCurrentSwimState()}, 鱼状态: {fish.CurrentFishState}");

            if (distance <= radius)
            {
                if (enableDebugLog)
                    Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 在触发范围内! 距离: {distance:F2}");

                // 不管什么状态，直接触发追逐
                float chaseDuration = UnityEngine.Random.Range(fishTankBaitChaseDurationMin, fishTankBaitChaseDurationMax);
                if (enableDebugLog)
                    Z_Logger.Log($"[FishTankManager] 触发鱼 {fish.UniqueId} 追逐，持续时间: {chaseDuration:F2}s");
                fish.StartChasingBait(baitPos, chaseDuration, fishTankBaitChaseSpeedMultiplier);
                anyFishTriggered = true;

                // 关键日志：鱼开始追逐
                LogInfo($"鱼 {fish.UniqueId} 开始追逐鱼饵! 距离: {distance:F2}");
            }
        }

        if (enableDebugLog)
        {
            Z_Logger.Log($"[FishTankManager] CheckNearbyFish 统计: 检查 {fishChecked} 条鱼, 跳过追逐中 {fishSkippedChasing} 条, 触发 {anyFishTriggered} 条");

            if (!anyFishTriggered)
            {
                Z_Logger.Log($"[FishTankManager] 没有鱼在鱼饵附近触发, 半径: {radius}");
            }

            Z_Logger.Log("[FishTankManager] === CheckNearbyFish 结束 ===");
        }
    }

    /// <summary>清除所有活跃鱼饵（归还到对象池）</summary>
    public void ClearAllBaits()
    {
        LogDebug("ClearAllBaits 被调用");

        // 将所有活跃鱼饵归还到池中
        while (_activeBaits.Count > 0)
        {
            GameObject bait = _activeBaits.Dequeue();
            if (bait != null)
            {
                ResetFishChasingBait(bait);
                ReturnBaitToPool(bait);
            }
        }

        LogDebug($"已清除所有鱼饵，池中空闲: {_baitPool.Count}");
    }

    /// <summary>移除指定鱼饵（被吃掉或手动移除）</summary>
    public void RemoveBait(GameObject bait)
    {
        LogDebug("RemoveBait 被调用");

        if (bait == null)
        {
            LogDebug("RemoveBait: bait 为空");
            return;
        }

        // 从活跃队列中移除（需要重建队列，因为Queue不支持直接移除指定元素）
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
                LogInfo("鱼饵已被移除!");
            }
            else
            {
                newQueue.Enqueue(current);
            }
        }

        _activeBaits = newQueue;
        LogDebug($"RemoveBait 完成，剩余活跃: {_activeBaits.Count}, 池中空闲: {_baitPool.Count}");
    }

    /// <summary>更新所有鱼饵（下落、检测）</summary>
    private void UpdateBaits()
    {
        // 使用快照避免在遍历时修改队列
        List<GameObject> baitsList = new List<GameObject>(_activeBaits);

        foreach (var bait in baitsList)
        {
            if (bait == null) continue;

            FishTankBaitComponent baitComp = bait.GetComponent<FishTankBaitComponent>();
            if (baitComp != null)
            {
                baitComp.UpdateBait();

                // 持续检测鱼饵附近的鱼
                CheckNearbyFish(bait);

                // 检查是否有鱼吃到鱼饵（只有追逐中的鱼才能吃掉）
                CheckBaitConsumption(bait);
            }
        }
    }

    /// <summary>检查鱼饵是否被鱼吃掉</summary>
    private void CheckBaitConsumption(GameObject bait)
    {
        if (bait == null) return;
        if (!_activeBaits.Contains(bait)) return;

        Vector3 baitPos = bait.transform.position;
        if (enableDebugLog) Z_Logger.Log($"[FishTankManager] CheckBaitConsumption 检查鱼饵位置: ({baitPos.x:F2}, {baitPos.y:F2})");

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;

            // 只有正在追逐鱼饵的鱼才能吃掉鱼饵
            if (!fish.IsChasingBait)
            {
                if (enableDebugLog) Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 不在追逐状态，跳过");
                continue;
            }

            // 检查鱼的目标位置是否和当前鱼饵位置匹配
            Vector3 fishTarget = fish.GetBaitTargetPosition();
            float targetDistance = Vector3.Distance(fishTarget, baitPos);
            if (enableDebugLog) Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 目标距离鱼饵: {targetDistance:F2}");

            // 如果目标距离大于阈值，说明鱼在追逐其他鱼饵，跳过
            if (targetDistance > 0.5f)
            {
                if (enableDebugLog) Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 的目标位置({fishTarget:F2})不是当前鱼饵({baitPos:F2})，跳过");
                continue;
            }

            float distance = Vector3.Distance(fish.transform.position, baitPos);
            if (enableDebugLog) Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 距离鱼饵: {distance:F2}, 吃掉阈值: 0.3");

            // 鱼到达鱼饵位置（距离 < 0.3f）则吃掉
            if (distance < 0.3f)
            {
                if (enableDebugLog) Z_Logger.Log($"[FishTankManager] 鱼 {fish.UniqueId} 吃到鱼饵! 距离: {distance:F2}");

                // 关键日志：鱼饵被吃掉
                LogInfo($"鱼饵已被吃掉，增加金币! (鱼: {fish.UniqueId})");

                RemoveBait(bait);

                // 鱼吃掉鱼饵后，重置鱼的状态回到正常
                fish.ResetFishState();
                break;
            }
        }
    }

    // ============================================================
    // 公共方法和属性
    // ============================================================

    /// <summary>刷新鱼缸（F5触发）</summary>
    public void Refresh()
    {
        LogInfo("F5 刷新");

        ClearAllBaits();

        foreach (var f in fullScreenSwimList)
        {
            if (f)
            {
                f.ResetFishState();
                SetBehavior(f);
            }
        }
        foreach (var f in fullScreenStaticList) if (f) SetBehavior(f);
        foreach (var f in bottomSwimList) if (f) SetBehavior(f);
        foreach (var f in bottomStaticList) if (f) SetBehavior(f);

        LogFishCount();
    }

    /// <summary>设置鱼的统一缩放</summary>
    public void SetUniformScale(float scale)
    {
        uniformScale = scale;
        foreach (var f in fullScreenSwimList) if (f) f.UniformScale = scale;
        foreach (var f in fullScreenStaticList) if (f) f.UniformScale = scale;
        foreach (var f in bottomSwimList) if (f) f.UniformScale = scale;
        foreach (var f in bottomStaticList) if (f) f.UniformScale = scale;
        if (enableDebugLog) Z_Logger.Log($"[FishTankManager] 同比例缩放设置为: {scale}");
    }

    /// <summary>设置渲染队列</summary>
    public void SetRenderQueue(int queue)
    {
        renderQueue = queue;
        foreach (var f in fullScreenSwimList) if (f) f.SetRenderQueue(queue);
        foreach (var f in fullScreenStaticList) if (f) f.SetRenderQueue(queue);
        foreach (var f in bottomSwimList) if (f) f.SetRenderQueue(queue);
        foreach (var f in bottomStaticList) if (f) f.SetRenderQueue(queue);
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

    private void Start()
    {
        OpenFishTank();
    }

    public void OpenFishTank()
    {
        gameObject.SetActive(true);
        ClearAllBaits();
        if (!_isInitialized) Init();
        else { UpdateRects(); Refresh(); }
    }

    public void CloseFishTank()
    {
        gameObject.SetActive(false);
        ClearAllBaits();
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
    }

    public void ReloadFishTank()
    {
        ClearAllBaits();
        CreateAllFish();
        LogFishCount();
    }

    private void OnDestroy()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }

        // 清空对象池
        while (_baitPool.Count > 0)
        {
            GameObject bait = _baitPool.Dequeue();
            if (bait != null) Destroy(bait);
        }

        ClearAllBaits();
        ClearAllFish();
    }

    private void OnEnable()
    {
        if (_isInitialized) UpdateRects();
    }

    // ============================================================
    // 公共查询方法
    // ============================================================

    public int GetBaitCount() => _activeBaits.Count;                // 获取当前活跃鱼饵数量
    public int GetBaitMaxQueueSize() => fishTankBaitMaxQueueSize;   // 获取鱼饵队列最大容量
    public int GetPoolCount() => _baitPool.Count;                   // 获取对象池空闲数量
}

// ============================================================
// 鱼饵组件（支持对象池复用）
// ============================================================

[System.Serializable]
public class FishTankBaitComponent : MonoBehaviour
{
    private FishTankManager _manager;   // 管理器引用
    private Rect _totalRect;            // 全屏区域矩形
    private float _fallSpeed;           // 下落速度
    private float _scale;               // 鱼饵缩放
    private bool _isFalling = true;     // 是否正在下落
    private bool _isTriggered = false;  // 是否已被触发（被吃掉或移除）
    private bool _isActive = false;     // 是否激活（从池中取出）
    private Vector3 _currentPosition;   // 当前位置

    /// <summary>初始化鱼饵组件（由Manager调用）</summary>
    public void Init(FishTankManager manager, Rect totalRect, float fallSpeed, float scale)
    {
        _manager = manager;
        _totalRect = totalRect;
        _fallSpeed = fallSpeed;
        _scale = scale;
        _isFalling = true;
        _isTriggered = false;
        _isActive = false;

        transform.localScale = Vector3.one * _scale;

        if (_manager.EnableDebugLog)
            Z_Logger.Log($"[FishTankBaitComponent] 初始化完成");
    }

    /// <summary>重置鱼饵到指定位置（从池中取出时调用）</summary>
    public void ResetBait(Vector3 position)
    {
        _currentPosition = position;
        transform.position = position;
        _isFalling = true;
        _isTriggered = false;
        _isActive = true;

        if (_manager.EnableDebugLog)
            Z_Logger.Log($"[FishTankBaitComponent] 重置鱼饵到位置: ({position.x:F2}, {position.y:F2})");
    }

    /// <summary>停用鱼饵（归还到池中时调用）</summary>
    public void Deactivate()
    {
        _isActive = false;
        _isTriggered = true;
    }

    /// <summary>更新鱼饵状态（每帧由Manager调用）</summary>
    public void UpdateBait()
    {
        if (_isTriggered || !_isActive) return;

        if (_isFalling)
        {
            Vector3 pos = transform.position;
            pos.y -= _fallSpeed * Time.deltaTime;

            // 触底停止下落
            if (pos.y <= _totalRect.yMin + 0.3f)
            {
                pos.y = _totalRect.yMin + 0.3f;
                _isFalling = false;
                if (_manager.EnableDebugLog)
                    Z_Logger.Log("[FishTankBaitComponent] 鱼饵停止移动");
            }

            transform.position = pos;
            _currentPosition = pos;
        }
    }

    public bool IsActive => _isActive;   // 是否激活
}
