using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace View.Detail
{
    public class UI_FishBagPrefab : MonoBehaviour
    {
        public Image iconImage;
        public Text quantityText;
        public Text nameText;
        public Text weightText;
        public Text priceText;
        public Button selectButton;
        public Image selectedImage;
        public Image newCatchImage;

        private int itemId;
        private int quantity;
        private ItemData itemData;
        private bool isSelected = false;
        private bool hasBeenSelected = false;

        public int ItemId => itemId;
        public int Quantity => quantity;
        public bool IsSelected => isSelected;
        public ItemData ItemDataRef => itemData;

        public event System.Action<UI_FishBagPrefab> OnSelectionChanged;

        void Start()
        {
            if (selectButton != null)
            {
                selectButton.onClick.AddListener(OnSelectButtonClick);
            }

            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(false);
            }
        }

        public void Init(int id, int qty, ItemData data, bool isNewCatch = false)
        {
            itemId = id;
            quantity = qty;
            itemData = data;
            isSelected = false;
            hasBeenSelected = false;

            UpdateDisplay();
            UpdateNewCatchStatus(isNewCatch);
            UpdateSelectedVisual();
        }

        public void SetSelectionCallback(System.Action<UI_FishBagPrefab> callback)
        {
            OnSelectionChanged = callback;
        }

        private void UpdateDisplay()
        {
            if (nameText != null && itemData != null)
            {
                nameText.text = itemData.name;
            }

            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(false);
            }

            if (weightText != null && itemData != null)
            {
                float weight = GetItemWeight(itemId);
                if (weight > 0)
                {
                    weightText.text = $"{weight:F1}kg";
                    weightText.gameObject.SetActive(true);
                }
                else
                {
                    weightText.text = "";
                    weightText.gameObject.SetActive(false);
                }
            }

            if (priceText != null && itemData != null)
            {
                if (itemData.sellPrice > 0)
                {
                    priceText.text = $"¥{itemData.sellPrice}";
                    priceText.gameObject.SetActive(true);
                }
                else
                {
                    priceText.text = "";
                    priceText.gameObject.SetActive(false);
                }
            }

            if (iconImage != null && itemData != null)
            {
                LoadIcon();
            }
        }

        public void UpdateQuantity(int newQuantity)
        {
            quantity = newQuantity;
            if (quantityText != null && quantity > 0)
            {
                quantityText.text = quantity.ToString();
                quantityText.gameObject.SetActive(true);
            }
            else if (quantityText != null)
            {
                quantityText.gameObject.SetActive(false);
            }
        }

        public void UpdateNewCatchStatus(bool isNew)
        {
            if (newCatchImage != null)
            {
                newCatchImage.gameObject.SetActive(isNew);
            }
        }

        public void SetSelection(bool selected)
        {
            if (selected && !hasBeenSelected)
            {
                hasBeenSelected = true;
                UpdateNewCatchStatus(false);
            }

            isSelected = selected;
            UpdateSelectedVisual();
            OnSelectionChanged?.Invoke(this);
        }

        private void OnSelectButtonClick()
        {
            if (!hasBeenSelected)
            {
                hasBeenSelected = true;
                UpdateNewCatchStatus(false);
            }

            isSelected = !isSelected;
            UpdateSelectedVisual();
            Debug.Log($"[UI_FishBagPrefab] 点击选择: itemId={itemId}, isSelected={isSelected}");
            OnSelectionChanged?.Invoke(this);
        }

        private void UpdateSelectedVisual()
        {
            if (selectedImage != null)
            {
                selectedImage.gameObject.SetActive(isSelected);
            }
        }

        private void LoadIcon()
        {
            if (!string.IsNullOrEmpty(itemData.iconPath))
            {
                Sprite icon = Resources.Load<Sprite>(itemData.iconPath);
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                }
                else
                {
                    Debug.LogError($"[UI_FishBagPrefab] 图标加载失败 - 物品ID: {itemId}, 名称: {itemData.name}, 路径: {itemData.iconPath}");
                    iconImage.sprite = null;
                    iconImage.color = Color.gray;
                }
            }
            else
            {
                Debug.LogError($"[UI_FishBagPrefab] 图标路径为空 - 物品ID: {itemId}, 名称: {itemData.name}");
                iconImage.sprite = null;
                iconImage.color = Color.gray;
            }
        }

        private float GetItemWeight(int itemId)
        {
            if (LoadDataManager.Instance != null)
            {
                FishData fishData = LoadDataManager.Instance.GetFishById(itemId);
                if (fishData != null)
                {
                    return fishData.baseWeight;
                }
            }
            return 0f;
        }

        public int GetTotalSellPrice()
        {
            if (itemData != null && quantity > 0)
            {
                // 只计算单个物品的价格，而不是所有同ID物品的总价
                return itemData.sellPrice;
            }
            return 0;
        }
    }
}
