// ==================== LoadDataManager.cs ====================
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using SharedModels;

public class LoadDataManager : SingletonMono<LoadDataManager>
{
    [Header("JSON文件路径")]
    private string islandsJsonPath = "JsonData/BaseFramework/islands";
    private string raritiesJsonPath = "JsonData/BaseFramework/rarities";
    private string baitsJsonPath = "JsonData/Game/BagItem/baits";
    private string timeSlotsJsonPath = "JsonData/BaseFramework/timeSlots";
    private string weathersJsonPath = "JsonData/BaseFramework/weathers";
    private string starRatingsJsonPath = "JsonData/BaseFramework/starRatings";
    private string fishSpeciesJsonPath = "JsonData/BaseFramework/fishSpecies";
    private string fishesJsonPath = "JsonData/Game/BagItem/fishes";
    private string itemCategoriesJsonPath = "JsonData/Game/GameFramework/itemCategories";
    private string itemsJsonPath = "JsonData/Game/Items/items";
    private string trashJsonPath = "JsonData/Game/BagItem/trash";
    private string abilitiesJsonPath = "JsonData/Ability/abilities";
    private string fishingComponentsJsonPath = "JsonData/Ability/fishing_components";
    private string charactersJsonPath = "JsonData/BaseFramework/characters";

    // ==================== 数据存储区域 ====================
    public List<IslandData> islands = new List<IslandData>();
    public List<RarityData> rarities = new List<RarityData>();
    public List<BaitData> baits = new List<BaitData>();
    public List<TimeSlotData> timeSlots = new List<TimeSlotData>();
    public List<WeatherData> weathers = new List<WeatherData>();
    public List<StarRatingData> starRatings = new List<StarRatingData>();
    public List<FishSpeciesData> fishSpecies = new List<FishSpeciesData>();
    public List<FishData> fishes = new List<FishData>();
    public List<BagCategoryData> bagCategories = new List<BagCategoryData>();
    public List<ItemData> items = new List<ItemData>();
    public List<TrashData> trashList = new List<TrashData>();
    public List<AbilityData> abilities = new List<AbilityData>();
    public List<FishingComponentConfig> fishingComponents = new List<FishingComponentConfig>();
    public List<CharacterConfig> characters = new List<CharacterConfig>();

    // 数据内容存储字符串
    private StringBuilder dataLog = new StringBuilder();
    // 数据加载完成标志
    public bool isDataLoaded = false;
    // 初始化完成事件
    public System.Action onDataLoaded;

    protected override void Awake()
    {
        base.Awake();
        // 初始化逻辑已移至Init方法
    }

    public void Init()
    {
        LoadAllData();
        PrintAllData();
        RegisterEvents();
    }

    private void RegisterEvents()
    {
        // 注册背包相关事件
        CommunicateEvent.Register("Bag_Open", HandleBagOpenEvent);
        CommunicateEvent.Register("Bag_Init", HandleBagInitEvent);
        CommunicateEvent.Register("Bag_RefreshItems", HandleBagRefreshItemsEvent);

        // 注册鱼篓相关事件
        CommunicateEvent.Register("FishBag_Open", HandleFishBagOpenEvent);
        CommunicateEvent.Register("FishBag_Init", HandleFishBagInitEvent);
        CommunicateEvent.Register("FishBag_RefreshItems", HandleFishBagRefreshItemsEvent);
    }

    // ==================== 数据加载方法区域 ====================

    public void LoadAllData()
    {
        dataLog.Clear();
        dataLog.AppendLine("========== 数据加载日志 ==========");

        LoadIslandData();
        LoadRarityData();
        LoadBaitData();
        LoadTimeSlotData();
        LoadWeatherData();
        LoadStarRatingData();
        LoadFishSpeciesData();
        LoadFishData();
        LoadBagCategoryData();
        LoadItemData();
        LoadTrashData();
        LoadAbilityData();
        LoadFishingComponentsData();
        LoadCharactersData();

        dataLog.AppendLine("===================================");
        isDataLoaded = true;

        // 触发数据加载完成事件
        if (onDataLoaded != null)
        {
            Debug.Log("[LoadDataManager] 触发数据加载完成事件");
            onDataLoaded();
        }
    }

    private void LoadIslandData()
    {
        string json = RWJsonData.LoadJsonFromResources(islandsJsonPath);
        var wrapper = RWJsonData.ParseJson<IslandListWrapper>(json);
        islands = (wrapper != null && wrapper.islands != null) ? wrapper.islands : new List<IslandData>();

        if (islands.Count > 0)
        {
            dataLog.AppendLine($"✓ 岛屿数据: 成功加载 {islands.Count} 个岛屿");
            foreach (var item in islands)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 岛屿数据: 加载失败");
        }
    }

    private void LoadRarityData()
    {
        string json = RWJsonData.LoadJsonFromResources(raritiesJsonPath);
        var wrapper = RWJsonData.ParseJson<RarityListWrapper>(json);
        rarities = (wrapper != null && wrapper.rarities != null) ? wrapper.rarities : new List<RarityData>();

        if (rarities.Count > 0)
        {
            dataLog.AppendLine($"✓ 稀有度数据: 成功加载 {rarities.Count} 个稀有度");
            foreach (var item in rarities)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 颜色: {item.color}, 权重: {item.weight}, 经验: {item.exp}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 稀有度数据: 加载失败");
        }
    }

    private void LoadBaitData()
    {
        string json = RWJsonData.LoadJsonFromResources(baitsJsonPath);
        var wrapper = RWJsonData.ParseJson<BaitListWrapper>(json);
        baits = (wrapper != null && wrapper.baits != null) ? new List<BaitData>(wrapper.baits) : new List<BaitData>();

        if (baits.Count > 0)
        {
            dataLog.AppendLine($"✓ 鱼饵数据: 成功加载 {baits.Count} 个鱼饵");
            foreach (var item in baits)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 权重: {item.baseWeight}, 解锁场景: {item.unlockScene}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 鱼饵数据: 加载失败");
        }
    }

    private void LoadTimeSlotData()
    {
        string json = RWJsonData.LoadJsonFromResources(timeSlotsJsonPath);
        var wrapper = RWJsonData.ParseJson<TimeSlotListWrapper>(json);
        timeSlots = (wrapper != null && wrapper.timeSlots != null) ? wrapper.timeSlots : new List<TimeSlotData>();

        if (timeSlots.Count > 0)
        {
            dataLog.AppendLine($"✓ 时段数据: 成功加载 {timeSlots.Count} 个时段");
            foreach (var item in timeSlots)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 时长: {item.durationMinutes}分钟, 权重: {item.weight}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 时段数据: 加载失败");
        }
    }

    private void LoadWeatherData()
    {
        string json = RWJsonData.LoadJsonFromResources(weathersJsonPath);
        var wrapper = RWJsonData.ParseJson<WeatherListWrapper>(json);
        weathers = (wrapper != null && wrapper.weathers != null) ? wrapper.weathers : new List<WeatherData>();

        if (weathers.Count > 0)
        {
            dataLog.AppendLine($"✓ 天气数据: 成功加载 {weathers.Count} 个天气");
            foreach (var item in weathers)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 概率: {item.percentage}%, 权重: {item.weight}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 天气数据: 加载失败");
        }
    }

    private void LoadStarRatingData()
    {
        string json = RWJsonData.LoadJsonFromResources(starRatingsJsonPath);
        var wrapper = RWJsonData.ParseJson<StarRatingListWrapper>(json);
        starRatings = (wrapper != null && wrapper.starRatings != null) ? wrapper.starRatings : new List<StarRatingData>();

        if (starRatings.Count > 0)
        {
            dataLog.AppendLine($"✓ 星级倍数数据: 成功加载 {starRatings.Count} 个星级");
            foreach (var item in starRatings)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 倍率: {item.multiplier}, 颜色: {item.color}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 星级倍数数据: 加载失败");
        }
    }

    private void LoadFishSpeciesData()
    {
        string json = RWJsonData.LoadJsonFromResources(fishSpeciesJsonPath);
        var wrapper = RWJsonData.ParseJson<FishSpeciesListWrapper>(json);
        fishSpecies = (wrapper != null && wrapper.fishSpecies != null) ? wrapper.fishSpecies : new List<FishSpeciesData>();

        if (fishSpecies.Count > 0)
        {
            dataLog.AppendLine($"✓ 鱼类品种数据: 成功加载 {fishSpecies.Count} 个品种");
            foreach (var item in fishSpecies)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 移动类型: {item.movementType}, 位置类型: {item.positionType}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 鱼类品种数据: 加载失败");
        }
    }

    private void LoadFishData()
    {
        string json = RWJsonData.LoadJsonFromResources(fishesJsonPath);
        var wrapper = RWJsonData.ParseJson<FishListWrapper>(json);
        fishes = (wrapper != null && wrapper.fishes != null) ? wrapper.fishes : new List<FishData>();

        if (fishes.Count > 0)
        {
            dataLog.AppendLine($"✓ 鱼类数据: 成功加载 {fishes.Count} 条鱼");
            foreach (var item in fishes)
            {
                string islandStr = item.islandId == 0 ? "所有岛屿" : item.islandId.ToString();
                string preferredStr = item.preferredIslandIds.Count > 0 ? string.Join(",", item.preferredIslandIds) : "无";
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 存在岛屿: {islandStr}, 偏向岛屿: [{preferredStr}], 稀有度ID: {item.rarityId}, 重量: {item.baseWeight}kg, 经验: {item.baseExp}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 鱼类数据: 加载失败");
        }
    }

    private void LoadBagCategoryData()
    {
        bagCategories.Clear();
        string json = RWJsonData.LoadJsonFromResources(itemCategoriesJsonPath);
        var wrapper = RWJsonData.ParseJson<ItemCategoryListWrapper>(json);

        if (wrapper != null && wrapper.categories != null)
        {
            foreach (var category in wrapper.categories)
            {
                var bagCategory = new BagCategoryData
                {
                    id = category.id,
                    folderName = category.code,
                    categoryName = category.name,
                    sortOrder = category.id
                };
                bagCategories.Add(bagCategory);

                if (category.subCategories != null)
                {
                    foreach (var subCat in category.subCategories)
                    {
                        var subBagCategory = new BagCategoryData
                        {
                            id = subCat.id,
                            folderName = category.code,
                            categoryName = subCat.name,
                            sortOrder = subCat.id
                        };
                        bagCategories.Add(subBagCategory);
                    }
                }
            }
        }

        if (bagCategories.Count > 0)
        {
            dataLog.AppendLine($"✓ 背包分类数据: 成功加载 {bagCategories.Count} 个分类");
            foreach (var item in bagCategories)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 文件夹: {item.folderName}, 分类名称: {item.categoryName}, 排序: {item.sortOrder}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 背包分类数据: 加载失败");
        }
    }

    private void LoadItemData()
    {
        string json = RWJsonData.LoadJsonFromResources(itemsJsonPath);
        var wrapper = RWJsonData.ParseJson<ItemListWrapper>(json);
        items = (wrapper != null && wrapper.items != null) ? wrapper.items : new List<ItemData>();

        if (items.Count > 0)
        {
            dataLog.AppendLine($"✓ 物品数据: 成功加载 {items.Count} 个物品");
            foreach (var item in items)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 类型: {item.itemType}, 售价: {item.sellPrice}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 物品数据: 加载失败");
        }
    }

    // ==================== 打印方法区域 ====================

    public void PrintAllData()
    {
        Debug.Log(dataLog.ToString());
    }

    // ==================== 岛屿查询方法区域 ====================

    public IslandData GetIslandById(int id)
    {
        foreach (var item in islands)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetIslandName(int id)
    {
        IslandData item = GetIslandById(id);
        return item != null ? item.name : "未知岛屿";
    }

    // ==================== 稀有度查询方法区域 ====================

    public RarityData GetRarityById(int id)
    {
        foreach (var item in rarities)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetRarityName(int id)
    {
        RarityData item = GetRarityById(id);
        return item != null ? item.name : "未知稀有度";
    }

    public string GetRarityColorCode(int id)
    {
        RarityData item = GetRarityById(id);
        return item != null ? item.colorCode : "#FFFFFF";
    }

    public int GetRarityWeight(int id)
    {
        RarityData item = GetRarityById(id);
        return item != null ? item.weight : 0;
    }

    public int GetRarityExp(int id)
    {
        RarityData item = GetRarityById(id);
        return item != null ? item.exp : 1;
    }

    // ==================== 鱼饵查询方法区域 ====================

    public BaitData GetBaitById(int id)
    {
        foreach (var item in baits)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetBaitName(int id)
    {
        BaitData item = GetBaitById(id);
        return item != null ? item.name : "未知鱼饵";
    }

    public int GetBaitWeight(int id)
    {
        BaitData item = GetBaitById(id);
        return item != null ? item.baseWeight : 100;
    }

    // ==================== 时段查询方法区域 ====================

    public TimeSlotData GetTimeSlotById(int id)
    {
        foreach (var item in timeSlots)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetTimeSlotName(int id)
    {
        TimeSlotData item = GetTimeSlotById(id);
        return item != null ? item.name : "未知时段";
    }

    // ==================== 天气查询方法区域 ====================

    public WeatherData GetWeatherById(int id)
    {
        foreach (var item in weathers)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetWeatherName(int id)
    {
        WeatherData item = GetWeatherById(id);
        return item != null ? item.name : "未知天气";
    }

    public int GetWeatherWeight(int id)
    {
        WeatherData item = GetWeatherById(id);
        return item != null ? item.weight : 100;
    }

    // ==================== 重量星级查询方法区域 ====================

    public StarRatingData GetStarRatingById(int id)
    {
        foreach (var item in starRatings)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetStarRatingName(int id)
    {
        StarRatingData item = GetStarRatingById(id);
        return item != null ? item.name : "未知星级";
    }

    public float GetStarRatingMultiplier(int id)
    {
        StarRatingData item = GetStarRatingById(id);
        return item != null ? item.multiplier : 1.0f;
    }

    public string GetStarRatingColor(int id)
    {
        StarRatingData item = GetStarRatingById(id);
        return item != null ? item.color : "#FFFFFF";
    }

    public List<StarRatingData> GetSortedStarRatings()
    {
        var sorted = new List<StarRatingData>(starRatings);
        sorted.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return sorted;
    }

    public StarRatingData GetStarRatingByWeight(float weightRatio)
    {
        var sortedRatings = GetSortedStarRatings();
        if (sortedRatings.Count == 0) return null;

        float prevMultiplier = 0.5f;
        foreach (var rating in sortedRatings)
        {
            if (weightRatio >= prevMultiplier && weightRatio <= rating.multiplier)
            {
                return rating;
            }
            prevMultiplier = rating.multiplier;
        }

        return sortedRatings[sortedRatings.Count - 1];
    }

    public float GetStarRatingWeight(int id)
    {
        StarRatingData item = GetStarRatingById(id);
        return item != null ? item.weight : 1.0f;
    }

    // ==================== 鱼类品种查询方法区域 ====================

    public FishSpeciesData GetFishSpeciesById(int id)
    {
        foreach (var item in fishSpecies)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetFishSpeciesName(int id)
    {
        FishSpeciesData item = GetFishSpeciesById(id);
        return item != null ? item.name : "未知品种";
    }

    public string GetFishSpeciesMovementType(int id)
    {
        FishSpeciesData item = GetFishSpeciesById(id);
        return item != null ? item.movementType : "free";
    }

    public string GetFishSpeciesPositionType(int id)
    {
        FishSpeciesData item = GetFishSpeciesById(id);
        return item != null ? item.positionType : "water";
    }

    // ==================== 鱼类查询方法区域 ====================

    public FishData GetFishById(int id)
    {
        foreach (var item in fishes)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetFishName(int id)
    {
        FishData item = GetFishById(id);
        return item != null ? item.name : "未知鱼类";
    }

    public List<FishData> GetFishesByIslandId(int islandId)
    {
        List<FishData> result = new List<FishData>();
        foreach (var fish in fishes)
        {
            if (fish.islandId == 0 || fish.islandId == islandId)
            {
                result.Add(fish);
            }
        }
        return result;
    }

    public List<FishData> GetFishesByPreferredIslandId(int islandId)
    {
        List<FishData> result = new List<FishData>();
        foreach (var fish in fishes)
        {
            if (fish.preferredIslandIds.Contains(islandId))
            {
                result.Add(fish);
            }
        }
        return result;
    }

    // ==================== 背包分类查询方法区域 ====================

    public BagCategoryData GetBagCategoryById(int id)
    {
        foreach (var item in bagCategories)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public BagCategoryData GetBagCategoryByFolderName(string folderName)
    {
        foreach (var item in bagCategories)
        {
            if (item.folderName == folderName) return item;
        }
        return null;
    }

    public List<BagCategoryData> GetBagCategoriesSorted()
    {
        List<BagCategoryData> sorted = new List<BagCategoryData>(bagCategories);
        sorted.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return sorted;
    }

    public string GetCategoryNameByFolderName(string folderName)
    {
        BagCategoryData item = GetBagCategoryByFolderName(folderName);
        return item != null ? item.categoryName : "未知分类";
    }

    // ==================== 物品查询方法区域 ====================

    private void LoadTrashData()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(trashJsonPath);
        if (jsonFile != null)
        {
            TrashListWrapper wrapper = JsonUtility.FromJson<TrashListWrapper>(jsonFile.text);
            if (wrapper != null && wrapper.trashList != null)
            {
                trashList = wrapper.trashList;
                dataLog.AppendLine($"垃圾数据加载成功，共 {trashList.Count} 条");
            }
        }
        else
        {
            dataLog.AppendLine($"垃圾数据文件未找到: {trashJsonPath}");
        }
    }

    public TrashData GetTrashById(int id)
    {
        foreach (var trash in trashList)
        {
            if (trash.id == id) return trash;
        }
        return null;
    }

    public string GetTrashName(int id)
    {
        TrashData trash = GetTrashById(id);
        return trash != null ? trash.name : "未知垃圾";
    }

    // ==================== 能力查询方法区域 ====================

    private void LoadAbilityData()
    {
        string json = RWJsonData.LoadJsonFromResources(abilitiesJsonPath);
        var wrapper = RWJsonData.ParseJson<AbilityListWrapper>(json);
        abilities = (wrapper != null && wrapper.abilities != null) ? wrapper.abilities : new List<AbilityData>();

        if (abilities.Count > 0)
        {
            dataLog.AppendLine($"✓ 钓鱼能力数据: 成功加载 {abilities.Count} 个能力");
            foreach (var item in abilities)
            {
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 类型: {item.abilityType}, 目标稀有度: {item.targetRarityId}");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 钓鱼能力数据: 加载失败");
        }
    }

    private void LoadFishingComponentsData()
    {
        TextAsset textAsset = Resources.Load<TextAsset>(fishingComponentsJsonPath);
        if (textAsset != null)
        {
            FishingComponentConfigArray arrayWrapper = JsonUtility.FromJson<FishingComponentConfigArray>(textAsset.text);
            if (arrayWrapper != null && arrayWrapper.items != null)
            {
                fishingComponents = new List<FishingComponentConfig>(arrayWrapper.items);

                if (fishingComponents.Count > 0)
                {
                    dataLog.AppendLine($"✓ 钓鱼组件数据: 成功加载 {fishingComponents.Count} 个组件");
                    foreach (var item in fishingComponents)
                    {
                        dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 类别: {item.category}");
                    }
                }
                else
                {
                    dataLog.AppendLine($"✗ 钓鱼组件数据: 加载失败");
                }
            }
            else
            {
                dataLog.AppendLine($"✗ 钓鱼组件数据: 解析失败");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 钓鱼组件数据: 加载失败，文件不存在");
        }
    }

    private void LoadCharactersData()
    {
        TextAsset textAsset = Resources.Load<TextAsset>(charactersJsonPath);
        if (textAsset != null)
        {
            CharacterConfigList listWrapper = JsonUtility.FromJson<CharacterConfigList>(textAsset.text);
            if (listWrapper != null && listWrapper.characters != null)
            {
                characters = listWrapper.characters;

                if (characters.Count > 0)
                {
                    dataLog.AppendLine($"✓ 人物数据: 成功加载 {characters.Count} 个人物");
                    foreach (var item in characters)
                    {
                        dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}");
                    }
                }
                else
                {
                    dataLog.AppendLine($"✗ 人物数据: 加载失败");
                }
            }
            else
            {
                dataLog.AppendLine($"✗ 人物数据: 解析失败");
            }
        }
        else
        {
            dataLog.AppendLine($"✗ 人物数据: 加载失败，文件不存在");
        }
    }

    public AbilityData GetAbilityById(int id)
    {
        foreach (var item in abilities)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public List<AbilityData> GetAbilitiesByType(string abilityType)
    {
        List<AbilityData> result = new List<AbilityData>();
        foreach (var item in abilities)
        {
            if (item.abilityType == abilityType)
            {
                result.Add(item);
            }
        }
        return result;
    }

    public List<AbilityData> GetAbilitiesByRarity(int rarityId)
    {
        List<AbilityData> result = new List<AbilityData>();
        foreach (var item in abilities)
        {
            if (item.targetRarityId == rarityId)
            {
                result.Add(item);
            }
        }
        return result;
    }

    // ==================== 物品查询方法区域 ====================

    public ItemData GetItemById(int id)
    {
        foreach (var item in items)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetItemName(int id)
    {
        ItemData item = GetItemById(id);
        return item != null ? item.name : "未知物品";
    }

    public FishingComponentConfig GetComponentById(int id)
    {
        foreach (var item in fishingComponents)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public CharacterConfig GetCharacterConfig(int id)
    {
        foreach (var item in characters)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    public string GetComponentName(int id)
    {
        FishingComponentConfig component = GetComponentById(id);
        if (component != null)
        {
            return component.name;
        }

        foreach (var character in characters)
        {
            if (character.id == id)
            {
                return character.name;
            }
        }

        return "未知组件";
    }

    public string GetItemIconPath(int id)
    {
        ItemData item = GetItemById(id);
        return item != null ? item.iconPath : "";
    }

    public List<ItemData> GetItemsByType(int itemType)
    {
        List<ItemData> result = new List<ItemData>();
        foreach (var item in items)
        {
            if (item.itemType == itemType)
            {
                result.Add(item);
            }
        }
        return result;
    }

    // ==================== 重载方法区域 ====================

    public void ReloadData()
    {
        LoadAllData();
        PrintAllData();
    }

    // ==================== 通信方法 ====================

    public void HandleBagOpenEvent()
    {
        Debug.Log("[LoadDataManager] 接收到背包打开事件");

        // 检查数据加载状态
        if (!isDataLoaded)
        {
            Debug.Log("[LoadDataManager] 数据未加载，开始加载数据");
            LoadAllData();
        }

        if (UIManager.Instance != null && UIManager.Instance.bagView != null)
        {
            UIManager.Instance.bagView.InitBag();
        }
    }

    public void HandleFishBagOpenEvent()
    {
        Debug.Log("[LoadDataManager] 接收到鱼篓打开事件");

        // 检查数据加载状态
        if (!isDataLoaded)
        {
            Debug.Log("[LoadDataManager] 数据未加载，开始加载数据");
            LoadAllData();
        }

        if (UIManager.Instance != null && UIManager.Instance.fishBagView != null)
        {
            UIManager.Instance.fishBagView.InitFishBag();
        }
    }

    public void HandleBagInitEvent()
    {
        Debug.Log("[LoadDataManager] 接收到背包初始化事件");

        // 检查数据加载状态
        if (!isDataLoaded)
        {
            Debug.Log("[LoadDataManager] 数据未加载，开始加载数据");
            LoadAllData();
        }

        // 初始化背包数据
        if (UIManager.Instance != null && UIManager.Instance.bagView != null && PlayerDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            var itemDataMap = GetItemDataMap();
            UIManager.Instance.bagView.UpdateBagItems(inventory, itemDataMap);
        }
    }

    public void HandleBagRefreshItemsEvent()
    {
        Debug.Log("[LoadDataManager] 接收到背包刷新事件");

        // 检查数据加载状态
        if (!isDataLoaded)
        {
            Debug.Log("[LoadDataManager] 数据未加载，开始加载数据");
            LoadAllData();
        }

        // 刷新背包数据
        if (UIManager.Instance != null && UIManager.Instance.bagView != null && PlayerDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            var itemDataMap = GetItemDataMap();
            UIManager.Instance.bagView.UpdateBagItems(inventory, itemDataMap);
        }
    }

    public void HandleFishBagInitEvent()
    {
        Debug.Log("[LoadDataManager] 接收到鱼篓初始化事件");

        // 检查数据加载状态
        if (!isDataLoaded)
        {
            Debug.Log("[LoadDataManager] 数据未加载，开始加载数据");
            LoadAllData();
        }

        // 初始化鱼篓数据
        if (UIManager.Instance != null && UIManager.Instance.fishBagView != null && PlayerDataManager.Instance != null)
        {
            var fishInventory = PlayerDataManager.Instance.GetFishInventory();
            var itemDataMap = GetItemDataMap();
            UIManager.Instance.fishBagView.UpdateFishItems(fishInventory, itemDataMap);
        }
    }

    public void HandleFishBagRefreshItemsEvent()
    {
        Debug.Log("[LoadDataManager] 接收到鱼篓刷新事件");

        // 检查数据加载状态
        if (!isDataLoaded)
        {
            Debug.Log("[LoadDataManager] 数据未加载，开始加载数据");
            LoadAllData();
        }

        // 刷新鱼篓数据
        if (UIManager.Instance != null && UIManager.Instance.fishBagView != null && PlayerDataManager.Instance != null)
        {
            var fishInventory = PlayerDataManager.Instance.GetFishInventory();
            var itemDataMap = GetItemDataMap();
            var fishDetailData = PlayerDataManager.Instance.GetFishDetailData();

            Debug.Log($"[LoadDataManager] 鱼篓数据: {fishInventory.Count} 种鱼");
            foreach (var item in fishInventory)
            {
                Debug.Log($"  鱼ID: {item.Key}, 数量: {item.Value}");
            }

            UIManager.Instance.fishBagView.UpdateFishBagWithInventory(fishInventory, itemDataMap, fishDetailData);
        }
        else
        {
            Debug.LogWarning("[LoadDataManager] 鱼篓刷新失败: UIManager或PlayerDataManager未初始化");
        }
    }

    public Dictionary<int, ItemData> GetItemDataMap()
    {
        Dictionary<int, ItemData> itemDataMap = new Dictionary<int, ItemData>();
        
        foreach (ItemData itemData in items)
        {
            itemDataMap[itemData.id] = itemData;
        }
        
        foreach (BaitData bait in baits)
        {
            if (!itemDataMap.ContainsKey(bait.id))
            {
                ItemData itemData = new ItemData
                {
                    id = bait.id,
                    name = bait.name,
                    description = bait.description,
                    sellPrice = 0,
                    buyPrice = 0,
                    itemType = 4,
                    categoryId = 2,
                    iconPath = ""
                };
                itemDataMap[bait.id] = itemData;
            }
        }
        
        return itemDataMap;
    }

    [System.Serializable]
    private class TrashListWrapper
    {
        public List<TrashData> trashList;
    }
}