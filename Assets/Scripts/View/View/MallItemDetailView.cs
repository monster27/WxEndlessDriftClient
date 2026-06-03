using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MallItemDetailView : MonoBehaviour
{
    public Image itemIconImage;
    public Text itemNameText;
    public Text priceText;
    public Text quantityText;

    public Button addBtn;
    public Button subtractBtn;
    public Slider quantitySlider;

    public Button confirmBtn;
    public Button cancelBtn;
    public Button maskBtn;

    private int itemId;
    private ItemData itemData;
    private MallItemData mallItemData;
    private int quantity = 0;
    private int maxQuantity = 0;

    void Awake()
    {
        // 初始化时默认隐藏二级界面
        gameObject.SetActive(false);
    }

    void Start()
    {
        if (addBtn != null)
        {
            addBtn.onClick.AddListener(OnAddClick);
        }

        if (subtractBtn != null)
        {
            subtractBtn.onClick.AddListener(OnSubtractClick);
        }

        if (quantitySlider != null)
        {
            quantitySlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        if (confirmBtn != null)
        {
            confirmBtn.onClick.AddListener(OnConfirmClick);
        }

        if (cancelBtn != null)
        {
            cancelBtn.onClick.AddListener(OnCancelClick);
        }

        if (maskBtn != null)
        {
            maskBtn.onClick.AddListener(OnMaskClick);
        }
    }

    public void ShowItem(int id, ItemData data, MallItemData mallData)
    {
        itemId = id;
        itemData = data;
        mallItemData = mallData;

        CalculateMaxQuantity();
        quantity = Mathf.Min(1, maxQuantity);

        UpdateDisplay();
        UpdateQuantityDisplay();

        gameObject.SetActive(true);
    }

    private void CalculateMaxQuantity()
    {
        if (mallItemData == null)
        {
            maxQuantity = 0;
            return;
        }

        int playerGold = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_GOLD, 0);
        int canAffordCount = playerGold / mallItemData.price;
        int stockLimitedCount = mallItemData.stock;

        maxQuantity = Mathf.Min(canAffordCount, stockLimitedCount);
        maxQuantity = Mathf.Max(0, maxQuantity);

        Debug.Log($"[MallItemDetailView] 最大可购买数量: 金币={playerGold}, 单价={mallItemData.price}, 可负担={canAffordCount}, 库存={stockLimitedCount}, 最终={maxQuantity}");
    }

    private void UpdateDisplay()
    {
        if (itemIconImage != null && itemData != null && !string.IsNullOrEmpty(itemData.iconPath))
        {
            Sprite icon = Resources.Load<Sprite>(itemData.iconPath);
            if (icon != null)
            {
                itemIconImage.sprite = icon;
            }
        }

        if (itemNameText != null && itemData != null)
        {
            itemNameText.text = itemData.name;
        }

        if (priceText != null && mallItemData != null)
        {
            priceText.text = $"售价: {mallItemData.price}";
        }

        if (quantitySlider != null)
        {
            quantitySlider.minValue = 0;
            quantitySlider.maxValue = maxQuantity;
            quantitySlider.wholeNumbers = true;
            quantitySlider.value = quantity;
            
            Debug.Log($"[MallItemDetailView] Slider设置: min=0, max={maxQuantity}, value={quantity}");
        }
    }

    private void UpdateQuantityDisplay()
    {
        if (quantityText != null)
        {
            quantityText.text = quantity.ToString();
        }
    }

    private void OnAddClick()
    {
        if (quantity < maxQuantity)
        {
            quantity++;
            UpdateQuantityDisplay();
            if (quantitySlider != null)
            {
                quantitySlider.value = quantity;
            }
        }
    }

    private void OnSubtractClick()
    {
        if (quantity > 0)
        {
            quantity--;
            UpdateQuantityDisplay();
            if (quantitySlider != null)
            {
                quantitySlider.value = quantity;
            }
        }
    }

    private void OnSliderValueChanged(float value)
    {
        int newQuantity = Mathf.RoundToInt(value);
        newQuantity = Mathf.Clamp(newQuantity, 0, maxQuantity);

        if (newQuantity != quantity)
        {
            quantity = newQuantity;
            UpdateQuantityDisplay();
        }
    }

    private void OnConfirmClick()
    {
        if (quantity <= 0)
        {
            Debug.LogWarning("[MallItemDetailView] 购买数量必须大于0");
            return;
        }

        CommunicateEvent.Modify<(int, int)>(CommunicateEvent.EVENT_PURCHASE_MALL_ITEM, (itemId, quantity));
        Debug.Log($"[MallItemDetailView] 已发送购买请求: itemId={itemId}, quantity={quantity}");
        CommunicateEvent.Modify("Mall_PurchaseSuccess", itemId);
        CloseDetailView();
    }

    private void OnCancelClick()
    {
        CloseDetailView();
    }

    private void OnMaskClick()
    {
        CloseDetailView();
    }

    private void CloseDetailView()
    {
        // 重置数量和Slider
        quantity = 0;
        if (quantitySlider != null)
        {
            quantitySlider.value = 0;
        }
        if (quantityText != null)
        {
            quantityText.text = "0";
        }

        gameObject.SetActive(false);
    }
}