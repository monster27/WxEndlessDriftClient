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
        public Image starRatingImage;      // ✅ 已有
        public Button selectButton;
        public Image selectedImage;
        public Image newCatchImage;
        public Image shinyIconImage;  // ✅ 闪光图标

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
        public bool IsShiny => fishDetail?.isShiny ?? false;

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

            // ========== 显示重量 ==========
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

            // ========== 显示价格 ==========
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

            // ========== 显示星级（使用图片） ==========
            UpdateStarRatingDisplay();

            // ========== 显示闪光图标 ==========
            UpdateShinyIconDisplay();

            if (iconImage != null && itemData != null)
            {
                LoadIcon();
            }
        }

        /// <summary>
        /// 更新闪光图标显示
        /// </summary>
        private void UpdateShinyIconDisplay()
        {
            bool isShiny = fishDetail?.isShiny ?? false;
            Debug.Log($"[UI_FishBagPrefab] UpdateShinyIconDisplay - itemId={itemId}, isShiny={isShiny}");

            if (shinyIconImage != null)
            {
                shinyIconImage.gameObject.SetActive(isShiny);
                if (isShiny)
                {
                    Debug.Log($"[UI_FishBagPrefab] 闪光鱼图标显示: {itemId}");
                }
            }
            else
            {
                Debug.LogWarning($"[UI_FishBagPrefab] shinyIconImage 为 null! itemId={itemId}");
            }
        }

        /// <summary>
        /// 更新星级显示 - 使用图片
        /// </summary>
        /// <summary>
        /// 更新星级显示 - 使用图片
        /// </summary>
        private void UpdateStarRatingDisplay()
        {
            int starRatingId = fishDetail != null ? fishDetail.starRatingId : 0;

            Debug.Log($"[UI_FishBagPrefab] UpdateStarRatingDisplay - itemId={itemId}, starRatingId={starRatingId}");

            // 星级图片
            if (starRatingImage != null)
            {
                // 打印当前状态
                Debug.Log($"[UI_FishBagPrefab] starRatingImage 状态 - 自身激活: {starRatingImage.gameObject.activeSelf}, " +
                          $"父节点激活: {(starRatingImage.transform.parent != null ? starRatingImage.transform.parent.gameObject.activeSelf : false)}, " +
                          $"颜色Alpha: {starRatingImage.color.a}, 当前Sprite: {(starRatingImage.sprite != null ? starRatingImage.sprite.name : "null")}");

                if (starRatingId > 0)
                {
                    Sprite starIcon = LoadStarRatingIcon(starRatingId);
                    if (starIcon != null)
                    {
                        starRatingImage.sprite = starIcon;
                        starRatingImage.gameObject.SetActive(true);

                        // 确保颜色Alpha为1（完全可见）
                        Color color = starRatingImage.color;
                        color.a = 1f;
                        starRatingImage.color = color;

                        // 确保Image组件启用
                        starRatingImage.enabled = true;

                        Debug.Log($"[UI_FishBagPrefab] 星级图标加载成功: ID={starRatingId}, 路径=UI/StarRating/star_{starRatingId}, " +
                                  $"图标尺寸: {starIcon.rect.width}x{starIcon.rect.height}, " +
                                  $"设置后激活状态: {starRatingImage.gameObject.activeSelf}, Image.enabled: {starRatingImage.enabled}");
                    }
                    else
                    {
                        Debug.LogWarning($"[UI_FishBagPrefab] 星级图标加载失败: ID={starRatingId}");
                        starRatingImage.gameObject.SetActive(false);
                    }
                }
                else
                {
                    Debug.Log($"[UI_FishBagPrefab] starRatingId <= 0，隐藏星级图标");
                    starRatingImage.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogError($"[UI_FishBagPrefab] starRatingImage 为 null! itemId={itemId}");
            }

            // 星级文字（保留作为备选）
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
                        Debug.Log($"[UI_FishBagPrefab] 星级文字显示: {starRating.name}");
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

        /// <summary>
        /// 根据星级ID加载对应的星级图标
        /// </summary>
        private Sprite LoadStarRatingIcon(int starRatingId)
        {
            // 根据星级ID生成对应的路径
            string path = $"UI/StarRating/star_{starRatingId}";
            Sprite icon = Resources.Load<Sprite>(path);

            if (icon != null)
            {
                Debug.Log($"[UI_FishBagPrefab] 星级图标加载成功: ID={starRatingId}, 路径={path}");
                return icon;
            }

            Debug.LogWarning($"[UI_FishBagPrefab] 星级图标加载失败: ID={starRatingId}, 路径={path}");

            // 备选：尝试其他路径
            string[] fallbackPaths = new string[]
            {
        $"StarRating/{starRatingId}",
        $"Images/StarRating/{starRatingId}",
        $"UI/Icon/Star/{starRatingId}"
            };

            foreach (string fallbackPath in fallbackPaths)
            {
                icon = Resources.Load<Sprite>(fallbackPath);
                if (icon != null)
                {
                    Debug.Log($"[UI_FishBagPrefab] 星级图标加载成功(备选路径): ID={starRatingId}, 路径={fallbackPath}");
                    return icon;
                }
            }

            // 如果所有路径都失败，创建纯色图标
            Debug.LogWarning($"[UI_FishBagPrefab] 所有路径加载失败，创建纯色备选图标: ID={starRatingId}");
            return CreateFallbackSprite(starRatingId);
        }

        private Sprite CreateFallbackSprite(int starRatingId)
        {
            // 从 LoadDataManager 获取星级颜色
            if (LoadDataManager.Instance != null)
            {
                var starRating = LoadDataManager.Instance.GetStarRatingById(starRatingId);
                if (starRating != null && !string.IsNullOrEmpty(starRating.color))
                {
                    Texture2D tex = new Texture2D(64, 64);
                    Color color = ParseColor(starRating.color);
                    for (int x = 0; x < tex.width; x++)
                    {
                        for (int y = 0; y < tex.height; y++)
                        {
                            tex.SetPixel(x, y, color);
                        }
                    }
                    tex.Apply();
                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            return null;
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