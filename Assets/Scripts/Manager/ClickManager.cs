// ==================== ClickManager.cs ====================
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 点击管理器 - 单例
/// </summary>
public class ClickManager : MonoBehaviour
{
    private static ClickManager _instance;
    public static ClickManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ClickManager");
                _instance = go.AddComponent<ClickManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get { return _isEnabled; }
        set
        {
            _isEnabled = value;
            Debug.Log($"[ClickManager] 功能已{(value ? "启用" : "禁用")}");
        }
    }

    /// <summary>
    /// 检查是否点击到了UI
    /// </summary>
    /// <returns>如果点击到UI返回true，否则返回false</returns>
    public bool IsPointerOverUI()
    {
        // 检查是否点击到了UI元素
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[ClickManager] EventSystem is null");
            return false;
        }

        // 检查鼠标是否在UI上
        if (EventSystem.current.IsPointerOverGameObject())
        {
            Debug.Log("[ClickManager] 点击到了UI，忽略物体点击");
            return true;
        }

        // 对于触摸设备，检查触摸点是否在UI上
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                Debug.Log("[ClickManager] 触摸到了UI，忽略物体点击");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 物体被点击时的处理
    /// </summary>
    public void OnObjectClicked(ClickableObject clickable)
    {
        if (!_isEnabled)
        {
            Debug.Log("[ClickManager] 功能已禁用，忽略点击");
            return;
        }

        // 检查是否点击到了UI，如果是则忽略
        if (IsPointerOverUI())
        {
            return;
        }

        Debug.Log($"[ClickManager] 点击物体: {clickable.objData} (类型: {clickable.objectType})");

        switch (clickable.objectType)
        {
            case ClickableType.Player:
                UIManager.Instance.OpenEquipment();
                break;
            case ClickableType.NestBaitsPlacement:
                HandleNestBaitsPlacementClick(clickable.gameObject);
                break;
            case ClickableType.FishBag:
                UIManager.Instance.OpenFishBag();
                break;
        }
    }

    private void HandleNestBaitsPlacementClick(GameObject clickedObject)
    {
        Debug.Log("[ClickManager] HandleNestBaitsPlacementClick 被调用");

        int currentBait = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, 0);
        Debug.Log($"[ClickManager] 当前窝料: {currentBait}");

        if (currentBait > 0)
        {
            CommunicateEvent.Modify(CommunicateEvent.EVENT_CONSUME_BAIT_AND_ENTER_CONTINUOUS_MODE);
            Debug.Log("[ClickManager] 消耗窝料并进入连续模式（30秒）");

            // 获取点击位置的屏幕坐标
            if (clickedObject != null)
            {
                Vector3 worldPos = clickedObject.transform.position;
                CommunicateEvent.Modify<Vector3>(CommunicateEvent.EVENT_SHOW_BAIT_COUNTDOWN_AT_POSITION, worldPos);
                Debug.Log($"[ClickManager] 发送窝料倒计时显示位置: {worldPos}");
            }
        }
        else
        {
            Debug.Log("[ClickManager] 窝料不足，无法使用");
            UIManager.ShowMessage("窝料不足");
        }
    }
}