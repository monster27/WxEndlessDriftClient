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
        public Image starRatingImage;          // 星级图标
        public Image rarityBackgroundImage;    // ✅ 新增：稀有度背景颜色图片
        public Button selectButton;
        public Image selectedImage;
        public Image newCatchImage;
        public Image shinyIconImage;           // 闪光图标
        public Image lockIcon;                 // 锁定图标

        private int itemId;
        private int quantity;
        private ItemData itemData;
        private bool isSelected = false;
        private bool isNewCatch = false;
        private bool isSold = false;
        private FishDetailData fishDetail;

        // 闪光脉冲协程引用
        private Coroutine shinyPulseCoroutine = null;

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

        void OnEnable()
        {
            // 当物体被激活时，如果是闪光鱼且图标显示，重新启动脉冲
            if (IsShiny && shinyIconImage != null && shinyIconImage.gameObject.activeSelf)
            {
                StartShinyPulse();
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

            // ========== ✅ 新增：更新稀有度背景颜色 ==========
            UpdateRarityBackground();

            // ========== 显示星级（使用图片） ==========
            UpdateStarRatingDisplay();

            // ========== 显示闪光图标 ==========
            UpdateShinyIconDisplay();

            // ========== 显示锁定图标 ==========
            UpdateLockIconDisplay();

            if (iconImage != null && itemData != null)
            {
                LoadIcon();
            }
        }

        /// <summary>
        /// 更新稀有度背景图片（根据稀有度ID加载对应的图片）
        /// </summary>
        private void UpdateRarityBackground()
        {
            if (rarityBackgroundImage == null)
            {
                return;
            }

            // 获取稀有度ID
            int rarityId = FishRarityId;

            // 如果没有稀有度ID或无效，尝试使用默认ID=0
            if (rarityId <= 0)
            {
                rarityId = 0;
            }

            // 尝试加载对应稀有度ID的图片
            Sprite raritySprite = LoadRarityBackgroundSprite(rarityId);

            if (raritySprite != null)
            {
                // 找到对应的图片，设置并显示
                rarityBackgroundImage.sprite = raritySprite;
                rarityBackgroundImage.gameObject.SetActive(true);
                rarityBackgroundImage.color = Color.white;
                Debug.Log($"[UI_FishBagPrefab] 稀有度背景图片加载成功: itemId={itemId}, rarityId={rarityId}");
            }
            else
            {
                // 没找到对应ID的图片，尝试加载默认ID=0
                if (rarityId != 0)
                {
                    Sprite defaultSprite = LoadRarityBackgroundSprite(0);
                    if (defaultSprite != null)
                    {
                        rarityBackgroundImage.sprite = defaultSprite;
                        rarityBackgroundImage.gameObject.SetActive(true);
                        rarityBackgroundImage.color = Color.white;
                        Debug.Log($"[UI_FishBagPrefab] 使用默认稀有度背景图片: itemId={itemId}, rarityId=0");
                        return;
                    }
                }

                // 如果连默认图片都没有，隐藏背景
                rarityBackgroundImage.gameObject.SetActive(false);
                Debug.Log($"[UI_FishBagPrefab] 稀有度背景图片不存在，隐藏背景: itemId={itemId}, rarityId={rarityId}");
            }
        }

        /// <summary>
        /// 加载稀有度背景图片
        /// </summary>
        private Sprite LoadRarityBackgroundSprite(int rarityId)
        {
            string path = $"UI/Icon/RarityBackground/{rarityId}";
            Sprite icon = Resources.Load<Sprite>(path);

            if (icon != null)
            {
                Debug.Log($"[UI_FishBagPrefab] 稀有度背景图片加载成功: ID={rarityId}, 路径={path}");
                return icon;
            }

            // 尝试备选路径
            string[] fallbackPaths = new string[]
            {
                $"RarityBackground/{rarityId}",
                $"Images/RarityBackground/{rarityId}",
                $"UI/Rarity/{rarityId}"
            };

            foreach (string fallbackPath in fallbackPaths)
            {
                icon = Resources.Load<Sprite>(fallbackPath);
                if (icon != null)
                {
                    Debug.Log($"[UI_FishBagPrefab] 稀有度背景图片加载成功(备选路径): ID={rarityId}, 路径={fallbackPath}");
                    return icon;
                }
            }

            return null;
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
                    StartShinyPulse();
                }
                else
                {
                    StopShinyPulse();
                }
            }
            else
            {
                Debug.LogWarning($"[UI_FishBagPrefab] shinyIconImage 为 null! itemId={itemId}");
            }
        }

        /// <summary>
        /// 更新锁定图标显示
        /// </summary>
        private void UpdateLockIconDisplay()
        {
            bool isLocked = fishDetail?.isLocked ?? false;

            if (lockIcon != null)
            {
                lockIcon.gameObject.SetActive(isLocked);
            }
        }

        /// <summary>
        /// 设置锁定状态
        /// </summary>
        public void SetLocked(bool locked)
        {
            if (fishDetail != null)
            {
                fishDetail.isLocked = locked;
                UpdateLockIconDisplay();
            }
        }

        /// <summary>
        /// 启动闪光图标脉冲闪烁效果
        /// </summary>
        private void StartShinyPulse()
        {
            if (shinyIconImage == null) return;

            // ✅ 检查 GameObject 是否激活，如果未激活则启动协程会失败，等待 OnEnable 时启动
            if (!gameObject.activeInHierarchy)
            {
                Debug.Log($"[UI_FishBagPrefab] GameObject 未激活，延迟启动脉冲: {gameObject.name}");
                return;
            }

            // 如果已有协程在运行，先停止
            if (shinyPulseCoroutine != null)
            {
                StopCoroutine(shinyPulseCoroutine);
                shinyPulseCoroutine = null;
            }

            // 重置为完全不透明，然后启动脉冲
            Color c = shinyIconImage.color;
            c.a = 1f;
            shinyIconImage.color = c;
            shinyPulseCoroutine = StartCoroutine(ShinyPulseCoroutine());
        }

        /// <summary>
        /// 停止闪光图标脉冲闪烁效果
        /// </summary>
        private void StopShinyPulse()
        {
            if (shinyPulseCoroutine != null)
            {
                StopCoroutine(shinyPulseCoroutine);
                shinyPulseCoroutine = null;
            }

            if (shinyIconImage != null)
            {
                // 重置为完全不透明
                Color c = shinyIconImage.color;
                c.a = 1f;
                shinyIconImage.color = c;
            }
        }

        /// <summary>
        /// 闪光图标脉冲闪烁协程（呼吸效果）
        /// </summary>
        private IEnumerator ShinyPulseCoroutine()
        {
            if (shinyIconImage == null) yield break;

            float speed = 2.5f;
            float minAlpha = 0.2f;
            float maxAlpha = 1f;

            while (true)
            {
                float t = Mathf.PingPong(Time.time * speed, 1f);
                float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

                Color c = shinyIconImage.color;
                c.a = alpha;
                shinyIconImage.color = c;

                yield return null;
            }
        }

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
                if (starRatingId > 0)
                {
                    Sprite starIcon = LoadStarRatingIcon(starRatingId);
                    if (starIcon != null)
                    {
                        starRatingImage.sprite = starIcon;
                        starRatingImage.gameObject.SetActive(true);

                        Color color = starRatingImage.color;
                        color.a = 1f;
                        starRatingImage.color = color;
                        starRatingImage.enabled = true;

                        Debug.Log($"[UI_FishBagPrefab] 星级图标加载成功: ID={starRatingId}");
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
            string path = $"UI/Icon/StarRating/star_{starRatingId}";
            Sprite icon = Resources.Load<Sprite>(path);

            if (icon != null)
            {
                Debug.Log($"[UI_FishBagPrefab] 星级图标加载成功: ID={starRatingId}, 路径={path}");
                return icon;
            }

            Debug.LogWarning($"[UI_FishBagPrefab] 星级图标加载失败: ID={starRatingId}, 路径={path}");

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

            Debug.LogWarning($"[UI_FishBagPrefab] 所有路径加载失败，创建纯色备选图标: ID={starRatingId}");
            return CreateFallbackSprite(starRatingId);
        }

        private Sprite CreateFallbackSprite(int starRatingId)
        {
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
            if (string.IsNullOrEmpty(itemData?.iconPath))
            {
                Debug.LogError($"[UI_FishBagPrefab] 图标路径为空 - 物品ID: {itemId}, 名称: {itemData?.name ?? "未知"}");
                iconImage.sprite = null;
                iconImage.color = Color.gray;
                return;
            }

            bool isShiny = fishDetail?.isShiny ?? false;
            string basePath = itemData.iconPath;
            Sprite loadedSprite = null;

            if (isShiny)
            {
                string shinyPath = basePath + "_s";

                // ✅ 先尝试加载 Texture2D 来诊断文件是否存在
                Texture2D tex = Resources.Load<Texture2D>(shinyPath);
                if (tex != null)
                {
                    Debug.Log($"[UI_FishBagPrefab] 闪光鱼纹理存在: {shinyPath}, 尺寸: {tex.width}x{tex.height}");
                }
                else
                {
                    Debug.Log($"[UI_FishBagPrefab] 闪光鱼纹理不存在: {shinyPath}");
                }

                loadedSprite = Resources.Load<Sprite>(shinyPath);

                if (loadedSprite != null)
                {
                    Debug.Log($"[UI_FishBagPrefab] 闪光鱼图标加载成功: {shinyPath}");
                }
                else
                {
                    // ✅ 改为 Warning（因为会回退到普通图标，不影响功能）
                    Debug.LogWarning($"[UI_FishBagPrefab] 闪光鱼图标不存在，回退到普通图标: {shinyPath}");
                }
            }

            // 如果闪光图标加载失败或不是闪光鱼，加载普通图标
            if (loadedSprite == null)
            {
                loadedSprite = Resources.Load<Sprite>(basePath);
                if (loadedSprite != null)
                {
                    Debug.Log($"[UI_FishBagPrefab] 普通图标加载成功: {basePath}");
                }
            }

            // 最终结果
            if (loadedSprite != null)
            {
                iconImage.sprite = loadedSprite;
                iconImage.color = Color.white;
            }
            else
            {
                Debug.LogError($"[UI_FishBagPrefab] 所有图标加载失败! 基础路径: {basePath}, 闪光鱼: {isShiny}, 物品ID: {itemId}");
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

        private void OnDestroy()
        {
            if (shinyPulseCoroutine != null)
            {
                StopCoroutine(shinyPulseCoroutine);
                shinyPulseCoroutine = null;
            }
        }
    }
}
