using System.Collections.Generic;

namespace ServerModels
{
    [System.Serializable]
    public class WeatherData
    {
        public int id;
        public string name = string.Empty;
    }

    [System.Serializable]
    public class WeatherListWrapper
    {
        public List<WeatherData> weathers = new List<WeatherData>();
    }
}