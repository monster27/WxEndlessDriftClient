using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FishTankFishState
{
    Normal,      // 正常状态（蓄力/恢复/冲刺）
    BaitChasing  // 追逐鱼饵状态
}

public class FishTankFishCtrl : MonoBehaviour
{
    private string _uniqueId;

    [SerializeField] private FishTankFishSpeciesData speciesData;
    [SerializeField] private FishTankFishSpeciesType speciesType;

    [SerializeField] private GameObject renderGo;
    private MeshRenderer _renderer;
    private Material _material;

    // ===== 大小参数 =====
    private float baseHeight = 0.5f;        // 鱼的基础高度
    private float uniformScale = 1f;        // 鱼的统一缩放

    // ===== 物理参数 =====
    private float moveSpeedMin = 0.35f;     // 水平移动最小速度
    private float moveSpeedMax = 1.2f;      // 水平移动最大速度
    private float verticalSpeedRatio = 0.4f; // 垂直速度比例
    private float verticalMoveProbability = 0.2f; // 垂直移动概率
    private float acceleration = 3.5f;      // 加速度
    private float dragForce = 0.8f;         // 阻力
    private float chargeSpeedRatio = 0.15f; // 蓄力速度比例

    // ===== 蓄力参数 =====
    private float chargeDurationMin = 0.4f; // 蓄力最短时间
    private float chargeDurationMax = 1f;   // 蓄力最长时间
    private float chargeScaleX = 0.6f;      // 蓄力X轴缩放
    private float chargeScaleY = 1.35f;     // 蓄力Y轴缩放

    // ===== 冲刺参数 =====
    private float sprintDurationMin = 1.5f; // 冲刺最短时间
    private float sprintDurationMax = 3.5f; // 冲刺最长时间

    // ===== 调试 =====
    private bool enableDebugLog = false;

    // ===== 个性系数 =====
    private float _personality = 1f;

    // ===== 内部状态 =====
    private Transform _transform;
    private Vector3 _basePosition;
    private Vector3 _targetPosition;
    private float _currentDirection = 1f;
    private float _verticalDirection = 1f;
    private bool _isVerticalMoving = false;

    // ===== 速度物理 =====
    private float _currentSpeed = 0f;
    private float _targetSpeed = 0f;
    private float _maxSpeed = 1.5f;

    // ===== 身体形状 =====
    private Vector2 _baseSize = new Vector2(0.4f, 0.28f);
    private Vector2 _currentShape = new Vector2(0.4f, 0.28f);
    private Vector2 _shapeTarget = new Vector2(0.4f, 0.28f);

    public Rect totalAreaRect;
    public Rect bottomAreaRect;

    private float _boundaryMargin = 0.3f;
    private float _boundaryPushBack = 0.5f;
    private float _boundaryLockTimer = 0f;
    private const float BOUNDARY_LOCK_DURATION = 0.3f;

    // ===== 状态机 =====
    private enum SwimState { Charging, Recovering, Sprinting }
    private SwimState _swimState = SwimState.Charging;

    private float _stateTimer = 0f;
    private float _chargeDuration = 0.4f;
    private float _halfChargeDuration = 0.2f;
    private float _sprintDuration = 3f;

    private float _directionChangeTimer = 0f;
    private float _directionChangeInterval = 3f;

    // ===== 鱼饵追逐状态 =====
    private FishTankFishState _fishState = FishTankFishState.Normal;
    private bool _isChasingBait = false;
    private Vector3 _baitTargetPosition;        // 鱼饵位置（用于判断鱼饵是否被吃掉）
    private Vector3 _baitInfiniteTarget;        // 无限延伸目标点（固定方向，鱼一直朝这个点游）
    private float _chaseDuration = 0f;
    private float _chaseSpeedMultiplier = 5f;   // 追逐速度倍数
    private bool _hasLoggedStart = false;

    // ============================================================
    // 公共属性
    // ============================================================

    public string UniqueId => _uniqueId;
    public FishTankFishSpeciesType SpeciesType => speciesType;
    public float UniformScale { get => uniformScale; set => uniformScale = value; }
    public bool EnableDebugLog { get => enableDebugLog; set => enableDebugLog = value; }
    public float GetCurrentMoveSpeed() => _currentSpeed;
    public float GetCurrentDirection() => _currentDirection;
    public bool IsChasingBait => _isChasingBait;
    public FishTankFishState CurrentFishState => _fishState;

    // ============================================================
    // 日志辅助方法
    // ============================================================

    private void LogDebug(string message)
    {
        if (enableDebugLog) Z_Logger.Log($"[FishTankFishCtrl] {_uniqueId} {message}");
    }

    private void LogInfo(string message)
    {
        Z_Logger.Log($"[FishTankFishCtrl] {_uniqueId} {message}");
    }

    // ============================================================
    // 外部设置
    // ============================================================

    public void SetBaseHeight(float height) { baseHeight = height; }

    public void SetPhysicsParams(
        float moveMin, float moveMax,
        float vertRatio, float vertProb,
        float accel, float drag,
        float chargeMin, float chargeMax,
        float scaleX, float scaleY,
        float speedRatio,
        float sprintMin, float sprintMax)
    {
        moveSpeedMin = moveMin;
        moveSpeedMax = moveMax;
        verticalSpeedRatio = vertRatio;
        verticalMoveProbability = vertProb;
        acceleration = accel;
        dragForce = drag;
        chargeDurationMin = chargeMin;
        chargeDurationMax = chargeMax;
        chargeScaleX = scaleX;
        chargeScaleY = scaleY;
        chargeSpeedRatio = speedRatio;
        sprintDurationMin = sprintMin;
        sprintDurationMax = sprintMax;
    }

    public void SetRenderQueue(int queue)
    {
        if (_material != null) _material.renderQueue = queue;
    }

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(FishTankFishSpeciesData data, Shader shader)
    {
        _uniqueId = Guid.NewGuid().ToString();
        gameObject.name = $"FishTankFishCtrl_{_uniqueId}";

        speciesData = data;
        speciesType = GetSpeciesType(data.type);

        _personality = UnityEngine.Random.Range(0.7f, 1.3f);
        _transform = transform;

        SetupRenderer(shader);
        ResetAllState();

        LogDebug($"初始化完成，状态: {_swimState}");
    }

    private void SetupRenderer(Shader shader)
    {
        if (renderGo == null)
        {
            renderGo = new GameObject("Render");
            renderGo.transform.SetParent(transform);
            renderGo.transform.localPosition = Vector3.zero;
            renderGo.transform.localRotation = Quaternion.identity;
            renderGo.transform.localScale = Vector3.one;
        }

        _renderer = renderGo.GetComponent<MeshRenderer>();
        if (_renderer == null) _renderer = renderGo.AddComponent<MeshRenderer>();

        if (renderGo.GetComponent<MeshFilter>() == null)
        {
            renderGo.AddComponent<MeshFilter>().mesh = CreateQuadMesh();
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Standard");
        }
        _material = new Material(shader);
        _renderer.material = _material;

        gameObject.SetActive(false);
        if (_renderer != null) _renderer.enabled = false;
    }

    private void ResetAllState()
    {
        _directionChangeInterval = UnityEngine.Random.Range(2f, 6f);
        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _boundaryLockTimer = 0f;
        _targetPosition = Vector3.zero;
        _currentSpeed = 0f;
        _currentShape = _baseSize;
        _shapeTarget = _baseSize;

        _swimState = SwimState.Charging;
        _stateTimer = 0f;
        _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
        _halfChargeDuration = _chargeDuration / 2f;
        _sprintDuration = UnityEngine.Random.Range(sprintDurationMin, sprintDurationMax) / _personality;

        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;

        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
        _hasLoggedStart = false;

        ApplyUniformScale();
    }

    private Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "Quad";

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0)
        };

        int[] triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        Vector2[] uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private FishTankFishSpeciesType GetSpeciesType(string type)
    {
        switch (type)
        {
            case "FullScreenSwim": return FishTankFishSpeciesType.FullScreenSwim;
            case "FullScreenStatic": return FishTankFishSpeciesType.FullScreenStatic;
            case "BottomSwim": return FishTankFishSpeciesType.BottomSwim;
            case "BottomStatic": return FishTankFishSpeciesType.BottomStatic;
            default: return FishTankFishSpeciesType.FullScreenStatic;
        }
    }

    // ============================================================
    // 贴图设置
    // ============================================================

    public void SetTexture(Texture2D tex)
    {
        if (_material != null && tex != null)
        {
            _material.SetTexture("_MainTex", tex);

            float finalHeight = baseHeight * uniformScale;
            float aspect = (float)tex.width / tex.height;
            float finalWidth = finalHeight * aspect;

            _baseSize = new Vector2(finalWidth, finalHeight);
            _currentShape = _baseSize;
            _shapeTarget = _baseSize;
            ApplyShapeToRenderer();
        }
    }

    public void ApplyUniformScale()
    {
        float finalHeight = baseHeight * uniformScale;
        float aspect = _baseSize.x / _baseSize.y;
        if (float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect <= 0) aspect = 1f;
        float finalWidth = finalHeight * aspect;
        _baseSize = new Vector2(finalWidth, finalHeight);
        _currentShape = _baseSize;
        _shapeTarget = _baseSize;
        ApplyShapeToRenderer();
    }

    // ============================================================
    // 身体形状控制
    // ============================================================

    private void ApplyShapeToRenderer()
    {
        float currentDir = Mathf.Sign(renderGo.transform.localScale.x);
        if (currentDir == 0) currentDir = 1;
        renderGo.transform.localScale = new Vector3(_currentShape.x * currentDir, _currentShape.y, 1f);
    }

    private void UpdateShape(float progress)
    {
        float eased = SmoothStep01(progress);
        float targetX = _baseSize.x * (1f + (chargeScaleX - 1f) * eased);
        float targetY = _baseSize.y * (1f + (chargeScaleY - 1f) * eased);
        _shapeTarget = new Vector2(targetX, targetY);
        _currentShape = Vector2.Lerp(_currentShape, _shapeTarget, Time.deltaTime * 12f);
        ApplyShapeToRenderer();
    }

    private void ResetShape()
    {
        _shapeTarget = _baseSize;
        _currentShape = Vector2.Lerp(_currentShape, _shapeTarget, Time.deltaTime * 10f);
        ApplyShapeToRenderer();
    }

    private float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void UpdateDirection(float direction)
    {
        float absY = Mathf.Abs(renderGo.transform.localScale.y);
        float absZ = Mathf.Abs(renderGo.transform.localScale.z);
        float absX = Mathf.Abs(renderGo.transform.localScale.x);
        float scaleX = direction < 0 ? absX : -absX;
        renderGo.transform.localScale = new Vector3(scaleX, absY, absZ);
    }

    // ============================================================
    // 强制进入蓄力状态
    // ============================================================

    private void ForceCharge()
    {
        _swimState = SwimState.Charging;
        _stateTimer = 0f;
        _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
        _halfChargeDuration = _chargeDuration / 2f;
        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality;
        _currentSpeed = _maxSpeed * chargeSpeedRatio;
        _targetSpeed = _maxSpeed;

        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
        if (_isVerticalMoving)
        {
            _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        }

        LogDebug($"进入蓄力状态, 速度: {_currentSpeed:F2}, 时长: {_chargeDuration:F2}s");
    }

    // ============================================================
    // 行为设置
    // ============================================================

    private void InitializeSwimState(float speedMin, float speedMax, float dirMin, float dirMax)
    {
        moveSpeedMin = speedMin;
        moveSpeedMax = speedMax;

        _directionChangeInterval = UnityEngine.Random.Range(dirMin, dirMax) / _personality;
        _directionChangeTimer = 0f;

        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;

        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality;
        _targetSpeed = _maxSpeed;
        _currentSpeed = _maxSpeed * chargeSpeedRatio;

        _swimState = SwimState.Charging;
        _stateTimer = 0f;
        _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
        _halfChargeDuration = _chargeDuration / 2f;
        _sprintDuration = UnityEngine.Random.Range(sprintDurationMin, sprintDurationMax) / _personality;

        _currentShape = _baseSize;
        _shapeTarget = _baseSize;
        ApplyShapeToRenderer();
        UpdateDirection(_currentDirection);

        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
        _hasLoggedStart = false;
    }

    public void SetFullScreenSwim(
        float speedMin, float speedMax,
        float dirMin, float dirMax,
        Vector2 customPos)
    {
        gameObject.SetActive(true);
        if (_renderer != null) _renderer.enabled = true;

        _transform.position = new Vector3(customPos.x, customPos.y, 0);
        _basePosition = _transform.position;
        _targetPosition = _transform.position;

        InitializeSwimState(speedMin, speedMax, dirMin, dirMax);

        LogDebug($"SetFullScreenSwim 完成，位置: ({customPos.x:F2}, {customPos.y:F2}), 状态: {_swimState}");
    }

    public void SetFullScreenStatic()
    {
        gameObject.SetActive(true);
        if (_renderer != null) _renderer.enabled = true;

        float margin = 0.5f;
        float x = UnityEngine.Random.Range(totalAreaRect.xMin + margin, totalAreaRect.xMax - margin);
        float y = UnityEngine.Random.Range(totalAreaRect.yMin + margin, totalAreaRect.yMax - margin);
        _transform.position = new Vector3(x, y, 0);
        _targetPosition = _transform.position;
        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _currentSpeed = 0f;
        _currentShape = _baseSize;
        _shapeTarget = _baseSize;
        ApplyShapeToRenderer();
        UpdateDirection(_currentDirection);

        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
        _hasLoggedStart = false;
    }

    public void SetBottomSwim(
        float speedMin, float speedMax,
        float dirMin, float dirMax)
    {
        gameObject.SetActive(true);
        if (_renderer != null) _renderer.enabled = true;

        float margin = 0.5f;
        float x = UnityEngine.Random.Range(bottomAreaRect.xMin + margin, bottomAreaRect.xMax - margin);
        float y = UnityEngine.Random.Range(bottomAreaRect.yMin + margin, bottomAreaRect.yMax - margin);
        _transform.position = new Vector3(x, y, 0);
        _basePosition = _transform.position;
        _targetPosition = _transform.position;

        InitializeSwimState(speedMin, speedMax, dirMin, dirMax);
    }

    public void SetBottomStatic()
    {
        gameObject.SetActive(true);
        if (_renderer != null) _renderer.enabled = true;

        float margin = 0.5f;
        float x = UnityEngine.Random.Range(bottomAreaRect.xMin + margin, bottomAreaRect.xMax - margin);
        float y = UnityEngine.Random.Range(bottomAreaRect.yMin + margin, bottomAreaRect.yMax - margin);
        _transform.position = new Vector3(x, y, 0);
        _targetPosition = _transform.position;
        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _currentSpeed = 0f;
        _currentShape = _baseSize;
        _shapeTarget = _baseSize;
        ApplyShapeToRenderer();
        UpdateDirection(_currentDirection);

        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
        _hasLoggedStart = false;
    }

    // ============================================================
    // 鱼饵追逐系统
    // ============================================================

    public void StartChasingBait(Vector3 baitPosition, float chaseDuration, float speedMultiplier = 5f)
    {
        LogDebug($"StartChasingBait 被调用, 当前追逐状态: {_isChasingBait}");

        if (_isChasingBait)
        {
            LogDebug($"已在追逐中，先重置再开始新追逐");
            _isChasingBait = false;
            _fishState = FishTankFishState.Normal;
        }

        _fishState = FishTankFishState.BaitChasing;
        _isChasingBait = true;
        _baitTargetPosition = baitPosition;
        _chaseDuration = chaseDuration;
        _chaseSpeedMultiplier = speedMultiplier;
        _stateTimer = 0f;
        _hasLoggedStart = true;

        Vector3 directionToBait = baitPosition - _transform.position;
        if (directionToBait.x != 0)
        {
            _currentDirection = directionToBait.x > 0 ? 1 : -1;
            UpdateDirection(_currentDirection);
        }

        directionToBait.Normalize();
        _baitInfiniteTarget = _transform.position + directionToBait * 100000f;

        LogInfo($"开始追逐鱼饵! 鱼饵位置: ({baitPosition.x:F2}, {baitPosition.y:F2}), 方向: {(_currentDirection > 0 ? "→" : "←")}");

        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality * _chaseSpeedMultiplier;
        _targetSpeed = _maxSpeed;
        _currentSpeed = _maxSpeed * 0.8f;
    }

    private void UpdateBaitChasing()
    {
        if (!_isChasingBait || !gameObject.activeSelf) return;

        _stateTimer += Time.deltaTime;

        Vector3 pos = _transform.position;
        Vector3 directionToTarget = (_baitInfiniteTarget - pos).normalized;

        if (Mathf.Abs(directionToTarget.x) > 0.1f)
        {
            _currentDirection = directionToTarget.x > 0 ? 1 : -1;
            UpdateDirection(_currentDirection);
        }

        // 速度物理
        UpdateSpeed();

        // 位置移动
        pos += directionToTarget * _currentSpeed * Time.deltaTime;
        pos.z = 0;
        _transform.position = pos;

        CheckBoundaries(ref pos);

        // 检查结束条件
        float minSpeed = _maxSpeed * chargeSpeedRatio;

        if (_stateTimer > _chaseDuration)
        {
            LogDebug($"结束追逐! 原因: 时间结束, 当前速度: {_currentSpeed:F2}, 用时: {_stateTimer:F2}s");
            EndBaitChasing();
            return;
        }

        if (_currentSpeed <= minSpeed * 1.05f && _stateTimer > 0.3f)
        {
            LogDebug($"结束追逐! 原因: 速度降到最小速度, 当前速度: {_currentSpeed:F2}, 用时: {_stateTimer:F2}s");
            EndBaitChasing();
            return;
        }
    }

    private void UpdateSpeed()
    {
        if (_currentSpeed < _targetSpeed)
        {
            _currentSpeed += acceleration * 2f * Time.deltaTime;
            if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
        }

        if (_currentSpeed > 0)
        {
            _currentSpeed -= dragForce * Time.deltaTime;
            if (_currentSpeed < 0) _currentSpeed = 0;
        }

        float minSpeed = _maxSpeed * chargeSpeedRatio;
        if (_currentSpeed < minSpeed)
        {
            _currentSpeed = minSpeed;
        }
    }

    private void CheckBoundaries(ref Vector3 pos)
    {
        if (_boundaryLockTimer > 0)
        {
            _boundaryLockTimer -= Time.deltaTime;
        }

        if (_boundaryLockTimer <= 0)
        {
            if (pos.x < totalAreaRect.xMin + _boundaryMargin)
            {
                pos.x = totalAreaRect.xMin + _boundaryPushBack;
                _currentDirection = 1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);

                if (_isChasingBait)
                {
                    LogDebug($"追逐时撞到左边界，结束追逐!");
                    EndBaitChasing();
                }
            }
            else if (pos.x > totalAreaRect.xMax - _boundaryMargin)
            {
                pos.x = totalAreaRect.xMax - _boundaryPushBack;
                _currentDirection = -1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);

                if (_isChasingBait)
                {
                    LogDebug($"追逐时撞到右边界，结束追逐!");
                    EndBaitChasing();
                }
            }
        }

        if (pos.y < totalAreaRect.yMin + _boundaryMargin)
        {
            pos.y = totalAreaRect.yMin + _boundaryMargin;
            if (_isChasingBait)
            {
                LogDebug($"追逐时撞到上边界，结束追逐!");
                EndBaitChasing();
            }
        }
        else if (pos.y > totalAreaRect.yMax - _boundaryMargin)
        {
            pos.y = totalAreaRect.yMax - _boundaryMargin;
            if (_isChasingBait)
            {
                LogDebug($"追逐时撞到下边界，结束追逐!");
                EndBaitChasing();
            }
        }
    }

    private void EndBaitChasing()
    {
        if (!_isChasingBait) return;

        float finalSpeed = _currentSpeed;
        _isChasingBait = false;
        _fishState = FishTankFishState.Normal;
        _hasLoggedStart = false;

        ForceCharge();

        LogDebug($"结束追逐鱼饵! 最终速度: {finalSpeed:F2} -> 蓄力速度: {_currentSpeed:F2}");
    }

    public void ResetFishState()
    {
        LogDebug($"ResetFishState 被调用");

        if (_isChasingBait)
        {
            LogDebug($"正在追逐中，强制重置");
        }

        _isChasingBait = false;
        _fishState = FishTankFishState.Normal;
        _stateTimer = 0f;
        _hasLoggedStart = false;
        ForceCharge();
    }

    public Vector3 GetBaitTargetPosition() => _baitTargetPosition;
    public string GetCurrentSwimState() => _swimState.ToString();

    // ============================================================
    // 更新逻辑
    // ============================================================

    private void UpdateNormalSwimming(float dirMin, float dirMax)
    {
        if (_boundaryLockTimer <= 0 && (_swimState == SwimState.Sprinting || _swimState == SwimState.Recovering))
        {
            _directionChangeTimer += Time.deltaTime;
            if (_directionChangeTimer > _directionChangeInterval)
            {
                _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                _directionChangeTimer = 0;
                _directionChangeInterval = UnityEngine.Random.Range(dirMin, dirMax) / _personality;
                UpdateDirection(_currentDirection);

                _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
                if (_isVerticalMoving)
                {
                    _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                }

                ForceCharge();
            }
        }

        Vector3 pos = _transform.position;
        UpdateSwimState(ref pos);

        Vector3 targetPos = pos;
        targetPos.x += _currentSpeed * _currentDirection * Time.deltaTime;

        if (_isVerticalMoving)
        {
            float verticalSpeed = _currentSpeed * verticalSpeedRatio;
            targetPos.y += verticalSpeed * _verticalDirection * Time.deltaTime;
        }

        _targetPosition = targetPos;
        ApplyBoundary(ref targetPos);

        Vector3 delta = targetPos - pos;
        float maxDelta = Mathf.Max(_currentSpeed, 0.5f) * Time.deltaTime * 3f;
        if (delta.magnitude > maxDelta)
        {
            delta = delta.normalized * maxDelta;
        }
        pos += delta;
        pos.z = 0;
        _transform.position = pos;
    }

    private void UpdateSwimState(ref Vector3 pos)
    {
        switch (_swimState)
        {
            case SwimState.Charging:
                _stateTimer += Time.deltaTime;
                float chargeProgress = Mathf.Clamp01(_stateTimer / _halfChargeDuration);
                UpdateShape(chargeProgress);

                float minSpeed = _maxSpeed * chargeSpeedRatio;
                _currentSpeed = minSpeed;
                _targetSpeed = _maxSpeed;

                if (_stateTimer >= _halfChargeDuration)
                {
                    _swimState = SwimState.Recovering;
                    _stateTimer = 0f;
                    _currentSpeed = minSpeed;
                    LogDebug($"蓄力完成, 开始恢复");
                }
                break;

            case SwimState.Recovering:
                _stateTimer += Time.deltaTime;
                float recoverProgress = Mathf.Clamp01(_stateTimer / _halfChargeDuration);

                float easedRecover = 1f - SmoothStep01(recoverProgress);
                float targetX = _baseSize.x * (1f + (chargeScaleX - 1f) * easedRecover);
                float targetY = _baseSize.y * (1f + (chargeScaleY - 1f) * easedRecover);
                _shapeTarget = new Vector2(targetX, targetY);
                _currentShape = Vector2.Lerp(_currentShape, _shapeTarget, Time.deltaTime * 12f);
                ApplyShapeToRenderer();

                float minSpeed2 = _maxSpeed * chargeSpeedRatio;
                if (_currentSpeed < _targetSpeed)
                {
                    _currentSpeed += acceleration * Time.deltaTime;
                    if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
                }

                if (_stateTimer >= _halfChargeDuration)
                {
                    _swimState = SwimState.Sprinting;
                    _stateTimer = 0f;
                    LogDebug($"恢复完成, 进入冲刺! 加速度={acceleration:F1}");
                }
                break;

            case SwimState.Sprinting:
                _stateTimer += Time.deltaTime;
                ResetShape();

                float progress = Mathf.Clamp01(_stateTimer / _sprintDuration);
                float speedMod = 0.7f + 0.3f * Mathf.Sin(progress * Mathf.PI * 1.2f);
                _targetSpeed = _maxSpeed * speedMod;

                float minSpeed3 = _maxSpeed * chargeSpeedRatio;
                if (_targetSpeed < minSpeed3) _targetSpeed = minSpeed3;

                if (_currentSpeed < _targetSpeed)
                {
                    _currentSpeed += acceleration * Time.deltaTime;
                    if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
                }
                else if (_currentSpeed > _targetSpeed)
                {
                    _currentSpeed -= dragForce * Time.deltaTime;
                    if (_currentSpeed < _targetSpeed) _currentSpeed = _targetSpeed;
                }

                if (_stateTimer > 0.3f)
                {
                    _currentSpeed -= dragForce * 0.3f * Time.deltaTime;
                }

                if (_currentSpeed < minSpeed3) _currentSpeed = minSpeed3;

                if (_currentSpeed <= minSpeed3 * 1.05f && _stateTimer > 0.3f)
                {
                    LogDebug($"力竭, 重新蓄力");
                    ResetToCharging();
                }

                if (_stateTimer > _sprintDuration * 1.5f)
                {
                    LogDebug($"冲刺超时, 强制蓄力");
                    ResetToCharging();
                }
                break;
        }
    }

    private void ResetToCharging()
    {
        _swimState = SwimState.Charging;
        _stateTimer = 0f;
        _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
        _halfChargeDuration = _chargeDuration / 2f;
        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality;
        _currentSpeed = _maxSpeed * chargeSpeedRatio;
        _targetSpeed = _maxSpeed;

        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
        if (_isVerticalMoving)
        {
            _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        }
    }

    private void ApplyBoundary(ref Vector3 targetPos)
    {
        if (_boundaryLockTimer <= 0)
        {
            if (targetPos.x < totalAreaRect.xMin + _boundaryMargin)
            {
                targetPos.x = totalAreaRect.xMin + _boundaryPushBack;
                _basePosition.x = targetPos.x;
                _currentDirection = 1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);

                _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
                if (_isVerticalMoving)
                {
                    _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                }

                ForceCharge();
                LogDebug($"左边界!");
            }
            else if (targetPos.x > totalAreaRect.xMax - _boundaryMargin)
            {
                targetPos.x = totalAreaRect.xMax - _boundaryPushBack;
                _basePosition.x = targetPos.x;
                _currentDirection = -1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);

                _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
                if (_isVerticalMoving)
                {
                    _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                }

                ForceCharge();
                LogDebug($"右边界!");
            }
        }

        if (targetPos.y < totalAreaRect.yMin + _boundaryMargin)
        {
            targetPos.y = totalAreaRect.yMin + _boundaryMargin;
            _basePosition.y = targetPos.y;
            _verticalDirection = 1;
        }
        else if (targetPos.y > totalAreaRect.yMax - _boundaryMargin)
        {
            targetPos.y = totalAreaRect.yMax - _boundaryMargin;
            _basePosition.y = targetPos.y;
            _verticalDirection = -1;
        }
    }

    // ============================================================
    // 公共更新方法
    // ============================================================

    public void UpdateFullScreenSwim(float dirMin, float dirMax)
    {
        if (!gameObject.activeSelf) return;

        if (_isChasingBait)
        {
            UpdateBaitChasing();
            return;
        }

        if (_boundaryLockTimer > 0)
        {
            _boundaryLockTimer -= Time.deltaTime;
        }

        UpdateNormalSwimming(dirMin, dirMax);
    }

    public void UpdateFullScreenStatic() { }

    public void UpdateBottomSwim(float dirMin, float dirMax)
    {
        if (!gameObject.activeSelf) return;

        if (_isChasingBait)
        {
            UpdateBaitChasing();
            return;
        }

        if (_boundaryLockTimer > 0)
        {
            _boundaryLockTimer -= Time.deltaTime;
        }

        UpdateNormalSwimming(dirMin, dirMax);
    }

    public void UpdateBottomStatic() { }

    public void Stop()
    {
        if (_renderer != null) _renderer.enabled = false;
        gameObject.SetActive(false);
    }

    public void Release()
    {
        if (_material != null)
        {
            Destroy(_material);
            _material = null;
        }
        Stop();
    }

    private void OnDestroy()
    {
        Release();
    }
}
