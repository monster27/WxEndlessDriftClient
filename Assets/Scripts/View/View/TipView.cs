using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 提示信息显示系统
/// 管理所有提示信息的显示、动画和对象池
/// </summary>
public class TipView : MonoBehaviour
{
    [Header("配置参数")]
    public GameObject uiTipPrefab;
    public Transform canvasTransform;
    public int poolSize = 10;
    public float animationDuration = 2f;
    public float fadeDuration = 0.5f;
    public float startYPositionRatio = 2f/3f;
    public float endYPositionRatio = 0.9f;
    public float spawnInterval = 0.2f;
    
    private Queue<GameObject> tipPool = new Queue<GameObject>();
    private List<Coroutine> activeAnimations = new List<Coroutine>();
    private Queue<string> messageQueue = new Queue<string>();
    private bool isProcessingQueue = false;
    
    private void Awake()
    {
        InitializePool();
    }
    
    /// <summary>
    /// 初始化对象池
    /// </summary>
    private void InitializePool()
    {
        if (uiTipPrefab == null)
        {
            Debug.LogError("[TipView] uiTipPrefab is not assigned!");
            return;
        }
        
        for (int i = 0; i < poolSize; i++)
        {
            GameObject tip = CreateTipObject();
            tipPool.Enqueue(tip);
        }
    }
    
    /// <summary>
    /// 创建提示对象
    /// </summary>
    private GameObject CreateTipObject()
    {
        GameObject tip = Instantiate(uiTipPrefab, canvasTransform);
        tip.SetActive(false);
        return tip;
    }
    
    /// <summary>
    /// 从对象池获取提示对象
    /// </summary>
    private GameObject GetTipFromPool()
    {
        if (tipPool.Count > 0)
        {
            GameObject tip = tipPool.Dequeue();
            tip.SetActive(true);
            return tip;
        }
        else
        {
            // 对象池已满，创建新对象
            GameObject tip = CreateTipObject();
            tip.SetActive(true);
            return tip;
        }
    }
    
    /// <summary>
    /// 回收提示对象到对象池
    /// </summary>
    private void ReturnTipToPool(GameObject tip)
    {
        tip.SetActive(false);
        tip.transform.SetParent(canvasTransform);
        tip.transform.localScale = Vector3.one;
        
        // 重置文本
        Text text = tip.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = "";
            text.color = new Color(text.color.r, text.color.g, text.color.b, 1f);
        }
        
        // 如果池未满，放回池里
        if (tipPool.Count < poolSize)
        {
            tipPool.Enqueue(tip);
        }
        else
        {
            // 池已满，销毁对象
            Destroy(tip);
        }
    }
    
    /// <summary>
    /// 显示提示信息（外部调用）
    /// </summary>
    public void ShowTip(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("[TipView] Message is empty!");
            return;
        }
        
        messageQueue.Enqueue(message);
        
        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessMessageQueue());
        }
    }
    
    /// <summary>
    /// 处理消息队列
    /// </summary>
    private IEnumerator ProcessMessageQueue()
    {
        isProcessingQueue = true;
        
        while (messageQueue.Count > 0)
        {
            string message = messageQueue.Dequeue();
            GameObject tip = GetTipFromPool();
            
            if (tip != null)
            {
                Coroutine anim = StartCoroutine(PlayTipAnimation(tip, message));
                activeAnimations.Add(anim);
            }
            
            // 控制提示出现的间隔
            yield return new WaitForSeconds(spawnInterval);
        }
        
        isProcessingQueue = false;
    }
    
    /// <summary>
    /// 播放提示动画
    /// </summary>
    private IEnumerator PlayTipAnimation(GameObject tip, string message)
    {
        Text text = tip.GetComponentInChildren<Text>();
        if (text == null)
        {
            Debug.LogError("[TipView] Text component not found in tip prefab!");
            ReturnTipToPool(tip);
            yield break;
        }
        
        // 设置文本
        text.text = message;
        
        RectTransform rectTransform = tip.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError("[TipView] RectTransform component not found in tip prefab!");
            ReturnTipToPool(tip);
            yield break;
        }
        
        // 获取 Canvas 的尺寸（使用父容器的 rect）
        Rect canvasRect = canvasTransform.GetComponent<RectTransform>().rect;
        
        // 计算起始和结束位置（使用锚点居中）
        float startY = canvasRect.height * (startYPositionRatio - 0.5f);
        float endY = canvasRect.height * (endYPositionRatio - 0.5f);
        
        Vector2 startPos = new Vector2(0f, startY);
        Vector2 endPos = new Vector2(0f, endY);
        
        // 设置锚点和pivot确保居中
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = startPos;
        
        float elapsedTime = 0f;
        Color originalColor = text.color;
        
        // 动画主循环
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / animationDuration);
            
            // 移动动画（使用 anchoredPosition）
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, progress);
            
            // 淡入淡出效果
            if (progress < fadeDuration / animationDuration)
            {
                // 淡入
                float fadeProgress = progress * animationDuration / fadeDuration;
                text.color = new Color(originalColor.r, originalColor.g, originalColor.b, fadeProgress);
            }
            else if (progress > (animationDuration - fadeDuration) / animationDuration)
            {
                // 淡出
                float fadeProgress = (1f - progress) * animationDuration / fadeDuration;
                text.color = new Color(originalColor.r, originalColor.g, originalColor.b, fadeProgress);
            }
            
            yield return null;
        }
        
        // 动画结束，回收对象
        ReturnTipToPool(tip);
        activeAnimations.RemoveAll(a => a == null);
    }
    
    /// <summary>
    /// 清除所有提示
    /// </summary>
    public void ClearAllTips()
    {
        // 停止所有动画
        foreach (Coroutine coroutine in activeAnimations)
        {
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
        }
        activeAnimations.Clear();
        
        // 清空消息队列
        messageQueue.Clear();
        
        // 回收所有活动的提示对象
        foreach (Transform child in canvasTransform)
        {
            if (child.GetComponent<Text>() != null && child.gameObject.activeSelf)
            {
                ReturnTipToPool(child.gameObject);
            }
        }
    }
}