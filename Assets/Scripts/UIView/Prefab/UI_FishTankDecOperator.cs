// 路径：Assets/Scripts/UIView/Prefab/UI_FishTankDecOperator.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 装饰操作面板（浮窗）
/// 位置规则：面板的「右下角」对齐装饰的「右上角」
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
            // 右下角对齐装饰右上角 → pivot 设在右下角
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

        if (dragHandle != null)
        {
            var trigger = dragHandle.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = dragHandle.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            AddEventTrigger(trigger, EventTriggerType.PointerDown, OnDragHandleDown);
            AddEventTrigger(trigger, EventTriggerType.Drag, OnDragHandleDrag);
            AddEventTrigger(trigger, EventTriggerType.PointerUp, OnDragHandleUp);
            AddEventTrigger(trigger, EventTriggerType.PointerExit, OnDragHandleUp);
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

    /// <summary>
    /// 重新对齐到目标装饰的右上角
    /// </summary>
    public void UpdatePosition()
    {
        if (_targetDecorationRect == null || _rect == null) return;

        // 装饰右上角世界坐标
        Vector3[] corners = new Vector3[4];
        _targetDecorationRect.GetWorldCorners(corners);
        Vector3 worldRightTop = corners[2];

        // 转换到 Operator 父容器本地坐标
        RectTransform parentRect = _rect.parent as RectTransform;
        if (parentRect == null) return;

        Canvas canvas = _parentCanvas;
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;

        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, worldRightTop);

        Vector2 localPos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out localPos))
        {
            _rect.anchoredPosition = localPos;
        }
    }

    /// <summary>
    /// 数据刷新后重新绑定目标（用于重建后继续跟随同一 recordId 的新对象）
    /// </summary>
    public void RebindTarget(RectTransform newRect)
    {
        _targetDecorationRect = newRect;
        UpdatePosition();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 拖拽
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

    private void OnDragHandleDown(BaseEventData data)
    {
        PointerEventData ped = data as PointerEventData;
        if (ped == null) return;
        _isDragging = true;
        _lastScreenPos = ped.position;
        _totalDragDelta = Vector2.zero;
    }

    private void OnDragHandleDrag(BaseEventData data)
    {
        if (!_isDragging) return;
        PointerEventData ped = data as PointerEventData;
        if (ped == null) return;

        Vector2 currentScreenPos = ped.position;

        // 屏幕坐标 → 父容器本地坐标
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

    private void OnDragHandleUp(BaseEventData data)
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

    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action.Invoke);
        trigger.triggers.Add(entry);
    }
}
