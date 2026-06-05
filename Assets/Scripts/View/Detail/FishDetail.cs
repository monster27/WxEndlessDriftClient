using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;

namespace View.Detail
{
    public class FishDetail : MonoBehaviour
    {
        public Transform contentTransform;
        private GameObject fishBagItemPrefab;

        private Dictionary<int, List<UI_FishBagPrefab>> fishPrefabs = new Dictionary<int, List<UI_FishBagPrefab>>();

        private System.Action<UI_FishBagPrefab> onFishSelectionChanged;

        // 缓存当前显示的鱼的数量
        private Dictionary<int, int> currentFishInventory = new Dictionary<int, int>();

        public void SetFishBagItemPrefab(GameObject prefab)
        {
            if (fishBagItemPrefab == null)
            {
                fishBagItemPrefab = prefab;
            }
        }

        void Start()
        {
            if (contentTransform == null)
            {
                contentTransform = transform.Find("Content");
            }

            if (fishBagItemPrefab == null)
            {
                Transform prefabTransform = transform.Find("FishBagItemPrefab");
                if (prefabTransform != null)
                {
                    fishBagItemPrefab = prefabTransform.gameObject;
                }
            }
        }

        public void SetOnFishSelectionChanged(System.Action<UI_FishBagPrefab> callback)
        {
            onFishSelectionChanged = callback;
        }

        /// <summary>
        /// 增量更新鱼篓显示
        /// </summary>
        public void UpdateFishItems(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> newInventory)
        {
            if (newInventory == null)
            {
                newInventory = new Dictionary<int, int>();
            }

            // ========== 添加调试日志 ==========
            int totalCount = 0;
            Debug.Log($"[FishDetail] ===== 开始更新鱼篓显示 =====");
            Debug.Log($"[FishDetail] 传入数据 - itemDataMap数量: {itemDataMap?.Count ?? 0}, newInventory物品类型数: {newInventory.Count}");
            foreach (var kvp in newInventory)
            {
                totalCount += kvp.Value;
                Debug.Log($"   [FishDetail] 物品ID: {kvp.Key}, 数量: {kvp.Value}");
            }
            Debug.Log($"   [FishDetail] 鱼篓总数量: {totalCount}");
            Debug.Log($"[FishDetail] 当前缓存 fishPrefabs 中的物品种类: {fishPrefabs.Count}");
            foreach (var kvp in fishPrefabs)
            {
                Debug.Log($"   缓存中 ID={kvp.Key}, 实例数={kvp.Value.Count}");
            }
            // ========== 调试日志结束 ==========

            // 1. 移除不再存在的鱼
            RemoveUnusedFish(newInventory);

            // 2. 更新或添加鱼
            foreach (var item in newInventory)
            {
                int itemId = item.Key;
                int newQuantity = item.Value;

                if (itemDataMap.TryGetValue(itemId, out ItemData itemData) && itemData != null)
                {
                    UpdateOrAddFish(itemId, newQuantity, itemData);
                }
                else
                {
                    // ========== 添加警告日志 ==========
                    Debug.LogWarning($"[FishDetail] 物品ID {itemId} 在 itemDataMap 中未找到！");
                    // ========== 调试日志结束 ==========
                }
            }

            // 3. 更新缓存
            currentFishInventory = new Dictionary<int, int>(newInventory);

            // ========== 添加完成日志 ==========
            Debug.Log($"[FishDetail] ===== 更新完成，当前 fishPrefabs 中的物品种类: {fishPrefabs.Count} =====");
            foreach (var kvp in fishPrefabs)
            {
                Debug.Log($"   最终 ID={kvp.Key}, 实例数={kvp.Value.Count}");
            }
            // ========== 调试日志结束 ==========
        }

        /// <summary>
        /// 移除不再存在的鱼
        /// </summary>
        private void RemoveUnusedFish(Dictionary<int, int> newInventory)
        {
            List<int> toRemove = new List<int>();
            foreach (var kvp in fishPrefabs)
            {
                int itemId = kvp.Key;
                if (!newInventory.ContainsKey(itemId))
                {
                    // 这种鱼完全没有了，回收所有实例
                    Debug.Log($"[FishDetail] 移除完全消失的物品种类: ID={itemId}, 实例数={kvp.Value.Count}");
                    foreach (var prefab in kvp.Value)
                    {
                        ReturnFishToPool(prefab);
                    }
                    toRemove.Add(itemId);
                }
                else
                {
                    // 这种鱼还有，但数量可能减少了，需要移除多余的实例
                    int newQuantity = newInventory[itemId];
                    List<UI_FishBagPrefab> prefabs = kvp.Value;

                    // 如果新数量少于当前实例数，移除多余的
                    if (prefabs.Count > newQuantity)
                    {
                        Debug.Log($"[FishDetail] 物品种类 ID={itemId} 数量减少: 原={prefabs.Count}, 新={newQuantity}");
                        while (prefabs.Count > newQuantity)
                        {
                            UI_FishBagPrefab lastPrefab = prefabs[prefabs.Count - 1];
                            ReturnFishToPool(lastPrefab);
                            prefabs.RemoveAt(prefabs.Count - 1);
                        }
                    }
                }
            }

            foreach (var id in toRemove)
            {
                fishPrefabs.Remove(id);
            }
        }

        /// <summary>
        /// 更新或添加鱼
        /// </summary>
        private void UpdateOrAddFish(int itemId, int newQuantity, ItemData itemData)
        {
            // ========== 添加调试日志 ==========
            Debug.Log($"[FishDetail] UpdateOrAddFish - ID={itemId}, 名称={itemData?.name}, newQuantity={newQuantity}");
            // ========== 调试日志结束 ==========

            if (!fishPrefabs.ContainsKey(itemId))
            {
                fishPrefabs[itemId] = new List<UI_FishBagPrefab>();
                Debug.Log($"[FishDetail]   新建物种类别: ID={itemId}");
            }

            List<UI_FishBagPrefab> prefabs = fishPrefabs[itemId];
            int currentCount = prefabs.Count;

            Debug.Log($"[FishDetail]   当前已有实例数: {currentCount}");

            // 如果数量增加，创建新的实例
            for (int i = currentCount; i < newQuantity; i++)
            {
                // 检查是否是新增的鱼（用于显示新鱼标记）
                bool isNewlyAdded = !currentFishInventory.ContainsKey(itemId) || i >= (currentFishInventory.TryGetValue(itemId, out int oldCount) ? oldCount : 0);
                Debug.Log($"[FishDetail]   创建新实例: index={i}, isNewlyAdded={isNewlyAdded}");
                UI_FishBagPrefab newFish = CreateFishItemPrefab(itemId, 1, itemData, isNewlyAdded);
                if (newFish != null)
                {
                    prefabs.Add(newFish);
                    Debug.Log($"[FishDetail]   创建成功，当前实例数: {prefabs.Count}");
                }
                else
                {
                    Debug.LogError($"[FishDetail]   创建失败！newFish 为 null");
                }
            }

            // 更新所有实例的显示
            for (int i = 0; i < prefabs.Count && i < newQuantity; i++)
            {
                UI_FishBagPrefab fish = prefabs[i];
                if (fish.ItemId != itemId)
                {
                    fish.Init(itemId, 1, itemData, false);
                }
                fish.UpdateQuantity(1);
                fish.gameObject.SetActive(true);
            }

            Debug.Log($"[FishDetail] UpdateOrAddFish 完成 - ID={itemId}, 最终实例数={prefabs.Count}");
        }

        private void ReturnFishToPool(UI_FishBagPrefab fish)
        {
            if (fish != null)
            {
                fish.SetSelection(false);
                fish.gameObject.SetActive(false);
                fishObjectPool.Add(fish);
            }
        }

        public List<UI_FishBagPrefab> GetAllFishPrefabs()
        {
            List<UI_FishBagPrefab> allPrefabs = new List<UI_FishBagPrefab>();
            foreach (var prefabs in fishPrefabs.Values)
            {
                foreach (var prefab in prefabs)
                {
                    if (prefab != null && prefab.gameObject.activeSelf)
                    {
                        allPrefabs.Add(prefab);
                    }
                }
            }
            return allPrefabs;
        }

        private UI_FishBagPrefab CreateFishItemPrefab(int itemId, int quantity, ItemData itemData, bool isNewCatch = false)
        {
            // ========== 添加调试日志 ==========
            Debug.Log($"[FishDetail] CreateFishItemPrefab - itemId={itemId}, quantity={quantity}, isNewCatch={isNewCatch}, 物品名称={(itemData != null ? itemData.name : "null")}");
            // ========== 调试日志结束 ==========

            if (fishBagItemPrefab == null)
            {
                Debug.LogError("[FishDetail] fishBagItemPrefab is not assigned");
                return null;
            }

            UI_FishBagPrefab fishItem = GetFishFromPool();

            if (fishItem == null)
            {
                Debug.Log($"[FishDetail] 从池中获取失败，创建新实例");
                GameObject itemObj = Instantiate(fishBagItemPrefab, contentTransform);
                fishItem = itemObj.GetComponent<UI_FishBagPrefab>();

                if (fishItem == null)
                {
                    Destroy(itemObj);
                    Debug.LogError("[FishDetail] UI_FishBagPrefab component not found");
                    return null;
                }
                Debug.Log($"[FishDetail] 新实例创建成功");
            }
            else
            {
                Debug.Log($"[FishDetail] 从池中获取实例成功");
                fishItem.transform.SetParent(contentTransform);
                fishItem.transform.localScale = Vector3.one;
            }

            fishItem.Init(itemId, quantity, itemData, isNewCatch);
            fishItem.SetSelectionCallback(OnFishSelectionChanged);
            fishItem.SetSelection(false);
            fishItem.gameObject.SetActive(true);

            Debug.Log($"[FishDetail] CreateFishItemPrefab 完成 - itemId={itemId}");
            return fishItem;
        }

        private void OnFishSelectionChanged(UI_FishBagPrefab fishPrefab)
        {
            onFishSelectionChanged?.Invoke(fishPrefab);
        }

        private List<UI_FishBagPrefab> fishObjectPool = new List<UI_FishBagPrefab>();

        private UI_FishBagPrefab GetFishFromPool()
        {
            if (fishObjectPool.Count > 0)
            {
                UI_FishBagPrefab item = fishObjectPool[0];
                fishObjectPool.RemoveAt(0);
                Debug.Log($"[FishDetail] 从池中获取物品, 池剩余: {fishObjectPool.Count}");
                return item;
            }
            return null;
        }
    }
}