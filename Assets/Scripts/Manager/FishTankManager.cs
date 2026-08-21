using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    // 鱼游动物理参数（可在 Inspector 面板调整）
    // ============================================================

    [Header("===== 水平移动(左右) =====")]
    [Tooltip("水平速度最小值(单位/秒)")]
    [SerializeField] private float moveSpeedMin = 0.5f;
    [Tooltip("水平速度最大值(单位/秒)")]
    [SerializeField] private float moveSpeedMax = 1.2f;

    [Header("===== 垂直移动(上下) =====")]
    [Tooltip("垂直速度占水平速度的比例(0.3~0.6), 值越大上下起伏越明显")]
    [SerializeField] private float verticalSpeedRatio = 0.4f;
    [Tooltip("每次转向时触发垂直移动的概率(0~1), 0.2表示20%概率会上下游动")]
    [Range(0f, 1f)]
    [SerializeField] private float verticalMoveProbability = 0.2f;

    [Header("===== 物理参数 =====")]
    [Tooltip("加速度最小值, 值越大加速越快")]
    [SerializeField] private float accelerationMin = 2.5f;
    [Tooltip("加速度最大值, 值越大加速越快")]
    [SerializeField] private float accelerationMax = 5.0f;
    [Tooltip("阻力强度, 值越大减速越快(建议0.3~3)")]
    [SerializeField] private float dragForce = 0.8f;

    [Header("===== 蓄力参数 =====")]
    [Tooltip("蓄力最短时间(秒)")]
    [SerializeField] private float chargeDurationMin = 0.2f;
    [Tooltip("蓄力最长时间(秒)")]
    [SerializeField] private float chargeDurationMax = 0.5f;
    [Tooltip("蓄力时X轴缩放比例(0.3~0.6), 值越小压得越扁")]
    [SerializeField] private float chargeScaleX = 0.35f;
    [Tooltip("蓄力时Y轴缩放比例(1.5~2.5), 值越大拉得越长")]
    [SerializeField] private float chargeScaleY = 1.75f;
    [Tooltip("蓄力速度为最大速度的百分比(0.1~0.3)")]
    [SerializeField] private float chargeSpeedRatio = 0.15f;

    [Header("===== 冲刺参数 =====")]
    [Tooltip("冲刺最短时间(秒)")]
    [SerializeField] private float sprintDurationMin = 1.5f;
    [Tooltip("冲刺最长时间(秒)")]
    [SerializeField] private float sprintDurationMax = 3.5f;

    private bool _isInitialized;
    private Coroutine _updateCoroutine;

    private Rect _totalRect;
    private Rect _bottomRect;

    public bool IsInitialized => _isInitialized;
    public int TotalFishCount => fullScreenSwimList.Count + fullScreenStaticList.Count +
                                 bottomSwimList.Count + bottomStaticList.Count;

    public void Init()
    {
        if (_isInitialized) return;

        InitContainer();
        InitAreas();
        UpdateRects();
        CreateAllFish();

        if (_updateCoroutine == null)
        {
            _updateCoroutine = StartCoroutine(UpdateLoop());
        }

        LogFishCount();
        _isInitialized = true;
        Debug.Log("[FishTankManager] ✅ 初始化完成");
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
            Debug.LogWarning("[FishTankManager] totalArea 未绑定，已自动创建");
        }

        if (bottomArea == null)
        {
            GameObject go = new GameObject("BottomArea");
            go.transform.SetParent(transform);
            bottomArea = go.transform;
            bottomArea.localPosition = Vector3.zero;
            bottomArea.localScale = Vector3.one;
            Debug.LogWarning("[FishTankManager] bottomArea 未绑定，已自动创建");
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
                Debug.Log($"[FishTankManager] 📐 TotalArea: X:[{_totalRect.xMin:F2}, {_totalRect.xMax:F2}], Y:[{_totalRect.yMin:F2}, {_totalRect.yMax:F2}]");
        }

        if (bottomArea != null)
        {
            Vector3 pos = bottomArea.position;
            Vector3 scale = bottomArea.localScale;
            float width = scale.x;
            float height = scale.y;
            _bottomRect = new Rect(pos.x - width / 2f, pos.y - height / 2f, width, height);

            if (enableDebugLog)
                Debug.Log($"[FishTankManager] 📐 BottomArea: X:[{_bottomRect.xMin:F2}, {_bottomRect.xMax:F2}], Y:[{_bottomRect.yMin:F2}, {_bottomRect.yMax:F2}]");
        }

        foreach (var fish in fullScreenSwimList) SetFishParams(fish);
        foreach (var fish in fullScreenStaticList) SetFishParams(fish);
        foreach (var fish in bottomSwimList) SetFishParams(fish);
        foreach (var fish in bottomStaticList) SetFishParams(fish);
    }

    private void SetFishParams(FishTankFishCtrl fish)
    {
        if (fish == null) return;

        float acceleration = Random.Range(accelerationMin, accelerationMax);

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
            if (tex != null) CreateFish(tex, FishSpeciesType.FullScreenSwim);

        foreach (var tex in tempFullScreenStaticTextures)
            if (tex != null) CreateFish(tex, FishSpeciesType.FullScreenStatic);

        foreach (var tex in tempBottomSwimTextures)
            if (tex != null) CreateFish(tex, FishSpeciesType.BottomSwim);

        foreach (var tex in tempBottomStaticTextures)
            if (tex != null) CreateFish(tex, FishSpeciesType.BottomStatic);

        LogFishCount();
    }

    private void CreateFish(Texture2D texture, FishSpeciesType type)
    {
        if (fishPrefab == null)
        {
            Debug.LogError("[FishTankManager] fishPrefab 为空");
            return;
        }

        GameObject go = Instantiate(fishPrefab, fishContainer.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        FishTankFishCtrl fish = go.GetComponent<FishTankFishCtrl>();
        if (fish == null) fish = go.AddComponent<FishTankFishCtrl>();

        FishSpeciesData data = new FishSpeciesData
        {
            id = (int)type,
            name = type.ToString(),
            type = type.ToString()
        };

        float acceleration = Random.Range(accelerationMin, accelerationMax);

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
        float x = Random.Range(rect.xMin + margin, rect.xMax - margin);
        float yMax = rect.yMax - margin;
        float yMin = rect.yMax - (rect.yMax - rect.yMin) * range + margin;
        if (yMin > yMax) yMin = yMax;
        float y = Random.Range(yMin, yMax);
        return new Vector2(x, y);
    }

    private void SetBehavior(FishTankFishCtrl fish)
    {
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

    private IEnumerator UpdateLoop()
    {
        int frameCount = 0;
        while (true)
        {
            yield return new WaitForEndOfFrame();
            if (!gameObject.activeSelf) continue;

            frameCount++;

            foreach (var f in fullScreenSwimList) f?.UpdateFullScreenSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var f in fullScreenStaticList) f?.UpdateFullScreenStatic();
            foreach (var f in bottomSwimList) f?.UpdateBottomSwim(directionChangeIntervalMin, directionChangeIntervalMax);
            foreach (var f in bottomStaticList) f?.UpdateBottomStatic();

            if (enableDebugLog && frameCount % 60 == 0 && fullScreenSwimList.Count > 0)
            {
                var fish = fullScreenSwimList[0];
                if (fish != null && fish.gameObject.activeSelf)
                {
                    Debug.Log($"[FishTankManager] 📍 鱼位置: ({fish.transform.position.x:F1}, {fish.transform.position.y:F1}), " +
                              $"速度: {fish.GetCurrentMoveSpeed():F1}, 方向: {(fish.GetCurrentDirection() > 0 ? "→" : "←")}");
                }
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        Debug.Log("[FishTankManager] 🔄 F5 刷新");
        foreach (var f in fullScreenSwimList) if (f) SetBehavior(f);
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
        Debug.Log($"[FishTankManager] 📐 同比例缩放设置为: {scale}");
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
        Debug.Log($"[FishTankManager] 🐟 鱼数量: {total} | " +
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
        if (!_isInitialized) Init();
        else { UpdateRects(); Refresh(); }
    }

    public void CloseFishTank()
    {
        gameObject.SetActive(false);
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
            _updateCoroutine = null;
        }
    }

    public void ReloadFishTank()
    {
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
        ClearAllFish();
    }

    private void OnEnable()
    {
        if (_isInitialized) UpdateRects();
    }
}
