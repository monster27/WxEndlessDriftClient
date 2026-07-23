using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using SharedModels;

namespace View.Detail
{
    public class BagDetail : MonoBehaviour
    {
        public Transform contentTransform;
        [SerializeField] private GameObject bagItemPrefab;

        private Dictionary<int, List<UI_BagPrefab>> itemPrefabs = new Dictionary<int, List<UI_BagPrefab>>();
        private HashSet<int> currentItemIds = new HashSet<int>();
        private List<UI_BagPrefab> objectPool = new List<UI_BagPrefab>();

        void Start()
        {
            if (contentTransform == null)
            {
                contentTransform = transform.Find("Content");
            }
            
            CommunicateEvent.Register<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipmentChanged);
        }
        
        void OnDestroy()
        {
            // 取消注册装备状态更新事件
            CommunicateEvent.Unregister<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_CHANGED, OnEquipmentChanged);
        }
        
        /// <summary>
        /// 装备状态更新事件处理器
        /// </summary>
        private void OnEquipmentChanged((EquipmentSlotType, int) data)
        {
            EquipmentSlotType slotType = data.Item1;
            int itemId = data.Item2;
            
            Debug.Log($"[BagDetail] 接收到装备状态更新事件: SlotType={slotType}, ItemId={itemId}");
            
            // 如果是鱼饵槽位的装备变化
            if (slotType == EquipmentSlotType.Bait)
            {
                UpdateBaitEquippedState(itemId);
            }
        }
        
        /// <summary>
        /// 更新鱼饵的装备状态显示
        /// </summary>
        private void UpdateBaitEquippedState(int newBaitId)
        {
            Debug.Log($"[BagDetail] UpdateBaitEquippedState - newBaitId={newBaitId}");
            
            foreach (var kvp in itemPrefabs)
            {
                int itemId = kvp.Key;
                bool shouldBeEquipped = false;
                
                if (itemId == 0)
                {
                    shouldBeEquipped = (newBaitId == 0);
                }
                else
                {
                    shouldBeEquipped = (itemId == newBaitId);
                }
                
                foreach (var prefab in kvp.Value)
                {
                    if (prefab != null)
                    {
                        prefab.SetEquipped(shouldBeEquipped);
                        Debug.Log($"[BagDetail] 更新物品装备状态: itemId={itemId}, shouldBeEquipped={shouldBeEquipped}, activeSelf={prefab.gameObject.activeSelf}");
                    }
                }
            }
        }

        /// <summary>
        /// 按物品类型更新物品显示
        /// </summary>
        public void UpdateItems(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> inventory, int itemType)
        {
            currentItemIds.Clear();

            if (inventory == null || inventory.Count == 0)
            {
                ReturnAllToPool();
                return;
            }

            foreach (var item in inventory)
            {
                int itemId = item.Key;
                int quantity = item.Value;

                if (itemDataMap.TryGetValue(itemId, out ItemData itemData) && itemData != null && itemData.itemType == itemType)
                {
                    currentItemIds.Add(itemId);
                    HandleItemStacking(itemId, quantity, itemData);
                }
            }

            ReturnUnusedToPool();
        }

        /// <summary>
        /// 更新所有物品显示
        /// </summary>
        public void UpdateAllItems(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> inventory)
        {
            currentItemIds.Clear();

            if (inventory == null || inventory.Count == 0)
            {
                ReturnAllToPool();
                return;
            }

            foreach (var item in inventory)
            {
                int itemId = item.Key;
                int quantity = item.Value;

                if (itemDataMap.TryGetValue(itemId, out ItemData itemData) && itemData != null)
                {
                    currentItemIds.Add(itemId);
                    HandleItemStacking(itemId, quantity, itemData);
                }
            }

            ReturnUnusedToPool();
        }

        /// <summary>
        /// 按小分类更新物品显示（新方法）
        /// </summary>
        public void UpdateItemsBySubCategory(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> inventory, List<int> categoryIds)
        {
            currentItemIds.Clear();

            if (inventory == null || inventory.Count == 0 || categoryIds == null || categoryIds.Count == 0)
            {
                ReturnAllToPool();
                return;
            }

            foreach (var item in inventory)
            {
                int itemId = item.Key;
                int quantity = item.Value;

                if (itemDataMap.TryGetValue(itemId, out ItemData itemData) && itemData != null)
                {
                    // 检查物品的categoryId是否在指定的小分类列表中
                    if (categoryIds.Contains(itemData.categoryId))
                    {
                        currentItemIds.Add(itemId);
                        HandleItemStacking(itemId, quantity, itemData);
                    }
                }
            }

            ReturnUnusedToPool();
        }

        /// <summary>
        /// 按单个小分类更新物品显示（新方法）
        /// </summary>
        public void UpdateItemsBySingleCategory(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> inventory, int categoryId)
        {
            Debug.Log($"[BagDetail] UpdateItemsBySingleCategory - 分类ID: {categoryId}, 物品数: {inventory?.Count ?? 0}");

            if (categoryId == 21)
            {
                Debug.Log("[BagDetail] 检测到鱼饵分类，调用 UpdateBaitItemsWithNoBaitOption");
                UpdateBaitItemsWithNoBaitOption(itemDataMap, inventory);
                return;
            }

            int count = 0;
            foreach (var item in inventory)
            {
                if (itemDataMap.TryGetValue(item.Key, out ItemData itemData) && itemData != null)
                {
                    if (itemData.categoryId == categoryId)
                    {
                        count++;
                        Debug.Log($"[BagDetail] 分类 {categoryId} 包含物品: ID={item.Key}, 名称={itemData.name}, 数量={item.Value}");
                    }
                }
            }
            Debug.Log($"[BagDetail] 分类 {categoryId} 共有 {count} 种物品");

            UpdateItemsBySubCategory(itemDataMap, inventory, new List<int> { categoryId });
        }

        /// <summary>
        /// 更新鱼饵物品，添加"无鱼饵"选项
        /// </summary>
        private void UpdateBaitItemsWithNoBaitOption(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> inventory)
        {
            currentItemIds.Clear();

            int equippedBaitId = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Bait);
            bool noBaitEquipped = equippedBaitId == 0;

            Debug.Log($"[BagDetail] UpdateBaitItemsWithNoBaitOption - equippedBaitId={equippedBaitId}, noBaitEquipped={noBaitEquipped}");

            ItemData noBaitData = new ItemData
            {
                id = 0,
                name = "无鱼饵",
                iconPath = "UI/Icon/BaitIcons/0",
                itemType = 2,
                categoryId = 21
            };

            currentItemIds.Add(0);
            Debug.Log($"[BagDetail] 创建无鱼饵选项 - isEquipped={noBaitEquipped}");
            HandleItemStacking(0, 1, noBaitData, noBaitEquipped);

            if (inventory != null)
            {
                foreach (var item in inventory)
                {
                    int itemId = item.Key;
                    int quantity = item.Value;

                    if (itemDataMap.TryGetValue(itemId, out ItemData itemData) && itemData != null)
                    {
                        if (itemData.categoryId == 21)
                        {
                            currentItemIds.Add(itemId);
                            bool isEquipped = (itemId == equippedBaitId);
                            Debug.Log($"[BagDetail] 创建鱼饵选项 - itemId={itemId}, quantity={quantity}, isEquipped={isEquipped}");
                            HandleItemStacking(itemId, quantity, itemData, isEquipped);
                        }
                    }
                }
            }

            ReturnUnusedToPool();
        }

        /// <summary>
        /// 按大分类更新物品显示（新方法）
        /// </summary>
        public void UpdateItemsByMainCategory(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> inventory, int mainCategoryId)
        {
            currentItemIds.Clear();

            if (inventory == null || inventory.Count == 0)
            {
                ReturnAllToPool();
                return;
            }

            foreach (var item in inventory)
            {
                int itemId = item.Key;
                int quantity = item.Value;

                if (itemDataMap.TryGetValue(itemId, out ItemData itemData) && itemData != null)
                {
                    // 根据物品ID范围判断大分类
                    int itemCategory = GetMainCategoryByItemId(itemId);
                    if (itemCategory == mainCategoryId)
                    {
                        currentItemIds.Add(itemId);
                        HandleItemStacking(itemId, quantity, itemData);
                    }
                }
            }

            ReturnUnusedToPool();
        }

        /// <summary>
        /// 根据物品ID获取大分类ID
        /// </summary>
        private int GetMainCategoryByItemId(int itemId)
        {
            if (itemId >= 1001 && itemId <= 1999) return 1;  // 水产
            if (itemId >= 2001 && itemId <= 2999) return 2;  // 饵料
            if (itemId >= 3001 && itemId <= 3999) return 3;  // 装备
            if (itemId >= 4001 && itemId <= 4999) return 4;  // 装饰
            if (itemId >= 5001 && itemId <= 5999) return 5;  // 宠物
            if (itemId >= 6001 && itemId <= 6999) return 6;  // 特殊
            if (itemId >= 9001 && itemId <= 9999) return 9;  // 垃圾
            return 0;
        }

        public void UpdateItemsWithData(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap, int itemType)
        {
            UpdateItems(itemDataMap, inventory, itemType);
        }

        public void UpdateAllItemsWithData(Dictionary<int, int> inventory, Dictionary<int, ItemData> itemDataMap)
        {
            UpdateAllItems(itemDataMap, inventory);
        }

        private void HandleItemStacking(int itemId, int totalQuantity, ItemData itemData)
        {
            HandleItemStacking(itemId, totalQuantity, itemData, IsItemEquipped(itemId));
        }

        private void HandleItemStacking(int itemId, int totalQuantity, ItemData itemData, bool isEquipped)
        {
            int maxStack = itemData.categoryId == 1 ? 1 : 99;
            List<UI_BagPrefab> prefabs = GetOrCreatePrefabList(itemId);

            int currentPrefabCount = prefabs.Count;
            int neededPrefabCount = CalculateNeededPrefabs(totalQuantity, maxStack);
            
            if (isEquipped && totalQuantity == 0)
            {
                neededPrefabCount = 1;
            }

            if (neededPrefabCount > currentPrefabCount)
            {
                for (int i = currentPrefabCount; i < neededPrefabCount; i++)
                {
                    CreateItemPrefab(itemId, 0, itemData);
                }
            }

            prefabs = itemPrefabs[itemId];
            int remainingQuantity = totalQuantity;

            for (int i = 0; i < prefabs.Count; i++)
            {
                if (remainingQuantity > 0)
                {
                    int stackQuantity = Mathf.Min(remainingQuantity, maxStack);
                    prefabs[i].Init(itemId, stackQuantity, itemData, isEquipped);
                    prefabs[i].gameObject.SetActive(true);
                    remainingQuantity -= stackQuantity;
                }
                else if (isEquipped && i == 0)
                {
                    prefabs[i].Init(itemId, 0, itemData, isEquipped);
                    prefabs[i].gameObject.SetActive(true);
                }
                else
                {
                    prefabs[i].Init(itemId, 0, itemData, isEquipped);
                    prefabs[i].gameObject.SetActive(false);
                    objectPool.Add(prefabs[i]);
                }
            }
        }

        /// <summary>
        /// 检查物品是否已装备
        /// </summary>
        private bool IsItemEquipped(int itemId)
        {
            return CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_ITEM_EQUIPPED, itemId);
        }

        private int CalculateNeededPrefabs(int totalQuantity, int maxStack)
        {
            if (maxStack <= 0) return 0;
            return (totalQuantity + maxStack - 1) / maxStack;
        }

        private List<UI_BagPrefab> GetOrCreatePrefabList(int itemId)
        {
            if (!itemPrefabs.ContainsKey(itemId))
            {
                itemPrefabs[itemId] = new List<UI_BagPrefab>();
            }
            return itemPrefabs[itemId];
        }

        private void ReturnAllToPool()
        {
            foreach (var prefabs in itemPrefabs.Values)
            {
                foreach (var prefab in prefabs)
                {
                    if (prefab != null)
                    {
                        prefab.Init(0, 0, null);
                        prefab.gameObject.SetActive(false);
                        objectPool.Add(prefab);
                    }
                }
            }
            itemPrefabs.Clear();
            currentItemIds.Clear();
        }

        private void ReturnUnusedToPool()
        {
            List<int> toRemove = new List<int>();
            foreach (var kvp in itemPrefabs)
            {
                if (!currentItemIds.Contains(kvp.Key))
                {
                    foreach (var prefab in kvp.Value)
                    {
                        // 【修复】添加null检查，防止prefab为null时崩溃
                        if (prefab != null)
                        {
                            prefab.Init(0, 0, null);
                            prefab.gameObject.SetActive(false);
                            objectPool.Add(prefab);
                        }
                        else
                        {
                            Debug.LogWarning($"[BagDetail] ReturnUnusedToPool - prefab为null");
                        }
                    }
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove)
            {
                itemPrefabs.Remove(id);
            }
        }

        private void CreateItemPrefab(int itemId, int quantity, ItemData itemData)
        {
            UI_BagPrefab prefab = null;
            
            // 先从对象池获取
            if (objectPool.Count > 0)
            {
                prefab = objectPool[objectPool.Count - 1];
                objectPool.RemoveAt(objectPool.Count - 1);
            }
            else
            {
                // 创建新的预制件
                GameObject obj = Instantiate(bagItemPrefab, contentTransform);
                prefab = obj.GetComponent<UI_BagPrefab>();
            }

            if (prefab != null)
            {
                prefab.gameObject.SetActive(true);
                itemPrefabs[itemId].Add(prefab);
            }
        }
    }
}