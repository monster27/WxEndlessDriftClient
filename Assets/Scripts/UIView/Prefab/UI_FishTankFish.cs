// UI_FishTankFish.cs
using System;
using UnityEngine;
using UnityEngine.UI;
public enum FishTankFishState
{
    Normal,      // 正常状态（蓄力/恢复/冲刺）
    BaitChasing  // 追逐鱼饵状态
}

public class UI_FishTankFish : MonoBehaviour
{
    private string _uniqueId;

    [SerializeField] private FishSpeciesData speciesData;
    [SerializeField] private FishSpeciesType speciesType;

    [SerializeField] private GameObject renderGo;
    private Image _image;

    // ===== 大小参数 =====
    [SerializeField] private float baseHeight = 50f;
    [SerializeField] private float baseScale = 1f;

    // ===== 物理参数 =====
    private float moveSpeedMin = 0.35f;
    private float moveSpeedMax = 1.2f;
    private float verticalSpeedRatio = 0.4f;
    private float verticalMoveProbability = 0.2f;
    private float acceleration = 3.5f;
    private float dragForce = 0.8f;
    private float chargeSpeedRatio = 0.15f;
    private float chargeDurationMin = 0.4f;
    private float chargeDurationMax = 1f;
    private float chargeScaleX = 0.6f;
    private float chargeScaleY = 1.35f;
    private float sprintDurationMin = 1.5f;
    private float sprintDurationMax = 3.5f;

    private float ScaleFactor => baseScale * 100f;

    private bool enableDebugLog = false;

    private float _personality = 1f;

    // ===== 内部状态 =====
    private RectTransform _rect;
    private Vector2 _basePosition;
    private Vector2 _targetPosition;
    private float _currentDirection = 1f;
    private float _verticalDirection = 1f;
    private bool _isVerticalMoving = false;

    private float _currentSpeed = 0f;
    private float _targetSpeed = 0f;
    private float _maxSpeed = 1.5f;

    private Vector2 _baseSize = new Vector2(50f, 50f);
    private Vector2 _currentShape = new Vector2(50f, 50f);

    private float _animScaleX = 1f;
    private float _animScaleY = 1f;

    public Rect totalAreaRect;
    public Rect bottomAreaRect;

    private float _boundaryMargin = 0.3f;
    private float _boundaryPushBack = 0.5f;
    private float _boundaryLockTimer = 0f;
    private const float BOUNDARY_LOCK_DURATION = 0.3f;

    private enum SwimState { Charging, Recovering, Sprinting }
    private SwimState _swimState = SwimState.Charging;

    private float _stateTimer = 0f;
    private float _chargeDuration = 0.4f;
    private float _halfChargeDuration = 0.2f;
    private float _sprintDuration = 3f;

    private float _directionChangeTimer = 0f;
    private float _directionChangeInterval = 3f;

    private FishTankFishState _fishState = FishTankFishState.Normal;
    private bool _isChasingBait = false;
    private Vector2 _baitTargetPosition;
    private Vector2 _baitInfiniteTarget;
    private float _chaseDuration = 0f;
    private float _chaseSpeedMultiplier = 5f;
    private bool _hasLoggedStart = false;

    // ===== 组件初始化标志 =====
    private bool _componentsReady = false;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public string UniqueId => _uniqueId;
    public FishSpeciesType SpeciesType => speciesType;
    public float UniformScale { get => baseScale; set { EnsureComponents(); baseScale = value; ApplyBaseSize(); } }
    public bool EnableDebugLog { get => enableDebugLog; set => enableDebugLog = value; }
    public float GetCurrentMoveSpeed() => _currentSpeed;
    public float GetCurrentDirection() => _currentDirection;
    public bool IsChasingBait => _isChasingBait;
    public FishTankFishState CurrentFishState => _fishState;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 组件懒初始化（替代 Awake）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void EnsureComponents()
    {
        if (_componentsReady) return;

        _rect = GetComponent<RectTransform>();
        if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);

        if (renderGo == null)
        {
            renderGo = new GameObject("Render");
            renderGo.transform.SetParent(transform);
            var rt = renderGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        _image = renderGo.GetComponent<Image>();
        if (_image == null) _image = renderGo.AddComponent<Image>();
        _image.raycastTarget = false;

        _componentsReady = true;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void Init(FishSpeciesData data, Shader shader)
    {
        EnsureComponents();

        _uniqueId = Guid.NewGuid().ToString();
        gameObject.name = $"UI_FishTankFish_{_uniqueId}";
        speciesData = data;
        speciesType = GetSpeciesType(data.type);
        _personality = UnityEngine.Random.Range(0.7f, 1.3f);
        ResetAllState();
        LogDebug($"初始化完成");
    }

    private void ResetAllState()
    {
        EnsureComponents();

        _directionChangeInterval = UnityEngine.Random.Range(2f, 6f);
        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _boundaryLockTimer = 0f;
        _targetPosition = Vector2.zero;
        _currentSpeed = 0f;
        _animScaleX = 1f;
        _animScaleY = 1f;

        _swimState = SwimState.Charging;
        _stateTimer = 0f;
        _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
        _halfChargeDuration = _chargeDuration / 2f;
        _sprintDuration = UnityEngine.Random.Range(sprintDurationMin, sprintDurationMax) / _personality;

        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;

        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
        _hasLoggedStart = false;

        ApplyBaseSize();
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

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void SetTexture(Texture2D tex)
    {
        EnsureComponents();
        if (_image == null || tex == null) return;

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        _image.sprite = sprite;

        float aspect = (float)tex.width / tex.height;
        float finalHeight = baseHeight;
        float finalWidth = finalHeight * aspect;

        _baseSize = new Vector2(finalWidth, finalHeight);
        _currentShape = _baseSize;
        ApplyBaseSize();

        if (gameObject.activeSelf)
        {
            Vector2 pos = _rect.anchoredPosition;
            pos = ClampPositionToBoundary(pos);
            _rect.anchoredPosition = pos;
            _basePosition = pos;
            _targetPosition = pos;
        }
    }

    private void ApplyBaseSize()
    {
        EnsureComponents();
        if (_rect == null) return;
        _rect.sizeDelta = _baseSize * baseScale;
        ApplyShapeToRenderer();
    }

    private void ApplyShapeToRenderer()
    {
        EnsureComponents();
        if (_image == null || renderGo == null) return;

        renderGo.transform.localScale = new Vector3(
            -_currentDirection * _animScaleX,
            _animScaleY,
            1f
        );
    }

    private void UpdateShape(float progress)
    {
        EnsureComponents();
        float eased = SmoothStep01(progress);
        float targetX = Mathf.Lerp(1f, chargeScaleX, eased);
        float targetY = Mathf.Lerp(1f, chargeScaleY, eased);
        _animScaleX = Mathf.Lerp(_animScaleX, targetX, Time.deltaTime * 12f);
        _animScaleY = Mathf.Lerp(_animScaleY, targetY, Time.deltaTime * 12f);
        ApplyShapeToRenderer();
    }

    private void ResetShape()
    {
        EnsureComponents();
        _animScaleX = Mathf.Lerp(_animScaleX, 1f, Time.deltaTime * 10f);
        _animScaleY = Mathf.Lerp(_animScaleY, 1f, Time.deltaTime * 10f);
        ApplyShapeToRenderer();
    }

    private float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void UpdateDirection(float direction)
    {
        _currentDirection = direction;
        ApplyShapeToRenderer();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void SetBaseHeight(float height) { /* 固定为 50，忽略 */ }

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

    public void SetRenderQueue(int queue) { }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private Vector2 ClampPositionToBoundary(Vector2 pos)
    {
        EnsureComponents();
        if (_rect == null) return pos;
        float halfWidth = _rect.sizeDelta.x * 0.5f;
        float halfHeight = _rect.sizeDelta.y * 0.5f;
        pos.x = Mathf.Clamp(pos.x,
            totalAreaRect.xMin + halfWidth + _boundaryMargin,
            totalAreaRect.xMax - halfWidth - _boundaryMargin);
        pos.y = Mathf.Clamp(pos.y,
            totalAreaRect.yMin + halfHeight + _boundaryMargin,
            totalAreaRect.yMax - halfHeight - _boundaryMargin);
        return pos;
    }

    private Vector2 GetRandomPosInRect(Rect rect, float halfWidth, float halfHeight)
    {
        float margin = 0.3f;
        float x = UnityEngine.Random.Range(rect.xMin + margin + halfWidth, rect.xMax - margin - halfWidth);
        float y = UnityEngine.Random.Range(rect.yMin + margin + halfHeight, rect.yMax - margin - halfHeight);
        return new Vector2(x, y);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void ForceCharge()
    {
        EnsureComponents();
        _swimState = SwimState.Charging;
        _stateTimer = 0f;
        _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
        _halfChargeDuration = _chargeDuration / 2f;
        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality * ScaleFactor;
        _currentSpeed = _maxSpeed * chargeSpeedRatio;
        _targetSpeed = _maxSpeed;

        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
        if (_isVerticalMoving) _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;

        LogDebug($"蓄力，速度: {_currentSpeed:F2}");
    }

    private void InitializeSwimState(float speedMin, float speedMax, float dirMin, float dirMax)
    {
        EnsureComponents();

        moveSpeedMin = speedMin;
        moveSpeedMax = speedMax;
        _directionChangeInterval = UnityEngine.Random.Range(dirMin, dirMax) / _personality;
        _directionChangeTimer = 0f;
        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality * ScaleFactor;
        _targetSpeed = _maxSpeed;
        _currentSpeed = _maxSpeed * chargeSpeedRatio;

        _swimState = SwimState.Charging;
        _stateTimer = 0f;
        _chargeDuration = UnityEngine.Random.Range(chargeDurationMin, chargeDurationMax) / _personality;
        _halfChargeDuration = _chargeDuration / 2f;
        _sprintDuration = UnityEngine.Random.Range(sprintDurationMin, sprintDurationMax) / _personality;

        _animScaleX = 1f;
        _animScaleY = 1f;
        ApplyShapeToRenderer();
        UpdateDirection(_currentDirection);
        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
        _hasLoggedStart = false;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void SetFullScreenSwim(
        float speedMin, float speedMax,
        float dirMin, float dirMax,
        Vector2 customPos)
    {
        EnsureComponents();
        gameObject.SetActive(true);
        if (_image != null) _image.enabled = true;

        customPos = ClampPositionToBoundary(customPos);
        _rect.anchoredPosition = customPos;
        _basePosition = customPos;
        _targetPosition = customPos;

        InitializeSwimState(speedMin, speedMax, dirMin, dirMax);
        LogDebug($"设置游动，位置: ({customPos.x:F1}, {customPos.y:F1})");
    }

    public void SetFullScreenStatic()
    {
        EnsureComponents();
        gameObject.SetActive(true);
        if (_image != null) _image.enabled = true;
        float halfWidth = _rect.sizeDelta.x * 0.5f;
        float halfHeight = _rect.sizeDelta.y * 0.5f;
        Vector2 pos = GetRandomPosInRect(totalAreaRect, halfWidth, halfHeight);
        _rect.anchoredPosition = pos;
        _targetPosition = pos;
        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _currentSpeed = 0f;
        _animScaleX = 1f;
        _animScaleY = 1f;
        ApplyShapeToRenderer();
        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
    }

    public void SetBottomSwim(
        float speedMin, float speedMax,
        float dirMin, float dirMax)
    {
        EnsureComponents();
        gameObject.SetActive(true);
        if (_image != null) _image.enabled = true;
        float halfWidth = _rect.sizeDelta.x * 0.5f;
        float halfHeight = _rect.sizeDelta.y * 0.5f;
        Vector2 pos = GetRandomPosInRect(bottomAreaRect, halfWidth, halfHeight);
        _rect.anchoredPosition = pos;
        _basePosition = pos;
        _targetPosition = pos;
        InitializeSwimState(speedMin, speedMax, dirMin, dirMax);
    }

    public void SetBottomStatic()
    {
        EnsureComponents();
        gameObject.SetActive(true);
        if (_image != null) _image.enabled = true;
        float halfWidth = _rect.sizeDelta.x * 0.5f;
        float halfHeight = _rect.sizeDelta.y * 0.5f;
        Vector2 pos = GetRandomPosInRect(bottomAreaRect, halfWidth, halfHeight);
        _rect.anchoredPosition = pos;
        _targetPosition = pos;
        _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
        _currentSpeed = 0f;
        _animScaleX = 1f;
        _animScaleY = 1f;
        ApplyShapeToRenderer();
        _fishState = FishTankFishState.Normal;
        _isChasingBait = false;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void StartChasingBait(Vector2 baitPosition, float chaseDuration, float speedMultiplier = 5f)
    {
        EnsureComponents();
        LogDebug($"StartChasingBait: 鱼饵位置={baitPosition}, 鱼位置={_rect.anchoredPosition}");

        if (_isChasingBait)
        {
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

        Vector2 directionToBait = baitPosition - _rect.anchoredPosition;
        if (directionToBait.x != 0)
        {
            _currentDirection = directionToBait.x > 0 ? 1 : -1;
            UpdateDirection(_currentDirection);
        }
        directionToBait.Normalize();
        _baitInfiniteTarget = _rect.anchoredPosition + directionToBait * 100000f;

        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality * ScaleFactor * _chaseSpeedMultiplier;
        _targetSpeed = _maxSpeed;
        _currentSpeed = _maxSpeed * 0.8f;
    }

    private void UpdateBaitChasing()
    {
        EnsureComponents();
        if (!_isChasingBait || !gameObject.activeSelf) return;
        _stateTimer += Time.deltaTime;
        Vector2 pos = _rect.anchoredPosition;
        Vector2 directionToTarget = (_baitInfiniteTarget - pos).normalized;

        if (Mathf.Abs(directionToTarget.x) > 0.1f)
        {
            _currentDirection = directionToTarget.x > 0 ? 1 : -1;
            UpdateDirection(_currentDirection);
        }

        UpdateSpeed();

        pos += directionToTarget * _currentSpeed * Time.deltaTime;
        _rect.anchoredPosition = pos;
        CheckBoundaries(ref pos);

        float minSpeed = _maxSpeed * chargeSpeedRatio;
        if (_stateTimer > _chaseDuration || (_currentSpeed <= minSpeed * 1.05f && _stateTimer > 0.3f))
            EndBaitChasing();
    }

    private void UpdateSpeed()
    {
        if (_currentSpeed < _targetSpeed)
        {
            _currentSpeed += acceleration * ScaleFactor * 2f * Time.deltaTime;
            if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
        }
        if (_currentSpeed > 0)
            _currentSpeed -= dragForce * ScaleFactor * Time.deltaTime;
        float minSpeed = _maxSpeed * chargeSpeedRatio;
        if (_currentSpeed < minSpeed) _currentSpeed = minSpeed;
    }

    private void CheckBoundaries(ref Vector2 pos)
    {
        EnsureComponents();
        if (_boundaryLockTimer > 0) _boundaryLockTimer -= Time.deltaTime;

        float halfWidth = _rect.sizeDelta.x * 0.5f;
        float halfHeight = _rect.sizeDelta.y * 0.5f;

        if (_boundaryLockTimer <= 0)
        {
            if (pos.x - halfWidth < totalAreaRect.xMin + _boundaryMargin)
            {
                pos.x = totalAreaRect.xMin + _boundaryMargin + halfWidth;
                _currentDirection = 1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);
                if (_isChasingBait) EndBaitChasing();
            }
            else if (pos.x + halfWidth > totalAreaRect.xMax - _boundaryMargin)
            {
                pos.x = totalAreaRect.xMax - _boundaryMargin - halfWidth;
                _currentDirection = -1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);
                if (_isChasingBait) EndBaitChasing();
            }
        }

        if (pos.y - halfHeight < totalAreaRect.yMin + _boundaryMargin)
        {
            pos.y = totalAreaRect.yMin + _boundaryMargin + halfHeight;
            if (_isChasingBait) EndBaitChasing();
        }
        else if (pos.y + halfHeight > totalAreaRect.yMax - _boundaryMargin)
        {
            pos.y = totalAreaRect.yMax - _boundaryMargin - halfHeight;
            if (_isChasingBait) EndBaitChasing();
        }
    }

    private void EndBaitChasing()
    {
        if (!_isChasingBait) return;
        _isChasingBait = false;
        _fishState = FishTankFishState.Normal;
        _hasLoggedStart = false;
        ForceCharge();
    }

    public void ResetFishState()
    {
        EnsureComponents();
        _isChasingBait = false;
        _fishState = FishTankFishState.Normal;
        _stateTimer = 0f;
        _hasLoggedStart = false;
        ForceCharge();
    }

    public Vector3 GetBaitTargetPosition() => _baitTargetPosition;
    public string GetCurrentSwimState() => _swimState.ToString();

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void Update()
    {
        if (!gameObject.activeSelf) return;
        if (!_componentsReady) return;

        if (_isChasingBait)
        {
            UpdateBaitChasing();
            return;
        }

        if (_boundaryLockTimer > 0) _boundaryLockTimer -= Time.deltaTime;

        if (_boundaryLockTimer <= 0 && (_swimState == SwimState.Sprinting || _swimState == SwimState.Recovering))
        {
            _directionChangeTimer += Time.deltaTime;
            if (_directionChangeTimer > _directionChangeInterval)
            {
                _currentDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                _directionChangeTimer = 0;
                _directionChangeInterval = UnityEngine.Random.Range(2f, 6f) / _personality;
                UpdateDirection(_currentDirection);

                _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
                if (_isVerticalMoving) _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                ForceCharge();
            }
        }

        Vector2 pos = _rect.anchoredPosition;
        UpdateSwimState(ref pos);

        Vector2 targetPos = pos;
        targetPos.x += _currentSpeed * _currentDirection * Time.deltaTime;
        if (_isVerticalMoving)
        {
            float verticalSpeed = _currentSpeed * verticalSpeedRatio;
            targetPos.y += verticalSpeed * _verticalDirection * Time.deltaTime;
        }

        _targetPosition = targetPos;
        ApplyBoundary(ref targetPos);

        Vector2 delta = targetPos - pos;
        float maxDelta = Mathf.Max(_currentSpeed, 0.5f * ScaleFactor) * Time.deltaTime * 3f;
        if (delta.magnitude > maxDelta) delta = delta.normalized * maxDelta;
        pos += delta;
        _rect.anchoredPosition = pos;
    }

    private void UpdateSwimState(ref Vector2 pos)
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
                    LogDebug("蓄力完成，进入恢复");
                }
                break;

            case SwimState.Recovering:
                _stateTimer += Time.deltaTime;
                ResetShape();

                float minSpeed2 = _maxSpeed * chargeSpeedRatio;
                if (_currentSpeed < _targetSpeed)
                {
                    _currentSpeed += acceleration * ScaleFactor * Time.deltaTime;
                    if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
                }
                if (_stateTimer >= _halfChargeDuration)
                {
                    _swimState = SwimState.Sprinting;
                    _stateTimer = 0f;
                    LogDebug($"进入冲刺，加速度={acceleration:F1}");
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
                    _currentSpeed += acceleration * ScaleFactor * Time.deltaTime;
                    if (_currentSpeed > _targetSpeed) _currentSpeed = _targetSpeed;
                }
                else if (_currentSpeed > _targetSpeed)
                {
                    _currentSpeed -= dragForce * ScaleFactor * Time.deltaTime;
                    if (_currentSpeed < _targetSpeed) _currentSpeed = _targetSpeed;
                }

                if (_stateTimer > 0.3f)
                    _currentSpeed -= dragForce * ScaleFactor * 0.3f * Time.deltaTime;

                if (_currentSpeed < minSpeed3) _currentSpeed = minSpeed3;

                if (_currentSpeed <= minSpeed3 * 1.05f && _stateTimer > 0.3f)
                {
                    LogDebug("力竭，重新蓄力");
                    ResetToCharging();
                }
                if (_stateTimer > _sprintDuration * 1.5f)
                {
                    LogDebug("冲刺超时，强制蓄力");
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
        _maxSpeed = UnityEngine.Random.Range(moveSpeedMin, moveSpeedMax) * _personality * ScaleFactor;
        _currentSpeed = _maxSpeed * chargeSpeedRatio;
        _targetSpeed = _maxSpeed;
        _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
        if (_isVerticalMoving) _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
    }

    private void ApplyBoundary(ref Vector2 targetPos)
    {
        EnsureComponents();
        float halfWidth = _rect.sizeDelta.x * 0.5f;
        float halfHeight = _rect.sizeDelta.y * 0.5f;

        if (_boundaryLockTimer <= 0)
        {
            if (targetPos.x - halfWidth < totalAreaRect.xMin + _boundaryMargin)
            {
                targetPos.x = totalAreaRect.xMin + _boundaryMargin + halfWidth;
                _basePosition.x = targetPos.x;
                _currentDirection = 1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);

                _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
                if (_isVerticalMoving) _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                ForceCharge();
                LogDebug("左边界");
            }
            else if (targetPos.x + halfWidth > totalAreaRect.xMax - _boundaryMargin)
            {
                targetPos.x = totalAreaRect.xMax - _boundaryMargin - halfWidth;
                _basePosition.x = targetPos.x;
                _currentDirection = -1;
                _boundaryLockTimer = BOUNDARY_LOCK_DURATION;
                UpdateDirection(_currentDirection);

                _isVerticalMoving = UnityEngine.Random.value < verticalMoveProbability;
                if (_isVerticalMoving) _verticalDirection = UnityEngine.Random.Range(0, 2) == 0 ? 1 : -1;
                ForceCharge();
                LogDebug("右边界");
            }
        }

        if (targetPos.y - halfHeight < totalAreaRect.yMin + _boundaryMargin)
        {
            targetPos.y = totalAreaRect.yMin + _boundaryMargin + halfHeight;
            _basePosition.y = targetPos.y;
            _verticalDirection = 1;
        }
        else if (targetPos.y + halfHeight > totalAreaRect.yMax - _boundaryMargin)
        {
            targetPos.y = totalAreaRect.yMax - _boundaryMargin - halfHeight;
            _basePosition.y = targetPos.y;
            _verticalDirection = -1;
        }
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    public void UpdateFullScreenSwim(float dirMin, float dirMax) { }
    public void UpdateFullScreenStatic() { }
    public void UpdateBottomSwim(float dirMin, float dirMax) { }
    public void UpdateBottomStatic() { }

    public void Stop()
    {
        EnsureComponents();
        if (_image != null) _image.enabled = false;
        gameObject.SetActive(false);
    }

    public void Release()
    {
        Stop();
    }

    private void OnDestroy()
    {
        Release();
    }

    private void LogDebug(string message)
    {
        if (enableDebugLog) Z_Logger.Log($"[UI_FishTankFish] {_uniqueId} {message}");
    }
}
