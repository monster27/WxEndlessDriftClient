using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 鱼缸中的鱼预制体 - 包含所有游动逻辑
/// </summary>
public class FishTankFishPrefab : MonoBehaviour
{
    [Header("鱼数据")]
    public FishSpeciesData speciesData;
    public FishSpeciesType speciesType;
    public Image fishImage;

    [Header("移动参数")]
    public float moveSpeed = 50f;
    public float floatSpeed = 1f;
    public float floatAmplitude = 5f;

    // 个性系数（每条鱼不同，0.7-1.3）
    private float _personality = 1f;

    // 内部状态
    private RectTransform _rect;
    private float _baseY;
    private float _floatOffset;
    private float _currentDirection = 1f;
    private float _verticalDirection = 1f;
    private float _directionChangeTimer;
    private float _directionChangeInterval = 3f;

    // 速度变化
    private float _currentMoveSpeed = 50f;
    private float _currentVerticalSpeed = 30f;
    private float _speedChangeTimer;
    private float _speedChangeInterval = 3f;

    // 垂直移动速度基础值
    private float _baseVerticalSpeed = 30f;

    // 区域边界
    public Rect moveAreaRect;
    public Rect bottomAreaRect;

    // 预制体固定大小
    private Vector2 _fixedSize;

    // 参数引用
    private float _moveSpeedMin;
    private float _moveSpeedMax;
    private float _verticalSpeedMin;
    private float _verticalSpeedMax;

    public void Init(FishSpeciesData data)
    {
        speciesData = data;
        speciesType = GetSpeciesType(data.type);

        // 生成个性系数（0.7-1.3）
        _personality = Random.Range(0.7f, 1.3f);

        _rect = transform as RectTransform;
        if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();

        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);

        if (fishImage == null) fishImage = GetComponent<Image>();

        _fixedSize = _rect.sizeDelta;
        if (_fixedSize == Vector2.zero)
        {
            _fixedSize = new Vector2(80, 60);
        }

        _floatOffset = Random.Range(0f, 100f);
        _directionChangeInterval = Random.Range(2f, 6f);
        _currentDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
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

    public void SetSprite(Sprite sprite)
    {
        if (fishImage != null && sprite != null)
        {
            fishImage.sprite = sprite;
            float fixedHeight = _fixedSize.y;
            float aspect = sprite.rect.width / sprite.rect.height;
            float width = fixedHeight * aspect;
            _rect.sizeDelta = new Vector2(width, fixedHeight);
        }
    }

    public void SetVerticalSpeed(float speed)
    {
        _baseVerticalSpeed = speed;
        _currentVerticalSpeed = speed * _personality;
    }

    public void SetSpeedRange(float moveMin, float moveMax, float verticalMin, float verticalMax)
    {
        _moveSpeedMin = moveMin;
        _moveSpeedMax = moveMax;
        _verticalSpeedMin = verticalMin;
        _verticalSpeedMax = verticalMax;
    }

    private void UpdateDirection(float currentX, float lastX)
    {
        float absX = Mathf.Abs(_rect.localScale.x);
        if (currentX < lastX)
        {
            _rect.localScale = new Vector3(absX, _rect.localScale.y, _rect.localScale.z);
        }
        else if (currentX > lastX)
        {
            _rect.localScale = new Vector3(-absX, _rect.localScale.y, _rect.localScale.z);
        }
    }

    private Vector2 GetRandomPosInRect(Rect rect)
    {
        float x = Random.Range(rect.xMin + 30f, rect.xMax - 30f);
        float y = Random.Range(rect.yMin + 30f, rect.yMax - 30f);
        return new Vector2(x, y);
    }

    #region 设置行为

    public void SetFullScreenSwim(float speedMin, float speedMax, float amp, float floatMin, float floatMax, float dirMin, float dirMax, float verticalSpeed, Vector2 customPos)
    {
        // 直接使用传入的位置
        _rect.anchoredPosition = customPos;
        _baseY = customPos.y;

        // 应用个性系数
        _directionChangeInterval = Random.Range(dirMin, dirMax) / _personality;

        _currentMoveSpeed = Random.Range(speedMin, speedMax) * _personality;
        floatSpeed = Random.Range(floatMin, floatMax) * _personality;
        floatAmplitude = amp * (0.8f + 0.4f * _personality);
        _currentDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        _floatOffset = Random.Range(0f, 100f);
        _directionChangeTimer = 0;

        _baseVerticalSpeed = verticalSpeed * _personality;
        _currentVerticalSpeed = _baseVerticalSpeed;

        _speedChangeInterval = Random.Range(2f, 6f);
        _speedChangeTimer = 0;

        // 保存参数引用
        SetSpeedRange(speedMin, speedMax, verticalSpeed * 0.5f, verticalSpeed * 1.5f);

        float absX = Mathf.Abs(_rect.localScale.x);
        _rect.localScale = new Vector3(_currentDirection < 0 ? absX : -absX, 1, 1);
        _rect.localRotation = Quaternion.identity;
    }

    public void SetFullScreenStatic()
    {
        Vector2 pos = GetRandomPosInRect(moveAreaRect);
        _rect.anchoredPosition = pos;
        float absX = Mathf.Abs(_rect.localScale.x);
        _currentDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        _rect.localScale = new Vector3(_currentDirection < 0 ? absX : -absX, 1, 1);
        _rect.localRotation = Quaternion.identity;
    }

    public void SetBottomSwim(float speedMin, float speedMax, float amp, float floatMin, float floatMax, float dirMin, float dirMax, float verticalSpeed)
    {
        Vector2 pos = GetRandomPosInRect(bottomAreaRect);
        _rect.anchoredPosition = pos;
        _baseY = pos.y;

        // 应用个性系数
        _directionChangeInterval = Random.Range(dirMin, dirMax) / _personality;

        _currentMoveSpeed = Random.Range(speedMin * 0.5f, speedMax * 0.7f) * _personality;
        floatSpeed = Random.Range(floatMin, floatMax) * _personality * 0.3f;
        floatAmplitude = amp * 0.3f * (0.8f + 0.4f * _personality);
        _currentDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        _floatOffset = Random.Range(0f, 100f);
        _directionChangeTimer = 0;

        _baseVerticalSpeed = verticalSpeed * 0.5f * _personality;
        _currentVerticalSpeed = _baseVerticalSpeed;

        _speedChangeInterval = Random.Range(2f, 6f);
        _speedChangeTimer = 0;

        SetSpeedRange(speedMin * 0.5f, speedMax * 0.7f, verticalSpeed * 0.25f, verticalSpeed * 0.75f);

        float absX = Mathf.Abs(_rect.localScale.x);
        _rect.localScale = new Vector3(_currentDirection < 0 ? absX : -absX, 1, 1);
        _rect.localRotation = Quaternion.identity;
    }

    public void SetBottomStatic()
    {
        Vector2 pos = GetRandomPosInRect(bottomAreaRect);
        _rect.anchoredPosition = pos;
        float absX = Mathf.Abs(_rect.localScale.x);
        _currentDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        _rect.localScale = new Vector3(_currentDirection < 0 ? absX : -absX, 1, 1);
        _rect.localRotation = Quaternion.identity;
    }

    #endregion

    #region 更新逻辑

    public void UpdateFullScreenSwim(float dirMin, float dirMax)
    {
        Vector2 pos = _rect.anchoredPosition;

        // 方向变化
        _directionChangeTimer += Time.deltaTime;
        if (_directionChangeTimer > _directionChangeInterval)
        {
            _currentDirection = Random.Range(0, 2) == 0 ? 1 : -1;
            _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
            _directionChangeTimer = 0;
            _directionChangeInterval = Random.Range(dirMin, dirMax) / _personality;
        }

        // 速度随机变化
        _speedChangeTimer += Time.deltaTime;
        if (_speedChangeTimer > _speedChangeInterval)
        {
            _currentMoveSpeed = Random.Range(_moveSpeedMin, _moveSpeedMax) * _personality;
            _currentVerticalSpeed = Random.Range(_verticalSpeedMin, _verticalSpeedMax);
            _speedChangeTimer = 0;
            _speedChangeInterval = Random.Range(2f, 6f);
        }

        // 水平移动
        float oldX = pos.x;
        pos.x += _currentMoveSpeed * _currentDirection * Time.deltaTime;

        // 垂直移动（上浮/下潜）
        _baseY += _currentVerticalSpeed * _verticalDirection * Time.deltaTime;

        // 正弦波动（鱼鳍摆动）
        _floatOffset += Time.deltaTime * floatSpeed;
        float waveOffset = Mathf.Sin(_floatOffset) * floatAmplitude * 0.3f;

        pos.y = _baseY + waveOffset;

        // 水平边界检测
        if (pos.x < moveAreaRect.xMin + 20f)
        {
            pos.x = moveAreaRect.xMin + 20f;
            _currentDirection = 1;
            _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        }
        else if (pos.x > moveAreaRect.xMax - 20f)
        {
            pos.x = moveAreaRect.xMax - 20f;
            _currentDirection = -1;
            _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        }

        // 垂直边界检测（到达边界时反转垂直方向）
        if (pos.y < moveAreaRect.yMin + 20f)
        {
            pos.y = moveAreaRect.yMin + 20f;
            _baseY = pos.y;
            _verticalDirection = 1; // 上浮
        }
        else if (pos.y > moveAreaRect.yMax - 20f)
        {
            pos.y = moveAreaRect.yMax - 20f;
            _baseY = pos.y;
            _verticalDirection = -1; // 下潜
        }

        UpdateDirection(pos.x, oldX);
        _rect.anchoredPosition = pos;
    }

    public void UpdateFullScreenStatic() { }

    public void UpdateBottomSwim(float dirMin, float dirMax)
    {
        Vector2 pos = _rect.anchoredPosition;

        _directionChangeTimer += Time.deltaTime;
        if (_directionChangeTimer > _directionChangeInterval)
        {
            _currentDirection = Random.Range(0, 2) == 0 ? 1 : -1;
            _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
            _directionChangeTimer = 0;
            _directionChangeInterval = Random.Range(dirMin, dirMax) / _personality;
        }

        _speedChangeTimer += Time.deltaTime;
        if (_speedChangeTimer > _speedChangeInterval)
        {
            _currentMoveSpeed = Random.Range(_moveSpeedMin, _moveSpeedMax) * _personality;
            _currentVerticalSpeed = Random.Range(_verticalSpeedMin, _verticalSpeedMax);
            _speedChangeTimer = 0;
            _speedChangeInterval = Random.Range(2f, 6f);
        }

        float oldX = pos.x;
        pos.x += _currentMoveSpeed * _currentDirection * Time.deltaTime;

        _baseY += _currentVerticalSpeed * _verticalDirection * Time.deltaTime;

        _floatOffset += Time.deltaTime * floatSpeed;
        float waveOffset = Mathf.Sin(_floatOffset) * floatAmplitude * 0.2f;

        pos.y = _baseY + waveOffset;

        // 水平边界检测
        if (pos.x < bottomAreaRect.xMin + 20f)
        {
            pos.x = bottomAreaRect.xMin + 20f;
            _currentDirection = 1;
            _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        }
        else if (pos.x > bottomAreaRect.xMax - 20f)
        {
            pos.x = bottomAreaRect.xMax - 20f;
            _currentDirection = -1;
            _verticalDirection = Random.Range(0, 2) == 0 ? 1 : -1;
        }

        // 垂直边界检测
        if (pos.y < bottomAreaRect.yMin + 15f)
        {
            pos.y = bottomAreaRect.yMin + 15f;
            _baseY = pos.y;
            _verticalDirection = 1;
        }
        else if (pos.y > bottomAreaRect.yMax - 15f)
        {
            pos.y = bottomAreaRect.yMax - 15f;
            _baseY = pos.y;
            _verticalDirection = -1;
        }

        UpdateDirection(pos.x, oldX);
        _rect.anchoredPosition = pos;
    }

    public void UpdateBottomStatic() { }

    #endregion
}
