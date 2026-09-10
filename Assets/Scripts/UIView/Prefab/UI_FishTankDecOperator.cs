// 路径：Assets/Scripts/UIView/Prefab/UI_FishTankDecOperator.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using static PlayerDataManager;

/// <summary>
/// 装饰操作面板 - 纯UI，所有操作通过事件发送
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

    // 当前选中的装饰信息
    private int _tankId;
    private int _recordId;
    private int _decorationId;
    private int _category;

    // 拖拽相关
    private bool _isDragging = false;
    private Vector2 _lastScreenPos;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _parentCanvas = GetComponentInParent<Canvas>();
        if (_parentCanvas == null)
        {
            Debug.LogError("UI_FishTankDecOperator 必须在 Canvas 下！");
            return;
        }

        if (mirrorBtn != null) mirrorBtn.onClick.AddListener(OnMirrorClick);
        if (removeBtn != null) removeBtn.onClick.AddListener(OnRemoveClick);

        // 拖拽区域事件
        if (dragHandle != null)
        {
            var trigger = dragHandle.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = dragHandle.gameObject.AddComponent<EventTrigger>();

            AddEventTrigger(trigger, EventTriggerType.PointerDown, OnDragHandleDown);
            AddEventTrigger(trigger, EventTriggerType.Drag, OnDragHandleDrag);
            AddEventTrigger(trigger, EventTriggerType.PointerUp, OnDragHandleUp);
            AddEventTrigger(trigger, EventTriggerType.PointerExit, OnDragHandleUp);
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        else gameObject.SetActive(false);

        // 监听选中事件
        CommunicateEvent.Register<SelectDecorationData>(FishTankMessage.SelectDecoration.ToString(), OnSelectDecoration);
        CommunicateEvent.Register(FishTankMessage.HideDecOperator.ToString(), OnHideDecOperator);
    }

    private void OnDestroy()
    {
        if (mirrorBtn != null) mirrorBtn.onClick.RemoveAllListeners();
        if (removeBtn != null) removeBtn.onClick.RemoveAllListeners();
        CommunicateEvent.Unregister<SelectDecorationData>(FishTankMessage.SelectDecoration.ToString(), OnSelectDecoration);
        CommunicateEvent.Unregister(FishTankMessage.HideDecOperator.ToString(), OnHideDecOperator);
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 事件处理
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnSelectDecoration(SelectDecorationData data)
    {
        if (data == null) return;
        _tankId = data.TankId;
        _recordId = data.RecordId;
        _category = data.Category; // 直接从事件获取品类

        // 获取装饰ID（用于展示，可选）
        var info = PlayerDataService.Instance?.GetDecorationInfoByRecordId(_recordId);
        if (info != null)
            _decorationId = info.DecorationId;

        ShowAt(data.ScreenPosition);
    }

    private void OnHideDecOperator()
    {
        Hide();
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 显示/隐藏
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void ShowAt(Vector3 screenPos)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        else gameObject.SetActive(true);

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentCanvas.transform as RectTransform,
            screenPos,
            _parentCanvas.worldCamera,
            out localPoint
        );
        _rect.anchoredPosition = localPoint;

        _isDragging = false;
    }

    private void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        else gameObject.SetActive(false);
        _isDragging = false;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 拖拽控制装饰移动（发送事件）
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnDragHandleDown(BaseEventData data)
    {
        PointerEventData ped = data as PointerEventData;
        if (ped == null) return;
        _isDragging = true;
        _lastScreenPos = ped.position;
    }

    private void OnDragHandleDrag(BaseEventData data)
    {
        if (!_isDragging) return;
        PointerEventData ped = data as PointerEventData;
        if (ped == null) return;

        Vector2 currentScreenPos = ped.position;
        Vector2 deltaScreen = currentScreenPos - _lastScreenPos;

        if (deltaScreen.magnitude > 0.001f)
        {
            var moveData = new MoveDecorationData
            {
                TankId = _tankId,
                RecordId = _recordId,
                DeltaX = deltaScreen.x,
                DeltaY = deltaScreen.y
            };
            CommunicateEvent.Modify(FishTankMessage.MoveDecoration.ToString(), moveData);
            _lastScreenPos = currentScreenPos;
        }
    }

    private void OnDragHandleUp(BaseEventData data)
    {
        _isDragging = false;
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 按钮事件
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void OnMirrorClick()
    {
        var data = new MirrorDecorationData
        {
            TankId = _tankId,
            RecordId = _recordId
        };
        CommunicateEvent.Modify(FishTankMessage.MirrorDecoration.ToString(), data);
    }

    private void OnRemoveClick()
    {
        var data = new RemoveDecorationData
        {
            TankId = _tankId,
            RecordId = _recordId
        };
        CommunicateEvent.Modify(FishTankMessage.RemoveDecoration.ToString(), data);
        Hide(); // 移除后隐藏操作面板
    }

    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    // 辅助
    // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    private void AddEventTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action.Invoke);
        trigger.triggers.Add(entry);
    }
}
