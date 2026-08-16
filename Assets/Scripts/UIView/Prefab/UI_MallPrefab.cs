using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
//using SharedModels;

public class UI_MallPrefab : MonoBehaviour
{
    public Image iconImage;
    public Text nameText;
    public Text priceText;
    public Text stockText;
    public Button itemButton;
    public GameObject ownedObj;
    public GameObject soldOutObj;
    public GameObject offSaleObj;

    private int itemId;
    private ItemData itemData;
    private MallItemData mallItemData;
    private AsyncOperationHandle<Sprite> _iconHandle;

    void Start()
    {
        if (itemButton != null)
        {
            itemButton.onClick.AddListener(OnItemClick);
        }
    }

    void OnDestroy()
    {
        AssetManager.ReleaseAddressable(_iconHandle);
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
        if (itemData == null) return;

        if (iconImage != null && !string.IsNullOrEmpty(itemData.iconPath))
        {
            AssetManager.LoadFromAddressables<Sprite>(itemData.iconPath, (sprite, handle) =>
            {
                _iconHandle = handle;
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                }
            });
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

        if (ownedObj != null)
        {
            bool alreadyOwned = false;
            if (itemData.isUnique && PlayerDataManager.Instance != null)
            {
                alreadyOwned = PlayerDataManager.Instance.GetItemQuantity(itemId) > 0;
            }
            ownedObj.SetActive(alreadyOwned);
        }

        if (soldOutObj != null)
        {
            bool isSoldOut = mallItemData != null && mallItemData.stock <= 0 && !itemData.isUnique;
            soldOutObj.SetActive(isSoldOut);
        }

        if (offSaleObj != null)
        {
            bool isOffSale = mallItemData != null && !mallItemData.isOnSale;
            offSaleObj.SetActive(isOffSale);
        }

        if (itemButton != null)
        {
            bool interactable = mallItemData != null &&
                                mallItemData.isOnSale &&
                                mallItemData.stock > 0;

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
