// UI_FishTankBait.cs
using UnityEngine;
using UnityEngine.UI;

public class UI_FishTankBait : MonoBehaviour
{
    private FishTankMainPanel _manager;
    private Rect _totalRect;
    private float _fallSpeedRatio;
    private bool _isFalling = true;
    private bool _isTriggered = false;
    private bool _isActive = false;

    private RectTransform _rect;
    private Image _image;

    private const float LingerDuration = 3f;
    private float _lingerTimer = 0f;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.pivot = new Vector2(0.5f, 0.5f);

        _image = GetComponent<Image>();
        if (_image == null) _image = gameObject.AddComponent<Image>();
        _image.raycastTarget = false;
    }

    public void Init(FishTankMainPanel manager, Rect totalRect, float fallSpeedRatio, float scale)
    {
        _manager = manager;
        _totalRect = totalRect;
        _fallSpeedRatio = fallSpeedRatio;
        _isFalling = true;
        _isTriggered = false;
        _isActive = false;

        transform.localScale = Vector3.one;
        _rect.localScale = Vector3.one;
    }

    public void ResetBait(Vector3 anchoredPos)
    {
        _rect.anchoredPosition = new Vector2(anchoredPos.x, anchoredPos.y);
        _isFalling = true;
        _isTriggered = false;
        _isActive = true;
        _lingerTimer = 0f;
    }

    public void Deactivate()
    {
        _isActive = false;
        _isTriggered = true;
    }

    public void UpdateBait()
    {
        if (_isTriggered || !_isActive) return;

        Rect currentRect = _manager != null ? _manager.TotalRect : _totalRect;
        if (currentRect.height <= 0.1f) return;

        if (_isFalling)
        {
            Vector2 pos = _rect.anchoredPosition;
            float fallDistancePerSecond = currentRect.height * _fallSpeedRatio;
            pos.y -= fallDistancePerSecond * Time.deltaTime;

            float bottomLimit = currentRect.yMin + 10f;
            if (pos.y <= bottomLimit)
            {
                pos.y = bottomLimit;
                _isFalling = false;
                _lingerTimer = 0f;
            }
            _rect.anchoredPosition = pos;
        }
        else
        {
            _lingerTimer += Time.deltaTime;
            if (_lingerTimer >= LingerDuration)
            {
                _manager?.ReturnBait(gameObject);
            }
        }
    }

    public bool IsActive => _isActive;
    public bool IsFalling => _isFalling;

    /// <summary>世界坐标（用于跨容器比较）</summary>
    public Vector3 GetWorldPosition() => _rect.position;

    /// <summary>当前所在容器的本地坐标（用于显示/下落）</summary>
    public Vector2 GetAnchoredPosition() => _rect.anchoredPosition;
}
