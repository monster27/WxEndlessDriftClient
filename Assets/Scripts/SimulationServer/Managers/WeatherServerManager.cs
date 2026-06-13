using System.Collections.Generic;
using UnityEngine;
using SharedModels;

public class WeatherServerManager
{
    private WeightPool<WeatherData> weatherPool;
    private int currentWeatherId;
    private float updateTimer = 0f;
    private const float UPDATE_INTERVAL = 60f;

    public int CurrentWeatherId => currentWeatherId;

    public event System.Action<int> OnWeatherChanged;

    public WeatherServerManager()
    {
        InitWeatherPool();
        Debug.Log("[WeatherServerManager] 天气服务器管理器初始化完成");
    }

    public void Initialize()
    {
        InitWeatherPool();
        Debug.Log("[WeatherServerManager] 天气管理器初始化完成");
    }

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

        UpdateWeatherByRandom();
    }

    public void Update(float deltaTime)
    {
        updateTimer += deltaTime;
        if (updateTimer >= UPDATE_INTERVAL)
        {
            updateTimer = 0f;
            RandomWeather();
        }
    }

    public int UpdateWeatherByRandom()
    {
        WeatherData selectedWeather = weatherPool.Get();
        if (selectedWeather != null)
        {
            currentWeatherId = selectedWeather.id;
            Debug.Log($"[WeatherServerManager] 天气切换到: ID={currentWeatherId}, 名称={selectedWeather.name}");
        }
        return currentWeatherId;
    }

    public void RandomWeather()
    {
        int oldWeatherId = currentWeatherId;
        UpdateWeatherByRandom();

        if (currentWeatherId != oldWeatherId)
        {
            NotifyWeatherChanged(currentWeatherId);
        }
    }

    public void ManualSetWeather(int weatherId)
    {
        int oldWeatherId = currentWeatherId;
        currentWeatherId = weatherId;
        Debug.Log($"[WeatherServerManager] 手动设置天气: ID={currentWeatherId}");

        if (currentWeatherId != oldWeatherId)
        {
            NotifyWeatherChanged(currentWeatherId);
        }
    }

    private void NotifyWeatherChanged(int weatherId)
    {
        OnWeatherChanged?.Invoke(weatherId);

        var data = new Dictionary<string, object>
        {
            { "weatherId", weatherId },
            { "weatherName", GetCurrentWeatherData()?.name ?? "未知" }
        };
        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_WEATHER_CHANGED, data);
    }

    public WeatherData GetCurrentWeatherData()
    {
        if (LoadDataManager.Instance == null || LoadDataManager.Instance.weathers == null)
            return null;

        return LoadDataManager.Instance.weathers.Find(w => w.id == currentWeatherId);
    }
}