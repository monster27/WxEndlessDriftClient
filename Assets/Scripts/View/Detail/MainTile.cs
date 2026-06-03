using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 钓鱼结果显示 Tile
/// 负责显示钓到的鱼或垃圾的图标、名称和重量
/// </summary>
public class MainTile : MonoBehaviour
{
    /// <summary>图标</summary>
    public Image iconImage;
    /// <summary>名称文本</summary>
    public Text nameText;
    /// <summary>重量文本</summary>
    public Text weightText;

    /// <summary>父容器（Canvas 或父 Panel）</summary>
    private RectTransform parentRectTransform;
    /// <summary>自身的 RectTransform</summary>
    private RectTransform rectTransform;
    /// <summary>是否激活</summary>
    private bool isActive = false;

    /// <summary>动画状态枚举</summary>
    private enum TileState
    {
        Idle,           // 空闲
        Showing,        // 显示中（滑入动画）
        Waiting,        // 等待显示
        Hiding          // 隐藏中（滑出动画）
    }

    /// <summary>当前状态</summary>
    private TileState currentState = TileState.Idle;
    /// <summary>起始 Y 位置</summary>
    private float startY;
    /// <summary>结束 Y 位置</summary>
    private float endY;
    /// <summary>动画持续时间</summary>
    [Tooltip("滑入/滑出动画的持续时间（秒）")]
    public float animationDuration = 0.4f;
    /// <summary>等待持续时间</summary>
    [Tooltip("显示结果的等待时间（秒）")]
    public float waitDuration = 2.5f;
    /// <summary>已用时间</summary>
    private float elapsedTime = 0f;
    /// <summary>滑出偏移量（像素）</summary>
    [Tooltip("屏幕外起始位置的偏移量（像素）")]
    public float slideOffset = 200f;
    /// <summary>显示位置占屏幕高度的比例（上方四分之一）</summary>
    [Tooltip("显示位置的 Y 值占屏幕高度的比例（0-1），0.25 表示上方四分之一处")]
    [Range(0f, 1f)]
    public float displayPositionRatio = 0.25f;

    /// <summary>钓鱼数据结构</summary>
    private struct CatchData
    {
        public string itemName;
        public float weight;
        public Sprite icon;

        public CatchData(string name, float w, Sprite i)
        {
            itemName = name;
            weight = w;
            icon = i;
        }
    }

    /// <summary>钓鱼结果队列</summary>
    private Queue<CatchData> catchQueue = new Queue<CatchData>();

    private void Awake()
    {
        // 获取自身的 RectTransform
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("[MainTile] RectTransform component not found!");
        }

        // 获取父容器的 RectTransform
        if (transform.parent != null)
        {
            parentRectTransform = transform.parent.GetComponent<RectTransform>();
        }

        // 如果父容器也没有，尝试找 Canvas
        if (parentRectTransform == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                parentRectTransform = canvas.GetComponent<RectTransform>();
            }
        }
    }

    /// <summary>
    /// 初始化 Tile
    /// </summary>
    public void Init(Vector3 pos)
    {
        // 确保锚点设置正确（居中，锚点在中间）
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        // 初始位置在屏幕外（顶部上方）
        SetOffScreenPosition();

        currentState = TileState.Idle;
        catchQueue.Clear();
        isActive = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 设置初始位置（屏幕外顶部上方）
    /// </summary>
    private void SetOffScreenPosition()
    {
        if (rectTransform != null)
        {
            // 起始位置在顶部上方（slideOffset 像素处）
            // 锚点在中心，所以 y>0 表示在中心上方
            float totalHeight = parentRectTransform != null ? parentRectTransform.rect.height : Screen.height;
            float offScreenY = totalHeight / 2f + slideOffset;
            rectTransform.anchoredPosition = new Vector2(0f, offScreenY);
        }
        else
        {
            transform.position = new Vector2(Screen.width / 2f, Screen.height + slideOffset);
        }
    }

    /// <summary>
    /// 入队钓鱼结果
    /// </summary>
    public void EnqueueCatchResult(string itemName, float weight, Sprite icon)
    {
        CatchData data = new CatchData(itemName, weight, icon);
        catchQueue.Enqueue(data);
        Debug.Log($"[MainTile] 入队成功：{itemName}, 当前队列长度：{catchQueue.Count}");

        if (currentState == TileState.Idle)
        {
            StartNextAnimation();
        }
    }

    /// <summary>
    /// 清空队列
    /// </summary>
    public void ClearQueue()
    {
        catchQueue.Clear();
        Debug.Log("[MainTile] 队列已清空");
    }

    /// <summary>
    /// 开始下一个动画
    /// </summary>
    private void StartNextAnimation()
    {
        if (catchQueue.Count == 0)
        {
            return;
        }

        CatchData data = catchQueue.Dequeue();
        Debug.Log($"[MainTile] 开始播放动画，剩余队列长度：{catchQueue.Count}");

        // 设置图标
        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
        }

        // 设置名称
        if (nameText != null)
            nameText.text = data.itemName;

        // 设置重量
        if (weightText != null)
            weightText.text = data.weight.ToString("F2");

        gameObject.SetActive(true);
        isActive = true;

        // 计算起始位置（屏幕外顶部上方）和结束位置（上方 displayPositionRatio 处）
        // 锚点在中心，所以 y>0 表示在中心上方，y<0 表示在中心下方
        float totalHeight = parentRectTransform != null ? parentRectTransform.rect.height : Screen.height;
        startY = totalHeight / 2f + slideOffset;  // 屏幕外顶部
        // displayPositionRatio = 0.2 表示从顶部向下 20% 的位置
        endY = (totalHeight / 2f) - (totalHeight * displayPositionRatio);  // 从顶部向下 displayPositionRatio 处

        // 设置起始位置
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(0f, startY);
        }
        else
        {
            transform.position = new Vector2(Screen.width / 2f, Screen.height + slideOffset);
        }

        elapsedTime = 0f;
        currentState = TileState.Showing;
    }

    /// <summary>
    /// 更新方法
    /// </summary>
    void Update()
    {
        switch (currentState)
        {
            case TileState.Idle:
                UpdateIdle();
                break;
            case TileState.Showing:
                UpdateShowing();
                break;
            case TileState.Waiting:
                UpdateWaiting();
                break;
            case TileState.Hiding:
                UpdateHiding();
                break;
        }
    }

    /// <summary>
    /// 更新空闲状态
    /// </summary>
    private void UpdateIdle()
    {
        if (catchQueue.Count > 0)
        {
            StartNextAnimation();
        }
        else if (isActive)
        {
            isActive = false;
            SetOffScreenPosition();
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 更新显示状态（滑入动画）
    /// </summary>
    private void UpdateShowing()
    {
        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / animationDuration);

        // 使用平滑插值
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
        float currentY = Mathf.Lerp(startY, endY, smoothProgress);

        // 更新位置（水平始终居中）
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(0f, currentY);
        }
        else
        {
            transform.position = new Vector2(Screen.width / 2f, Screen.height + currentY);
        }

        if (progress >= 1f)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(0f, endY);
            }
            elapsedTime = 0f;
            currentState = TileState.Waiting;
        }
    }

    /// <summary>
    /// 更新等待状态
    /// </summary>
    private void UpdateWaiting()
    {
        elapsedTime += Time.deltaTime;

        if (elapsedTime >= waitDuration)
        {
            // 起始位置是当前显示位置
            startY = endY;
            // 结束位置是顶部上方（屏幕外）
            float totalHeight = parentRectTransform != null ? parentRectTransform.rect.height : Screen.height;
            endY = totalHeight / 2f + slideOffset;

            elapsedTime = 0f;
            currentState = TileState.Hiding;
        }
    }

    /// <summary>
    /// 更新隐藏状态（滑出动画）
    /// </summary>
    private void UpdateHiding()
    {
        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / animationDuration);

        // 使用平滑插值
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
        float currentY = Mathf.Lerp(startY, endY, smoothProgress);

        // 更新位置（水平始终居中）
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(0f, currentY);
        }
        else
        {
            transform.position = new Vector2(Screen.width / 2f, Screen.height + currentY);
        }

        if (progress >= 1f)
        {
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = new Vector2(0f, endY);
            }
            currentState = TileState.Idle;
        }
    }

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }
}
