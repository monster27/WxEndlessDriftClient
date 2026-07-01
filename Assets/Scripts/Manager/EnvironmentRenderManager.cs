using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 环境渲染管理器
/// 负责管理环境图片的切换，依赖 SceneMatManager 和 SceneMatCtrl 进行渲染控制
/// 只控制时段层（Time）和天气层（Weather）
/// </summary>
public class EnvironmentRenderManager : MonoBehaviour
{
    public static EnvironmentRenderManager Instance { get; private set; }

    [Header("环境渲染配置")]
    [Header("时段环境图片 (ID -> Sprite)")]
    public List<TimeEnvironmentEntry> timeEnvironmentEntries = new List<TimeEnvironmentEntry>();
    public Sprite timeDefaultSprite;

    [Header("天气环境图片 (ID -> Sprite)")]
    public List<WeatherEnvironmentEntry> weatherEnvironmentEntries = new List<WeatherEnvironmentEntry>();
    public Sprite weatherDefaultSprite;

    [Header("SceneMatCtrl 引用")]
    [Tooltip("时段层控制器 (Timelmg)")]
    public SceneMatCtrl timeLayerController;
    [Tooltip("天气层控制器 (Weather)")]
    public SceneMatCtrl weatherLayerController;

    private Dictionary<int, Sprite> timeEnvironmentDict = new Dictionary<int, Sprite>();
    private Dictionary<int, Sprite> weatherEnvironmentDict = new Dictionary<int, Sprite>();

    private Sprite currentTimeSprite;
    private Sprite currentWeatherSprite;
    private int currentTimeId = 401;
    private int currentWeatherId = 301;
    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        BuildDictionaries();
        InitializeEnvironment();
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
        // 初始化时段
        Sprite initialTimeSprite = null;
        if (timeEnvironmentDict.TryGetValue(currentTimeId, out initialTimeSprite) && initialTimeSprite != null)
        {
            currentTimeSprite = initialTimeSprite;
            SetTimeSprite(initialTimeSprite);
        }
        else if (timeDefaultSprite != null)
        {
            currentTimeSprite = timeDefaultSprite;
            SetTimeSprite(timeDefaultSprite);
        }
        else if (timeEnvironmentEntries.Count > 0 && timeEnvironmentEntries[0].sprite != null)
        {
            currentTimeSprite = timeEnvironmentEntries[0].sprite;
            SetTimeSprite(timeEnvironmentEntries[0].sprite);
        }
        else
        {
            Debug.LogWarning("[EnvironmentRenderManager] 初始化时没有可用的时段图片");
        }

        // 初始化天气
        Sprite initialWeatherSprite = null;
        if (weatherEnvironmentDict.TryGetValue(currentWeatherId, out initialWeatherSprite) && initialWeatherSprite != null)
        {
            currentWeatherSprite = initialWeatherSprite;
            SetWeatherSprite(initialWeatherSprite);
        }
        else if (weatherDefaultSprite != null)
        {
            currentWeatherSprite = weatherDefaultSprite;
            SetWeatherSprite(weatherDefaultSprite);
        }
        else
        {
            Debug.Log("[EnvironmentRenderManager] 初始化时没有可用的天气图片（天气可以为空）");
        }

        isInitialized = true;
        Debug.Log($"[EnvironmentRenderManager] 环境初始化完成: TimeId={currentTimeId}, WeatherId={currentWeatherId}");
    }

    /// <summary>
    /// 设置时段层图片
    /// </summary>
    public void SetTimeSprite(Sprite sprite)
    {
        if (timeLayerController == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 时段层控制器未设置");
            return;
        }

        if (sprite == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 时段图片为空，不进行渲染");
            return;
        }

        Texture2D texture = SpriteToTexture2D(sprite);
        if (texture != null)
        {
            timeLayerController.SetMainTexture(texture);
            Debug.Log($"[EnvironmentRenderManager] 设置时段层图片: {sprite.name}");
        }
    }

    /// <summary>
    /// 设置时段层图片（带渐变）
    /// </summary>
    public void SetTimeSpriteSmooth(Sprite sprite, float duration = -1f)
    {
        if (timeLayerController == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 时段层控制器未设置");
            return;
        }

        if (sprite == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 时段图片为空，不进行渲染");
            return;
        }

        if (duration < 0) duration = 0.5f;

        Texture2D texture = SpriteToTexture2D(sprite);
        if (texture != null)
        {
            timeLayerController.SetMainTextureSmooth(texture, duration);
            Debug.Log($"[EnvironmentRenderManager] 平滑设置时段层图片: {sprite.name}, 时长: {duration}s");
        }
    }

    /// <summary>
    /// 设置天气层图片
    /// </summary>
    public void SetWeatherSprite(Sprite sprite)
    {
        if (weatherLayerController == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 天气层控制器未设置");
            return;
        }

        if (sprite == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 天气图片为空，不进行渲染");
            return;
        }

        Texture2D texture = SpriteToTexture2D(sprite);
        if (texture != null)
        {
            weatherLayerController.SetMainTexture(texture);
            Debug.Log($"[EnvironmentRenderManager] 设置天气层图片: {sprite.name}");
        }
    }

    /// <summary>
    /// 设置天气层图片（带渐变）
    /// </summary>
    public void SetWeatherSpriteSmooth(Sprite sprite, float duration = -1f)
    {
        if (weatherLayerController == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 天气层控制器未设置");
            return;
        }

        if (sprite == null)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 天气图片为空，不进行渲染");
            return;
        }

        if (duration < 0) duration = 0.5f;

        Texture2D texture = SpriteToTexture2D(sprite);
        if (texture != null)
        {
            weatherLayerController.SetMainTextureSmooth(texture, duration);
            Debug.Log($"[EnvironmentRenderManager] 平滑设置天气层图片: {sprite.name}, 时长: {duration}s");
        }
    }

    /// <summary>
    /// 将Sprite转换为Texture2D
    /// </summary>
    private Texture2D SpriteToTexture2D(Sprite sprite)
    {
        if (sprite == null) return null;
        return sprite.texture;
    }

    /// <summary>
    /// 切换时段环境
    /// </summary>
    public void SwitchTimeEnvironment(int timeId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 尚未初始化，无法切换时段");
            return;
        }

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

        if (timeId == currentTimeId && currentTimeSprite == target) return;

        currentTimeId = timeId;
        currentTimeSprite = target;

        SetTimeSpriteSmooth(target);
        Debug.Log($"[EnvironmentRenderManager] 切换时段环境: ID={timeId}, 名称={(target != null ? target.name : "null")}");
    }

    /// <summary>
    /// 切换天气环境
    /// </summary>
    public void SwitchWeatherEnvironment(int weatherId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 尚未初始化，无法切换天气");
            return;
        }

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

        if (weatherId == currentWeatherId && currentWeatherSprite == target) return;

        currentWeatherId = weatherId;
        currentWeatherSprite = target;

        SetWeatherSpriteSmooth(target);
        Debug.Log($"[EnvironmentRenderManager] 切换天气环境: ID={weatherId}, 名称={(target != null ? target.name : "null")}");
    }

    /// <summary>
    /// 立即切换时段环境（无渐变）
    /// </summary>
    public void SwitchTimeEnvironmentImmediate(int timeId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 尚未初始化，无法切换时段");
            return;
        }

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

        if (timeId == currentTimeId && currentTimeSprite == target) return;

        currentTimeId = timeId;
        currentTimeSprite = target;

        SetTimeSprite(target);
        Debug.Log($"[EnvironmentRenderManager] 立即切换时段环境: ID={timeId}");
    }

    /// <summary>
    /// 立即切换天气环境（无渐变）
    /// </summary>
    public void SwitchWeatherEnvironmentImmediate(int weatherId)
    {
        if (!isInitialized)
        {
            Debug.LogWarning($"[EnvironmentRenderManager] 尚未初始化，无法切换天气");
            return;
        }

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

        if (weatherId == currentWeatherId && currentWeatherSprite == target) return;

        currentWeatherId = weatherId;
        currentWeatherSprite = target;

        SetWeatherSprite(target);
        Debug.Log($"[EnvironmentRenderManager] 立即切换天气环境: ID={weatherId}");
    }

    /// <summary>
    /// 获取当前时段ID
    /// </summary>
    public int GetCurrentTimeId() => currentTimeId;

    /// <summary>
    /// 获取当前天气ID
    /// </summary>
    public int GetCurrentWeatherId() => currentWeatherId;

    /// <summary>
    /// 获取当前时段Sprite
    /// </summary>
    public Sprite GetCurrentTimeSprite() => currentTimeSprite;

    /// <summary>
    /// 获取当前天气Sprite
    /// </summary>
    public Sprite GetCurrentWeatherSprite() => currentWeatherSprite;

    /// <summary>
    /// 重新初始化环境（在场景切换后调用）
    /// </summary>
    public void Reinitialize()
    {
        Debug.Log($"[EnvironmentRenderManager] 重新初始化环境");
        BuildDictionaries();

        // 重新应用当前时段和天气
        if (currentTimeSprite != null)
        {
            SetTimeSprite(currentTimeSprite);
        }

        if (currentWeatherSprite != null)
        {
            SetWeatherSprite(currentWeatherSprite);
        }

        isInitialized = true;
        Debug.Log($"[EnvironmentRenderManager] 重新初始化完成: TimeId={currentTimeId}, WeatherId={currentWeatherId}");
    }
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