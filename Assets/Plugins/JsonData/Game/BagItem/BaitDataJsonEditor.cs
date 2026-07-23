// ==================== BaitDataJsonEditor.cs ====================
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

// 直接使用全局命名空间中的BaitData定义

public class BaitDataJsonEditor : BaseDataEditor<BaitData>
{
    private int editId = 2001;
    private string editName = "";
    private string editDescription = "";
    private int editBaseWeight = 100;
    private int editUnlockScene = 101;

    // 表头宽度
    private float col1 = 50;   // ID
    private float col2 = 100;  // 名称
    private float col3 = 80;   // 权重
    private float col4 = 100;  // 解锁场景
    private float col5 = 300;  // 描述

    private const string RELATIVE_PATH = "Resources/JsonData/Game/BagItem/baits.json";

    public BaitDataJsonEditor() : base(RELATIVE_PATH) { }

    [MenuItem("Tools/游戏内容/2.物品内部数据(记得编辑通用数据)/2001_鱼饵")]
    public static void ShowWindow()
    {
        BaitDataJsonEditor window = GetWindow<BaitDataJsonEditor>("饵料数据编辑器");
        window.minSize = new Vector2(800, 600);
        window.Show();
    }

    private void OnEnable() => LoadData();

    private void Update() => HandleColumnResize();

    private void OnGUI()
    {
        DrawToolbar();
        DrawDataTable();
        DrawEditPanel();
        DrawBottomButtons();
        HandleMouseUp();
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
        EditorGUILayout.LabelField("饵料列表", EditorStyles.boldLabel);

        // ==================== 表头 ====================
        EditorGUILayout.BeginHorizontal("box");

        DrawResizableColumn("ID", ref col1, "col1");
        DrawResizableColumn("名称", ref col2, "col2");
        DrawResizableColumn("权重", ref col3, "col3");
        DrawResizableColumn("解锁场景", ref col4, "col4");
        DrawResizableColumn("描述", ref col5, "col5");

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
        BaitData item = dataList[index];

        // 奇偶行不同底色
        if (selectedIndex == index)
            GUI.backgroundColor = Color.cyan;
        else if (index % 2 == 0)
            GUI.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        else
            GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        EditorGUILayout.BeginHorizontal("box");

        // ID
        EditorGUILayout.LabelField(item.id.ToString(), GUILayout.Width(col1));
        // 名称
        EditorGUILayout.LabelField(item.name, GUILayout.Width(col2));
        // 权重
        EditorGUILayout.LabelField(item.baseWeight.ToString(), GUILayout.Width(col3));
        // 解锁场景
        EditorGUILayout.LabelField(item.unlockScene.ToString(), GUILayout.Width(col4));
        // 描述
        EditorGUILayout.LabelField(item.description, GUILayout.Width(col5));

        // 操作
        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("编辑", GUILayout.Width(50))) selectedIndex = index;

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("删除", GUILayout.Width(50)) && EditorUtility.DisplayDialog("确认删除", $"确定要删除鱼饵 [{item.id}] {item.name} 吗？", "删除", "取消"))
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
            BaitData item = dataList[selectedIndex];
            EditorGUILayout.LabelField($"正在编辑: [{item.id}] {item.name}");
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID:", GUILayout.Width(40));
            int newId = EditorGUILayout.IntField(item.id);
            if (newId != item.id && !IsIdDuplicate(newId, selectedIndex)) item.id = newId;
            else if (newId != item.id) EditorUtility.DisplayDialog("错误", $"ID {newId} 已存在", "确定");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称:", GUILayout.Width(40));
            item.name = EditorGUILayout.TextField(item.name);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("描述:", GUILayout.Width(40));
            item.description = EditorGUILayout.TextField(item.description);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("权重:", GUILayout.Width(40));
            item.baseWeight = EditorGUILayout.IntField(item.baseWeight);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("解锁场景:", GUILayout.Width(60));
            item.unlockScene = EditorGUILayout.IntField(item.unlockScene);
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
        EditorGUILayout.LabelField("ID:", GUILayout.Width(30));
        editId = EditorGUILayout.IntField(editId, GUILayout.Width(60));
        EditorGUILayout.LabelField("名称:", GUILayout.Width(30));
        editName = EditorGUILayout.TextField(editName, GUILayout.Width(100));
        EditorGUILayout.LabelField("权重:", GUILayout.Width(30));
        editBaseWeight = EditorGUILayout.IntField(editBaseWeight, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("解锁场景:", GUILayout.Width(60));
        editUnlockScene = EditorGUILayout.IntField(editUnlockScene, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("描述:", GUILayout.Width(30));
        editDescription = EditorGUILayout.TextField(editDescription, GUILayout.Width(300));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("新增", GUILayout.Width(100))) AddQuickItem();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：修改后记得点击\"保存修改\"或使用快速新增区域添加数据", MessageType.Info);
    }

    private void LoadData()
    {
        if (File.Exists(FullPath))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<BaitListWrapper>(File.ReadAllText(FullPath));
                dataList = wrapper?.baits != null ? new List<BaitData>(wrapper.baits) : new List<BaitData>();
                if (dataList.Count > 0) Debug.Log($"加载成功，共{dataList.Count}条数据");
            }
            catch (System.Exception e) { Debug.LogError($"加载失败: {e.Message}"); dataList = new List<BaitData>(); }
        }
        else
        {
            Debug.LogWarning($"文件不存在: {FullPath}，创建空列表");
            dataList = new List<BaitData>();
        }
        Repaint();
    }

    private void SaveData()
    {
        string directory = Path.Combine(Application.dataPath, "Resources/JsonData/Game");
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(FullPath, JsonUtility.ToJson(new BaitListWrapper { baits = dataList.ToArray() }, true));
        AssetDatabase.Refresh();
        Debug.Log("保存成功");
    }

    private void AddNewItem()
    {
        int newId = 2001;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            newId = maxId + 1;
        }
        dataList.Add(new BaitData { id = newId, name = "新鱼饵", description = "描述", baseWeight = 100, unlockScene = 101 });
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
        dataList.Add(new BaitData { id = editId, name = editName, description = editDescription, baseWeight = editBaseWeight, unlockScene = editUnlockScene });
        for (int i = 0; i < dataList.Count - 1; i++)
        {
            for (int j = i + 1; j < dataList.Count; j++)
            {
                if (dataList[i].id > dataList[j].id)
                {
                    BaitData temp = dataList[i];
                    dataList[i] = dataList[j];
                    dataList[j] = temp;
                }
            }
        }
        SaveData();
        LoadData();
        int nextId = 2001;
        if (dataList.Count > 0)
        {
            int maxId = 0;
            foreach (var item in dataList) if (item.id > maxId) maxId = item.id;
            nextId = maxId + 1;
        }
        editId = nextId;
        editName = "";
        editDescription = "";
        editBaseWeight = 100;
        editUnlockScene = 101;
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