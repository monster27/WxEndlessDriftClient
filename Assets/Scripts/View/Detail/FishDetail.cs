using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Linq;
using SharedModels;

namespace View.Detail
{
    public class FishDetail : MonoBehaviour
    {
        public Transform contentTransform;
        private GameObject fishBagItemPrefab;

        private Dictionary<int, List<UI_FishBagPrefab>> fishPrefabs = new Dictionary<int, List<UI_FishBagPrefab>>();

        private System.Action<UI_FishBagPrefab> onFishSelectionChanged;

        private Dictionary<int, int> currentFishInventory = new Dictionary<int, int>();

        private Dictionary<int, ItemData> currentItemDataMap = new Dictionary<int, ItemData>();

        private Dictionary<int, List<FishDetailData>> currentFishDetailData = new Dictionary<int, List<FishDetailData>>();

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

        public void UpdateFishItems(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> newInventory)
        {
            UpdateFishItems(itemDataMap, newInventory, null);
        }

        public void UpdateFishItems(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> newInventory, Dictionary<int, List<FishDetailData>> detailData)
        {
            if (newInventory == null) newInventory = new Dictionary<int, int>();

            Debug.Log($"[FishDetail] ===== 开始更新鱼篓显示 =====");
            Debug.Log($"[FishDetail] 传入数据 - itemDataMap数量: {itemDataMap?.Count ?? 0}, newInventory物品类型数: {newInventory.Count}, detailData数量: {detailData?.Count ?? 0}");

            if (detailData != null && detailData.Count > 0)
            {
                RemoveUnusedFishByDetail(detailData);

                foreach (var kvp in detailData)
                {
                    int itemId = kvp.Key;
                    List<FishDetailData> fishDetails = kvp.Value;
                    if (itemDataMap.TryGetValue(itemId, out ItemData itemData))
                    {
                        UpdateOrAddFish(itemId, fishDetails.Count, itemData, fishDetails);
                    }
                }
            }
            else
            {
                RemoveUnusedFish(newInventory);
                foreach (var item in newInventory)
                {
                    if (itemDataMap.TryGetValue(item.Key, out ItemData itemData))
                    {
                        UpdateOrAddFish(item.Key, item.Value, itemData, null);
                    }
                }
            }

            currentFishInventory = new Dictionary<int, int>(newInventory);
            currentItemDataMap = new Dictionary<int, ItemData>(itemDataMap);
            currentFishDetailData = detailData != null ? new Dictionary<int, List<FishDetailData>>(detailData) : new Dictionary<int, List<FishDetailData>>();

            Debug.Log($"[FishDetail] ===== 更新完成，当前 fishPrefabs 中的物品种类: {fishPrefabs.Count} =====");
            foreach (var kvp in fishPrefabs)
            {
                Debug.Log($"   最终 ID={kvp.Key}, 实例数={kvp.Value.Count}");
            }
        }

        /// <summary>
        /// 根据详情数据移除不再存在的鱼
        /// </summary>
        private void RemoveUnusedFishByDetail(Dictionary<int, List<FishDetailData>> detailData)
        {
            List<int> toRemove = new List<int>();
            foreach (var kvp in fishPrefabs)
            {
                int itemId = kvp.Key;
                if (!detailData.ContainsKey(itemId))
                {
                    foreach (var prefab in kvp.Value)
                    {
                        ReturnFishToPool(prefab);
                    }
                    toRemove.Add(itemId);
                }
                else
                {
                    int newQuantity = detailData[itemId].Count;
                    List<UI_FishBagPrefab> prefabs = kvp.Value;

                    if (prefabs.Count > newQuantity)
                    {
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

        private void UpdateOrAddFish(int itemId, int newQuantity, ItemData itemData, List<FishDetailData> fishDetails = null)
        {
            Debug.Log($"[FishDetail] UpdateOrAddFish - ID={itemId}, 名称={itemData?.name}, newQuantity={newQuantity}, 详情条数={fishDetails?.Count ?? 0}");

            if (!fishPrefabs.ContainsKey(itemId)) fishPrefabs[itemId] = new List<UI_FishBagPrefab>();
            List<UI_FishBagPrefab> prefabs = fishPrefabs[itemId];

            while (prefabs.Count > newQuantity)
            {
                ReturnFishToPool(prefabs[prefabs.Count - 1]);
                prefabs.RemoveAt(prefabs.Count - 1);
            }

            for (int i = 0; i < newQuantity; i++)
            {
                FishDetailData detail = (fishDetails != null && i < fishDetails.Count) ? fishDetails[i] : CreateDefaultDetail(itemId, itemData);

                if (i < prefabs.Count)
                {
                    UI_FishBagPrefab existing = prefabs[i];
                    existing.Init(itemId, 1, itemData, existing.IsNewCatch, detail);
                }
                else
                {
                    UI_FishBagPrefab newFish = CreateFishItemPrefab(itemId, 1, itemData, true, detail);
                    if (newFish != null) prefabs.Add(newFish);
                }
            }

            Debug.Log($"[FishDetail] UpdateOrAddFish 完成 - ID={itemId}, 最终实例数={prefabs.Count}");
        }

        private FishDetailData CreateDefaultDetail(int itemId, ItemData itemData)
        {
            return new FishDetailData
            {
                fishId = itemId,
                weight = GetItemWeight(itemId),
                starRatingId = 0,
                calculatedPrice = itemData?.sellPrice ?? 0,
                caughtTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
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

        // ✅ 修改：按重量排序时使用实际重量（从 FishDetailData 获取）
        public void SortFishItems(FishBagView.SortType sortType)
        {
            Debug.Log($"[FishDetail] SortFishItems - 排序类型: {sortType}");

            if (contentTransform == null || fishPrefabs.Count == 0)
            {
                Debug.Log("[FishDetail] SortFishItems - 没有需要排序的鱼");
                return;
            }

            List<UI_FishBagPrefab> allActivePrefabs = GetAllFishPrefabs();
            if (allActivePrefabs.Count == 0)
            {
                Debug.Log("[FishDetail] SortFishItems - 没有活动的鱼预制体");
                return;
            }

            // 根据排序类型进行排序
            switch (sortType)
            {
                case FishBagView.SortType.Rarity:
                    // 稀有度降序
                    allActivePrefabs.Sort((a, b) => b.FishRarityId.CompareTo(a.FishRarityId));
                    Debug.Log($"[FishDetail] 按稀有度排序（降序）");
                    break;

                case FishBagView.SortType.CatchOrder:
                    // ✅ 修复：钓获顺序使用实际时间戳
                    allActivePrefabs.Sort((a, b) =>
                    {
                        long diff = a.CatchTimestamp - b.CatchTimestamp;
                        if (diff != 0) return diff > 0 ? 1 : -1;
                        return b.FishRarityId.CompareTo(a.FishRarityId);
                    });
                    Debug.Log($"[FishDetail] 按钓获时间排序（升序，最先钓上来的在前）");
                    break;

                case FishBagView.SortType.Price:
                    // 价格降序
                    allActivePrefabs.Sort((a, b) =>
                    {
                        int priceA = a.GetTotalSellPrice();
                        int priceB = b.GetTotalSellPrice();
                        return priceB.CompareTo(priceA);
                    });
                    Debug.Log($"[FishDetail] 按价格排序（降序）");
                    break;

                case FishBagView.SortType.Weight:
                    // ✅ 修复：重量降序，使用实际重量
                    allActivePrefabs.Sort((a, b) => b.FishWeight.CompareTo(a.FishWeight));
                    Debug.Log($"[FishDetail] 按重量排序（降序）");
                    break;

                default:
                    // 默认按稀有度降序
                    allActivePrefabs.Sort((a, b) => b.FishRarityId.CompareTo(a.FishRarityId));
                    Debug.Log($"[FishDetail] 默认按稀有度排序（降序）");
                    break;
            }

            for (int i = 0; i < allActivePrefabs.Count; i++)
            {
                allActivePrefabs[i].transform.SetSiblingIndex(i);
            }

            Debug.Log($"[FishDetail] SortFishItems - 完成排序，共 {allActivePrefabs.Count} 条鱼");
        }

        private float GetSortValue(UI_FishBagPrefab prefab, FishBagView.SortType sortType)
        {
            int itemId = prefab.ItemId;

            switch (sortType)
            {
                case FishBagView.SortType.CatchOrder:
                    return itemId;

                case FishBagView.SortType.Rarity:
                    return GetRarityValue(itemId);

                case FishBagView.SortType.Price:
                    return GetPriceValue(itemId);

                case FishBagView.SortType.Weight:
                    return GetWeightValue(itemId);

                default:
                    return itemId;
            }
        }

        private float GetRarityValue(int itemId)
        {
            FishData fishData = LoadDataManager.Instance?.GetFishById(itemId);
            if (fishData != null)
            {
                return fishData.rarityId;
            }
            return 0;
        }

        private float GetPriceValue(int itemId)
        {
            if (currentItemDataMap.TryGetValue(itemId, out ItemData itemData))
            {
                return itemData.sellPrice;
            }
            return 0;
        }

        // ✅ 修改：获取鱼的重量，优先从详情数据获取
        private float GetWeightValue(int itemId)
        {
            // 尝试从详情数据获取实际重量
            if (currentFishDetailData != null && currentFishDetailData.ContainsKey(itemId))
            {
                var details = currentFishDetailData[itemId];
                if (details != null && details.Count > 0)
                {
                    // 返回第一条鱼的重量（用于排序，实际排序使用每条鱼的具体重量）
                    return details[0].weight;
                }
            }

            FishData fishData = LoadDataManager.Instance?.GetFishById(itemId);
            if (fishData != null)
            {
                return fishData.baseWeight;
            }
            return 0;
        }

        // ✅ 新增：获取鱼的默认重量
        private float GetItemWeight(int itemId)
        {
            FishData fishData = LoadDataManager.Instance?.GetFishById(itemId);
            if (fishData != null)
            {
                return fishData.baseWeight;
            }
            return 0f;
        }

        // ✅ 修改：创建鱼预制体时传递详情数据
        private UI_FishBagPrefab CreateFishItemPrefab(int itemId, int quantity, ItemData itemData, bool isNewCatch = false, FishDetailData detail = null)
        {
            Debug.Log($"[FishDetail] CreateFishItemPrefab - itemId={itemId}, quantity={quantity}, isNewCatch={isNewCatch}, 物品名称={(itemData != null ? itemData.name : "null")}, 重量={(detail != null ? detail.weight.ToString() : "无")}, 星级ID={(detail != null ? detail.starRatingId.ToString() : "无")}");

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

            // ✅ 关键：传递详情数据到 UI_FishBagPrefab
            fishItem.Init(itemId, quantity, itemData, isNewCatch, detail);
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