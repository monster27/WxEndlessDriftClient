using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
//using SharedModels;
//using Z_Logger = Utils.Z_Logger;
using System;

public partial class NetServerManager
{
    private float heartbeatTimer = 0f;
    private int missedHeartbeats = 0;
    private long lastServerTime = 0;
    private Coroutine heartbeatCoroutine;
    private const float HEARTBEAT_INTERVAL = 10f;
    private const float HEARTBEAT_TIMEOUT = 30f;

    public int MissedHeartbeats => missedHeartbeats;
    public long LastServerTime => lastServerTime;

    #region 玩家连接状态管理

    public void SendPlayerExit()
    {
        if (!isConnected)
        {
            Z_Logger.Log("[NetServerManager] 未连接服务器，跳过退出请求");
            return;
        }

        Z_Logger.LogColor("[NetServerManager] 发送玩家退出请求", "orange");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>(ServerUrls.Player.Exit(_currentPlayerId), requestData,
            onSuccess: (response) =>
            {
                Z_Logger.LogColor("[NetServerManager] 玩家退出请求成功", "green");
                StopHeartbeat();
            },
            onError: (error) =>
            {
                Z_Logger.LogWarning("[NetServerManager] 玩家退出请求失败: " + error);
            },
            forcePost: true
        ));
    }

    public void RequestReconnect()
    {
        Z_Logger.LogColor("[NetServerManager] 请求重连恢复状态", "orange");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>(ServerUrls.Player.Reconnect(_currentPlayerId), requestData,
            onSuccess: (response) =>
            {
                Z_Logger.LogColor("[NetServerManager] 重连请求成功，开始恢复钓鱼状态", "green");

                StartCoroutine(FetchGameState());
                StartCoroutine(FetchBaitCount());
                StartCoroutine(FetchPlayerData());
                StartCoroutine(PollFishingStatus());

                StartHeartbeat();
            },
            onError: (error) =>
            {
                Z_Logger.LogWarning("[NetServerManager] 重连请求失败: " + error + "，尝试重新连接");
                Reconnect();
            },
            forcePost: true
        ));
    }

    private void StartHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
        }
        heartbeatCoroutine = StartCoroutine(SendHeartbeatCoroutine());
        Z_Logger.Log("[NetServerManager] 心跳协程已启动");
    }

    private void StopHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
            Z_Logger.Log("[NetServerManager] 心跳协程已停止");
        }
    }

    /// <summary>
    /// 心跳协程：每 HEARTBEAT_INTERVAL 秒触发一次心跳
    /// ✅ 统一调用 SendHeartbeatRequest，不再走旧的 SendHeartbeat
    /// </summary>
    private IEnumerator SendHeartbeatCoroutine()
    {
        while (isConnected && this != null)
        {
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);

            if (!isConnected || this == null)
                yield break;

            yield return SendHeartbeatRequest();
        }
    }

    #endregion

    private void CheckHeartbeatTimeout()
    {
        if (!_isEnabled)
            return;

        if (missedHeartbeats >= NetUtils.MAX_MISSED_HEARTBEATS)
        {
            Z_Logger.LogError("[NetServerManager] 心跳超时，断开连接");
            networkState = NetUtils.NetworkState.Reconnecting;
            isConnected = false;
            missedHeartbeats = 0;
            GameUIManager.Instance?.ShowTip("网络连接断开，正在尝试重新连接...");
        }
    }

    /// <summary>
    /// 心跳请求（协程）
    /// ✅ 走 /api/player/{playerId}/heartbeat
    /// ✅ 处理 mallDataRefreshed（商城刷新）
    /// ✅ 处理 petHungerDirty（宠物饥饿度刷新，看到 true 就拉 /pets）
    /// </summary>
    private IEnumerator SendHeartbeatRequest()
    {
        Z_Logger.Log("[NetServerManager] SendHeartbeatRequest 被调用");

        long clientTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },        // ✅ 补上 playerId
            { "clientTime", clientTime }
        };

        // ✅ 走 /api/player/{playerId}/heartbeat（和 mallDataRefreshed 走的一致）
        yield return SendRequest<HeartbeatResponse>(ServerUrls.Player.Heartbeat(_currentPlayerId), requestData,
            (response) =>
            {
                if (response != null)
                {
                    Z_Logger.Log("[NetServerManager] OnHeartbeatResponse 收到心跳响应");
                    lastServerTime = response.serverTime;
                    isConnected = true;
                    missedHeartbeats = 0;
                    networkState = NetUtils.NetworkState.Connected;

                    // ✅ 商城数据刷新
                    if (response.mallDataRefreshed)
                    {
                        Z_Logger.Log("[NetServerManager] 服务器通知商城数据已刷新，立即同步");
                        SyncMallItemsFromServer();
                    }

                    // ✅ 宠物饥饿度刷新（照抄 mallDataRefreshed 模式）
                    if (response.petHungerDirty)
                    {
                        Z_Logger.Log("[NetServerManager] 服务器通知宠物饥饿度已变化，拉取最新宠物列表");
                        FetchPlayerPets();
                    }
                }
            },
            (error) =>
            {
                Z_Logger.LogWarning("[NetServerManager] 心跳请求失败: " + error);
                isConnected = false;
            });
    }
}
