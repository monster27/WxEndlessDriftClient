using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum CollectionInfoState
{
    Unknown = 0,      // 未获取情报
    InfoObtained = 1, // 已获取情报但未实际获取物品
    Obtained = 2      // 已实际获取物品
}

public class UI_CollectionPrefab : MonoBehaviour
{
    public Image icon;
    public Image levelHightLightMask;
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
    private string pageName = "";

    // ===== AA 加载句柄（用于释放资源） =====
    private AsyncOperationHandle<Sprite> _iconHandle;
    private AsyncOperationHandle<Sprite> _outlineHandle;
    private AsyncOperationHandle<Sprite> _levelHandle;
    private AsyncOperationHandle<Sprite> _rarityHandle;
    private AsyncOperationHandle<Sprite> _unknownHandle;

    public int EntryId => entryId;
    public CollectionInfoState InfoState => infoState;
    public string PageName => pageName;
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

    void OnDestroy()
    {
        // 释放所有 AA 资源
        ReleaseHandle(ref _iconHandle);
        ReleaseHandle(ref _outlineHandle);
        ReleaseHandle(ref _levelHandle);
        ReleaseHandle(ref _rarityHandle);
        ReleaseHandle(ref _unknownHandle);
    }

    private void ReleaseHandle(ref AsyncOperationHandle<Sprite> handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
            handle = default;
        }
    }

    public void Init(int id, bool fishFlag, CollectionInfoState state = CollectionInfoState.Unknown, string pageName = "")
    {
        entryId = id;
        isFish = fishFlag;
        infoState = state;
        this.pageName = pageName ?? "";

        StartCoroutine(UpdateDisplayByStateCoroutine());

        if (!isFish && shinyToggleButton != null)
        {
            shinyToggleButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 根据情报状态更新显示（协程版）
    /// </summary>
    private IEnumerator UpdateDisplayByStateCoroutine()
    {
        switch (infoState)
        {
            case CollectionInfoState.Unknown:
                yield return StartCoroutine(ShowUnknownStateCoroutine());
                break;
            case CollectionInfoState.InfoObtained:
                yield return StartCoroutine(ShowInfoObtainedStateCoroutine());
                break;
            case CollectionInfoState.Obtained:
                yield return StartCoroutine(ShowObtainedStateCoroutine());
                break;
        }
    }

    /// <summary>
    /// 通用 AA 加载 Sprite 协程（修正版：返回 handle）
    /// </summary>
    private IEnumerator LoadSpriteCoroutine(string key, System.Action<Sprite, AsyncOperationHandle<Sprite>> onLoaded)
    {
        if (string.IsNullOrEmpty(key))
        {
            onLoaded?.Invoke(null, default);
            yield break;
        }
        yield return StartCoroutine(AssetManager.LoadFromAddressablesCoroutine<Sprite>(key, onLoaded));
    }

    /// <summary>
    /// 显示未获取情报状态
    /// </summary>
    private IEnumerator ShowUnknownStateCoroutine()
    {
        Sprite unknownSprite = null;
        yield return StartCoroutine(LoadSpriteCoroutine("UI/Icon/Common/unKnown", (sprite, handle) =>
        {
            unknownSprite = sprite;
            _unknownHandle = handle;
        }));
        if (unknownSprite != null)
        {
            SetIcon(unknownSprite);
        }

        nameText.text = "???";

        if (levelImage != null) levelImage.gameObject.SetActive(false);
        if (shinyImage != null) shinyImage.gameObject.SetActive(false);
        if (levelHightLightMask != null) levelHightLightMask.gameObject.SetActive(false);

        yield return StartCoroutine(UpdateRarityBackgroundCoroutine());

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = false;
        }
    }

    /// <summary>
    /// 显示已获取情报状态
    /// </summary>
    private IEnumerator ShowInfoObtainedStateCoroutine()
    {
        if (isFish)
        {
            Sprite outlineSprite = null;
            yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/FishIcons/{entryId}_Outline", (sprite, handle) =>
            {
                outlineSprite = sprite;
                _outlineHandle = handle;
            }));
            if (outlineSprite == null)
            {
                yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/FishIcons/{entryId}", (sprite, handle) =>
                {
                    outlineSprite = sprite;
                    _outlineHandle = handle;
                }));
            }
            if (outlineSprite != null)
            {
                SetIcon(outlineSprite);
            }

            var fishData = LoadDataManager.Instance?.GetFishById(entryId);
            if (fishData != null)
            {
                nameText.text = fishData.name;
            }

            yield return StartCoroutine(UpdateRarityBackgroundCoroutine());
        }
        else
        {
            itemData = LoadDataManager.Instance?.GetItemById(entryId);
            if (itemData != null)
            {
                nameText.text = itemData.name;
                Sprite outlineSprite = null;
                yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/ItemIcons/{entryId}_Outline", (sprite, handle) =>
                {
                    outlineSprite = sprite;
                    _outlineHandle = handle;
                }));
                if (outlineSprite == null)
                {
                    yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/ItemIcons/{entryId}", (sprite, handle) =>
                    {
                        outlineSprite = sprite;
                        _outlineHandle = handle;
                    }));
                }
                if (outlineSprite != null)
                {
                    SetIcon(outlineSprite);
                }
            }

            yield return StartCoroutine(UpdateRarityBackgroundCoroutine());
        }

        if (levelImage != null) levelImage.gameObject.SetActive(false);
        if (shinyImage != null) shinyImage.gameObject.SetActive(false);
        if (levelHightLightMask != null) levelHightLightMask.gameObject.SetActive(true);

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
        }
    }

    /// <summary>
    /// 显示已获取物品状态
    /// </summary>
    private IEnumerator ShowObtainedStateCoroutine()
    {
        if (isFish)
        {
            yield return StartCoroutine(LoadFishDataCoroutine());
        }
        else
        {
            yield return StartCoroutine(LoadNonFishDataCoroutine());
        }

        yield return StartCoroutine(UpdateLevelDisplayCoroutine());
        UpdateShinyDisplay();

        if (levelHightLightMask != null) levelHightLightMask.gameObject.SetActive(true);

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
        }
    }

    private IEnumerator LoadFishDataCoroutine()
    {
        itemData = LoadDataManager.Instance?.GetItemById(entryId);

        Sprite fishSprite = null;
        yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/FishIcons/{entryId}", (sprite, handle) =>
        {
            fishSprite = sprite;
            _iconHandle = handle;
        }));
        if (fishSprite != null)
        {
            SetIcon(fishSprite);
        }

        var fishData = LoadDataManager.Instance?.GetFishById(entryId);
        if (fishData != null)
        {
            nameText.text = fishData.name;
        }

        yield return StartCoroutine(UpdateRarityBackgroundCoroutine());
    }

    private IEnumerator LoadNonFishDataCoroutine()
    {
        itemData = LoadDataManager.Instance?.GetItemById(entryId);
        if (itemData != null)
        {
            nameText.text = itemData.name;
            Sprite itemSprite = null;
            yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/ItemIcons/{entryId}", (sprite, handle) =>
            {
                itemSprite = sprite;
                _iconHandle = handle;
            }));
            if (itemSprite != null)
            {
                SetIcon(itemSprite);
            }
        }

        yield return StartCoroutine(UpdateRarityBackgroundCoroutine());
    }

    private IEnumerator UpdateLevelDisplayCoroutine()
    {
        if (levelImage != null)
        {
            if (collectionLevel > 0)
            {
                Sprite levelSprite = null;
                yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/Collection/{collectionLevel}", (sprite, handle) =>
                {
                    levelSprite = sprite;
                    _levelHandle = handle;
                }));
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
        StartCoroutine(LoadIconCoroutine(isShiny));
    }

    private IEnumerator LoadIconCoroutine(bool isShiny)
    {
        if (string.IsNullOrEmpty(itemData?.iconPath))
        {
            yield break;
        }

        string basePath = itemData.iconPath;
        Sprite loadedSprite = null;

        if (isShiny)
        {
            string shinyPath = basePath + "_s";
            yield return StartCoroutine(LoadSpriteCoroutine(shinyPath, (sprite, handle) =>
            {
                loadedSprite = sprite;
                _iconHandle = handle;
            }));
        }

        if (loadedSprite == null)
        {
            yield return StartCoroutine(LoadSpriteCoroutine(basePath, (sprite, handle) =>
            {
                loadedSprite = sprite;
                _iconHandle = handle;
            }));
        }

        if (loadedSprite != null)
        {
            SetIcon(loadedSprite);
        }
    }

    /// <summary>
    /// 设置图标
    /// </summary>
    private void SetIcon(Sprite sprite)
    {
        if (icon != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(true);
        }

        if (levelHightLightMask != null)
        {
            levelHightLightMask.sprite = sprite;
            levelHightLightMask.gameObject.SetActive(true);
        }
    }

    private IEnumerator UpdateRarityBackgroundCoroutine()
    {
        if (rarityBackgroundImage == null)
        {
            yield break;
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

        Sprite raritySprite = null;
        yield return StartCoroutine(LoadSpriteCoroutine($"UI/Icon/RarityBackground/{rarityId}", (sprite, handle) =>
        {
            raritySprite = sprite;
            _rarityHandle = handle;
        }));

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
                Sprite defaultSprite = null;
                yield return StartCoroutine(LoadSpriteCoroutine("UI/Icon/RarityBackground/0", (sprite, handle) =>
                {
                    defaultSprite = sprite;
                    _rarityHandle = handle;
                }));
                if (defaultSprite != null)
                {
                    rarityBackgroundImage.sprite = defaultSprite;
                    rarityBackgroundImage.gameObject.SetActive(true);
                    rarityBackgroundImage.color = Color.white;
                    yield break;
                }
            }
            rarityBackgroundImage.gameObject.SetActive(false);
        }
    }

    public void SetCollectionLevel(int level)
    {
        collectionLevel = Mathf.Clamp(level, 0, 3);
        StartCoroutine(UpdateLevelDisplayCoroutine());
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
