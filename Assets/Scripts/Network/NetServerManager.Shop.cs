using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager : SingletonMono<NetServerManager>
{
    private Dictionary<int, MallItemData> mallItems = new Dictionary<int, MallItemData>();

    private void OnPurchaseMallItem((int itemId, int quantity) request)
    {
        var (itemId, quantity) = request;
        Logger.Log($"[NetServerManager] OnPurchaseMallItem - itemId={itemId}, quantity={quantity}");
        PurchaseMallItem(itemId, quantity, (success, message) =>
        {
            if (success)
            {
                Logger.Log($"[NetServerManager] 购买成功: {message}");
            }
            else
            {
                Logger.LogWarning($"[NetServerManager] 购买失败: {message}");
            }
        });
    }

    public void SyncMallItemsFromServer()
    {
        StartCoroutine(SyncMallItemsCoroutine());
    }

    private IEnumerator SyncMallItemsCoroutine()
    {
        string url = serverUrl + "/api/player/mall/items";
        Logger.Log($"[NetServerManager] 同步商城物品列表: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 5;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    Logger.Log("[NetServerManager] 商城物品列表响应: " + json);

                    var response = JsonUtility.FromJson<MallItemsResponse>(json);
                    if (response != null && response.success && response.items != null)
                    {
                        mallItems.Clear();
                        foreach (var item in response.items)
                        {
                            mallItems[item.itemId] = new MallItemData
                            {
                                id = item.itemId,
                                price = item.price,
                                stock = item.stock
                            };
                            Logger.Log($"[NetServerManager] 商城物品: ID={item.itemId}, 价格={item.price}, 库存={item.stock}");
                        }

                        Logger.Log($"[NetServerManager] 同步商城物品列表完成，共 {mallItems.Count} 个商品");

                        CommunicateEvent.Modify<Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_MALL_DATA_CHANGED, mallItems);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析商城物品列表失败: {ex.Message}");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 获取商城物品列表失败: {request.error}");
            }
        }
    }

    public Dictionary<int, MallItemData> GetMallItems()
    {
        return new Dictionary<int, MallItemData>(mallItems);
    }

    public MallItemData GetMallItem(int itemId)
    {
        return mallItems.ContainsKey(itemId) ? mallItems[itemId] : null;
    }

    public void PurchaseMallItem(int itemId, int quantity, System.Action<bool, string> callback)
    {
        StartCoroutine(PurchaseMallItemCoroutine(itemId, quantity, callback));
    }

    private IEnumerator PurchaseMallItemCoroutine(int itemId, int quantity, System.Action<bool, string> callback)
    {
        string url = serverUrl + "/api/player/mall/purchase";
        string jsonData = $"{{\"PlayerId\":{_currentPlayerId},\"ItemId\":{itemId},\"Quantity\":{quantity}}}";

        Logger.Log($"[NetServerManager] 购买商城物品请求: {jsonData}");

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string responseText = request.downloadHandler.text;
                Logger.Log($"[NetServerManager] 购买商城物品响应: {responseText}");

                try
                {
                    var response = JsonUtility.FromJson<PurchaseMallItemResponse>(responseText);
                    if (response != null && response.success)
                    {
                        Logger.Log($"[NetServerManager] 成功购买物品 {itemId}, 数量 {quantity}, 总价 {response.totalPrice}");
                        callback?.Invoke(true, response.message);

                        if (playerInventory.ContainsKey(itemId))
                        {
                            playerInventory[itemId] += quantity;
                            Logger.Log($"[NetServerManager] 本地背包数据更新: ItemId={itemId}, 新数量={playerInventory[itemId]}");
                        }
                        else
                        {
                            playerInventory[itemId] = quantity;
                            Logger.Log($"[NetServerManager] 本地背包数据新增: ItemId={itemId}, 数量={quantity}");
                        }

                        PlayerDataManager.Instance?.SyncInventoryFromServer();

                        CommunicateEvent.Modify("Mall_PurchaseSuccess", itemId);
                        CommunicateEvent.Modify("Bag_RefreshItems");
                        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_ITEM_QUANTITY_CHANGED, (itemId, playerInventory[itemId]));

                        if (itemId == 2501)
                        {
                            CommunicateEvent.Modify("BaitCountChanged");
                            Logger.Log("[NetServerManager] 发送窝料数量更新事件");
                            StartCoroutine(SyncContinuousModeStatusCoroutine());
                        }

                        if (itemId >= 2001 && itemId <= 2007)
                        {
                            CommunicateEvent.Modify("BaitCountChanged");
                            CommunicateEvent.Modify("BaitDataUpdated");
                            Logger.Log($"[NetServerManager] 发送鱼饵数量更新事件: itemId={itemId}");
                        }

                        SyncMallItemsFromServer();
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 购买商城物品失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false, response?.message ?? "购买失败");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析购买商城物品响应失败: {ex.Message}");
                    callback?.Invoke(false, "解析响应失败");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 购买商城物品请求失败: {request.error}");
                callback?.Invoke(false, request.error);
            }
        }
    }

    // ========== 辅助数据类 ==========

    [System.Serializable]
    private class MallItemsResponse
    {
        public bool success;
        public MallItemDataJson[] items;
    }

    [System.Serializable]
    private class MallItemDataJson
    {
        public int itemId;
        public int price;
        public int stock;
    }

    [System.Serializable]
    private class PurchaseMallItemResponse
    {
        public bool success;
        public string message;
        public int totalPrice;
        public int remainingGold;
    }
}