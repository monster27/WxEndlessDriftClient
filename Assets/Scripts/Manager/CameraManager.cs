using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 摄像头移动管理器
/// 控制摄像头左右滑动移动
/// </summary>
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [Header("摄像头设置")]
    public Camera targetCamera;

    [Header("移动范围")]
    public float minX = 0f;
    public float maxX = 5.5f;

    [Header("移动参数")]
    [Tooltip("手指滑动距离与摄像头移动距离的比例")]
    public float moveScale = 1f;

    [Tooltip("平滑移动速度")]
    public float smoothSpeed = 10f;

    [Header("移动设置")]
    [Tooltip("启用后，手指右滑摄像头左移，手指左滑摄像头右移")]
    public bool isMirrored = false;

    [Header("UI检测")]
    [Tooltip("是否检测UI点击，启用后点击UI不会触发摄像头移动")]
    public bool checkUI = true;

    // 目标位置
    private float targetX;
    private float currentX;

    // 手指状态
    private bool isDragging = false;
    private float dragStartX;
    private float cameraStartX;
    private float lastTouchX;

    // UI检测缓存
    private PointerEventData pointerEventData;
    private GraphicRaycaster[] graphicRaycasters;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            currentX = targetCamera.transform.position.x;
            targetX = currentX;
        }

        // 缓存所有 GraphicRaycaster 组件
        if (checkUI)
        {
            graphicRaycasters = FindObjectsOfType<GraphicRaycaster>();
        }
    }

    private void Update()
    {
        HandleTouchInput();

        // 平滑移动到目标位置
        SmoothMoveToTarget();
    }

    private void SmoothMoveToTarget()
    {
        if (targetCamera == null) return;

        if (Mathf.Abs(targetCamera.transform.position.x - targetX) > 0.01f)
        {
            float newX = Mathf.Lerp(targetCamera.transform.position.x, targetX, smoothSpeed * Time.deltaTime);
            targetCamera.transform.position = new Vector3(newX, targetCamera.transform.position.y, targetCamera.transform.position.z);
            currentX = newX;
        }
        else
        {
            // 确保精确到达目标位置
            targetCamera.transform.position = new Vector3(targetX, targetCamera.transform.position.y, targetCamera.transform.position.z);
            currentX = targetX;
        }
    }

    private void HandleTouchInput()
    {
        // 如果有触摸输入
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // 检测是否点击到UI
            if (checkUI && IsPointerOverUI(touch.position))
            {
                // 如果点击到UI，终止拖拽
                if (isDragging)
                {
                    isDragging = false;
                }
                return;
            }

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    isDragging = true;
                    dragStartX = touch.position.x;
                    cameraStartX = currentX;
                    lastTouchX = touch.position.x;
                    break;

                case TouchPhase.Moved:
                    if (isDragging && targetCamera != null)
                    {
                        UpdateCameraPosition(touch.position);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isDragging = false;
                    break;
            }
        }
        // 支持鼠标模拟（编辑器测试用）
        else if (Application.isEditor)
        {
            HandleMouseInput();
        }
    }

    private void HandleMouseInput()
    {
        Vector3 mousePosition = Input.mousePosition;

        // 检测是否点击到UI
        if (checkUI && IsPointerOverUI(mousePosition))
        {
            // 如果点击到UI，终止拖拽
            if (isDragging)
            {
                isDragging = false;
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            dragStartX = mousePosition.x;
            cameraStartX = currentX;
            lastTouchX = mousePosition.x;
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateCameraPosition(mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private void UpdateCameraPosition(Vector3 currentPosition)
    {
        float deltaX = currentPosition.x - lastTouchX;
        lastTouchX = currentPosition.x;

        float newX;
        if (isMirrored)
        {
            // 镜像模式：反向移动
            newX = cameraStartX - (currentPosition.x - dragStartX) * moveScale * 0.01f;
        }
        else
        {
            // 正常模式
            newX = cameraStartX + (currentPosition.x - dragStartX) * moveScale * 0.01f;
        }
        newX = Mathf.Clamp(newX, minX, maxX);
        targetX = newX;
    }

    /// <summary>
    /// 检测是否点击到UI
    /// </summary>
    /// <param name="position">屏幕坐标位置</param>
    /// <returns>是否点击到UI</returns>
    private bool IsPointerOverUI(Vector3 position)
    {
        if (!checkUI)
            return false;

        // 使用 EventSystem 检测
        if (EventSystem.current != null)
        {
            pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = position
            };

            // 检测所有 Canvas
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerEventData, results);

            if (results.Count > 0)
            {
                return true;
            }
        }

        // 备用检测：使用 GraphicRaycaster
        if (graphicRaycasters != null && graphicRaycasters.Length > 0)
        {
            pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = position
            };

            foreach (var raycaster in graphicRaycasters)
            {
                if (raycaster == null) continue;

                var results = new System.Collections.Generic.List<RaycastResult>();
                raycaster.Raycast(pointerEventData, results);

                if (results.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 检测是否点击到UI（使用屏幕坐标）
    /// </summary>
    public bool IsPointerOverUI()
    {
        if (!checkUI)
            return false;

        Vector3 position = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;
        return IsPointerOverUI(position);
    }

    /// <summary>
    /// 移动摄像头到指定位置
    /// </summary>
    public void MoveToX(float x)
    {
        targetX = Mathf.Clamp(x, minX, maxX);
        Debug.Log($"[CameraManager] 移动摄像头到 X={targetX}");
    }

    /// <summary>
    /// 移动摄像头到中心位置（X=0）
    /// </summary>
    public void MoveToCenter()
    {
        MoveToX(0f);
    }

    /// <summary>
    /// 移动摄像头到指定位置（平滑）
    /// </summary>
    public void MoveToXSmooth(float x, float speed = -1f)
    {
        targetX = Mathf.Clamp(x, minX, maxX);
        if (speed > 0)
        {
            smoothSpeed = speed;
        }
        Debug.Log($"[CameraManager] 平滑移动摄像头到 X={targetX}");
    }

    /// <summary>
    /// 获取当前摄像头X位置
    /// </summary>
    public float GetCurrentX()
    {
        return currentX;
    }

    /// <summary>
    /// 获取目标摄像头X位置
    /// </summary>
    public float GetTargetX()
    {
        return targetX;
    }

    /// <summary>
    /// 设置移动范围
    /// </summary>
    public void SetRange(float min, float max)
    {
        minX = min;
        maxX = max;
        // 确保当前值在新范围内
        targetX = Mathf.Clamp(targetX, minX, maxX);
        currentX = Mathf.Clamp(currentX, minX, maxX);
    }

    /// <summary>
    /// 切换镜像模式
    /// </summary>
    public void ToggleMirrorMode()
    {
        isMirrored = !isMirrored;
        Debug.Log($"[CameraManager] 镜像模式: {(isMirrored ? "开启" : "关闭")}");
    }

    /// <summary>
    /// 设置镜像模式
    /// </summary>
    public void SetMirrorMode(bool enabled)
    {
        isMirrored = enabled;
        Debug.Log($"[CameraManager] 镜像模式设置为: {(isMirrored ? "开启" : "关闭")}");
    }

    /// <summary>
    /// 设置UI检测开关
    /// </summary>
    public void SetCheckUI(bool enabled)
    {
        checkUI = enabled;
        Debug.Log($"[CameraManager] UI检测: {(checkUI ? "开启" : "关闭")}");
    }
    /// <summary>
    /// 根据镜像模式自动调整摄像头位置
    /// 镜像模式：拉到最大X (maxX)
    /// 非镜像模式：拉到最小X (minX)
    /// </summary>
    public void AdjustPositionByMirrorMode()
    {
        if (isMirrored)
        {
            MoveToX(maxX);
            Debug.Log($"[CameraManager] 镜像模式：摄像头拉到最大位置 X={maxX}");
        }
        else
        {
            MoveToX(minX);
            Debug.Log($"[CameraManager] 非镜像模式：摄像头拉到最小位置 X={minX}");
        }
    }

    /// <summary>
    /// 根据镜像模式自动调整摄像头位置（平滑移动）
    /// </summary>
    public void AdjustPositionByMirrorModeSmooth(float speed = -1f)
    {
        if (isMirrored)
        {
            MoveToXSmooth(maxX, speed);
            Debug.Log($"[CameraManager] 镜像模式：摄像头平滑移动到最大位置 X={maxX}");
        }
        else
        {
            MoveToXSmooth(minX, speed);
            Debug.Log($"[CameraManager] 非镜像模式：摄像头平滑移动到最小位置 X={minX}");
        }
    }
    /// <summary>
    /// 重置摄像头位置
    /// </summary>
    public void ResetPosition()
    {
        targetX = 0f;
        currentX = 0f;
        if (targetCamera != null)
        {
            targetCamera.transform.position = new Vector3(0f, targetCamera.transform.position.y, targetCamera.transform.position.z);
        }
        Debug.Log("[CameraManager] 摄像头位置已重置");
    }

    /// <summary>
    /// 判断是否正在拖拽
    /// </summary>
    public bool IsDragging()
    {
        return isDragging;
    }

    /// <summary>
    /// 强制停止拖拽
    /// </summary>
    public void StopDragging()
    {
        isDragging = false;
    }
}