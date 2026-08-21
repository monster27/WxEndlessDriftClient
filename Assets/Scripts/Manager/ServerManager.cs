using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
//using SharedModels;

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

    // AA 句柄
    private AsyncOperationHandle<Sprite> _iconHandle;

    void OnDestroy()
    {
        AssetManager.ReleaseAddressable(_iconHandle);
    }

    public void Init()
    {
        RegisterEvents();
        RegisterServerEvents();
        Z_Logger.Log("<color=green>[ServerManager] 单机服务器管理器初始化完成</color>");
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        Z_Logger.Log($"<color=orange>[ServerManager] 设置启用状态: {enabled}</color>");
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
        CommunicateEvent.Register(CommunicateEvent.EVENT_SYNC_GOLD, OnSyncGold);
    }

    private void Update()
    {
        if (!_isEnabled)
            return;
    }

    private void OnTimeSlotChanged(Dictionary<string, object> data)
    {
        if (!_isEnabled) return;
        Z_Logger.Log("[ServerManager] 转发时间槽变化事件到客户端");
        CommunicateEvent.Modify(CommunicateEvent.EVENT_CLIENT_TIME_SLOT_CHANGED, data);
    }

    private void OnWeatherChanged(Dictionary<string, object> data)
    {
        if (!_isEnabled) return;
        Z_Logger.Log("[ServerManager] 转发天气变化事件到客户端");
        CommunicateEvent.Modify(CommunicateEvent.EVENT_CLIENT_WEATHER_CHANGED, data);
    }

    private void OnGoldChanged(Dictionary<string, object> data)
    {
        if (!_isEnabled) return;
        Z_Logger.Log("[ServerManager] 转发金币变化事件到客户端");
        CommunicateEvent.Modify(CommunicateEvent.EVENT_CLIENT_GOLD_CHANGED, data);
    }

    private void OnSyncGold()
    {
        if (!_isEnabled) return;

        Z_Logger.Log("[ServerManager] 收到金币同步请求");

        if (NetServerManager.Instance != null)
        {
            int currentGold = NetServerManager.Instance.GetPlayerGold();
            Z_Logger.Log($"[ServerManager] 当前金币: {currentGold}");

            var goldData = new Dictionary<string, object>
            {
                { "gold", currentGold },
                { "add", 0 },
                { "reduce", 0 }
            };

            CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, goldData);
            CommunicateEvent.Modify<int>(CommunicateEvent.EVENT_GOLD_CHANGED, currentGold);
        }
    }

    public void NotifyPlayIdleAnimation()
    {
        Z_Logger.Log("[ServerManager] 通知播放Idle动画");
        PlayerAniManager.Instance?.PlayIdleAnimation();
    }

    public void NotifyPlayLazyAnimation()
    {
        Z_Logger.Log("[ServerManager] 通知播放Lazy动画");
        PlayerAniManager.Instance?.PlayLazyAnimation();
    }

    public void NotifyPlayReelAnimation(float struggleTime, System.Action onComplete)
    {
        Z_Logger.Log($"[ServerManager] 通知播放Reel动画，挣扎时间: {struggleTime}");
        PlayerAniManager.Instance?.PlayReelAnimation(struggleTime, onComplete);
    }

    public void NotifySyncInventoryFromServer()
    {
        Z_Logger.Log("[ServerManager] 通知同步背包数据");
        PlayerDataManager.Instance?.SyncInventoryFromServer();
    }

    public void NotifyAddFish(int fishId, int quantity)
    {
        Z_Logger.Log($"[ServerManager] 通知添加鱼: fishId={fishId}, quantity={quantity}");
        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_FISH_CAUGHT, (fishId, quantity));
    }

    public void NotifyRefreshUI()
    {
        Z_Logger.Log("[ServerManager] 通知刷新UI");
        PlayerDataManager.Instance?.RefreshUI();
    }

    public void NotifyShowCatchResult(string itemName, float weight, Sprite icon)
    {
        Z_Logger.Log($"[ServerManager] 通知显示捕获结果: {itemName}");
        GameUIManager.Instance?.ShowCatchResult(itemName, weight, icon);
    }

    public void OnServerFishingResult(FishingResult result)
    {
        if (!_isEnabled) return;

        if (result == null)
        {
            Z_Logger.LogError("[ServerManager] 收到空的钓鱼结果");
            return;
        }

        Z_Logger.LogFormat("<color=cyan>[ServerManager] 收到服务器钓鱼结果:</color>");
        Z_Logger.LogFormat("<color=cyan>  - 第一组数据(检测到): ID={0}</color>", result.detectedFishId);
        Z_Logger.LogFormat("<color=cyan>  - 第二组数据(实际): ID={0}, 是否垃圾={1}</color>", result.actualItemId, result.isTrash);
        Z_Logger.LogFormat("<color=cyan>  - 挣扎时间: {0}秒</color>", result.struggleTime);

        if (PlayerAniManager.Instance != null)
        {
            float struggleTime = result.struggleTime > 0f ? result.struggleTime : 3f;

            PlayerAniManager.Instance.PlayReelAnimationWithTwoIds(
                result.detectedFishId,
                result.actualItemId,
                struggleTime,
                result.isTrash,
                () => {
                    Z_Logger.Log("[ServerManager] 拉杆动画结束，开始播放MainTile动画并更新鱼篓数据");

                    ShowCatchResult(result.actualItemId);

                    if (PlayerDataManager.Instance != null)
                    {
                        PlayerDataManager.Instance.SyncInventoryFromServer();
                        PlayerDataManager.Instance.RefreshUI();
                    }

                    Z_Logger.Log("[ServerManager] 拉杆动画结束，等待 CheckAndUpdateAnimationState 决定动画");
                }
            );
        }
    }

    private void OnFishingResponse(Dictionary<string, object> data)
    {
        if (!_isEnabled) return;

        if (data.TryGetValue("itemId", out object itemIdObj) &&
            data.TryGetValue("fishId", out object fishIdObj) &&
            data.TryGetValue("struggleTime", out object struggleTimeObj))
        {
            int finalId = System.Convert.ToInt32(itemIdObj);
            int fishId = System.Convert.ToInt32(fishIdObj);
            float struggleTime = System.Convert.ToSingle(struggleTimeObj);

            Z_Logger.LogFormat("<color=cyan>[ServerManager] 收到钓鱼结果: 鱼类ID={0}, 最终物品ID={1}, 挣扎时间={2}秒</color>", fishId, finalId, struggleTime);

            ProcessFishingResult(fishId, finalId, struggleTime);
        }
    }

    private void ProcessFishingResult(int fishId, int finalId, float struggleTime)
    {
        if (PlayerAniManager.Instance != null)
        {
            PlayerAniManager.Instance.PlayReelAnimation(struggleTime, () => {
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

                if (!string.IsNullOrEmpty(itemData.iconPath))
                {
                    AssetManager.LoadFromAddressables<Sprite>(itemData.iconPath, (sprite, handle) =>
                    {
                        _iconHandle = handle;
                        GameUIManager.Instance.ShowCatchResult(itemName, weight, sprite);
                    });
                }
                else
                {
                    GameUIManager.Instance.ShowCatchResult(itemName, weight, null);
                }
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

        Z_Logger.Log($"[ServerManager] 发送心跳包: clientTime={clientTime}");
    }

    private void OnHeartbeatResponse(Dictionary<string, object> data)
    {
        if (!_isEnabled) return;

        if (data.TryGetValue("serverTime", out object serverTimeObj))
        {
            lastServerTime = System.Convert.ToInt64(serverTimeObj);
            isConnected = true;
            missedHeartbeats = 0;
            Z_Logger.Log($"[ServerManager] 收到心跳响应: serverTime={lastServerTime}, isConnected={isConnected}");
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
