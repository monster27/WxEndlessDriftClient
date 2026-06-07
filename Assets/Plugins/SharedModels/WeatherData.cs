using System;
using System.Collections.Generic;

namespace SharedModels
{
    [Serializable]
    public class WeatherData
    {
        public int id;
        public string name = string.Empty;
    }

    [Serializable]
    public class WeatherListWrapper
    {
        public List<WeatherData> weathers = new List<WeatherData>();
    }
}