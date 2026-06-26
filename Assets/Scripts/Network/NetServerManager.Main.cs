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

    // ========== 持久化控制 ==========
    private bool _isInitCalled = false;
    private bool _isPersistent = false;

    // ========== 重写 Awake 实现持久化 ==========
    protected override void Awake()
    {
        // 调用基类 Awake（基类会处理单例逻辑）
        base.Awake();

        // 确保对象在场景切换时不被销毁
        if (!_isPersistent)
        {
            // 确保是根对象
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
            _isPersistent = true;
            Logger.LogColor("[NetServerManager] 已设置为 DontDestroyOnLoad，将在场景切换中保持存在", "cyan");
        }
    }

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
        if (_isInitCalled)
        {
            Logger.Log("[NetServerManager] Init() 已被调用，跳过重复初始化");
            return;
        }

        RegisterNetworkEvents();
        RegisterServerEvents();
        _isInitCalled = true;

        Logger.LogColor("[NetServerManager] 网络服务器管理器初始化完成，服务器地址: " + serverUrl, "green");
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;

        if (enabled && networkState == NetUtils.NetworkState.Disconnected)
        {
            // 不自动连接，等待 StartInitialization 调用
            Logger.Log("[NetServerManager] 网络已启用，等待初始化...");
        }
        else if (!enabled)
        {
            Disconnect();
        }

        Logger.LogColor("[NetServerManager] 设置启用状态: " + enabled, "orange");
    }

    public void StartConnect()
    {
        // 仅在未连接时连接
        if (isConnected) return;

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

                // 开始心跳
                StartHeartbeat();
            }
            else
            {
                Logger.LogError("[NetServerManager] 连接服务器失败: " + request.error);
                networkState = NetUtils.NetworkState.Disconnected;
                isConnected = false;
                GameUIManager.Instance?.ShowTip("无法连接到服务器，请检查网络连接");
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
                // 不自动连接，由外部调用 StartInitialization
                Logger.Log("[NetServerManager] 当前未连接，等待主动初始化");
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
            GameUIManager.Instance?.ShowTip("网络请求失败，请检查网络连接");
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
        GameUIManager.Instance?.ShowTip("网络连接失败，请检查网络连接后重试");
    }
}