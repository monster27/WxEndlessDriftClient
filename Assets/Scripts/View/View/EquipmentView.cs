using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EquipmentView : MonoBehaviour
{
    public Button maskBtn;
    public Button closeBtn;

    public MainEquipmentView mainEquipmentView;
    public FishingEquipView fishingEquipView;
    public EquipPlayerView equipPlayerView;
    public InfoSkillView infoSkillView;
    public InfoFishEquipView infoFishEquipView;

    private Dictionary<int, Sprite> iconCache = new Dictionary<int, Sprite>();

    private MonoBehaviour currentSubView;


    void Start()
    {
        if (maskBtn != null) maskBtn.onClick.AddListener(OnMaskClick);
        if (closeBtn != null) closeBtn.onClick.AddListener(OnCloseClick);
    }

    public void Init()
    {
        LoadAllIcons();

        if (mainEquipmentView != null)
        {
            mainEquipmentView.Init();
            mainEquipmentView.SetCallback(OnMainEquipmentCallback);
        }

        if (fishingEquipView != null)
        {
            fishingEquipView.Init();
            fishingEquipView.SetCallback(OnFishingEquipCallback);
        }

        if (equipPlayerView != null)
        {
            equipPlayerView.Init();
            equipPlayerView.SetCallback(OnEquipPlayerCallback);
        }

        if (infoSkillView != null)
        {
            infoSkillView.Init();
            infoSkillView.SetCallback(OnInfoSkillCallback);
        }

        if (infoFishEquipView != null)
        {
            infoFishEquipView.SetCallback(OnInfoFishEquipCallback);
        }
    }

    private void LoadAllIcons()
    {
        iconCache.Clear();

        var fishingConfig = CompleteFishingSkillConfig.LoadFromResources("JsonData/Ability/fishing_components");
        if (fishingConfig != null)
        {
            var iconPaths = fishingConfig.GetAllIconPaths();
            foreach (var kvp in iconPaths)
            {
                string path = kvp.Value;
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite != null)
                {
                    iconCache[kvp.Key] = sprite;
                }
            }
        }

        var characterConfig = CharacterConfigList.LoadFromResources();
        if (characterConfig != null)
        {
            var characterIds = characterConfig.GetAllCharacterIds();
            foreach (var id in characterIds)
            {
                string path = $"UI/Icon/Equipment/Character/{id}";
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite != null)
                {
                    iconCache[id] = sprite;
                }
            }
        }
    }

    public Sprite GetIcon(int id)
    {
        if (iconCache.TryGetValue(id, out Sprite sprite))
        {
            return sprite;
        }
        return null;
    }

    public void Show()
    {
        HideAllSubViews();
        currentSubView = mainEquipmentView;
        if (mainEquipmentView != null)
        {
            mainEquipmentView.Show();
        }
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        HideAllSubViews();
        currentSubView = null;
        gameObject.SetActive(false);
    }

    private void HideAllSubViews()
    {
        if (mainEquipmentView != null) mainEquipmentView.Hide();
        if (fishingEquipView != null) fishingEquipView.Hide();
        if (equipPlayerView != null) equipPlayerView.Hide();
        if (infoSkillView != null) infoSkillView.Hide();
        if (infoFishEquipView != null) infoFishEquipView.Hide();
    }

    private void OnMaskClick()
    {
        Hide();
    }

    private void OnCloseClick()
    {
        Hide();
    }

    private void OnMainEquipmentCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "OpenFishingRod":
                ShowFishingEquipView(FishingEquipType.Rod);
                break;
            case "OpenFishingLine":
                ShowFishingEquipView(FishingEquipType.Line);
                break;
            case "OpenFishingHook":
                ShowFishingEquipView(FishingEquipType.Hook);
                break;
            case "OpenSkill":
                int skillSlot = args != null && args.Length > 0 ? (int)args[0] : 1;
                ShowInfoSkillView(skillSlot);
                break;
            case "OpenCharacter":
                ShowEquipPlayerView();
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void OnFishingEquipCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back":
                ShowMainEquipmentView();
                break;
            case "OpenInfo":
                FishingEquipType type = (FishingEquipType)args[0];
                int equipId = (int)args[1];
                ShowInfoFishEquipView(type, equipId);
                break;
            case "EquipAction":
                FishingEquipType equipType = (FishingEquipType)args[0];
                int itemId = (int)args[1];
                OnEquipAction(equipType, itemId);
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void OnEquipPlayerCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back":
                ShowMainEquipmentView();
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void OnInfoSkillCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back":
                ShowMainEquipmentView();
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
            case "RefreshAllViews":
                RefreshAllViews();
                break;
        }
    }

    private void OnInfoFishEquipCallback(string eventName, params object[] args)
    {
        switch (eventName)
        {
            case "Back":
                FishingEquipType type = (FishingEquipType)args[0];
                ShowFishingEquipView(type);
                break;
            case "OpenAd":
                OpenAdvertisingView((string)args[0], (int)args[1], (string)args[2], (System.Action)args[3]);
                break;
        }
    }

    private void ShowMainEquipmentView()
    {
        HideAllSubViews();
        currentSubView = mainEquipmentView;
        if (mainEquipmentView != null)
        {
            mainEquipmentView.Show();
        }
    }

    public void ShowFishingEquipView(FishingEquipType type)
    {
        HideAllSubViews();
        currentSubView = fishingEquipView;
        if (fishingEquipView != null)
        {
            fishingEquipView.Show(type);
        }
    }

    public void ShowEquipPlayerView()
    {
        HideAllSubViews();
        currentSubView = equipPlayerView;
        if (equipPlayerView != null)
        {
            equipPlayerView.Show();
        }
    }

    public void ShowInfoSkillView(int skillSlot = 1)
    {
        HideAllSubViews();
        currentSubView = infoSkillView;
        if (infoSkillView != null)
        {
            infoSkillView.Show(skillSlot);
        }
    }

    public void ShowInfoFishEquipView(FishingEquipType type, int equipId)
    {
        HideAllSubViews();
        currentSubView = infoFishEquipView;
        if (infoFishEquipView != null)
        {
            infoFishEquipView.Show(type, equipId);
        }
    }

    private void OnEquipAction(FishingEquipType type, int equipId)
    {
        Debug.Log($"[EquipmentView] OnEquipAction - type={type}, equipId={equipId}");
        
        EquipmentSlotType slotType = EquipmentSlotType.FishingRod;
        switch (type)
        {
            case FishingEquipType.Rod:
                slotType = EquipmentSlotType.FishingRod;
                break;
            case FishingEquipType.Line:
                slotType = EquipmentSlotType.FishingLine;
                break;
            case FishingEquipType.Hook:
                slotType = EquipmentSlotType.FishingHook;
                break;
        }

        CommunicateEvent.Modify<(EquipmentSlotType, int)>(CommunicateEvent.EVENT_EQUIP_ITEM, (slotType, equipId));
        Debug.Log($"[EquipmentView] OnEquipAction - 已发送装备请求 {slotType} 为 {equipId}");

        mainEquipmentView.UpdateDisplay();
        if (fishingEquipView != null && fishingEquipView.gameObject.activeSelf)
        {
            fishingEquipView.UpdateDisplay();
        }
    }

    private void OpenAdvertisingView(string info, int targetId, string btnText, System.Action onConfirm)
    {
        string callbackId = CommunicateEvent.RegisterCallback(onConfirm);
        var request = new CommunicateEvent.AdvertisingRequest
        {
            info = info,
            targetId = targetId,
            btnText = btnText,
            callbackId = callbackId
        };
        CommunicateEvent.Modify<CommunicateEvent.AdvertisingRequest>(CommunicateEvent.EVENT_UI_SHOW_ADVERTISING, request);
    }

    private void RefreshAllViews()
    {
        Debug.Log("[EquipmentView] RefreshAllViews");
        if (mainEquipmentView != null)
        {
            mainEquipmentView.UpdateDisplay();
        }
        if (equipPlayerView != null && equipPlayerView.gameObject.activeSelf)
        {
            equipPlayerView.UpdateDisplay();
        }
    }
}