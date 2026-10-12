using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 消消乐单个格子预制体
/// 
/// 性能优化：
///   - SetValue / SetSelected / SetHighlight / SetSelectSprite 全部带变化检测
///   - 呼吸灯用 Update 驱动，不用协程
///   - 提供 ResetForReuse / CleanupForPool 支持对象池
/// </summary>
public class UI_GameMatch3GridPrefab : MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [Header("===== 显示 =====")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image overlayImage;
    [SerializeField] private Image selectImage;
    [SerializeField] private Image highlightImage;
    [SerializeField] private Button clickBtn;

    // ────────── 缓存（避免无意义赋值）──────────
    private int _value = -1;
    private Sprite _currentIcon;
    private Sprite _currentOverlay;
    private Sprite _currentSelectSprite;
    private bool _isSelected;
    private bool _isHighlighted;

    // ────────── 呼吸灯 ──────────
    private float _highlightTimer;

    // ────────── 输入 ──────────
    private Action<UI_GameMatch3GridPrefab> _onPointerDown;
    private Action<UI_GameMatch3GridPrefab, Vector2> _onPointerExit;
    private Action<UI_GameMatch3GridPrefab, Vector2> _onPointerUp;
    private Vector2 _pointerDownScreenPos;
    private bool _isPressed;

    public RectTransform Rect => transform as RectTransform;
    public int Value => _value;
    public bool IsPressed => _isPressed;

    // ============================================================
    // Update：驱动呼吸灯（只在激活时有效）
    // ============================================================

    private void Update()
    {
        if (!_isHighlighted || highlightImage == null) return;

        _highlightTimer += Time.deltaTime;
        float alpha = 0.3f + 0.5f * Mathf.Abs(Mathf.Sin(_highlightTimer * 2.5f));
        var c = highlightImage.color;
        highlightImage.color = new Color(c.r, c.g, c.b, alpha);
    }

    // ============================================================
    // 初始化
    // ============================================================

    public void Init(
        Action<UI_GameMatch3GridPrefab> onPointerDown,
        Action<UI_GameMatch3GridPrefab, Vector2> onPointerExit,
        Action<UI_GameMatch3GridPrefab, Vector2> onPointerUp)
    {
        _onPointerDown = onPointerDown;
        _onPointerExit = onPointerExit;
        _onPointerUp = onPointerUp;

        if (clickBtn != null)
            clickBtn.onClick.RemoveAllListeners();

        SetSelected(false);
        SetHighlight(false, null);
    }

    // ============================================================
    // 对象池接口
    // ============================================================

    /// <summary>
    /// 从池中取出时调用：重置位置 / 缩放 / 缓存 / 视觉
    /// </summary>
    public void ResetForReuse()
    {
        // 位置 / 缩放 / 旋转
        var rect = Rect;
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        // 缓存
        _value = -1;
        _currentIcon = null;
        _currentOverlay = null;
        _currentSelectSprite = null;
        _isSelected = false;
        _isHighlighted = false;
        _highlightTimer = 0f;
        _isPressed = false;

        // 视觉
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.gameObject.SetActive(false);
        }
        if (overlayImage != null)
        {
            overlayImage.sprite = null;
            overlayImage.gameObject.SetActive(false);
        }
        if (selectImage != null)
        {
            selectImage.gameObject.SetActive(false);
        }
        if (highlightImage != null)
        {
            highlightImage.gameObject.SetActive(false);
            var c = highlightImage.color;
            highlightImage.color = new Color(c.r, c.g, c.b, 1f);
        }
    }

    /// <summary>
    /// 回收到池时调用：清理状态
    /// </summary>
    public void CleanupForPool()
    {
        _isPressed = false;
        _isHighlighted = false;
        _highlightTimer = 0f;
    }

    // ============================================================
    // 显示更新（带变化检测）
    // ============================================================

    public void SetValue(int value, Sprite icon, Sprite overlay, Sprite magicBird)
    {
        _value = value;

        if (iconImage != null)
        {
            if (_currentIcon != icon)
            {
                _currentIcon = icon;
                iconImage.sprite = icon;
            }
            bool show = icon != null;
            if (iconImage.gameObject.activeSelf != show)
                iconImage.gameObject.SetActive(show);
        }

        if (overlayImage != null)
        {
            if (_currentOverlay != overlay)
            {
                _currentOverlay = overlay;
                overlayImage.sprite = overlay;
            }
            bool show = overlay != null;
            if (overlayImage.gameObject.activeSelf != show)
                overlayImage.gameObject.SetActive(show);
        }
    }

    public void SetSelectSprite(Sprite sprite)
    {
        if (selectImage == null || sprite == null) return;
        if (_currentSelectSprite == sprite) return;

        _currentSelectSprite = sprite;
        selectImage.sprite = sprite;
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected) return;
        _isSelected = selected;

        if (selectImage != null)
            selectImage.gameObject.SetActive(selected);
    }

    public void SetHighlight(bool on, Sprite sprite)
    {
        if (highlightImage == null) return;
        if (_isHighlighted == on) return;

        _isHighlighted = on;

        if (!on)
        {
            highlightImage.gameObject.SetActive(false);
            return;
        }

        if (sprite != null) highlightImage.sprite = sprite;
        _highlightTimer = 0f;
        highlightImage.gameObject.SetActive(true);
    }

    // ============================================================
    // 指针事件
    // ============================================================

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        _pointerDownScreenPos = eventData.position;
        _onPointerDown?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isPressed) return;
        Vector2 delta = eventData.position - _pointerDownScreenPos;
        _onPointerExit?.Invoke(this, delta);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isPressed) return;

        _isPressed = false;
        Vector2 delta = eventData.position - _pointerDownScreenPos;
        _onPointerUp?.Invoke(this, delta);
    }

    public void ForceEndPress()
    {
        _isPressed = false;
    }

    private void OnDestroy()
    {
        if (clickBtn != null) clickBtn.onClick.RemoveAllListeners();
    }
}
