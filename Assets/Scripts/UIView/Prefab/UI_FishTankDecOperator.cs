// 路径：Assets/Scripts/UIView/Prefab/UI_FishTankDecOperator.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 装饰操作面板（浮窗）
/// 位置规则：面板的「右下角」对齐装饰的「视觉右上角」
/// 镜像时自动切换为视觉右上角（corners[1]）
/// </summary>
public class UI_FishTankDecOperator : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Image dragHandle;
    [SerializeField] private Button mirrorBtn;
    [SerializeField] private Button removeBtn;
    [SerializeField] private GameObject panelRoot;

    private RectTransform _rect;
    private Canvas _parentCanvas;

    private int _tankId;
    private int _recordId;
    private int _category;
    private int _decorationId;
    private RectTransform _targetDecorationRect;

    private bool _isDragging = false;
    private Vector2 _lastScreenPos;
    private Vector2 _totalDragDelta;

    private bool _isInitialized = false;

    // 回调
    private Action<int, float, float> _onDragMove;    // (recordId, dx, dy)
    private Action<int, float, float> _onDragEnd;     // (recordId, totalDx, totalDy)
    private Action<int> _onMirror;                    // (recordId)
    private Action<int> _onRemove;                    // (recordId)

    public int CurrentRecordId => _recordId;
    public bool IsShowing => panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 初始化
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void EnsureInit()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _rect = GetComponent<RectTransform>();
        if (_rect != null)
        {
            _rect.pivot = new Vector2(1f, 0f);
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
        }

        _parentCanvas = GetComponentInParent<Canvas>();

        if (mirrorBtn != null)
        {
            mirrorBtn.onClick.RemoveAllListeners();
            mirrorBtn.onClick.AddListener(OnMirrorClick);
        }

        if (removeBtn != null)
        {
            removeBtn.onClick.RemoveAllListeners();
            removeBtn.onClick.AddListener(OnRemoveClick);
        }

        // ★ 用 IBeginDragHandler / IDragHandler / IEndDragHandler 替代 EventTrigger
        if (dragHandle != null)
        {
            var proxy = dragHandle.gameObject.GetComponent<DragProxy>();
            if (proxy == null) proxy = dragHandle.gameObject.AddComponent<DragProxy>();
            proxy.Bind(OnDragHandleDown, OnDragHandleDrag, OnDragHandleUp);

            // 保险：确保 dragHandle 有 raycastTarget
            var img = dragHandle.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
        }

        Hide();
    }

    public void SetCallbacks(
        Action<int, float, float> onDragMove,
        Action<int, float, float> onDragEnd,
        Action<int> onMirror,
        Action<int> onRemove)
    {
        _onDragMove = onDragMove;
        _onDragEnd = onDragEnd;
        _onMirror = onMirror;
        _onRemove = onRemove;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 显示 / 隐藏
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    public void ShowForDecoration(int tankId, int recordId, int category, int decorationId, RectTransform decorationRect)
    {
        EnsureInit();

        _tankId = tankId;
        _recordId = recordId;
        _category = category;
        _decorationId = decorationId;
        _targetDecorationRect = decorationRect;

        if (panelRoot != null) panelRoot.SetActive(true);
        else gameObject.SetActive(true);

        UpdatePosition();

        _isDragging = false;
        _totalDragDelta = Vector2.zero;
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        else gameObject.SetActive(false);

        _isDragging = false;
        _totalDragDelta = Vector2.zero;
        _targetDecorationRect = null;
    }

    public void UpdatePosition()
    {
        if (_targetDecorationRect == null || _rect == null) return;

        Vector3[] corners = new Vector3[4];
        _targetDecorationRect.GetWorldCorners(corners);

        // 镜像时视觉右上角是 corners[1]
        bool isFlipped = _targetDecorationRect.localScale.x < 0;
        Vector3 worldVisualRightTop = isFlipped ? corners[1] : corners[2];

        RectTransform parentRect = _rect.parent as RectTransform;
        if (parentRect == null) return;

        Canvas canvas = _parentCanvas;
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldVisualRightTop);

        Vector2 localPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out localPos))
        {
            _rect.anchoredPosition = localPos;
        }
    }

    public void RebindTarget(RectTransform newRect)
    {
        _targetDecorationRect = newRect;
        UpdatePosition();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 拖拽（DragProxy 转调）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnDragHandleDown(PointerEventData ped)
    {
        if (ped == null) return;
        _isDragging = true;
        _lastScreenPos = ped.position;
        _totalDragDelta = Vector2.zero;
    }

    private void OnDragHandleDrag(PointerEventData ped)
    {
        if (!_isDragging) return;
        if (ped == null) return;

        Vector2 currentScreenPos = ped.position;

        RectTransform parentRect = _rect != null ? _rect.parent as RectTransform : null;
        if (parentRect == null) return;

        Camera cam = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _parentCanvas.worldCamera : null;

        Vector2 localCurrent, localLast;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, currentScreenPos, cam, out localCurrent);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, _lastScreenPos, cam, out localLast);

        Vector2 deltaLocal = localCurrent - localLast;

        if (deltaLocal.sqrMagnitude > 0.0001f)
        {
            _totalDragDelta += deltaLocal;
            _onDragMove?.Invoke(_recordId, deltaLocal.x, deltaLocal.y);
            _lastScreenPos = currentScreenPos;
        }
    }

    private void OnDragHandleUp(PointerEventData ped)
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (_totalDragDelta.sqrMagnitude > 0.0001f)
        {
            _onDragEnd?.Invoke(_recordId, _totalDragDelta.x, _totalDragDelta.y);
        }
        _totalDragDelta = Vector2.zero;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 按钮
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnMirrorClick()
    {
        _onMirror?.Invoke(_recordId);
    }

    private void OnRemoveClick()
    {
        _onRemove?.Invoke(_recordId);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 内部辅助：拖动代理
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    /// <summary>
    /// 挂在 dragHandle 上的拖动代理。
    /// 使用 IBeginDragHandler / IDragHandler / IEndDragHandler，
    /// 鼠标离开 dragHandle 后 Unity 依旧持续派发 OnDrag，直到松手。
    /// </summary>
    private class DragProxy : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Action<PointerEventData> _onBegin;
        private Action<PointerEventData> _onDrag;
        private Action<PointerEventData> _onEnd;

        public void Bind(
            Action<PointerEventData> onBegin,
            Action<PointerEventData> onDrag,
            Action<PointerEventData> onEnd)
        {
            _onBegin = onBegin;
            _onDrag = onDrag;
            _onEnd = onEnd;
        }

        public void OnBeginDrag(PointerEventData eventData) => _onBegin?.Invoke(eventData);
        public void OnDrag(PointerEventData eventData) => _onDrag?.Invoke(eventData);
        public void OnEndDrag(PointerEventData eventData) => _onEnd?.Invoke(eventData);
    }
}
