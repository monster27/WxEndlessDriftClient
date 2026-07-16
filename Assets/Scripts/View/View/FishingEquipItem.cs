using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class FishingEquipItem : MonoBehaviour
{
    public Button equipBtn;
    public Button equipActionBtn;
    public Button watchAdBtn;
    public Image longIcon;
    public Image shortIcon;
    public Text nameText;
    public Text levelText;
    public Image levelIcon;
    public GameObject ownerUseObj;
    public GameObject ownerUnUseObj;
    public GameObject lockedObj;

    private Dictionary<int, Sprite> levelIconCache = new Dictionary<int, Sprite>();

    private int currentEquipId = 0;
    private FishingEquipType currentType = FishingEquipType.Rod;
    private System.Action<FishingEquipType, int> onClickCallback;
    private System.Action<FishingEquipType, int> onEquipActionCallback;
    private System.Action<FishingEquipType, int> onWatchAdCallback;

    public void Init()
    {
        if (equipBtn != null)
        {
            equipBtn.onClick.AddListener(OnEquipClick);
        }
        if (equipActionBtn != null)
        {
            equipActionBtn.onClick.AddListener(OnEquipActionClick);
        }
        if (watchAdBtn != null)
        {
            watchAdBtn.onClick.AddListener(OnWatchAdClick);
        }

        LoadLevelIcons();
    }

    private void LoadLevelIcons()
    {
        levelIconCache.Clear();
        for (int i = 1; i <= 10; i++)
        {
            string path = $"UI/Icon/Equipment/Level/{i}";
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
            {
                levelIconCache[i] = sprite;
            }
        }
    }

    public void SetData(FishingEquipType type, int equipId, Sprite icon, string name, EquipState state, System.Action<FishingEquipType, int> onClick)
    {
        SetData(type, equipId, icon, name, state, onClick, null, null, 1);
    }

    public void SetData(FishingEquipType type, int equipId, Sprite icon, string name, EquipState state, System.Action<FishingEquipType, int> onClick, System.Action<FishingEquipType, int> onEquipAction, System.Action<FishingEquipType, int> onWatchAd)
    {
        SetData(type, equipId, icon, name, state, onClick, onEquipAction, onWatchAd, 1);
    }

    public void SetData(FishingEquipType type, int equipId, Sprite icon, string name, EquipState state, System.Action<FishingEquipType, int> onClick, System.Action<FishingEquipType, int> onEquipAction, System.Action<FishingEquipType, int> onWatchAd, int level)
    {
        currentType = type;
        currentEquipId = equipId;
        onClickCallback = onClick;
        onEquipActionCallback = onEquipAction;
        onWatchAdCallback = onWatchAd;

        SetIcon(type, icon);
        SetName(name);
        SetState(state);
        SetLevel(level);
    }

    private void SetIcon(FishingEquipType type, Sprite icon)
    {
        if (icon == null) return;

        bool useLongIcon = (type == FishingEquipType.Rod);

        if (longIcon != null)
        {
            longIcon.sprite = icon;
            longIcon.color = Color.white;
            longIcon.gameObject.SetActive(useLongIcon);
        }

        if (shortIcon != null)
        {
            shortIcon.sprite = icon;
            shortIcon.color = Color.white;
            shortIcon.gameObject.SetActive(!useLongIcon);
        }
    }

    private void SetName(string name)
    {
        if (nameText != null)
        {
            nameText.text = name;
        }
    }

    private void SetLevel(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Lv.{level}";
            levelText.gameObject.SetActive(true);
        }

        if (levelIcon != null)
        {
            if (levelIconCache.TryGetValue(level, out Sprite icon))
            {
                levelIcon.sprite = icon;
                levelIcon.color = Color.white;
                levelIcon.gameObject.SetActive(true);
            }
            else
            {
                levelIcon.gameObject.SetActive(false);
            }
        }
    }

    private void SetState(EquipState state)
    {
        if (ownerUseObj != null)
        {
            ownerUseObj.SetActive(state == EquipState.OwnerUse);
        }

        if (ownerUnUseObj != null)
        {
            ownerUnUseObj.SetActive(state == EquipState.OwnerUnUse);
        }

        if (lockedObj != null)
        {
            lockedObj.SetActive(state == EquipState.Locked);
        }
    }

    private void OnEquipClick()
    {
        Debug.Log($"[FishingEquipItem] OnEquipClick - type={currentType}, equipId={currentEquipId}");
        
        // 获取当前状态
        bool isLocked = lockedObj != null && lockedObj.activeSelf;
        
        if (isLocked)
        {
            // 未解锁，播放看广告获取界面
            OnWatchAdClick();
        }
        else
        {
            // 已解锁，打开信息界面
            if (onClickCallback != null)
            {
                onClickCallback(currentType, currentEquipId);
            }
        }
    }

    private void OnEquipActionClick()
    {
        Debug.Log($"[FishingEquipItem] OnEquipActionClick - type={currentType}, equipId={currentEquipId}");
        if (onEquipActionCallback != null)
        {
            onEquipActionCallback(currentType, currentEquipId);
        }
    }

    private void OnWatchAdClick()
    {
        Debug.Log($"[FishingEquipItem] OnWatchAdClick - type={currentType}, equipId={currentEquipId}");
        if (onWatchAdCallback != null)
        {
            onWatchAdCallback(currentType, currentEquipId);
        }
    }

    public void SetNameObjectActive(bool active)
    {
        gameObject.SetActive(active);
    }
}

public enum EquipState
{
    OwnerUse,
    OwnerUnUse,
    Locked
}
