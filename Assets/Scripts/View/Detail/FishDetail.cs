using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace View.Detail
{
    public class FishDetail : MonoBehaviour
    {
        public Transform contentTransform;
        private GameObject fishBagItemPrefab;

        private Dictionary<int, List<UI_FishBagPrefab>> fishPrefabs = new Dictionary<int, List<UI_FishBagPrefab>>();
        private HashSet<int> currentItemIds = new HashSet<int>();

        private System.Action<UI_FishBagPrefab> onFishSelectionChanged;

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

        public void UpdateFishItems(Dictionary<int, ItemData> itemDataMap, Dictionary<int, int> inventory)
        {
            currentItemIds.Clear();

            if (inventory == null || inventory.Count == 0)
            {
                ReturnAllFishToPool();
                return;
            }

            foreach (var item in inventory)
            {
                int itemId = item.Key;
                int quantity = item.Value;

                if (itemDataMap.TryGetValue(itemId, out ItemData itemData) && itemData != null)
                {
                    currentItemIds.Add(itemId);
                    HandleFishItemStacking(itemId, quantity, itemData);
                }
            }

            ReturnUnusedFishToPool();
        }

        private void HandleFishItemStacking(int itemId, int totalQuantity, ItemData itemData)
        {
            // 鱼篓的内容不能堆叠
            int maxStack = 1;
            List<UI_FishBagPrefab> prefabs = GetOrCreateFishPrefabList(itemId);

            int currentPrefabCount = prefabs.Count;
            int neededPrefabCount = CalculateNeededPrefabs(totalQuantity, maxStack);

            if (neededPrefabCount > currentPrefabCount)
            {
                for (int i = currentPrefabCount; i < neededPrefabCount; i++)
                {
                    CreateFishItemPrefab(itemId, 0, itemData, true);
                }
            }
            else if (neededPrefabCount < currentPrefabCount)
            {
                for (int i = currentPrefabCount - 1; i >= neededPrefabCount; i--)
                {
                    UI_FishBagPrefab prefab = prefabs[i];
                    prefab.gameObject.SetActive(false);
                    fishObjectPool.Add(prefab);
                    prefabs.RemoveAt(i);
                }
            }

            prefabs = fishPrefabs[itemId];
            int remainingQuantity = totalQuantity;

            for (int i = 0; i < prefabs.Count && remainingQuantity > 0; i++)
            {
                int stackQuantity = Mathf.Min(remainingQuantity, maxStack);

                if (i < currentPrefabCount)
                {
                    prefabs[i].UpdateQuantity(stackQuantity);
                }
                else
                {
                    prefabs[i].Init(itemId, stackQuantity, itemData, true);
                    prefabs[i].SetSelectionCallback(OnFishSelectionChanged);
                }

                prefabs[i].gameObject.SetActive(true);
                remainingQuantity -= stackQuantity;
            }
        }

        private void OnFishSelectionChanged(UI_FishBagPrefab fishPrefab)
        {
            onFishSelectionChanged?.Invoke(fishPrefab);
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

        private int CalculateNeededPrefabs(int totalQuantity, int maxStack)
        {
            if (maxStack <= 0) return 0;
            return (totalQuantity + maxStack - 1) / maxStack;
        }

        private List<UI_FishBagPrefab> GetOrCreateFishPrefabList(int itemId)
        {
            if (!fishPrefabs.ContainsKey(itemId))
            {
                fishPrefabs[itemId] = new List<UI_FishBagPrefab>();
            }
            return fishPrefabs[itemId];
        }

        private void ReturnAllFishToPool()
        {
            foreach (var prefabs in fishPrefabs.Values)
            {
                foreach (var prefab in prefabs)
                {
                    if (prefab != null)
                    {
                        prefab.Init(0, 0, null);
                        prefab.gameObject.SetActive(false);
                        fishObjectPool.Add(prefab);
                    }
                }
            }
            fishPrefabs.Clear();
            currentItemIds.Clear();
        }

        private void ReturnUnusedFishToPool()
        {
            List<int> toRemove = new List<int>();
            foreach (var kvp in fishPrefabs)
            {
                if (!currentItemIds.Contains(kvp.Key))
                {
                    foreach (var prefab in kvp.Value)
                    {
                        prefab.Init(0, 0, null);
                        prefab.gameObject.SetActive(false);
                        fishObjectPool.Add(prefab);
                    }
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove)
            {
                fishPrefabs.Remove(id);
            }
        }

        private void CreateFishItemPrefab(int itemId, int quantity, ItemData itemData, bool isNewCatch = false)
        {
            if (fishBagItemPrefab == null)
            {
                Debug.LogError("[FishDetail] fishBagItemPrefab is not assigned"); 
                return;
            }

            UI_FishBagPrefab fishItem = GetFishFromPool();

            if (fishItem == null)
            {
                GameObject itemObj = Instantiate(fishBagItemPrefab, contentTransform);
                fishItem = itemObj.GetComponent<UI_FishBagPrefab>();

                if (fishItem == null)
                {
                    Destroy(itemObj);
                    Debug.LogError("[FishDetail] UI_FishBagPrefab component not found");
                    return;
                }
            }
            else
            {
                fishItem.transform.SetParent(contentTransform);
                fishItem.transform.localScale = Vector3.one;
            }

            fishItem.Init(itemId, quantity, itemData, isNewCatch);
            fishItem.SetSelectionCallback(OnFishSelectionChanged);
            fishItem.gameObject.SetActive(true);
            if (!fishPrefabs.ContainsKey(itemId))
            {
                fishPrefabs[itemId] = new List<UI_FishBagPrefab>();
            }
            fishPrefabs[itemId].Add(fishItem);
        }

        private List<UI_FishBagPrefab> fishObjectPool = new List<UI_FishBagPrefab>();

        private UI_FishBagPrefab GetFishFromPool()
        {
            if (fishObjectPool.Count > 0)
            {
                UI_FishBagPrefab item = fishObjectPool[0];
                fishObjectPool.RemoveAt(0);
                return item;
            }
            return null;
        }
    }
}