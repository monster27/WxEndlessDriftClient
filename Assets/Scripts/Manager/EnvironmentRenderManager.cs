using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 环境渲染管理器
/// 负责管理环境图片的切换和渐变效果
/// </summary>
public class EnvironmentRenderManager : MonoBehaviour
{
    public static EnvironmentRenderManager Instance { get; private set; }

    [Header("环境渲染配置")]
    public Renderer environmentRenderer;
    public Material transitionMaterial;

    [Header("时段环境图片 (ID -> Sprite)")]
    public List<TimeEnvironmentEntry> timeEnvironmentEntries = new List<TimeEnvironmentEntry>();
    public Sprite timeDefaultSprite;

    [Header("天气环境图片 (ID -> Sprite)")]
    public List<WeatherEnvironmentEntry> weatherEnvironmentEntries = new List<WeatherEnvironmentEntry>();
    public Sprite weatherDefaultSprite;

    [Header("渐变设置")]
    public float transitionDuration = 1f;

    [Header("渲染层级设置")]
    public LayerRenderSettings timeLayerSettings = new LayerRenderSettings("时段层", 1000);
    public LayerRenderSettings backgroundLayerSettings = new LayerRenderSettings("背景层", 1100);
    public LayerRenderSettings sceneLayerSettings = new LayerRenderSettings("场景层", 1200);
    public LayerRenderSettings weatherLayerSettings = new LayerRenderSettings("天气层", 1300);

    [Header("层级渲染器列表")]
    public List<RendererData> timeRenderers = new List<RendererData>();
    public List<RendererData> backgroundRenderers = new List<RendererData>();
    public List<RendererData> sceneRenderers = new List<RendererData>();
    public List<RendererData> weatherRenderers = new List<RendererData>();

    private Dictionary<int, Sprite> timeEnvironmentDict = new Dictionary<int, Sprite>();
    private Dictionary<int, Sprite> weatherEnvironmentDict = new Dictionary<int, Sprite>();

    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private Sprite currentSprite;
    private Sprite targetSprite;
    private int currentTimeId = 401;
    private int currentWeatherId = 301;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        BuildDictionaries();
        ApplyRenderQueueSettings();
        InitializeEnvironment();
    }

    private void Update()
    {
        if (isTransitioning)
        {
            UpdateTransition();
        }
    }

    /// <summary>
    /// 应用渲染队列设置
    /// </summary>
    private void ApplyRenderQueueSettings()
    {
        ApplyLayerRenderQueue(timeRenderers, timeLayerSettings);
        ApplyLayerRenderQueue(backgroundRenderers, backgroundLayerSettings);
        ApplyLayerRenderQueue(sceneRenderers, sceneLayerSettings);
        ApplyLayerRenderQueue(weatherRenderers, weatherLayerSettings);
    }

    /// <summary>
    /// 应用特定层级的渲染队列设置
    /// </summary>
    private void ApplyLayerRenderQueue(List<RendererData> rendererDatas, LayerRenderSettings layerSettings)
    {
        for (int i = 0; i < rendererDatas.Count; i++)
        {
            RendererData data = rendererDatas[i];
            if (data.renderer != null && data.renderer.material != null)
            {
                // 基础渲染队列 + 层级偏移 + 单个渲染器偏移
                int finalQueue = layerSettings.baseRenderQueue + layerSettings.offset + data.offset;
                data.renderer.material.renderQueue = finalQueue;
                Debug.Log($"[EnvironmentRenderManager] 设置{layerSettings.layerName}渲染器[{i}]队列: {finalQueue}");
            }
        }
    }

    /// <summary>
    /// 构建ID到Sprite的字典
    /// </summary>
    private void BuildDictionaries()
    {
        timeEnvironmentDict.Clear();
        foreach (var entry in timeEnvironmentEntries)
        {
            if (entry.sprite != null)
            {
                timeEnvironmentDict[entry.id] = entry.sprite;
            }
        }

        weatherEnvironmentDict.Clear();
        foreach (var entry in weatherEnvironmentEntries)
        {
            if (entry.sprite != null)
            {
                weatherEnvironmentDict[entry.id] = entry.sprite;
            }
        }
    }

    /// <summary>
    /// 初始化环境
    /// </summary>
    private void InitializeEnvironment()
    {
        Sprite initialSprite = null;

        if (timeEnvironmentDict.TryGetValue(currentTimeId, out initialSprite) && initialSprite != null)
        {
            SetEnvironmentSprite(initialSprite);
        }
        else if (timeDefaultSprite != null)
        {
            SetEnvironmentSprite(timeDefaultSprite);
        }
        else if (timeEnvironmentEntries.Count > 0 && timeEnvironmentEntries[0].sprite != null)
        {
            SetEnvironmentSprite(timeEnvironmentEntries[0].sprite);
        }
        else
        {
            Debug.LogWarning("[EnvironmentRenderManager] 初始化时没有可用的时段图片");
        }
    }

    /// <summary>
    /// 设置当前环境图片
    /// </summary>
    public void SetEnvironmentSprite(Sprite sprite)
    {
        if (environmentRenderer == null || sprite == null) return;

        currentSprite = sprite;
        environmentRenderer.material.mainTexture = sprite.texture;
    }

    /// <summary>
    /// 切换时段环境
    /// </summary>
    public void SwitchTimeEnvironment(int timeId)
    {
        Sprite target = null;

        if (!timeEnvironmentDict.TryGetValue(timeId, out target) || target == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 时段ID {timeId} 未找到对应图片");
            
            if (timeDefaultSprite != null)
            {
                Debug.LogWarning($"[EnvironmentRenderManager] 使用时段默认图片");
                target = timeDefaultSprite;
            }
            else
            {
                Debug.LogWarning("[EnvironmentRenderManager] 时段默认图片也未设置，不进行渲染");
                return;
            }
        }

        if (timeId == currentTimeId && currentSprite == target) return;

        currentTimeId = timeId;
        StartTransition(target);
        Debug.Log($"[EnvironmentRenderManager] 切换时段环境: ID={timeId}");
    }

    /// <summary>
    /// 切换天气环境
    /// </summary>
    public void SwitchWeatherEnvironment(int weatherId)
    {
        Sprite target = null;

        if (!weatherEnvironmentDict.TryGetValue(weatherId, out target) || target == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 天气ID {weatherId} 未找到对应图片");
            
            if (weatherDefaultSprite != null)
            {
                Debug.LogWarning($"[EnvironmentRenderManager] 使用天气默认图片");
                target = weatherDefaultSprite;
            }
            else
            {
                Debug.LogWarning("[EnvironmentRenderManager] 天气默认图片也未设置，不进行渲染（天气可以没有渲染效果）");
                return;
            }
        }

        if (weatherId == currentWeatherId && currentSprite == target) return;

        currentWeatherId = weatherId;
        StartTransition(target);
        Debug.Log($"[EnvironmentRenderManager] 切换天气环境: ID={weatherId}");
    }

    /// <summary>
    /// 设置特定层级的Sprite
    /// </summary>
    public void SetLayerSprite(LayerType layerType, Sprite sprite)
    {
        List<RendererData> targetRenderers = GetLayerRenderers(layerType);
        if (targetRenderers != null && sprite != null)
        {
            foreach (var rendererData in targetRenderers)
            {
                if (rendererData.renderer != null)
                {
                    rendererData.renderer.material.mainTexture = sprite.texture;
                }
            }
            Debug.Log($"[EnvironmentRenderManager] 设置{layerType}图片");
        }
        else if (sprite == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] {layerType}图片为空，不进行渲染");
        }
    }

    /// <summary>
    /// 获取层级对应的渲染器列表
    /// </summary>
    private List<RendererData> GetLayerRenderers(LayerType layerType)
    {
        switch (layerType)
        {
            case LayerType.Time: return timeRenderers;
            case LayerType.Background: return backgroundRenderers;
            case LayerType.Scene: return sceneRenderers;
            case LayerType.Weather: return weatherRenderers;
            default: return null;
        }
    }

    /// <summary>
    /// 开始渐变过渡
    /// </summary>
    private void StartTransition(Sprite target)
    {
        if (environmentRenderer == null || target == null)
        {
            SetEnvironmentSprite(target);
            return;
        }

        targetSprite = target;

        if (transitionMaterial != null)
        {
            transitionMaterial.SetTexture("_MainTex", currentSprite.texture);
            transitionMaterial.SetTexture("_NextTex", target.texture);
            transitionMaterial.SetFloat("_Transition", 0f);
            environmentRenderer.material = transitionMaterial;
        }
        else
        {
            environmentRenderer.material.mainTexture = target.texture;
        }

        isTransitioning = true;
        transitionTimer = 0f;
    }

    /// <summary>
    /// 更新渐变过渡
    /// </summary>
    private void UpdateTransition()
    {
        transitionTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(transitionTimer / transitionDuration);

        if (transitionMaterial != null)
        {
            transitionMaterial.SetFloat("_Transition", progress);
        }

        if (progress >= 1f)
        {
            CompleteTransition();
        }
    }

    /// <summary>
    /// 完成渐变过渡
    /// </summary>
    private void CompleteTransition()
    {
        isTransitioning = false;
        SetEnvironmentSprite(targetSprite);
        Debug.Log($"[EnvironmentRenderManager] 环境切换完成");
    }

    /// <summary>
    /// 获取当前时段ID
    /// </summary>
    public int GetCurrentTimeId() => currentTimeId;

    /// <summary>
    /// 获取当前天气ID
    /// </summary>
    public int GetCurrentWeatherId() => currentWeatherId;
}

[System.Serializable]
public class TimeEnvironmentEntry
{
    public int id;
    public string name = "时段名称";
    public Sprite sprite;
}

[System.Serializable]
public class WeatherEnvironmentEntry
{
    public int id;
    public string name = "天气名称";
    public Sprite sprite;
}

[System.Serializable]
public class RendererData
{
    public Renderer renderer;
    public int offset;

    public RendererData()
    {
        offset = 0;
    }
}

[System.Serializable]
public class LayerRenderSettings
{
    public string layerName;
    public int baseRenderQueue;
    public int offset;

    public LayerRenderSettings(string name, int baseQueue)
    {
        layerName = name;
        baseRenderQueue = baseQueue;
        offset = 0;
    }
}

public enum LayerType
{
    Time,
    Background,
    Scene,
    Weather
}
