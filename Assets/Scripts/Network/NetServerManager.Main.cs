using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Utils;
using Logger = Utils.Logger;
using SharedModels;
using System;

public partial class NetServerManager : SingletonMono<NetServerManager>
{
    private string serverUrl = "http://localhost:5000";

    private bool _isEnabled = true;
    public bool IsEnabled => _isEnabled;

    private NetUtils.NetworkState networkState = NetUtils.NetworkState.Disconnected;
    public NetUtils.NetworkState NetworkState => networkState;

    private bool isConnected = false;
    public bool IsConnected => isConnected;

    private Coroutine connectCoroutine;

    private bool isApplicationPaused = false;
    private bool hasSentExitOnPause = false;

    private int _currentPlayerId = 1;

    public void SetCurrentPlayerId(int playerId)
    {
        _currentPlayerId = playerId;
        Logger.Log($"[NetServerManager] 当前玩家ID已更新为: {_currentPlayerId}");
    }

    public int GetCurrentPlayerId()
    {
        return _currentPlayerId;
    }

    public void Init()
    {
        RegisterNetworkEvents();
        RegisterServerEvents();

        Logger.LogColor("[NetServerManager] 网络服务器管理器初始化完成，服务器地址: " + serverUrl, "green");

        StartConnect();
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;

        if (enabled && networkState == NetUtils.NetworkState.Disconnected)
        {
            StartConnect();
        }
        else if (!enabled)
        {
            Disconnect();
        }

        Logger.LogColor("[NetServerManager] 设置启用状态: " + enabled, "orange");
    }

    private void StartConnect()
    {
        if (connectCoroutine != null)
        {
            StopCoroutine(connectCoroutine);
        }
        connectCoroutine = StartCoroutine(ConnectToServer());
    }

    private IEnumerator ConnectToServer()
    {
        if (networkState == NetUtils.NetworkState.Connecting)
            yield break;

        networkState = NetUtils.NetworkState.Connecting;
        Logger.LogColor("[NetServerManager] 正在连接到服务器: " + serverUrl, "yellow");

        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/ping"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Logger.LogColor("[NetServerManager] 连接服务器成功", "green");
                networkState = NetUtils.NetworkState.Connected;
                isConnected = true;
                missedHeartbeats = 0;

                StartCoroutine(FetchGameState());
                StartCoroutine(FetchBaitCount());
                StartCoroutine(FetchPlayerData());

                StartCoroutine(PollFishingStatus());

                StartHeartbeat();
            }
            else
            {
                Logger.LogError("[NetServerManager] 连接服务器失败: " + request.error);
                networkState = NetUtils.NetworkState.Disconnected;
                isConnected = false;
                UIManager.Instance?.ShowTip("无法连接到服务器，请检查网络连接");
            }
        }
    }

    public void Reconnect()
    {
        if (!_isEnabled)
            return;

        networkState = NetUtils.NetworkState.Reconnecting;
        isConnected = false;
        missedHeartbeats = 0;
        StopHeartbeat();

        Logger.LogColor("[NetServerManager] 尝试重新连接...", "orange");
        StartConnect();
    }

    public void Disconnect()
    {
        if (!_isEnabled)
            return;

        SendPlayerExit();
        StopHeartbeat();

        networkState = NetUtils.NetworkState.Disconnected;
        isConnected = false;
        isPlayingReelAnimation = false;
        Logger.LogColor("[NetServerManager] 已断开连接", "red");
    }

    #region Unity生命周期回调

    void OnApplicationPause(bool pause)
    {
        Logger.Log($"[NetServerManager] OnApplicationPause: {pause}");

        if (pause)
        {
            isApplicationPaused = true;
            if (isConnected && !hasSentExitOnPause)
            {
                SendPlayerExit();
                hasSentExitOnPause = true;
            }
            StopHeartbeat();
        }
        else
        {
            isApplicationPaused = false;
            if (!isConnected)
            {
                Logger.Log("[NetServerManager] 应用恢复但未连接到服务器，尝试重连");
                RequestReconnect();
            }
            else
            {
                StartHeartbeat();
            }
            hasSentExitOnPause = false;
        }
    }

    void OnApplicationQuit()
    {
        Logger.LogColor("[NetServerManager] OnApplicationQuit - 应用退出", "red");

        SendPlayerExit();
        StopHeartbeat();
        isConnected = false;
    }

    private void OnDestroy()
    {
        Logger.LogColor("[NetServerManager] OnDestroy - 对象销毁", "red");

        if (isConnected)
        {
            SendPlayerExit();
        }

        StopHeartbeat();

        if (connectCoroutine != null)
        {
            StopCoroutine(connectCoroutine);
            connectCoroutine = null;
        }

        StopAllCoroutines();
        Disconnect();
    }

    #endregion

    private void Update()
    {
        if (!_isEnabled)
            return;

        heartbeatTimer += Time.deltaTime;
        if (heartbeatTimer >= NetUtils.HEARTBEAT_INTERVAL)
        {
            heartbeatTimer = 0f;

            if (networkState == NetUtils.NetworkState.Connected)
            {
                missedHeartbeats++;
                Logger.Log("[NetServerManager] 等待心跳响应，未收到响应次数: " + missedHeartbeats);
                StartCoroutine(SendHeartbeatRequest());
            }
            else if (networkState == NetUtils.NetworkState.Reconnecting)
            {
                StartConnect();
            }
            else if (networkState == NetUtils.NetworkState.Disconnected)
            {
                StartConnect();
            }
        }

        CheckHeartbeatTimeout();
        UpdateContinuousModeRemainingTime();
    }

    private IEnumerator SendRequest<T>(string endpoint, object? data = null, System.Action<T>? onSuccess = null, System.Action<string>? onError = null, bool forcePost = false)
    {
        if (!_isEnabled)
        {
            yield break;
        }

        if (!isConnected || networkState != NetUtils.NetworkState.Connected)
        {
            ShowNetworkError();
            onError?.Invoke("未连接到服务器");
            yield break;
        }

        string url = serverUrl + endpoint;
        UnityWebRequest request;

        if (data != null || forcePost)
        {
            string jsonData = data != null ? NetUtils.SerializeToJson(data as Dictionary<string, object>) : "{}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
        }
        else
        {
            request = UnityWebRequest.Get(url);
        }

        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 10;

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Logger.LogColor("[NetServerManager] 请求成功: " + endpoint, "cyan");
            try
            {
                string jsonResponse = request.downloadHandler.text;
                T? response = NetUtils.ParseJson<T>(jsonResponse);
                onSuccess?.Invoke(response);
            }
            catch (System.Exception ex)
            {
                Logger.LogError("[NetServerManager] 解析响应失败: " + ex.Message);
                onError?.Invoke("解析响应失败");
            }
        }
        else
        {
            Logger.LogError("[NetServerManager] 请求失败: " + endpoint + ", 错误: " + request.error);
            UIManager.Instance?.ShowTip("网络请求失败，请检查网络连接");
            onError?.Invoke(request.error);
        }
    }

    private bool CheckNetworkConnection()
    {
        if (!_isEnabled)
        {
            Logger.LogWarning("[NetServerManager] 网络管理器未启用");
            return false;
        }

        if (!isConnected || networkState != NetUtils.NetworkState.Connected)
        {
            ShowNetworkError();
            return false;
        }

        return true;
    }

    private void ShowNetworkError()
    {
        Logger.LogError("[NetServerManager] 网络连接失败，请检查网络连接后重试");
        UIManager.Instance?.ShowTip("网络连接失败，请检查网络连接后重试");
    }
}