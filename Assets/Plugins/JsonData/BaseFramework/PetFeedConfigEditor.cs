#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

/// <summary>
/// 宠物自动喂食配置编辑器
/// 用于编辑 petFeedConfig.json
/// 路径：Assets/Addressables/JsonData/Game/GameFramework/petFeedConfig.json
/// </summary>
public class PetFeedConfigEditor : EditorWindow
{
    private const string RELATIVE_PATH = "Addressables/JsonData/Game/GameFramework/petFeedConfig.json";

    private int baseInterval = 7200;
    private List<PetFeedLevelData> dataList = new List<PetFeedLevelData>();
    private List<string> notes = new List<string>();

    private Vector2 scrollPosition;
    private int selectedIndex = -1;

    // 表头宽度
    private float col1 = 60;   // 等级
    private float col2 = 100;  // 间隔（秒）
    private float col3 = 100;  // 间隔（易读）
    private float col4 = 100;  // 升级费用
    private float col5 = 280;  // 升级描述

    private string FullPath => Path.Combine(Application.dataPath, RELATIVE_PATH);

    [MenuItem("Tools/游戏内容/5.宠物自动喂食配置", false)]
    public static void ShowWindow()
    {
        PetFeedConfigEditor window = GetWindow<PetFeedConfigEditor>("宠物自动喂食配置");
        window.minSize = new Vector2(900, 600);
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
            EditorUtility.DisplayDialog("成功", "宠物自动喂食配置已保存", "确定");
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
        EditorGUILayout.LabelField("基础间隔（秒）:", GUILayout.Width(120));
        baseInterval = EditorGUILayout.IntField(baseInterval, GUILayout.Width(100));
        EditorGUILayout.LabelField($"= {FormatSeconds(baseInterval)}", GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("说明：baseInterval 是未升级时的基础间隔（一般等于 level 1 的 interval）", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        GUILayout.Space(5);
    }

    // ==================== 表格 ====================
    private void DrawDataTable()
    {
        EditorGUILayout.LabelField("等级配置列表", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("等级", EditorStyles.boldLabel, GUILayout.Width(col1));
        EditorGUILayout.LabelField("间隔（秒）", EditorStyles.boldLabel, GUILayout.Width(col2));
        EditorGUILayout.LabelField("易读格式", EditorStyles.boldLabel, GUILayout.Width(col3));
        EditorGUILayout.LabelField("升级费用", EditorStyles.boldLabel, GUILayout.Width(col4));
        EditorGUILayout.LabelField("升级描述", EditorStyles.boldLabel, GUILayout.Width(col5));
        EditorGUILayout.LabelField("操作", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));

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
        PetFeedLevelData item = dataList[index];

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;
        else if (index % 2 == 0) GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        else GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        EditorGUILayout.BeginHorizontal("box");

        EditorGUILayout.LabelField($"Lv.{item.level}", GUILayout.Width(col1));
        EditorGUILayout.LabelField($"{item.interval}", GUILayout.Width(col2));
        EditorGUILayout.LabelField(FormatSeconds(item.interval), GUILayout.Width(col3));

        string costText = item.upgradeCost > 0 ? $"{item.upgradeCost}金币" : "已满级";
        EditorGUILayout.LabelField(costText, GUILayout.Width(col4));

        EditorGUILayout.LabelField(item.upgradeDescription, GUILayout.Width(col5));

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
            PetFeedLevelData item = dataList[selectedIndex];
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
            EditorGUILayout.LabelField("间隔（秒）:", GUILayout.Width(80));
            item.interval = EditorGUILayout.IntField(item.interval, GUILayout.Width(80));
            if (item.interval < 0) item.interval = 0;
            EditorGUILayout.LabelField($"= {FormatSeconds(item.interval)}", GUILayout.Width(200));
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
            "• 自动喂食最多 10 级\n" +
            "• 每级提升自动喂食频率（interval 越小，喂食越频繁）\n" +
            "• upgradeCost=0 表示满级，不可继续升级\n" +
            "• level 1 默认可用，自动喂食功能初始即开启\n" +
            "• 过滤配置参考 FishBagSelectPanel（稀有度/星级/闪光/鱼种类）",
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
                var wrapper = JsonUtility.FromJson<PetFeedConfigWrapper>(json);
                if (wrapper != null)
                {
                    baseInterval = wrapper.baseInterval > 0 ? wrapper.baseInterval : 7200;
                    dataList = wrapper.levels ?? new List<PetFeedLevelData>();
                    notes = wrapper.notes ?? new List<string>();
                    dataList = dataList.OrderBy(x => x.level).ToList();
                    Z_Logger.Log($"[PetFeedConfigEditor] 加载成功，共 {dataList.Count} 个等级");
                }
            }
            catch (System.Exception e)
            {
                Z_Logger.LogError($"[PetFeedConfigEditor] 加载失败: {e.Message}");
                CreateDefaultData();
            }
        }
        else
        {
            Z_Logger.LogWarning($"[PetFeedConfigEditor] 文件不存在: {FullPath}，创建默认数据");
            CreateDefaultData();
            SaveData();
        }
        Repaint();
    }

    private void CreateDefaultData()
    {
        baseInterval = 7200;
        dataList = new List<PetFeedLevelData>
        {
            new PetFeedLevelData { level = 1,  interval = 7200, upgradeCost = 0,     upgradeDescription = "未升级，每2小时自动喂食一次" },
            new PetFeedLevelData { level = 2,  interval = 5400, upgradeCost = 500,   upgradeDescription = "升级到下一等级需500金币" },
            new PetFeedLevelData { level = 3,  interval = 3600, upgradeCost = 1000,  upgradeDescription = "升级到下一等级需1000金币" },
            new PetFeedLevelData { level = 4,  interval = 2700, upgradeCost = 2000,  upgradeDescription = "升级到下一等级需2000金币" },
            new PetFeedLevelData { level = 5,  interval = 1800, upgradeCost = 4000,  upgradeDescription = "升级到下一等级需4000金币" },
            new PetFeedLevelData { level = 6,  interval = 1500, upgradeCost = 8000,  upgradeDescription = "升级到下一等级需8000金币" },
            new PetFeedLevelData { level = 7,  interval = 1200, upgradeCost = 15000, upgradeDescription = "升级到下一等级需15000金币" },
            new PetFeedLevelData { level = 8,  interval = 900,  upgradeCost = 30000, upgradeDescription = "升级到下一等级需30000金币" },
            new PetFeedLevelData { level = 9,  interval = 600,  upgradeCost = 50000, upgradeDescription = "升级到下一等级需50000金币" },
            new PetFeedLevelData { level = 10, interval = 300,  upgradeCost = 0,     upgradeDescription = "已满级，每5分钟自动喂食一次" }
        };
        notes = new List<string>
        {
            "baseInterval：未升级时的基础间隔（秒），默认7200秒（2小时）",
            "levels[].interval：该等级的自动喂食间隔（秒）",
            "levels[].upgradeCost：升级到下一等级消耗金币（0=满级）",
            "自动喂食功能初始可用（level 1）"
        };
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        dataList = dataList.OrderBy(x => x.level).ToList();

        var wrapper = new PetFeedConfigWrapper
        {
            baseInterval = baseInterval,
            levels = dataList,
            notes = notes
        };

        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(FullPath, json);
        AssetDatabase.Refresh();
        Z_Logger.Log($"[PetFeedConfigEditor] 保存成功: {FullPath}");
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

        int prevInterval = dataList.Count > 0 ? dataList.Last().interval : baseInterval;
        int nextInterval = Mathf.Max(60, prevInterval - 300);
        dataList.Add(new PetFeedLevelData
        {
            level = newLevel,
            interval = nextInterval,
            upgradeCost = newLevel * 1000,
            upgradeDescription = $"升级到下一等级需{newLevel * 1000}金币"
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

    /// <summary>秒数格式化：7200 → "2小时"，300 → "5分钟"</summary>
    private string FormatSeconds(int totalSeconds)
    {
        if (totalSeconds < 60) return $"{totalSeconds}秒";
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        if (hours > 0)
        {
            if (minutes > 0) return $"{hours}小时{minutes}分钟";
            return $"{hours}小时";
        }
        if (seconds > 0) return $"{minutes}分{seconds}秒";
        return $"{minutes}分钟";
    }
}
#endif
