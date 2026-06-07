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
        private bool isNewCatch = false;
        private bool isSold = false;  // 是否已售出（等待移除）

        public int ItemId => itemId;
        public int Quantity => quantity;
        public bool IsSelected => isSelected;
        public ItemData ItemDataRef => itemData;
        public bool IsNewCatch => isNewCatch;
        public bool IsSold => isSold;

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

        public void Init(int id, int qty, ItemData data, bool isNewCatchFlag = false)
        {
            itemId = id;
            quantity = qty;
            itemData = data;
            isSelected = false;
            isNewCatch = isNewCatchFlag;
            isSold = false;

            UpdateDisplay();
            UpdateNewCatchStatus(isNewCatchFlag);
            UpdateSelectedVisual();
        }

        public void SetSelectionCallback(System.Action<UI_FishBagPrefab> callback)
        {
            OnSelectionChanged = callback;
        }

        public void MarkAsSold()
        {
            isSold = true;
            isSelected = false;
            gameObject.SetActive(false);
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
        }

        public void UpdateNewCatchStatus(bool isNew)
        {
            isNewCatch = isNew;
            if (newCatchImage != null)
            {
                newCatchImage.gameObject.SetActive(isNew);
            }
        }

        public void SetSelection(bool selected)
        {
            if (selected && isNewCatch)
            {
                UpdateNewCatchStatus(false);
            }

            isSelected = selected;
            UpdateSelectedVisual();
            OnSelectionChanged?.Invoke(this);
        }

        private void OnSelectButtonClick()
    {
        Debug.Log($"[UI_FishBagPrefab] OnSelectButtonClick - itemId={itemId}, isSelected={isSelected}");
        if (isNewCatch)
        {
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
                return itemData.sellPrice;
            }
            return 0;
        }
    }
}