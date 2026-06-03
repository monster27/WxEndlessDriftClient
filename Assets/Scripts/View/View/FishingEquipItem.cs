using UnityEngine;
using UnityEngine.UI;

public class FishingEquipItem : MonoBehaviour
{
    public Button equipBtn;
    public Button equipActionBtn;
    public Button watchAdBtn;
    public Image longIcon;
    public Image shortIcon;
    public Text nameText;
    public GameObject ownerUseObj;
    public GameObject ownerUnUseObj;
    public GameObject lockedObj;

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
    }

    public void SetData(FishingEquipType type, int equipId, Sprite icon, string name, EquipState state, System.Action<FishingEquipType, int> onClick)
    {
        SetData(type, equipId, icon, name, state, onClick, null, null);
    }

    public void SetData(FishingEquipType type, int equipId, Sprite icon, string name, EquipState state, System.Action<FishingEquipType, int> onClick, System.Action<FishingEquipType, int> onEquipAction, System.Action<FishingEquipType, int> onWatchAd)
    {
        currentType = type;
        currentEquipId = equipId;
        onClickCallback = onClick;
        onEquipActionCallback = onEquipAction;
        onWatchAdCallback = onWatchAd;

        SetIcon(type, icon);
        SetName(name);
        SetState(state);
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
