using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SharedModels;

public class UI_CollectionPrefab : MonoBehaviour
{
    public Image icon;
    public Image levelImage;
    public Button shinyToggleButton;
    public Image shinyImage;
    public Text nameText;
    public Image rarityBackgroundImage;

    private int entryId;
    private bool isFish;
    private int collectionLevel = 0;
    private bool isShiny = false;
    private bool hasShiny = false;
    private ItemData itemData;

    public int EntryId => entryId;
    public event System.Action<UI_CollectionPrefab> OnClick;

    void Start()
    {
        if (shinyToggleButton != null)
        {
            shinyToggleButton.onClick.AddListener(OnShinyToggleClick);
        }
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnClick?.Invoke(this));
        }
    }

    public void Init(int id, bool fishFlag)
    {
        entryId = id;
        isFish = fishFlag;

        if (isFish)
        {
            LoadFishData();
        }
        else
        {
            LoadNonFishData();
        }

        UpdateLevelDisplay();
        UpdateShinyDisplay();
        
        if (!isFish && shinyToggleButton != null)
        {
            shinyToggleButton.gameObject.SetActive(false);
        }
    }

    private void LoadFishData()
    {
        itemData = LoadDataManager.Instance?.GetItemById(entryId);
        
        Sprite fishSprite = Resources.Load<Sprite>($"UI/Icon/FishIcons/{entryId}");
        if (fishSprite != null)
        {
            icon.sprite = fishSprite;
            icon.gameObject.SetActive(true);
        }

        var fishData = LoadDataManager.Instance?.GetFishById(entryId);
        if (fishData != null)
        {
            nameText.text = fishData.name;
        }

        UpdateRarityBackground();
    }

    private void LoadNonFishData()
    {
        itemData = LoadDataManager.Instance?.GetItemById(entryId);
        if (itemData != null)
        {
            nameText.text = itemData.name;
            Sprite itemSprite = Resources.Load<Sprite>($"UI/Icon/ItemIcons/{entryId}");
            if (itemSprite != null)
            {
                icon.sprite = itemSprite;
                icon.gameObject.SetActive(true);
            }
        }

        if (rarityBackgroundImage != null)
        {
            rarityBackgroundImage.gameObject.SetActive(false);
        }
    }

    private void UpdateLevelDisplay()
    {
        if (levelImage != null)
        {
            if (collectionLevel > 0)
            {
                Sprite levelSprite = Resources.Load<Sprite>($"UI/Icon/Collection/{collectionLevel}");
                if (levelSprite != null)
                {
                    levelImage.sprite = levelSprite;
                }
                levelImage.gameObject.SetActive(true);
            }
            else
            {
                levelImage.gameObject.SetActive(false);
            }
        }
    }

    private void UpdateShinyDisplay()
    {
        if (shinyImage != null)
        {
            shinyImage.gameObject.SetActive(isShiny);
        }
    }

    private void OnShinyToggleClick()
    {
        isShiny = !isShiny;
        UpdateShinyDisplay();
        LoadIcon(isShiny);
    }

    private void LoadIcon(bool isShiny)
    {
        if (string.IsNullOrEmpty(itemData?.iconPath))
        {
            return;
        }

        string basePath = itemData.iconPath;
        Sprite loadedSprite = null;

        if (isShiny)
        {
            string shinyPath = basePath + "_s";
            loadedSprite = Resources.Load<Sprite>(shinyPath);
        }

        if (loadedSprite == null)
        {
            loadedSprite = Resources.Load<Sprite>(basePath);
        }

        if (loadedSprite != null)
        {
            icon.sprite = loadedSprite;
            icon.color = Color.white;
        }
    }

    private void UpdateRarityBackground()
    {
        if (rarityBackgroundImage == null)
        {
            return;
        }

        int rarityId = 0;
        var fishData = LoadDataManager.Instance?.GetFishById(entryId);
        if (fishData != null)
        {
            rarityId = fishData.rarityId;
        }

        if (rarityId <= 0)
        {
            rarityId = 0;
        }

        Sprite raritySprite = Resources.Load<Sprite>($"UI/Icon/RarityBackground/{rarityId}");
        if (raritySprite != null)
        {
            rarityBackgroundImage.sprite = raritySprite;
            rarityBackgroundImage.gameObject.SetActive(true);
            rarityBackgroundImage.color = Color.white;
        }
        else
        {
            if (rarityId != 0)
            {
                Sprite defaultSprite = Resources.Load<Sprite>("UI/Icon/RarityBackground/0");
                if (defaultSprite != null)
                {
                    rarityBackgroundImage.sprite = defaultSprite;
                    rarityBackgroundImage.gameObject.SetActive(true);
                    rarityBackgroundImage.color = Color.white;
                    return;
                }
            }
            rarityBackgroundImage.gameObject.SetActive(false);
        }
    }

    public void SetCollectionLevel(int level)
    {
        collectionLevel = Mathf.Clamp(level, 0, 3);
        UpdateLevelDisplay();
    }

    public void SetHasShiny(bool value)
    {
        hasShiny = value;
        if (shinyToggleButton != null)
        {
            shinyToggleButton.gameObject.SetActive(hasShiny);
        }
    }
}
