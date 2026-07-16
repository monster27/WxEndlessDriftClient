#if UNITY_EDITOR
// ==================== ItemDataEditor.cs ====================
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ItemDataEditor : EditorWindow
{
    private Dictionary<string, int> groupPageIndex = new Dictionary<string, int>(); // 新增：存储每个组的当前页码
    private string inputPath = "JsonData/Game/Items/items";
    private List<ItemData> items = new List<ItemData>();
    private int selectedIndex = -1;
    private Vector2 scrollPosition;

    private float col1 = 60;
    private float col2 = 120;
    private float col3 = 60;
    private float col4 = 200;
    private float col5 = 60;
    private float col6 = 60;
    private float col7 = 60;

    private bool showFishList = true;
    private bool showBaitList = true;
    private bool showNestList = true;
    private bool showOtherList = true;

    //[MenuItem("Tools/Item Tools/编辑物品数据")]
    [MenuItem("Tools/游戏内容/3.物品通用数据/2.编辑物品数据")]

    
    public static void ShowWindow()
    {
        ItemDataEditor window = GetWindow<ItemDataEditor>("物品数据编辑器");
        window.minSize = new Vector2(750, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadItemsFromJson();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawDataTable();
        DrawEditPanel();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadItemsFromJson();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {items.Count} 条数据（仅可编辑售价）", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawDataTable()
    {
        EditorGUILayout.LabelField("物品列表", EditorStyles.boldLabel);

        if (items.Count == 0)
        {
            EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        List<ItemData> fishItems = items.FindAll(item => item.itemType == 1);
        List<ItemData> baitItems = items.FindAll(item => item.itemType == 2);
        List<ItemData> nestItems = items.FindAll(item => item.itemType == 3);
        List<ItemData> otherItems = items.FindAll(item => item.itemType >= 4);

        DrawItemGroup("🐟 水产数据", fishItems, ref showFishList);
        DrawItemGroup("🎣 饵料数据", baitItems, ref showBaitList);
        DrawItemGroup("🪣 窝料数据", nestItems, ref showNestList);
        DrawItemGroup("📦 其他物品", otherItems, ref showOtherList);
    }

    private void DrawItemGroup(string title, List<ItemData> groupItems, ref bool isExpanded)
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        isExpanded = EditorGUILayout.Foldout(isExpanded, title, true, EditorStyles.foldoutHeader);

        GUI.backgroundColor = new Color(0.9f, 0.9f, 0.6f);
        EditorGUILayout.LabelField($"共 {groupItems.Count} 条", GUILayout.Width(60));
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (isExpanded && groupItems.Count > 0)
        {
            EditorGUI.indentLevel++;

            // 固定表头
            EditorGUILayout.BeginHorizontal("box");
            DrawResizableColumn("ID", ref col1);
            DrawResizableColumn("名称", ref col2);
            DrawResizableColumn("类型", ref col3);
            DrawResizableColumn("描述", ref col4);
            DrawResizableColumn("出售价", ref col5);
            DrawResizableColumn("购买价", ref col6);
            DrawResizableColumn("所属ID", ref col7);
            EditorGUILayout.LabelField("操作", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            // 创建滚动视图 - 每次显示5条
            int itemsPerPage = 5;
            int totalPages = Mathf.CeilToInt((float)groupItems.Count / itemsPerPage);

            // 使用静态变量或成员变量来存储当前页码（需要在类中添加）
            // 这里使用一个字典来存储每个组的当前页码
            if (!groupPageIndex.ContainsKey(title))
            {
                groupPageIndex[title] = 0;
            }

            int currentPage = groupPageIndex[title];

            // 计算当前页显示的项
            int startIndex = currentPage * itemsPerPage;
            int endIndex = Mathf.Min(startIndex + itemsPerPage, groupItems.Count);

            // 滚动区域
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(180)); // 固定高度

            for (int i = startIndex; i < endIndex; i++)
            {
                ItemData item = groupItems[i];
                int originalIndex = items.IndexOf(item);

                if (selectedIndex == originalIndex)
                    GUI.backgroundColor = Color.cyan;
                else if (i % 2 == 0)
                    GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                else
                    GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

                EditorGUILayout.BeginHorizontal("box");

                EditorGUILayout.LabelField(item.id.ToString(), GUILayout.Width(col1));
                EditorGUILayout.LabelField(item.name, GUILayout.Width(col2));
                EditorGUILayout.LabelField(GetItemTypeName(item.itemType), GUILayout.Width(col3));
                EditorGUILayout.LabelField(item.description.Length > 25 ? item.description.Substring(0, 25) + "..." : item.description, GUILayout.Width(col4));
                EditorGUILayout.LabelField(item.sellPrice.ToString(), GUILayout.Width(col5));
                EditorGUILayout.LabelField(item.buyPrice.ToString(), GUILayout.Width(col6));
                EditorGUILayout.LabelField(item.categoryId.ToString(), GUILayout.Width(col7));

                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = originalIndex;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // 分页控制
            if (totalPages > 1)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // 上一页按钮
                GUI.enabled = currentPage > 0;
                if (GUILayout.Button("◀ 上一页", GUILayout.Width(80)))
                {
                    groupPageIndex[title]--;
                    scrollPosition = Vector2.zero; // 重置滚动位置
                }
                GUI.enabled = true;

                // 页码显示
                EditorGUILayout.LabelField($"第 {currentPage + 1} / {totalPages} 页", GUILayout.Width(80));

                // 下一页按钮
                GUI.enabled = currentPage < totalPages - 1;
                if (GUILayout.Button("下一页 ▶", GUILayout.Width(80)))
                {
                    groupPageIndex[title]++;
                    scrollPosition = Vector2.zero; // 重置滚动位置
                }
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }
        else if (isExpanded && groupItems.Count == 0)
        {
            EditorGUILayout.LabelField("无数据", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    private void DrawResizableColumn(string title, ref float width)
    {
        EditorGUILayout.LabelField(title, GUILayout.Width(width));
    }

    private string GetItemTypeName(int itemType)
    {
        switch (itemType)
        {
            case 1: return "水产";
            case 2: return "饵料";
            case 3: return "窝料";
            case 4: return "装备";
            case 5: return "装饰";
            case 6: return "特殊";
            default: return "未知";
        }
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < items.Count)
        {
            ItemData item = items[selectedIndex];
            
            // 标题行
            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // 只读字段组
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("基础信息 (不可修改)", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            GUI.enabled = false;
            
            // ID和名称
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(100));
            EditorGUILayout.IntField(item.id, GUILayout.Width(100));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("名称:", GUILayout.Width(100));
            EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
            
            // 描述
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(100), GUILayout.Height(40));
            EditorGUILayout.TextArea(item.description, GUILayout.Height(40));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
            
            // 物品类型
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("物品类型:", GUILayout.Width(100));
            EditorGUILayout.IntField(item.itemType, GUILayout.Width(100));
            EditorGUILayout.LabelField($"({GetItemTypeName(item.itemType)})", GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);

            // 可编辑字段组
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("可编辑信息", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // 价格
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("出售价格:", GUILayout.Width(100));
            item.sellPrice = EditorGUILayout.IntField(item.sellPrice, GUILayout.Width(100));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("购买价格:", GUILayout.Width(100));
            item.buyPrice = EditorGUILayout.IntField(item.buyPrice, GUILayout.Width(100));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);
            
            // 所属ID和图标路径
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("所属分类ID:", GUILayout.Width(100));
            item.categoryId = EditorGUILayout.IntField(item.categoryId, GUILayout.Width(100));
            GUILayout.Space(20);
            EditorGUILayout.LabelField("图标路径:", GUILayout.Width(100));
            item.iconPath = EditorGUILayout.TextField(item.iconPath);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            GUILayout.Space(15);

            // 保存按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("保存修改", GUILayout.Width(120), GUILayout.Height(30)))
            {
                SaveItemsToJson();
                EditorUtility.DisplayDialog("成功", "数据已保存", "确定");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("请从上方列表选择要编辑的项", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void LoadItemsFromJson()
    {
        string fullPath = Path.Combine(Application.dataPath, "Resources", $"{inputPath}.json");

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[物品编辑器] 文件不存在: {fullPath}");
            items = new List<ItemData>();
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            ItemListWrapper wrapper = JsonUtility.FromJson<ItemListWrapper>(json);

            if (wrapper == null || wrapper.items == null)
            {
                Debug.LogError($"[物品编辑器] JSON文件解析失败！");
                items = new List<ItemData>();
                return;
            }

            items = wrapper.items;
            selectedIndex = -1;
            Debug.Log($"[物品编辑器] 加载了 {items.Count} 条物品数据");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[物品编辑器] 错误: {e.Message}");
            items = new List<ItemData>();
        }
        Repaint();
    }

    private void SaveItemsToJson()
    {
        ItemListWrapper wrapper = new ItemListWrapper
        {
            items = items
        };

        string json = JsonUtility.ToJson(wrapper, true);
        string fullPath = Path.Combine(Application.dataPath, "Resources", $"{inputPath}.json");

        string directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"[物品编辑器] 已保存 {items.Count} 条物品数据");
    }
}

#endif
