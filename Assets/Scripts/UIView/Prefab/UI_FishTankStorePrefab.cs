using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System;

public class UI_FishTankStorePrefab : MonoBehaviour
{
    [Header("===== 显示组件 =====")]
    [SerializeField] private Image rarityBg;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text weightText;
    [SerializeField] private Image starImage;
    [SerializeField] private GameObject shinyIcon;
    [SerializeField] private Text harvestText;

    [Header("===== 点击 =====")]
    [SerializeField] private Button clickBtn;

    private FishDetailData _fishDetail;
    private AsyncOperationHandle<Sprite> _iconHandle;
    private AsyncOperationHandle<Sprite> _rarityHandle;
    private AsyncOperationHandle<Sprite> _starHandle;

    private Action<UI_FishTankStorePrefab> _onClick;
    private bool _isDestroyed = false;

    public FishDetailData FishDetail => _fishDetail;

    private void Awake()
    {
        if (clickBtn != null)
            clickBtn.onClick.AddListener(OnClick);
    }

    public void Init(FishDetailData detail)
    {
        if (_isDestroyed) return;
        _fishDetail = detail;
        UpdateDisplay();
    }

    public void UpdateData(FishDetailData detail)
    {
        if (_isDestroyed) return;
        if (_fishDetail != null && _fishDetail.id == detail.id)
        {
            _fishDetail = detail;
            UpdateDisplay();
        }
        else
        {
            Init(detail);
        }
    }

    public void SetClickCallback(Action<UI_FishTankStorePrefab> callback)
    {
        _onClick = callback;
    }

    private void UpdateDisplay()
    {
        if (_fishDetail == null || _isDestroyed) return;

        if (nameText != null)
        {
            var itemData = LoadDataManager.Instance?.GetItemById(_fishDetail.fishId);
            nameText.text = itemData?.name ?? $"鱼#{_fishDetail.fishId}";
        }

        if (weightText != null)
        {
            weightText.text = $"{_fishDetail.weight:F2}kg";
        }

        if (shinyIcon != null)
            shinyIcon.SetActive(_fishDetail.isShiny);

        LoadRarityBackground();
        LoadStarIcon();
        LoadIcon();

        if (harvestText != null)
        {
            float displayMultiplier = LoadDataManager.Instance.baseEarningRate; 
            int displayPrice = Mathf.RoundToInt(_fishDetail.calculatedPrice * displayMultiplier);
            harvestText.text = $" {displayPrice}";
            harvestText.gameObject.SetActive(displayPrice > 0);
        }

        if (clickBtn != null)
            clickBtn.interactable = true;
    }

    private void LoadRarityBackground()
    {
        if (rarityBg == null || _isDestroyed) return;

        var fishData = LoadDataManager.Instance?.GetFishById(_fishDetail.fishId);
        int rarityId = fishData?.rarityId ?? 0;

        string path = $"UI/Icon/RarityBackground/{rarityId}";
        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
        {
            if (_isDestroyed || this == null || rarityBg == null)
            {
                AssetManager.ReleaseAddressable(handle);
                return;
            }

            _rarityHandle = handle;
            if (sprite != null)
            {
                rarityBg.sprite = sprite;
                rarityBg.gameObject.SetActive(true);
            }
            else
            {
                rarityBg.gameObject.SetActive(false);
            }
        });
    }

    private void LoadStarIcon()
    {
        if (starImage == null || _isDestroyed) return;

        int starId = _fishDetail.starRatingId;
        if (starId <= 0)
        {
            starImage.gameObject.SetActive(false);
            return;
        }

        string path = $"UI/Icon/StarRating/star_{starId}";
        AssetManager.LoadFromAddressables<Sprite>(path, (sprite, handle) =>
        {
            if (_isDestroyed || this == null || starImage == null)
            {
                AssetManager.ReleaseAddressable(handle);
                return;
            }

            _starHandle = handle;
            if (sprite != null)
            {
                starImage.sprite = sprite;
                starImage.gameObject.SetActive(true);
            }
            else
            {
                starImage.gameObject.SetActive(false);
            }
        });
    }

    private void LoadIcon()
    {
        if (iconImage == null || _isDestroyed) return;

        var itemData = LoadDataManager.Instance?.GetItemById(_fishDetail.fishId);
        string basePath = itemData?.iconPath ?? "";
        if (string.IsNullOrEmpty(basePath))
        {
            iconImage.gameObject.SetActive(false);
            return;
        }

        string loadPath = _fishDetail.isShiny ? basePath + "_s" : basePath;
        AssetManager.LoadFromAddressables<Sprite>(loadPath, (sprite, handle) =>
        {
            if (_isDestroyed || this == null || iconImage == null)
            {
                AssetManager.ReleaseAddressable(handle);
                return;
            }

            _iconHandle = handle;
            if (sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                string fallbackPath = basePath;
                AssetManager.LoadFromAddressables<Sprite>(fallbackPath, (fallbackSprite, fallbackHandle) =>
                {
                    if (_isDestroyed || this == null || iconImage == null)
                    {
                        AssetManager.ReleaseAddressable(fallbackHandle);
                        return;
                    }

                    _iconHandle = fallbackHandle;
                    if (fallbackSprite != null)
                    {
                        iconImage.sprite = fallbackSprite;
                        iconImage.gameObject.SetActive(true);
                    }
                    else
                    {
                        iconImage.gameObject.SetActive(false);
                    }
                });
            }
        });
    }

    private void OnClick()
    {
        if (_isDestroyed) return;
        _onClick?.Invoke(this);
    }

    private void OnDestroy()
    {
        _isDestroyed = true;

        AssetManager.ReleaseAddressable(_iconHandle);
        AssetManager.ReleaseAddressable(_rarityHandle);
        AssetManager.ReleaseAddressable(_starHandle);

        if (clickBtn != null)
            clickBtn.onClick.RemoveAllListeners();
    }
}
