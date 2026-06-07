using System;
using System.Collections.Generic;

namespace SharedModels
{
    [Serializable]
    public class TimeSlotData
    {
        public int id;
        public string name = string.Empty;
    }

    [Serializable]
    public class TimeSlotListWrapper
    {
        public List<TimeSlotData> timeSlots = new List<TimeSlotData>();
    }

    public enum TimeStatus
    {
        Daytime = 0,
        Sunset = 1,
        Night = 2,
        Dawn = 3
    }
}