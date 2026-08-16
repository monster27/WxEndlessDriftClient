using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Threading.Tasks;

public class LoadDataManager : SingletonMono<LoadDataManager>
{
    [Header("JSON文件路径")]
    private string islandsJsonPath = "JsonData/BaseFramework/islands";
    private string raritiesJsonPath = "JsonData/BaseFramework/rarities";
    private string baitsJsonPath = "JsonData/Game/BagItem/baits";
    private string nestBaitsJsonPath = "JsonData/Game/BagItem/nestBaits";
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
    private string uiTextsJsonPath = "JsonData/Game/GameFramework/uiTexts";
    private string sceneDataPath = "JsonData/Game/SceneTransData/mainTransData";

    // 数据存储
    public List<IslandData> islands = new List<IslandData>();
    public List<RarityData> rarities = new List<RarityData>();
    public List<BaitData> baits = new List<BaitData>();
    public Dictionary<int, NestBaitData> nestBaitDict = new Dictionary<int, NestBaitData>();
    public NestBaitConstants nestBaitConstants = new NestBaitConstants();
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
    public UITextsConfig uiTextsConfig;
    public SceneDataWrapper sceneDataWrapper = new SceneDataWrapper();
    public bool isSceneDataLoaded = false;

    private StringBuilder dataLog = new StringBuilder();
    public bool isDataLoaded = false;
    public System.Action onDataLoaded;
    private Dictionary<int, ItemData> _cachedItemDataMap;

    protected override void Awake()
    {
        base.Awake();
    }

    public void Init()
    {
        LoadAllData();
        PrintAllData();
        RegisterEvents();
        DontDestroyOnLoad(gameObject);
    }

    private void RegisterEvents()
    {
        CommunicateEvent.Register("Bag_Open", HandleBagOpenEvent);
        CommunicateEvent.Register("Bag_Init", HandleBagInitEvent);
        CommunicateEvent.Register("Bag_RefreshItems", HandleBagRefreshItemsEvent);
        CommunicateEvent.Register("FishBag_Open", HandleFishBagOpenEvent);
        CommunicateEvent.Register("FishBag_Init", HandleFishBagInitEvent);
        CommunicateEvent.Register("FishBag_RefreshItems", HandleFishBagRefreshItemsEvent);
    }

    // ==================== 数据加载 ====================

    public async void LoadAllData()
    {
        dataLog.Clear();
        dataLog.AppendLine("========== 数据加载日志 ==========");

        await LoadIslandData();
        await LoadRarityData();
        await LoadBaitData();
        await LoadNestBaitData();
        await LoadTimeSlotData();
        await LoadWeatherData();
        await LoadStarRatingData();
        await LoadFishSpeciesData();
        await LoadFishData();
        await LoadBagCategoryData();
        await LoadItemData();
        await LoadTrashData();
        await LoadAbilityData();
        await LoadFishingComponentsData();
        await LoadCharactersData();
        await LoadSceneData();
        await LoadUITextsData();

        dataLog.AppendLine("===================================");
        isDataLoaded = true;

        if (onDataLoaded != null)
        {
            Debug.Log("[LoadDataManager] 触发数据加载完成事件");
            onDataLoaded();
        }
    }

    private async Task LoadIslandData()
    {
        string json = await RWJsonData.LoadJson(islandsJsonPath);
        var wrapper = RWJsonData.ParseJson<IslandListWrapper>(json);
        islands = (wrapper != null && wrapper.islands != null) ? wrapper.islands : new List<IslandData>();
        if (islands.Count > 0)
        {
            dataLog.AppendLine($"✓ 岛屿数据: 成功加载 {islands.Count} 个岛屿");
            foreach (var item in islands)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}");
        }
        else dataLog.AppendLine($"✗ 岛屿数据: 加载失败");
    }

    private async Task LoadRarityData()
    {
        string json = await RWJsonData.LoadJson(raritiesJsonPath);
        var wrapper = RWJsonData.ParseJson<RarityListWrapper>(json);
        rarities = (wrapper != null && wrapper.rarities != null) ? wrapper.rarities : new List<RarityData>();
        if (rarities.Count > 0)
        {
            dataLog.AppendLine($"✓ 稀有度数据: 成功加载 {rarities.Count} 个稀有度");
            foreach (var item in rarities)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 颜色: {item.color}, 权重: {item.weight}, 经验: {item.exp}");
        }
        else dataLog.AppendLine($"✗ 稀有度数据: 加载失败");
    }

    private async Task LoadBaitData()
    {
        string json = await RWJsonData.LoadJson(baitsJsonPath);
        var wrapper = RWJsonData.ParseJson<BaitListWrapper>(json);
        baits = (wrapper != null && wrapper.baits != null) ? new List<BaitData>(wrapper.baits) : new List<BaitData>();
        if (baits.Count > 0)
        {
            dataLog.AppendLine($"✓ 鱼饵数据: 成功加载 {baits.Count} 个鱼饵");
            foreach (var item in baits)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 权重: {item.baseWeight}, 解锁场景: {item.unlockScene}");
        }
        else dataLog.AppendLine($"✗ 鱼饵数据: 加载失败");
    }

    private async Task LoadNestBaitData()
    {
        string json = await RWJsonData.LoadJson(nestBaitsJsonPath);
        var wrapper = RWJsonData.ParseJson<NestBaitListWrapper>(json);
        nestBaitDict.Clear();
        nestBaitConstants = new NestBaitConstants();

        if (wrapper != null)
        {
            if (wrapper.constants != null)
            {
                nestBaitConstants = wrapper.constants;
                dataLog.AppendLine($"✓ 窝料常量: defaultBaitItemId={nestBaitConstants.defaultBaitItemId}, continuousModeAddTime={nestBaitConstants.continuousModeAddTime}, continuousModeMaxTime={nestBaitConstants.continuousModeMaxTime}");
            }
            if (wrapper.nestBaits != null)
            {
                foreach (var item in wrapper.nestBaits)
                    nestBaitDict[item.id] = item;
                dataLog.AppendLine($"✓ 窝料数据: 成功加载 {nestBaitDict.Count} 个窝料");
                foreach (var item in nestBaitDict.Values)
                    dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 适用场景: {item.applicableScene}");
            }
        }
        else dataLog.AppendLine($"✗ 窝料数据: 加载失败");
    }

    private async Task LoadTimeSlotData()
    {
        string json = await RWJsonData.LoadJson(timeSlotsJsonPath);
        var wrapper = RWJsonData.ParseJson<TimeSlotListWrapper>(json);
        timeSlots = (wrapper != null && wrapper.timeSlots != null) ? wrapper.timeSlots : new List<TimeSlotData>();
        if (timeSlots.Count > 0)
        {
            dataLog.AppendLine($"✓ 时段数据: 成功加载 {timeSlots.Count} 个时段");
            foreach (var item in timeSlots)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 时长: {item.durationMinutes}分钟");
        }
        else dataLog.AppendLine($"✗ 时段数据: 加载失败");
    }

    private async Task LoadWeatherData()
    {
        string json = await RWJsonData.LoadJson(weathersJsonPath);
        var wrapper = RWJsonData.ParseJson<WeatherListWrapper>(json);
        weathers = (wrapper != null && wrapper.weathers != null) ? wrapper.weathers : new List<WeatherData>();
        if (weathers.Count > 0)
        {
            dataLog.AppendLine($"✓ 天气数据: 成功加载 {weathers.Count} 个天气");
            foreach (var item in weathers)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 概率: {item.percentage}%, 权重: {item.weight}");
        }
        else dataLog.AppendLine($"✗ 天气数据: 加载失败");
    }

    private async Task LoadStarRatingData()
    {
        string json = await RWJsonData.LoadJson(starRatingsJsonPath);
        var wrapper = RWJsonData.ParseJson<StarRatingListWrapper>(json);
        starRatings = (wrapper != null && wrapper.starRatings != null) ? wrapper.starRatings : new List<StarRatingData>();
        if (starRatings.Count > 0)
        {
            dataLog.AppendLine($"✓ 星级倍数数据: 成功加载 {starRatings.Count} 个星级");
            foreach (var item in starRatings)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 倍率: {item.multiplier}, 颜色: {item.color}");
        }
        else dataLog.AppendLine($"✗ 星级倍数数据: 加载失败");
    }

    private async Task LoadFishSpeciesData()
    {
        string json = await RWJsonData.LoadJson(fishSpeciesJsonPath);
        var wrapper = RWJsonData.ParseJson<FishSpeciesListWrapper>(json);
        fishSpecies = (wrapper != null && wrapper.fishSpecies != null) ? wrapper.fishSpecies : new List<FishSpeciesData>();
        if (fishSpecies.Count > 0)
        {
            dataLog.AppendLine($"✓ 鱼类品种数据: 成功加载 {fishSpecies.Count} 个品种");
            foreach (var item in fishSpecies)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 类型: {item.type}");
        }
        else dataLog.AppendLine($"✗ 鱼类品种数据: 加载失败");
    }

    private async Task LoadFishData()
    {
        string json = await RWJsonData.LoadJson(fishesJsonPath);
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
        else dataLog.AppendLine($"✗ 鱼类数据: 加载失败");
    }

    private async Task LoadBagCategoryData()
    {
        bagCategories.Clear();
        string json = await RWJsonData.LoadJson(itemCategoriesJsonPath);
        var wrapper = RWJsonData.ParseJson<ItemCategoryListWrapper>(json);
        if (wrapper != null && wrapper.categories != null)
        {
            foreach (var category in wrapper.categories)
            {
                bagCategories.Add(new BagCategoryData
                {
                    id = category.id,
                    folderName = category.code,
                    categoryName = category.name,
                    sortOrder = category.id
                });
                if (category.subCategories != null)
                {
                    foreach (var subCat in category.subCategories)
                    {
                        bagCategories.Add(new BagCategoryData
                        {
                            id = subCat.id,
                            folderName = category.code,
                            categoryName = subCat.name,
                            sortOrder = subCat.id
                        });
                    }
                }
            }
        }
        if (bagCategories.Count > 0)
        {
            dataLog.AppendLine($"✓ 背包分类数据: 成功加载 {bagCategories.Count} 个分类");
            foreach (var item in bagCategories)
                dataLog.AppendLine($"    - ID: {item.id}, 文件夹: {item.folderName}, 分类名称: {item.categoryName}, 排序: {item.sortOrder}");
        }
        else dataLog.AppendLine($"✗ 背包分类数据: 加载失败");
    }

    private async Task LoadItemData()
    {
        string json = await RWJsonData.LoadJson(itemsJsonPath);
        var wrapper = RWJsonData.ParseJson<ItemListWrapper>(json);
        items = (wrapper != null && wrapper.items != null) ? wrapper.items : new List<ItemData>();
        _cachedItemDataMap = null;
        if (items.Count > 0)
        {
            dataLog.AppendLine($"✓ 物品数据: 成功加载 {items.Count} 个物品");
            foreach (var item in items)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 类型: {item.itemType}, 售价: {item.sellPrice}, 唯一: {item.isUnique}");
        }
        else dataLog.AppendLine($"✗ 物品数据: 加载失败");
    }

    private async Task LoadTrashData()
    {
        string json = await RWJsonData.LoadJson(trashJsonPath);
        if (!string.IsNullOrEmpty(json))
        {
            var wrapper = JsonUtility.FromJson<TrashListWrapper>(json);
            if (wrapper != null && wrapper.trashList != null)
            {
                trashList = wrapper.trashList;
                dataLog.AppendLine($"垃圾数据加载成功，共 {trashList.Count} 条");
                return;
            }
        }
        dataLog.AppendLine($"垃圾数据文件未找到: {trashJsonPath}");
    }

    private async Task LoadAbilityData()
    {
        string json = await RWJsonData.LoadJson(abilitiesJsonPath);
        var wrapper = RWJsonData.ParseJson<AbilityListWrapper>(json);
        abilities = (wrapper != null && wrapper.abilities != null) ? wrapper.abilities : new List<AbilityData>();
        if (abilities.Count > 0)
        {
            dataLog.AppendLine($"✓ 钓鱼能力数据: 成功加载 {abilities.Count} 个能力");
            foreach (var item in abilities)
                dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 类型: {item.abilityType}, 目标稀有度: {item.targetRarityId}");
        }
        else dataLog.AppendLine($"✗ 钓鱼能力数据: 加载失败");
    }

    private async Task LoadFishingComponentsData()
    {
        string json = await RWJsonData.LoadJson(fishingComponentsJsonPath);
        if (!string.IsNullOrEmpty(json))
        {
            var arrayWrapper = JsonUtility.FromJson<FishingComponentConfigArray>(json);
            if (arrayWrapper != null && arrayWrapper.items != null)
            {
                fishingComponents = new List<FishingComponentConfig>(arrayWrapper.items);
                dataLog.AppendLine($"✓ 钓鱼组件数据: 成功加载 {fishingComponents.Count} 个组件");
                foreach (var item in fishingComponents)
                    dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}, 类别: {item.category}");
                return;
            }
        }
        dataLog.AppendLine($"✗ 钓鱼组件数据: 加载失败");
    }

    private async Task LoadCharactersData()
    {
        string json = await RWJsonData.LoadJson(charactersJsonPath);
        if (!string.IsNullOrEmpty(json))
        {
            var listWrapper = JsonUtility.FromJson<CharacterConfigList>(json);
            if (listWrapper != null && listWrapper.characters != null)
            {
                characters = listWrapper.characters;
                dataLog.AppendLine($"✓ 人物数据: 成功加载 {characters.Count} 个人物");
                foreach (var item in characters)
                    dataLog.AppendLine($"    - ID: {item.id}, 名称: {item.name}");
                return;
            }
        }
        dataLog.AppendLine($"✗ 人物数据: 加载失败");
    }

    private async Task LoadUITextsData()
    {
        string json = await RWJsonData.LoadJson(uiTextsJsonPath);
        if (!string.IsNullOrEmpty(json))
        {
            uiTextsConfig = JsonUtility.FromJson<UITextsConfig>(json);
            if (uiTextsConfig != null)
            {
                dataLog.AppendLine($"✓ UI文本配置: 成功加载");
                return;
            }
        }
        dataLog.AppendLine($"✗ UI文本配置: 加载失败");
    }

    private async Task LoadSceneData()
    {
        try
        {
            string json = await RWJsonData.LoadJson(sceneDataPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning($"[LoadDataManager] 无法加载场景数据文件: {sceneDataPath}，创建空数据");
                sceneDataWrapper = new SceneDataWrapper();
                isSceneDataLoaded = true;
                return;
            }
            sceneDataWrapper = RWJsonData.ParseJson<SceneDataWrapper>(json);
            if (sceneDataWrapper == null || sceneDataWrapper.scenes == null)
            {
                Debug.LogWarning("[LoadDataManager] 场景数据解析失败，创建空数据");
                sceneDataWrapper = new SceneDataWrapper();
            }
            isSceneDataLoaded = true;
            Debug.Log($"[LoadDataManager] 加载场景数据完成，共 {sceneDataWrapper.scenes.Count} 个场景");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LoadDataManager] 加载场景数据异常: {e.Message}");
            sceneDataWrapper = new SceneDataWrapper();
            isSceneDataLoaded = true;
        }
    }

    // ==================== 查询方法 ====================

    public void PrintAllData() => Debug.Log(dataLog.ToString());

    public IslandData GetIslandById(int id)
    {
        foreach (var item in islands) if (item.id == id) return item;
        return null;
    }
    public string GetIslandName(int id) => GetIslandById(id)?.name ?? "未知岛屿";

    public RarityData GetRarityById(int id)
    {
        foreach (var item in rarities) if (item.id == id) return item;
        return null;
    }
    public string GetRarityName(int id) => GetRarityById(id)?.name ?? "未知稀有度";
    public string GetRarityColorCode(int id) => GetRarityById(id)?.colorCode ?? "#FFFFFF";
    public int GetRarityWeight(int id) => GetRarityById(id)?.weight ?? 0;
    public int GetRarityExp(int id) => GetRarityById(id)?.exp ?? 1;

    public BaitData GetBaitById(int id)
    {
        foreach (var item in baits) if (item.id == id) return item;
        return null;
    }
    public string GetBaitName(int id) => GetBaitById(id)?.name ?? "未知鱼饵";
    public int GetBaitWeight(int id) => GetBaitById(id)?.baseWeight ?? 100;

    public TimeSlotData GetTimeSlotById(int id)
    {
        foreach (var item in timeSlots) if (item.id == id) return item;
        return null;
    }
    public string GetTimeSlotName(int id) => GetTimeSlotById(id)?.name ?? "未知时段";

    public WeatherData GetWeatherById(int id)
    {
        foreach (var item in weathers) if (item.id == id) return item;
        return null;
    }
    public string GetWeatherName(int id) => GetWeatherById(id)?.name ?? "未知天气";
    public int GetWeatherWeight(int id) => GetWeatherById(id)?.weight ?? 100;

    public StarRatingData GetStarRatingById(int id)
    {
        foreach (var item in starRatings) if (item.id == id) return item;
        return null;
    }
    public string GetStarRatingName(int id) => GetStarRatingById(id)?.name ?? "未知星级";
    public float GetStarRatingMultiplier(int id) => GetStarRatingById(id)?.multiplier ?? 1.0f;
    public string GetStarRatingColor(int id) => GetStarRatingById(id)?.color ?? "#FFFFFF";

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
                return rating;
            prevMultiplier = rating.multiplier;
        }
        return sortedRatings[sortedRatings.Count - 1];
    }
    public float GetStarRatingWeight(int id) => GetStarRatingById(id)?.weight ?? 1.0f;

    public FishSpeciesData GetFishSpeciesById(int id)
    {
        foreach (var item in fishSpecies) if (item.id == id) return item;
        return null;
    }
    public FishSpeciesData GetFishSpeciesByType(string type)
    {
        foreach (var item in fishSpecies) if (item.type == type) return item;
        return null;
    }
    public string GetFishSpeciesName(int id) => GetFishSpeciesById(id)?.name ?? "未知品种";
    public FishSpeciesType GetFishSpeciesType(int id)
    {
        if (System.Enum.IsDefined(typeof(FishSpeciesType), id)) return (FishSpeciesType)id;
        return FishSpeciesType.FullScreenSwim;
    }
    public FishSpeciesType GetFishSpeciesType(string type)
    {
        switch (type)
        {
            case "FullScreenSwim": return FishSpeciesType.FullScreenSwim;
            case "FullScreenStatic": return FishSpeciesType.FullScreenStatic;
            case "BottomSwim": return FishSpeciesType.BottomSwim;
            case "BottomStatic": return FishSpeciesType.BottomStatic;
            default: return FishSpeciesType.FullScreenSwim;
        }
    }

    public FishData GetFishById(int id)
    {
        foreach (var item in fishes) if (item.id == id) return item;
        return null;
    }
    public string GetFishName(int id) => GetFishById(id)?.name ?? "未知鱼类";
    public List<FishData> GetFishesByIslandId(int islandId)
    {
        var result = new List<FishData>();
        foreach (var fish in fishes)
            if (fish.islandId == 0 || fish.islandId == islandId) result.Add(fish);
        return result;
    }
    public List<FishData> GetFishesByPreferredIslandId(int islandId)
    {
        var result = new List<FishData>();
        foreach (var fish in fishes)
            if (fish.preferredIslandIds.Contains(islandId)) result.Add(fish);
        return result;
    }

    public BagCategoryData GetBagCategoryById(int id)
    {
        foreach (var item in bagCategories) if (item.id == id) return item;
        return null;
    }
    public BagCategoryData GetBagCategoryByFolderName(string folderName)
    {
        foreach (var item in bagCategories) if (item.folderName == folderName) return item;
        return null;
    }
    public List<BagCategoryData> GetBagCategoriesSorted()
    {
        var sorted = new List<BagCategoryData>(bagCategories);
        sorted.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return sorted;
    }
    public string GetCategoryNameByFolderName(string folderName) => GetBagCategoryByFolderName(folderName)?.categoryName ?? "未知分类";
    public string GetSubCategoryNameById(int subCategoryId)
    {
        foreach (var category in bagCategories)
            if (category.id == subCategoryId) return category.categoryName;
        return "未知分类";
    }

    public TrashData GetTrashById(int id)
    {
        foreach (var trash in trashList) if (trash.id == id) return trash;
        return null;
    }
    public string GetTrashName(int id) => GetTrashById(id)?.name ?? "未知垃圾";

    public AbilityData GetAbilityById(int id)
    {
        foreach (var item in abilities) if (item.id == id) return item;
        return null;
    }
    public List<AbilityData> GetAbilitiesByType(string abilityType)
    {
        var result = new List<AbilityData>();
        foreach (var item in abilities)
            if (item.abilityType == abilityType) result.Add(item);
        return result;
    }
    public List<AbilityData> GetAbilitiesByRarity(int rarityId)
    {
        var result = new List<AbilityData>();
        foreach (var item in abilities)
            if (item.targetRarityId == rarityId) result.Add(item);
        return result;
    }

    public ItemData GetItemById(int id)
    {
        foreach (var item in items) if (item.id == id) return item;
        return null;
    }
    public string GetItemName(int id) => GetItemById(id)?.name ?? "未知物品";
    public FishingComponentConfig GetComponentById(int id)
    {
        foreach (var item in fishingComponents) if (item.id == id) return item;
        return null;
    }
    public CharacterConfig GetCharacterConfig(int id)
    {
        foreach (var item in characters) if (item.id == id) return item;
        return null;
    }
    public string GetComponentName(int id)
    {
        var component = GetComponentById(id);
        if (component != null) return component.name;
        foreach (var character in characters)
            if (character.id == id) return character.name;
        return "未知组件";
    }
    public string GetItemIconPath(int id) => GetItemById(id)?.iconPath ?? "";
    public List<ItemData> GetItemsByType(int itemType)
    {
        var result = new List<ItemData>();
        foreach (var item in items)
            if (item.itemType == itemType) result.Add(item);
        return result;
    }

    public string GetEquipmentUIText(string key)
    {
        if (uiTextsConfig == null || uiTextsConfig.equipment == null) return key;
        switch (key)
        {
            case "notEquipped": return uiTextsConfig.equipment.notEquipped;
            case "equipped": return uiTextsConfig.equipment.equipped;
            case "equipSuccess": return uiTextsConfig.equipment.equipSuccess;
            case "equipFailed": return uiTextsConfig.equipment.equipFailed;
            case "maxLevel": return uiTextsConfig.equipment.maxLevel;
            case "upgradeSuccess": return uiTextsConfig.equipment.upgradeSuccess;
            case "upgradeFailed": return uiTextsConfig.equipment.upgradeFailed;
            case "notEnoughGold": return uiTextsConfig.equipment.notEnoughGold;
            case "currentLevel": return uiTextsConfig.equipment.currentLevel;
            case "nextLevelEffect": return uiTextsConfig.equipment.nextLevelEffect;
            default: return key;
        }
    }

    public SceneData GetSceneData(string sceneId)
    {
        if (sceneDataWrapper == null || sceneDataWrapper.scenes == null) return null;
        return sceneDataWrapper.scenes.Find(s => s.sceneId == sceneId);
    }
    public List<SceneData> GetAllSceneData() => sceneDataWrapper?.scenes;

    public void SaveSceneData(SceneDataWrapper data)
    {
        if (data == null) return;
        sceneDataWrapper = data;
        isSceneDataLoaded = true;
#if UNITY_EDITOR
        SaveSceneDataToFile();
#endif
    }

#if UNITY_EDITOR
    public void SaveSceneDataToFile()
    {
        if (sceneDataWrapper == null) return;
        string json = JsonUtility.ToJson(sceneDataWrapper, true);
        string fullPath = System.IO.Path.Combine(Application.dataPath, "Resources", sceneDataPath + ".json");
        string directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(fullPath, json);
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log($"[LoadDataManager] 场景数据已保存到: {fullPath}");
    }
#endif

    public Dictionary<int, ItemData> GetItemDataMap()
    {
        if (_cachedItemDataMap != null && _cachedItemDataMap.Count == items.Count)
            return _cachedItemDataMap;
        _cachedItemDataMap = new Dictionary<int, ItemData>();
        foreach (ItemData itemData in items)
            _cachedItemDataMap[itemData.id] = itemData;
        return _cachedItemDataMap;
    }

    public void ReloadData()
    {
        LoadAllData();
        PrintAllData();
    }

    // ==================== 事件处理 ====================

    public void HandleBagOpenEvent()
    {
        Debug.Log("[LoadDataManager] 接收到背包打开事件");
        if (!isDataLoaded) LoadAllData();
        if (GameUIManager.Instance != null && GameUIManager.Instance.bagView != null)
            GameUIManager.Instance.bagView.InitBag();
    }

    public void HandleFishBagOpenEvent()
    {
        Debug.Log("[LoadDataManager] 接收到鱼篓打开事件");
        if (!isDataLoaded) LoadAllData();
        if (GameUIManager.Instance != null && GameUIManager.Instance.fishBagView != null)
            GameUIManager.Instance.fishBagView.InitFishBag();
    }

    public void HandleBagInitEvent()
    {
        Debug.Log("[LoadDataManager] 接收到背包初始化事件");
        if (!isDataLoaded) LoadAllData();
        if (GameUIManager.Instance != null && GameUIManager.Instance.bagView != null && PlayerDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            var itemDataMap = GetItemDataMap();
            GameUIManager.Instance.bagView.UpdateBagItems(inventory, itemDataMap);
        }
    }

    public void HandleBagRefreshItemsEvent()
    {
        Debug.Log("[LoadDataManager] 接收到背包刷新事件");
        if (!isDataLoaded) LoadAllData();
        if (GameUIManager.Instance != null && GameUIManager.Instance.bagView != null && PlayerDataManager.Instance != null)
        {
            var inventory = PlayerDataManager.Instance.GetInventory();
            var itemDataMap = GetItemDataMap();
            GameUIManager.Instance.bagView.UpdateBagItems(inventory, itemDataMap);
        }
    }

    public void HandleFishBagInitEvent()
    {
        Debug.Log("[LoadDataManager] 接收到鱼篓初始化事件");
        if (!isDataLoaded) LoadAllData();
        if (GameUIManager.Instance != null && GameUIManager.Instance.fishBagView != null && PlayerDataManager.Instance != null)
        {
            var fishInventory = PlayerDataManager.Instance.GetFishInventory();
            var itemDataMap = GetItemDataMap();
            var fishDetailData = PlayerDataManager.Instance.GetFishDetailData();
            GameUIManager.Instance.fishBagView.UpdateFishItems(fishInventory, itemDataMap, fishDetailData);
        }
    }

    public void HandleFishBagRefreshItemsEvent()
    {
        Debug.Log("[LoadDataManager] 接收到鱼篓刷新事件");
        if (!isDataLoaded) LoadAllData();
        if (GameUIManager.Instance != null && GameUIManager.Instance.fishBagView != null && PlayerDataManager.Instance != null)
        {
            var fishInventory = PlayerDataManager.Instance.GetFishInventory();
            var itemDataMap = GetItemDataMap();
            var fishDetailData = PlayerDataManager.Instance.GetFishDetailData();
            GameUIManager.Instance.fishBagView.UpdateFishItems(fishInventory, itemDataMap, fishDetailData);
        }
        else
        {
            Debug.LogWarning("[LoadDataManager] 鱼篓刷新失败: UIManager或PlayerDataManager未初始化");
        }
    }

    [System.Serializable]
    private class TrashListWrapper
    {
        public List<TrashData> trashList;
    }
}
