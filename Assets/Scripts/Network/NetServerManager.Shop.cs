using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using SharedModels;
using Logger = Utils.Logger;

public partial class NetServerManager
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

                global::ItemData itemData = LoadDataManager.Instance?.GetItemById(itemId);
                if (itemData != null && itemData.itemType == 7)
                {
                    PurchaseCollectionInfo(itemId, (infoSuccess) =>
                    {
                        if (infoSuccess)
                        {
                            Logger.Log($"[NetServerManager] 图鉴情报页面 {itemId} 购买记录成功");
                        }
                        else
                        {
                            Logger.LogWarning($"[NetServerManager] 图鉴情报页面 {itemId} 购买记录失败（可能已购买）");
                        }
                    });
                }
            }
            else
            {
                Logger.LogWarning($"[NetServerManager] 购买失败: {message}");
                GameUIManager.ShowMessage(message);
            }
        });
    }

    public void SyncMallItemsFromServer()
    {
        StartCoroutine(SyncMallItemsCoroutine());
    }

    /// <summary>
    /// 从服务器同步商城物品列表
    /// </summary>
    private IEnumerator SyncMallItemsCoroutine()
    {
        // ✅ 添加 playerId 参数
        string url = serverUrl + ServerUrls.Player.MallItems + $"?playerId={_currentPlayerId}";
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
                                itemId = item.itemId,
                                price = item.price,
                                stock = item.stock,
                                isUnique = item.isUnique,
                                isOnSale = item.isOnSale,
                                name = item.name ?? GetItemNameById(item.itemId),
                                description = item.description ?? "",
                                type = item.type ?? 0,
                                count = item.count ?? 1,
                                iconId = item.iconId ?? 0,
                                isHot = item.isHot ?? false,
                                isNew = item.isNew ?? false
                            };

                            Logger.Log($"[NetServerManager] 商城物品: ID={item.itemId}, 价格={item.price}, 库存={item.stock}, 上架={item.isOnSale}");
                        }

                        int onSaleCount = 0;
                        foreach (var kvp in mallItems)
                        {
                            if (kvp.Value.isOnSale) onSaleCount++;
                        }
                        Logger.Log($"[NetServerManager] 同步商城物品列表完成，共 {mallItems.Count} 个商品（已上架 {onSaleCount} 个）");

                        CommunicateEvent.Modify<Dictionary<int, MallItemData>>(CommunicateEvent.EVENT_MALL_DATA_CHANGED, mallItems);
                    }
                    else
                    {
                        Logger.LogWarning("[NetServerManager] 商城物品列表响应失败或为空");
                        mallItems.Clear();
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
        string url = serverUrl + ServerUrls.Player.MallPurchase;
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
                        Logger.Log($"[NetServerManager] 成功购买物品 {itemId}, 数量 {quantity}, 总价 {response.totalPrice}, 剩余金币 {response.remainingGold}");

                        playerGold = response.remainingGold;

                        CommunicateEvent.Modify<Dictionary<string, object>>(CommunicateEvent.EVENT_GOLD_CHANGED, new Dictionary<string, object>
                        {
                            { "gold", playerGold },
                            { "add", 0 },
                            { "reduce", response.totalPrice }
                        });
                        CommunicateEvent.Modify<int>(CommunicateEvent.EVENT_GOLD_CHANGED, playerGold);

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

                        bool isNestBait = LoadDataManager.Instance.nestBaitDict.ContainsKey(itemId);
                        if (isNestBait)
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

                        string itemName = GetItemNameById(itemId);
                        string tipMessage = $"🎣 购买成功！\n{itemName} x{quantity}\n花费 {response.totalPrice} 金币";
                        GameUIManager.Instance?.ShowTip(tipMessage);
                    }
                    else
                    {
                        Logger.LogWarning($"[NetServerManager] 购买商城物品失败: {response?.message ?? "未知错误"}");
                        callback?.Invoke(false, response?.message ?? "购买失败");
                        GameUIManager.Instance?.ShowTip($"购买失败：{response?.message ?? "未知错误"}");
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[NetServerManager] 解析购买商城物品响应失败: {ex.Message}");
                    callback?.Invoke(false, "解析响应失败");
                    GameUIManager.Instance?.ShowTip("购买失败，请重试");
                }
            }
            else
            {
                Logger.LogError($"[NetServerManager] 购买商城物品请求失败: {request.error}");
                callback?.Invoke(false, request.error);
                GameUIManager.Instance?.ShowTip("网络请求失败，请检查网络");
            }
        }
    }

    //private string GetItemNameById(int itemId)
    //{
    //    if (LoadDataManager.Instance != null)
    //    {
    //        var itemData = LoadDataManager.Instance.GetItemById(itemId);
    //        if (itemData != null)
    //        {
    //            return itemData.name;
    //        }
    //    }
    //    return $"物品#{itemId}";
    //}

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
        public bool isUnique;
        public bool isOnSale;
        public string? name;
        public string? description;
        public int? type;
        public int? count;
        public int? iconId;
        public bool? isHot;
        public bool? isNew;
    }

    [System.Serializable]
    private class PurchaseMallItemResponse
    {
        public bool success;
        public string message;
        public int totalPrice;
        public int remainingGold;
        public bool isIslandInfo;
        public bool isCollectionInfo;
    }
}
