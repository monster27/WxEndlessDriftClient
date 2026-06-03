// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 天气系统服务器管理器
/// 负责管理游戏内的天气随机选择和切换
/// </summary>
public class WeatherServerManager
{
    private WeightPool<WeatherData> weatherPool;    // 天气权重池
    private int currentWeatherId;                   // 当前天气ID
    private float updateTimer = 0f;                 // 更新计时器
    private float updateInterval = 60f;             // 更新间隔（秒）

    /// <summary>
    /// 当前天气ID
    /// </summary>
    public int CurrentWeatherId => currentWeatherId;

    /// <summary>
    /// 天气变化事件
    /// </summary>
    public event System.Action<int> OnWeatherChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    public WeatherServerManager()
    {
        InitWeatherPool();
        Debug.Log("[WeatherServerManager] 天气服务器管理器初始化完成");
    }

    /// <summary>
    /// 初始化天气管理器
    /// </summary>
    public void Initialize()
    {
        InitWeatherPool();
        Debug.Log("[WeatherServerManager] 天气管理器初始化完成");
    }

    /// <summary>
    /// 初始化天气权重池
    /// </summary>
    private void InitWeatherPool()
    {
        weatherPool = new WeightPool<WeatherData>();

        if (LoadDataManager.Instance == null || LoadDataManager.Instance.weathers == null)
        {
            Debug.LogError("[WeatherServerManager] 无法加载天气数据");
            return;
        }

        List<WeatherData> weathers = LoadDataManager.Instance.weathers;
        foreach (WeatherData weather in weathers)
        {
            weatherPool.Add(weather, weather.weight);
        }

        // 初始化默认天气
        UpdateWeatherByRandom();
    }

    /// <summary>
    /// 更新方法
    /// </summary>
    /// <param name="deltaTime">帧时间</param>
    public void Update(float deltaTime)
    {
        updateTimer += deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            RandomWeather();
        }
    }

    /// <summary>
    /// 随机更新天气
    /// </summary>
    /// <returns>新的天气ID</returns>
    public int UpdateWeatherByRandom()
    {
        WeatherData selectedWeather = weatherPool.Get();
        if (selectedWeather != null)
        {
            currentWeatherId = selectedWeather.id;
            Debug.Log($"[WeatherServerManager] 天气切换到: ID={currentWeatherId}");
        }
        return currentWeatherId;
    }

    /// <summary>
    /// 随机切换天气
    /// </summary>
    public void RandomWeather()
    {
        int oldWeatherId = currentWeatherId;
        UpdateWeatherByRandom();

        if (currentWeatherId != oldWeatherId)
        {
            OnWeatherChanged?.Invoke(currentWeatherId);
        }
    }

    /// <summary>
    /// 获取当前天气数据
    /// </summary>
    /// <returns>当前天气数据</returns>
    public WeatherData GetCurrentWeatherData()
    {
        if (LoadDataManager.Instance == null || LoadDataManager.Instance.weathers == null)
            return null;

        return LoadDataManager.Instance.weathers.Find(w => w.id == currentWeatherId);
    }
}
*/
