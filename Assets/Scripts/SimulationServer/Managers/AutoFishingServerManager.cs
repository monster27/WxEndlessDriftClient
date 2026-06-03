using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 自动钓鱼服务器管理器
/// 负责管理自动钓鱼的时间间隔和触发逻辑
/// </summary>
public class AutoFishingServerManager
{
    /// <summary>
    /// 钓鱼模式枚举
    /// </summary>
    public enum FishingMode
    {
        Normal,     // 普通钓鱼：每次钓鱼后等待正常间隔
        Continuous  // 连续钓鱼：每次钓鱼后只有0.5秒停滞
    }

    private SimulationServer simulationServer;      // 引用模拟服务器
    private float nextFishingTime = 0f;            // 下次自动钓鱼时间
    private float minFishingInterval = 3f;         // 最小钓鱼间隔（秒）- 普通模式
    private float maxFishingInterval = 20f;        // 最大钓鱼间隔（秒）- 普通模式
    private float lastStruggleTime = 0f;           // 上次挣扎时间
    private bool isAutoFishing = false;             // 是否正在自动钓鱼
    private bool hasNotifiedFishingResult = false; // 是否已通知钓鱼结果
    private bool isPaused = false;                 // 是否处于停滞状态
    private float pauseEndTime = 0f;               // 停滞结束时间
    private float continuousPauseDuration = 1f;     // 连续钓鱼停滞时长（闲置等待时间）
    private float normalPauseDuration = 0.5f;       // 普通钓鱼停滞时长（闲置等待时间）
    private FishingMode currentFishingMode = FishingMode.Normal;  // 当前钓鱼模式

    /// <summary>
    /// 下次钓鱼时间
    /// </summary>
    public float NextFishingTime => nextFishingTime;

    /// <summary>
    /// 上次挣扎时间
    /// </summary>
    public float LastStruggleTime => lastStruggleTime;

    /// <summary>
    /// 是否正在自动钓鱼
    /// </summary>
    public bool IsAutoFishing => isAutoFishing;

    /// <summary>
    /// 当前钓鱼模式
    /// </summary>
    public FishingMode CurrentFishingMode => currentFishingMode;

    /// <summary>
    /// 是否处于停滞状态
    /// </summary>
    public bool IsPaused => isPaused;

    /// <summary>
    /// 停滞剩余时间
    /// </summary>
    public float PauseRemainingTime => isPaused ? Mathf.Max(0f, pauseEndTime - Time.time) : 0f;

    /// <summary>
    /// 连续钓鱼停滞时长
    /// </summary>
    public float ContinuousPauseDuration
    {
        get => continuousPauseDuration;
        set => continuousPauseDuration = Mathf.Max(0.1f, value);
    }

    /// <summary>
    /// 普通钓鱼停滞时长
    /// </summary>
    public float NormalPauseDuration
    {
        get => normalPauseDuration;
        set => normalPauseDuration = Mathf.Max(0.1f, value);
    }

    /// <summary>
    /// 最小钓鱼间隔
    /// </summary>
    public float MinFishingInterval
    {
        get => minFishingInterval;
        set => minFishingInterval = Mathf.Max(0.5f, value);
    }

    /// <summary>
    /// 最大钓鱼间隔
    /// </summary>
    public float MaxFishingInterval
    {
        get => maxFishingInterval;
        set => maxFishingInterval = Mathf.Max(minFishingInterval, value);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="server">模拟服务器引用</param>
    public AutoFishingServerManager(SimulationServer server)
    {
        simulationServer = server;
        SetNextFishingTime();
        Debug.Log("[AutoFishingServerManager] 自动钓鱼服务器管理器初始化完成");
    }

    /// <summary>
    /// 开始自动钓鱼
    /// </summary>
    public void StartAutoFishing()
    {
        isAutoFishing = true;
        hasNotifiedFishingResult = false;
        isPaused = false;
        SetNextFishingTime();
        Debug.Log("[AutoFishingServerManager] 开始自动钓鱼");
    }

    /// <summary>
    /// 停止自动钓鱼
    /// </summary>
    public void StopAutoFishing()
    {
        isAutoFishing = false;
        isPaused = false;
        Debug.Log("[AutoFishingServerManager] 停止自动钓鱼");
    }

    /// <summary>
    /// 切换钓鱼模式
    /// </summary>
    public void ToggleFishingMode()
    {
        if (currentFishingMode == FishingMode.Normal)
        {
            SetFishingMode(FishingMode.Continuous);
        }
        else
        {
            SetFishingMode(FishingMode.Normal);
        }
    }

    /// <summary>
    /// 设置钓鱼模式
    /// </summary>
    public void SetFishingMode(FishingMode mode)
    {
        if (currentFishingMode != mode)
        {
            currentFishingMode = mode;
            string modeName = mode == FishingMode.Normal ? "普通钓鱼" : "连续钓鱼";
            Debug.Log($"[AutoFishingServerManager] 钓鱼模式切换为: {modeName}");

            // 切换模式后，如果正在自动钓鱼，重新设置下次钓鱼时间
            if (isAutoFishing && !hasNotifiedFishingResult)
            {
                SetNextFishingTime();
            }
        }
    }

    /// <summary>
    /// 更新方法
    /// </summary>
    /// <param name="deltaTime">帧时间</param>
    public void Update(float deltaTime)
    {
        if (simulationServer == null)
            return;

        // 检查鱼篓状态，决定是否切换自动钓鱼状态
        CheckFishBagStatus();

        if (isAutoFishing)
        {
            // 处理停滞状态（闲置等待）
            if (isPaused)
            {
                if (Time.time >= pauseEndTime)
                {
                    isPaused = false;
                    Debug.Log("[AutoFishingServerManager] 停滞结束，继续钓鱼");
                }
                return;
            }

            // 检查是否到达钓鱼时间
            if (Time.time >= nextFishingTime && !hasNotifiedFishingResult)
            {
                // 执行钓鱼
                var result = simulationServer.DoFishing();

                if (result != null)
                {
                    // 记录挣扎时间
                    lastStruggleTime = result.struggleTime;

                    // 设置停滞状态
                    float pauseDuration = currentFishingMode == FishingMode.Continuous
                        ? continuousPauseDuration
                        : normalPauseDuration;
                    isPaused = true;
                    pauseEndTime = Time.time + pauseDuration;

                    // 通知客户端钓鱼结果
                    simulationServer.NotifyFishingResultToClient(result);
                    hasNotifiedFishingResult = true;

                    Debug.Log($"[AutoFishingServerManager] 钓鱼完成，挣扎时间: {lastStruggleTime:F1}秒");
                }
            }
        }
    }

    /// <summary>
    /// 钓鱼完成后的处理（动画播放完毕后调用）
    /// </summary>
    public void OnFishingComplete()
    {
        hasNotifiedFishingResult = false;

        // 根据当前模式设置下次钓鱼时间
        SetNextFishingTime();

        Debug.Log($"[AutoFishingServerManager] 钓鱼完成，下次钓鱼: {GetTimeUntilNextFishing():F1}秒后");
    }

    /// <summary>
    /// 设置下次钓鱼时间
    /// </summary>
    private void SetNextFishingTime()
    {
        if (currentFishingMode == FishingMode.Continuous)
        {
            // 连续钓鱼模式：间隔1秒后钓鱼，挣扎动画播放完毕后，再等待1秒
            float interval = continuousPauseDuration;
            nextFishingTime = Time.time + interval;
            Debug.Log($"[AutoFishingServerManager] 连续模式，下次钓鱼间隔: {interval:F1}秒");
        }
        else
        {
            // 普通钓鱼模式：正常的随机间隔（3-20秒）
            float interval = Random.Range(minFishingInterval, maxFishingInterval);
            nextFishingTime = Time.time + interval;
            Debug.Log($"[AutoFishingServerManager] 普通模式，下次钓鱼间隔: {interval:F1}秒");
        }
    }

    /// <summary>
    /// 检查鱼篓状态，切换自动钓鱼/Lazy状态
    /// </summary>
    private void CheckFishBagStatus()
    {
        bool isFull = simulationServer.IsFishBagFull();

        if (isFull && isAutoFishing)
        {
            // 鱼篓已满，切换到Lazy状态（停止自动钓鱼）
            StopAutoFishing();

            // 通过ServerManager通知播放Lazy动画
            if (ServerManager.Instance != null)
            {
                ServerManager.Instance.NotifyPlayLazyAnimation();
                Debug.Log("[AutoFishingServerManager] 鱼篓已满，播放Lazy动画，停止自动钓鱼");
            }
        }
        else if (!isFull && !isAutoFishing && !isPaused)
        {
            // 鱼篓有空间，切换到自动钓鱼状态
            StartAutoFishing();

            // 通过ServerManager通知播放Idle动画
            if (ServerManager.Instance != null)
            {
                ServerManager.Instance.NotifyPlayIdleAnimation();
                Debug.Log("[AutoFishingServerManager] 鱼篓有空间，播放Idle动画，开始自动钓鱼");
            }
        }
    }

    /// <summary>
    /// 获取距离下次钓鱼的剩余时间
    /// </summary>
    /// <returns>剩余时间（秒）</returns>
    public float GetTimeUntilNextFishing()
    {
        if (isPaused)
        {
            return PauseRemainingTime;
        }
        return Mathf.Max(0f, nextFishingTime - Time.time);
    }

    /// <summary>
    /// 立即触发钓鱼（重置计时器）
    /// </summary>
    public void TriggerFishingNow()
    {
        if (isAutoFishing)
        {
            nextFishingTime = Time.time;
            hasNotifiedFishingResult = false;
            isPaused = false;
            Debug.Log("[AutoFishingServerManager] 立即触发钓鱼");
        }
    }

    /// <summary>
    /// 重置通知状态（外部调用，用于动画播放完毕）
    /// </summary>
    public void ResetNotificationState()
    {
        if (hasNotifiedFishingResult)
        {
            OnFishingComplete();
        }
    }
}