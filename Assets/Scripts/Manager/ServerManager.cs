using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using SharedModels;

public class ServerManager : SingletonMono<ServerManager>
{
    private const string EVENT_FISHING_RESPONSE = "FishingResponse";

    private const float HEARTBEAT_INTERVAL = 3f;
    private const int MAX_MISSED_HEARTBEATS = 3;

    private float heartbeatTimer = 0f;
    private int missedHeartbeats = 0;
    private bool isConnected = false;
    private long lastServerTime = 0;

    private bool _isEnabled = true;
    public bool IsEnabled => _isEnabled;

    public void Init()
    {
        RegisterEvents();
        RegisterServerEvents();
        Debug.Log("<color=green>[ServerManager] 单机服务器管理器初始化完成</color>");
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        Debug.Log($"<color=orange>[ServerManager] 设置启用状态: {enabled}</color>");
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register<Dictionary<string, object>>(EVENT_FISHING_RESPONSE, OnFishingResponse);
        CommunicateEvent.Register<Dictionary<string, object>>("HeartbeatResponse", OnHeartbeatResponse);
    }

    private void RegisterServerEvents()
    {
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_TIME_SLOT_CHANGED, OnTimeSlotChanged);
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_WEATHER_CHANGED, OnWeatherChanged);
        CommunicateEvent.Register<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, OnGoldChanged);
        // ========================================================
        // SimulationServer 相关代码已注释（当前使用网络模式）
        // ========================================================
        /*
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_ADD_ITEM, OnAddItem);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_REMOVE_ITEM, OnRemoveItem);
        CommunicateEvent.Register<(int, int)>(CommunicateEvent.EVENT_ADD_FISH, OnAddFish);
        */
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_GOLD, OnSyncGold);
    }

    private void Update()
    {
        if (!_isEnabled)
            return;

        // ========================================================
        // SimulationServer 相关代码已注释（当前使用网络模式）
        // ========================================================
        /*
        if (SimulationServer.Instance != null && SimulationServer.Instance.IsRunning())
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                heartbeatTimer = 0f;
                SendHeartbeat();
            }
        }
        */
    }

    private void OnTimeSlotChanged(Dictionary<string, object> data)
    {
        if (!_isEnabled)
            return;
        Debug.Log("[ServerManager] 转发时间槽变化事件到客户端");
        CommunicateEvent.Modify(CommunicateEvent.EVENT_CLIENT_TIME_SLOT_CHANGED, data);
    }

    private void OnWeatherChanged(Dictionary<string, object> data)
    {
        if (!_isEnabled)
            return;
        Debug.Log("[ServerManager] 转发天气变化事件到客户端");
        CommunicateEvent.Modify(CommunicateEvent.EVENT_CLIENT_WEATHER_CHANGED, data);
    }

    private void OnGoldChanged(Dictionary<string, object> data)
    {
        if (!_isEnabled)
            return;
        Debug.Log("[ServerManager] 转发金币变化事件到客户端");
        CommunicateEvent.Modify(CommunicateEvent.EVENT_CLIENT_GOLD_CHANGED, data);
    }

    private void OnSyncGold()
    {
        if (!_isEnabled)
            return;
        
        Debug.Log("[ServerManager] 收到金币同步请求");
        
        // 使用网络服务器模式获取金币
        if (NetServerManager.Instance != null)
        {
            int currentGold = NetServerManager.Instance.GetPlayerGold();
            Debug.Log($"[ServerManager] 当前金币: {currentGold}");
            
            var goldData = new Dictionary<string, object>
            {
                { "gold", currentGold },
                { "add", 0 },
                { "reduce", 0 }
            };
            
            CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
        }
    }

    // ========================================================
    // SimulationServer 相关代码已注释（当前使用网络模式）
    // ========================================================
    /*
    private void OnAddItem((int itemId, int quantity) data)
    {
        if (!_isEnabled)
            return;
        Debug.Log($"[ServerManager] 处理添加物品请求: itemId={data.itemId}, quantity={data.quantity}");
        SimulationServer.Instance?.AddItem(data.itemId, data.quantity);
    }

    private void OnRemoveItem((int itemId, int quantity) data)
    {
        if (!_isEnabled)
            return;
        Debug.Log($"[ServerManager] 处理移除物品请求: itemId={data.itemId}, quantity={data.quantity}");
        SimulationServer.Instance?.RemoveItem(data.itemId, data.quantity);
    }

    private void OnAddFish((int fishId, int quantity) data)
    {
        if (!_isEnabled)
            return;
        Debug.Log($"[ServerManager] 处理添加鱼请求: fishId={data.fishId}, quantity={data.quantity}");
        SimulationServer.Instance?.AddFish(data.fishId, data.quantity);
    }
    */

    public void NotifyPlayIdleAnimation()
    {
        Debug.Log("[ServerManager] 通知播放Idle动画");
        PlayerAniManager.Instance?.PlayIdleAnimation();
    }

    public void NotifyPlayLazyAnimation()
    {
        Debug.Log("[ServerManager] 通知播放Lazy动画");
        PlayerAniManager.Instance?.PlayLazyAnimation();
    }

    public void NotifyPlayReelAnimation(float struggleTime, System.Action onComplete)
    {
        Debug.Log($"[ServerManager] 通知播放Reel动画，挣扎时间: {struggleTime}");
        PlayerAniManager.Instance?.PlayReelAnimation(struggleTime, onComplete);
    }

    public void NotifySyncInventoryFromServer()
    {
        Debug.Log("[ServerManager] 通知同步背包数据");
        PlayerDataManager.Instance?.SyncInventoryFromServer();
    }

    public void NotifyAddFish(int fishId, int quantity)
    {
        Debug.Log($"[ServerManager] 通知添加鱼: fishId={fishId}, quantity={quantity}");
        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_FISH_CAUGHT, (fishId, quantity));
    }

    public void NotifyRefreshUI()
    {
        Debug.Log("[ServerManager] 通知刷新UI");
        PlayerDataManager.Instance?.RefreshUI();
    }

    public void NotifyShowCatchResult(string itemName, float weight, Sprite icon)
    {
        Debug.Log($"[ServerManager] 通知显示捕获结果: {itemName}");
        GameUIManager.Instance?.ShowCatchResult(itemName, weight, icon);
    }

    public void OnServerFishingResult(FishingResult result)
    {
        if (!_isEnabled)
            return;

        if (result == null)
        {
            Debug.LogError("[ServerManager] 收到空的钓鱼结果");
            return;
        }

        Debug.LogFormat("<color=cyan>[ServerManager] 收到服务器钓鱼结果:</color>");
        Debug.LogFormat("<color=cyan>  - 第一组数据(检测到): ID={0}</color>", result.detectedFishId);
        Debug.LogFormat("<color=cyan>  - 第二组数据(实际): ID={0}, 是否垃圾={1}</color>", result.actualItemId, result.isTrash);
        Debug.LogFormat("<color=cyan>  - 挣扎时间: {0}秒</color>", result.struggleTime);

        if (PlayerAniManager.Instance != null)
        {
            float struggleTime = result.struggleTime > 0f ? result.struggleTime : 3f;

            PlayerAniManager.Instance.PlayReelAnimationWithTwoIds(
                result.detectedFishId,
                result.actualItemId,
                struggleTime,
                result.isTrash,
                () => {
                    Debug.Log("[ServerManager] 拉杆动画结束，开始播放MainTile动画并更新鱼篓数据");

                    ShowCatchResult(result.actualItemId);

                    if (PlayerDataManager.Instance != null)
                    {
                        PlayerDataManager.Instance.SyncInventoryFromServer();
                        PlayerDataManager.Instance.RefreshUI();
                    }

                    // ========================================================
                    // SimulationServer 相关代码已注释（当前使用网络模式）
                    // ========================================================
                    /*
                    if (SimulationServer.Instance != null)
                    {
                        bool isFishBagFull = SimulationServer.Instance.IsFishBagFull();

                        if (isFishBagFull)
                        {
                            PlayerAniManager.Instance.PlayLazyAnimation();
                            Debug.Log("[ServerManager] 鱼篓已满，切换到Lazy动画");
                        }
                        else
                        {
                            PlayerAniManager.Instance.PlayIdleAnimation();
                            Debug.Log("[ServerManager] 拉杆动画结束，切换到Idle动画");
                        }

                        SimulationServer.Instance.AutoFishingManager?.ResetNotificationState();
                    }
                    */
                    
                    // 默认播放Idle动画
                    PlayerAniManager.Instance.PlayIdleAnimation();
                    Debug.Log("[ServerManager] 拉杆动画结束，切换到Idle动画");
                }
            );
        }
    }

    // ========================================================
    // SimulationServer 相关代码已注释（当前使用网络模式）
    // ========================================================
    /*
    public void RequestFishingData(int detectedFishId, int actualItemId, bool isTrash)
    {
        if (!_isEnabled)
            return;

        Debug.LogFormat("<color=yellow>[ServerManager] 发送请求到服务器: detectedFishId={0}, actualItemId={1}, isTrash={2}</color>",
            detectedFishId, actualItemId, isTrash);

        if (SimulationServer.Instance != null)
        {
            var result = SimulationServer.Instance.CurrentFishingResult;
            if (result != null)
            {
                ShowCatchResult(result.actualItemId);

                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.SyncInventoryFromServer();
                    PlayerDataManager.Instance.RefreshUI();
                }
            }
        }
    }
    */

    private void OnFishingResponse(Dictionary<string, object> data)
    {
        if (!_isEnabled)
            return;

        if (data.TryGetValue("itemId", out object itemIdObj) &&
            data.TryGetValue("fishId", out object fishIdObj) &&
            data.TryGetValue("struggleTime", out object struggleTimeObj))
        {
            int finalId = System.Convert.ToInt32(itemIdObj);
            int fishId = System.Convert.ToInt32(fishIdObj);
            float struggleTime = System.Convert.ToSingle(struggleTimeObj);

            Debug.LogFormat("<color=cyan>[ServerManager] 收到钓鱼结果: 鱼类ID={0}, 最终物品ID={1}, 挣扎时间={2}秒</color>", fishId, finalId, struggleTime);

            ProcessFishingResult(fishId, finalId, struggleTime);
        }
    }

    private void ProcessFishingResult(int fishId, int finalId, float struggleTime)
    {
        if (PlayerAniManager.Instance != null)
        {
            PlayerAniManager.Instance.PlayReelAnimation(struggleTime, () => {
                PlayerAniManager.Instance.PlayIdleAnimation();
                ShowCatchResult(finalId);

                if (PlayerDataManager.Instance != null)
                {
                    PlayerDataManager.Instance.SyncInventoryFromServer();
                    PlayerDataManager.Instance.RefreshUI();
                }
            });
        }
    }

    private void ShowCatchResult(int itemId)
    {
        if (GameUIManager.Instance != null)
        {
            ItemData itemData = GetItemDataById(itemId);
            if (itemData != null)
            {
                string itemName = itemData.name;
                float weight = GetItemWeight(itemId);

                Sprite icon = null;
                if (!string.IsNullOrEmpty(itemData.iconPath))
                {
                    icon = Resources.Load<Sprite>(itemData.iconPath);
                }

                GameUIManager.Instance.ShowCatchResult(itemName, weight, icon);
            }
        }
    }

    private ItemData GetItemDataById(int itemId)
    {
        if (LoadDataManager.Instance != null && LoadDataManager.Instance.items != null)
        {
            foreach (ItemData item in LoadDataManager.Instance.items)
            {
                if (item.id == itemId)
                {
                    return item;
                }
            }
        }
        return null;
    }

    private float GetItemWeight(int itemId)
    {
        ItemData itemData = GetItemDataById(itemId);
        if (itemData == null)
        {
            return 1.0f;
        }

        if (LoadDataManager.Instance != null)
        {
            FishData fishData = LoadDataManager.Instance.GetFishById(itemId);
            if (fishData != null)
            {
                return fishData.baseWeight;
            }
        }

        return 1.0f;
    }

    private void SendHeartbeat()
    {
        long clientTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var heartbeatData = new Dictionary<string, object>
        {
            { "clientTime", clientTime },
            { "type", "heartbeat" }
        };

        Debug.Log($"[ServerManager] 发送心跳包: clientTime={clientTime}");

        // ========================================================
        // SimulationServer 相关代码已注释（当前使用网络模式）
        // ========================================================
        /*
        if (SimulationServer.Instance != null)
        {
            SimulationServer.Instance.ProcessHeartbeat(heartbeatData);
        }
        */
    }

    private void OnHeartbeatResponse(Dictionary<string, object> data)
    {
        if (!_isEnabled)
            return;

        if (data.TryGetValue("serverTime", out object serverTimeObj))
        {
            lastServerTime = System.Convert.ToInt64(serverTimeObj);
            isConnected = true;
            missedHeartbeats = 0;
            Debug.Log($"[ServerManager] 收到心跳响应: serverTime={lastServerTime}, isConnected={isConnected}");
        }
    }

    public bool IsConnected()
    {
        return isConnected;
    }

    public int GetMissedHeartbeats()
    {
        return missedHeartbeats;
    }

    public long GetLastServerTime()
    {
        return lastServerTime;
    }
}