using UnityEngine;
using System.Collections.Generic;

public class SkinManager : SingletonMonoFromScene<SkinManager>
{
    public GameObject indoorSceneObj;
    public GameObject outdoorSceneObj;

    private Dictionary<int, int> equippedSkins = new Dictionary<int, int>();
    private bool isSceneMatReady = false;
    private bool hasPendingSkins = false;

    private Dictionary<int, SceneMatManager.RenderElementType> slotTypeToRenderType = new Dictionary<int, SceneMatManager.RenderElementType>
    {
        { 41, SceneMatManager.RenderElementType.FishBag },
        { 42, SceneMatManager.RenderElementType.Tent },
        { 43, SceneMatManager.RenderElementType.FishTip },
        { 51, SceneMatManager.RenderElementType.Indoor_Wall },
        { 52, SceneMatManager.RenderElementType.Indoor_Floor },
        { 53, SceneMatManager.RenderElementType.Indoor_Stair },
        { 54, SceneMatManager.RenderElementType.Indoor_LightStrip },
        { 55, SceneMatManager.RenderElementType.Indoor_HungDecoration },
        { 56, SceneMatManager.RenderElementType.Indoor_Telescope },
        { 57, SceneMatManager.RenderElementType.Indoor_InsectRoom },
        { 58, SceneMatManager.RenderElementType.Indoor_PetHouse },
        { 59, SceneMatManager.RenderElementType.Indoor_FishTank },
        { 60, SceneMatManager.RenderElementType.Indoor_Panda },
        { 61, SceneMatManager.RenderElementType.Indoor_Parrot },
        { 62, SceneMatManager.RenderElementType.Indoor_Table }
    };

    private const string OUTDOOR_SKIN_PATH_PREFIX = "UI/Icon/OutdoorSkinIcons/";
    private const string INDOOR_SKIN_PATH_PREFIX = "UI/Icon/IndoorSkinIcons/";

    private void Awake()
    {
        RegisterEvents();
        CheckAndApplySkins();
    }

    private void CheckAndApplySkins()
    {
        if (equippedSkins.Count > 0 && SceneMatManager.Instance != null && SceneMatManager.Instance.IsInitialized)
        {
            Debug.Log("[SkinManager] Awake时发现已有皮肤数据，立即应用");
            ApplyAllSkins();
        }
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register<Dictionary<int, int>>(CommunicateEvent.EVENT_SKIN_DATA_UPDATED, OnSkinDataUpdated);
        CommunicateEvent.Register<string>(CommunicateEvent.EVENT_ALL_LOADING_COMPLETE, OnAllLoadingComplete);
    }

    private void OnDestroy()
    {
        CommunicateEvent.Unregister<Dictionary<int, int>>(CommunicateEvent.EVENT_SKIN_DATA_UPDATED, OnSkinDataUpdated);
        CommunicateEvent.Unregister<string>(CommunicateEvent.EVENT_ALL_LOADING_COMPLETE, OnAllLoadingComplete);
    }

    private void OnAllLoadingComplete(string message)
    {
        Debug.Log("[SkinManager] 收到所有加载完成事件，开始应用皮肤");
        isSceneMatReady = true;
        if (hasPendingSkins && equippedSkins.Count > 0)
        {
            ApplyAllSkins();
        }
    }

    public void SwitchToIndoorScene()
    {
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(true);
        }
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(false);
        }
        Debug.Log("[SkinManager] 切换到室内场景");
    }

    public void SwitchToOutdoorScene()
    {
        if (outdoorSceneObj != null)
        {
            outdoorSceneObj.SetActive(true);
        }
        if (indoorSceneObj != null)
        {
            indoorSceneObj.SetActive(false);
        }
        Debug.Log("[SkinManager] 切换到室外场景");
    }

    public void EquipSkin(int slotType, int skinId)
    {
        equippedSkins[slotType] = skinId;
        Debug.Log($"[SkinManager] 装备皮肤: slotType={slotType}, skinId={skinId}");
        ApplySkinRender(slotType, skinId);
    }

    public int GetEquippedSkin(int slotType)
    {
        if (equippedSkins.TryGetValue(slotType, out int skinId))
        {
            return skinId;
        }
        return 0;
    }

    public bool IsSkinEquipped(int itemId)
    {
        return equippedSkins.ContainsValue(itemId);
    }

    public Dictionary<int, int> GetAllEquippedSkins()
    {
        return new Dictionary<int, int>(equippedSkins);
    }

    public void ApplyAllSkins()
    {
        Debug.Log($"[SkinManager] 开始应用所有皮肤，共 {equippedSkins.Count} 个");
        foreach (var kvp in equippedSkins)
        {
            ApplySkinRender(kvp.Key, kvp.Value);
        }
        hasPendingSkins = false;
    }

    private void OnSkinDataUpdated(Dictionary<int, int> skins)
    {
        equippedSkins.Clear();
        foreach (var kvp in skins)
        {
            equippedSkins[kvp.Key] = kvp.Value;
        }
        Debug.Log($"[SkinManager] 皮肤数据更新，共 {skins.Count} 个皮肤");

        if (isSceneMatReady && SceneMatManager.Instance != null && SceneMatManager.Instance.IsInitialized)
        {
            ApplyAllSkins();
        }
        else
        {
            hasPendingSkins = true;
            Debug.Log("[SkinManager] SceneMatManager尚未就绪，延迟应用皮肤");
        }
    }

    private void ApplySkinRender(int slotType, int skinId)
    {
        if (!slotTypeToRenderType.TryGetValue(slotType, out SceneMatManager.RenderElementType renderType))
        {
            Debug.LogWarning($"[SkinManager] 未找到slotType={slotType}对应的渲染类型");
            return;
        }

        if (SceneMatManager.Instance == null)
        {
            Debug.LogWarning("[SkinManager] SceneMatManager未找到");
            return;
        }

        if (!SceneMatManager.Instance.IsInitialized)
        {
            Debug.LogWarning("[SkinManager] SceneMatManager尚未初始化");
            return;
        }

        SceneMatCtrl controller = SceneMatManager.Instance.GetController(renderType);
        if (controller == null)
        {
            Debug.LogWarning($"[SkinManager] 未找到渲染类型{renderType}对应的控制器");
            return;
        }

        string skinPath = GetSkinPath(slotType, skinId);
        Debug.Log($"[SkinManager] 应用皮肤: slotType={slotType}, skinId={skinId}, renderType={renderType}, path={skinPath}");

        controller.SetMainTextureByPath(skinPath);
    }

    private string GetSkinPath(int slotType, int skinId)
    {
        if (slotType >= 41 && slotType <= 43)
        {
            return OUTDOOR_SKIN_PATH_PREFIX + skinId;
        }
        else if (slotType >= 51)
        {
            return INDOOR_SKIN_PATH_PREFIX + skinId;
        }
        return "";
    }
}
