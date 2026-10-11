using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 消消乐单个格子预制体
/// 
/// 事件：
///   - PointerDown：按下，通知 View 选中
///   - PointerExit：指针移出该格子，通知 View 判断方向（若位移超阈值，立即触发交换）
///   - PointerUp：松开，通知 View（用于"轻点"判断）
/// 
/// 注意：
///   - 位置由 GridLayoutGroup 控制，本脚本不修改位置
///   - 不实现 IBeginDragHandler 等，避免跟手
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

    private int _value;
    private Action<UI_GameMatch3GridPrefab> _onPointerDown;
    private Action<UI_GameMatch3GridPrefab, Vector2> _onPointerExit;
    private Action<UI_GameMatch3GridPrefab, Vector2> _onPointerUp;

    private Vector2 _pointerDownScreenPos;
    private bool _isPressed;          // 是否处于按下状态（还没结束操作）

    private Coroutine _highlightCoroutine;

    public RectTransform Rect => transform as RectTransform;
    public int Value => _value;
    public bool IsPressed => _isPressed;

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
        if (overlayImage != null) overlayImage.gameObject.SetActive(false);
    }

    public void SetValue(int value, Sprite icon, Sprite overlay, Sprite magicBird)
    {
        _value = value;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        if (overlayImage != null)
        {
            bool hasOverlay = overlay != null;
            overlayImage.sprite = overlay;
            overlayImage.gameObject.SetActive(hasOverlay);
        }
    }

    // ============================================================
    // 选中 / 高亮
    // ============================================================

    public void SetSelectSprite(Sprite selectSprite)
    {
        if (selectImage == null) return;
        if (selectSprite != null) selectImage.sprite = selectSprite;
    }

    public void SetSelected(bool selected)
    {
        if (selectImage == null) return;
        selectImage.gameObject.SetActive(selected);
    }

    public void SetHighlight(bool on, Sprite highlightSprite)
    {
        if (highlightImage == null) return;

        if (_highlightCoroutine != null)
        {
            StopCoroutine(_highlightCoroutine);
            _highlightCoroutine = null;
        }

        if (!on)
        {
            highlightImage.gameObject.SetActive(false);
            return;
        }

        if (highlightSprite != null) highlightImage.sprite = highlightSprite;
        highlightImage.gameObject.SetActive(true);
        _highlightCoroutine = StartCoroutine(BreathRoutine());
    }

    private IEnumerator BreathRoutine()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime;
            float alpha = 0.3f + 0.5f * Mathf.Abs(Mathf.Sin(t * 2.5f));
            var c = highlightImage.color;
            highlightImage.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }
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
        // 只要移出就通知 View，由 View 判断是否需要触发交换
        _onPointerExit?.Invoke(this, delta);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isPressed)
        {
            // 已经被 OnPointerExit 结束过，忽略
            return;
        }

        _isPressed = false;
        Vector2 delta = eventData.position - _pointerDownScreenPos;
        _onPointerUp?.Invoke(this, delta);
    }

    /// <summary>
    /// 外部调用：强制结束按下状态（例如 View 判定交换已触发）
    /// </summary>
    public void ForceEndPress()
    {
        _isPressed = false;
    }

    // ============================================================
    // 生命周期
    // ============================================================

    private void OnDestroy()
    {
        if (clickBtn != null) clickBtn.onClick.RemoveAllListeners();
        if (_highlightCoroutine != null) StopCoroutine(_highlightCoroutine);
    }
}
