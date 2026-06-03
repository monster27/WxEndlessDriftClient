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
        Debug.Log("[NetServerManager] 注册网络模式下的事件处理器");
        
        // 注册连续模式相关的请求处理器
        CommunicateEvent.RegisterRequest<int, bool>(CommunicateEvent.EVENT_IS_IN_CONTINUOUS_MODE, _ => IsInContinuousMode());
        CommunicateEvent.RegisterRequest<int, float>(CommunicateEvent.EVENT_GET_CONTINUOUS_MODE_REMAINING_TIME, _ => GetContinuousModeRemainingTime());
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CURRENT_SCENE_BAIT_COUNT, _ => GetCurrentSceneBaitCount());
        
        // 注册玩家数据相关的请求处理器
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_INVENTORY, _ => GetPlayerInventory());
        CommunicateEvent.RegisterRequest<int, Dictionary<int, int>>(CommunicateEvent.EVENT_GET_FISH_INVENTORY, _ => GetPlayerFishInventory());
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_FISH_BAG_CAPACITY, _ => GetFishBagCapacity());
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_GOLD, _ => GetPlayerGold());
        
        // 注册装备相关的请求处理器
        CommunicateEvent.RegisterRequest<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, slotType => GetEquippedItem(slotType));
        CommunicateEvent.RegisterRequest<int, int>(CommunicateEvent.EVENT_GET_CHARACTER_LEVEL, _ => GetCharacterLevel());
        CommunicateEvent.RegisterRequest<int, PlayerData>(CommunicateEvent.EVENT_GET_PLAYER_DATA, _ => GetPlayerData());

        // 注册 CharacterServerManager 相关的请求处理器
        CommunicateEvent.RegisterRequest<int, PlayerCharacterData>("CharacterServerManager_GetPlayerData", _ => GetPlayerCharacterData());
        CommunicateEvent.RegisterRequest<int, int>("CharacterServerManager_GetExpToNextLevel", _ => GetExpToNextLevel());
    }
    
    private bool isInContinuousMode = false;
    private float continuousModeRemainingTime = 0f;
    private int currentSceneBaitCount = 0;
    
    // 玩家数据
    private Dictionary<int, int> playerInventory = new Dictionary<int, int>();
    private Dictionary<int, int> fishInventory = new Dictionary<int, int>();
    private int fishBagCapacity = 20;
    private int playerGold = 0;
    
    // 玩家装备数据
    private int equippedRodId = 1;       // 默认普通钓竿
    private int equippedLineId = 1;      // 默认普通钓线
    private int equippedHookId = 1;      // 默认普通钓钩
    private int equippedSkill1Id = 0;    // 技能1槽位
    private int equippedSkill2Id = 0;    // 技能2槽位
    private int equippedCharacterId = 3401; // 默认人物
    private int characterLevel = 1;      // 人物等级
    
    private bool IsInContinuousMode()
    {
        return isInContinuousMode;
    }
    
    private float GetContinuousModeRemainingTime()
    {
        return continuousModeRemainingTime;
    }
    
    private int GetCurrentSceneBaitCount()
    {
        return currentSceneBaitCount;
    }
    
    private Dictionary<int, int> GetPlayerInventory()
    {
        return playerInventory;
    }
    
    private Dictionary<int, int> GetPlayerFishInventory()
    {
        return fishInventory;
    }
    
    private int GetFishBagCapacity()
    {
        return fishBagCapacity;
    }
    
    private int GetPlayerGold()
    {
        return playerGold;
    }
    
    private int GetEquippedItem(EquipmentSlotType slotType)
    {
        EquipmentSlotType slot = slotType;
        switch (slot)
        {
            case EquipmentSlotType.FishingRod:
                return equippedRodId;
            case EquipmentSlotType.FishingLine:
                return equippedLineId;
            case EquipmentSlotType.FishingHook:
                return equippedHookId;
            case EquipmentSlotType.Skill1:
                return equippedSkill1Id;
            case EquipmentSlotType.Skill2:
                return equippedSkill2Id;
            case EquipmentSlotType.Character:
                return equippedCharacterId;
            default:
                return 0;
        }
    }
    
    private int GetCharacterLevel()
    {
        return characterLevel;
    }
    
    private PlayerData GetPlayerData()
    {
        return new PlayerData
        {
            playerId = 1,
            nickname = "Player",
            gold = playerGold,
            level = 1,
            experience = 0,
            currentSceneId = 1,
            maxFishBagCapacity = fishBagCapacity
        };
    }
    
    private PlayerCharacterData GetPlayerCharacterData()
    {
        return new PlayerCharacterData
        {
            equippedCharacterId = equippedCharacterId,
            isEquipped = equippedCharacterId > 0,
            currentLevel = characterLevel,
            currentExp = 0
        };
    }
    
    private int GetExpToNextLevel()
    {
        // 返回下一级所需经验（简化实现，实际应该从配置读取）
        return 100;
    }
    
    private IEnumerator FetchGameState()
    {
        if (!isConnected)
            yield break;
            
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/game/continuous-mode/status"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<ContinuousModeStatus>(json);
                    if (data != null)
                    {
                        isInContinuousMode = data.isInContinuousMode;
                        continuousModeRemainingTime = data.remainingTime;
                        Debug.Log("[NetServerManager] 更新连续模式状态: " + isInContinuousMode + ", 剩余时间: " + continuousModeRemainingTime);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析游戏状态失败: " + ex.Message);
                }
            }
        }
    }
    
    private IEnumerator FetchBaitCount()
    {
        if (!isConnected)
            yield break;
            
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/game/bait/count"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<BaitCountResponse>(json);
                    if (data != null)
                    {
                        currentSceneBaitCount = data.baitCount;
                        Debug.Log("[NetServerManager] 更新鱼饵数量: " + currentSceneBaitCount);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析鱼饵数量失败: " + ex.Message);
                }
            }
        }
    }
    
    private IEnumerator FetchPlayerData()
    {
        if (!isConnected)
            yield break;
            
        // 获取玩家金币
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/gold/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<GoldResponse>(json);
                    if (data != null)
                    {
                        playerGold = data.gold;
                        Debug.Log("[NetServerManager] 更新玩家金币: " + playerGold);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析金币数据失败: " + ex.Message);
                }
            }
        }
        
        // 获取玩家背包
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/inventory/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        playerInventory.Clear();
                        foreach (var item in data.items)
                        {
                            playerInventory[item.key] = item.value;
                        }
                        Debug.Log("[NetServerManager] 更新玩家背包: " + playerInventory.Count + " 件物品");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析背包数据失败: " + ex.Message);
                }
            }
        }
        
        // 获取玩家鱼篓
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/fish-inventory/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<InventoryResponse>(json);
                    if (data != null && data.items != null)
                    {
                        fishInventory.Clear();
                        foreach (var item in data.items)
                        {
                            fishInventory[item.key] = item.value;
                        }
                        Debug.Log("[NetServerManager] 更新玩家鱼篓: " + fishInventory.Count + " 条鱼");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析鱼篓数据失败: " + ex.Message);
                }
            }
        }
        
        // 获取鱼篓容量
        using (UnityWebRequest request = UnityWebRequest.Get(serverUrl + "/api/player/fish-bag-capacity/1"))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    var data = JsonUtility.FromJson<CapacityResponse>(json);
                    if (data != null)
                    {
                        fishBagCapacity = data.capacity;
                        Debug.Log("[NetServerManager] 更新鱼篓容量: " + fishBagCapacity);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[NetServerManager] 解析鱼篓容量失败: " + ex.Message);
                }
            }
        }
    }
    
    [System.Serializable]
    private class ContinuousModeStatus
    {
        public bool isInContinuousMode;
        public float remainingTime;
    }
    
    [System.Serializable]
    private class BaitCountResponse
    {
        public int baitCount;
    }
    
    [System.Serializable]
    private class GoldResponse
    {
        public int gold;
    }
    
    [System.Serializable]
    private class InventoryResponse
    {
        public List<KeyValuePair> items;
    }
    
    [System.Serializable]
    private class KeyValuePair
    {
        public int key;
        public int value;
    }
    
    [System.Serializable]
    private class CapacityResponse
    {
        public int capacity;
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
                
                // 连接成功后获取游戏状态
                StartCoroutine(FetchGameState());
                StartCoroutine(FetchBaitCount());
                StartCoroutine(FetchPlayerData());
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

    public void OnServerFishingResult(FishingResult result)
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

    /// <summary>
    /// 玩家数据类
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        public int playerId;
        public string nickname;
        public int gold;
        public int level;
        public int experience;
        public int currentSceneId;
        public int maxFishBagCapacity;
    }
}
