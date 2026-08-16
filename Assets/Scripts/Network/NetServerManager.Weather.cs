using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Logger = Utils.Logger;

public partial class NetServerManager
{
    private int currentWeatherId = 0;
    private string currentWeatherName = "";
    private int currentTimeSlotId = 0;
    private string currentTimeSlotName = "";
    private TimeStatus currentTimeStatus = TimeStatus.Daytime;

    private Dictionary<int, WeatherData> _weatherCache;
    private Dictionary<int, TimeSlotData> _timeSlotCache;
    private bool _weatherDataReady = false;

    // ========== 数据加载（异步，全部从 Addressables） ==========

    private async void EnsureWeatherDataLoaded()
    {
        if (_weatherDataReady) return;
        _weatherDataReady = true;

        // 优先使用 LoadDataManager 已加载的数据
        if (LoadDataManager.Instance != null)
        {
            if (LoadDataManager.Instance.weathers != null && LoadDataManager.Instance.weathers.Count > 0)
            {
                _weatherCache = new Dictionary<int, WeatherData>();
                foreach (var w in LoadDataManager.Instance.weathers)
                    _weatherCache[w.id] = w;
            }
            if (LoadDataManager.Instance.timeSlots != null && LoadDataManager.Instance.timeSlots.Count > 0)
            {
                _timeSlotCache = new Dictionary<int, TimeSlotData>();
                foreach (var t in LoadDataManager.Instance.timeSlots)
                    _timeSlotCache[t.id] = t;
            }
        }

        // 降级：自行从 Addressables 加载
        if (_weatherCache == null || _weatherCache.Count == 0)
            await LoadWeathersFromAddressables();
        if (_timeSlotCache == null || _timeSlotCache.Count == 0)
            await LoadTimeSlotsFromAddressables();
    }

    private async Task LoadWeathersFromAddressables()
    {
        string json = await RWJsonData.LoadJson("JsonData/BaseFramework/weathers");
        if (string.IsNullOrEmpty(json)) return;
        var wrapper = RWJsonData.ParseJson<WeatherListWrapper>(json);
        if (wrapper?.weathers == null) return;

        _weatherCache = new Dictionary<int, WeatherData>();
        foreach (var w in wrapper.weathers)
            _weatherCache[w.id] = w;
        Logger.Log($"[NetServerManager] 从 Addressables 加载天气数据: {_weatherCache.Count} 条");
    }

    private async Task LoadTimeSlotsFromAddressables()
    {
        string json = await RWJsonData.LoadJson("JsonData/BaseFramework/timeSlots");
        if (string.IsNullOrEmpty(json)) return;
        var wrapper = RWJsonData.ParseJson<TimeSlotListWrapper>(json);
        if (wrapper?.timeSlots == null) return;

        _timeSlotCache = new Dictionary<int, TimeSlotData>();
        foreach (var t in wrapper.timeSlots)
            _timeSlotCache[t.id] = t;
        Logger.Log($"[NetServerManager] 从 Addressables 加载时段数据: {_timeSlotCache.Count} 条");
    }

    // ========== 查询接口 ==========

    public string GetWeatherNameById(int weatherId)
    {
        EnsureWeatherDataLoaded();
        if (_weatherCache != null && _weatherCache.TryGetValue(weatherId, out var w))
            return w.name;
        return $"未知天气({weatherId})";
    }

    public string GetTimeSlotNameById(int timeSlotId)
    {
        EnsureWeatherDataLoaded();
        if (_timeSlotCache != null && _timeSlotCache.TryGetValue(timeSlotId, out var t))
            return t.name;
        return $"未知时段({timeSlotId})";
    }

    public string GetWeatherDescription(int weatherId)
    {
        EnsureWeatherDataLoaded();
        if (_weatherCache != null && _weatherCache.TryGetValue(weatherId, out var w))
            return w.description;
        return "";
    }

    public string GetTimeSlotDescription(int timeSlotId)
    {
        EnsureWeatherDataLoaded();
        if (_timeSlotCache != null && _timeSlotCache.TryGetValue(timeSlotId, out var t))
            return t.description;
        return "";
    }

    public int GetWeatherWeight(int weatherId)
    {
        EnsureWeatherDataLoaded();
        if (_weatherCache != null && _weatherCache.TryGetValue(weatherId, out var w))
            return w.weight;
        return 100;
    }

    public int GetTimeSlotDuration(int timeSlotId)
    {
        EnsureWeatherDataLoaded();
        if (_timeSlotCache != null && _timeSlotCache.TryGetValue(timeSlotId, out var t))
            return t.durationMinutes;
        return 10;
    }

    public List<WeatherData> GetAllWeathers()
    {
        EnsureWeatherDataLoaded();
        return _weatherCache != null ? new List<WeatherData>(_weatherCache.Values) : new List<WeatherData>();
    }

    public List<TimeSlotData> GetAllTimeSlots()
    {
        EnsureWeatherDataLoaded();
        return _timeSlotCache != null ? new List<TimeSlotData>(_timeSlotCache.Values) : new List<TimeSlotData>();
    }

    public void FetchCurrentWeather()
    {
        StartCoroutine(FetchGetJson<WeatherResponse>("/api/scene/weather", (response) =>
        {
            if (response != null && response.weatherId > 0)
            {
                currentWeatherId = response.weatherId;
                currentWeatherName = GetWeatherNameById(response.weatherId);
                Debug.Log($"[NetServerManager] 获取当前天气: ID={currentWeatherId}, 名称={currentWeatherName}");

                CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_CLIENT_WEATHER_CHANGED, new Dictionary<string, object>
                {
                    { "weatherId", currentWeatherId },
                    { "weatherName", currentWeatherName }
                });
            }
        }));
    }

    private class WeatherResponse
    {
        public int weatherId;
    }
}
