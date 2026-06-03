using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 时间槽服务器管理器
/// 负责管理游戏内的时间周期和时间段切换
/// </summary>
public class TimeSlotServerManager
{
    private List<TimeSlotData> timeSlots;           // 时间段数据列表
    private List<int> timeSlotBounds;               // 时间段边界列表
    private int totalCycleMinutes = 30;             // 完整时间周期（分钟）
    private EnvManager envManager;                  // 环境管理器引用
    private TimeStatus currentTimeStatus = TimeStatus.Daytime;  // 当前时间状态

    /// <summary>
    /// 当前时间状态
    /// </summary>
    public TimeStatus CurrentTimeStatus => currentTimeStatus;

    /// <summary>
    /// 时间槽变化事件
    /// </summary>
    public event System.Action<TimeStatus> OnTimeSlotChanged;

    /// <summary>
    /// 构造函数
    /// </summary>
    public TimeSlotServerManager()
    {
        InitTimeSlots();
        Debug.Log("[TimeSlotServerManager] 时间槽服务器管理器初始化完成");
    }

    /// <summary>
    /// 初始化时间槽管理器
    /// </summary>
    /// <param name="envManager">环境管理器引用</param>
    public void Initialize(EnvManager envManager)
    {
        this.envManager = envManager;
        InitTimeSlots();
        Debug.Log("[TimeSlotServerManager] 时间槽管理器初始化完成");
    }

    /// <summary>
    /// 初始化时间段数据
    /// </summary>
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
    }

    /// <summary>
    /// 更新方法
    /// </summary>
    /// <param name="deltaTime">帧时间</param>
    public void Update(float deltaTime)
    {
        UpdateCurrentTimeSlot();
    }

    /// <summary>
    /// 更新当前时间段
    /// </summary>
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
                    OnTimeSlotChanged?.Invoke(newTimeStatus);
                }
                break;
            }
        }
    }

    /// <summary>
    /// 手动切换时间槽
    /// </summary>
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
        OnTimeSlotChanged?.Invoke(currentTimeStatus);
        Debug.Log($"[TimeSlotServerManager] 手动切换时间槽: {currentTimeStatus}");
    }

    /// <summary>
    /// 根据时间段ID获取时间状态
    /// </summary>
    /// <param name="slotId">时间段ID</param>
    /// <returns>时间状态</returns>
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
}