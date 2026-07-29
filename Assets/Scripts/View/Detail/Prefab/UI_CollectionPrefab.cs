using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SharedModels;

public enum CollectionInfoState
{
    Unknown = 0,      // 未获取情报
    InfoObtained = 1, // 已获取情报但未实际获取物品
    Obtained = 2      // 已实际获取物品
}

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
    private CollectionInfoState infoState = CollectionInfoState.Unknown;

    public int EntryId => entryId;
    public CollectionInfoState InfoState => infoState;
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

    public void Init(int id, bool fishFlag, CollectionInfoState state = CollectionInfoState.Unknown)
    {
        entryId = id;
        isFish = fishFlag;
        infoState = state;

        UpdateDisplayByState();
        
        if (!isFish && shinyToggleButton != null)
        {
            shinyToggleButton.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 根据情报状态更新显示
    /// </summary>
    private void UpdateDisplayByState()
    {
        switch (infoState)
        {
            case CollectionInfoState.Unknown:
                ShowUnknownState();
                break;
            case CollectionInfoState.InfoObtained:
                ShowInfoObtainedState();
                break;
            case CollectionInfoState.Obtained:
                ShowObtainedState();
                break;
        }
    }
    
    /// <summary>
    /// 显示未获取情报状态（显示unKnown图标）
    /// </summary>
    private void ShowUnknownState()
    {
        Sprite unknownSprite = Resources.Load<Sprite>("UI/Icon/Common/unKnown");
        if (unknownSprite != null)
        {
            icon.sprite = unknownSprite;
            icon.gameObject.SetActive(true);
        }
        
        nameText.text = "???";
        
        if (levelImage != null) levelImage.gameObject.SetActive(false);
        if (shinyImage != null) shinyImage.gameObject.SetActive(false);
        if (rarityBackgroundImage != null) rarityBackgroundImage.gameObject.SetActive(false);
        
        // 禁用点击事件
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = false;
        }
    }
    
    /// <summary>
    /// 显示已获取情报状态（显示Outline图标）
    /// </summary>
    private void ShowInfoObtainedState()
    {
        if (isFish)
        {
            // 显示鱼类Outline图标
            Sprite outlineSprite = Resources.Load<Sprite>($"UI/Icon/FishIcons/{entryId}_Outline");
            if (outlineSprite == null)
            {
                outlineSprite = Resources.Load<Sprite>($"UI/Icon/FishIcons/{entryId}");
            }
            if (outlineSprite != null)
            {
                icon.sprite = outlineSprite;
                icon.gameObject.SetActive(true);
            }
            
            var fishData = LoadDataManager.Instance?.GetFishById(entryId);
            if (fishData != null)
            {
                nameText.text = fishData.name;
            }
            
            UpdateRarityBackground();
        }
        else
        {
            itemData = LoadDataManager.Instance?.GetItemById(entryId);
            if (itemData != null)
            {
                nameText.text = itemData.name;
                Sprite outlineSprite = Resources.Load<Sprite>($"UI/Icon/ItemIcons/{entryId}_Outline");
                if (outlineSprite == null)
                {
                    outlineSprite = Resources.Load<Sprite>($"UI/Icon/ItemIcons/{entryId}");
                }
                if (outlineSprite != null)
                {
                    icon.sprite = outlineSprite;
                    icon.gameObject.SetActive(true);
                }
            }
            
            if (rarityBackgroundImage != null)
            {
                rarityBackgroundImage.gameObject.SetActive(false);
            }
        }
        
        if (levelImage != null) levelImage.gameObject.SetActive(false);
        if (shinyImage != null) shinyImage.gameObject.SetActive(false);
        
        // 启用点击事件
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
        }
    }
    
    /// <summary>
    /// 显示已获取物品状态（正常显示）
    /// </summary>
    private void ShowObtainedState()
    {
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
        
        // 启用点击事件
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
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
