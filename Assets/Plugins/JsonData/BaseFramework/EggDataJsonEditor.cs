#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class EggDataJsonEditor : EditorWindow
{
    private const string RELATIVE_PATH = "Addressables/JsonData/BaseFramework/eggs.json";
    private const int START_ID = 10201;

    private List<EggData> dataList = new List<EggData>();
    private Vector2 scrollPosition;
    private int selectedIndex = -1;

    // 快速新增字段
    private int editId = START_ID;
    private int editRarityId = 201;
    private string editName = "";
    private string editDescription = "";
    private int editHatchTime = 60;
    private int editSkipCost = 100;
    private int editUpgradeRarityCost = 200;

    private string[] rarityOptions = new string[]
    {
        "普通(201)", "罕见(202)", "稀有(203)", "史诗(204)", "传说(205)", "幻想(206)"
    };
    private int[] rarityIds = { 201, 202, 203, 204, 205, 206 };

    private string FullPath => Path.Combine(Application.dataPath, RELATIVE_PATH);

    [MenuItem("Tools/基础框架/611_蛋")]
    public static void ShowWindow()
    {
        var window = GetWindow<EggDataJsonEditor>("蛋数据编辑器");
        window.minSize = new Vector2(900, 700);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void OnGUI()
    {
        DrawToolbar();
        DrawDataList();
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

    private void DrawDataList()
    {
        EditorGUILayout.LabelField("蛋列表", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        // ✅ 表头
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("ID", GUILayout.Width(60));
        EditorGUILayout.LabelField("稀有度", GUILayout.Width(80));
        EditorGUILayout.LabelField("名称", GUILayout.Width(90));
        EditorGUILayout.LabelField("孵化时间(秒)", GUILayout.Width(90));
        EditorGUILayout.LabelField("跳孵金币", GUILayout.Width(70));
        EditorGUILayout.LabelField("升品金币", GUILayout.Width(70));
        EditorGUILayout.LabelField("描述", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(250));

        for (int i = 0; i < dataList.Count; i++) DrawListItem(i);
        if (dataList.Count == 0)
            EditorGUILayout.LabelField("暂无数据，点击\"新增\"添加", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawListItem(int index)
    {
        EggData item = dataList[index];
        EditorGUILayout.BeginHorizontal();

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;

        EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(60));
        EditorGUILayout.LabelField(GetRarityName(item.rarityId), GUILayout.Width(80));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(90));
        EditorGUILayout.LabelField($"{item.hatchTime}", GUILayout.Width(90));
        EditorGUILayout.LabelField($"{item.skipCost}", GUILayout.Width(70));

        // ✅ 最高稀有度（幻想蛋）不显示升品金币
        string upgradeText = item.upgradeRarityCost > 0 ? item.upgradeRarityCost.ToString() : "--";
        EditorGUILayout.LabelField(upgradeText, GUILayout.Width(70));

        EditorGUILayout.LabelField(item.description, GUILayout.Width(200));

        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) &&
            EditorUtility.DisplayDialog("确认删除", $"确定要删除蛋 [{item.id}] {item.name} 吗？", "删除", "取消"))
        {
            dataList.RemoveAt(index);
            if (selectedIndex >= dataList.Count) selectedIndex = -1;
            SaveData();
            LoadData();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        if (index < dataList.Count - 1) EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    private void DrawEditPanel()
    {
        EditorGUILayout.LabelField("编辑区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (selectedIndex >= 0 && selectedIndex < dataList.Count)
        {
            EggData item = dataList[selectedIndex];
            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}");
            GUILayout.Space(5);

            // ID
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(100));
            int newId = EditorGUILayout.IntField(item.id);
            if (newId != item.id && !IsIdDuplicate(newId, selectedIndex)) item.id = newId;
            else if (newId != item.id) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
            EditorGUILayout.EndHorizontal();

            // 稀有度
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("稀有度:", GUILayout.Width(100));
            int rarityIndex = Array.IndexOf(rarityIds, item.rarityId);
            if (rarityIndex < 0) rarityIndex = 0;
            int newRarityIndex = EditorGUILayout.Popup(rarityIndex, rarityOptions);
            item.rarityId = rarityIds[newRarityIndex];
            EditorGUILayout.EndHorizontal();

            // 名称
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称:", GUILayout.Width(100));
            item.name = EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();

            // 孵化时间
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("孵化时间(秒):", GUILayout.Width(100));
            item.hatchTime = EditorGUILayout.IntField(item.hatchTime, GUILayout.Width(100));
            if (item.hatchTime < 0) item.hatchTime = 0;
            EditorGUILayout.LabelField($"= {FormatSeconds(item.hatchTime)}", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();

            // ✅ 跳过孵化金币
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("跳过孵化金币:", GUILayout.Width(100));
            item.skipCost = EditorGUILayout.IntField(item.skipCost, GUILayout.Width(100));
            if (item.skipCost < 0) item.skipCost = 0;
            EditorGUILayout.LabelField("(金币不足自动走广告)", GUILayout.Width(200));
            EditorGUILayout.EndHorizontal();

            // ✅ 提升稀有度金币
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("提升稀有度金币:", GUILayout.Width(100));
            item.upgradeRarityCost = EditorGUILayout.IntField(item.upgradeRarityCost, GUILayout.Width(100));
            if (item.upgradeRarityCost < 0) item.upgradeRarityCost = 0;
            EditorGUILayout.LabelField("(0=最高稀有度，按钮不显示)", GUILayout.Width(250));
            EditorGUILayout.EndHorizontal();

            // 描述
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(100));
            item.description = EditorGUILayout.TextField(item.description);
            EditorGUILayout.EndHorizontal();

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
            EditorGUILayout.LabelField("请从上方列表选择要编辑的项", EditorStyles.centeredGreyMiniLabel);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("快速新增", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        editId = EditorGUILayout.IntField(editId, GUILayout.Width(70));
        EditorGUILayout.LabelField("稀有度:", GUILayout.Width(50));
        int quickRarityIndex = Array.IndexOf(rarityIds, editRarityId);
        if (quickRarityIndex < 0) quickRarityIndex = 0;
        int newQuickRarityIndex = EditorGUILayout.Popup(quickRarityIndex, rarityOptions, GUILayout.Width(100));
        editRarityId = rarityIds[newQuickRarityIndex];
        EditorGUILayout.LabelField("名称:", GUILayout.Width(40));
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("孵化时间(秒):", GUILayout.Width(90));
        editHatchTime = EditorGUILayout.IntField(editHatchTime, GUILayout.Width(80));
        if (editHatchTime < 0) editHatchTime = 0;
        EditorGUILayout.LabelField("跳过金币:", GUILayout.Width(70));
        editSkipCost = EditorGUILayout.IntField(editSkipCost, GUILayout.Width(80));
        EditorGUILayout.LabelField("升品金币:", GUILayout.Width(70));
        editUpgradeRarityCost = EditorGUILayout.IntField(editUpgradeRarityCost, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(400));
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
            "提示：蛋的ID从 10201 开始，稀有度使用 201-206\n" +
            "· 孵化时间：单位秒\n" +
            "· 跳过孵化金币：金币不足自动走广告\n" +
            "· 提升稀有度金币：0=最高稀有度（如幻想蛋），按钮不显示",
            MessageType.Info);
    }

    /// <summary>将秒数格式化为易读字符串</summary>
    private string FormatSeconds(int totalSeconds)
    {
        if (totalSeconds < 60) return $"{totalSeconds}秒";
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        if (hours > 0) return $"{hours}时{minutes}分{seconds}秒";
        return $"{minutes}分{seconds}秒";
    }

    private string GetRarityName(int rarityId)
    {
        int idx = Array.IndexOf(rarityIds, rarityId);
        return idx >= 0 ? rarityOptions[idx] : $"未知({rarityId})";
    }

    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<EggListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.eggs ?? new List<EggData>();
                if (dataList.Count > 0) Z_Logger.Log($"[EggDataJsonEditor] 加载成功，共{dataList.Count}条数据");
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[EggDataJsonEditor] 加载失败: {e.Message}");
                dataList = new List<EggData>();
            }
        }
        else
        {
            Z_Logger.LogWarning($"[EggDataJsonEditor] 文件不存在: {FullPath}，创建空列表");
            dataList = new List<EggData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FullPath, JsonUtility.ToJson(new EggListWrapper { eggs = dataList }, true));
        AssetDatabase.Refresh();
        Z_Logger.Log($"[EggDataJsonEditor] 保存成功: {FullPath}");
    }

    private void AddNewItem()
    {
        int newId = START_ID;
        if (dataList.Count > 0)
        {
            int maxId = dataList.Max(e => e.id);
            newId = maxId + 1;
        }
        dataList.Add(new EggData
        {
            id = newId,
            rarityId = 201,
            name = "新蛋",
            description = "",
            hatchTime = 60,
            skipCost = 100,
            upgradeRarityCost = 200
        });
        selectedIndex = dataList.Count - 1;
        SaveData();
        LoadData();
    }

    private void AddQuickItem()
    {
        if (string.IsNullOrEmpty(editName))
        {
            EditorUtility.DisplayDialog("错误", "名称不能为空", "确定");
            return;
        }
        if (IsIdDuplicate(editId, -1))
        {
            EditorUtility.DisplayDialog("错误", $"ID {editId} 已存在", "确定");
            return;
        }

        dataList.Add(new EggData
        {
            id = editId,
            rarityId = editRarityId,
            name = editName,
            description = editDescription,
            hatchTime = editHatchTime,
            skipCost = editSkipCost,
            upgradeRarityCost = editUpgradeRarityCost
        });
        dataList = dataList.OrderBy(e => e.id).ToList();
        SaveData();
        LoadData();

        editId = dataList.Count > 0 ? dataList.Max(e => e.id) + 1 : START_ID;
        editName = "";
        editDescription = "";
        editHatchTime = 60;
        editSkipCost = 100;
        editUpgradeRarityCost = 200;

        EditorUtility.DisplayDialog("成功", "新增成功", "确定");
    }

    private bool IsIdDuplicate(int id, int excludeIndex)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            if (i != excludeIndex && dataList[i].id == id) return true;
        }
        return false;
    }
}
#endif
