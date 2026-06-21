using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using SharedModels;

namespace View.Detail
{
    public class UI_FishBagPrefab : MonoBehaviour
    {
        public Image iconImage;
        public Text quantityText;
        public Text nameText;
        public Text weightText;
        public Text priceText;
        public Text starRatingText;
        public Image starRatingImage;
        public Button selectButton;
        public Image selectedImage;
        public Image newCatchImage;

        private int itemId;
        private int quantity;
        private ItemData itemData;
        private bool isSelected = false;
        private bool isNewCatch = false;
        private bool isSold = false;
        private FishDetailData fishDetail;

        public int ItemId => itemId;
        public int Quantity => quantity;
        public bool IsSelected => isSelected;
        public ItemData ItemDataRef => itemData;
        public bool IsNewCatch => isNewCatch;
        public bool IsSold => isSold;
        public FishDetailData FishDetail => fishDetail;

        /// <summary>
        /// 获取钓获时间戳（用于排序）
        /// </summary>
        public long CatchTimestamp => fishDetail?.caughtTimestamp ?? 0;

        /// <summary>
        /// 获取鱼的重量（用于排序）
        /// </summary>
        public float FishWeight => fishDetail?.weight ?? GetItemWeight(itemId);

        /// <summary>
        /// 获取鱼的稀有度（用于排序）
        /// </summary>
        public int FishRarityId
        {
            get
            {
                if (itemData != null)
                {
                    var fishData = LoadDataManager.Instance?.GetFishById(itemId);
                    if (fishData != null)
                    {
                        return fishData.rarityId;
                    }
                }
                return 0;
            }
        }

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

        public void Init(int id, int qty, ItemData data, bool isNewCatchFlag = false, FishDetailData detail = null)
        {
            itemId = id;
            quantity = qty;
            itemData = data;
            fishDetail = detail;
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

            float displayWeight = fishDetail != null ? fishDetail.weight : GetItemWeight(itemId);
            if (weightText != null)
            {
                if (displayWeight > 0)
                {
                    weightText.text = $"{displayWeight:F2}kg";
                    weightText.gameObject.SetActive(true);
                }
                else
                {
                    weightText.text = "";
                    weightText.gameObject.SetActive(false);
                }
            }

            int displayPrice = CalculateDisplayPrice();
            if (priceText != null)
            {
                if (displayPrice > 0)
                {
                    // 价格显示为小数点后两位
                    priceText.text = $"¥{displayPrice:F2}";
                    priceText.gameObject.SetActive(true);
                }
                else
                {
                    priceText.text = "";
                    priceText.gameObject.SetActive(false);
                }
            }

            UpdateStarRatingDisplay();

            if (iconImage != null && itemData != null)
            {
                LoadIcon();
            }
        }

        private void UpdateStarRatingDisplay()
        {
            int starRatingId = fishDetail != null ? fishDetail.starRatingId : 0;
            
            if (starRatingText != null)
            {
                if (starRatingId > 0 && LoadDataManager.Instance != null)
                {
                    var starRating = LoadDataManager.Instance.GetStarRatingById(starRatingId);
                    if (starRating != null)
                    {
                        starRatingText.text = starRating.name;
                        starRatingText.color = ParseColor(starRating.color);
                        starRatingText.gameObject.SetActive(true);
                    }
                    else
                    {
                        starRatingText.gameObject.SetActive(false);
                    }
                }
                else
                {
                    starRatingText.gameObject.SetActive(false);
                }
            }

            if (starRatingImage != null)
            {
                if (starRatingId > 0 && LoadDataManager.Instance != null)
                {
                    var starRating = LoadDataManager.Instance.GetStarRatingById(starRatingId);
                    if (starRating != null)
                    {
                        starRatingImage.color = ParseColor(starRating.color);
                        starRatingImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        starRatingImage.gameObject.SetActive(false);
                    }
                }
                else
                {
                    starRatingImage.gameObject.SetActive(false);
                }
            }
        }

        private Color ParseColor(string colorCode)
        {
            if (ColorUtility.TryParseHtmlString(colorCode, out Color color))
            {
                return color;
            }
            return Color.white;
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

        private int CalculateDisplayPrice()
        {
            if (fishDetail != null && fishDetail.calculatedPrice > 0)
            {
                return fishDetail.calculatedPrice;
            }

            if (itemData != null)
            {
                int basePrice = itemData.sellPrice;
                
                if (fishDetail != null && fishDetail.starRatingId > 0 && LoadDataManager.Instance != null)
                {
                    var starRating = LoadDataManager.Instance.GetStarRatingById(fishDetail.starRatingId);
                    if (starRating != null)
                    {
                        return Mathf.RoundToInt(basePrice * starRating.multiplier);
                    }
                }
                
                return basePrice;
            }
            
            return 0;
        }

        public int GetTotalSellPrice()
        {
            return CalculateDisplayPrice();
        }
    }
}