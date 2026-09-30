#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// 宠物栏储存配置编辑器
/// 用于编辑 petStorage.json：初始容量 + 各等级容量/升级费用
/// 路径：Addressables/JsonData/Game/GameFramework/petStorage.json
/// </summary>
public class PetStorageEditor : EditorWindow
{
    // ✅ 修正后的路径
    private const string RELATIVE_PATH = "Addressables/JsonData/Game/GameFramework/petStorage.json";

    private int baseCapacity = 20;
    private List<PetStorageLevelData> dataList = new List<PetStorageLevelData>();
    private List<string> notes = new List<string>();

    private Vector2 scrollPosition;
    private int selectedIndex = -1;

    // 表头宽度
    private float col1 = 60;   // 等级
    private float col2 = 80;   // 容量
    private float col3 = 100;  // 升级费用
    private float col4 = 260;  // 升级描述

    private string FullPath => Path.Combine(Application.dataPath, RELATIVE_PATH);

    [MenuItem("Tools/游戏内容/5.宠物栏储存配置", false)]
    public static void ShowWindow()
    {
        PetStorageEditor window = GetWindow<PetStorageEditor>("宠物栏储存配置编辑器");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void OnGUI()
    {
        DrawToolbar();
        DrawBaseConfig();
        DrawDataTable();
        DrawEditPanel();
        DrawHelpInfo();
    }

    // ==================== 工具栏 ====================
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60))) LoadData();
        if (GUILayout.Button("新增等级", EditorStyles.toolbarButton, GUILayout.Width(80))) AddNewLevel();

        GUI.backgroundColor = new Color(1f, 0.8f, 0.4f);
        if (GUILayout.Button("生成默认10级", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            if (EditorUtility.DisplayDialog("确认", "将覆盖当前所有等级配置，生成默认10级数据。确定吗？", "确定", "取消"))
            {
                CreateDefaultData();
                SaveData();
                LoadData();
            }
        }
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"共 {dataList.Count} 个等级", GUILayout.Width(100));

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("💾 保存", EditorStyles.toolbarButton, GUILayout.Width(70)))
        {
            SaveData();
            EditorUtility.DisplayDialog("成功", "宠物栏配置已保存", "确定");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);
    }

    // ==================== 基础配置 ====================
    private void DrawBaseConfig()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("基础配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("初始容量（baseCapacity）:", GUILayout.Width(180));
        baseCapacity = EditorGUILayout.IntField(baseCapacity, GUILayout.Width(80));
        EditorGUILayout.LabelField("格", GUILayout.Width(40));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("说明：baseCapacity 是玩家初始的宠物栏格子数（一般等于 level 1 的 capacity）", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    // ==================== 表格 ====================
    private void DrawDataTable()
    {
        EditorGUILayout.LabelField("等级配置列表", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal("box");
        DrawResizableColumn("等级", ref col1);
        DrawResizableColumn("容量", ref col2);
        DrawResizableColumn("升级费用", ref col3);
        DrawResizableColumn("升级描述", ref col4);
        EditorGUILayout.LabelField("操作", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(220));

        for (int i = 0; i < dataList.Count; i++)
        {
            DrawDataRow(i);
        }

        if (dataList.Count == 0)
        {
            EditorGUILayout.LabelField("暂无数据，点击\"新增等级\"或\"生成默认10级\"", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.Space(5);
    }

    private void DrawDataRow(int index)
    {
        PetStorageLevelData item = dataList[index];

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;
        else if (index % 2 == 0) GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        else GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        EditorGUILayout.BeginHorizontal("box");

        EditorGUILayout.LabelField($"Lv.{item.level}", GUILayout.Width(col1));
        EditorGUILayout.LabelField($"{item.capacity}格", GUILayout.Width(col2));

        string costText = item.upgradeCost > 0 ? $"{item.upgradeCost}金币" : "已满级";
        EditorGUILayout.LabelField(costText, GUILayout.Width(col3));

        EditorGUILayout.LabelField(item.upgradeDescription, GUILayout.Width(col4));

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) &&
            EditorUtility.DisplayDialog("确认删除", $"确定要删除 Lv.{item.level} 吗？", "删除", "取消"))
        {
            dataList.RemoveAt(index);
            if (selectedIndex >= dataList.Count) selectedIndex = -1;
            SaveData();
            LoadData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // ==================== 编辑面板 ====================
    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < dataList.Count)
        {
            PetStorageLevelData item = dataList[selectedIndex];
            EditorGUILayout.LabelField($"正在编辑: Lv.{item.level}", EditorStyles.boldLabel);
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("等级:", GUILayout.Width(80));
            int newLevel = EditorGUILayout.IntField(item.level, GUILayout.Width(80));
            if (newLevel != item.level && !IsLevelDuplicate(newLevel, selectedIndex))
            {
                item.level = newLevel;
            }
            else if (newLevel != item.level)
            {
                EditorUtility.DisplayDialog("错误", $"等级 {newLevel} 已存在", "确定");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("容量（格）:", GUILayout.Width(80));
            item.capacity = EditorGUILayout.IntField(item.capacity, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("升级费用:", GUILayout.Width(80));
            item.upgradeCost = EditorGUILayout.IntField(item.upgradeCost, GUILayout.Width(80));
            EditorGUILayout.LabelField("金币（0=满级）", GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("升级描述:", GUILayout.Width(80));
            item.upgradeDescription = EditorGUILayout.TextField(item.upgradeDescription);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("保存修改", GUILayout.Width(100)))
            {
                dataList = dataList.OrderBy(x => x.level).ToList();
                SaveData();
                LoadData();
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
        GUILayout.Space(5);
    }

    // ==================== 帮助 ====================
    private void DrawHelpInfo()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("说明", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "• 宠物栏最多 10 级\n" +
            "• 每级提升容量（格子数）\n" +
            "• upgradeCost=0 表示满级，不可继续升级\n" +
            "• 服务器会用此配置计算玩家可容纳的宠物上限\n" +
            "• 宠物栏满了之后：孵化收获失败",
            MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    // ==================== 数据操作 ====================
    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                string json = File.ReadAllText(FullPath);
                var wrapper = JsonUtility.FromJson<PetStorageListWrapper>(json);
                if (wrapper != null)
                {
                    baseCapacity = wrapper.baseCapacity > 0 ? wrapper.baseCapacity : 20;
                    dataList = wrapper.levels ?? new List<PetStorageLevelData>();
                    notes = wrapper.notes ?? new List<string>();
                    dataList = dataList.OrderBy(x => x.level).ToList();
                    Z_Logger.Log($"[PetStorageEditor] 加载成功，共 {dataList.Count} 个等级");
                }
            }
            catch (System.Exception e)
            {
                Z_Logger.LogError($"[PetStorageEditor] 加载失败: {e.Message}");
                CreateDefaultData();
            }
        }
        else
        {
            Z_Logger.LogWarning($"[PetStorageEditor] 文件不存在: {FullPath}，创建默认数据");
            CreateDefaultData();
            SaveData();
        }
        Repaint();
    }

    private void CreateDefaultData()
    {
        baseCapacity = 20;
        dataList = new List<PetStorageLevelData>
        {
            new PetStorageLevelData { level = 1,  capacity = 20,  upgradeCost = 200,   upgradeDescription = "升级到下一等级需200金币" },
            new PetStorageLevelData { level = 2,  capacity = 25,  upgradeCost = 400,   upgradeDescription = "升级到下一等级需400金币" },
            new PetStorageLevelData { level = 3,  capacity = 30,  upgradeCost = 700,   upgradeDescription = "升级到下一等级需700金币" },
            new PetStorageLevelData { level = 4,  capacity = 40,  upgradeCost = 1200,  upgradeDescription = "升级到下一等级需1200金币" },
            new PetStorageLevelData { level = 5,  capacity = 50,  upgradeCost = 2000,  upgradeDescription = "升级到下一等级需2000金币" },
            new PetStorageLevelData { level = 6,  capacity = 65,  upgradeCost = 3200,  upgradeDescription = "升级到下一等级需3200金币" },
            new PetStorageLevelData { level = 7,  capacity = 80,  upgradeCost = 5000,  upgradeDescription = "升级到下一等级需5000金币" },
            new PetStorageLevelData { level = 8,  capacity = 100, upgradeCost = 8000,  upgradeDescription = "升级到下一等级需8000金币" },
            new PetStorageLevelData { level = 9,  capacity = 130, upgradeCost = 12000, upgradeDescription = "升级到下一等级需12000金币" },
            new PetStorageLevelData { level = 10, capacity = 160, upgradeCost = 0,     upgradeDescription = "已满级" }
        };
        notes = new List<string>
        {
            "宠物栏初始1级，20格",
            "每升1级增加格子数",
            "level 10 是满级，upgradeCost=0 表示不可升级"
        };
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        dataList = dataList.OrderBy(x => x.level).ToList();

        var wrapper = new PetStorageListWrapper
        {
            baseCapacity = baseCapacity,
            levels = dataList,
            notes = notes
        };

        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(FullPath, json);
        AssetDatabase.Refresh();
        Z_Logger.Log($"[PetStorageEditor] 保存成功: {FullPath}");
    }

    private void AddNewLevel()
    {
        int newLevel = 1;
        if (dataList.Count > 0) newLevel = dataList.Max(x => x.level) + 1;

        if (newLevel > 10)
        {
            EditorUtility.DisplayDialog("提示", "已达到最大等级 10", "确定");
            return;
        }

        int prevCapacity = dataList.Count > 0 ? dataList.Last().capacity : baseCapacity;
        dataList.Add(new PetStorageLevelData
        {
            level = newLevel,
            capacity = prevCapacity + 10,
            upgradeCost = newLevel * 500,
            upgradeDescription = $"升级到下一等级需{newLevel * 500}金币"
        });

        selectedIndex = dataList.Count - 1;
        SaveData();
        LoadData();
    }

    private bool IsLevelDuplicate(int level, int excludeIndex)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            if (i != excludeIndex && dataList[i].level == level) return true;
        }
        return false;
    }

    private void DrawResizableColumn(string title, ref float width)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(width));
    }
}
#endif
