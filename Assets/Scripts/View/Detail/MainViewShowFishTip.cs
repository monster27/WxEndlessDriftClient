using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 新物品提示组件
/// 当玩家第一次获取某个物品时显示提示
/// 支持排队显示，每个提示显示3秒后消失
/// 点击MaskBtn可立即关闭
/// </summary>
public class MainViewShowFishTip : MonoBehaviour
{
    /// <summary>图标</summary>
    public Image iconImage;
    /// <summary>名称文本</summary>
    public Text nameText;
    /// <summary>遮罩按钮，点击关闭提示</summary>
    public Button maskBtn;

    /// <summary>父容器</summary>
    private RectTransform parentRectTransform;
    /// <summary>自身的 RectTransform</summary>
    private RectTransform rectTransform;
    /// <summary>是否激活</summary>
    private bool isActive = false;

    /// <summary>动画状态枚举</summary>
    private enum TipState
    {
        Idle,           // 空闲
        Showing,        // 显示中（淡入动画）
        Waiting,        // 等待显示
        Hiding          // 隐藏中（淡出动画）
    }

    /// <summary>当前状态</summary>
    private TipState currentState = TipState.Idle;
    /// <summary>动画持续时间</summary>
    public float animationDuration = 0.3f;
    /// <summary>等待持续时间（显示时间）</summary>
    public float waitDuration = 3f;
    /// <summary>已用时间</summary>
    private float elapsedTime = 0f;

    /// <summary>新物品数据结构</summary>
    private struct NewItemData
    {
        public string itemName;
        public Sprite icon;

        public NewItemData(string name, Sprite i)
        {
            itemName = name;
            icon = i;
        }
    }

    /// <summary>新物品提示队列</summary>
    private Queue<NewItemData> newItemQueue = new Queue<NewItemData>();

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("[MainViewShowFishTip] RectTransform component not found!");
        }

        if (transform.parent != null)
        {
            parentRectTransform = transform.parent.GetComponent<RectTransform>();
        }

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
    /// 初始化
    /// </summary>
    public void Init()
    {
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        // 注册遮罩按钮点击事件
        if (maskBtn != null)
        {
            maskBtn.onClick.RemoveAllListeners();
            maskBtn.onClick.AddListener(OnMaskBtnClick);
        }

        SetInitialPosition();
        currentState = TipState.Idle;
        newItemQueue.Clear();
        isActive = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 设置初始位置（屏幕外或透明）
    /// </summary>
    private void SetInitialPosition()
    {
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(0f, 0f);
        }
    }

    /// <summary>
    /// 入队新物品提示
    /// </summary>
    public void EnqueueNewItem(string itemName, Sprite icon)
    {
        NewItemData data = new NewItemData(itemName, icon);
        newItemQueue.Enqueue(data);
        Debug.Log($"[MainViewShowFishTip] 入队新物品：{itemName}, 当前队列长度：{newItemQueue.Count}");

        if (currentState == TipState.Idle)
        {
            StartNextTip();
        }
    }

    /// <summary>
    /// 清空队列
    /// </summary>
    public void ClearQueue()
    {
        newItemQueue.Clear();
        Debug.Log("[MainViewShowFishTip] 队列已清空");
    }

    /// <summary>
    /// 开始下一个提示
    /// </summary>
    private void StartNextTip()
    {
        if (newItemQueue.Count == 0)
        {
            return;
        }

        NewItemData data = newItemQueue.Dequeue();
        Debug.Log($"[MainViewShowFishTip] 开始显示提示，剩余队列长度：{newItemQueue.Count}");

        // 设置图标
        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
            iconImage.color = new Color(1f, 1f, 1f, 0f);
        }

        // 设置名称
        if (nameText != null)
        {
            nameText.text = data.itemName;
            //nameText.color = new Color(1f, 1f, 1f, 0f);
        }

        gameObject.SetActive(true);
        isActive = true;
        elapsedTime = 0f;
        currentState = TipState.Showing;
    }

    /// <summary>
    /// 更新方法
    /// </summary>
    void Update()
    {
        switch (currentState)
        {
            case TipState.Idle:
                UpdateIdle();
                break;
            case TipState.Showing:
                UpdateShowing();
                break;
            case TipState.Waiting:
                UpdateWaiting();
                break;
            case TipState.Hiding:
                UpdateHiding();
                break;
        }
    }

    /// <summary>
    /// 更新空闲状态
    /// </summary>
    private void UpdateIdle()
    {
        if (newItemQueue.Count > 0)
        {
            StartNextTip();
        }
        else if (isActive)
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 更新显示状态（淡入动画）
    /// </summary>
    private void UpdateShowing()
    {
        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / animationDuration);
        float alpha = Mathf.SmoothStep(0f, 1f, progress);

        if (iconImage != null)
        {
            Color c = iconImage.color;
            c.a = alpha;
            iconImage.color = c;
        }

        if (nameText != null)
        {
            Color c = nameText.color;
            c.a = alpha;
            nameText.color = c;
        }

        if (progress >= 1f)
        {
            elapsedTime = 0f;
            currentState = TipState.Waiting;
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
            elapsedTime = 0f;
            currentState = TipState.Hiding;
        }
    }

    /// <summary>
    /// 更新隐藏状态（淡出动画）
    /// </summary>
    private void UpdateHiding()
    {
        elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsedTime / animationDuration);
        float alpha = Mathf.SmoothStep(1f, 0f, progress);

        if (iconImage != null)
        {
            Color c = iconImage.color;
            c.a = alpha;
            iconImage.color = c;
        }

        if (nameText != null)
        {
            Color c = nameText.color;
            c.a = alpha;
            nameText.color = c;
        }

        if (progress >= 1f)
        {
            currentState = TipState.Idle;
        }
    }

    /// <summary>
    /// 是否激活
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }

    /// <summary>
    /// 遮罩按钮点击回调，立即关闭提示并清空队列
    /// </summary>
    private void OnMaskBtnClick()
    {
        Debug.Log("[MainViewShowFishTip] MaskBtn 点击，立即关闭提示");
        newItemQueue.Clear();
        currentState = TipState.Idle;
        isActive = false;
        gameObject.SetActive(false);
    }
}
