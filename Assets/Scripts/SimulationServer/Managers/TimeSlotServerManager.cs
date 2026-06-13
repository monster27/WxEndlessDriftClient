using System.Collections.Generic;
using UnityEngine;
using SharedModels;

public class TimeSlotServerManager
{
    private List<TimeSlotData> timeSlots;
    private List<int> timeSlotBounds;
    private int totalCycleMinutes = 30;
    private TimeStatus currentTimeStatus = TimeStatus.Daytime;

    public TimeStatus CurrentTimeStatus => currentTimeStatus;

    public event System.Action<TimeStatus> OnTimeSlotChanged;

    public TimeSlotServerManager()
    {
        InitTimeSlots();
        Debug.Log("[TimeSlotServerManager] 时间槽服务器管理器初始化完成");
    }

    public void Initialize()
    {
        InitTimeSlots();
        Debug.Log("[TimeSlotServerManager] 时间槽管理器初始化完成");
    }

    private void InitTimeSlots()
    {
        if (LoadDataManager.Instance == null)
        {
            Debug.LogError("[TimeSlotServerManager] LoadDataManager未找到");
            return;
        }

        timeSlots = LoadDataManager.Instance.timeSlots;
        timeSlotBounds = new List<int>();

        int currentBound = 0;
        foreach (TimeSlotData slot in timeSlots)
        {
            currentBound += slot.durationMinutes;
            timeSlotBounds.Add(currentBound);
        }

        totalCycleMinutes = currentBound;
        UpdateCurrentTimeSlot();
    }

    public void Update(float deltaTime)
    {
        UpdateCurrentTimeSlot();
    }

    public void UpdateCurrentTimeSlot()
    {
        if (timeSlots == null || timeSlotBounds == null)
        {
            InitTimeSlots();
            if (timeSlots == null) return;
        }

        System.DateTime now = System.DateTime.Now;
        int totalMinutes = now.Hour * 60 + now.Minute;
        int cyclePosition = totalMinutes % totalCycleMinutes;

        for (int i = 0; i < timeSlotBounds.Count; i++)
        {
            if (cyclePosition < timeSlotBounds[i])
            {
                TimeSlotData currentSlot = timeSlots[i];
                TimeStatus newTimeStatus = GetTimeStatusFromSlotId(currentSlot.id);

                if (newTimeStatus != currentTimeStatus)
                {
                    currentTimeStatus = newTimeStatus;
                    NotifyTimeSlotChanged(newTimeStatus);
                }
                break;
            }
        }
    }

    public void SwitchTimeSlot()
    {
        if (timeSlots == null || timeSlots.Count == 0)
        {
            InitTimeSlots();
            if (timeSlots == null) return;
        }

        TimeStatus[] statuses = { TimeStatus.Earlymorning, TimeStatus.Daytime, TimeStatus.Evening, TimeStatus.LateAtNigh };
        int currentIndex = System.Array.IndexOf(statuses, currentTimeStatus);
        int nextIndex = (currentIndex + 1) % statuses.Length;

        currentTimeStatus = statuses[nextIndex];
        NotifyTimeSlotChanged(currentTimeStatus);
        Debug.Log($"[TimeSlotServerManager] 手动切换时间槽: {currentTimeStatus}");
    }

    public void ManualSetTimeSlot(TimeStatus status)
    {
        if (currentTimeStatus != status)
        {
            currentTimeStatus = status;
            NotifyTimeSlotChanged(status);
            Debug.Log($"[TimeSlotServerManager] 手动设置时间槽: {currentTimeStatus}");
        }
    }

    private void NotifyTimeSlotChanged(TimeStatus status)
    {
        OnTimeSlotChanged?.Invoke(status);

        var data = new Dictionary<string, object>
        {
            { "timeStatus", (int)status },
            { "timeSlotName", GetCurrentTimeSlotName() }
        };
        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_TIME_SLOT_CHANGED, data);
    }

    private TimeStatus GetTimeStatusFromSlotId(int slotId)
    {
        switch (slotId)
        {
            case 401: return TimeStatus.Earlymorning;
            case 402: return TimeStatus.Daytime;
            case 403: return TimeStatus.Evening;
            case 404: return TimeStatus.LateAtNigh;
            default: return TimeStatus.Daytime;
        }
    }

    public string GetCurrentTimeSlotName()
    {
        foreach (var slot in timeSlots)
        {
            if (GetTimeStatusFromSlotId(slot.id) == currentTimeStatus)
            {
                return slot.name;
            }
        }
        return "未知时段";
    }

    public TimeSlotData GetCurrentTimeSlotData()
    {
        foreach (var slot in timeSlots)
        {
            if (GetTimeStatusFromSlotId(slot.id) == currentTimeStatus)
            {
                return slot;
            }
        }
        return null;
    }
}