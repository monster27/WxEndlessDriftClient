using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Text;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
//using SharedModels;

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
        public Image rarityBackgroundImage;
        public Button selectButton;
        public Image selectedImage;
        public Image newCatchImage;
        public Image shinyIconImage;
        public Image lockIcon;

        private int itemId;
        private int quantity;
        private ItemData itemData;
        private bool isSelected = false;
        private bool isNewCatch = false;
        private bool isSold = false;
        private FishDetailData fishDetail;

        // AA 句柄
        private AsyncOperationHandle<Sprite> _iconHandle;
        private AsyncOperationHandle<Sprite> _rarityHandle;
        private AsyncOperationHandle<Sprite> _starHandle;

        public int ItemId => itemId;
        public int Quantity => quantity;
        public bool IsSelected => isSelected;
        public ItemData ItemDataRef => itemData;
        public bool IsNewCatch => isNewCatch;
        public bool IsSold => isSold;
        public FishDetailData FishDetail => fishDetail;
        public bool IsShiny => fishDetail?.isShiny ?? false;
        public bool IsLocked => fishDetail?.isLocked ?? false;

        public long CatchTimestamp => fishDetail?.caughtTimestamp ?? 0;
        public float FishWeight => fishDetail?.weight ?? GetItemWeight(itemId);

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

        void OnDestroy()
        {
            AssetManager.ReleaseAddressable(_iconHandle);
            AssetManager.ReleaseAddressable(_rarityHandle);
            AssetManager.ReleaseAddressable(_starHandle);
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
                    priceText.text = $"¥{displayPrice}";
                    priceText.gameObject.SetActive(true);
                }
                else
                {
                    priceText.text = "";
                    priceText.gameObject.SetActive(false);
                }
            }

            UpdateRarityBackground();
            UpdateStarRatingDisplay();
            UpdateShinyIconDisplay();
            UpdateLockIconDisplay();

            LoadIcon();
        }

        private void UpdateRarityBackground()
        {
            if (rarityBackgroundImage == null) return;

            int rarityId = FishRarityId;
            if (rarityId <= 0) rarityId = 0;

            string path = $"UI/Icon/RarityBackground/{rarityId}";
            AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
            {
                _rarityHandle = handle;
                if (sprite != null)
                {
                    rarityBackgroundImage.sprite = sprite;
                    rarityBackgroundImage.gameObject.SetActive(true);
                    rarityBackgroundImage.color = Color.white;
                }
                else
                {
                    if (rarityId != 0)
                    {
                        AssetManager.LoadFromAddressables<Sprite>("UI/Icon/RarityBackground/0", (defaultSprite, defaultHandle) =>
                        {
                            _rarityHandle = defaultHandle;
                            if (defaultSprite != null)
                            {
                                rarityBackgroundImage.sprite = defaultSprite;
                                rarityBackgroundImage.gameObject.SetActive(true);
                                rarityBackgroundImage.color = Color.white;
                                return;
                            }
                            rarityBackgroundImage.gameObject.SetActive(false);
                        });
                    }
                    else
                    {
                        rarityBackgroundImage.gameObject.SetActive(false);
                    }
                }
            });
        }

        private void UpdateShinyIconDisplay()
        {
            bool isShiny = fishDetail?.isShiny ?? false;
            if (shinyIconImage != null)
            {
                shinyIconImage.gameObject.SetActive(isShiny);
            }
        }

        private void UpdateLockIconDisplay()
        {
            bool isLocked = fishDetail?.isLocked ?? false;
            if (lockIcon != null)
            {
                lockIcon.gameObject.SetActive(isLocked);
            }
        }

        public void SetLocked(bool locked)
        {
            if (fishDetail != null)
            {
                fishDetail.isLocked = locked;
                UpdateLockIconDisplay();
            }
        }

        private void UpdateStarRatingDisplay()
        {
            int starRatingId = fishDetail != null ? fishDetail.starRatingId : 0;

            if (starRatingImage != null)
            {
                if (starRatingId > 0)
                {
                    string path = $"UI/Icon/StarRating/star_{starRatingId}";
                    AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
                    {
                        _starHandle = handle;
                        if (sprite != null)
                        {
                            starRatingImage.sprite = sprite;
                            starRatingImage.gameObject.SetActive(true);
                            starRatingImage.color = Color.white;
                            starRatingImage.enabled = true;
                        }
                        else
                        {
                            starRatingImage.gameObject.SetActive(false);
                        }
                    });
                }
                else
                {
                    starRatingImage.gameObject.SetActive(false);
                }
            }

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
            if (string.IsNullOrEmpty(itemData?.iconPath))
            {
                Debug.LogError($"[UI_FishBagPrefab] 图标路径为空 - 物品ID: {itemId}");
                iconImage.sprite = null;
                iconImage.color = Color.gray;
                return;
            }

            bool isShiny = fishDetail?.isShiny ?? false;
            string basePath = itemData.iconPath;

            if (isShiny)
            {
                string shinyPath = basePath + "_s";
                AssetManager.LoadFromAddressables<Sprite>(shinyPath, (sprite, handle) =>
                {
                    _iconHandle = handle;
                    if (sprite != null)
                    {
                        iconImage.sprite = sprite;
                        iconImage.color = Color.white;
                    }
                    else
                    {
                        // 回退到普通图标
                        LoadFallbackIcon(basePath);
                    }
                });
            }
            else
            {
                LoadFallbackIcon(basePath);
            }
        }

        private void LoadFallbackIcon(string path)
        {
            AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
            {
                _iconHandle = handle;
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                }
                else
                {
                    Debug.LogError($"[UI_FishBagPrefab] 图标加载失败: {path}, 物品ID: {itemId}");
                    iconImage.sprite = null;
                    iconImage.color = Color.gray;
                }
            });
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
                return itemData.sellPrice;
            }

            return 0;
        }

        public int GetTotalSellPrice()
        {
            return CalculateDisplayPrice();
        }
    }
}
