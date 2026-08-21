using System;
using UnityEngine;

public class FishTankFishCtrl : MonoBehaviour
{
    private string _uniqueId;

    [SerializeField] private FishSpeciesData speciesData;
    [SerializeField] private FishSpeciesType speciesType;

    [SerializeField] private GameObject renderGo;
    private MeshRenderer _renderer;
    private Material _material;

    // ===== 大小参数 =====
    private float baseHeight = 0.28f;
    private float uniformScale = 0.6f;

    // ===== 物理参数 =====
    private float moveSpeedMin = 0.5f;
    private float moveSpeedMax = 1.2f;
    private float verticalSpeedRatio = 0.4f;
    private float verticalMoveProbability = 0.2f;
    private float acceleration = 3.5f;
    private float dragForce = 0.8f;
    private float chargeSpeedRatio = 0.15f;

    // ===== 蓄力参数 =====
    private float chargeDurationMin = 0.2f;
    private float chargeDurationMax = 0.5f;
    private float chargeScaleX = 0.35f;
    private float chargeScaleY = 1.75f;

    // ===== 冲刺参数 =====
    private float sprintDurationMin = 1.5f;
    private float sprintDurationMax = 3.5f;

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

    // ===== 公共属性 =====
    public string UniqueId => _uniqueId;
    public FishSpeciesType SpeciesType => speciesType;
    public float UniformScale { get => uniformScale; set => uniformScale = value; }
    public bool EnableDebugLog { get => enableDebugLog; set => enableDebugLog = value; }
    public float GetCurrentMoveSpeed() => _currentSpeed;
    public float GetCurrentDirection() => _currentDirection;

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
    public void Init(FishSpeciesData data, Shader shader)
    {
        _uniqueId = Guid.NewGuid().ToString();
        gameObject.name = $"FishTankFishCtrl_{_uniqueId}";

        speciesData = data;
        speciesType = GetSpeciesType(data.type);

        _personality = UnityEngine.Random.Range(0.7f, 1.3f);
        _transform = transform;

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

    private FishSpeciesType GetSpeciesType(string type)
    {
        switch (type)
        {
            case "FullScreenSwim": return FishSpeciesType.FullScreenSwim;
            case "FullScreenStatic": return FishSpeciesType.FullScreenStatic;
            case "BottomSwim": return FishSpeciesType.BottomSwim;
            case "BottomStatic": return FishSpeciesType.BottomStatic;
            default: return FishSpeciesType.FullScreenStatic;
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
    // ⭐ 身体形状控制（平滑过渡）
    // ============================================================
    private void ApplyShapeToRenderer()
    {
        float currentDir = Mathf.Sign(renderGo.transform.localScale.x);
        if (currentDir == 0) currentDir = 1;
        renderGo.transform.localScale = new Vector3(_currentShape.x * currentDir, _currentShape.y, 1f);
    }

    private void UpdateShape(float progress)
    {
        // progress: 0→1 表示从正常到完全压缩
        float eased = SmoothStep01(progress);
        float targetX = _baseSize.x * (1f + (chargeScaleX - 1f) * eased);
        float targetY = _baseSize.y * (1f + (chargeScaleY - 1f) * eased);
        _shapeTarget = new Vector2(targetX, targetY);

        // ⭐ 平滑趋近目标
        _currentShape = Vector2.Lerp(_currentShape, _shapeTarget, Time.deltaTime * 12f);
        ApplyShapeToRenderer();
    }

    private void ResetShape()
    {
        // ⭐ 平滑恢复到正常形状
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

        if (enableDebugLog)
            Debug.Log($"[FishTankFishCtrl] {_uniqueId} 🔄 进入蓄力状态, 时长: {_chargeDuration:F2}s");
    }

    // ============================================================
    // 行为设置
    // ============================================================
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
    }

    // ============================================================
    // 更新逻辑
    // ============================================================
    public void UpdateFullScreenSwim(float dirMin, float dirMax)
    {
        if (!gameObject.activeSelf) return;

        if (_boundaryLockTimer > 0)
        {
            _boundaryLockTimer -= Time.deltaTime;
        }

        // ===== 方向变化（随机转向） =====
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

        // ===== 状态机 =====
        switch (_swimState)
        {
            case SwimState.Charging:
                _stateTimer += Time.deltaTime;
                float chargeProgress = Mathf.Clamp01(_stateTimer / _halfChargeDuration);

                // 身体压缩
                UpdateShape(chargeProgress);

                // 速度 = 极慢
                float minSpeed = _maxSpeed * chargeSpeedRatio;
                _currentSpeed = minSpeed;
                _targetSpeed = _maxSpeed;

                // 蓄力阶段结束 → 进入恢复阶段
                if (_stateTimer >= _halfChargeDuration)
                {
                    _swimState = SwimState.Recovering;
                    _stateTimer = 0f;
                    _currentSpeed = minSpeed;

                    if (enableDebugLog)
                        Debug.Log($"[FishTankFishCtrl] {_uniqueId} 🔄 蓄力完成, 开始恢复");
                }
                break;

            case SwimState.Recovering:
                _stateTimer += Time.deltaTime;
                float recoverProgress = Mathf.Clamp01(_stateTimer / _halfChargeDuration);

                // ⭐ 身体恢复（从压缩平滑回到正常）
                float easedRecover = 1f - SmoothStep01(recoverProgress);
                float targetX = _baseSize.x * (1f + (chargeScaleX - 1f) * easedRecover);
                float targetY = _baseSize.y * (1f + (chargeScaleY - 1f) * easedRecover);
                _shapeTarget = new Vector2(targetX, targetY);
                _currentShape = Vector2.Lerp(_currentShape, _shapeTarget, Time.deltaTime * 12f);
                ApplyShapeToRenderer();

                // 恢复的同时开始加速
                float minSpeed2 = _maxSpeed * chargeSpeedRatio;
                if (_currentSpeed < _targetSpeed)
                {
                    _currentSpeed += acceleration * Time.deltaTime;
                    if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
                }

                // 恢复阶段结束 → 进入冲刺阶段
                if (_stateTimer >= _halfChargeDuration)
                {
                    _swimState = SwimState.Sprinting;
                    _stateTimer = 0f;

                    if (enableDebugLog)
                        Debug.Log($"[FishTankFishCtrl] {_uniqueId} ⚡ 恢复完成, 进入冲刺! 加速度={acceleration:F1}");
                }
                break;

            case SwimState.Sprinting:
                _stateTimer += Time.deltaTime;

                // ⭐ 身体持续平滑恢复到正常
                ResetShape();

                // 目标速度：正弦波起伏
                float progress = Mathf.Clamp01(_stateTimer / _sprintDuration);
                float speedMod = 0.7f + 0.3f * Mathf.Sin(progress * Mathf.PI * 1.2f);
                _targetSpeed = _maxSpeed * speedMod;

                float minSpeed3 = _maxSpeed * chargeSpeedRatio;
                if (_targetSpeed < minSpeed3) _targetSpeed = minSpeed3;

                // 加速/减速
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

                // 持续阻力
                if (_stateTimer > 0.3f)
                {
                    _currentSpeed -= dragForce * 0.3f * Time.deltaTime;
                }

                if (_currentSpeed < minSpeed3) _currentSpeed = minSpeed3;

                // 力竭判断
                if (_currentSpeed <= minSpeed3 * 1.05f && _stateTimer > 0.3f)
                {
                    if (enableDebugLog)
                        Debug.Log($"[FishTankFishCtrl] {_uniqueId} 🔄 力竭, 重新蓄力");

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

                // 冲刺超时保护
                if (_stateTimer > _sprintDuration * 1.5f)
                {
                    if (enableDebugLog)
                        Debug.Log($"[FishTankFishCtrl] {_uniqueId} ⏰ 冲刺超时, 强制蓄力");

                    _swimState = SwimState.Charging;
                    _stateTimer = 0f;
                    _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
                    _halfChargeDuration = _chargeDuration / 2f;
                    _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality;
                    _currentSpeed = _maxSpeed * chargeSpeedRatio;
                    _targetSpeed = _maxSpeed;
                }
                break;
        }

        // ===== 位置计算 =====
        Vector3 targetPos = pos;

        // 水平移动
        targetPos.x += _currentSpeed * _currentDirection * Time.deltaTime;

        // 垂直移动
        if (_isVerticalMoving)
        {
            float verticalSpeed = _currentSpeed * verticalSpeedRatio;
            targetPos.y += verticalSpeed * _verticalDirection * Time.deltaTime;
        }

        _targetPosition = targetPos;

        // ===== 边界检测 =====
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

                if (enableDebugLog)
                    Debug.Log($"[FishTankFishCtrl] {_uniqueId} 🧱 左边界!");
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

                if (enableDebugLog)
                    Debug.Log($"[FishTankFishCtrl] {_uniqueId} 🧱 右边界!");
            }
        }

        // 垂直边界
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

        _targetPosition = targetPos;

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

    public void UpdateFullScreenStatic() { }

    public void UpdateBottomSwim(float dirMin, float dirMax)
    {
        if (!gameObject.activeSelf) return;

        if (_boundaryLockTimer > 0)
        {
            _boundaryLockTimer -= Time.deltaTime;
        }

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

                if (_stateTimer > _sprintDuration * 1.5f)
                {
                    _swimState = SwimState.Charging;
                    _stateTimer = 0f;
                    _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
                    _halfChargeDuration = _chargeDuration / 2f;
                    _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality;
                    _currentSpeed = _maxSpeed * chargeSpeedRatio;
                    _targetSpeed = _maxSpeed;
                }
                break;
        }

        Vector3 targetPos = pos;
        targetPos.x += _currentSpeed * _currentDirection * Time.deltaTime;

        if (_isVerticalMoving)
        {
            float verticalSpeed = _currentSpeed * verticalSpeedRatio * 0.6f;
            targetPos.y += verticalSpeed * _verticalDirection * Time.deltaTime;
        }

        _targetPosition = targetPos;

        if (_boundaryLockTimer <= 0)
        {
            if (targetPos.x < bottomAreaRect.xMin + _boundaryMargin)
            {
                targetPos.x = bottomAreaRect.xMin + _boundaryPushBack;
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
            }
            else if (targetPos.x > bottomAreaRect.xMax - _boundaryMargin)
            {
                targetPos.x = bottomAreaRect.xMax - _boundaryPushBack;
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
            }
        }

        if (targetPos.y < bottomAreaRect.yMin + _boundaryMargin)
        {
            targetPos.y = bottomAreaRect.yMin + _boundaryMargin;
            _basePosition.y = targetPos.y;
            _verticalDirection = 1;
        }
        else if (targetPos.y > bottomAreaRect.yMax - _boundaryMargin)
        {
            targetPos.y = bottomAreaRect.yMax - _boundaryMargin;
            _basePosition.y = targetPos.y;
            _verticalDirection = -1;
        }

        _targetPosition = targetPos;

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
