using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FishTankFishSpeciesType
{
    FullScreenSwim,
    FullScreenStatic,
    BottomSwim,
    BottomStatic
}

[System.Serializable]
public class FishTankFishSpeciesData
{
    public int id;
    public string name;
    public string type;
}

public class FishTankManager : MonoBehaviour
{
    [Header("鱼缸区域(绑定Quad)")]
    [SerializeField] private Transform totalArea;
    [SerializeField] private Transform bottomArea;

    [Header("鱼容器")]
    [SerializeField] private GameObject fishContainer;

    [Header("四个行为类型列表")]
    [SerializeField] private List<FishTankFishCtrl> fullScreenSwimList = new List<FishTankFishCtrl>();
    [SerializeField] private List<FishTankFishCtrl> fullScreenStaticList = new List<FishTankFishCtrl>();
    [SerializeField] private List<FishTankFishCtrl> bottomSwimList = new List<FishTankFishCtrl>();
    [SerializeField] private List<FishTankFishCtrl> bottomStaticList = new List<FishTankFishCtrl>();

    [Header("临时图片列表")]
    [SerializeField] private List<Texture2D> tempFullScreenSwimTextures = new List<Texture2D>();
    [SerializeField] private List<Texture2D> tempFullScreenStaticTextures = new List<Texture2D>();
    [SerializeField] private List<Texture2D> tempBottomSwimTextures = new List<Texture2D>();
    [SerializeField] private List<Texture2D> tempBottomStaticTextures = new List<Texture2D>();

    [Header("鱼预制体")]
    [SerializeField] private GameObject fishPrefab;

    [Header("Shader(用于创建独立材质)")]
    [SerializeField] private Shader fishShader;

    [Header("===== 大小参数 =====")]
    [SerializeField] private float baseHeight = 0.28f;
    [SerializeField] private float uniformScale = 0.6f;

    [Header("===== 方向变化间隔(秒) =====")]
    [SerializeField] private float directionChangeIntervalMin = 4f;
    [SerializeField] private float directionChangeIntervalMax = 20f;

    [Header("===== 全屏移动鱼类生成范围 =====")]
    [Range(0.1f, 1f)]
    [SerializeField] private float fullScreenSwimSpawnRange = 0.7f;

    [Header("===== 渲染队列 =====")]
    [SerializeField] private int renderQueue = 3000;

    [Header("===== 调试 =====")]
    [SerializeField] private bool enableDebugLog = false;

    // ============================================================
    // 鱼游动物理参数
    // ============================================================

    [Header("===== 水平移动(左右) =====")]
    [SerializeField] private float moveSpeedMin = 0.5f;
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
    [SerializeField] private float chargeDurationMin = 0.2f;
    [SerializeField] private float chargeDurationMax = 0.5f;
    [SerializeField] private float chargeScaleX = 0.35f;
    [SerializeField] private float chargeScaleY = 1.75f;
    [SerializeField] private float chargeSpeedRatio = 0.15f;

    [Header("===== 冲刺参数 =====")]
    [SerializeField] private float sprintDurationMin = 1.5f;
    [SerializeField] private float sprintDurationMax = 3.5f;

    // ============================================================
    // 鱼饵系统
    // ============================================================

    [Header("===== 鱼饵系统 =====")]
    [SerializeField] private GameObject fishTankBaitPrefab;
    [SerializeField] private Transform fishTankBaitContainer;
    [SerializeField] private float fishTankBaitTriggerRadius = 3f;
    [SerializeField] private float fishTankBaitFallSpeed = 0.5f;
    [SerializeField] private float fishTankBaitChaseDurationMin = 0.5f;
    [SerializeField] private float fishTankBaitChaseDurationMax = 1.5f;
    [SerializeField] private float fishTankBaitChaseSpeedMultiplier = 2.5f;
    [SerializeField] private int fishTankBaitMaxQueueSize = 50;
    [SerializeField] private float fishTankBaitScale = 1f;

    private bool _isInitialized;
    private Coroutine _updateCoroutine;

    private Rect _totalRect;
    private Rect _bottomRect;

    private Queue<GameObject> _activeBaits = new Queue<GameObject>();

    public bool IsInitialized => _isInitialized;
    public int TotalFishCount => fullScreenSwimList.Count + fullScreenStaticList.Count +
                                 bottomSwimList.Count + bottomStaticList.Count;
    public bool EnableDebugLog => enableDebugLog;
    public int BaitCount => _activeBaits.Count;

    public void Init()
    {
        if (_isInitialized) return;

        InitContainer();
        InitAreas();
        InitBaitSystem();
        UpdateRects();
        CreateAllFish();

        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
        }

        LogFishCount();
        _isInitialized = true;
         if (enableDebugLog) Debug.Log("[FishTankManager] 初始化完成");
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
             if (enableDebugLog) Debug.LogWarning("[FishTankManager] totalArea 未绑定，已自动创建");
        }

        if (bottomArea == null)
        {
            GameObject go = new GameObject("BottomArea");
            go.transform.SetParent(transform);
            bottomArea = go.transform;
            bottomArea.localPosition = Vector3.zero;
            bottomArea.localScale = Vector3.one;
             if (enableDebugLog) Debug.LogWarning("[FishTankManager] bottomArea 未绑定，已自动创建");
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
                 if (enableDebugLog) Debug.Log($"[FishTankManager] TotalArea: X:[{_totalRect.xMin:F2}, {_totalRect.xMax:F2}], Y:[{_totalRect.yMin:F2}, {_totalRect.yMax:F2}]");
        }

        if (bottomArea != null)
        {
            Vector3 pos = bottomArea.position;
            Vector3 scale = bottomArea.localScale;
            float width = scale.x;
            float height = scale.y;
            _bottomRect = new Rect(pos.x - width / 2f, pos.y - height / 2f, width, height);

            if (enableDebugLog)
                 if (enableDebugLog) Debug.Log($"[FishTankManager] BottomArea: X:[{_bottomRect.xMin:F2}, {_bottomRect.xMax:F2}], Y:[{_bottomRect.yMin:F2}, {_bottomRect.yMax:F2}]");
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
             if (enableDebugLog) Debug.LogError("[FishTankManager] fishPrefab 为空");
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
                     if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼位置: ({fish.transform.position.x:F1}, {fish.transform.position.y:F1}), " +
                              $"速度: {fish.GetCurrentMoveSpeed():F1}, 方向: {(fish.GetCurrentDirection() > 0 ? "→" : "←")}, " +
                              $"追逐状态: {fish.IsChasingBait}, 鱼饵数量: {_activeBaits.Count}");
                }
            }
        }
    }

    // ============================================================
    // 修改 Update 方法 - 鼠标点击检测
    // ============================================================

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
                        Debug.Log($"[FishTankManager] 点击位置 ({worldPos.x:F2}, {worldPos.y:F2}) 不在鱼缸范围内");
                }
            }
        }
    }

    // ============================================================
    // 新增方法：在指定位置生成鱼饵
    // ============================================================

    public void SpawnBaitAtPosition(Vector3 position)
    {
        if (enableDebugLog) Debug.Log("[FishTankManager] === SpawnBaitAtPosition 开始 ===");

        if (fishTankBaitPrefab == null)
        {
            if (enableDebugLog) Debug.LogError("[FishTankManager] fishTankBaitPrefab 为空!");
            return;
        }

        if (enableDebugLog) Debug.Log($"[FishTankManager] 当前队列数量: {_activeBaits.Count}/{fishTankBaitMaxQueueSize}");

        // 检查队列是否已满
        if (_activeBaits.Count >= fishTankBaitMaxQueueSize)
        {
            GameObject oldestBait = _activeBaits.Dequeue();
            if (oldestBait != null)
            {
                if (enableDebugLog) Debug.Log("[FishTankManager] 队列已满，移除最旧鱼饵");
                ResetFishChasingBait(oldestBait);
                Destroy(oldestBait);
            }
        }

        // 确保鱼饵在 totalArea 范围内
        float margin = 0.5f;
        Vector3 spawnPos = position;
        spawnPos.x = Mathf.Clamp(spawnPos.x, _totalRect.xMin + margin, _totalRect.xMax - margin);
        spawnPos.y = Mathf.Clamp(spawnPos.y, _totalRect.yMin + margin, _totalRect.yMax - margin);
        spawnPos.z = 0;

        if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼饵生成位置: ({spawnPos.x:F2}, {spawnPos.y:F2})");

        GameObject bait = Instantiate(fishTankBaitPrefab, fishTankBaitContainer);
        bait.transform.position = spawnPos;
        bait.transform.localScale = Vector3.one * fishTankBaitScale;

        FishTankBaitComponent baitComp = bait.GetComponent<FishTankBaitComponent>();
        if (baitComp == null)
        {
            baitComp = bait.AddComponent<FishTankBaitComponent>();
        }
        baitComp.Init(this, spawnPos, _totalRect, fishTankBaitFallSpeed, fishTankBaitScale);

        _activeBaits.Enqueue(bait);

        if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼饵已入队，当前队列数量: {_activeBaits.Count}");

        // 生成时立即检测一次附近的鱼
        CheckNearbyFish(bait);

        if (enableDebugLog) Debug.Log($"[FishTankManager] === SpawnBaitAtPosition 结束 ===");
    }

    // ============================================================
    // 保留原来的 SpawnBait 方法（随机生成，可用于调试）
    // ============================================================

    public void SpawnBait()
    {
        if (enableDebugLog) Debug.Log("[FishTankManager] === SpawnBait 开始 ===");

        if (fishTankBaitPrefab == null)
        {
            if (enableDebugLog) Debug.LogError("[FishTankManager] fishTankBaitPrefab 为空!");
            return;
        }

        if (enableDebugLog) Debug.Log($"[FishTankManager] 当前队列数量: {_activeBaits.Count}/{fishTankBaitMaxQueueSize}");

        if (_activeBaits.Count >= fishTankBaitMaxQueueSize)
        {
            GameObject oldestBait = _activeBaits.Dequeue();
            if (oldestBait != null)
            {
                if (enableDebugLog) Debug.Log("[FishTankManager] 队列已满，移除最旧鱼饵");
                ResetFishChasingBait(oldestBait);
                Destroy(oldestBait);
            }
        }

        float margin = 0.5f;
        float x = UnityEngine.Random.Range(_totalRect.xMin + margin, _totalRect.xMax - margin);
        float y = UnityEngine.Random.Range(_totalRect.yMin + margin, _totalRect.yMax - margin);
        Vector3 spawnPos = new Vector3(x, y, 0);

        if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼饵生成位置: ({x:F2}, {y:F2})");

        GameObject bait = Instantiate(fishTankBaitPrefab, fishTankBaitContainer);
        bait.transform.position = spawnPos;
        bait.transform.localScale = Vector3.one * fishTankBaitScale;

        FishTankBaitComponent baitComp = bait.GetComponent<FishTankBaitComponent>();
        if (baitComp == null)
        {
            baitComp = bait.AddComponent<FishTankBaitComponent>();
        }
        baitComp.Init(this, spawnPos, _totalRect, fishTankBaitFallSpeed, fishTankBaitScale);

        _activeBaits.Enqueue(bait);

        if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼饵已入队，当前队列数量: {_activeBaits.Count}");

        CheckNearbyFish(bait);

        if (enableDebugLog) Debug.Log($"[FishTankManager] === SpawnBait 结束 ===");
    }

    public void Refresh()
    {
         if (enableDebugLog) Debug.Log("[FishTankManager] F5 刷新");

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

    public void SetUniformScale(float scale)
    {
        uniformScale = scale;
        foreach (var f in fullScreenSwimList) if (f) f.UniformScale = scale;
        foreach (var f in fullScreenStaticList) if (f) f.UniformScale = scale;
        foreach (var f in bottomSwimList) if (f) f.UniformScale = scale;
        foreach (var f in bottomStaticList) if (f) f.UniformScale = scale;
         if (enableDebugLog) Debug.Log($"[FishTankManager] 同比例缩放设置为: {scale}");
    }

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
         if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼数量: {total} | " +
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
        ClearAllBaits();
        ClearAllFish();
    }

    private void OnEnable()
    {
        if (_isInitialized) UpdateRects();
    }

    // ============================================================
    // 鱼饵系统方法
    // ============================================================

    private void ResetFishChasingBait(GameObject bait)
    {
        if (bait == null) return;

         if (enableDebugLog) Debug.Log($"[FishTankManager] ResetFishChasingBait 被调用");

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;
            if (fish.IsChasingBait)
            {
                float distance = Vector3.Distance(fish.transform.position, bait.transform.position);
                 if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 正在追逐，距离鱼饵: {distance:F2}");

                if (distance < 0.5f)
                {
                     if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 距离鱼饵太近({distance:F2})，跳过重置");
                    continue;
                }

                if (distance < fishTankBaitTriggerRadius)
                {
                    if (Vector3.Distance(fish.GetBaitTargetPosition(), bait.transform.position) < 0.1f)
                    {
                         if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 追逐的鱼饵被移除，继续向最后位置移动");
                    }
                }
            }
        }
    }

    private void CheckNearbyFish(GameObject bait)
    {
        if (enableDebugLog)
             if (enableDebugLog) Debug.Log("[FishTankManager] === CheckNearbyFish 开始 ===");

        if (bait == null)
        {
            if (enableDebugLog)
                 if (enableDebugLog) Debug.Log("[FishTankManager] CheckNearbyFish: bait 为空");
            return;
        }

        Vector3 baitPos = bait.transform.position;
        float radius = fishTankBaitTriggerRadius;

        if (enableDebugLog)
        {
             if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼饵位置: ({baitPos.x:F2}, {baitPos.y:F2}), 触发半径: {radius}");
             if (enableDebugLog) Debug.Log($"[FishTankManager] 全屏游动鱼数量: {fullScreenSwimList.Count}");
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
                     if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 已在追逐中，跳过");
                continue;
            }

            float distance = Vector3.Distance(fish.transform.position, baitPos);
            if (enableDebugLog)
                 if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 距离鱼饵: {distance:F2}, 状态: {fish.GetCurrentSwimState()}, 鱼状态: {fish.CurrentFishState}");

            if (distance <= radius)
            {
                if (enableDebugLog)
                     if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 在触发范围内! 距离: {distance:F2}");

                // 不管什么状态，直接触发追逐
                float chaseDuration = UnityEngine.Random.Range(fishTankBaitChaseDurationMin, fishTankBaitChaseDurationMax);
                if (enableDebugLog)
                     if (enableDebugLog) Debug.Log($"[FishTankManager] 触发鱼 {fish.UniqueId} 追逐，持续时间: {chaseDuration:F2}s");
                fish.StartChasingBait(baitPos, chaseDuration, fishTankBaitChaseSpeedMultiplier);
                anyFishTriggered = true;
            }
        }

        if (enableDebugLog)
        {
             if (enableDebugLog) Debug.Log($"[FishTankManager] CheckNearbyFish 统计: 检查 {fishChecked} 条鱼, 跳过追逐中 {fishSkippedChasing} 条, 触发 {anyFishTriggered} 条");

            if (!anyFishTriggered)
            {
                 if (enableDebugLog) Debug.Log($"[FishTankManager] 没有鱼在鱼饵附近触发, 半径: {radius}");
            }

             if (enableDebugLog) Debug.Log("[FishTankManager] === CheckNearbyFish 结束 ===");
        }
    }

    public void ClearAllBaits()
    {
         if (enableDebugLog) Debug.Log("[FishTankManager] ClearAllBaits 被调用");

        List<GameObject> baitsList = new List<GameObject>(_activeBaits);
         if (enableDebugLog) Debug.Log($"[FishTankManager] 清除 {baitsList.Count} 个鱼饵");

        foreach (var bait in baitsList)
        {
            if (bait != null)
            {
                ResetFishChasingBait(bait);
                Destroy(bait);
            }
        }

        _activeBaits.Clear();

         if (enableDebugLog) Debug.Log("[FishTankManager] 已清除所有鱼饵");
    }

    public void RemoveBait(GameObject bait)
    {
         if (enableDebugLog) Debug.Log("[FishTankManager] RemoveBait 被调用");

        if (bait == null)
        {
             if (enableDebugLog) Debug.Log("[FishTankManager] RemoveBait: bait 为空");
            return;
        }

        if (!_activeBaits.Contains(bait))
        {
             if (enableDebugLog) Debug.Log("[FishTankManager] RemoveBait: 鱼饵不在队列中");
            return;
        }

         if (enableDebugLog) Debug.Log("[FishTankManager] 移除鱼饵");

        Queue<GameObject> newQueue = new Queue<GameObject>();
        bool found = false;

        while (_activeBaits.Count > 0)
        {
            GameObject current = _activeBaits.Dequeue();
            if (current == bait && !found)
            {
                found = true;
                ResetFishChasingBait(bait);
                Destroy(bait);
                 if (enableDebugLog) Debug.Log("[FishTankManager] 鱼饵已被移除!");
            }
            else
            {
                newQueue.Enqueue(current);
            }
        }

        _activeBaits = newQueue;
         if (enableDebugLog) Debug.Log($"[FishTankManager] RemoveBait 完成，剩余队列数量: {_activeBaits.Count}");
    }

    private void UpdateBaits()
    {
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

    private void CheckBaitConsumption(GameObject bait)
    {
        if (bait == null) return;
        if (!_activeBaits.Contains(bait)) return;

        Vector3 baitPos = bait.transform.position;
         if (enableDebugLog) Debug.Log($"[FishTankManager] CheckBaitConsumption 检查鱼饵位置: ({baitPos.x:F2}, {baitPos.y:F2})");

        foreach (var fish in fullScreenSwimList)
        {
            if (fish == null || !fish.gameObject.activeSelf) continue;

            // 只有正在追逐鱼饵的鱼才能吃掉鱼饵
            if (!fish.IsChasingBait)
            {
                 if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 不在追逐状态，跳过");
                continue;
            }

            // 检查鱼的目标位置是否和当前鱼饵位置匹配
            Vector3 fishTarget = fish.GetBaitTargetPosition();
            float targetDistance = Vector3.Distance(fishTarget, baitPos);
             if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 目标距离鱼饵: {targetDistance:F2}");

            // 如果目标距离大于阈值，说明鱼在追逐其他鱼饵，跳过
            if (targetDistance > 0.5f)
            {
                 if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 的目标位置({fishTarget:F2})不是当前鱼饵({baitPos:F2})，跳过");
                continue;
            }

            float distance = Vector3.Distance(fish.transform.position, baitPos);
             if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 距离鱼饵: {distance:F2}, 吃掉阈值: 0.3");

            if (distance < 0.3f)
            {
                 if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼 {fish.UniqueId} 吃到鱼饵! 距离: {distance:F2}");
                RemoveBait(bait);
                 if (enableDebugLog) Debug.Log($"[FishTankManager] 鱼饵已被吃掉，增加金币! (鱼: {fish.UniqueId})");

                // 鱼吃掉鱼饵后，重置鱼的状态回到正常
                fish.ResetFishState();
                break;
            }
        }
    }

    public int GetBaitCount()
    {
        return _activeBaits.Count;
    }

    public int GetBaitMaxQueueSize()
    {
        return fishTankBaitMaxQueueSize;
    }
}

// ============================================================
// 鱼饵组件
// ============================================================

[System.Serializable]
public class FishTankBaitComponent : MonoBehaviour
{
    private FishTankManager _manager;
    private Vector3 _startPosition;
    private Rect _totalRect;
    private float _fallSpeed;
    private float _scale;
    private bool _isFalling = true;
    private bool _isTriggered = false;

    public void Init(FishTankManager manager, Vector3 startPos, Rect totalRect, float fallSpeed, float scale)
    {
        _manager = manager;
        _startPosition = startPos;
        _totalRect = totalRect;
        _fallSpeed = fallSpeed;
        _scale = scale;
        _isFalling = true;
        _isTriggered = false;

        transform.localScale = Vector3.one * _scale;

        if (_manager.EnableDebugLog)
              Debug.Log($"[FishTankBaitComponent] 初始化完成，位置: ({startPos.x:F2}, {startPos.y:F2})");
    }

    public void UpdateBait()
    {
        if (_isTriggered) return;

        if (_isFalling)
        {
            Vector3 pos = transform.position;
            pos.y -= _fallSpeed * Time.deltaTime;

            if (pos.y <= _totalRect.yMin + 0.3f)
            {
                pos.y = _totalRect.yMin + 0.3f;
                _isFalling = false;
                if (_manager.EnableDebugLog)
                     Debug.Log("[FishTankBaitComponent] 鱼饵停止移动");
            }

            transform.position = pos;
        }
    }
}
