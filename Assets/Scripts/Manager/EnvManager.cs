using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnvManager : SingletonMono<EnvManager>
{
    public Dictionary<TimeStatus, string> timeNameDic = new Dictionary<TimeStatus, string>();
    public TimeStatus timeStatus;
    
    public int currentWeatherId;
    public string currentWeatherName;
    public Dictionary<int, string> weatherNameDic = new Dictionary<int, string>();
    
    public int currentSceneId = 1; // 默认场景ID
    
    public void Init()
    {
        InitTimeNameDic();
        InitWeatherNameDic();
        
        // 注册服务器事件监听
        RegisterServerEvents();
    }
    
    /// <summary>
    /// 注册服务器事件监听（通过ServerManager中转）
    /// </summary>
    private void RegisterServerEvents()
    {
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_TIME_SLOT_CHANGED, OnTimeSlotChanged);
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_WEATHER_CHANGED, OnWeatherChanged);

        Debug.Log("[EnvManager] 已注册服务器事件监听（通过ServerManager）");
    }
    
    /// <summary>
    /// 时间段变化事件处理
    /// </summary>
    private void OnTimeSlotChanged(Dictionary<string, object> eventData)
    {
        if (eventData.TryGetValue("timeStatus", out object statusObj) &&
            eventData.TryGetValue("timeSlotName", out object nameObj) &&
            eventData.TryGetValue("weatherId", out object weatherObj))
        {
            TimeStatus status = (TimeStatus)statusObj;
            string timeName = nameObj.ToString();
            int weatherId = System.Convert.ToInt32(weatherObj);

            Debug.Log($"[EnvManager] 收到时间段变化事件: {status}, 名称: {timeName}, 天气: {weatherId}");

            UpdateTimeStatus(status, timeName, weatherId);

            int timeSlotId = (int)status + 401;
            EnvironmentRenderManager.Instance?.SwitchTimeEnvironment(timeSlotId);
        }
    }
    
    /// <summary>
    /// 天气变化事件处理
    /// </summary>
    private void OnWeatherChanged(Dictionary<string, object> eventData)
    {
        if (eventData.TryGetValue("weatherId", out object idObj) &&
            eventData.TryGetValue("weatherName", out object nameObj))
        {
            int weatherId = System.Convert.ToInt32(idObj);
            string weatherName = nameObj.ToString();

            Debug.Log($"[EnvManager] 收到天气变化事件: ID={weatherId}, 名称: {weatherName}");

            this.currentWeatherId = weatherId;
            this.currentWeatherName = weatherName;

            if (UIManager.Instance != null && UIManager.Instance.mainGameView != null)
            {
                UIManager.Instance.UpdateMainViewWeather(weatherId, weatherName);
            }

            EnvironmentRenderManager.Instance?.SwitchWeatherEnvironment(weatherId);
        }
    }

    void Update()
    {

    }

    public void InitTimeNameDic()
    {
        timeNameDic.Clear();
        List<TimeSlotData> timeSlots = LoadDataManager.Instance.timeSlots;
        
        foreach (TimeSlotData slot in timeSlots)
        {
            switch (slot.id)
            {
                case 401:
                    timeNameDic[TimeStatus.Earlymorning] = slot.name;
                    break;
                case 402:
                    timeNameDic[TimeStatus.Daytime] = slot.name;
                    break;
                case 403:
                    timeNameDic[TimeStatus.Evening] = slot.name;
                    break;
                case 404:
                    timeNameDic[TimeStatus.LateAtNigh] = slot.name;
                    break;
            }
        }
    } 
    
    public void InitWeatherNameDic()
    {
        weatherNameDic.Clear();
        List<WeatherData> weathers = LoadDataManager.Instance.weathers;
        
        foreach (WeatherData weather in weathers)
        {
            weatherNameDic[weather.id] = weather.name;
        }
    }

    public void UpdateTimeStatus(TimeStatus timeStatus, string timeName, int weatherId)
    {
        this.timeStatus = timeStatus;
        this.currentWeatherId = weatherId;
        this.currentWeatherName = LoadDataManager.Instance.GetWeatherName(weatherId);
        
        if (UIManager.Instance != null && UIManager.Instance.mainGameView != null)
        {
            UIManager.Instance.UpdateMainViewTimee(timeStatus, timeName);
            UIManager.Instance.UpdateMainViewWeather(currentWeatherId, currentWeatherName);
        }
    }
}

public enum TimeStatus
{
    Earlymorning,
    Daytime,
    Evening,
    LateAtNigh
}