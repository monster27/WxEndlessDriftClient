using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager : SingletonMono<NetServerManager>
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
            Logger.Log("[NetServerManager] 未连接服务器，跳过退出请求");
            return;
        }

        Logger.LogColor("[NetServerManager] 发送玩家退出请求", "orange");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/exit", requestData,
            onSuccess: (response) =>
            {
                Logger.LogColor("[NetServerManager] 玩家退出请求成功", "green");
                StopHeartbeat();
            },
            onError: (error) =>
            {
                Logger.LogWarning("[NetServerManager] 玩家退出请求失败: " + error);
            },
            forcePost: true
        ));
    }

    public void RequestReconnect()
    {
        Logger.LogColor("[NetServerManager] 请求重连恢复状态", "orange");

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "timestamp", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/reconnect", requestData,
            onSuccess: (response) =>
            {
                Logger.LogColor("[NetServerManager] 重连请求成功，开始恢复钓鱼状态", "green");

                StartCoroutine(FetchGameState());
                StartCoroutine(FetchBaitCount());
                StartCoroutine(FetchPlayerData());
                StartCoroutine(PollFishingStatus());

                StartHeartbeat();
            },
            onError: (error) =>
            {
                Logger.LogWarning("[NetServerManager] 重连请求失败: " + error + "，尝试重新连接");
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
        Logger.Log("[NetServerManager] 心跳协程已启动");
    }

    private void StopHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
            Logger.Log("[NetServerManager] 心跳协程已停止");
        }
    }

    private IEnumerator SendHeartbeatCoroutine()
    {
        while (isConnected && this != null)
        {
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);

            if (!isConnected || this == null)
                yield break;

            SendHeartbeat();
        }
    }

    private void SendHeartbeat()
    {
        if (!isConnected)
            return;

        var requestData = new Dictionary<string, object>
        {
            { "playerId", _currentPlayerId },
            { "clientTime", System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        };

        StartCoroutine(SendRequest<object>("/api/player/" + _currentPlayerId + "/heartbeat", requestData,
            onSuccess: (response) =>
            {
                Logger.LogColor("[NetServerManager] 心跳发送成功", "cyan");
            },
            onError: (error) =>
            {
                Logger.LogWarning("[NetServerManager] 心跳发送失败: " + error);
            },
            forcePost: true
        ));
    }

    #endregion

    private void CheckHeartbeatTimeout()
    {
        if (!_isEnabled)
            return;

        if (missedHeartbeats >= NetUtils.MAX_MISSED_HEARTBEATS)
        {
            Logger.LogError("[NetServerManager] 心跳超时，断开连接");
            networkState = NetUtils.NetworkState.Reconnecting;
            isConnected = false;
            missedHeartbeats = 0;
            UIManager.Instance?.ShowTip("网络连接断开，正在尝试重新连接...");
        }
    }

    private IEnumerator SendHeartbeatRequest()
    {
        Logger.Log("[NetServerManager] SendHeartbeat 被调用");

        long clientTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var requestData = new Dictionary<string, object>
        {
            { "clientTime", clientTime }
        };

        yield return SendRequest<HeartbeatResponse>("/api/heartbeat", requestData,
            (response) =>
            {
                if (response != null)
                {
                    Logger.Log("[NetServerManager] OnHeartbeatResponse 收到心跳响应");
                    lastServerTime = response.serverTime;
                    isConnected = true;
                    missedHeartbeats = 0;
                    networkState = NetUtils.NetworkState.Connected;
                    NetUtils.LogResponse("HeartbeatResponse", new Dictionary<string, object>
                    {
                        { "serverTime", lastServerTime }
                    });
                }
            },
            (error) =>
            {
                Logger.LogWarning("[NetServerManager] 心跳请求失败: " + error);
                isConnected = false;
            });
    }
}