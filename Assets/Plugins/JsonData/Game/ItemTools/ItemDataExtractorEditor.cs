#if UNITY_EDITOR
// ==================== ItemDataExtractorEditor.cs ====================
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class ItemDataExtractorEditor : EditorWindow
{
    private string outputPath = "JsonData/Game/Items/items";
    private string fishDataPath = "Addressables/JsonData/Game/BagItem/fishes.json";
    private string baitDataPath = "Addressables/JsonData/Game/BagItem/baits.json";
    private string trashDataPath = "Addressables/JsonData/Game/BagItem/trash.json";
    private string nestBaitDataPath = "Addressables/JsonData/Game/BagItem/nestBaits.json";
    private string indoorSkinDataPath = "Addressables/JsonData/Game/BagItem/indoorSkin.json";
    private string outdoorSkinDataPath = "Addressables/JsonData/Game/BagItem/outdoorSkin.json";
    private string categoryDataPath = "Addressables/JsonData/Game/GameFramework/itemCategories.json";
    private string collectionDataPath = "Addressables/JsonData/BaseFramework/collection.json";
    private string islandInfoDataPath = "Addressables/JsonData/Game/GameFramework/islandInfo.json";  // ✅ 新增：岛屿情报数据路径
    private Vector2 scrollPosition;
    private List<ItemData> extractedItems = new List<ItemData>();
    private List<ItemData> existingItems = new List<ItemData>();
    private CategoryListWrapper categoryWrapper;
    private CollectionWrapper collectionWrapper;
    private IslandInfoListWrapper islandInfoWrapper;  // ✅ 新增：岛屿情报数据包装器

    private bool showFishList = true;
    private bool showBaitList = true;
    private bool showTrashList = true;
    private bool showNestBaitList = true;
    private bool showIndoorSkinList = true;
    private bool showOutdoorSkinList = true;
    private bool showCollectionInfoList = true;
    private bool showIslandInfoList = true;  // ✅ 新增：岛屿情报列表折叠

    [MenuItem("Tools/游戏内容/3.物品通用数据/1.提取物品数据(用于价格)")]
    public static void ShowWindow()
    {
        GetWindow<ItemDataExtractorEditor>("物品数据提取器");
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawInfoSection();
        DrawExtractButton();
        DrawItemLists();

        EditorGUILayout.EndScrollView();
    }

    private void DrawInfoSection()
    {
        EditorGUILayout.LabelField("数据路径配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField($"鱼类数据: {fishDataPath}");
        EditorGUILayout.LabelField($"鱼饵数据: {baitDataPath}");
        EditorGUILayout.LabelField($"垃圾数据: {trashDataPath}");
        EditorGUILayout.LabelField($"窝料数据: {nestBaitDataPath}");
        EditorGUILayout.LabelField($"室内皮肤数据: {indoorSkinDataPath}");
        EditorGUILayout.LabelField($"室外皮肤数据: {outdoorSkinDataPath}");
        EditorGUILayout.LabelField($"图鉴数据: {collectionDataPath}");
        EditorGUILayout.LabelField($"岛屿情报数据: {islandInfoDataPath}");  // ✅ 新增
        EditorGUILayout.LabelField($"输出路径: {outputPath}");

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawExtractButton()
    {
        EditorGUILayout.BeginVertical("box");

        GUI.backgroundColor = new Color(0.7f, 0.9f, 0.7f);
        if (GUILayout.Button("提取物品数据", GUILayout.Height(35)))
        {
            ExtractItems();
        }

        GUI.backgroundColor = new Color(0.9f, 0.7f, 0.7f);
        if (GUILayout.Button("写入物品数据", GUILayout.Height(35)))
        {
            WriteItemsData();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawItemLists()
    {
        if (extractedItems.Count == 0)
        {
            EditorGUILayout.LabelField("暂无数据，请点击\"提取物品数据\"按钮", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        List<ItemData> fishItems = extractedItems.FindAll(item => item.itemType == 1);
        List<ItemData> baitItems = extractedItems.FindAll(item => item.itemType == 2);
        List<ItemData> trashItems = extractedItems.FindAll(item => item.itemType == 3);
        List<ItemData> outdoorSkinItems = extractedItems.FindAll(item => item.itemType == 4);
        List<ItemData> indoorSkinItems = extractedItems.FindAll(item => item.itemType == 5);
        List<ItemData> nestBaitItems = extractedItems.FindAll(item => item.itemType == 6);
        List<ItemData> collectionInfoItems = extractedItems.FindAll(item => item.itemType == 7);
        List<ItemData> islandInfoItems = extractedItems.FindAll(item => item.itemType == 8);  // ✅ 新增：岛屿情报（itemType=8）

        DrawItemGroup("🐟 鱼类数据", fishItems, ref showFishList);
        DrawItemGroup("🎣 鱼饵数据", baitItems, ref showBaitList);
        DrawItemGroup("🗑️ 垃圾数据", trashItems, ref showTrashList);
        DrawItemGroup("🏕️ 室外皮肤数据", outdoorSkinItems, ref showOutdoorSkinList);
        DrawItemGroup("🏠 室内皮肤数据", indoorSkinItems, ref showIndoorSkinList);
        DrawItemGroup("🪣 窝料数据", nestBaitItems, ref showNestBaitList);
        DrawItemGroup("📖 图鉴情报数据", collectionInfoItems, ref showCollectionInfoList);
        DrawItemGroup("🏝️ 岛屿情报数据", islandInfoItems, ref showIslandInfoList);  // ✅ 新增
    }

    private void DrawItemGroup(string title, List<ItemData> items, ref bool isExpanded)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, EditorStyles.foldoutHeader);

        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.6f);
        EditorGUILayout.LabelField($"共 {items.Count} 条", GUILayout.Width(60));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (isExpanded)
        {
            EditorGUI.indentLevel++;

            if (items.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("ID", GUILayout.Width(60));
                EditorGUILayout.LabelField("名称", GUILayout.Width(120));
                EditorGUILayout.LabelField("类型", GUILayout.Width(50));
                EditorGUILayout.LabelField("描述", GUILayout.Width(200));
                EditorGUILayout.EndHorizontal();

                foreach (var item in items)
                {
                    EditorGUILayout.BeginHorizontal("helpBox");
                    EditorGUILayout.LabelField(item.id.ToString(), GUILayout.Width(60));
                    EditorGUILayout.LabelField(item.name, GUILayout.Width(120));
                    EditorGUILayout.LabelField(GetItemTypeName(item.itemType), GUILayout.Width(50));
                    EditorGUILayout.LabelField(item.description.Length > 25 ? item.description.Substring(0, 25) + "..." : item.description, GUILayout.Width(200));
                    EditorGUILayout.EndHorizontal();
                }
            }
            else
            {
                EditorGUILayout.LabelField("无数据");
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private string GetItemTypeName(int itemType)
    {
        switch (itemType)
        {
            case 1: return "鱼类";
            case 2: return "鱼饵";
            case 3: return "垃圾";
            case 4: return "室外皮肤";
            case 5: return "室内皮肤";
            case 6: return "窝料";
            case 7: return "图鉴情报";
            case 8: return "岛屿情报";  // ✅ 新增
            default: return "未知";
        }
    }

    private void ExtractItems()
    {
        extractedItems.Clear();

        LoadExistingItems();
        LoadCategoryData();
        LoadCollectionData();
        LoadIslandInfoData();  // ✅ 新增：加载岛屿情报数据

        List<FishData> fishes = LoadFishData();
        List<BaitData> baits = LoadBaitData();
        List<TrashData> trashList = LoadTrashData();
        List<NestBaitData> nestBaits = LoadNestBaitData();

        if (fishes == null || fishes.Count == 0)
        {
            Debug.LogError("[物品提取] 未找到鱼类数据！");
            return;
        }

        Debug.Log($"[物品提取] 加载完成：鱼类={fishes.Count}，鱼饵={baits?.Count ?? 0}，垃圾={trashList?.Count ?? 0}，窝料={nestBaits?.Count ?? 0}，已存在物品={existingItems.Count}");

        // ========== 处理鱼类 ==========
        foreach (var fish in fishes)
        {
            ItemData existingItem = FindItemById(fish.id);
            ItemData item;

            if (existingItem != null)
            {
                item = existingItem;
                item.name = fish.name;
                item.description = fish.description;
                item.itemType = 1;
                item.categoryId = GetCategoryIdByItemId(fish.id);
                item.iconPath = $"UI/Icon/FishIcons/{fish.id}";
            }
            else
            {
                item = new ItemData
                {
                    id = fish.id,
                    name = fish.name,
                    description = fish.description,
                    sellPrice = -1,
                    buyPrice = -1,
                    itemType = 1,
                    categoryId = GetCategoryIdByItemId(fish.id),
                    iconPath = $"UI/Icon/FishIcons/{fish.id}"
                };
            }
            extractedItems.Add(item);
        }

        // ========== 处理鱼饵 ==========
        if (baits != null)
        {
            foreach (var bait in baits)
            {
                ItemData existingItem = FindItemById(bait.id);
                ItemData item;

                if (existingItem != null)
                {
                    item = existingItem;
                    item.name = bait.name;
                    item.description = bait.description;
                    item.itemType = 2;
                    item.categoryId = GetCategoryIdByItemId(bait.id);
                    item.iconPath = $"UI/Icon/BaitIcons/{bait.id}";
                }
                else
                {
                    item = new ItemData
                    {
                        id = bait.id,
                        name = bait.name,
                        description = bait.description,
                        sellPrice = -1,
                        buyPrice = -1,
                        itemType = 2,
                        categoryId = GetCategoryIdByItemId(bait.id),
                        iconPath = $"UI/Icon/BaitIcons/{bait.id}"
                    };
                }
                extractedItems.Add(item);
            }
        }

        // ========== 处理垃圾 ==========
        if (trashList != null)
        {
            foreach (var trash in trashList)
            {
                ItemData existingItem = FindItemById(trash.id);
                ItemData item;

                if (existingItem != null)
                {
                    item = existingItem;
                    item.name = trash.name;
                    item.description = "垃圾物品，没有特殊效果";
                    item.itemType = 3;
                    item.categoryId = GetCategoryIdByItemId(trash.id);
                    item.iconPath = $"UI/Icon/TrashIcons/{trash.id}";
                }
                else
                {
                    item = new ItemData
                    {
                        id = trash.id,
                        name = trash.name,
                        description = "垃圾物品，没有特殊效果",
                        sellPrice = -1,
                        buyPrice = -1,
                        itemType = 3,
                        categoryId = GetCategoryIdByItemId(trash.id),
                        iconPath = $"UI/Icon/TrashIcons/{trash.id}"
                    };
                }
                extractedItems.Add(item);
            }
        }

        // ========== 处理室外皮肤 ==========
        List<OutdoorSkinData> outdoorSkins = LoadOutdoorSkinData();
        if (outdoorSkins != null)
        {
            foreach (var skin in outdoorSkins)
            {
                ItemData existingItem = FindItemById(skin.id);
                ItemData item;

                if (existingItem != null)
                {
                    item = existingItem;
                    item.name = skin.name;
                    item.description = skin.description;
                    item.itemType = 4;
                    item.categoryId = GetCategoryIdByItemId(skin.id);
                    item.iconPath = $"UI/Icon/OutdoorSkinIcons/{skin.id}";
                }
                else
                {
                    item = new ItemData
                    {
                        id = skin.id,
                        name = skin.name,
                        description = skin.description,
                        sellPrice = -1,
                        buyPrice = -1,
                        itemType = 4,
                        categoryId = GetCategoryIdByItemId(skin.id),
                        iconPath = $"UI/Icon/OutdoorSkinIcons/{skin.id}"
                    };
                }
                extractedItems.Add(item);
            }
        }

        // ========== 处理室内皮肤 ==========
        List<IndoorSkinData> indoorSkins = LoadIndoorSkinData();
        if (indoorSkins != null)
        {
            foreach (var skin in indoorSkins)
            {
                ItemData existingItem = FindItemById(skin.id);
                ItemData item;

                if (existingItem != null)
                {
                    item = existingItem;
                    item.name = skin.name;
                    item.description = skin.description;
                    item.itemType = 5;
                    item.categoryId = GetCategoryIdByItemId(skin.id);
                    item.iconPath = $"UI/Icon/IndoorSkinIcons/{skin.id}";
                }
                else
                {
                    item = new ItemData
                    {
                        id = skin.id,
                        name = skin.name,
                        description = skin.description,
                        sellPrice = -1,
                        buyPrice = -1,
                        itemType = 5,
                        categoryId = GetCategoryIdByItemId(skin.id),
                        iconPath = $"UI/Icon/IndoorSkinIcons/{skin.id}"
                    };
                }
                extractedItems.Add(item);
            }
        }

        // ========== 处理窝料 ==========
        if (nestBaits != null)
        {
            foreach (var nestBait in nestBaits)
            {
                ItemData existingItem = FindItemById(nestBait.id);
                ItemData item;

                if (existingItem != null)
                {
                    item = existingItem;
                    item.name = nestBait.name;
                    item.description = nestBait.description ?? "窝料，用于打窝吸引鱼类";
                    item.itemType = 6;
                    item.categoryId = GetCategoryIdByItemId(nestBait.id);
                    item.iconPath = $"UI/Icon/NestBaitIcons/{nestBait.id}";
                }
                else
                {
                    item = new ItemData
                    {
                        id = nestBait.id,
                        name = nestBait.name,
                        description = nestBait.description ?? "窝料，用于打窝吸引鱼类",
                        sellPrice = -1,
                        buyPrice = -1,
                        itemType = 6,
                        categoryId = GetCategoryIdByItemId(nestBait.id),
                        iconPath = $"UI/Icon/NestBaitIcons/{nestBait.id}"
                    };
                }
                extractedItems.Add(item);
            }
        }

        // ========== 处理图鉴情报 ==========
        if (collectionWrapper?.collection?.categories != null)
        {
            foreach (var category in collectionWrapper.collection.categories)
            {
                if (category.pages != null)
                {
                    foreach (var page in category.pages)
                    {
                        int infoId = page.id;

                        if (infoId < 7000)
                        {
                            infoId = 7000 + (infoId - 800);
                        }

                        string infoName = page.pageName + "图鉴情报";
                        string infoDescription = $"解锁{page.pageName}的图鉴情报，解锁后可查看该页面所有相关条目";

                        ItemData existingItem = FindItemById(infoId);
                        ItemData item;

                        if (existingItem != null)
                        {
                            item = existingItem;
                            item.name = infoName;
                            item.description = infoDescription;
                            item.itemType = 7;
                            item.categoryId = GetCategoryIdByItemId(infoId);
                            item.iconPath = $"UI/Icon/CollectionIcons/{infoId}";
                            item.isUnique = true;
                        }
                        else
                        {
                            item = new ItemData
                            {
                                id = infoId,
                                name = infoName,
                                description = infoDescription,
                                sellPrice = -1,
                                buyPrice = -1,
                                itemType = 7,
                                categoryId = GetCategoryIdByItemId(infoId),
                                iconPath = $"UI/Icon/CollectionIcons/{infoId}",
                                isUnique = true
                            };
                        }

                        if (page.entries != null && page.entries.Count > 0)
                        {
                            if (item.collectionInfoPages == null)
                            {
                                item.collectionInfoPages = new List<int>();
                            }
                            foreach (var entryId in page.entries)
                            {
                                if (!item.collectionInfoPages.Contains(entryId))
                                {
                                    item.collectionInfoPages.Add(entryId);
                                }
                            }
                        }

                        extractedItems.Add(item);
                        Debug.Log($"[物品提取] 添加图鉴情报: ID={infoId}, Name={infoName}, Entries={page.entries?.Count ?? 0}");
                    }
                }
            }
        }

        // ========== ✅ 新增：处理岛屿情报 ==========
        if (islandInfoWrapper?.islandInfoList != null)
        {
            foreach (var islandInfo in islandInfoWrapper.islandInfoList)
            {
                int infoId = islandInfo.infoId;
                string infoName = islandInfo.infoName;
                string infoDescription = $"解锁{islandInfo.islandName}的岛屿情报，解锁后可查看该岛屿相关的图鉴条目";

                ItemData existingItem = FindItemById(infoId);
                ItemData item;

                if (existingItem != null)
                {
                    item = existingItem;
                    item.name = infoName;
                    item.description = infoDescription;
                    item.itemType = 8;
                    item.categoryId = GetCategoryIdByItemId(infoId);
                    item.iconPath = islandInfo.iconPath ?? $"UI/Icon/IslandInfoIcons/{infoId}";
                    item.isUnique = true;
                    item.sellPrice = islandInfo.sellPrice;
                    item.buyPrice = islandInfo.price > 0 ? islandInfo.price : -1;
                }
                else
                {
                    item = new ItemData
                    {
                        id = infoId,
                        name = infoName,
                        description = infoDescription,
                        sellPrice = islandInfo.sellPrice,
                        buyPrice = islandInfo.price > 0 ? islandInfo.price : -1,
                        itemType = 8,
                        categoryId = GetCategoryIdByItemId(infoId),
                        iconPath = islandInfo.iconPath ?? $"UI/Icon/IslandInfoIcons/{infoId}",
                        isUnique = true
                    };
                }

                extractedItems.Add(item);
                Debug.Log($"[物品提取] 添加岛屿情报: ID={infoId}, Name={infoName}, Price={islandInfo.price}");
            }
        }

        SaveItemsToJson();
        Debug.Log($"[物品提取] 完成！共 {extractedItems.Count} 条物品（含 {extractedItems.FindAll(i => i.itemType == 7).Count} 条图鉴情报，{extractedItems.FindAll(i => i.itemType == 8).Count} 条岛屿情报）");
        Repaint();
    }

    private void WriteItemsData()
    {
        if (extractedItems.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先提取物品数据", "确定");
            return;
        }

        LoadExistingItems();

        List<string> inconsistencies = new List<string>();
        foreach (var newItem in extractedItems)
        {
            var existingItem = FindItemById(newItem.id);
            if (existingItem != null)
            {
                if (existingItem.name != newItem.name)
                {
                    inconsistencies.Add($"ID {newItem.id}: 名称不一致 ({existingItem.name} → {newItem.name})");
                }
                if (existingItem.description != newItem.description)
                {
                    inconsistencies.Add($"ID {newItem.id}: 描述不一致");
                }
                if (existingItem.isUnique != newItem.isUnique)
                {
                    inconsistencies.Add($"ID {newItem.id}: isUnique 不一致 ({existingItem.isUnique} → {newItem.isUnique})");
                }
            }
        }

        if (inconsistencies.Count > 0)
        {
            string message = "检测到以下不一致：\n";
            foreach (var inconsistency in inconsistencies)
            {
                message += "- " + inconsistency + "\n";
            }
            message += "\n是否强制覆盖？";

            if (!EditorUtility.DisplayDialog("检测到不一致", message, "强制覆盖", "取消"))
            {
                return;
            }
        }

        SaveItemsToJson();
        EditorUtility.DisplayDialog("成功", $"物品数据写入成功！共 {extractedItems.Count} 条物品", "确定");
        Repaint();
    }

    private void LoadExistingItems()
    {
        existingItems.Clear();
        string fullPath = Path.Combine(Application.dataPath, "Resources", $"{outputPath}.json");

        if (!File.Exists(fullPath))
        {
            Debug.Log("[物品提取] 未找到已存在的物品文件，将创建新文件");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<ItemListWrapper>(json);
            if (wrapper != null && wrapper.items != null)
            {
                existingItems = wrapper.items;
                Debug.Log($"[物品提取] 已加载 {existingItems.Count} 条已存在物品");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载已存在物品失败: {e.Message}");
        }
    }

    private ItemData FindItemById(int id)
    {
        foreach (var item in existingItems)
        {
            if (item.id == id) return item;
        }
        return null;
    }

    private List<FishData> LoadFishData()
    {
        string fullPath = Path.Combine(Application.dataPath, fishDataPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[物品提取] 鱼类文件不存在: {fullPath}");
            return new List<FishData>();
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<FishListWrapper>(json);
            return wrapper?.fishes ?? new List<FishData>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载鱼类数据失败: {e.Message}");
            return new List<FishData>();
        }
    }

    private List<BaitData> LoadBaitData()
    {
        string fullPath = Path.Combine(Application.dataPath, baitDataPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 鱼饵文件不存在: {fullPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<BaitListWrapper>(json);
            return wrapper?.baits ?? null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载鱼饵数据失败: {e.Message}");
            return null;
        }
    }

    private List<TrashData> LoadTrashData()
    {
        string fullPath = Path.Combine(Application.dataPath, trashDataPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 垃圾文件不存在: {fullPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<TrashListWrapper>(json);
            return wrapper?.trashList ?? null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载垃圾数据失败: {e.Message}");
            return null;
        }
    }

    private List<NestBaitData> LoadNestBaitData()
    {
        string fullPath = Path.Combine(Application.dataPath, nestBaitDataPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 窝料文件不存在: {fullPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<NestBaitListWrapper>(json);
            return wrapper?.nestBaits ?? null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载窝料数据失败: {e.Message}");
            return null;
        }
    }

    private List<OutdoorSkinData> LoadOutdoorSkinData()
    {
        string fullPath = Path.Combine(Application.dataPath, outdoorSkinDataPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 室外皮肤文件不存在: {fullPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<OutdoorSkinListWrapper>(json);
            return wrapper?.decorations ?? null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载室外皮肤数据失败: {e.Message}");
            return null;
        }
    }

    private List<IndoorSkinData> LoadIndoorSkinData()
    {
        string fullPath = Path.Combine(Application.dataPath, indoorSkinDataPath);
        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 室内皮肤文件不存在: {fullPath}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            var wrapper = JsonUtility.FromJson<IndoorSkinListWrapper>(json);
            return wrapper?.decorations ?? null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载室内皮肤数据失败: {e.Message}");
            return null;
        }
    }

    private void LoadCategoryData()
    {
        string fullPath = Path.Combine(Application.dataPath, categoryDataPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 物品分类文件不存在: {fullPath}");
            categoryWrapper = null;
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            categoryWrapper = JsonUtility.FromJson<CategoryListWrapper>(json);
            Debug.Log($"[物品提取] 已加载物品分类框架数据");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载物品分类数据失败: {e.Message}");
            categoryWrapper = null;
        }
    }

    private void LoadCollectionData()
    {
        string fullPath = Path.Combine(Application.dataPath, collectionDataPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 图鉴数据文件不存在: {fullPath}");
            collectionWrapper = null;
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            collectionWrapper = JsonUtility.FromJson<CollectionWrapper>(json);
            if (collectionWrapper?.collection?.categories != null)
            {
                Debug.Log($"[物品提取] 已加载图鉴数据，共 {collectionWrapper.collection.categories.Count} 个分类");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载图鉴数据失败: {e.Message}");
            collectionWrapper = null;
        }
    }

    // ✅ 新增：加载岛屿情报数据
    private void LoadIslandInfoData()
    {
        string fullPath = Path.Combine(Application.dataPath, islandInfoDataPath);

        if (!File.Exists(fullPath))
        {
            Debug.LogWarning($"[物品提取] 岛屿情报数据文件不存在: {fullPath}");
            islandInfoWrapper = null;
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            islandInfoWrapper = JsonUtility.FromJson<IslandInfoListWrapper>(json);
            if (islandInfoWrapper?.islandInfoList != null)
            {
                Debug.Log($"[物品提取] 已加载岛屿情报数据，共 {islandInfoWrapper.islandInfoList.Count} 个");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品提取] 加载岛屿情报数据失败: {e.Message}");
            islandInfoWrapper = null;
        }
    }

    private int GetCategoryIdByItemId(int itemId)
    {
        if (categoryWrapper == null || categoryWrapper.categories == null)
        {
            return 99;
        }

        foreach (var category in categoryWrapper.categories)
        {
            if (itemId >= category.startId && itemId <= category.endId)
            {
                if (category.subCategories != null && category.subCategories.Count > 0)
                {
                    foreach (var subCat in category.subCategories)
                    {
                        if (itemId >= subCat.startId && itemId <= subCat.endId)
                        {
                            return subCat.id;
                        }
                    }
                }
                return category.id;
            }
        }

        return 99;
    }

    private void SaveItemsToJson()
    {
        ItemListWrapper wrapper = new ItemListWrapper
        {
            items = extractedItems
        };

        string json = JsonUtility.ToJson(wrapper, true);
        string fullPath = Path.Combine(Application.dataPath, "Resources", $"{outputPath}.json");

        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"[物品提取] 已保存到: {fullPath}");
    }

    // ========== 序列化类 ==========
    [System.Serializable]
    private class CategoryListWrapper
    {
        public List<CategoryData> categories;
        public List<string> notes;
    }

    [System.Serializable]
    private class CategoryData
    {
        public int id;
        public string name;
        public string code;
        public string description;
        public int startId;
        public int endId;
        public List<SubCategoryData> subCategories;
    }

    [System.Serializable]
    private class SubCategoryData
    {
        public int id;
        public string name;
        public string description;
        public int startId;
        public int endId;
    }

    [System.Serializable]
    private class ItemListWrapper
    {
        public List<ItemData> items;
    }

    [System.Serializable]
    private class FishListWrapper
    {
        public List<FishData> fishes;
    }

    [System.Serializable]
    private class BaitListWrapper
    {
        public List<BaitData> baits;
    }

    [System.Serializable]
    private class TrashListWrapper
    {
        public List<TrashData> trashList;
    }

    [System.Serializable]
    private class NestBaitListWrapper
    {
        public List<NestBaitData> nestBaits;
    }

    [System.Serializable]
    private class OutdoorSkinListWrapper
    {
        public List<OutdoorSkinData> decorations;
    }

    [System.Serializable]
    private class IndoorSkinListWrapper
    {
        public List<IndoorSkinData> decorations;
    }

    [System.Serializable]
    private class CollectionWrapper
    {
        public CollectionRoot collection;
    }

    [System.Serializable]
    private class CollectionRoot
    {
        public List<CollectionCategory> categories;
    }

    [System.Serializable]
    private class CollectionCategory
    {
        public int id;
        public string name;
        public string icon;
        public List<CollectionPage> pages;
    }

    [System.Serializable]
    private class CollectionPage
    {
        public int id;
        public string pageName;
        public List<CollectionReward> rewards;
        public List<int> entries;
    }

    [System.Serializable]
    private class CollectionReward
    {
        public int percent;
        public int rewardId;
        public int rewardAmount;
    }

    // ✅ 新增：岛屿情报序列化类
    [System.Serializable]
    private class IslandInfoListWrapper
    {
        public List<IslandInfoSaveData> islandInfoList;
        public string version;
        public string lastUpdateTime;
    }

    [System.Serializable]
    private class IslandInfoSaveData
    {
        public int infoId;
        public int islandId;
        public string islandName;
        public string infoName;
        public int price;
        public bool isOnSale;
        public int stock;
        public bool isUnique;
        public int buyPrice;
        public int sellPrice;
        public string iconPath;
        public int categoryId;
    }
}
#endif
