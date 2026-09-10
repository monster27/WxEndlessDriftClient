//using UnityEngine;

//public class FishTankDecOperatorCtrl : MonoBehaviour
//{
//    [Header("碰撞器引用")]
//    [SerializeField] private Collider dragAreaCollider;
//    [SerializeField] private Collider mirrorBtnCollider;
//    [SerializeField] private Collider removeBtnCollider;

//    [Header("偏移量")]
//    [SerializeField] private Vector3 offset = new Vector3(0.8f, 0.8f, 0);

//    private FishTankDecManager _manager;
//    private string _currentInstanceId;
//    private bool _isDragging = false;
//    private Vector3 _lastMouseWorldPos;

//    // 固定Z轴（与装饰一致）
//    private float _fixedZ = 0;

//    public void Init(FishTankDecManager manager)
//    {
//        _manager = manager;
//        gameObject.SetActive(false);
//    }

//    public void ShowAt(Vector3 worldPos, string instanceId)
//    {
//        _currentInstanceId = instanceId;
//        _fixedZ = worldPos.z;  // 记录装饰的Z
//        transform.position = worldPos + offset;
//        gameObject.SetActive(true);
//        _isDragging = false;
//    }

//    public void UpdatePosition(Vector3 worldPos)
//    {
//        if (!gameObject.activeSelf) return;
//        transform.position = worldPos + offset;
//    }

//    public void Hide()
//    {
//        gameObject.SetActive(false);
//        _currentInstanceId = null;
//        _isDragging = false;
//    }

//    private void OnMouseDown()
//    {
//        // 直接用 OnMouseDown 检测点击，无需射线检测
//        // 但需要区分点击区域，这里利用碰撞器检测
//        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//        RaycastHit hit;
//        if (Physics.Raycast(ray, out hit))
//        {
//            if (hit.collider == dragAreaCollider)
//            {
//                OnDragAreaDown();
//            }
//            else if (hit.collider == mirrorBtnCollider)
//            {
//                OnMirrorClick();
//            }
//            else if (hit.collider == removeBtnCollider)
//            {
//                OnRemoveClick();
//            }
//        }
//    }

//    private void OnDragAreaDown()
//    {
//        if (string.IsNullOrEmpty(_currentInstanceId) || _manager == null) return;
//        _isDragging = true;
//        _lastMouseWorldPos = GetMouseWorldPos();
//    }

//    private void OnMouseDrag()
//    {
//        if (!_isDragging || string.IsNullOrEmpty(_currentInstanceId) || _manager == null) return;
//        Vector3 currentWorldPos = GetMouseWorldPos();
//        Vector3 delta = currentWorldPos - _lastMouseWorldPos;
//        delta.z = 0; // 只允许XY移动
//        if (delta.magnitude > 0.001f)
//        {
//            _manager.MoveDecoration(_currentInstanceId, delta);
//            _lastMouseWorldPos = currentWorldPos;
//        }
//    }

//    private void OnMouseUp()
//    {
//        if (_isDragging && !string.IsNullOrEmpty(_currentInstanceId) && _manager != null)
//        {
//            _manager.DropDecoration(_currentInstanceId);
//        }
//        _isDragging = false;
//    }

//    private void OnMirrorClick()
//    {
//        if (!string.IsNullOrEmpty(_currentInstanceId) && _manager != null)
//            _manager.MirrorDecoration(_currentInstanceId);
//    }

//    private void OnRemoveClick()
//    {
//        if (!string.IsNullOrEmpty(_currentInstanceId) && _manager != null)
//            _manager.RemoveDecoration(_currentInstanceId);
//        Hide();
//    }

//    private Vector3 GetMouseWorldPos()
//    {
//        // ★ 关键修正：Z轴使用摄像机到装饰平面的距离
//        Vector3 screenPos = Input.mousePosition;
//        // 计算摄像机到固定Z平面的距离（假设摄像机朝向Z负方向）
//        screenPos.z = Mathf.Abs(Camera.main.transform.position.z - _fixedZ);
//        return Camera.main.ScreenToWorldPoint(screenPos);
//    }
//}
