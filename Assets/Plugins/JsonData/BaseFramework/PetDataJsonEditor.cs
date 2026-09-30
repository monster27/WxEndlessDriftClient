#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

public class PetDataJsonEditor : EditorWindow
{
    private const string RELATIVE_PATH = "Addressables/JsonData/BaseFramework/pets.json";
    private const int START_ID = 10101;

    private List<PetData> dataList = new List<PetData>();
    private Vector2 scrollPosition;
    private int selectedIndex = -1;

    private int editId = START_ID;
    private int editRarityId = 201;
    private string editName = "";
    private string editDescription = "";
    private int editMaxHunger = 100;   // ✅ 新增

    private string[] rarityOptions = new string[]
    {
        "普通(201)", "罕见(202)", "稀有(203)", "史诗(204)", "传说(205)", "幻想(206)"
    };
    private int[] rarityIds = { 201, 202, 203, 204, 205, 206 };

    private string FullPath => Path.Combine(Application.dataPath, RELATIVE_PATH);

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/10101_宠物", false, 1001)]
    public static void ShowWindow()
    {
        var window = GetWindow<PetDataJsonEditor>("宠物数据编辑器");
        window.minSize = new Vector2(800, 600);
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
        EditorGUILayout.LabelField("宠物列表", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        // 表头
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("ID", GUILayout.Width(70));
        EditorGUILayout.LabelField("稀有度", GUILayout.Width(80));
        EditorGUILayout.LabelField("名称", GUILayout.Width(120));
        EditorGUILayout.LabelField("饥饿度上限", GUILayout.Width(80));   // ✅ 新增
        EditorGUILayout.LabelField("描述", GUILayout.Width(250));
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
        PetData item = dataList[index];
        EditorGUILayout.BeginHorizontal();

        if (selectedIndex == index) GUI.backgroundColor = Color.cyan;

        EditorGUILayout.LabelField($"[{item.id}]", GUILayout.Width(70));
        EditorGUILayout.LabelField(GetRarityName(item.rarityId), GUILayout.Width(80));
        EditorGUILayout.LabelField(item.name, GUILayout.Width(120));
        EditorGUILayout.LabelField(item.maxHunger.ToString(), GUILayout.Width(80));   // ✅ 新增
        EditorGUILayout.LabelField(item.description, GUILayout.Width(250));

        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) &&
            EditorUtility.DisplayDialog("确认删除", $"确定要删除宠物 [{item.id}] {item.name} 吗？", "删除", "取消"))
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
            PetData item = dataList[selectedIndex];
            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}");
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(80));
            int newId = EditorGUILayout.IntField(item.id);
            if (newId != item.id && !IsIdDuplicate(newId, selectedIndex)) item.id = newId;
            else if (newId != item.id) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("稀有度:", GUILayout.Width(80));
            int rarityIndex = Array.IndexOf(rarityIds, item.rarityId);
            if (rarityIndex < 0) rarityIndex = 0;
            int newRarityIndex = EditorGUILayout.Popup(rarityIndex, rarityOptions);
            item.rarityId = rarityIds[newRarityIndex];
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称:", GUILayout.Width(80));
            item.name = EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(80));
            item.description = EditorGUILayout.TextField(item.description);
            EditorGUILayout.EndHorizontal();

            // ✅ 新增：饥饿度上限
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("饥饿度上限:", GUILayout.Width(80));
            item.maxHunger = EditorGUILayout.IntField(item.maxHunger, GUILayout.Width(80));
            EditorGUILayout.LabelField("(0=无法捕捉昆虫)", GUILayout.Width(150));
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
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(300));
        // ✅ 新增：饥饿度上限
        EditorGUILayout.LabelField("饥饿度上限:", GUILayout.Width(80));
        editMaxHunger = EditorGUILayout.IntField(editMaxHunger, GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：宠物的ID从 10101 开始，稀有度使用 201-206（与鱼类共用）\n" +
            "饥饿度上限：宠物出战时会消耗饥饿度，为 0 时无法捕捉昆虫", MessageType.Info);
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
                var wrapper = JsonUtility.FromJson<PetListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.pets ?? new List<PetData>();
                if (dataList.Count > 0) Z_Logger.Log($"[PetDataJsonEditor] 加载成功，共{dataList.Count}条数据");
            }
            catch (Exception e)
            {
                Z_Logger.LogError($"[PetDataJsonEditor] 加载失败: {e.Message}");
                dataList = new List<PetData>();
            }
        }
        else
        {
            Z_Logger.LogWarning($"[PetDataJsonEditor] 文件不存在: {FullPath}，创建空列表");
            dataList = new List<PetData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.GetDirectoryName(FullPath);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FullPath, JsonUtility.ToJson(new PetListWrapper { pets = dataList }, true));
        AssetDatabase.Refresh();
        Z_Logger.Log($"[PetDataJsonEditor] 保存成功: {FullPath}");
    }

    private void AddNewItem()
    {
        int newId = START_ID;
        if (dataList.Count > 0)
        {
            int maxId = dataList.Max(e => e.id);
            newId = maxId + 1;
        }
        dataList.Add(new PetData { id = newId, rarityId = 201, name = "新宠物", description = "", maxHunger = 100 });
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

        dataList.Add(new PetData
        {
            id = editId,
            rarityId = editRarityId,
            name = editName,
            description = editDescription,
            maxHunger = editMaxHunger   // ✅ 新增
        });
        dataList = dataList.OrderBy(e => e.id).ToList();
        SaveData();
        LoadData();

        editId = dataList.Count > 0 ? dataList.Max(e => e.id) + 1 : START_ID;
        editName = "";
        editDescription = "";
        editMaxHunger = 100;   // ✅ 新增

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
