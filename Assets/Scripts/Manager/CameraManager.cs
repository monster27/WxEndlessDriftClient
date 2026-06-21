using UnityEngine;

/// <summary>
/// 摄像头移动管理器
/// 控制摄像头左右滑动移动
/// </summary>
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    // 摄像头引用
    public Camera targetCamera;

    // 移动范围
    public float minX = 0f;
    public float maxX = 5.5f;

    // 移动速度系数（手指滑动距离与摄像头移动距离的比例）
    public float moveScale = 1f;

    // 平滑移动速度
    public float smoothSpeed = 10f;

    // 镜像移动 - 当启用时，手指滑动方向与摄像头移动方向相反
    [Header("移动设置")]
    [Tooltip("启用后，手指右滑摄像头左移，手指左滑摄像头右移")]
    public bool isMirrored = false;

    // 目标位置
    private float targetX;

    // 手指状态
    private bool isDragging = false;
    private float dragStartX;
    private float cameraStartX;
    private float lastTouchX;

    // 当前摄像头位置
    private float currentX;

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
    }

    private void Update()
    {
        HandleTouchInput();

        // 平滑移动到目标位置
        if (targetCamera != null && Mathf.Abs(targetCamera.transform.position.x - targetX) > 0.01f)
        {
            float newX = Mathf.Lerp(targetCamera.transform.position.x, targetX, smoothSpeed * Time.deltaTime);
            targetCamera.transform.position = new Vector3(newX, targetCamera.transform.position.y, targetCamera.transform.position.z);
            currentX = newX;
        }
        else if (targetCamera != null)
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

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    // 手指按下，记录初始位置
                    isDragging = true;
                    dragStartX = touch.position.x;
                    cameraStartX = currentX;
                    lastTouchX = touch.position.x;
                    break;

                case TouchPhase.Moved:
                    // 手指移动，摄像头跟随移动
                    if (isDragging && targetCamera != null)
                    {
                        float deltaX = touch.position.x - lastTouchX;
                        lastTouchX = touch.position.x;

                        // 根据镜像设置决定移动方向
                        float direction = isMirrored ? 1f : -1f;
                        float moveAmount = direction * deltaX * moveScale * 0.01f;
                        float newX = cameraStartX + (touch.position.x - dragStartX) * moveScale * 0.01f;

                        // 如果是镜像模式，反转移动方向
                        if (isMirrored)
                        {
                            newX = cameraStartX - (touch.position.x - dragStartX) * moveScale * 0.01f;
                        }

                        // 限制范围
                        newX = Mathf.Clamp(newX, minX, maxX);
                        targetX = newX;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    // 手指抬起
                    isDragging = false;
                    break;
            }
        }
        // 支持鼠标模拟（编辑器测试用）
        else if (Application.isEditor)
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                dragStartX = Input.mousePosition.x;
                cameraStartX = currentX;
                lastTouchX = Input.mousePosition.x;
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                float deltaX = Input.mousePosition.x - lastTouchX;
                lastTouchX = Input.mousePosition.x;

                float newX;
                if (isMirrored)
                {
                    // 镜像模式：反向移动
                    newX = cameraStartX - (Input.mousePosition.x - dragStartX) * moveScale * 0.01f;
                }
                else
                {
                    // 正常模式
                    newX = cameraStartX + (Input.mousePosition.x - dragStartX) * moveScale * 0.01f;
                }
                newX = Mathf.Clamp(newX, minX, maxX);
                targetX = newX;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
        }
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
    /// 获取当前摄像头X位置
    /// </summary>
    public float GetCurrentX()
    {
        return currentX;
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
}