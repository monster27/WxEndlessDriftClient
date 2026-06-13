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

        if (stockText != null && mallItemData != null)
        {
            stockText.text = mallItemData.stock.ToString();
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