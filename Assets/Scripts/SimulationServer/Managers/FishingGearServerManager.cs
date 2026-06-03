// ========================================================
// 模拟服务器已被移除 - 客户端现在仅使用网络服务器模式
// 此文件中的所有代码已被注释，以支持纯在线模式
// ========================================================
/*
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 钓鱼装备服务器管理器
/// 负责从fishing_components.json加载和管理钓鱼装备配置
/// </summary>
public class FishingGearServerManager : MonoBehaviour
{
    private static FishingGearServerManager instance;
    public static FishingGearServerManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new FishingGearServerManager();
                instance.LoadGearConfig();
            }
            return instance;
        }
    }

    private List<FishingGear> gearList = new List<FishingGear>();

    private const string COMPONENTS_PATH = "Resources/JsonData/Ability/fishing_components.json";

    private void LoadGearConfig()
    {
        string fullPath = Path.Combine(Application.dataPath, COMPONENTS_PATH);

        gearList = new List<FishingGear>();

        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                var array = JsonUtility.FromJson<FishingComponentConfigArray>(json);
                if (array != null && array.items != null)
                {
                    foreach (var component in array.items)
                    {
                        if (component.category == FishingComponentCategory.Skill)
                            continue;

                        FishingGear gear = ConvertToFishingGear(component);
                        if (gear != null)
                        {
                            gearList.Add(gear);
                        }
                    }
                    Debug.Log($"[FishingGearServerManager] 成功加载 {gearList.Count} 个钓鱼装备");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FishingGearServerManager] 加载装备配置失败: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[FishingGearServerManager] 装备配置文件不存在: {fullPath}");
        }
    }

    private FishingGear ConvertToFishingGear(FishingComponentConfig component)
    {
        if (component == null || component.levelDataList == null || component.levelDataList.Count == 0)
            return null;

        var level1Data = component.levelDataList.FirstOrDefault(l => l.level == 1);
        if (level1Data == null || level1Data.paramsList == null)
            return null;

        FishingGear gear = new FishingGear();
        gear.id = component.id;
        gear.name = component.name;
        gear.description = component.description;
        gear.type = GetGearType(component.category);
        gear.rarity = GetRarityById(component.id);
        gear.stats = new GearStats();

        foreach (var param in level1Data.paramsList)
        {
            if (param.paramId == 0) continue;
            ApplyGearStat(gear.stats, component.category, param.paramId, param.value);
        }

        return gear;
    }

    private void ApplyGearStat(GearStats stats, FishingComponentCategory category, int paramId, float value)
    {
        switch (category)
        {
            case FishingComponentCategory.Rod:
                switch (paramId)
                {
                    case 709:
                        stats.catchRate = 1.0f + value;
                        break;
                    case 708:
                        stats.struggleTime = 1.0f - value;
                        break;
                    case 712:
                        stats.minigameDifficulty = 1.0f - value;
                        break;
                }
                break;

            case FishingComponentCategory.Line:
                switch (paramId)
                {
                    case 710:
                    case 703:
                        stats.rareFishRate = 1.0f + value;
                        break;
                    case 704:
                        stats.qualityRate = 1.0f + value;
                        break;
                }
                break;

            case FishingComponentCategory.Hook:
                switch (paramId)
                {
                    case 711:
                        stats.shinyRate = 1.0f + value;
                        break;
                    case 707:
                        stats.qualityRate = 1.0f + value;
                        break;
                }
                break;
        }
    }

    private string GetGearType(FishingComponentCategory category)
    {
        return category switch
        {
            FishingComponentCategory.Rod => "Rod",
            FishingComponentCategory.Line => "Line",
            FishingComponentCategory.Hook => "Hook",
            _ => "Unknown"
        };
    }

    private string GetRarityById(int id)
    {
        int idInCategory = id % 100;
        if (idInCategory % 2 == 1)
            return "Common";
        else
            return "Rare";
    }

    public FishingGear GetGearById(int gearId)
    {
        return gearList.Find(g => g.id == gearId);
    }

    public List<FishingGear> GetGearsByType(string gearType)
    {
        return gearList.FindAll(g => g.type.Equals(gearType, System.StringComparison.OrdinalIgnoreCase));
    }

    public List<FishingGear> GetAllGears()
    {
        return new List<FishingGear>(gearList);
    }

    public GearStats CalculateCombinedStats(List<int> equippedGearIds)
    {
        GearStats combined = new GearStats();

        foreach (int gearId in equippedGearIds)
        {
            FishingGear gear = GetGearById(gearId);
            if (gear != null)
            {
                combined.catchRate *= gear.stats.catchRate;
                combined.struggleTime *= gear.stats.struggleTime;
                combined.minigameDifficulty *= gear.stats.minigameDifficulty;
                combined.rareFishRate *= gear.stats.rareFishRate;
                combined.biteRate *= gear.stats.biteRate;
                combined.shinyRate *= gear.stats.shinyRate;
                combined.qualityRate *= gear.stats.qualityRate;
            }
        }

        return combined;
    }
}

[System.Serializable]
public class FishingGear
{
    public int id;
    public string name;
    public string description;
    public string type;
    public string rarity;
    public GearStats stats;
}

[System.Serializable]
public class GearStats
{
    public float catchRate = 1.0f;
    public float struggleTime = 1.0f;
    public float minigameDifficulty = 1.0f;
    public float rareFishRate = 1.0f;
    public float biteRate = 1.0f;
    public float shinyRate = 1.0f;
    public float qualityRate = 1.0f;
}
*/
