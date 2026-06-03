// ==================== ItemDataExtractorEditor.cs ====================
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class ItemDataExtractorEditor : EditorWindow
{
    private string outputPath = "JsonData/Game/Items/items";
    private string fishDataPath = "Resources/JsonData/Game/BagItem/fishes.json";
    private string baitDataPath = "Resources/JsonData/Game/BagItem/baits.json";
    private string trashDataPath = "Resources/JsonData/Game/BagItem/trash.json";
    private string categoryDataPath = "Resources/JsonData/Game/GameFramework/itemCategories.json";
    private Vector2 scrollPosition;
    private List<ItemData> extractedItems = new List<ItemData>();
    private List<ItemData> existingItems = new List<ItemData>();
    private CategoryListWrapper categoryWrapper;

    private bool showFishList = true;
    private bool showBaitList = true;
    private bool showTrashList = true;

    //[MenuItem("Tools/Item Tools/提取物品数据")]
    [MenuItem("Tools/游戏内容/3.物品通用数据/1.提取物品数据")]


    
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

        DrawItemGroup("🐟 鱼类数据", fishItems, ref showFishList);
        DrawItemGroup("🎣 鱼饵数据", baitItems, ref showBaitList);
        DrawItemGroup("🗑️ 垃圾数据", trashItems, ref showTrashList);
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
            case 4: return "装备";
            case 5: return "装饰";
            case 6: return "特殊";
            default: return "未知";
        }
    }

    private void ExtractItems()
    {
        extractedItems.Clear();

        LoadExistingItems();
        LoadCategoryData();

        List<FishData> fishes = LoadFishData();
        List<BaitData> baits = LoadBaitData();
        List<TrashData> trashList = LoadTrashData();

        if (fishes == null || fishes.Count == 0)
        {
            Debug.LogError("[物品提取] 未找到鱼类数据！");
            return;
        }

        Debug.Log($"[物品提取] 加载完成：鱼类={fishes.Count}，鱼饵={baits?.Count ?? 0}，垃圾={trashList?.Count ?? 0}，已存在物品={existingItems.Count}");

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

        SaveItemsToJson();
        Debug.Log($"[物品提取] 完成！共 {extractedItems.Count} 条物品");
        Repaint();
    }

    private void WriteItemsData()
    {
        if (extractedItems.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先提取物品数据", "确定");
            return;
        }

        // 加载现有数据进行比较
        LoadExistingItems();
        
        // 检测不一致
        List<string> inconsistencies = new List<string>();
        foreach (var newItem in extractedItems)
        {
            var existingItem = FindItemById(newItem.id);
            if (existingItem != null)
            {
                // 检查名称和描述是否一致
                if (existingItem.name != newItem.name)
                {
                    inconsistencies.Add($"ID {newItem.id}: 名称不一致 ({existingItem.name} → {newItem.name})");
                }
                if (existingItem.description != newItem.description)
                {
                    inconsistencies.Add($"ID {newItem.id}: 描述不一致");
                }
            }
        }

        // 显示不一致信息并确认
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

        // 写入数据
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

    private int GetCategoryIdByItemId(int itemId)
    {
        if (categoryWrapper == null || categoryWrapper.categories == null)
        {
            return 0;
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

        return 0;
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
}
