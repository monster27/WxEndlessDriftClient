using System;
using System.Collections.Generic;

namespace SharedModels
{
    [Serializable]
    public class TimeSlotData
    {
        public int id;
        public string name = string.Empty;
        public string description = string.Empty;
        public int durationMinutes;
    }

    [Serializable]
    public class TimeSlotListWrapper
    {
        public List<TimeSlotData> timeSlots = new List<TimeSlotData>();
    }

    public enum TimeStatus
    {
        Earlymorning = 0,
        Daytime = 1,
        Evening = 2,
        LateAtNigh = 3
    }
}