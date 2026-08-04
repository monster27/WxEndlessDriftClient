using UnityEngine;

/// <summary>
/// 图标高亮移动效果 - 每5秒从起点移动到终点
/// </summary>
public class IconHightLightEffect : MonoBehaviour
{
    [Header("移动参数")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;
    [SerializeField] private GameObject targetObject;
    [SerializeField] private float interval = 5f;
    [SerializeField] private float moveDuration = 1.5f;

    private float timer = 0f;
    private bool isMoving = false;
    private float moveStartTime = 0f;
    private Vector3 startPos;
    private Vector3 endPos;

    void Start()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }

        if (startPoint != null)
        {
            startPos = startPoint.localPosition;
        }
        else
        {
            startPos = new Vector3(-30f, 0f, 0f);
        }

        if (endPoint != null)
        {
            endPos = endPoint.localPosition;
        }
        else
        {
            endPos = new Vector3(30f, 0f, 0f);
        }

        if (targetObject != null)
        {
            targetObject.transform.localPosition = startPos;
        }
    }

    void Update()
    {
        if (isMoving)
        {
            UpdateMove();
        }
        else
        {
            UpdateTimer();
        }
    }

    private void UpdateTimer()
    {
        timer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0f;
            StartMove();
        }
    }

    private void StartMove()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(true);
            targetObject.transform.localPosition = startPos;
        }

        isMoving = true;
        moveStartTime = Time.time;
    }

    private void UpdateMove()
    {
        if (targetObject == null) return;

        float elapsed = Time.time - moveStartTime;
        float progress = Mathf.Clamp01(elapsed / moveDuration);

        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
        targetObject.transform.localPosition = Vector3.Lerp(startPos, endPos, smoothProgress);

        if (progress >= 1f)
        {
            isMoving = false;
            targetObject.SetActive(false);
            targetObject.transform.localPosition = startPos;
        }
    }

    /// <summary>
    /// 手动触发移动效果
    /// </summary>
    public void TriggerMove()
    {
        timer = 0f;
        if (!isMoving)
        {
            StartMove();
        }
    }

    /// <summary>
    /// 设置移动间隔
    /// </summary>
    public void SetInterval(float newInterval)
    {
        interval = Mathf.Max(0.1f, newInterval);
    }

    /// <summary>
    /// 设置起点
    /// </summary>
    public void SetStartPoint(Transform point)
    {
        startPoint = point;
        if (startPoint != null)
        {
            startPos = startPoint.localPosition;
        }
    }

    /// <summary>
    /// 设置终点
    /// </summary>
    public void SetEndPoint(Transform point)
    {
        endPoint = point;
        if (endPoint != null)
        {
            endPos = endPoint.localPosition;
        }
    }

    /// <summary>
    /// 设置目标物体
    /// </summary>
    public void SetTargetObject(GameObject obj)
    {
        targetObject = obj;
    }
}
