using System.Collections.Generic;
using UnityEngine;

public class MallServerManager
{
    private static MallServerManager instance;
    public static MallServerManager Instance => instance;

    private Dictionary<int, MallItemData> mallItems = new Dictionary<int, MallItemData>();
    private bool isInitialized = false;

    public void Initialize()
    {
        if (instance == null)
        {
            instance = this;
        }
        InitMallData();
        Debug.Log("[MallServerManager] 商城服务器管理器初始化完成");
    }

    private void InitMallData()
    {
        mallItems.Clear();

        // 从JSON文件加载所有鱼饵数据
        TextAsset baitJson = Resources.Load<TextAsset>("JsonData/Game/BagItem/baits");
        if (baitJson != null)
        {
            BaitListWrapper baitWrapper = JsonUtility.FromJson<BaitListWrapper>(baitJson.text);
            if (baitWrapper != null && baitWrapper.baits != null && baitWrapper.baits.Length > 0)
            {
                int[] prices = { 10, 15, 20, 25, 30, 35, 100 };
                int totalStock = 9;

                for (int i = 0; i < baitWrapper.baits.Length; i++)
                {
                    BaitData bait = baitWrapper.baits[i];
                    int price = i < prices.Length ? prices[i] : 50;
                    int stock = totalStock;

                    mallItems[bait.id] = new MallItemData
                    {
                        itemId = bait.id,
                        price = price,
                        stock = stock
                    };

                    Debug.Log($"[MallServerManager] 添加商品: ID={bait.id}, 名称={bait.name}, 价格={price}, 库存={stock}");
                }
            }
        }
        else
        {
            Debug.LogError("[MallServerManager] 无法加载baits.json文件");
            // 备用数据
            int[] baitIds = { 2001, 2002, 2003, 2004, 2005, 2006, 2007 };
            int[] prices = { 10, 15, 20, 25, 30, 35, 100 };
            int totalStock = 9;

            for (int i = 0; i < baitIds.Length; i++)
            {
                int itemId = baitIds[i];
                int price = prices[i];
                int stock = totalStock;

                mallItems[itemId] = new MallItemData
                {
                    itemId = itemId,
                    price = price,
                    stock = stock
                };
            }
        }

        isInitialized = true;
        Debug.Log($"[MallServerManager] 商城数据初始化完成，共 {mallItems.Count} 种商品");
    }

    public Dictionary<int, MallItemData> GetAllMallItems()
    {
        return new Dictionary<int, MallItemData>(mallItems);
    }

    public MallItemData GetMallItem(int itemId)
    {
        return mallItems.ContainsKey(itemId) ? mallItems[itemId] : null;
    }

    public bool IsItemAvailable(int itemId)
    {
        if (!mallItems.ContainsKey(itemId))
            return false;
        return mallItems[itemId].stock > 0;
    }

    public int GetMaxPurchasableCount(int itemId, int playerGold)
    {
        if (!mallItems.ContainsKey(itemId))
            return 0;

        MallItemData item = mallItems[itemId];
        int canAffordCount = playerGold / item.price;
        int stockLimitedCount = item.stock;
        return Mathf.Min(canAffordCount, stockLimitedCount);
    }

    public bool PurchaseItem(int itemId, int quantity, int playerGold, out string message)
    {
        message = "";

        if (!mallItems.ContainsKey(itemId))
        {
            message = "商品不存在";
            return false;
        }

        MallItemData item = mallItems[itemId];

        if (item.stock < quantity)
        {
            message = "库存不足";
            return false;
        }

        int totalPrice = item.price * quantity;
        if (playerGold < totalPrice)
        {
            message = "金币不足";
            return false;
        }

        item.stock -= quantity;
        Debug.Log($"[MallServerManager] 购买成功: 商品ID={itemId}, 数量={quantity}, 剩余库存={item.stock}");

        CommunicateEvent.Modify(CommunicateEvent.EVENT_MALL_DATA_CHANGED, GetAllMallItems());

        return true;
    }

    public bool IsInitialized => isInitialized;
}