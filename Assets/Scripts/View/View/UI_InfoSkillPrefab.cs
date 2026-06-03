using UnityEngine;
using UnityEngine.UI;

public class UI_InfoSkillPrefab : MonoBehaviour
{
    public Image iconImg;
    public Text nameText;
    public Text levelText;
    public Text descriptionText;
    public Button detailBtn;
    public Button upgradeBtn;
    public Button watchAdBtn;
    public Button equipBtn;
    public GameObject equippedIcon;

    public GameObject ownerUseObj;
    public GameObject ownerUnUseObj;
    public GameObject lockedObj;

    private int skillId = 0;
    private System.Action<int> onDetailClick;
    private System.Action<int> onUpgradeClick;
    private System.Action<int> onWatchAdClick;
    private System.Action<int> onEquipClick;

    public void Init()
    {
        if (detailBtn != null)
        {
            detailBtn.onClick.AddListener(OnDetailBtnClick);
        }
        if (upgradeBtn != null)
        {
            upgradeBtn.onClick.AddListener(OnUpgradeBtnClick);
        }
        if (watchAdBtn != null)
        {
            watchAdBtn.onClick.AddListener(OnWatchAdBtnClick);
        }
        if (equipBtn != null)
        {
            equipBtn.onClick.AddListener(OnEquipBtnClick);
        }
    }

    public void SetData(int id, Sprite icon, string name, int level, string description, EquipState state,
                        string unlockCondition,
                        System.Action<int> detailCallback,
                        System.Action<int> upgradeCallback,
                        System.Action<int> watchAdCallback,
                        System.Action<int> equipCallback)
    {
        skillId = id;
        onDetailClick = detailCallback;
        onUpgradeClick = upgradeCallback;
        onWatchAdClick = watchAdCallback;
        onEquipClick = equipCallback;

        SetIcon(icon);
        SetName(name);
        SetLevel(level);
        
        // 如果技能未获取，显示获取方式
        if (state == EquipState.Locked && !string.IsNullOrEmpty(unlockCondition))
        {
            SetDescription(unlockCondition);
        }
        else
        {
            SetDescription(description);
        }
        
        SetState(state);

        bool isUnlocked = state != EquipState.Locked;
        bool isMaxLevel = level >= 10;
        bool isOwnerUnUse = state == EquipState.OwnerUnUse;
        bool isOwnerUse = state == EquipState.OwnerUse;

        if (upgradeBtn != null)
        {
            upgradeBtn.gameObject.SetActive(isUnlocked && !isMaxLevel);
        }
        if (watchAdBtn != null)
        {
            watchAdBtn.gameObject.SetActive(state == EquipState.Locked);
        }
        if (equipBtn != null)
        {
            equipBtn.gameObject.SetActive(isOwnerUnUse);
        }
        if (equippedIcon != null)
        {
            equippedIcon.SetActive(isOwnerUse);
        }
    }

    private void SetIcon(Sprite icon)
    {
        if (iconImg != null && icon != null)
        {
            iconImg.sprite = icon;
            iconImg.color = Color.white;
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
            levelText.text = $"等级: {level}";
        }
    }

    private void SetDescription(string description)
    {
        if (descriptionText != null)
        {
            descriptionText.text = description;
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

    public void RefreshDisplay()
    {
        // 获取当前技能状态
        EquipState state = EquipState.Locked;

        if (CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId))
        {
            int equippedSkill1 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill1);
            int equippedSkill2 = CommunicateEvent.Request<EquipmentSlotType, int>(CommunicateEvent.EVENT_GET_EQUIPPED_ITEM, EquipmentSlotType.Skill2);

            if (equippedSkill1 == skillId || equippedSkill2 == skillId)
            {
                state = EquipState.OwnerUse;
            }
            else
            {
                state = EquipState.OwnerUnUse;
            }
        }

        // 更新等级
        int level = CommunicateEvent.Request<int, int>(CommunicateEvent.EVENT_GET_COMPONENT_LEVEL, skillId);
        SetLevel(level);

        // 更新状态显示
        SetState(state);
        
        // 更新按钮状态
        bool isUnlocked = state != EquipState.Locked;
        bool isMaxLevel = level >= 10;
        bool isOwnerUnUse = state == EquipState.OwnerUnUse;
        bool isOwnerUse = state == EquipState.OwnerUse;

        if (upgradeBtn != null)
        {
            upgradeBtn.gameObject.SetActive(isUnlocked && !isMaxLevel);
        }
        if (watchAdBtn != null)
        {
            watchAdBtn.gameObject.SetActive(state == EquipState.Locked);
        }
        if (equipBtn != null)
        {
            equipBtn.gameObject.SetActive(isOwnerUnUse);
        }
        if (equippedIcon != null)
        {
            equippedIcon.SetActive(isOwnerUse);
        }
    }

    private void OnDetailBtnClick()
    {
        Debug.Log($"[UI_InfoSkillPrefab] OnDetailBtnClick - skillId={skillId}");
        
        // 检查技能是否已解锁
        bool isObtained = CommunicateEvent.Request<int, bool>(CommunicateEvent.EVENT_IS_SKILL_OBTAINED, skillId);
        
        if (!isObtained)
        {
            // 未解锁，播放看广告获取界面
            OnWatchAdBtnClick();
        }
        else
        {
            // 已解锁，打开信息界面
            if (onDetailClick != null)
            {
                onDetailClick(skillId);
            }
        }
    }

    private void OnUpgradeBtnClick()
    {
        if (onUpgradeClick != null)
        {
            onUpgradeClick(skillId);
        }
    }

    private void OnWatchAdBtnClick()
    {
        if (onWatchAdClick != null)
        {
            onWatchAdClick(skillId);
        }
    }

    private void OnEquipBtnClick()
    {
        Debug.Log($"[UI_InfoSkillPrefab] OnEquipBtnClick - skillId={skillId}");
        if (onEquipClick != null)
        {
            onEquipClick(skillId);
        }
        else
        {
            Debug.LogWarning("[UI_InfoSkillPrefab] onEquipClick is null!");
        }
    }
}
