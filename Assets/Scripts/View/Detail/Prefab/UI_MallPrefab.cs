using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SharedModels;

public class UI_MallPrefab : MonoBehaviour
{
    public Image iconImage;
    public Text nameText;
    public Text priceText;
    public Text stockText;
    public Button itemButton;
    public GameObject ownedObj;
    public GameObject soldOutObj;  // ✅ 新增：售罄标识
    public GameObject offSaleObj;  // ✅ 新增：已下架标识

    private int itemId;
    private ItemData itemData;
    private MallItemData mallItemData;

    void Start()
    {
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(OnItemClick);
        }
    }

    private void OnItemClick()
    {
        Debug.Log($"[UI_MallPrefab] OnItemClick - itemId={itemId}");
        CommunicateEvent.Modify("Mall_ItemClicked", itemId);
    }

    public void Init(int id, ItemData data, MallItemData mallData)
    {
        itemId = id;
        itemData = data;
        mallItemData = mallData;
        UpdateDisplay();
    }

    public void UpdateDisplay(ItemData data, MallItemData mallData)
    {
        itemData = data;
        mallItemData = mallData;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (itemData == null)
            return;

        if (iconImage != null && !string.IsNullOrEmpty(itemData.iconPath))
        {
            Sprite icon = Resources.Load<Sprite>(itemData.iconPath);
            if (icon != null)
            {
                iconImage.sprite = icon;
            }
        }

        if (nameText != null)
        {
            nameText.text = itemData.name;
        }

        if (priceText != null && mallItemData != null)
        {
            priceText.text = mallItemData.price.ToString();
        }

        // ✅ 库存显示
        if (stockText != null && mallItemData != null)
        {
            if (itemData.isUnique)
            {
                stockText.text = "";
                stockText.gameObject.SetActive(false);
            }
            else
            {
                stockText.text = mallItemData.stock.ToString();
                stockText.gameObject.SetActive(true);
            }
        }

        // ✅ 已拥有标识
        if (ownedObj != null)
        {
            bool alreadyOwned = false;
            if (itemData.isUnique && PlayerDataManager.Instance != null)
            {
                alreadyOwned = PlayerDataManager.Instance.GetItemQuantity(itemId) > 0;
            }
            ownedObj.SetActive(alreadyOwned);
        }

        // ✅ 售罄标识
        if (soldOutObj != null)
        {
            bool isSoldOut = mallItemData != null && mallItemData.stock <= 0 && !itemData.isUnique;
            soldOutObj.SetActive(isSoldOut);
        }

        // ✅ 下架标识
        if (offSaleObj != null)
        {
            bool isOffSale = mallItemData != null && !mallItemData.isOnSale;
            offSaleObj.SetActive(isOffSale);
        }

        // ✅ 按钮交互状态
        if (itemButton != null)
        {
            bool interactable = mallItemData != null &&
                                mallItemData.isOnSale &&
                                mallItemData.stock > 0;

            // 唯一物品已拥有时不可购买
            if (itemData.isUnique && PlayerDataManager.Instance != null)
            {
                if (PlayerDataManager.Instance.GetItemQuantity(itemId) > 0)
                {
                    interactable = false;
                }
            }

            itemButton.interactable = interactable;
        }
    }

    public int GetItemId()
    {
        return itemId;
    }

    public MallItemData GetMallItemData()
    {
        return mallItemData;
    }
}
