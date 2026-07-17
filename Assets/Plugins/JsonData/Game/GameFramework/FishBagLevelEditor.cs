// ==================== FishBagLevelEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// 鱼篓等级配置数据类
/// </summary>
[System.Serializable]
public class FishBagLevelData
{
    public int level;
    public int capacity;
    public int autoSellInterval;
    public string upgradeDescription;
    public int upgradeCost;
}

/// <summary>
/// 鱼篓等级配置列表包装器
/// </summary>
[System.Serializable]
public class FishBagLevelListWrapper
{
    public List<FishBagLevelData> fishBagLevels;
}

/// <summary>
/// 鱼篓等级配置编辑器
/// </summary>
public class FishBagLevelEditor : EditorWindow
{
    private List<FishBagLevelData> dataList = new List<FishBagLevelData>();
    private Vector2 scrollPosition;
    private int selectedIndex = -1;

    // 编辑变量
    private int editLevel = 1;
    private int editCapacity = 10;
    private int editAutoSellInterval = 0;
    private string editUpgradeDescription = "";
    private int editUpgradeCost = 100;

    // 表头宽度
    private float col1 = 50;   // 等级
    private float col2 = 60;   // 容量
    private float col3 = 120;  // 自动出售间隔
    private float col4 = 200;  // 升级描述
    private float col5 = 80;   // 升级费用

    private const string RELATIVE_PATH = "Resources/JsonData/Game/GameFramework/fishBagLevels.json";

    private string FullPath => Path.Combine(Application.dataPath, RELATIVE_PATH);

    [MenuItem("Tools/游戏内容/4.鱼篓等级配置")]
    public static void ShowWindow()
    {
        FishBagLevelEditor window = GetWindow<FishBagLevelEditor>("鱼篓等级配置编辑器");
        window.minSize = new Vector2(750, 600);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void OnGUI()
    {
        DrawToolbar();
        DrawDataTable();
        DrawEditPanel();
        DrawBottomButtons();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadData();
        if (GUILayout.Button("新增", EditorStyles.toolbarButton, GUILayout.Width(60))) AddNewItem();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {dataList.Count} 条数据", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    private void DrawDataTable()
    {
        EditorGUILayout.LabelField("鱼篓等级列表", EditorStyles.boldLabel);

        // ==================== 表头 ====================
        EditorGUILayout.BeginHorizontal("box");

        DrawResizableColumn("等级", ref col1, "col1");
        DrawResizableColumn("容量", ref col2, "col2");
        DrawResizableColumn("自动出售间隔", ref col3, "col3");
        DrawResizableColumn("升级描述", ref col4, "col4");
        DrawResizableColumn("升级费用", ref col5, "col5");

        EditorGUILayout.LabelField("操作", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // ==================== 数据行 ====================
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < dataList.Count; i++)
        {
            DrawDataRow(i);
        }

        if (dataList.Count == 0)
        {
            EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(10);
    }

    private void DrawDataRow(int index)
    {
        FishBagLevelData item = dataList[index];

        // 奇偶行不同底色
        if (selectedIndex == index)
            GUI.backgroundColor = Color.cyan;
        else if (index % 2 == 0)
            GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        else
            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        EditorGUILayout.BeginHorizontal("box");

        // 等级
        EditorGUILayout.LabelField($"Lv.{item.level}", GUILayout.Width(col1));
        // 容量
        EditorGUILayout.LabelField(item.capacity.ToString(), GUILayout.Width(col2));
        // 自动出售间隔
        string intervalText = GetAutoSellDisplayText(item.autoSellInterval);
        EditorGUILayout.LabelField(intervalText, GUILayout.Width(col3));
        // 升级描述
        EditorGUILayout.LabelField(item.upgradeDescription, GUILayout.Width(col4));
        // 升级费用
        string costText = item.upgradeCost > 0 ? $"{item.upgradeCost}金币" : "已满级";
        EditorGUILayout.LabelField(costText, GUILayout.Width(col5));

        // 操作
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除等级 [{item.level}] 的配置吗？", "删除", "取消"))
        {
            dataList.RemoveAt(index);
            if (selectedIndex >= dataList.Count) selectedIndex = -1;
            SaveData();
            LoadData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < dataList.Count)
        {
            FishBagLevelData item = dataList[selectedIndex];
            EditorGUILayout.LabelField($"正在编辑: Lv.{item.level}", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 等级（只读）
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("等级:", GUILayout.Width(60));
            EditorGUILayout.LabelField(item.level.ToString(), GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // 容量
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("容量:", GUILayout.Width(60));
            item.capacity = EditorGUILayout.IntField(item.capacity, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // 自动出售间隔
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("自动出售间隔:", GUILayout.Width(100));
            item.autoSellInterval = EditorGUILayout.IntField(item.autoSellInterval, GUILayout.Width(80));
            EditorGUILayout.LabelField("分钟", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            // 显示当前配置的友好文本
            string displayText = GetAutoSellDisplayText(item.autoSellInterval);
            EditorGUILayout.LabelField($"  当前: {displayText}", EditorStyles.miniLabel);

            // 升级描述
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("升级描述:", GUILayout.Width(60));
            item.upgradeDescription = EditorGUILayout.TextField(item.upgradeDescription);
            EditorGUILayout.EndHorizontal();

            // 升级费用
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("升级费用:", GUILayout.Width(60));
            item.upgradeCost = EditorGUILayout.IntField(item.upgradeCost, GUILayout.Width(80));
            EditorGUILayout.LabelField("金币", GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();

            if (item.upgradeCost <= 0 && item.level < 10)
            {
                EditorGUILayout.HelpBox("升级费用为0，升级将免费", MessageType.Info);
            }
            if (item.level >= 10)
            {
                EditorGUILayout.HelpBox("已满级，无需升级费用", MessageType.Info);
            }

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("保存修改", GUILayout.Width(100)))
            {
                SaveData();
                LoadData();
                EditorUtility.DisplayDialog("成功", "数据已保存", "确定");
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.LabelField("请从左侧列表选择要编辑的项", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("快速新增", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("等级:", GUILayout.Width(40));
        editLevel = EditorGUILayout.IntField(editLevel, GUILayout.Width(60));
        EditorGUILayout.LabelField("容量:", GUILayout.Width(40));
        editCapacity = EditorGUILayout.IntField(editCapacity, GUILayout.Width(60));
        EditorGUILayout.LabelField("出售间隔:", GUILayout.Width(70));
        editAutoSellInterval = EditorGUILayout.IntField(editAutoSellInterval, GUILayout.Width(60));
        EditorGUILayout.LabelField("分钟", GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("升级描述:", GUILayout.Width(70));
        editUpgradeDescription = EditorGUILayout.TextField(editUpgradeDescription, GUILayout.Width(250));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("升级费用:", GUILayout.Width(70));
        editUpgradeCost = EditorGUILayout.IntField(editUpgradeCost, GUILayout.Width(80));
        EditorGUILayout.LabelField("金币", GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "提示：\n" +
            "• 自动出售间隔：0表示不自动出售，大于0表示每隔X分钟自动出售一次\n" +
            "• 升级费用：0表示免费升级（仅限满级使用）",
            MessageType.Info);
    }

    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                string json = File.ReadAllText(FullPath);
                var wrapper = JsonUtility.FromJson<FishBagLevelListWrapper>(json);
                dataList = wrapper?.fishBagLevels ?? new List<FishBagLevelData>();
                // 按等级排序
                dataList = dataList.OrderBy(x => x.level).ToList();
                if (dataList.Count > 0) Debug.Log($"[FishBagLevelEditor] 加载成功，共{dataList.Count}条数据");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FishBagLevelEditor] 加载失败: {e.Message}");
                dataList = new List<FishBagLevelData>();
            }
        }
        else
        {
            Debug.LogWarning($"[FishBagLevelEditor] 文件不存在: {FullPath}，创建默认数据");
            CreateDefaultData();
        }
        Repaint();
    }

    private void CreateDefaultData()
    {
        dataList.Clear();
        dataList.Add(new FishBagLevelData { level = 1, capacity = 10, autoSellInterval = 0, upgradeDescription = "升级到下一等级需100金币", upgradeCost = 100 });
        dataList.Add(new FishBagLevelData { level = 2, capacity = 15, autoSellInterval = 0, upgradeDescription = "升级到下一等级需200金币", upgradeCost = 200 });
        dataList.Add(new FishBagLevelData { level = 3, capacity = 20, autoSellInterval = 0, upgradeDescription = "升级到下一等级需350金币", upgradeCost = 350 });
        dataList.Add(new FishBagLevelData { level = 4, capacity = 25, autoSellInterval = 0, upgradeDescription = "升级到下一等级需500金币", upgradeCost = 500 });
        dataList.Add(new FishBagLevelData { level = 5, capacity = 30, autoSellInterval = 0, upgradeDescription = "升级到下一等级需750金币", upgradeCost = 750 });
        dataList.Add(new FishBagLevelData { level = 6, capacity = 40, autoSellInterval = 0, upgradeDescription = "升级到下一等级需1000金币", upgradeCost = 1000 });
        dataList.Add(new FishBagLevelData { level = 7, capacity = 50, autoSellInterval = 0, upgradeDescription = "升级到下一等级需1500金币", upgradeCost = 1500 });
        dataList.Add(new FishBagLevelData { level = 8, capacity = 65, autoSellInterval = 120, upgradeDescription = "升级到下一等级需2000金币", upgradeCost = 2000 });
        dataList.Add(new FishBagLevelData { level = 9, capacity = 80, autoSellInterval = 90, upgradeDescription = "升级到下一等级需3000金币", upgradeCost = 3000 });
        dataList.Add(new FishBagLevelData { level = 10, capacity = 100, autoSellInterval = 60, upgradeDescription = "已满级", upgradeCost = 0 });
        SaveData();
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        // 按等级排序后保存
        dataList = dataList.OrderBy(x => x.level).ToList();
        var wrapper = new FishBagLevelListWrapper { fishBagLevels = dataList };
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(FullPath, json);
        AssetDatabase.Refresh();
        Debug.Log($"[FishBagLevelEditor] 保存成功: {FullPath}");
    }

    private void AddNewItem()
    {
        int newLevel = 1;
        if (dataList.Count > 0)
        {
            int maxLevel = dataList.Max(x => x.level);
            newLevel = maxLevel + 1;
        }

        if (newLevel > 10)
        {
            EditorUtility.DisplayDialog("提示", "已达到最大等级(10级)，无法继续添加", "确定");
            return;
        }

        dataList.Add(new FishBagLevelData
        {
            level = newLevel,
            capacity = 10 + (newLevel - 1) * 5,
            autoSellInterval = 0,
            upgradeDescription = $"升级到下一等级需{(newLevel + 1) * 100}金币",
            upgradeCost = (newLevel + 1) * 100
        });

        // 更新最后一个等级的描述
        if (newLevel == 10)
        {
            var last = dataList.Last();
            last.upgradeDescription = "已满级";
            last.upgradeCost = 0;
        }

        selectedIndex = dataList.Count - 1;
        SaveData();
        LoadData();
    }

    private void AddQuickItem()
    {
        // 检查等级是否已存在
        if (dataList.Any(x => x.level == editLevel))
        {
            EditorUtility.DisplayDialog("错误", $"等级 {editLevel} 已存在", "确定");
            return;
        }

        if (editLevel < 1 || editLevel > 10)
        {
            EditorUtility.DisplayDialog("错误", "等级必须在1-10之间", "确定");
            return;
        }

        if (string.IsNullOrEmpty(editUpgradeDescription))
        {
            EditorUtility.DisplayDialog("错误", "升级描述不能为空", "确定");
            return;
        }

        dataList.Add(new FishBagLevelData
        {
            level = editLevel,
            capacity = editCapacity,
            autoSellInterval = editAutoSellInterval,
            upgradeDescription = editUpgradeDescription,
            upgradeCost = editUpgradeCost
        });

        dataList = dataList.OrderBy(x => x.level).ToList();
        SaveData();
        LoadData();

        // 重置输入
        editLevel = dataList.Count + 1;
        if (editLevel > 10) editLevel = 10;
        editCapacity = 10 + (editLevel - 1) * 5;
        editAutoSellInterval = 0;
        editUpgradeDescription = "";
        editUpgradeCost = editLevel * 100;

        EditorUtility.DisplayDialog("成功", "新增成功", "确定");
    }

    private string GetAutoSellDisplayText(int minutes)
    {
        if (minutes <= 0) return "不自动出售";
        if (minutes % 60 == 0) return $"{minutes / 60}小时";
        if (minutes > 60) return $"{minutes / 60}小时{minutes % 60}分钟";
        return $"{minutes}分钟";
    }

    private void DrawResizableColumn(string title, ref float width, string colKey)
    {
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(width));
        EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);

        // 绘制调整手柄
        Rect handleRect = new Rect(rect.x + rect.width - 3, rect.y, 5, rect.height);
        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeHorizontal);
    }

    private bool IsIdDuplicate(int level, int excludeIndex)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            if (i != excludeIndex && dataList[i].level == level) return true;
        }
        return false;
    }
}
#endif
