using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class NetServerManager : SingletonMono<NetServerManager>
{
    private string serverUrl = "http://localhost:5000";

    private float heartbeatTimer = 0f;
    private int missedHeartbeats = 0;
    private bool isConnected = false;
    private long lastServerTime = 0;
    private NetUtils.NetworkState networkState = NetUtils.NetworkState.Disconnected;

    private bool _isEnabled = true;
    public bool IsEnabled => _isEnabled;

    public NetUtils.NetworkState NetworkState => networkState;
    public bool IsConnected => isConnected;
    public int MissedHeartbeats => missedHeartbeats;
    public long LastServerTime => lastServerTime;

    private Coroutine connectCoroutine;

    public void Init()
    {
        RegisterNetworkEvents();
        RegisterServerEvents();

        Debug.Log("<color=green>[NetServerManager] 网络服务器管理器初始化完成，服务器地址: " + serverUrl + "</color>");

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

        Debug.Log("<color=orange>[NetServerManager] 设置启用状态: " + enabled + "</color>");
    }

    private void RegisterNetworkEvents()
    {
    }

    private void RegisterServerEvents()
    {
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
        Debug.Log("<color=yellow>[NetServerManager] 正在连接到服务器: " + serverUrl + "</color>");

        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/ping"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("<color=green>[NetServerManager] 连接服务器成功</color>");
                networkState = NetUtils.NetworkState.Connected;
                isConnected = true;
                missedHeartbeats = 0;
            }
            else
            {
                Debug.LogError("<color=red>[NetServerManager] 连接服务器失败: " + request.error + "</color>");
                networkState = NetUtils.NetworkState.Disconnected;
                isConnected = false;
                UIManager.Instance?.ShowTip("无法连接到服务器，请检查网络连接");
            }
        }
    }

    private IEnumerator SendRequest<T>(string endpoint, object? data = null, System.Action<T>? onSuccess = null, System.Action<string>? onError = null)
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

        if (data != null)
        {
            string jsonData = NetUtils.SerializeToJson(data as Dictionary<string, object>);
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
            Debug.Log("<color=cyan>[NetServerManager] 请求成功: " + endpoint + "</color>");
            try
            {
                string jsonResponse = request.downloadHandler.text;
                T? response = NetUtils.ParseJson<T>(jsonResponse);
                onSuccess?.Invoke(response);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[NetServerManager] 解析响应失败: " + ex.Message);
                onError?.Invoke("解析响应失败");
            }
        }
        else
        {
            Debug.LogError("<color=red>[NetServerManager] 请求失败: " + endpoint + ", 错误: " + request.error + "</color>");
            UIManager.Instance?.ShowTip("网络请求失败，请检查网络连接");
            onError?.Invoke(request.error);
        }
    }

    private bool CheckNetworkConnection()
    {
        if (!_isEnabled)
        {
            Debug.LogWarning("[NetServerManager] 网络管理器未启用");
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
        Debug.LogError("[NetServerManager] 网络连接失败，请检查网络连接后重试");
        UIManager.Instance?.ShowTip("网络连接失败，请检查网络连接后重试");
    }

    private void Update()
    {
        if (!_isEnabled)
            return;

        if (SimulationServer.Instance != null && SimulationServer.Instance.IsRunning())
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= NetUtils.HEARTBEAT_INTERVAL)
            {
                heartbeatTimer = 0f;

                if (networkState == NetUtils.NetworkState.Connected)
                {
                    missedHeartbeats++;
                    Debug.Log("[NetServerManager] 等待心跳响应，未收到响应次数: " + missedHeartbeats);
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
        }

        CheckHeartbeatTimeout();
    }

    private void CheckHeartbeatTimeout()
    {
        if (!_isEnabled)
            return;

        if (missedHeartbeats >= NetUtils.MAX_MISSED_HEARTBEATS)
        {
            Debug.LogError("[NetServerManager] 心跳超时，断开连接");
            networkState = NetUtils.NetworkState.Reconnecting;
            isConnected = false;
            missedHeartbeats = 0;
            UIManager.Instance?.ShowTip("网络连接断开，正在尝试重新连接...");
        }
    }

    private IEnumerator SendHeartbeatRequest()
    {
        Debug.Log("[NetServerManager] SendHeartbeat 被调用");

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
                    Debug.Log("[NetServerManager] OnHeartbeatResponse 收到心跳响应");
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
                Debug.LogWarning("[NetServerManager] 心跳请求失败: " + error);
                isConnected = false;
            });
    }

    public void OnAddItem((int itemId, int quantity) data)
    {
        if (!CheckNetworkConnection())
            return;
        Debug.Log("[NetServerManager] 处理添加物品请求: itemId=" + data.itemId + ", quantity=" + data.quantity);

        var requestData = new Dictionary<string, object>
        {
            { "itemId", data.itemId },
            { "quantity", data.quantity }
        };
        StartCoroutine(SendRequest<object>("/api/inventory/add", requestData));
    }

    public void OnRemoveItem((int itemId, int quantity) data)
    {
        if (!CheckNetworkConnection())
            return;
        Debug.Log("[NetServerManager] 处理移除物品请求: itemId=" + data.itemId + ", quantity=" + data.quantity);

        var requestData = new Dictionary<string, object>
        {
            { "itemId", data.itemId },
            { "quantity", data.quantity }
        };
        StartCoroutine(SendRequest<object>("/api/inventory/remove", requestData));
    }

    public void OnAddFish((int fishId, int quantity) data)
    {
        if (!CheckNetworkConnection())
            return;
        Debug.Log("[NetServerManager] 处理添加鱼请求: fishId=" + data.fishId + ", quantity=" + data.quantity);

        var requestData = new Dictionary<string, object>
        {
            { "itemId", data.fishId },
            { "quantity", data.quantity }
        };
        StartCoroutine(SendRequest<object>("/api/inventory/fish/add", requestData));
    }

    public void RequestFishingData(int detectedFishId, int actualItemId, bool isTrash)
    {
        if (!CheckNetworkConnection())
            return;

        NetUtils.LogRequest("RequestFishingData", new Dictionary<string, object>
        {
            { "detectedFishId", detectedFishId },
            { "actualItemId", actualItemId },
            { "isTrash", isTrash }
        });

        var requestData = new Dictionary<string, object>
        {
            { "detectedFishId", detectedFishId },
            { "actualItemId", actualItemId },
            { "isTrash", isTrash }
        };
        StartCoroutine(SendRequest<object>("/api/fishing", requestData));
    }

    public void OnServerFishingResult(SimulationServer.FishingResult result)
    {
        if (!_isEnabled)
            return;
    }

    public void NotifyPlayIdleAnimation()
    {
        Debug.Log("[NetServerManager] 通知播放Idle动画");
        PlayerAniManager.Instance?.PlayIdleAnimation();
    }

    public void NotifyPlayLazyAnimation()
    {
        Debug.Log("[NetServerManager] 通知播放Lazy动画");
        PlayerAniManager.Instance?.PlayLazyAnimation();
    }

    public void NotifyPlayReelAnimation(float struggleTime, System.Action onComplete)
    {
        Debug.Log("[NetServerManager] 通知播放Reel动画，挣扎时间: " + struggleTime);
        PlayerAniManager.Instance?.PlayReelAnimation(struggleTime, onComplete);
    }

    public void NotifySyncInventoryFromServer()
    {
        Debug.Log("[NetServerManager] 通知同步背包数据");
        PlayerDataManager.Instance?.SyncInventoryFromServer();
    }

    public void NotifyAddFish(int fishId, int quantity)
    {
        Debug.Log("[NetServerManager] 通知添加鱼: fishId=" + fishId + ", quantity=" + quantity);
    }

    public void NotifyRefreshUI()
    {
        Debug.Log("[NetServerManager] 通知刷新UI");
        PlayerDataManager.Instance?.RefreshUI();
    }

    public void NotifyShowCatchResult(string itemName, float weight, Sprite icon)
    {
        Debug.Log("[NetServerManager] 通知显示捕获结果: " + itemName);
        UIManager.Instance?.ShowCatchResult(itemName, weight, icon);
    }

    public void Reconnect()
    {
        if (!_isEnabled)
            return;

        networkState = NetUtils.NetworkState.Reconnecting;
        isConnected = false;
        missedHeartbeats = 0;

        Debug.Log("<color=orange>[NetServerManager] 尝试重新连接...</color>");
        StartConnect();
    }

    public void Disconnect()
    {
        if (!_isEnabled)
            return;

        networkState = NetUtils.NetworkState.Disconnected;
        isConnected = false;
        Debug.Log("<color=red>[NetServerManager] 已断开连接</color>");
    }

    [System.Serializable]
    private class HeartbeatResponse
    {
        public long serverTime;
        public long clientTime;
        public bool isConnected;
    }

    [System.Serializable]
    private class HeartbeatRequest
    {
        public long clientTime;
    }
}
